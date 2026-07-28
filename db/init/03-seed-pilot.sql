-- OCAB seed data — applied after 02-repositories.sql.
-- Idempotent: re-running the seed is a no-op (ON CONFLICT DO NOTHING).

INSERT INTO repositories (slug, display_name, default_branch, writable, allowed_agents, read_only_only, validations, policies)
VALUES
  ('ocab-pilot',
   'OCAB Pilot (fixture local)',
   'main',
   TRUE,
   ARRAY['opencode']::TEXT[],
   FALSE,
   '[]'::jsonb,
   '{"requireReview": false, "maxFilesChanged": 5, "readOnlyOnly": false}'::jsonb),
  ('docs-site',
   'Documentation Site',
   'main',
   TRUE,
   ARRAY['opencode']::TEXT[],
   FALSE,
   '[]'::jsonb,
   '{}'::jsonb)
ON CONFLICT (slug) DO NOTHING;
