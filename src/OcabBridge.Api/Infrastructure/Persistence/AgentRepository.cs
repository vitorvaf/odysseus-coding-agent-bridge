using Dapper;
using OcabBridge.Api.Domain;

namespace OcabBridge.Api.Infrastructure.Persistence;

// Read-only repository for slice 1.1.4 (MCP Contract Completion).
// Writes are deferred until the operator surface (Phase 10).
public sealed class AgentRepository
{
    private readonly NpgsqlConnectionFactory _factory;

    public AgentRepository(NpgsqlConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<Agent>> ListAsync(bool includeDisabled, CancellationToken ct)
    {
        var sql = @"
SELECT id            AS Id,
       name          AS Name,
       display_name  AS DisplayName,
       version       AS Version,
       enabled       AS Enabled,
       capabilities  AS Capabilities,
       endpoint      AS Endpoint,
       created_at    AS CreatedAt
FROM agents"
               + (includeDisabled ? string.Empty : " WHERE enabled = TRUE")
               + " ORDER BY name";

        await using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Agent>(
            new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<Agent?> GetByNameAsync(string name, CancellationToken ct)
    {
        const string sql = @"
SELECT id            AS Id,
       name          AS Name,
       display_name  AS DisplayName,
       version       AS Version,
       enabled       AS Enabled,
       capabilities  AS Capabilities,
       endpoint      AS Endpoint,
       created_at    AS CreatedAt
FROM agents
WHERE name = @Name";
        await using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<Agent>(
            new CommandDefinition(sql, new { Name = name }, cancellationToken: ct));
    }
}
