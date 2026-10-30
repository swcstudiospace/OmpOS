# R4: Data plane recon (Dragonfly, TimescaleDB, GreptimeDB, ledger) and namespace plan

Scope: read-only recon of worktree `/root/src/repos/autonogrammer-autonogrammer` (branch `feat/autonogrammer`). Facts carry repo-relative `path:line`. `[INFERENCE]` marks anything not read from a file. `[EXTERNAL]` marks evidence from a mounted MCP tool description, not from the repo. No credential value was read or recorded; variable NAMES only. Nothing was built, run or contacted.

## 1. Headline findings

1. **Nothing of Autonogrammer is deployed on Railway.** The API was never created (deploys paused, `docs/runbooks/evidence/railway-autonogrammer-api-2026-10-21.md:3,30-42`; `docs/runbooks/hosting.md:41`). The Autonogrammer schema `autonogrammer` and its tables did not exist on the Railway TimescaleDB on 2026-10-21 (`railway-autonogrammer-api-2026-10-21.md:25-26`). So every `autonogrammer*` physical name lives only in local/test servers today.
2. **A desktop install reaches no Railway data service.** The desktop head composes no Dragonfly/Postgres/Herdr/remote service (`docs/decisions/ADR-0011-logging-split.md:28`). Its only network path to the data plane is the optional Substrate URL (`docs/runbooks/running-the-desktop-app.md:40`). Railway private DNS is runtime-only inside the project (`docs/plans/v0.15.0-research/09-remote-cloud-plan.md:78`).
3. **Three existing namespaces are in use and must be frozen:** Dragonfly prefix `autonogrammer:`; Postgres schema `autonogrammer` (+ `timescaledb` extension in `public`); GreptimeDB database `public` (OTLP header and, per the Substrate dossier, the Substrate ledger default too).
4. **GreptimeDB `public` is a shared namespace.** Autonogrammer OTLP uses `X-Greptime-DB-Name=public` (`docs/runbooks/hosting.md:107`) and the Substrate ledger reads `/v1/sql?db=public` by default (`docs/plans/agent-loop/dossier-substrate.md:70`). New Autonogrammer telemetry must not go there.
5. **No migration tool exists.** Schema is idempotent `CREATE ... IF NOT EXISTS` DDL run by the API at startup under `pg_advisory_xact_lock` (`src/Autonogrammer.Sessions.Postgres/PostgresSessionSchema.cs:29-124`). No retention or compression policy exists anywhere in the repo (grep for `add_retention`, `add_compression`: 0 hits). `session_events` therefore grows without bound.
6. **Solana: devnet only, by the one piece of evidence available**; no repo file mentions Solana beyond "Solana anchor" as an optional Substrate plane. No mainnet evidence exists anywhere (section 5).
7. **`plugins/agent-substrate` is a private submodule and is ABSENT here** (empty directory; section 4). All Substrate facts below come from the committed dossiers `docs/plans/agent-loop/dossier-substrate.md` (HEAD 36580a6 of a sibling checkout, not the pinned 754c51a) and the host client code, not from Substrate source.
8. **Railway service inventory is inconsistent across docs** (section 6). Must be re-read before any provisioning.

## 2. How Autonogrammer code uses each store

### 2.1 Dragonfly (`src/Autonogrammer.Coordination`, `src/Autonogrammer.Coordination.Dragonfly`)

| Item | Fact | Source |
|---|---|---|
| Client | StackExchange.Redis, one singleton `IConnectionMultiplexer`; connection string passed through unchanged; `abortConnect=false` required | `src/Autonogrammer.Coordination.Dragonfly/DragonflyCoordinationServiceCollectionExtensions.cs:14-16,25`; `Autonogrammer.Coordination.Dragonfly.csproj` |
| Database index | Default `GetDatabase()` (no explicit index) | `DragonflyIdempotencyStore.cs:22`, `DragonflySessionRunLeases.cs:21`, `DragonflyCoordinationHealthCheck.cs:16` |
| Prefix | every key and channel starts `autonogrammer:` | `DragonflyKeys.cs:7,10` |
| Run lease | `autonogrammer:lease:run:{sessionId}`, `SET NX PX` with owner id; TTL 30 s, renewed every 10 s | `DragonflyKeys.cs:24`; `DragonflySessionRunLeases.cs:37`; `src/Autonogrammer.Coordination/CoordinationDefaults.cs:7,10` |
| Idempotency | `autonogrammer:idem:{sha256hex(scope + "\n" + key)}`; value `P\|{fingerprint}` (pending, TTL 60 s) then `C\|{fingerprint}\|{responseJson}` (TTL 24 h). Scope is the session id | `DragonflyKeys.cs:13,16,31`; `CoordinationDefaults.cs:13,16`; `DragonflyIdempotencyStore.cs:40-44`; `services/Autonogrammer.Api/Endpoints/SessionEndpoints.cs:272` |
| Pub/sub | channel `autonogrammer:signals`, payload = session id; best-effort, "never carries the change" | `DragonflyKeys.cs:19`; `src/Autonogrammer.Coordination/ISessionSignals.cs` |
| Lua | 3 scripts, each touches only `KEYS[1]` (Dragonfly rejects undeclared keys): renew, delete-if-value, complete-if-pending | `DragonflyScripts.cs:7-17` |
| Health | `PING`, readiness tag `ready` | `DragonflyCoordinationHealthCheck.cs:21-34`; `CoordinationRegistration.cs:35` |
| Every key has a TTL | yes (lease 30 s, pending 60 s, completed 24 h); no persistent keys | `CoordinationDefaults.cs` |
| Config names | `Coordination__Store` (`Dragonfly`\|`InMemory`), `Coordination__Dragonfly__ConnectionString` (secret). Key `Coordination:Dragonfly:ConnectionString` | `services/Autonogrammer.Api/Options/CoordinationOptions.cs:15,22`; `.env.example:32-36` |
| Startup | PING at start; failure names the key, never the value; Production warns on InMemory | `services/Autonogrammer.Api/Coordination/CoordinationStartup.cs:61`; `hosting.md:102` |
| Reaper | reads `Coordination:Store` per sweep; only acts when Dragonfly | `services/Autonogrammer.Api/Workflows/SessionRunReaper.cs:80-91` |
| Tests | env `AUTONOGRAMMER_TEST_DRAGONFLY_CONNECTION`; ids `autonogrammer-test-<guid>` so reruns on a shared server do not collide; one test hardcodes `autonogrammer:idem:` | `tests/Autonogrammer.Coordination.Tests/DragonflyTestEnvironment.cs:7,24`; `DragonflyCoordinationTests.cs:234` |

