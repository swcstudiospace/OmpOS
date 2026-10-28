# R8: Dispatch relay prior art in the repo and a relay design that reuses it

Worktree `/root/src/repos/ompos-autonogrammer` (branch `feat/autonogrammer`). Read-only recon; nothing was built, run or changed except this file. Date 2026-10-28.
Legend: [FACT] read in the file cited (`path:line`). [INFERENCE] my reasoning. [EXTERNAL] not verified here. All paths repo-relative. Secret values never recorded; variable names only.

## 1. Verdict

- Nothing in the repo is a relay, a device registry, a presence service, or a signed command channel. `rg` for `DeviceId|device registry|DeviceRegistry|Dispatch` in `src services apps` finds no relay code (the only `Dispatch` hits are unrelated: `DirectSessionRunDispatcher`, `CloudHookBridgeHandler`, UI block dispatch). Open question OQ-68 (`docs/open-questions.md:105`) states "No pairing or remote code exists in src, services or apps" and ADE-08 (`.planning/REQUIREMENTS.md:211-214`) is PARTIAL: only the unauthenticated rejection is tested (`docs/quality/cursor-parity-audit-2026-10-26.md:98`).
- Roughly 80 percent of the *mechanisms* exist as parts: JWT ES256 issue/validate with lifetime rules, a 1 MiB one-JSON-object frame guard, WebSocket client with bearer provider and keep-alive, SSE with `Last-Event-ID` resume and Seq de-duplication, an append-only Seq-numbered log with cross-replica wake-ups, Dragonfly leases, shared idempotency, HMAC signature plus timestamp plus replay checks, atomic per-owner caps in Postgres, and a disabled-feature middleware pattern.
- Direction mismatch to design around: the Phase 60 bridge is API dials worker (worker is the server, `services/OmpOS.AgentWorker/Bridge/AcpBridgeEndpoint.cs:33-37`). Dispatch is the inverse (desktop dials the hub). Browsers cannot set `Authorization` on a WebSocket [EXTERNAL, platform rule], and the web head has no WebSocket code, only SSE.
- Recommended shape: a durable per-device command log plus a wake-only signal plus an SSE results stream (the `/sessions/{id}/events` pattern, with the producer and consumer roles reversed for the desktop leg), hosted in a new module `services/OmpOS.Api/Dispatch/`. Desktop leg: outbound WebSocket (reusing `AcpWebSocketTransport`). Controller leg (mobile/web): HTTPS POST plus SSE only.

## 2. Reuse map

### 2.1 Phase 42 and 43 (cloud, mobile, web terminals)

| Item | Evidence | What it gives Dispatch | Gap |
|---|---|---|---|
| Phase 42 cloud terminals runbook | `docs/runbooks/cloud-terminals.md:34-36` | Per-session container model; `CloudSessionHost` over a fake | Not a relay. Criterion 2 (`:35,42`): remote desktop and browser streaming PAUSED until Railway confirms in writing (OQ-73). No VNC/noVNC/streaming code may be built. |
| Phase 43 mobile/web terminals | `docs/runbooks/android-ios-terminals.md:41-48` | Confirms heads: Android references `Native.Views` which references `Harness.Terminal` (process spawning); web head has no terminal | Not a relay. Android device checks BLOCKED (OQ-74). |
| OQ-14 | `docs/open-questions.md:20` | Rule: never promise mobile background streams; resume by sequence on resume | Dispatch mobile must be pull/resume based. No push channel exists (audit row `cursor-parity-audit-2026-10-26.md:98`: "no notification push, SSE only"). |
| Cursor prior art | `docs/plans/v0.15.0-research/09-remote-cloud-plan.md:65` (C8 My Machines: worker opens OUTBOUND HTTPS, no inbound ports, agent loop in cloud, tools on the machine) and `02-cursor-cloud-remote.md:30,38` (CA-24 Remote Control, CA-32) | Direct design precedent for outbound-only desktops | Research only, no code. |

### 2.2 Herdr (`src/OmpOS.Harness.Herdr`)

| Item | Evidence | Reuse |
|---|---|---|
| Session lifecycle contract with `AttachAsync(sessionId, afterSequence)`, detach never stops the agent, `Lost` state, `HeartbeatsAsync` | `IHerdrSessionClient.cs:22-87`, `HerdrSessionState.cs:7-22`, `HerdrSessionSnapshot.cs:11-16` (`LastSequence`) | Borrow the *semantics*: device `hello{lastAppliedSeq}` equals attach-after-sequence; "device offline" equals `Lost`, never reported as running. |
| Heartbeat monitor: beat every 15 s, stale after 3 missed (45 s), monotonic timestamps, injected `TimeProvider` | `HerdrHeartbeatMonitor.cs:13-19,55-62` | Copy the numbers and the "silence measured on monotonic clock" rule for presence staleness. |
| Contract version const | `HerdrContract.cs:12` (`ContractVersion = 1`) | Pattern for a `v` field in the envelope plus a refuse-on-mismatch rule. |
| Persistence of every confirmed change before return | `HerdrSessionService.cs:8-12` | Same write-ahead rule for the command log. |

Herdr is a client for an external herdr server and a local store; it has no network listener and no relay role [FACT: files listed in `src/OmpOS.Harness.Herdr`]. Reuse by pattern only.

### 2.3 Phase 60 cloud agent worker bridge (the closest code)

