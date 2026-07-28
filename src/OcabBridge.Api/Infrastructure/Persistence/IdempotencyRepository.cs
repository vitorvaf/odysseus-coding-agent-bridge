using Dapper;
using OcabBridge.Api.Domain;

namespace OcabBridge.Api.Infrastructure.Persistence;

// Idempotency store for slice 1.1.4 (MCP Contract Completion).
// Implements SLICE-MCP-COMPLETION-001 acceptance criteria:
//   - same idempotencyKey + same payload -> same runId
//   - same idempotencyKey + different payload -> 409 idempotency_conflict
// Lifetime: 24 hours default (TTL column in the table).
public sealed class IdempotencyRepository
{
    private readonly NpgsqlConnectionFactory _factory;

    public IdempotencyRepository(NpgsqlConnectionFactory factory) => _factory = factory;

    public async Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct)
    {
        const string sql = @"
SELECT key          AS Key,
       request_hash AS RequestHash,
       run_id       AS RunId,
       created_at   AS CreatedAt,
       expires_at   AS ExpiresAt
FROM idempotency_keys
WHERE key = @Key AND expires_at > NOW()";

        await using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<IdempotencyRecord>(
            new CommandDefinition(sql, new { Key = key }, cancellationToken: ct));
    }

    public async Task InsertAsync(IdempotencyRecord rec, CancellationToken ct)
    {
        const string sql = @"
INSERT INTO idempotency_keys (key, request_hash, run_id, expires_at)
VALUES (@Key, @RequestHash, @RunId, @ExpiresAt)
ON CONFLICT (key) DO NOTHING";
        await using var conn = _factory.Create();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            rec.Key,
            rec.RequestHash,
            rec.RunId,
            rec.ExpiresAt
        }, cancellationToken: ct));
    }

    public async Task<int> PurgeExpiredAsync(CancellationToken ct)
    {
        const string sql = "DELETE FROM idempotency_keys WHERE expires_at <= NOW()";
        await using var conn = _factory.Create();
        return await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }
}