Not verified in the repo: the Dragonfly logical database count, ACL support, and persistence settings. Compose runs `--bind=0.0.0.0 --dir=/data` with `DFLY_requirepass` only (`deploy/compose.yaml:31-33`); no ACL file, no `--dbnum`, no snapshot flags. [INFERENCE] Dragonfly is treated as an ephemeral cache.

### 2.2 TimescaleDB (`src/Autonogrammer.Sessions.Postgres`)

| Item | Fact | Source |
|---|---|---|
| Driver | Npgsql; `NpgsqlDataSource` with forced `SearchPath = "autonogrammer,public"` whatever the string says | `ServiceCollectionExtensions.cs:14,39,47-48` |
| Config names | `Sessions__Store` (`Postgres`\|`InMemory`; Production refuses InMemory), `Sessions__Postgres__ConnectionString` (secret). Key `Sessions:Postgres:ConnectionString` | `ServiceCollectionExtensions.cs:11`; `services/Autonogrammer.Api/Persistence/SessionStoreStartup.cs:18-19,42-53`; `.env.example:24-29` |
| Schema | `CREATE SCHEMA IF NOT EXISTS autonogrammer`; `CREATE EXTENSION IF NOT EXISTS timescaledb WITH SCHEMA public` | `PostgresSessionSchema.cs:33,35`; `PostgresCloudAgentSchema.cs:26` |
| Tables (unqualified, resolve via search path to `autonogrammer`) | `sessions` (PK id; idx `sessions_status_updated_at_idx`), `session_events` (hypertable), `session_tool_approvals` (PK `(session_id, request_id)`), `cloud_agents` | `PostgresSessionSchema.cs:37-79`; `PostgresCloudAgentSchema.cs:28-51` |
| Hypertable | `session_events`, dimension `occurred_at`, `chunk_time_interval => 1 day`, `if_not_exists`, PK `(session_id, seq, occurred_at)`, FK `session_id -> sessions(id)` | `PostgresSessionSchema.cs:52-65` |
| Cloud agents indexes | `cloud_agents_session_idx` (unique), `cloud_agents_owner_key_idx` (unique partial on `(owner_id, idempotency_key)`), `cloud_agents_status_updated_at_idx`, `cloud_agents_owner_created_at_idx` | `PostgresCloudAgentSchema.cs:41-51` |
| Retention / compression | none defined | repo-wide grep |
| Migration mechanism | no tool. `EnsureCreatedAsync` runs all DDL in one transaction after `pg_advisory_xact_lock(AdvisoryKey("autonogrammer.sessions.schema"))` (and `"autonogrammer.cloud-agents.schema"`). Doc says later changes add `ADD COLUMN IF NOT EXISTS` statements to the same batch ("migration 1") | `PostgresSessionSchema.cs:31,89-121`; `PostgresCloudAgentSchema.cs:18-19,24,59-90`; `services/Autonogrammer.Api/Cloud/CloudAgentStoreStartup.cs:41` |
| Advisory keys | `AdvisoryKey(value)` = first 8 bytes of SHA-256 as int64. Session locks use `pg_advisory_lock(AdvisoryKey(sessionId))` with the raw session id (no prefix) | `PostgresSql.cs`; `PostgresSessionRepository.cs:223-228` |
| Row lock for seq | `SELECT 1 FROM sessions WHERE id=@id FOR UPDATE` before choosing `seq` | `PostgresSessionRepository.cs:66` |
| Images | tests, compose and `scripts/test-services.sh`: `docker.io/timescale/timescaledb:2.13.1-pg16@sha256:1b2b5508...` (digest pinned). Hosted: Railway template `timescale-postgis-ssl:pg16-ts2.13` | `deploy/compose.yaml:11`; `scripts/test-services.sh:11`; `tests/Autonogrammer.Sessions.Postgres.Tests/PodmanPostgresContainer.cs:15`; `hosting.md:32` |
| Test isolation | each test creates schema `t_<guid>` and sets search path to it | `tests/Autonogrammer.Sessions.Postgres.Tests/PostgresTestSchema.cs:26-38` |
| TLS | Production string uses `Ssl Mode=Require;Trust Server Certificate=true` (self-signed template cert) | `hosting.md:101`; `handoff.md:210` |
| Open | Q-DATA-TSDB-VERSION: hypertable DDL "has not been executed" on the 2.13 Railway template | `docs/open-questions.md:49` |

