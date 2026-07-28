namespace OcabBridge.Api.Domain;

// Persistent record of an agent adapter registered with the bridge.
// Mirrors the `agents` table created by db/migrations/V005__create_agents.sql
// and seeded by db/init/06-agents.sql.
//
// `endpoint` is the runner URL used by IRunnerAdapter implementations.
// ADR-0014-style identification: agents are referenced by name (slug);
// no physical paths are accepted.
public sealed record Agent(
    Guid Id,
    string Name,
    string DisplayName,
    string Version,
    bool Enabled,
    string[] Capabilities,
    string Endpoint,
    DateTimeOffset CreatedAt);
