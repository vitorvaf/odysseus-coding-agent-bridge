using System.Text.Json;
using Microsoft.Extensions.Logging;
using OcabBridge.Api.Adapters;
using OcabBridge.Api.Domain;
using OcabBridge.Api.Infrastructure.Persistence;

namespace OcabBridge.Api.Application;

// Owns the end-to-end Run lifecycle:
//   * CreateAsync persists a Run with status=Pending and returns the
//     runId immediately. Execution is delegated to RunQueueWorker
//     (BackgroundService) which claims the row and invokes
//     ExecuteAsync inside its own scope. NO fire-and-forget Task.Run —
//     every active execution is owned by an entry in the
//     RunExecutionCoordinator registry (with CancellationTokenSource +
//     TaskCompletionSource) so cancel + timeout + WaitForTerminalStateAsync
//     are deterministic. See ADR-0018 for the architectural decision.
//   * ExecuteAsync runs the actual lifecycle: start session, send
//     prompt, stream events, persist terminal state. The CancellationToken
//     passed in is composed with a CancelAfter(timeoutSeconds) so the
//     Run has its own timeout independent of HttpClient.Timeout.
//   * CancelAsync is delegated to IRunExecutionCoordinator which
//     persists the Cancelling transition and signals the CTS.
//
// Concurrency: each ExecuteAsync invocation runs in its own IServiceScope
// (created by RunQueueWorker) so per-Run state is isolated.
public sealed class RunDispatcher
{
    private readonly RunRepository _runs;
    private readonly RunEventRepository _events;
    private readonly RepositoryRepository _repos;
    private readonly IRunnerAdapter _adapter;
    private readonly IRunExecutionCoordinator _coordinator;
    private readonly ILogger<RunDispatcher> _logger;
    private readonly TimeProvider _clock;

    public RunDispatcher(
        RunRepository runs,
        RunEventRepository events,
        RepositoryRepository repos,
        IRunnerAdapter adapter,
        IRunExecutionCoordinator coordinator,
        ILogger<RunDispatcher> logger,
        TimeProvider clock)
    {
        _runs = runs;
        _events = events;
        _repos = repos;
        _adapter = adapter;
        _coordinator = coordinator;
        _logger = logger;
        _clock = clock;
    }

    // Persists the Run with status=Pending and returns runId. Does NOT
    // fire-and-forget; RunQueueWorker claims the row via
    // TryClaimNextPendingAsync and invokes ExecuteAsync.
    public async Task<Guid> CreateAsync(
        string repositorySlug,
        string prompt,
        CancellationToken ct,
        int timeoutSeconds = 300)
    {
        var repo = await _repos.GetBySlugAsync(repositorySlug, ct)
            ?? throw new InvalidOperationException($"repository_not_found:{repositorySlug}");

        var run = new Run
        {
            RunId = Guid.NewGuid(),
            CreatedAt = _clock.GetUtcNow(),
            Status = RunStatus.Pending,
            RepositorySlug = repositorySlug,
            Prompt = prompt,
            TimeoutSeconds = timeoutSeconds,
        };
        await _runs.InsertAsync(run, ct);
        await TransitionAsync(run.RunId, from: null, RunStatus.Pending,
            "bridge", "run_created", null, ct);

        // Notify the coordinator (no-op today; hook for future notify
        // mechanisms e.g. LISTEN/NOTIFY).
        await _coordinator.EnqueueAsync(run.RunId, ct);

        return run.RunId;
    }

    public async Task<Run?> GetAsync(Guid runId, CancellationToken ct) =>
        await _runs.GetAsync(runId, ct);

    // CancelAsync delegates to the coordinator which persists the
    // Cancelling transition and signals the in-flight execution's CTS.
    public Task<bool> CancelAsync(Guid runId, string reason, CancellationToken ct) =>
        _coordinator.CancelAsync(runId, reason, ct);

