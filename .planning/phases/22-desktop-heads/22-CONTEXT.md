# Phase 22: Desktop Heads, discuss record

Recorded 2026-10-24 by the gsd-autonomous run (graph ut-mvm12su0-1fae51f6). Milestone v0.10.0. Status: BLOCKED.
Depends on Phases 20 and 21 (both complete). Mac channel: yes. UI hint: yes.

## Goal and criteria (ROADMAP.md lines 428 to 439)

Goal: the Linux, Windows and macOS desktop heads build from the shared project and expose names and roles through each platform's accessibility API.

1. The Linux head starts on X11 and on Wayland with the native backend opt-in, and each start names its backend. A Wayland run with no Wayland session is recorded as not run.
2. The Linux AT-SPI2 tree exposes the prompt input, send control and transcript.
3. The Windows head builds for net10.0-windows on Windows 11 24H2, and UI Automation exposes names and roles.
4. The macOS head builds for macOS 26, and NSAccessibility exposes names and roles.
5. The macOS head builds unsigned in CI. Signing and notarization are Mac-channel items under XP-11.

## Current evidence

- Criterion 1: partial. The X11 start is recorded (docs/runbooks/phase-22-desktop-heads.md, 2026-10-22). The XWayland start is recorded (docs/runbooks/evidence/phase-22-wayland-2026-10-24/probe-output.txt). The native opt-in run is not done.
- Criterion 2: met on X11 (docs/runbooks/evidence/phase-20-avalonia-foundation-2026-10-21/atspi-query.txt).
- Criteria 3 to 5: blocked on external hosts.

## Verified paths

- apps/OmpOS.Native/Program.cs: the desktop entry, where the backend is chosen.
- apps/OmpOS.Native/OmpOS.Native.csproj
- Directory.Packages.props: Avalonia 12.1.4 pins at lines 21 to 25, Avalonia.Skia at line 79. Any Wayland package would be pinned here.
- docs/runbooks/phase-22-desktop-heads.md
- docs/runbooks/evidence/phase-22-wayland-2026-10-24/ (probe.sh, probe-output.txt)

## Reuse and conventions

- The probe in the evidence folder (headless weston with Xwayland) is the harness for any native run.
- New packages go through central package management in Directory.Packages.props, with a lock file per project.

## Edge cases

- No Wayland session: record as not run, never as passed.
- The native backend is an opt-in. The X11 default does not change.

## Grey areas

- G22-1, material (changes which backend the shipped head uses, and adds a package). Question: add Avalonia.Wayland and call UseWayland(), so a native Wayland run can be recorded? Default: no. Rejected: criterion 1 stays partial. Accepted: the run uses the probe harness and records the backend it names.
- G22-2, material (accessibility). Native Wayland has no AT-SPI support, so a Wayland-native session cannot satisfy the criterion 2 query. Default: record the gap in the runbook, with no workaround. It needs an owner decision before G22-1 ships.
- G22-3, decided, immaterial: the X11 default stays.

## Automated and manual

- Criterion 1, native run: automated headless smoke, once G22-1 is accepted.
- Criteria 3 to 5: external. Owner-run on a Windows 11 24H2 runner and on the Mac channel.

## Pauses

- GREY_AREA: G22-1 and G22-2, batched with the other phases' grey areas.
- BLOCKER: criteria 3 to 5. Minimal action: provide a Windows 11 24H2 runner and a Mac host, or record these criteria as owner-blocked.
