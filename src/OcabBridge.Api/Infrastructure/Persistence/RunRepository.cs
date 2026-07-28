using Dapper;
using OcabBridge.Api.Domain;

namespace OcabBridge.Api.Infrastructure.Persistence;

// Minimal repository for slice 1.1.1 (Foundation). Only InsertAsync and
// GetAsync are required by the backlog acceptance criteria. State
// transitions, cancellation, timeout, and listing are deferred to
// later slices.
public sealed class RunRepository
{
    private readonly NpgsqlConnectionFactory _factory;

    public RunRepository(NpgsqlConnectionFactory factory) => _factory = factory;

    public async Task InsertAsync(Run run, CancellationToken ct)
    {
        const string sql = @"
INSERT INTO runs (run_id, created_at, status, repository_slug, prompt, result)
VALUES (@RunId, @CreatedAt, @Status, @RepositorySlug, @Prompt, @ResultJson::jsonb)";

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
                run.ResultJson
            },
            cancellationToken: ct));
    }

    public async Task<Run?> GetAsync(Guid runId, CancellationToken ct)
    {
        const string sql = @"
SELECT run_id         AS RunId,
       created_at     AS CreatedAt,
       status         AS Status,
       repository_slug AS RepositorySlug,
       prompt         AS Prompt,
       result::text   AS ResultJson
FROM runs
WHERE run_id = @RunId";

        await using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<Run>(
            new CommandDefinition(sql, new { RunId = runId }, cancellationToken: ct));
    }
}
