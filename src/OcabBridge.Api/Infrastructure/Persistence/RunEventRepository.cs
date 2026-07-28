using Dapper;
using OcabBridge.Api.Domain;

namespace OcabBridge.Api.Infrastructure.Persistence;

// Append-only repository for run_events. The dispatcher is the only
// writer; readers (run_report, audit) consume via ListAsync.
public sealed class RunEventRepository
{
    private readonly NpgsqlConnectionFactory _factory;

    public RunEventRepository(NpgsqlConnectionFactory factory) => _factory = factory;

    public async Task InsertAsync(RunEvent evt, CancellationToken ct)
    {
        const string sql = @"
INSERT INTO run_events (run_id, from_state, to_state, actor, reason, metadata)
VALUES (@RunId, @FromState, @ToState, @Actor, @Reason, @Metadata::jsonb)";
        await using var conn = _factory.Create();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            evt.RunId,
            evt.FromState,
            evt.ToState,
            evt.Actor,
            evt.Reason,
            Metadata = evt.MetadataJson
        }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<RunEvent>> ListAsync(Guid runId, CancellationToken ct)
    {
        const string sql = @"
SELECT id            AS Id,
       run_id        AS RunId,
       sequence      AS Sequence,
       from_state    AS FromState,
       to_state      AS ToState,
       actor         AS Actor,
       reason        AS Reason,
       metadata::text AS MetadataJson,
       created_at    AS CreatedAt
FROM run_events
WHERE run_id = @RunId
ORDER BY sequence";
        await using var conn = _factory.Create();
        var rows = await conn.QueryAsync<RunEvent>(
            new CommandDefinition(sql, new { RunId = runId }, cancellationToken: ct));
        return rows.ToList();
    }
}
