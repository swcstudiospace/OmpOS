# Phase brief 68: Data plane (ADR-0031)

Names are post-rename (`Autonogrammer.*`); paths are post-rename spellings of repo paths; line numbers in ADR-0031 cite the pre-rename files. Decision record: `docs/decisions/ADR-0031-data-plane-namespaces-and-resilience.md`. Research: `04-data-plane.md`, `03`, `08`, `09`.

## Goal

Cut every Autonogrammer-owned physical data name over to `autonogrammer` now (nothing is deployed), add the additive PostgreSQL/TimescaleDB schema that accounts, Cowork and Dispatch need with a reversible migration runner and least-privilege roles, move telemetry to GreptimeDB database `autonogrammer`, wrap every store call in bounded resilience with specific degraded states, and define the optional default-off audit path to the Substrate ledger. Plugin and Substrate namespaces are never touched.

## Requirements

| ID | Requirement |
| --- | --- |
| AG-68-1 | Every Dragonfly key and channel is built by `DragonflyKeyspace` under the prefix `autonogrammer:v1:`, with a mandatory TTL of at most 24 h in the same command. |
| AG-68-2 | Autonogrammer uses its own pinned Dragonfly service, with no ACL user as the isolation boundary and no eviction. |
| AG-68-3 | The schema, database and test names are `autonogrammer`, `autonogrammer` and `autonogrammer_test`; no first-party source contains the old physical names. |
| AG-68-4 | Migrations up to the compiled head (M0001 to M0010; phase 68 U3 authors M0001 to M0009, M0006 carries the artifact tables and M0009 the ADR-0037 projects, instructions and memory tables, and M0010 `dispatch_device_state` is authored and registered in the runner list by phase 74 U2) are numbered, checksummed, additive, each with a down step, run by `Autonogrammer.Api migrate` under one advisory lock with the migrator role only. |
| AG-68-5 | The runtime role `autonogrammer_app` has DML only (INSERT and SELECT only on `dispatch_audit`); a role test proves it cannot run DDL or read another schema. |
| AG-68-6 | `ISessionRepository.ListByOwnerAsync` and `SetClosedAsync`, `Session.Surface` and `Session.ClosedAt` exist in the in-memory and Postgres stores. |
| AG-68-7 | Telemetry goes to GreptimeDB database `autonogrammer` with source `Autonogrammer.Api` and `autonogrammer.*` instruments, using only the pinned OpenTelemetry 1.20.0 packages. |
| AG-68-8 | `Autonogrammer.Resilience` gives every Postgres, Dragonfly and ledger call a deadline, bounded jittered retry (idempotent calls only), circuit breaker and bulkhead, on an injected `TimeProvider`. |
| AG-68-9 | Dragonfly outages fail closed (503 `data_plane_unavailable`, never a separate coordination problem type) for leases, idempotency and device nonces, and open for signals and presence; rate limits degrade to the in-process limiter. |
| AG-68-10 | `GET /status/data-plane` and the 503 problem `data_plane_unavailable` expose only component, state and a closed reason enum. |
| AG-68-11 | The ledger sink is off by default, outbox-fed, writes only through `SubstrateGateway.EmitEventAsync`, and sends only the closed field set (no personal data). |
| AG-68-12 | No client build, head or log holds a data-plane credential, connection string or pepper; configuration errors name the key, never the value. |
| AG-68-13 | Throwaway containers and variables are named `autonogrammer-test-*` and `AUTONOGRAMMER_TEST_*`; live tests are trait- and variable-gated; `GateParityTests` covers the test-services calls. |
| AG-68-14 | Account erasure deletes owner rows from every user-content table (including `cowork_memory_entries`, `cowork_memory_settings`, `cowork_instructions`, `cowork_projects`) and the tenant's Dragonfly keys, and leaves only keyed-hash audit rows. |

## Depends on

- Rename tool (parallel task) is applied after these units; units write post-rename names directly and must not run it. ADR-0030 (brief 67) consumes M0003 and the repository methods; brief 67 depends on this phase for the session part. ADR-0036 (Dispatch) consumes M0005 and the keyspace `presence`, `auth` and `sig:dispatch` domains. `cowork_events.kind` is the closed list fixed in ADR-0031 M0006 (`artifact.created`, `artifact.updated`, `artifact.grant`, `artifact.revoke`, `schedule.run`, `schedule.late`, `schedule.waiting_for_approval`); the Cowork tool audit is the desktop's local `FileCoworkToolAudit` and does not use this phase. Phase 71 U3 writes `PostgresArtifactStore` against the M0006 tables and no migration.
- Shared registry files (`Autonogrammer.slnx`, `Autonogrammer.Linux.slnf`, `LayerMap.cs`, `Autonogrammer.ArchitectureTests.csproj`, `THIRD-PARTY-NOTICES.md`, `CHANGELOG.md`, `docs/architecture.md`, `Program.cs`) are registered for every project by the one serialised scaffold step (00-scaffold-manifest.md), which also bumps `ContractVersion` to 1.6.0; units here add code only and edit a registry file only for rows the manifest marks "per phase".

