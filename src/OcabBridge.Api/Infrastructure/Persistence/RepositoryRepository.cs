using Dapper;
using OcabBridge.Api.Domain;

namespace OcabBridge.Api.Infrastructure.Persistence;

// Read-only repository for slice 1.1.2 (Repository Registry). Provides
// list + lookup-by-slug. Write operations (insert, update, disable)
// are deferred — slice scope is "list and validate", per backlog.md
// SLICE-REPO-REGISTRY-001.
public sealed class RepositoryRepository
{
    private readonly NpgsqlConnectionFactory _factory;

    public RepositoryRepository(NpgsqlConnectionFactory factory) => _factory = factory;

    private const string Projection = @"
SELECT id                  AS Id,
       slug                AS Slug,
       display_name        AS DisplayName,
       default_branch      AS DefaultBranch,
       writable            AS Writable,
       allowed_agents      AS AllowedAgents,
       read_only_only      AS ReadOnlyOnly,
       validations::text   AS ValidationsJson,
       policies::text      AS PoliciesJson,
       created_at          AS CreatedAt
FROM repositories";

    public async Task<IReadOnlyList<Repository>> ListAsync(CancellationToken ct)
    {
        var sql = Projection + "\nORDER BY slug";
        await using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Repository>(
            new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    // Slice 1.1.4 — paginated list. Offset/limit applied at SQL level so
    // the cursor in MCP tools stays bounded regardless of table size.
    public async Task<IReadOnlyList<Repository>> ListAsync(int offset, int limit, CancellationToken ct)
    {
        var sql = Projection + "\nORDER BY slug OFFSET @Offset LIMIT @Limit";
        await using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Repository>(
            new CommandDefinition(sql, new { Offset = offset, Limit = limit }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<int> CountAsync(CancellationToken ct)
    {
        const string sql = "SELECT COUNT(*) FROM repositories";
        await using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<Repository?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        var sql = Projection + "\nWHERE slug = @Slug";
        await using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<Repository>(
            new CommandDefinition(sql, new { Slug = slug }, cancellationToken: ct));
    }
}
