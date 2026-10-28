# Phase 74: Dispatch relay - Context
Source: docs/plans/v0.16.0-research/briefs/74-dispatch.md
**Gathered:** 2026-10-28
**Status:** Ready for planning

<domain>
## Phase Boundary

A signed-in client (phone app, web app, another desktop) sends tasks to one of the account's registered desktops through a relay hosted in the existing `Autonogrammer.Api`; desktops connect outbound only. Delivered: contracts, API (registry, hub, feed), desktop connector, and the Devices and Dispatch surfaces for the Native head (shared with Android) and the Web head. Requirement ids AG-74-1 to AG-74-18 (brief 74 lines 11-28). Work units U1 to U6 (brief lines 52-61): U1 first; U2, U3, U5 follow U1 in parallel; U4 needs U1 and U2; U6 needs U1 and runs against a fake host.

Depends on (index phase table): 67 (`IAccountSession`, `Harness.Secrets.Os`, `IAccountFeature`), 68 (M0005, M0008, keyspace, `ITenantHasher`, `IAuditLedger`, `accounts.feed_seq`; U3 needed by U2), 69 and 70 (SurfaceHost chrome slot, Cowork host), 72 and 73 (tool classes, `Cowork:ComputerUse:AllowDispatchSessions`, `IComputerApprovalPrompt`). The scaffold step registers all new projects first (`Dispatch.Client`, `Dispatch.Desktop` and tests; manifest rows 20, 21, T13, T14).

Must NOT do: a full mobile app, push notifications, screen or browser streaming, transcript mirroring, a `computer.*` operation, controller end-to-end signing, a browser WebSocket, a second service, any new NuGet package, edits to `ContractVersion.cs` (scaffold bumps to 1.6.0, R9), LayerMap or project registration (R17), or an M0011+ migration. Android is compile-checked only.
</domain>

<decisions>
## Implementation Decisions

### Placement, routes, feature switch
- **D-01:** The hub is a new module `services/Autonogrammer.Api/Dispatch/` laid out like `Cloud/` (registration, endpoints, options plus validator, metrics, hosted sweeper, `DispatchDisabledMiddleware`, `DispatchRouteMarker`); it is the only code that signs a command; no separate service. (ADR-0036 D1)
- **D-02:** Routes, all behind the fallback policy with owner = `sub` and a foreign or absent device the same 404: `POST/GET /dispatch/devices`, `GET/PATCH/DELETE /dispatch/devices/{id}`, `GET /dispatch/devices/{id}/connect` (WebSocket, device credential), `POST /dispatch/devices/{id}/commands`, `GET /dispatch/commands`, `GET /dispatch/commands/{id}`, `POST /dispatch/commands/{id}/cancel`, `GET /dispatch/events` (SSE), plus `DELETE /dispatch/devices` (revoke all). (ADR-0036 D1; AG-74-3)
- **D-03:** `Dispatch:Enabled=false` by default; routes stay mapped for OpenAPI; `DispatchDisabledMiddleware` answers 404 `dispatch_disabled` after authorization, as `CloudDisabledMiddleware`. The module registers `IAccountFeature` key `dispatch`. (ADR-0036 D1; AG-74-17)
- **D-04:** `app.UseWebSockets()` is added with `KeepAliveInterval` 20 s; liveness is the application heartbeat, not the framework timeout. Controllers use HTTPS POST plus SSE with `Authorization: Bearer` only, never a browser WebSocket. (ADR-0036 D1; AG-74-16)
- **D-05:** The OpenAPI transformer lists the connect route with security scheme `DispatchDevice`, next to the anonymous operations; the access token is refused there and the credential is refused elsewhere. (ADR-0036 D2; R10)

