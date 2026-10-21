#!/usr/bin/env bash
# Throwaway PostgreSQL for test runs that need a real server: the API session-store and workflow-enabled host tests.
# `up` starts one container on loopback with a random password and prints an export line for
# OMPOS_TEST_POSTGRES_CONNECTION. `down` removes it. Podman only. The password is never written to the repository.
set -euo pipefail

NAME=ompos-test-postgres
DB=ompos_test
DB_USER=ompos
IMAGE=docker.io/library/postgres:17

case "${1:-}" in
  up)
    podman rm -f "$NAME" >/dev/null 2>&1 || true
    PASSWORD="$(openssl rand -hex 16)"
    podman run -d --name "$NAME" \
      -e POSTGRES_USER="$DB_USER" -e POSTGRES_DB="$DB" -e POSTGRES_PASSWORD="$PASSWORD" \
      -p 127.0.0.1::5432 "$IMAGE" >/dev/null
    MAPPING="$(podman port "$NAME" 5432/tcp | head -n 1)"
    PORT="${MAPPING##*:}"
    # The image restarts its server once after initialisation. Wait for a TCP round trip from inside the container.
    for _ in $(seq 1 90); do
      if podman exec -e PGPASSWORD="$PASSWORD" "$NAME" psql -h 127.0.0.1 -U "$DB_USER" -d "$DB" -tAc 'select 1' >/dev/null 2>&1; then
        printf "export OMPOS_TEST_POSTGRES_CONNECTION='Host=127.0.0.1;Port=%s;Database=%s;Username=%s;Password=%s'\n" \
          "$PORT" "$DB" "$DB_USER" "$PASSWORD"
        exit 0
      fi
      sleep 1
    done
    echo "error: test PostgreSQL did not accept TCP connections within 90 seconds" >&2
    podman logs "$NAME" >&2 || true
    podman rm -f "$NAME" >/dev/null 2>&1 || true
    exit 1
    ;;
  down)
    podman rm -f "$NAME" >/dev/null 2>&1 || true
    ;;
  *)
    echo "usage: $0 up|down" >&2
    exit 2
    ;;
esac
