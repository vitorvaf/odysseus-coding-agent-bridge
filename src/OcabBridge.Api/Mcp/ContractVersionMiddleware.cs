namespace OcabBridge.Api.Mcp;

// Emits X-OCAB-Contract-Version on every HTTP response from the bridge.
// Per docs/specs/004-mcp-contract/spec.md (MCP-NFR-005). The literal
// value is "v1" for slice 1.1.4 and remains pinned until version 2 is
// published via a separate ADR.
public sealed class ContractVersionMiddleware
{
    public const string HeaderName = "X-OCAB-Contract-Version";
    public const string Version = "v1";

    private readonly RequestDelegate _next;

    public ContractVersionMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.OnStarting(static state =>
        {
            var http = (HttpContext)state;
            if (!http.Response.Headers.ContainsKey(HeaderName))
            {
                http.Response.Headers[HeaderName] = Version;
            }
            return Task.CompletedTask;
        }, ctx);
        return _next(ctx);
    }
}
