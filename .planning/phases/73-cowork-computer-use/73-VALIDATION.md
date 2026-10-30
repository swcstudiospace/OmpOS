# Phase 73 Validation Strategy (Nyquist)

Derived from the Validation Architecture section of 73-RESEARCH.md and the six success
criteria of the ROADMAP phase 73 block. No build or test was run while writing this file.

## Requirement-to-test map

| Requirement | Plans | Test classes |
| --- | --- | --- |
| AG-73-1, 3, 10, 18 (states) | 73-01 | CoordinateMapTests, ComputerUseOptionsValidatorTests, DenialCodeCatalogTests |
| AG-73-2, 4, 5, 6, 7, 9, 11, 12 (takeover) | 73-02 | GateOrderTests, GrantTierTests, DenylistTests, ForbiddenChordTests, SensitivityClassifierTests, RateLimiterTests, KillSwitchCoordinatorTests, ApprovalSerialisationTests, OwnWindowTests, ComputerUseBoundaryTests |
| AG-73-13, 14, 15 | 73-03 | VisibleRegionMaskerTests, FrameScalerTests, PngEncoderTests, ImageBudgetTests, AuditLogChainTests, AuditRedactionTests, ComputerToolProviderTests |
| AG-73-22 (non-UI: registration rule, no relay op, Dispatch grant ceiling) | 73-02, 73-03 | GrantTierTests (30-minute ceiling), ComputerUseDispatchRegistrationTests |
| AG-73-16, 17, 18 (Wayland) | 73-04 | KeysymMapperTests, WaylandDetectionTests, X11BackendLiveTests (Live) |
| AG-73-19, 20 | 73-05 | SendInputBuilderTests, CoordinateScaleTests, ScreenCaptureArgumentsTests |
| AG-73-8, 12 (indicator), 21, 22 (UI side), 23 | 73-06 | ApprovalDialogViewTests, IndicatorWindowViewTests, ComputerUseSettingsPageTests, PermissionAdvisorTests, DispatchOriginTests |
| AG-73-24 | 73-06 | ProjectGraphTests unchanged, licence plus notices checks unchanged |

## Quick run (per plan, from the worktree root)

```
/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.ComputerUse.Tests -c Release -- --filter-not-trait "Category=Live"
/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.ComputerUse.X11.Tests -c Release -- --filter-not-trait "Category=Live"
/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.ComputerUse.Windows.Tests -c Release -- --filter-not-trait "Category=Live"
/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.ComputerUse.MacOS.Tests -c Release -- --filter-not-trait "Category=Live"
/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*PermissionAdvisorTests*" --filter-class "*DispatchOriginTests*"
/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*ApprovalDialogViewTests*" --filter-class "*IndicatorWindowViewTests*" --filter-class "*ComputerUseSettingsPageTests*"
/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*ComputerUseBoundaryTests*" --filter-class "*ProjectGraphTests*"
```

Each runnable command fails when: non-zero exit, or the `Failed:` count is above 0,
or the summary shows `Total: 0` / `0 passed` (a filtered run matching zero tests counts
as failure).

## Full run (all six ROADMAP success criteria)

1. Core offline tests: the first command above passes.
2. X11 Live under a private Xvfb, or every test skips with reason:
   `AUTONOGRAMMER_LIVE_X11=1 /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.ComputerUse.X11.Tests -c Release -- --filter-trait "Category=Live"`
   The fixture starts `Xvfb -displayfd 3 -nolisten tcp -screen 0 1280x800x24` on a free
   display, passes the display name to the backend options, and never reads or sets the
   session `DISPLAY`. Each Live test opens with `Assert.Skip` unless
   `AUTONOGRAMMER_LIVE_X11=1` and Xvfb exists ("Xvfb not found").
3. Live cases: xdotool pointer read-back; backend click logged by xev; type of
   `Hello, ünï` reaching xev as the same keysyms; scrot and backend frames of a fixed
   xterm agreeing within per-channel tolerance; hotkey firing on
   `xdotool key ctrl+alt+shift+Escape`; second grab reporting `KillSwitchUnavailable`;
   bare Xvfb listing windows through `XQueryTree`.
4. Compile gates (warning-free on Linux, never `dotnet` directly):
   `/tmp/ag-bin/ag-dotnet build src/Autonogrammer.Harness.ComputerUse.Windows/Autonogrammer.Harness.ComputerUse.Windows.csproj -c Release --no-restore -warnaserror -nodeReuse:false -m:1`
   and the same path with `.MacOS`.
5. Graph plus boundary plus decision records:
   `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*ProjectGraphTests*"`
   plus `--filter-class "*ComputerUseBoundaryTests*"` plus
   `--filter-class "*DecisionRecordTests*"` (ADR-0035 indexed).
6. No-package gates: `bash scripts/ci/check-licences.sh` and
   `python3 scripts/legal/generate-third-party-notices.py --check` pass unchanged.

## Live gating

Live-category tests carry `[Trait("Category", "Live")]` and an environment gate with
`Assert.Skip` as the first statement, so selecting the trait without the variable skips
rather than fails (report 09 section 6 convention). The default gate excludes them with
`--filter-not-trait "Category=Live"`.

## Cannot be verified on this host

Windows (SendInput, UIPI, GDI, secure desktop, hotkey); macOS (TCC, CGEvent,
`screencapture` attribution, Carbon hotkey, Spaces); Wayland portals; AT-SPI; real
multi-DPI multi-monitor; non-US layouts; top-most over full-screen applications; live
model vision accuracy and cost; omp reaching the tools and honouring
`computer.enabled=false` (OD-07). X11 is verifiable under a private Xvfb.

## Nyquist verdict rule

`nyquist_compliant` holds for this phase when every row above has a passing (or
reason-stated skipped, for Live-only) run recorded at the phase HEAD, and any gap names
its blocker from the cannot-be-verified list instead of claiming success.
