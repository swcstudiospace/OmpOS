# 68-10 Summary: all-writer fence (M0012/M0014) that fixes SEC-77-07

Status: **complete in code on `main`; SEC-77-07 fixed (security review), SEC-77-09 (Low) open.**
This plan (68-10-PLAN.md, `autonomous: false`, wave 8) required the 68-09 human approval, a
revised writer-ownership matrix, and explicit D-18/D-34 authority before any source edit. The
implementation below is what actually landed on `main` (not the `gsd/v0.10.0-cross-platform-ui`
line, whose waves `30afe342`/`da8339b1`/`74238161`/`7548213a` are not ancestors of `c04095ba`).

## Commits on `main` (ancestors of `c04095ba`, SHAs verified)

- `c4e6fe2e feat(db): account erasure fence migrations M0012 and M0014; memory idempotency
  becomes M0013` (Refs SPE-32459 SPE-32463; 7 files, +250/−19):
  `Migrations/M0012_AccountErasureFence.cs` (194 lines, new),
  `M0013_MemoryIdempotency.cs` (renamed from the unreleased claim-table migration, checksum
  unchanged), `M0014_MemoryIdempotencyFence.cs` (35 lines), `MigrationRegistry.cs`,
  plus `DataPlaneStartupGateTests.cs`, `DataPlaneStatusTests.cs`, `MigrationRunnerTests.cs`
  (head moved past version 12; registry at `c04095ba` is M0010–M0015, head
  `M0015_SessionOwnerSurfaceIndex`).
- `332660ee fix(sec-77-07): fenced, resumable account erasure with a typed refusal`
  (Refs SPE-32459 SPE-32463; 11 files, +1038/−23):
  `Migrations/`-adjacent `AccountErasureFence.cs` (41 lines),
  `PostgresAccountErasure.cs` (+125, lifecycle-first under the account advisory lock),
  typed refusals `AccountErasingException.cs` / `AccountErasureBusyException.cs`,
  `ResiliencePipeline.cs` / `ResiliencePolicy.cs` / `PostgresResilienceOptions.cs` /
  `PostgresResilience.cs` wiring, and the regression suites
  `AccountErasureFenceTests.cs` (464 lines) + `AccountErasureFenceMigrationTests.cs` (304 lines).
  No `services/Autonogrammer.Api/Accounts/AccountErasureAdmission.cs` exists at `c04095ba`
  (68-10-PLAN Task 3 named it): the API-side admission is the database trigger via M0012/M0014,
  not a separate file — the plan's file list was a candidate, not the shipped shape.
- `7dadceb5 docs(sec-77-07): record the erasure fence, close SEC-77-07 with its regression
  tests` (CHANGELOG, `docs/quality/v0.16.0-security-review.md`, cowork-projects-memory doc,
  `docs/runbooks/{accounts,data-plane}.md`; SEC-77-09 opened as Low in the same commit).

## What the fence does (design note, `v0.16.0-security-review.md` §SEC-77-07 design notes 1-12)

Per-account advisory lock admission: writers take `pg_advisory_xact_lock_shared` then check the
`account_erasure_fence` row; the eraser takes it exclusively, commits the fence row, then deletes
families in D-31 order and marks the fence `erased`. A trigger on every account-owned table (17
tables, plus the memory-claim table through M0014) refuses later insert/update with SQLSTATE
AE001, covering the protected `PostgresArtifactStore`, direct SQL and older binaries. Digest-only
tombstone (SHA-256 of namespaced account id, permanent; AE004 on alteration), fail-closed AE002
(unknown owner) and AE003 (non-READ-COMMITTED), 20 s lock wait → `AccountErasureBusyException`,
runner-transaction rollback guard AE005 (M0014 carries it too), least-privilege
`SECURITY DEFINER` check, mixed-version behavior documented (unreleased-ledger checksum rebuild
note). `dispatch_audit` / `ledger_outbox` deliberately unfenced (keyed-hash, append-only) and
Dragonfly keys unfenced (≤24 h TTL, no user content; tenant scan covers tenant-carrying keys) —
both tracked in SEC-77-09, which this finish leaves open.

## Verification (security review §Verification, run by the fixing session)

"SEC-77-07 evidence ran against a run-owned TimescaleDB 2.13.1 / PostgreSQL 16 container:
Sessions.Postgres 148/148 (including `AccountErasureFenceTests` 10/10 and
`AccountErasureFenceMigrationTests` 7/7), Resilience 62/62, Cowork.Artifacts.Stores 13/13,
Cowork.Memory 35/35, Api.IntegrationTests 1556/1557 (one failure `DesktopHubIntegrationTests`
needs an OS keyring, unrelated). Removing the shared admission lock from the check makes the
admitted-writer and busy cases fail." Architecture suite 107/107 (non-Live) at the time.
Re-running those suites with services is done by the 68-VERIFICATION re-check companion
(`68-VERIFICATION.md` §Re-verification 2026-11-21), not claimed here.

## UNVERIFIED

Production database, deployment, and Dragonfly erasure were never exercised (review's own
sentence). An older binary hitting an erased account sees a raw `PostgresException` (AE001),
not the typed refusal — recorded, not a reopened finding.
