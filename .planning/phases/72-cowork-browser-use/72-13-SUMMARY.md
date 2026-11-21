# Plan 72-13 SUMMARY: Required browser audit

Status: blocked — not executed. The plan requires 72-12 and 75-11 including its full transitive chain to land first, reconciled against the actual integrated `CoworkToolHost` implementation (75-10 owns it), and forbids concurrent work (72-13-PLAN.md:38). That chain is absent: no 75-11 SUMMARY exists and 72-12 is unexecuted (72-12-SUMMARY.md). No task was run and no product code was changed.

## What was checked (tree at c04095ba, 2026-11-21)

- `tests/Autonogrammer.Cowork.Tests/Tools/CoworkToolHostAuditTests.cs` does not exist. Task 1 and Task 2's core artifact is absent.
- `src/Autonogrammer.Cowork/Tools/CoworkToolHost.cs` and `FileCoworkToolAudit.cs` exist from the 72-03 baseline, but there is no browser-specific required-audit admission: browser effects can still start without a durable intent, audit I/O failure is still swallowed in the host's `finally` block after invocation, and there is no fail-closed "browser effects disabled until validated recovery" state. The truth 72-VERIFICATION.md:32 records ("CoworkToolHost uses… SwallowingAudit") still holds.
- `docs/runbooks/browser-use.md` contains no required-audit recovery procedure; Task 2's runbook artifact is absent. The runbook still states the live omp turn was not run (OD-07).
- `ICoworkToolAudit.cs` documents no distinction between the browser-required path and non-browser best-effort behavior.

## Blockers (unchanged from the plan)

- 72-12 (guarded session) and the 75-11 transitive chain unintegrated; 75-10 owns `CoworkToolHost` and tool leases — this plan must reconcile with its actual implementation, never work concurrently.
- Browser provider must not be exposed before 72-14 integrates the required-audit admission; 72-14 is itself blocked (D-12/D-13 incompatibility, 72-14-PLAN.md:35).
- No live omp model turn is approved (OD-07).

## Not run

- `CoworkToolHostAuditTests` (Cowork.Tests, run twice by the plan: admission failure and outcome-write failure) — not run; the class does not exist and the required-admission code was not implemented.
