namespace OcabBridge.Api.Domain;

// Persistent record of an agent adapter registered with the bridge.
// Mirrors the `agents` table created by db/migrations/V005__create_agents.sql
// and seeded by db/init/06-agents.sql.
//
// `endpoint` is the runner URL used by IRunnerAdapter implementations.
// ADR-0014-style identification: agents are referenced by name (slug);
// no physical paths are accepted.
//
// POCO with public settable properties and no constructor — see the
// rationale on Domain/Run.cs.

public sealed class Agent
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Version { get; set; } = "0.0.0";
    public bool Enabled { get; set; } = true;
    public string[] Capabilities { get; set; } = Array.Empty<string>();
    public string Endpoint { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
