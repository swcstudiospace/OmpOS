# Phase 74: Dispatch relay - Research
Source: docs/plans/v0.16.0-research/briefs/74-dispatch.md

Summarises research reports 08 (dispatch prior art), 03 (API and auth), 05 (Supabase), 06 (parity row C18), 09 (recipe), plus ADR-0036 and index R1, R2, R10, R16, R18.

## Standard Stack / What to reuse
- `src/Autonogrammer.Harness.Acp/AcpWebSocketTransport.cs` as the desktop transport unchanged; `BearerTokenProvider` is required and called on every connect (`AcpWebSocketTransportOptions.cs:23`); frame cap `DefaultMaxFrameBytes` 1 MiB (`:10`), keep-alive 20 s (`:13`).
- `services/Autonogrammer.Api/Cloud/Workers/WorkerTokenIssuer.cs:18` (`Audience = "autonogrammer-worker"`): template for the credential issuer; Dispatch needs a NEW audience and key (report 08:42).
- `services/Autonogrammer.AgentWorker/Security/WorkerJwtValidator.cs:17` and `Bridge/AcpFrameGuard.cs:11` are `internal`: copy, do not reference (OQ-422).
- `services/Autonogrammer.Api/Sse/SessionEventStreamer.cs:24` (`PollInterval` 200 ms) and `Endpoints/EventStreamEndpoints.cs:49` (`Last-Event-ID`): delivery loop and feed pattern.
- `services/Autonogrammer.Api/Cloud/CloudDisabledMiddleware.cs:33` (`cloud_disabled`): copy as `dispatch_disabled`.
- `services/Autonogrammer.Api/Security/RateLimitRejection.cs:20` (`Handler`); `SessionRateLimiterPolicy.cs:37` is one global bucket named `"prompts"`, so partitions by `sub` are new.
- `services/Autonogrammer.Api/Endpoints/SessionEndpoints.cs:144` idempotency flow; `Idempotency/IdempotencyKeyHeader.cs`.
- `src/Autonogrammer.Sessions.Postgres/PostgresSessionRepository.cs:45-46` insert with `seq`; report 08:84 for the lock technique.
- `src/Autonogrammer.Harness.Herdr/HerdrHeartbeatMonitor.cs` (15 s beat, 45 s stale; report 08:29).

## Facts the planner needs
- Nothing relay-like exists: no device registry, presence, signed channel (report 08:8). `UseWebSockets` is absent from `services/Autonogrammer.Api/Program.cs` (grep, no match) and present only at `services/Autonogrammer.AgentWorker/WorkerApp.cs:53`.
- Direction is inverse to the worker bridge (API dials worker); the desktop dials out (report 08:10). Browsers cannot set `Authorization` on a WebSocket, so controllers use POST plus SSE (report 08:240).
- `ReconnectPolicy` caps attempts at 100 (`src/Autonogrammer.EngineHost/ReconnectPolicy.cs:11`), no jitter: not reusable; build an own loop (ADR-0036 D9).
- `ISessionSignals` carries no payload (report 03:143); hence new `IDispatchSignals` on its own channel (report 08:66).
- Pairing freshness is the newest non-refresh `amr` entry (R18); claim list at report 05:93; the `accounts` row, not token validity, decides account membership (report 05:144, F7).
- Defaults: queue TTL 5 min, credential 30 d (max 90), payload 65,536 (max 262,144), skew 30 s (max 2 min), 20 devices, queue 100, `CommandApprovalTimeout` 5 min (ADR-0036 D6, D11, D2). Report 08:200 proposed other expiries (120 s, 15 min, 24 h): superseded by ADR-0036 D6.
- Migration list is M0001 to M0010; M0010 is this phase's U2; M0009 is `cowork_projects_memory` (R2). U2 needs phase 68 U3 first (brief 74 line 55).
- Layer rules: `Dispatch.Client` and `Dispatch.Desktop` are L3, no `SameLayerAllowed` tuple (ADR-0036 Consequences; manifest rows 20, 21). Web head must stay free of process-spawning references (`tests/Autonogrammer.ArchitectureTests/ProjectGraphTests.cs:36-41`). Process ban is `Directory.Build.props:52` (exempt: `Autonogrammer.Harness.*`, `Autonogrammer.Sandbox`, `*Tests`).
- The device key never goes to the file store; compile-only on Windows and macOS (brief 74 line 86).
- Railway: API service does not exist and deploys are paused (`docs/runbooks/hosting.md:15`); OD-03 sign-in authority unset.

## Architecture Patterns
- Store is the queue, Dragonfly signal is a hint, 200 ms poll is the safety net (report 08:222; ADR-0036 D10).
- Compare-and-set state table for every ack/res/evt; replay is a no-op plus audit row (ADR-0036 D5; report 08:208).
- Hub signs at delivery over canonical bytes from stored body; nothing about the signature is stored (ADR-0036 D4).
- Audit row in the same transaction as the state change; ledger copy through ADR-0031 outbox, default off (ADR-0036 D12).
- Presentation (L2, framework-free) models plus views written once per renderer (Avalonia and Razor); fake host for UI tests (ADR-0036 D14).

