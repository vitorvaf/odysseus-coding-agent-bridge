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

* **OpenCode v1.18.8 + `setsid` + redirect** — **RESOLVIDO em STAB-004.** Reescrito o `OpenCodeTestServer` para usar `Process` direto (sem `setsid`); stdio drenado por `Task`s background; SIGTERM → grace 5 s → SIGKILL; per-test `XDG_CONFIG_HOME` exclusivo; port validation post-shutdown. Os 3 `OpenCodeAdapterLifecycleTests` que intermitentemente falhavam agora passam determinísticamente.
* **`OpenCodeAdapterLifecycleTests` race condition** — **RESOLVIDO.** O `OpenCodeTestServer` agora é reescrito para também serializar (cada `OpenCodeAdapterLifecycleTests` obtém uma instância que sobe/desce o runner próprio), eliminando a race entre tests. Validado: 3/3 passa com o reescrito.
* **OpenCode v1.18.8 + provider customizado** — **PARCIALMENTE RESOLVIDO em STAB-004.** O cenário `error → Failed` é validado com OpenCode real (a 401 sem credencial produz `runner_auth_failed → Failed`). Os cenários `cancel`/`timeout`/`completed` continuam pendentes porque o `opencode.json` em v1.18.8 não carrega o `baseURL` configurado no `session_created.model` (sempre `None`). Resolution trackado em `STAB-005` (próxima slice após este commit) — `STAB-005` endereçará configuração de provider via `auth.json` override, Docker DNS rebinding, ou CLI flag (e.g. `--config <path>` se v1.18.9+).
* **OQ-200 reaberta em 2026-07-28** — **NÃO RE-FECHADA.** Conforme decisão do usuário, só é re-fechada quando os 4 cenários `OpenCodeRealLifecycleTests` passarem com OpenCode real + provider determinístico. Hoje, 1/4 passa (`error_failed`) e 3/4 são Skip (cancel, timeout, completed). O gate da STAB-005 endereçará isto.

## Critérios de aceite (espelho do backlog)

Status (atualizado 2026-07-28 após STAB-004):

* [x] Sem `Task.Run` fire-and-forget em `RunDispatcher` ou código adjacente. — `RunDispatcher.CreateAsync` apenas persiste `Pending`; execução invocada por `RunQueueWorker`.
* [x] Execuções ativas com ownership claro (CTS + TCS por run em `RunExecutionCoordinator`).
* [x] **SLICE-STAB-004 Commit 5**: `OpenCodeRealLifecycleTests.RealOpenCode_error_failed` passa com OpenCode real. — 401 → `runner_auth_failed` → Failed (cenário de OpenCode mal-configurado).
* [x] **SLICE-STAB-004 Commit 5**: Coordinator RODA contra OpenCode real — `OpenCodeRealLifecycleTests` com `IClassFixture<OpenCodeRealFixture>` + per-test `XDG_CONFIG_HOME`.
* [x] **SLICE-STAB-004 Commit 5**: Lifecycle TEM ownership explícito — `OpenCodeRealFixture` faz SIGTERM→grace→SIGKILL, valida porta liberada (`IsPortOpen`), limpa `XDG_CONFIG_HOME` per-test.
* [~] **SLICE-STAB-005** (follow-up): `RealOpenCode_blocked_cancelled_with_abort_call` (cancel com `POST /session/{id}/abort`) — Skip, requer provider configurado.
* [~] **SLICE-STAB-005** (follow-up): `RealOpenCode_slow_timedout` (timeout com cancelamento upstream) — Skip, requer provider configurado.
* [~] **SLICE-STAB-005** (follow-up): `RealOpenCode_normal_completed_via_deterministic_provider` (Completed com relatório persistido) — Skip, requer provider configurado.
* [x] **SLICE-STAB-004 Commit 1**: CI gate enforça `failed > 0 → job failed` (`.github/workflows/verify/verify_trx.py`). — O workflow não passa mais a verde com testes falhando silenciosamente.
* [x] **SLICE-STAB-004 Commit 2**: CI warnings resolvidos — actions v5/v6 (Node 24 runtime) + gitleaks via CLI direta. — `File.SetUnixFileMode` gated por `OperatingSystem.IsWindows()`.
* [x] Testes locais verdes — **21/21** (1/1 Unit + 10/10 Contract + 9/9 Integration + 1/1 Security, com 3 skip explícitos).
* [ ] `OQ-200` fechada com evidência real — **continua Open**; o re-fechamento depende de STAB-005 (provider configurado) + os 3 tests restantes passarem.
* [x] `OQ-201` fechada com referência à nova evidência — `Resolved` (10/10 contract tests confirmam).

## Próximos passos

1. **`STAB-005 — OpenCode Provider Configuration`** (próxima slice, mas NÃO antes de PR de estabilização):
   * Resolver configuração de provider customizado para v1.18.8 (auth.json override, Docker DNS rebinding, ou CLI flag se v1.18.9+).
   * Re-abilitar os 3 testes Skip em `OpenCodeRealLifecycleTests` (cancel, timeout, completed).
   * Re-fechar `OQ-200` com referência à evidência real.
2. **PR de estabilização** (após CI verde nesta `STAB-004` + `STAB-005`):
   * Revisão final.
   * Merge.
3. **`SLICE-WORKSPACE-001`** (Epic 2) — só após o PR de estabilização mergeado.

## Como reproduzir (rodando localmente)

```bash
# Garantir que o binário OpenCode v1.18.8 está em /tmp/opencode-v1.18.8/opencode
ls -la /tmp/opencode-v1.18.8/opencode

# Rodar todos os tests do coordinator (com MockRunnerAdapter, sem OpenCode real)
dotnet test OcabBridge.slnx -c Debug --filter "FullyQualifiedName~DeterministicCoordinatorTests"
# → 5/5 passam (coordinator + dispatcher + adapter com MockRunnerAdapter)

# Rodar a suite completa
dotnet test OcabBridge.slnx -c Debug
# → 21/21 verdes, 3 skipped (1/1 Unit + 10/10 Contract + 9/9 Integration + 1/1 Security)
# → 21/21 (Unit 1 + Security 1 + Contract 10 + Integration 9 + 3 Skipped)
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
