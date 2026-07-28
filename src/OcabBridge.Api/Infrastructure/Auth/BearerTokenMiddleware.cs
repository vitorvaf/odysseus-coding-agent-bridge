namespace OcabBridge.Api.Infrastructure.Auth;

// Bearer token middleware. Validates the Authorization header against
// OCAB_MCP_TOKEN (env) or Ocab:McpToken (config) per
// docs/discovery/006-authentication-and-networking.md.
//
// Endpoints /health, /ready, /metrics are intentionally open to allow
// the Compose healthcheck and Prometheus scraper to operate without a
// token. All other endpoints (e.g., /v1/runs) require the bearer token.
public sealed class BearerTokenMiddleware
{
    private const string AuthHeader = "Authorization";
    private const string BearerPrefix = "Bearer ";

    private static readonly string[] AnonymousPaths =
    {
        "/health", "/ready", "/metrics"
    };

    private readonly RequestDelegate _next;
    private readonly string? _expectedToken;
    private readonly ILogger<BearerTokenMiddleware> _logger;

    public BearerTokenMiddleware(
        RequestDelegate next,
        IConfiguration cfg,
        ILogger<BearerTokenMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _expectedToken = Environment.GetEnvironmentVariable("OCAB_MCP_TOKEN")
            ?? cfg["Ocab:McpToken"];
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? string.Empty;
        if (AnonymousPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(ctx);
            return;
        }

        if (string.IsNullOrEmpty(_expectedToken) || _expectedToken == "__SET_ME__")
        {
            _logger.LogError("OCAB_MCP_TOKEN not configured; rejecting request {Path}", path);
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await ctx.Response.WriteAsync("service_unavailable");
            return;
        }

        if (!ctx.Request.Headers.TryGetValue(AuthHeader, out var values) || values.Count == 0)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsync("unauthorized");
            return;
        }

        var raw = values.ToString();
        if (!raw.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsync("unauthorized");
            return;
        }

        var token = raw.Substring(BearerPrefix.Length).Trim();
        if (!string.Equals(token, _expectedToken, StringComparison.Ordinal))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsync("unauthorized");
            return;
        }

        ctx.Items["Actor"] = "bearer";
        await _next(ctx);
    }
}
