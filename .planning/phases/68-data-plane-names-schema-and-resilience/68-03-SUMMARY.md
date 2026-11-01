---
phase: 68-data-plane-names-schema-and-resilience
plan: 03
requirements: [AG-68-3, AG-68-4, AG-68-5, AG-68-6, AG-68-14]
status: complete
---

# 68-03 Summary: Postgres data plane (runner, M0001-M0009, roles, erasure, resilience, EnsureCreated gone)

## Commits

| Hash | Message |
| --- | --- |
| `cb454ce7` | feat(68-03): SessionSurface, SessionCursor, ListByOwnerAsync, SetClosedAsync and the postgres resilience pipeline in both stores |
| `25d75784` | feat(68-03): regenerate openapi for Session.Surface, Session.ClosedAt and SessionSurface |
| `1f51b250` | feat(68-03): migration runner, steps M0001-M0009, advisory lock, checksums, down steps; migrated test schemas and databases |
| `1097e03b` | feat(68-03): roles via bootstrap.sql, account erasure, EnsureCreated deleted and every caller migrated, feed ordering tests |

The one-hunk edit of `JwtTestHost.PostgresConnectionString()` (call to `TestDatabaseMigrator`) was swept into lead-67a's commit `23c7c2b2`.

## Verification (what was actually run)

- `Sessions.Tests` (in-memory contract incl. the new list/close tests): 122 passed, 0 failed.
- `Sessions.Postgres.Tests` against the throwaway container, run 1: 94 total, 92 passed, 2 failed: `Update_IsAtomicAcrossStores` (40 concurrent calls exceed the interactive bulkhead of 16 by design, DataPlaneTimeoutException BulkheadFull) and a `DROP SCHEMA` deadlock in test cleanup. Run 2 used a stale binary (the combined msbuild step did not rebuild that project because of analyzer error MA0006 in my cleanup code): 89/94, the other failures were the same cleanup deadlock (SQLSTATE 40P01 against the TimescaleDB retention jobs the migrations start).
- Fixes (committed): test cleanup deletes the schema's TimescaleDB jobs and retries the drop on 40P01; the atomic-update test builds its stores with a wider bulkhead; MA0006 fixed. Run 3 (rebuilt with the fixes): 94 total, 94 passed, 0 failed, 0 skipped (6m 32s).
- All new classes passed in run 1: `MigrationRunnerTests`, `PostgresRoleTests` (bootstrap.sql applied by psql in the container, twice, plus after the migrations), `PostgresAccountErasureTests`, `PostgresTwoProcessFeedTests` (two OS processes), `PostgresResilienceTests`, `PostgresSessionRepositoryContractTests`.
- `Api.IntegrationTests` `ApiAuthStartupTests`, `SessionStoreProductionTests`, `PostgresRoutedRunTests` (container): 8 passed, 0 failed.
- Builds: Api (regenerates `docs/api/openapi.json`) and `Api.IntegrationTests` with `-warnaserror` succeeded on the last attempt (an earlier CA2000 in `JwtTestHost.NewEcKey` came from commit 23c7c2b2, not mine; lead-67a told).
- Not run: the final full-solution Release gate (orchestrator), `ArchitectureTests`.

## What was built

- `Contracts/Sessions`: `SessionSurface {Code, Cowork}`, `SessionCursor`, `Session.Surface` (init, default Code) and `ClosedAt`.
- `ISessionRepository.ListByOwnerAsync` (newest first, keyset on (updated_at, id), limit 1..200, headers+messages, no events) and `SetClosedAsync` (true only on change, does not move UpdatedAt) in the in-memory twin and Postgres; both test doubles updated.
- `Sessions.Postgres/Migrations`: `IMigrationStep`, `IMigrationPrecondition`, `MigrationRunner` (target schema parameter, default `autonogrammer`; one tx per step under `pg_advisory_xact_lock("autonogrammer.schema.migrations")`; SHA-256 checksums; `MigrateAsync`, `DownAsync(version)`, `ListAppliedAsync`, `GetCurrentVersionAsync`, `Head`), `MigrationRegistry.All`, M0001-M0009, `MigrationChecksumMismatchException`, `MigrationPreconditionException`.
- `PostgresAccountErasure.EraseAsync(accountId, tenantHash, Func<string,CancellationToken,Task>, ct)`: one transaction per family in D-31 order, delegate awaited once after the last commit; `dispatch_audit` untouched.
- `deploy/data-plane/bootstrap.sql` (psql, idempotent, secrets only from psql variables or `AUTONOGRAMMER_*_PASSWORD` env, none in the file).
- `PostgresRole`, `PostgresSessionConnection.CreateDataSource(role, options)` (search path, connect timeout, pool 20/2/4), `PostgresResilience` (shared pipeline, driver predicates), DI registration reads `DataPlane:Postgres:Resilience:*`.
- `EnsureCreated` classes deleted; callers migrated: `SessionStoreStartup` (runner only when `Sessions:Postgres:AutoMigrate=true`), `CloudAgentStoreStartup` + registration line deleted, Probe, seven test classes, `TestDatabaseMigrator` + `JwtTestHost`. `grep EnsureCreated|PostgresSessionSchema|PostgresCloudAgentSchema` over src, tests, services, apps (.cs): empty. `SessionsPostgresOptions` gained `AutoMigrate`, `MigratorConnectionString`.

## Resilience per method (operation class, idempotent flag)

