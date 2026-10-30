#!/usr/bin/env bash
# Throwaway TimescaleDB and Dragonfly for test runs that need real servers: the session store and the run-lease,
# idempotency and signal tests. `up` starts both containers on loopback with random passwords and random host
# ports, waits until each accepts authenticated connections, writes two export lines to a mode 600 file, and prints
# only that file's path. `down` removes both containers and is idempotent. Podman only. No password is written to
# the repository.
#
# Concurrent runs: `up` begins with `down`, so two runs on one host that use the same container names remove each other's
# running containers in the middle of a test run (every test that needs a server then fails with a refused connection).
# AUTONOGRAMMER_TEST_SERVICES_NAME gives a run a pair of its own (autonogrammer-test-postgres-<name> and
# autonogrammer-test-dragonfly-<name>); `up` and `down` of that run touch only that pair. Unset, the names are the plain ones.
set -euo pipefail

NAME_SUFFIX="${AUTONOGRAMMER_TEST_SERVICES_NAME:-}"
if [ -n "$NAME_SUFFIX" ] && ! [[ "$NAME_SUFFIX" =~ ^[a-z0-9][a-z0-9-]{0,31}$ ]]; then
  echo "error: AUTONOGRAMMER_TEST_SERVICES_NAME must be 1 to 32 characters of a-z, 0-9 and -, starting with a letter or a digit" >&2
  exit 2
fi
POSTGRES_NAME="autonogrammer-test-postgres${NAME_SUFFIX:+-$NAME_SUFFIX}"
DRAGONFLY_NAME="autonogrammer-test-dragonfly${NAME_SUFFIX:+-$NAME_SUFFIX}"
POSTGRES_IMAGE=docker.io/timescale/timescaledb:2.13.1-pg16@sha256:1b2b5508848bbbbd671e02aeafd75fefb57771168f0782b6842bfb2f66af444b
DRAGONFLY_IMAGE=docker.io/dragonflydb/dragonfly:v1.27.1@sha256:385cb73078f4dabe43d536766842eab444f3d791b0fcf86951decdcbcda37917
DB=autonogrammer_test
DB_USER=autonogrammer
READY_BUDGET_SECONDS=90

# Prints the host port that Podman published for container port $2 of container $1.
host_port() {
  local mapping
  mapping="$(podman port "$1" "$2")"
  mapping="${mapping%%$'\n'*}"
  echo "${mapping##*:}"
}

# Sends AUTH then PING over a raw TCP connection. Succeeds only when both replies are +OK and +PONG.
dragonfly_ping() {
  local port="$1" password="$2" auth="" pong=""
  { exec 3<>"/dev/tcp/127.0.0.1/$port"; } 2>/dev/null || return 1
  printf 'AUTH %s\r\nPING\r\n' "$password" >&3
  IFS= read -r -t 5 auth <&3 || true
  IFS= read -r -t 5 pong <&3 || true
  exec 3>&-
  [ "$auth" = $'+OK\r' ] && [ "$pong" = $'+PONG\r' ]
}

# Usage: wait_ready <description> <command...>. Polls once per second within the readiness budget.
wait_ready() {
  local description="$1"
  shift
  local deadline=$((SECONDS + READY_BUDGET_SECONDS))
  until "$@" >/dev/null 2>&1; do
    if [ "$SECONDS" -ge "$deadline" ]; then
      echo "error: $description did not accept connections within $READY_BUDGET_SECONDS seconds" >&2
      return 1
    fi
    sleep 1
  done
}

down() {
  podman rm -f "$POSTGRES_NAME" >/dev/null 2>&1 || true
  podman rm -f "$DRAGONFLY_NAME" >/dev/null 2>&1 || true
}

up() {
  down
  local postgres_password dragonfly_password postgres_port dragonfly_port env_file
  postgres_password="$(openssl rand -hex 16)"
  dragonfly_password="$(openssl rand -hex 16)"

  # Passwords reach podman through its environment (-e NAME without a value), never through the argument list.
  POSTGRES_PASSWORD="$postgres_password" podman run -d --name "$POSTGRES_NAME" \
    -e POSTGRES_USER="$DB_USER" -e POSTGRES_DB="$DB" -e POSTGRES_PASSWORD \
    -p 127.0.0.1::5432 "$POSTGRES_IMAGE" >/dev/null
  DFLY_requirepass="$dragonfly_password" podman run -d --name "$DRAGONFLY_NAME" \
    -e DFLY_requirepass -p 127.0.0.1::6379 "$DRAGONFLY_IMAGE" --bind=0.0.0.0 >/dev/null

  postgres_port="$(host_port "$POSTGRES_NAME" 5432/tcp)"
  dragonfly_port="$(host_port "$DRAGONFLY_NAME" 6379/tcp)"

  # pg_isready over TCP inside the container: the timescale image's initialisation server listens on the Unix
  # socket only, so a TCP success means the final server is accepting connections.
  if ! wait_ready "test TimescaleDB" podman exec "$POSTGRES_NAME" pg_isready -h 127.0.0.1 -p 5432 -U "$DB_USER" -d "$DB"; then
    podman logs "$POSTGRES_NAME" >&2 || true
    down
    exit 1
  fi
  if ! wait_ready "test Dragonfly" dragonfly_ping "$dragonfly_port" "$dragonfly_password"; then
    podman logs "$DRAGONFLY_NAME" >&2 || true
    down
    exit 1
  fi

  env_file="$(mktemp "${TMPDIR:-/tmp}/autonogrammer-test-services.XXXXXX")"
  chmod 600 "$env_file"
  printf "export AUTONOGRAMMER_TEST_POSTGRES_CONNECTION='Host=127.0.0.1;Port=%s;Database=%s;Username=%s;Password=%s'\n" \
    "$postgres_port" "$DB" "$DB_USER" "$postgres_password" >>"$env_file"
  printf "export AUTONOGRAMMER_TEST_DRAGONFLY_CONNECTION='127.0.0.1:%s,password=%s,abortConnect=false'\n" \
    "$dragonfly_port" "$dragonfly_password" >>"$env_file"
  echo "$env_file"
}

case "${1:-}" in
  up) up ;;
  down) down ;;
  *)
    echo "usage: $0 up|down" >&2
    exit 2
    ;;
esac
