---
description: "Record of Phase 22: Linux, Windows and macOS desktop head status and what is not run."
---

# Phase 22 desktop heads: platform runbook

Status: Record (historical, 2026-10-22; Windows and macOS were never run)

Last verified: 2026-10-28 (the status only; the body is a dated record and was not re-verified)

Applies to: v0.15.0

Scope: XP-02 (Linux), XP-03 (Windows), XP-04 (macOS) and XP-11 (Mac-channel items). Status as of 2026-10-22.

Phase 22 has no plan files yet (`.planning/ROADMAP.md`: Plans TBD). This runbook records status only. It does not claim a pass that no artefact supports.

## Status key

- **PASS**: a log, dump or CI run link for the route exists in the repo. Two XP-02 rows (X11 under Xvfb, and the AT-SPI2 names on X11) cite `docs/runbooks/evidence/phase-20-avalonia-foundation-2026-10-21/`. No other row has evidence yet.
- **PENDING**: the route is planned, and its evidence has not been produced.
- **NOT RUN**: the route could not be run on the available host or runner. It is never recorded as passed.
- **BLOCKED**: a named prerequisite is missing. The owner and route are in the table.

## Sources

- `.planning/ROADMAP.md`, Phase 22 success criteria 1 to 5.
- `.planning/REQUIREMENTS.md`, XP-02, XP-03, XP-04 and XP-11.
- `docs/plans/ade-harness-plan.md`, section 3 (per-platform verification matrix) and the Mac-channel items table.
- `docs/decisions/ADR-0004-cross-platform-ui.md`.
- `docs/runbooks/phase-20-avalonia-foundation.md`, section 3 (the AT-SPI2 query, recorded on X11 under Xvfb: `docs/runbooks/evidence/phase-20-avalonia-foundation-2026-10-21/atspi-query.txt`).
- `docs/runbooks/evidence/avalonia-spike-2026-10-21/README.md` (spike evidence, not product evidence).

## Host and runner facts (2026-10-22)

- Display environment, read from the shell on 2026-10-22: `WAYLAND_DISPLAY` unset, `DISPLAY` unset, `XDG_SESSION_TYPE=tty`. This host had no Wayland session then. On 2026-10-24 the owner approved a headless Wayland session on this host: weston 13.0.0 (headless backend with Xwayland 23.2.6) was installed and run for the probe in `docs/runbooks/evidence/phase-22-wayland-2026-10-24/`.
- Linux host recorded in the spike README: Ubuntu 24.04.4 LTS, kernel 6.8.0-147-generic, x64. Ubuntu 24.x is Avalonia Tier 2, not Tier 1.
- `.github/workflows/ci.yml`: the `build-test` job runs on `ubuntu-latest`, and its image version is not pinned in the repo. It builds `OmpOS.Linux.slnf`, which includes `OmpOS.Native`. The `shells` job runs on `macos-15`, not macOS 26, and builds only `apps/OmpOS.App` (MAUI, Android target). No CI step builds `OmpOS.Native` on macOS. No Windows job exists.
- The matrix in `docs/plans/ade-harness-plan.md` states that the hosted `windows-2025` image is Windows Server 2025 and does not guarantee Windows 11 24H2.

## Platform matrix (XP-02, XP-03, XP-04, XP-11)

