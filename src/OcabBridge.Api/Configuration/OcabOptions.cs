namespace OcabBridge.Api.Configuration;

// Typed options bound from configuration section "Ocab".
// See appsettings.json. Values prefixed with __SET_ME__ must be supplied
// at runtime via environment variables (OCAB_MCP_TOKEN, OCAB__McpToken).
public sealed class OcabOptions
{
    public const string SectionName = "Ocab";

    public string? McpToken { get; set; }
}
