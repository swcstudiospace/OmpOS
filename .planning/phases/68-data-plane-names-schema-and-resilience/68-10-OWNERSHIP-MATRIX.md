# 68-10 Ownership Matrix: all-writer fence for account erasure (SEC-77-07)

Evidence: every RESOLVED row cites a `file:line` read or grepped in this session. Paths are repo-relative. The brief names `services/Autonomogrammer.Api`; the directory on disk is `services/Autonogrammer.Api`, and that is what is cited here.

## Legend

- **Status.** RESOLVED: writer, lock, and caller are evidenced. UNRESOLVED: listed in section F with the exact next read or grep.
- **Owner-fence-mode.**
  - `ADMISSION`: the write is reached from an API request. The only admission gate is the global `Accounts:Enabled` switch (`services/Autonogrammer.Api/Program.cs:233`, `services/Autonogrammer.Api/Accounts/AccountDisabledMiddleware.cs:27`). No per-account disabled state exists: `accounts.status` (`src/Autonogrammer.Sessions.Postgres/Migrations/M0004_Accounts.cs:11`) has no writer, confirmed by a repo-wide grep for `accounts SET|deleted_at|'disabled'|accounts.status`. ADMISSION therefore refuses no individual account today.
  - `DRAIN`: the write comes from a background loop, the dispatch WebSocket hub, or an engine-side run. `PostgresAccountErasure.cs:21` requires callers to quiesce these before erasure.
  - `N/A-with-reason`: no production caller, or no account-keyed data.
- **Lock today.** The lock and its line. "none" means a single autocommit statement, or a transaction with no lock beyond the row locks it names.

## Findings that block SEC-77-07

1. **The erasure is not wired.** `PostgresAccountErasure.EraseAsync` (`src/Autonogrammer.Sessions.Postgres/PostgresAccountErasure.cs:75`) has no caller in `src`, `services`, `apps` or `tests` (grep for `PostgresAccountErasure|EraseAsync\(|_erasure\.` returned only the class), and no DI registration (grep `PostgresAccountErasure\b.*(Add|Try|new )` returned nothing).
2. **The production dispatch store is not wired.** `PostgresDispatchStore` (`src/Autonogrammer.Sessions.Postgres/PostgresDispatchStore.cs:19`) has no registration. `services/Autonogrammer.Api/Dispatch/DispatchModule.cs:40-45` registers the in-memory store only in Development. `DispatchOptionsValidator.cs:77-78` refuses the in-memory store outside Development. The Postgres class is constructed only in tests (`tests/Autonogrammer.Sessions.Postgres.Tests/Dispatch/PostgresDispatchStoreTests.cs:247,317`). So no production path writes `dispatch_*` rows, and the dispatch erasure has never run in a composed host.
3. **No per-account admission gate.** `AccountEnrollmentMiddleware.TouchAsync` upserts `accounts` on every authenticated request (`services/Autonogrammer.Api/Accounts/AccountEnrollmentMiddleware.cs:87-97`, after `Program.cs:232-234`), with no disabled check. The erasure deletes `accounts` last (`PostgresAccountErasure.cs:87`), so an in-flight request can recreate the row.
4. **Durable or semi-durable state outside the erased set.** Automations are file-backed and keyed by `Owner` (`services/Autonogrammer.Api/Automations/AutomationServiceRegistration.cs:38-41`; `src/Autonogrammer.Harness.Automations/Hosted/UserAutomation.cs:13-14`). The custom-provider ledger is process memory (`services/Autonogrammer.Api/Services/Providers/CustomProviderLedger.cs:11`). Cloud secrets are process memory (`services/Autonogrammer.Api/Cloud/CloudServiceRegistration.cs:47-49`). Artifacts fall back to memory when the store is not Postgres (`services/Autonogrammer.Api/Cowork/Artifacts/ArtifactAccountDirectory.cs:15,285`). Artifact grants are an in-memory dictionary (`services/Autonogrammer.Api/Cowork/Artifacts/ArtifactGrants.cs:9`).
5. **Dragonfly keys without the tenant segment are outside the tenant scan.** Lease, idempotency and nonce keys are session-, hash- or nonce-keyed (`src/Autonogrammer.Coordination.Dragonfly/DragonflyKeyspace.cs:38-51,75-83`). The scan pattern matches only `*:<tenant>:*` (`DragonflyKeyspace.cs:89-92`), and the eraser documents this exclusion (`DragonflyTenantErasure.cs:7-9`). These keys expire within 24 hours (`DragonflyKeyspace.cs:115-123`).
6. **Writes with no account-level lock.** Session close/reopen (`PostgresSessionRepository.cs:556-577`); cloud agent delete (`PostgresCloudAgentStore.cs:298-315`); project archive, delete and instructions (`services/Autonogrammer.Api/Cowork/Projects/PostgresProjectAccountStore.cs:91-127`); memory delete and reset (`src/Autonogrammer.Cowork.Memory.Postgres/PostgresMemoryStore.cs:380-423`); artifact writes, which rely only on a per-request gate (`src/Autonogrammer.Cowork.Artifacts.Stores/PostgresArtifactStore.cs:19,43`).
7. **Reads that write.** Dispatch GET paths purge command bodies (`PostgresDispatchStore.cs:486-488`, `554-555`). Feed reads delete expired events (`PruneFeedAsync` at `PostgresDispatchStore.cs:813-841`, called from `ReadFeedCoreAsync` at `:629`).

---

## (A) Erased set

The erasure deletes these 17 tables (`src/Autonogrammer.Sessions.Postgres/PostgresAccountErasure.cs:28-54`, executed in the order at `:81-89`):

| # | table | erased by (file:line) | account key | status |
|---|---|---|---|---|
| A-01 | dispatch_commands | `PostgresAccountErasure.cs:28,94` | `account_id` | RESOLVED |
| A-02 | dispatch_events | `:29,96` | `account_id` | RESOLVED |
| A-03 | dispatch_devices | `:30,98` | `account_id` | RESOLVED |
| A-04 | cowork_events | `:32,104` | `account_id` | RESOLVED |
| A-05 | cowork_memory_idempotency | `:34,110` | `account_id` | RESOLVED |
| A-06 | cowork_memory_entries | `:35,112` | `account_id` | RESOLVED |
| A-07 | cowork_memory_settings | `:36,114` | `account_id` | RESOLVED |
| A-08 | cowork_instructions | `:37,116` | `account_id` | RESOLVED |
| A-09 | cowork_projects | `:38,118` | `account_id` | RESOLVED |
| A-10 | artifact_grants | `:40,124` | `account_id` | RESOLVED |
| A-11 | artifact_versions | `:41,126` | `account_id` | RESOLVED |
| A-12 | artifacts | `:42,128` | `account_id` | RESOLVED |
| A-13 | session_events | `:44-45,134` | `session_id IN (sessions.owner_id)` | RESOLVED |
| A-14 | session_tool_approvals | `:47-48,136` | `session_id IN (sessions.owner_id)` | RESOLVED |
| A-15 | sessions | `:50,138` | `owner_id` | RESOLVED |
| A-16 | cloud_agents | `:52,144` | `owner_id` | RESOLVED |
| A-17 | accounts | `:54,150` | `id` | RESOLVED |