## Projects and files owned

| Project | Layer | State |
| --- | --- | --- |
| `src/Autonogrammer.Resilience` + `tests/Autonogrammer.Resilience.Tests` | L1 | new; one `LayerMap` line; no package |
| `src/Autonogrammer.Coordination` (adds `ITenantHasher`) and `src/Autonogrammer.Coordination.Dragonfly` | L1, L2 | existing; renamed keys, `DragonflyKeyspace`, presence and rate-limit helpers; only `Coordination.Dragonfly` (L2) references Resilience, `Coordination` (L1) does not (a same-layer edge would need a `SameLayerAllowed` entry, ADR-0031 Consequences) |
| `src/Autonogrammer.Sessions`, `src/Autonogrammer.Sessions.Postgres` | L2 | existing; `Migrations/`, runner, data sources, new stores; the `EnsureCreated` schema classes are deleted |
| `src/Autonogrammer.Contracts` (`Sessions/SessionSurface.cs`, `DataPlane/`) | L0 | existing; additive (the scaffold step bumps `ContractVersion` to 1.6.0, no unit edits it) (`SessionSurface` is created here and reused by ADR-0030) |
| `services/Autonogrammer.Api` (`DataPlane/`, `Audit/`, `Telemetry/`, `Metrics/`, `Persistence/`) | L3 | existing; gains a `ProjectReference` to `Autonogrammer.Harness.AgentLoop` (L2) for the ledger sink |

Existing files touched: `scripts/test-services.sh`, `deploy/compose.yaml`, `.env.example`, `.github/workflows/ci.yml` (container names in the wait step only), `docs/runbooks/hosting.md` (variable table, data-plane section), `docs/api/openapi.json` (regenerated), `apps/Autonogrammer.Web/Demo/**` (fixture for `/status/data-plane`), `tests/Autonogrammer.ArchitectureTests/GateParityTests.cs`. New files: `deploy/data-plane/bootstrap.sql`, `scripts/data-plane/bootstrap-greptime.sh`, `docs/runbooks/data-plane.md`, `tests/Autonogrammer.ArchitectureTests/DataPlaneGuardTests.cs`.

## Work units (file-disjoint; order U1 then U2, U3 in parallel, then U4, U5; U6 any time)

