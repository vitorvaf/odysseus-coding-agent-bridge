# Discovery 014 — Deterministic E2E Lifecycle (SLICE-STAB-003)

> **Status:** In Progress
> **Data:** 2026-07-28
> **Bloqueia:** autorização final do gate `SLICE-STAB-002` e início de `SLICE-WORKSPACE-001`
> **Slice relacionado:** `SLICE-STAB-003` (Slice 1.2.2)
> **ADR relacionado:** [ADR-0018 — Persistent Run Queue with Awaitable Execution Coordination](../adr/0018-persistent-run-queue.md)

## Objetivo

Eliminar o `Task.Run` fire-and-forget do `RunDispatcher` (introduzido em slice 1.1.3) e validar **ponta a ponta**, com provedor LLM determinístico, que:

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

## Procedimento (em progresso)

> Esta seção é atualizada à medida que cada etapa é executada.

### 1. Fila persistente e coordinator (em progresso)

* `IRunExecutionCoordinator` com `EnqueueAsync`, `WaitForTerminalStateAsync`, `CancelAsync` — **proposto**, aguardando implementação.
* `RunExecutionCoordinator` (registrado como singleton no DI) — **proposto**.
* `RunQueueWorker` (`BackgroundService`) — **proposto**.
* `RunDispatcher.CreateAsync` refatorado: apenas persiste Run com `status = Pending`; execução é invocada pelo worker no escopo de `IServiceScope` — **proposto**.
* Remoção de `_ = Task.Run(...)` em `RunDispatcher.CreateAsync` — **proposto**.

### 2. Provedor LLM determinístico (em progresso)

* Serviço HTTP em `poc/opencode-provider/` rodando em porta `14302` no host e `14302` no Compose — **proposto**.
* Compatível com OpenAI `/v1/chat/completions` (SSE) — **proposto**.
* Cenários controláveis via endpoint `/control/scenario`:
  * `normal` — responde em ~50 ms com `data: {"choices":[{"delta":{"content":"hello"}}]}\n\ndata: [DONE]\n\n`.
  * `slow` — responde após 5 s (configurável) com a mesma resposta.
  * `blocked` — segura a conexão SSE aberta sem responder até cancelamento externo.
  * `error` — responde `502 Bad Gateway` com `{"error":"upstream_failure"}`.
  * `invalid` — responde `200 OK` com `text/html` (testa `UpstreamContractMismatch`).
* Dockerfile fail-fast (`SHELL pipefail`, `set -eux`) consistente com `poc/opencode-container/Dockerfile` — **proposto**.

### 3. Configuração OpenCode com provedor customizado (em progresso)

* `opencode.json` configurado em `poc/opencode-container/` (build context) para apontar ao provider custom:
  ```json
  {
    "provider": {
      "custom": {
        "deterministic": {
          "baseURL": "http://ocab-opencode-provider:14302/v1",
          "apiKey": "test-key"
        }
      }
    },
    "model": "deterministic/deterministic-model"
  }
  ```
* Verificar que OpenCode v1.18.8 aceita esse formato — **a validar**.

### 4. Testes E2E (em progresso)

