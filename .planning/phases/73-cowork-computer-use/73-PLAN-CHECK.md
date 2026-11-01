# Phase 73 plan check, round 2 (2026-10-29)

Verdict: **PASS with warnings** (0 BLOCKER, 2 WARNING, 2 INFO). Read only; nothing built or run.

## Round-1 items re-verified
- B1 fixed: 73-06 is wave 4, `depends_on` 73-01..05; 73-03 wave 3, 73-02/04/05 wave 2, 73-01 wave 1. Wave = max(deps)+1 holds for all six.
- B2 fixed: `NullComputerUseBackend.cs` and `NullComputerUseBackendTests.cs` are in 73-02 frontmatter, Task 4 files and action and Artifacts; `ComputerPlatform` in 73-01 has `Unsupported`; 73-02 claims AG-73-18 (so do 73-01 and 73-04). All AG-73-1..24 still covered.
- B3 fixed: 73-06 lists 17 `packages.lock.json` files; `grep -rl '"autonogrammer.presentation": {'` finds exactly 16 others plus Presentation's own: no missing, no extra.
- B4 fixed: `SettingsWindowTests.cs` is in 73-06 files with a `Pages` row and acceptance text.
- W1/W2/W3/W5, I2/I3/I5 addressed in the plan text: `ComputerStatusAdapter`, `ComputerAccessFactsAdapter`, `ComputerUseHeadComposition` are named and the allow-list in 73-02 names them; `ComputerUseCopy.cs` is listed.
- Structure: no `files_modified` overlap among the six plans; every task has `read_first` and `acceptance_criteria`; every `<automated>` has a `<fails_when>`; every plan has `## Artifacts this phase produces`.
- Tree facts: `apps/Autonogrammer.Native/Agents/AgentHead.cs` has `new HeadSettingsOptions` at lines 107 and 196 as cited.

## WARNING
- **W-A. `apps/Autonogrammer.Native/Cowork/CoworkHead.cs` is in 73-06 `files_modified` but not on disk** (created by `70-04-PLAN.md`, which lists it at line 16). Acceptable because 73 depends on 70 and the plan says to open the real file first, but a 70-04 rename would silently invalidate the entry. Fix: execution preflight that the file exists, else stop.
- **W-B. Port hand-off between `CoworkHead` and `AgentHead`.** `ComputerUseHeadComposition.Build` is called from `CoworkHead.cs`, while `HeadSettingsOptions.ComputerUse` is assigned at the two `AgentHead.cs` sites (107, 196). How the ports flow from the Cowork composition into `AgentHead` is not specified [INFERENCE: depends on phase 70 types]. Fix: name the carrier (an `AgentHeadOptions` member or `HeadServices` property) in the Task 3 action.

## INFO
- I1: ROADMAP phase 73 `**Plans**` lists only `73-01-PLAN.md` (outside the phase dir; owner to fix).
- W4 (phases 72/73/76 ordering on shared Settings files, ROADMAP line 1301 parallel listing) remains a ROADMAP matter outside the phase dir; D-42 now records the order.
