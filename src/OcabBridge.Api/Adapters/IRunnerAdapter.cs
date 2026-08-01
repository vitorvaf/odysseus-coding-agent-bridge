namespace OcabBridge.Api.Adapters;

// Abstraction between the bridge (control plane) and any runner
// implementation. Codex and Antigravity adapters will implement this
// interface in later phases (slice 4.1.1 and slice 5.1.1 respectively).
//
// Slice 1.1.3 ships OpenCodeAdapter only. The HTTP shape mirrors the
// OpenCode Server's documented surface — POST /sessions, /prompt,
// stream /events, POST /cancel — with file-change payloads kept as
// opaque JSON to avoid leaking runner-specific shapes upstream.

public interface IRunnerAdapter
{
    string AgentId { get; }

    Task<RunnerSession> StartSessionAsync(
        StartSessionRequest request,
        CancellationToken ct);

    Task SendPromptAsync(
        string sessionId,
        string prompt,
        CancellationToken ct);

    IAsyncEnumerable<RunnerEvent> StreamEventsAsync(
        string sessionId,
        CancellationToken ct);

    Task CancelAsync(string sessionId, CancellationToken ct);
}

public sealed record StartSessionRequest(
    string RepositorySlug,
    string AccessMode);

public sealed record RunnerSession(
    string SessionId,
    string Status,
    string? UpstreamVersion = null,
    string? ContractChecksum = null);

public sealed record RunnerEvent(
    string Type,
    string Data,
    DateTimeOffset Timestamp);
