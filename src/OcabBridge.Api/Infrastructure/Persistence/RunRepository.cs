using System.Diagnostics;
using Dapper;
using Npgsql;
using OcabBridge.Api.Domain;

namespace OcabBridge.Api.Infrastructure.Persistence;

// Minimal repository for slice 1.1.1 (Foundation). Only InsertAsync and
// GetAsync are required by the backlog acceptance criteria. State
// transitions, cancellation, timeout, and listing are deferred to
// later slices.
//
// SLICE-STAB-003 (ADR-0018) extends the repository with methods used by
// RunQueueWorker: TryClaimNextPendingAsync (atomic claim of a Pending
// row), ListAbandonedRunningAsync (reconciler), and an UpdateResultAsync
// helper for the dispatcher to persist the final report.
public sealed class RunRepository
{
    private readonly NpgsqlConnectionFactory _factory;

    public RunRepository(NpgsqlConnectionFactory factory) => _factory = factory;

    public async Task InsertAsync(Run run, CancellationToken ct)
    {
        const string sql = @"
INSERT INTO runs (
    run_id, created_at, status, repository_slug, prompt, result, timeout_seconds, idempotency_key
) VALUES (
    @RunId, @CreatedAt, @Status, @RepositorySlug, @Prompt, @ResultJson::jsonb,
    @TimeoutSeconds, @IdempotencyKey
)";

        await using var conn = _factory.Create();
        await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                run.RunId,
                run.CreatedAt,
                run.Status,
                run.RepositorySlug,
                run.Prompt,
                run.ResultJson,
                run.TimeoutSeconds,
                run.IdempotencyKey,
            },
            cancellationToken: ct));
    }

    public async Task<Run?> GetAsync(Guid runId, CancellationToken ct)
    {
        const string sql = @"
SELECT run_id              AS RunId,
       created_at          AS CreatedAt,
       started_at          AS StartedAt,
       finished_at         AS FinishedAt,
       status              AS Status,
       repository_slug      AS RepositorySlug,
       prompt              AS Prompt,
       result::text        AS ResultJson,
       timeout_seconds     AS TimeoutSeconds,
       idempotency_key     AS IdempotencyKey
FROM runs
WHERE run_id = @RunId";

        await using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<Run>(
            new CommandDefinition(sql, new { RunId = runId }, cancellationToken: ct));
    }

    public async Task UpdateStatusAsync(Guid runId, string status, CancellationToken ct)
    {
        const string sql = @"
UPDATE runs
SET status = @Status,
    started_at = COALESCE(started_at, CASE WHEN @Status = 'Running' THEN NOW() ELSE started_at END),
    finished_at = CASE
        WHEN @Status IN ('Completed', 'Failed', 'Cancelled', 'TimedOut') AND finished_at IS NULL
        THEN NOW()
        ELSE finished_at
    END
WHERE run_id = @RunId";
        await using var conn = _factory.Create();
        await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { RunId = runId, Status = status },
            cancellationToken: ct));
    }

    public async Task UpdateResultAsync(Guid runId, string resultJson, CancellationToken ct)
    {
        const string sql = @"
UPDATE runs
SET result = @ResultJson::jsonb
WHERE run_id = @RunId";
        await using var conn = _factory.Create();
        await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { RunId = runId, ResultJson = resultJson },
            cancellationToken: ct));
    }

    // Atomic claim of the oldest Pending run. Uses
    // `UPDATE runs SET status='Running' WHERE run_id = (SELECT … WHERE
    // status='Pending' ORDER BY created_at ASC FOR UPDATE SKIP LOCKED
    // LIMIT 1) RETURNING run_id` so concurrent workers cannot claim the
    // same run. Returns null when no Pending run is available.
    public async Task<Guid?> TryClaimNextPendingAsync(CancellationToken ct)
    {
        const string sql = @"
UPDATE runs
SET status = 'Running',
    started_at = NOW()
WHERE run_id = (
    SELECT run_id
    FROM runs
    WHERE status = 'Pending'
    ORDER BY created_at ASC
    FOR UPDATE SKIP LOCKED
    LIMIT 1
)
RETURNING run_id";
        await using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(sql, cancellationToken: ct));
    }

    // Reconciler: returns Runs in 'Running' whose started_at is older
    // than timeout_seconds/2 ago (i.e. they are likely abandoned by a
    // previous bridge instance). Tuple: (run_id, timeout_seconds).
    public async Task<IReadOnlyList<(Guid RunId, int TimeoutSeconds)>> ListAbandonedRunningAsync(CancellationToken ct)
    {
        const string sql = @"
SELECT run_id AS RunId, timeout_seconds AS TimeoutSeconds
FROM runs
WHERE status = 'Running'
  AND started_at IS NOT NULL
  AND started_at < NOW() - (timeout_seconds || ' seconds')::interval / 2";
        await using var conn = _factory.Create();
        var rows = await conn.QueryAsync<(Guid RunId, int TimeoutSeconds)>(
            new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }
}
