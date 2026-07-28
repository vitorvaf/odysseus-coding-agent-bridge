using System.Text;
using System.Text.Json;

namespace OcabBridge.Api.Mcp;

// Opaque cursor encoder/decoder for paginated MCP tools.
// The wire format is base64url(JSON { offset }) — deterministic, opaque
// to the client, and cheap to validate. Page size is NOT part of the
// cursor; clients pass it explicitly per docs/specs/004-mcp-contract/spec.md.
public static class Pagination
{
    public sealed record Cursor(int Offset);

    public static string Encode(Cursor cursor)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(cursor);
        return Convert.ToBase64String(json)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static Cursor? Decode(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var normalized = token.Replace('-', '+').Replace('_', '/');
            switch (normalized.Length % 4)
            {
                case 2: normalized += "=="; break;
                case 3: normalized += "="; break;
            }
            var bytes = Convert.FromBase64String(normalized);
            var cursor = JsonSerializer.Deserialize<Cursor>(bytes);
            return cursor is null ? null : cursor.Offset < 0 ? null : cursor;
        }
        catch (FormatException) { return null; }
        catch (JsonException) { return null; }
    }
}
