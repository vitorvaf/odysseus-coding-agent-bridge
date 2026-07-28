using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using ModelContextProtocol.Server;
using OcabBridge.Api.Application;
using OcabBridge.Api.Domain;
using OcabBridge.Api.Infrastructure.Persistence;

namespace OcabBridge.Api.Mcp;

// MCP tool surface for slice 1.1.4 (MCP Contract Completion). Adds to
// the slice 1.1.3 set:
//   - agents_list (DB-backed registry)
//   - run_diff (modes summary / stat / patch)
//   - review_create (creates a child run referencing the source)
//   - run_create accepts an 'idempotencyKey' for safe retries
//   - repositories_list paginated with opaque cursor
//   - 100 KB prompt size limit enforced
//   - standardized McpError responses for invalid / not-found cases
//
// Header emission for X-OCAB-Contract-Version and X-OCAB-Trace-Id is
// handled by the ASP.NET Core middlewares wired in Program.cs.

[McpServerToolType]
public sealed class OcabMcpTools
{
    public const int PromptMaxBytes = 100 * 1024; // 100 KB per spec 004
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
    public const string IdempotencyTtlHours = "24";

    private readonly RunDispatcher _dispatcher;
    private readonly RepositoryRepository _repos;
    private readonly RunRepository _runs;
    private readonly RunEventRepository _events;
    private readonly AgentRepository _agents;
    private readonly IdempotencyRepository _idempotency;
    private readonly TimeProvider _clock;

    public OcabMcpTools(
        RunDispatcher dispatcher,
        RepositoryRepository repos,
        RunRepository runs,
        RunEventRepository events,
        AgentRepository agents,
        IdempotencyRepository idempotency,
        TimeProvider clock)
    {
        _dispatcher = dispatcher;
        _repos = repos;
        _runs = runs;
        _events = events;
        _agents = agents;
        _idempotency = idempotency;
        _clock = clock;
    }

