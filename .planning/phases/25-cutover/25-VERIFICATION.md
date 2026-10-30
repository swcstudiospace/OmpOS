---
phase: 25-cutover
status: passed
score: 4/4
verified: 2026-10-25
---

# Phase 25: Cutover Verification Report

**Phase goal** (`.planning/ROADMAP.md`, Phase 25 section):

> The MAUI head is removed after parity, and the Razor component library is removed once the Blazor web head no longer references it.

**Success criteria** (same section, quoted verbatim, not paraphrased):

1. Before any removal, every row of the Phase 21 parity inventory has a passing Avalonia test. Verify: gate run at phase HEAD.
2. apps/Autonogrammer.App and its test projects are removed, and a repository search finds no reference to apps/Autonogrammer.App in the solution, project or CI files. Verify: search output in the phase record and the gate.
3. src/Autonogrammer.UI and its test projects are removed only when no project in the solution references them. src/Autonogrammer.UI stays because the Blazor web head uses it; its removal is tracked under gate G-WEB, not under XP-01. Verify: repository search output and the gate.
4. The gate is green at the phase HEAD. Verify: gate run at phase HEAD.

**Method note:** `.planning/phases/25-cutover/25-SUMMARY.md` was treated as claims to test, not evidence. Criteria 1–3 were checked against the working tree at HEAD `f8b5faf`. Criterion 4 was first recorded as the SUMMARY's claim (gate not re-run by the verifier per assignment); the orchestrator then supplied the re-run gate output as session artifact `artifact://576`, against which criterion 4 was re-checked (see update note below).

**Update 2026-10-25 (criterion 4 re-check):** the gate was re-run at HEAD `f8b5faf` after the initial report: `./bootstrap.sh` exit 0 with "bootstrap: OK" (SDK 10.0.402, locked restore, Release build, tests, format, web smoke with axe). Total 2270, failed 0, succeeded 2270, skipped 0. Web smoke exit 0 (evidence JSON `web-smoke-f8b5faf-2026-10-25.json`). Full output: `artifact://576`. Criterion 4 is now VERIFIED against that run; status raised to `passed`, score to 4/4.

## Criteria table

| # | Criterion (verbatim, abbreviated to the assertion) | Status | File:line evidence |
|---|-----------------------------------------------------|--------|--------------------|
| 1 | Every parity-inventory row has a passing Avalonia test before removal | VERIFIED (23 behaviour tests) + row 11 accepted by design | `docs/parity/session-chat-parity.md` — zero `pending:` cells (only the legend at line 9); per-row test mapping in the 24-row table below; gate run confirms the named tests pass (criterion 4) |
| 2 | `apps/Autonogrammer.App` removed; no reference in solution, project or CI files | VERIFIED | `apps/` lists `Autonogrammer.Android/`, `Autonogrammer.Native/`, `Autonogrammer.Web/` only (no `Autonogrammer.App`); `Autonogrammer.slnx` has no `Autonogrammer.App` entry; repo-wide search for `apps/Autonogrammer.App` over `*.slnx *.slnf *.csproj *.props *.targets *.yml *.cs` returns 0 matches; broad `Autonogrammer\.App` search over `*.cs *.csproj *.yml *.yaml *.props *.targets *.slnf` also returns 0 matches; `.github/workflows/ci.yml` has no `Autonogrammer.App`/`shells` match (shells job removed) |
| 3 | `src/Autonogrammer.UI` stays, referenced by the Blazor web head | VERIFIED | `src/Autonogrammer.UI/` exists; `apps/Autonogrammer.Web/Autonogrammer.Web.csproj:17` — `<ProjectReference Include="..\..\src\Autonogrammer.UI\Autonogrammer.UI.csproj" />`; `tests/Autonogrammer.UI.Tests/Autonogrammer.UI.Tests.csproj:6` — `<ProjectReference Include="../../src/Autonogrammer.UI/Autonogrammer.UI.csproj" />` |
| 4 | Gate green at the phase HEAD | VERIFIED | Gate re-run at HEAD `f8b5faf` on 2026-10-25 (`artifact://576`): `./bootstrap.sh` exit 0, "bootstrap: OK (SDK 10.0.402, locked restore, Release build, tests, format, web smoke with axe)"; total 2270, failed 0, succeeded 2270, skipped 0; web smoke exit 0 (`commit=f8b5faf`, evidence `web-smoke-f8b5faf-2026-10-25.json`) |

