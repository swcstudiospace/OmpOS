# Phase 25: Cutover, Summary

**Status: complete (2026-10-25). Removal, parity, and the QUALITY_BAR fixes are committed (0be414e, 59e9dd1). gsd-verifier passed 4 of 4 (25-VERIFICATION.md). The checklist below has no open fail; one gap stays recorded and owner-accepted: the dead-code check.** apps/OmpOS.App is removed (criterion 2). The owner's waiver covers the dependencies on Phases 22 and 24 (G25-1, STATE.md line 346). Phase 23 is not waived: the owner chose to keep it open on 2026-10-25.

Mac channel: No direct step. The Phase 22 and Phase 24 dependencies are waived for this phase's removal; their Mac-channel evidence stays open in those phases.

## Success criteria (ROADMAP Phase 25)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Every Phase 21 parity-inventory row has a passing Avalonia test before removal | Met | 23 rows with asserting native tests (10 in ParityGapClosureTests and ParityComposerGapClosureTests; 13 in ParitySessionGapTests and ParityShellGapTests); row 11 by design, accepted by the owner; 0 `pending:` cells (25-VERIFICATION.md) |
| 2 | apps/OmpOS.App and its test projects removed; no reference in the solution, project or CI files | Met | `apps/` holds OmpOS.Android, OmpOS.Native and OmpOS.Web only; 0 matches in .slnx, .slnf, .csproj, .props, .targets, .yml and .cs (25-VERIFICATION.md) |
| 3 | src/OmpOS.UI stays while the web head uses it; its removal is tracked under G-WEB | Met | Referenced by apps/OmpOS.Web/OmpOS.Web.csproj:17 and tests/OmpOS.UI.Tests/OmpOS.UI.Tests.csproj:6. The removal decision stays open under Phase 23 (owner, 2026-10-25) |
| 4 | Gate green at the phase HEAD | Met (2026-10-25) | `./bootstrap.sh` exit 0 on commit 59e9dd1's tree: total 2276, failed 0, skipped 0; web smoke exit 0 (gate output in this session, not committed) |

## Open items

- Decide the src/OmpOS.UI removal under G-WEB. Owner decision. Route: criterion 3; Phase 23 open items.
- Phase 22 and Phase 24 Mac-channel and iOS evidence: waived for this phase's removal (G25-1 answer, 2026-10-25); still open in those phases. Route: docs/runbooks/phase-22-desktop-heads.md; ROADMAP Phase 24.
- Phase 23 dependency: kept open by owner decision (2026-10-25). The verifier reads it as not a gap for this phase's goal. Route: Phase 23 open items.
- Phase 25 is closed. The dead-code check stays a recorded gap, because the repo has no such check. The uplift audit (25.1) can propose one. Route: QUALITY_BAR maintainability; 25.1.

## Commits

- cf4b617: criterion 1 classification and native parity tests (partial, awaiting owner decision).
- 9171508: the 13 unrendered Avalonia parity behaviours; the MAUI head removed (Phase 25).
- 0bea2bf: follow-up fixes (SessionView subscription lifetime; MainView.Close dispose trace), committed in the baseline commit.
- 9e5913d: gate-green HEAD before the removal.

## Run 2026-10-25 (gsd-autonomous, graph ut-mvmegw7f-4a7312d5; superseded by the update below)

Status: awaiting owner decision. Criterion 1 unmet (Class A 10 met; Class B 1 awaiting acceptance; Class C 13 open). Criteria 2 and 3 not started.

Gate on the checkpoint tree: `./bootstrap.sh` exit 0; total 2256 passed, 0 failed, 0 skipped; web smoke exit 0. Tests added: tests/OmpOS.Native.Tests/Parity/ParityGapClosureTests.cs (rows 11, 21, 22, 36, 39) and ParityComposerGapClosureTests.cs (rows 31 to 35, 41). The parity table's Avalonia column names each new test; Class C cells state that the Avalonia head does not render the behaviour. No product code changed.

Deviations: none in product code. Two analyzer findings in the new tests were fixed in the test files (an unnecessary using; a constant array argument).

## Update 2026-10-25: criteria met; VERIFY_GATE open

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Every parity row has a passing Avalonia test before removal | Met | 24 rows: 10 native behaviour tests (ParityGapClosureTests, ParityComposerGapClosureTests); row 11 by design (owner accepted); 13 rows implemented with native tests (ParitySessionGapTests, ParityShellGapTests). Gate: 2269 passed, 0 failed, 0 skipped |
| 2 | apps/OmpOS.App and its test projects removed; no reference in solution, project or CI files | Met | `OmpOS.slnx` entry removed; `apps/OmpOS.App` removed (13 tracked files); the `shells` CI job removed; LayerMap entry and the loader exception removed. Search over .slnx, .slnf, .csproj, .props, .targets, .yml and .cs: 0 matches |
| 3 | src/OmpOS.UI stays; removal tracked under G-WEB | Met | Referenced by apps/OmpOS.Web/OmpOS.Web.csproj:17 and tests/OmpOS.UI.Tests/OmpOS.UI.Tests.csproj:6 |