### 2.3 GreptimeDB (telemetry only in Autonogrammer code)

- `services/Autonogrammer.Api/Program.cs:205-224`: `Otel:Exporter` = `none|console|otlp`; `otlp` calls `AddOtlpExporter()` with SDK defaults, so endpoint/headers come only from `OTEL_EXPORTER_OTLP_*` variables.
- Variable names: `Otel__Exporter`, `OTEL_EXPORTER_OTLP_ENDPOINT` (`http://greptimedb.railway.internal:4000/v1/otlp`), `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf`, `OTEL_EXPORTER_OTLP_HEADERS` (`Authorization=<Basic>`, `X-Greptime-DB-Name=public`, `X-Greptime-Pipeline-Name=greptime_trace_v1`) (`hosting.md:104-107`; `.env.example:57-61`). The pipeline header must be in the general header variable, not the traces-specific one (`smoke-backend-local-2026-10-21.md:43-46`).
- Source/meter name `Autonogrammer.Api` (`services/Autonogrammer.Api/Telemetry/ApiTelemetry.cs:12`). Instruments: `autonogrammer.sessions.count`, `autonogrammer.stream.latency`, `autonogrammer.parser.fallback.rate`, `autonogrammer.sandbox.provision.time` (`services/Autonogrammer.Api/Metrics/ApiMetrics.cs:24-27`); `autonogrammer.cloud.*` incl. `autonogrammer.cloud.compute.seconds`, `autonogrammer.cloud.provision.time` (`services/Autonogrammer.Api/Cloud/CloudMetrics.cs:41-46`).
- Observed in local smoke: table `opentelemetry_traces` (8 spans) and ASP.NET Core metric tables (`smoke-backend-local-2026-10-21.md:37-38`).
- Auth: Railway GreptimeDB has `static_user_provider`; local compose has none. Railway variables are `GREPTIME_USERNAME` and `GREPTIME_PASSWORD` (no single Basic value) (`railway-autonogrammer-api-2026-10-21.md:49`; `deploy/compose.yaml:7-8`). Image `greptime/greptimedb:v1.1.4` (pinned) (`compose.yaml:40`).
- `scripts/test-services.sh` starts only Postgres + Dragonfly, not Greptime.

## 3. Local/test services and ports

| What | Value | Source |
|---|---|---|
| `scripts/test-services.sh up` | podman only; containers `autonogrammer-test-postgres`, `autonogrammer-test-dragonfly`; random password (`openssl rand -hex 16`) and random loopback host ports; DB `autonogrammer_test`, user `autonogrammer`; passwords via podman env, never argv; writes a mode-600 file exporting `AUTONOGRAMMER_TEST_POSTGRES_CONNECTION` and `AUTONOGRAMMER_TEST_DRAGONFLY_CONNECTION`; prints only the file path | `scripts/test-services.sh:9-16,57-90` |
| Images | `timescale/timescaledb:2.13.1-pg16@sha256:1b2b...444b`; `dragonflydb/dragonfly:v1.27.1@sha256:385cb7...7917` | `scripts/test-services.sh:11-12` |
| `deploy/compose.yaml` (profile `backend`) | postgres 127.0.0.1:5432 (`AUTONOGRAMMER_POSTGRES_USER`, `AUTONOGRAMMER_POSTGRES_DB` default `autonogrammer`, `AUTONOGRAMMER_POSTGRES_PASSWORD` required), dragonfly 127.0.0.1:6379 (`AUTONOGRAMMER_DRAGONFLY_PASSWORD` required, passed as `DFLY_requirepass`), greptimedb 127.0.0.1:4000 (HTTP; RPC bound 4001 inside container, not published) with no auth | `deploy/compose.yaml:6-8,10-51` |
| CI | `build-test` job needs rootless podman; failed on the Omega runner at "Start test services" (OQ-168) | `docs/open-questions.md:205` |

## 4. Plugins and plugin host

