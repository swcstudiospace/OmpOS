---
description: "STRIDE threat model for the single-operator hosted Autonogrammer deployment: trust boundaries, controls that exist, controls planned in program 66, and the residual risks."
---

# Hosted threat model

Status: Draft (written from the code at the integration commit of the rename; nothing is deployed, so no control below was observed on a deployed system)

Review date: 2026-10-29

Owner role: deployment owner (the single operator of the hosted API); security review by autonogrammer-maintainers

Applies to: the hosted API (`services/Autonogrammer.Api`) on Railway with the static web head on Vercel, as designed in [Hosting](../runbooks/hosting.md)

## Purpose

This page lists what can go wrong at each trust boundary of the hosted deployment, which control answers each threat, and which risks stay. It is the input for the go/no-go decision to expose the API on a public domain. It is not a penetration test and not a security review.

## Scope

In scope: one operator, one Railway environment, the API, its Postgres (TimescaleDB) and Dragonfly services, the `omp acp` child process the API starts for every prompt, the browser web head on Vercel, and the secrets that sit in the service environment.

Out of scope:

- Cloud agents (`Cloud:Enabled=false` by default, see [Cloud agents on Railway](../runbooks/cloud-agents.md)) and automations (`Automations:Enabled=false` by default, including the anonymous webhook route `POST /hooks/automations/{id}`). Enabling either needs its own model: they add a Railway API token, GitHub credentials and a public webhook receiver.
- Multi-user or multi-tenant use. The program ships one operator identity (the owner token). There is no per-user credential store.
- The desktop heads, local runs and the Demo build of the web head (static, no backend, no secret; see [Web head runbook](../runbooks/web-demo.md)).
- The browser-held provider key ([Web key threat model](../runbooks/web-key-threat-model.md)).

## Status vocabulary

| Status | Meaning |
|--------|---------|
| Existing | In the tree at the base commit; the evidence column names the file. Not exercised on a deployment. |
| Planned (unit X) | Delivered by program 66 unit X under the fixed option names. Nothing in this row has been compiled, merged or observed when this page was written. After the merge and the unit's tests, the owner of this page changes the row to "Implemented in this program" with the test class as evidence. |
| Accepted risk | No control; the operator accepts the risk knowingly. |
| Open | No control and no decision yet. Listed in "Questions for the orchestrator". |

## What is built, and what the program adds

| Item | State | Evidence |
|------|-------|----------|
| Authentication | Existing: OIDC bearer (`Api:Auth:Authority`, `Api:Auth:Audience`), required outside Development. No identity provider is chosen (OD-03), so a production API cannot start or be used today. Planned (unit api-auth): `Api:Auth:Mode` = `Oidc` (default) or `OwnerToken`, `Api:Auth:OwnerToken:Sha256` (1 to 3 lowercase-hex SHA-256 digests), `Api:Auth:OwnerToken:Subject` (default `owner`), anonymous `GET /auth/config` | `services/Autonogrammer.Api/Options/ApiAuthOptionsValidator.cs`, `services/Autonogrammer.Api/Program.cs` lines 80 to 89 |
| Fallback authorization | Existing: every endpoint requires an authenticated user unless it opts out (`/health/live`, `/health/ready`, `/source`, the Development-only OpenAPI and Scalar pages, the automation webhook) | `Program.cs` lines 89, 256 to 275; `Endpoints/SourceEndpoints.cs` |
| Approval policy | Existing: `AcpApprovalPolicy.DenyAll` is the default of `AcpClientOptions.ApprovalPolicy`; a permission request is granted only when its tool kind is allow-listed and the store-backed resolver receives an allow decision | `src/Autonogrammer.Harness.Acp/AcpClientOptions.cs` line 34, `AcpApprovalPolicy.cs`, `services/Autonogrammer.Api/Harness/SessionPermissionResolver.cs` |
| Engine environment | Existing: the `omp acp` child inherits the whole environment of the API. Planned (unit engine-isolation): `Harness:Acp:Environment:Mode` (`Inherit` or `Allowlist`), `Harness:Acp:WorkspaceRoot`, a non-dumpable API process (`ProcessHardening.Apply`) | `src/Autonogrammer.Harness.Acp/AcpStdioTransport.cs` lines 235 to 239 |
| Sandbox | Existing: `FakeSandboxProvider` (in-memory fake) is the registered `ISandboxProvider`. Nothing isolates the engine | `Program.cs` line 130 |
| Rate limits | Existing: one global token bucket for prompts and one for session creation (`Api:RateLimit:PromptsPerMinute`, `Api:RateLimit:SessionsPerMinute`, default 60 each); the partition key is a constant. Planned (unit api-auth): per-subject partitions | `services/Autonogrammer.Api/Security/SessionRateLimiterPolicy.cs` lines 36 to 38 |
| Transport and headers | Existing: none beyond Railway's TLS termination. Planned (unit api-hosting): `Hosting:*` (forwarded headers, HSTS, security headers, request timeouts, SSE heartbeat) | `services/Autonogrammer.Api/Program.cs` (no forwarded-headers or HSTS call) |
| Error bodies | Existing: RFC 9457 ProblemDetails; a 500 carries the fixed detail "An unexpected error occurred."; the developer exception page is Development-only | `Program.cs` lines 168 to 181, 233 to 238 |
| Prompt logging | Existing: `Autonogrammer:Debug:LogPrompts` is `false`; prompts are logged as `<redacted len=N>` | `Options/DebugOptions.cs`, `appsettings.json` |
| Container | Existing: runs as uid 10001 (`USER 10001:10001`), no shell login, listens on `[::]:$PORT` | `deploy/api.Containerfile` lines 83 to 84, 113, 120 |

