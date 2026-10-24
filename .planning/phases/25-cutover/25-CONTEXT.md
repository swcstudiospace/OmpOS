# Phase 25: Cutover, discuss record

Recorded 2026-10-24 by the gsd-autonomous run (graph ut-mvm12su0-1fae51f6). Milestone v0.10.0. Status: BLOCKED on Phases 22 and 24.
Depends on Phases 21, 22, 23 and 24. Mac channel: no direct step.

## Goal and criteria (ROADMAP.md lines 470 to 479)

Goal: the MAUI head is removed after parity, and the Razor component library is removed once the Blazor web head no longer references it.

1. Before any removal, every row of the Phase 21 parity inventory has a passing Avalonia test. Verify: a gate run at the phase HEAD.
2. apps/OmpOS.App and its test projects are removed, and a repository search finds no reference to apps/OmpOS.App in the solution, project or CI files.
3. src/OmpOS.UI and its test projects are removed only when no project references them. src/OmpOS.UI stays while the Blazor web head uses it, and its removal is tracked under G-WEB.
4. The gate is green at the phase HEAD.

## Current evidence

- Criterion 1: the parity inventory exists (docs/parity/session-chat-parity.md, 45 rows, ParityInventoryTests). It is not yet confirmed that every row has a passing Avalonia test.
- Criteria 2 to 4: not started. Blocked on Phases 22 and 24.

## Verified paths

- apps/OmpOS.App/OmpOS.App.csproj (exists)
- src/OmpOS.UI (exists; the Blazor web head still uses it)
- docs/runbooks/local-dev.md (states that apps/OmpOS.App retires at v0.10.0 Phase 25)
- .github/workflows/ci.yml: the shells job references apps/OmpOS.App from line 144 onward

## Grey areas

- G25-1, material (deletes code, and the change cannot be undone by a gate). Question: remove apps/OmpOS.App only after the parity inventory is fully green, with Phases 22 and 24 closed? Default: yes, and no deletion happens in this run. Rejected: the run would delete the MAUI head before the Avalonia heads are verified. That is a product risk, so it needs an explicit owner decision.
- Deferred to the phase's own turn: once Phases 22 and 24 close, the criterion 2 search and the shells-job change (ci.yml).

## Automated and manual

- Criteria 1, 2 and 4: automated, at the phase HEAD, once Phases 22 and 24 close.
- Criterion 3: automated search. The removal of src/OmpOS.UI is recorded under G-WEB and stays out of this phase.

## Pauses

- BLOCKER: Phases 22 and 24 must close first. Minimal action: close those phases, or record them as owner-blocked and decide whether this phase may proceed.
- GREY_AREA: G25-1, deferred until the blockers above are resolved.
