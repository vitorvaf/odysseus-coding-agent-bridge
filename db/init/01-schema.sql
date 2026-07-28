-- OCAB runs table — applied by postgres initdb on first container start.
-- Mirrors db/migrations/V001__create_runs.sql.

CREATE TABLE IF NOT EXISTS runs (
    run_id UUID PRIMARY KEY,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    finished_at TIMESTAMPTZ,
    status TEXT NOT NULL,
    repository_slug TEXT,
    prompt TEXT,
    result JSONB
);

CREATE INDEX IF NOT EXISTS runs_created_at_idx ON runs(created_at DESC);
CREATE INDEX IF NOT EXISTS runs_repository_slug_idx ON runs(repository_slug);
