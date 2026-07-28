using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OcabBridge.Api.Adapters;

// HTTP adapter for the OpenCode Server. Designed for slice 1.1.3's
// read-only path: session is opened against a registered repository
// slug, the prompt is dispatched, and the events stream is consumed
// until a terminal event arrives. CancelAsync performs a soft cancel
// via the OpenCode /cancel endpoint and falls back to a process-level
// SIGTERM when the runner does not ACK within the bridge's grace window
// (the bridge orchestrator owns that timing).
//
// All endpoints assume the upstream contract documented in
// docs/discovery/003-opencode-container-poc.md. Any drift from that
// contract is a Slice 1.1.4 follow-up.

public sealed class OpenCodeAdapter : IRunnerAdapter
{
    public string AgentId => "opencode";

    private readonly HttpClient _http;
    private readonly ILogger<OpenCodeAdapter> _logger;

    public OpenCodeAdapter(HttpClient http, ILogger<OpenCodeAdapter> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<RunnerSession> StartSessionAsync(
        StartSessionRequest request,
        CancellationToken ct)
    {
        var path = $"/sessions?repository={Uri.EscapeDataString(request.RepositorySlug)}" +
                   $"&accessMode={Uri.EscapeDataString(request.AccessMode)}";

        using var resp = await _http.PostAsync(path, content: null, ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<StartSessionResponse>(cancellationToken: ct);
        return new RunnerSession(body?.SessionId ?? throw new InvalidOperationException("missing sessionId"),
            body?.Status ?? "ready");
    }

    public async Task SendPromptAsync(string sessionId, string prompt, CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync(
            $"/sessions/{Uri.EscapeDataString(sessionId)}/prompt",
            new { prompt },
            ct);
        resp.EnsureSuccessStatusCode();
    }

    public async IAsyncEnumerable<RunnerEvent> StreamEventsAsync(
        string sessionId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var url = $"/sessions/{Uri.EscapeDataString(sessionId)}/events";
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            if (ct.IsCancellationRequested) yield break;
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;
            yield return ParseEvent(line);
        }
    }

    public async Task CancelAsync(string sessionId, CancellationToken ct)
    {
        using var resp = await _http.PostAsync(
            $"/sessions/{Uri.EscapeDataString(sessionId)}/cancel",
            content: null,
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenCode cancel returned {Status}", resp.StatusCode);
        }
    }

    private static RunnerEvent ParseEvent(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() ?? "unknown"
                : "unknown";
            return new RunnerEvent(type, root.GetRawText(), DateTimeOffset.UtcNow);
        }
        catch (JsonException)
        {
            return new RunnerEvent("raw", line, DateTimeOffset.UtcNow);
        }
    }

    private sealed class StartSessionResponse
    {
        public string? SessionId { get; set; }
        public string? Status { get; set; }
    }
}
