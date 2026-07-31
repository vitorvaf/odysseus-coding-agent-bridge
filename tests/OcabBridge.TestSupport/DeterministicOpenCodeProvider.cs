// Deterministic OpenAI-compatible mock provider used by SLICE-STAB-003
// E2E tests. Lives inside the OpenCodeTestServer fixture so it starts
// and stops alongside the real OpenCode Server binary. The OpenCode
// runner is configured (via poc/opencode-container/opencode.json) to
// use a custom provider pointing at this mock, replacing only the
// inference dependency while keeping the runner + adapter + coordinator
// on the real path.
//
// Endpoints:
//   POST /v1/chat/completions
//     Standard OpenAI chat completions endpoint. Returns SSE
//     (Content-Type: text/event-stream). Behaviour depends on the
//     active scenario (POST /control/scenario).
//
//   POST /control/scenario
//     Body: { "scenario": "normal" | "slow" | "blocked" | "error" | "invalid" }
//     Sets the active scenario for subsequent chat-completions calls.
//
//   GET  /control/scenario
//     Returns the current scenario as { "scenario": "..." }.
//
// Scenarios:
//   - normal:  returns a small deterministic SSE stream ending in
//              "hello world from deterministic provider".
//   - slow:    sleeps for SlowDelayMs (default 10_000) before
//              responding. Used to exercise the per-Run timeout path.
//   - blocked: holds the response open indefinitely (no body, no
//              termination). Used to exercise cancellation: the test
//              calls run_cancel and asserts the adapter's
//              POST /session/{id}/abort fires.
//   - error:   returns HTTP 502 with body {"error":"upstream_failure"}.
//   - invalid: returns HTTP 200 with Content-Type: text/html and body
//              "<html>not json</html>" — exercises the
//              UpstreamContractMismatch code path in OpenCodeAdapter.
//
// The HTTP server binds to 127.0.0.1 only (loopback) on the configured
// port (default 14302). It runs on a background thread; DisposeAsync
// stops the listener and waits for the thread to drain.

using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OcabBridge.TestSupport;

