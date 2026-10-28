# Brief 74: Dispatch relay (ADR-0036)

Names are post-rename (OmpOS becomes Autonogrammer); the repo on disk still uses OmpOS, so `Autonogrammer.Api` is `OmpOS.Api` there. Decisions D1 to D16 are in [ADR-0036](../../../decisions/ADR-0036-dispatch-relay.md); this brief only splits the work. Research: reports 08, 03, 04, 05, 06 (row C18), 09. Reconciled with ADR-0030 (brief 67), ADR-0031 (brief 68), ADR-0032 (briefs 69, 70), ADR-0035 (brief 73).

## Goal

A signed-in client (phone app, web app, another desktop) sends tasks to one of the account's registered desktops through a relay in the existing `Autonogrammer.Api`; desktops connect outbound only. Delivered: contracts, API (registry, hub, feed), desktop connector, and the Devices and Dispatch surfaces for the Native head (shared with Android) and the Web head. Not delivered: a full mobile app, push, screen streaming, transcript mirroring.

## Requirements

- AG-74-1 A desktop pairs under the signed-in account and receives a device id plus an API-signed ES256 credential (own key variable) bound to a device-held P-256 key stored only in the OS secret store.
- AG-74-2 Pairing requires a full sign-in no older than 10 minutes (newest non-refresh `amr` entry of the authenticated principal); Dispatch never re-validates user tokens.
- AG-74-3 Devices can be listed, renamed and revoked (one or all); each carries a capability list with versions, presence and last seen.
- AG-74-4 The hub is a WebSocket endpoint in `Autonogrammer.Api`; the newest connection of a device wins (close 4009); the hello challenge is single use (Dragonfly `auth` nonce).
- AG-74-5 Every command is a signed envelope (commandId, per-device seq, userId, deviceId, issuedAt, expiresAt, nonce, capability, payload at most 64 KiB, ES256 signature over canonical bytes), signed at delivery.
- AG-74-6 Per-device `seq` is strictly increasing and gap-free at allocation; delivery is at-least-once; the device applies each command once.
- AG-74-7 Acknowledgement states received, accepted, running, completed and failed carry a typed reason from a closed set.
- AG-74-8 A command for an offline device queues for at most 5 minutes and is then expired and never delivered.
- AG-74-9 No `computer.*` operation exists; Computer Use is a tool class named in the `toolClasses` of a `cowork.task` payload (`computer_use` is a capability flag and never a command capability; ADR-0035 D12 registers `computer_*` only for such a task on a desktop with `Cowork:ComputerUse:Enabled` and `Cowork:ComputerUse:AllowDispatchSessions`, and never for `code.prompt`), needs a local approval at task start and per action (ADR-0035), and cannot be approved remotely.
- AG-74-10 Revocation closes the socket within 2 seconds, fails queued and running commands, and makes the desktop end dispatched sessions and withdraw grants; "Sign out everywhere" revokes all devices.
- AG-74-11 Reconnect uses full-jitter backoff, unbounded, ceiling 30 seconds, with close-code specific behaviour.
- AG-74-12 Per-account rate limits (partitioned by `sub`), a per-device queue cap and a per-account device cap hold.
- AG-74-13 Every state change writes a `dispatch_audit` row in the same transaction; the ledger copy is ADR-0031's outbox, default off.
- AG-74-14 Presence is a Dragonfly TTL key under `autonogrammer:v1:presence`; first launch is one replica; the validator refuses unsafe multi-replica and in-memory settings.
- AG-74-15 The Devices and Dispatch views show no_devices, device_offline, queued with expiry, command_pending_approval, running with cancel, failed with reason, completed with summary, and every degraded state, accessibly.
- AG-74-16 Controllers use HTTPS POST plus SSE with `Last-Event-ID` resume only; no browser WebSocket.
- AG-74-17 The feature is off by default (404 `dispatch_disabled`); the desktop shows a persistent "Remote task running" strip.
- AG-74-18 Dispatch-started sessions use `SessionSurface.Code` or `Cowork` of ADR-0030 and reach the Cowork session host of ADR-0032; Cowork rows never enter the Code catalog.

## Depends on

