# 73-06 Summary

Approval, indicator, permission advisor, settings page, and head composition for Computer Use.

## What landed

- `ComputerApprovalViewModel` shows host facts only: quoted title at 80 characters, host-words action, crop or agent-text preview, Local or `Dispatch: <device>`, security phrase. Deny, Allow once, and Allow for this session. A 120 s `TimeProvider` timer denies. `TryApplyRemote` always returns false.
- `ComputerIndicatorViewModel` reads `IKillSwitchControl.StateChanged`. Stop calls `TripAsync`. Dispatch shows `Remote (Dispatch): <device>`. Armed and idle versus acting.
- Avalonia dialog and indicator use compiled bindings and `DynamicResource` tokens. Deny is focused when the dialog opens. The indicator is top-most and not focusable.
- `ComputerPermissionAdvisor` maps every `ComputerAvailability` to a panel with a deep link or why-blocked text. The poll is 1 s for at most 5 minutes. A missing permission does not name another mechanism.
- Settings page covers provider disclosure, opt-in, grants, denylist additions, kill chord, security phrase, and audit. `SettingsSection.ComputerUse = 22`.
- `ComputerUseHeadComposition.ChooseBackend` registers X11, Windows, or macOS, and `NullComputerUseBackend` otherwise (`computer.unavailable.platform`). `ComputerAccessFactsAdapter` reads `ICoworkAccountGate` only.
- `FileComputerUseSettingsStore` writes owner-only `computer-use/settings.json` (schema 1, 64 KiB). Unknown version, oversize, and malformed files return defaults and `LastFailure`.
- Presentation references `Autonogrammer.Harness.ComputerUse.Abstractions` only. `docs/architecture.md` states the desktop-only boundary.

## Tests

`/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*DispatchOriginTests*" --filter-class "*PermissionAdvisorTests*" --filter-class "*PresentationIsolationTests*" --filter-class "*NetworkAndKeyboardCopyTests*"` passed (17).

Native headless suites (`ApprovalDialogViewTests`, `IndicatorWindowViewTests`, `ComputerUseSettingsPageTests`, `SettingsWindowTests`, `FileComputerUseSettingsStoreTests`, `ComputerUseHeadCompositionTests`) did not run: sibling untracked Cowork files under `src/Autonogrammer.Presentation/Cowork/{Projects,Memory,Instructions,Scheduling}` do not compile, and `ConventionCatalog.ScanPluginMarkdownAsync` is over the method-length gate while that sibling edit is in the tree.

## Gaps

- `apps/Autonogrammer.Native/Cowork/CoworkHead.cs` is not on disk. Composition does not invent `CoworkHead`, `CoworkToolHost`, or `ICoworkToolProvider`. `ICoworkAccountGate` is on disk and is the only account read. `ComputerUseHeadComposition.SettingsFactory` stays null until that head lands and assigns a page factory. AgentHead evaluates the factory at both settings construction sites.
- `docs/parity/cowork-parity.md` is not on disk (manifest: 75-06 creates it). Rows for the owning phase: Computer Use approvals stay local; Dispatch origin is a label (`Dispatch: <device>` on the prompt, `Remote (Dispatch): <device>` on the indicator); the gate denies when the indicator is absent; settings cover disclosure, grants, denylist additions, kill chord, security phrase, and audit.
- Indicator placement on every display edge is a head duty against `SurfaceChromeModel` / HeaderEndSlot. This plan adds the entry when `ComputerUseHeadOptions.Chrome` is set. It does not edit the web HeaderEndSlot.
- No package was added. Licences and notices were not regenerated.

## Registry

No slnx, slnf, LayerMap, ProjectGraphTests, ScaffoldRegistrationTests, Directory.Packages.props, ContractVersion, CHANGELOG, or THIRD-PARTY-NOTICES edit. The only csproj edit is the Presentation reference to ComputerUse.Abstractions. Lock files record that project dependency.
