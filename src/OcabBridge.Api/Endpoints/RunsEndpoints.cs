using OcabBridge.Api.Domain;
using OcabBridge.Api.Infrastructure.Persistence;

namespace OcabBridge.Api.Endpoints;

// Minimal REST surface for slice 1.1.1 (Foundation). POST creates a Run
// in status=Pending; GET reads the row. State transitions, cancellation,
// timeout, listing, and the full MCP contract are deferred to slice 1.1.3
// per backlog.md SLICE-FOUNDATION-001 / SLICE-OPENCODE-READONLY-VS.
//
// Slice 1.1.2 adds slug validation: POST /v1/runs rejects unknown slugs
// with 404 repository_not_found, satisfying the second acceptance
// criterion of SLICE-REPO-REGISTRY-001.
public static class RunsEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/v1/runs").WithTags("runs");

        group.MapPost("/", async (
            CreateRunRequest body,
            RunRepository runRepo,
            RepositoryRepository repoRepo,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.RepositorySlug))
            {
                return Results.BadRequest(new
                {
                    error = "repository_slug_required"
                });
            }

            var repo = await repoRepo.GetBySlugAsync(body.RepositorySlug, ct);
            if (repo is null)
            {
                return Results.NotFound(new
                {
                    error = "repository_not_found",
                    slug = body.RepositorySlug
                });
            }

            var run = new Run(
                RunId: Guid.NewGuid(),
                CreatedAt: DateTimeOffset.UtcNow,
                Status: "Pending",
                RepositorySlug: body.RepositorySlug,
                Prompt: body.Prompt,
                ResultJson: null);

            await runRepo.InsertAsync(run, ct);

            return Results.Created($"/v1/runs/{run.RunId}", new
            {
                runId = run.RunId,
                status = run.Status,
                createdAt = run.CreatedAt,
                repositorySlug = run.RepositorySlug
            });
        });

        group.MapGet("/{runId:guid}", async (
            Guid runId,
            RunRepository repo,
            CancellationToken ct) =>
        {
            var run = await repo.GetAsync(runId, ct);
            return run is null
                ? Results.NotFound(new { error = "run_not_found", runId })
                : Results.Ok(new
                {
                    runId = run.RunId,
                    status = run.Status,
                    repositorySlug = run.RepositorySlug,
                    createdAt = run.CreatedAt
                });
        });
    }

    public sealed record CreateRunRequest(string? RepositorySlug, string? Prompt);
}