- `.gitmodules` lists `plugins/claude-ultrathink`, `plugins/agent-swarm`, `plugins/agent-substrate` (`.gitmodules`). In this worktree all three directories are **empty** (verified: each has 1 entry, `.`/`..`); only `plugins/gsd-core-omp` and `plugins/autonogrammer-hooks-omp` hold files. `agent-substrate` is private (`docs/plugins.md:35,54-56`; CI checks it out only with secret `SUBMODULE_TOKEN`, `.github/workflows/ci.yml:45-56`). Pin `754c51a17a9e6ffbc222d04bdaf4e783b4eca720` (`docs/plugins.md:35`).
- Grep for Dragonfly/Timescale/Greptime/Solana in `plugins/gsd-core-omp` and `plugins/autonogrammer-hooks-omp`: 0 hits. The Autonogrammer-owned plugins use no data-plane service.
- `src/Autonogrammer.Harness.Plugins`, `Autonogrammer.Harness.Registry`, `Autonogrammer.Harness.McpGateway`: no Dragonfly/Timescale/Greptime/Solana references. Harness.Plugins is the hook engine (loopback bridge); Registry is a file registry export; McpGateway is a host MCP endpoint on IPv4 loopback with bearer secret and loopback Host/Origin checks (`src/Autonogrammer.Harness.McpGateway/McpGatewayServer.cs:10-18,54-55,264-267`).
- The only host code touching the data plane indirectly is `src/Autonogrammer.Harness.AgentLoop/Substrate/*`: `SubstrateGateway` calls Substrate over HTTP: `GET /healthz`, `POST /brief`, `POST /mcp`, `POST /events`, `POST /events/query` (`SubstrateGateway.cs:12-16`). It reads `index` / `greptime` / `eventsWritable` booleans from `/healthz` (`SubstrateGateway.cs:96-102`). Limits: 10 s timeout, 1 MiB body cap, no redirects, bearer only in `Authorization` and never logged (`SubstrateGateway.cs:12-16,28`).
- `SubstrateEndpoint`: https, or http only on loopback; no userinfo, query or fragment (`src/Autonogrammer.Harness.AgentLoop/Substrate/SubstrateEndpoint.cs:20-45`). Desktop config names: `AUTONOGRAMMER_SUBSTRATE_URL`, `AUTONOGRAMMER_SUBSTRATE_TOKEN_ENV` (default `SUBSTRATE_TOKEN_OMP`), also `SUBSTRATE_TOKEN_FILE`, `SUBSTRATE_TOKENS_DIR` (`docs/runbooks/running-the-desktop-app.md:40-43`). Extension env: `SUBSTRATE_URL`, `SUBSTRATE_OMP_PENDING_DIR` (`docs/plugins.md:111-115`).
- Substrate server data needs, from the dossier (not source): index = vanilla Postgres via `SUBSTRATE_PG_URL` (example DB name `substrate`), ledger = GreptimeDB HTTP SQL via `GREPTIME_URL` (+ `GREPTIME_USER/PASSWORD/DB`, default DB `public`), optional cache `DRAGONFLY_URL`, optional event bus (needs Dragonfly), optional "Solana anchor" plane (`dossier-substrate.md:69-80`). Schema is created at boot by `substrate.migrate()` (`dossier-substrate.md:88`). Dragonfly key prefix used by Substrate is **UNKNOWN** (submodule absent): collision with an Autonogrammer prefix cannot be ruled out except by choosing a distinctive one.
- Ledger properties: append-only, hash-chained, cannot be scrubbed; development traffic hits the production ledger (`dossier-substrate.md:196`). `EVENT_KINDS` (12) and `SURFACES` are closed and lock-tested; new concepts use `payload.action` (`dossier-substrate.md:105,197`). Memory reads are not scope-access-controlled (`dossier-substrate.md:186-187`).
- Autonogrammer never starts a Substrate server (`docs/plugins.md:151`); tests never touch live `127.0.0.1:7410` (`docs/plugins.md:186`). A live substrate runs on this dev host (`dossier-substrate.md:19-21`): treat as shared production.
- ADRs: ADR-0010 (MCP gateway), ADR-0012 (agent-loop plugins), ADR-0013 (plugin isolation), ADR-0020 (cloud agents on Railway). ADR-0011 notes desktop composes no Dragonfly service (`ADR-0011-logging-split.md:28`). No ADR defines data-plane namespaces.

## 5. Ledger and Solana: what the evidence says

- Repo: the only Solana mention is "Solana anchor" in a list of optional Substrate planes (`docs/plans/agent-loop/dossier-substrate.md:80`). `grep -i solana|devnet|mainnet` over docs, src, services, plugins, `.planning` (excluding lockfiles/notices): that one line. No cluster, RPC URL, signer or program id is recorded in the repo.
- `[EXTERNAL]` mounted MCP tool `substrate_checkpoint_verify` (description, read via `xd://`, tool not called): "inside an hourly Merkle root that was written to **Solana devnet**"; statuses include `unverified` for "a devnet reset"; "Read-only: no transaction is ever sent"; evidence is hashes, proof path, public keys and a tx signature only. This is the sole cluster evidence and it says **devnet**.
- **Verdict: devnet. Mainnet has no supporting evidence.** The anchor is of hourly Merkle roots of the Greptime ledger, not a ledger itself. Devnet can be reset, so it is not a durability guarantee.
- `gsd-core-omp` and `autonogrammer-hooks-omp` have no Solana usage, so "already used by plugins" holds only for the private Substrate plugin, and only as an optional plane [INFERENCE: whether it is enabled on the live host is unknown; the dossier healthz lists `ledger` but the value was not recorded].

## 6. Railway services and reachability today

