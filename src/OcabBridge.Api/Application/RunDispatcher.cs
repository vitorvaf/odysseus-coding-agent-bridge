using Microsoft.Extensions.Logging;
using OcabBridge.Api.Adapters;
using OcabBridge.Api.Domain;
using OcabBridge.Api.Infrastructure.Persistence;

namespace OcabBridge.Api.Application;

// RunDispatcher owns the end-to-end Run flow:
//   1. resolve repository by slug (404 if unknown)
//   2. select adapter based on the repository's allowedAgents
//   3. open a session against the runner
//   4. send the prompt
//   5. stream events until a terminal event arrives
//   6. transition the Run to Completed / Failed / Cancelled
//
// Slice 1.1.3 ships this orchestrator for the read-only path. The
// canceller / timeout / reconciliation machinery is deferred to
// later slices (Phase 3 + spec 003).
//
// Concurrency: ExecuteAsync is launched on a background task by
// CreateRunAsync. The bridge returns the runId immediately to the
// caller (MCP /mcp clients poll via run_get). Errors are recorded in
// run_events + the Run.status field, never re-thrown to the caller.

public sealed class RunDispatcher
{
    private readonly RunRepository _runs;
    private readonly RunEventRepository _events;
    private readonly RepositoryRepository _repos;
    private readonly IRunnerAdapter _adapter;
    private readonly ILogger<RunDispatcher> _logger;
    private readonly TimeProvider _clock;

    public RunDispatcher(
        RunRepository runs,
        RunEventRepository events,
        RepositoryRepository repos,
        IRunnerAdapter adapter,
        ILogger<RunDispatcher> logger,
        TimeProvider clock)
    {
        _runs = runs;
        _events = events;
        _repos = repos;
        _adapter = adapter;
        _logger = logger;
        _clock = clock;
    }

    public async Task<Guid> CreateAsync(
        string repositorySlug,
        string prompt,
        CancellationToken ct)
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
            ResultJson = null
        };
        await _runs.InsertAsync(run, ct);
        await TransitionAsync(run.RunId, from: null, RunStatus.Pending,
            "bridge", "run_created", null, ct);

        _ = Task.Run(() => ExecuteAsync(run.RunId, repo.ReadOnlyOnly, prompt, CancellationToken.None),
            CancellationToken.None);

        return run.RunId;
    }

    public Task<Run?> GetAsync(Guid runId, CancellationToken ct) =>
        _runs.GetAsync(runId, ct);

    public async Task<bool> CancelAsync(Guid runId, string reason, CancellationToken ct)
    {
        var run = await _runs.GetAsync(runId, ct);
        if (run is null) return false;
        if (RunStatus.IsTerminal(run.Status)) return true;
        await TransitionAsync(run.RunId, run.Status, RunStatus.Cancelling,
            "user", reason, null, ct);
        return true;
    }

    private async Task ExecuteAsync(Guid runId, bool readOnly, string prompt, CancellationToken ct)
    {
        try
        {
            var run = await _runs.GetAsync(runId, ct);
            if (run is null) return;

            var sessionReq = new StartSessionRequest(
                RepositorySlug: run.RepositorySlug ?? throw new InvalidOperationException("missing slug"),
                AccessMode: readOnly ? "ReadOnly" : "WorkspaceWrite");

            var session = await _adapter.StartSessionAsync(sessionReq, ct);
            await TransitionAsync(runId, RunStatus.Pending, RunStatus.Running,
                "adapter.opencode", "session_started", $"session={session.SessionId}", ct);

            await _adapter.SendPromptAsync(session.SessionId, prompt, ct);

            await foreach (var evt in _adapter.StreamEventsAsync(session.SessionId, ct))
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
                    CreatedAt = _clock.GetUtcNow()
                }, ct);

                if (string.Equals(evt.Type, "done", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                if (string.Equals(evt.Type, "error", StringComparison.OrdinalIgnoreCase))
                {
                    await TransitionAsync(runId, RunStatus.Running, RunStatus.Failed,
                        $"adapter.{_adapter.AgentId}", "runner_error", evt.Data, ct);
                    return;
                }
            }

            await TransitionAsync(runId, RunStatus.Running, RunStatus.Completed,
                $"adapter.{_adapter.AgentId}", "session_done", null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run {RunId} failed", runId);
            try
            {
                await TransitionAsync(runId, null, RunStatus.Failed,
                    "dispatcher", "exception", ex.GetType().Name, CancellationToken.None);
            }
            catch (Exception inner)
            {
                _logger.LogError(inner, "Run {RunId} failed to transition to Failed", runId);
            }
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
            CreatedAt = _clock.GetUtcNow()
        }, ct);
        await _runs.UpdateStatusAsync(runId, to, ct);
    }
}
