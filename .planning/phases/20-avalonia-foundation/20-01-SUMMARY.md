---
phase: 20-avalonia-foundation
plan: "01"
subsystem: ui
tags: [plugin-contracts, presentation-layer, central-packages, avalonia, architecture-tests, xunit-v3]

requires: []
provides:
  - "Five UI-free plugin contribution contracts in OmpOS.Contracts.Plugins (descriptor, command, panel, tool, agent mode)"
  - "OmpOS.Presentation project (L2) with MotionPreference and a framework-neutral explicit-registration PluginHost"
  - "Contract and isolation tests that fail on any Avalonia, MAUI or ASP.NET Core Components reference"
  - "Avalonia 12.1.4 central PackageVersion pins (five entries)"
  - "LayerMap, OmpOS.slnx, OmpOS.Linux.slnf, ArchitectureTests reference and .axaml TODO scanner wiring"
affects: [20-02 presentation extraction, 20-03 OmpOS.Native head, 20-04 gate and evidence]

key-files:
  created:
    - src/OmpOS.Contracts/Plugins/PluginDescriptor.cs
    - src/OmpOS.Contracts/Plugins/CommandContribution.cs
    - src/OmpOS.Contracts/Plugins/PanelContribution.cs
    - src/OmpOS.Contracts/Plugins/ToolContribution.cs
    - src/OmpOS.Contracts/Plugins/AgentModeContribution.cs
    - src/OmpOS.Presentation/OmpOS.Presentation.csproj
    - src/OmpOS.Presentation/MotionPreference.cs
    - src/OmpOS.Presentation/Plugins/PluginHost.cs
    - tests/OmpOS.Presentation.Tests/OmpOS.Presentation.Tests.csproj
    - tests/OmpOS.Presentation.Tests/PresentationIsolationTests.cs
    - tests/OmpOS.Presentation.Tests/PluginContractTests.cs
    - tests/OmpOS.Presentation.Tests/Plugins/SamplePlugin.cs
    - tests/OmpOS.Contracts.Tests/Plugins/ContractsUiFreeTests.cs
  modified:
    - Directory.Packages.props
    - tests/OmpOS.ArchitectureTests/LayerMap.cs
    - tests/OmpOS.ArchitectureTests/OmpOS.ArchitectureTests.csproj
    - tests/OmpOS.ArchitectureTests/TodoMarkerScanner.cs
    - tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs
    - OmpOS.slnx
    - OmpOS.Linux.slnf

key-decisions:
  - "PluginHost gained RegisterPlugin and RegisteredPlugins in addition to the four contribution kinds. A contribution must name a registered plugin, so PluginId on each contribution has a use and the descriptor is not dead weight."
  - "PluginHost refuses a duplicate identifier within each contribution kind (ArgumentException, paramName set). It refuses a contribution whose plugin is not registered."
  - "Contribution records share one shape: PluginId, Id, Title, Description (all required init). No shared base record, because the four kinds carry no common behaviour."
  - "Test namespaces follow folders: OmpOS.Contracts.Tests.Plugins and OmpOS.Presentation.Tests.Plugins."
  - "Presentation project references Markdig with no version (central pin), as the plan states. No Presentation code uses Markdig yet; the MarkdownRenderer move is in plan 20-02."
  - "The Avalonia pins are added with no project referencing them in this wave, so restore does not resolve them yet. Their first consumer is plan 20-03."

patterns-established:
  - "Plugin contributions are sealed records in OmpOS.Contracts.Plugins with required init members and per-member XML summaries, copied from SandboxDescriptor."
  - "The host registers explicitly. No assembly scanning, no reflection, no Process, no JS."
  - "Isolation is checked from metadata: csproj PackageReference and ProjectReference names, Assembly.GetReferencedAssemblies, and the base types and interfaces of exported types."

requirements-completed: [XP-01, XP-09]

# Metrics
duration: not measured
completed: 2026-10-21
status: complete
---

# Phase 20: Avalonia Foundation, Plan 01 Summary

**Five UI-free plugin contracts, a framework-neutral OmpOS.Presentation host with a sample-plugin registration test, Avalonia 12.1.4 central pins, and the layer, solution, filter and TODO-scanner wiring, all written but not yet built or run.**

## Performance

- **Duration:** not measured
- **Tasks:** 3 of 3 implemented
- **Files created:** 13
- **Files modified:** 7

