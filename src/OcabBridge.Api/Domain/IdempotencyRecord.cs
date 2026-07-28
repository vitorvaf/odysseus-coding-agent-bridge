namespace OcabBridge.Api.Domain;

// Idempotency mapping persisted across run_create invocations.
// Mirrors the `idempotency_keys` table created by
// db/migrations/V004__create_idempotency_keys.sql.
//
// `request_hash` is the SHA-256 hex string of the canonical request body
// (repository slug + readOnly flag + prompt). A repeat invocation with
// the same `key` and the same `request_hash` returns the stored `RunId`;
// a repeat with the same `key` but a different `request_hash` raises
// 409 idempotency_conflict per docs/specs/004-mcp-contract/spec.md.
//
// POCO with public settable properties and no constructor — see the
// rationale on Domain/Run.cs.

public sealed class IdempotencyRecord
{
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public Guid RunId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
