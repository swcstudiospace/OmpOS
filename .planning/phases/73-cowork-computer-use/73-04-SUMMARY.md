---
phase: 73-cowork-computer-use
plan: 73-04
subsystem: harness
tags: [computer-use, x11, hotkey, wayland]
requires:
  - 73-01
provides:
  - X11Backend
  - X11HotkeyRegistrar
  - WaylandDetection
affects: [73-06]
tech-stack:
  added: []
  patterns:
    - System libX11, libXtst and libXrandr via DllImport
    - One injection connection and a second hotkey connection
    - Private Xvfb live tests never touch the session DISPLAY
key-files:
  created:
    - src/Autonogrammer.Harness.ComputerUse.X11/X11Backend.cs
    - src/Autonogrammer.Harness.ComputerUse.X11/X11HotkeyRegistrar.cs
    - src/Autonogrammer.Harness.ComputerUse.X11/WaylandDetection.cs
    - src/Autonogrammer.Harness.ComputerUse.X11/X11Connection.cs
    - src/Autonogrammer.Harness.ComputerUse.X11/X11Interop.cs
  modified: []
decisions:
  - Extra type files exist so MA0048 holds
  - Scratch keysyms stay mapped until the text run is flushed so xev can look them up
metrics:
  duration: null
  completed: null
---

# Phase 73 Plan 04: X11 backend and hotkey Summary

The X11 project now captures with XGetImage, injects with XTest, lists windows through EWMH or XQueryTree, and grabs the kill chord on a second connection. Wayland and a missing display stay unavailable.

## What landed

- `X11Interop` loads `libX11.so.6`, `libXtst.so.6` and `libXrandr.so.2` with `DllImport` and `DefaultDllImportSearchPaths`. No unsafe blocks and no new package.
- `X11Connection` owns one thread and a command queue. `XOpenDisplay` uses `X11BackendOptions.DisplayName`. A null name reads `DISPLAY` only through the injected environment function.
- `X11Backend` reports Available only when the session is not Wayland, a display opens, and `XTestQueryExtension` passes. Otherwise the state is WaylandUnsupported, NoDisplay, or InputInjectionUnavailable, each with its remediation id. Capture is a root `XGetImage` crop in BGRA. Pointer moves use at most 8 steps. Text uses ASCII keysyms or `0x01000000 | code point`, with a scratch keycode restored after `XSync`. A failed restore is logged and thrown without the typed text. Clipboard goes through `IClipboardTextPort` only. App identity is WM_CLASS plus the `/proc/<pid>/exe` basename, or `unknown` when that read fails.
- `X11HotkeyRegistrar` grabs Ctrl+Alt+Shift+Escape (or the given chord) on its own connection and thread, including the lock-modifier combinations. `BadAccess` is `KillSwitchUnavailable`. Unregister closes that connection.
- `WaylandDetection` treats `XDG_SESSION_TYPE=wayland` or a set `WAYLAND_DISPLAY` as Wayland even when `DISPLAY` points at XWayland.
- Live tests run only when `AUTONOGRAMMER_LIVE_X11=1` and Xvfb exists. The fixture starts `Xvfb -displayfd 3 -nolisten tcp -screen 0 1280x800x24` and never reads or sets the session `DISPLAY`. Without the opt-in, every Live test skips.

## Tests

- Unit: `KeysymMapperTests` and `WaylandDetectionTests` passed (9 passed with the assembly marker under `--filter-not-trait Category=Live`).
- Live with `AUTONOGRAMMER_LIVE_X11=1`: 7 passed (pointer, xev click, typing of `Hello, ünï`, scrot agreement, hotkey, second grab, XQueryTree).
- Live without the opt-in: 7 skipped.

## Gaps

- Extra files beyond the plan list exist so MA0048 holds: `KeysymMapper.cs`, `KeysymPlan.cs`, `X11BackendOptions.cs`.
- On this Xvfb, a pointer warp is discarded when the client that sent it disconnects. The pointer live case keeps `xdotool mousemove … sleep 3` alive until `GetPointerAsync` returns.
- Scratch mappings are restored after a short wait so xev can resolve the keysym from the keycode. A crash in that window still leaves the map changed.
- The live desktop was not driven.
