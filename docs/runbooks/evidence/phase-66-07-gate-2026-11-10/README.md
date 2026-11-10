# Phase 66-07 evidence (2026-11-10)

Commit under test: `5ce514eb` (branch `gate/head-fixes`), worktree `/tmp/autonogrammer-gate`.

- `bootstrap-gate-5ce514eb.log`: `./bootstrap.sh` run, `EXIT=0`, `bootstrap: OK`. Covers locked restore, Release build (0 warnings, 0 errors), skip-marker, licence, brand and changelog checks, .NET suite (14,634 total, 0 failed, 3 skipped), format, web smoke with axe, API smoke, Demo smoke, artifact smoke and size budget.
- `rebrand-dry-run-fresh.txt`, `rebrand-verify-fresh.txt`: fresh report directory; dry-run exit 0; verify prints `verify OK: nothing left to rename`.
- `rebrand-verify-original-informational.txt`: verify against the original post-decision baseline, exit 1, recorded as informational. Differences are keep-pattern growth only (`OMP_*` 78→81, `PI_CODING_AGENT_DIR` 113→114, `bare omp` 8398→8430); no lowered counts and no collision or "still carry" lines.
- Rebrand tool unit tests: `Ran 61 tests … OK` (the plan's acceptance text says 49; the observed count is 61).
- LegacyBrandNames inventory: 14 tracked files (3 product, 9 test, 2 services), matching the plan's verify pin set. The plan's acceptance text says 12; recorded as a discrepancy.

Not covered here: Tasks 3 to 7 of 66-07 (test-service containers, web smoke on private ports, window title, owner-action list, final record).