## Trust boundaries

| ID | Boundary | Crosses |
|----|----------|---------|
| TB1 | Browser and the Vercel static host | Blazor WebAssembly bundle, Content-Security-Policy, the owner token typed by the operator |
| TB2 | Browser and the API over the public Railway domain | HTTPS requests, CORS, bearer owner token, SSE streams |
| TB3 | API and Postgres or Dragonfly over the Railway private network | Connection strings, session log, run leases, idempotency records |
| TB4 | API and the `omp acp` child | Process environment, stdio, permission requests |
| TB5 | Engine tools and the filesystem and network | Everything the agent can read, write or call as uid 10001 |
| TB6 | Railway edge and private network | TLS termination, proxy headers, reachability between services of one environment |
| TB7 | Secrets in the service environment and on volumes | Railway variables, `~/.omp` credentials, the workspace volume |
| TB8 | Build and supply chain | Containerfile, pinned Bun and omp, NuGet locked restore, Vercel installer |

## TB1: browser and Vercel static host

| ID | STRIDE | Threat | Control | Status | Evidence |
|----|--------|--------|---------|--------|----------|
| T1.1 | Spoofing | A look-alike page collects the owner token the operator types | None technical. The operator uses the one bookmarked origin; the token is typed once per page session | Accepted risk | No file; operator procedure |
| T1.2 | Tampering | A modified bundle is served (Vercel account or build compromise) | The Vercel build installs a pinned .NET SDK and checks its SHA-512 before extraction; `dotnet restore --locked-mode` is used for the API image; deployments are immutable and rollback is a promote of a previous deployment | Existing | `vercel.ts` lines 29 to 33, `deploy/api.Containerfile` |
| T1.3 | Tampering | Script injection (XSS) reads the in-memory token and calls the API | CSP `default-src 'self'`, `script-src 'self' 'wasm-unsafe-eval'`, `object-src 'none'`, `frame-ancestors 'none'`, `connect-src` limited to the one API origin; no web-storage use for credentials (a build-failing architecture test). `style-src 'unsafe-inline'` stays | Existing | `vercel.ts` lines 16 to 25, `scripts/vercel/render-csp.sh`, [Web key threat model](../runbooks/web-key-threat-model.md) |
| T1.4 | Information disclosure | The token persists in the browser and survives a closed tab | The web head keeps credentials in memory only; no `localStorage`, `sessionStorage`, IndexedDB or cookie | Planned (unit web-shell) | Repository rule for the unit; the unit's tests are the evidence |
| T1.5 | Information disclosure | A page that renders agent output sends data to a remote image URL | CSP `img-src 'self' data:` blocks remote images | Existing | `vercel.ts` line 21 |
| T1.6 | Repudiation | No record of which browser used the token | None. One operator, one subject | Accepted risk | n/a |
| T1.7 | Denial of service | Vercel outage makes the UI unavailable | The API is unaffected; the operator can call it with a client of choice | Accepted risk | n/a |
| T1.8 | Elevation of privilege | Not applicable: the static host holds no secret and runs no server code | n/a | n/a | `docs/runbooks/web-demo.md` |

