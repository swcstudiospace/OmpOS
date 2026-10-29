---
phase: "74"
slug: "dispatch-relay"
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-10-28"
updated: "2026-10-29 (integration amendments, plans 74-07 to 74-09)"
---

# Phase 74 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 on Microsoft Testing Platform (`dotnet test --project ... -- --filter-class`, report 09 section 6); bUnit for `UI.Tests`, Avalonia.Headless for `Native.Tests` |
| **Config file** | existing per-project csproj; the Dispatch.Desktop.Tests and Dispatch.Client.Tests projects are created by phase 66.1 |
| **Quick run command** | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Contracts.Tests -c Release -- --filter-class "*Dispatch*"` |
| **Full suite command** | the per-project commands of the verification map below, then `source "$(bash scripts/test-services.sh up)"` (and `bash scripts/test-services.sh down` afterwards) plus `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*"` and the Sessions.Postgres.Tests equivalent, then `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` |
| **Estimated runtime** | ~120 s per project; ArchitectureTests is the most expensive single project (builds every product project) |

Builds and tests run only through the serialising wrapper `/tmp/ag-bin/ag-dotnet`; never bare `dotnet`. Service-gated tests (Postgres, Dragonfly) need `source "$(bash scripts/test-services.sh up)"` first (the script prints the path of a file that exports `AUTONOGRAMMER_TEST_POSTGRES_CONNECTION` and `AUTONOGRAMMER_TEST_DRAGONFLY_CONNECTION`).

---

## Sampling Rate