| Item | Evidence | Reuse |
|---|---|---|
| `AcpWebSocketTransport` (client): ws/wss only, no userinfo, `Authorization: Bearer` set on upgrade from `BearerTokenProvider` called on EVERY connect, keep-alive 20 s, 1 MiB frame cap, bounded 1024-frame queue, write gate, one text frame per message, connect/close timeouts | `src/OmpOS.Harness.Acp/AcpWebSocketTransport.cs:40-45,64-74,86-141,144-170`; `AcpWebSocketTransportOptions.cs:10-35` | Use as the desktop connector transport as is (it implements `IAcpTransport`, `src/OmpOS.Harness.Abstractions/IAcpTransport.cs`: Connect/Send/Receive/Close of byte frames). Naming is ACP-flavoured and exceptions are `*HarnessException`; no reconnect inside (reconnect is the caller's job). |
| Worker bridge server: 401/403/400/409/503 before upgrade, one attached connection per subject (409 for a second), close codes 1000/1001/1007/1009/1011 | `AcpBridgeEndpoint.cs:48-98`; `AcpBridgeSession.cs:8-17` | Copy the status/close-code table and `AcceptWebSocketAsync` flow for `GET /dispatch/devices/{id}/connect`. Dispatch must choose newest-wins instead of 409 (see 4.9). |
| Frame guard: exactly one JSON object, valid UTF-8, line breaks stripped | `services/OmpOS.AgentWorker/Bridge/AcpFrameGuard.cs:17-42` | Reuse the rule for every inbound relay frame (the code is `internal` in the worker; copy or move to a shared L1/L2 assembly). |
| Worker token issuer: ES256, `iss`, `aud=ompos-worker`, `sub=sessionId`, unique `jti`, `iat`, `exp` at most 15 min, PEM key from config, never logged | `services/OmpOS.Api/Cloud/Workers/WorkerTokenIssuer.cs:10-14,18,73-92` | Template for the hub-signed per-command token and the device connection token. Use a NEW audience (do not reuse `ompos-worker`) and a NEW signing key variable. |
| Worker JWT validator: algorithm allow-list ES256/RS256, issuer+audience, `exp`/`iat` required, `exp-iat <= MaxTokenLifetime`, skew from options, injected `TimeProvider`, header-only token | `services/OmpOS.AgentWorker/Security/WorkerJwtValidator.cs:22,42-56,66-80,85-100`; `WorkerJwtBearerSetup.cs` (reads header only; query string never read) | Device-side verification of hub-signed commands: copy the parameter set. |
| Secret redaction of bearer tokens in stderr and logs | `docs/decisions/ADR-0020-cloud-agents-on-railway.md` D7 (line 77) | Same rule for relay logs: never log tokens or command bodies. |
| Side-channel frames on the same socket with an `_ompos/` method prefix | `src/OmpOS.Harness.Acp/AcpHookNotificationFrames.cs:17` (`Method = "_ompos/hook"`); `services/OmpOS.AgentWorker/Bridge/HookFramePump.cs` | Precedent for namespacing OmpOS-specific frames; relevant if a later phase tunnels ACP through the relay. |
| Railway GraphQL client | `src/OmpOS.Sandbox/RailwayGraphQlTransport.cs`, `RailwayCloudEnvironmentClient.cs`; endpoint `https://backboard.railway.com/graphql/v2` (`ADR-0020` D8, line 81) | NOT reusable for relaying. It provisions workers (service/volume/variables/deploy). Only operator-time use: set relay env vars, replicas. [INFERENCE] |
| ADR-0020 D3-D6 (protocol, auth, isolation, idle) | `docs/decisions/ADR-0020-cloud-agents-on-railway.md:31-73` | Decision text to cite and mirror for Dispatch. WebSocket is exempt from Railway duration and inactivity limits (`09-remote-cloud-plan.md:79`, D6); whether an idle socket keeps a Serverless service awake is unknown (ADR-0020 line 105). The API is not Serverless [INFERENCE], so no sleep issue for the hub. |

### 2.4 SSH remote (`src/OmpOS.Harness.Remote`)

| Item | Evidence | Reuse |
|---|---|---|
| `SshAcpTransport`: noise-tolerant stdio until first frame, then strict; `RemoteDiagnosis` typed failures | `src/OmpOS.Harness.Remote/SshAcpTransport.cs:1-60` | None for transport. The typed-diagnosis idea fits the device "why offline" reason. |
| `ReverseForwardAllocator`, `-R` forwards, `RemoteLaunchMaterializer` | `src/OmpOS.Harness.Remote/ReverseForwardAllocator.cs` | An alternative (ssh reverse tunnel) that is NOT recommended: browsers and phones cannot ssh, and it needs an ssh endpoint. |
| `EnvironmentProfile`, `EnvironmentKind {Local, Ssh, Cloud, Container, Wsl}`, `RemoteConnectionState {Disconnected, Connecting, Connected, Reconnecting, Unreachable, Closed}` | `src/OmpOS.Contracts/Environments/EnvironmentKind.cs`, `RemoteConnectionState.cs`, `EnvironmentProfile.cs:7-25` | Reuse `RemoteConnectionState` words for device status in the UI. Whether a Dispatch device is a new `EnvironmentKind` or a separate registry is a decision (section 7, D5). |
| `ReconnectingEngineHost`, `ReconnectPolicy` (1,2,4,8,16 s then Unreachable; max attempts 1-100; no jitter) | `src/OmpOS.EngineHost/ReconnectingEngineHost.cs:8-14`; `ReconnectPolicy.cs:11-40` | Reuse the schedule shape. A desktop connector wants unbounded retries with jitter and a 30 s ceiling: needs a small variant (policy bounds attempts at 100). |

### 2.5 `src/OmpOS.Coordination` (+ Dragonfly) and the API composition

| Item | Evidence | Reuse |
|---|---|---|
| `IIdempotencyStore.ClaimAsync/CompleteAsync/ReleaseAsync(scope, key, fingerprint)`; outcomes Claimed, Replay, InProgress, Conflict; pending 60 s, completed 24 h | `src/OmpOS.Coordination/IIdempotencyStore.cs:14-20`; `IdempotencyOutcome.cs:6-16`; `CoordinationDefaults.cs:13,16` | Controller command idempotency (scope = owner+device). Dragonfly key is `ompos:idem:` + SHA-256(scope+"\n"+key) (`DragonflyKeys.cs:28-32`); completion is Lua compare-and-set (`DragonflyScripts.cs:16`). |
| API idempotent endpoint flow (400 invalid key, replay stored 202, 409 `idempotency-in-progress`, 409 `idempotency-key-reused`, completion retried 3 times and never changes the response) | `services/OmpOS.Api/Idempotency/IdempotencyKeyHeader.cs:9,12,27-38` (1-128 visible ASCII); `PromptFingerprint.cs:18-23`; `IdempotencyCompletion.cs:12,28-58`; `services/OmpOS.Api/Endpoints/SessionEndpoints.cs:246-312` | Copy the flow for `POST .../commands`. Note completion is best-effort, so the command store needs its own unique key (the cloud agent store does: next row). |
| Store-level idempotency and atomic caps: unique `(owner_id, idempotency_key)` index, create plus cap check plus insert in one step | `src/OmpOS.Contracts/Cloud/ICloudAgentStore.cs:4-17`; `src/OmpOS.Sessions.Postgres/PostgresCloudAgentSchema.cs:24,28-51` (idempotent DDL under advisory lock) | Template for `dispatch_devices` and `dispatch_commands` tables, per-device queue cap and per-owner device cap. |
| Run leases: `SET NX PX`, Lua renew and delete-if-owner, TTL 30 s, renew every 10 s, owner `"{InstanceId}:{runId}"` | `ISessionRunLeases.cs:14`; `ISessionRunLease.cs:19`; `DragonflySessionRunLeases.cs` (StringSet NotExists); `DragonflyScripts.cs:10,13`; `CoordinationDefaults.cs:7,10`; `services/OmpOS.Api/Services/PromptRunner.cs:23,119`; `Workflows/LeaseRenewal.cs` | Device presence lease and "driver" lease. The key is hard-wired `ompos:lease:run:{sessionId}` (`DragonflyKeys.cs:22-25`); add a sibling `Presence` key rather than abusing run-lease ids. |
| `ISessionSignals`: wake-only, payload is a session id string, Dragonfly channel `ompos:signals`, per-subscription inbox 256 drop-oldest, publish failures non-fatal | `ISessionSignals.cs:12,18`; `DragonflySessionSignals.cs:27`; `services/OmpOS.Api/Sse/SessionChangePublisher.cs:39-54`; `SessionSignalSubscriber.cs:37-43` | Wake the replica that holds a device socket. Key space `dispatch:{deviceId}` would also be fed to `SessionEventStreamer.Notify` by the existing subscriber (harmless but noisy): prefer a second interface `IDispatchSignals` on a new channel. |
| Coordination registration: Dragonfly when `Coordination:Store=Dragonfly` and a connection string, else in-memory | `services/OmpOS.Api/Coordination/CoordinationRegistration.cs:25-45` | In-memory is single-process: Dispatch must refuse to enable multi-replica on it (validator, see 4.12). Production hosting expects Dragonfly (`docs/runbooks/hosting.md:33,102`). |

### 2.6 SSE with `Last-Event-ID` (server and clients)

| Item | Evidence | Reuse |
|---|---|---|
| Per-session resumable SSE: `Last-Event-ID` header beats `?since=`, digits only, exclusive resume, 400 on malformed, `ReconnectionInterval` 15 s | `services/OmpOS.Api/Endpoints/EventStreamEndpoints.cs:48-74,86-90,103-104` | Copy for `GET /dispatch/events` and per-command streams. |
| Streamer: log is truth, replay then tail, wake plus 200 ms poll fallback, no missed append | `services/OmpOS.Api/Sse/SessionEventStreamer.cs:24,71-125` | Pattern for the device-side delivery loop (read rows `> lastDeliveredSeq`, wake or poll) and the controller result stream. |
| User-level bounded in-memory history with `resync` event when position is lost; capacity 1024; process-local | `services/OmpOS.Api/Services/CatalogEventHub.cs:15,72`; `Endpoints/CatalogEventEndpoints.cs:31-54`; `src/OmpOS.Contracts/Catalog/CatalogResyncEvent.cs` | Model for "devices changed / command status changed" user feed. NOT multi-replica safe as is (a position from another process gets `resync`): back the Dispatch feed with the store. |
| Cloud agent events reuse the session stream | `services/OmpOS.Api/Cloud/CloudAgentEndpoints.cs:107,277-314` | Precedent: a feature route that fronts the existing session log. |
| Client side: `RemoteEngineHost` resume by `?since=` plus `Last-Event-ID`, Seq claim de-dup, `ReconnectAsync`, mobile-oriented | `src/OmpOS.EngineHost/RemoteEngineHost.cs:17-29,147-167,354` | The mobile controller leg. |
| Web client: `SseEventStream`, `UserEventFeed` (never ends), `SseStreamOptions` (backoff 250 ms to 5 s, 10 reconnects), `SeqTracker` high-water de-dup, `BearerTokenHandler` + `IApiTokenProvider` | `apps/OmpOS.Web/Services/SseEventStream.cs`, `UserEventFeed.cs`, `SseStreamOptions.cs:10-26`, `SeqTracker.cs`, `BearerTokenHandler.cs`, `IApiTokenProvider.cs`; default `NoApiTokenProvider` (`WebServiceRegistration.cs:33`) | Web controller leg already has the parts; `fetch`-based SSE carries `Authorization`, a WebSocket would not. |

### 2.7 Session registry, durable log, approvals

| Item | Evidence | Reuse |
|---|---|---|
| `ISessionRepository`: append-only log, repository-assigned Seq, strictly monotonic, no gaps even under concurrent appends; `LockSessionAsync` across processes | `src/OmpOS.Sessions/ISessionRepository.cs:11-19` and members `AppendAsync`, `ListFromSeqAsync`, `LockSessionAsync`; Postgres impl `src/OmpOS.Sessions.Postgres/PostgresSessionRepository.cs`, `PostgresSessionLock.cs` | Gives per-device ordering for free if the command log reuses the same technique. Also usable as the DATA plane (mirror a desktop session into the API session log so mobile watches it with the unchanged `RemoteEngineHost`). |
| Tool approvals decided by `requestId` in the store, resolves a request on any replica; allow above the request ceiling is 403; unknown or decided is 409 | `services/OmpOS.Api/Endpoints/ToolApprovalEndpoints.cs:12-20`; `Harness/SessionPermissionResolver.cs` | Semantics for the remote `approval.decide` op (idempotent, ceiling-bound, never reveals which ids exist). |
| Instance-local state: `SessionRegistry` (in-process), `LiveSessionTracker` ("a session has a live engine only while a run is active", `Services/LiveSessionTracker.cs:7-12`), cancel "executing on this server instance" (`Cloud/CloudAgentService.cs:288`) | files cited | Warns that anything keyed to a socket must be routed across replicas through the store plus signal, not memory. See `03-api-auth-backend.md:167` (same finding). |

### 2.8 Android head and web head

| Item | Evidence | Reuse |
|---|---|---|
| Android head: only `MainActivity`, Avalonia app, motion; no networking, token or background code | `apps/OmpOS.Android/*.cs` (file list); `docs/runbooks/android-ios-terminals.md:45` | Nothing to reuse yet; controller UI for mobile will be Avalonia Android or web. |
| Desktop gets its token from env var `OMPOS_API_TOKEN` and base URL from `OMPOS_API_BASE_URL`; `ApiCloudAgentsHost` takes a `Func<CancellationToken, ValueTask<string?>>` token supplier | `apps/OmpOS.Native/Composition/HeadServices.cs:44,47,311-318,439-453`; `apps/OmpOS.Native/Cloud/ApiCloudAgentsHost.cs:41,58,69,368` | The supplier shape equals `AcpWebSocketTransportOptions.BearerTokenProvider`; wire the account session's access token there. |
| Web head holds no credential and no browser storage by design | `apps/OmpOS.Web/Program.cs:12` (comment); `docs/runbooks/web-key-threat-model.md` | Web controller gets the token from the new sign-in provider; no persistence. CSP `connect-src 'self' <API origin>` (`scripts/vercel/render-csp.sh:62`): SSE and POST fit; a WebSocket to the same host should be allowed by CSP3 scheme matching [INFERENCE, not verified]. |

### 2.9 Auth on those channels (claims and audiences actually used)

| Channel | Validation | Claims | Evidence |
|---|---|---|---|
| API (all endpoints, fallback policy `RequireAuthenticatedUser`) | JwtBearer vs `Api:Auth:Authority`, audience `Api:Auth:Audience`, optional `ValidIssuer`; `MapInboundClaims=false`; `sub` mandatory | Only `sub` is read. Owner of everything = `sub`. No role, scope or device claims anywhere | `services/OmpOS.Api/Security/JwtBearerSetup.cs:27-45`; `AuthenticatedUserContext.cs:20-22`; `Program.cs:82-89,148-159`; `Options/ApiAuthOptions.cs:10-25` |
| Worker `/acp` | ES256/RS256 allow-list, `iss`, `aud=ompos-worker`, `sub=sessionId`, `iat`+`exp`, lifetime max 15 min, skew default 30 s (max 2 min), header-only | `iss aud sub jti iat exp` | `WorkerTokenIssuer.cs:73-92`; `WorkerJwtValidator.cs:42-56`; `ADR-0020` D4 (`:49-58`) |
| Hook bridge (desktop-local loopback) | per-session 32-byte random bearer, constant-time compare via SHA-256 digests | n/a | `src/OmpOS.Harness.AgentLoop/Hooks/BridgeToken.cs:18-37`; MCP gateway `src/OmpOS.Harness.McpGateway/McpGatewayBearerToken.cs:37-50` |
| Webhooks | HMAC-SHA256 over `timestamp.body`, `sha256=` header, constant-time verify, skew window, replay cache (in-process) | n/a | `src/OmpOS.Harness.Automations/Hosted/WebhookSignature.cs:19-57`; `WebhookTrigger.cs:11,63-117,20,108` (order: size, signature, timestamp, replay) |

The planned Supabase facts (audience `authenticated`, ES256 JWKS) are in `05-supabase-auth-reference.md:18-19,93-97` and the seams in `03-api-auth-backend.md:116-146`; not repeated here.

### 2.10 Rate-limit, audit, correlation patterns

| Item | Evidence | Reuse / gap |
|---|---|---|
| Keyed `IRateLimiterPolicy<string>` per endpoint group, 429 with `Retry-After: 60`, log without content | `Program.cs:185-195`; `Security/SessionRateLimiterPolicy.cs:33-48`; `Security/RateLimitRejection.cs:26-27`; options `Options/ApiRateLimitOptions.cs:10-16` | All current policies are ONE GLOBAL bucket (`GetTokenBucketLimiter("prompts", ...)`, `SessionRateLimiterPolicy.cs:37`); the comment says per-session fairness waits for auth. Auth now exists: Dispatch needs partitions keyed by `sub` and by device. Confirm idle-partition cleanup of the framework before shipping [INFERENCE]. |
| Sliding-window limit on the injected clock | `src/OmpOS.Harness.Automations/RateLimit.cs` | Alternative for per-device command rate. |
| Per-owner caps enforced atomically in the store | `ICloudAgentStore.cs:4-17,64-74`; `Cloud/Options/CloudOptions.cs:116` (`MaxConcurrentRunningPerOwner = 2`) | Template for device-count and queue-depth caps. |
| Feature off by option, routes mapped, 404 problem `cloud_disabled` before any handler, after authorization | `Cloud/CloudDisabledMiddleware.cs`, `CloudRouteMarker.cs`, `CloudAgentEndpoints.cs:40` (`MapGroup(...).WithMetadata(CloudRouteMarker.Instance)`), `Program.cs:251-252` | Copy as `DispatchDisabledMiddleware` / `DispatchRouteMarker`. |
| Metrics via `IMeterFactory` counters/histograms with tags | `Cloud/CloudMetrics.cs:28-46`; `Metrics/ApiMetrics.cs` | Dispatch counters: commands accepted/expired/refused, devices online, ack latency. |
| Correlation id | `Middleware/CorrelationIdMiddleware.cs:13` (`x-correlation-id`) | Put into the envelope `corr`. |
| Audit | No audit abstraction found in `services/OmpOS.Api` [FACT: grep returned none]. Existing durable trails are the session log, `HookRunLog` (`src/OmpOS.Harness.Plugins/Hooks/HookRunLog.cs`) and the external Substrate hash-chained ledger | New append-only `dispatch_audit` table (see 4.12). The Substrate ledger and its Solana anchor are external (`plugins/agent-substrate` is an empty submodule in this worktree; `docs/plans/agent-loop/dossier-substrate.md:80,102`): optional notarisation, not on the hot path. |
| Redaction | `Services/PromptRedactor.cs` (length-only marker), `Cloud/Secrets/CloudSecretRedactor.cs`, `CloudRedactingEventFilter.cs` | Never log command bodies; run redactor on any relayed event before storing. |

### 2.11 Kanban lease holder

- `CardDetailsResponse.LeaseHolder` (`src/OmpOS.Contracts/Catalog/CardDetailsResponse.cs:30-32`) and `KanbanCardViewModel.LeaseText` (`src/OmpOS.Presentation/Kanban/KanbanCardViewModel.cs:80`) are DISPLAY ONLY. The desktop head returns null (`apps/OmpOS.Native/Agents/HeadKanbanDetailsSource.cs:76`); the value comes from the external Substrate `graph_get` (`SubstrateStageMapper.cs:111-149`: only holder surface and expiry shown, never lease id). It is not a mutual-exclusion primitive in this repo.
- The Substrate lease semantics are the best written pattern for a "driver lease": holder = (surface, session), epoch, expiry, `lease_id` fencing token returned only on claim, heartbeat with reasons `lost|expired|unheld|not-holder`, signed handoff packet (HMAC-SHA256, ledger anchored) (`dossier-substrate.md:98-102`). The run-lease code above is the in-repo implementation to reuse.

## 3. Not present (must be built)

1. A device identity, registry, pairing flow, per-device credential (OQ-68).
2. A WebSocket endpoint on the API: `UseWebSockets` appears only in `services/OmpOS.AgentWorker/WorkerApp.cs:53`, not in `Program.cs`.
3. A cross-replica payload or command queue. `ISessionSignals` has no payload; the in-process `CatalogEventHub` is process-local.
4. Presence (online/offline, last seen).
5. Per-user rate limits (all global today), audit log, signed commands, command expiry and replay protection across replicas (the webhook replay cache is in-process, `WebhookTrigger.cs:20`).
6. Push notifications (FCM/APNs): none; OQ-14 forbids promising background streams.
7. Large-artifact storage (screenshots for Computer Use): no blob store in code; Railway bucket tools exist only as MCP operator tools.
8. OpenAPI security scheme and Demo handler fixtures for new routes (`03-api-auth-backend.md:158,173`).

## 4. Recommended design

### 4.1 Topology

```
Controller (mobile Avalonia / web WASM)            Hub (services/OmpOS.Api, Railway)                 Desktop connector (Native head)
 POST /dispatch/devices/{id}/commands  -------->   validate, own-check, idem, rate, append row
 GET  /dispatch/events (SSE, Last-Event-ID) <----  results/status/presence feed                    (outbound only, dials hub)
                                                    command log (Postgres, per-device seq) ---WS-->  wss .../dispatch/devices/{id}/connect
                                                    presence lease + wake signal (Dragonfly)  <--WS--  ack / result / event / heartbeat
```
Control plane = this relay. Data plane = existing session log and SSE (4.11).

### 4.2 Where the hub lives

New module `services/OmpOS.Api/Dispatch/` (the sibling recon `03-api-auth-backend.md:146` proposes `Relay/`; pick one name, see D9), following the `Cloud/` module layout (`CloudServiceRegistration`, `CloudAgentEndpoints`, `CloudDisabledMiddleware`, `CloudRouteMarker`, `CloudOptions`+validator, `CloudMetrics`, hosted sweeper like `CloudIdleSweeper`). Contract records in `src/OmpOS.Contracts/Dispatch/` with a `DispatchJsonContext` like `CloudJsonContext` (source-gen only, `Program.cs:92-96`). Store interface `IDispatchStore` in Contracts like `ICloudAgentStore`; Postgres impl in `src/OmpOS.Sessions.Postgres` (new `PostgresDispatchSchema` like `PostgresCloudAgentSchema`); in-memory twin for tests. `Program.cs` additions only: `AddDispatchServices()` near `:126`, `app.UseWebSockets()` before routes (before `UseAuthentication` is not required; it must precede the endpoint), `app.UseMiddleware<DispatchDisabledMiddleware>()` after `:252`, `MapDispatchEndpoints()` after `:290`. Existing routes and contracts are untouched; additive minor bump of `ContractVersion.Current` (`src/OmpOS.Contracts/ContractVersion.cs:7`, "1.5.0") and regeneration of `docs/api/openapi.json` (equality-tested per `03-api-auth-backend.md:158`).

Routes (all behind the fallback policy, owner = `sub`, a foreign device reads as 404 like cloud agents):

| Route | Purpose |
|---|---|
| `POST /dispatch/devices` | Pair: register a device under the caller (name, platform, public key). Returns `deviceId` and bootstrap info. |
| `GET /dispatch/devices`, `GET /dispatch/devices/{id}`, `DELETE /dispatch/devices/{id}` | List with presence, read, revoke (closes socket, rejects later connects). |
| `GET /dispatch/devices/{id}/connect` | WebSocket upgrade, desktop only. |
| `POST /dispatch/devices/{id}/commands` | Enqueue (header `Idempotency-Key`). 202 with command id and seq. |
| `GET /dispatch/commands/{id}`; `POST /dispatch/commands/{id}/cancel` | Status; cancel (queued: drop; delivered: send `cancel` command). |
| `GET /dispatch/events` | User-level SSE: device presence, command status, relayed result envelopes. `id:` = durable event id, `Last-Event-ID` resume, `resync` when pruned. |

### 4.3 Identities and credentials

- Controller: account access token (the existing `Api:Auth` JwtBearer path). No new claims needed; `sub` = owner. [`03` D2 suggests pinning ES256 and rejecting anonymous.]
- Desktop connect: the same account access token (supplied per connect through `BearerTokenProvider`, so reconnect always presents a fresh one, `AcpWebSocketTransportOptions.cs:18-23`) PLUS proof of device key: hub sends a nonce in its first frame, device answers `hello` with a signature over `(nonce, deviceId, hubOrigin)` using the private key it generated at pairing (key stays in the OS credential store; `03` finding 6 says no such store exists yet in Native). Without this, a stolen access token could impersonate any device of the user. Hub authorises only `devices.owner_id == sub` and `revoked_at is null`.
- Hub-to-device command authenticity: compact JWS, ES256, issued like `WorkerTokenIssuer`: `iss` = Dispatch issuer, NEW `aud = ompos-device`, `sub` = deviceId, `jti` = commandId, plus private claims `own` (owner sub), `seq`, `op`, `bh` (SHA-256 of canonical body), `iat`, `exp`. Key in a NEW variable (names under `Dispatch__...`; none exist today); public key delivered to the device at pairing and refreshed by `kid`. Device verifies with the `WorkerJwtValidator` parameter set (algorithm allow-list, issuer, audience, lifetime cap, injected clock). Scope: this protects the device against a forged frame and against replay of an old frame; it does NOT protect against a compromised hub (hub holds the key). End-to-end controller-signed commands are an optional later layer (D4).

### 4.4 Message envelope (one JSON object per text frame, at most 1 MiB, validated like `AcpFrameGuard`)

```
{ "v":1, "t":"cmd|ack|res|evt|hello|hb|err|cancel",
  "id":"<command id, 26-char sortable>", "ref":"<command id this answers>",
  "dev":"<deviceId>", "seq":123,            // per-device, hub-assigned, cmd only
  "ik":"<idempotency key 1-128 visible ASCII>", "corr":"<x-correlation-id>",
  "op":"session.list|session.prompt|session.steer|session.cancel|approval.decide|cowork.task|...",
  "iat":1730000000, "exp":1730000300,
  "tok":"<compact JWS from 4.3>",           // cmd only
  "body":{...} }
```
- `v` is the contract version (Herdr `ContractVersion` pattern, `HerdrContract.cs:12`); a connection with an unsupported `v` closes 1002 or a 4xxx application code (D8).
- `op` names are a closed enum per `v`; unknown `op` is rejected with `err{code:"unsupported_op"}` and an ack status `rejected`, never executed.
- Do NOT name the envelope or kinds `Intent*`: `OmpOS.Contracts.Intents.IntentEnvelope` and `IntentKind` already exist for the prompt parser (`src/OmpOS.Contracts/Intents/IntentEnvelope.cs`).
- Size classes: POST body cap 256 KiB (webhook precedent `WebhookTrigger.cs:31-33`), frame cap 1 MiB (`AcpBridgeSession.cs:17`). Screenshots and other bulk data are references, not inline (needs storage, D7).

### 4.5 Command state machine (store is authority)

`Queued -> Delivered -> Accepted -> Running -> Succeeded | Failed | Cancelled` and `Queued|Delivered -> Expired | Rejected`. Transitions are compare-and-set in the store (like `ICloudAgentStore.UpdateAsync` / `TryActivateAsync`), each appends a `dispatch_events` row that feeds `/dispatch/events`.

### 4.6 Per-device ordering, acknowledgement, idempotency

- Ordering: `dispatch_commands` is an append-only log per device; `seq` is assigned by the store inside the insert (per-device counter or row lock, same guarantee as `ISessionRepository` Seq: strictly monotonic, no gaps, `src/OmpOS.Sessions/ISessionRepository.cs:11-19`). The device applies strictly in `seq` order, one control op at a time per device per session; ops on different sessions may run concurrently only if the op declares `par:true` [INFERENCE, keep v1 serial].
- Delivery: at-least-once. The hub pushes rows with `seq > deliveredSeq` when woken (signal) or on a 200 ms poll fallback (`SessionEventStreamer.cs:24`). On connect the device sends `hello{lastAppliedSeq, caps, appVersion}`; the hub replays `seq > lastAppliedSeq` (Herdr `AttachAsync(afterSequence)` semantics). Device dedups by `seq` high-water mark (`SeqTracker` logic, `apps/OmpOS.Web/Services/SeqTracker.cs`) AND recent `jti` set, persisted to a small atomic file so a restart does not re-execute (pattern: `AtomicFile`, `FileAutomationGuardStore` in `src/OmpOS.Harness.Automations`). Effect: at-least-once transport plus idempotent apply equals effectively-once.
- Acks: `ack{ref, seq, status: accepted|duplicate|rejected, reason}` is cumulative-capable (`ackSeq`) and means "durably accepted by the device"; completion is `res{ref, status, body}`; progress is `evt`. Hub marks `Delivered` on socket write, `Accepted` on ack, never marks success without `res`.
- Controller idempotency: header `Idempotency-Key` (`IdempotencyKeyHeader`), scope `dispatch:{owner}:{device}`, fingerprint = SHA-256 of canonical JSON of the request through the source-gen context (`PromptFingerprint` method). Flow copied from `SessionEndpoints.cs:246-312`. AUTHORITY is a unique index `(owner_id, device_id, idempotency_key)` in `dispatch_commands` (the Dragonfly claim is the fast path), because `IdempotencyCompletion` is best-effort (`IdempotencyCompletion.cs:9-11`). Keys are retained at least as long as the longest `exp` (cap 24 h = `IdempotencyCompletedTimeToLive`).

### 4.7 Expiry

- Every command has `exp` (default 120 s for approvals and cancels, 15 min for prompts, hard cap 24 h for queued "run when the desktop comes online" tasks). Hub sweeper (hosted service like `CloudIdleSweeper`/`SessionRunReaper`, injected `TimeProvider`) moves overdue `Queued|Delivered` to `Expired` and emits an event. Device refuses `exp < now - skew`, skew default 30 s, max 2 min (`ADR-0020` D4 numbers). Expired approvals must never be approvable later (maps to the 409 rule of `ToolApprovalEndpoints.cs:12-20`).
- The device also refuses a command whose `exp - iat` exceeds the cap for its `op`.

### 4.8 Replay protection and signing (layers)

1. TLS, short-lived account token, header-only token (`WorkerJwtBearerSetup` rule).
2. Per-command signed JWS bound to `deviceId`, `seq`, `jti`, body hash, `exp` (4.3). A captured frame is useless on another device (`sub`/`dev`), after `exp`, or with another body (`bh`).
3. Device high-water `seq` and `jti` set (4.6) reject re-delivery after restart.
4. Hub side: shared idempotency store for the controller leg (NOT `WebhookReplayCache`, which is process-local, `WebhookTrigger.cs:20`); for the device-to-hub frames `ack/res/evt` the hub checks `ref` belongs to this device and the transition is legal (CAS), so a replayed `res` is a no-op.
5. Constant-time comparisons everywhere a secret or digest is compared (`BridgeToken.Matches`, `WebhookSignature.Verify`).
Optional, later: HMAC webhook-style signing is not needed once ES256 JWS is used.

### 4.9 Presence

- Device connects: hub takes `presence:{deviceId}` with `SET NX PX 30000`, owner `"{InstanceId}:{connectionId}"` (`PromptRunner.cs:23,119` style), renewed every 10 s (`CoordinationDefaults.cs:7,10`) with Lua compare-and-set. WebSocket ping 20 s (`AcpWebSocketTransportOptions.cs:13`) plus app `hb` every 15 s carrying `lastAppliedSeq`, active run count and a coarse status; hub treats 3 missed `hb` (45 s) as stale (`HerdrHeartbeatMonitor.cs:13-19`). `last_seen_at` persisted at most every 60 s, not every beat.
- Online = presence lease alive. State words reuse `RemoteConnectionState` (`Connected`, `Reconnecting`, `Disconnected`, `Unreachable`, `Closed`).
- Second connection for the same device: newest wins (close the older socket with an application close code) instead of the worker's 409 (`AcpBridgeEndpoint.cs:62-67`), because after a Wi-Fi switch the old half-open socket would otherwise block the reconnect until TCP timeout. Flag as D3.
- Controller UI gets presence changes on `/dispatch/events` (event kinds `device.online`, `device.offline`).
- Optional "driver lease" per (device, session) so two controllers do not both steer: reuse `ISessionRunLeases` semantics with a fencing epoch like Substrate `graph_claim`; v1 may skip (D6).

### 4.10 Cross-replica routing

- Any replica accepts the controller POST: it writes the row (durable) and publishes a wake on a Dispatch channel (new `IDispatchSignals`, or reuse `ISessionSignals` with prefixed ids). The replica that holds the device socket (not necessarily the writer) wakes, reads rows `> deliveredSeq`, pushes, records delivery. No sticky sessions, no payload pub/sub: the store is the queue, the signal is a hint, the poll is the safety net. This matches the existing replica model (`SessionEventStreamer.cs:10-19`) and removes the need for the payload channel that `03-api-auth-backend.md:143` anticipates. Dragonfly inbox drops oldest at 256 (`DragonflySessionSignals.cs:27`): harmless because of the poll.
- Results from the device are written to the store by whichever replica owns the socket; `/dispatch/events` readers on other replicas see them via the same wake-or-poll loop.
- Single-replica first is acceptable (`03` D8) but the design does not depend on it.

### 4.11 Data plane (what mobile and web watch)

Preferred: the desktop appends the session's `StreamEvent`s to the API session log through a hub frame `evt{sid, payload}` (the hub validates and calls `ISessionRepository.AppendAsync`, then `SessionChangePublisher.NotifyAsync`), so the existing `GET /sessions/{id}/events`, `RemoteEngineHost` and `SseEventStream` work unchanged with `Last-Event-ID` resume, Postgres durability and cross-replica streaming. Costs: transcripts leave the desktop into Railway Postgres (needs explicit opt-in per session; D2) and the catalog/board would show a new session kind (`CatalogSessionKind {agent, terminal, cloud}`, `CatalogSessionKind.cs`) so a `dispatch`/`remote` kind is a contract addition. Alternative (no transcript upload): relay only a bounded ring of recent frames, accepted loss on reconnect. Choose in D2.

### 4.12 Limits, audit, validation

- Rate limits: new keyed policy `DispatchPolicy` partitioned by `sub` (token bucket) plus a per-device in-flight/queue cap enforced atomically in the store (as `ICloudAgentStore` caps; suggested defaults 100 queued per device, 20 devices per owner [INFERENCE]). 429 via `RateLimitRejection.Handler`.
- Options `Dispatch:*` with `ValidateOnStart` like `CloudOptionsValidator`; Production requires `Coordination:Store=Dragonfly` and `Sessions:Store=Postgres` when `Dispatch:Enabled`, and a configured signing key; off by default (`appsettings.json`).
- Audit: append-only `dispatch_audit` (who `sub`, controller client id, device, op, command id, `corr`, outcome, timestamps; never bodies; prev-hash column optional) written in the same transaction as state changes. Metrics via `IMeterFactory` like `CloudMetrics`. Logs use `LoggerMessage` with ids and no body (`AcpBridgeEndpoint.cs:21-28` style, `PromptRedactor`).
- Disabled feature: `DispatchDisabledMiddleware` 404 `dispatch_disabled` after authorization (`CloudDisabledMiddleware`).

### 4.13 Client side

- Desktop connector (new class in the Native head or a small L3 library; `LayerMap.cs:15-56` lists layers: Native is 4, EngineHost 3, Acp 2): `AcpWebSocketTransport` with the account token provider, wrapped in a reconnect loop (variant of `ReconnectPolicy`: jitter, unbounded, 30 s ceiling), `hello` on every connect, a command executor that maps `op` to existing host calls (`IEngineHost.SubmitPromptAsync/CancelAsync`, tool approval decision, session list from `SessionEngineSet`), persisted dedup state. Nothing inbound is ever opened.
- Mobile/web controllers: POST plus SSE only (`RemoteEngineHost`, `SseEventStream`, `UserEventFeed`, `BearerTokenHandler`). No WebSocket in browsers, so no `OnMessageReceived` or `Sec-WebSocket-Protocol` token carrier is needed (that carrier is listed as a gap in `03-api-auth-backend.md:142`).

### 4.14 Test seams (main agent runs them; not run here)

`tests/OmpOS.Api.IntegrationTests/Cloud/TestOwnerAuthHandler.cs` (owner auth in tests), `FakeCloudWorker.cs` and `CloudApiHost.cs` (host fixtures), `ManualClock.cs` (time), `tests/OmpOS.AgentWorker.Tests/WorkerSocket.cs` (plain `ClientWebSocket` test client), `tests/OmpOS.Coordination.Tests/DragonflyTestEnvironment.cs` (podman Dragonfly; main agent decides), `CloudAgentAccessTests.cs`-style cross-owner 404 tests. Add `LayerMap` line, `OmpOS.slnx`, `OmpOS.Linux.slnf` and `deploy/api.Containerfile` COPY only if a NEW project is created (`03-api-auth-backend.md:159`); a module inside `OmpOS.Api` needs none.

## 5. Risks

1. Railway acceptable use. `docs/plans/v0.15.0-research/09-remote-cloud-plan.md:87-88` (D14, D15): Trust and Safety prohibits "remote desktops for personal or professional browsing" and proxies; resale of compute. A command/result relay of the user's own machines is neither [INFERENCE], but relaying Computer Use screen images or live video through Railway is adjacent and is the exact paused item (OQ-73, `cloud-terminals.md:35,42`). Keep Dispatch to commands, text, status, still thumbnails; no live streaming; get Railway's written answer first.
2. Hub compromise equals desktop control. A relay that can issue commands to a desktop that runs Computer Use is a remote-code-execution path. Mitigate: signed commands, per-op permission ceilings on the device (the approval-ceiling rule `ToolApprovalEndpoints.cs:12-20` must apply to remote approvals), device-side allow-list of ops, user-visible "remote session active" indicator, revoke. Computer Use ops should require a per-device opt-in on the desktop itself.
3. Account token theft equals impersonation of controller. Device-key challenge protects the device leg only; controller leg relies on Supabase token security (short life, `aal`, revocation gap noted in `03:126`).
4. In-memory fallbacks (`SessionRegistry`, `CatalogEventHub`, `WebhookReplayCache`, `Coordination:Store=InMemory`) silently break with 2+ replicas. Validator must refuse.
5. Rate limits are global today (finding 3 in `03:168`): one user can starve all unless the new policy partitions by `sub`.
6. Mobile OS background limits: no push exists; commands queue while the phone is not watching, results wait in the log. Notifications need a push provider (out of scope for repo code).
7. `Last-Event-ID` ids across replicas: use store-backed monotonic ids for the user feed, not `CatalogEventHub`'s in-process counter.
8. Clock skew between desktop, phone and hub for `exp` and `iat`: use hub time as the only clock for `exp` and send `serverTime` in the first frame so the device can estimate offset.
9. Idempotency completion is best-effort (`IdempotencyCompletion.cs:9-11`); do not rely on Dragonfly alone.
10. Dragonfly image is unpinned (`hosting.md` Q-HOST-DFLY-PIN), API never deployed, Railway deploys paused, OD-03 authority unset (`cloud-terminals.md:50-52`): nothing here can be live-tested yet.

## 6. Open questions

1. Is a Dispatch device a new `EnvironmentKind` or a separate registry (`03-api-auth-backend.md:137` proposes a `devices` table)?
2. Do transcripts of a Dispatch-controlled desktop session get mirrored into the API session log (needs privacy opt-in), or only a bounded frame ring?
3. Which ops are in v1 (list sessions, prompt, steer, cancel, approve; Cowork tasks; Browser Use; Computer Use)? Computer Use and Browser Use with vision need artifact transport.
4. Where do large artifacts live (Railway bucket via code, Supabase Storage, none)?
5. Push provider for mobile notifications.
6. Does `ReconnectPolicy` get a jittered unbounded variant in `OmpOS.EngineHost`, or does the connector own its own loop?
7. Custom WebSocket close codes: reserve 4000-4099 for Dispatch (4001 revoked, 4009 replaced by newer connection, 4010 unsupported version) [INFERENCE: 4000-4999 is the application range of RFC 6455].
8. Whether Railway Serverless is ever enabled for the API service (would drop sockets, ADR-0020 line 105 question).

## 7. Decisions needed and gating

| # | Decision | Default proposed [INFERENCE] | Gate |
|---|---|---|---|
| D1 | Direction and transport: desktops outbound WebSocket, controllers POST plus SSE | As written in 4.1 and 4.13 | None technically; browsers lack WS auth headers (platform). |
| D2 | Data plane: mirror desktop sessions into the API session log vs bounded frame ring | Mirror with per-session opt-in | Owner privacy decision; contract addition for session kind. |
| D3 | Second connection policy: newest wins vs 409 | Newest wins | None. |
| D4 | Command authenticity: hub-signed JWS now; controller E2E signing later | Hub-signed ES256, new `aud=ompos-device`, new key variable | Owner must provision and rotate a key; key management; OQ-68 pairing model. |
| D5 | Device registry shape and pairing (OQ-68) | New `dispatch_devices` table, pairing with a user token plus device keypair | Owner decision on OQ-68; OS credential store for the device key (`03:171`). |
| D6 | Driver lease per session | Skip in v1 | None. |
| D7 | Artifact storage for screenshots | None in v1; refs only | Owner choice of storage; Railway acceptable-use answer (OQ-73). |
| D8 | Contract version bump and close-code range | Minor bump of `ContractVersion.Current` (1.5.0 to 1.6.0), 4000-4099 | Regenerate `docs/api/openapi.json`; coordinated with accounts work. |
| D9 | Module name `Dispatch/` vs `Relay/` (sibling report uses `Relay/`) | `Dispatch/` for routes and options, `Relay` only as a type word inside | Orchestrator naming call. |
| D10 | Public exposure | Hub off by default; enable only after OD-03 authority and Railway deploys unpaused | `docs/runbooks/hosting.md` blockers 1 and 5; OQ-73 for any screen streaming. |
| D11 | Single vs multi replica at launch | Design supports multi; launch single | Railway replica config; Dragonfly pin. |
