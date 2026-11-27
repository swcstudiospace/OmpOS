# Phase 81 Summary — Terminal Engine Retirement (81-01)

Linear: SPE-37886. ADR-0038 decision 4.

## OQ-527 mapping as shipped

`SessionKind.Terminal = 1` and `CatalogSessionKind.Terminal` ("terminal") remain defined forever as read-only
persisted/wire values — no state-file migration, no renumbering (`AgentSessionStateFile.ParseKind`/`TryParseKind`
parses any defined enum name, fallback Chat). Every read path (ParseKind, registry load, `CatalogMapper`, Native
runtime load) yields a *retired* presentation: listed with word tag "Terminal" + word status, opening renders
`error.unavailable` (SCR-SessionTerminal → `TerminalView` Unavailable frame), and no path constructs a
starter/transport/engine. Retired copy never promises PTY transcript replay (UX open question (a) stays open) and
offers New Agent (Chat) only. Closed in `docs/open-questions.md` OQ-527 with test citations.

## Deletion inventory — final dispositions

Prior-lane commits: `e01f4811` (Tasks 1-3), `b04d3ba1` (Tasks 4-10), `bff6f24e` (Task 11), `de722175`
(Tasks 12, 13 hunks 1-2).

| Inventory site | Disposition | Recount |
|---|---|---|
| `SessionEngineSet` creation + starter wiring | Modified: 0 production `registry.Create(..., SessionKind.Terminal)` | Tasks 6/13 hunks 1-2 |
| `PlannedOmpTerminalStarter.cs` | Deleted | Task 13 hunk 1 (3 files/5 hits incl. tests) |
| `OmpTerminalLauncher` + 5 result/request files + 4 test files | Deleted | Task 13 hunk 2 (9 files/24 hits incl. tests) |
| `OmpLaunchPlan.ForTerminal` + `OmpTerminalCommand` + `OmpTerminalOptions` + `OmpTerminalCommandTests` | Deleted (this lane, Task 13 hunk 3) | `rg -lw` word-boundary, prod `src apps services`: `ForTerminal`=1 file (own `OmpLaunchPlan.cs`), `OmpTerminalCommand`=2 files (own file + plan), `OmpTerminalOptions`=2 files (own file + plan). `RemoteOmpTerminalOptions` is a separate kept Remote type. Tests rewritten: `OmpTerminalCommandTests` deleted; `MultiRootLaunchTests:118`, `HookBridgeLaunchTests:126`, `OmpLaunchPlannerTests:311`, `ProviderKeyHeadTests:96-98` re-homed onto the plan itself (resume/prompt-specific assertions removed with the deleted API) |
| Presentation creation contract (`CreateTerminalSessionAsync`, `NewTerminalCommand`) | Deleted | Task 5; 0 hits repo-wide in prod (guards pin this) |
| Web/Native rails, shortcut, slash action | Deleted | Tasks 2-4 |
| `TerminalAutoRun` end to end | Deleted | Task 12 (commit `de722175`) |
| Native omp terminal view family | Deleted; `TerminalPanelView` (plain shell) kept | Task 11 (`bff6f24e`) |
| `Harness.Terminal` local-PTY family: `LocalPtyBackend`, `LinuxLocalPtyBackend`, `WindowsLocalPtyBackend`, `PtyTerminalSession` + 3 test files | Deleted (Task 14) | Prod (outside defining files): `LocalPtyBackend`=0, `LinuxLocalPtyBackend`=external 0 (internal refs only: factory + `PtyCore` docs), `WindowsLocalPtyBackend`=external 0, `PtyTerminalSession`=external 0 (only backends) |
| `PtyCore`, `PtyOutputCallback`, `PtyProcessRunner` | Kept | `PtyCore` used by `PtyProcessRunner` (kept, used by `TerminalPanelView` plain shell) |
| `ITerminalBackend`, `ITerminalSession`, `TerminalSessionOptions` | Kept | Prod consumers: `Harness.Remote` launchers (`RemoteTerminalLauncher`, `ContainerTerminalLauncher`, `WslTerminalLauncher`). No prod implementation remains — the only factory (`LocalPtyBackend`) is deleted with the starter; launchers take the interface as a parameter |
| `IProcessRunner`, `SystemProcessRunner`, `ProcessRequest`, `ProcessOutcome`, `IProcessOutputSink` | Kept | Prod consumers: `Sandbox` (`LocalPodmanComposeProvider`, `OpenShellProvider`), `GitMentionDiffSource`, `TerminalPanelView`, `TerminalGate` |
| `TerminalGate`, `TerminalCommand`, `CommandProposal`, `TerminalSettings` family (`TerminalSettings`, `TerminalSettingsLoader`, `TerminalSettingsFile`, `TerminalSettingsJsonContext`, `TerminalSettingsLoadResult`) | Kept | `TerminalPanelView` (panel + gate), `MainView`, `AgentsSettingsSource` use `TerminalSettings`; plan keep-list names `TerminalSettings*` — `TerminalSettingsLoader` (prod=0) kept per that instruction |
| `TerminalPalette` | Kept | `TerminalPanelView.axaml.cs` |
| `Porta.Pty` pin | Kept | Used by `PtyCore` (kept) |
| `Iciclecreek.Avalonia.Terminal` pin | Kept | `Native.Views` (`TerminalPalette.cs`, `TerminalPanelView.axaml`) |
| `Microsoft.Windows.Console.ConPTY` pin | Kept — **not** consumer-less: it is a `CentralTransitive` dependency in lockfiles (via `Porta.Pty`); removing the pin breaks locked restore (NU1004). `Directory.Packages.props` untouched | Task 16 recount |
| Presentation `Terminal/*`, `Mentions/IMentionTerminalProvider`, `Approvals/ITerminalCommandProposals`, UI `TerminalChunk*` | All kept | Every type has live prod consumers: `TerminalView.razor`, `WebWorkbench`, `UnavailableTerminalTransport`, `MentionSourceFactory`, `CatalogMentionProvider` |
| `Harness.Herdr` + tests | Deleted (Tasks 15-16) | Verbatim recount: `rg -l 'Herdr' src apps services --glob '!src/Autonogrammer.Harness.Herdr/**'` → **no output**; project-reference scan → only `tests/Autonogrammer.ArchitectureTests` csproj (named separately, deregistered in Task 16) and Herdr's own test csproj |
| False positives (word "terminal" = final state, `WaitForTerminalAsync`, `RemoteOmpTerminalOptions`, etc.) | Kept | `rg -l 'ForTerminal'` remaining hits are vendor `mermaid.min.js` only; `SessionRunWorkflowTests.WaitForTerminalAsync` is unrelated |