## TB2: browser and API (public domain)

| ID | STRIDE | Threat | Control | Status | Evidence |
|----|--------|--------|---------|--------|----------|
| T2.1 | Spoofing | An attacker guesses or replays the owner token | Token of at least 32 random bytes, verified server-side against a configured SHA-256 digest, never read from a query string; `Api:Auth:Mode=OwnerToken`, `Api:Auth:OwnerToken:Sha256`, `Api:Auth:OwnerToken:Subject`; digest list of 1 to 3 entries allows rotation | Planned (unit api-auth) | Contract C2; tests under `tests/Autonogrammer.Api.IntegrationTests/Security/` |
| T2.2 | Spoofing | Brute force of the token | Per-subject and failure-based rate limits ahead of the endpoints | Planned (unit api-auth) | Contract C3 pipeline order |
| T2.3 | Spoofing | The development principal is enabled in production | `Api:Auth:AllowDevelopmentPrincipal` is refused outside Development at startup | Existing | `Options/ApiAuthOptionsValidator.cs` |
| T2.4 | Spoofing | Cross-site pages call the API with the operator's token | The token is a header, not a cookie, so the browser attaches nothing automatically. CORS lists exact origins in `Api:Cors:Origins`; an empty list denies cross-origin traffic | Existing | `Security/ConfigureCorsOptions.cs` |
| T2.5 | Tampering | Requests altered in transit | TLS at the Railway edge. HSTS and security headers: `Hosting:Hsts:Enabled`, `Hosting:*` | Existing (TLS, platform); Planned (unit api-hosting) for HSTS and headers | Railway docs; no file for the platform |
| T2.6 | Tampering | Oversized or malformed bodies | `Api:MaxRequestBodyBytes` (default 1048576), `Api:MaxPromptLength` (default 64000), `Idempotency-Key` handling | Existing | `Options/ApiOptions.cs`, `Endpoints/SessionEndpoints.cs` |
| T2.7 | Repudiation | A change cannot be attributed or reconstructed | Every session event is appended to the session log with a sequence number; the session header carries `owner_id`; a correlation id is returned in `x-correlation-id`. With one subject the log shows what happened, not who | Existing | `PostgresSessionSchema.cs`, `Middleware/CorrelationIdMiddleware.cs` |
| T2.8 | Information disclosure | Stack traces or internals in error bodies | ProblemDetails with a fixed 500 detail; developer exception page Development-only; OpenAPI and Scalar anonymous in Development only | Existing | `Program.cs` lines 168 to 181, 233 to 264 |
| T2.9 | Information disclosure | Health or discovery endpoints reveal configuration | `/health/live` and `/health/ready` are anonymous. The default ASP.NET Core writer returns the status word only [INFERENCE: default `ResponseWriter`; not read from a response]. `GET /source` is anonymous by design (AGPL section 13): licence, source URL, revision. Planned `GET /auth/config` returns mode, authority and audience, no secret | Existing; Planned (unit api-auth) | `Program.cs` lines 265 to 275, `Endpoints/SourceEndpoints.cs` |
| T2.10 | Information disclosure | The workspace and settings endpoints read server files | Authenticated; `Api:Workspace:Owners` narrows readers; secret files and links that leave the root are refused; user scope is off unless `Api:Workspace:IncludeUserScope` | Existing | `Services/WorkspaceSettings/WorkspaceAccess.cs`, `SecretFilePolicy.cs` |
| T2.11 | Denial of service | Request flood or slow clients | One global token bucket for prompts and creation (a flood of one caller exhausts the budget of all). Planned: per-subject buckets and `Hosting:*` timeouts and connection limits | Existing (global); Planned (units api-auth, api-hosting) | `SessionRateLimiterPolicy.cs` |
| T2.12 | Denial of service | Many open SSE streams | No cap is in the code [INFERENCE: not found by search]. Planned: `Hosting:Sse:HeartbeatSeconds` keeps proxies from idling streams; connection caps are in the unit's `Hosting:Kestrel:*` options (names not fixed by the contract) | Planned (unit api-hosting) | `Endpoints/EventStreamEndpoints.cs` |
| T2.13 | Elevation of privilege | Any authenticated caller is the operator | There is one identity. Whoever holds the token can run prompts, read the workspace, and change provider settings if `Providers:AllowCredentialWrite` or `Providers:AllowConfigWrite` are on (both default off) | Accepted risk | `Services/Providers/ProvidersOptions.cs` |

