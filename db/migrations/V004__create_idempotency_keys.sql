-- OCAB idempotency_keys table — slice 1.1.4 (MCP Contract Completion).
-- Stores the mapping from caller-supplied idempotencyKey to the runId
-- produced by run_create. Mirrors db/init/05-idempotency-keys.sql which
-- is applied by postgres initdb on first container start.

CREATE TABLE IF NOT EXISTS idempotency_keys (
    key TEXT PRIMARY KEY,
    request_hash TEXT NOT NULL,
    run_id UUID NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() + INTERVAL '24 hours')
);

CREATE INDEX IF NOT EXISTS idempotency_keys_run_id_idx ON idempotency_keys(run_id);
CREATE INDEX IF NOT EXISTS idempotency_keys_expires_at_idx ON idempotency_keys(expires_at);