- Project `Ultrathink`, environment `production`; private DNS `SERVICE_NAME.railway.internal` (`hosting.md:27`; `handoff.md:207`).
- Names by source:
  - Pre-flight 2026-10-21: Agent, Dragonfly, Gate, GreptimeDB, OpenBot, Postgres, Routines, TimescaleDB, tailscale-forwarder, tailscale-vpn; no `ompos-api` (`railway-autonogrammer-api-2026-10-21.md:14`).
  - `hosting.md:31-36` table: planned `autonogrammer-api`; existing TimescaleDB, Dragonfly, GreptimeDB, Postgres (plain), "Agent, Routines, OpenBot, Gate".
  - Read-back 2026-10-28: "TimescaleDB, Dragonfly, GreptimeDB and two tailscale services only" (`hosting.md:41`; `docs/open-questions.md:107,137`).
  - **Conflict**: the 10-28 read-back omits Postgres, Agent, Routines, OpenBot, Gate. Whether they were removed or the read-back is partial is **unknown**. Re-inventory read-only before relying on any name.
- Private hosts/ports: `timescaledb.railway.internal` (port [INFERENCE: 5432; read `PGPORT`]), `dragonfly.railway.internal:6379`, `greptimedb.railway.internal:4000`, `ompos-api.railway.internal:8080` (`hosting.md:31-34`).
- Public/proxy exposure found in the repo: the TimescaleDB service has a **TCP proxy** (used by `RAILWAY_TCP_PROXY_DOMAIN`/`RAILWAY_TCP_PROXY_PORT` in the read-only pre-check, `railway-autonogrammer-api-2026-10-21.md:18`); GreptimeDB has a public service domain `greptimedb-production.up.railway.app`, designated telemetry fallback only (`hosting.md:48`). Substrate's stores are reached through Tailscale forwarders, tailnet host names must never be written to the repo (`dossier-substrate.md:194`).
- Service variable names (values never recorded): Timescale `PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`, `PGPASSWORD`; Dragonfly `DFLY_requirepass`; Greptime `GREPTIME_USERNAME`, `GREPTIME_PASSWORD` (`railway-autonogrammer-api-2026-10-21.md:47-49`; `hosting.md:101,103,118`).
- The Railway private network is flat: every service in an environment reaches every other; databases are protected only by passwords (`09-remote-cloud-plan.md:78,169`; `ADR-0020...md:62`). ADR-0020 puts untrusted cloud workers in a separate Railway project (`autonogrammer-agents`) with no route to the databases before a second user exists.
- Open: Q-HOST-PRIVNET (private reachability from IPv6-only env, unverified), Q-HOST-DFLY-PIN (Railway Dragonfly is `ghcr.io/dragonflydb/dragonfly:latest`, unpinned), Q-HOST-GREPTIME-AUTH (`docs/open-questions.md:46-48`).
- **How a desktop install reaches them today: it does not.** Desktop: local omp + optional Substrate URL (https or loopback http). Web head: Vercel Demo, static, simulated engine, no backend (`hosting.md:41`). The real API has no public domain until sign-in (OD-03) exists (`hosting.md:45`; `ADR-0017-web-head-demo-mode.md:14`). No pairing/remote code for a second device exists (OQ-68, `docs/open-questions.md:105`). `RemoteEngineHost` consumes the hosted API over HTTP/SSE (`09-remote-cloud-plan.md:26`).

## 7. Existing safety rules to preserve

| Rule | Source |
|---|---|
| Secrets: nothing committed; `.env.example` documents every variable; startup fails fast naming the key, never the value | `AGENTS.md:17`; `.env.example`; `CoordinationStartup.cs:61` |
| Compose publishes only on 127.0.0.1; every password required from env, no default | `deploy/compose.yaml:6` |
| Hosted data plane reached over `*.railway.internal`; no public domain for API; GreptimeDB public domain only as fallback | `hosting.md:27,45,48` |
| Substrate URL: https or loopback http only; never in argv; bearer only in header | `SubstrateEndpoint.cs:20-30`; `SubstrateGateway.cs:15-16` |
| Substrate fabric endpoint classes `loopback`, `proxy`, `direct`, `unset`, `not-applicable`; states `ok/degraded/unavailable/unconfigured`; compliance checks "data-plane endpoints are private", "endpoint policy is enforce", "no secret-shaped value in report" `[EXTERNAL]` (`substrate_fabric_status`, `substrate_fabric_compliance` descriptions, tools not called). Variable names from the dossier: `SUBSTRATE_DB_ENDPOINT_MODE`, `SUBSTRATE_DB_PROXY_ALLOW` | `dossier-substrate.md:194` |
| Hosted API refuses link-local, metadata and reserved addresses for custom endpoints; private LAN needs operator opt-in | `docs/open-questions.md:189` (OQ-152) |
| Podman only, no Docker tooling | `AGENTS.md:14`; `BannedSymbols*.txt` |
| Tests hermetic: no live Substrate, no live Railway; Dragonfly test ids are random | `docs/plugins.md:186`; `DragonflyTestEnvironment.cs:20-25` |
| Railway workers never get DB passwords, Railway token or signing key | `docs/runbooks/cloud-agents.md:26` |

## 8. Recommended namespace plan for Autonogrammer data

Principle: **additive namespace beside `autonogrammer`, never a rename in place.** Existing `autonogrammer*` names are frozen. All Autonogrammer data is reachable only through the Autonogrammer API (Supabase-JWT authenticated); desktop and mobile never speak Postgres/Redis/Greptime protocols. Dispatch needs a desktop-initiated outbound connection to the API (precedent: Cursor "My Machines" outbound-only worker, `09-remote-cloud-plan.md:65`).

### 8.1 Dragonfly