### Pairing and device credential
- **D-06:** Pairing (`POST /dispatch/devices`) needs the newest non-`token_refresh` `amr` entry no older than `Dispatch:Pairing:MaxSignInAge` (10 min); `iat` is not used; Dispatch never re-validates the user token; otherwise 403 `recent_sign_in_required`; `session_id` is stored as `paired_session_id`. (ADR-0036 D2; R18; AG-74-2)
- **D-07:** The desktop generates a P-256 key pair; the private key lives only in `ISecretStore` under `autonogrammer.dispatch.device-key.<deviceId>` (credential under `autonogrammer.dispatch.device-credential.<deviceId>`) via `Harness.Secrets.Os`; no file fallback; unavailable store gives `KeyStoreUnavailable`, pairing refused, a paired desktop does not connect. (ADR-0036 D2; ADR-0030 D6 rule 4; AG-74-1)
- **D-08:** The credential is an API-signed ES256 JWT with its own key variable `Dispatch:Credentials:SigningKeyPem` (distinct from `Dispatch:CommandSigning:KeyPem`): `iss` `Dispatch:Credentials:Issuer`, `aud` `autonogrammer-dispatch-device`, `sub` device id, `own` account id, `cnf.jkt` SHA-256 thumbprint of the device key, `jti`, `iat`, `exp`; lifetime 30 days (validator refuses more than 90); the hub stores the current `jti`, renewal invalidates older copies. (ADR-0036 D2)
- **D-09:** `DeviceCredentialValidator` copies the `WorkerJwtValidator` parameter set (ES256 only, issuer, audience, `exp` and `iat` required, lifetime cap, injected `TimeProvider`, header-only token); no extraction to a shared library now. (ADR-0036 D2; OQ-422)
- **D-10:** Pairing request carries `name` (1 to 64 chars, trimmed), `platform`, `appVersion`, `protocolVersions`, capability list and public key JWK; reply carries `deviceId`, credential, `hubOrigin`, hub verification keys (`kid`, ES256 public JWK, validity window) and limits. Capability entries are `{id, version, enabled}` with `id` in `sessions`, `code`, `cowork`, `browser_use`, `computer_use`; the last two are tool-class flags, never a command capability. (ADR-0036 D2; index section 2)
- **D-11:** Hub refuses to enqueue a command whose capability the snapshot lacks or disables (409 `capability_unavailable`); the device refuses with `capability_disabled`. (ADR-0036 D2)
- **D-12:** M0010 `dispatch_device_state` (file-owned by U2, registered in the runner's compiled list by U2) adds `credential_jti`, `credential_expires_at`, `capabilities jsonb`, `app_version`, `protocol`, `revoke_reason`, `paired_session_id`, `last_source_prefix` (IPv4 /24 or IPv6 /48, device row only) with `ADD COLUMN IF NOT EXISTS`, and unique `(id, account_id)`; M0009 stays `cowork_projects_memory`. (ADR-0036 D2; R2)

### Handshake, frames, close codes
- **D-13:** Pre-upgrade statuses: 400, 401 `credential_invalid`, 401 `device_revoked`, 403 (foreign device, unavailable account, or any `Origin` header), 426, 429, 503. (ADR-0036 D3)
- **D-14:** Hub sends `challenge` (128-bit nonce, `serverTime`, `origin`, `protocols`); nonce stored `autonogrammer:v1:auth:nonce:{sha256hex}` with `SET NX PX` 10 min and deleted on verified `hello`; Dragonfly down closes 1013. `hello` is a device signature over `autonogrammer-dispatch-hello-v1`, nonce, deviceId, believed hub origin and `lastAppliedSeq`; failure closes 4002. Hub then sends `welcome` (`serverTime`, heartbeat, `kid` set, optional renewed credential, `resumeFromSeq`); device computes `clockOffset`. (ADR-0036 D3; AG-74-4)
- **D-15:** Frames are single JSON text frames at most 1 MiB with `v` (1) and `t`; hub to device `challenge`, `welcome`, `cmd`, `revoke`, `err`; device to hub `hello`, `ack`, `evt`, `res`, `hb`, `err`; `AcpFrameGuard` rules both ways (UTF-8, one object, no duplicate property names via `Utf8JsonReader`); unknown `t` gives `err{unsupported_frame}` and close 1008 after three; binary ignored; unknown `op` never executed. (ADR-0036 D3)
- **D-16:** Newest connection wins: the older socket closes 4009. Close codes: 1000, 1001, 1007, 1008, 1009, 1011, 1013, 4001 revoked (terminal), 4002 credential or proof invalid, 4009 replaced, 4010 unsupported version, 4011 account unavailable (terminal), 4020 rate limited. (ADR-0036 D3)

### Command envelope and signature
- **D-17:** `cmd` carries `v`, `t`, `commandId` (26-char ULID, equals `dispatch_commands.id`), `seq`, `userId`, `deviceId`, `capability`, `op`, `issuedAt`, `expiresAt` (Unix s, hub clock), `nonce` (128-bit, new per delivery), `payload` (base64url, at most `Dispatch:MaxPayloadBytes` 65,536 decoded; validator max 262,144), `payloadSha256`, `kid`, `sig`; signed at delivery over bytes serialised from the stored `body`. (ADR-0036 D4; AG-74-5)
- **D-18:** `sig` is raw P1363 ES256 over UTF-8, LF, fixed-order canonical bytes: `autonogrammer-dispatch-cmd-v1` then `v=`, `commandId=`, `seq=`, `userId=`, `deviceId=`, `capability=`, `op=`, `issuedAt=`, `expiresAt=`, `nonce=`, `payloadSha256=`, `kid=`; every signed value restricted to `[A-Za-z0-9._:-]`; canonicalisation lives in `Autonogrammer.Contracts/Dispatch/DispatchCanonical.cs` with a shared vector file. (ADR-0036 D4; AG-74-5)
- **D-19:** Closed `v=1` operations: `session.list`, `session.steer`, `session.cancel`, `approval.decide` (capability `sessions`); `code.prompt` (`code`, `SessionSurface.Code`); `cowork.task` (`cowork`, `SessionSurface.Cowork`, payload may name `toolClasses` subset of `browser_use`, `computer_use`); `command.cancel` (any capability, same device). No `computer.*` operation; `approval.decide` for kind `computer` is refused. (ADR-0036 D4; R1; AG-74-9)
- **D-20:** Device accepts a `kid` only from the set delivered at pairing or a `welcome` signed by a pinned key; rotation ships the new public key at least 24 h before signing. (ADR-0036 D4)

### Ordering, ack, expiry
- **D-21:** `seq` is allocated in the insert transaction by `UPDATE dispatch_devices SET command_seq = command_seq + 1 ... RETURNING` under the device row lock (strictly increasing, gap-free at allocation); unique `(device_id, seq)`; queue cap `Dispatch:Limits:MaxQueuedPerDevice` 100 checked in the same transaction. (ADR-0036 D5; AG-74-6, AG-74-12)
- **D-22:** Delivery is at-least-once: loop reads `seq > deliveredSeq` in `queued`, wakes on signal, 200 ms poll fallback, marks `delivered` after the write; `deliveredSeq` starts at `hello.lastAppliedSeq`; device refuses `seq <= highWater` and never waits for gaps; device persists `highWater` and 1,024 recent `commandId`s atomically before `ack received`. (ADR-0036 D5)
- **D-23:** Hub states: `queued`, `delivered`, `received`, `awaiting_approval`, `accepted`, `running`, `completed`, `failed`, `cancelled`, `expired`; every transition is compare-and-set against the table; an out-of-table frame is a no-op plus audit row. Hub never marks `completed` on its own. (ADR-0036 D5; AG-74-7)
- **D-24:** `DispatchFailureReason` closed set: `expired`, `revoked`, `signature_invalid`, `unsupported_version`, `unsupported_operation`, `capability_disabled`, `approval_denied`, `approval_timeout`, `policy_denied`, `payload_invalid`, `payload_too_large`, `device_busy`, `device_restarted`, `execution_error`, `timeout`, `cancelled_by_user`, `internal`; `res` text at most 4,096 chars, `evt` text 1,024, redacted. (ADR-0036 D5)
- **D-25:** `Idempotency-Key` (1 to 128 visible ASCII) is stored in `dispatch_commands.idempotency_key`; unique `(account_id, device_id, idempotency_key)` from M0005 is the authority; same key and fingerprint returns the original 202; different fingerprint is 409 `idempotency-key-reused`; Dragonfly `idem` domain not used. (ADR-0036 D5)
- **D-26:** `expiresAt = issuedAt + Dispatch:QueueTtl` (default and max 5 min); offline commands queue for at most 5 min then `expired`, never delivered; `queueIfOffline:false` gives 409 `device_offline`; sweeper every 15 s with injected `TimeProvider`; device refuses `expiresAt` older than `Dispatch:ClockSkew` (30 s, max 2 min). (ADR-0036 D6; AG-74-8)
- **D-27:** After `received` the run is bounded by `Dispatch:RunLimits` (4 h tasks; 60 s `session.*` and `command.cancel`) ending `failed(timeout)`; local approval wait bounded by `Dispatch:CommandApprovalTimeout` (default 5 min) ending `failed(approval_timeout)`. A device that disconnects keeps its command `running`; `hello` lists up to 32 running ids, others become `failed(device_restarted)`. (ADR-0036 D6; R16)

### Local policy, approval, revocation, reconnect
- **D-28:** Policy is on the desktop and cannot be raised remotely: `sessions` read and control accepted without prompt; `code` and `cowork` disabled until the user enables them; `browser_use` refused unless Browser Use is enabled for dispatched sessions (ADR-0034 policy applies); `computer_use` refused unless `Cowork:ComputerUse:Enabled`, `Cowork:ComputerUse:AllowDispatchSessions` and the on-desktop opt-in hold, then a local approval at the start of every such task plus the per-action gate of ADR-0035; hard-coded floor, no setting; no remote decision. "Mode" means only `CoworkPermissionMode`; a dispatched task never changes it. (ADR-0036 D7; R1; R16; AG-74-9)
- **D-29:** The approval prompt shows controller device name, operation, capability, `toolClasses`, first 200 chars of task text as plain text; choices Approve once, Deny, Deny and pause Dispatch. A persistent "Remote task running" strip with Stop shows while any command is accepted or running; Computer Use tasks add the ADR-0035 D6 indicator "Remote (Dispatch): <device>"; Dispatch-origin grants last 30 min and never carry `AutoApproveTyping`. "Pause Dispatch" reports `status:"paused"` in `hb` and the hub fails queued commands `policy_denied`. (ADR-0036 D7; AG-74-17)
- **D-30:** Revocation (`DELETE /dispatch/devices/{id}` and `DELETE /dispatch/devices`) sets `revoked_at`, clears `jti`, cancels `queued` and `delivered` (reason `revoked`), fails `running` (`revoked`), deletes the presence key, publishes a revoke signal, sends `revoke`, closes 4001, all within 2 s; next connect is 401 `device_revoked`; the desktop ends dispatched sessions and withdraws grants; "Sign out everywhere" calls revoke-all first, best effort. (ADR-0036 D8; AG-74-10)
- **D-31:** Reconnect is the connector's own unbounded loop: delay uniform(0, min(30 s, 1 s x 2^attempt)), reset after 60 s up; injected `TimeProvider` and `IJitterSource`; 1001, 1013 and network errors retry with jitter; 4020 waits the advertised delay; 4009 waits 60 s with a message; 4001, 4002, 4010, 4011 stop with a specific state. `ReconnectPolicy` is not extended; `AcpWebSocketTransport` is reused unchanged. (ADR-0036 D9; AG-74-11)

### Presence, limits, audit, retention
- **D-32:** `hb` every 15 s (`lastAppliedSeq`, running count, status `idle|busy|approval_pending|paused`); three missed beats (45 s) is stale. Presence key `autonogrammer:v1:presence:{tenant}:{deviceId}`, value `<instanceId>:<connectionId>`, `SET ... PX 45000` (never SET then EXPIRE), Lua compare-and-set refresh, built only by `DragonflyKeyspace`; `last_seen_at` persisted at most every 60 s. (ADR-0036 D10; AG-74-14)
- **D-33:** A command insert publishes the device id on `autonogrammer:v1:sig:dispatch` through new `IDispatchSignals` (interface in `Coordination`, Dragonfly and in-memory implementations); store is the queue, signal a hint, poll the safety net. `Dispatch:ReplicaMode=Single` at first launch; `Multi` needs `Coordination:Store=Dragonfly` and Postgres; outside Development Dragonfly is required in both modes; in-memory only in Development with `Single`. User feed ids come from `accounts.feed_seq`, never `CatalogEventHub`. (ADR-0036 D10; AG-74-14)
- **D-34:** Dragonfly down: sockets stay, presence is `unknown` (never offline), new hellos 1013, command POST 503 `data_plane_unavailable` with `Retry-After`; Postgres down fails every write and connect closed. (ADR-0036 D10; R13)
- **D-35:** Per-account token buckets partitioned by `sub`: `DispatchCommands` 30/min burst 10, `DispatchPairing` 5/hour, `DispatchRead` 120/min; queue cap 100, 20 devices per account, payload 64 KiB, frame 1 MiB, result 4,096, progress 1,024 chars and 50 events per command; 429 via `RateLimitRejection.Handler`; failed connects limited per source address and device id. (ADR-0036 D11; AG-74-12)
- **D-36:** `dispatch_audit` (M0005; `account_id_hash` is the keyed tenant hash) row is written in the same transaction as every state change, never holds payload, prompt, token, device name, email or address; ledger copy via ADR-0031's `ledger_outbox` only when `DataPlane:Ledger:Enabled` (default false), closed `AuditLedgerEvent` fields through `IAuditLedger`. (ADR-0036 D12; AG-74-13)
- **D-37:** Command bodies live in `dispatch_commands.body`, nulled 7 days after a terminal state (`body_purged_at`); never logged; feed `dispatch_events` retains 30 days; ids from `accounts.feed_seq` under the account row lock. (ADR-0036 D13; ADR-0031 D4)

### Surfaces
- **D-38:** `Autonogrammer.Presentation/Dispatch/` holds `IDispatchHost`, `IThisDeviceHost`, `FakeDispatchHost`, `NotConfiguredDispatchHost`, `DispatchHostException`, view models and `DispatchPhase` (`NoDevices`, `DeviceOffline`, `Queued`, `PendingApproval`, `Running`, `Failed`, `Completed`, `Cancelled`, `Expired`, plus `SignedOut`, `Disabled`, `Unreachable`, `Loading`). Views are `Native.Views/Views/Dispatch/` and `UI/Components/Dispatch/`; each phase has its own text, Cancel is a keyboard-reachable named button, polite live region for changes and assertive for failures, reduced motion honoured, Android target 44. (ADR-0036 D14; AG-74-15)
- **D-39:** Hosts: `Autonogrammer.Dispatch.Client` (L3) `HttpDispatchHost` for Native and Android (token from `IAccountSession.GetAccessTokenAsync` adapted to `Func<CancellationToken, ValueTask<string?>>`); the web head implements `IDispatchHost` in `apps/Autonogrammer.Web/Services/Dispatch/` over `ApiHttp` and `SseEventStream`; handlers for `sessions`, `code`, `cowork` are in the Native head. Dispatch-started sessions reach the Cowork session host and never enter the Code catalog. (ADR-0036 D14; AG-74-18)
- **D-40:** Degraded states each get their own model state and message: `SignedOut`, `Disabled`, `Unreachable`, `NoDevices`, `DeviceOffline`, `PresenceUnknown`, `CredentialExpired`, `DeviceRevoked`, `ReplacedByOtherInstance`, `UpdateRequired`, `AccountUnavailable`, `RateLimited`, `PayloadTooLarge`, `CapabilityUnavailable`, `PausedByUser`, `CoordinationPaused`, `KeyStoreUnavailable`, `PairedToAnotherAccount`, `PendingApproval` and each failure reason. (ADR-0036 D16; brief 74 line 82)

### Claude's Discretion
- Internal class layout inside `Dispatch/` subfolders beyond the file list; metric names and tags (bounded to `result`, `reason`); `LoggerMessage` event ids; shape of `IDispatchStore` beyond the contract tests; DTO field names not fixed by the ADR; handler wiring inside the Native head; Settings page layout.
</decisions>

<specifics>
## Specific Ideas

- Herdr semantics are borrowed by pattern only: `hello{lastAppliedSeq}` is attach-after-sequence, "device offline" is `Lost`, never reported running (report 08:28-31).
- Anthropic's Dispatch is the product reference; Dispatch-spawned computer-use approvals expire after 30 minutes there (report 06:99).
- Tests use a manual `TimeProvider`, no sleeps; the test `WebSocket` client follows `WorkerSocket`.
</specifics>

<canonical_refs>
## Canonical References

- `docs/decisions/ADR-0036-dispatch-relay.md` - D1 to D16 (the authority).
- `docs/plans/v0.16.0-research/briefs/74-dispatch.md` - requirements, units, success criteria, OQ-420 to OQ-431.
- `docs/plans/v0.16.0-research/briefs/00-index.md` - phase table, naming rules (section 2), R1, R2, R9, R10, R13, R16, R17, R18.
- `docs/plans/v0.16.0-research/briefs/00-scaffold-manifest.md` - rows 20, 21, T13, T14, `LayerMap` tuples (none for Dispatch).
- `docs/plans/v0.16.0-research/08-dispatch-relay-prior-art.md` - reuse map (section 2), design (section 4), risks (section 5).
- `docs/plans/v0.16.0-research/03-api-auth-backend.md` - section 11.3 and 11.4 seams, section 6 rate limits.
- `docs/plans/v0.16.0-research/05-supabase-auth-reference.md` - line 93 (`amr` claim), line 144 (F7: the `accounts` row decides).
- `docs/plans/v0.16.0-research/06-cowork-parity-and-references.md` - row C18 (line 79), Dispatch mechanics (line 96).
- `docs/decisions/ADR-0030`, `ADR-0031`, `ADR-0032`, `ADR-0034`, `ADR-0035` - accounts, data plane, Cowork host, Browser Use, Computer Use (sibling contracts).
- `docs/runbooks/hosting.md` - API service does not exist (line 15); `docs/open-questions.md` OQ-420 to OQ-431, OQ-73, OQ-168.

### Existing code to read before planning (verified)
- `services/Autonogrammer.Api/Cloud/Workers/WorkerTokenIssuer.cs`, `Cloud/CloudDisabledMiddleware.cs`, `Sse/SessionEventStreamer.cs`, `Endpoints/EventStreamEndpoints.cs`, `Endpoints/SessionEndpoints.cs`, `Idempotency/IdempotencyKeyHeader.cs`, `Security/SessionRateLimiterPolicy.cs`, `Security/RateLimitRejection.cs`, `Program.cs`.
- `services/Autonogrammer.AgentWorker/Security/WorkerJwtValidator.cs`, `Bridge/AcpFrameGuard.cs` (both `internal`).
- `src/Autonogrammer.Harness.Acp/AcpWebSocketTransport.cs`, `AcpWebSocketTransportOptions.cs`; `src/Autonogrammer.EngineHost/ReconnectPolicy.cs`; `src/Autonogrammer.Harness.Herdr/HerdrHeartbeatMonitor.cs`; `src/Autonogrammer.Sessions.Postgres/PostgresSessionRepository.cs`; `apps/Autonogrammer.Native/Composition/HeadServices.cs`.
- Tests: `tests/Autonogrammer.Api.IntegrationTests/Cloud/{TestOwnerAuthHandler,CloudApiHost,FakeCloudWorker,ManualClock}.cs`, `tests/Autonogrammer.AgentWorker.Tests/WorkerSocket.cs`, `tests/Autonogrammer.ArchitectureTests/LayerMap.cs`, `scripts/test-services.sh`.
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `AcpWebSocketTransport` as the desktop transport unchanged (`BearerTokenProvider` called on every connect); `WorkerTokenIssuer` as template for the credential issuer (new audience and key); `SessionEventStreamer` wake-plus-200 ms-poll pattern for delivery loop and feed; `IdempotencyKeyHeader` and `SessionEndpoints` idempotency flow; `CloudDisabledMiddleware` and `RateLimitRejection.Handler`.
- Created by earlier phases: `IAccountSession`, `Accounts.Desktop`, `Harness.Secrets.Os` (67); `DragonflyKeyspace`, `ITenantHasher`, `IAuditLedger`, `ledger_outbox`, `accounts.feed_seq`, M0005/M0008/M0009, `Autonogrammer.Resilience` (68); `SurfaceHost`, `HeaderEndSlot`, `SurfaceChromeModel`, `ICoworkSessionCatalog` (69, 70); `IComputerApprovalPrompt` (73).

### Established Patterns
- Owner scoping by `sub` with 404 for foreign rows (`FindOwnedSessionAsync`); feature off via mapped routes plus middleware; options with `IValidateOptions` and `ValidateOnStart`; `LoggerMessage` with event ids and no body; store contract base class run per implementation; Dragonfly keys only through `DragonflyKeyspace`.

### Integration Points
- `Program.cs` (WebSockets, scheme, policies, map), `appsettings.json`, `.env.example`, `docs/api/openapi.json`; migration runner's compiled list (add M0010); `Coordination` (`IDispatchSignals`); Native `HeadServices.cs`; Web `Services/Dispatch/`; settings enum entries, `ViewMapping.cs`.
</code_context>

<deferred>
## Deferred Ideas and Omitted-by-Decision

- OMITTED: `computer.*` operation (R1); live screen or browser streaming (OQ-73 pending, ADR-0036 Blocked); push notifications (OQ-428); full mobile app, Android release (OQ-429); separate relay service (ADR-0036 Candidate); transcript mirroring (OQ-421); ssh reverse tunnel (report 08:54).
- Open questions and defaults (brief 74 lines 90-101): OQ-420 Railway long WebSockets (assume yes, jittered reconnect; blocked external); OQ-421 no transcript mirror; OQ-422 copy JWT parameter set, extract on third use; OQ-423 purge `result` with body at 7 days (ADR-0031 owner); OQ-424 `IAccountStatus` unavailable means status not active or `deleted_at` set; OQ-425 no application-layer encryption of bodies; OQ-426 no controller end-to-end signing; OQ-427 `cowork.task` refuses `browser_use` until ADR-0034 policy exists; OQ-428 no push; OQ-429 Android compile-only; OQ-430 `amr` kept across refresh assumed, first live run checks; OQ-431 Linux without Secret Service cannot use Dispatch.
- Driver lease per session, payload pub/sub, hub key end-to-end signing: later (report 08:218, 279).
</deferred>