`PostgresSessionRepository`: Get Interactive/idempotent; Save, Append Interactive, whole-transaction (one retry on 40001/40P01 only, never on transient errors); ListFromSeq, ListByOwner, GetToolApproval Interactive/idempotent; ListSessionIdsByStatus Background/idempotent; SetClosed, TryAddToolApproval, TryDecideToolApproval Interactive/not idempotent (a retry after a commit would change the answer); `LockSessionAsync` stays outside the pipeline on its dedicated unpooled connection (the lock wait is unbounded by design). `PostgresCloudAgentStore`: Create, Update, TryActivate Interactive/whole-transaction; Get, GetBySession, FindByIdempotencyKey, ListByOwner, GetUsage Interactive/idempotent; ListByStatus, ListAll Background/idempotent; Delete Interactive/not idempotent. Erasure families and every runner command: Background/idempotent. Transient = Npgsql IsTransient or SQLSTATE 53300/57P01/57P02/57P03; a server answer (constraint, 40001, 40P01) is not.

## Deviations and decisions (for the orchestrator)

1. Exception type is `MigrationChecksumMismatchException` (CA1710 requires the suffix); the plan's `MigrationChecksumMismatch` name is not used.
2. `Surface` and `ClosedAt` are written when a session is created and kept as stored on every later `SaveAsync` (a stale header cannot reopen a session). `SetClosedAsync` is the only writer of `closed_at`. Documented on the interface, tested.
3. `sessions.surface` stores the lower-case name (`code`, `cowork`, per D-20/D-23 literals) through `SessionSurfaceColumn`; DEFAULT `'code'`.
4. "Every statement schema-qualified" is implemented as unqualified names resolved through the forced search path (`autonogrammer,public` for the stores; the runner sets the schema first in each transaction's search path), so the same SQL runs in the `t_<guid>` test schemas.
5. M0008 licence guard is an `IMigrationPrecondition` (code `retention_requires_timescale_license`).
6. Additive extras not named in the plan: indexes `cowork_events_account_idx`, `artifact_versions_account_idx`, `artifact_grants_account_idx` (erasure by account), `dispatch_devices_account_idx`, `dispatch_commands_account_idx`, `ledger_outbox_pending_idx`; `dispatch_audit.id` and `ledger_outbox.id` are `GENERATED BY DEFAULT AS IDENTITY`; chunk intervals: 1 day for session/cowork/dispatch events, 7 days for audit; `cowork_memory_settings` flags default false; no FK from `dispatch_commands` to devices. M0005 ends with a guarded `REVOKE UPDATE, DELETE, TRUNCATE ON dispatch_audit FROM autonogrammer_app` (only when the role exists); bootstrap.sql repeats it for tables that already exist.
7. Observation for 68-04/Api: the 68-01 bulkhead rejects (does not queue) above 16 concurrent interactive Postgres calls per process; a burst beyond that surfaces as `DataPlaneTimeoutException` (BulkheadFull).
8. D-28 production writers belong to phase 74; the ordering tests drive the locking pattern with the Probe in new `feed` and `commands` modes (two OS processes) and a resumer.
9. Test infra: `PostgresTestDatabase` creates from `template_timescaledb` when the server has it; `PostgresServerFixture` fails clearly when the extension is missing in the default database (no extension statement in first-party test code) and exposes `ContainerName` (default `autonogrammer-test-postgres`); `PostgresRoleTests` uses `podman cp` and `podman exec psql` on that container with secrets passed through the environment.
10. Not done here (68-07 owns): docs quoting the deleted schema classes (`hosted-threat-model.md`, `data-retention.md`), CHANGELOG, `docs/open-questions.md`. `docs/api/openapi.json` is regenerated and committed (68-04 regenerates last).

## Suppressions and skips

- Added: one `SuppressMessage` CA2100 on `MigrationRunner.ExecuteTextAsync` (executes the up/down SQL of registered steps; constants of internal classes). Needs a SUP row.
- Existing, unchanged: CA2100 in `PostgresTestDatabase.cs` (SUP-117) and `PostgresTestSchema.cs` (SUP-118); `Assert.Skip` in `PostgresTwoProcessAppendTests.cs` (SUP-119). No `#pragma`, no new `Assert.Skip`.

## Files changed

Src: `Contracts/Sessions/{SessionSurface,SessionCursor,Session}.cs`; `Sessions/{ISessionRepository,InMemorySessionRepository}.cs`; `Sessions.Postgres/{PostgresSessionRepository,PostgresCloudAgentStore,PostgresResilience,PostgresRole,SessionSurfaceColumn,PostgresSessionConnection,ServiceCollectionExtensions,PostgresAccountErasure}.cs`, `Migrations/*` (15 files); deleted `PostgresSessionSchema.cs`, `PostgresCloudAgentSchema.cs`. Api: `Persistence/SessionStoreStartup.cs`, `Cloud/CloudServiceRegistration.cs`, `Options/SessionsPostgresOptions.cs`; deleted `Cloud/CloudAgentStoreStartup.cs`. `deploy/data-plane/bootstrap.sql`, `docs/api/openapi.json`. Tests: `Sessions.Tests/{SessionRepositoryContractTests,ObservingSessionRepository}.cs`; `Api.IntegrationTests/{Hosting/FailingSessionRepository,JwtTestHost,DataPlane/TestDatabaseMigrator}.cs`; `Sessions.Postgres.Probe/Program.cs`; `Sessions.Postgres.Tests/` new `MigrationRunnerTests`, `PostgresRoleTests`, `PostgresAccountErasureTests`, `PostgresTwoProcessFeedTests`, `PostgresResilienceTests`, changed `PostgresTestSchema`, `PostgresTestDatabase`, `PostgresServerFixture`, `PodmanPostgresContainer`, `PostgresSessionRepositoryContractTests`, `PostgresSessionStoreTests`, `PostgresTwoProcessAppendTests`, `PostgresServiceRegistrationTests`, `PostgresSessionLockTests`, `PostgresSessionServiceCompletionTests`, `Cloud/PostgresCloudAgentStoreTests`.