## Verification status

No build, restore, test, lint or format command was run, as the orchestrator contract requires. Every gate result below is pending the orchestrator. Nothing is recorded as passed.

## Accomplishments

- Five sealed contribution records in `OmpOS.Contracts.Plugins` (`PluginDescriptor`, `CommandContribution`, `PanelContribution`, `ToolContribution`, `AgentModeContribution`). None references a UI framework.
- `OmpOS.Presentation` (L2) project with `Markdig` as its only package, plus `ProjectReference`s to `OmpOS.Contracts` and `OmpOS.GenUi`.
- `MotionPreference` enum (`System`, `Reduced`, `Full`). `System` resolves to `Full` only in the consumer, as D-20-08 requires.
- `PluginHost`: framework-neutral, explicit `RegisterPlugin`, `RegisterCommand`, `RegisterPanel`, `RegisterTool` and `RegisterAgentMode`, with read-only list properties.
- Tests: `ContractsUiFreeTests` (6 cases), `PresentationIsolationTests` (3), `PluginContractTests` (3, including the one-of-each sample-plugin registration), and the `.axaml` TODO control test in `RepoHygieneTests`.
- `SamplePlugin` (test code only) registers one command, one panel, one tool and one agent mode through the host.
- `Directory.Packages.props`: `Label="Avalonia native head"` group with `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Headless`, `Avalonia.Headless.XUnit` at `12.1.4`. No transitive pins added (A-06).
- `LayerMap`: `OmpOS.Presentation = 2`; same-layer allow-list gains `(OmpOS.UI, OmpOS.Presentation)` and `(OmpOS.Presentation, OmpOS.GenUi)`; doc comment updated. No test projects added (A-03).
- `OmpOS.slnx` and `OmpOS.Linux.slnf` register the Presentation and Presentation.Tests projects. The slnf stays valid JSON (checked with a JSON parser, which is not a build).
- `OmpOS.ArchitectureTests.csproj` references `OmpOS.Presentation` only.
- `TodoMarkerScanner` scans `.axaml` (A-04), and the doc comment lists it.

## Task Status

| Task | Status | Notes |
|---|---|---|
| 1. Plugin contracts plus UI-free contract test | done (gate pending) | Five records and `ContractsUiFreeTests`. Test assumptions are listed under Issues and Risks. |
| 2. Presentation skeleton, plugin host, isolation test, sample plugin | done (gate pending) | Project, enum, host, three test files, sample plugin and test csproj. |
| 3. Package pins, layer and solution wiring, scanner AXAML coverage | done (gate pending) | All wiring edits and the `.axaml` control test. |

## Files Created/Modified

Created:
- `src/OmpOS.Contracts/Plugins/PluginDescriptor.cs`, `CommandContribution.cs`, `PanelContribution.cs`, `ToolContribution.cs`, `AgentModeContribution.cs`
- `src/OmpOS.Presentation/OmpOS.Presentation.csproj`, `MotionPreference.cs`, `Plugins/PluginHost.cs`
- `tests/OmpOS.Presentation.Tests/OmpOS.Presentation.Tests.csproj`, `PresentationIsolationTests.cs`, `PluginContractTests.cs`, `Plugins/SamplePlugin.cs`
- `tests/OmpOS.Contracts.Tests/Plugins/ContractsUiFreeTests.cs`

Modified:
- `Directory.Packages.props` (Avalonia group)
- `tests/OmpOS.ArchitectureTests/LayerMap.cs`, `OmpOS.ArchitectureTests.csproj`, `TodoMarkerScanner.cs`, `RepoHygieneTests.cs`
- `OmpOS.slnx`, `OmpOS.Linux.slnf`

No file outside the plan's Files list was touched. This summary is the only addition.

## Decisions Made

- `PluginHost` also has `RegisterPlugin` and `RegisteredPlugins`. Each contribution names a plugin, and the host rejects a contribution whose plugin is not registered.
- Identifiers are unique within each contribution kind. A duplicate is rejected with `ArgumentException`.
- The host is documented as not thread-safe and as meant for startup registration.
- Contribution records share the shape `PluginId`, `Id`, `Title`, `Description`. Descriptor fields are `Id`, `Name`, `Version`.

## Deviations from Plan

