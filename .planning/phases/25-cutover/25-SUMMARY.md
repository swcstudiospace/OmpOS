# Phase 25: Cutover, Summary

**Status: blocked.** Cutover depends on Phase 22 and Phase 24 Mac-channel evidence. apps/OmpOS.App is not removed.

Mac channel: No direct step, but the dependencies are Mac-channel and host-blocked (see Phases 22 and 24).

## Success criteria (ROADMAP Phase 25)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Every Phase 21 parity-inventory row has a passing Avalonia test before removal | Not met (removal gated) | Inventory guard green: tests/OmpOS.Presentation.Tests/ParityInventoryTests.cs. Removal not started |
| 2 | apps/OmpOS.App and its test projects removed; no references remain | Blocked | apps/OmpOS.App is present and not removed. Waits on Phases 22 and 24 evidence |
| 3 | src/OmpOS.UI removed only when no project references it | Retained by design | src/OmpOS.UI stays because the Blazor web head uses it. Removal is tracked under G-WEB (Phase 23), not XP-01 |
| 4 | Gate green at the phase HEAD | Met at HEAD 9e5913d | Gate: 1596 passed, 0 failed, 0 skipped. The phase itself is not complete |

## Open items

- Phase 22 Mac-channel evidence and Phase 24 iOS evidence. Owner: owner, Mac channel. Route: docs/runbooks/phase-22-desktop-heads.md.
- Remove apps/OmpOS.App and its test projects, then search the solution, projects and CI files for references. Owner: engineering. Route: ROADMAP Phase 25 criteria 1 and 2.
- Decide the src/OmpOS.UI removal under G-WEB. Owner: owner decision. Route: Phase 23 open items.

## Commits

- No Phase 25 code commit.
- 9e5913d: gate-green HEAD

## Run 2026-10-25 (gsd-autonomous, graph ut-mvmegw7f-4a7312d5)

Status: awaiting owner decision. Criterion 1 unmet (Class A 10 met; Class B 1 awaiting acceptance; Class C 13 open). Criteria 2 and 3 not started.

Gate on the checkpoint tree: `./bootstrap.sh` exit 0; total 2256 passed, 0 failed, 0 skipped; web smoke exit 0. Tests added: tests/OmpOS.Native.Tests/Parity/ParityGapClosureTests.cs (rows 11, 21, 22, 36, 39) and ParityComposerGapClosureTests.cs (rows 31 to 35, 41). The parity table's Avalonia column names each new test; Class C cells state that the Avalonia head does not render the behaviour. No product code changed.

Deviations: none in product code. Two analyzer findings in the new tests were fixed in the test files (an unnecessary using; a constant array argument).

## Update 2026-10-25: criteria met (complete)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Every parity row has a passing Avalonia test before removal | Met | 24 rows: 10 native behaviour tests (ParityGapClosureTests, ParityComposerGapClosureTests); row 11 by design (owner accepted); 13 rows implemented with native tests (ParitySessionGapTests, ParityShellGapTests). Gate: 2269 passed, 0 failed, 0 skipped |
| 2 | apps/OmpOS.App and its test projects removed; no reference in solution, project or CI files | Met | `OmpOS.slnx` entry removed; `apps/OmpOS.App` removed (13 tracked files); the `shells` CI job removed; LayerMap entry and the loader exception removed. Search over .slnx, .slnf, .csproj, .props, .targets, .yml and .cs: 0 matches |
| 3 | src/OmpOS.UI stays; removal tracked under G-WEB | Met | Referenced by apps/OmpOS.Web/OmpOS.Web.csproj:17 and tests/OmpOS.UI.Tests/OmpOS.UI.Tests.csproj:6 |

Gate after removal: `./bootstrap.sh` exit 0 on rerun; total 2269 passed, 0 failed, 0 skipped; web smoke exit 0. One flaky test (LinuxLocalPtyBackendTests.DisposeEndsARunningProcessAndReturns) failed once and passed on rerun.

Docs: README.md and CONTRIBUTING.md updated for the removed MAUI head and shells job, and for the Android head. Owner-visible unknown: whether the android CI job is green (not pushed).

## Review follow-up 2026-10-25 (uncommitted)

The parity review found one major defect in SessionView: the transcript kept its model subscription after it left the visual tree. It now subscribes on attach and unsubscribes on detach, following the conversation list's pattern, and redraws its state on attach. The regression test HiddenTranscriptStopsFollowingItsModelUntilShownAgain fails on the pre-fix view and passes on the fixed view.

Also in this phase: MainView.Close now reports a failed engine-host dispose and a failed MCP dispose through a trace error. Before, the engine-host failure was posted to a closed window, and the MCP task was unobserved. Findings that were not applied, with their evidence (the copy specified by 20-UI-SPEC.md, the parameterless constructor the XAML loader requires, and reachability), are recorded in STATE.md under Follow-up run 2026-10-25.

Gate: ./bootstrap.sh exit 0; total 2270, failed 0, skipped 0; web smoke exit 0.
