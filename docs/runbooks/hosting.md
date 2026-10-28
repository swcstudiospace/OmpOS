---
description: "Hosting runbook for the OmpOS API on Railway: topology, limits and the deploy definition."
---

# Hosting runbook (HOST-01)

Status: Blocked (the API is not deployed; the production API origin and the sign-in authority are owner decisions; a staged private worker was deployed on 2026-10-28)

Last verified: 2026-10-28

Applies to: v0.15.0

## Status

Nothing is deployed by this runbook. The `ompos-api` service does not exist yet; the only OmpOS object on Railway is a staged private worker (section "Staged worker on Railway (2026-10-28)" below). This document and `deploy/api.Containerfile` are the hosting definition only.

The topology below is the owner decision of 2026-10-21 ([decision text](../handoff.md#temporal-removal-and-railway-topology-2026-10-21)). Services marked "existing" are described from that decision. They were not re-inspected in this rewrite [INFERENCE: state at deploy time].

Hosted runtime limits:

- The hosted API is configured for the ACP harness (`Harness__Adapter=Acp`). Production refuses the InMemory adapter at startup.
- No real omp runtime, sandbox provider, or model credential is deployed for the API. The staged worker of 2026-10-28 carries omp 18.12.5 and no model credential.
- HOST-04 stays open.

## Topology

Railway project `Ultrathink`, environment `production`. All service-to-service traffic uses private networking at `SERVICE_NAME.railway.internal`.

| Service | State | Image or source | Private host and port | Role |
|---------|-------|-----------------|-----------------------|------|
| `ompos-api` | Planned, not created | `deploy/api.Containerfile`, repository root as build context | `ompos-api.railway.internal:8080` | ASP.NET Core API. Session runs execute in-process. |
| TimescaleDB | Existing | `timescale-postgis-ssl:pg16-ts2.13` (Railway template: PostgreSQL 16, TimescaleDB 2.13, TLS on) | `timescaledb.railway.internal`, port [INFERENCE: 5432, the PostgreSQL default; read `PGPORT`] | Session log, schema `ompos` |
| Dragonfly | Existing | `ghcr.io/dragonflydb/dragonfly:latest` (unpinned; open item Q-HOST-DFLY-PIN) | `dragonfly.railway.internal:6379` | Run leases, idempotency, SSE wake-up pub/sub |
| GreptimeDB | Existing | `greptime/greptimedb:v1.1.4` (pinned) | `greptimedb.railway.internal:4000` | OTLP telemetry (auth enabled) |
| Postgres (plain) | Existing | Not used by this change | n/a | Retirement is an open owner decision (Q-PG-RETIRE) |
| Agent, Routines, OpenBot, Gate | Existing | Not used by this change | n/a | None |

Not services:

- **Worker.** No Temporal worker. The staged agent worker of 2026-10-28 is a different thing (see "Staged worker on Railway (2026-10-28)" below). The Temporal worker project was removed on 2026-10-21, and `deploy/` no longer contains a Temporal worker image (`deploy/worker.Containerfile` is the agent worker image).
- **Web.** Vercel hosts the Blazor WebAssembly web head outside Railway. Its configuration is `vercel.ts`; the CSP `connect-src` is generated at build time from `OMPOS_API_ORIGIN` by `scripts/vercel/render-csp.sh`. The **Demo** build is deployed (recorded 2026-10-28) at <https://ompos-web.vercel.app>, the production alias of project `ompos-web` in team `swcstudiospace`: static, simulated engine, no backend and no secret, so it needs no API origin (see [Web head runbook](web-demo.md)). The Vercel origin for a real web head and the production API origin are still open items (Q-VERCEL-CSP). Nothing of OmpOS is deployed to Railway: its project "Ultrathink" holds TimescaleDB, Dragonfly, GreptimeDB and two tailscale services only.

## Network and ports

- **No public domain for `ompos-api`.** Do not generate one until the sign-in method is decided (OD-03). Until then the API has no authentication (single local dev principal), so it must stay private.
- **Listener.** The API listens on `[::]:8080` (dual-stack). Dual-stack is required for private networking in legacy IPv6-only environments.
- **PORT=8080 rule.** Set `PORT=8080` on `ompos-api`. The API binds the `PORT` value, and the hosted value is 8080. Confirm the injected value and the listener in the deploy logs (decided at deploy time).
- **Fallback domain (telemetry only).** GreptimeDB has a public service domain, `greptimedb-production.up.railway.app` over HTTPS. Use it only if the private path fails (Q-HOST-PRIVNET). It is not an API domain.

## Service settings (dashboard; no config file)

Railway Config as Code is deprecated, and new services cannot opt into it (https://docs.railway.com/config-as-code). This repository commits no `railway.json` or `railway.toml`. Every setting below is a dashboard setting or a service variable.

| Setting | Value | Source |
|---------|-------|--------|
| Builder | Dockerfile | Previous revision of this runbook, citing https://docs.railway.com/builds/dockerfiles; not re-verified in this rewrite |
| Dockerfile path | Variable `RAILWAY_DOCKERFILE_PATH` = `deploy/api.Containerfile` | Previous revision, citing https://docs.railway.com/variables/reference; not re-verified |
| Root Directory | Repository root (default) | The Containerfile copies repo-relative paths; build context is the Root Directory |
| Health check path | `/health/ready` | Owner decision 2026-10-21 |
| Health check timeout | 120 s | Chosen value, not a platform default |
| Restart policy | On failure | Dashboard setting; exact label not re-verified |

The platform health check runs at deploy time from host `healthcheck.railway.app` (https://docs.railway.com/deployments/healthchecks, cited in the previous revision).

## Base images

| Stage | Image | Why |
|-------|-------|-----|
| Build | `mcr.microsoft.com/dotnet/sdk:10.0.402-noble` | `global.json` pins SDK `10.0.402` with `rollForward: latestPatch`. |
| Runtime | `mcr.microsoft.com/dotnet/aspnet:10.0.13-noble` | Runtime bundled with SDK 10.0.402 is 10.0.13. Noble is used because the entrypoint needs `/bin/sh` to map `PORT`. |

Both stages pass `--locked-mode` restore, then `publish --no-restore`. The final stage runs as UID/GID `10001`. Pin by digest before production (for example `sdk:10.0.402-noble@sha256:...`). No digest has been recorded, because no registry pull was performed.

The build files are named `*.Containerfile` because `RepoHygieneTests` forbids files named `Dockerfile*` or `docker-compose*`. Builds use podman only. `.containerignore` excludes `bin/`, `obj/`, `.git`, `.dotnet`, `.nuget`, `.env*`, and `.planning/`.

## Build and local smoke test (podman only)

Run from the repository root:

```sh
podman build -f deploy/api.Containerfile -t ompos-api:local .
podman run --rm -p 8080:8080 -e PORT=8080 -e ASPNETCORE_ENVIRONMENT=Development -e Harness__Adapter=InMemory -e Sessions__Store=InMemory -e Coordination__Store=InMemory ompos-api:local
curl -fsS http://127.0.0.1:8080/health/ready
```

The `Sessions__Store` and `Coordination__Store` keys default to `InMemory` in `appsettings.json`, so the smoke test does not need them. The explicit `-e` settings above only make the mode visible. The command sets `ASPNETCORE_ENVIRONMENT=Development` because Production refuses the InMemory harness adapter and any session store other than Postgres at startup (`HarnessAdapterSelectionTests`, `SessionStoreProductionTests`). Under Production, `Coordination__Store=InMemory` is not refused; it logs the InMemory warning (event 32). This smoke test has not been run in this rewrite [INFERENCE: expected to pass; not executed].

## Environment variables

Names only. Config keys use `:` in code and `__` in the environment. Values never go in this file or in the repository. Mark secret variables as secrets in Railway.

Reference variables use the shape `${{SERVICE.VARIABLE}}`, resolved by Railway at deploy time. The `PGHOST` reference is from the owner decision. The other Postgres reference names are the standard template names [INFERENCE: confirm on the TimescaleDB Variables tab].

### `ompos-api`

| Variable | Hosted value (shape) | Required | Notes |
|----------|----------------------|----------|-------|
| `PORT` | `8080` | Yes | Rule in "Network and ports". |
| `ASPNETCORE_ENVIRONMENT` | `Production` (set by image) | No | Never `Development`. |
| `Sessions__Store` | `Postgres` | Yes, explicit in production | `Postgres` or `InMemory`. Defaults to `InMemory` in `appsettings.json`; production must set it explicitly. Production refuses any value other than `Postgres` at startup, and the startup error names `Sessions:Store` (`SessionStoreProductionTests`). |
| `Sessions__Postgres__ConnectionString` | `Host=${{TimescaleDB.PGHOST}};Port=${{TimescaleDB.PGPORT}};Database=${{TimescaleDB.PGDATABASE}};Username=${{TimescaleDB.PGUSER}};Password=${{TimescaleDB.PGPASSWORD}};Ssl Mode=Require;Trust Server Certificate=true` | Yes, secret | Npgsql. `Trust Server Certificate` is needed because the template's TLS certificate is self-signed on the private network. |
| `Coordination__Store` | `Dragonfly` | Yes, explicit in production | `Dragonfly` or `InMemory`. Defaults to `InMemory` in `appsettings.json`; production must set it explicitly. A warning is logged in Production when it is `InMemory` (event 32). |
| `Coordination__Dragonfly__ConnectionString` | `dragonfly.railway.internal:6379,password=${{Dragonfly.DFLY_requirepass}},abortConnect=false` | Yes, secret | StackExchange.Redis format. `abortConnect=false` is required. [INFERENCE: the Dragonfly reference name `Dragonfly`; the private host is lowercase in DNS.] |
| `Otel__Exporter` | `otlp` | No | `none` (default), `console`, or `otlp`. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://greptimedb.railway.internal:4000/v1/otlp` | Yes when `otlp` | Read by the .NET OpenTelemetry SDK. The private path is unverified (Q-HOST-PRIVNET). |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `http/protobuf` | Yes when `otlp` | |
| `OTEL_EXPORTER_OTLP_HEADERS` | `Authorization=<Basic credential>,X-Greptime-DB-Name=public,X-Greptime-Pipeline-Name=greptime_trace_v1` | Yes, secret | The only OTLP header variable. Set it in the hosting environment; the Authorization value is never committed. Traces are rejected with `400 Pipeline is required for this API` without the pipeline header, so do not use `OTEL_EXPORTER_OTLP_TRACES_HEADERS` (it was not applied in the local smoke). See [smoke evidence, finding 1](evidence/smoke-backend-local-2026-10-21.md). `X-Greptime-DB-Name=public` comes from the 2026-10-21 orchestrator decision; the smoke file records only the pipeline header [INFERENCE]. |
| `Harness__Adapter` | `Acp` | Yes | Production refuses `InMemory` at startup (`HarnessAdapterSelectionTests`). The hosted value is `Acp`. The ACP adapter needs an omp runtime: the API image now contains one (`/usr/local/bin/omp`, see "Known blockers and gaps" item 2). No model credential is deployed (HOST-04 open). |
| `OmpOS__Debug__LogPrompts` | `false` | No | `true` writes prompt bodies to logs. Keep `false` when hosted. |
| `Api__Cors__Origins__0` (and `__1`, ...) | Unset | Owner decision | An empty list denies all cross-origin traffic. Set only after the Vercel origin mechanism is decided (Q-VERCEL-CSP). |
| `Api__MaxPromptLength` | Default `64000` | No | |
| `Api__MaxRequestBodyBytes` | Default `1048576` | No | |
| `Api__RateLimit__PromptsPerMinute` | Default `60` | No | |
| `Api__RateLimit__SessionsPerMinute` | Default `60` | No | |

Removed: every `Workflows__*` variable (`Workflows__Enabled`, `Workflows__Address`, `Workflows__Namespace`, `Workflows__TaskQueue`, `Workflows__ApiKey`, `Workflows__Tls`). The Temporal configuration is gone. Do not set any of them.

The TimescaleDB, Dragonfly, and GreptimeDB variables belong to their own services. The Dragonfly password lives in the variable `DFLY_requirepass` on the Dragonfly service. Read it through the reference shown above; never copy it.

## Deploy steps

Each step says whether it has been verified. Steps not yet verified are marked "decided at deploy time".

1. **Check the existing services.** Confirm TimescaleDB, Dragonfly, and GreptimeDB are running in `Ultrathink` / `production`. Read their private hosts and ports from their Variables tabs. *Decided at deploy time.*
2. **Create `ompos-api`** from this repository, with Root Directory at the repository root and Dockerfile path `deploy/api.Containerfile`. Do not generate a domain. *Not yet performed.*
3. **Set the variables** from the table above, with secrets marked as secrets and references used for every connection string. *Decided at deploy time.*
4. **Set the health check** path to `/health/ready` and the timeout to 120 s. *Not yet performed.*
5. **Deploy and read the logs.** Confirm the listener is `[::]:8080` and that the Postgres schema and hypertable are present. The DDL has not been executed (Q-DATA-TSDB-VERSION). *Decided at deploy time.*
6. **Check health.** `/health/ready` and `/health/live` return 200. *Decided at deploy time.*
7. **Check the private paths.** From `ompos-api`, confirm Dragonfly and GreptimeDB are reachable over the private network (Q-HOST-PRIVNET). If GreptimeDB fails, the fallback is the public HTTPS domain; that choice is not yet made. *Decided at deploy time.*
8. **Smoke-test a session.** A prompt returns 202, the SSE stream completes, and the run lease is released. This runs on the InMemory harness and `FakeSandboxProvider`, so it proves plumbing only. *Decided at deploy time.*
9. **Confirm the topology.** No worker service exists, and `ompos-api` has no public domain. *Decided at deploy time.*
10. **Do not** enable public exposure, add the Vercel origin, or set `Api__Cors__Origins__0` until OD-03 and the Vercel mechanism are decided.

## Owner decisions and open items

| ID | Item | Status |
|----|------|--------|
| OD-03 | Sign-in method. Blocks public exposure and any API domain. | Open (owner) |
| Q-VERCEL-CSP | Mechanism for the Vercel origin and the production API origin in CSP `connect-src`. | Open (owner) |
| Q-APPROVAL-ADAPTER | Approval adapter: ACP or RPC. | Open (owner) |
| Q-PG-RETIRE | Whether the plain Postgres service in Ultrathink is retired. | Open (owner) |
| Q-HOST-PRIVNET | Private-path reachability of GreptimeDB and Dragonfly from an IPv6-only environment. | Open (platform) |
| Q-HOST-DFLY-PIN | Dragonfly image unpinned on Railway. | Open (platform) |
| Q-HOST-GREPTIME-AUTH | Basic auth credential rotation for GreptimeDB. | Open (platform) |
| Q-DATA-TSDB-VERSION | TimescaleDB 2.13 versus a newer version for the hypertable DDL. | Open (ompos-maintainers) |
| Q-LOCAL-PARITY | Local compose gap: no TLS and no PostGIS locally. | Open (ompos-maintainers) |
| Q-HOST-RUNTIME | Real omp runtime, sandbox provider, and model credential for the hosted API (HOST-04). | Open (ompos-maintainers), narrowed. The omp runtime is in the image (pinned; "Known blockers and gaps" item 2). The sandbox provider and the model credential remain open. |

Decided, not open: worker placement is none.

## Verification

- Local smoke, 2026-10-21: [smoke-backend-local-2026-10-21.md](evidence/smoke-backend-local-2026-10-21.md). It ran the Release `OmpOS.Api` binary in Production mode against local stand-ins (TimescaleDB 2.13.1, Dragonfly v1.27.1, GreptimeDB v1.1.4). It covered health, the session and prompt flow, idempotency, lease release, reaper settlement, restart durability, and traces and metrics in GreptimeDB.
- Its finding 1 sets the OTLP header rule in the table above. Its finding 2 (a startup failure aborts with SIGABRT, exit 134) applies on Railway too.
- Not verified by the smoke: Railway private networking over IPv6, TLS on the TimescaleDB template, GreptimeDB Basic authentication, and the deployed image. Only the Railway deploy verifies these.
- Image smoke, 2026-10-21 on this host (podman 4.9.3, Ubuntu 24.04): `podman build -f deploy/api.Containerfile -t ompos-api:local .` exited 0. `podman run --rm --user 10001:10001 --entrypoint /bin/sh ompos-api:local -c 'id -u && omp --version'` printed `10001` and `omp/18.12.5`. The runbook smoke (`PORT=8080`, `ASPNETCORE_ENVIRONMENT=Development`, InMemory harness and stores; host port 18080) returned 200 for `/health/ready` and `/health/live`, and the container ran as uid 10001. This verifies the image only. The deployed image, Railway private networking and a model run remain unverified.
- Known limits KL-01 to KL-07 are listed in [open-questions.md](../open-questions.md). None is fixed by this change.

## Known blockers and gaps

1. The API requires an OIDC bearer token (OD-03, decided autonomously 2026-10-21). No identity provider authority is configured yet, so the API is not exposed publicly.
2. The hosted API is configured for the ACP harness (`Harness__Adapter=Acp`); Production refuses the in-memory adapter at startup. The API image now contains an omp runtime, installed as `/usr/local/bin/omp`: Bun `1.4.3` (`bun-linux-x64.zip`, SHA-256 `3e730528d89775f03f87f05c06787c4df4099d0a37f3f252e7b829cb23816e18`, from the release's `SHASUMS256.txt`) and `@oh-my-pi/pi-coding-agent` `18.12.5` (npm integrity `sha512-MZxW8b5bM2OMwRQzz5j0JQp6nsCXS0gHmwxSpbotWcNt6dpi1Sr1nMABjseHkPk3sFEXdGpSGJgfHmROxSASJA==`, checked before extraction). The omp package ships no lockfile, so its transitive dependencies are not frozen; `bun install` resolves them at image build time. Image build and `omp --version` as uid 10001 were verified locally on 2026-10-21 (see Verification); the deployed image is not verified. No model credential is deployed, and no sandbox provider is wired to the hosted path. HOST-04 criterion 2 still needs a deploy, OD-03, and a model credential.
3. Private-path reachability of GreptimeDB and Dragonfly is unverified (Q-HOST-PRIVNET).
4. The Dragonfly image is unpinned (Q-HOST-DFLY-PIN).
5. The API has not been deployed to Railway. The production image builds with podman locally (verified), and no `ompos-api` service exists. On 2026-10-21 Railway refused deploys as paused (`docs/runbooks/evidence/railway-ompos-api-2026-10-21.md`); that refusal did not recur on 2026-10-28, when a staged private worker was deployed in a separate project (section "Staged worker on Railway (2026-10-28)" below). The API stays blocked by item 1 (OD-03).
6. The local compose stack does not match hosted TLS or extensions (Q-LOCAL-PARITY).

## Deploy checklist (Phase 12, OD-03 gated)

Status on 2026-10-21: nothing is deployed. Railway refuses deploys for this account while they are paused, so no item below has been observed on a deployed URL. Update 2026-10-28: the refusal did not recur and a staged private worker was deployed (section "Staged worker on Railway (2026-10-28)" below); the API is still not deployed, so no item below has been observed on a deployed API URL. Each item states how it was verified.

1. **Build settings.** Set them on the service, not in a committed file: builder Dockerfile, Dockerfile path `deploy/api.Containerfile`, healthcheck path `/health/ready`. Verified: the image builds with podman (`ompos-api:local`, 244 MB, runs as uid 10001, honors `PORT`). Railway config-as-code is not used. Its own docs say it is deprecated and that new services cannot opt in (https://docs.railway.com/config-as-code, hard cutoff 2026-12-01), so no `railway.json` is committed.
2. **Variables.** Set them in the Railway service, never committed: `Api__Auth__Authority` (https), `Api__Auth__Audience`, `Harness__Adapter=Acp`, `Sessions__Store=Postgres` with `Sessions__Postgres__ConnectionString`, `Coordination__Store=Dragonfly` with `Coordination__Dragonfly__ConnectionString`, `Api__Cors__Origins__0` (the web origin), and the OTLP variables. The API refuses to start when a required value is missing, and `.env.example` documents each key.
3. **Locked CORS default.** With no `Api__Cors__Origins__*` value, cross-origin requests are denied. Verified by `ApiCorsPreflightTests` in the gate for an unlisted origin. Check the deployed URL with:
   `curl -s -o /dev/null -D - -X OPTIONS -H 'Origin: https://unlisted.example' -H 'Access-Control-Request-Method: POST' https://<api-host>/sessions`
   The response must carry no `Access-Control-Allow-Origin` header for the unlisted origin.
4. **Sign-in gate.** `GET https://<api-host>/sessions` without a token returns 401. `/health/live` and `/health/ready` answer without a token.
5. **Public exposure.** No public domain is generated for `ompos-api` until `Api__Auth__Authority` and `Api__Auth__Audience` are set (OD-03, HOST-02). Generate the domain only after items 2 and 4 pass.
6. **Not verified until a deploy exists:** `/health/live` and `/health/ready` on the deployed URL (Phase 12 criterion 2), the hosted real omp run (Phase 13 criterion 2), and the Vercel origin (Phase 14 criteria 2 and 3).

## Staged worker on Railway (2026-10-28)

After the v0.15.0 release a staged agent worker was deployed to Railway and exercised: the project `ompos-staging` (environment `staging`, service `ompos-worker`, volume `ompos-worker-data` at `/data`), private (no domain, no TCP proxy), built by Railway from the released tree with `deploy/worker.Containerfile`. The record, with object ids, the steps and the teardown steps, is [evidence/railway-ompos-worker-2026-10-28.md](evidence/railway-ompos-worker-2026-10-28.md). The refusal of 2026-10-21 (`Deploys have been paused temporarily`) did not recur that day; this does not say that Railway deploys are unrestricted. The service stays up and is billed at Railway usage rates; whether to keep it is OQ-171. It is not the `ompos-api` service of this runbook and it sits in a separate project from `Ultrathink`.

What it proves:

- The worker image builds on Railway's builder from the repository with the committed Containerfile and lockfiles; the entrypoint copes with a root-owned Railway volume and binds `[::]:$PORT`; Railway's health check accepts `/healthz`; the JWT gate and the omp bridge work on Railway's runtime as they did under podman ([evidence](evidence/cloud-worker-container-2026-10-28/README.md)).

What it does not prove:

- The control plane on Railway (the API is not deployed: production mode needs an identity provider, OD-03, and the substrate databases of the `Ultrathink` project were not touched).
- That the control plane mints tokens the worker accepts (a script played that role).
- A prompt turn (no model credential, OD-07).
- A git checkout, branch or pull request from the worker (the worker bootstrap is not built).
- Hooks in the worker.
- Sleep and wake of the service (serverless mode is off).
- The private network between two services (there is one service).
- Any remote desktop or browser streaming (OQ-73 is untouched: the worker has none).