## Criterion 1: the 24 rows

All 24 rows that carried `pending:` in the Avalonia column now name a test that exists under `tests/Autonogrammer.Native.Tests/`, carries `[Fact]` with no `Skip`/`Ignore`/`Explicit` marker (grep for `Fact(Skip`, `Theory(Skip`, `[Ignore]`, `[Explicit]`, `.Skip(` returns nothing in the parity test files or `ComposerRunInputTests.cs`), and asserts behaviour with Shouldly (`ShouldBe`/`ShouldBeTrue`/`ShouldBeEmpty`/`ShouldContain`). The single `Skip` grep hit is the `SkipLink` control name, not a marker. Spot-checked bodies (e.g. `ParitySessionGapTests.AwaitingApprovalDrawsAReadOnlyNotice`, `ParityGapClosureTests.ErrorBlockDrawsNoActionByDesign`) render a real view/model and assert visible copy and control state.

| Row | Avalonia test named in the parity table | Exists at | Status |
|-----|------------------------------------------|-----------|--------|
| 11 | `ParityGapClosureTests.ErrorBlockDrawsNoActionByDesign` | `tests/Autonogrammer.Native.Tests/Parity/ParityGapClosureTests.cs:98` | ACCEPTED BY DESIGN (owner decision 2026-10-25, recorded in `25-CONTEXT.md`): asserts the error block draws the message and offers no button in both retryable cases. Reported as accepted, not as a passing behaviour test |
| 21 | `ParityGapClosureTests.LoadingHistoryDrawsItsPlaceholderInTheTranscript` | `ParityGapClosureTests.cs:23` | VERIFIED |
| 22 | `ParityGapClosureTests.AnEmptyTranscriptDrawsItsPrompt` | `ParityGapClosureTests.cs:44` | VERIFIED |
| 24 | `ParitySessionGapTests.AwaitingApprovalDrawsAReadOnlyNotice` | `tests/Autonogrammer.Native.Tests/Parity/ParitySessionGapTests.cs:27` | VERIFIED |
| 25 | `ParitySessionGapTests.ReconnectingDrawsItsNotice` | `ParitySessionGapTests.cs:47` | VERIFIED |
| 26 | `ParitySessionGapTests.OfflineDrawsItsNotice` | `ParitySessionGapTests.cs:65` | VERIFIED |
| 27 | `ParitySessionGapTests.StreamingTurnOffersStopInTheTranscript` | `ParitySessionGapTests.cs:83` | VERIFIED |
| 28 | `ParitySessionGapTests.RecoverableFailureOffersRetryInTheTranscript` | `ParitySessionGapTests.cs:109` | VERIFIED |
| 29 | `ParitySessionGapTests.PausedTranscriptOffersJumpToLatest` | `ParitySessionGapTests.cs:136` | VERIFIED |
| 30 | `ParitySessionGapTests.AutoScrollFollowsOutputUntilTheUserScrollsAway` | `ParitySessionGapTests.cs:165` | VERIFIED |
| 31 | `ParityComposerGapClosureTests.ADraftTypedWhileAResponseStreamsIsKeptOnEnter` + `ComposerRunInputTests.WhileARunIsActiveSendSteersTheRunAtOnceAndClearsTheDraft` | `tests/Autonogrammer.Native.Tests/Parity/ParityComposerGapClosureTests.cs:23`, `tests/Autonogrammer.Native.Tests/RunControls/ComposerRunInputTests.cs:19` | VERIFIED (both exist) |
| 32 | `ParityComposerGapClosureTests.ShiftEnterWhileStreamingNeitherSendsNorStopsAndEscapeStopsTheTurn` | `ParityComposerGapClosureTests.cs:47` | VERIFIED |
| 33 | `ParityComposerGapClosureTests.TheSendButtonDoesNotSendAWhitespaceOnlyDraft` | `ParityComposerGapClosureTests.cs:91` | VERIFIED |
| 34 | `ParityComposerGapClosureTests.TheComposerStopButtonCancelsTheStreamingTurn` | `ParityComposerGapClosureTests.cs:137` | VERIFIED |
| 35 | `ParityComposerGapClosureTests.EnterOnAWhitespaceOnlyDraftIsANoOp` | `ParityComposerGapClosureTests.cs:116` | VERIFIED |
| 36 | `ParityGapClosureTests.ShellWithNoSessionsDrawsTheEmptyConversationListPrompt` | `ParityGapClosureTests.cs:64` | VERIFIED |
| 37 | `ParityShellGapTests.CurrentConversationRowIsTheOnlyRowMarkedCurrent` | `tests/Autonogrammer.Native.Tests/Parity/ParityShellGapTests.cs:26` | VERIFIED |
| 38 | `ParityShellGapTests.ChoosingARowRaisesItsIdentifier` | `ParityShellGapTests.cs:45` | VERIFIED |
| 39 | `ParityGapClosureTests.TheSkipLinkMovesFocusToTheTranscript` | `ParityGapClosureTests.cs:79` | VERIFIED |
| 40 | `ParityShellGapTests.EngineFailureShowsAnAssertiveBanner` | `ParityShellGapTests.cs:67` | VERIFIED |
| 41 | `ParityComposerGapClosureTests.ARefusedSendFromTheSendControlKeepsTheDraftAndShowsTheRefusal` | `ParityComposerGapClosureTests.cs:173` | VERIFIED |
| 42 | `ParityShellGapTests.HostRetryResendsTheLastPromptAndStopCancelsTheTurn` | `ParityShellGapTests.cs:107` | VERIFIED |
| 43 | `ParitySessionGapTests.ScrollTransitionsReachTheShellOncePerChange` | `ParitySessionGapTests.cs:201` | VERIFIED |
| 44 | `ParityShellGapTests.EngineEventsFoldIntoTheTranscriptAndADiagnosticIsDropped` | `ParityShellGapTests.cs:154` | VERIFIED |

