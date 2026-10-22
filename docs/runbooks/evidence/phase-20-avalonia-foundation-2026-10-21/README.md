# Phase 20 AT-SPI2 evidence (criterion 4, Linux half)

Evidence for `docs/runbooks/phase-20-avalonia-foundation.md`, section 3. It queries the AT-SPI2 tree of `OmpOS.Native` (Avalonia 12.1.4) under Xvfb. It is product evidence, not spike evidence. The spike in `docs/runbooks/evidence/avalonia-spike-2026-10-21/` was read only and is not changed.

## Files

- `run-atspi-native.sh`: launches `OmpOS.Native` under `xvfb-run -a` and `dbus-run-session`, starts `at-spi-bus-launcher`, waits for the OmpOS window, runs `atspi_dump.py`, then stops the app. It builds nothing.
- `atspi_dump.py`: the AT-SPI2 query. It prints the three required controls (role, exact name, tree path), three extra controls, and the complete tree of the OmpOS application.
- `atspi-query.txt`: raw stdout of the run, verbatim.

## Command

Run from this directory, with the gate build already present:

```
timeout 300 ./run-atspi-native.sh > atspi-query.txt 2> /tmp/ompos-atspi-phase20-stderr.txt
```

Wrapper exit status: `0`. The script's own `run_exit=0` and `query_exit=0` match.

The app DLL is `apps/OmpOS.Native/bin/Release/net10.0/OmpOS.Native.dll`, sha256 `fb911d70c02608fe38fa6a81b72a9927ac5c0981d28e25cbeb669b10a0315ded`. The gate built it. No build was run for this evidence.

## Date and host

- Run date (`date -u +%F`): 2026-10-22. The folder name uses the date the runbook assigned for Phase 20 evidence (2026-10-21). The date recorded here is the actual run date.
- `uname -sr`: `Linux 6.8.0-147-generic`
- `/etc/os-release` PRETTY_NAME: `Ubuntu 24.04.4 LTS` (Avalonia Tier 2 on Linux)
- `dpkg-query -W at-spi2-core`: `at-spi2-core	2.52.0-1build1`
- Also present: `xvfb 2:21.1.12-1ubuntu1.8`, `python3-pyatspi 2.46.1-1`, `gir1.2-atspi-2.0 2.52.0-1build1`, .NET runtime `Microsoft.NETCore.App 10.0.13`.
- Display: `xvfb-run -a` chose `DISPLAY=:100`. `WAYLAND_DISPLAY` was unset.

## Result: the three controls

The query matches exact accessible names in the complete tree. Each name matched exactly one node.

| Control (Phase 20 criterion 4) | Source name | AT-SPI2 role | Accessible name | Tree path |
| --- | --- | --- | --- | --- |
| Prompt input | `ComposerView.axaml` `TextBox` | `entry` | `Message` | `1.0.1.3.0.0.1.1.2.0.0.0.0.0` |
| Send control | `ComposerView.axaml` `Button` | `push button` | `Send message` | `1.0.1.3.0.0.1.1.2.0.0.0.0.1` |
| Transcript | `SessionView.axaml` `ScrollViewer` | `scroll pane` | `Session transcript` | `1.0.1.3.0.0.1.1.0.0.0.0.0` |

Extra controls the runbook asks to record:

| Control | AT-SPI2 role | Accessible name | Observation |
| --- | --- | --- | --- |
| Conversations | `list` | `Conversations` | Present |
| Stop response | `push button` | `Stop response` | Present |
| Session status | not in tree | `Session status` | NOT FOUND at idle |

`Session status` is the `StatusLine` `TextBlock` in `MainWindow.axaml`, which is declared with `IsVisible="False"`. The node is absent at idle, so no AT-SPI node exists for it. This is what the markup predicts. The run did not drive the app into a state that shows the status line, so `AutomationProperties.LiveSetting` was not observed on this host. That leaves OQ-39 open for the live-region half.

The application is listed on the AT-SPI2 desktop as `Avalonia Application`, not `OmpOS`. The top-level frame reports the name `OmpOS` (the window title), and the query matches on that frame. The full tree is in `atspi-query.txt`, starting at line 11.

## Backend

Backend: X11, through Xvfb. Evidence is from the app process while the query ran:

- `Avalonia.X11.dll` from the build output is mapped into the process.
- `libX11.so.6` and `libxcb` are mapped.
- No `libwayland-client` and no `Avalonia.Wayland` mapping.
- `WAYLAND_DISPLAY` unset, `DISPLAY=:100`, and 5 socket file descriptors.

## Diagnostics (stderr, verbatim)

```
repo=/root/src/repos/ompos
app_dll=/root/src/repos/ompos/apps/OmpOS.Native/bin/Release/net10.0/OmpOS.Native.dll
log_dir=/tmp/ompos-atspi-gP3n0z
DISPLAY=:100
a11y_bus_ready=yes
app_pid=2931887
query_exit=0
backend probe app_pid=2931887 WAYLAND_DISPLAY=<unset> DISPLAY=:100
/root/src/repos/ompos/apps/OmpOS.Native/bin/Release/net10.0/Avalonia.X11.dll
/usr/lib/x86_64-linux-gnu/libX11-xcb.so.1.0.0
/usr/lib/x86_64-linux-gnu/libX11.so.6.4.0
/usr/lib/x86_64-linux-gnu/libxcb-dri3.so.0.1.0
/usr/lib/x86_64-linux-gnu/libxcb-glx.so.0.0.0
/usr/lib/x86_64-linux-gnu/libxcb-present.so.0.0.0
/usr/lib/x86_64-linux-gnu/libxcb-randr.so.0.1.0
/usr/lib/x86_64-linux-gnu/libxcb-shm.so.0.0.0
/usr/lib/x86_64-linux-gnu/libxcb-sync.so.1.0.0
/usr/lib/x86_64-linux-gnu/libxcb-xfixes.so.0.0.0
/usr/lib/x86_64-linux-gnu/libxcb.so.1.1.0
socket_fds=5
app_gone=yes
run_exit=0 log_dir=/tmp/ompos-atspi-gP3n0z
```

After the run, `pgrep` found no `OmpOS.Native.dll` process and no process from the run's log directory.

## Failures and discarded runs

- No failure in the recorded run. All three required controls resolved to one node each, and the query exited 0.
- An earlier run, before this one, was discarded. Its stdout was polluted by the private session bus and the xdg-desktop-portal processes it activated, so the raw output was not a clean query result. The launcher was then changed to write all session output to a log, and the run recorded here was the only one written to `atspi-query.txt`. That earlier run's query output agreed on the three controls and the `Session status` absence. Its logs are in `/tmp/ompos-atspi-qm9CON` and are not part of the evidence.

## Limits

- One idle state on one run. Focus order, the transcript's keyboard reach (A-08, OQ-40) and live-region announcements were not exercised.
- `Session status` was not observed in the tree, so `LiveSetting` (OQ-39) and the font face (OQ-41) are not confirmed by this run.
- Xvfb is a virtual X server. The result is for the X11 backend only. The Wayland row in the Phase 22 runbook stays NOT RUN.
- Ubuntu 24.04 is Avalonia Tier 2 on Linux.
