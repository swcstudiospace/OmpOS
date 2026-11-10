#!/usr/bin/env bash
# Throwaway TimescaleDB and Dragonfly for test runs that need real servers: the session store and the run-lease,
# idempotency and signal tests. `up` starts both containers on loopback with random passwords and random host
# ports, waits until each accepts authenticated connections, writes two export lines to a mode 600 file, and prints
# only that file's path. `up --with-greptime` also starts a throwaway GreptimeDB (no authentication, loopback only),
# waits for its /health and adds AUTONOGRAMMER_TEST_GREPTIME_URL to the file. Plain `up` is what bootstrap.sh and CI
# call. `down` requires the run's export file (TEST_SERVICES_ENV) or its sourced ownership token.
# Same-name containers are collisions, never replacements. Use AUTONOGRAMMER_TEST_SERVICES_NAME
# to give concurrent runs distinct names; unset, the names are the plain ones.
set -euo pipefail

NAME_SUFFIX="${AUTONOGRAMMER_TEST_SERVICES_NAME:-}"
if [ -n "$NAME_SUFFIX" ] && ! [[ "$NAME_SUFFIX" =~ ^[a-z0-9][a-z0-9-]{0,31}$ ]]; then
  echo "error: AUTONOGRAMMER_TEST_SERVICES_NAME must be 1 to 32 characters of a-z, 0-9 and -, starting with a letter or a digit" >&2
  exit 2
fi
POSTGRES_NAME="autonogrammer-test-postgres${NAME_SUFFIX:+-$NAME_SUFFIX}"
DRAGONFLY_NAME="autonogrammer-test-dragonfly${NAME_SUFFIX:+-$NAME_SUFFIX}"
GREPTIME_NAME="autonogrammer-test-greptimedb${NAME_SUFFIX:+-$NAME_SUFFIX}"
POSTGRES_IMAGE=docker.io/timescale/timescaledb:2.13.1-pg16@sha256:1b2b5508848bbbbd671e02aeafd75fefb57771168f0782b6842bfb2f66af444b
DRAGONFLY_IMAGE=docker.io/dragonflydb/dragonfly:v1.27.1@sha256:385cb73078f4dabe43d536766842eab444f3d791b0fcf86951decdcbcda37917
GREPTIME_IMAGE=docker.io/greptime/greptimedb:v1.1.4@sha256:9726587eac95d0360755254cd59a528dbf48abfdf268478aea6a644f62afe44c
DB=autonogrammer_test
DB_USER=autonogrammer
READY_BUDGET_SECONDS=90
OWNER_LABEL=org.autonogrammer.test-services
OWNER_VALUE="${AUTONOGRAMMER_TEST_SERVICES_OWNER:-}"

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

