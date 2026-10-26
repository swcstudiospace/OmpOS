#!/usr/bin/env bash
# Probe: start the OmpOS native head in a headless Wayland session (weston + Xwayland).
# Default platform detection on Linux picks X11, which runs through Xwayland on a Wayland session.
set -u
export XDG_RUNTIME_DIR=/tmp/ompos-xdg
rm -rf "$XDG_RUNTIME_DIR"; mkdir -p "$XDG_RUNTIME_DIR"; chmod 700 "$XDG_RUNTIME_DIR"
WESTON_LOG=/tmp/ompos-weston.log
APP_OUT=/tmp/ompos-native-wayland.out
APP=/root/src/repos/ompos/apps/OmpOS.Native/bin/Release/net10.0/OmpOS.Native

weston --backend=headless --xwayland --socket=wayland-ompos --idle-time=0 --log="$WESTON_LOG" >/dev/null 2>&1 &
WESTON_PID=$!
for _ in $(seq 1 60); do [ -S "$XDG_RUNTIME_DIR/wayland-ompos" ] && break; sleep 0.5; done
echo "weston pid: $WESTON_PID"
echo "wayland socket present: $([ -S "$XDG_RUNTIME_DIR/wayland-ompos" ] && echo yes || echo no)"

X_NUM=""
for _ in $(seq 1 60); do
  X_NUM=$(ls /tmp/.X11-unix/ 2>/dev/null | sed -n 's/^X\([0-9]\+\)$/\1/p' | head -1)
  [ -n "$X_NUM" ] && break
  sleep 0.5
done
echo "xwayland display: :${X_NUM:-none}"

if [ ! -x "$APP" ]; then echo "app binary missing: $APP"; kill "$WESTON_PID" 2>/dev/null; exit 2; fi

export WAYLAND_DISPLAY=wayland-ompos DISPLAY=":${X_NUM}" XDG_SESSION_TYPE=wayland
timeout 90 "$APP" >"$APP_OUT" 2>&1 &
TIMEOUT_PID=$!
sleep 30

APP_PID=""
for p in /proc/[0-9]*; do
  cmd=$(tr '\0' ' ' < "$p/cmdline" 2>/dev/null)
  case "$cmd" in "$APP"*) APP_PID=${p#/proc/}; break ;; esac
done
echo "app pid (process running the head, not timeout): ${APP_PID:-none}"
if [ -n "$APP_PID" ]; then
  echo "app alive after 30s: $(kill -0 "$APP_PID" 2>/dev/null && echo yes || echo no)"
  echo "--- backend libraries mapped into the app process ---"
  grep -oE '[^ ]*(Avalonia\.X11|libX11|Avalonia\.Wayland|libwayland-client|libEGL|libGL)[^ ]*' "/proc/$APP_PID/maps" 2>/dev/null | sed 's#.*/##' | sort -u
  echo "--- unix socket paths held by the app process ---"
  INODES=$(ls -l "/proc/$APP_PID/fd" 2>/dev/null | grep -oE 'socket:\[[0-9]+\]' | grep -oE '[0-9]+')
  for ino in $INODES; do grep -E " $ino( |$)" /proc/net/unix | awk '{print $NF}'; done | sort -u
fi
if command -v xwininfo >/dev/null 2>&1; then
  echo "--- X windows on the Xwayland display ---"
  DISPLAY=":${X_NUM}" xwininfo -root -tree 2>/dev/null | grep -i "ompos" | head -5
else
  echo "xwininfo not installed; window tree not captured"
fi
echo "--- app output (first 25 lines) ---"
head -25 "$APP_OUT" 2>/dev/null
echo "--- weston log: client and xwayland lines ---"
grep -i -E "xwayland|client|launching" "$WESTON_LOG" 2>/dev/null | tail -10

[ -n "$APP_PID" ] && kill "$APP_PID" 2>/dev/null
kill "$TIMEOUT_PID" 2>/dev/null
kill "$WESTON_PID" 2>/dev/null
wait 2>/dev/null
echo "probe finished"
