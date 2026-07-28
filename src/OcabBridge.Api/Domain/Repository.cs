namespace OcabBridge.Api.Domain;

// Persistent record of a registered repository. Mirrors the `repositories`
// table created by db/migrations/V002__create_repositories.sql and the
// seed file db/init/03-seed-pilot.sql.
//
// Slug is the canonical identifier used by run_create, repository_list,
// and any future API surface (ADR-0014). Physical paths are intentionally
// absent from this slice; the URL mapping is deferred to a follow-up.
public sealed record Repository(
    Guid Id,
    string Slug,
    string DisplayName,
    string DefaultBranch,
    bool Writable,
    string[] AllowedAgents,
    bool ReadOnlyOnly,
    string ValidationsJson,
    string PoliciesJson,
    DateTimeOffset CreatedAt);
