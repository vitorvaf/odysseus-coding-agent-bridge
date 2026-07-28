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

* ⏳ ADR-0018 redigida (Proposed)
* ⏳ Fila persistente + coordinator + worker — em design
* ⏳ Provedor determinístico — em design
* ⏳ Testes E2E — em design
* ⏳ `dotnet test OcabBridge.slnx` — pendente
* ⏳ Smoke test ponta a ponta com bridge real — pendente

## Referências

* `docs/adr/0018-persistent-run-queue.md` — ADR que fundamenta esta slice.
* `docs/discovery/013-opencode-end-to-end-smoke.md` — problema herdado.
* `docs/specs/003-run-lifecycle/spec.md` — Spec atualizada para refletir o novo lifecycle.
* `docs/specs/005-opencode-runner/spec.md` — Spec atualizada para o provider customizado.
* `docs/open-questions.md` — `OQ-200` (Estabilização do Epic 1) e `OQ-201` (contract drift).
* `docs/planning/backlog.md` — `SLICE-STAB-003` (Slice 1.2.2).