## Land-shape note (Tasks 5/6)

- Task 5 (commit `b04d3ba1`): presentation creation contract deleted; `AgentRow.IsTerminal` kept as the
  retired-record signal with the retired word-status in `AgentPanelCopy`.
- Task 6 (same commit): all four hosts lost `CreateTerminalSessionAsync`; `SessionEngineSet` loads a persisted
  Terminal record as a retired slot (no starter call, `TerminalStarter` null, fresh-terminal resume branch deleted).
  `CreateTerminalStarter`/`PlannedOmpTerminalStarter` stayed unreachable-but-present until Task 13 hunk 1 — the
  buildable intermediate state.

## The one relock (Task 16)

Deregistered `src/Autonogrammer.Harness.Herdr` + `tests/Autonogrammer.Harness.Herdr.Tests` from
`Autonogrammer.slnx` (2 rows), `Autonogrammer.Linux.slnf` (2 rows), the Herdr layer row from `LayerMap.cs`,
and the Herdr `ProjectReference` from `tests/Autonogrammer.ArchitectureTests` csproj. `Directory.Packages.props`
untouched (no pin proved consumer-less — ConPTY is a live transitive pin).

Relock method: `dotnet restore tests/Autonogrammer.ArchitectureTests --force-evaluate` (repo enforces
`RestoreLockedMode`, so a plain non-locked restore re-fails with NU1004 by design). Lockfile diff, reviewed —
removals only, no version movement (6 lockfiles + 2 deleted Herdr lockfiles):

- `tests/Autonogrammer.ArchitectureTests/packages.lock.json`: Herdr entry removed; `OmpLauncher→Terminal` edge removed
- `src/Autonogrammer.Harness.OmpLauncher/packages.lock.json`: stale `Terminal` project + `Porta.Pty`/`ConPTY` transitives removed (ref already dropped in Task 13 hunk 2)
- `apps/Autonogrammer.Native`, `src/Autonogrammer.Native.Views`, `src/Autonogrammer.Harness.OmpConfig`,
  `src/Autonogrammer.Harness.RemoteProvision`, `services/Autonogrammer.Api`: `OmpLauncher→Terminal` edge (and, in Api,
  the `Terminal`/`Porta.Pty`/`ConPTY` transitives that only arrived via the launcher) removed

Known pre-existing failure, **not** fixed here (stop-and-report per plan): `dotnet restore
Autonogrammer.Linux.slnf --locked-mode` fails only on
`tests/Autonogrammer.EngineHost.Tests` — NU1004 "new project reference to Autonogrammer.Harness.Rpc", introduced in
commit `90b69a52` (Phase 80, Tasks 8-13) and stale at HEAD. All other projects restore in locked mode.

## OpenAPI D-04 gate (Task 17)

