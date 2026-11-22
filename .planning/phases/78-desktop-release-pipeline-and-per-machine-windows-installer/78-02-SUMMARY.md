---
phase: 78-desktop-release-pipeline-and-per-machine-windows-installer
plan: "02"
subsystem: native-app, launch-directory, webview2
tags: [read-only-install, launch-directory, webview2]
dependency_graph:
  requires: []
  provides: [read-only-safe-launch, webview2-per-user-data]
  affects: [78-01, 78-06]
tech_stack:
  added: []
  patterns: [install-dir refused as workspace, head-resolved WorkspaceRoot to window, WebView2 data under LocalApplicationData]
key_files:
  modified:
    - apps/Autonogrammer.Native/Agents/LaunchDirectory.cs
    - apps/Autonogrammer.Native/Agents/AgentLoopEnvironment.cs
    - apps/Autonogrammer.Native/App.axaml.cs
    - src/Autonogrammer.Native.Views/Views/Cowork/Artifacts/NativeWebViewArtifactSurface.cs
    - tests/Autonogrammer.Native.Tests/AgentLoop/LaunchDirectoryTests.cs
    - tests/Autonogrammer.Native.Tests/WebView2UserDataFolderTests.cs
decisions:
  - "LaunchDirectory refuses install folder/system roots/missing dirs → home dir; terminal launches inside a project keep that project"
  - "Window uses head.WorkspaceRoot, never Environment.CurrentDirectory"
metrics:
  tasks: 2
  status: complete
---

# Phase 78 Plan 02: Read-Only Install Directory Summary

**One-liner:** Native app is safe to launch from `C:\Program Files` as a standard user: install-dir workspace refused, WebView2 data forced under per-user LocalApplicationData, pinned by `LaunchDirectoryTests` (18) and `WebView2UserDataFolderTests` (3).

## Tasks Executed

**Task 1 (auto, tdd):** `apps/Autonogrammer.Native/Agents/LaunchDirectory.cs` resolver + `AgentLoopEnvironment.Resolve` + `App.axaml.cs CreateWindow(head.WorkspaceRoot)`. Behaviors pinned: CWD == or under base dir → home; system root / filesystem root / missing dir → home; other existing dir → itself normalized (no trailing separator). `tests/Autonogrammer.Native.Tests/AgentLoop/LaunchDirectoryTests.cs`.

**Task 2 (auto, tdd):** `NativeWebViewArtifactSurface.ConfigureEnvironment`: Windows branch sets `UserDataFolder = WebView2UserDataFolder()` = `Autonogrammer/webview2` under `LocalApplicationData`; test asserts the combined path and that it is never under `AppContext.BaseDirectory`. `tests/Autonogrammer.Native.Tests/WebView2UserDataFolderTests.cs`.

## Verification Performed

- `grep -n "UserDataFolder" src/Autonogrammer.Native.Views/Views/Cowork/Artifacts/NativeWebViewArtifactSurface.cs` → lines 133, 150, 151 observed this batch
- 78-VERIFICATION criterion 2 (observed evidence): `LaunchDirectoryTests` 18/18, `WebView2UserDataFolderTests` 3/3 (both `--filter-class` matched ≥1) — **not re-run in this batch** (dotnet test out of batch scope; cited evidence is 78-VERIFICATION.md's recorded run on `08601167`)
- Smoke-test assertion "nothing written under install directory" belongs to 78-01 rehearsal run `44524125822`

## Deviations

None. No new feature; no signing/notarization (owner out-of-scope).

**Status:** 78-02 complete (per 78-VERIFICATION criterion 2 evidence).