# Succeeds when GreptimeDB answers GET /health with 200 on the loopback host port $1.
greptime_health() {
  local port="$1" status=""
  { exec 3<>"/dev/tcp/127.0.0.1/$port"; } 2>/dev/null || return 1
  printf 'GET /health HTTP/1.0\r\nHost: localhost\r\n\r\n' >&3
  IFS= read -r -t 5 status <&3 || true
  exec 3>&-
  [[ "$status" == *" 200 "* ]]
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

# Podman distinguishes a missing container (1) from an engine or inspection error.
container_exists() {
  local status
  if podman container exists "$1"; then
    return 0
  else
    status=$?
    if [ "$status" -eq 1 ]; then return 1; fi
    echo "error: cannot determine whether container $1 exists" >&2
    return 2
  fi
}

# Remove by immutable container ID so a reused name cannot target a foreign container.
remove_owned() {
  local name="$1" details id owner status
  if container_exists "$name"; then
    :
  else
    status=$?
    [ "$status" -eq 1 ] && return 0
    return "$status"
  fi
  if ! details="$(podman inspect --format '{{.Id}} {{index .Config.Labels "org.autonogrammer.test-services"}}' "$name" 2>/dev/null)"; then
    echo "error: cannot inspect container $name" >&2
    return 1
  fi
  read -r id owner <<<"$details"
  if [[ "$OWNER_VALUE" =~ ^[0-9a-f]{64}$ ]] && [ "$owner" = "$OWNER_VALUE" ] && [[ "$id" =~ ^[0-9a-f]{64}$ ]]; then
    podman rm -f "$id" >/dev/null
  else
    echo "error: refusing to remove unowned container $name" >&2
    return 1
  fi
}

down() {
  local failed=0
  remove_owned "$POSTGRES_NAME" || failed=1
  remove_owned "$DRAGONFLY_NAME" || failed=1
  remove_owned "$GREPTIME_NAME" || failed=1
  return "$failed"
}

up() (
  local with_greptime="$1" name status success=0 env_file=""
  local postgres_password dragonfly_password postgres_port dragonfly_port greptime_port=""
  # Check all names, including optional Greptime, without ever removing an existing container.
  for name in "$POSTGRES_NAME" "$DRAGONFLY_NAME" "$GREPTIME_NAME"; do
    if container_exists "$name"; then
      echo "error: container $name already exists" >&2
      return 1
    else
      status=$?
      [ "$status" -eq 1 ] || return "$status"
    fi
  done
  OWNER_VALUE="$(openssl rand -hex 32)"
  [[ "$OWNER_VALUE" =~ ^[0-9a-f]{64}$ ]] || return 1
  # On any failure, remove only containers carrying this invocation's token.
  trap 'if [ "$success" -ne 1 ]; then down || true; [ -z "$env_file" ] || rm -f "$env_file"; fi' EXIT
  postgres_password="$(openssl rand -hex 16)"
  dragonfly_password="$(openssl rand -hex 16)"

  # Passwords reach podman through its environment (-e NAME without a value), never through the argument list.
  POSTGRES_PASSWORD="$postgres_password" podman run -d --name "$POSTGRES_NAME" --label "$OWNER_LABEL=$OWNER_VALUE" \
    -e POSTGRES_USER="$DB_USER" -e POSTGRES_DB="$DB" -e POSTGRES_PASSWORD \
    -p 127.0.0.1::5432 "$POSTGRES_IMAGE" >/dev/null
  DFLY_requirepass="$dragonfly_password" podman run -d --name "$DRAGONFLY_NAME" --label "$OWNER_LABEL=$OWNER_VALUE" \
    -e DFLY_requirepass -p 127.0.0.1::6379 "$DRAGONFLY_IMAGE" --bind=0.0.0.0 --proactor_threads=2 --maxmemory=512m >/dev/null
  if [ "$with_greptime" = "1" ]; then
    podman run -d --name "$GREPTIME_NAME" --label "$OWNER_LABEL=$OWNER_VALUE" -p 127.0.0.1::4000 "$GREPTIME_IMAGE" \
      standalone start --http-addr=0.0.0.0:4000 --rpc-bind-addr=0.0.0.0:4001 >/dev/null
  fi

  postgres_port="$(host_port "$POSTGRES_NAME" 5432/tcp)"
  dragonfly_port="$(host_port "$DRAGONFLY_NAME" 6379/tcp)"

  # pg_isready over TCP inside the container: the timescale image's initialisation server listens on the Unix
  # socket only, so a TCP success means the final server is accepting connections.
  if ! wait_ready "test TimescaleDB" podman exec "$POSTGRES_NAME" pg_isready -h 127.0.0.1 -p 5432 -U "$DB_USER" -d "$DB"; then
    down
    exit 1
  fi
  if ! wait_ready "test Dragonfly" dragonfly_ping "$dragonfly_port" "$dragonfly_password"; then
    down
    exit 1
  fi
  if [ "$with_greptime" = "1" ]; then
    greptime_port="$(host_port "$GREPTIME_NAME" 4000/tcp)"
    if ! wait_ready "test GreptimeDB" greptime_health "$greptime_port"; then
      down
      exit 1
    fi
  fi

  env_file="$(mktemp "${TMPDIR:-/tmp}/autonogrammer-test-services.XXXXXX")"
  chmod 600 "$env_file"
  printf "export AUTONOGRAMMER_TEST_SERVICES_OWNER='%s'\n" "$OWNER_VALUE" >>"$env_file"
  printf "export AUTONOGRAMMER_TEST_POSTGRES_CONNECTION='Host=127.0.0.1;Port=%s;Database=%s;Username=%s;Password=%s'\n" \
    "$postgres_port" "$DB" "$DB_USER" "$postgres_password" >>"$env_file"
  printf "export AUTONOGRAMMER_TEST_POSTGRES_CONTAINER='%s'\n" "$POSTGRES_NAME" >>"$env_file"
  printf "export AUTONOGRAMMER_TEST_DRAGONFLY_CONNECTION='127.0.0.1:%s,password=%s,abortConnect=false'\n" \
    "$dragonfly_port" "$dragonfly_password" >>"$env_file"
  if [ "$with_greptime" = "1" ]; then
    printf "export AUTONOGRAMMER_TEST_GREPTIME_URL='http://127.0.0.1:%s'\n" "$greptime_port" >>"$env_file"
  fi
  success=1
  echo "$env_file"
)

case "${1:-}" in
  up)
    case "${2:-}" in
      "") up 0 ;;
      --with-greptime) up 1 ;;
      *)
        echo "usage: $0 up [--with-greptime]|down" >&2
        exit 2
        ;;
    esac
    ;;
  down)
    # CI passes the export file path without sourcing it; parse only the token, never execute the file.
    if [ -n "${TEST_SERVICES_ENV:-}" ]; then
      [ -f "$TEST_SERVICES_ENV" ] && [ -r "$TEST_SERVICES_ENV" ] || { echo "error: cannot read test services export file" >&2; exit 1; }
      OWNER_VALUE=""
      while IFS= read -r line; do
        if [[ "$line" =~ ^export\ AUTONOGRAMMER_TEST_SERVICES_OWNER=\'([0-9a-f]{64})\'$ ]]; then
          [ -z "$OWNER_VALUE" ] || { echo "error: duplicate ownership token" >&2; exit 1; }
          OWNER_VALUE="${BASH_REMATCH[1]}"
        fi
      done < "$TEST_SERVICES_ENV"
      [[ "$OWNER_VALUE" =~ ^[0-9a-f]{64}$ ]] || { echo "error: missing ownership token" >&2; exit 1; }
    fi
    down
    ;;
  *)
    echo "usage: $0 up [--with-greptime]|down" >&2
    exit 2
    ;;
esac
