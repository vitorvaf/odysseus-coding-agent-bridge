using System.ComponentModel;
using ModelContextProtocol.Server;
using OcabBridge.Api.Application;
using OcabBridge.Api.Domain;
using OcabBridge.Api.Infrastructure.Persistence;
using OcabBridge.Api.Mcp;

namespace OcabBridge.Api.Mcp;

// MCP tool surface for slice 1.1.3 (OpenCode Read-Only Vertical Slice).
// Implements the minimal slice contract: repositories_list, run_create,
// run_get, run_cancel, run_report. Phase 4 (slice 1.1.4) extends this
// with idempotency, pagination, and full versions of the remaining tools.

[McpServerToolType]
public sealed class OcabMcpTools
{
    private readonly RunDispatcher _dispatcher;
    private readonly RepositoryRepository _repos;
    private readonly RunRepository _runs;
    private readonly RunEventRepository _events;

    public OcabMcpTools(
        RunDispatcher dispatcher,
        RepositoryRepository repos,
        RunRepository runs,
        RunEventRepository events)
    {
        _dispatcher = dispatcher;
        _repos = repos;
        _runs = runs;
        _events = events;
    }

    [McpServerTool(Name = "repositories_list"),
     Description("Returns registered repositories (ADR-0014 slug registry).")]
    public async Task<object> RepositoriesListAsync(CancellationToken ct)
    {
        var items = await _repos.ListAsync(ct);
        return new
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
        };
    }

    [McpServerTool(Name = "run_create"),
     Description("Creates a Run against a registered repository slug.")]
    public async Task<object> RunCreateAsync(
        [Description("Repository slug from repositories_list")] string repositorySlug,
        [Description("Optional prompt (read-only runs may omit)")] string? prompt,
        [Description("Force read-only mode (default true for slice 1.1.3)")] bool? readOnly,
        CancellationToken ct)
    {
        try
        {
            var effectiveReadOnly = readOnly ?? true;
            var effectivePrompt = prompt ?? string.Empty;
            var runId = await _dispatcher.CreateAsync(repositorySlug, effectivePrompt, ct);
            return new
            {
                runId,
                status = "Pending",
                readOnly = effectiveReadOnly
            };
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("repository_not_found"))
        {
            return new McpError(McpErrorCodes.NotFound, "repository_not_found",
                new { repositorySlug });
        }
    }

    [McpServerTool(Name = "run_get"),
     Description("Returns the current state of a Run.")]
    public async Task<object> RunGetAsync(
        [Description("Run GUID returned by run_create")] Guid runId,
        CancellationToken ct)
    {
        var run = await _dispatcher.GetAsync(runId, ct);
        if (run is null) return new McpError(McpErrorCodes.NotFound, "run_not_found");
        return new
        {
            runId = run.RunId,
            status = run.Status,
            repositorySlug = run.RepositorySlug,
            createdAt = run.CreatedAt
        };
    }

    [McpServerTool(Name = "run_cancel"),
     Description("Requests cancellation of a non-terminal Run.")]
    public async Task<object> RunCancelAsync(
        [Description("Run GUID")] Guid runId,
        [Description("Reason for cancellation")] string? reason,
        CancellationToken ct)
    {
        var accepted = await _dispatcher.CancelAsync(runId, reason ?? "user_requested", ct);
        if (!accepted) return new McpError(McpErrorCodes.NotFound, "run_not_found");
        return new { runId, status = "Cancelling" };
    }

    [McpServerTool(Name = "run_report"),
     Description("Returns the structured report for a completed Run.")]
    public async Task<object> RunReportAsync(
        [Description("Run GUID")] Guid runId,
        CancellationToken ct)
    {
        var run = await _dispatcher.GetAsync(runId, ct);
        if (run is null) return new McpError(McpErrorCodes.NotFound, "run_not_found");

        var events = await _events.ListAsync(runId, ct);
        var summary = events
            .Where(e => string.Equals(e.ToState, RunStatus.Completed, StringComparison.Ordinal))
            .Select(e => e.Reason ?? "completed")
            .FirstOrDefault() ?? "no_completion_event_recorded";

        return new RunReportDto(
            RunId: run.RunId.ToString(),
            Status: run.Status,
            RepositorySlug: run.RepositorySlug ?? "unknown",
            Summary: summary,
            Scope: Array.Empty<string>(),
            FilesChanged: Array.Empty<ChangedFileDto>(),
            Findings: Array.Empty<FindingDto>(),
            Artifacts: Array.Empty<string>());
    }
}
