# Railway configuration for the hosted API

Review date: 2026-10-29. Owner role: deployment owner. Status: Draft. Nothing here has been applied to a Railway project: the API service does not exist yet (see `docs/runbooks/hosting.md`).

This folder holds the settings of the hosted API in the shape Railway documents, and the list of every variable the API reads. It does not create anything by itself.

| File | What it is |
|------|------------|
| `api.railway.json` | Build and deploy settings of the `autonogrammer-api` service |
| `postgres.railway.json` | Replica count of the TimescaleDB service (a template service, no build section) |
| `dragonfly.railway.json` | Replica count of the Dragonfly service |

## Read this first: Config as Code is deprecated

Railway's documentation, read on 2026-10-29 (https://docs.railway.com/config-as-code and https://docs.railway.com/infrastructure-as-code), says:

- Config as Code (`railway.json` or `railway.toml`) is deprecated. It is still read for existing (legacy) services and overrides the dashboard for them.
- New services cannot opt into Config as Code. Existing files stop being read on 2026-12-01.
- The replacement is Infrastructure as Code in `.railway/railway.ts`, which needs the npm package `railway`. This repository adds no npm dependency, so no `.railway/railway.ts` is written here.

The JSON files in this folder are therefore the reviewed record of the intended values, with the field names checked against Railway's JSON schema (https://railway.com/railway.schema.json, read on 2026-10-29: `build.builder`, `build.dockerfilePath`, `deploy.healthcheckPath`, `deploy.healthcheckTimeout`, `deploy.restartPolicyType`, `deploy.restartPolicyMaxRetries`, `deploy.numReplicas`, `deploy.drainingSeconds`). Whether Railway reads them for a new service is not verified. Apply the same values with the service settings or the Railway MCP `update-service` tool, as the staged worker was configured on 2026-10-28 (`docs/runbooks/evidence/railway-autonogrammer-worker-2026-10-28.md`, "How it was done").

| Setting in `api.railway.json` | Value | Why | `update-service` field | Verified |
|-------------------------------|-------|-----|------------------------|----------|
| `build.builder` | `DOCKERFILE` | The build file is `deploy/api.Containerfile` | none (Railway builds with a Dockerfile when it is pointed at one; the worker used `dockerfilePath`) | Worker: yes, 2026-10-28 |
| `build.dockerfilePath` | `deploy/api.Containerfile` | The file is named `*.Containerfile` because a repository test forbids `Dockerfile*` names | `dockerfilePath` (or the variable `RAILWAY_DOCKERFILE_PATH`) | Worker: yes |
| `deploy.healthcheckPath` | `/health/ready` | Anonymous; checks harness, session store and Dragonfly | `healthcheckPath` | Worker used `/healthz`; the API path is not yet run on Railway |
| `deploy.healthcheckTimeout` | `120` | Owner decision of 2026-10-21; the start-up schema step and the omp check run before the first answer | `healthcheckTimeout` | Worker: yes (120 s) |
| `deploy.restartPolicyType` | `ON_FAILURE` | A crash restarts; a clean exit does not | `restartPolicyType` | Worker: yes |
| `deploy.restartPolicyMaxRetries` | `3` | A configuration error exits at start (exit 134, see the 2026-10-21 smoke evidence), so more retries only repeat it | `restartPolicyMaxRetries` | Worker: yes (3) |
| `deploy.numReplicas` | `1` | Run-time indexes (`SessionRegistry`, `RunRegistry`) live in process memory and are not shared between instances | `regions` (for example `{"<region>": {"numReplicas": 1}}`; it replaces the whole region map) | Not run |
| `deploy.drainingSeconds` | `35` | Five seconds above the host shutdown timeout of 30 s that unit api-hosting sets (`Hosting:ShutdownTimeoutSeconds`), so the host ends open streams first | `drainingSeconds` | Not run |

If the service should read the file, the service setting "config file path" (`railwayConfigFile` in `update-service`) names it; the default file name Railway looks for is `railway.json` at the root of the service source, not `api.railway.json`.

The health check runs from the host `healthcheck.railway.app` at deploy time only. If a later release adds host filtering (`AllowedHosts`), that host must be allowed.

## Services

### `autonogrammer-api`

Source: this repository, build context at the repository root, `deploy/api.Containerfile`. Port: the image binds `[::]:${PORT:-8080}`; set `PORT=8080`. No public domain until the checks in `docs/runbooks/deploy-and-rollback.md` pass. No volume is defined by the image; the omp agent directory and the workspace location are open decisions (threat model, Q-TM-5).

### Postgres (TimescaleDB)

