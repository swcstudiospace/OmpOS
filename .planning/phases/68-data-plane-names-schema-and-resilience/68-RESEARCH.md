# Phase 68: Data plane: names, schema and resilience - Research
Source: docs/plans/v0.16.0-research/briefs/68-data-plane.md

Summarises research reports 04 (data plane), 03 (API/auth backend) and 09 (project recipe and conventions), plus ADR-0031 and index R-items R2, R3, R4, R13, R15. Citations are `file:line` repo-relative; report citations are into `docs/plans/v0.16.0-research/`.

## Standard Stack / What to reuse
- Dragonfly keys: `internal static class DragonflyKeys` `src/Autonogrammer.Coordination.Dragonfly/DragonflyKeys.cs:8`, `Prefix` `:10`; Lua scripts touch only `KEYS[1]` `DragonflyScripts.cs:4`; TTL constants `src/Autonogrammer.Coordination/CoordinationDefaults.cs:5-16` (lease 30 s / renew 10 s, pending 60 s, completed 24 h); signals carry an id only `ISessionSignals.cs`.
- Postgres schema today: `PostgresSessionSchema.cs:31` (lock key `autonogrammer.sessions.schema`), `:33` (`CREATE SCHEMA`), `:38` `sessions`, `:50` index, `:53` `session_events`, `:89` `EnsureCreatedAsync`; `PostgresCloudAgentSchema.cs:24` (lock key), `:29` table, `:42-51` four indexes. Both classes are deleted by U3.
- Search path forced in code: `src/Autonogrammer.Sessions.Postgres/ServiceCollectionExtensions.cs:14,39,47`; row lock for `seq` `PostgresSessionRepository.cs:66`; raw-session-id advisory lock `:223`; `PostgresSql.AdvisoryKey` (first 8 bytes of SHA-256).
- Repository: `src/Autonogrammer.Sessions/ISessionRepository.cs:21` (interface), `:68` `ListSessionIdsByStatusAsync` is the only list; twin `InMemorySessionRepository.cs`; record `src/Autonogrammer.Contracts/Sessions/Session.cs:8`; `SessionKind` already exists in `Contracts/Agents` (hence `SessionSurface`).
- Contract suites: `tests/Autonogrammer.Sessions.Tests/SessionRepositoryContractTests.cs` (+ `InMemoryRepositoryFixture.cs`), `tests/Autonogrammer.Sessions.Postgres.Tests/PostgresSessionRepositoryContractTests.cs:10`, `PostgresTwoProcessAppendTests.cs`, `PostgresTestSchema.cs:34` (`t_<guid>`), `PodmanPostgresContainer.cs:36` (`autonogrammer-test-pg-<guid>`).
- Test services: `scripts/test-services.sh:9-14` (names, images, DB, user), `:86-88` (exports `AUTONOGRAMMER_TEST_POSTGRES_CONNECTION`, `..._DRAGONFLY_CONNECTION`), `:93-95` (`up`, `down` verbs); `deploy/compose.yaml:11,29,40` (images: Timescale 2.13.1-pg16, Dragonfly v1.27.1, GreptimeDB v1.1.4, each pinned by digest); CI waits `.github/workflows/ci.yml:113-119,148-149`.
- Telemetry: OTel wiring `services/Autonogrammer.Api/Program.cs:205-227` (`AddOtlpExporter()` at `:215`, `:227`); source name `Telemetry/ApiTelemetry.cs:12`; instruments `Metrics/ApiMetrics.cs:24-27`, `Cloud/CloudMetrics.cs:13-22`.
- Startup validation that names the key and never the value: `services/Autonogrammer.Api/Coordination/CoordinationStartup.cs:61`; store registration `Persistence/SessionStoreStartup.cs`; readiness `Health/SessionRepositoryCheck.cs`.
- Ledger gateway: `src/Autonogrammer.Harness.AgentLoop/Substrate/SubstrateGateway.cs:21` (1 MiB cap), `:28` (10 s), `:168` `EmitEventAsync`; endpoint rules `SubstrateEndpoint.cs` (https or loopback http).
- Guards to extend: `tests/Autonogrammer.ArchitectureTests/GateParityTests.cs` (facts at `:27,59,88,98`), `DecisionRecordTests.cs:103` `ReadmeIndexListsEveryAdr`, `ProjectGraphTests.cs`.

