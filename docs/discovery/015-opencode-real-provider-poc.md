# Discovery 015 — OpenCode Real Provider POC (STAB-005A.1 + STAB-005A.2 + STAB-005A.3)

> **Status:** Completed — STAB-005A.1 provou a conectividade entre
> OpenCode v1.18.8 e o provider determinístico. STAB-005A.2 reabilitou o
> cenário `Completion` no Bridge com polling em `/event`. STAB-005A.3
> fechou o ciclo usando `GET /session/{id}/message` como autoridade
> terminal, com o SSE mantido como pump auxiliar de eventos. O teste
> `RealOpenCode_completion_returns_known_response` passa em ~13s.
>
> **Versão alvo:** OpenCode v1.18.8
>
> **Bloqueia:** OQ-200, merge da PR de estabilização, início do Epic 2
>
> **ADRs relacionados:** [ADR-0017](../adr/0017-pin-opencode-version.md),
> [ADR-0018](../adr/0018-persistent-run-queue.md)

## Objetivo

Comprovar experimentalmente o caminho:

```text
Bridge → OpenCode v1.18.8 real → provider determinístico vivo
      → request HTTP observada → resposta conhecida
```

E reabilitar o cenário `RealOpenCode_completion_returns_known_response`
no Bridge, com a ordem correta de subscribe-before-prompt.

## Arquivos usados (STAB-005A.1 + STAB-005A.2)

### POC standalone (STAB-005A.1)

| Arquivo | Função |
| --- | --- |
| `poc/fixtures/stab005a1-server/Program.cs` | Provider Kestrel in-process com `Stab005a1.ServerApp` (renomeado de `Program` para evitar conflito de namespace), endpoints `GET /health`, `GET /v1/models`, `POST /v1/chat/completions`, `POST /v1/responses` e catch-all 404. |
| `poc/fixtures/stab005a1-liveness/Program.cs` | Validador standalone do liveness gate. |
| `poc/fixtures/stab005a1-driver/Program.cs` | Driver completo que hospeda o provider, inicia o OpenCode via `ProcessStartInfo`, executa HTTP direto + CLI, e apresenta relatório. |

### Modificações no Bridge (STAB-005A.2)

| Arquivo | Modificação |
| --- | --- |
| `src/OcabBridge.Api/Adapters/IRunnerAdapter.cs` | Adicionado `OpenEventStreamAsync(sessionId, ct)` que retorna `RunnerEventStream` (handle que garante que a conexão SSE foi estabelecida antes do retorno). `RunnerEventStream` é `sealed` com construtor `public` para permitir que `MockRunnerAdapter` (em `OcabBridge.TestSupport`) construa handles stub. |
| `src/OcabBridge.Api/Adapters/OpenCodeAdapter.cs` | `OpenEventStreamAsync` abre `GET /event?directory=...` com `HttpCompletionOption.ResponseHeadersRead` e valida o status 200 antes de devolver o handle. `ReadSseEventsAsync` foi reescrito para usar `stream.ReadAsync` com timeout de inatividade de 30s, tratando `HttpIOException`/`IOException`/`OperationCanceledException` (inatividade) como fim do stream. O diretório do workspace vem de `OCAB_OPENCODE_WORKSPACE_DIR`. |
| `src/OcabBridge.Api/Application/RunDispatcher.cs` | `ExecuteAsync` agora abre `OpenEventStreamAsync` antes de `SendPromptAsync` (ordem subscribe-before-prompt). O loop de eventos filtra por `eventSessionId == currentSessionId` (local no dispatcher). Helpers `ExtractEventContext` e `TryExtractAssistantTerminal` parseiam o JSON e identificam o terminal: `info.role: "assistant"`, `info.finish: "stop"`, sem `info.error`. O `resultJson` é serializado com `{text, messageId, providerId, modelId}`. |

### Testes e fixture (STAB-005A.2)