The hosting topology uses the Railway template image `timescale-postgis-ssl:pg16-ts2.13` (PostgreSQL 16, TimescaleDB 2.13, TLS on), existing service `TimescaleDB` in the project `Ultrathink`. Local parity: `docker.io/timescale/timescaledb:2.13.1-pg16` (digest in `deploy/compose.yaml`). Notes:

- The API creates its own schema `autonogrammer`, the `timescaledb` extension and the `session_events` hypertable at start (`IF NOT EXISTS`); it needs a role that may create them.
- Use the private host `timescaledb.railway.internal` and the service's own `PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`, `PGPASSWORD` through references. Do not copy values.
- The connection uses TLS with `Trust Server Certificate=true`, because the template's certificate is self-signed.
- Do not enable or keep a public TCP proxy on this service for the API's sake (threat model, T3.11).
- The service holds data of other workloads (see `docs/decisions/ADR-0031-data-plane-namespaces-and-resilience.md`). A dedicated database or project is an open owner decision.
- Backups: `docs/runbooks/backup-and-restore.md`.

### Dragonfly

Image `ghcr.io/dragonflydb/dragonfly:latest` on Railway (unpinned, open item Q-HOST-DFLY-PIN); local parity `docker.io/dragonflydb/dragonfly:v1.27.1`. Notes:

- Password in the variable `DFLY_requirepass` on the Dragonfly service. The API reads it through the reference in `Coordination__Dragonfly__ConnectionString`.
- The API stores only leases (30 s), idempotency records (up to 24 h) and pub/sub wake-ups. Nothing needs a backup.
- The connection string in the hosting runbook has no TLS option; the private network carries it in clear.

## Variables of `autonogrammer-api`

Config keys use `:` in code and `__` in the environment. Mark every variable marked "secret: yes" as sealed or secret in Railway. "Added by unit" means the unit of program 66 that introduces the name; the names are fixed in the program contract and the unit is not merged when this page was written. "In progress" means the name was seen in the unit's unmerged worktree and is not part of the contract.

### Platform and runtime

| Variable | Secret | Example placeholder | Read in | Safe default |
|----------|--------|---------------------|---------|--------------|
| `PORT` | no | `8080` | `deploy/api.Containerfile` (entrypoint) | `8080` |
| `ASPNETCORE_ENVIRONMENT` | no | `Production` | image `ENV`; `services/Autonogrammer.Api/Program.cs` | `Production` (set by the image). Never `Development` |
| `RAILWAY_DOCKERFILE_PATH` | no | `deploy/api.Containerfile` | Railway builder | Unset when `dockerfilePath` is set |
| `GIT_SHA` | no | `<40-character commit sha>` | `services/Autonogrammer.Api/Services/SourceInfoProvider.cs` (`GET /source`) | `unknown` from the image build argument `AUTONOGRAMMER_REVISION` |

### Stores

| Variable | Secret | Example placeholder | Read in | Safe default |
|----------|--------|---------------------|---------|--------------|
| `Sessions__Store` | no | `Postgres` | `Options/SessionsOptions.cs`, `Persistence/SessionStoreStartup.cs` | `InMemory` in `appsettings.json`; Production refuses it at start. Set `Postgres` |
| `Sessions__Postgres__ConnectionString` | yes | `Host=${{TimescaleDB.PGHOST}};Port=${{TimescaleDB.PGPORT}};Database=${{TimescaleDB.PGDATABASE}};Username=${{TimescaleDB.PGUSER}};Password=${{TimescaleDB.PGPASSWORD}};Ssl Mode=Require;Trust Server Certificate=true` | `src/Autonogrammer.Sessions.Postgres/ServiceCollectionExtensions.cs` | None; start fails naming the key |
| `Coordination__Store` | no | `Dragonfly` | `Options/CoordinationOptions.cs`, `Coordination/CoordinationStartup.cs` | `InMemory` in `appsettings.json`; allowed in Production with a warning (not shared between instances). Set `Dragonfly` |
| `Coordination__Dragonfly__ConnectionString` | yes | `dragonfly.railway.internal:6379,password=${{Dragonfly.DFLY_requirepass}},abortConnect=false` | `Options/CoordinationDragonflyOptions.cs` | None; start fails naming the key |

### Engine