Brief 67 (ADR-0030): `Autonogrammer.Accounts` (`IAccountSession`), `Autonogrammer.Accounts.Desktop`, `Autonogrammer.Harness.Secrets.Os`, API `Accounts/` module, `IAccountFeature` registration, `SessionSurface`. Brief 68 (ADR-0031): migrations M0005, M0008 and M0009 (the compiled list this phase extends with M0010), `DragonflyKeyspace` and the presence, nonce and rate helpers, `ITenantHasher`, `Autonogrammer.Resilience`, `IAuditLedger` and `ledger_outbox`, `accounts.feed_seq`. Briefs 69 and 70 (ADR-0032): `SurfaceHost`, `HeaderEndSlot` and `SurfaceChromeModel` (Devices entry), `ICoworkSessionCatalog`, the Cowork session host. Briefs 72 and 73 (ADR-0034, ADR-0035): tool classes, `Cowork:ComputerUse:AllowDispatchSessions`, `IComputerApprovalPrompt`. Existing: `AcpWebSocketTransport`, `WorkerTokenIssuer` and `WorkerJwtValidator` patterns, `ReconnectPolicy` shape.

## Projects and files owned

| Project (layer) | New or existing | Files |
| --- | --- | --- |
| `Autonogrammer.Contracts` (L0) | existing, new folder | `Dispatch/`: frames, envelope, `DispatchCapability`, `DispatchOp`, `DispatchCommandState`, `DispatchFailureReason`, `DispatchCloseCodes`, `DispatchCanonical`, REST DTOs, `DispatchJsonContext`, `IDispatchStore`; additive; no edit to `ContractVersion.cs` (the scaffold step bumps it to 1.6.0) |
| `Autonogrammer.Api` (L3) | existing, new module | `Dispatch/` (Devices, Commands, Hub, Events, Audit writer, Store/InMemory, Options, Metrics, rate limits, middleware, `IAccountFeature` registration); edits to `Program.cs`, `appsettings.json`, `.env.example`, `docs/api/openapi.json` |
| `Autonogrammer.Sessions.Postgres` (L2) | existing | `Migrations/M0010_dispatch_device_state` (file-owned by this phase, unit U2; additive columns `credential_jti`, `credential_expires_at`, `capabilities`, `app_version`, `protocol`, `revoke_reason`, `paired_session_id`, `last_source_prefix`, unique `(id, account_id)`), registered in the migration runner's compiled list by U2, `PostgresDispatchStore.cs` over M0005 tables |
| `Autonogrammer.Coordination` and `.Dragonfly` (L1, L2) | existing | `IDispatchSignals`, Dragonfly and in-memory implementations on the `sig:dispatch` channel |
| `Autonogrammer.Dispatch.Desktop` (L3) | **new** + `.Tests` | connector, reconnect, verifier, dedupe store, policy store, handler interfaces |
| `Autonogrammer.Dispatch.Client` (L3) | **new** + `.Tests` | `HttpDispatchHost`, SSE reader |
| `Autonogrammer.Presentation` (L2), `Native.Views` (L3), `UI` (L2) | existing | `Dispatch/` models and host interfaces; `Views/Dispatch/`; `Components/Dispatch/`; `ViewMapping.cs`, settings enum entries |
| `Autonogrammer.Native`, `Android`, `Web` (L4) | existing | Native `Dispatch/` composition and handlers, `HeadServices.cs`; Android composition; Web `Services/Dispatch/`, route surface |
| `tests/Autonogrammer.ArchitectureTests` | existing | no edit by this phase: the scaffold step adds the `LayerMap.cs` lines and project references; no `SameLayerAllowed` entry |

The serialised scaffold step registers every new project (slnx, slnf, LayerMap, ArchitectureTests reference, lock files, project files, test projects); this phase adds code only and edits a registry file only for rows the scaffold manifest marks "per phase" (OpenAPI, notices when a package lands, `docs/architecture.md` prose, parity audit rows), plus `CHANGELOG.md`. No new NuGet package.

## Work units (file-disjoint)

