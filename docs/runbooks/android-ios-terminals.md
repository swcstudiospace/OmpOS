# Android and iOS terminals runbook

Scope: Phase 43 (VX-08, VX-10), criterion 4, and the mobile part of criterion 1. Status as of 2026-10-26.

This record states status only. Nothing in it is built, run or deployed by this document. It lists the checks that cannot run on the Linux gate host, so that none of them is recorded as passed.

## Status key

- **PASS**: a gate test or a recorded run asserts it.
- **NOT RUN**: the check could not be run. It is never recorded as passed.
- **BLOCKED**: a named prerequisite is missing. The owner and route are in the entry.

## Android 16 accessibility dump: BLOCKED

Phase 43 criterion 4 needs an Android 16 AccessibilityNodeInfo dump from a device or emulator, showing the name and role of the terminal surface.

- Status: NOT RUN, BLOCKED.
- Prerequisites missing on this host (`/dev/kvm` and `ANDROID_HOME` checked on 2026-10-26): no `/dev/kvm`, so no hardware-accelerated emulator can run; `ANDROID_HOME` is unset; no Android SDK API 36 is recorded. No Android 16 device or emulator is recorded for this host.
- What exists: the Android head `apps/OmpOS.Android` builds locally with `-warnaserror` (evidence: `docs/runbooks/evidence/phase-24-android-2026-10-25/`). The `android` CI job is written and no CI run is recorded.
- Related unmet check: Phase 24 criterion 5 (Android 16 accessibility dump of prompt, send and transcript), blocked for the same reasons.
- Owner: owner, device or emulator host. Route: OQ-74.
- Evidence to record when the run happens: device or emulator model and Android build, the app build, the command that produced the dump, and the dump file under `docs/runbooks/evidence/`.

## iOS build and accessibility: BLOCKED on the Mac channel

- The iOS build (net10.0-ios), the UIAccessibility inspection, code signing and device runs need macOS with Xcode. They are Mac-channel items under XP-11 and are human-gated.
- Status: NOT RUN, BLOCKED. No iOS head project exists yet (Phase 24 summary: creating it is an open item whose build and signing need the Mac channel).
- Owner: owner, Mac channel. Route: OQ-74 and the XP-11 entry in `.planning/REQUIREMENTS.md`.
- Evidence to record when the run happens: macOS and Xcode versions, the build log, the UIAccessibility dump for the terminal surface, and the signing and device-run notes.

## Criterion 1 for mobile: not met at this date

Phase 43 criterion 1 requires that mobile and web builds reference no local process spawning, and that the local terminal shows as unavailable with a reason.

- `apps/OmpOS.Android` references `src/OmpOS.Native.Views`, and `src/OmpOS.Native.Views/OmpOS.Native.Views.csproj` references `src/OmpOS.Harness.Terminal`, which spawns processes. The terminal panel is hosted in the native shell.
- The web head `apps/OmpOS.Web` references only Contracts and the shared UI library, and has no terminal surface (roadmap scout of 2026-10-26; not re-read for this record).
- No "unavailable with reason" surface exists. The architecture rule for the web head covers `Harness.Acp` and `Harness.Rpc`, not `Harness.Terminal` or the Android head.
- This is a unit of work, not an external blocker: a guard test and a split of the terminal panel out of the shared views assembly. It is recorded here so the mobile runs above are not read as clearing criterion 1.

## Open items

1. Android 16 device or emulator host, SDK API 36, and the accessibility dump: owner. Route: OQ-74.
2. iOS build, accessibility dump, signing and device run: owner, Mac channel (XP-11). Route: OQ-74.
3. Mobile and web builds reference no process-spawning assembly (criterion 1): engineering, no external prerequisite.