Gate after removal: `./bootstrap.sh` exit 0 on rerun; total 2269 passed, 0 failed, 0 skipped; web smoke exit 0. One flaky test (LinuxLocalPtyBackendTests.DisposeEndsARunningProcessAndReturns) failed once and passed on rerun.

Docs: README.md and CONTRIBUTING.md updated for the removed MAUI head and shells job, and for the Android head. Owner-visible unknown: whether the android CI job is green (not pushed).

## Review follow-up 2026-10-25 (committed in 0bea2bf)

The parity review found one major defect in SessionView: the transcript kept its model subscription after it left the visual tree. It now subscribes on attach and unsubscribes on detach, following the conversation list's pattern, and redraws its state on attach. The regression test HiddenTranscriptStopsFollowingItsModelUntilShownAgain fails on the pre-fix view and passes on the fixed view.

Also in this phase: MainView.Close now reports a failed engine-host dispose and a failed MCP dispose through a trace error. Before, the engine-host failure was posted to a closed window, and the MCP task was unobserved. Findings that were not applied, with their evidence (the copy specified by 20-UI-SPEC.md, the parameterless constructor the XAML loader requires, and reachability), are recorded in STATE.md under Follow-up run 2026-10-25.

Gate: ./bootstrap.sh exit 0; total 2270, failed 0, skipped 0; web smoke exit 0.

## QUALITY_BAR checklist, 2026-10-25

Scope: the code this phase touched (MainView, SessionView, ComposerView, ConversationListView, the parity tests, the architecture tests, bootstrap.sh, ci.yml, OmpOS.slnx). Evidence is grep output and the cited lines, read at f8b5faf.

| Item | Status | Evidence |
|---|---|---|
| Pattern fidelity | Pass | New parity tests use [Fact] and name their parity row (tests/OmpOS.Native.Tests/Parity). Architecture rules and LayerMap updated for the removed project. No new pattern; the Phase 24 seams are recorded in 24-SUMMARY D1 to D5. |
| Correctness | Pass | Behaviour tests cover empty, whitespace-only and refused input: TheSendButtonDoesNotSendAWhitespaceOnlyDraft, EnterOnAWhitespaceOnlyDraftIsANoOp, ARefusedSendFromTheSendControlKeepsTheDraftAndShowsTheRefusal, AnEmptyTranscriptDrawsItsPrompt. The catch sites in MainView.axaml.cs (lines 299, 546, 623) show their fault in the transcript or status line. Both close traces are tested in tests/OmpOS.Native.Tests/MainViewCloseFailureTests.cs: the engine host (ClosingTheWindowTracesAnEngineHostDisposeFailure) and the MCP host (ClosingTheWindowTracesAnMcpHostDisposeFailure). Fixed in 0be414e and 59e9dd1; the MCP host goes through the internal IMcpRegistryHost, so no public harness type changed. |
| Boundaries | Pass | The composer rejects whitespace-only drafts at entry (ComposerView.axaml.cs:180, 197), covered by the tests above. |
| Security | Pass, residual noted | No TLS bypass, unsafe deserialization or process spawn in touched code. No secret literal; "Secrets" in LayerMap is a project name. The gitleaks CLI is pinned by SHA-256 (ci.yml:23, 76 to 77). bootstrap.sh removes the test export file on exit (bootstrap.sh:41). Residual: CI service passwords derive from run ids (ci.yml:24 to 25) and reach only services inside the job (ci.yml:111 to 112). |
| Maintainability and observability | Fail: no dead-code check (gap, owner-accepted) | Pass on markers (no TODO, FIXME, HACK or XXX in touched code; the hits are scanner strings and fixtures in tests/OmpOS.ArchitectureTests), suppressions and skips (none), commented-out code (none by heuristic), and failure logging (Trace.TraceError with context, MainView.axaml.cs:691, 702). No repo check finds dead code; the warnings-as-errors build is the only evidence. The owner's fix-now answer (2026-10-25) keeps this gap recorded. |
| Accessibility: labelled controls | Pass | AutomationProperties.Name on the touched controls (SessionView.axaml:6 to 63; MainView.axaml:17 to 124), with LiveSetting on live regions. |
| Accessibility: keyboard operable | Pass for composer and skip link | ShiftEnterWhileStreamingNeitherSendsNorStopsAndEscapeStopsTheTurn and TheSkipLinkMovesFocusToTheTranscript. The toolbar ToggleButtons use Avalonia's standard keyboard handling; they are not tested one by one. |
| Accessibility: visible focus | Pass (rendered-state test) | Fixed in 0be414e: App.axaml draws the AccentPurple focus border on ToggleButton too (ToggleButton:focus-visible, App.axaml:116 to 120). FocusingAToolbarToggleDrawsTheAccentFocusBorder moves Tab focus to ConventionsToggle and asserts the AccentPurple border and FocusBorder thickness. It failed before the style (border Transparent) and passes after. The look on screen is not checked by a person. |
| Accessibility: contrast | Pass at token level | ThemeContrastTests: every text pair at 4.5:1 and every non-text pair at 3:1 in both variants, including AccentPurple as the focus colour (EveryTextPairMeetsTheTextMinimum, EveryNonTextPairMeetsTheNonTextMinimum). The views hold no colour literal; hex colours occur only in Themes/OmpOSTokens.axaml. Residual: token pairs, not composited pixels. |
| Accessibility: reduced motion | N/A | No Transition, Animation, Keyframe or Duration in the touched views (grep empty). |
| Compatibility and reproducibility | Pass | No lockfile or package-version file in the phase's change list; the only project removed is apps/OmpOS.App. No contract or data-format file changed. |
| Unbounded loops and user-sized work | Pass | Fixed in 0be414e: RunInputScheduler accepts at most 16 messages waiting to start (MaxQueuedMessages; steers held plus follow-ups queued). The 17th is refused with a fixed message, and the draft stays (RunInputSchedulerCapTests: RefusesTheSeventeenthQueuedMessage, AcceptsAgainOnceOneMessageStarts, KeepsAcceptedMessagesInSendOrder). The turn loop (MainView.axaml.cs:293 to 329) therefore never has more than 16 messages waiting. The terminal test loop (LinuxLocalPtyBackendTests.cs:254) is bounded by the test token and process exit; AssertProcessEndedAsync is capped at 250 attempts. |
| Lint, type-check, test, build | Pass | ./bootstrap.sh exit 0 at 59e9dd1's tree: Release build with warnings as errors, format check, 2276 passed, 0 failed, 0 skipped, web smoke with axe exit 0. bootstrap.sh declares no separate linter. |