- **Prefix:** `autonogrammer:v1:` for every key and channel. `v1` allows a later key-shape change without scans. Not `ag:` (too short to be distinctive against the unknown Substrate cache prefix).
- **Grammar:** `autonogrammer:v1:<domain>:<tenant>:<entity>[:<id>]`. `<tenant>` = lowercase hex of the first 16 bytes of SHA-256(account UUID), never an email or token. Domains: `presence` (desktop online), `dispatch` (wake signals), `rl` (rate-limit counters), `auth` (sign-in throttles, one-time nonces), `idem` (idempotency, same hashed pattern as `autonogrammer:idem:`).
- **Channels:** `autonogrammer:v1:sig:<domain>`; payload is an id only (same rule as `ISessionSignals`: signals never carry the change).
- **TTL rules (MUST):** every key written with `PX`/`EX` in the same command (`SET ... PX`, never `SET` then `EXPIRE`); no key without TTL; max TTL 24 h except idempotency completed records (24 h, matching `autonogrammer`). Proposed defaults: `presence` 45 s (heartbeat 15 s), `auth` nonce 10 min, `rl` window length, `dispatch` wake hint 5 min.
- **Source of truth:** Dragonfly holds only reconstructible state. Dispatch commands, accounts, devices and anything that must survive a restart live in Postgres; Dragonfly only wakes subscribers (consistent with `ISessionSignals`). Compose has no persistence settings (`compose.yaml:31-33`), so a restart may drop everything [INFERENCE].
- **Lua:** declare every touched key in `KEYS` (`DragonflyScripts.cs:4-6`).
- **Role isolation:** compose/Railway use only `requirepass` (one shared password). Preferred: a Dragonfly ACL user `autonogrammer` limited to `~autonogrammer:v1:*` and `&autonogrammer:v1:*`, no `FLUSH*`/`KEYS`/`CONFIG`; **gated on verifying ACL support in the pinned image v1.27.1** and on pinning the Railway image (Q-HOST-DFLY-PIN). Fallback if ACL is not verified: a dedicated Dragonfly service (own password) for Autonogrammer so an `autonogrammer`/Substrate incident cannot reach it.
- **Config names (proposed):** `Autonogrammer__Dragonfly__ConnectionString` (secret). The prefix is a code constant, not configuration. Tests: `AUTONOGRAMMER_TEST_DRAGONFLY_CONNECTION` is reused; test ids `autonogrammer-test-<guid>`.
- **Never:** `FLUSHDB`/`FLUSHALL`, `KEYS autonogrammer:*`, `SCAN`-and-delete outside the own prefix, `SELECT` to a different DB as an isolation mechanism (unverified behavior).

### 8.2 TimescaleDB / PostgreSQL

- **Schema `autonogrammer`** in the same cluster and database as `autonogrammer` unless a separate database is chosen (section 10, D3). Reuse the `timescaledb` extension already in `public`; new migrations MUST NOT `CREATE EXTENSION`, `ALTER EXTENSION`, or touch `public`.
- **Separate data source:** a new `NpgsqlDataSource` with `SearchPath = "autonogrammer,public"` (`public` only for `create_hypertable`). Never reuse `PostgresSessionConnection`'s `autonogrammer,public` source (`ServiceCollectionExtensions.cs:39,48`). Every statement schema-qualified (`autonogrammer.cw_events`), not relying on the search path.
- **Table naming** (collision-free with unqualified `autonogrammer` names `sessions`, `session_events`, `session_tool_approvals`, `cloud_agents`): prefix by domain. Regular tables: `acct_accounts`, `acct_devices`, `acct_sign_in_events`, `dp_commands` (dispatch, pending/acked), `cw_chats`, `cw_artifacts`, `cw_artifact_versions`, `bu_runs` (browser use), `cu_runs` (computer use). Hypertables (time-series/append-only): `cw_events` (chat/cowork event log), `dp_command_log`, `usage_samples`. Hypertable rules: partition on a `timestamptz` column that is part of every unique key (as `session_events` does, `PostgresSessionSchema.cs:59`); `chunk_time_interval` stated explicitly; `if_not_exists => TRUE`.
- **Retention/compression (new; autonogrammer has none):** `add_retention_policy` on telemetry-like hypertables (`usage_samples` 90 d, `dp_command_log` 30 d [INFERENCE: values are proposals]); `add_compression_policy` (after 7 d, `segmentby` account id) on `cw_events`; user content retention is a product decision (D6). Verify each call on the pinned 2.13.1 image before shipping (not exercised in the repo).
- **Roles (least privilege):** `ag_owner` (NOLOGIN; owns schema and objects), `ag_migrator` (LOGIN; member of `ag_owner`; used only by the migration step), `ag_app` (LOGIN; `USAGE` on schema, `SELECT/INSERT/UPDATE/DELETE` on tables, `USAGE` on sequences, no DDL; `ALTER DEFAULT PRIVILEGES` so future tables inherit), `ag_ro` (LOGIN; `SELECT` only, for support/analytics). `REVOKE ALL ON SCHEMA autonogrammer FROM ag_app, ag_ro, ag_migrator` and the reverse for the Autonogrammer role on `autonogrammer`. Row-level security keyed on `account_id` for `ag_app` is recommended for user-owned tables [INFERENCE: design choice, needs a performance check on hypertables]. Today the Autonogrammer connection string takes `PGUSER` from the template (`hosting.md:101`), which on Railway is presumably an admin role [INFERENCE]; Autonogrammer MUST NOT reuse it at runtime.
- **Config names (proposed):** `Autonogrammer__Postgres__ConnectionString` (runtime, `ag_app`), `Autonogrammer__Postgres__MigratorConnectionString` (secret, used only when `Autonogrammer__Postgres__Migrate=true`), both `Ssl Mode=Require` like `hosting.md:101`.
- **Migrations (additive, reversible), keeping the repo's own pattern, no new tool:**
  1. A `autonogrammer.schema_migrations(version int primary key, name text, checksum text, applied_at timestamptz)` table.
  2. Numbered steps in C# next to the store (like `PostgresCloudAgentSchema` "migration 1"), each idempotent (`IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`), run in one transaction under `pg_advisory_xact_lock(AdvisoryKey("autonogrammer.schema"))`. Prefix the lock string so it cannot equal a raw session id key (`PostgresSessionRepository.cs:223`).
  3. Each step ships a documented down step (DROP only objects it created); down steps are run by an operator, never automatically, and never in the same release as the up step (expand/contract: contract steps ship one release later).
  4. No destructive change to a column in use; no `ALTER TABLE ... TYPE`, no `DROP` in an up step.
  5. Do not run migrations from a runtime role; the startup hook (compare `SessionStoreStartup.cs:52-53`) uses the migrator string only when explicitly enabled.
  6. Compose/tests: new test fixture creates `t_<guid>` style schemas like `PostgresTestSchema` (`PostgresTestSchema.cs:26-38`), never `autonogrammer` itself.

