#!/usr/bin/env bash
# Throwaway spike: run the Avalonia desktop head under Xvfb with a session bus and AT-SPI, then dump the accessibility tree.
set -u
export DOTNET_ROOT=/root/.dotnet
export PATH=/root/.dotnet:$PATH
export DOTNET_NOLOGO=1
Xvfb :99 -screen 0 1280x900x24 -nolisten tcp >/tmp/avalonia-spike/xvfb.log 2>&1 &
XVFB_PID=$!
sleep 2
export DISPLAY=:99
dbus-run-session -- bash -c '
  /usr/libexec/at-spi-bus-launcher --launch-immediately >/tmp/avalonia-spike/atspi-launcher.log 2>&1 &
  sleep 2
  dotnet /tmp/avalonia-spike/OmpOSSpike/OmpOSSpike.Desktop/bin/Release/net10.0/OmpOSSpike.Desktop.dll >/tmp/avalonia-spike/desktop.log 2>&1 &
  APP_PID=$!
  sleep 15
  python3 /tmp/avalonia-spike/atspi_dump.py > /tmp/avalonia-spike/atspi-tree.txt 2>&1
  echo "dump_exit=$?"
  kill $APP_PID 2>/dev/null
  sleep 2
'
kill $XVFB_PID 2>/dev/null
echo "run_done"
