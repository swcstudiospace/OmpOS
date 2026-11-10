# Phase 66-07 evidence (2026-11-10)

Commit under test: `5ce514eb` (branch `gate/head-fixes`), worktree `/tmp/autonogrammer-gate`.

- `bootstrap-gate-5ce514eb.txt` (committed as text; `*.log` is gitignored): `./bootstrap.sh` run, `EXIT=0`, `bootstrap: OK`. Covers locked restore, Release build (0 warnings, 0 errors), skip-marker, licence, brand and changelog checks, .NET suite (14,634 total, 0 failed, 3 skipped), format, web smoke with axe, API smoke, Demo smoke, artifact smoke and size budget.
- `rebrand-dry-run-fresh.txt`, `rebrand-verify-fresh.txt`: fresh report directory; dry-run exit 0; verify prints `verify OK: nothing left to rename`.
- `rebrand-verify-original-informational.txt`: verify against the original post-decision baseline, exit 1, recorded as informational. Differences are keep-pattern growth only (`OMP_*` 78→81, `PI_CODING_AGENT_DIR` 113→114, `bare omp` 8398→8430); no lowered counts and no collision or "still carry" lines.
- Rebrand tool unit tests: `Ran 61 tests … OK` (the plan's acceptance text says 49; the observed count is 61).
- LegacyBrandNames inventory: 14 tracked files (3 product, 9 test, 2 services), matching the plan's verify pin set. The plan's acceptance text says 12; recorded as a discrepancy.

Not covered here: Tasks 3 to 7 of 66-07 (test-service containers, web smoke on private ports, window title, owner-action list, final record).

## Task 4: web smoke on private ports (2026-11-10)

- Ports 5387 (API), 5380 (web), 5391 (demo): free before and after the run (`ss -ltn`), and no listener left behind.
- Run under `/tmp/ag-dotnet.lock` with `SMOKE_TARGETS="api demo"`, `OUT_DIR` a mktemp directory outside the repo.
- Result: `api smoke exit=0`, `demo smoke exit=0`, `SMOKE_EXIT=0`.
- `web-smoke-3731a813-2026-11-10.json`: `noErrorBarOrConsoleErrorsAfterPrompt: true`.
- `web-smoke-demo-3731a813-2026-11-10.json`: `passed: true`, `failures: []`, including `demoAxeNoViolations`, `demoAxeNoIncomplete`, `demoNoConsoleErrorsOrFailedRequests`, `a11yAxe`, `a11yReflow`, `a11yTargetSize`.
- Commit under test: `3731a813` (same tree as integration HEAD plus evidence files).

## Task 6: owner-action list (2026-11-10)

- `.planning/phases/66-rebrand-to-autonogrammer/66-OWNER-ACTIONS.md` (from commit `06400a79`) passes the plan's validator: eight items `## 1.` to `## 8.` in order, each with exactly `Status: open` and one proving command.
- No completed-external-rename claim (scan for "renamed to", "has been renamed", "completed rename", "is done": no match).
- Proving commands run read-only; output in `task6-owner-actions-proving-output.txt` (exit 0 overall; a non-zero line inside is a expected "no match" from grep, not a failure of the list).

## Task 5: window title, headless route (2026-11-10)

- Existing test `tests/Autonogrammer.Native.Tests/Branding/MainWindowTitleTests.cs` run through the wrapper `/tmp/ag-bin/ag-dotnet` at commit `3731a813`: `Passed! total: 1`, exit 0 (`task5-window-title-headless.txt`).
- Not run: the second route (desktop head under Xvfb showing the title). Recorded as not verified.
- Note: an outer `flock /tmp/ag-dotnet.lock` deadlocks with the wrapper, which takes the same lock itself. Run the wrapper without an outer lock.

## Task 7: partial final record (2026-11-10)

- Done: Task 1 (gate `5ce514eb`, `bootstrap: OK`), Task 2 (rebrand tool, 61 unit tests, fresh verify exact string, 14 LegacyBrandNames files), Task 4 (web smoke, private ports, passed), Task 5 headless (passed), Task 6 (owner-action list validated).
- Blocked: Task 3 (AG-66-12). The unsuffixed test containers and the `-g7405-*` containers belong to other sessions; see `task3-blocked.md`. The gate at `5ce514eb` ran the service-backed suites green with suffixed containers, cited as the gate record.
- Not complete: 66-07 is not marked passed. Criterion 3 is blocked; the desktop Xvfb title route is not run.
