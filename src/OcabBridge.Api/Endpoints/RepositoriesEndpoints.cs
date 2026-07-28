using OcabBridge.Api.Infrastructure.Persistence;

namespace OcabBridge.Api.Endpoints;

// Minimal REST surface for the repository registry (slice 1.1.2).
// Mirrors the MCP `repositories_list` tool — MCP Streamable HTTP transport
// is the focus of slice 1.1.4 (MCP Contract Completion). Until then we
// surface the equivalent data over HTTP so the Odysseus stack and
// integration tests can use it.
public static class RepositoriesEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/v1/repositories").WithTags("repositories");

        group.MapGet("/", async (
            RepositoryRepository repos,
            CancellationToken ct) =>
        {
            var items = await repos.ListAsync(ct);
            return Results.Ok(new
            {
                repositories = items.Select(r => new
                {
                    slug = r.Slug,
                    displayName = r.DisplayName,
                    defaultBranch = r.DefaultBranch,
                    writable = r.Writable,
                    allowedAgents = r.AllowedAgents,
                    readOnlyOnly = r.ReadOnlyOnly
                })
            });
        });
    }
}
