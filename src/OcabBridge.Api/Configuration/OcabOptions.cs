namespace OcabBridge.Api.Configuration;

// Typed options bound from configuration section "Ocab".
// See appsettings.json. Values prefixed with __SET_ME__ must be supplied
// at runtime via environment variables (OCAB_MCP_TOKEN, OCAB__McpToken).
public sealed class OcabOptions
{
    public const string SectionName = "Ocab";

    public string? McpToken { get; set; }

    // SLICE-STAB-002 / ADR-0017: Basic Auth password shared with the
    // OpenCode runner container (OPENCODE_SERVER_PASSWORD). If null or
    // empty, no Authorization header is sent — the upstream is then
    // expected to be running with OPENCODE_SERVER_PASSWORD unset
    // (unsecured dev mode).
    public string? OpenCodePassword { get; set; }
}
