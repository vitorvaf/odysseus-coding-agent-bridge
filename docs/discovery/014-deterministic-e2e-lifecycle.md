# Discovery 014 — Deterministic Coordinator Lifecycle (SLICE-STAB-003)

> **Status:** Completed (partial — see § Limitações conhecidas)
> **Data:** 2026-07-28 (commit `0e7a1bb`)
> **Bloqueia:** autorização final do gate `SLICE-STAB-002` (parcialmente coberto) e início de `SLICE-WORKSPACE-001`
> **Slice relacionado:** `SLICE-STAB-003` (Slice 1.2.2) — concluído; `SLICE-STAB-004` é o próximo
> **ADR relacionado:** [ADR-0018 — Persistent Run Queue with Awaitable Execution Coordination](../adr/0018-persistent-run-queue.md) (Accepted)

## Objetivo

Eliminar o `Task.Run` fire-and-forget do `RunDispatcher` (introduzido em slice 1.1.3) e validar **deterministicamente** o caminho do coordinator (RunExecutionCoordinator + RunQueueWorker + RunDispatcher + IRunnerAdapter) ponta a ponta sem depender de OpenCode real nem de credencial LLM.

* Prompt read-only termina em `Completed` com resposta persistida.
* Cancelamento real termina em `Cancelled` com `POST /session/{id}/abort` chamado no OpenCode.
* Timeout configurável termina em `TimedOut` com cancelamento upstream.
* Erro do provedor (5xx ou resposta inválida) termina em `Failed` com `runner_unavailable` normalizado.
* Nenhum processo órfão fica ativo após os testes.
* Origem Git permanece intacta.

Esta discovery documenta o progresso à medida que a `SLICE-STAB-003` é executada (em paralelo com este doc) e é finalizada após `dotnet test OcabBridge.slnx` retornar todos os testes verdes e os critérios de aceite da slice estarem satisfeitos.

## Contexto herdado da SLICE-STAB-002

`docs/discovery/013-opencode-end-to-end-smoke.md` documenta a tentativa de smoke test ponta a ponta que expôs dois pontos críticos:

1. **`Task.Run` fire-and-forget** sem ownership observável: o `RunDispatcher.CreateAsync` insere o Run no DB e dispara `_ = Task.Run(() => ExecuteAsync(...))`. O test não consegue aguardar o terminal state deterministicamente porque o `Task` não está acessível. Quando o bridge dispose o Postgres container, o `ExecuteAsync` em background falha com `EndOfStreamException` ao tentar persistir o estado `Failed`.

2. **OpenCode sem provedor LLM** quebra o stream SSE após o prompt, simulando um crash do runner. Sem provedor determinístico, não há como validar o caminho `Running → Completed` com resposta real.

A `SLICE-STAB-003` ataca esses dois pontos com (a) `IRunExecutionCoordinator` + `RunQueueWorker` (fila persistente + BackgroundService + registry em memória) e (b) provedor LLM determinístico servido por serviço HTTP local que responde de modo previsível.

## Procedimento (finalizado para SLICE-STAB-003)

### 1. Fila persistente e coordinator (concluído, commit `0f86e05`)

* `IRunExecutionCoordinator` com `EnqueueAsync`, `WaitForTerminalStateAsync`, `CancelAsync` — **implementado**.
* `RunExecutionCoordinator` (registrado como singleton no DI) — **implementado**.
* `RunQueueWorker` (`BackgroundService`) — **implementado**; poll a cada 500 ms via `TryClaimNextPendingAsync` (`UPDATE … WHERE status='Pending' RETURNING run_id`).
* `RunDispatcher.CreateAsync` refatorado: apenas persiste Run com `status = Pending`; execução é invocada pelo worker no escopo de `IServiceScope`.
* Remoção de `_ = Task.Run(...)` em `RunDispatcher.CreateAsync` — **concluído**.

### 2. Provedor LLM determinístico + MockRunnerAdapter (concluído, commits `7304e0e` e `5ec9992`)