## TB3: API and Postgres or Dragonfly

| ID | STRIDE | Threat | Control | Status | Evidence |
|----|--------|--------|---------|--------|----------|
| T3.1 | Spoofing | Another service of the project connects to Postgres or Dragonfly | Postgres needs the password in the connection string; Dragonfly needs `requirepass` | Existing | `docs/runbooks/hosting.md` variables table |
| T3.2 | Spoofing, Tampering | A man in the middle on the private network | Postgres: `Ssl Mode=Require;Trust Server Certificate=true` encrypts but does not verify the server certificate. The Dragonfly connection string in the hosting runbook has no `ssl` option, so that hop is plaintext [INFERENCE: from the shape of the string] | Accepted risk | `docs/runbooks/hosting.md` lines for the two connection strings |
| T3.3 | Tampering | A holder of Dragonfly access forges leases or idempotency records | Keys are namespaced `autonogrammer:`; values are not signed. Dragonfly access equals control of run admission | Accepted risk | `src/Autonogrammer.Coordination.Dragonfly/DragonflyKeys.cs` |
| T3.4 | Tampering | SQL injection | Commands use parameters (for example the advisory lock key in the schema code). This is a sample reading, not an audit [INFERENCE] | Existing | `PostgresSessionSchema.cs`, `PostgresCloudAgentSchema.cs` |
| T3.5 | Information disclosure | The API database role reads other data in the shared TimescaleDB | The hosting runbook wires the connection from the TimescaleDB service's own `PGUSER`, which is the service's owner role [INFERENCE]. The service also holds the Substrate ledger tables (ADR-0031 context). Runtime and DDL use one credential | Open | `docs/decisions/ADR-0031-data-plane-namespaces-and-resilience.md`, `docs/runbooks/hosting.md` |
| T3.6 | Information disclosure | Prompts, outputs and file diffs are in the database in clear | Stored as jsonb in `sessions.messages` and `session_events.payload`; protected only by database access and Railway volume handling. See [Data retention](../privacy/data-retention.md) | Accepted risk | `PostgresSessionSchema.cs` |
| T3.7 | Information disclosure | Idempotency replays expose a response to another caller | The key is hashed together with a scope; the record lives 24 hours | Existing | `DragonflyKeys.cs`, `src/Autonogrammer.Coordination/CoordinationDefaults.cs` |
| T3.8 | Repudiation | Event log rows altered after the fact | The store never updates or deletes an event row, but the database role can; there is no hash chain | Accepted risk | `PostgresSessionSchema.cs` remarks |
| T3.9 | Denial of service | Dragonfly or Postgres down | `/health/ready` runs the repository read and the Dragonfly PING and answers 503; startup fails fast when the stores are unreachable; the reaper settles runs whose lease lapsed. Railway's health check runs at deploy time, not continuously | Existing | `Health/SessionRepositoryCheck.cs`, `Coordination/CoordinationStartup.cs`, `Program.cs` lines 197 to 200 |
| T3.10 | Elevation of privilege | The schema step runs with a powerful role at every start | `CREATE EXTENSION IF NOT EXISTS timescaledb` needs a privileged role; the same role serves requests | Accepted risk | `PostgresSessionSchema.cs` |
| T3.11 | Information disclosure, Spoofing | The shared TimescaleDB service is reachable from the internet | On 2026-10-21 a read-only pre-check reached the service through its TCP proxy (`RAILWAY_TCP_PROXY_DOMAIN`, `RAILWAY_TCP_PROXY_PORT`). If the proxy is still enabled, the database that will hold session data is exposed to password guessing and to any weakness of the Postgres version. Whether the proxy exists now was not checked | Open | `docs/runbooks/evidence/railway-autonogrammer-api-2026-10-21.md` (Step 1) |

## TB4: API and the `omp acp` child