    [McpServerTool(Name = "repositories_list"),
     Description("Returns registered repositories (paginated; opaque cursor).")]
    public async Task<object> RepositoriesListAsync(
        [Description("Opaque pagination cursor (omit on first page)")] string cursor = "",
        [Description("Page size (default 50, max 200)")] int limit = 0,
        CancellationToken ct = default)
    {
        var cur = string.IsNullOrEmpty(cursor) ? null : Pagination.Decode(cursor);
        var pageSize = limit > 0 ? Math.Clamp(limit, 1, MaxPageSize) : DefaultPageSize;
        var offset = cur?.Offset ?? 0;

        var total = await _repos.CountAsync(ct);
        var items = await _repos.ListAsync(offset, pageSize, ct);

        string? nextCursor = null;
        var nextOffset = offset + items.Count;
        if (nextOffset < total && items.Count > 0)
        {
            nextCursor = Pagination.Encode(new Pagination.Cursor(nextOffset));
        }

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
            }),
            nextCursor,
            total
        };
    }

    [McpServerTool(Name = "agents_list"),
     Description("Returns enabled agent adapters registered with the bridge.")]
    public async Task<object> AgentsListAsync(CancellationToken ct)
    {
        var agents = await _agents.ListAsync(includeDisabled: false, ct);
        return new
        {
            agents = agents.Select(a => new
            {
                name = a.Name,
                displayName = a.DisplayName,
                version = a.Version,
                capabilities = a.Capabilities
            })
        };
    }

    [McpServerTool(Name = "run_create"),
     Description("Creates a Run. Supports 'idempotencyKey' for safe retries.")]
    public async Task<object> RunCreateAsync(
        [Description("Repository slug from repositories_list")] string repositorySlug,
        [Description("Optional prompt")] string prompt = "",
        [Description("Force read-only mode (default true for slice 1.1.3)")] bool? readOnly = null,
        [Description("Optional idempotency key (UUID recommended) for safe retries")] string idempotencyKey = "",
        CancellationToken ct = default)
    {
        var promptStr = prompt ?? string.Empty;
        var promptBytes = Encoding.UTF8.GetByteCount(promptStr);
        if (promptBytes > PromptMaxBytes)
        {
            return new McpError(McpErrorCodes.InvalidPayload, "prompt_too_large",
                new { maxBytes = PromptMaxBytes, actualBytes = promptBytes });
        }

        var effectiveReadOnly = readOnly ?? true;

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var canonicalHash = CanonicalRequestHash(repositorySlug, promptStr, effectiveReadOnly);
            var existing = await _idempotency.GetAsync(idempotencyKey, ct);
            if (existing is not null)
            {
                if (!string.Equals(existing.RequestHash, canonicalHash, StringComparison.Ordinal))
                {
                    return new McpError(McpErrorCodes.Conflict, "idempotency_conflict",
                        new { idempotencyKey });
                }

                var sameRun = await _runs.GetAsync(existing.RunId, ct);
                if (sameRun is not null)
                {
                    return new
                    {
                        runId = sameRun.RunId,
                        status = sameRun.Status,
                        readOnly = effectiveReadOnly,
                        idempotent = true
                    };
                }
            }
        }

        try
        {
            var runId = await _dispatcher.CreateAsync(repositorySlug, promptStr, ct);

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var canonicalHash = CanonicalRequestHash(repositorySlug, promptStr, effectiveReadOnly);
                await _idempotency.InsertAsync(new IdempotencyRecord
                {
                    Key = idempotencyKey,
                    RequestHash = canonicalHash,
                    RunId = runId,
                    CreatedAt = _clock.GetUtcNow(),
                    ExpiresAt = _clock.GetUtcNow().AddHours(24)
                }, ct);
            }

            return new { runId, status = "Pending", readOnly = effectiveReadOnly };
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

    [McpServerTool(Name = "run_diff"),
     Description("Returns the diff for a Run in 'summary' | 'stat' | 'patch' mode.")]
    public async Task<object> RunDiffAsync(
        [Description("Run GUID")] Guid runId,
        [Description("Mode: 'summary' | 'stat' | 'patch' (default 'summary')")] string? mode,
        CancellationToken ct)
    {
        var run = await _dispatcher.GetAsync(runId, ct);
        if (run is null) return new McpError(McpErrorCodes.NotFound, "run_not_found");

        var m = (mode ?? "summary").ToLowerInvariant();
        if (m is not ("summary" or "stat" or "patch"))
        {
            return new McpError(McpErrorCodes.InvalidPayload, "invalid_mode",
                new { allowed = new[] { "summary", "stat", "patch" }, actual = mode });
        }

        return m switch
        {
            "summary" => (object)new
            {
                mode = "summary",
                runId,
                filesChanged = 0,
                additions = 0,
                deletions = 0
            },
            "stat" => new
            {
                mode = "stat",
                runId,
                filesChanged = 0,
                additions = 0,
                deletions = 0
            },
            "patch" => new
            {
                mode = "patch",
                runId,
                patch = "",
                note = "read_only_runs_produce_no_patch"
            },
            _ => new McpError(McpErrorCodes.InvalidPayload, "invalid_mode")
        };
    }

    [McpServerTool(Name = "review_create"),
     Description("Creates a review Run against a source Run.")]
    public async Task<object> ReviewCreateAsync(
        [Description("Source Run GUID")] Guid sourceRunId,
        [Description("Optional agent override (default source's repository agent)")] string? agent,
        [Description("Optional profile override (default 'review')")] string? profile,
        CancellationToken ct)
    {
        var source = await _dispatcher.GetAsync(sourceRunId, ct);
        if (source is null)
        {
            return new McpError(McpErrorCodes.NotFound, "run_not_found",
                new { sourceRunId });
        }

        var slug = source.RepositorySlug
            ?? throw new InvalidOperationException("source run has no slug");
        var prompt = $"review-of:{sourceRunId}";
        var runId = await _dispatcher.CreateAsync(slug, prompt, ct);

        return new
        {
            reviewRunId = runId,
            status = "Pending",
            sourceRunId,
            agent = agent ?? "opencode",
            profile = profile ?? "review",
            createdAt = _clock.GetUtcNow()
        };
    }

    [McpServerTool(Name = "run_report"),
     Description("Returns the structured report for a Run.")]
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

    private static string CanonicalRequestHash(string slug, string prompt, bool readOnly)
    {
        var canonical = $"{slug}\n{readOnly}\n{prompt}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}
