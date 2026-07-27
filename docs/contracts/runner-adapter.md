# Runner Adapter — Contrato entre bridge e runners

> **Status:** Proposed

Define a interface conceitual entre o Coding Agent Bridge e os runners (OpenCode, Codex, Antigravity).

## Interface conceitual

```csharp
public interface IRunnerAdapter
{
    Task<RunnerHealth> CheckHealthAsync(CancellationToken ct);
    Task<SessionHandle> CreateSessionAsync(SessionRequest request, CancellationToken ct);
    Task SendPromptAsync(SessionHandle handle, PromptPayload payload, CancellationToken ct);
    IAsyncEnumerable<RunnerEvent> StreamEventsAsync(SessionHandle handle, CancellationToken ct);
    Task CancelAsync(SessionHandle handle, CancellationToken ct);
    Task<RunnerResult> GetResultAsync(SessionHandle handle, CancellationToken ct);
}
```

## Tipos

### `SessionRequest`

```csharp
public record SessionRequest(
    string RepositorySlug,
    string AccessMode,        // "ReadOnly" | "WorkspaceWrite"
    string BaseReference,
    string Profile,
    IReadOnlyDictionary<string, string> Metadata
);
```

### `SessionHandle`

```csharp
public record SessionHandle(
    string RunnerSessionId,
    string AgentId,
    string WorkspacePath
);
```

### `PromptPayload`

```csharp
public record PromptPayload(
    string Text,
    IReadOnlyDictionary<string, string> Attachments,
    int MaxTokens,
    TimeSpan Timeout
);
```

### `RunnerEvent`

```csharp
public record RunnerEvent(
    string Type,           // "started", "progress", "tool_call", "tool_result", "message", "done", "error"
    DateTime Timestamp,
    string? Content,
    IReadOnlyDictionary<string, string>? Metadata
);
```

### `RunnerResult`

```csharp
public record RunnerResult(
    bool Completed,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyList<ArtifactReference> Artifacts
);
```

### `RunnerHealth`

```csharp
public record RunnerHealth(
    string AgentId,
    bool Healthy,
    string? Reason,
    DateTime CheckedAt
);
```

## Invariantes

* Adapter é stateless do ponto de vista do bridge.
* Estado da sessão é mantido pelo runner.
* Bridge é responsável por timeout, cancelamento e idempotência.
* Adapter nunca persiste artefatos diretamente; bridge coleta e persiste.

## Transporte

* OpenCode Runner: HTTP + Server-Sent Events (SSE) ou streaming HTTP.
* Codex Runner: subprocesso com captura de stdout estruturado.
* Antigravity Runner: a definir (discovery).

## Eventos

Ver [`events.md`](events.md).

## Política

* Adapter não executa comandos fora da política.
* Bridge aplica policy engine antes de despachar.

## Erros

Ver [`errors.md`](errors.md).

## Segurança

* Adapter não recebe credenciais de escrita para o repositório.
* Sessão é criada em workspace isolado.
* Cancelamento é cooperativo.

## Observabilidade

* Bridge emite spans `runner.dispatch`, `runner.execute`, `runner.cancel`.
* Adapter emite eventos estruturados.

## Testes de contrato

* Adapter fake deve implementar `IRunnerAdapter`.
* Bridge deve ser testável com adapter fake.
* Cada adapter real deve ter teste de contrato contra servidor de teste.

## Referências relacionadas

* [`mcp-tools.md`](mcp-tools.md)
* [`events.md`](events.md)
* [`errors.md`](errors.md)
* [`../specs/005-opencode-runner/spec.md`](../specs/005-opencode-runner/spec.md)
* [`../specs/012-codex-runner/spec.md`](../specs/012-codex-runner/spec.md)
* [`../specs/013-antigravity-runner/spec.md`](../specs/013-antigravity-runner/spec.md)
* [ADR-0003](../adr/0003-agent-adapter-boundary.md)