Diff result: **empty by construction.** No file in the serving chain was touched in this phase:
`docs/api/openapi.json`, `src/Autonogrammer.Contracts/Agents/SessionKind.cs` (`Terminal = 1` retained),
`src/Autonogrammer.Contracts/Catalog/CatalogSessionKind.cs` (`Terminal` retained),
`services/Autonogrammer.Api/Services/CatalogProjector.cs` (maps only `Cloud`/`Agent` — never emits Terminal)
are all unmodified (`git diff` empty). The existing contract route is
`tests/Autonogrammer.Api.IntegrationTests/GuardTests.cs::OpenApi_ServedEqualsCommitted`
(served `/openapi/v1.json` vs committed `docs/api/openapi.json`, `JsonNode.DeepEquals`) plus
`OpenApiOperationIdTests`. Build of `services/Autonogrammer.Api` and its integration-test project: 0 errors.
No catalog test asserts `Kind` today, so no "terminal may still be carried" assertion slot exists; no untested
test edit was added — the gate runs the two existing routes.

## Provisioning control AC-305 (Task 18)

`rg -l 'class OmpManagedInstaller|class OmpLocator' src/Autonogrammer.Harness.OmpLauncher` = **2 files**
(`OmpManagedInstaller.cs`, `OmpLocator.cs`). `src/Autonogrammer.Harness.OmpLauncher` builds (0 errors).
OmpLauncher test suite: not run (build-only lane) — cite the Task 13 test run from the executor's transcript.

## Architecture guards (Task 18)

New `tests/Autonogrammer.ArchitectureTests/TerminalRetirementArchitectureTests.cs` (suite idiom: self-test +
full-tree scan): Herdr projects absent from disk; no product project references `Autonogrammer.Harness.Herdr`
(via `ProjectGraphChecker.ReadEdges`); no production file contains `CreateTerminalSessionAsync`,
`NewTerminalCommand`, or `.Create(` with `SessionKind.Terminal`. The stale `OmpLauncher → Terminal` LayerMap edge
was deleted here with its recount: **0** — OmpLauncher's csproj has no `ProjectReference` to `Harness.Terminal`
and none of its sources reference the terminal types (the lockfile edge never matched the csproj). The
`Remote → Terminal` edge stays (remote launchers consume `ITerminalBackend`/`ITerminalSession`).

## Axe evidence

Named absence: the repo has no automated axe route at 1440/1024/390 px against the retired fixture (web a11y is
static gates in `WebShellAccessibilityTests` + bUnit-style UI tests; the retired frame is the existing
`TerminalView` Unavailable frame covered by `tests/Autonogrammer.UI.Tests/TerminalViewTests.cs`). No axe run is
claimed; the runtime audit at 1440/1024/390 px is a named gap for A08.

## Per-task build evidence (this lane)

All builds `dotnet build -c Release`, 0 errors / 0 warnings:

- Task 13 hunk 3: `src/Autonogrammer.Harness.AgentLoop`, `tests/Autonogrammer.Harness.AgentLoop.Tests`,
  `tests/Autonogrammer.Native.Tests` — Build succeeded (per-project runs: 5, 6, 46 projects, 0/0)
- Task 14: `src/Autonogrammer.Harness.Terminal`, `tests/Autonogrammer.Harness.Terminal.Tests`,
  `src/Autonogrammer.Harness.Remote` — Build succeeded (after doc-`cref` fixes in `PtyCore.cs` /
  `PtyProcessRunner.cs` for the deleted backend types)
- Task 15: `src/Autonogrammer.Harness.OmpLauncher` — Build succeeded
- Task 16: `dotnet restore tests/Autonogrammer.ArchitectureTests --force-evaluate` ok;
  `dotnet restore Autonogrammer.Linux.slnf --locked-mode` ok except the pre-existing EngineHost.Tests staleness
- Task 17: `services/Autonogrammer.Api`, `tests/Autonogrammer.Api.IntegrationTests` — Build succeeded
- Task 18: `tests/Autonogrammer.ArchitectureTests` — Build succeeded

Tests were not run in this lane (build-only constraint); the main agent runs the per-task suites:
`Harness.OmpLauncher.Tests` (Task 13 + AC-305), `Harness.Terminal.Tests` (Task 14 survivors),
`Api.IntegrationTests` (Task 17), `ArchitectureTests` (Tasks 1, 18), plus `Harness.AgentLoop.Tests` /
`Native.Tests` (hunk 3 rewrites).

## Deviations

1. OQ-76 disposition edit was applied with a script (one-line table-cell replacement) rather than the edit tool;
   the row's column count was re-verified after.
2. Task 14 deletion hunk is 7 files in one commit (4 prod + 3 test) — the subjects' tests in the same hunk per
   plan; the ≤5 guidance could not be met without an uncompilable intermediate.
3. `TerminalSettingsLoader` family kept with prod count 0, per the plan's explicit `TerminalSettings*` keep —
   cited, not force-deleted.
4. Locked restore of the full slnf still fails on pre-existing EngineHost.Tests staleness (Phase 80);
   reported, not fixed (unrelated lockfile movement is a stop-and-report).
