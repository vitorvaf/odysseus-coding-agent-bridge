namespace OcabBridge.Api.Domain;

// Persistent record of an OCAB execution. Mirrors the `runs` table
// created by db/migrations/V001__create_runs.sql (and db/init/01-schema.sql).
//
// POCO with public settable properties and no constructor — Dapper's
// IL materializer cannot bind positional primary-constructor parameters
// against snake_case columns that Postgres returns lowercased. With
// parameterless construction + property setters, Dapper maps columns
// to properties case-insensitively by name. State machine transitions
// and full lifecycle are deferred to slice 1.1.3 and later per
// backlog.md.

public sealed class Run
{
    public Guid RunId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RepositorySlug { get; set; }
    public string? Prompt { get; set; }
    public string? ResultJson { get; set; }
}
