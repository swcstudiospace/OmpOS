#!/usr/bin/env bash
# Probe: start the Autonogrammer native head in a headless Wayland session (weston, with Xwayland).
# Usage: probe.sh default | wayland
#   default: AUTONOGRAMMER_NATIVE_BACKEND unset. Avalonia's platform detection picks X11, which runs through Xwayland.
#   wayland: AUTONOGRAMMER_NATIVE_BACKEND=wayland. The native Avalonia.Wayland backend; DISPLAY is unset for the head.
# The backend the head starts on is named in the "backend selected" line, and checked against the libraries mapped into the process.
set -u
MODE=${1:-}
case "$MODE" in default | wayland) ;; *) echo "usage: probe.sh default|wayland"; exit 64 ;; esac

export XDG_RUNTIME_DIR=/tmp/autonogrammer-xdg
rm -rf "$XDG_RUNTIME_DIR"; mkdir -p "$XDG_RUNTIME_DIR"; chmod 700 "$XDG_RUNTIME_DIR"
SOCKET=wayland-autonogrammer
WESTON_LOG=/tmp/autonogrammer-weston-$MODE.log
APP_OUT=/tmp/autonogrammer-native-$MODE.out
REPO=/root/src/repos/autonogrammer
APP=$REPO/apps/Autonogrammer.Native/bin/Debug/net10.0/Autonogrammer.Native

if [ ! -x "$APP" ]; then echo "app binary missing: $APP"; exit 2; fi
echo "date: $(date -Is)"
echo "mode: $MODE"
echo "app binary: $APP"
echo "app build: $(stat -c '%y' "$APP" | cut -d. -f1)"

weston --backend=headless --xwayland --socket=$SOCKET --idle-time=0 --log="$WESTON_LOG" >/dev/null 2>&1 &
WESTON_PID=$!
for _ in $(seq 1 60); do [ -S "$XDG_RUNTIME_DIR/$SOCKET" ] && break; sleep 0.5; done
echo "weston pid: $WESTON_PID"
echo "wayland socket present: $([ -S "$XDG_RUNTIME_DIR/$SOCKET" ] && echo yes || echo no)"

X_NUM=""
for _ in $(seq 1 60); do
  X_NUM=$(ls /tmp/.X11-unix/ 2>/dev/null | sed -n 's/^X\([0-9]\+\)$/\1/p' | head -1)
  [ -n "$X_NUM" ] && break
  sleep 0.5
done
echo "xwayland display: :${X_NUM:-none}"

export WAYLAND_DISPLAY=$SOCKET XDG_SESSION_TYPE=wayland
if [ "$MODE" = wayland ]; then
  export AUTONOGRAMMER_NATIVE_BACKEND=wayland
  unset DISPLAY
  echo "backend selected: wayland (AUTONOGRAMMER_NATIVE_BACKEND=wayland, DISPLAY unset)"
else
  unset AUTONOGRAMMER_NATIVE_BACKEND
  export DISPLAY=":${X_NUM}"
  echo "backend selected: x11 default platform detection (AUTONOGRAMMER_NATIVE_BACKEND unset, DISPLAY=:${X_NUM} through Xwayland)"
fi

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
  grep -oE '[^ ]*(Avalonia\.X11|libX11|Avalonia\.Wayland|libwayland-client)[^ ]*' "/proc/$APP_PID/maps" 2>/dev/null | sed 's#.*/##' | sort -u
  echo "--- unix socket paths held by the app process ---"
  INODES=$(ls -l "/proc/$APP_PID/fd" 2>/dev/null | grep -oE 'socket:\[[0-9]+\]' | grep -oE '[0-9]+')
  for ino in $INODES; do grep -E " $ino( |$)" /proc/net/unix | awk '{print $NF}'; done | sort -u | grep -v dotnet-diagnostic
fi
if command -v xwininfo >/dev/null 2>&1 && [ -n "$X_NUM" ]; then
  echo "--- X windows on the Xwayland display ---"
  DISPLAY=":${X_NUM}" xwininfo -root -tree 2>/dev/null | grep -i "autonogrammer" | head -5
else
  echo "xwininfo not installed or no Xwayland display; X window tree not captured"
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
