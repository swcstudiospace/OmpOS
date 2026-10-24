# Phase 22: Desktop Heads, Summary

**Status: blocked (3 of 5 criteria open).** Criterion 1 is met on the XWayland path (2026-10-24). Criterion 2 is met on X11 (2026-10-22). Criterion 3 needs a Windows 11 24H2 runner, and criteria 4 and 5 need the Mac channel. Phases 25 to 28 depend on this phase.

The macOS scope replaces v0.9.2 Phases 15 and 16 under ADR-0004.

Mac channel: Yes. The macOS 26 build, the NSAccessibility inspection, signing and notarization need a Mac host, and the owner runs them there.

## Success criteria (ROADMAP Phase 22)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Linux head starts on X11 and on Wayland, backend named | Met on the XWayland path. Native Wayland backend not run | X11: docs/runbooks/phase-22-desktop-heads.md (2026-10-22, Xvfb). Wayland: docs/runbooks/evidence/phase-22-wayland-2026-10-24/probe-output.txt (headless weston with Xwayland; `Avalonia.X11.dll` and `libX11` mapped; no `libwayland-client`; window `OmpOS` 960x640). The native-backend row stays open in the runbook |
| 2 | Linux AT-SPI2 exposes prompt, send and transcript | Met (X11) | docs/runbooks/evidence/phase-20-avalonia-foundation-2026-10-21/atspi-query.txt |
| 3 | Windows 11 24H2 builds net10.0-windows; UI Automation names and roles | Blocked | No Windows 11 24H2 runner available |
| 4 | macOS 26 build; NSAccessibility names and roles | Blocked | Mac channel (owner) |
| 5 | macOS unsigned CI build; signing and notarization under XP-11 | Blocked | Signing and notarization on the Mac channel. No CI step builds OmpOS.Native on macOS |

## Open items

- Windows 11 24H2 runner for the build and the UI Automation dump. Owner: owner (runner) and engineering. Route: ROADMAP Phase 22 criterion 3.
- macOS 26 build, NSAccessibility dump, signing and notarization with the ticket identifier. Owner: owner, Mac channel. Route: XP-11 entry in docs/runbooks/phase-22-desktop-heads.md.
- Native Wayland backend (`Avalonia.Wayland` with `UseWayland()`): not added. It needs a package addition, and native Wayland has no AT-SPI support, so the accessibility gap is a separate decision. Owner: engineering, with an owner decision on that gap.

## Commits

- 98fc3f2: Phase 22 platform records (with the Phase 20 AT-SPI2 X11 evidence)
- 9e5913d: gate-green HEAD
- Uncommitted: the 2026-10-24 Wayland evidence and the runbook update. No commit was made.