| ID | STRIDE | Threat | Control | Status | Evidence |
|----|--------|--------|---------|--------|----------|
| T4.1 | Information disclosure | The child inherits every API variable: Postgres and Dragonfly credentials, the OTLP `Authorization` header, provider keys | `Harness:Acp:Environment:Mode` (`Inherit` or `Allowlist`) with an allow list; the hosted value is `Allowlist`. The unit's worktree also has `Harness:Acp:AllowInheritedEnvironment` as an explicit acknowledgement [in progress, not in the contract; the default of `Mode` is not fixed by the contract] | Planned (unit engine-isolation) | `AcpStdioTransport.cs` lines 235 to 239 |
| T4.2 | Information disclosure | The child reads the API's memory or environment through `/proc` or `ptrace` (same uid) | The API process is made non-dumpable at startup (`ProcessHardening.Apply`) | Planned (unit engine-isolation) | Contract C3 |
| T4.3 | Tampering | The agent grants itself permission to run a tool | The API answers every permission request: policy deny by default, then a store-backed decision with a deny on timeout, failure or cancel | Existing | `SessionPermissionResolver.cs`, `AcpApprovalPolicy.cs` |
| T4.4 | Tampering | The agent edits the API's own files | `/app` is copied with `--chown=10001:10001`, so the run uid owns the API binaries and `appsettings.json` and the agent can change them for the life of the container; `/opt/omp` is root-owned and read-only to uid 10001 | Existing for `/opt/omp`; Open for `/app` | `deploy/api.Containerfile` lines 85 to 89 |
| T4.5 | Denial of service | A run consumes all CPU, memory or processes of the container | Railway service limits (an owner action, see `deploy/railway/README.md`); one run lease per session; no per-run limit in code | Accepted risk | `src/Autonogrammer.Coordination.Dragonfly/DragonflySessionRunLease.cs` |
| T4.6 | Elevation of privilege | The permission prompt never appears, so the deny policy never runs | Not verified: no run with a real model has happened (no model credential, OD-07). Whether omp raises `session/request_permission` for every shell and file tool in the hosted configuration is unknown | Open | `docs/runbooks/hosting.md` blockers |

## TB5: engine tools, filesystem and network

| ID | STRIDE | Threat | Control | Status | Evidence |
|----|--------|--------|---------|--------|----------|
| T5.1 | Information disclosure | The agent reads files it should not: application files, `appsettings*.json`, the omp agent directory with provider credentials, `/tmp` | Working directory confined to `Harness:Acp:WorkspaceRoot` is a start directory, not a jail: uid 10001 can still read every file that uid can read. Credentials belong in the omp directory and in the environment | Planned (unit engine-isolation) for the working directory; Accepted risk for the rest | `src/Autonogrammer.Harness.Acp/AcpClientOptions.cs` line 25 |
| T5.2 | Tampering | The agent writes outside the workspace | No filesystem sandbox. `FakeSandboxProvider` is registered, so nothing contains the engine | Accepted risk | `Program.cs` line 130 |
| T5.3 | Information disclosure | The agent calls out to the internet or to other services of the Railway project and sends data | No egress control in the code or the image. Other services of the same Railway environment are reachable on the private network [INFERENCE: Railway private networking is per environment] | Open | `docs/runbooks/hosting.md` topology |
| T5.4 | Elevation of privilege | The agent escapes to the container root | Non-root uid, no `sudo`, no published ports of its own. No seccomp or capability changes from this repository; Railway's runtime controls apply [INFERENCE] | Accepted risk | `deploy/api.Containerfile` |
| T5.5 | Denial of service | A tool call loops or forks | Container limits only | Accepted risk | n/a |

### Prompt-injection path

Untrusted text reaches the agent through the owner's prompt, repository files the agent reads, fetched web pages, issue text and tool output. The agent can then act on it. The path to harm has four steps. Each step has a control or an open gap:

| Step | What happens | Control | Status |
|------|--------------|---------|--------|
| 1. Untrusted text enters | A file or page tells the model to read and send secrets | None can filter this reliably. The owner decides what the agent reads | Accepted risk |
| 2. The agent reads secrets | The model calls a file or shell tool for the environment, `appsettings`, the omp directory | Environment allow list (`Harness:Acp:Environment:Mode=Allowlist`), non-dumpable API process, working directory `Harness:Acp:WorkspaceRoot`; approval policy deny by default stops a tool the engine asks permission for | Planned (units engine-isolation); Existing (policy) with T4.6 open |
| 3. The agent exfiltrates | A shell command or web tool sends the data out; or the output is rendered so a link or image leaks it | CSP `img-src 'self' data:` and `connect-src` in the web head stop the browser from sending it. No egress control for the engine | Existing (browser); Open (engine egress, T5.3) |
| 4. Detection | Someone notices | The session log keeps tool calls and results; no alert exists on tool use or on large outputs | Open |

What stays exposed even with every planned control: the provider key, which the engine needs in its environment or directory to call the model, and everything under the workspace. Keep the key limited in spend at the provider and rotate it after any suspicious session.

## TB6: Railway edge and private network

| ID | STRIDE | Threat | Control | Status | Evidence |
|----|--------|--------|---------|--------|----------|
| T6.1 | Spoofing | Client address and scheme are wrong behind the proxy, so limits and HSTS misbehave | `Hosting:ForwardedHeaders:Mode` (`None`, `TrustAllSingleHop`, `KnownNetworks`); the default trusts nothing | Planned (unit api-hosting) | Contract C3 |
| T6.2 | Information disclosure | An API domain is generated before authentication exists | Hosting runbook rule: no domain until sign-in is decided; with the owner token the domain may be generated only after the checks of [Deploy and rollback](../runbooks/deploy-and-rollback.md) pass | Existing (rule) | `docs/runbooks/hosting.md` |
| T6.3 | Denial of service | Volumetric attack on the public domain | Railway's platform protections only [INFERENCE]. No WAF is configured | Accepted risk | n/a |
| T6.4 | Elevation of privilege | Railway account or token compromise exposes every variable | Owner action: account two-factor sign-in and a scoped token. Not checkable from this repository | Open | n/a |
| T6.5 | Information disclosure | The API shares a Railway project with unrelated services (`Ultrathink` holds the Substrate databases and two tailscale services) | A separate project for the API (the staged worker already lives in `ompos-staging`) | Open | `docs/runbooks/evidence/railway-autonogrammer-worker-2026-10-28.md` |

## TB7: secrets in the environment and on volumes

| ID | STRIDE | Threat | Control | Status | Evidence |
|----|--------|--------|---------|--------|----------|
| T7.1 | Information disclosure | A secret appears in a log, error or option dump | The Postgres and Dragonfly startup messages name the key and never the value; prompts are redacted unless `Autonogrammer:Debug:LogPrompts` is `true`; the owner token is never accepted in a query string and only its digest is configured | Existing; Planned (unit api-auth) for the token | `ServiceCollectionExtensions.cs` (Sessions.Postgres), `Coordination/CoordinationStartup.cs`, `Options/DebugOptions.cs` |
| T7.2 | Information disclosure | Provider keys on disk in the omp agent directory | The directory sits on a volume if the owner mounts one; the API image defines no volume and no home directory for uid 10001 [INFERENCE: `useradd --no-create-home`]. Keys are stored unencrypted by omp | Accepted risk | `deploy/api.Containerfile` line 84, `docs/plugins.md` |
| T7.3 | Information disclosure | The owner token is lost or leaked | Rotation: generate, digest, set the variable, redeploy ([Incident response](../runbooks/incident-response.md)). The digest list allows two valid tokens during rotation | Planned (unit api-auth) | Contract C2 |
| T7.4 | Tampering | Provider settings are changed through the API | `Providers:AllowCredentialWrite` and `Providers:AllowConfigWrite` default to `false`; the hosted deployment keeps them off | Existing | `Services/Providers/ProvidersOptions.cs` |
| T7.5 | Information disclosure | Backups copy secrets (omp directory volume) | Restrict snapshots of the agent volume; see [Backup and restore](../runbooks/backup-and-restore.md) | Open | n/a |

## TB8: build and supply chain

