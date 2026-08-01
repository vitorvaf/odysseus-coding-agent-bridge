using OcabBridge.Api.Domain;

namespace OcabBridge.Api.Application;

// Coordinates the lifecycle of a Run from enqueue to terminal state.
//
// Backed by ADR-0018 (Persistent Run Queue with Awaitable Execution
// Coordination). Responsibilities:
//   * EnqueueAsync(runId): idem-potently marks the Run as ready for the
//     worker to claim (the row is already inserted by RunDispatcher.CreateAsync
//     with status=Pending; this method is a no-op and exists for symmetry
//     with future enqueue paths that may not have persisted yet).
//   * WaitForTerminalStateAsync(runId, timeout, ct): blocks the caller
//     until the run reaches a terminal state (Completed, Failed,
//     Cancelled, TimedOut), or the timeout expires (returns TimedOut
//     as the caller's perspective of terminal — the worker continues
//     in the background until the real terminal state is persisted).
//   * CancelAsync(runId, reason): signals the in-flight execution's
//     CancellationTokenSource (which propagates to the adapter and to
//     the HttpClient) and persists the Cancelling transition. Idempotent
//     when the run is already in a terminal state.
//
// Implementations must NOT use fire-and-forget Task.Run; every active
// execution is owned by an entry in the in-memory registry keyed by
// runId, and the Postgres row remains the source of truth for status.
public interface IRunExecutionCoordinator
{
    Task EnqueueAsync(Guid runId, CancellationToken cancellationToken);

    Task<RunTerminalResult> WaitForTerminalStateAsync(
        Guid runId,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    Task<bool> CancelAsync(
        Guid runId,
        string reason,
        CancellationToken cancellationToken);

    // Returns true if the runId has an active execution currently
    // registered (useful for diagnostics and tests).
    bool IsActive(Guid runId);

    // Snapshot of currently active executions (used by the
    // OQ-200/OQ-201 verification script and by tests).
    IReadOnlyCollection<Guid> ActiveRunIds { get; }
}
