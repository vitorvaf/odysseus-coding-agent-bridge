namespace OcabBridge.Api.Domain;

// Persistent record of a registered repository. Mirrors the `repositories`
// table created by db/migrations/V002__create_repositories.sql and the
// seed file db/init/03-seed-pilot.sql.
//
// Slug is the canonical identifier used by run_create, repository_list,
// and any future API surface (ADR-0014). Physical paths are intentionally
// absent from this slice; the URL mapping is deferred to a follow-up.
//
// POCO with public settable properties and no constructor — see the
// rationale on Domain/Run.cs.

public sealed class Repository
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = "main";
    public bool Writable { get; set; } = true;
    public string[] AllowedAgents { get; set; } = Array.Empty<string>();
    public bool ReadOnlyOnly { get; set; }
    public string ValidationsJson { get; set; } = "[]";
    public string PoliciesJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}