## Facts the planner needs
- Nothing is deployed on Railway; Railway deploys are paused (report 04 line 7; brief 68 "Cannot be verified"). Every hosted step is out of scope; no Railway mutation.
- On disk today the physical constants already read the target spelling (`DragonflyKeys.cs:10` `"autonogrammer:"`; `test-services.sh:9-14`; `ServiceCollectionExtensions.cs:14`). Remaining deltas: `v1:` segment, merged lock string, roles, database, `autonogrammer.dataplane.*` instruments, `X-Greptime-DB-Name` header.
- `DragonflyCoordinationTests.cs:234` hard-codes the idempotency key prefix (`"autonogrammer:idem:"`); U2 must change it to the `v1` shape.
- Dragonfly client is StackExchange.Redis with one multiplexer, `abortConnect=false` required (report 04 line 22); every current key has a TTL (report 04 line 30).
- Postgres: `sessions` PK id, `session_events` hypertable PK `(session_id, seq, occurred_at)`, `session_tool_approvals`, `cloud_agents` with four indexes (report 04 lines 45-47). A hypertable unique key must contain the time column, so `dispatch_commands` is a regular table (ADR-0031 D4).
- Timescale `add_retention_policy` is Timescale-License; the throwaway image is `timescale/timescaledb:2.13.1-pg16` (`scripts/test-services.sh:11`); the Railway template `timescale-postgis-ssl:pg16-ts2.13` edition is unverified (OQ-345, report 04 line 52, open item 6).
- `autonogrammer.schema_migrations` columns: `version int PK, name, checksum, applied_at, applied_by` (ADR-0031 D5); lock string `autonogrammer.schema.migrations` contains dots, no session id does.
- Migration ownership: M0001 to M0009 phase 68 U3, M0010 phase 74 U2 (R2; `00-scaffold-manifest.md` section 3 last row). M0006 content cap 5,242,880 bytes; flat artifact names, no RLS (R3). `cowork_events.kind` closed list (R15). Accounts DDL is M0004 (R5).
- 503: one problem type `data_plane_unavailable` with closed `reason` (R13). Reason enum: None, CircuitOpen, Timeout, AuthFailed, NotConfigured, SchemaBehind, SchemaAhead, RetentionUnsupported, ExporterFailing, LedgerBacklog (ADR-0031 D8).
- Layer rule: `Autonogrammer.Resilience` L1 (manifest row 1); `Coordination.Dragonfly` and `Sessions.Postgres` (L2) reference it; `Coordination` (L1) must not (a same-layer edge needs a `SameLayerAllowed` tuple, `tests/Autonogrammer.ArchitectureTests/LayerMap.cs:65`); `Api` gains a reference to `Autonogrammer.Harness.AgentLoop` (L2) for the ledger sink.
- No package is added (`Directory.Packages.props` untouched): OTel 1.20.0, Npgsql 10.0.3, StackExchange.Redis 3.4.5 already pinned (ADR-0031 Licence table); `bash scripts/ci/check-licences.sh` must still pass.
- GreptimeDB: pipeline header must be in the general `OTEL_EXPORTER_OTLP_HEADERS`, not a traces-specific one (report 04 line 60, evidence `docs/runbooks/evidence/smoke-backend-local-2026-10-21.md:43-46`); local compose has no auth, Railway has `static_user_provider` (report 04 line 63); static users per database unverified (report 04 line 160).
- Ledger: Substrate ledger is append-only, hash-chained, unscrubbable; development traffic hits the production ledger; live instance `127.0.0.1:7410` must never be used (report 04 lines 83-84; ADR-0031 D10).
- Shared tracked files edited here: `.env.example` (`Sessions__*`, `Coordination__*`, `AUTONOGRAMMER_POSTGRES_*` lines 25-51), `docs/runbooks/hosting.md`, `docs/api/openapi.json` (regenerated at API build, report 03 line 89; compared by `GuardTests.cs:64-69`), `apps/Autonogrammer.Web/Demo/**` (fixture for `/status/data-plane`).

