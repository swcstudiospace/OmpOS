---
phase: 73-cowork-computer-use
plan: 73-05
subsystem: harness
tags: [computer-use, windows, macos]
requires:
  - 73-01
provides:
  - WindowsBackend
  - MacOSBackend
  - SendInputBuilder
  - ScreenCaptureArguments
affects: [73-06]
tech-stack:
  added: []
  patterns:
    - OS entry points run only when OperatingSystem.IsWindows or IsMacOS is true
    - SendInput and screencapture arguments are pure builders
    - CoordinateMap is the only macOS scale authority
key-files:
  created:
    - src/Autonogrammer.Harness.ComputerUse.Windows/WindowsBackend.cs
    - src/Autonogrammer.Harness.ComputerUse.Windows/WindowsInterop.cs
    - src/Autonogrammer.Harness.ComputerUse.Windows/SendInputBuilder.cs
    - src/Autonogrammer.Harness.ComputerUse.Windows/WindowsHotkeyRegistrar.cs
    - src/Autonogrammer.Harness.ComputerUse.MacOS/MacOSBackend.cs
    - src/Autonogrammer.Harness.ComputerUse.MacOS/MacOSInterop.cs
    - src/Autonogrammer.Harness.ComputerUse.MacOS/ScreenCaptureArguments.cs
    - src/Autonogrammer.Harness.ComputerUse.MacOS/MacOSHotkeyRegistrar.cs
    - tests/Autonogrammer.Harness.ComputerUse.Windows.Tests/SendInputBuilderTests.cs
    - tests/Autonogrammer.Harness.ComputerUse.Windows.Tests/CoordinateScaleTests.cs
    - tests/Autonogrammer.Harness.ComputerUse.MacOS.Tests/ScreenCaptureArgumentsTests.cs
    - tests/Autonogrammer.Harness.ComputerUse.MacOS.Tests/CoordinateScaleTests.cs
  modified: []
decisions:
  - INPUT records are internal nested types so CA1034 and CA1724 pass without a new file
  - UIPI uses kernel32 and advapi32 in addition to user32, gdi32 and dwmapi
  - macOS capture runner is internal because a public nested interface fails CA1034
metrics:
  duration: null
  completed: null
---

# Phase 73 Plan 05: Windows and macOS backends Summary

Windows and macOS Computer Use backends compile warning-free on Linux. Native entry points are not called on this host. Pure builders cover absolute normalisation, Unicode text, scan-code chords, screencapture arguments and scale.

## What landed

- `SendInputBuilder` maps the virtual-desktop origin to 0 and the far corner to 65535, clamps instead of wrapping, encodes `KEYEVENTF_UNICODE` text, and encodes chords as scan-code down/up pairs in order. Fractional DPI goes through that same normaliser once.
- `WindowsBackend` injects via SendInput, captures with GDI `SRCCOPY|CAPTUREBLT`, and exposes `DetectElevatedTarget` (`DenialCode.ElevatedTarget`) and `DetectSecureDesktop` (`DenialCode.SecureDesktop`). `GetPermissionStatusAsync` reports those two facts. `WindowsHotkeyRegistrar` uses `RegisterHotKey` on a message-only thread and returns false on Linux.
- `ScreenCaptureArguments` builds `["-x", "-t", "png", outfile]` with the outfile under the state directory, a 10 second timeout, and an environment that keeps only `HOME`, `TMPDIR`, `USER`, `LOGNAME`, `LANG` and `LC_*`. Capture pixels go through `CoordinateMap` once. The OQ-382 attribution comment is on the type.
- `MacOSBackend` posts CGEvent to `kCGHIDEventTap`, lists windows through `CGWindowListCopyWindowInfo`, and captures through an injected runner. The default runner starts `/usr/sbin/screencapture` by absolute path with `ArgumentList`, no shell, the allow-listed environment, a timeout and kill-on-cancel, and refuses to start on Linux. `GetPermissionStatusAsync` reads Screen Recording and Accessibility TCC only on macOS. `MacOSHotkeyRegistrar` uses Carbon `RegisterEventHotKey` and returns false on Linux.
- Neither csproj gained a ProjectReference or PackageReference. macOS source names no Sandbox, Harness.Terminal or Harness.Acp type.

## Verification

- Windows tests: 8 passed, 0 failed (`--filter-not-trait Category=Live`).
- macOS tests: 6 passed, 0 failed (same filter).
- Both Release builds with `--no-restore -warnaserror -nodeReuse:false -m:1` exited 0 with 0 warnings.

## Gaps

- Window identities are `Unverified:` plus class name or owner name (OQ-388). Windows signer and executable path are not read; the plan's three capture DLLs do not include wintrust.
- macOS clipboard write throws unavailable. Capture returns the PNG bytes, not decoded BGRA.
- The runner constructor is internal so CA1034 does not force a file outside the plan list.
- screencapture attribution remains unverified (OQ-382). No desktop was driven.
