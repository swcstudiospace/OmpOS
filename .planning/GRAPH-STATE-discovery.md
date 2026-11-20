# GRAPH-STATE Discovery — v0.16.0 outstanding phases (SPE-35244, Wave 1 · n1)

Date: 2026-11-20. Sources read: `.planning/ROADMAP.md` + `.planning/STATE.md` in
worktree `/root/src/worktrees/ag-plugin-connector` (WT, branch `docs/phase68-closeout`,
HEAD `94d64280`, parent `f5d453d1` = PR #23 / Phase 66.4 closeout) and same pair in
main checkout `/root/src/repos/autonogrammer` (MAIN, branch `main`, HEAD `9f22f50d`).
Artifact counts below are WT filesystem inventory of `.planning/phases/<dir>/`.
Assignment truth (repo-truth context): main at `f5d453d1` (66.4 merged); 67 closeout
is PR #24 (CI building); 68 docs committed locally on `docs/phase68-closeout` (WT HEAD).

## Outstanding milestone

**v0.16.0 Autonogrammer: accounts, Cowork and Dispatch** (ROADMAP:13, both copies).
WT STATE: milestone v0.16.0, current_phase `78`, status `executing`, 18 total phases /
0 completed / 155 plans / 82 done (53%). MAIN STATE: v0.16.0, current_phase `74.1`,
status `blocked`, 17 phases / 11 completed / 111 plans / 85 done (65%) — stale
(2026-11-16 vs WT 2026-11-19 reconciliation). WT reconciliation 2026-11-19
(STATE "Reconciliation 2026-11-19", ROADMAP "Progress (v0.16.0)"): 0/17 phases
verified by a gate run; 2 complete (66.1, 69), 7 substantially complete
(66, 66.2, 66.3, 66.4, 67, 68, 74.1), 7 partial (70, 71, 73, 74, 75, 76, 77),
1 blocked on owner (72); 107 outstanding + 64 blocked items. `completed_phases: 0`
in WT — no VERIFICATION passed by gate.

## Per-phase inventory (ROADMAP order) — 66.4 / 67 / 68 / 71 / 73 / 74 / 75 / 76 / 77

Artifact columns: CONTEXT = `*-CONTEXT.md`, PLAN = `*-PLAN.md` count, SUMMARY =
`*-SUMMARY.md` count, VERIFICATION = `*-VERIFICATION.md` count (+ VALIDATION).

| Phase | Dir | C | P | S | SOTA | VERIF | VALID | Total | Status (WT recon 2026-11-19) | Blockers |
|---|---|---|---|---|---|---|---|---|---|---|
| 66.4 Quality uplift: CI, ops, brand | `66.4-quality-uplift-ci-ops-brand` | 1 | 1 | 1 | 0 | 3 (`-VERIFICATION`, `-2026-11-05`, `-2026-11-08`) | 0 | 6 | **Closed** per assignment (merged `f5d453d1`, PR #23). Recon: substantially complete, never verified complete, 6 outstanding / 4 blocked | Owner: SLO targets need deployment measurement, logo/trademark OQ-105 |
| 67 Accounts: Supabase passwordless sign-in | `67-accounts-supabase-passwordless-sign-in` | 1 | 8 | 7 | 1 | 1 | 1 | 22 (+RESEARCH/REVIEW/SECURITY) | **Closing via PR #24** (CI building). Recon: substantially complete, `human_needed`, 7 outstanding / 5 blocked | Human verification / credential-gated live sign-in; VERIFICATION 2026-11-10 was static-only, SDK 10 absent |
| 68 Data plane: names, schema, resilience | `68-data-plane-names-schema-and-resilience` | 1 | 13 (01–11 +?; 09–11 blocked drafts) | 8 (01–08) | 1 | 1 | 1 | 25 (+RESEARCH, PLAN-CHECK) | **Docs-done, pending merge** (WT HEAD `94d64280 docs(phase68): closeout reconciliation`). Recon: substantially complete, 6 outstanding / 3 blocked | Erasure writer-drain + composition boundary (STATE:41); SEC-77-07 fixed on main `7dadceb5`; no fresh VERIFICATION |
| 71 Cowork Artifacts | `71-cowork-artifacts` | 1 | 14 (01–13) | 7 (01–06; 07–12 unsummarized) | 2 | 1 | 1 | 26 (+RESEARCH) | Partial; 8 outstanding / 6 blocked | Blocked on non-lossy sync rollout, verified device/session identity, browser destination enforcement; 71-13 uncommitted |
| 73 Cowork Computer Use | `73-cowork-computer-use` | 1 | 11 (01–11) | 8 (01–06, 08) | 5 | 1 | 1 | 27 (+RESEARCH) | Partial; 9 outstanding / 4 blocked | No 6-criteria run observed; ADR-0035 still Proposed; Dispatch-facing closure needs 74 |
| 74 Dispatch relay | `74-dispatch-relay` | 1 | 12 (01–12) | 10 | 1 | 1 | 1 | 27 (+RESEARCH) | Partial; 7 outstanding / 3 blocked | No 74-11-SUMMARY equivalents closed; VERIFICATION says "has not passed"; production path disabled by owner, legacy-row policy undecided |
| 75 Cowork parity A: projects, memory, schedules | `75-cowork-parity-projects-memory-schedules` | 1 | 13 (01–13) | 6 | 1 | 1 | 1 | 24 (+RESEARCH) | Partial; 8 outstanding / 5 blocked; 75-12 held (74.1 verification) | Plans 07–13 gaps; ROADMAP lists only 01–06; no gate passed |
| 76 Cowork parity B: plugins, Auto mode, isolation | `76-cowork-parity-plugins-auto-isolation` | 1 | 10 (01–09 +?) | 8 | 2 | 1 | 1 | 24 (+RESEARCH) | Partial; 7 outstanding / 4 blocked | Bubblewrap positive-control failed (malformed bind args); isolation unverified; SEC-77-08 High open; consumes 75-06 seam |
| 77 Hardening, docs and integration | `77-hardening-docs-and-integration` | 1 | 12 (01–11 +?) | 5 (01,03,07,08 +?) | 2 | **0** | 1 | 21 (+RESEARCH, PLAN-CHECK) | Partial; 9 outstanding / 4 blocked | **No 77-VERIFICATION.md**; bootstrap BLOCKED at container pre-check; 0/11 plans verified |

Notes: 66.4 is the only phase in scope with 3 VERIFICATION files and no VALIDATION.
77 is the only in-scope phase with no VERIFICATION file at all. All other in-scope
phases have both VERIFICATION and VALIDATION. Plans-vs-summaries gaps (unsummarized
plans): 68-09..11, 71-07..12, 73-07, 74-04..09 (per MAIN STATE:121), 75-07..13,
76-09, 77-02/04/05/06/09/10/11.

## Empty-case check

**Nonzero remaining phases exist.** 8 of 9 in-scope phases are not gate-verified
(0/17 verified overall per recon). Even counting assignment truth (66.4 closed,
67 closing, 68 docs-done), phases 71/73/74/75/76/77 are all partial with open
outstanding+blocked items — the graph is not empty.

## Cross-file conflicts (WT vs MAIN, and within-file)

1. **STATE current_phase/status**: WT `78 / executing / 2026-11-19` vs MAIN
   `74.1 / blocked / 2026-11-16`. MAIN is stale; WT supersedes. MAIN frontmatter
   `completed_phases: 11` vs WT `0` (WT explicitly: no gate run = 0 completed).
2. **ROADMAP length**: WT 1958 lines vs MAIN 1908 lines; WT adds Phase 78 entry,
   2026-11-19 reconciliation notes, and progress table rows with Linear parents.
3. **Progress table divergence (material)**: MAIN progress rows use old
   summary-slot counts (73: 6/6, 74: 4/10, 75: 5/6, no Linear links); WT rows use
   reconciled counts (73: 5/10, 74: 8/11, 75: 4/13, 76: 1/9, 77: 1/11) with
   outstanding/blocked counts and Linear parents (SPE-34897 … SPE-34946). WT is
   newer; MAIN rows are superseded.
4. **Within-WT caveat**: ROADMAP phase-list checkboxes show 66.4 `[x]` and
   66.2/66.3 `[x]` (closeout 2026-11-20 notes) while the 2026-11-19 Progress table
   still says "never verified complete" for the same phases — checkbox = recon
   closeout, not gate pass. 67/68/71/73/74/75/76/77 remain `[ ]`.
5. **66.4 status naming**: assignment truth says "closed" (merged #23); WT Progress
   table (2026-11-19) says "substantially complete, never verified complete". The
   merge is the closure event; the table predates it by one day — record as
   closed-via-merge, verification still not gate-run.
6. **Deferred Verification table** (both STATEs, pre-2026-11-19 provenance): lists
   66.4 as `verification_deferred_human` with a "never execute" hold — superseded
   by the `f5d453d1` closeout merge; do not treat as current.
7. **Recon vs assignment on 67/68**: recon (11-19) says substantially complete;
   assignment dates 67 closeout to PR #24 (CI building) and 68 to WT HEAD
   `94d64280` (11-20) — these are post-recon events, consistent as next steps.

No edits made beyond this file.
