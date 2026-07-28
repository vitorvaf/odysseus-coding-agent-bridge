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
            await _adapter.SendPromptAsync(sessionId, run.Prompt ?? string.Empty, linked.Token);

            // Collect the final report (last event data) for persistence
            // in runs.result. The runner report contract is opaque JSON;
            // the dispatcher forwards it as-is.
            string? finalReportJson = null;

            await foreach (var evt in _adapter.StreamEventsAsync(sessionId, linked.Token).WithCancellation(linked.Token))
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
                }, linked.Token);

                // Capture the last event payload as the report.
                finalReportJson = evt.Data;

                if (string.Equals(evt.Type, "done", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                if (string.Equals(evt.Type, "error", StringComparison.OrdinalIgnoreCase))
                {
                    await FinalizeAsTerminalAsync(runId, RunStatus.Failed,
                        $"adapter.{_adapter.AgentId}", "runner_error", finalReportJson, linked.Token);
                    return;
                }
            }

            await FinalizeAsTerminalAsync(runId, RunStatus.Completed,
                $"adapter.{_adapter.AgentId}", "session_done", finalReportJson, linked.Token);
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
}
