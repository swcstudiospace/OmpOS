# Phase 24: iOS and Android Heads, Summary

**Status: partially complete (2026-10-25).** Criterion 4's local build is met: `apps/Autonogrammer.Android` builds with `-warnaserror` (evidence: docs/runbooks/evidence/phase-24-android-2026-10-25/). Its `android` CI job is written and has not run, because the push is not authorized (G24-2). Criteria 1 to 3 and 5 are blocked, and no iOS head project exists yet. The 2026-10-24 correction stands: the SDK was present under /root/Android/Sdk, and only ANDROID_HOME was unset.

The iOS and mobile scope replaces v0.9.2 Phases 15 and 16 under ADR-0004.

Mac channel: Yes for iOS. The iOS build, the UIAccessibility inspection, code signing and device runs are human-gated and listed under XP-11. Android needs no Mac step.

## Success criteria (ROADMAP Phase 24)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | iOS builds net10.0-ios on iOS/iPadOS 26; build log recorded | Blocked | Needs macOS with Xcode (Mac channel) |
| 2 | UIAccessibility returns name and role of prompt, send and transcript | Blocked | No iOS 26 run; Mac channel |
| 3 | Code signing and device runs listed under XP-11 | Blocked | Owner-run on the Mac channel |
| 4 | Android builds net10.0-android in CI on the Linux runner | Partial | Local build met (docs/runbooks/evidence/phase-24-android-2026-10-25/). The `android` job is written in ci.yml; no CI run is recorded (no push, G24-2) |
| 5 | Android 16 AccessibilityNodeInfo returns name and role of the three controls | Blocked | No emulator or device run recorded |

## Open items

- CI run of the `android` job for criterion 4. It needs a push, which is a separate decision (G24-2). Owner: owner (push). Route: ROADMAP Phase 24 criterion 4.
- iOS build, accessibility dump, signing and device run on a Mac with Xcode. Owner: owner, Mac channel. Route: XP-11 entry in the runbook.
- Create the iOS head project. Its build, accessibility dump and signing need the Mac channel (XP-11). Owner: engineering, then owner. Route: ROADMAP Phase 24 criteria 1 to 3.

## Commits

- fe2d398: shared Avalonia views library and the Android head (Phase 24 local build).
- f8b5faf: the vulnerability gate covers the Android head (Phase 24 fold-in).
- 1f4cb4f: ROADMAP reconciliation with the v0.9.2 audit (planning record)
- 9e5913d: gate-green HEAD

## Run 2026-10-25 (gsd-autonomous, graph ut-mvmegw7f-4a7312d5)

Status: partially complete. Criterion 4: local build met; CI run not done (no push). Criteria 1 to 3 and 5 blocked.

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | iOS head builds for net10.0-ios | Blocked | Mac channel (no Mac with Xcode) |
| 2 | UIAccessibility dump on iOS 26 | Blocked | Mac channel |
| 3 | Code signing and device runs | Blocked | Owner, Mac channel |
| 4 | Android head builds for net10.0-android in CI on the Linux runner | Partial | Local: `dotnet build apps/Autonogrammer.Android ... -warnaserror` succeeded (docs/runbooks/evidence/phase-24-android-2026-10-25/). CI: job `android` written, not pushed |
| 5 | Android 16 accessibility dump | Blocked | No device; /dev/kvm absent |

Gate on the working tree after this phase: `./bootstrap.sh` exit 0, "bootstrap: OK", total 2245, failed 0, skipped 0, web smoke exit 0. One run failed `LinuxLocalPtyBackendTests.DisposeEndsARunningProcessAndReturns` with an IOException reading `/proc/<pid>/stat` after the process was reaped; the rerun passed. Recorded as flaky. The terminal harness is not touched by this phase. Follow-up: the test helper should treat a missing `/proc` entry as an ended process.

Deviations:
- D1. The shared split. `MainWindow` becomes a Window host (`apps/Autonogrammer.Native/MainWindow.axaml(.cs)`) and `MainView` (`src/Autonogrammer.Native.Views`). The host holds the send path in a field, and `MainView.Close()` disposes it on close, as before.
- D2. `IPromptChannel` is a public seam, so `MainView` never references the desktop `EnginePromptChannel`.
- D3. `UnconnectedRunInput` and `UnconnectedToolApprovalBridge` are public, for the Android head.
- D4. `MainView` has a public parameterless constructor (no engine, current directory). Avalonia's XAML loader needs it; without it, AVLN3001 fails the build under warnings as errors.
- D5. The architecture tests exempt the generated `CompiledAvaloniaXaml` helper namespace from the layer rule. Each XAML-compiling assembly generates its own copy under the same name, so the loader reads them as one type and reports a false edge from the shared library to the desktop head. Product types stay checked.
- D6. The architecture tests accept an absent Android head in the Linux test output, because the net10.0 test projects cannot reference net10.0-android. The MAUI head already has this exception.
- D7. The Android head derives from Avalonia 12.1.4's `AvaloniaAndroidApplication<T>`, which builds the application with the Android platform. The activity is non-generic.
- D8. The Android head's workspace is the app's private files folder, created before the view is built.
- D9. Criterion 4 names CI, so the `android` job is in `ci.yml`, not pushed (G24-2).

Follow-ups outside this phase's scope: the Window styles of the desktop head are not carried to Android (intended; confirm on a device); the flaky terminal helper above.

## Fold-in 2026-10-25 (gsd-autonomous, graph ut-mvnwnmzr-3a2a4035)

- Change: the vulnerability audit now covers the Android head. `scripts/ci/check-vulnerable-packages.py` (new) is the one gate; the build-test step and the `android` job both call it. Commit f8b5faf.
- Why: the Linux step audited `Autonogrammer.Linux.slnf`, which does not include `apps/Autonogrammer.Android`. A local listing of the Android project found no vulnerable packages, so this is a coverage gap, not a current finding.
- Verification: the Linux and Android reports pass the gate (exit 0; each lists its project; the Android report has 6 top-level and 99 transitive packages under net10.0-android36.0). A synthetic vulnerable report fails it (exit 1, one hit). No arguments exit 2. `.github/workflows/ci.yml` parses as YAML with the new step. `./bootstrap.sh` exit 0 with 2270 passed, 0 failed, 0 skipped, and web smoke exit 0, on the tree with these files.
- Not verified: the CI run. It needs a push, which is not authorized (G24-2), and the `android` job has never run on GitHub.
- Deferred: the Android teardown does not call `MainView.Close` (owner decision a4, 2026-10-25). Closing the view on finish would leave a same-process relaunch with a disposed view; `AutonogrammerAndroidApp.axaml.cs:28` is the only place the view is built.
- Criterion 4 is unchanged: its local build is met, and the CI run is still open.