| Unit | Owner project | Content | Tests | Acceptance |
| --- | --- | --- | --- | --- |
| U1 Resilience | `Autonogrammer.Resilience` | deadline, decorrelated-jitter retry, breaker (5 consecutive or 50 % of 20 in 30 s, open 15 s, one probe), semaphore bulkhead, `DependencyState`, `DataPlaneTimeoutException`; options records with validators; `LoggerMessage` events | `ManualTimeProvider`, seeded `Random`: every transition, retry budget bound, cancellation passes through, bulkhead rejects at the cap | `dotnet test --project tests/Autonogrammer.Resilience.Tests` passes; `ProjectGraphTests` accepts the new L1 project; no new package in `Directory.Packages.props` |
| U2 Dragonfly | `Autonogrammer.Coordination(.Dragonfly)` | `DragonflyKeys` becomes `DragonflyKeyspace` (domains `lease`, `idem`, `presence`, `rl`, `auth`, `sig`); `ITenantHasher` (HMAC-SHA256, 16 bytes hex, pepper); TTL guard; presence lease, rate counter, nonce; fail-closed or fail-open per ADR table; health check reports `DependencyState` | existing Dragonfly tests renamed (the hard-coded `ompos:idem:` test, `DragonflyCoordinationTests.cs:234`); new: TTL guard, key grammar, tenant hash stable and keyed, fail modes against a stopped container, erasure scan deletes only the tenant | `dotnet test --project tests/Autonogrammer.Coordination.Tests` with `source "$(bash scripts/test-services.sh up)"` passes; `DragonflyAclTests` is Live-gated |
| U3 Postgres | `Autonogrammer.Sessions(.Postgres)`, `Contracts` | `IMigrationStep`, `MigrationRunner` (`schema_migrations`, checksum, lock, up and down), M0001 to M0009 (M0006 with `artifacts`, `artifact_versions` capped at 5,242,880 bytes and `artifact_grants`), role-aware data sources, `ListByOwnerAsync` and `SetClosedAsync` in both stores, erasure, `deploy/data-plane/bootstrap.sql`, retention licence preflight | contract tests for both stores (cross-owner is not found), runner idempotency and checksum mismatch, down steps, two-process feed `id` ordering, role test (app cannot DDL, cannot `UPDATE dispatch_audit`, cannot read `public` tables), retention policy on 2.13.1 | `dotnet test --project tests/Autonogrammer.Sessions.Postgres.Tests`; `migrate` then `migrate --down 3` then `migrate` leaves `schema_migrations` at head on a throwaway database |
| U4 API composition | `services/Autonogrammer.Api` | `DataPlaneOptions` (`DataPlane:*`, `IValidateOptions`, `ValidateOnStart`), `DataPlaneStatusService`, `/status/data-plane`, 503 mapper, readiness checks incl. schema head wait, `migrate` verb in `Program.cs`, meter and instrument rename plus `autonogrammer.dataplane.*`, exporter `EventListener` and Greptime reachability probe, per-`sub` nothing (owned by 67) | options validator (Production refuses `AutoMigrate`, missing pepper), status contract, problem shape, OTLP request recorded by an in-process listener carries `X-Greptime-DB-Name: autonogrammer` and the new names, `GuardTests` openapi equality | `dotnet test --project tests/Autonogrammer.Api.IntegrationTests`; `docs/api/openapi.json` regenerated and committed |
| U5 Ledger sink | `services/Autonogrammer.Api/Audit` | `IAuditLedger`, `NullAuditLedger` (default), `SubstrateAuditLedger`, `LedgerOutboxPump` hosted service, closed `AuditLedgerEvent` type | fake `SubstrateGateway` handler: off by default writes nothing, backlog and retry, breaker, field-set lock test, refuses port 7410 in the live test | tests above pass; `DataPlane:Ledger:Enabled=false` leaves `ledger_outbox` empty |
| U6 Test infra and docs | scripts, deploy, docs, `ArchitectureTests` | rename in `test-services.sh`, `--with-greptime`, compose and `.env.example` rename, `bootstrap-greptime.sh`, runbook, hosting variable table, `GateParityTests` test, `DataPlaneGuardTests` | the two new architecture tests plus `ReadmeIndexListsEveryAdr` | `dotnet test --project tests/Autonogrammer.ArchitectureTests -- --filter-class "*DataPlaneGuardTests*"` passes |

## Success criteria

1. `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` and `dotnet restore Autonogrammer.Linux.slnf --locked-mode` pass; `bash scripts/ci/check-licences.sh` passes with no new package.
2. `source "$(bash scripts/test-services.sh up)"; dotnet test Autonogrammer.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"` passes.
3. `grep -rniE "ompos:|schema ompos|ompos_test|ompos\.(sessions|cloud)" src services tests scripts deploy .env.example` returns nothing outside the allowed external omp tokens.
4. `AUTONOGRAMMER_LIVE_GREPTIME=1 dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release --no-build --filter-trait "Category=Live"` after `bash scripts/test-services.sh up --with-greptime` passes on a machine with podman.
5. `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*DecisionRecordTests*"` passes with ADR-0031 indexed.

## Threat model

- Cross-account read through a missed owner filter: every store method scopes by owner; contract test per method; no RLS in v0.16.0 (OQ-342).
- Audit tampering: `dispatch_audit` is INSERT and SELECT only for the runtime role; the API process has no DDL and no migrator secret (separate service).
- Credential exposure: template superuser unused by the API; secrets only as Railway variables of the owning service; startup errors name keys; no secret in any head, log, metric tag or ledger event.
- Key injection or unbounded growth in Dragonfly: ids hashed or charset-validated before use in a key, TTL mandatory, `--maxmemory` set, eviction off.
- Cross-tenant key guessing: tenant is a keyed hash, not the account id.
- Permanent leakage through the ledger: default off, closed field set, no personal data, account only as keyed hash.
- Resource exhaustion: deadlines, bulkheads and breakers bound the effect of a slow store; the telemetry queue drops oldest.
- Dev data in production ledger: tests never use `127.0.0.1:7410`; the live test refuses it.
- Residual: the Timescale cluster is shared with an unknown Substrate index; roles can connect to other databases but hold no privilege there.