- **After every task commit:** the task's own `<automated>` command (one project, filtered).
- **After every plan wave:** the full suite command above, plus `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Web.Tests -c Release -- --filter-class "*ServiceWorkerRules*"` and `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*GuardTests*"` from wave 2 on.
- **Before `/gsd:verify-work`:** full suite green, plus `/tmp/ag-bin/ag-dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror -nodeReuse:false -m:1` and `/tmp/ag-bin/ag-dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore`.
- **Max feedback latency:** ~180 s.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 74-01-01 | 01 | 1 | AG-74-4, AG-74-9 | T-74-01c | closed op/capability/close-code sets, no `computer.*`, unknown `t`/`op` rejected | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Contracts.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-01-02 | 01 | 1 | AG-74-5, AG-74-7 | T-74-01, T-74-01b | byte-identical canonical signing bytes, charset gate, transition table | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Contracts.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-01-03 | 01 | 1 | AG-74-6 | T-74-01b | in-memory store passes the shared store contract (seq, cap, idempotency, CAS, device cap, feed with resync and retention); `IJitterSource` bounds | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*Store*"` | ❌ W0 | ⬜ pending |
| 74-10-01 | 10 | 1 | AG-74-14 | — | `IDispatchSignals` wakes with the device id only; registered beside `ISessionSignals` in both coordination registrations | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Coordination.Tests -c Release -- --filter-class "*Registration*"` and `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Coordination.Tests -c Release -- --filter-class "*InMemoryDispatch*"` | ❌ W0 | ⬜ pending |
| 74-10-02 | 10 | 1 | AG-74-4, AG-74-14 | T-74-10, T-74-10b | nonce single-use and fail closed; presence one-command TTL, compare-and-set refresh, Unknown when Dragonfly is down | unit and integration (service-gated) | `source "$(bash scripts/test-services.sh up)" && /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Coordination.Tests -c Release -- --filter-class "*DragonflyDispatch*"` | ❌ W0 | ⬜ pending |
| 74-02-01 | 02 | 2 | AG-74-13 | — | M0010 idempotent, additive, registered after M0009 | integration (service-gated) | `source "$(bash scripts/test-services.sh up)" && /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Sessions.Postgres.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-02-02 | 02 | 2 | AG-74-6, AG-74-12, AG-74-13 | — | atomic seq and cap, audit row in the same transaction, foreign account reads nothing | integration (service-gated) | `source "$(bash scripts/test-services.sh up)" && /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Sessions.Postgres.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-02-03 | 02 | 2 | AG-74-6 | — | 32-writer race gap-free; body purge on injected clock | integration (service-gated) | `source "$(bash scripts/test-services.sh up)" && /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Sessions.Postgres.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-03-01 | 03 | 2 | AG-74-14, AG-74-17 | T-74-03d | options validator refuses unsafe settings including `Dispatch:Enabled` with `Api:Auth:Mode=OwnerToken` (D-41); default engine allow-list refuses the `Dispatch__*` key names (D-47); OpenAPI transformer lists `DispatchDevice` on connect only | integration | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*Options*"` | ❌ W0 | ⬜ pending |
| 74-03-02 | 03 | 2 | AG-74-1, AG-74-2 | T-74-03, T-74-03e | pairing needs a recent full sign-in; `DispatchDevice` scheme and policy: credential refused off the connect route, access token refused on it; connect re-checks `AllowedSubjects` (D-42, D-43) | integration | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*Pairing*"` | ❌ W0 | ⬜ pending |
| 74-03-03 | 03 | 2 | AG-74-14, AG-74-17 | T-74-03c | connect route: disabled 404, 503 `hub_not_ready`, NoTimeout metadata, limiter keyed by address and device id; `/dispatch` in the service worker prefixes; openapi.json equality | integration | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*"` then `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*GuardTests*"` then `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Web.Tests -c Release -- --filter-class "*ServiceWorkerRules*"` | ❌ W0 | ⬜ pending |
| 74-04-01 | 04 | 2 | AG-74-1, AG-74-6 | — | forged, replayed, expired, wrong-user and wrong-kid frames refused; restart does not re-run | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Dispatch.Desktop.Tests -c Release` | ❌ W0 | ⬜ pending |
| 74-04-02 | 04 | 2 | AG-74-1, AG-74-10, AG-74-11 | T-74-04c | jitter bounds, every close code, every pre-upgrade status (D-54), no file key store, revoke ends dispatched sessions | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Dispatch.Desktop.Tests -c Release` | ❌ W0 | ⬜ pending |
| 74-04-03 | 04 | 2 | AG-74-9, AG-74-18 | T-74-04b | local policy floor, `approval.decide` kind `computer` refused, Cowork rows never in the Code catalog | unit (Avalonia.Headless) | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-06-01 | 06 | 2 | AG-74-3, AG-74-17 | T-74-06 | every phase and degraded state has its own model state and text; `dispatch_disabled` maps to Disabled | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-06-02 | 06 | 2 | AG-74-15, AG-74-17 | T-74-06, T-74-06b | Cancel keyboard reachable and named; polite and assertive live regions; reduced motion (stylesheet override, `HeadMotionTests`) | component (bUnit) and Avalonia.Headless | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.UI.Tests -c Release -- --filter-class "*Dispatch*"` and `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*DispatchView*"` and `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*HeadMotion*"` | ❌ W0 | ⬜ pending |
| 74-07-01 | 07 | 3 | AG-74-3, AG-74-10, AG-74-12 | T-74-03b, T-74-03c | cross-owner 404 on every route (and the `AllowedSubjects` 403 row), idempotency replay and 409, per-sub limits, revoke store transaction | integration | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-07-02 | 07 | 3 | AG-74-16 | T-74-07 | SSE feed resume by `Last-Event-ID`/`?since=`, `resync`, account isolation, outlives the 30 s request timeout; OpenAPI equality | integration | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*"` then `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*GuardTests*"` | ❌ W0 | ⬜ pending |
| 74-08-01 | 08 | 3 | AG-74-15 | T-74-08 | Dispatch section in the Native settings window; every enum-count and settings-window lock test updated | unit (Avalonia.Headless) | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*Settings*"` and `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*NetworkAndKeyboardCopy*"` | ❌ W0 | ⬜ pending |
| 74-08-02 | 08 | 3 | AG-74-15 | T-74-08 | Dispatch section at `/settings/dispatch` on the web; no route list changes (`IndexRoutesTests` untouched) | component and unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Web.Tests -c Release -- --filter-class "*Settings*"` and `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.UI.Tests -c Release -- --filter-class "*SettingsWebPages*"` | ❌ W0 | ⬜ pending |
| 74-05-01 | 05 | 4 | AG-74-4 | T-74-05c | single-use challenge, origin binding, newest wins 4009, frame-guard strikes; 101 upgrade with the security headers and request timeout installed; drain closes 1001 within 10 s (D-45) | integration | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*Hub*"` | ❌ W0 | ⬜ pending |
| 74-05-02 | 05 | 4 | AG-74-5, AG-74-7, AG-74-8, AG-74-10, AG-74-13, AG-74-14 | T-74-05, T-74-05b, T-74-05d | sign at delivery, expiry at 300 s never delivered, revoke within 2 s, Dragonfly down handling, no body in logs or audit | integration (service-gated) | `source "$(bash scripts/test-services.sh up)" && /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-05-03 | 05 | 4 | AG-74-14 | — | LayerMap, ADR-0036 row (one row beginning `| [ADR-0036]`) and Web free of process-spawning references; hosting runbook paragraph | architecture | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` | ✅ | ⬜ pending |
| 74-09-01 | 09 | 4 | AG-74-16 | T-74-06c | `HttpDispatchHost` uses bearer POST and SSE resume only; 401, 429 and `dispatch_disabled` handled; no WebSocket in the project | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Dispatch.Client.Tests -c Release` | ❌ W0 | ⬜ pending |
| 74-09-02 | 09 | 4 | AG-74-16 | T-74-06b | web host over `ApiHttp` and a `UserEventFeed`-style feed; 401 expires the registered session; every used route is in openapi.json; Demo registers the not-configured host | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Web.Tests -c Release -- --filter-class "*Dispatch*"` and `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*ProjectGraph*"` | ❌ W0 | ⬜ pending |
| 74-09-03 | 09 | 4 | AG-74-15, AG-74-17 | T-74-06b | Native head composes the controller host from the account session token (never the owner token); the Remote task strip and the approval prompt are bound to the 69 chrome; Android composition compile-checked only | unit (Avalonia.Headless) | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Autonogrammer.Dispatch.Desktop.Tests` and `tests/Autonogrammer.Dispatch.Client.Tests` projects — created by phase 66.1 (manifest rows T13, T14), AssemblyMarker only.
- [ ] `tests/Autonogrammer.Contracts.Tests/Dispatch/TestData/dispatch-canonical-vectors.json` — shared vector file (74-01).
- [ ] `tests/Autonogrammer.Contracts.Tests/Dispatch/Store/DispatchStoreContractTests.cs` — abstract store contract oracle (74-01), linked into Api.IntegrationTests and Sessions.Postgres.Tests.
- [ ] `tests/Autonogrammer.Api.IntegrationTests/Dispatch/` — folder with the Dispatch host fixtures: `DispatchApiHost.cs` (Program.cs relative pipeline order: `UseHostingHardening`, authentication, `UseRateLimiter`, `UseAuthorization`, `DispatchDisabledMiddleware`, `MapDispatchEndpoints`; `UseWebSockets` added by 74-05) and `DispatchTestAuthHandler.cs` (sub plus `amr` and `session_id`; `Cloud/TestOwnerAuthHandler.cs` emits `sub` only) (74-03).
- [ ] Timeout tests build on `Hosting/HostingTestHost.cs` with a shortened default policy, never on `ApiFactory` (which sets `Hosting:RequestTimeoutSeconds=600`) (74-03, 74-07, 74-05).
- [ ] `tests/Autonogrammer.Dispatch.Desktop.Tests` links `TestWebSocketServer.cs` and `TestWebSocketSession.cs` from `Autonogrammer.Harness.Acp.Tests` (two Compile items, 74-04).
- [ ] Lock tests edited in the same plan as the change that breaks them: `Web.Tests/ServiceWorkerRulesTests` (`/dispatch` prefix, 74-03), `GuardTests.OpenApi_ServedEqualsCommitted` (74-03, 74-07), settings-section tests (`NetworkAndKeyboardCopyTests`, `SettingsWindowTests`, `SettingsWindowHostTests`, `WebSettingsPagesTests`, `SettingsWebPagesTests`, 74-08), `InMemoryCoordinationRegistrationTests` (74-01).
- [ ] Phase 68 prerequisites present before 74-02, 74-05 and (through the in-memory twin) 74-07: M0005, M0008, M0009, the migration runner compiled list, `accounts.feed_seq`, `ITenantHasher`, `IAuditLedger`, `ledger_outbox`, the presence and nonce keyspace helpers.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Android composition builds and runs on a phone | AG-74-15 | Android build and a real device cannot run on this host (compile-checked only) | Owner builds `apps/Autonogrammer.Android` and opens Devices and Dispatch |
| Windows and macOS credential store and desktop head | AG-74-1 | compile-checked only here | Owner pairs on Windows and macOS |
| Live Supabase `amr` claim kept across refresh (OQ-430) | AG-74-2 | no live tokens on this host | First live run pairs after a token refresh |
| Railway long-lived WebSockets and deploy restarts (OQ-420) | AG-74-4 | the API service does not exist on Railway yet | Owner creates the service, sets the two P-256 key variables, `Dispatch__Enabled`, `Api__Auth__Mode=Oidc` and `Hosting__ForwardedHeaders__Mode`, connects a desktop and checks that the host drain (30 s) closes the socket with 1001 before Railway `drainingSeconds` (35) |
| Dragonfly multi-replica routing on Railway | AG-74-14 | two-replica test uses throwaway containers only | Owner runs two API replicas |
| Screen-reader behaviour of the approval prompt | AG-74-15 | NVDA, VoiceOver, TalkBack unavailable | Owner walks the approval prompt with a screen reader |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 180 s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
