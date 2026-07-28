using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using OcabBridge.Api.Adapters;
using OcabBridge.Api.Domain;

namespace OcabBridge.TestSupport;

// Deterministic IRunnerAdapter for SLICE-STAB-003 E2E tests.
//
// Substitui o adapter (não o OpenCode real) para validar o caminho
// coordinator + dispatcher + adapter chain ponta a ponta sem depender
// de credencial LLM real. O OpenCode real continua testado em outros
// cenários (cancelamento, timeout, erro de provider) pelos testes em
// OcabBridge.IntegrationTests.EndToEndLifecycleTests que sobem o
// runner real via OpenCodeTestServer.
//
// Cenários controláveis via SetScenario:
//   - normal:  StartSessionAsync retorna "ses_<uuid>"; SendPromptAsync
//              retorna imediatamente; StreamEventsAsync emite um
//              evento "done" com payload "hello from mock".
//   - slow:    SendPromptAsync segura por SlowDelayMs antes de retornar
//              (testa timeout do Run).
//   - blocked: StreamEventsAsync segura indefinidamente (testa
//              cancelamento via coordinator.CancelAsync).
//   - error:   StartSessionAsync lança RunnerAdapterException com code
//              "runner_unavailable" (testa Failed).
//   - invalid: StartSessionAsync lança RunnerContractMismatchException
//              com ActualContentType = "text/html" (testa
//              UpstreamContractMismatch).
//
// Métodos thread-safe via ConcurrentDictionary para activeSessions.

public sealed class MockRunnerAdapter : IRunnerAdapter
{
    private readonly ConcurrentDictionary<string, MockSession> _activeSessions = new();
    private readonly ILogger<MockRunnerAdapter>? _logger;

    public string AgentId => "mock";

    public string ActiveScenario { get; private set; } = "normal";

    public TimeSpan SlowDelayMs { get; set; } = TimeSpan.FromSeconds(10);

    public IReadOnlyCollection<string> ActiveSessionIds => _activeSessions.Keys.ToArray();

    public MockRunnerAdapter(ILogger<MockRunnerAdapter>? logger = null)
    {
        _logger = logger;
    }

    public void SetScenario(string scenario) => ActiveScenario = scenario;

    public Task<RunnerSession> StartSessionAsync(
        StartSessionRequest request,
        CancellationToken ct)
    {
        switch (ActiveScenario)
        {
            case "error":
                _logger?.LogInformation("MockRunnerAdapter StartSessionAsync: error scenario");
                throw new RunnerAdapterException(
                    "runner_unavailable",
                    "start_session",
                    System.Net.HttpStatusCode.BadGateway,
                    "mock provider upstream_failure");
            case "invalid":
                _logger?.LogInformation("MockRunnerAdapter StartSessionAsync: invalid scenario");
                throw new RunnerContractMismatchException(
                    "start_session",
                    System.Net.HttpStatusCode.OK,
                    "text/html",
                    "mock returned text/html instead of json");
        }

        var session = new MockSession(
            SessionId: $"ses_{Guid.NewGuid():N}",
            UpstreamVersion: "1.18.8-mock",
            ContractChecksum: "deadbeef" + new string('0', 56));
        _activeSessions[session.SessionId] = session;
        _logger?.LogInformation("MockRunnerAdapter StartSessionAsync: created {SessionId}", session.SessionId);
        return Task.FromResult<RunnerSession>(new RunnerSession(
            SessionId: session.SessionId,
            Status: "ready",
            UpstreamVersion: session.UpstreamVersion,
            ContractChecksum: session.ContractChecksum));
    }

    public async Task SendPromptAsync(
        string sessionId,
        string prompt,
        CancellationToken ct)
    {
        if (!_activeSessions.ContainsKey(sessionId))
        {
            throw new RunnerAdapterException(
                "session_not_found",
                "send_prompt",
                System.Net.HttpStatusCode.NotFound,
                $"mock session {sessionId} not found");
        }
        if (ActiveScenario == "slow")
        {
            _logger?.LogInformation("MockRunnerAdapter SendPromptAsync: slow scenario, sleeping {Ms}ms",
                SlowDelayMs.TotalMilliseconds);
            try { await Task.Delay(SlowDelayMs, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
        }
        _logger?.LogInformation("MockRunnerAdapter SendPromptAsync sessionId={SessionId}", sessionId);
    }

    public async IAsyncEnumerable<RunnerEvent> StreamEventsAsync(
        string sessionId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!_activeSessions.ContainsKey(sessionId))
        {
            throw new RunnerAdapterException(
                "session_not_found",
                "stream_events",
                System.Net.HttpStatusCode.NotFound,
                $"mock session {sessionId} not found");
        }

        switch (ActiveScenario)
        {
            case "blocked":
                _logger?.LogInformation("MockRunnerAdapter StreamEventsAsync: blocked scenario, holding until cancellation");
                try { await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { yield break; }
                yield break;
            case "error":
                yield return new RunnerEvent("error", "{\"error\":\"mock upstream_failure\"}", DateTimeOffset.UtcNow);
                yield break;
        }

        // normal: emit one server.connected event, then a done event
        yield return new RunnerEvent(
            "server.connected",
            $"{{\"id\":\"evt_{Guid.NewGuid():N}\",\"type\":\"server.connected\"}}",
            DateTimeOffset.UtcNow);

        // Brief delay so cancel/timeout have a chance to fire if the
        // test exercises them mid-stream.
        try { await Task.Delay(50, ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { yield break; }

        yield return new RunnerEvent(
            "done",
            "{\"id\":\"chatcmpl-mock-001\",\"object\":\"chat.completion\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"hello from mock runner\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":4,\"total_tokens\":5}}",
            DateTimeOffset.UtcNow);
    }

    public Task CancelAsync(string sessionId, CancellationToken ct)
    {
        if (_activeSessions.TryRemove(sessionId, out _))
        {
            _logger?.LogInformation("MockRunnerAdapter CancelAsync sessionId={SessionId}", sessionId);
        }
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _activeSessions.Clear();
        ActiveScenario = "normal";
    }

    // Mirrors the upstream OpenCodeAdapter metadata pattern. Cannot
    // derive from RunnerSession (sealed record) so we hold the
    // SessionId separately and let StartSessionAsync return a freshly
    // constructed RunnerSession.
    private sealed record MockSession(
        string SessionId,
        string UpstreamVersion,
        string ContractChecksum);
}
