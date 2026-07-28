using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OcabBridge.Api.Adapters;

// HTTP adapter for the OpenCode Server aligned to the v1.18.8 contract
// captured in docs/discovery/012-opencode-contract-spike.md and pinned
// in docs/adr/0017-pin-opencode-version.md.
//
// Endpoints used (all singular — see ADR-0017):
//   GET  /global/health                  liveness (NOT /health — SPA fallback)
//   POST /session                         create session
//   POST /session/{id}/prompt_async       send prompt async; bridge consumes /event SSE
//   GET  /event                           global SSE event stream
//   POST /session/{id}/abort              cancel session
//
// Error normalization (mapped to Run state by the dispatcher):
//   401                  → runner_auth_failed
//   404                  → session_not_found
//   409                  → session_conflict
//   429                  → runner_rate_limited
//   5xx                  → runner_unavailable
//   text/html on JSON    → upstream_contract_mismatch (RunnerContractMismatchException)
//
// The adapter lazily captures UpstreamVersion (from /global/health)
// and ContractChecksum (SHA-256 hex of the JSON body of /doc) on
// first use and exposes them in the returned RunnerSession. The
// dispatcher logs these on the Run for downstream auditing — see
// ADR-0017 § Tratamento de contract drift and Spec 005 § OCR-AC-009.
public sealed class OpenCodeAdapter : IRunnerAdapter
{
    public string AgentId => "opencode";

    private const string MediaJson = "application/json";
    private const string MediaHtml = "text/html";

    private readonly HttpClient _http;
    private readonly ILogger<OpenCodeAdapter> _logger;
    private RunnerMetadata? _metadata;
    private readonly SemaphoreSlim _metadataLock = new(1, 1);

    public OpenCodeAdapter(HttpClient http, ILogger<OpenCodeAdapter> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<RunnerSession> StartSessionAsync(
        StartSessionRequest request,
        CancellationToken ct)
    {
        await EnsureMetadataLoadedAsync(ct);

        var title = $"ocab-{SanitizeSlug(request.RepositorySlug)}-{SanitizeSlug(request.AccessMode)}";
        using var resp = await _http.PostAsJsonAsync(
            "/session",
            new { title },
            ct);

        await EnsureSuccessAsync(resp, "create_session", ct);
        var body = await resp.Content.ReadFromJsonAsync<StartSessionResponse>(cancellationToken: ct);

        return new RunnerSession(
            SessionId: body?.Id ?? throw new InvalidOperationException("missing session id"),
            Status: body?.Status ?? body?.Slug ?? "ready",
            UpstreamVersion: _metadata?.UpstreamVersion,
            ContractChecksum: _metadata?.ContractChecksum);
    }

    public async Task SendPromptAsync(string sessionId, string prompt, CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync(
            $"/session/{Uri.EscapeDataString(sessionId)}/prompt_async",
            new
            {
                parts = new[]
                {
                    new { type = "text", text = prompt }
                }
            },
            ct);

        await EnsureSuccessAsync(resp, "send_prompt", ct);
    }

    public async IAsyncEnumerable<RunnerEvent> StreamEventsAsync(
        string sessionId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Global SSE event stream. The server emits SSE events tagged with
        // type; the dispatcher forwards each as opaque JSON to RunEvent.
        using var resp = await _http.GetAsync(
            "/event",
            HttpCompletionOption.ResponseHeadersRead,
            ct);
        await EnsureSuccessAsync(resp, "subscribe_event", ct);

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            if (ct.IsCancellationRequested) yield break;
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var payload = line.Substring("data:".Length).Trim();
            yield return ParseEvent(payload);
        }
    }