| Variable | Secret | Example placeholder | Read in | Safe default |
|----------|--------|---------------------|---------|--------------|
| `Harness__Adapter` | no | `Acp` | `Options/HarnessOptions.cs`, `Program.cs` lines 49 to 57 | Required; Production refuses `InMemory` |
| `Harness__Acp__OmpPath` | no | `omp` | `Options/HarnessOptions.cs`, `Health/HarnessReadinessCheck.cs` | `omp` (the image links `/usr/local/bin/omp`) |
| `Harness__Acp__Environment__Mode` | no | `Allowlist` | Added by unit engine-isolation | `Allowlist` for the hosted API |
| `Harness__Acp__Environment__Allow__0` and following | no | `PATH`, `HOME`, `LANG` | In progress, unit engine-isolation | The smallest list that lets omp start and reach its model provider |
| `Harness__Acp__AllowInheritedEnvironment` | no | `false` | In progress, unit engine-isolation | `false` |
| `Harness__Acp__WorkspaceRoot` | no | `<absolute directory the engine starts in>` | Added by unit engine-isolation | A directory outside `/app`, empty at start |
| `Worktrees__Root`, `Worktrees__Repository` | no | `<absolute paths>` | `Program.cs` lines 72 to 78, `Harness/AcpHarnessSessionFactory.cs` | Unset (the engine uses the process working directory). Both must be absolute |
| `AUTONOGRAMMER_PROVIDER_<ID>_KEY` | yes | `<provider key>` | `Services/Providers/ProviderKeyNames.cs` (the name omp's `models.yml` block refers to) | Unset until a model credential is decided (HOST-04). Limit its spend at the provider |
| `Providers__AllowCredentialWrite`, `Providers__AllowConfigWrite`, `Providers__AllowLiveTest` | no | `false` | `Services/Providers/ProvidersOptions.cs` | `false` |
| `Providers__AgentDirectory`, `Providers__OmpPath` | no | `<absolute path>` | `Services/Providers/ProvidersOptions.cs` | Unset (omp's own default) |
| `Providers__RequestsPerMinute`, `Providers__TestTimeoutSeconds`, `Providers__StateCacheSeconds` | no | `60`, `8`, `15` | `Services/Providers/ProvidersOptions.cs` | `60`, `8`, `15` |

### Authentication

| Variable | Secret | Example placeholder | Read in | Safe default |
|----------|--------|---------------------|---------|--------------|
| `Api__Auth__Mode` | no | `OwnerToken` | Added by unit api-auth | `Oidc`. Set `OwnerToken` for the single-operator deployment |
| `Api__Auth__OwnerToken__Sha256__0` (and `__1`, `__2` for rotation) | no | `<64 lowercase hexadecimal characters: SHA-256 of the token>` | Added by unit api-auth | None; one to three digests are required in `OwnerToken` mode |
| `Api__Auth__OwnerToken__Subject` | no | `owner` | Added by unit api-auth | `owner` |
| `Api__Auth__OwnerToken__MaxFailuresPerMinute` | no | `10` | In progress, unit api-auth | `10` |
| `Api__Auth__Authority`, `Api__Auth__Audience`, `Api__Auth__ValidIssuer` | no | `https://<issuer>` | `Options/ApiAuthOptions.cs`, `Options/ApiAuthOptionsValidator.cs` | Only for `Oidc` mode; in `OwnerToken` mode the unit's validator refuses them (in progress). No identity provider is chosen (OD-03) |
| `Api__Auth__AllowedSubjects__0`, `Api__Auth__AllowAnyAuthenticatedUser` | no | `<subject>`, `false` | In progress, unit api-auth | Only for `Oidc` mode |
| `Api__Auth__AllowDevelopmentPrincipal` | no | not set | `Options/ApiAuthOptions.cs`, `Program.cs` lines 148 to 159 | Unset. The validator refuses it outside Development |

The owner token itself is never a Railway variable. See `docs/runbooks/incident-response.md` for generating the token and its digest.

### HTTP surface and hosting

| Variable | Secret | Example placeholder | Read in | Safe default |
|----------|--------|---------------------|---------|--------------|
| `Api__Cors__Origins__0` (and `__1`) | no | `https://<web-head-host>` | `Options/ApiCorsOptions.cs`, `Security/ConfigureCorsOptions.cs` | Unset: no cross-origin call is allowed. Set to exactly the Vercel origin |
| `Api__MaxPromptLength` | no | `64000` | `Options/ApiOptions.cs` | `64000` |
| `Api__MaxRequestBodyBytes` | no | `1048576` | `Options/ApiOptions.cs` | `1048576` |
| `Api__RateLimit__PromptsPerMinute`, `Api__RateLimit__SessionsPerMinute` | no | `60`, `60` | `Options/ApiRateLimitOptions.cs`, `Security/SessionRateLimiterPolicy.cs` | `60`, `60` |
| `Api__Workspace__Root` | no | `<absolute directory>` | `Services/SessionWorkspaceOptions.cs`, `Services/WorkspaceLocator.cs` | Unset: the workspace endpoints answer that no workspace exists |
| `Api__Workspace__IncludeUserScope`, `Api__Workspace__Owners__0`, `Api__Workspace__RequestsPerMinute` | no | `false`, `<subject>`, `120` | `Services/WorkspaceSettings/WorkspaceSettingsOptions.cs` | `false`, unset (any authenticated caller), `120` |
| `Hosting__ForwardedHeaders__Mode` | no | `TrustAllSingleHop` | Added by unit api-hosting | `None`. Choose at the first deploy by checking the client address the API sees behind Railway's edge [INFERENCE: the edge sets `X-Forwarded-For` and `X-Forwarded-Proto`] |
| `Hosting__Hsts__Enabled` | no | `true` | Added by unit api-hosting | `true` in the unit's worktree |
| `Hosting__Sse__HeartbeatSeconds` | no | `15` | Added by unit api-hosting | `15` in the unit's worktree |
| Other `Hosting__*` keys (`RequestTimeoutSeconds`, `ShutdownTimeoutSeconds`, `SecurityHeaders__Enabled`, `Kestrel__*`, `JsonLogs`) | no | see the unit | In progress, unit api-hosting | The unit's defaults |

### Telemetry and logging

| Variable | Secret | Example placeholder | Read in | Safe default |
|----------|--------|---------------------|---------|--------------|
| `Otel__Exporter` | no | `none` | `Options/OtelOptions.cs`, `Program.cs` line 204 | `none` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | no | `http://greptimedb.railway.internal:4000/v1/otlp` | .NET OpenTelemetry SDK | Unset; needed only for `otlp` |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | no | `http/protobuf` | .NET OpenTelemetry SDK | Unset; needed only for `otlp` |
| `OTEL_EXPORTER_OTLP_HEADERS` | yes | `Authorization=<Basic credential>,X-Greptime-DB-Name=public,X-Greptime-Pipeline-Name=greptime_trace_v1` | .NET OpenTelemetry SDK; rule in `docs/runbooks/hosting.md` | Unset; needed only for `otlp` |
| `Autonogrammer__Debug__LogPrompts` | no | `false` | `Options/DebugOptions.cs` | `false`. `true` writes prompt text to logs |

### Leave unset

| Variable | Why |
|----------|-----|
| `Cloud__Enabled`, `Automations__Enabled` | Off by default. Enabling either adds secrets and a public webhook route that the threat model does not cover; each has its own required keys (`docs/runbooks/cloud-agents.md`) |
| `Workflows__*` | Removed with Temporal; do not set |

## Variables of the Vercel project (web head)

| Variable | Secret | Example placeholder | Read in | Safe default |
|----------|--------|---------------------|---------|--------------|
| `AUTONOGRAMMER_API_ORIGIN` | no | `https://<api-host>` | `scripts/vercel/render-csp.sh`, `vercel.ts` | None; the build fails without a valid https origin |
| `AUTONOGRAMMER_DEMO` | no | not set | `vercel.ts`, `scripts/vercel/render-csp.sh` | Unset for the real head; `1` builds the Demo |

## Cost controls

All of these are owner actions; none has been applied.

| Control | How | Note |
|---------|-----|------|
| One replica | `numReplicas: 1` in the file; `update-service` `regions` | The API is not designed for two instances today (in-memory indexes) |
| Resource limits | `update-service` (the limit fields are in its schema: read `xd://mcp__railway_update_service`); in file form `deploy.limitOverride.containers.cpu` and `memoryBytes` | Choose values from `get-service-metrics` after a week of real use; a low memory limit kills the process during a large run |
| Sleep when idle | `update-service` `sleepApplication` | Not recommended for the API: a cold start pays the omp handshake (about 8 s) and drops open streams. Not verified on this service. Fine for a staged worker |
| Spend alert and hard limit | Workspace Usage page, "Set Usage Limits": a custom email alert (soft) and a hard limit, separately for compute and agent usage (https://docs.railway.com/pricing/cost-control#configuring-usage-limits); CLI `railway usage` | Set the alert at the expected monthly spend. A hard limit stops services when reached; decide that knowingly |
| Volumes | Keep volumes small and attach only what is needed | Billed per GB; snapshots add the data unique to each |
| Provider key | Spend cap at the model provider | The engine can run unattended, so the provider's cap is the real limit |
| Staged resources | Delete what is not used (`ompos-worker` in `ompos-staging` is billed while it runs, OQ-171) | See the worker evidence for teardown |

## Verification status

- Field names: read against Railway's JSON schema on 2026-10-29.
- Variable names, defaults and file references: read in the source at the integration commit of the rename. Names added by other units come from the program contract; names marked "in progress" come from unmerged worktrees and may change.
- Not verified: that Railway reads these files for a new service; any value applied to a real service; `drainingSeconds` against the real host shutdown behaviour.
