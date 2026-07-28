-- OCAB repositories table — versioned migration (slice 1.1.2 — Repository Registry).
-- Mirrors db/init/02-repositories.sql which is applied by postgres initdb
-- on first container start.
--
-- Repositories are referenced only by their deterministic slug
-- (ADR-0014). Physical paths are NOT stored; runners fetch the canonical
-- URL via repositories.url_canonical elsewhere (deferred — this slice
-- registers the row shape, not the URL mapping).

CREATE TABLE IF NOT EXISTS repositories (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    slug TEXT UNIQUE NOT NULL,
    display_name TEXT NOT NULL,
    default_branch TEXT NOT NULL DEFAULT 'main',
    writable BOOLEAN NOT NULL DEFAULT TRUE,
    allowed_agents TEXT[] NOT NULL DEFAULT ARRAY['opencode']::TEXT[],
    read_only_only BOOLEAN NOT NULL DEFAULT FALSE,
    validations JSONB NOT NULL DEFAULT '[]'::jsonb,
    policies JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS repositories_slug_idx ON repositories(slug);
