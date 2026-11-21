# Cleaned brief T77d9-req

Status: Record (cleaned brief for T77d9-req)

Source: task.assign T77d9-req, correlation 77d97ef8-56a4-4b53-ae66-5b8c5e5d659e.
PII scan: found none. Ambiguity: score 0.0 (8 sentences, 0 flagged).
Note: origin/main at 434e214e is the baseline. Local main is 93 commits behind
and holds 10 modified auth and secrets files under services/Autonogrammer.Api,
src/Autonogrammer.Accounts, src/Autonogrammer.Harness.Secrets.Os and 4 test files.
The .NET 10.0.402 SDK is absent on this host. Verification runs in CI only.
Global.json and the SDK must not change.

## Stakeholders

- Maintainer: owns the Autonogrammer codebase and releases.
- A01 orchestrator: owns branch, PR and merge sequencing.
- A08 quality, A09 review, A12 release: enforce this spec at gates.
- Owner: supplies credentials, sign-in decisions and release approval.

## Constraints

- Branch from origin/main. Preserve the 10 dirty files onto that branch.
  Keep main clean. No direct commits to main.
- Audit and harden the top State-of-the-Art gaps only. Reuse phases 25.1-25.9
  lenses 1-6 and the 25.2-25.9 slices. Introduce no new conventions.
  Shrink no scope. Prohibited patterns stay prohibited: Docker tooling,
  Newtonsoft, FluentAssertions 8.x.
- Stack bar: net10.0, nullable with warnings as errors, System.Text.Json
  source generation, xUnit v3 with Microsoft Testing Platform, Shouldly,
  ArchUnitNET, bUnit, central package management with locked restores,
  podman for containers.
- Map GSD phases 66 through 78, including 66.1, 66.2, 66.3, 66.4 and 74.1,
  to Linear parents and sub-issues with Given/When/Then criteria.
  Create no duplicates. Link every mapping to its GSD phase.
  A phase blocked on an owner action stays blocked.
- Merge to main only when every CI job is green. Update STATE.md and
  ROADMAP.md at the end.

## Open questions

- Phase 72 stays blocked on an owner action. Which credential or decision
  unblocks it, and who supplies it? Tracked as SPE-34915 and the owner register.
- Phase 78 has no CI proof yet. Which rehearsal run id proves plan 78-06?
- The 10 dirty files are auth and secrets work. Do they merge cleanly onto
  origin/main, or do they conflict with the 0.19.x line on origin?