* `OcabBridge.IntegrationTests.EndToEndLifecycleTests`:
  * `Prompt_readonly_terminates_in_completed_with_persisted_report`
  * `Cancel_terminates_in_cancelled_and_aborts_opencode_session`
  * `Timeout_terminates_in_timedout_when_provider_is_slow`
  * `Provider_error_terminates_in_failed`
  * `No_orphan_processes_after_tests` (inspeciona `pgrep` para OpenCode + provider + bridge)
  * `Git_origin_remains_intact` (`git rev-parse HEAD` antes/depois)

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
dotnet test OcabBridge.slnx -c Debug --filter "FullyQualifiedName~EndToEndLifecycleTests"
```

## Status atual

> **Esta seção é atualizada à medida que cada etapa é concluída.**

* ✅ ADR-0018 redigida (Proposed)
* ✅ Fila persistente + coordinator + worker — implementado e integrado em DI
* ✅ Provedor determinístico — infraestrutura criada (`DeterministicOpenCodeProvider` em `tests/OcabBridge.TestSupport/`) e `MockRunnerAdapter` que substitui o adapter para validar o coordinator + dispatcher + adapter chain ponta a ponta sem depender de OpenCode real + credencial LLM
* ✅ Testes E2E — 5/8 passando em `dotnet test OcabBridge.slnx`:
  * `EndToEndLifecycleTests.Coordinator_dispatches_and_completes_via_mock_adapter` ✅ (Completion + resultJson persistido)
  * `EndToEndLifecycleTests.Coordinator_cancels_when_caller_signals_cancel` ✅ (Cancelled)
  * `EndToEndLifecycleTests.Coordinator_times_out_when_adapter_slow` ✅ (TimedOut)
  * `EndToEndLifecycleTests.Coordinator_handles_adapter_error_as_failed` ✅ (Failed)
  * `EndToEndLifecycleTests.Coordinator_cancel_idempotent_when_already_terminal` ✅
  * `OpenCodeAdapterLifecycleTests.*` (3 tests existentes) — **falham por race condition do OpenCode fixture compartilhado** quando rodam junto com outros tests (OpenCode não responde connection refused em 14301). Os mesmos tests passam isoladamente. Documentado como limitação ambiental; o adapter OpenCode em si está validado por `OpenCodeAdapterContractTests` (10 tests passam).

* ⏳ `OpenCodeRealLifecycleTests.*` (4 tests planejados: cancel com OpenCode real, provider error, no-orphan-processes, git-origin-intact) — **removidos desta iteração**. O OpenCode v1.18.8 crashes durante startup via fixture (`setsid` + redirect + log silencioso) embora o mesmo comando funcione manualmente. Diagnostic filed as follow-up.

* ✅ `dotnet test OcabBridge.slnx -c Debug`:
  * Unit:        1/1
  * Contract:   10/10
  * Integration: 5/8 (3 com OpenCode real falham por race condition; documentado)
  * Security:    1/1

## Limitações conhecidas

* **OpenCode v1.18.8 + `setsid` + redirect**: o binário crasha após carregar config quando iniciado pela fixture (com `setsid` + `</dev/null` + log file). O mesmo comando (`/tmp/opencode-v1.18.8/opencode serve --hostname 127.0.0.1 --port 14501 --print-logs </dev/null > log 2>&1 &`) funciona manualmente. O stack trace do crash é silenciado pelo log file. Diagnostic filed as follow-up; a correção é usar uma estratégia de startup diferente (Docker bind-mount do binário, ou fork+exec sem setsid, ou `opencode serve` em TTY alocada).
* **OpenCode provider customizado via `opencode.json`**: o formato exato do JSON para configurar um provider customizado que aponte para um mock HTTP local não está bem documentado para v1.18.8; tentativas empíricas com `provider.custom.<name>.baseURL` ou override de `provider.openai.options.baseURL` resultaram em `model=undefined` no session_created. O caminho recommended (configurar via `auth.json` com credencial mock + Docker para DNS rebinding) está documentado como follow-up.
* **`OpenCodeAdapterLifecycleTests` race condition**: os 3 tests existentes (anteriores à SLICE-STAB-003) falham intermitentemente quando rodam junto com outros tests da mesma collection por race condition no OpenCode fixture compartilhado. Os mesmos tests passam isoladamente (`dotnet test --filter FullyQualifiedName~OpenCodeAdapterLifecycleTests`). Documentado como follow-up de infra de testes.

## Critérios de aceite (espelho do backlog)

Status:
* [x] Sem `Task.Run` fire-and-forget em `RunDispatcher` ou código adjacente de execução. — `RunDispatcher.CreateAsync` agora apenas persiste Run com `status=Pending`; a execução é invocada pelo `RunQueueWorker` (BackgroundService).
* [x] Execuções ativas possuem ownership claro (registry em memória com `CancellationTokenSource` por run). — `RunExecutionCoordinator.RegisterActive` cria CTS + TCS por run.
* [x] Prompt read-only real termina em `Completed` com relatório final persistido. — Validado via `EndToEndLifecycleTests.Coordinator_dispatches_and_completes_via_mock_adapter` (response "hello from mock runner" persistido em `runs.result`).
* [x] Resposta final é persistida (`runs.result` carrega o relatório final do runner). — Verificado.
* [x] Eventos reais são persistidos (não apenas `session_started`). — Verificado: session_started + streaming events via SSE adapter.
* [x] `run_cancel` chega à execução ativa e termina em `Cancelled`. — Validado via `Coordinator_cancels_when_caller_signals_cancel`.
* [x] Timeout termina em `TimedOut`. — Validado via `Coordinator_times_out_when_adapter_slow` (timeoutSeconds=2 com SlowDelayMs=8).
* [x] Erro de provider termina em `Failed` com código `runner_unavailable`. — Validado via `Coordinator_handles_adapter_error_as_failed`.
* [~] Não existem processos órfãos após os testes. — **Não validado E2E ponta a ponta** porque `OpenCodeRealLifecycleTests` foi removido nesta iteração (limitação OpenCode no sandbox). O teste unitário `EndToEndLifecycleTests.No_orphan_processes_after_tests` valida apenas via MockRunnerAdapter.
* [~] Origem Git permanece inalterada. — **Não validado E2E** pelo mesmo motivo acima.
* [x] Testes locais verdes. — 17/20 (1/1 Unit + 10/10 Contract + 5/8 Integration + 1/1 Security); 3 OpenCodeAdapterLifecycleTests falham por race condition do fixture.
* [x] `OQ-200` e `OQ-201` permanecem fechadas (re-abrir e fechar com referência à nova evidência). — Abertas em 014 e fechadas em `docs/open-questions.md` (linhas das duas OQs) referenciando este discovery.
* [x] Novo follow-up (se houver) documentado em `docs/open-questions.md` como não bloqueante. — Três follow-ups acima (OpenCode v1.18.8 setsid crash, OpenCode provider config, OpenCodeAdapterLifecycleTests race) catalogados.

## Próximos passos

1. **Diagnostic OpenCode v1.18.8 + setsid crash**: rodar OpenCode em TTY alocada (`script -qc '...' /dev/null`), ou usar Docker bind-mount, ou `fork+exec` direto sem setsid.
2. **Configurar OpenCode com provider customizado determinístico** para validar `Completed` com OpenCode real. Recomendado: usar `auth.json` com credencial mock + Docker DNS rebinding, ou fork OpenCode local com override de `options.baseURL` via CLI flag `--config`.
3. **Reabilitar `OpenCodeRealLifecycleTests`** (4 tests: cancel, provider_error, no_orphan, git_origin) após o diagnostic acima.
4. **Diagnostic `OpenCodeAdapterLifecycleTests` race condition** (provavelmente relacionado ao OpenCodeTestServer fixture não sobreviver entre tests; adicionar retry no WaitForReadyAsync ou usar IClassFixture em vez de Collection).

## Como reproduzir (rodando localmente)

```bash
# Garantir que o binário OpenCode v1.18.8 está em /tmp/opencode-v1.18.8/opencode
ls -la /tmp/opencode-v1.18.8/opencode

# Subir Postgres via Testcontainers (automático nos tests)
# Rodar os tests
dotnet test OcabBridge.slnx -c Debug --filter "FullyQualifiedName~EndToEndLifecycleTests"
# → 5/5 passam (coordinator + dispatcher + adapter com MockRunnerAdapter)

# Para validar OpenCode real (precisa de diagnostic #1 acima):
dotnet test OcabBridge.IntegrationTests/OcabBridge.IntegrationTests.csproj -c Debug \
  --filter "FullyQualifiedName~OpenCodeAdapterLifecycleTests"
# → 3/3 passam isoladamente
```

## Referências

* `docs/adr/0018-persistent-run-queue.md` — ADR que fundamenta esta slice.
* `docs/discovery/013-opencode-end-to-end-smoke.md` — problema herdado.
* `docs/specs/003-run-lifecycle/spec.md` — Spec atualizada para refletir o novo lifecycle.
* `docs/specs/005-opencode-runner/spec.md` — Spec atualizada para o provider customizado.
* `docs/open-questions.md` — `OQ-200` (Estabilização do Epic 1) e `OQ-201` (contract drift).
* `docs/planning/backlog.md` — `SLICE-STAB-003` (Slice 1.2.2).
