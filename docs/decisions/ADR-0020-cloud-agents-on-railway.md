# ADR-0020: Cloud agents on Railway

- Status: Accepted 2026-10-27
- Date: 2026-10-27
- Owner: ompos-maintainers
- Deciders: ompos-maintainers
- Scope: how an omp session runs in a Railway container for a cloud agent (requirement PX-17, milestone v0.15.0, Phase 60): the execution tiers, the run contract, the worker bridge protocol, authentication, isolation, the idle model, secrets and what is blocked. The research is [09-remote-cloud-plan.md](../plans/v0.15.0-research/09-remote-cloud-plan.md) (sections D, G, H, I, J). The control plane in the API (units U16 and U18 to U22) is the next wave and is not part of this record's implementation. Remote environments over ssh are a separate record.
- Defaults taken 2026-10-27, both non-blocking: OQ-97 (the worker bridges `omp acp` to a WebSocket with a bearer JWT of at most 15 minutes) and OQ-100 (private networking, no public domain for an unauthenticated API, no browser or desktop streaming).

## Context

omp has no session server: `omp acp` speaks ACP over stdio only (OQ-75, `omp://cli-reference.md`). A cloud agent therefore needs an OmpOS-owned process next to omp. Railway constrains the design: a service sleeps implicitly after about 5 to 10 minutes without outbound packets and wakes on inbound traffic, the first request after sleep may return 502 ([Serverless](https://docs.railway.com/deployments/serverless.md)); volumes are root-owned ([Volumes](https://docs.railway.com/volumes.md)); every service of an environment reaches every other over the private network ([Private networking](https://docs.railway.com/networking/private-networking/how-it-works.md)); WebSockets are exempt from the HTTP duration limits ([Specs and limits](https://docs.railway.com/networking/public-networking/specs-and-limits.md)). The existing code already gives one `omp acp` child per session (ADR-0012 D5), an `IAcpTransport` seam, `CloudSessionHost` over an `ICloudEnvironmentClient` and a hosted run contract (`POST /sessions/{id}/runs`, contract 1.2.0).

## Decision

### D1. Execution tiers

| Tier | What runs omp | Built by this record | Live status |
| --- | --- | --- | --- |
| T0 in-API | omp child inside the API container (exists) | no change | blocked: Railway deploys paused, OD-03, HOST-04 |
| T1 pooled workers | a few always-warm worker services, each with one volume; a session is a directory on that volume | yes: `StaticPoolEnvironmentClient` | blocked, same reasons; also the route for the first live run on one hand-made worker |
| T2 service per session | one Railway service plus one volume per session, Serverless on (owner decision 2026-10-22) | yes: `RailwayCloudEnvironmentClient` over the Railway GraphQL API, tested against recorded fixtures | blocked: no live schema capture, no deploy |
| T3 Railway Sandbox | VM from a template, network ISOLATED, checkpoints | no | needs introspection of `sandboxCreate` (Q-SB-RAILWAY); `RailwaySwarmProvider` stays fail-closed |

All tiers sit behind `ICloudEnvironmentClient`, so the API, the heads and the tests do not change between tiers. The tier is chosen by configuration.

### D2. Run contract

A cloud agent is one container plus one volume plus one worker. The control plane (next wave) creates it through `ICloudEnvironmentClient`, mints a worker token for the session, and opens an `AcpWebSocketTransport` from a `CloudWorkerHarnessSessionFactory`, which plugs into the existing `transportFactory` seam of `AcpHarnessSessionFactory`. Nothing in `AcpAgentSession`, the heads or the run endpoints changes. `POST /sessions/{id}/runs` stays the headless entry point; for a cloud agent only the harness factory differs. Run records must become durable before cloud runs are advertised (OQ-70).

### D3. Worker bridge protocol

`OmpOS.AgentWorker` (ASP.NET Core, Kestrel) exposes three routes.

- `GET /acp`: WebSocket upgrade. One JSON-RPC message per WebSocket text frame in both directions, `initialize` first, the ACP framing of the streamable WebSocket RFD (https://agentclientprotocol.com/rfds/streamable-http-websocket-transport, status Active, not stable; the page was not re-read in this pass, the framing comes from Q-HARN-WS). Binary frames are ignored. Every frame must be exactly one JSON object; line breaks between tokens are removed before the frame is written to omp's line-delimited stdio. The worker spawns `omp acp` through `AcpStdioTransport` (the harness process abstraction), in `<Worker:WorkspaceRoot>/<session id>`, and pumps stdout lines to frames and frames to stdin lines.
- `GET /healthz`: anonymous, 200 `ok`; the wake target of the control plane.
- `GET /readyz`: anonymous, 200 or 503 with a fixed reason; reads the local file system only.

Limits and codes:

- Frame cap 1 MiB (1,048,576 bytes) each way. A larger client frame closes with 1009. A line from omp over 1 MiB is refused by the stdio reader and closes with 1011.
- Keep-alive control frames every `Worker:PingInterval` (default 20 s) while a client is attached, from the server and from `AcpWebSocketTransport`. The pump of the client reads continuously so control frames are answered even when no caller is receiving.
- Close codes: 1000 client closed or connection lost; 1001 worker shutting down (the bridge sends it on `ApplicationStopping`, then kills the omp tree); 1007 a client frame is not one JSON object; 1009 frame over 1 MiB; 1011 omp exited, could not be started mid-session, or wrote a malformed or oversized line (the close reason carries `omp acp exited with code N.`).
- Errors before the upgrade are plain HTTP: 400 not a WebSocket request, 401 token missing or invalid, 403 subject not served, 409 the session already has an attached connection, 503 omp or the session directory could not be started.
- One omp child per session subject; a second connection for the same subject is refused with 409 until the first is gone. Closing the socket closes omp's stdin and kills the whole process tree if omp does not exit within the 2 second grace period of `AcpStdioTransport`.

`AcpWebSocketTransport` (`src/OmpOS.Harness.Acp`) is the client side. It replaces `WebSocketOmpTransportStub`, which is deleted with its tests. It sends the token in the `Authorization` header of the upgrade request and nowhere in the URL, calls its token provider on every connect (a reconnect presents a fresh 15 minute token), and maps faults to the exceptions `AcpStdioTransport` raises: a failed connect (including a refused upgrade, which names the HTTP status) is `ProcessStartHarnessException`; a dropped connection or a close from the worker is `DisconnectedHarnessException` carrying the close code and reason; an undecodable or oversized frame is `MalformedFrameHarnessException`. A cancelled receive does not abort the connection. Reconnect is not part of the transport: a new transport reconnects and the session reloads with `session/load` on the same volume (omp sessions live under `HOME/.omp` on the volume).

### D4. Authentication

This decides the WebSocket upgrade authentication that Q-HARN-WS left open. The control plane signs a token per session with a key only it holds; the worker holds only public keys.

- Header `Authorization: Bearer <JWT>` on the upgrade request. The scheme reads the header only, never a query string.
- Algorithms ES256 or RS256 only (an allow-list; `none` and HMAC are refused, which defeats algorithm confusion). Public keys are PEM (`Worker:Auth:PublicKeysPem:N`, several for rotation); a private key or an RSA key under 2048 bits stops startup.
- Claims: `iss` equal to `Worker:Auth:Issuer` (default `ompos-api`), `aud` equal to `ompos-worker`, `sub` equal to the session id, `iat` and `exp` both present.
- Lifetime: `exp - iat` at most 15 minutes (`Worker:Auth:MaxTokenLifetime`, which may not be set above 15 minutes); `iat` not in the future and `exp` not in the past beyond a 30 second skew (`Worker:Auth:ClockSkew`, at most 2 minutes). Time comes from the injected `TimeProvider`.
- The subject is the session directory name, so it must be 1 to 64 characters of `[A-Za-z0-9._-]` starting with a letter or digit, else 403. A service-per-session worker pins its session with `Worker:SessionId`; any other subject is 403.
- A token authorises a connection only; it is not renewed on an open socket. A long session reconnects with a fresh token, which is why reconnect plus `session/load` is the recovery path.

### D5. Isolation

Railway's private network is flat (D5 of the research): every service of an environment reaches every other. Option P (workers in the API's environment) is acceptable for a single owner. Before a second user exists, workers go into a separate Railway project (`ompos-agents`, option S): the API reaches them at `wss://<service domain>/acp` with the per-session JWT, and the workers have no route to the databases. A worker public domain accepts only API-signed tokens but is created only after the API gate (OD-03) passes. The worker image runs omp as uid 10001 after an entrypoint repaired the volume; no worker holds a Railway token, a database password or the signing key. Railway offers no per-service egress allowlist (D12), so egress is unrestricted: a documented parity gap with Cursor, mitigated by omp tool approvals and by giving a worker only the secrets of its session. Running other people's agents on one Railway account may count as reselling compute under the acceptable use policy (D15); until Railway answers (K3) the scope is single owner or a per-user template deploy.

### D6. Idle model

Railway sleeps a service implicitly and no sleep command exists in the documentation read. `ICloudEnvironmentClient` therefore has `RequestIdleAsync` and `EnsureAwakeAsync` instead of explicit sleep and wake.

- Logical idle is decided by OmpOS (no run, no attached client). The control plane closes the WebSocket (pings stop) and calls `RequestIdleAsync`, which records the intent and reports `Sleeping` when the provider shows the deployment as `SLEEPING` and `Idle` otherwise; it never claims that the container sleeps.
- The worker emits no telemetry, opens no database connection, makes no outbound call and writes no log line for `/healthz` and `/readyz`, so Railway's own detection can sleep it 5 to 10 minutes after the last outbound packet. Whether an idle open WebSocket without frames keeps a Serverless service awake is not stated by Railway (K3 e): the control plane closes the socket first.
- `EnsureAwakeAsync` sends `GET /healthz` and retries 502, 503, 504 and connection failures every 2 seconds for up to 60 seconds measured with the injected `TimeProvider`; then the omp preflight and `session/load`. Process state is lost across a sleep and `CloudSessionHost` says so in its resume marker.
- Serverless takes effect only when the container is created, so the service is created with `sleepApplication` set and deployed once afterwards.
- A documented `deploymentStop` mutation exists (https://docs.railway.com/integrations/api/manage-deployments.md). It stops a deployment, which is not the same as Railway's sleep and is woken only by a restart; it is not used. Whether a stopped service keeps its volume and bills nothing is UNVERIFIED: a written Railway answer (K3 f) or introspection decides.
- Default logical idle is 15 minutes (K5). The 24 hour value in `CloudSessionHostOptions` stays the library default until the control plane sets its own.

### D7. Secrets and redaction

Per-session secrets (provider keys, git credentials) are injected by the control plane into the omp process environment through Railway service variables of that session's service only, never into shared services and never onto the volume. The Railway client treats every variable value as a secret: values are never logged, never put in an exception message, and are replaced by `[REDACTED]` in any server text echoed into an error. The worker never writes a secret to argv (`--config` overlays and the environment carry them). Its stderr pump logs omp's stderr lines through a redactor that replaces the value of every environment variable whose name contains KEY, TOKEN, SECRET, PASSWORD, PASSPHRASE, CREDENTIAL or PEM, and the bearer token of every attached connection. Canary tests assert that no secret appears in a log line or in argv. Run output and transcript redaction through `PromptRedactor` belongs to unit U19.

### D8. Railway client and what is verified

Endpoint `https://backboard.railway.com/graphql/v2`, `Authorization: Bearer <token>` (https://docs.railway.com/integrations/api.md). Errors follow the GraphQL convention: a 200 with an `errors` array is a failure and an authorization denial is a 200 with `Not Authorized`; 429 carries `Retry-After`; mutations are not retried once a response arrived. Operation names and inputs below were read in the Railway API guides on 2026-10-27 (the examples, not the schema); anything marked UNVERIFIED needs introspection, which the orchestrator captures later.

| Operation | Use | State |
| --- | --- | --- |
| `project(id){services{edges{node{id name}}} volumes{...}}` | find a session's service and volume by label | VERIFIED (manage-services.md, manage-volumes.md) |
| `serviceCreate(input{projectId,name,source{image},variables})` | create the worker service | VERIFIED; `input.environmentId` UNVERIFIED and not sent |
| `variableCollectionUpsert(input{projectId,environmentId,serviceId,variables,skipDeploys})` | write variables (values write-only) | VERIFIED (manage-variables.md); return type UNVERIFIED |
| `serviceInstanceUpdate(serviceId,environmentId,input{sleepApplication,healthcheckPath,restartPolicyType,numReplicas})` | Serverless and health check | VERIFIED (manage-services.md); return type UNVERIFIED |
| `volumeCreate(input{projectId,serviceId,mountPath,environmentId})`, `volumeUpdate`, `volumeInstanceUpdate`, `volumeDelete` | the session volume, created attached | VERIFIED (manage-volumes.md); per-environment instance lookup UNVERIFIED |
| `serviceInstanceDeployV2`, `serviceInstanceRedeploy` | start a deployment after the settings | VERIFIED (manage-services.md); whether DeployV2 starts an image-sourced service UNVERIFIED |
| `deployments(input{projectId,serviceId,environmentId},first)` | status for phase and wake | VERIFIED (manage-deployments.md); newest-first order and the statuses INITIALIZING, REMOVING, NEEDS_APPROVAL UNVERIFIED |
| `serviceDelete(id)` | delete a service | VERIFIED, also in code since Q-SB-RAILWAY |

The service for a session is named `ompos-agent-<sanitised session id>` (a short hash is appended when the id had to be altered), which makes create idempotent: a second create adopts the existing service. The worker address is `http://<service name>.railway.internal:8080/`; that private DNS name equals the service name is UNVERIFIED.

## Blocked, and by what

- Live deploy of API, workers or any Railway object: Railway deploys paused (hosting Known blocker 5) and the written Railway answers (OQ-73, K3).
- Public worker domain and any public API domain: OD-03 (identity provider).
- Cloud runs with a model: HOST-04 and OQ-71 (credential strategy).
- Browser or desktop streaming from a cloud agent: OQ-73, paused.
- T3 Railway Sandbox: Q-SB-RAILWAY (`sandboxCreate` inputs).
- Live GraphQL schema capture and the UNVERIFIED rows above: an API token and an introspection query run by the orchestrator.
- Volume files over the Railway API: there is no file endpoint; `RailwayCloudEnvironmentClient` and `StaticPoolEnvironmentClient` throw `Unsupported` from `AppendVolumeFileAsync` and `ReadVolumeFileAsync`. The control plane keeps the transcript in its own store, or a worker endpoint is added (proposed OQ).
- Whether an idle open WebSocket keeps a Serverless service awake, the maximum services per project and whether one volume attaches to several services: K3 questions to Railway.

## Consequences

- The worker image needs git, openssh-client, bun and omp (`deploy/worker.Containerfile`); it starts as root only so the entrypoint can repair the Railway volume ownership and then drops to uid 10001 with `setpriv` (alternative: `RAILWAY_RUN_UID=0`).
- `WebSocketOmpTransportStub` and its tests are gone; Q-HARN-WS is answered for the OmpOS-owned bridge (framing, upgrade authentication, close codes); omp itself still has no remote endpoint.
- The hosting runbook line "worker placement is none" (2026-10-21) is superseded by the owner direction of 2026-10-27 and must be amended by the documentation unit.
- A new project `services/OmpOS.AgentWorker` and its test project need registration in the solution, the layer map and the architecture gates.

## Candidate chosen

An OmpOS-owned worker service (`services/OmpOS.AgentWorker`) that bridges one WebSocket to one `omp acp` child, reached by `AcpWebSocketTransport` and created through a hand-written Railway GraphQL client (`RailwayCloudEnvironmentClient`) or a static pool (`StaticPoolEnvironmentClient`). Decisions D1 to D8 above give the protocol, authentication, isolation and the verified Railway operations.

## Licence

| Component | SPDX | Source | Verified |
| --- | --- | --- | --- |
| `OmpOS.AgentWorker`, `AcpWebSocketTransport`, `OmpOS.Sandbox` v2 | AGPL-3.0-or-later | https://www.gnu.org/licenses/agpl-3.0.html | OmpOS-owned code, the project licence of milestone v0.15.0 (ADR-0018) |
| `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.13 (existing central package) | MIT | https://github.com/dotnet/aspnetcore/blob/main/LICENSE.txt | Already in `Directory.Packages.props` |
| Railway public GraphQL API (a hosted service, no code is taken) | not applicable (terms of service) | https://docs.railway.com/integrations/api | Read by the unit that wrote the client on 2026-10-27 |

## Verification route

`dotnet test --project tests/OmpOS.AgentWorker.Tests -c Release --no-build`, `dotnet test --project tests/OmpOS.Sandbox.Tests -c Release --no-build` and `dotnet test --project tests/OmpOS.Harness.Acp.Tests -c Release --no-build` run the worker, the client with recorded fixtures and the transport against a test WebSocket server. The image is built with `podman build -f deploy/worker.Containerfile -t ompos-agent-worker:local .`. The UNVERIFIED Railway rows need a schema introspection run with an API token (see the blocked list above).

## Open risks

- The UNVERIFIED rows of D8 (return types, `serviceCreate` environment input, deployment statuses, private DNS name) can differ from the live schema.
- Railway has no file API, so a cloud session's transcript cannot be read from the volume; the control plane must keep it.
- Whether an idle open WebSocket keeps a Serverless service awake is a question to Railway (K3).
- The `BootGeneration` counter is lost when the control plane restarts; the resume marker says state is not preserved.
- The worker image was not built by the implementation unit; `podman build` is the first real check.
