# Phase 22: Desktop Heads, Summary

**Status: partially complete.** The Linux X11 AT-SPI2 evidence is recorded. Wayland was not run, and Windows and macOS are blocked.

The macOS scope replaces v0.9.2 Phases 15 and 16 under ADR-0004.

Mac channel: Yes. The macOS 26 build, the NSAccessibility inspection, signing and notarization need a Mac host, and the owner runs them there.

## Success criteria (ROADMAP Phase 22)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Linux head starts on X11 and on Wayland, backend named | Partial | X11 recorded in docs/runbooks/phase-22-desktop-heads.md. Wayland NOT RUN: no Wayland session |
| 2 | Linux AT-SPI2 exposes prompt, send and transcript | Met (X11) | docs/runbooks/evidence/phase-20-avalonia-foundation-2026-10-21/atspi-query.txt; docs/runbooks/phase-22-desktop-heads.md |
| 3 | Windows 11 24H2 builds net10.0-windows; UI Automation names and roles | Blocked | No Windows runner available |
| 4 | macOS 26 build; NSAccessibility names and roles | Blocked | Mac channel (owner) |
| 5 | macOS unsigned CI build; signing and notarization under XP-11 | Blocked | Signing and notarization on the Mac channel. CI run link: not recorded |

## Open items

- Wayland run in a Wayland session, recorded with its backend. Owner: owner (host session). Route: docs/runbooks/phase-22-desktop-heads.md.
- Windows 11 24H2 runner for the build and UI Automation dump. Owner: owner (runner) and engineering. Route: ROADMAP Phase 22 criterion 3.
- macOS 26 build, NSAccessibility dump, signing and notarization with the ticket identifier. Owner: owner, Mac channel. Route: XP-11 entry in docs/runbooks/phase-22-desktop-heads.md.

## Commits

- 98fc3f2: Phase 22 platform records (with the Phase 20 AT-SPI2 X11 evidence)
- 9e5913d: gate-green HEAD
