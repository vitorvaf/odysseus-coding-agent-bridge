namespace OcabBridge.Api.Domain;

// Append-only event row for a Run. Mirrors the `run_events` table
// created by db/migrations/V003__create_run_events.sql.
//
// POCO with public settable properties and no constructor — same
// rationale as Domain/Run.cs: Dapper's IL materializer binds cleanly
// against property names when records are not in play.

public sealed class RunEvent
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public long Sequence { get; set; }
    public string? FromState { get; set; }
    public string ToState { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
