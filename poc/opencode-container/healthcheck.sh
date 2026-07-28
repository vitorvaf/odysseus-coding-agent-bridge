#!/bin/sh
# OCAB OpenCode Runner — liveness probe.
# Mirrors the in-Dockerfile HEALTHCHECK so compose can use it directly
# when the operator runs the image outside Compose for ad-hoc checks.

set -eu
wget --no-verbose --tries=1 --spider "http://127.0.0.1:${OPENCODE_PORT:-4096}/health" \
  || { echo "opencode-runner: /health probe failed" >&2; exit 1; }
echo "opencode-runner: ok"
