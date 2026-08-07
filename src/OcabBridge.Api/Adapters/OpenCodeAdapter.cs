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

    public async Task<RunnerEventStream> OpenEventStreamAsync(
        string sessionId,
        CancellationToken ct)
    {
        // Per OpenCode v1.18.8, the SSE stream lives on EventApi at /event
        // and is scoped to the workspace via the `directory` query param.
        // The workspace equals the pilot repository for the POC.
        var workspaceDir = Environment.GetEnvironmentVariable("OCAB_OPENCODE_WORKSPACE_DIR");
        var url = "/event";
        if (!string.IsNullOrWhiteSpace(workspaceDir))
        {
            url += "?directory=" + Uri.EscapeDataString(workspaceDir);
        }
        var resp = await _http.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            ct);
        await EnsureSuccessAsync(resp, "open_event_stream", ct);

        // Connection is established here. Wrap the live HttpResponseMessage
        // into a RunnerEventStream handle that the dispatcher can iterate
        // after calling SendPromptAsync.
        return new RunnerEventStream(
            sessionId,
            innerCt => ReadSseEventsAsync(resp, innerCt));
    }

    public async Task<RunnerTerminalResult> WaitForTerminalResultAsync(
        string sessionId,
        TimeSpan pollInterval,
        CancellationToken cancellationToken)
    {
        // Terminal authority for OpenCode v1.18.8 lives in the session
        // message list, not the SSE stream. We poll GET
        // /session/{id}/message every pollInterval (100-250ms per the
        // STAB-005A.3 contract) and stop as soon as the last assistant
        // message reports finish="stop" or any error is present.
        if (pollInterval < TimeSpan.FromMilliseconds(100))
        {
            pollInterval = TimeSpan.FromMilliseconds(100);
        }
        if (pollInterval > TimeSpan.FromMilliseconds(250))
        {
            pollInterval = TimeSpan.FromMilliseconds(250);
        }

        RunnerTerminalResult? result = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            using var resp = await _http.GetAsync(
                $"/session/{Uri.EscapeDataString(sessionId)}/message",
                cancellationToken);
            await EnsureSuccessAsync(resp, "wait_for_terminal", cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            result = TryParseTerminalFromMessages(body);
            if (result is not null) return result;
            try { await Task.Delay(pollInterval, cancellationToken); }
            catch (OperationCanceledException) { throw; }
        }
        // Loop exits only when the caller's CT is cancelled. The Run
        // dispatcher's catch for OperationCanceledException will route
        // the cancellation to TimedOut/Cancelled; we return the last
        // observed state (or failure) so the caller has evidence.
        return result ?? new RunnerTerminalResult(
            false, null, "cancelled_before_terminal", string.Empty);
    }

    // Inspects the JSON array returned by GET /session/{id}/message and
    // returns a terminal result when the last assistant message is
    // finished (finish="stop" and no error) or reports an error.
    // Returns null when no terminal signal is present yet.
    private static RunnerTerminalResult? TryParseTerminalFromMessages(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            JsonElement? lastAssistantInfo = null;
            JsonElement? lastAssistantMessage = null;
            string? lastSessionId = null;
            foreach (var msg in doc.RootElement.EnumerateArray())
            {
                if (msg.ValueKind != JsonValueKind.Object) continue;
                if (!msg.TryGetProperty("info", out var info)
                    || info.ValueKind != JsonValueKind.Object) continue;
                if (!info.TryGetProperty("role", out var roleEl)
                    || roleEl.ValueKind != JsonValueKind.String) continue;
                if (!string.Equals(roleEl.GetString(), "assistant", StringComparison.Ordinal)) continue;
                if (info.TryGetProperty("sessionID", out var sidEl)
                    && sidEl.ValueKind == JsonValueKind.String)
                {
                    lastSessionId = sidEl.GetString();
                }
                lastAssistantInfo = info.Clone();
                lastAssistantMessage = msg.Clone();
            }
            if (lastAssistantInfo is null) return null;
            var root = lastAssistantInfo.Value;

            // Error signal wins over success regardless of finish value.
            // Only treat it as a terminal error when the error object
            // carries a non-empty `name` field — the v1.18.8 schema
            // may include an empty error placeholder on success paths.
            if (root.TryGetProperty("error", out var errEl)
                && errEl.ValueKind == JsonValueKind.Object
                && errEl.TryGetProperty("name", out var errNameEl)
                && errNameEl.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(errNameEl.GetString()))
            {
                var errJson = errEl.GetRawText();
                return new RunnerTerminalResult(false, null, errJson, body);
            }
            if (!root.TryGetProperty("finish", out var finishEl)
                || finishEl.ValueKind != JsonValueKind.String)
            {
                return null;
            }
            var finish = finishEl.GetString();
            if (!string.Equals(finish, "stop", StringComparison.Ordinal))
            {
                return null;
            }
            // Success: concatenate text parts and expose provider/model ids.
            // In v1.18.8 the parts[] lives at the message level (sibling
            // of info), not inside info. The text extraction therefore
            // reads from the message, not from the info object.
            var text = lastAssistantMessage is not null
                ? ExtractTextFromMessageParts(lastAssistantMessage.Value)
                : null;
            var providerId = root.TryGetProperty("providerID", out var pidEl)
                && pidEl.ValueKind == JsonValueKind.String
                ? pidEl.GetString()
                : null;
            var modelId = root.TryGetProperty("modelID", out var midEl)
                && midEl.ValueKind == JsonValueKind.String
                ? midEl.GetString()
                : null;
            var messageId = root.TryGetProperty("id", out var idEl)
                && idEl.ValueKind == JsonValueKind.String
                ? idEl.GetString()
                : null;
            var report = new Dictionary<string, object?>
            {
                ["text"] = text,
                ["sessionId"] = lastSessionId ?? "",
                ["providerId"] = providerId,
                ["modelId"] = modelId,
                ["messageId"] = messageId,
            };
            var reportJson = JsonSerializer.Serialize(report);
            return new RunnerTerminalResult(
                !string.IsNullOrEmpty(text),
                text,
                null,
                reportJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Concatenates the text of all parts where type == "text" in the
    // assistant message. The OpenCode v1.18.8 message shape places
    // the model output in parts[].text and the terminal metadata in
    // the last step-finish part. We only extract the text parts because
    // tool calls and step markers carry no payload.
    private static string? ExtractTextFromMessageParts(JsonElement message)
    {
        if (!message.TryGetProperty("parts", out var partsEl)
            || partsEl.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var sb = new System.Text.StringBuilder();
        foreach (var part in partsEl.EnumerateArray())
        {
            if (part.ValueKind != JsonValueKind.Object) continue;
            if (part.TryGetProperty("type", out var typeEl)
                && typeEl.ValueKind == JsonValueKind.String
                && string.Equals(typeEl.GetString(), "text", StringComparison.Ordinal)
                && part.TryGetProperty("text", out var textEl)
                && textEl.ValueKind == JsonValueKind.String)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(textEl.GetString());
            }
        }
        return sb.Length == 0 ? null : sb.ToString();
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
        using var resp = await _http.GetAsync(
            "/event",
            HttpCompletionOption.ResponseHeadersRead,
            ct);
        await EnsureSuccessAsync(resp, "subscribe_event", ct);

        await foreach (var evt in ReadSseEventsAsync(resp, ct).ConfigureAwait(false).WithCancellation(ct))
        {
            yield return evt;
        }
    }

    private static async IAsyncEnumerable<RunnerEvent> ReadSseEventsAsync(
        HttpResponseMessage resp,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);

        // The OpenCode v1.18.8 SSE handler closes the chunked stream
        // without sending the terminating zero-length chunk after the
        // assistant message is committed. Reading the response with
        // StreamReader.ReadLineAsync then blocks indefinitely waiting
        // for the next chunk and only throws HttpIOException when the
        // caller's HttpClient.Timeout fires. To avoid that, read raw
        // bytes with a per-read inactivity timeout; whenever the
        // timeout elapses without new bytes we treat the stream as
        // gracefully closed and yield break.
        var inactivity = TimeSpan.FromSeconds(30);
        var buffer = new System.IO.MemoryStream();
        var lineBuffer = new System.Text.StringBuilder();
        var lastByteAt = DateTimeOffset.UtcNow;

        while (true)
        {
            if (ct.IsCancellationRequested) yield break;
            var remaining = inactivity - (DateTimeOffset.UtcNow - lastByteAt);
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            using var inactivityCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            inactivityCts.CancelAfter(remaining == TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : remaining);
            var tmp = new byte[4096];
            int read;
            try
            {
                read = await stream.ReadAsync(tmp.AsMemory(0, tmp.Length), inactivityCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Inactivity timeout: treat as graceful shutdown. The
                // OpenCode v1.18.8 SSE handler has emitted the terminal
                // events and closed the stream; the next read would
                // otherwise block forever.
                yield break;
            }
            catch (HttpIOException) { yield break; }
            catch (IOException) { yield break; }
            if (read <= 0) yield break;
            lastByteAt = DateTimeOffset.UtcNow;
            buffer.Write(tmp, 0, read);
            buffer.Position = 0;
            for (int i = 0; i < read; i++)
            {
                int b = buffer.ReadByte();
                if (b < 0) break;
                if (b == '\n')
                {
                    var line = lineBuffer.ToString();
                    lineBuffer.Clear();
                    if (line.EndsWith('\r')) line = line.Substring(0, line.Length - 1);
                    if (!string.IsNullOrWhiteSpace(line)
                        && line.StartsWith("data:", StringComparison.Ordinal))
                    {
                        var payload = line.Substring("data:".Length).Trim();
                        yield return ParseEvent(payload);
                    }
                }
                else
                {
                    lineBuffer.Append((char)b);
                }
            }
            // Drain everything we have already consumed from the buffer.
            var leftover = (int)(buffer.Length - buffer.Position);
            if (leftover > 0)
            {
                var keep = new byte[leftover];
                for (int i = 0; i < leftover; i++) keep[i] = (byte)buffer.ReadByte();
                buffer.SetLength(0);
                buffer.Write(keep, 0, leftover);
            }
            else
            {
                buffer.SetLength(0);
            }
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
