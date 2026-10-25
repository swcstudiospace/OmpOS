# Phase 24: iOS and Android Heads, Summary

**Status: blocked.** Android is no longer blocked on this host. The Android SDK with platform API 36 and build-tools 36.0.0, and the .NET Android workload, are installed (2026-10-24). No head project has been created yet, and iOS needs a Mac. Correction 2026-10-24: the earlier "Android is blocked on this host" and "no SDK API 36" statements were wrong. The SDK was present under /root/Android/Sdk; only ANDROID_HOME was unset.

The iOS and mobile scope replaces v0.9.2 Phases 15 and 16 under ADR-0004.

Mac channel: Yes for iOS. The iOS build, the UIAccessibility inspection, code signing and device runs are human-gated and listed under XP-11. Android needs no Mac step.

## Success criteria (ROADMAP Phase 24)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | iOS builds net10.0-ios on iOS/iPadOS 26; build log recorded | Blocked | Needs macOS with Xcode (Mac channel) |
| 2 | UIAccessibility returns name and role of prompt, send and transcript | Blocked | No iOS 26 run; Mac channel |
| 3 | Code signing and device runs listed under XP-11 | Blocked | Owner-run on the Mac channel |
| 4 | Android builds net10.0-android in CI on the Linux runner | Partial | Local prerequisites installed 2026-10-24: /root/Android/Sdk (platforms android-36, build-tools 36.0.0) and the .NET Android workload 36.1.118. No Android head project exists yet, and no CI job builds it. CI run log not recorded |
| 5 | Android 16 AccessibilityNodeInfo returns name and role of the three controls | Blocked | No emulator or device run recorded |

## Open items

- Create the Android head project, build it locally with ANDROID_HOME=/root/Android/Sdk, then add the CI job. A CI run needs a push, which is a separate decision. Owner: engineering, then owner (push). Route: ROADMAP Phase 24 criterion 4.
- iOS build, accessibility dump, signing and device run on a Mac with Xcode. Owner: owner, Mac channel. Route: XP-11 entry in the runbook.
- Create the iOS and Android head projects once the host blockers are resolved. Owner: engineering. Route: ROADMAP Phase 24.

## Commits

- No Phase 24 code commit.
- 1f4cb4f: ROADMAP reconciliation with the v0.9.2 audit (planning record)
- 9e5913d: gate-green HEAD

## Run 2026-10-25 (gsd-autonomous, graph ut-mvmegw7f-4a7312d5)

Status: partially complete. Criterion 4: local build met; CI run not done (no push). Criteria 1 to 3 and 5 blocked.

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | iOS head builds for net10.0-ios | Blocked | Mac channel (no Mac with Xcode) |
| 2 | UIAccessibility dump on iOS 26 | Blocked | Mac channel |
| 3 | Code signing and device runs | Blocked | Owner, Mac channel |
| 4 | Android head builds for net10.0-android in CI on the Linux runner | Partial | Local: `dotnet build apps/OmpOS.Android ... -warnaserror` succeeded (docs/runbooks/evidence/phase-24-android-2026-10-25/). CI: job `android` written, not pushed |
| 5 | Android 16 accessibility dump | Blocked | No device; /dev/kvm absent |

Gate on the working tree after this phase: `./bootstrap.sh` exit 0, "bootstrap: OK", total 2245, failed 0, skipped 0, web smoke exit 0. One run failed `LinuxLocalPtyBackendTests.DisposeEndsARunningProcessAndReturns` with an IOException reading `/proc/<pid>/stat` after the process was reaped; the rerun passed. Recorded as flaky. The terminal harness is not touched by this phase. Follow-up: the test helper should treat a missing `/proc` entry as an ended process.

Deviations:
- D1. The shared split. `MainWindow` becomes a Window host (`apps/OmpOS.Native/MainWindow.axaml(.cs)`) and `MainView` (`src/OmpOS.Native.Views`). The host holds the send path in a field, and `MainView.Close()` disposes it on close, as before.
- D2. `IPromptChannel` is a public seam, so `MainView` never references the desktop `EnginePromptChannel`.
- D3. `UnconnectedRunInput` and `UnconnectedToolApprovalBridge` are public, for the Android head.
- D4. `MainView` has a public parameterless constructor (no engine, current directory). Avalonia's XAML loader needs it; without it, AVLN3001 fails the build under warnings as errors.
- D5. The architecture tests exempt the generated `CompiledAvaloniaXaml` helper namespace from the layer rule. Each XAML-compiling assembly generates its own copy under the same name, so the loader reads them as one type and reports a false edge from the shared library to the desktop head. Product types stay checked.
- D6. The architecture tests accept an absent Android head in the Linux test output, because the net10.0 test projects cannot reference net10.0-android. The MAUI head already has this exception.
- D7. The Android head derives from Avalonia 12.1.4's `AvaloniaAndroidApplication<T>`, which builds the application with the Android platform. The activity is non-generic.
- D8. The Android head's workspace is the app's private files folder, created before the view is built.
- D9. Criterion 4 names CI, so the `android` job is in `ci.yml`, not pushed (G24-2).

Follow-ups outside this phase's scope: the Window styles of the desktop head are not carried to Android (intended; confirm on a device); the flaky terminal helper above.
