---
phase: "74"
slug: "dispatch-relay"
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-10-28"
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
| **Full suite command** | the per-project commands of the verification map below, then `bash scripts/test-services.sh up` plus `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*"` and the Sessions.Postgres.Tests equivalent, then `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` |
| **Estimated runtime** | ~120 s per project; ArchitectureTests is the most expensive single project (builds every product project) |

Builds and tests run only through the serialising wrapper `/tmp/ag-bin/ag-dotnet`; never bare `dotnet`. Service-gated tests (Postgres, Dragonfly) need `bash scripts/test-services.sh up` first (exports `AUTONOGRAMMER_TEST_POSTGRES_CONNECTION` and `AUTONOGRAMMER_TEST_DRAGONFLY_CONNECTION`).

---

## Sampling Rate

- **After every task commit:** the task's own `<automated>` command (one project, filtered).
- **After every plan wave:** the full suite command above.
- **Before `/gsd:verify-work`:** full suite green, plus `/tmp/ag-bin/ag-dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror -nodeReuse:false -m:1` and `/tmp/ag-bin/ag-dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore`.
- **Max feedback latency:** ~180 s.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 74-01-01 | 01 | 1 | AG-74-4, AG-74-9 | T-74-01c | closed op/capability/close-code sets, no `computer.*`, unknown `t`/`op` rejected | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Contracts.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-01-02 | 01 | 1 | AG-74-5, AG-74-7 | T-74-01, T-74-01b | byte-identical canonical signing bytes, charset gate, transition table | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Contracts.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-01-03 | 01 | 1 | AG-74-6 | T-74-01b | in-memory store passes the shared store contract (seq, cap, idempotency, CAS) | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*Store*"` | ❌ W0 | ⬜ pending |
| 74-02-01 | 02 | 2 | AG-74-13 | — | M0010 idempotent, additive, registered after M0009 | integration (service-gated) | `bash scripts/test-services.sh up >/dev/null 2>&1; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Sessions.Postgres.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-02-02 | 02 | 2 | AG-74-6, AG-74-12, AG-74-13 | — | atomic seq and cap, audit row in the same transaction, foreign account reads nothing | integration (service-gated) | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Sessions.Postgres.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-02-03 | 02 | 2 | AG-74-6 | — | 32-writer race gap-free; body purge on injected clock | integration (service-gated) | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Sessions.Postgres.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-03-01 | 03 | 2 | AG-74-14, AG-74-17 | — | options validator refuses unsafe settings; disabled 404 `dispatch_disabled` | integration | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*Options*"` | ❌ W0 | ⬜ pending |
| 74-03-02 | 03 | 2 | AG-74-1, AG-74-2 | — | pairing needs a recent full sign-in; credential refused off the connect route, access token refused on it | integration | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*Pairing*"` | ❌ W0 | ⬜ pending |
| 74-03-03 | 03 | 2 | AG-74-3, AG-74-10, AG-74-12, AG-74-16 | — | cross-owner 404 on every route, idempotency, 429, SSE resume and `resync`, OpenAPI equality | integration | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-04-01 | 04 | 2 | AG-74-1, AG-74-6 | — | forged, replayed, expired, wrong-user and wrong-kid frames refused; restart does not re-run | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Dispatch.Desktop.Tests -c Release` | ❌ W0 | ⬜ pending |
| 74-04-02 | 04 | 2 | AG-74-1, AG-74-10, AG-74-11 | — | jitter bounds, every close code, no file key store, revoke ends dispatched sessions | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Dispatch.Desktop.Tests -c Release` | ❌ W0 | ⬜ pending |
| 74-04-03 | 04 | 2 | AG-74-9, AG-74-18 | — | local policy floor, `approval.decide` kind `computer` refused, Cowork rows never in the Code catalog | unit (Avalonia.Headless) | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-05-01 | 05 | 3 | AG-74-4 | — | single-use challenge, origin binding, newest wins 4009, frame-guard strikes | integration | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*Hub*"` | ❌ W0 | ⬜ pending |
| 74-05-02 | 05 | 3 | AG-74-5, AG-74-7, AG-74-8, AG-74-10, AG-74-13, AG-74-14 | — | sign at delivery, expiry at 300 s never delivered, revoke within 2 s, Dragonfly down handling, no body in logs or audit | integration (service-gated) | `bash scripts/test-services.sh up >/dev/null 2>&1; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-05-03 | 05 | 3 | AG-74-14 | — | LayerMap, ADR-0036 row and Web free of process-spawning references | architecture | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` | ✅ | ⬜ pending |
| 74-06-01 | 06 | 2 | AG-74-3, AG-74-15 | — | every phase and degraded state has its own model state and text | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-06-02 | 06 | 2 | AG-74-15, AG-74-17 | — | Cancel keyboard reachable and named; polite and assertive live regions; reduced motion | component (bUnit) | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.UI.Tests -c Release -- --filter-class "*Dispatch*"` | ❌ W0 | ⬜ pending |
| 74-06-03 | 06 | 2 | AG-74-16 | — | `HttpDispatchHost` uses bearer POST and SSE resume only; 401 and 429 handled | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Dispatch.Client.Tests -c Release` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Autonogrammer.Dispatch.Desktop.Tests` and `tests/Autonogrammer.Dispatch.Client.Tests` projects — created by phase 66.1 (manifest rows T13, T14), AssemblyMarker only.
- [ ] `tests/Autonogrammer.Contracts.Tests/Dispatch/TestData/dispatch-canonical-vectors.json` — shared vector file (74-01).
- [ ] `tests/Autonogrammer.Contracts.Tests/Dispatch/Store/DispatchStoreContractTests.cs` — abstract store contract oracle (74-01), linked into Api.IntegrationTests and Sessions.Postgres.Tests.
- [ ] `tests/Autonogrammer.Api.IntegrationTests/Dispatch/` — folder with the Dispatch host fixtures (74-01, 74-03).
- [ ] Phase 68 prerequisites present before 74-02 and 74-05: M0005, M0008, M0009, the migration runner compiled list, `accounts.feed_seq`, `ITenantHasher`, `IAuditLedger`, `ledger_outbox`, the presence and nonce keyspace helpers.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Android composition builds and runs on a phone | AG-74-15 | Android build and a real device cannot run on this host (compile-checked only) | Owner builds `apps/Autonogrammer.Android` and opens Devices and Dispatch |
| Windows and macOS credential store and desktop head | AG-74-1 | compile-checked only here | Owner pairs on Windows and macOS |
| Live Supabase `amr` claim kept across refresh (OQ-430) | AG-74-2 | no live tokens on this host | First live run pairs after a token refresh |
| Railway long-lived WebSockets and deploy restarts (OQ-420) | AG-74-4 | the API service does not exist on Railway yet | Owner creates the service, sets the two P-256 key variables and `Dispatch__Enabled`, connects a desktop |
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
