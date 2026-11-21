# 66-09 Summary: gap closure for approved-baseline preservation and the current-tree gate (SUPERSEDED, not executed)

Status: **superseded by `3f6fe8f0`** (2026-11-20). This plan was never executed: its three tasks are
`autonomous: false` / `gap_closure: true` and gated on a blocking-human decision checkpoint
(66-09-PLAN.md frontmatter, Task 1 decision checkpoint). No guarded external evidence directory was
created by this lane, no repository file was edited by this plan, and no source change was made.
What follows records the outcome and the durable evidence that replaced the plan's route.

## What the plan asked for (66-09-PLAN.md)

- Task 1: bind the recorded approved-baseline failure to authenticated historical inputs
  (`/root/src/backups/rebrand-baseline-post-decision-2026-11-04/rebrand-report.json`, SHA256
  `6e55ba16…504` per the plan) via a guarded `provenance.py` in a mode-0700 external evidence dir.
- Task 2: reconcile every rejected/keep occurrence with multiplicity, including the 293 protected
  URL records and the 1074 excluded/frozen protected URL occurrences.
- Decision checkpoint: present the D-05/AG-66-3 strict-equality rule against the 66-07
  informational-growth ruling to a named owner; no baseline-pass claim before the decision.
- Task 3: only after explicit owner authorization and an isolated environment, run the full
  current-tree gate and `bootstrap.sh`.

## Why it was superseded (commit `3f6fe8f0`, ancestor of `c04095ba`)

`3f6fe8f0 docs(phase66): close out rebrand phase with a durable verify gate` retired the
2026-11-04 strict-equality baseline, because strict equality cannot hold after v0.16.0–v0.20.0
shipped, and redefined "clean tree" as the durable gate the plan's supersession paragraph records:

- `python3 scripts/rebrand/rebrand.py verify` (exit 0 = nothing rewritable remains), plus
- `python3 -m unittest scripts/rebrand/test_rebrand.py` (61 tests),

both wired into `scripts/ci/repo-checks.sh`. The same commit reworded the three bare `OmpOS`
tokens (78-04 plan, OQ-63, OQ-102) so `verify` passes on the current tree, refreshed
`66-VERIFICATION.md` to `passed 5/5` (CI green on merged HEAD `ed0a9a02`, runs 44532185561 and
44541692228), and appended the supersession paragraph to `66-09-PLAN.md` itself ("This plan's
guarded evidence-gate approach is retired"). The baseline file at
`/root/src/backups/rebrand-baseline-post-decision-2026-11-04/rebrand-report.json` still exists on
this host; its existence does not reinstate the plan — no provenance, occurrence ledger, or
gate-status evidence was produced.

## Re-run in this pass (2026-11-21, HEAD `c04095ba`, SDK 10.0.402)

- `python3 -m unittest scripts/rebrand/test_rebrand.py` → Ran 61 tests, OK, exit 0 (19 s).
- `python3 scripts/rebrand/rebrand.py verify` → `verify OK: nothing left to rename (327 URL
  tokens still protected)`, exit 0. (Token count is 327 at `c04095ba`; 66-VERIFICATION.md quoted
  294 at `ed0a9a02` — the tree grew; the gate is the exit code, not the count.)
- `bash scripts/ci/repo-checks.sh` → `repo-checks: ok`, exit 0; it internally runs the same 61
  rebrand tests and `verify` (327 protected tokens) plus brand, changelog (29 versions), docs
  (224 files, 0 errors), skip markers (94 kinds / 92 files), and a 58-test script suite — all OK.

## UNVERIFIED / open in this pass

- Full web smoke (`scripts/web-smoke/run-smoke.sh`) and the headless desktop-title test were NOT
  re-run here (ports/services/Chrome + display, ~90 min); 66-VERIFICATION.md records the prior
  gate evidence. CI on `main` at `c04095ba` (run 44955765885) was still in progress when this
  summary was written, and the last completed `main` CI runs (44860704074, 44872419265,
  44877775564) failed — so no current-tree CI-green claim is made from this pass.
- Owner actions 1–8 (66-OWNER-ACTIONS.md, runbook s.5) remain open as recorded; this plan never
  claimed them.

## Files

- Modified by this plan: none. (`3f6fe8f0` edited ROADMAP.md, 66-09-PLAN.md, 66-VERIFICATION.md,
  78-04-PLAN.md, docs/open-questions.md, docs/runbooks/rebrand-autonogrammer.md,
  scripts/ci/repo-checks.sh — 7 files, +32/−28.)
- Created by this summary: `.planning/phases/66-rebrand-to-autonogrammer/66-09-SUMMARY.md` only.
