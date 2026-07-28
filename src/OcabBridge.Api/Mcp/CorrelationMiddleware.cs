namespace OcabBridge.Api.Mcp;

// Emits X-OCAB-Trace-Id on every response. Honors an upstream
// X-OCAB-Trace-Id if present (e.g., when called via Odysseus), otherwise
// mints a fresh opaque ID. The trace id is also placed on
// HttpContext.Items["TraceId"] for downstream loggers.
//
// X-OCAB-Run-Id is set by the MCP tool implementations themselves when a
// runId is produced by the call (run_create, run_cancel). The header is
// only emitted when the value is present.
public sealed class CorrelationMiddleware
{
    public const string TraceHeader = "X-OCAB-Trace-Id";
    public const string RunHeader = "X-OCAB-Run-Id";

    private readonly RequestDelegate _next;

    public CorrelationMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext ctx)
    {
        string? traceId = null;
        if (ctx.Request.Headers.TryGetValue(TraceHeader, out var existing)
            && !string.IsNullOrWhiteSpace(existing.ToString()))
        {
            traceId = existing.ToString();
        }
        else
        {
            traceId = Guid.NewGuid().ToString("N");
        }

        ctx.Items["TraceId"] = traceId;

        ctx.Response.OnStarting(static state =>
        {
            var (http, trace) = ((HttpContext, string))state;
            if (!http.Response.Headers.ContainsKey(TraceHeader))
            {
                http.Response.Headers[TraceHeader] = trace;
            }
            return Task.CompletedTask;
        }, (ctx, traceId));

        return _next(ctx);
    }
}