Tables created by migrations, checked against the erased set. Grep `CREATE TABLE IF NOT EXISTS` over `src/Autonogrammer.Sessions.Postgres/Migrations` gave 20 tables: the 17 above plus the three below.

| # | table | status | reason |
|---|---|---|---|
| NE-01 | dispatch_audit | RESOLVED | Not erased by design: holds `account_id_hash` only (`PostgresAccountErasure.cs:14-15`; `M0005_Dispatch.cs:47-50`). |
| NE-02 | ledger_outbox | RESOLVED | No account column in the first 4 DDL lines (`M0007_LedgerOutbox.cs:11-14`). It joins to `dispatch_audit` via `audit_id` (`PostgresLedgerOutboxStore.cs:63-66`). Full DDL not re-read: see U-14. |
| NE-03 | schema_migrations | RESOLVED | Migration bookkeeping, not account data (`MigrationRunner.cs:57`). |

The only account-keyed tables not erased are dispatch_audit (hash only) and ledger_outbox (no account column). Foreign keys: `session_events` and `session_tool_approvals` reference `sessions` (`M0001_SessionStore.cs:17-18,26-27`), and the erasure deletes children before `sessions`. `artifact_versions` cascades from `artifacts` (`M0006_Cowork.cs:36-37`).

---

## (B) SQL writers

Columns: id | writer (type.member) | file:line | table | kind | lock today (evidence line) | owner-fence-mode | status.

