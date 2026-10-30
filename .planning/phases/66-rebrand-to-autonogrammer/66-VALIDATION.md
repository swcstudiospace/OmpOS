---
phase: "66"
slug: "rebrand-to-autonogrammer"
status: draft
nyquist_compliant: true
wave_0_complete: false
created: "2026-10-28"
---

# Phase 66 - Validation Strategy

> Every dotnet command runs through `/tmp/ag-bin/ag-dotnet` (serialised, niced); never bare `dotnet`. Old-brand tokens are never written literally in planning files (bracket-safe forms only).

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Frameworks** | Python `unittest` (stdlib) for `scripts/rebrand/`; xunit v3 4.0.2 on Microsoft Testing Platform with Shouldly 4.3.0 for .NET |
| **Quick run** | `python3 -m unittest scripts/rebrand/test_rebrand.py`; `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.OmpConfig.Tests -c Release` |
| **Full suite** | Task 1 and Task 3 of `66-07-PLAN.md` (Linux slnf build with `-warnaserror`, `test ... --no-build -- --filter-not-trait "Category=Live"`, format verify, repo checks); `bootstrap.sh` is never run directly on the shared host |
| **Live (gated, not in the gate)** | desktop title launch under a private Xvfb (`:131`) in `66-07` Task 5 route B; web smoke on ports 5387/5380/5391 |

## Per-Plan Verification Map

| Plan | Wave | Status | Requirements | Proof |
|------|------|--------|--------------|-------|
| 66-01 | 1 | done (commits 5cd0ada, 4af7d83) | AG-66-1..5 (commit 1) | read-only evidence: unit tests, `verify --baseline`, runbook s.2 probes |
| 66-02 | 1 | done (f7a9388) | AG-66-11 | read-only evidence on AGENTS.md, CLAUDE.md, CHANGELOG.md |
| 66-03 | 2 | in progress (regenerate agent) | AG-66-5 (commit 2), AG-66-6 | locked restore, build, format, notices, licences, brand checks, commit-2 shape |
| 66-04 | 3 | pending | AG-66-7 | resolver matrix tests (move, migrated, both present, unreadable, racers) in Abstractions/Workspace/Mcp/Remote/Native test projects |
| 66-06 | 3 | pending | AG-66-9 | `Harness.OmpConfig.Tests` dual-marker matrix and byte preservation |
| 66-05 | 4 | pending | AG-66-8, AG-66-10 | per-file-kind legacy discovery tests, enforced legacy-only safety files, no write into legacy, architecture literal guard |
| 66-07 | 5 | pending | AG-66-1..4, 6, 12, 13, 14 | full gate, fresh-baseline verify, services up/down, web smoke, title check, owner-action list |

## Wave 0 Requirements

- [ ] Resolver fake-home fixtures and tests (66-04 Task 1)
- [ ] Dual-marker `models.yml` fixtures (66-06 Task 1) with `LegacyBrandNames.cs` in the test project
- [ ] Per-file-kind legacy discovery tests and snapshot-before/after no-write assertions (66-05 Tasks 1 to 2)
- [ ] Old-name literal guard with self-test (66-05 Task 3)

## Sampling

After each task its own `<automated>` commands; after each wave the touched test projects; before verify-work the 66-07 gate. Max feedback latency 300 s per command (wrapper waits for host load).

## Manual-only (cannot be verified here)

Windows and macOS data-directory move and system hook paths (compile only); Android `ApplicationId`; every deployment and external rename; live omp reading renamed `models.yml` blocks; the three submodules.

## Validation Sign-Off

- [x] All plans carry automated verify with `fails_when`; plan-check: `66-PLAN-CHECK.md` (0 blockers, 3 accepted scope warnings)
- [ ] `nyquist_compliant` confirmed after execution
