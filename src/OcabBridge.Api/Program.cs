using Npgsql;
using OcabBridge.Api.Adapters;
using OcabBridge.Api.Application;
using OcabBridge.Api.Configuration;
using OcabBridge.Api.Endpoints;
using OcabBridge.Api.Infrastructure.Auth;
using OcabBridge.Api.Infrastructure.Persistence;
using Prometheus;

// Composition root for the OCAB Coding Agent Bridge (slice 1.1.3).
// See:
//  - docs/specs/001-platform-foundation/spec.md
//  - docs/specs/005-opencode-runner/spec.md
//  - docs/specs/004-mcp-contract/spec.md
//  - docs/discovery/002-mcp-sdk-evaluation.md
//  - docs/discovery/006-authentication-and-networking.md (Bearer token topology)
//  - docs/discovery/008-resource-limits-baseline.md (resource caps)
//  - docs/adr/0015-runtime-version.md (.NET 8 LTS)

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

// Typed options
builder.Services.Configure<OcabOptions>(builder.Configuration.GetSection(OcabOptions.SectionName));

// JSON logging to stdout (does not contain tokens; bearer middleware
// only writes status codes, never request/response bodies).
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(opts =>
{
    opts.UseUtcTimestamp = true;
    opts.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
    opts.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
});

// Persistence (Npgsql + Dapper; EF Core deferred per Phase 0 decision).
builder.Services.AddSingleton<NpgsqlConnectionFactory>();
builder.Services.AddSingleton<RunRepository>();
builder.Services.AddSingleton<RunEventRepository>();
builder.Services.AddSingleton<RepositoryRepository>();

// Runner adapter (slice 1.1.3). HttpClient is configured via
// AddHttpClient<TClient> so the OpenCode base URL is sourced from
// OCAB__OpenCodeUrl env (or Ocab:OpenCodeUrl config).
var openCodeBaseUrl = builder.Configuration["Ocab:OpenCodeUrl"]
    ?? "http://ocab-opencode-runner:4096";
builder.Services.AddHttpClient<OpenCodeAdapter>(c =>
{
    c.BaseAddress = new Uri(openCodeBaseUrl);
    c.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddSingleton<IRunnerAdapter>(sp => sp.GetRequiredService<OpenCodeAdapter>());

// Dispatcher + clock.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RunDispatcher>();

// MCP server (slice 1.1.3 contract). Tool types registered via
// WithToolsFromAssembly scan — the OcabMcpTools class carries the
// [McpServerToolType] attribute.
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// ASP.NET Core health-check registration.
builder.Services.AddHealthChecks();

var app = builder.Build();

// Bearer token middleware is registered BEFORE endpoint mapping so it
// gates /v1/runs and /mcp while keeping /health, /ready, /metrics open.
app.UseMiddleware<BearerTokenMiddleware>();

// Liveness — always 200 if the process is up.
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "ocab-bridge",
    version = "1.0.0-MVP"
}));

// Readiness — verifies the postgres connection by running SELECT 1.
// Returns 503 with diagnostic detail on failure so the Compose healthcheck
// can mark the container unhealthy during schema-migration windows.
app.MapGet("/ready", async (NpgsqlConnectionFactory factory, CancellationToken ct) =>
{
    try
    {
        await using var conn = factory.Create();
        await using var cmd = new NpgsqlCommand("SELECT 1", conn);
        await cmd.ExecuteScalarAsync(ct);
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "not_ready", error = ex.GetType().Name, detail = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

// Prometheus exposition.
app.MapMetrics();

// HTTP API (kept for ad-hoc testing; the canonical surface is /mcp).
RunsEndpoints.Map(app);
RepositoriesEndpoints.Map(app);

// MCP Streamable HTTP transport — the canonical surface for Odysseus.
app.MapMcp("/mcp");

app.Run();