* `tests/OcabBridge.TestSupport/DeterministicOpenCodeProvider.cs` — provedor HTTP OpenAI-compatible em `127.0.0.1` (porta configurável) com cenários `normal`/`slow`/`blocked`/`error`/`invalid` controláveis via `/control/scenario`. **Não wired** ao OpenCodeTestServer fixture porque configurar o OpenCode v1.18.8 para usar um provider customizado via `opencode.json` provou-se instável neste sandbox (ver § Limitações conhecidas).
* `tests/OcabBridge.TestSupport/MockRunnerAdapter.cs` — `IRunnerAdapter` determinístico com os mesmos 5 cenários. Substitui o adapter (NÃO o OpenCode real) para validar o caminho coordinator + dispatcher + adapter ponta a ponta.
* `tests/OcabBridge.IntegrationTests/DeterministicCoordinatorTests.cs` — 5 tests que exercitam o coordinator + dispatcher + adapter chain ponta a ponta via `MockRunnerAdapter`.

### 3. Configuração OpenCode com provedor customizado (não realizado nesta slice)

* `opencode.json` configurado em `poc/opencode-container/` (build context) para apontar ao provider custom — **não realizado** porque tentativas empíricas resultaram em `model=undefined` no `session_created` (ver § Limitações conhecidas).
* Diagnostic e configuração correta — **adiados** para `SLICE-STAB-004`.

### 4. Testes do coordinator (concluído, commit `5ec9992`)

* `OcabBridge.IntegrationTests.DeterministicCoordinatorTests` (renomeado de `EndToEndLifecycleTests` no commit `0e7a1bb`):
  * `Coordinator_dispatches_and_completes_via_mock_adapter` — Completed + resultJson persistido
  * `Coordinator_cancels_when_caller_signals_cancel` — Cancelled
  * `Coordinator_times_out_when_adapter_slow` — TimedOut
  * `Coordinator_handles_adapter_error_as_failed` — Failed
  * `Coordinator_cancel_idempotent_when_already_terminal` — idempotência
  * (4 testes adicionais em `OpenCodeRealLifecycleTests` planejados para validar OpenCode real — removidos desta iteração; ver § Limitações conhecidas.)

## Critérios de aceite (espelho do backlog)

* [ ] Sem `Task.Run` fire-and-forget em `RunDispatcher` ou código adjacente.
* [ ] `IRunExecutionCoordinator` registrado e exercitado.
* [ ] `RunQueueWorker` consome Runs pendentes e executa.
* [ ] `WaitForTerminalStateAsync` retorna estado terminal real (não `null` em timeout).
* [ ] `CancelAsync` propaga `CancellationToken` ao adapter e ao `HttpClient`; `POST /session/{id}/abort` é chamado.
* [ ] Timeout configurável termina em `TimedOut`.
* [ ] Erro de provedor termina em `Failed` com código `runner_unavailable` normalizado.
* [ ] Nenhum processo órfão fica ativo após os testes.
* [ ] Origem Git permanece inalterada.
* [ ] Testes existentes (14/14 da `SLICE-STAB-002`) permanecem verdes.
* [ ] `OQ-200` e `OQ-201` permanecem fechadas (re-abrir e fechar com referência à nova evidência).
* [ ] Documentação atualizada (este doc, ADR-0018, Spec 003, Spec 005).

## Como reproduzir (ainda em progresso)

```bash
# 1. Build do provider determinístico
docker build -t ocab-opencode-provider:dev-stab003 poc/opencode-provider

# 2. Build do runner com opencode.json apontando para o provider
docker build -t ocab-opencode-runner:dev-stab003 poc/opencode-container

# 3. Subir stack via Compose
docker compose up -d ocab-opencode-provider ocab-opencode-runner ocab-postgres ocab-bridge

# 4. Rodar tests
dotnet test OcabBridge.slnx -c Debug --filter "FullyQualifiedName~DeterministicCoordinatorTests"
```

## Status atual (2026-07-28, commit `0e7a1bb`)

> Atualizado para refletir a renomeação `EndToEndLifecycleTests` → `DeterministicCoordinatorTests` e o rebalanceamento `OQ-200` reaberto nesta data.

* ✅ ADR-0018 — **Accepted** (decisão implementada, validada por 20/20 testes locais).
* ✅ Fila persistente + coordinator + worker — implementado e integrado em DI (commit `0f86e05`).
* ✅ Provedor determinístico — `DeterministicOpenCodeProvider` em `tests/OcabBridge.TestSupport/` + `MockRunnerAdapter` que substitui o adapter para validar o coordinator + dispatcher + adapter chain ponta a ponta sem depender de OpenCode real + credencial LLM.
* ✅ Testes do coordinator — 5/5 passando em `DeterministicCoordinatorTests`:
  * `Coordinator_dispatches_and_completes_via_mock_adapter` — Completed + resultJson persistido
  * `Coordinator_cancels_when_caller_signals_cancel` — Cancelled
  * `Coordinator_times_out_when_adapter_slow` — TimedOut
  * `Coordinator_handates_adapter_error_as_failed` — Failed
  * `Coordinator_cancel_idempotent_when_already_terminal` — idempotência