## Pitfalls
- Dragonfly idempotency completion is best-effort (`IdempotencyCompletion.cs`, report 08:256): the unique Postgres key is the authority.
- `CatalogEventHub` and `SessionRegistry` are process-local; a user feed from them breaks with 2+ replicas (report 08:251, 254): use `accounts.feed_seq`.
- Sockets half-open after a network change: newest-wins 4009, not 409 (report 08:216).
- Clock skew: hub time alone decides `expiresAt`; send `serverTime` (report 08:255).
- `SET` then `EXPIRE` on presence, or a Lua script with undeclared keys, breaks on Dragonfly (ADR-0036 D10).
- Signed values outside `[A-Za-z0-9._:-]` make canonical bytes ambiguous: refuse before signing and verifying (ADR-0036 D4).
- A `computer.*` operation or `code.prompt` registering `computer_*` violates R1; `approval.decide` must refuse kind `computer`.
- Do not name the Dispatch approval timeout `ApprovalTimeout` or call a policy a "mode" (R16).
- Logging a body, token or device name breaks D12/D15; canary tests needed.
- Do not edit `ContractVersion.cs`, `LayerMap.cs` or the solution files (R9, R17).

## Verification commands (brief 74 lines 65-70)
- `dotnet test --project tests/Autonogrammer.Contracts.Tests -c Release -- --filter-class "*Dispatch*"`
- `bash scripts/test-services.sh up`, then `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*"` and `dotnet test --project tests/Autonogrammer.Sessions.Postgres.Tests -c Release -- --filter-class "*Dispatch*"` (`scripts/test-services.sh:86` exports `AUTONOGRAMMER_TEST_POSTGRES_CONNECTION`).
- `dotnet test --project tests/Autonogrammer.Dispatch.Desktop.Tests -c Release` and `...Dispatch.Client.Tests`; Presentation, UI, Native, Web with `-- --filter-class "*Dispatch*"`.
- `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release`.
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror`; `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore`; regenerate `docs/api/openapi.json` (`GuardTests` equality).

## Cannot be verified on this host (index row 74; brief 74 line 86)
Android build and a real phone; Windows and macOS (compile only); live Supabase tokens; Railway long WebSockets and deploy restarts; Dragonfly multi-replica routing on Railway (two-replica test uses throwaway containers only); screen-reader behaviour of the approval prompt; the Substrate ledger (never called). Owner actions: two P-256 keys (`Dispatch__CommandSigning__KeyPem`, `Dispatch__Credentials__SigningKeyPem`), unpause deploys, create API service, set `Dispatch__Enabled`.

## Validation Architecture
- Framework: xUnit v3 on Microsoft Testing Platform ([INFERENCE from the `dotnet test --project` form, report 09:97]). Projects: `Autonogrammer.Contracts.Tests`, `Autonogrammer.Api.IntegrationTests`, `Autonogrammer.Sessions.Postgres.Tests`, `Autonogrammer.Dispatch.Desktop.Tests`, `Autonogrammer.Dispatch.Client.Tests`, `Autonogrammer.Presentation.Tests`, `Autonogrammer.UI.Tests`, `Autonogrammer.Native.Tests`, `Autonogrammer.Web.Tests`, `Autonogrammer.ArchitectureTests`.
- Quick run: `nice -n 15 dotnet test --project tests/Autonogrammer.Contracts.Tests -c Release -- --filter-class "*Dispatch*"` (report 09:99). Full run: the success-criteria list above, then `./bootstrap.sh`.
- Requirement to test map (brief 74 lines 54-59):
  - AG-74-5, 6: `DispatchCanonicalTests` (shared vector file), `DispatchTransitionTests`, `DispatchFrameGuardTests`; U2 `seq` strictly increasing under 32 concurrent inserts.
  - AG-74-1, 2, 3, 12, 16, 17: `Api.IntegrationTests/Dispatch/*` (pairing, stale sign-in 403, cross-owner 404 on every route, `Idempotency-Key` replay and 409, 429, disabled 404, feed resume and `resync`).
  - AG-74-4, 7, 8, 10, 13, 14: U4 hub tests with test `WebSocket` client and manual clock (ordering, redelivery, 4009, expiry at 300 s, revoke within 2 s, 4002, Dragonfly down, ledger off writes no outbox row).
  - AG-74-1, 9, 11, 18: `Dispatch.Desktop.Tests` (forged, replayed, expired, wrong-user, wrong-kid refused; restart does not re-run; jitter bounds; each close code; no file key store; `approval.decide` kind `computer` refused) and `Native.Tests`.
  - AG-74-15, 16, 17: `Presentation.Tests`, `UI.Tests` (bUnit, aria-live), `Native.Tests` (Avalonia.Headless), `Dispatch.Client.Tests` (SSE resume, 401, 429), `Web.Tests`, `HeadMotionTests`.
- Wave 0 gaps (none exist yet): `Autonogrammer.Dispatch.Desktop.Tests` and `.Client.Tests` projects (scaffold step); `tests/Autonogrammer.Api.IntegrationTests/Dispatch/`; store contract base class and in-memory store; shared canonical vector file; `Sessions.Postgres.Tests` has only `PodmanPostgresContainer` helpers today; a test Dragonfly two-replica fixture (`Coordination.Tests` has podman Dragonfly per report 08:244); M0010 runner registration test.
