# Phase 24: iOS and Android Heads, discuss record

Recorded 2026-10-24 by the gsd-autonomous run (graph ut-mvm12su0-1fae51f6). Milestone v0.10.0. Status: BLOCKED. Criterion 4 is partial.
Depends on Phases 20 and 21 (complete). Mac channel: yes, for iOS. UI hint: yes.

## Goal and criteria (ROADMAP.md lines 456 to 467)

Goal: the iOS and Android heads build from the shared project and expose names and roles through their platform accessibility APIs.

1. The iOS head builds for net10.0-ios on iOS or iPadOS 26, and the log is recorded (Mac channel).
2. On an iOS 26 run, UIAccessibility returns the name and role of the prompt input, send control and transcript (Mac channel).
3. Code signing and iOS device runs are performed by the owner on the Mac channel, and are listed under XP-11.
4. The Android head builds for net10.0-android in CI on the Linux runner. Verify: the CI run log.
5. On an Android 16 run, AccessibilityNodeInfo returns the name and role of the three controls. Verify: an emulator or device dump.

## Current evidence

- Criteria 1 to 3: blocked on the Mac channel.
- Criterion 4: partial. Installed 2026-10-24 under this run's authorization: platform android-36, build-tools 36.0.0 and the .NET workload android 36.1.118 (SDK 10.0.400 band), with ANDROID_HOME=/root/Android/Sdk set per build. No Android head project exists, no CI job builds one, and no CI log exists.
- Criterion 5: blocked. /dev/kvm is absent, so no emulator runs on this host, and no device is attached.

## Verified paths

- apps/Autonogrammer.Native/Autonogrammer.Native.csproj and apps/Autonogrammer.Native/Program.cs (the shared Avalonia native project)
- Directory.Packages.props: Avalonia 12.1.4 pins at lines 21 to 25, Avalonia.Skia at line 79
- .github/workflows/ci.yml: build-test job from line 28 (ubuntu-latest), shells job from line 135 (macos-15), which restores apps/Autonogrammer.App at line 148 with --locked-mode
- CONTRIBUTING.md, section "Adding a project" (the five steps for a new project)
- docs/decisions/ADR-0004-cross-platform-ui.md

## Reuse and conventions

- Unify, do not invent. The native head is one Avalonia project. Each new head reuses it through a shared library, and does not repeat the shell.
- Central package management: Avalonia.Android is pinned in Directory.Packages.props at the same version as the native head.
- New projects follow the five steps in CONTRIBUTING.md.

## Edge cases

- The Android project must build with warnings as errors, from a clean restore with --locked-mode.
- The shells job restores apps/Autonogrammer.App with --locked-mode. The quality audit recorded that this project has no lock file, so that job would fail. This is an inference (not run) and belongs to Phase 25.

## Grey areas

- G24-1, material (cross-phase: changes how the native head depends on shared code, and Phase 25 removes apps/Autonogrammer.App). Question: how is the Android head structured? (a) A new Android head project that references a shared Avalonia library, with the views in apps/Autonogrammer.Native moved into that library as a behavior-neutral move. (b) A separate Android project that repeats the native shell. Default: (a), because the repository's convention is one shared Avalonia project and (b) is a second convention. Rejected (a): (b) needs a recorded reason.
- G24-2, material (external effect). Question: write the Android CI job now, and authorize a push so the CI run can log criterion 4? Default: write the job in ci.yml, because criterion 4 names CI, and do not push. Accepted: the branch is pushed for a CI run. GIT_SAFETY allows a push only when a phase requires one, and this phase does.
- G24-3, decided, immaterial: Avalonia.Android is pinned at 12.1.4, as the native head is. No other package is added. ANDROID_HOME is set per build, and no global setting changes.

## Automated and manual

- Criterion 4: automated, after G24-2 is answered. The log comes from the CI run.
- Criterion 5: manual device run. Blocked on a device, or on a host with KVM.

## Pauses

- GREY_AREA: G24-1 and G24-2, batched with the other phases.
- BLOCKER: criteria 1 to 3 (a Mac with Xcode, and an iOS 26 device for the device run). Criterion 5 (an Android 16 device, or a KVM-capable host).

## Run record 2026-10-25 (graph ut-mvmegw7f-4a7312d5)

- G24-1 accepted (owner answer: new head over a shared Avalonia library). `src/Autonogrammer.Native.Views` (layer 3) holds the views, the theme, the fonts, the main view and the engine-independent inputs. `apps/Autonogrammer.Native` keeps Program, the desktop App, the MainWindow host and the engine composition. `apps/Autonogrammer.Android` is the Android head (layer 4).
- G24-2 accepted (owner answer: write the job, no push). The `android` job is in `.github/workflows/ci.yml`. It is not pushed, so criterion 4's CI run is not done.
- Criterion 4, local part: met. `ANDROID_HOME=/root/Android/Sdk dotnet build apps/Autonogrammer.Android/Autonogrammer.Android.csproj -c Release -warnaserror` succeeds. Evidence: docs/runbooks/evidence/phase-24-android-2026-10-25/.
- Criterion 5 stays blocked (no Android 16 device; no /dev/kvm). Criteria 1 to 3 stay blocked on the Mac channel.