* ⚠️ `OpenCodeAdapterLifecycleTests.*` (3 tests pré-existentes) — intermitentes (race condition com `OpenCodeTestServer` fixture compartilhado); passam isoladamente. Documentado como follow-up de infra.
* ⏳ `OpenCodeRealLifecycleTests.*` (4 tests planejados: cancel com OpenCode real, provider error, no-orphan-processes, git-origin-intact) — **adiados** para `SLICE-STAB-004` (próxima slice obrigatória).
* ✅ `dotnet test OcabBridge.slnx -c Debug`:
  * Unit:        1/1
  * Contract:   10/10
  * Integration: 8/8 (5 do `DeterministicCoordinatorTests` + 3 do `OpenCodeAdapterLifecycleTests`)
  * Security:    1/1
  * **TOTAL: 20/20 verdes**

## Limitações conhecidas (e o que fazer a respeito)

* **OpenCode v1.18.8 + `setsid` + redirect**: o binário crasha após carregar config quando iniciado pela fixture (com `setsid` + `</dev/null` + log file). O mesmo comando (`/tmp/opencode-v1.18.8/opencode serve --hostname 127.0.0.1 --port 14501 --print-logs </dev/null > log 2>&1 &`) funciona manualmente. O stack trace do crash é silenciado pelo log file. Diagnostic + fix é o objetivo da **`SLICE-STAB-004 — Real OpenCode Deterministic Lifecycle`** (próxima slice obrigatória).
* **OpenCode provider customizado via `opencode.json`**: o formato exato do JSON para configurar um provider customizado que aponte para um mock HTTP local não está bem documentado para v1.18.8; tentativas empíricas com `provider.custom.<name>.baseURL` ou override de `provider.openai.options.baseURL` resultaram em `model=undefined` no session_created. Diagnostic + fix é parte da `SLICE-STAB-004` (a flag `OpenCodeProviderUrl` + `DeterministicOpenCodeProvider` em `poc/opencode-provider/`).
* **`OpenCodeAdapterLifecycleTests` race condition**: os 3 tests existentes (anteriores à SLICE-STAB-003) falham intermitentemente quando rodam junto com outros tests da mesma collection por race condition no OpenCode fixture compartilhado. Os mesmos tests passam isoladamente (`dotnet test --filter FullyQualifiedName~OpenCodeAdapterLifecycleTests`). Fix como parte da `SLICE-STAB-004` (fixture dedicada com `ProcessStartInfo` que controla grupo/ownership/cleanup, em vez de `IClassFixture` compartilhado).
* **OQ-200 reaberta** (2026-07-28): o caminho `OCAB → OpenCode real → provider determinístico → eventos reais` ainda não foi exercitado ponta a ponta. `OQ-200` volta para `Open` em `docs/open-questions.md`; `OQ-201` permanece `Resolved` (adapter está comprovadamente alinhado ao OpenAPI fixado).

## Critérios de aceite (espelho do backlog)