## Degraded and error states

| Component | State and reason | Behaviour |
| --- | --- | --- |
| PostgreSQL | Unavailable (`CircuitOpen`, `Timeout`, `AuthFailed`), `SchemaBehind`, `SchemaAhead` | writes 503 `data_plane_unavailable`; heads show the account-service banner, cached lists marked stale; Code mode unaffected |
| Dragonfly | Unavailable (`CircuitOpen`, `Timeout`) | runs (lease down), prompts and Dispatch sends refused with 503 `data_plane_unavailable` (`component` Dragonfly, closed `reason`) and `Retry-After`; presence "unknown"; signals and rate limits degrade |
| Telemetry | Degraded (`ExporterFailing`), Disabled (`NotConfigured`) | operator view only |
| Ledger | Disabled (default), Degraded (`LedgerBacklog`) | operator view only; user requests never wait on it |
| Retention | `RetentionUnsupported` | M0008 fails with a named error; operator decision (OQ-345) |

Banners use text and an icon, a live region for state changes, and focusable retry controls; strings are owned by ADR-0032 UX and tested with the head's accessibility tests.

## Cannot be verified on this host

Railway private-network reachability, the Railway Timescale edition and `add_retention_policy` there, Substrate's Dragonfly prefix and index location, Greptime per-database authorisation on the Railway instance, any hosted deploy (paused, OQ-73), the Supabase-signed tokens that feed the owner id (ADR-0030), rootless podman in CI (OQ-168), and the head banners on Windows, macOS and Android (compile-checked only).

## Open questions (final numbers, block OQ-340 to OQ-359)

| ID | Question | Proposed default | Owner |
| --- | --- | --- | --- |
| OQ-340 | What are Substrate's Dragonfly prefix and PostgreSQL index location? | Not needed: dedicated Dragonfly, own database; the maintainer confirms before the first deploy | substrate maintainer |
| OQ-341 | Does Dragonfly v1.27.1 enforce ACL key and channel patterns? | Use as defence in depth only after `DragonflyAclTests` passes | platform |
| OQ-342 | Add row-level security on user tables? | No in v0.16.0, artifact tables included; revisit with a hypertable performance test | architect |
| OQ-343 | Are artifact bodies synced to the server? | Off by default; metadata only until the user enables sync (merged with the former OQ-402; the row lives in docs/open-questions.md) | product owner |
| OQ-344 | Retention of chat, Cowork and artifact content? | Keep until user deletion or account erasure; no automatic expiry | owner and legal |
| OQ-345 | Is the Railway Timescale template the community (Timescale License) edition? | Preflight `timescaledb.license`; if `apache`, run retention from a one-shot service with the migrator role | platform |
| OQ-346 | Can Greptime static users be limited per database on v1.1.4? | Treat the database as a namespace only | platform |
| OQ-347 | Close the Timescale TCP proxy and the public Greptime domain? | Yes after the first successful private-network deploy; nothing depends on them | owner |
| OQ-348 | Does Substrate add an Autonogrammer `surface`? | Use the token's surface; kind `note`, action `autonogrammer.audit.v1` | substrate maintainer |
| OQ-349 | Which Railway services exist (2026-10-21 and 2026-10-28 lists conflict)? | Read-only re-inventory before provisioning | owner |
| OQ-350 | How is the tenant-hash pepper rotated? | Two-pepper window (new writes use key 2, reads try both) specified in a later ADR; no rotation in v0.16.0 | architect |

## Index rows

ADR index (`docs/decisions/README.md`, four columns):

`| [ADR-0031](ADR-0031-data-plane-namespaces-and-resilience.md) | Data plane namespaces, schema, telemetry sink and resilience | Proposed 2026-10-28 | Cutover of every owned physical name to autonogrammer; dedicated Dragonfly service; own database, roles and numbered additive migrations run by a separate one-shot service; GreptimeDB database autonogrammer through the pinned OTLP exporter; hand-written L1 resilience library; default-off outbox to the Substrate ledger (devnet anchor, no Solana code); hosted steps blocked by paused Railway deploys, OQ-73 |`

Open-question rows (`docs/open-questions.md`, six columns): one row per OQ-340 to OQ-350 above, with the question as "Question", "Blocks or affects" = the ADR-0031 section named in the table, "Default" = the proposed default, the owner, and status `open (non-blocking default)`; OQ-345, OQ-347 and OQ-349 are hosted-only and blocked by OQ-73.
