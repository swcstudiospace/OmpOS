#!/bin/sh
# OmpOS agent worker entrypoint (image: deploy/worker.Containerfile).
#
# Railway mounts volumes root-owned (https://docs.railway.com/volumes.md), so the image starts as root, repairs
# ownership of the mount, then drops to OMPOS_RUN_UID:OMPOS_RUN_GID with setpriv and execs the program. Started
# as a non-root user (for example RAILWAY_RUN_UID=10001 or a local podman --user), it skips the ownership repair.
#
# Environment:
#   OMPOS_VOLUME_PATH  volume mount point (default /data)
#   OMPOS_RUN_UID      uid the program runs as (default 10001)
#   OMPOS_RUN_GID      gid the program runs as (default 10001)
#   PORT               listen port (default 8080)
#   HOME               home directory of the program, on the volume (default /data/home)
# Arguments: the program to exec; with none, `dotnet /app/OmpOS.AgentWorker.dll`.
# Exit codes: 64 invalid uid/gid, 66 volume path missing, 69 setpriv missing, 73 volume path not writable.
# Nothing in this script prints environment variables or secret values.
set -eu

log() {
  printf 'worker-entrypoint: %s\n' "$*" >&2
}

# Succeeds when $1 is a positive decimal integer without a leading zero.
is_positive_int() {
  case "$1" in
    '' | *[!0-9]* | 0*) return 1 ;;
  esac
  return 0
}

# Gives $1 to the run user when its owner differs. $2 is "recursive" or "flat".
repair_owner() {
  repair_current_uid=$(stat -c %u "$1")
  if [ "$repair_current_uid" != "$run_uid" ]; then
    if [ "$2" = recursive ]; then
      chown -R "$run_uid:$run_gid" "$1"
    else
      chown "$run_uid:$run_gid" "$1"
    fi
  fi
}

volume_path="${OMPOS_VOLUME_PATH:-/data}"
run_uid="${OMPOS_RUN_UID:-10001}"
run_gid="${OMPOS_RUN_GID:-10001}"
HOME="${HOME:-/data/home}"
export HOME

# Bind dual-stack: [::] accepts IPv4 and IPv6, and the Railway private network needs IPv6.
# ASPNETCORE_HTTP_PORTS is unset so it cannot override ASPNETCORE_URLS.
unset ASPNETCORE_HTTP_PORTS
ASPNETCORE_URLS="http://[::]:${PORT:-8080}"
export ASPNETCORE_URLS

if ! is_positive_int "$run_uid"; then
  log "OMPOS_RUN_UID must be a positive integer"
  exit 64
fi
if ! is_positive_int "$run_gid"; then
  log "OMPOS_RUN_GID must be a positive integer"
  exit 64
fi

if [ "$#" -eq 0 ]; then
  set -- dotnet /app/OmpOS.AgentWorker.dll
fi

if [ "$(id -u)" = "0" ]; then
  if [ ! -d "$volume_path" ]; then
    log "volume path $volume_path is not an existing directory; mount a volume there or set OMPOS_VOLUME_PATH"
    exit 66
  fi
  if ! command -v setpriv >/dev/null 2>&1; then
    log "setpriv not found (util-linux); cannot drop privileges. Install util-linux or start the container as a non-root user"
    exit 69
  fi

  omp_dir_existed=1
  if [ ! -d "$HOME/.omp" ]; then
    omp_dir_existed=0
  fi
  mkdir -p "$HOME" "$HOME/.omp" "$volume_path/work" "$volume_path/ompos"

  # The mount point itself is repaired without walking the volume.
  chown "$run_uid:$run_gid" "$volume_path"
  # The state trees are walked only when their top-level owner is wrong, so a woken worker with a big
  # volume does not traverse it on every start.
  repair_owner "$HOME" recursive
  repair_owner "$volume_path/work" recursive
  repair_owner "$volume_path/ompos" recursive
  if [ "$omp_dir_existed" = 0 ]; then
    repair_owner "$HOME/.omp" flat
  fi

  exec setpriv --reuid="$run_uid" --regid="$run_gid" --clear-groups --no-new-privs -- "$@"
fi

if [ ! -w "$volume_path" ]; then
  log "volume path $volume_path is not writable by uid $(id -u); fix the volume ownership or run as root so the entrypoint can repair it"
  exit 73
fi

# exec replaces the shell, so the program is PID 1 and receives termination signals directly.
exec "$@"
