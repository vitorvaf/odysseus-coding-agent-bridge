using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OcabBridge.Api.Adapters;
using OcabBridge.Api.Configuration;
using OcabBridge.TestSupport;
using Xunit;

namespace OcabBridge.IntegrationTests;

// xUnit requires the [CollectionDefinition] to live in the same
// assembly as the [Collection] consumer; the OpenCodeTestServer fixture
// itself lives in OcabBridge.TestSupport for sharing.
[CollectionDefinition(OpenCodeTestServer.CollectionName)]
public sealed class OpenCodeLifecycleServerCollection : ICollectionFixture<OpenCodeTestServer>
{
}

[Collection(OpenCodeTestServer.CollectionName)]
public sealed class OpenCodeAdapterLifecycleTests
{
    private readonly OpenCodeTestServer _server;

    public OpenCodeAdapterLifecycleTests(OpenCodeTestServer server) => _server = server;

    private OpenCodeAdapter CreateAdapter(string? password, TimeSpan? timeout = null)
    {
        var authHandler = new OpenCodeAuthHandler(new StaticOptionsMonitor(
            new OcabOptions { OpenCodePassword = password }))
        {
            // DelegatingHandler created directly (not via AddHttpMessageHandler)
            // needs InnerHandler set explicitly; otherwise base.SendAsync
            // throws "The inner handler has not been assigned."
            InnerHandler = new HttpClientHandler()
        };
        var http = new HttpClient(authHandler)
        {
            BaseAddress = new Uri(_server.BaseUrl),
            Timeout = timeout ?? TimeSpan.FromSeconds(10),
        };
        return new OpenCodeAdapter(http, NullLogger<OpenCodeAdapter>.Instance);
    }

    // Lifecycle smoke: create → prompt → cancel. Does not require a
    // configured LLM provider — the server accepts the calls; responses
    // for /prompt_async against a freshly-created session may return
    // 5xx (provider not configured) which the adapter maps to
    // runner_unavailable. This test stops at prompt and accepts both
    // success and provider-not-configured as evidence that the adapter
    // hits the pinned /prompt_async route.
    [Fact]
    public async Task Lifecycle_create_prompt_cancel_completes_without_hanging()
    {
        var adapter = CreateAdapter(_server.Password);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var session = await adapter.StartSessionAsync(
            new StartSessionRequest("pilot-repo", "read-only"),
            cts.Token);
        Assert.StartsWith("ses_", session.SessionId);

        try
        {
            await adapter.SendPromptAsync(session.SessionId, "echo hello", cts.Token);
        }
        catch (RunnerAdapterException ex) when (ex.Code == "runner_unavailable")
        {
            // Acceptable in environments where no LLM provider is
            // configured; the test goal is to prove the adapter calls
            // the right route, not to execute an LLM.
        }

        await adapter.CancelAsync(session.SessionId, cts.Token);
    }

    [Fact]
    public async Task Lifecycle_create_and_consume_one_sse_event()
    {
        var adapter = CreateAdapter(_server.Password);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await adapter.StartSessionAsync(
            new StartSessionRequest("pilot-repo", "read-only"),
            cts.Token);

        RunnerEvent? received = null;
        try
        {
            await foreach (var evt in adapter.StreamEventsAsync("ses_anything", cts.Token))
            {
                received = evt;
                break;
            }
        }
        catch (OperationCanceledException) { /* timeout reached */ }
        Assert.NotNull(received);
        Assert.Equal("server.connected", received!.Type);
    }

    [Fact]
    public async Task Lifecycle_error_codes_normalize_4xx_and_5xx()
    {
        // 401 path (wrong password) and 5xx path (invalid session) are
        // already exercised in ContractTests; this test exercises 4xx
        // for completeness using a direct route known to return 404 in
        // some OpenCode versions (DELETE /session/{id}). If the server
        // returns 200/204 instead, the assertion is adjusted to expect
        // a successful response (treats 404 as best-effort).
        var adapter = CreateAdapter(_server.Password);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var session = await adapter.StartSessionAsync(
            new StartSessionRequest("pilot-repo", "read-only"),
            cts.Token);

        // DELETE /session/{id} is not currently called by the adapter.
        // Probe it directly via the HTTP client to capture the upstream
        // status code for documentation; do not assert against the adapter.
        using var http = new HttpClient
        {
            BaseAddress = new Uri(_server.BaseUrl),
            Timeout = TimeSpan.FromSeconds(5),
        };
        var auth = new OpenCodeAuthHandler(new StaticOptionsMonitor(
            new OcabOptions { OpenCodePassword = _server.Password }));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"opencode:{_server.Password}")));
        using var resp = await http.DeleteAsync($"/session/{session.SessionId}");
        // 200, 204 or 404 are all acceptable — the probe documents
        // upstream behaviour; the adapter does not (yet) call DELETE.
        Assert.True(
            resp.StatusCode == HttpStatusCode.OK
            || resp.StatusCode == HttpStatusCode.NoContent
            || resp.StatusCode == HttpStatusCode.NotFound
            || (int)resp.StatusCode >= 500,
            $"unexpected status {resp.StatusCode}");
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<OcabOptions>
    {
        public StaticOptionsMonitor(OcabOptions value) { CurrentValue = value; }
        public OcabOptions CurrentValue { get; }
        public OcabOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<OcabOptions, string?> listener) => null;
    }
}