## Architecture Patterns
- One `NpgsqlDataSource` per role, built once by registration; the resilience pipeline is built once per dependency and never runs on a UI thread (ADR-0031 D7). Stores exist only in the API process.
- Options + `IValidateOptions<T>` + `ValidateOnStart` for `DataPlane:*`; failures name the key (report 09 line 59). Logging by `LoggerMessage.Define` with explicit `EventId`, optional `ILogger<T>? logger = null` (report 09 line 61). `TimeProvider` injected, `CancellationToken` last (report 09 line 60).
- Hand-written `ManualTimeProvider` and `Fake*`/`InMemory*` shipped in product code with contract-test base classes (report 09 lines 66-68); live tests trait plus `Assert.Skip` first line (report 09 line 69).
- Outbox pattern for the ledger: audit row in the same transaction, pump drains `ledger_outbox` (ADR-0031 D10); a ledger failure never touches a user request.
- Expand-then-contract migrations; down steps are operator-run, never in the shipping release (ADR-0031 D5).

## Pitfalls
- Brief success criterion 3 (`grep -rniE "autonogrammer:|schema autonogrammer|autonogrammer_test|autonogrammer\.(sessions|cloud)" src services tests scripts deploy .env.example` "returns nothing") matches the NEW names in this tree (`DragonflyKeys.cs:10`, `test-services.sh:13`). [INFERENCE] the patterns were written for the pre-rename token (`ompos`, see report 01 lines 160-161); the planner must define the check against the old token, or against `autonogrammer:` not followed by `v1:`.
- Report 04 section 8 is superseded where it conflicts with ADR-0031: keep-names (report 04 D1), `acct_/dp_/cw_` prefixes (04 line 143), RLS (04 line 145), `ag_*` roles, tenant = plain SHA-256 (04 line 130). Use the ADR (HMAC with pepper, flat names, no RLS, `autonogrammer_*` roles).
- `SET` then `EXPIRE` is forbidden (report 04 line 132); Lua undeclared keys are rejected by Dragonfly (report 04 line 28).
- `FOR UPDATE` on the owning row assigns per-device `seq` and per-account feed `id`; without it a resumer can skip a late commit (ADR-0031 D4; precedent `PostgresSessionRepository.cs:66`).
- Container-dependent tests fail in CI until rootless podman works (OQ-168; report 04 line 73); gating them locally needs `source "$(bash scripts/test-services.sh up)"`.
- The Railway inventory is contradictory between 2026-10-21 and 2026-10-28 (report 04 lines 98-101): no provisioning before a read-only re-inventory (OQ-349).
- Railway pre-deploy commands see the API's variables; the migrator secret must stay in the separate `autonogrammer-migrate` service (ADR-0031 D5).
- Dragonfly ACL pattern support on v1.27.1 is unproven; never rely on it as isolation (ADR-0031 D3).
- A hand-written breaker and retry must be proven by the injected clock; no sleeps (brief U1).
- Do not widen `SameLayerAllowed`: the plan relies on L1 `Coordination` not referencing `Resilience` (R14, brief 68 "Depends on").
- The old test-services container names are data-bearing on dev boxes (report 04 line 186); they are renamed anyway by decision.
- [INFERENCE] `DataPlaneGuardTests` scans first-party source text for forbidden literals (`FLUSHDB`, `KEYS `, `CREATE EXTENSION`, `DROP SCHEMA`): its own needle strings and the guard-scanned SQL steps must be built so the test does not flag itself (the `DeferredScopeTests` needle scan is the precedent, report 03 line 90). `TodoMarkerScanner` fails uppercase TODO markers in code (report 09 line 43).

## Verification commands
From brief 68 "Success criteria" and ADR-0031 "Verification route":
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror`; `dotnet restore Autonogrammer.Linux.slnf --locked-mode`; `bash scripts/ci/check-licences.sh`
- `source "$(bash scripts/test-services.sh up)"; dotnet test Autonogrammer.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"`
- `dotnet test --project tests/Autonogrammer.Resilience.Tests -c Release --no-build`; `dotnet test --project tests/Autonogrammer.Sessions.Postgres.Tests -c Release --no-build`; `dotnet test --project tests/Autonogrammer.Coordination.Tests -c Release --no-build` (last two after `source "$(bash scripts/test-services.sh up)"`)
- `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release --no-build` (regenerate and commit `docs/api/openapi.json`)
- `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release --no-build -- --filter-class "*DataPlaneGuardTests*" --filter-class "*GateParityTests*" --filter-class "*DecisionRecordTests*"`
- Migration round trip on a throwaway database: `migrate`, then `migrate --down 3`, then `migrate` leaves `schema_migrations` at head.
- Live: `AUTONOGRAMMER_LIVE_GREPTIME=1 dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release --no-build --filter-trait "Category=Live"` after `bash scripts/test-services.sh up --with-greptime`.