| # | Unit | Owner project | Tests | Acceptance |
| --- | --- | --- | --- | --- |
| U1 | Contracts, canonical bytes, closed enums, transition table, store interface, in-memory store, `IDispatchSignals` | Contracts, Api `Dispatch/Store`, Coordination | `Contracts.Tests` (`DispatchCanonicalTests` with a shared vector file, `DispatchTransitionTests`, `DispatchFrameGuardTests`), store contract base class on in-memory | Same bytes on hub and device for every vector; illegal transitions refused; unknown `op` or `t` rejected; signed values outside `[A-Za-z0-9._:-]` refused |
| U2 | Postgres store over M0005 plus M0010 (this unit writes `M0010_dispatch_device_state` and registers it in the runner list): atomic `command_seq` allocation and queue cap, composite owner key, body purge hook, feed ids from `accounts.feed_seq` | `Sessions.Postgres` | store contract base class against the throwaway Timescale container; two-writer `seq` race | `seq` strictly increasing under 32 concurrent inserts; cap atomic; foreign account reads nothing; idempotent DDL; needs brief 68 unit U3 |
| U3 | API registry and controller leg: options plus validator (section `Dispatch`, including `CommandApprovalTimeout` default 5 minutes), disabled middleware, `DispatchDevice` scheme (the OpenAPI transformer lists the connect route with scheme `DispatchDevice`, next to the anonymous operations), credential issuer and validator, pairing with the freshness rule, device and command routes, `DELETE /dispatch/devices`, SSE feed, rate limits, OpenAPI | Api `Dispatch/Devices`, `Commands`, `Events`, `Options` | `Api.IntegrationTests/Dispatch/*` (pairing, stale sign-in 403, cross-owner 404 on every route, `Idempotency-Key` replay and 409, 429, disabled 404, feed resume and `resync`) | Credential refused off the connect route, access token refused on it; options failures name keys only; no user-token validation in Dispatch |
| U4 | Relay hub: `UseWebSockets`, handshake with Dragonfly nonce, frame pump, signer, delivery loop, sweeper, presence, revocation, audit writer through `IAuditLedger` | Api `Dispatch/Hub`, `Audit` | test `WebSocket` client and manual clock: ordering, redelivery, 4009, expiry at 300 s, revoke within 2 s, 4002, Dragonfly down (presence unknown, 1013, 503 `data_plane_unavailable` on POST), ledger off writes no outbox row | An offline command expires after 300 s and is never delivered; every D3 close code and D5 transition covered; no body in any log line or audit row |
| U5 | Desktop connector and Native composition: verifier, persisted dedupe, clock offset, reconnect loop, policy store, approval flow, pairing with `KeyStoreUnavailable`, revoke handling, handlers for `sessions`, `code`, `cowork` | `Dispatch.Desktop`, Native `Dispatch/`, `Composition/HeadServices.cs` | `Dispatch.Desktop.Tests` (forged, replayed, expired, wrong-user, wrong-kid frames refused; restart does not re-run; jitter bounds; each close code; no file key store); `Native.Tests` | Zero execution of a refused frame; local prompt at every `computer_use` task start; `approval.decide` for kind `computer` refused; revoke ends dispatched sessions |
| U6 | Surfaces: Presentation models and hosts, Avalonia and Razor views, `HttpDispatchHost`, web host, Android composition, Devices entry through `IAccountFeature` and `SurfaceChromeModel`, settings page | Presentation, Native.Views, UI, `Dispatch.Client`, Web, Android | `Presentation.Tests`, `UI.Tests` (bUnit, aria-live, keyboard), `Native.Tests` (Avalonia.Headless, automation names), `Dispatch.Client.Tests` (SSE resume, 401, 429), `Web.Tests` (route surface, OpenAPI coverage), `HeadMotionTests` | Every phase and degraded state renders its own text; Cancel keyboard reachable and named; failure announced assertively; reduced motion honoured; Android target 44 |

U1 first; U2, U3 and U5 follow on U1 in parallel; U4 needs U1 and U2; U6 needs U1 and runs against a fake host.

## Success criteria