| id | writer | file:line | table | kind | lock today | owner-fence-mode | status |
|---|---|---|---|---|---|---|---|
| B-01 | PostgresSessionRepository.SaveAsync → WriteSessionAsync → UpsertHeaderAsync | `src/Autonogrammer.Sessions.Postgres/PostgresSessionRepository.cs:53-57,168-201,670-673` | sessions | INSERT ON CONFLICT DO UPDATE | row lock implicit in upsert (`:53-57`). Callers under `SessionService` take stripe+advisory lock (`src/Autonogrammer.Sessions/SessionService.cs:685-695`) at `:162,297,357,599,653`. `SessionService.CreateAsync` `:123` has no EnterAsync (new id). `services/Autonogrammer.Api/Cloud/CloudAgentService.cs:140` (SaveSessionAsync `:429-443`) takes no lock. `services/Autonogrammer.Api/Cowork/CoworkSessionService.cs:79` takes only the per-owner gate (`:46-47`). | ADMISSION (Cowork PUT, cloud create, session create) and DRAIN (SessionService under lease) | RESOLVED |
| B-02 | PostgresSessionRepository.AppendEventAsync / InsertEventAsync | `PostgresSessionRepository.cs:45-47,205-209,227-268,753-756,771-774` | session_events | INSERT | `SELECT … FOR UPDATE` on header (`LockSessionRowSql` `:84`, `LockSessionRowAsync` `:771-774`) before `max(seq)+1` (`NextSeqSql` `:86-87`); also `SessionService.AppendAsync` `:729` under lease | DRAIN | RESOLVED |
| B-03 | PostgresSessionRepository.TryAddToolApprovalAsync | `PostgresSessionRepository.cs:89-92,322-358` (command `:341-354`); caller `SessionService.cs:321-357` | session_tool_approvals | INSERT ON CONFLICT DO NOTHING | session stripe + `SELECT pg_advisory_lock` on a dedicated unpooled connection (`PostgresSessionRepository.cs:269-283`) via `EnterAsync` (`SessionService.cs:332,685-695`) | DRAIN (engine request approval) | RESOLVED |
| B-04 | PostgresSessionRepository.TryDecideToolApprovalAsync → DecideToolApprovalAsync | `PostgresSessionRepository.cs:98-100,403-440` (command `:427-437`) | session_tool_approvals | UPDATE … WHERE decision IS NULL | session lock: `SessionService.ResolveToolApprovalAsync` `:397-422` under `EnterAsync` (`:402`); `DenyLapsedAsync` `:509-511` under `EnterAsync` | ADMISSION (`ToolApprovalEndpoints.cs:33`) and DRAIN (`DenyLapsedAsync`) | RESOLVED |
| B-05 | PostgresSessionRepository.SetClosedAsync → ChangeClosedAsync | `PostgresSessionRepository.cs:71,73,501-507,556-577` (command `:561`) | sessions (closed_at) | UPDATE conditional | none: single statement, no transaction, no session lock (`:561`). Callers `services/Autonogrammer.Api/Endpoints/CatalogEndpoints.cs:175` (close) and `:195` (reopen) and `services/Autonogrammer.Api/Cowork/CoworkSessionService.cs:104` (delete) take no EnterAsync. | ADMISSION | RESOLVED |
| B-06 | PostgresCloudAgentStore.CreateAsync → CreateCoreAsync | `src/Autonogrammer.Sessions.Postgres/PostgresCloudAgentStore.cs:36-38,65-125` (LockOwnerAsync `:82`; insert `:110-120`) | cloud_agents | INSERT | owner advisory xact lock: `LockOwnerAsync` `:357-363`, key `autonogrammer.cloud-agents.owner:<owner>` (`PostgresSql.AdvisoryKey`), taken first (`:82`), then count (`:99`) and insert in one transaction | ADMISSION (`services/Autonogrammer.Api/Cloud/CloudAgentEndpoints.cs:41`) | RESOLVED |
| B-07 | PostgresCloudAgentStore.UpdateAsync → UpdateCoreAsync → WriteAsync | `PostgresCloudAgentStore.cs:19,40-41,202-236` (WriteAsync `:231`) | cloud_agents | UPDATE | row lock `SELECT … FOR UPDATE` (`:19`, `BindForUpdate` `:217`). No owner lock in `UpdateCoreAsync` `:209-236`. Callers: `services/Autonogrammer.Api/Cloud/CloudAgentLifecycle.cs:85,150,165,215,256`; `CloudAgentRuntime.cs:142`. | DRAIN (sweeper, runtime) and ADMISSION (API via lifecycle) | RESOLVED |
| B-08 | PostgresCloudAgentStore.TryActivateAsync → TryActivateCoreAsync | `PostgresCloudAgentStore.cs:239-295` (owner `:253-259`; LockOwnerAsync `:266`; BindForUpdate `:267`; WriteAsync `:290`) | cloud_agents | UPDATE | owner advisory xact lock (`:266`) then row `FOR UPDATE` (`:267`). Callers `services/Autonogrammer.Api/Cloud/CloudAgentService.cs:218,335`. | ADMISSION | RESOLVED |
| B-09 | PostgresCloudAgentStore.DeleteAsync → DeleteCoreAsync | `PostgresCloudAgentStore.cs:43,298-315` (command `:311-314`) | cloud_agents | DELETE | none: `PostgresSql.Create(connection, null)` `:311`, no transaction, no LockOwnerAsync. Callers `services/Autonogrammer.Api/Cloud/CloudAgentService.cs:387` (DeleteAsync `:358`) and `services/Autonogrammer.Api/Cloud/CloudIdleSweeper.cs:176` (FinishDeletionAsync `:167`). Confirms item 5: **not** under the owner lock. | ADMISSION (API) and DRAIN (sweeper) | RESOLVED |
| B-10 | PostgresAccountStore.TouchAsync | `services/Autonogrammer.Api/Accounts/AccountService.cs:82-84,100-115` | accounts | INSERT ON CONFLICT DO UPDATE | none: single statement (`:103`) | ADMISSION (global switch only) | RESOLVED |
| B-11 | PostgresDispatchStore.LockAccountAsync (writes the row, then locks it) | `src/Autonogrammer.Sessions.Postgres/PostgresDispatchStore.cs:843-857` (INSERT `:847`; FOR UPDATE `:854`) | accounts | INSERT ON CONFLICT DO NOTHING + SELECT FOR UPDATE | row lock on accounts (`:854`). Callers `CreateDeviceCoreAsync` `:209`, `AppendFeedCoreAsync` `:603`. | ADMISSION | RESOLVED |
| B-12 | PostgresDispatchStore.IncrementFeedAsync | `PostgresDispatchStore.cs:893-899` (UPDATE `:896`) | accounts (feed_seq) | UPDATE | accounts row lock from B-11 (`:603`); caller `AppendFeedCoreAsync` `:605` | ADMISSION | RESOLVED |
| B-13 | PostgresDispatchStore.CreateDeviceCoreAsync → InsertDeviceAsync | `PostgresDispatchStore.cs:200-224,667-684` (INSERT `:670-672`); public `:65` | dispatch_devices | INSERT ON CONFLICT DO NOTHING | accounts row lock (`:209`) and device count (`:210`) in one transaction. Caller `services/Autonogrammer.Api/Dispatch/Devices/DeviceEndpoints.cs:119`. | ADMISSION | RESOLVED |
| B-14 | PostgresDispatchStore.RenameCoreAsync | `PostgresDispatchStore.cs:87-94,265-281` (UPDATE `:274`) | dispatch_devices | UPDATE | none explicit: single UPDATE in a transaction. Caller `DeviceEndpoints.cs:497`. | ADMISSION | RESOLVED |
| B-15 | PostgresDispatchStore.RevokeCoreAsync → LockOwnedDeviceAsync, CancelOpenCommandsAsync, ApplyTransitionAsync | `PostgresDispatchStore.cs:282-306` (lock `:291`; cascade `:296`; UPDATE `:298`), `:308-315` (FOR UPDATE `:311`), `:317-346` (FOR UPDATE on commands `:322`), `:794-811` | dispatch_devices, dispatch_commands | UPDATE, SELECT FOR UPDATE | device row `FOR UPDATE` (`:311`) held across the cascade; command row locks (`:322`, `:770`). Caller `DeviceEndpoints.cs:542` (RevokeOwnedAsync `:540`). | ADMISSION | RESOLVED |
| B-16 | PostgresDispatchStore.CredentialCoreAsync | `PostgresDispatchStore.cs:348-365` (UPDATE `:357`) | dispatch_devices | UPDATE | none explicit | ADMISSION (device credential) | RESOLVED |
| B-17 | PostgresDispatchStore.CapabilityCoreAsync | `PostgresDispatchStore.cs:367-383` (UPDATE `:376`) | dispatch_devices | UPDATE | none explicit. Caller `services/Autonogrammer.Api/Dispatch/DispatchHub.cs:315` (WS hello). | ADMISSION (device credential at WS hello) | RESOLVED |
| B-18 | PostgresDispatchStore.TouchCoreAsync | `PostgresDispatchStore.cs:385-401` (UPDATE `:394`) | dispatch_devices | UPDATE | none explicit | N/A-with-reason: no production caller of `TouchLastSeenAsync` (grep `\.TouchLastSeenAsync\(` over src and services returned none) | RESOLVED |
| B-19 | PostgresDispatchStore.PairedCoreAsync | `PostgresDispatchStore.cs:403-419` (UPDATE `:412`) | dispatch_devices | UPDATE | none explicit | N/A-with-reason: no production caller of `UpdatePairedSessionAsync` (grep over src and services returned none) | RESOLVED |
| B-20 | PostgresDispatchStore.EnqueueCoreAsync → LockOwnedDeviceAsync, LockLiveDeviceAsync, AllocateSeqAsync, InsertCommandAsync | `PostgresDispatchStore.cs:421-457` (locks `:429,439`), `:308-315` (FOR UPDATE `:311`), `:686-694` (FOR UPDATE `:689`), `:736-743` (UPDATE `:739`), `:745-765` (INSERT `:750-751`) | dispatch_devices (command_seq), dispatch_commands | INSERT, UPDATE | device row `FOR UPDATE` taken twice in one transaction (`:311`, `:689`) and held to commit | ADMISSION | RESOLVED |
| B-21 | PostgresDispatchStore.TransitionCoreAsync → ApplyTransitionAsync | `PostgresDispatchStore.cs:566-592` (`:580-589`), `:794-811` (UPDATE `:803`), row lock in `ReadCommandAsync` `:767-778` (`:770`) | dispatch_commands | UPDATE | command row `FOR UPDATE` (`:770`) in one transaction. Callers `services/Autonogrammer.Api/Dispatch/DispatchDeliveryLoop.cs:52` (DRAIN), `DispatchHub.cs:345` (DRAIN), `services/Autonogrammer.Api/Dispatch/Commands/CommandEndpoints.cs:349` (ADMISSION). | DRAIN and ADMISSION | RESOLVED |
| B-22 | PostgresDispatchStore.PurgeBodyAsync (inside read paths) | `PostgresDispatchStore.cs:780-792` (UPDATE `:784`); called from `GetCommandCoreAsync` `:486-488` and `ListCommandsCoreAsync` `:554-555` | dispatch_commands (body NULL) | UPDATE | row lock from `ReadCommandAsync` `:770` (`GetCommandCore`) and `FOR UPDATE` on the list `:537` | ADMISSION (a GET writes) | RESOLVED |
| B-23 | PostgresDispatchStore.PruneFeedAsync | `PostgresDispatchStore.cs:813-841` (DELETE `:837`); called from `AppendFeedCoreAsync` `:604` and `ReadFeedCoreAsync` `:629` | dispatch_events | DELETE | accounts row lock: `LockAccountAsync` `:603` on append, `LockFeedAsync` `:628` on read (`:885-891`, FOR UPDATE `:888`). Read caller `services/Autonogrammer.Api/Dispatch/Events/DispatchFeedStreamer.cs:56`. | ADMISSION (GET feed writes) | RESOLVED |
| B-24 | PostgresDispatchStore.InsertFeedAsync | `PostgresDispatchStore.cs:901-911` (INSERT `:904`), called from `AppendFeedCoreAsync` `:606` | dispatch_events | INSERT | accounts row lock (`:603`) | N/A-with-reason: the only caller of `AppendFeedEventAsync` is absent (grep `\.AppendFeedEventAsync\(` over src and services returned none) | RESOLVED |
| B-25 | PostgresDispatchStore.AuditAsync | `PostgresDispatchStore.cs:950-956` (INSERT `:953`, hash `:954`); called from every dispatch writer (e.g. `:219,452,582,607,646,790,810`) | dispatch_audit | INSERT | inherits the caller's transaction; no lock of its own | DRAIN and ADMISSION (`dispatch_audit` is not erased by design, NE-01) | RESOLVED |
| B-26 | PostgresMemoryStore.SetSettingsAsync | `src/Autonogrammer.Cowork.Memory.Postgres/PostgresMemoryStore.cs:69-92` (lock `:77`; INSERT ON CONFLICT `:80-83`) | cowork_memory_settings | INSERT ON CONFLICT DO UPDATE | `pg_advisory_xact_lock(hashtextextended('cowork-memory:' || account))` via `LockAccountAsync` `:345-351` (`:347`). Callers `services/Autonogrammer.Api/Cowork/Memory/MemoryEndpoints.cs:249`; `src/Autonogrammer.Cowork.Memory/MemoryToolProvider.cs:157,161` (agent turn). | ADMISSION (API) and DRAIN (agent) | RESOLVED |
| B-27 | PostgresMemoryStore.InsertClaimedEntryAsync: entry insert | `PostgresMemoryStore.cs:262-304` (INSERT `:268-271`); reached from `SaveIdempotentInTransactionAsync` `:136-182` (lock `:140`; entry call site `:164-181`, see U-07) | cowork_memory_entries | INSERT | advisory xact lock `:140,347` | ADMISSION | RESOLVED |
| B-28 | PostgresMemoryStore.InsertClaimedEntryAsync: claim insert | `PostgresMemoryStore.cs:286-291` (INSERT `:288`) | cowork_memory_idempotency | INSERT SELECT | advisory xact lock `:140,347` | ADMISSION | RESOLVED |
| B-29 | PostgresMemoryStore.PruneExpiredClaimsAsync | `PostgresMemoryStore.cs:195-215` (DELETE `:200-210`, `:221-222`) | cowork_memory_idempotency | DELETE | advisory xact lock `:140,347` (called inside the locked transaction at `:151`) | ADMISSION | RESOLVED |
| B-30 | PostgresMemoryStore.SaveAsync → InsertAsync → CheckWriteScopeAsync | `PostgresMemoryStore.cs:95-107,475-527` (INSERT `:491-493`; lock path `:483`), `:529-556` (`LockAccountAsync` `:535`; ownership FOR KEY SHARE `:548-549`) | cowork_memory_entries | INSERT | advisory xact lock `:535,347`. Callers `services/Autonogrammer.Api/Cowork/Memory/MemoryEndpoints.cs:94`; `MemoryToolProvider.cs:164`. | ADMISSION and DRAIN | RESOLVED |
| B-31 | PostgresMemoryStore.DeleteAsync | `PostgresMemoryStore.cs:380-410` (DELETE `:391-398`; command from `_dataSource` `:389`) | cowork_memory_entries | DELETE | none: no transaction, no lock; settings read unlocked (`:384`) | ADMISSION and DRAIN (`MemoryToolProvider.cs:176`) | RESOLVED |
| B-32 | PostgresMemoryStore.ResetAsync | `PostgresMemoryStore.cs:413-423` (DELETE `:416`) | cowork_memory_entries | DELETE all for account | none: no transaction, no lock. Caller `MemoryEndpoints.cs:203`. Leaves settings and claim rows (observation). | ADMISSION | RESOLVED |
| B-33 | PostgresArtifactStore.MutateAsync → InsertRowsAsync (artifact header) | `src/Autonogrammer.Cowork.Artifacts.Stores/PostgresArtifactStore.cs:39-76` (BeginTransaction `:49`; replay `:54-57`; commit `:59`), `:341-357` (INSERT `:343`), `:412-422` (`Insert`) | artifacts | INSERT | per-instance `SemaphoreSlim _gate` (`:19,43`). `ArtifactAccountDirectory.Open` makes a new store per call (`services/Autonogrammer.Api/Cowork/Artifacts/ArtifactAccountDirectory.cs:283`, used at `:142,145,155`), so the gate does not serialize requests. No row or advisory lock. Caller `ArtifactAccountDirectory.PutAsync` `:137-157` → `Put` `:195-232`, from `services/Autonogrammer.Api/Cowork/Artifacts/ArtifactEndpoints.cs:42`. | ADMISSION | RESOLVED |
| B-34 | PostgresArtifactStore: InsertVersionAsync (artifact_versions) | `PostgresArtifactStore.cs:359-377` (INSERT `:361`); reached via `InsertRowsAsync` `:356` and `Append` `:438-440` | artifact_versions | INSERT | same as B-33 | ADMISSION | RESOLVED |
| B-35 | PostgresArtifactStore.Append (latest_number advance) | `PostgresArtifactStore.cs:424-451` (UPDATE `:441-445`; integrity throw `:446-448`) | artifacts | UPDATE | same as B-33. Parent check `:428-432` uses the snapshot loaded inside the same transaction, not a row lock. Observation: a concurrent append can pass the check. | ADMISSION | RESOLVED |
| B-36 | PostgresArtifactStore.Delete (artifact row) | `PostgresArtifactStore.cs:453-471` (DELETE `:466`); API route `ArtifactEndpoints.cs:244` → `ArtifactAccountDirectory.DeleteAsync` `:164-192` | artifacts (versions cascade, `M0006_Cowork.cs:36-37`) | DELETE | same as B-33 | ADMISSION | RESOLVED (call edge from `:164-192` to `Delete`: see U-03) |
| B-37 | PostgresArtifactStore grant insert | `PostgresArtifactStore.cs:118-127` (INSERT `:123`) | artifact_grants | INSERT | same as B-33 | ADMISSION | UNRESOLVED (enclosing method and live caller: U-02). Grants on the API route are in memory (`ArtifactGrants.cs:9`). |
| B-38 | ArtifactAudit.WriteAsync | `services/Autonogrammer.Api/Cowork/Artifacts/ArtifactAudit.cs:40-86` (INSERT `:67`, guarded by `_dataSource` `:60`); data source only when `Sessions:Postgres:ConnectionString` is set (`ArtifactsServiceRegistration.cs:30-34`) | cowork_events | INSERT autocommit | none | ADMISSION | RESOLVED (callers: U-06) |
| B-39 | PostgresProjectAccountStore.UpsertAsync | `services/Autonogrammer.Api/Cowork/Projects/PostgresProjectAccountStore.cs:38-89` (advisory lock `:44-47`; INSERT ON CONFLICT `:59-65`, same-account guard `:65`) | cowork_projects | INSERT ON CONFLICT DO UPDATE | `pg_advisory_xact_lock(hashtextextended(@account, 0))` `:44`. Key namespace differs from memory's `'cowork-memory:'` key, so the two do not serialize (observation). Caller `services/Autonogrammer.Api/Cowork/Projects/ProjectsEndpoints.cs:111`. | ADMISSION | RESOLVED |
| B-40 | PostgresProjectAccountStore.ArchiveAsync | `PostgresProjectAccountStore.cs:91-101` (UPDATE `:93`) | cowork_projects | UPDATE | none (`dataSource.CreateCommand`, no transaction). Caller `ProjectsEndpoints.cs:153`. | ADMISSION | RESOLVED |
| B-41 | PostgresProjectAccountStore.DeleteAsync | `PostgresProjectAccountStore.cs:103-110` (DELETE `:105`) | cowork_projects | DELETE | none. Caller `ProjectsEndpoints.cs:159`. Deleting a project cascades to its memory entries (`M0009_CoworkProjectsMemory.cs:32`, `ON DELETE CASCADE`). | ADMISSION | RESOLVED |
| B-42 | PostgresProjectAccountStore.SetInstructionsAsync | `PostgresProjectAccountStore.cs:120-127` (INSERT ON CONFLICT `:122`) | cowork_instructions | UPSERT | none. Caller `ProjectsEndpoints.cs:195`. | ADMISSION | RESOLVED |
| B-43 | PostgresLedgerOutboxStore.EnqueueAsync (CTE) | `services/Autonogrammer.Api/Audit/PostgresLedgerOutboxStore.cs:30-` (CTE `:131-138`) | dispatch_audit (`:132`), ledger_outbox (`:136`) | INSERT (CTE) | caller-supplied transaction (`:30`); no lock of its own. Entry from `services/Autonogrammer.Api/Audit/SubstrateAuditLedger.cs:47-48`. | DRAIN and ADMISSION (mixed) | RESOLVED (callers: U-05) |
| B-44 | PostgresLedgerOutboxStore.MarkSentSqlAsync | `PostgresLedgerOutboxStore.cs:190` (UPDATE `:190`); called from `MarkSentAsync` ← `services/Autonogrammer.Api/Audit/LedgerOutboxPump.cs:283` | ledger_outbox | UPDATE | none | DRAIN | RESOLVED |
| B-45 | PostgresLedgerOutboxStore.ScheduleAsync | `PostgresLedgerOutboxStore.cs:205` (UPDATE `:205`); called from `LedgerOutboxPump.cs:307` | ledger_outbox | UPDATE | none | DRAIN | RESOLVED |
| B-46 | MigrationRunner (apply/revert) | `src/Autonogrammer.Sessions.Postgres/Migrations/MigrationRunner.cs:57,329-332,363-364` | schema_migrations (and DDL in M0001–M0011) | INSERT / DELETE / DDL | `pg_advisory_xact_lock` `:40` | N/A-with-reason: schema, not account data | RESOLVED |

