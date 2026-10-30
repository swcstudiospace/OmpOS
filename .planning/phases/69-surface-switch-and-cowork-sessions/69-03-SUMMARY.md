---
phase: 69-surface-switch-and-cowork-sessions
plan: 69-03
subsystem: native
tags: [cowork, surface-host, settings, shortcuts]
requires:
  - phase: 69-01
    provides: ICoworkAccountGate, CoworkAccountFolder, CoworkSessionRegistry
  - phase: 69-02
    provides: SurfaceSwitchModel, SurfaceChromeModel, CoworkShellModel
provides:
  - SurfaceHost holding MainView and CoworkView with visibility-only switching
  - shell.surface persistence through SettingsShellSection and FileSettingsStore.SetSurface
  - Ctrl+Shift+1/2 and Meta+Shift+1/2 tunnel handling
  - NativeCoworkAccountGate and CoworkSessionCatalogAdapter
affects: [69-04, 70-04]
actuals:
  tokens: 0
  tasks: 3
  commits: 1
tech-stack:
  added: []
  patterns:
    - Window content is SurfaceHost; ShellHarness.MainViewOf and HeadSettings.Attach resolve MainView through SurfaceHost.Code
    - Persisted surface words come only from SurfaceSwitchModel.Format and Parse
key-files:
  created:
    - src/Autonogrammer.Native.Views/Shell/SurfaceHost.axaml
    - src/Autonogrammer.Native.Views/Shell/SurfaceSwitchView.axaml
    - src/Autonogrammer.Native.Views/Views/Cowork/CoworkView.axaml
    - apps/Autonogrammer.Native/Settings/SettingsShellSection.cs
    - apps/Autonogrammer.Native/NativeCoworkAccountGate.cs
    - apps/Autonogrammer.Native/Cowork/CoworkSessionCatalogAdapter.cs
  modified:
    - src/Autonogrammer.Native.Views/MainView.axaml
    - apps/Autonogrammer.Native/MainWindow.axaml.cs
    - apps/Autonogrammer.Native/Settings/FileSettingsStore.cs
key-decisions:
  - "CoworkSurfaceServices names no Autonogrammer.Cowork type. The head adapter opens the registry."
  - "Ctrl+Shift+1/2 are handled on SurfaceHost before MainView, and MainView key cases are unchanged."
  - "ConversationHost stays empty for phase 70."
patterns-established:
  - "Pattern: both top bars bind one SurfaceSwitchModel and one SurfaceChromeModel."
requirements-completed: [AG-69-1, AG-69-2, AG-69-3, AG-69-4, AG-69-8, AG-69-12, AG-69-13]
coverage:
  - id: D-04
    description: Segmented Code | Cowork toggles with exact accessible names
    verification:
      - kind: test
        ref: SurfaceSwitchViewTests
    human_judgment: false
  - id: D-08
    description: shell.surface round trip and unusable value
    verification:
      - kind: test
        ref: SurfaceSettingsTests
    human_judgment: false
metrics:
  tasks: 3
  files: 30
---

# 69-03 Summary

Native window content is a `SurfaceHost`. With `CoworkSurfaceServices` the Code | Cowork switch is shown and both stacks stay alive. Without services the switch is absent and Code stays selected.

`shell.surface` is stored as `code` or `cowork` from `SurfaceSwitchModel`. An unusable value throws the existing settings error and is not overwritten.

## Gaps

- `ShellFixture.Main` now calls `ShellHarness.MainViewOf` so existing shell tests keep resolving the main view. That file was not on the plan list.
- A full `ag-dotnet test` of the Native test project is blocked while sibling projects `Autonogrammer.Harness.BrowserUse` and `Autonogrammer.Harness.ComputerUse` fail to compile. Targeted classes were run with `BuildProjectReferences=false` before a disk-full wipe of `bin/`.
---
