#!/usr/bin/env bash
# Creates the Autonogrammer telemetry database in a GreptimeDB (ADR-0031 D4): `CREATE DATABASE IF NOT EXISTS autonogrammer
# WITH (ttl='30d')`, over the HTTP SQL endpoint. Idempotent. It touches only the `autonogrammer` database and never
# the shared default database, whose tables belong to other tenants of the same server.
#
# Usage: bootstrap-greptime.sh [base-url]
#   base-url  GreptimeDB HTTP address; defaults to $GREPTIME_URL, then the local compose stack (http://127.0.0.1:4000).
# Set GREPTIME_BASIC_AUTH to `user:password` when the server requires authentication (hosting only; read from the
# environment, never from the argument list, never printed).
set -euo pipefail

base_url="${1:-${GREPTIME_URL:-http://127.0.0.1:4000}}"
base_url="${base_url%/}"

sql="CREATE DATABASE IF NOT EXISTS autonogrammer WITH (ttl='30d')"

# The credential reaches curl through a config on stdin (-K -), so it never appears in the process list.
auth_config=""
if [ -n "${GREPTIME_BASIC_AUTH:-}" ]; then
  auth_config="user = \"${GREPTIME_BASIC_AUTH}\""
fi

response="$(printf '%s\n' "$auth_config" | curl --silent --show-error --fail-with-body --max-time 30 \
  -K - -X POST "$base_url/v1/sql" --data-urlencode "sql=$sql")" || {
  echo "error: GreptimeDB at $base_url did not accept the statement" >&2
  exit 1
}

# A 200 answer can still carry a SQL error in the JSON body (`"code"` other than 0).
if printf '%s' "$response" | grep -Eq '"code"[[:space:]]*:[[:space:]]*[1-9]'; then
  echo "error: GreptimeDB rejected the statement: $response" >&2
  exit 1
fi

echo "database autonogrammer ready (ttl 30d) at $base_url"
