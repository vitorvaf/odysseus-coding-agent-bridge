-- OCAB repositories table — applied by postgres initdb on first container start.
-- Mirrors db/migrations/V002__create_repositories.sql.

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