Notes for section B:
- `ClaimDueAsync` (`PostgresLedgerOutboxStore.cs:55-101`) is a SELECT only. It is not a writer.
- `ReplayCoreAsync` and `ReadIdempotentAsync` (`PostgresDispatchStore.cs:459-470`, `696-724`) are reads.
- Every writer above that is not in the `dispatch_audit` or `ledger_outbox` family is reached only through the composition in section E. The Postgres dispatch writers (B-11 to B-25) are not reachable in a composed production host (Finding 2).

---

## (C) Dragonfly key families

Keyspace definitions live in `src/Autonogrammer.Coordination.Dragonfly/DragonflyKeyspace.cs`. Every write path asks `RequireTimeToLive` for a lifetime of at most 24 hours (`:111-123`).

| id | key pattern | file:line | writer | tenant segment | erasure (`DeleteTenantKeysAsync`) | status |
|---|---|---|---|---|---|---|
| C-01 | `autonogrammer:v1:lease:run:<sessionId>` | keyspace `:38-41`; writers `DragonflySessionRunLeases.cs:34-45` (SET NX `:44`), `DragonflySessionRunLease.cs:41-61` (renew `:44-47`, delete `:59-61`) | `DragonflySessionRunLeases.TryAcquireAsync`, `DragonflySessionRunLease` | no (session id) | not reached (`DragonflyTenantErasure.cs:7-9`) | RESOLVED |
| C-02 | `autonogrammer:v1:idem:<sha256(scope + "\n" + key)>` | keyspace `:47-51`; writers `DragonflyIdempotencyStore.cs:41` (claim SET NX `:48`), `:81` (complete script `:90`), `:105` (release script `:109`) | `DragonflyIdempotencyStore.ClaimAsync`, `CompleteAsync`, `ReleaseAsync`. Caller `services/Autonogrammer.Api/Endpoints/SessionEndpoints.cs:258-270` (scope = session id) | no (hashed) | not reached | RESOLVED |
| C-03 | `autonogrammer:v1:presence:<tenantHash>:<deviceId>` | keyspace `:57-60`; key built at `DragonflyDispatchPresence.cs:97` from `HashTenant(accountId)`; writers `:31-42` (SET `:40`), `:45-63` (renew `:55`), `:66-71` (delete `:70`) | `DragonflyDispatchPresence.SetAsync`, `RefreshAsync`, `RemoveAsync` | yes | reached by `autonogrammer:v1:*:<tenant>:*` (`keyspace:89-92`; `DragonflyTenantErasure.cs:36,51`) | RESOLVED |
| C-04 | `autonogrammer:v1:rl:<policy>:<tenantHash>:<window>` | keyspace `:67-70` | none found: grep `DragonflyKeyspace\.RateLimit\(` over src and services returned no call site | yes | would be reached if written | RESOLVED (no writer exists) |
| C-05 | `autonogrammer:v1:auth:nonce:<sha256Hex>` | keyspace `:75-84`; writers `DragonflyDispatchAuthNonces.cs:32-39` (claim SET NX `:39`), `:48-51` (consume, GETDEL per comment `:50`). Caller `services/Autonogrammer.Api/Dispatch/DispatchHub.cs:165` | `DragonflyDispatchAuthNonces.TryClaimAsync`, `TryConsumeAsync` | no (hash of nonce) | not reached | RESOLVED |
| C-06 | pub/sub channel `autonogrammer:v1:sig:session` | keyspace `:29-30`; publisher `DragonflySessionSignals.cs:62-68` (`:68`) | `DragonflySessionSignals.PublishAsync` | n/a (channel, not key) | n/a | RESOLVED |
| C-07 | pub/sub channel `autonogrammer:v1:sig:dispatch` | keyspace `:32-33`; publisher `DragonflyDispatchSignals.cs:54-62` (`:59`) | `DragonflyDispatchSignals.PublishAsync` | n/a | n/a | RESOLVED |