    public async Task CancelAsync(string sessionId, CancellationToken ct)
    {
        using var resp = await _http.PostAsync(
            $"/session/{Uri.EscapeDataString(sessionId)}/abort",
            content: null,
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "OpenCode abort returned {Status} for session={SessionId}",
                resp.StatusCode, sessionId);
        }
    }

    // Lazy-loads UpstreamVersion and ContractChecksum from the runner on
    // first use. Cached in-process for the lifetime of the adapter.
    private async Task EnsureMetadataLoadedAsync(CancellationToken ct)
    {
        if (_metadata is not null) return;
        await _metadataLock.WaitAsync(ct);
        try
        {
            if (_metadata is not null) return;

            string? version = null;
            string? checksum = null;
            try
            {
                using var healthResp = await _http.GetAsync("/global/health", ct);
                await EnsureSuccessAsync(healthResp, "load_health", ct);
                var healthBody = await healthResp.Content.ReadFromJsonAsync<HealthResponse>(cancellationToken: ct);
                version = healthBody?.Version;

                using var docResp = await _http.GetAsync("/doc", ct);
                await EnsureSuccessAsync(docResp, "load_doc", ct);
                var docBytes = await docResp.Content.ReadAsByteArrayAsync(ct);
                checksum = Convert.ToHexString(SHA256.HashData(docBytes)).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "OpenCode metadata could not be loaded; adapter will run without UpstreamVersion/ContractChecksum");
            }

            _metadata = new RunnerMetadata(version ?? "unknown", checksum ?? "unavailable");
            _logger.LogInformation(
                "OpenCode metadata: version={Version} contractChecksum={Checksum}",
                _metadata.UpstreamVersion, _metadata.ContractChecksum);
        }
        finally
        {
            _metadataLock.Release();
        }
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage resp, string operation, CancellationToken ct)
    {
        var ctHeader = resp.Content.Headers.ContentType?.MediaType;

        // Contract drift: HTML response where JSON was expected. Detected
        // first so we surface the drift regardless of HTTP status (the
        // server sometimes returns 200 OK with HTML for SPA fallbacks).
        if (string.Equals(ctHeader, MediaHtml, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(
                "OpenCode contract drift on {Op}: returned {ContentType} status={Status}",
                operation, ctHeader ?? MediaHtml, resp.StatusCode);
            throw new RunnerContractMismatchException(
                operation,
                resp.StatusCode,
                ctHeader ?? MediaHtml,
                "OpenCode returned HTML instead of JSON");
        }

        if (!resp.IsSuccessStatusCode)
        {
            var code = NormalizeErrorCode(resp.StatusCode);
            var detail = await SafeReadErrorBodyAsync(resp, ct);
            _logger.LogError(
                "OpenCode {Op} failed status={Status} code={Code} detail={Detail}",
                operation, resp.StatusCode, code, detail);
            throw new RunnerAdapterException(code, operation, resp.StatusCode, detail);
        }

        // Validate Content-Type on success too: 2xx with non-JSON,
        // non-SSE Content-Type is contract drift (the SPA fallback can
        // come back as 200 OK + text/html when the upstream served a
        // stale route). Allow application/json (response body) and
        // text/event-stream (SSE for /event) but reject anything else.
        if (!string.IsNullOrEmpty(ctHeader)
            && !string.Equals(ctHeader, MediaJson, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(ctHeader, "text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(
                "OpenCode contract drift on {Op}: returned {ContentType} status={Status}",
                operation, ctHeader, resp.StatusCode);
            throw new RunnerContractMismatchException(
                operation,
                resp.StatusCode,
                ctHeader,
                $"OpenCode returned unexpected Content-Type {ctHeader}");
        }
    }

    private static string NormalizeErrorCode(HttpStatusCode status) => (int)status switch
    {
        401 => "runner_auth_failed",
        404 => "session_not_found",
        409 => "session_conflict",
        429 => "runner_rate_limited",
        >= 500 and < 600 => "runner_unavailable",
        _ => "runner_error",
    };

    private static async Task<string> SafeReadErrorBodyAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            const int max = 4096;
            return bytes.Length <= max
                ? Encoding.UTF8.GetString(bytes)
                : Encoding.UTF8.GetString(bytes, 0, max) + "…(truncated)";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SanitizeSlug(string? s) =>
        string.IsNullOrWhiteSpace(s)
            ? "unknown"
            : s.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');

    private static RunnerEvent ParseEvent(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() ?? "unknown"
                : "unknown";
            return new RunnerEvent(type, root.GetRawText(), DateTimeOffset.UtcNow);
        }
        catch (JsonException)
        {
            return new RunnerEvent("raw", json, DateTimeOffset.UtcNow);
        }
    }

    private sealed class StartSessionResponse
    {
        public string? Id { get; set; }
        public string? Slug { get; set; }
        public string? Status { get; set; }
    }

    private sealed class HealthResponse
    {
        public string? Version { get; set; }
    }
}

// Carries the upstream contract metadata recorded on each Run.
// See ADR-0017 § Tratamento de contract drift.
public sealed record RunnerMetadata(
    string UpstreamVersion,
    string ContractChecksum);