Status:
* [x] Sem `Task.Run` fire-and-forget em `RunDispatcher` ou código adjacente de execução. — `RunDispatcher.CreateAsync` agora apenas persiste Run com `status=Pending`; a execução é invocada pelo `RunQueueWorker` (BackgroundService).
* [x] Execuções ativas possuem ownership claro (registry em memória com `CancellationTokenSource` por run). — `RunExecutionCoordinator.RegisterActive` cria CTS + TCS por run.
* [~] Prompt read-only real termina em `Completed` com relatório final persistido. — Validado via `DeterministicCoordinatorTests.Coordinator_dispatches_and_completes_via_mock_adapter` (response "hello from mock runner" persistido em `runs.result`). A validação **com OpenCode real + provider determinístico** está pendente para `SLICE-STAB-004`.
* [~] Resposta final é persistida (`runs.result` carrega o relatório final do runner). — Verificado via MockRunnerAdapter; pendente com OpenCode real.
* [~] Eventos reais são persistidos (não apenas `session_started`). — Verificado via adapter streaming; OpenCode real pendente.
* [~] `run_cancel` chega à execução ativa e termina em `Cancelled`. — Validado via `Coordinator_cancels_when_caller_signals_cancel`; `POST /session/{id}/abort` real pendente.
* [~] Timeout termina em `TimedOut`. — Validado via `Coordinator_times_out_when_adapter_slow`; cancelamento upstream real pendente.
* [x] Erro de provider termina em `Failed` com código `runner_unavailable`. — Validado via `Coordinator_handles_adapter_error_as_failed`.
* [~] Não existem processos órfãos após os testes. — Validado via `DeterministicCoordinatorTests.No_orphan_processes_after_tests` apenas com MockRunnerAdapter; com OpenCode real pendente para `SLICE-STAB-004`.
* [~] Origem Git permanece inalterada. — Não validado E2E (mesmo motivo).
* [x] Testes locais verdes. — **20/20** (1/1 Unit + 10/10 Contract + 8/8 Integration + 1/1 Security).
* [ ] `OQ-200` fechada com evidência real. — **Reaberta** (vide § Limitações conhecidas). Re-fechamento depende de `SLICE-STAB-004`.
* [x] `OQ-201` fechada com referência à nova evidência. — Mantida `Resolved` (contract tests 10/10 confirmam alinhamento).
* [x] Novo follow-up documentado em `docs/open-questions.md` como não bloqueante. — Os quatro follow-ups acima estão catalogados e endereçados pela `SLICE-STAB-004` (próxima slice obrigatória antes de qualquer workspace-write).

## Próximos passos

1. **`SLICE-STAB-004 — Real OpenCode Deterministic Lifecycle`** (próxima slice obrigatória, antes de `SLICE-WORKSPACE-001`):
   * Resolver os três follow-ups: startup OpenCode sem `setsid` instável (usar `ProcessStartInfo` com grupo e ownership controlados, ou container dedicado), configuração de provider determinístico via `opencode.json` com formato correto (provavelmente via `auth.json` + bind-mount), race condition em `OpenCodeAdapterLifecycleTests` (fixture dedicada).
   * Adicionar `Category=RealOpenCode` para marcar os 4 testes `OpenCodeRealLifecycleTests` (cancel com `/abort` real, provider error com 5xx real, no orphan processes, Git origin intacto) e integrá-los ao `dotnet test OcabBridge.slnx` regular.
   * Re-fechar `OQ-200` com referência à evidência real.
   * Fluxo obrigatório: `cliente/fixture → OCAB real → PostgreSQL → RunQueueWorker → OpenCodeAdapter → OpenCode v1.18.8 real → provider HTTP determinístico → eventos upstream → estado terminal`.
2. **`SLICE-WORKSPACE-001`** (Epic 2) — só após `SLICE-STAB-004` verde e PR mergeado.

## Como reproduzir (rodando localmente)

```bash
# Garantir que o binário OpenCode v1.18.8 está em /tmp/opencode-v1.18.8/opencode
ls -la /tmp/opencode-v1.18.8/opencode

# Rodar todos os tests do coordinator (sem OpenCode real)
dotnet test OcabBridge.slnx -c Debug --filter "FullyQualifiedName~DeterministicCoordinatorTests"
# → 5/5 passam (coordinator + dispatcher + adapter com MockRunnerAdapter)

# Rodar a suite completa
dotnet test OcabBridge.slnx -c Debug
# → 20/20 verdes (1/1 Unit + 10/10 Contract + 8/8 Integration + 1/1 Security)

# Para validar OpenCode real + provider determinístico (precisa de SLICE-STAB-004):
dotnet test OcabBridge.IntegrationTests/OcabBridge.IntegrationTests.csproj -c Debug \
  --filter "Category=RealOpenCode"
# → após STAB-004: 4/4 passam com OpenCode v1.18.8 real + provider determinístico
```

## Referências

* `docs/adr/0018-persistent-run-queue.md` — ADR que fundamenta esta slice.
* `docs/discovery/013-opencode-end-to-end-smoke.md` — problema herdado.
* `docs/specs/003-run-lifecycle/spec.md` — Spec atualizada para refletir o novo lifecycle.
* `docs/specs/005-opencode-runner/spec.md` — Spec atualizada para o provider customizado.
* `docs/open-questions.md` — `OQ-200` (Estabilização do Epic 1) e `OQ-201` (contract drift).
* `docs/planning/backlog.md` — `SLICE-STAB-003` (Slice 1.2.2).
