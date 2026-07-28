-- OCAB agents table — applied by postgres initdb on first container start.

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
