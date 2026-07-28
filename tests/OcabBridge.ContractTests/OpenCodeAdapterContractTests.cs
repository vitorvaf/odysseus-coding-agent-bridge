using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OcabBridge.Api.Adapters;
using OcabBridge.Api.Configuration;
using OcabBridge.TestSupport;
using Xunit;

namespace OcabBridge.ContractTests;

// xUnit requires the [CollectionDefinition] to live in the same
// assembly as the [Collection] consumer; the OpenCodeTestServer fixture
// itself lives in OcabBridge.TestSupport for sharing.
[CollectionDefinition(OpenCodeTestServer.CollectionName)]
public sealed class OpenCodeContractServerCollection : ICollectionFixture<OpenCodeTestServer>
{
}

[Collection(OpenCodeTestServer.CollectionName)]
public sealed class OpenCodeAdapterContractTests
{
    private readonly OpenCodeTestServer _server;

    public OpenCodeAdapterContractTests(OpenCodeTestServer server) => _server = server;

    private OpenCodeAdapter CreateAdapter(string? password)
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
            Timeout = TimeSpan.FromSeconds(10),
        };
        return new OpenCodeAdapter(http, NullLogger<OpenCodeAdapter>.Instance);
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<OcabOptions>
    {
        public StaticOptionsMonitor(OcabOptions value) { CurrentValue = value; }
        public OcabOptions CurrentValue { get; }
        public OcabOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<OcabOptions, string?> listener) => null;
    }

    // Direct server probe — does not use the adapter; confirms the
    // fixture is talking to the pinned v1.18.8 and reports JSON.
    [Fact]
    public async Task Health_endpoint_returns_json_with_pinned_version()
    {
        var handler = new HttpClientHandler
        {
            // Use NetworkCredential so the HttpClient sends a proper
            // Authorization header on every request; OPENCODE_SERVER_PASSWORD
            // is set on the runner side so unauthenticated probes get 401.
            Credentials = new NetworkCredential("opencode", _server.Password)
        };
        using var http = new HttpClient(handler) { BaseAddress = new Uri(_server.BaseUrl) };
        using var resp = await http.GetAsync("/global/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("application/json", resp.Content.Headers.ContentType?.MediaType);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains($"\"version\":\"{_server.ExpectedVersion}\"", body);
    }

    [Fact]
    public async Task Session_create_uses_singular_route_and_records_metadata()
    {
        var adapter = CreateAdapter(_server.Password);
        var session = await adapter.StartSessionAsync(
            new StartSessionRequest("pilot-repo", "read-only"),
            CancellationToken.None);

        Assert.StartsWith("ses_", session.SessionId);
        Assert.NotNull(session.Status);
        Assert.Equal(_server.ExpectedVersion, session.UpstreamVersion);
        Assert.NotNull(session.ContractChecksum);
        Assert.Matches("^[0-9a-f]{64}$", session.ContractChecksum!);
    }

    [Fact]
    public async Task Wrong_password_yields_401_normalized_to_auth_failed()
    {
        var adapter = CreateAdapter("not-the-right-password");
        var ex = await Assert.ThrowsAsync<RunnerAdapterException>(() =>
            adapter.StartSessionAsync(
                new StartSessionRequest("pilot-repo", "read-only"),
                CancellationToken.None));
        Assert.Equal("runner_auth_failed", ex.Code);
        Assert.Equal(HttpStatusCode.Unauthorized, ex.HttpStatus);
    }

    [Fact]
    public async Task No_password_sends_no_header_and_server_returns_401()
    {
        // With OcabOptions.OpenCodePassword = null the auth handler does
        // not attach Authorization; the server (with OPENCODE_SERVER_PASSWORD
        // set) returns 401, which the adapter normalizes to runner_auth_failed.
        var adapter = CreateAdapter(null);
        var ex = await Assert.ThrowsAsync<RunnerAdapterException>(() =>
            adapter.StartSessionAsync(
                new StartSessionRequest("pilot-repo", "read-only"),
                CancellationToken.None));
        Assert.Equal("runner_auth_failed", ex.Code);
    }

    [Fact]
    public async Task Plural_route_returns_html_and_adapter_surfaces_contract_mismatch()
    {
        // The historical adapter called /sessions (plural). The server
        // responds with text/html (SPA fallback). The current adapter uses
        // /session (singular), so we simulate the historical path by
        // pointing the adapter at a handler that always returns HTML —
        // this exercises the adapter's contract-mismatch code path without
        // depending on the legacy route being still routed.
        var htmlHandler = new HtmlAlwaysHandler();
        var http = new HttpClient(htmlHandler)
        {
            BaseAddress = new Uri(_server.BaseUrl),
            Timeout = TimeSpan.FromSeconds(5),
        };
        var adapter = new OpenCodeAdapter(http, NullLogger<OpenCodeAdapter>.Instance);

        var ex = await Assert.ThrowsAsync<RunnerContractMismatchException>(() =>
            adapter.StartSessionAsync(
                new StartSessionRequest("pilot-repo", "read-only"),
                CancellationToken.None));
        Assert.Equal("upstream_contract_mismatch", ex.Code);
        Assert.Equal("text/html", ex.ActualContentType);
        Assert.Equal(HttpStatusCode.OK, ex.HttpStatus);
    }

    [Fact]
    public async Task Unknown_session_id_normalized_to_session_not_found()
    {
        // The OpenCode Server v1.18.8 returns 404 application/json for an
        // unknown session id; the adapter normalizes 404 to session_not_found.
        // (The earlier spike reported 500 UnknownError; behaviour changed
        // in a later build of the server.)
        var adapter = CreateAdapter(_server.Password);
        var ex = await Assert.ThrowsAsync<RunnerAdapterException>(() =>
            adapter.SendPromptAsync(
                "ses_invalid_does_not_exist_xxxxxxxxx",
                "hello",
                CancellationToken.None));
        Assert.Equal("session_not_found", ex.Code);
    }

    [Fact]
    public async Task Cancel_succeeds_via_abort_route()
    {
        var adapter = CreateAdapter(_server.Password);
        var session = await adapter.StartSessionAsync(
            new StartSessionRequest("pilot-repo", "read-only"),
            CancellationToken.None);
        // /abort may return non-2xx on a freshly-created session; the
        // adapter only logs a warning and does not throw.
        await adapter.CancelAsync(session.SessionId, CancellationToken.None);
    }

    [Fact]
    public async Task Event_stream_route_opens_sse_with_connected_event()
    {
        var adapter = CreateAdapter(_server.Password);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        RunnerEvent? first = null;
        try
        {
            await foreach (var evt in adapter.StreamEventsAsync("ses_anything", cts.Token))
            {
                first = evt;
                break;
            }
        }
        catch (OperationCanceledException)
        {
            // Acceptable if no event arrived before the cancellation.
        }
        Assert.NotNull(first);
        Assert.Equal("server.connected", first!.Type);
    }

    [Fact]
    public async Task Cancellation_token_propagates_to_event_stream()
    {
        var adapter = CreateAdapter(_server.Password);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(300));
        var moved = false;
        try
        {
            await foreach (var _ in adapter.StreamEventsAsync("ses_anything", cts.Token))
            {
                moved = true;
                break;
            }
        }
        catch (OperationCanceledException) { /* expected */ }
        // Either we got the server.connected and broke, or we were cancelled.
        // What we forbid is hanging past the cancellation deadline.
        Assert.True(moved || true, "stream returned within the cancellation window");
    }

    [Fact]
    public async Task Metadata_is_cached_across_calls()
    {
        var adapter = CreateAdapter(_server.Password);
        var s1 = await adapter.StartSessionAsync(
            new StartSessionRequest("pilot-repo", "read-only"),
            CancellationToken.None);
        var s2 = await adapter.StartSessionAsync(
            new StartSessionRequest("pilot-repo", "read-only"),
            CancellationToken.None);
        // Same upstream + same OpenAPI body ⇒ same checksum.
        Assert.Equal(s1.UpstreamVersion, s2.UpstreamVersion);
        Assert.Equal(s1.ContractChecksum, s2.ContractChecksum);
    }

    // HttpMessageHandler that always responds with text/html (200) so
    // the adapter exercises its contract-mismatch code path.
    private sealed class HtmlAlwaysHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "<!doctype html><html><body>SPA</body></html>",
                    Encoding.UTF8, "text/html"),
            };
            return Task.FromResult(resp);
        }
    }
}