1. **Host validation and `RegisterPlugin`.** The plan asks for explicit Register and List methods for the four kinds. I added `RegisterPlugin`/`RegisteredPlugins` and the unregistered-plugin and duplicate-id checks. Reason: without a registered-plugin check the `PluginId` field and the `PluginDescriptor` contract have no consumer. This is a small addition, not a scope change.
2. **Two extra host tests.** `RegisterCommand_RejectsContributionFromUnregisteredPlugin` and `RegisterCommand_RejectsDuplicateIdentifier` test the validation above. The plan's behaviour list named only the registration test.
3. **Namespaces follow folders.** The plan did not fix test namespaces. Tests under `Plugins/` use `*.Tests.Plugins` to match the folder convention (`dotnet_style_namespace_match_folder`).
4. **XML documentation is not asserted by a test.** The plan's Task 1 behaviour says the contract test checks "XML summary docs". A runtime test cannot read doc comments. The build enforces them instead: CS1591 is at its default warning level for product code (the `none` override in `.editorconfig` applies only to `tests/**`), and warnings are errors. The test checks sealed, `IEquatable<T>` and required-member shape only.

## Issues Encountered

None during editing. Two edits in `MotionPreference.cs` broke the doc comment and an unresolved `cref` (`Full`). Both were repaired before the file was finished, and the final file was read back.

## Risks and Unverified Items

- **Not compiled.** No build ran, so every compile claim is unverified. Most likely failures: analyzer diagnostics under `AnalysisLevel latest-all` (warnings are errors), and Shouldly API shapes. I used `ShouldHaveSingleItem()` without relying on its return value and `Should.Throw<T>` with its return value. The Shouldly XML in the local cache does not list these members, so I could not confirm the return types there.
- **`RequiredMemberAttribute` detection** (`ContractsUiFreeTests.HasRequiredMembers`) checks the attribute on the type or on its properties by full name. I believe the compiler emits it on members, but this was not confirmed in this environment. If the test fails with no real defect, adjust the check to the emitted metadata.
- **Lock files are missing for the two new projects.** `RestorePackagesWithLockFile` is on, and `RestoreLockedMode` is on in CI. The plan's Files list does not include `packages.lock.json`, and D-20-12 places restore and lock creation in the execution phase. Without the lock files, CI restore fails. The orchestrator must run `dotnet restore --force-evaluate` for `src/OmpOS.Presentation/OmpOS.Presentation.csproj` and `tests/OmpOS.Presentation.Tests/OmpOS.Presentation.Tests.csproj`, and commit both lock files.
- **Plan verify step for the Avalonia pins does not apply in this wave.** The plan says restore resolves the five Avalonia pins. No project references them until plan 20-03, so restore will not resolve them here. Only the lock-file step above matters now.
- **Markdig is referenced with no consumer yet.** Harmless, but it is an unused reference until the 20-02 move.
- **ArchitectureTests now loads the Presentation assembly.** `ArchUnitLayerTests` requires its built DLL, which the new `ProjectReference` provides.

## User Setup Required

None. No external service configuration is needed.

## Verification the Orchestrator Must Run

1. `dotnet restore --force-evaluate` for the two new projects, then review and commit their `packages.lock.json` (D-20-12).
2. `./bootstrap.sh`, or the equivalent: build `OmpOS.Linux.slnf` with warnings as errors, then run tests.
3. Expect green: `ContractsUiFreeTests`, `PresentationIsolationTests`, `PluginContractTests`, `ArchitectureTests` (ProjectGraph and ArchUnit with the new LayerMap entries), and the `RepoHygieneTests` `.axaml` control test.
4. `dotnet format --verify-no-changes` on the changed projects.
5. Confirm restore for the Avalonia pins is not yet required (no consumer), and record that it will be resolved in plan 20-03.

## Next Phase Readiness

- Plan 20-02 can start from `OmpOS.Presentation` and `PluginHost`. Its moves go into the namespaces `OmpOS.Presentation`, `OmpOS.Presentation.State` and `OmpOS.Presentation.Services`, per the mirror-folders decision in 20-PATTERNS.md section F.
- Plan 20-03 can add `OmpOS.Native` (L4, `LayerMap` and wiring) and consume the Avalonia pins.
- Blockers: none in this wave, beyond the lock-file step and the unbuilt-code risks above.

---
*Phase: 20-avalonia-foundation*
*Completed: 2026-10-21*
