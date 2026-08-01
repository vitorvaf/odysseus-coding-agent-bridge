using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OcabBridge.Api.Domain;
using OcabBridge.Api.Infrastructure.Persistence;

namespace OcabBridge.Api.Application;

// BackgroundService that consumes the persistent run queue (runs
// table with status='Pending') and dispatches each run via a scoped
// RunDispatcher.ExecuteAsync. Owns the in-memory registry of active
// executions (via RunExecutionCoordinator) and the CancellationTokenSource
// per run that drives timeout + cancel propagation.
//
// Implements IHostedService so it starts with the bridge and stops on
// graceful shutdown (drains active executions before exiting).
public sealed class RunQueueWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RunQueueWorker> _logger;
    private readonly RunExecutionCoordinator _coordinator;

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ReconcilerInterval = TimeSpan.FromMinutes(1);

    public RunQueueWorker(
        IServiceProvider services,
        ILogger<RunQueueWorker> logger,
        RunExecutionCoordinator coordinator)
    {
        _services = services;
        _logger = logger;
        _coordinator = coordinator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RunQueueWorker started (poll={PollMs}ms, reconciler={ReconcilerMin}min)",
            PollInterval.TotalMilliseconds, ReconcilerInterval.TotalMinutes);

        // Run an initial reconciliation pass on startup so any Runs
        // abandoned by a previous bridge instance are caught before we
        // start polling.
        await ReconcileStaleAsync(stoppingToken).ConfigureAwait(false);

        var lastReconcileAt = DateTimeOffset.UtcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // host is shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RunQueueWorker dispatch loop iteration failed");
            }

            if (DateTimeOffset.UtcNow - lastReconcileAt > ReconcilerInterval)
            {
                try
                {
                    await ReconcileStaleAsync(stoppingToken).ConfigureAwait(false);
                    lastReconcileAt = DateTimeOffset.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RunQueueWorker reconciler iteration failed");
                }
            }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { /* host shutdown */ }
        }

        _logger.LogInformation("RunQueueWorker stopping");
    }

    // Lists Pending runs and tries to claim one. Claim is atomic via
    // UPDATE … WHERE status='Pending' RETURNING run_id. The first
    // available runId wins; if multiple workers race, the loser gets
    // no row back and retries on the next tick.
    private async Task DispatchOnceAsync(CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var runRepo = scope.ServiceProvider.GetRequiredService<RunRepository>();
        var claimed = await runRepo.TryClaimNextPendingAsync(ct);
        if (claimed is null) return;

        _ = ExecuteClaimedAsync(claimed.Value, _coordinator);
    }

    // Runs the actual lifecycle for a claimed Run on a background
    // task. The task is owned by the host's stoppingToken: when the
    // host shuts down, the dispatcher's CTS propagates and the
    // execution finalizes with a Cancelled state.
    private async Task ExecuteClaimedAsync(Guid runId, RunExecutionCoordinator coordinator)
    {
        // Register the active execution so WaitForTerminalStateAsync can
        // wake up deterministically.
        var cts = new CancellationTokenSource();
        var exec = coordinator.RegisterActive(runId, cts);

        try
        {
            using var scope = _services.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<RunDispatcher>();
            await dispatcher.ExecuteAsync(runId, cts.Token).ConfigureAwait(false);

            // Read terminal state from DB.
            var runRepo = scope.ServiceProvider.GetRequiredService<RunRepository>();
            var run = await runRepo.GetAsync(runId, CancellationToken.None).ConfigureAwait(false);
            var terminal = new RunTerminalResult(
                run?.Status ?? Domain.RunStatus.Failed,
                run?.ResultJson);
            coordinator.Finalize(runId, terminal);
            _logger.LogInformation("Run {RunId} finalized status={Status}", runId, terminal.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run {RunId} execution threw", runId);
            // Make sure the run is marked terminal. Dispatcher.ExecuteAsync
            // already transitions to Failed on unobserved exceptions, but
            // we double-protect the registry entry so WaitForTerminalStateAsync
            // does not hang.
            coordinator.FinalizeAsFailed(runId, ex.GetType().Name);
        }
        finally
        {
            cts.Dispose();
        }
    }

    // Reconciliation: pick up Runs abandoned by a previous bridge
    // instance. Two passes:
    //   1. Runs stuck in 'Running' whose updated_at is older than
    //      timeout_seconds / 2: treat as abandoned and cancel them
    //      (the next ExecuteAsync observes the cancellation).
    //   2. Runs in 'Pending' whose created_at is older than 1 minute:
    //      the previous worker may have crashed before claim; let the
    //      normal poll pick them up on the next tick (no action here).
    private async Task ReconcileStaleAsync(CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var runRepo = scope.ServiceProvider.GetRequiredService<RunRepository>();
        var stale = await runRepo.ListAbandonedRunningAsync(ct);
        foreach (var (runId, timeoutSeconds) in stale)
        {
            _logger.LogWarning("Reconciler: abandoning Run {RunId} (timeout={TimeoutSec}s)", runId, timeoutSeconds);
            await runRepo.UpdateStatusAsync(runId, Domain.RunStatus.Cancelled, CancellationToken.None);
            // The worker that originally owned the run is gone; mark
            // the registry entry as Failed so a subsequent
            // WaitForTerminalStateAsync does not hang.
            _coordinator.FinalizeAsFailed(runId, "reconciled_after_restart");
        }
    }
}
