-- OCAB run_events table — slice 1.1.3 (OpenCode Read-Only VS).
-- Append-only log of Run state transitions and runner-emitted events.
-- Mirrors db/init/04-run-events.sql applied at postgres initdb.

CREATE TABLE IF NOT EXISTS run_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    run_id UUID NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
    sequence BIGSERIAL NOT NULL,
    from_state TEXT,
    to_state TEXT NOT NULL,
    actor TEXT NOT NULL,
    reason TEXT,
    metadata JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS run_events_run_id_idx ON run_events(run_id, sequence);
