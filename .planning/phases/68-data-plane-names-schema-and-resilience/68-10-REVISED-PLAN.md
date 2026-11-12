# 68-10 revised plan: all-writer fence for account erasure (SEC-77-07)

Status: draft for owner sign-off. Planning artifact only; no source changes are authorised by this file.

Owner approvals this plan relies on (recorded in STATE.md resume-gate record): Candidate A (operator-only erasure entry, no public route); additive migration M0012; database triggers and functions as a D-34 exception; non-SQL server state in scope.

## 1. Scope and exclusions

In scope: server-side account data in the erased set (17 tables, matrix section A) and every writer that can reach them. Writers are the 46 SQL rows in matrix section B, the cache keys in section C, the background producers in section D and the API entry groups in section E.

Out of scope: desktop-local files under apps/Autonogrammer.Native (matrix U-10). They are per-user state on one machine, not server account data. They do not need the server fence.

Explicitly still open after this plan (matrix section F, appendix): artifact grants and change decisions are held in process memory and are cleared by restart; nine library writers in U-09 are not yet traced to a caller; PostgresDispatchStore has no production composition (U-11), so its admission is verified by tests only.

## 2. Admission design

2.1 Lifecycle row. M0012 adds one additive table `account_lifecycle` keyed by account id: `state` (active, erasing, erased), `erasure_epoch` bigint, `updated_at`. Additive only (CREATE TABLE IF NOT EXISTS); it does not alter any erased table. Rows are created on first write for an account, so existing data needs no backfill.

2.2 Admission. Every SQL writer in the matrix takes a shared admission on the account before its write and holds it through commit. The admission is a `SELECT ... FOR SHARE` on the lifecycle row inside the same transaction. A writer that finds state erasing or erased fails closed with a typed error and writes nothing.

2.3 Erasure. The erasure entry takes `FOR UPDATE` on the lifecycle row, sets state erasing, increments the epoch, and only then deletes the 17 tables in one transaction. Because writers hold FOR SHARE through commit, erasure cannot interleave with an in-flight write. Erasure sets state erased last, in the same transaction.

2.4 Triggers (D-34 exception, exact SQL to be reviewed before merge). A trigger on each erased table raises when the account row is in state erasing or erased. This covers direct and legacy writers that bypass the application admission. Rollout: install triggers after application admission is deployed, so no writer is blocked by an unfenced build.

2.5 Cross-process drain. Background producers (matrix section D: SessionRunReaper, DispatchDeliveryLoop, CloudIdleSweeper, CloudAgentLifecycle, CloudAgentRuntime, AutomationScheduler, AutomationRunner, CoworkSessionSync, LedgerOutboxPump) check the lifecycle state before each unit of work and stop when erasing. Erasure waits until in-flight units finish, bounded by a timeout that fails closed.

2.6 Cache keys (matrix section C). Dragonfly tenant-keyed families are invalidated by erasure through the existing DragonflyTenantErasure path. Any key family without an erasure path is listed as a gap, not silently ignored.

2.7 Entry points (matrix section E). The erasure entry is operator-only: a command in the API host (matching the MigrateCommand pattern), not an HTTP route. Its authorisation is an operator identity supplied at start-up and an explicit target account.

## 3. Task list and file ownership

Wave 1 (schema, no behaviour change)
- T1 owns src/Autonogrammer.Sessions.Postgres/Migrations/M0012_AccountLifecycle.cs (create) and src/Autonogrammer.Sessions.Postgres/Migrations/MigrationRegistry.cs. Change: register M0012 after M0011. Acceptance: migration applies to an empty and to an M0011 database; re-run is a no-op; checksum stable.

Wave 2 (admission in SQL writers, file-disjoint)
- T2 owns src/Autonogrammer.Sessions.Postgres/PostgresSessionRepository.cs. Change: FOR SHARE admission in each write path (matrix B-01 to B-05 family). Acceptance: write under erasing fails closed; write under active succeeds.
- T3 owns src/Autonogrammer.Sessions.Postgres/PostgresCloudAgentStore.cs. Change: admission on insert, update, delete (matrix section B cloud rows).
- T4 owns src/Autonogrammer.Sessions.Postgres/PostgresDispatchStore.cs. Change: admission on device, command, event and account writes. Acceptance proven by tests only (U-11).
- T5 owns src/Autonogrammer.Cowork.Memory.Postgres/PostgresMemoryStore.cs. Change: admission inside SaveIdempotentInTransactionAsync (matrix U-07, call at line 168) and settings and delete paths.
- T6 owns src/Autonogrammer.Cowork.Artifacts.Stores/PostgresArtifactStore.cs. Change: admission on artifact, version and delete writes (B-36, B-37). Requires the owner's artifact-store authority, already approved.
- T7 owns services/Autonogrammer.Api/Cowork/Projects/PostgresProjectAccountStore.cs. Change: admission on project and instruction upserts.
- T8 owns services/Autonogrammer.Api/Cowork/Artifacts/ArtifactAudit.cs. Change: admission on the cowork_events insert (U-06). Also records the unbounded in-memory queue observation for follow-up.
- T9 owns services/Autonogrammer.Api/Audit/PostgresLedgerOutboxStore.cs. Change: admission on dispatch_audit and ledger_outbox enqueue (U-08). ledger_outbox has no account column, so admission goes through dispatch_audit's account hash.

Wave 3 (producers and entry points)
- T10 owns the background producer files named in section D: admission check before each unit of work (2.5). Files are listed in the matrix section D; each task names one file.
- T11 owns services/Autonogrammer.Api/Endpoints/CatalogEndpoints.cs and services/Autonogrammer.Api/Endpoints/SessionConfigEndpoints.cs (U-01, U-12): admission before the engine write.
- T12 owns services/Autonogrammer.Api/Cloud/CloudAgentService.cs (U-13): admission before the cloud_agents update.

Wave 4 (erasure and triggers)
- T13 owns src/Autonogrammer.Sessions.Postgres/PostgresAccountErasure.cs. Change: lifecycle-first erasure per 2.3, replacing the seven separate family transactions. Acceptance: concurrent writer during erasure is blocked or fails closed; unrelated account unaffected.
- T14 owns src/Autonogrammer.Sessions.Postgres/Migrations/M0013_AccountLifecycleTriggers.cs (create). Change: trigger SQL per 2.4. Gated on the owner reviewing the exact SQL.
- T15 owns services/Autonogrammer.Api/Cli/ (operator erasure command; create the file under the existing command pattern). Change: operator entry per 2.7.

Wave 5 (proof)
- T16 owns tests/Autonogrammer.Sessions.Postgres.Tests/AccountErasureFenceTests.cs (create). Concurrency tests against a run-owned database.
- T17 owns tests/Autonogrammer.Api.IntegrationTests/Security/AccountErasureAdmissionTests.cs (create). Admission from API paths.

## 4. Verification per wave

Each wave is verified by the orchestrator, not by a subagent: build with warnings as errors, then the named test classes against a run-owned Postgres (scripts/test-services.sh up, with a dedicated AUTONOGRAMMER_TEST_SERVICES_NAME). Live tests stay gated on that run-owned service. A wave that fails stops the later waves.

## 5. Owner decisions still needed

1. Approve the exact trigger SQL in T14 before it is merged (D-34 exception requires it).
2. Confirm the drain timeout behaviour in 2.5 (fail closed after how long).
3. Confirm the operator identity mechanism for T15.
4. Confirm that the in-memory artifact grants and change decisions may stay out of the fence, with the restart gap written into the security review.