- `dotnet test --project tests/Autonogrammer.Contracts.Tests -c Release -- --filter-class "*Dispatch*"` passes.
- `bash scripts/test-services.sh up`, then `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*"` and `dotnet test --project tests/Autonogrammer.Sessions.Postgres.Tests -c Release -- --filter-class "*Dispatch*"` pass.
- `dotnet test --project tests/Autonogrammer.Dispatch.Desktop.Tests -c Release` and `...Dispatch.Client.Tests` pass with no sleeps (manual `TimeProvider`).
- `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*Dispatch*"`, and the same filter for `UI.Tests`, `Native.Tests`, `Web.Tests`, pass.
- `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` passes (LayerMap, `DecisionRecordTests` for ADR-0036 and the README row, Web free of process-spawning references, no TODO marker without an OQ row).
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` and `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore` are clean; `docs/api/openapi.json` is regenerated and `GuardTests` equality holds.

## Threat model

- Stolen device credential: proof of possession by the device key, `jti` rotation, revoke, new-network flag. Residual: thief with credential and key is the device.
- Replay: signature over device, seq, commandId, nonce, expiry, payload hash; persisted device high-water; unique `(device_id, seq)`; compare-and-set acks.
- Malicious or compromised relay: no `computer.*` op; local start approval plus per-action gate; pinned hub keys. Residual: forged `code` and `cowork` commands for devices that enabled them.
- Confused deputy between users: account check on every hop, composite key, device refuses a foreign `userId`, the `accounts` row decides what is an account on the shared Supabase project.
- Self-approval, prompt injection, account takeover, denial of service, leaks: ADR-0036 D15 rows 5 to 9.

## Degraded and error states

`SignedOut`, `Disabled`, `Unreachable`, `NoDevices`, `DeviceOffline` (last seen, expiry countdown), `PresenceUnknown`, `CoordinationPaused`, `CredentialExpired`, `DeviceRevoked`, `ReplacedByOtherInstance`, `UpdateRequired`, `AccountUnavailable`, `RateLimited`, `PayloadTooLarge`, `CapabilityUnavailable`, `PausedByUser`, `KeyStoreUnavailable`, `PairedToAnotherAccount`, `PendingApproval` ("waiting for approval on the desktop"), and each failure reason. Hub: fail closed when Postgres is unreachable; Dragonfly loss pauses sends and hellos but keeps established sockets.

## Cannot be verified on this host

Android build and a real phone; Windows and macOS compile only (credential store, desktop head); live Supabase tokens; Railway behaviour of long WebSockets and deploy restarts; Dragonfly multi-replica routing on Railway (the two-replica test uses throwaway containers only); real screen-reader behaviour of the approval prompt (NVDA, VoiceOver, TalkBack); the Substrate ledger (never called by tests). Owner actions: generate the two P-256 keys and set `Dispatch__CommandSigning__KeyPem` and `Dispatch__Credentials__SigningKeyPem`, unpause Railway deploys, create the API service, enable `Dispatch__Enabled` after sign-in works.

## Open questions (default; owner)

- OQ-420 Can a long-lived WebSocket through the Railway edge carry this traffic, and does the API ever run Serverless? Default: assume yes with jittered reconnect; ask Railway with OQ-73; owner.
- OQ-421 Mirror a dispatched desktop transcript into the API session log? Default: no; text progress and summary only; owner (privacy).
- OQ-422 Extract the worker's JWT parameter set and `AcpFrameGuard` into a shared library? Default: copy now, extract on the third use; architect.
- OQ-423 Purge `dispatch_commands.result` text together with the body at 7 days (ADR-0031 amendment)? Default: yes; owner of ADR-0031.
- OQ-424 Which `accounts.status` values mean unavailable for `IAccountStatus`? Default: any value other than active, and `deleted_at` set; owners of ADR-0030 and ADR-0031.
- OQ-425 Application-layer encryption of command bodies at rest. Default: none; owner.
- OQ-426 Controller end-to-end command signing. Default: not in v0.16.0; architect.
- OQ-427 Browser Use policy for dispatched tasks. Default: ADR-0034's policy applies, `cowork.task` refuses `browser_use` until it exists; owner of ADR-0034.
- OQ-428 Push notifications for approval and done. Default: none; clients poll and resume by sequence (OQ-14); owner.
- OQ-429 Android as controller beyond a compile-checked composition. Default: compile-only, no store release; owner.
- OQ-430 Does Supabase keep the original sign-in `amr` entry across refreshes? Default: assumed; the first live run checks it, else pairing asks for a fresh sign-in each time; owner (live run).
- OQ-431 A Linux desktop without a Secret Service cannot use Dispatch (no file key fallback, ADR-0030 D6 rule 4). Default: accepted, shown as `KeyStoreUnavailable`; owner.

## Index rows

ADR index (docs/decisions/README.md): `| [ADR-0036](ADR-0036-dispatch-relay.md) | Dispatch relay: control of the user's desktops through an account-bound relay | Proposed 2026-10-28 | Hub in Autonogrammer.Api, outbound-only desktops, ES256 device credential with proof of possession and OS-store-only key, signed commands with 5 minute queue expiry, local approval floor for Computer Use, instant revocation; M0010 adds device columns to ADR-0031's tables; live checks blocked on Railway and Supabase |`

Open-questions rows (docs/open-questions.md; columns ID, question, why it matters, default taken, owner, status): OQ-420 to OQ-431 as listed, status `open` (OQ-420 `open (blocked external)`). The final block OQ-420 to OQ-439 belongs to this brief (assigned by the design integrator).