---

## (D) Background producers

| id | producer | file:line | writes | lock today | fence | status |
|---|---|---|---|---|---|---|
| D-01 | SessionRunReaper | `services/Autonogrammer.Api/Workflows/SessionRunReaper.cs:80-83` (runs only with Dragonfly), `:100,128`, `:136` | `RecordCompletionAsync` → sessions and session_events via SessionService | Dragonfly lease check, then `SessionService.EnterAsync` (`SessionService.cs:268`) → advisory lock | DRAIN | RESOLVED |
| D-02 | DispatchDeliveryLoop | `services/Autonogrammer.Api/Dispatch/DispatchDeliveryLoop.cs:21,41,52` | `TryTransitionAsync` Queued→Delivered (dispatch_commands) | command row FOR UPDATE (`PostgresDispatchStore.cs:770`) | DRAIN | RESOLVED |
| D-03 | LedgerOutboxPump | `services/Autonogrammer.Api/Audit/LedgerOutboxPump.cs:270,283,307` | `MarkSentAsync` (`:283`), `ScheduleAsync` (`:307`) on ledger_outbox; `ClaimDueAsync` (`:270`) is read-only | none | DRAIN | RESOLVED |
| D-04 | CloudIdleSweeper | `services/Autonogrammer.Api/Cloud/CloudIdleSweeper.cs:67-103` (FailAsync `:78`; GoIdleAsync `:138,151`; FinishDeletionAsync `:167-176`: RemoveWorkerAsync `:171`, `_store.DeleteAsync` `:176`) | cloud_agents UPDATE and DELETE | row lock only (B-07); no owner lock on delete (B-09) | DRAIN | RESOLVED |
| D-05 | CloudAgentLifecycle | `services/Autonogrammer.Api/Cloud/CloudAgentLifecycle.cs:85,150,165,215,256` | `UpdateAsync` (cloud_agents) | row lock only (B-07) | DRAIN | RESOLVED |
| D-06 | CloudAgentRuntime | `services/Autonogrammer.Api/Cloud/CloudAgentRuntime.cs:128-145` (`RunEndedAsync` `:128-131` → `TouchAsync`; `RecordNativeSessionAsync` `:138-145`) | `UpdateAsync` (cloud_agents) | row lock only (B-07) | DRAIN | RESOLVED |
| D-07 | AutomationScheduler | `src/Autonogrammer.Harness.Automations/Hosted/AutomationScheduler.cs:49-65` (cursor `:53,64`), `:166` (missed run upsert) | file store cursor and run history (`FileAutomationStore`); starts runs via D-08 | in-process `SemaphoreSlim _gate` (`FileAutomationStore.cs:18,94,108`) | DRAIN | RESOLVED |
| D-08 | AutomationRunner | `src/Autonogrammer.Harness.Automations/Hosted/AutomationRunner.cs:102-118,147-157,259-260,287,362`; cloud target `services/Autonogrammer.Api/Automations/CloudAutomationTarget.cs:45` → `CloudServiceAgentLauncher.cs:48,61` → `CloudAgentService.CreateAsync` (`CloudAgentService.cs:103,140,142`) and `PromptAsync` (`:199,218,335`) | file store run history; cloud_agents and sessions rows via the cloud service | file gate (D-07); cloud locks per B-06/B-08 | DRAIN | RESOLVED |
| D-09 | CoworkSessionSync (client) | `src/Autonogrammer.Cowork/Sync/CoworkSessionSync.cs:179-181`; interface `ICoworkSessionApi.cs:14,20`. Writes arrive at `services/Autonogrammer.Api/Cowork/CoworkSessionEndpoints.cs:35,48` → `CoworkSessionService.PutAsync` `:37-87` (B-01) and `DeleteAsync` `:94-107` (B-05) | sessions (cowork surface) over HTTP | per-owner gate `CoworkSessionService.cs:46-47` only | DRAIN (client sync) | RESOLVED |
| D-10 | DispatchHub (device WebSocket) | `services/Autonogrammer.Api/Dispatch/DispatchHub.cs:165` (nonce), `:315` (B-17), `:345` (B-21), presence (C-03) | dispatch_devices, dispatch_commands, Dragonfly keys | row locks as in B-17, B-21 | DRAIN | RESOLVED |
| D-11 | SessionLifecycleSweeper (native client) | `apps/Autonogrammer.Native/Settings/SessionLifecycleSweeper.cs:100-124` (`registry.DeleteStateDirectory` `:124`) | local engine state directories on the user's machine | none (client) | N/A-with-reason: outside the API account boundary | RESOLVED |

