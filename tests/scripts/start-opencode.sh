#!/bin/bash
# Starts OpenCode detached from the calling process (test runner) so
# it survives even when .NET Process.Start pipes / handles interfere with
# the binary's signal handling. Captures the PID in a file so the test
# fixture can kill it on teardown.
#
# Usage: start-opencode.sh <port> <pid-file> <log-file> <binary-path> <password>
set -e

PORT="$1"
PIDFILE="$2"
LOGFILE="$3"
BINARY="$4"
PASSWORD="$5"

if [ -z "$PORT" ] || [ -z "$PIDFILE" ] || [ -z "$LOGFILE" ] || [ -z "$BINARY" ] || [ -z "$PASSWORD" ]; then
  echo "usage: $0 <port> <pid-file> <log-file> <binary> <password>" >&2
  exit 2
fi

# Export the Basic Auth password so the runner enforces it. The bridge
# adapter sends `Authorization: Basic opencode:<value>` on every request.
export OPENCODE_SERVER_PASSWORD="$PASSWORD"

# Use setsid to detach from the test runner's process group / session.
# </dev/null + redirect stdout/stderr to log file (no pipe inheritance).
# --print-logs enables the binary's own structured logging.
setsid "$BINARY" serve \
    --hostname 127.0.0.1 \
    --port "$PORT" \
    --print-logs \
    </dev/null >"$LOGFILE" 2>&1 &

PID=$!
echo "$PID" > "$PIDFILE"
disown
