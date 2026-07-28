namespace OcabBridge.Api.Domain;

// Persistent record of an OCAB execution. Mirrors the `runs` table created
// by db/migrations/V001__create_runs.sql (and db/init/01-schema.sql).
// State machine transitions, timeouts, and full lifecycle are deferred
// to slice 1.1.3 (OpenCode Read-Only Vertical Slice) per backlog.md.
public sealed record Run(
    Guid RunId,
    DateTimeOffset CreatedAt,
    string Status,
    string? RepositorySlug,
    string? Prompt,
    string? ResultJson);