Notes: `apps/Autonogrammer.Native/Logging/CleanupFailureReporter.cs` only logs. A grep for `INSERT INTO|DELETE FROM|UPDATE\s+\w+\s+SET|CREATE TABLE|CommandText|NpgsqlCommand` over `apps/` returned no matches, so no native client writes SQL.

---

## (E) API entry points, grouped by endpoint file

Mutating routes only (grep `\.Map(Post|Put|Delete|Patch)\("…"`). Each group lists the writers it reaches by row id.

1. `services/Autonogrammer.Api/Endpoints/CatalogEndpoints.cs`: `POST /sessions/{id}/close` (`:36`, B-05 at `:175`); `POST /sessions/{id}/reopen` (`:40`, B-05 at `:195`); `POST /sessions/{id}/resume` (`:44`, UNRESOLVED body: U-01).
2. `services/Autonogrammer.Api/Endpoints/ChangeReviewEndpoints.cs`: `POST /sessions/{id}/changes/{path}/accept` (`:30`), `…/reject` (`:40`), `…/changes/{**rest}` (`:53`). The writer is `ChangeDecisionStore` (UNRESOLVED: U-04).
3. `services/Autonogrammer.Api/Endpoints/ProvidersEndpoints.cs`: `PUT /providers/{id}/credential` (`:44`, `backend.ImportCredentialAsync` `:176`), `PUT /models/default` (`:56`, `:243`), `POST /providers/custom` (`:66`, ledger `:279`, `UpsertBlockAsync` `:284`), `DELETE /providers/custom/{id}` (`:77`, ledger `:315`, `RemoveBlockAsync` `:320`), `POST /providers/{id}/test` (`:84`, probe only). All writes go through `IProviderBackend`, implemented by `services/Autonogrammer.Api/Services/Providers/OmpProviderBackend.cs:15`, which writes omp configuration on disk. That is host-wide and not account-keyed, so it is outside the SQL matrix. The custom-provider ledger is in memory (`CustomProviderLedger.cs:11`).
4. `services/Autonogrammer.Api/Endpoints/RunEndpoints.cs`: `POST /sessions/{id}/runs` (`:39`), `POST /sessions/{id}/runs/{runId}/cancel` (`:52`). Reaches SessionService; body not re-read (U-01 scope).
5. `services/Autonogrammer.Api/Endpoints/SessionEndpoints.cs`: `POST /sessions` (`:42`, B-01 via `SessionService.CreateAsync` `:107-125`), `POST /sessions/{id}/prompts` (`:51`, `AdmitIdempotentAsync` `:258-270` → C-02; `SubmitPromptAsync` B-01).
6. `services/Autonogrammer.Api/Endpoints/SteeringEndpoints.cs`: `POST /sessions/{id}/steer` (`:34`, `SubmitSteerAsync`/`SubmitFollowUpAsync` `SessionService.cs:223,237`, lock at `:634`).
7. `services/Autonogrammer.Api/Endpoints/SessionConfigEndpoints.cs`: `PUT /sessions/{id}/config-options/{optionId}` (`:33`; handler body UNRESOLVED: U-12).
8. `services/Autonogrammer.Api/Endpoints/ToolApprovalEndpoints.cs`: `POST /sessions/{id}/tool-approvals/{requestId}` (`:33`, `ResolveToolApprovalAsync` `SessionService.cs:397` → B-04).
9. `services/Autonogrammer.Api/Cowork/CoworkSessionEndpoints.cs`: `PUT /cowork/sessions/{id}` (`:35`, B-01 via `CoworkSessionService.PutAsync`), `DELETE /cowork/sessions/{id}` (`:48`, B-05 via `CoworkSessionService.DeleteAsync` `:94-107`).
10. `services/Autonogrammer.Api/Cowork/Projects/ProjectsEndpoints.cs`: `PUT /cowork/projects/{id}` (`:24`, B-39), `POST /cowork/projects/{id}/archive` (`:25`, B-40), `DELETE /cowork/projects/{id}` (`:26`, B-41), `PUT /cowork/instructions/global` (`:28`, B-42).
11. `services/Autonogrammer.Api/Cowork/Memory/MemoryEndpoints.cs`: `POST /cowork/memory` (`:21`, B-30 via `SaveAsync` `:94`, or B-27/B-28/B-29 via `SaveIdempotentAsync` `:107`), `POST /cowork/memory/reset` (`:24`, B-32), `PUT /cowork/memory/settings` (`:27`, B-26), `DELETE /cowork/memory/{id}` (`:29`, B-31). `POST /cowork/memory/recall` (`:23`) is a read.
12. `services/Autonogrammer.Api/Cowork/Artifacts/ArtifactEndpoints.cs`: `PUT /cowork/artifacts/{id}/versions/{n}` (`:42`, B-33/B-34/B-35), `DELETE /cowork/artifacts/{id}` (`:59`, B-36), `POST …/render-tickets` (`:67`, in-memory `ArtifactRenderTickets`), `PUT …/grants` (`:83`, B-37 UNRESOLVED; grants in memory `ArtifactGrants.cs:9`).
13. `services/Autonogrammer.Api/Cloud/CloudAgentEndpoints.cs`: `POST /cloud/agents` (`:41`, B-06), `POST /cloud/agents/{id}/prompts` (`:62`, B-08), `POST …/cancel` (`:80`, in-process run cancel; U-13), `POST …/stop` (`:86`, B-07 via lifecycle), `POST …/wake` (`:92`, B-08), `DELETE /cloud/agents/{id}` (`:100`, B-09), `POST …/pull-request` (`:113`, external), `PUT /cloud/secrets/{name}` (`:138`, in-memory `InMemoryCloudSecretStore`), `DELETE /cloud/secrets/{name}` (`:147`, `:380`, same).
14. `services/Autonogrammer.Api/Automations/AutomationEndpoints.cs`: `POST /automations` (`:44`), `PUT /automations/{id}` (`:74`), `DELETE /automations/{id}` (`:82`; `AutomationService.DeleteAsync` `:189-190`), `PUT /automations/{id}/enabled` (`:88`), `POST /automations/{id}/webhook-secret` (`:95`), `POST /automations/{id}/runs` (`:102`), `POST /hooks/automations/{id}` (`:119`, anonymous HMAC). All write the file store (D-07/D-08) or the file secret store (`FileAutomationSecretStore.cs:82-83`). Runs can launch cloud agents (B-06, B-08).
15. `services/Autonogrammer.Api/Dispatch/Commands/CommandEndpoints.cs`: `POST /dispatch/devices/{id}/commands` (`:34`, B-20 via `EnqueueAsync` `:109`), `POST /dispatch/commands/{id}/cancel` (`:57`, B-21 via `TryTransitionAsync` `:349`).
16. `services/Autonogrammer.Api/Dispatch/Devices/DeviceEndpoints.cs`: `POST /dispatch/devices` (`:33`, B-13 via `CreateDeviceAsync` `:119`), `PATCH /dispatch/devices/{id}` (`:62`, B-14 via `:497`), `DELETE /dispatch/devices/{id}` (`:70`, B-15 via `RevokeOwnedAsync` `:540-542`), `DELETE /dispatch/devices` (`:76`, `RevokeAllDevicesAsync` `:522`, B-15 per device).