### 8.3 GreptimeDB

- **Separate database `autonogrammer`** (`CREATE DATABASE IF NOT EXISTS autonogrammer`), selected by `X-Greptime-DB-Name=autonogrammer` in `OTEL_EXPORTER_OTLP_HEADERS` for the Autonogrammer API process (one header set per process, so the whole process moves; acceptable because `autonogrammer-api` is not deployed, `hosting.md:41`). Keep `X-Greptime-Pipeline-Name=greptime_trace_v1` in the same variable (`smoke-backend-local-2026-10-21.md:43-46`). `public` stays reserved for the Substrate ledger and any existing Autonogrammer telemetry.
- **Naming:** new instruments `autonogrammer.<area>.<name>` with units, e.g. `autonogrammer.dispatch.latency` (ms), `autonogrammer.auth.sign_in.count`, `autonogrammer.cowork.artifact.count`. Existing `autonogrammer.*` instruments (`ApiMetrics.cs:24-27`, `CloudMetrics.cs:41-46`) are left as they are in this release (renaming breaks any dashboard); source name `Autonogrammer.Api` stays until a deliberate cutover (D4). No account id, email or device name as a metric label/tag; use a bounded `result`/`reason` tag like `CloudMetrics` does.
- **Retention:** set a database/table TTL on `autonogrammer` telemetry [INFERENCE: Greptime supports a TTL option; verify on v1.1.4]. Ledger-class data is not written to Greptime by Autonogrammer.
- **Least privilege:** Railway Greptime uses `static_user_provider` with `GREPTIME_USERNAME`/`GREPTIME_PASSWORD`. Whether static users can be restricted per database is **not verified in the repo**; until verified, a separate database is a namespace, not an access boundary. Plan: one Autonogrammer write user and one read-only operator user if supported; the Basic header is set only in the hosting environment (`hosting.md:107`).
- **Never write** to the Substrate ledger tables, never post to `/events` with test data against the live instance (append-only, unscrubbable).

### 8.4 Ledger / Solana

- Autonogrammer MUST NOT hold a Solana signer, send a transaction, or configure an RPC URL. At most it may call Substrate's read-only `checkpoint_verify` through `SubstrateGateway` (`POST /mcp` `tools/call`, `SubstrateGateway.cs:142-146`) behind the existing endpoint rules.
- Any new auditable Autonogrammer event reuses Substrate's closed vocabulary via `payload.action` (`dossier-substrate.md:197`) and goes through a Substrate phase, not a new table.
- Cluster is devnet by the only evidence. No docs or UI may describe the anchor as mainnet, permanent or legally binding.

### 8.5 Endpoint reachability rules for the new services

- Hosted: all Autonogrammer data services over `*.railway.internal` from `autonogrammer-api` only. No TCP proxy and no public domain for Postgres/Dragonfly. The existing Timescale TCP proxy (section 6) is an exposure to review (D7); Autonogrammer must not depend on it.
- Desktop/mobile/web: HTTPS to the API only. A desktop that is dispatched to holds an outbound authenticated connection; the API never dials the desktop.
- Cloud workers: separate Railway project, no route to databases (`ADR-0020`; `09-remote-cloud-plan.md:169`).
- Local dev: compose ports stay on 127.0.0.1 (`deploy/compose.yaml:6`).

## 9. Must not be touched (explicit list)