| ID | STRIDE | Threat | Control | Status | Evidence |
|----|--------|--------|---------|--------|----------|
| T8.1 | Tampering | A dependency changes between builds | NuGet restore with `--locked-mode`; Bun zip and the omp tarball checked against pinned digests | Existing | `deploy/api.Containerfile` |
| T8.2 | Tampering | Transitive dependencies of omp change | The omp package ships no lockfile, so `bun install --production` resolves them at build time | Accepted risk | `deploy/api.Containerfile` comment "NOT FROZEN" |
| T8.3 | Tampering | A base image tag moves | Base images are pinned by tag, not by digest (hosting runbook: pin before production). The Dragonfly image on Railway is unpinned (`Q-HOST-DFLY-PIN`) | Open | `docs/runbooks/hosting.md` |

## Residual risks

These remain after every planned control. The operator accepts them, or the deployment does not go public.

| ID | Risk | Why it stays | Mitigation the operator can apply |
|----|------|--------------|-----------------------------------|
| R1 | No sandbox: `FakeSandboxProvider` is registered, and the engine runs inside the API container | No real sandbox provider exists for the hosted API (HOST-04 open) | Deploy the API in its own Railway project with nothing else in it; keep the volume small; keep the model key spend-limited |
| R2 | The agent runs as the API's uid (10001) | The engine is a child process of the API | Environment allow list and a non-dumpable process reduce, not remove, what the engine can read |
| R3 | Owner token compromise equals a shell on the container | One identity; the token authorizes prompts, and a prompt can run shell tools if the engine permits | Rotate on suspicion; keep `Api:Auth:OwnerToken:Sha256` at one digest outside rotation; disable the public domain during an incident |
| R4 | No per-user credential store | The program is single-operator | Do not add a second person to the deployment |
| R5 | Shared TimescaleDB and Dragonfly with other workloads | The existing services hold the Substrate data (T3.5, T6.5) | A dedicated database and role, or a separate project: owner decision |
| R6 | No egress control and no alert on tool use | Platform and product do not offer it | Review the session log after any run that read untrusted input |

## Questions for the orchestrator

| ID | Question |
|----|----------|
| Q-TM-1 | Does the API get its own Railway project, or does it join `Ultrathink` (T3.5, T6.5)? |
| Q-TM-2 | Which database role does the API use: the service owner role, or a dedicated role with only the `autonogrammer` schema (T3.5, T3.10)? |
| Q-TM-3 | Does `/app` need to be root-owned and read-only for uid 10001 (T4.4)? The Containerfile copies it with `--chown=10001:10001`. |
| Q-TM-4 | Does omp ask permission for each shell and file tool in hosted mode, and which mode does the API select (T4.6)? Needs one run with a model credential. |
| Q-TM-5 | Where does the omp agent directory live in the API container (`HOME`, `PI_CODING_AGENT_DIR`), and is it on a volume (T7.2)? |
| Q-TM-6 | Does the `TimescaleDB` service still have a public TCP proxy (T3.11), and may it be removed? |

## How verified

- Read at the integration commit: `services/Autonogrammer.Api/Program.cs`, `Options/*.cs`, `Security/*.cs`, `Health/*.cs`, `Harness/*.cs`, `Middleware/*.cs`; `src/Autonogrammer.Harness.Acp/AcpStdioTransport.cs`, `AcpApprovalPolicy.cs`, `AcpClientOptions.cs`; `src/Autonogrammer.Sessions.Postgres/*Schema.cs`; `src/Autonogrammer.Coordination.Dragonfly/DragonflyKeys.cs`; `deploy/api.Containerfile`, `deploy/compose.yaml`; `vercel.ts`, `scripts/vercel/render-csp.sh`.
- Read: [Hosting](../runbooks/hosting.md), [Cloud agents on Railway](../runbooks/cloud-agents.md), `docs/decisions/ADR-0020-cloud-agents-on-railway.md`, `docs/decisions/ADR-0031-data-plane-namespaces-and-resilience.md`, the Railway evidence files under `docs/runbooks/evidence/`.
- The contract option names of units api-auth, api-hosting and engine-isolation come from the program contract. The unit worktrees were seen in progress and uncommitted; names beyond the contract are marked as in progress.
- Not done: no deployment, no scan, no run of the code, no penetration test. Every row marked Existing was read in source, none was exercised on Railway (the API has never been deployed; the only deployed object is a private staged worker, see [evidence](../runbooks/evidence/railway-autonogrammer-worker-2026-10-28.md)).
- Review: re-read this page when any `Planned` row merges, when OD-03 is decided, and before the first public domain is generated.
