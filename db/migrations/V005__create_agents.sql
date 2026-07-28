-- OCAB agents table — slice 1.1.4 (MCP Contract Completion).
-- Registry of agent adapters reachable from the bridge. The seed row
-- lists the OpenCode runner wired in slice 1.1.3. Codex and Antigravity
-- entries are NOT seeded at this stage; the bridge refuses to dispatch
-- to an agent whose row is not present (or whose enabled flag is false).
-- Mirrors db/init/06-agents.sql.

CREATE TABLE IF NOT EXISTS agents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT UNIQUE NOT NULL,
    display_name TEXT NOT NULL,
    version TEXT NOT NULL DEFAULT '0.0.0',
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    capabilities TEXT[] NOT NULL DEFAULT ARRAY[]::TEXT[],
    endpoint TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS agents_name_idx ON agents(name);

INSERT INTO agents (name, display_name, version, enabled, capabilities, endpoint)
VALUES (
  'opencode',
  'OpenCode',
  '1.x',
  TRUE,
  ARRAY['plan', 'implement', 'review', 'document']::TEXT[],
  'http://ocab-opencode-runner:4096'
)
ON CONFLICT (name) DO NOTHING;