## Cannot be verified on this host
- Railway private-network reachability, the Railway Timescale edition and `add_retention_policy` there, Greptime per-database authorisation, any hosted deploy (paused, OQ-73).
- Substrate's Dragonfly prefix and PostgreSQL index location (`plugins/agent-substrate` submodule absent: report 04 line 77; OQ-340).
- Supabase-signed tokens that feed the owner id (ADR-0030); rootless podman in CI (OQ-168); head banners on Windows, macOS, Android (compile-checked only).
- Whether throwaway containers can run here is [INFERENCE] (the brief expects podman; no container may be started by this planning pass).

## Validation Architecture
Framework: xunit.v3 4.0.2 + Shouldly 4.3.0 on Microsoft.Testing.Platform (report 09 line 66); Live tests excluded by `--filter-not-trait "Category=Live"`.
Quick run: `dotnet test --project tests/Autonogrammer.Resilience.Tests -c Release` (no container needed). Full run: the commands under Verification commands, with test services sourced.

| Requirement | Test (project: behaviour, from brief units) |
| --- | --- |
| AG-68-1 | `Coordination.Tests`: key grammar, TTL guard (no TTL or over 24 h throws), tenant hash stable and keyed; `DataPlaneGuardTests` prefix constant and no write outside `DragonflyKeyspace` |
| AG-68-2 | `DragonflyAclTests` Live-gated; compose/test-services review (dedicated service, no eviction flag) |
| AG-68-3 | `DataPlaneGuardTests` schema constant; renamed Dragonfly test at `DragonflyCoordinationTests.cs:234`; grep check (see Pitfalls) |
| AG-68-4 | `Sessions.Postgres.Tests`: runner idempotency, checksum mismatch, down steps, M0001 to M0009 in order, `migrate`/`--down 3`/`migrate` round trip; guard scan of step sources (no `DROP`, `ALTER ... TYPE`) |
| AG-68-5 | `Sessions.Postgres.Tests` role test: app role cannot DDL, cannot `UPDATE dispatch_audit`, cannot read `public` tables; `has_database_privilege` |
| AG-68-6 | contract tests for both stores (`SessionRepositoryContractTests`, `PostgresSessionRepositoryContractTests`): list per surface, cross-owner not found, `SetClosedAsync` true only on change |
| AG-68-7 | `Api.IntegrationTests`: in-process OTLP listener sees `X-Greptime-DB-Name: autonogrammer` and new instrument names; Live Greptime test |
| AG-68-8 | `Resilience.Tests`: every breaker transition, retry budget bound, cancellation passthrough, bulkhead cap, on `ManualTimeProvider` and seeded `Random` |
| AG-68-9 | `Coordination.Tests`: fail modes against a stopped container (lease, idem, nonce closed; signals, presence open; rl falls back to in-process); `Api.IntegrationTests` 503 `data_plane_unavailable` |
| AG-68-10 | `Api.IntegrationTests`: status contract, problem shape (component, closed reason only), `GuardTests` OpenAPI equality |
| AG-68-11 | `Api.IntegrationTests` fake `SubstrateGateway` handler: off by default writes nothing (`ledger_outbox` empty), backlog and retry, breaker, field-set lock, live test refuses port 7410 |
| AG-68-12 | options validator rows (Production refuses `AutoMigrate`, missing pepper) naming the key never the value |
| AG-68-13 | `ArchitectureTests` `GateParityTests.BootstrapAndCiStartAndStopTheSameTestServices`; `DataPlaneGuardTests` |
| AG-68-14 | `Sessions.Postgres.Tests` erasure across every user-content family incl. memory family; `Coordination.Tests` erasure scan deletes only the tenant |

Wave 0 gaps (do not exist today): `tests/Autonogrammer.Resilience.Tests` (scaffold T1) and its `ManualTimeProvider` copy; `DataPlaneGuardTests.cs`; the `BootstrapAndCiStartAndStopTheSameTestServices` test; migration runner, role, retention and erasure tests; `ListByOwnerAsync`/`SetClosedAsync` contract cases; two-process feed `id` ordering test; Dragonfly fail-mode tests with a stopped container and `DragonflyAclTests`; OTLP in-process listener fixture; fake `SubstrateGateway` handler and the field-set lock test; Greptime live test and `up --with-greptime`; `/status/data-plane` Demo fixture and `DemoFixturesContractTests` row.
