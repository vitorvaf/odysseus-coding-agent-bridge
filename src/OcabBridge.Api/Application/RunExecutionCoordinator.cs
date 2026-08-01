using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OcabBridge.Api.Domain;
using OcabBridge.Api.Infrastructure.Persistence;

namespace OcabBridge.Api.Application;

// Concrete IRunExecutionCoordinator. Owns:
//   * the in-memory registry of active executions (one entry per Run
//     that the worker has claimed but not yet finalized);
//   * a TaskCompletionSource per Run that resolves WaitForTerminalStateAsync
//     immediately when the Run reaches a terminal state (so tests and
//     MCP clients do not have to poll);
//   * a CancellationTokenSource per Run that propagates cancel + timeout
//     signals into the dispatcher and into the HttpClient.
//
// Postgres is the source of truth for status. The registry is a wake-up
// cache: if the entry is missing (e.g. bridge restarted), the polling
// fallback in WaitForTerminalStateAsync still produces the correct
// answer.
//
// Implements IDisposable to release any in-flight CTS at host shutdown.
public sealed class RunExecutionCoordinator : IRunExecutionCoordinator, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RunExecutionCoordinator> _logger;
    private readonly ConcurrentDictionary<Guid, ActiveExecution> _active = new();

    public RunExecutionCoordinator(
        IServiceProvider services,
        ILogger<RunExecutionCoordinator> logger)
    {
        _services = services;
        _logger = logger;
    }

    public IReadOnlyCollection<Guid> ActiveRunIds => _active.Keys.ToArray();

    public bool IsActive(Guid runId) => _active.ContainsKey(runId);

    public Task EnqueueAsync(Guid runId, CancellationToken cancellationToken)
    {
        // RunDispatcher.CreateAsync persists the Run with status=Pending
        // before returning; the worker polls the runs table and claims
        // the row via RunQueueWorker.TryClaimAsync. Nothing to do here.
        _logger.LogDebug("EnqueueAsync runId={RunId}", runId);
        return Task.CompletedTask;
    }

    public async Task<RunTerminalResult> WaitForTerminalStateAsync(
        Guid runId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        // Fast path: TCS already registered.
        if (_active.TryGetValue(runId, out var exec))
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(timeout);
            try
            {
                return await exec.Terminal.Task.WaitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Caller did not cancel — timeout fired. Report the
                // current DB state as the caller's perspective of
                // terminal. The worker continues in the background.
                var run = await ResolveRunFromScopeAsync(runId, cancellationToken).ConfigureAwait(false);
                return new RunTerminalResult(
                    run?.Status ?? "TimedOut",
                    run?.ResultJson);
            }
        }

        // Fallback path: registry miss (bridge restarted or caller is
        // on a different replica). Poll the DB.
        return await WaitForTerminalViaPollAsync(runId, timeout, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CancelAsync(
        Guid runId,
        string reason,
        CancellationToken cancellationToken)
    {
        // Persist the Cancelling transition first so a crash between
        // here and the actual cancel signal leaves the row in a state
        // the reconciler can resolve.
        await using var scope = _services.CreateAsyncScope();
        var runRepo = scope.ServiceProvider.GetRequiredService<RunRepository>();
        var runEventRepo = scope.ServiceProvider.GetRequiredService<RunEventRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var run = await runRepo.GetAsync(runId, cancellationToken);
        if (run is null)
        {
            _logger.LogWarning("CancelAsync: run {RunId} not found", runId);
            return false;
        }

        if (Domain.RunStatus.IsTerminal(run.Status))
        {
            _logger.LogDebug("CancelAsync: run {RunId} already in terminal state {Status}", runId, run.Status);
            return true;
        }

        await runRepo.UpdateStatusAsync(runId, Domain.RunStatus.Cancelling, cancellationToken);
        await runEventRepo.InsertAsync(new RunEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Sequence = 0,
            FromState = run.Status,
            ToState = Domain.RunStatus.Cancelling,
            Actor = "user",
            Reason = reason,
            MetadataJson = System.Text.Json.JsonSerializer.Serialize(reason),
            CreatedAt = clock.GetUtcNow(),
        }, cancellationToken);

        // Signal the in-flight execution, if any.
        if (_active.TryGetValue(runId, out var exec))
        {
            try
            {
                exec.Cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // already disposed by the worker; nothing to do
            }
        }
        return true;
    }

    // Called by RunQueueWorker once it claims the run, to register the
    // active execution in the in-memory registry. The TCS is resolved
    // when the worker finalizes the run (any terminal state).
    internal ActiveExecution RegisterActive(Guid runId, CancellationTokenSource cts)
    {
        var exec = new ActiveExecution(cts, new TaskCompletionSource<RunTerminalResult>());
        _active[runId] = exec;
        return exec;
    }

    // Called by RunQueueWorker when the run reaches a terminal state.
    // Removes the entry from the registry and resolves the TCS so any
    // pending WaitForTerminalStateAsync returns immediately.
    internal void Finalize(Guid runId, RunTerminalResult result)
    {
        if (_active.TryRemove(runId, out var exec))
        {
            exec.Terminal.TrySetResult(result);
            try { exec.Cts.Dispose(); } catch { /* ignore */ }
        }
    }

    // Called when the execution ends without reaching a terminal state
    // (e.g. the worker was cancelled). Resolves the TCS with the
    // caller's last-known status so WaitForTerminalStateAsync does not
    // hang forever.
    internal void FinalizeAsFailed(Guid runId, string reason)
    {
        if (_active.TryRemove(runId, out var exec))
        {
            exec.Terminal.TrySetResult(new RunTerminalResult("Failed", reason));
            try { exec.Cts.Dispose(); } catch { /* ignore */ }
        }
    }

    private async Task<RunTerminalResult> WaitForTerminalViaPollAsync(
        Guid runId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!linked.Token.IsCancellationRequested)
        {
            await using var scope = _services.CreateAsyncScope();
            var runRepo = scope.ServiceProvider.GetRequiredService<RunRepository>();
            var run = await runRepo.GetAsync(runId, linked.Token);
            if (run is { Status: Domain.RunStatus.Completed or Domain.RunStatus.Failed or Domain.RunStatus.Cancelled or Domain.RunStatus.TimedOut })
            {
                return new RunTerminalResult(run.Status, run.ResultJson);
            }
            try { await Task.Delay(200, linked.Token); }
            catch (OperationCanceledException) { break; }
        }
        // Timed out from caller's perspective. Reflect whatever the DB
        // says (the worker continues in the background).
        await using var finalScope = _services.CreateAsyncScope();
        var finalRepo = finalScope.ServiceProvider.GetRequiredService<RunRepository>();
        var finalRun = await finalRepo.GetAsync(runId, CancellationToken.None);
        return new RunTerminalResult(
            finalRun?.Status ?? Domain.RunStatus.TimedOut,
            finalRun?.ResultJson);
    }

    private async Task<Run?> ResolveRunFromScopeAsync(Guid runId, CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<RunRepository>();
        return await repo.GetAsync(runId, ct);
    }

    public void Dispose()
    {
        foreach (var (_, exec) in _active)
        {
            try { exec.Cts.Cancel(); } catch { /* ignore */ }
            try { exec.Cts.Dispose(); } catch { /* ignore */ }
        }
        _active.Clear();
    }

    // One active execution: a CTS (drives the HttpClient + adapter) and
    // a TCS that resolves when the run reaches terminal state.
    internal sealed record ActiveExecution(
        CancellationTokenSource Cts,
        TaskCompletionSource<RunTerminalResult> Terminal);
}