Gate: after commits 0be414e and 59e9dd1, every row passes except one recorded gap, the dead-code check, which the owner's fix-now answer (2026-10-25) keeps recorded. Phase 25 is closed.

## Fixes applied, 2026-10-25 (commit 0be414e)

- Queue cap: src/OmpOS.Presentation/Runs/RunInputScheduler.cs (MaxQueuedMessages = 16; EnsureRoomForAnotherMessage in SteerAsync and QueueFollowUp); src/OmpOS.Native.Views/Views/ComposerView.axaml.cs (a refused follow-up faults its task, so Track reports it and the draft stays).
- Tests: tests/OmpOS.Presentation.Tests/RunInputSchedulerCapTests.cs (3 tests); tests/OmpOS.Native.Tests/MainViewCloseFailureTests.cs (ClosingTheWindowTracesAnEngineHostDisposeFailure); tests/OmpOS.Native.Tests/FocusVisibleToggleTests.cs (FocusingAToolbarToggleDrawsTheAccentFocusBorder).
- Focus style: apps/OmpOS.Native/App.axaml (ToggleButton:focus-visible; the focus comment names the controls it covers).
- Documentation: src/OmpOS.Native.Views/MainView.axaml.cs (the CloseEngineAsync summary: failures are traced, not shown on the status line).
- Gate: ./bootstrap.sh exit 0, 2275 passed, 0 failed, 0 skipped, web smoke exit 0. Before the style was added, FocusVisibleToggleTests failed with the border Transparent, as expected.
- Not in this commit: the .planning records (uncommitted, by the owner's answer).
- MCP host seam: src/OmpOS.Native.Views/Views/Mcp/IMcpRegistryHost.cs (new); src/OmpOS.Native.Views/Views/Mcp/McpToolRegistrySource.cs (implements the host; disposal delegates to the registry); src/OmpOS.Native.Views/MainView.axaml.cs (an internal constructor takes the optional host, and the public constructor chains to it); tests/OmpOS.Native.Tests/MainViewCloseFailureTests.cs (ClosingTheWindowTracesAnMcpHostDisposeFailure; the listener takes its marker). Commit 59e9dd1; gate 2276 passed.
