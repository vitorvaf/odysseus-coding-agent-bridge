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

    // Blocks until the runner reports a terminal state for the session
    // (assistant message with finish="stop" and no error, or an error
    // event). The implementation polls the runner's session-message
    // endpoint; SSE is consumed separately by the dispatcher as an event
    // pump. The returned RunnerTerminalResult is provider-agnostic.
    Task<RunnerTerminalResult> WaitForTerminalResultAsync(
        string sessionId,
        TimeSpan pollInterval,
        CancellationToken cancellationToken);

    // Opens the runner event stream (e.g. SSE on /event) and returns a
    // handle bound to a connection confirmed by the runner (headers
    // read, status 200). The dispatcher should call this BEFORE
    // SendPromptAsync to avoid losing early events; events are consumed
    // as a pump to persist RunEvent rows, not for terminal detection.
    Task<RunnerEventStream> OpenEventStreamAsync(
        string sessionId,
        CancellationToken ct);

    IAsyncEnumerable<RunnerEvent> StreamEventsAsync(
        string sessionId,
        CancellationToken ct);

    Task CancelAsync(string sessionId, CancellationToken ct);
}

// Provider-agnostic terminal result. The dispatcher treats IsSuccess as
// the sole evidence that the assistant produced a final answer.
public sealed record RunnerTerminalResult(
    bool IsSuccess,
    string? Text,
    string? Error,
    string RawJson);

// Handle returned by OpenEventStreamAsync. The connection is already
// established when this handle is returned; the dispatcher can call
// SendPromptAsync immediately after and then iterate ReadAllAsync.
public sealed class RunnerEventStream : IAsyncDisposable
{
    private readonly Func<CancellationToken, IAsyncEnumerable<RunnerEvent>> _reader;
    public string SessionId { get; }

    // Public so test mocks (in OcabBridge.TestSupport) can construct a
    // handle that wraps a stub IAsyncEnumerable without depending on
    // the OpenCodeAdapter internals. Production callers use
    // OpenCodeAdapter.OpenEventStreamAsync.
    public RunnerEventStream(
        string sessionId,
        Func<CancellationToken, IAsyncEnumerable<RunnerEvent>> reader)
    {
        SessionId = sessionId;
        _reader = reader;
    }

    public IAsyncEnumerable<RunnerEvent> ReadAllAsync(CancellationToken ct) => _reader(ct);

    public async ValueTask DisposeAsync()
    {
        // Best-effort cancellation hook; the reader enumerable closes
        // the underlying stream when the caller's enumerator is disposed.
        await Task.CompletedTask;
    }
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
