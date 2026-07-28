-- OCAB runs.timeout_seconds — versioned migration (SLICE-STAB-003 / ADR-0018).
-- Adds the per-Run timeout that drives RunQueueWorker's
-- CancellationTokenSource.CancelAfter(timeoutSeconds).

ALTER TABLE runs
    ADD COLUMN IF NOT EXISTS timeout_seconds INT NOT NULL DEFAULT 300;

-- Backfill: any existing rows get the default 300. The dispatcher
-- only honours per-Run timeout when a row was inserted with an
-- explicit value (CreateAsync sets Run.TimeoutSeconds from the
-- payload, defaulting to 300 when the payload omits it).
