#!/usr/bin/env bash
# Phase 20 criterion 4 (AT-SPI2 half): launch OmpOS.Native under Xvfb and a private session bus,
# start the AT-SPI2 bus, wait for the OmpOS window, then run atspi_dump.py.
# Stdout: the query output only (redirect it to atspi-query.txt). Stderr: run diagnostics.
# Builds nothing. Needs the gate build at apps/OmpOS.Native/bin/Release/net10.0/OmpOS.Native.dll.
set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../../.." && pwd)"
APP_DLL="$REPO/apps/OmpOS.Native/bin/Release/net10.0/OmpOS.Native.dll"
QUERY="$HERE/atspi_dump.py"
LOG_DIR="$(mktemp -d "${TMPDIR:-/tmp}/ompos-atspi-XXXXXX")"
mkdir -m 700 "$LOG_DIR/xdg"

export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
# Private runtime dir, so the AT-SPI2 bus does not collide with another session's bus under /run/user.
export XDG_RUNTIME_DIR="$LOG_DIR/xdg"

if [ ! -f "$APP_DLL" ]; then
  echo "missing $APP_DLL: run the gate build first; this script does not build" >&2
  exit 2
fi
echo "repo=$REPO" >&2
echo "app_dll=$APP_DLL" >&2
echo "log_dir=$LOG_DIR" >&2

# Everything the session starts (bus, portals, app) writes to session.log, so stdout carries only the query.
xvfb-run -a dbus-run-session -- bash -c '
  app_dll="$1"; query="$2"; log_dir="$3"
  say() { echo "$*" >>"$log_dir/diagnostics.txt"; }
  say "DISPLAY=$DISPLAY"
  /usr/libexec/at-spi-bus-launcher --launch-immediately >"$log_dir/atspi-launcher.log" 2>&1 &
  launcher_pid=$!
  ready=no
  for _ in $(seq 1 30); do
    if gdbus call --session --dest org.a11y.Bus --object-path /org/a11y/bus --method org.a11y.Bus.GetAddress >/dev/null 2>&1; then
      ready=yes
      break
    fi
    sleep 0.5
  done
  say "a11y_bus_ready=$ready"

  dotnet "$app_dll" >"$log_dir/app.log" 2>&1 &
  app_pid=$!
  say "app_pid=$app_pid"

  python3 "$query" --timeout 90 >"$log_dir/atspi-query.txt" 2>"$log_dir/query-stderr.txt"
  query_status=$?
  say "query_exit=$query_status"

  {
    echo "backend probe app_pid=$app_pid WAYLAND_DISPLAY=${WAYLAND_DISPLAY:-<unset>} DISPLAY=$DISPLAY"
    grep -oE "[^ ]*(libX11|libxcb|libwayland-client|Avalonia\.X11|Avalonia\.Wayland)[^ ]*" "/proc/$app_pid/maps" 2>/dev/null | sort -u
    echo "socket_fds=$(ls -l "/proc/$app_pid/fd" 2>/dev/null | grep -c socket)"
  } >>"$log_dir/diagnostics.txt" 2>&1

  kill "$app_pid" 2>/dev/null
  sleep 3
  kill -KILL "$app_pid" 2>/dev/null
  wait "$app_pid" 2>/dev/null
  say "app_gone=$(kill -0 "$app_pid" 2>/dev/null && echo no || echo yes)"
  kill "$launcher_pid" 2>/dev/null
  exit "$query_status"
' _ "$APP_DLL" "$QUERY" "$LOG_DIR" >"$LOG_DIR/session.log" 2>&1
status=$?

cat "$LOG_DIR/atspi-query.txt"
cat "$LOG_DIR/diagnostics.txt" "$LOG_DIR/query-stderr.txt" >&2
echo "run_exit=$status log_dir=$LOG_DIR" >&2
exit "$status"