Files with **no** mutating route (grep over the directories returned no `Map(Post|Put|Delete|Patch)` lines): `services/Autonogrammer.Api/Accounts/` (account writes are middleware, B-10), `services/Autonogrammer.Api/Dispatch/Events/DispatchEventEndpoints.cs` (GET), `services/Autonogrammer.Api/DataPlane/`, `services/Autonogrammer.Api/Endpoints/` `StageEndpoints.cs`, `SourceEndpoints.cs`, `WorkspaceSettingsEndpoints.cs`, `AuthConfigEndpoints.cs`, `EventStreamEndpoints.cs`, `CatalogEventEndpoints.cs`, and `services/Autonogrammer.Api/Cloud/Workers/` and `CloudEndpointsMapped.cs`.

---

## (F) UNRESOLVED list

| id | what is unproven | exact next read or grep |
|---|---|---|
| U-01 | `CatalogEndpoints.ResumeAsync` body (which writes it makes, via which SessionService method) | `read services/Autonogrammer.Api/Endpoints/CatalogEndpoints.cs:200-271` |
| U-02 | Enclosing method of the grant INSERT (`PostgresArtifactStore.cs:123`) and whether the live grant endpoint reaches it or the in-memory `ArtifactGrants` (`ArtifactGrants.cs:9`) | `read src/Autonogrammer.Cowork.Artifacts.Stores/PostgresArtifactStore.cs:100-130`; then `grep -n "Grant" services/Autonogrammer.Api/Cowork/Artifacts/ArtifactEndpoints.cs services/Autonogrammer.Api/Cowork/Artifacts/ArtifactAccountDirectory.cs` |
| U-03 | Call edge from `ArtifactAccountDirectory.DeleteAsync` (`:164-192`) to `IArtifactMutation.Delete` | `read services/Autonogrammer.Api/Cowork/Artifacts/ArtifactAccountDirectory.cs:164-194` |
| U-04 | Location of `ChangeDecisionStore` and `WorkspaceLocator` (the accept/reject writers); not under `src/` or `services/` | `grep -rln "class ChangeDecisionStore\|class WorkspaceLocator" /root/src/repos/autonogrammer --include=*.cs` |
| U-05 | Callers of `SubstrateAuditLedger` (the origin of `ledger_outbox` and `dispatch_audit` enqueues, B-43) and whether each carries an account | `grep -rn "IAuditLedger\|SubstrateAuditLedger" services src --include=*.cs` then read each call site |
| U-06 | Callers of `ArtifactAudit.WriteAsync` (B-38) | `grep -rn "\.WriteAsync(\"artifact\." services/Autonogrammer.Api --include=*.cs` |
| U-07 | Call site of `InsertClaimedEntryAsync` inside `SaveIdempotentInTransactionAsync` (`PostgresMemoryStore.cs:164-181`) | `read src/Autonogrammer.Cowork.Memory.Postgres/PostgresMemoryStore.cs:160-183` |
| U-08 | Names of the ledger enqueue helper around `:131-138` and of the methods at `:185-210` (writes at `:190`, `:205` are RESOLVED; names are not) | `read services/Autonogrammer.Api/Audit/PostgresLedgerOutboxStore.cs:28-52,110-150,183-212` |
| U-09 | Which process hosts `CoworkProjectStore` and the other file writers in `src/` (`CoworkJsonFile.cs:107-108`, `CoworkOwnerFiles.cs:70`, `FileArtifactStore.cs:314-356`, `DispatchStateFile.cs:37`, `FileUsageLedger.cs:127`, `FileSteeringChannel.cs:282`, `DispatchPolicyStore.cs:40`, `DispatchPairing.cs:87`). These are not enumerated; the grep returned 54 files | `grep -rln "File\.\(Write\|Move\|Delete\)\|AtomicFile\|WriteAtomic\|WriteAllBytes" src services --include=*.cs`, then page with `skip=20`; for each, `grep -rn "<ClassName>" services/Autonogrammer.Api --include=*.cs` to decide host |
| U-10 | File writers in `apps/` other than `Autonogrammer.Native` (Android, Web, AgentWorker, Desktop) | `grep -rn "File\.\(Delete\|Write\|Move\)\|Directory\.Delete" apps --include=*.cs` |
| U-11 | Whether `PostgresDispatchStore` is composed by any entry point outside `src/`, `services/`, `apps/`, `tests/` (for example a deployment script) | `grep -rn "PostgresDispatchStore" /root/src/repos/autonogrammer --include=*.cs --include=*.json --include=*.props --include=*.md -l` |
| U-12 | `SessionConfigEndpoints.SetAsync` handler body (writes engine-side or to the store) | `read services/Autonogrammer.Api/Endpoints/SessionConfigEndpoints.cs:33-80` |
| U-13 | `CloudAgentEndpoints.CancelAsync` and `PullRequestAsync` side effects (in-process run tracker and GitHub only, or a store write) | `grep -n "_store\.\|_repository\." services/Autonogrammer.Api/Cloud/CloudAgentService.cs` restricted to the `CancelAsync` and `PullRequestAsync` ranges, then read them |
| U-14 | Full column list of `ledger_outbox` (only `:11-14` read) to confirm no account column | `read src/Autonogrammer.Sessions.Postgres/Migrations/M0007_LedgerOutbox.cs:10-30` |

