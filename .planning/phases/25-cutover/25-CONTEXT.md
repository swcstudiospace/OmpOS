# Phase 25: Cutover, discuss record

Recorded 2026-10-24 by the gsd-autonomous run (graph ut-mvm12su0-1fae51f6). Milestone v0.10.0. Status: BLOCKED on Phases 22 and 24.
Depends on Phases 21, 22, 23 and 24. Mac channel: no direct step.

## Goal and criteria (ROADMAP.md lines 470 to 479)

Goal: the MAUI head is removed after parity, and the Razor component library is removed once the Blazor web head no longer references it.

1. Before any removal, every row of the Phase 21 parity inventory has a passing Avalonia test. Verify: a gate run at the phase HEAD.
2. apps/Autonogrammer.App and its test projects are removed, and a repository search finds no reference to apps/Autonogrammer.App in the solution, project or CI files.
3. src/Autonogrammer.UI and its test projects are removed only when no project references them. src/Autonogrammer.UI stays while the Blazor web head uses it, and its removal is tracked under G-WEB.
4. The gate is green at the phase HEAD.

## Current evidence

- Criterion 1: the parity inventory exists (docs/parity/session-chat-parity.md, 45 rows, ParityInventoryTests). It is not yet confirmed that every row has a passing Avalonia test.
- Criteria 2 to 4: not started. Blocked on Phases 22 and 24.

## Verified paths

- apps/Autonogrammer.App/Autonogrammer.App.csproj (exists)
- src/Autonogrammer.UI (exists; the Blazor web head still uses it)
- docs/runbooks/local-dev.md (states that apps/Autonogrammer.App retires at v0.10.0 Phase 25)
- .github/workflows/ci.yml: the shells job references apps/Autonogrammer.App from line 144 onward

## Grey areas

- G25-1, material (deletes code, and the change cannot be undone by a gate). Question: remove apps/Autonogrammer.App only after the parity inventory is fully green, with Phases 22 and 24 closed? Default: yes, and no deletion happens in this run. Rejected: the run would delete the MAUI head before the Avalonia heads are verified. That is a product risk, so it needs an explicit owner decision.
- Deferred to the phase's own turn: once Phases 22 and 24 close, the criterion 2 search and the shells-job change (ci.yml).

## Automated and manual

- Criteria 1, 2 and 4: automated, at the phase HEAD, once Phases 22 and 24 close.
- Criterion 3: automated search. The removal of src/Autonogrammer.UI is recorded under G-WEB and stays out of this phase.

## Pauses

- BLOCKER: Phases 22 and 24 must close first. Minimal action: close those phases, or record them as owner-blocked and decide whether this phase may proceed.
- GREY_AREA: G25-1, deferred until the blockers above are resolved.

## Run record 2026-10-25 (graph ut-mvmegw7f-4a7312d5)

- Dependency waiver accepted (owner answer: waive the dependencies for MAUI removal). Phases 22 and 24 are not closed, so the waiver covers Phase 25's dependency on them. It does not cover criterion 1.
- Criterion 1 classification of the 24 pending rows in docs/parity/session-chat-parity.md: Class A (10 rows: 21, 22, 31, 32, 33, 34, 35, 36, 39, 41) have passing native tests in tests/Autonogrammer.Native.Tests/Parity/. Class B (row 11) has a by-design test, awaiting owner acceptance. Class C (13 rows: 24 to 30, 37, 38, 40, 42, 43, 44) are not rendered by the Avalonia head; they need product work or an owner-approved amendment.
- Removal (criterion 2) and the reference search are not started. They wait on criterion 1.

## Pause payload (GREY_AREA, 2026-10-25)

- Question 1: how does criterion 1 proceed for the 13 Class C rows? Options: implement the features in the Avalonia head with tests (default, keeps the criterion); amend criterion 1 to accept recorded exceptions (a ROADMAP change, owner approval); defer Phase 25.
- Question 2: accept row 11 (the error block draws no retry action by design) as its passing Avalonia test?
- Resume point: Wave 8 of Phase 25 with the answers. Class A tests are already in place; the classification stays in the parity table.

## Owner decisions 2026-10-25 (answers to the GREY_AREA questions)

- Criterion 1, Class C rows (13): implement the features in the Avalonia head, each with a native test (owner answer: implement the 13 features with tests).
- Row 11 (Class B): the by-design native test is accepted as its passing Avalonia test (owner answer: accept by design).
- Removal (criterion 2) and the reference search start only after criterion 1 passes.