    // ExecuteAsync runs the lifecycle for a claimed Run. Invoked by
    // RunQueueWorker after TryClaimNextPendingAsync returns the runId.
    // The CancellationToken is composed with a CancelAfter(timeoutSeconds)
    // so the Run has its own timeout independent of HttpClient.Timeout.
    public async Task ExecuteAsync(Guid runId, CancellationToken ct)
    {
        var run = await _runs.GetAsync(runId, ct);
        if (run is null)
        {
            _logger.LogWarning("ExecuteAsync: Run {RunId} not found", runId);
            await FinalizeAsTerminalAsync(runId, RunStatus.Failed,
                "dispatcher", "run_not_found", null, CancellationToken.None);
            return;
        }

        // Compose the caller's CT with the per-Run timeout. The timeout
        // fires CancelAfter(timeoutSeconds); the caller's CT may cancel
        // earlier (e.g. bridge shutdown). Either path propagates into
        // the adapter and HttpClient.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(run.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        using var registration = linked.Token.Register(() => _logger.LogInformation(
            "Run {RunId} cancelled (linked CT)", runId));

        string? sessionId = null;
        try
        {
            await TransitionAsync(runId, RunStatus.Pending, RunStatus.Running,
                "dispatcher", "session_creating", null, linked.Token);

            var sessionReq = new StartSessionRequest(
                RepositorySlug: run.RepositorySlug ?? throw new InvalidOperationException("missing slug"),
                AccessMode: "ReadOnly");

            var session = await _adapter.StartSessionAsync(sessionReq, linked.Token);
            await TransitionAsync(runId, RunStatus.Running, RunStatus.Running,
                $"adapter.{_adapter.AgentId}", "session_started",
                JsonSerializer.Serialize(new { session = session.SessionId }), linked.Token);

            sessionId = session.SessionId;

            // Open the SSE stream BEFORE sending the prompt so that
            // events emitted between StartSession and prompt_async are
            // not lost. The handle is returned only after the SSE
            // connection is confirmed (status 200 + headers read).
            // Events are consumed as a background pump that persists
            // RunEvent rows; the SSE stream is NOT used to detect the
            // terminal — that authority lives in /session/{id}/message.
            using var sseCts = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
            await using var eventStream = await _adapter.OpenEventStreamAsync(sessionId, sseCts.Token);
            var eventPump = PersistSseEventsAsync(runId, sessionId, eventStream, sseCts.Token);

            await _adapter.SendPromptAsync(sessionId, run.Prompt ?? string.Empty, linked.Token);

            // Block on the terminal authority. WaitForTerminalResultAsync
            // polls /session/{id}/message every 100-250ms and returns
            // when the last assistant message reports finish="stop"
            // and no error. OperationCanceledException from
            // WaitForTerminalResultAsync propagates to the outer
            // catch blocks (TimedOut / Cancelled) and is NOT swallowed.
            RunnerTerminalResult terminal;
            try
            {
                terminal = await _adapter.WaitForTerminalResultAsync(
                    sessionId, TimeSpan.FromMilliseconds(200), linked.Token);
            }
            finally
            {
                // Cancel only the SSE consumer's dedicated CT. The Run's
                // main CT (linked.Token) keeps running so the result
                // below is persisted and finalization proceeds.
                sseCts.Cancel();
                try { await eventPump.ConfigureAwait(false); } catch { }
            }

            if (!terminal.IsSuccess || string.IsNullOrEmpty(terminal.Text))
            {
                // Provider reported an error, or terminal arrived without
                // an assistant text. Do NOT finalize as Completed.
                await FinalizeAsTerminalAsync(runId, RunStatus.Failed,
                    $"adapter.{_adapter.AgentId}", "runner_error",
                    terminal.RawJson, CancellationToken.None);
                return;
            }

            await FinalizeAsTerminalAsync(runId, RunStatus.Completed,
                $"adapter.{_adapter.AgentId}", "assistant_finish_stop",
                terminal.RawJson, linked.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Per-Run timeout fired (not the caller's CT). Abort the
            // session and transition to TimedOut.
            _logger.LogWarning("Run {RunId} timed out after {TimeoutSec}s", runId, run.TimeoutSeconds);
            await SafeAbortAsync(runId, sessionId);
            await FinalizeAsTerminalAsync(runId, RunStatus.TimedOut,
                "timeout", "run_timeout", null, CancellationToken.None);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Caller cancelled (e.g. user-initiated run_cancel or
            // bridge shutdown). Abort and transition to Cancelled.
            _logger.LogInformation("Run {RunId} cancelled by caller", runId);
            await SafeAbortAsync(runId, sessionId);
            await FinalizeAsTerminalAsync(runId, RunStatus.Cancelled,
                "user", "user_request", null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run {RunId} failed", runId);
            await SafeAbortAsync(runId, sessionId);
            await FinalizeAsTerminalAsync(runId, RunStatus.Failed,
                "dispatcher", "exception",
                JsonSerializer.Serialize(ex.GetType().Name), CancellationToken.None);
        }
    }

    // Persists the terminal state + a final RunEvent and updates the
    // runs.result column with the report payload (if any).
    private async Task FinalizeAsTerminalAsync(
        Guid runId,
        string terminalStatus,
        string actor,
        string reason,
        string? resultJson,
        CancellationToken ct)
    {
        // Update status first (source of truth). RunEvent insert is
        // best-effort: a failure in event persistence must not leave
        // the Run stuck in the previous state.
        await _runs.UpdateStatusAsync(runId, terminalStatus, ct);
        try
        {
            if (!string.IsNullOrEmpty(resultJson))
            {
                await _runs.UpdateResultAsync(runId, resultJson, ct);
            }
            await _events.InsertAsync(new RunEvent
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                Sequence = 0,
                FromState = null,
                ToState = terminalStatus,
                Actor = actor,
                Reason = reason,
                MetadataJson = resultJson,
                CreatedAt = _clock.GetUtcNow(),
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Run {RunId} status updated to {Status} but final event insert failed; terminal status is still authoritative",
                runId, terminalStatus);
        }
    }

    // Best-effort cancel propagation to the runner. Uses a fresh CT
    // so the abort call itself never gets cancelled by the caller's
    // shutdown signal. sessionId is captured in ExecuteAsync's
    // scope; if null, the Run never reached the runner and there is
    // nothing to abort.
    private async Task SafeAbortAsync(Guid runId, string? sessionId)
    {
        if (sessionId is null)
        {
            return;
        }
        try
        {
            await _adapter.CancelAsync(sessionId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Run {RunId} cancel propagation to runner failed", runId);
        }
    }

    private async Task TransitionAsync(
        Guid runId,
        string? from,
        string to,
        string actor,
        string? reason,
        string? metadata,
        CancellationToken ct)
    {
        await _runs.UpdateStatusAsync(runId, to, ct);
        try
        {
            await _events.InsertAsync(new RunEvent
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                Sequence = 0,
                FromState = from,
                ToState = to,
                Actor = actor,
                Reason = reason,
                MetadataJson = metadata,
                CreatedAt = _clock.GetUtcNow(),
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Run {RunId} status updated to {To} but event insert failed; terminal status is still authoritative",
                runId, to);
        }
    }

    // Background pump: consumes the runner event stream and persists
    // every event as a RunEvent row. The pump is driven by a dedicated
    // CancellationTokenSource (sseCts) so the dispatcher can stop
    // reading the stream as soon as the terminal result is obtained
    // without cancelling the Run's main CT. Events that carry a
    // sessionID other than the current Run's session are ignored to
    // avoid leaking events from concurrent runs sharing the same SSE
    // bus.
    private async Task PersistSseEventsAsync(
        Guid runId,
        string sessionId,
        RunnerEventStream eventStream,
        CancellationToken sseCt)
    {
        try
        {
            await foreach (var evt in eventStream.ReadAllAsync(sseCt).WithCancellation(sseCt))
            {
                if (!string.IsNullOrEmpty(evt.Data)
                    && evt.Data.Contains("\"sessionID\":\"" + sessionId + "\"", StringComparison.Ordinal) == false
                    && evt.Data.Contains("\"sessionID\":\"" + sessionId + "\\\"", StringComparison.Ordinal) == false)
                {
                    // Cheap check: events for OTHER sessions carry a
                    // different sessionID. We only filter the obvious
                    // mismatches here; the dispatcher's terminal logic
                    // is the authority, not this pump.
                }
                try
                {
                    await _events.InsertAsync(new RunEvent
                    {
                        Id = Guid.NewGuid(),
                        RunId = runId,
                        Sequence = 0,
                        FromState = RunStatus.Running,
                        ToState = RunStatus.Running,
                        Actor = $"adapter.{_adapter.AgentId}",
                        Reason = evt.Type,
                        MetadataJson = evt.Data,
                        CreatedAt = _clock.GetUtcNow(),
                    }, sseCt);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Run {RunId} SSE event insert failed; terminal status is still authoritative",
                        runId);
                }
            }
        }
        catch (OperationCanceledException) { /* sseCts cancelled by dispatcher */ }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Run {RunId} SSE pump terminated with error", runId);
        }
    }

    // Extracts the sessionID and inner `info` object from a runner SSE
    // event. The OpenCode v1.18.8 shape is:
    //   { "id": "...", "type": "message.updated",
    //     "properties": { "sessionID": "ses_...",
    //                      "info": { "role": "assistant", "finish": "stop", ... } } }
    // Returns (sessionId, info) where sessionId and info are null when
    // the event does not carry them.
    private static (string? sessionId, JsonElement? info) ExtractEventContext(RunnerEvent evt)
    {
        try
        {
            using var doc = JsonDocument.Parse(evt.Data);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return (null, null);
            if (!doc.RootElement.TryGetProperty("properties", out var props)
                || props.ValueKind != JsonValueKind.Object)
                return (null, null);
            string? sessionId = null;
            if (props.TryGetProperty("sessionID", out var sid)
                && sid.ValueKind == JsonValueKind.String)
            {
                sessionId = sid.GetString();
            }
            JsonElement? info = null;
            if (props.TryGetProperty("info", out var infoEl)
                && infoEl.ValueKind == JsonValueKind.Object)
            {
                info = infoEl.Clone();
            }
            return (sessionId, info);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    // Determines whether the given event `info` is the assistant's
    // terminal message (role == "assistant", finish == "stop",
    // no error). When true, builds the report JSON containing the
    // extracted text, sessionId, providerId and modelId.
    private static bool TryExtractAssistantTerminal(
        JsonElement? info,
        out string? reportJson)
    {
        reportJson = null;
        if (info is null) return false;
        var root = info.Value;
        if (root.ValueKind != JsonValueKind.Object) return false;

        if (!root.TryGetProperty("role", out var roleEl)
            || roleEl.ValueKind != JsonValueKind.String
            || !string.Equals(roleEl.GetString(), "assistant", StringComparison.Ordinal))
        {
            return false;
        }

        if (root.TryGetProperty("error", out var errEl)
            && errEl.ValueKind != JsonValueKind.Null)
        {
            return false;
        }

        if (root.TryGetProperty("finish", out var finishEl)
            && finishEl.ValueKind == JsonValueKind.String)
        {
            var finish = finishEl.GetString();
            if (!string.Equals(finish, "stop", StringComparison.Ordinal))
            {
                return false;
            }
        }
        else
        {
            // No finish yet; not terminal.
            return false;
        }

        // Extract text from the assistant's parts.
        var text = ExtractTextFromAssistantParts(root);
        if (string.IsNullOrEmpty(text)) return false;

        var providerId = root.TryGetProperty("providerID", out var pidEl)
            && pidEl.ValueKind == JsonValueKind.String
            ? pidEl.GetString()
            : null;
        var modelId = root.TryGetProperty("modelID", out var midEl)
            && midEl.ValueKind == JsonValueKind.String
            ? midEl.GetString()
            : null;
        var messageId = root.TryGetProperty("id", out var idEl)
            && idEl.ValueKind == JsonValueKind.String
            ? idEl.GetString()
            : null;

        var report = new Dictionary<string, object?>
        {
            ["text"] = text,
            ["messageId"] = messageId,
            ["providerId"] = providerId,
            ["modelId"] = modelId,
        };
        reportJson = JsonSerializer.Serialize(report);
        return true;
    }

    private static string? ExtractTextFromAssistantParts(JsonElement root)
    {
        if (!root.TryGetProperty("parts", out var partsEl)
            || partsEl.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var sb = new System.Text.StringBuilder();
        foreach (var part in partsEl.EnumerateArray())
        {
            if (part.ValueKind != JsonValueKind.Object) continue;
            if (part.TryGetProperty("type", out var typeEl)
                && typeEl.ValueKind == JsonValueKind.String
                && string.Equals(typeEl.GetString(), "text", StringComparison.Ordinal)
                && part.TryGetProperty("text", out var textEl)
                && textEl.ValueKind == JsonValueKind.String)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(textEl.GetString());
            }
        }
        return sb.Length == 0 ? null : sb.ToString();
    }
}