| Req | Platform and route | Status (2026-10-22) | Evidence in repo | Verification route | Owner |
| --- | --- | --- | --- | --- | --- |
| XP-02 | Linux, X11 (default backend), launched under Xvfb | PASS (X11 via Xvfb, 2026-10-22) | `docs/runbooks/evidence/phase-20-avalonia-foundation-2026-10-21/README.md`: launch under `xvfb-run -a` with `DISPLAY=:100` and `WAYLAND_DISPLAY` unset; backend probe shows `Avalonia.X11.dll` and `libX11` mapped in the process and no Wayland client library. | Launch `OmpOS.Native` under `xvfb-run` with the X11 backend. Done by `run-atspi-native.sh`. | ompos-maintainers |
| XP-02 | Linux, AT-SPI2 role and name of the prompt input, send control and transcript (X11 run) | PASS (X11 run, 2026-10-22) | `docs/runbooks/evidence/phase-20-avalonia-foundation-2026-10-21/atspi-query.txt`: `entry` `Message`; `push button` `Send message`; `scroll pane` `Session transcript`. | Run the query in `docs/runbooks/phase-20-avalonia-foundation.md`, section 3, under `xvfb-run` and `dbus-run-session`. Record role and name for `Message`, `Send message` and `Session transcript`. | ompos-maintainers |
| XP-02 | Linux, Wayland with the native backend opted in (`Avalonia.Wayland` package and `UseWayland()`, Avalonia 12.1.0 or later) | NOT RUN (native opt-in not added) | Opt-in mechanism verified 2026-10-24 from the Avalonia Linux guide (`dotnet add package Avalonia.Wayland`; `AppBuilder.UseWayland()`). `Avalonia.Wayland` does not reference AT-SPI, so native Wayland has no screen-reader support. Criterion 1 is met on the XWayland path (row below). This row stays open. | Add the package and the opt-in as a separate change, with its licence and purpose recorded. Then run under the headless session and record the backend named, together with the accessibility gap. | ompos-maintainers |
| XP-02 | Linux, XWayland (Wayland session, native backend not opted in) | PASS (headless Wayland session, 2026-10-24) | `docs/runbooks/evidence/phase-22-wayland-2026-10-24/probe-output.txt` and `probe.sh`. weston 13.0.0 headless with Xwayland 23.2.6 on socket `wayland-ompos`. The head ran 30 s with `DISPLAY=:0`, `WAYLAND_DISPLAY` set and `XDG_SESSION_TYPE=wayland`. Mapped backend libraries: `Avalonia.X11.dll` and `libX11.so.6`; no `libwayland-client`. An `OmpOS` window (960x640) exists on the Xwayland display. | Re-run `bash docs/runbooks/evidence/phase-22-wayland-2026-10-24/probe.sh` on any Wayland session. | ompos-maintainers |
| XP-02 | Linux, Tier 1 distribution (Ubuntu 25.x, Fedora 43 or Debian 13) for the build and the smoke launch | BLOCKED | The `build-test` runner image is not pinned in the repo. This host is Ubuntu 24.04 (Tier 2). | Run on a Tier 1 runner or self-hosted image, and record the image name in the log. This row is BLOCKED-07. | platform |
| XP-03 | Windows 11 24H2, build for `net10.0-windows` with warnings as errors | BLOCKED (no Windows 11 24H2 runner) | No Windows job in `.github/workflows/ci.yml`. | Use a self-hosted Windows 11 24H2 runner or VM, or confirm a hosted image that is 24H2. Attach the build log to the CI run. This row is BLOCKED-02. | ompos-maintainers |
| XP-03 | Windows 11 24H2, UI Automation Name and ControlType of the prompt input, send control and transcript | BLOCKED (same prerequisite) | None | Launch unpackaged on the 24H2 machine. A scripted UIA client or Accessibility Insights for Windows records Name and ControlType. Attach the dump to the CI run. | ompos-maintainers (BLOCKED-02) |
| XP-04 | macOS 26, build with warnings as errors | BLOCKED (no macOS 26 host) | The `shells` job runs on `macos-15` and builds only `apps/OmpOS.App`. Hosted macOS 26 availability is unconfirmed. | Build on a macOS 26 runner or on the Mac channel, and record the build log here. This row is BLOCKED-03. | owner (Mac channel) |
| XP-04 | macOS 26, unsigned build of `OmpOS.Native` on the CI macOS runner (ROADMAP criterion 5) | NOT RUN | No CI step builds `OmpOS.Native` on macOS. | Add a macOS build route for `OmpOS.Native` and record the CI run link. | ompos-maintainers |
| XP-04 | macOS 26, NSAccessibility Role and Name of the prompt input, send control and transcript | BLOCKED (no macOS 26 host) | None | Use the Xcode Accessibility Inspector on macOS 26, and record role and name for the three controls. This row is BLOCKED-03. | owner (Mac channel) |
| XP-04, XP-11 | macOS 26, VoiceOver pass against the NSAccessibility route | BLOCKED (no macOS 26 host) | None | Manual pass per release, recorded as a manual artefact. Per the matrix, the owner is A06 with a human tester. | A06 with human tester |
| XP-11 | macOS signing and notarization (ticket identifier) | BLOCKED (Mac channel) | No ticket identifier exists. | Check the notarization ticket on a clean macOS 26 machine. Record the ticket identifier and the human sign-off line. Not marked done from a non-Mac run. | Human (Mac channel owner) |
| XP-11 | macOS device run: signed launch on macOS 26 | BLOCKED (Mac channel) | None | Signed launch on the Mac channel, with the human sign-off line recorded. Not marked done from a non-Mac run. | Human (Mac channel owner) |

iOS and iPadOS signing and device runs (XP-05, XP-11) are tracked in Phase 24, not in this runbook.

## Rules

- A row becomes PASS only when its artefact is in the repo: a log, a dump or a CI run link. Each row is judged alone; one passing row does not pass another.
- A Wayland run with no Wayland session is recorded as NOT RUN, never as passed (ROADMAP Phase 22 criterion 1).
- XP-11 items are never marked done from a non-Mac run.
- Dated evidence goes in `docs/runbooks/evidence/<name>-<date>/`, as in phase 20. Spike evidence is referenced, not edited.

## Open items

- Wayland native backend opt-in mechanism: not verified in this repo.
- Hosted macOS 26 runner availability: unconfirmed (matrix in `docs/plans/ade-harness-plan.md`).
- Tier 1 Linux runner image: unconfirmed; the `ubuntu-latest` label is not pinned.
- Windows 11 24H2 runner: none in the repo.
- Phase 22 plan files: not written (ROADMAP Plans TBD).