Result: 23 rows with asserting behaviour tests + row 11 accepted by design = 24/24 accounted for, 0 `pending:` cells remain.

## Findings

No BLOCKERs. All four criteria are verified.

- No WARNINGs remain. The earlier criterion-4 claim-only WARNING is resolved by the re-run gate output (`artifact://576`) at HEAD `f8b5faf`. The earlier HEAD-drift note is likewise resolved: the gate ran on `f8b5faf` itself.

## Phase 23 reading (dependency question)

ROADMAP.md line 477 states Phase 25 **Depends on**: Phases 21, 22, 23, 24. The owner's waiver text (ROADMAP line 479; STATE.md lines 346 and 384) names **Phases 22 and 24 only**. Phase 23 is partial per `.planning/phases/23-web-head-shared-presentation/23-SUMMARY.md` (criteria 1, 4, 5 met; criteria 2 and 3 blocked on the deployed origin `AUTONOGRAMMER_API_ORIGIN` and the Railway/Vercel deploys).

Reading under the phase goal: **Phase 23 is not a gap for Phase 25 — it is a separate human (owner) decision.** Reasons:

1. Phase 25 criterion 3 explicitly requires `src/Autonogrammer.UI` to **stay** while the Blazor web head uses it, and moves its removal to gate G-WEB under Phase 23, not XP-01. Phase 23's blocked items (deployed axe/CSP/Vercel smoke) therefore cannot block a criterion whose satisfied state is retention.
2. Nothing in the Phase 25 goal ("the MAUI head is removed after parity…") consumes Phase 23's blocked deploy outputs; the web head's shared-presentation consumption (Phase 23 criterion 1) is met.
3. Whether the owner's waiver for Phases 22 and 24 also extends to Phase 23 is not decided by the goal text — that extension is the owner's call, not the verifier's. This report does not decide the waiver; it records that the goal as written is satisfiable without Phase 23's blocked criteria.

**Owner answer 2026-10-25: keep Phase 23 open.** Recorded here as an **open dependency**, not as a waiver and not as a Phase 25 gap. The reading above stands: Phase 23 stays open on its own deploy blockers, and none of them blocks the Phase 25 goal.

## Human verification required

None remaining. The single prior item (confirm the gate record at the phase HEAD) is resolved by the re-run gate output at `f8b5faf` (`artifact://576`).

---

_Verified: 2026-10-25 (criterion 4 re-checked against the re-run gate output the same day)_
_Verifier: Phase25Verifier (goal-backward: ROADMAP criteria → tree evidence; SUMMARY treated as claims)_
