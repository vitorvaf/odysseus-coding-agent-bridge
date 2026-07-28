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

    public int Port { get; }

    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public TimeSpan SlowDelayMs { get; set; } = TimeSpan.FromSeconds(10);

    public IReadOnlyCollection<string> Requests => _requests;

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
            var path = ctx.Request.Url?.AbsolutePath ?? "/";
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
                await HandleChatCompletionsAsync(ctx, ct).ConfigureAwait(false);
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
                await WriteJsonAsync(ctx.Response, HttpStatusCode.OK, new { ok = true }).ConfigureAwait(false);
                return;
            }

            ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
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

    private async Task HandleChatCompletionsAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        // Capture the request for diagnostics.
        using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
        {
            var body = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
            _requests.Add(body);
        }

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

        var chunks = new[]
        {
            "{\"id\":\"chatcmpl-det-001\",\"object\":\"chat.completion.chunk\",\"created\":1700000000,\"model\":\"deterministic-model\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"hello \"},\"finish_reason\":null}]}",
            "{\"id\":\"chatcmpl-det-001\",\"object\":\"chat.completion.chunk\",\"created\":1700000000,\"model\":\"deterministic-model\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"world \"},\"finish_reason\":null}]}",
            "{\"id\":\"chatcmpl-det-001\",\"object\":\"chat.completion.chunk\",\"created\":1700000000,\"model\":\"deterministic-model\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"from deterministic provider\"},\"finish_reason\":null}]}",
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