1. Dragonfly keys/channels under `autonogrammer:` (`autonogrammer:lease:run:*`, `autonogrammer:idem:*`, `autonogrammer:signals`) and the constants in `DragonflyKeys.cs`.
2. Postgres schema `autonogrammer` and its objects: `sessions`, `session_events` (hypertable), `session_tool_approvals`, `cloud_agents`, their indexes, and advisory-lock strings `autonogrammer.sessions.schema`, `autonogrammer.cloud-agents.schema` and raw-session-id advisory keys.
3. The `timescaledb` extension and anything in `public`.
4. The `autonogrammer*` connection-string keys `Sessions:Postgres:ConnectionString`, `Coordination:Dragonfly:ConnectionString` and env names `Sessions__*`, `Coordination__*`, `AUTONOGRAMMER_POSTGRES_*`, `AUTONOGRAMMER_DRAGONFLY_PASSWORD`, `AUTONOGRAMMER_TEST_*`.
5. GreptimeDB database `public` (Autonogrammer OTLP + Substrate ledger), tables `opentelemetry_traces*`, and the existing `autonogrammer.*` metric names.
6. Everything of Substrate: its Postgres index database, its Greptime ledger, its Dragonfly cache and event bus, the Solana anchor plane, tokens under `/etc/substrate/`, the live instance on `127.0.0.1:7410`.
7. Railway services not owned by Autonogrammer: Agent, Routines, OpenBot, Gate, plain Postgres, tailscale-forwarder, tailscale-vpn (and the TimescaleDB TCP proxy configuration) until the inventory conflict is resolved and the owner decides.
8. Railway Dragonfly image tag, Greptime `static_user_provider` credentials and `PG*`/`DFLY_requirepass`/`GREPTIME_*` variables of existing services.
9. `scripts/test-services.sh` container names `autonogrammer-test-postgres` / `autonogrammer-test-dragonfly` and `deploy/compose.yaml` volume names (`postgres-data`, `dragonfly-data`, `greptimedb-data`): rename = data loss on existing dev boxes.

## 10. Decisions needed and gating

| ID | Decision | Recommendation | Gate |
|---|---|---|---|
| D1 | Keep `autonogrammer` physical names (key prefix, schema, DB `public`) for existing features, or cut over now since nothing is on Railway | Keep. Rename is cosmetic, breaks tests (`DragonflyCoordinationTests.cs:234`), local volumes, dashboards | Owner |
| D2 | Dragonfly isolation: ACL user vs dedicated service | ACL only after verification on v1.27.1 and a pinned Railway image; otherwise dedicated service | Q-HOST-DFLY-PIN; platform |
| D3 | Postgres isolation: schema `autonogrammer` in the existing DB vs separate database | Schema + dedicated roles (needs `CREATE ROLE`/`GRANT` privilege and an inventory showing the cluster is not shared with Substrate's `substrate` DB); a separate DB needs `CREATEDB` and its own `CREATE EXTENSION timescaledb` | Read-only inventory of Railway TimescaleDB; owner |
| D4 | Telemetry: whole API process to Greptime DB `autonogrammer`, and when to rename the `Autonogrammer.Api` source and `autonogrammer.*` instruments | Move the DB now; rename instruments later | Owner; dashboards |
| D5 | Greptime per-database/per-user authorization | Verify `static_user_provider` capabilities on v1.1.4 | Platform |
| D6 | Retention of user content (chat events, artifacts, browser/computer-use captures) and of `session_events` | Product decision before launch; telemetry-class data gets TTLs now | Owner/legal |
| D7 | Existing public exposure: Timescale TCP proxy and Greptime public domain | Review and close or restrict; Autonogrammer must not depend on them | Platform |
| D8 | Railway inventory conflict (10-21 vs 10-28) | Read-only re-inventory before any provisioning | Railway MCP/CLI read access |
| D9 | Where migrations run (API startup vs release job) and who holds `ag_migrator` | Release job with the migrator secret; API runtime has no DDL | Owner; hosting runbook |
| D10 | Solana anchoring for Autonogrammer data | Out of scope; devnet-only read-only verification via Substrate | Owner |
| D11 | Dispatch persistence: Postgres queue + Dragonfly wake (recommended) vs Dragonfly-only | Postgres + wake, since Dragonfly durability is unverified | Architect |

Hard gates before any live write: Railway deploys are paused at account level (`railway-autonogrammer-api-2026-10-21.md:42,80`); no sign-in authority (OD-03, `hosting.md:45`); private reachability unverified (Q-HOST-PRIVNET); Timescale hypertable DDL never executed on the Railway template (Q-DATA-TSDB-VERSION); `plugins/agent-substrate` source is absent, so Substrate Dragonfly prefix, Greptime table names and anchor settings cannot be checked.

## 11. Open questions (facts I could not obtain)

1. Substrate's Dragonfly key prefix and Greptime ledger table names (private submodule absent; dossier does not list them).
2. Whether the Substrate index Postgres is the Railway TimescaleDB, the plain `Postgres` service, or elsewhere (`dossier-substrate.md:69,194`).
3. Whether the Solana anchor plane is enabled on the live host, the RPC provider, and signer-key custody.
4. Dragonfly ACL/persistence/`dbnum` behavior on v1.27.1 and the unpinned Railway `latest`.
5. Greptime static-user permission modes on v1.1.4; database-level TTL syntax.
6. The role (`PGUSER`) privilege level on the Railway TimescaleDB template, and whether the 2.13 policy functions (`add_retention_policy`, `add_compression_policy`) run on it as expected.