public sealed class DeterministicOpenCodeProvider : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptLoop;
    private string _scenario = "normal";
    private readonly ConcurrentBag<string> _requests = new();
    private long _allRequests;
    private long _inferenceRequests;
    private readonly ConcurrentBag<string> _auditLog = new();
    private string _logPath = "";

    public int Port { get; }

    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public TimeSpan SlowDelayMs { get; set; } = TimeSpan.FromSeconds(10);

    public IReadOnlyCollection<string> Requests => _requests;

    public long AllRequests => Interlocked.Read(ref _allRequests);

    public long InferenceRequests => Interlocked.Read(ref _inferenceRequests);

    public IReadOnlyCollection<string> AuditLog => _auditLog;

    public void SetLogPath(string path) => _logPath = path;

    public DeterministicOpenCodeProvider(int port = 14302)
    {
        Port = port;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public Task StartAsync()
    {
        _listener.Start();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { /* ignore */ }
        try { _listener.Close(); } catch { /* ignore */ }
        if (_acceptLoop is not null)
        {
            try { await _acceptLoop.ConfigureAwait(false); }
            catch { /* ignore */ }
        }
        if (!string.IsNullOrEmpty(_logPath))
        {
            try
            {
                var lines = new[]
                {
                    $"summary all={AllRequests} inference={InferenceRequests}",
                }.Concat(_auditLog);
                File.WriteAllLines(_logPath, lines);
            }
            catch { /* ignore */ }
        }
    }

    public void SetScenario(string scenario) => _scenario = scenario;

    public string GetScenario() => _scenario;

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync().ConfigureAwait(false); }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            _ = Task.Run(() => HandleAsync(ctx, ct), ct);
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        try
        {
            Interlocked.Increment(ref _allRequests);
            var path = ctx.Request.Url?.AbsolutePath ?? "/";
            var query = ctx.Request.Url?.Query ?? "";
            var authPresent = ctx.Request.Headers["Authorization"] != null;
            var contentType = ctx.Request.ContentType ?? "";
            var contentLength = ctx.Request.ContentLength64;
            string body = "";
            if (path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase)
                || path == "/chat/completions"
                || path == "/responses"
                || path.StartsWith("/control/", StringComparison.OrdinalIgnoreCase))
            {
                using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                {
                    body = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                }
            }
            var firstLevel = ExtractFirstLevelJson(body);
            _auditLog.Add(string.Format(CultureInfo.InvariantCulture,
                "{0:o} method={1} path={2}{3} ct=\"{4}\" len={5} auth={6} firstLevel=[{7}]",
                DateTimeOffset.UtcNow,
                ctx.Request.HttpMethod,
                path,
                string.IsNullOrEmpty(query) ? "" : "?" + query,
                contentType,
                contentLength,
                authPresent ? "yes" : "no",
                firstLevel));
            if (path == "/v1/chat/completions" || path == "/chat/completions"
                || path == "/v1/responses" || path == "/responses")
            {
                Interlocked.Increment(ref _inferenceRequests);
            }
            if (path == "/control/scenario" && ctx.Request.HttpMethod == "POST")
            {
                await HandleSetScenarioAsync(ctx, ct).ConfigureAwait(false);
                return;
            }
            if (path == "/control/scenario" && ctx.Request.HttpMethod == "GET")
            {
                await HandleGetScenarioAsync(ctx, ct).ConfigureAwait(false);
                return;
            }
            if (path == "/v1/chat/completions")
            {
                await HandleChatCompletionsAsync(ctx, body, ct).ConfigureAwait(false);
                return;
            }
            if (path == "/v1/responses" || path == "/responses")
            {
                await HandleResponsesAsync(ctx, body, ct).ConfigureAwait(false);
                return;
            }
            if (path == "/v1/models")
            {
                await HandleModelsAsync(ctx, ct).ConfigureAwait(false);
                return;
            }
            if (path == "/control/requests")
            {
                await HandleListRequestsAsync(ctx).ConfigureAwait(false);
                return;
            }
            if (path == "/control/reset")
            {
                _requests.Clear();
                Interlocked.Exchange(ref _allRequests, 0);
                Interlocked.Exchange(ref _inferenceRequests, 0);
                await WriteJsonAsync(ctx.Response, HttpStatusCode.OK, new { ok = true }).ConfigureAwait(false);
                return;
            }
            if (path == "/control/stats")
            {
                await WriteJsonAsync(ctx.Response, HttpStatusCode.OK, new
                {
                    all = AllRequests,
                    inference = InferenceRequests,
                }).ConfigureAwait(false);
                return;
            }

            _auditLog.Add($"catchall -> 404 {path}");
            ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
            ctx.Response.ContentType = "application/json";
            var body404 = JsonSerializer.Serialize(new
            {
                error = "not_found",
                path,
                method = ctx.Request.HttpMethod ?? "",
            });
            var bytes = Encoding.UTF8.GetBytes(body404);
            await ctx.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
            ctx.Response.Close();
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            try
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                ctx.Response.Close();
            }
            catch { /* ignore */ }
            _ = ex;
        }
    }

    private static string ExtractFirstLevelJson(string body)
    {
        if (string.IsNullOrEmpty(body)) return "";
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return "";
            var keys = new List<string>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    keys.Add($"{prop.Name}=\"{prop.Value.GetString()}\"");
                }
                else
                {
                    keys.Add($"{prop.Name}={prop.Value.ValueKind}");
                }
            }
            return string.Join(",", keys);
        }
        catch
        {
            return "<invalid-json>";
        }
    }

    private async Task HandleSetScenarioAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("scenario", out var scenario))
            {
                SetScenario(scenario.GetString() ?? "normal");
            }
        }
        catch (JsonException) { /* ignore — keep current scenario */ }

        await WriteJsonAsync(ctx.Response, HttpStatusCode.OK, new { scenario = _scenario })
            .ConfigureAwait(false);
    }

    private async Task HandleGetScenarioAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        await WriteJsonAsync(ctx.Response, HttpStatusCode.OK, new { scenario = _scenario })
            .ConfigureAwait(false);
    }

    private async Task HandleListRequestsAsync(HttpListenerContext ctx)
    {
        var arr = _requests.ToArray();
        await WriteJsonAsync(ctx.Response, HttpStatusCode.OK, new { requests = arr })
            .ConfigureAwait(false);
    }

    private async Task HandleChatCompletionsAsync(HttpListenerContext ctx, string body, CancellationToken ct)
    {
        _requests.Add(body);
        switch (_scenario)
        {
            case "slow":
                await DelayAndRespondNormalAsync(ctx, ct).ConfigureAwait(false);
                return;
            case "blocked":
                await BlockUntilCancelledAsync(ctx, ct).ConfigureAwait(false);
                return;
            case "error":
                await WriteJsonAsync(ctx.Response, HttpStatusCode.BadGateway,
                    new { error = "upstream_failure" }).ConfigureAwait(false);
                return;
            case "invalid":
                await WriteTextHtmlAsync(ctx.Response,
                    "<!doctype html><html><body>not json</body></html>")
                    .ConfigureAwait(false);
                return;
            default:
                await DelayAndRespondNormalAsync(ctx, ct).ConfigureAwait(false);
                return;
        }
    }

    private async Task HandleResponsesAsync(HttpListenerContext ctx, string body, CancellationToken ct)
    {
        _requests.Add("responses:" + body);
        await DelayAndRespondNormalAsync(ctx, ct).ConfigureAwait(false);
    }

    private static async Task HandleModelsAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        await WriteJsonAsync(ctx.Response, HttpStatusCode.OK, new Dictionary<string, object?>
        {
            ["object"] = "list",
            ["data"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "deterministic-model",
                    ["object"] = "model",
                    ["owned_by"] = "ocab"
                }
            }
        }).ConfigureAwait(false);
    }


    private async Task DelayAndRespondNormalAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        if (_scenario == "slow")
        {
            try { await Task.Delay(SlowDelayMs, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
        await WriteSseNormalAsync(ctx.Response).ConfigureAwait(false);
    }

    private async Task BlockUntilCancelledAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        // Open headers so the upstream SSE consumer sees the open
        // stream, then hold without writing anything until the test
        // cancels (typically via run_cancel).
        ctx.Response.StatusCode = (int)HttpStatusCode.OK;
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers["Cache-Control"] = "no-cache";
        ctx.Response.Headers["Connection"] = "keep-alive";
        try { ctx.Response.SendChunked = true; } catch { /* not supported on HttpListener */ }
        await ctx.Response.OutputStream.FlushAsync(ct).ConfigureAwait(false);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* expected on cancel */ }
        finally
        {
            try { ctx.Response.Close(); } catch { /* ignore */ }
        }
    }

    private static async Task WriteSseNormalAsync(HttpListenerResponse response)
    {
        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentType = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";

        // Single-chunk deterministic response: the assistant returns
        // exactly "OCAB_PROVIDER_OK" so the dispatcher can verify the
        // provider was reached and the terminal event was captured.
        var chunks = new[]
        {
            "{\"id\":\"chatcmpl-det-001\",\"object\":\"chat.completion.chunk\",\"created\":1700000000,\"model\":\"deterministic-model\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"OCAB_PROVIDER_OK\"},\"finish_reason\":null}]}",
            "{\"id\":\"chatcmpl-det-001\",\"object\":\"chat.completion.chunk\",\"created\":1700000000,\"model\":\"deterministic-model\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}",
            "[DONE]"
        };

        foreach (var chunk in chunks)
        {
            var bytes = Encoding.UTF8.GetBytes($"data: {chunk}\n\n");
            await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
            await response.OutputStream.FlushAsync().ConfigureAwait(false);
            await Task.Delay(20).ConfigureAwait(false);
        }

        response.Close();
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, HttpStatusCode status, object body)
    {
        response.StatusCode = (int)status;
        response.ContentType = "application/json";
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body));
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }

    private static async Task WriteTextHtmlAsync(HttpListenerResponse response, string html)
    {
        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentType = "text/html";
        var bytes = Encoding.UTF8.GetBytes(html);
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }
}