| Arquivo | Modificação |
| --- | --- |
| `tests/OcabBridge.TestSupport/DeterministicOpenCodeProvider.cs` | `WriteSseNormalAsync` agora retorna `"OCAB_PROVIDER_OK"` em chunk único (em vez de `"hello world from deterministic provider"` em três chunks). |
| `tests/OcabBridge.TestSupport/MockRunnerAdapter.cs` | Adicionado `OpenEventStreamAsync` stub que retorna um `RunnerEventStream` envolvendo o mesmo `IAsyncEnumerable` do `StreamEventsAsync` (para o `DeterministicCoordinatorTests`). |
| `tests/OcabBridge.IntegrationTests/OpenCodeRealLifecycleTests.cs` | `OpenCodeRealFixture` agora inicia o `DeterministicOpenCodeProvider` em uma porta livre, escreve `opencode.jsonc` e `auth.json` no `XDG_CONFIG_HOME` isolado, define `OPENCODE_SERVER_PASSWORD` consistente com `Ocab:OpenCodePassword` e o `WorkingDirectory` no pilot-repo. `RealOpenCode_completion_returns_known_response` reabilitado com `Trait("Category", "RealOpenCodePoc")`, prompt `"Responda somente: OCAB_PROVIDER_OK"`, timeout de `WaitForTerminalStateAsync` de 180s. Os 3 outros testes (cancel/timeout/provider-error) permanecem skipped. |

### Fixture do repositório piloto

`poc/fixtures/pilot-repo/` — repositório Git local com README, `package.json`, `src/index.js` e `.gitignore`. HEAD inicial `4e5c71604f03003901e85c5c09b4530c60949aaa` no checkout principal e `d7ce1ad71f334161fee04b9395cdfdd9c2ee2bf4` no worktree da POC.

## Configuração investigada

A configuração efetivamente suportada pela release foi confirmada pela source v1.18.8 e pelo comando `opencode debug config`:

* `OPENCODE_CONFIG_CONTENT` ou arquivo `opencode.jsonc` em `XDG_CONFIG_HOME/opencode/`.
* `OPENCODE_AUTH_CONTENT` ou arquivo `auth.json` em `XDG_CONFIG_HOME/opencode/`.
* Provider V1 em `provider.<id>` com `npm: @ai-sdk/openai-compatible`, `api` field, `options.apiKey`, `models.<id>` com `limit.context` e `limit.output`.
* Modelo explícito `model: "deterministic/deterministic-model"` no `OPENCODE_CONFIG_CONTENT` ou no `opencode.jsonc`.
* O `api` field é o único caminho confiável para `baseURL` (leitura direta em `resolveSDK()`); o `options.baseURL` é silenciosamente ignorado para `@ai-sdk/openai-compatible` (bug #5674 upstream).
* CLI `--hostname`, `--port`, `--print-logs` e `--config <path>` (se suportado).

Config usada no driver e no teste:

```json
{
  "$schema": "https://opencode.ai/config.json",
  "model": "deterministic/deterministic-model",
  "provider": {
    "deterministic": {
      "npm": "@ai-sdk/openai-compatible",
      "name": "OCAB deterministic provider",
      "api": "http://127.0.0.1:<porta>/v1",
      "options": { "apiKey": "__SET_ME__" },
      "models": {
        "deterministic-model": {
          "name": "OCAB deterministic model",
          "limit": { "context": 32768, "output": 4096 }
        }
      }
    }
  }
}
```

## Rotas reais do OpenCode v1.18.8

| Rota | Verbo | Resultado no binário |
| --- | --- | --- |
| `/session` | POST | 200 (criação) |
| `/session/{id}/prompt_async` | POST | 204 No Content (fire-and-forget) |
| `/event` | GET | 200 (SSE) — requer `?directory=...` para o `WorkspaceRoutingMiddleware` |
| `/doc` | GET | 200 (OpenAPI JSON) |
| `/global/health` | GET | 404 (RootHttpApi não montado neste binário) |
| `/global/event` | GET | 404 (idem) |
| `/session/{id}/message` | GET | 200 (array de mensagens) |
| `/session/{id}/message/{messageID}` | GET | 200 (mensagem individual) |
| `/session/status` | GET | 200 (status global) |
| `/session/{id}/abort` | POST | abort |

## Contrato SSE observado (v1.18.8)

### Esquema de cada linha

```text
data: <json>\n\n
```

### Eventos capturados (driver STAB-005A.1)

| Tipo | `properties.sessionID` | `properties.info` |
| --- | --- | --- |
| `server.connected` | (ausente) | (ausente) |
| `session.created` | presente | `SessionInfo` |
| `session.updated` | presente | `SessionInfo` (com `model.variant`) |
| `message.updated` | presente | `MessageInfo` (user ou assistant) |
| `message.part.updated` | presente | `MessagePart` |
| `session.status` | presente | `{ "status": { "type": "busy" } }` |
| `session.diff` | presente | `{ "diff": [] }` |
| `plugin.added` | (ausente) | (ausente) |
| `catalog.updated` | (ausente) | (ausente) |
| `reference.updated` | (ausente) | (ausente) |
| `integration.updated` | (ausente) | (ausente) |
| `server.heartbeat` | (ausente) | (ausente) |

### Localização do `sessionID`

`event.properties.sessionID` para todos os eventos de sessão/mensagem. Para `session.created`, `event.properties.info.sessionID` contém o mesmo valor.

### Evento terminal real

**Não há um evento `done` ou `session.idle` no v1.18.8.** O terminal é identificado pela presença da mensagem do assistant com `info.role: "assistant"` e `info.finish: "stop"` (sem `info.error`) no evento `message.updated`. O text do assistant está em `event.properties.info.parts[].text` (onde `type: "text"`).

Ordem necessária: **abrir `GET /event` antes de `POST /session/{id}/prompt_async`**, para que o terminal event não seja perdido.

### Schema de `prompt_async`

```json
{
  "model": { "providerID": "deterministic", "modelID": "deterministic-model" },
  "parts": [{ "type": "text", "text": "..." }]
}
```

Nota: `model.id` é usado em `POST /session`, mas `model.modelID` (camelCase) é usado em `POST /session/{id}/prompt_async`. Schema v1.18.8 retorna 400 `BadRequest` se a chave estiver errada.

## Status de sucesso (provider)

Cenário `normal` do `DeterministicOpenCodeProvider`:

```json
{
  "id": "chatcmpl-det-001",
  "object": "chat.completion.chunk",
  "model": "deterministic-model",
  "choices": [
    {
      "index": 0,
      "delta": { "role": "assistant", "content": "OCAB_PROVIDER_OK" },
      "finish_reason": "stop"
    }
  ]
}
```

Enviado como SSE com `data:` prefix e terminado por `[DONE]`.

## Evidência do STAB-005A.1 (driver)

| Cenário | Resultado | providerRequests |
| --- | --- | --- |
| Liveness gate (GET /health + POST /v1/chat/completions) | ✅ 200 + `OCAB_PROVIDER_OK` | 1 |
| HTTP direto (POST /session + prompt_async) | ✅ 200 + 204; assistant respondeu `OCAB_PROVIDER_OK` | 4 |
| CLI (opencode run --attach) | ✅ exit 0; stdout com `step_start`, `text:"OCAB_PROVIDER_OK"`, `step_finish` | 6 |

**Status: Proven — OpenCode reached provider.**

## Evidência do STAB-005A.3 (teste `RealOpenCode_completion_returns_known_response`)

| Item | Resultado |
| --- | --- |
| `OpenCode real iniciou` | ✅ PID 3542, port 44765, `/doc` respondeu 200 |
| `provider recebeu request` | ✅ 1 request a `POST /v1/chat/completions` |
| `prompt contém OCAB_PROVIDER_OK` | ✅ (enviado ao OpenCode) |
| `SSE foi aberto antes do prompt` | ✅ `GET /event?directory=...` retornou 200 antes de `POST /prompt_async` |
| `WaitForTerminalResultAsync` polling | ✅ `GET /session/{id}/message` polled ~10x em ~1s |
| `evento terminal foi capturado` | ✅ `info.role: "assistant"`, `info.finish: "stop"`, `info.error` ausente |
| `texto == OCAB_PROVIDER_OK` | ✅ extraído de `parts[].text` (sibling de `info`) |
| `Run finaliza antes do timeout` | ✅ 13.0s total (timeout 30s) |
| `status observado == Completed` | ✅ `coordinator.WaitForTerminalStateAsync` retornou `Completed` |
| `runs.result contém OCAB_PROVIDER_OK` | ✅ `{"text":"OCAB_PROVIDER_OK","sessionId":"ses_...","providerId":"deterministic","modelId":"deterministic-model","messageId":"msg_..."}` |
| `coordinator retorna Completed` | ✅ `finalized status=Completed` |
| `duração < 30 s` | ✅ 13.0s |
| `pilot-repo intacto` | ✅ `git status --short` vazio, HEAD `d7ce1ad71f334161fee04b9395cdfdd9c2ee2bf4` |
| `nenhum processo residual` | ✅ (verificado) |
| `nenhuma porta residual` | ✅ (verificado) |

**Status: Proven — Bridge Run completed with `OCAB_PROVIDER_OK`.**

### Implementação STAB-005A.3 (Completion)

| Arquivo | Modificação |
| --- | --- |
| `src/OcabBridge.Api/Adapters/IRunnerAdapter.cs` | Adicionado `WaitForTerminalResultAsync(sessionId, pollInterval, ct)` e `RunnerTerminalResult(IsSuccess, Text, Error, RawJson)`. `OpenEventStreamAsync` + `RunnerEventStream` mantidos como pump auxiliar de eventos. |
| `src/OcabBridge.Api/Adapters/OpenCodeAdapter.cs` | `WaitForTerminalResultAsync` faz polling em `GET /session/{id}/message` (100-250ms). Terminal = última mensagem assistant com `info.finish: "stop"` e `info.error` ausente. Texto extraído de `parts[].text` (sibling de `info`). `OpenEventStreamAsync` abre `/event?directory=...` com `HttpCompletionOption.ResponseHeadersRead`. `ReadSseEventsAsync` usa `stream.ReadAsync` com timeout de inatividade de 30s para tratar o chunked stream do v1.18.8 que não envia chunk final. |
| `src/OcabBridge.Api/Application/RunDispatcher.cs` | `ExecuteAsync` agora: (1) abre SSE pump com CTS dedicado (`sseCts`); (2) envia prompt; (3) bloqueia em `WaitForTerminalResultAsync`; (4) no `finally`, cancela apenas o `sseCts`, aguarda o pump; (5) se `terminal.IsSuccess && !string.IsNullOrEmpty(terminal.Text)` finaliza como `Completed`; (6) caso contrário, finaliza como `Failed` (nunca finaliza como `Completed` por fim de stream). Helpers `PersistSseEventsAsync` (background pump) adicionado. |
| `tests/OcabBridge.IntegrationTests/OpenCodeRealLifecycleTests.cs` | Fixture instrumentada com `DeterministicOpenCodeProvider` (porta livre, `opencode.jsonc`, `auth.json`, `OPENCODE_SERVER_PASSWORD`). Teste `RealOpenCode_completion_returns_known_response` reabilitado com `Trait("Category", "RealOpenCodePoc")`, prompt `"Responda somente: OCAB_PROVIDER_OK"`, timeout de `WaitForTerminalStateAsync` de 30s. 3 outros testes (cancel/timeout/provider-error) permanecem skipped. |
| `tests/OcabBridge.TestSupport/MockRunnerAdapter.cs` | Adicionado `WaitForTerminalResultAsync` (para o `DeterministicCoordinatorTests`). Cenários: `normal`/`slow` → `IsSuccess=true, Text="hello from mock runner"`; `blocked` → cancela (propaga `OperationCanceledException`); `error` → `IsSuccess=false, Error="mock upstream_failure"`. |
| `tests/OcabBridge.TestSupport/DeterministicOpenCodeProvider.cs` | `WriteSseNormalAsync` agora retorna `"OCAB_PROVIDER_OK"` em chunk único (necessário para o teste Completion). Contadores e audit log removidos (instrumentação desnecessária). |

## Limitações restantes

1. **Bug #5674 (upstream) ainda não corrigido em v1.18.8.** `providerOptions` para `@ai-sdk/openai-compatible` usa a chave errada (`model.providerID.split(".")[0]` em vez de `"openaiCompatible"`), silenciosamente ignorando `options.baseURL` e `options.apiKey` per-call. O `api` field (lido como `model.api.url`) é o único caminho confiável para o `baseURL`. Não bloqueia a chamada HTTP, mas pode afetar outras opções per-call.

2. **SSE como autoridade terminal não é confiável no v1.18.8.** O OpenCode fecha o stream chunked sem enviar o chunk final de tamanho 0, e o `ReadSseEventsAsync` precisa de timeout de inatividade. A autoridade terminal vive em `GET /session/{id}/message`, não no SSE.

3. **Cancelamento real, timeout real e provider error ainda não implementados** (fora do escopo da STAB-005A.3). O `ExecuteAsync` tem os caminhos `catch (OperationCanceledException)` para `TimedOut` e `Cancelled`, mas não foram exercitados.

4. **A `OpenCodeRealFixture` precisa de `IWorkerId` em builds que incluam a STAB-004A** (lease/heartbeat), mas o worktree limpo baseado em `origin/phase-1-mvp` não tem `IWorkerId.cs`. O `RunQueueWorker` no worktree limpo não requer `IWorkerId` (foi adicionado pela STAB-004A local).

5. **`OpenEventStreamAsync` ainda usa `Environment.GetEnvironmentVariable("OCAB_OPENCODE_WORKSPACE_DIR")`** para construir `/event?directory=...`. O `RunDispatcher` é agnóstico ao provider; o `OpenCodeAdapter` é o único que lê essa env. Quando outros providers forem adicionados, o mecanismo de "workspace dir" deve ser abstraído.

## Comandos

### Liveness gate standalone

```bash
dotnet run --project poc/fixtures/stab005a1-liveness -- \
  poc/fixtures/stab005a1-server/bin/Debug/net8.0/stab005a1-server.dll
```

### Driver STAB-005A.1

```bash
dotnet run --project poc/fixtures/stab005a1-driver -- \
  /tmp/opencode-v1.18.8/opencode poc/fixtures/pilot-repo
```

### Teste Completion (STAB-005A.2)

```bash
dotnet test tests/OcabBridge.IntegrationTests/OcabBridge.IntegrationTests.csproj \
  -c Debug --no-restore \
  --filter "FullyQualifiedName~RealOpenCode_completion_returns_known_response"
```

## OQ-200: Resolved (PR #3 `6e21573f`)

A OQ-200 foi fechada pela PR #3 (`STAB-005A.3: Complete OpenCode bridge run via session messages`) mergeada em `phase-1-mvp`. O `Run` é finalizado como `Completed` com `runs.result == "OCAB_PROVIDER_OK"`. A autoridade terminal passou de `GET /event` (chunked stream incompleto em v1.18.8) para `GET /session/{id}/message` (polling 100-250ms, terminal = última mensagem assistant com `info.finish: "stop"` e `info.error` ausente), enquanto `GET /event` permanece como pump SSE auxiliar para `RunEvent`. Cenários `RealOpenCodePoc` `cancel`/`timeout`/`provider-error` permanecem como follow-up fora da STAB-005A.3 e o gate `Accepted` da Spec 005 ainda depende dos quatro cenários `RealOpenCodePoc` verdes.

## Spec 005: Proposed (parcialmente comprovada pela PR #3)

A Spec 005 permanece `Proposed`. O critério `OCR-AC-012` (provider customizado determinístico) passou para `Partially Implemented` pela PR #3 (`6e21573f`): completion real é provado pelo teste `RealOpenCode_completion_returns_known_response` com `runs.result == "OCAB_PROVIDER_OK"`, e a autoridade terminal é `GET /session/{id}/message` (não mais `GET /event`, que serve apenas como pump SSE auxiliar de `RunEvent`). Os cenários `RealOpenCodePoc` `cancel`/`timeout`/`provider-error` permanecem como follow-up e o gate `Accepted` da Spec 005 continua dependente dos quatro cenários `RealOpenCodePoc` verdes.

## Referências

* `docs/adr/0017-pin-opencode-version.md` — pin de versão `v1.18.8` e contrato HTTP.
* `docs/adr/0018-persistent-run-queue.md` — fila persistente e coordinator.
* `docs/discovery/014-deterministic-e2e-lifecycle.md` — STAB-003 (DeterministicCoordinatorTests).
* `docs/specs/005-opencode-runner/spec.md` — Spec do OpenCode Runner.
* `docs/specs/003-run-lifecycle/spec.md` — Spec do Run Lifecycle.
* `docs/planning/backlog.md` — `SLICE-STAB-005A` e `SLICE-STAB-005A.2`.