---

## (G) Counts

| section | rows | RESOLVED | UNRESOLVED |
|---|---|---|---|
| A. Erased set (17 erased + 3 not erased, of 20 migration tables) | 20 | 20 | 0 |
| B. SQL writers | 46 | 45 | 1 (B-37 → U-02) |
| C. Dragonfly key families (5 key families + 2 pub/sub channels) | 7 | 7 | 0 |
| D. Background producers | 11 | 11 | 0 |
| E. API entry groups with mutating routes | 16 files | 16 | 0 (U-01, U-04, U-12, U-13 are sub-items noted in-line) |
| F. UNRESOLVED list | 14 | n/a | 14 |

Requested items 1 to 10 status: 1 RESOLVED (file-backed automation store, `AutomationServiceRegistration.cs:38-41`). 2 RESOLVED (approval/close/header writers and their locks, B-01 to B-05). 3 RESOLVED (`LockAccountAsync` callers: memory `:77,140,535`; project `:44` from `ProjectsEndpoints.cs:111`). 4 RESOLVED (dispatch enqueue, device-row locks, `LockAccountAsync` `:843`, `LockFeedAsync` `:885`). 5 RESOLVED (not under owner lock, B-09). 6 RESOLVED (dispatch store not registered; in-memory Development only). 7 RESOLVED (provider writes are host-wide omp configuration, not SQL). 8 RESOLVED (multi-line grep: every multi-line SQL statement keeps its table name on the verb line). 9 RESOLVED (AutomationScheduler and AutomationRunner → file store and cloud rows; CoworkSessionSync → HTTP → sessions). 10 RESOLVED for `apps/Autonogrammer.Native` (D-11); other apps UNRESOLVED (U-10).
