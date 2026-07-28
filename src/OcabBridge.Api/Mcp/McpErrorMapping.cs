namespace OcabBridge.Api.Mcp;

// Standardized error codes carried in MCP error responses. Mirrors the
// codes documented in docs/contracts/errors.md and the slice-level
// guidance in docs/specs/004-mcp-contract/spec.md.
public static class McpErrorCodes
{
    public const string InvalidPayload = "invalid_payload";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string ValidationFailed = "validation_failed";
    public const string DependencyUnavailable = "dependency_unavailable";
    public const string Internal = "internal_error";
}

public sealed record McpError(string Code, string Message, object? Details = null);
