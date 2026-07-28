namespace OcabBridge.Api.Domain;

// Append-only event row for a Run. Mirrors the `run_events` table
// created by db/migrations/V003__create_run_events.sql.
//
// `sequence` is monotonic per `run_id` (BIGSERIAL); it is the source of
// truth for ordering, not `created_at`. `metadata` is a free-form JSON
// blob carrying runner-emitted details, error info, etc.
public sealed record RunEvent(
    Guid Id,
    Guid RunId,
    long Sequence,
    string? FromState,
    string ToState,
    string Actor,
    string? Reason,
    string? MetadataJson,
    DateTimeOffset CreatedAt);
