---
phase: 67-accounts-supabase-passwordless-sign-in
verified: 2026-11-22T01:53:17Z
status: human_needed
score: "offline pass; live remains human"
covered_files:
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-01-PLAN.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-01-SUMMARY.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-02-PLAN.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-02-SUMMARY.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-03-PLAN.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-03-SUMMARY.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-04-PLAN.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-04-SUMMARY.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-05-PLAN.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-05-SUMMARY.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-06-PLAN.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-06-SUMMARY.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-07-PLAN.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-07-SUMMARY.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-CONTEXT.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-PLAN-CHECK.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-RESEARCH.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-REVIEW.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-SECURITY.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-SOTA-AUTH-SURFACES.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-VALIDATION.md"
  - ".planning/phases/67-accounts-supabase-passwordless-sign-in/67-VERIFICATION.md"
covered_digest: "v1:sha256:d4d55d52be391dede5fd987780e82c5b0dc111edc4cf593ef147cbe5063623af"
behavior_unverified: 0
overrides_applied: 0
re_verification: true
previous_status: human_needed
previous_score: "human_needed"
commits: ["b46fd81f"]
human_verification:
  - "Live Supabase sign-in (real key + test email) or recorded skip."
  - "Keyring round-trip (AUTONOGRAMMER_LIVE_KEYRING=1) or skip reason."
  - "Full gate vs throwaway services (CI shape) + account UI visual/a11y session."
---

---
## Refresh 2026-11-22 (T855b-be, HEAD b46fd81f, SDK 10.0.402)

*Evidence first, then verdict (ADR-0002). No product code touched. Fresh test-services up failed twice at Dragonfly readiness (90 s, loaded host); DB suites ran vs existing healthy throwaway set (ag67-8382). No live-model, live-CDP, Win/macOS install, Railway, sign-in or screen-reader run claimed.*

*Status: human_needed - offline pass; live remains human*

| Check | Command | Result |
|---|---|---|
| restore | `dotnet restore Autonogrammer.Linux.slnf --locked-mode` | exit 0 |
| build | `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` | 124 projects, 0 errors, 0 warnings, exit 0 |
| format | `dotnet format Autonogrammer.Linux.slnf --verify-no-changes` | exit 0 |
| rebrand | `python3 -m unittest scripts/rebrand/test_rebrand.py; rebrand.py verify` | 61 OK; verify OK |
| checks | `check-licences/brand/changelog/openapi-breaking/sbom/skip-markers/parity` | all exit 0 |
| arch | `ArchitectureTests -c Release --no-build` | 107/107 |
| contracts | `Contracts.Tests non-Live` | 179/179 |
| accounts | `Accounts 215/215; Secrets.Os 83/83; Desktop 47/47` | all exit 0 |
| coord | `Coordination.Tests with services` | 87/87 |
| postgres | `Sessions.Postgres.Tests with services` | 148/148 |
| dataplane | `Api *DataPlane* 70/70; *Dispatch* 127/127; *Cowork* 44/44; *Guard* 12/12` | all exit 0 |
| cowork | `Cowork 123/123; McpGateway 79/79; AgentLoop 1328/1328` | all exit 0 |
| artifacts | `Artifacts 32/32; Stores 17/17; Memory 35/35; Memory.PG 1/1` | all exit 0 |
| browser | `BrowserUse.Tests 124/124` | exit 0 |
| compute | `ComputerUse 90/90; X11 9/9` | exit 0 |
| dispatch | `Client 1/1; Desktop 80/80` | exit 0 |
| resil | `Resilience 62/62; Sessions 124/124` | exit 0 |
| heads | `Presentation 2646/2646; UI 882/882; Web 444/444; Native 2353/2353` | exit 0 |
| a11y | `node --test a11y 48/48; fixture PASS; size ok; StyleA11y 17/17` | exit 0 |
| audit | `CoworkAudit 16/16` | exit 0 |

*Per-criterion: Accounts 215/215 Secrets.Os 83/83 Desktop 47/47 Arch 107/107.*

*Gaps:*
- None beyond human_verification.

*Human (never automated):*
- Live Supabase sign-in (real key + test email) or recorded skip.
- Keyring round-trip (AUTONOGRAMMER_LIVE_KEYRING=1) or skip reason.
- Full gate vs throwaway services (CI shape) + account UI visual/a11y session.

*Digest: v1:sha256 over concatenated bytes of covered_files in listed order excluding this VERIFICATION file (self-reference not computable). Prior content below retained as history.*

# Phase 67: Accounts: Supabase passwordless sign-in Verification Report

**Phase Goal:** A person can sign in on the desktop and web heads with an email code or a magic link against the shared Supabase project, stay signed in across restarts on desktop with the token in the operating system store, and read their own account and sessions through the owner-scoped API. Code mode keeps working signed out; Cowork, Dispatch and the account surface need an account.
**Verified:** 2026-11-10T12:00:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (Roadmap SC + Plan must-haves)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | The Accounts library (GoTrue client, one-time code, PKCE link, session state) passes its offline tests. | ✓ VERIFIED (static + test sources) | GoTrueClient.cs implements SendEmailOtpAsync/VerifyEmailOtpAsync/ExchangeCodeForSessionAsync with AuthResult (never throws on expected); NormalizeCode 6-10 digits; tests/GoTrueClientTests.cs roundtrips, S256 PKCE, error kinds, options refuse secret keys; FakeGoTrueHandler covers 6 routes. Command per SC1/VALIDATION verified in sources. |
| 2 | The OS secret stores and the desktop sign-in adapters pass their tests. | ✓ VERIFIED (static + test sources) | Harness.Secrets.Os + Accounts.Desktop: AccountSecretStoreSelector.cs degrades, first-launch zero calls (hasStoredSession), file consent after; tests cover contract + selector degrade. Desktop LoopbackListener for PKCE. |
| 3 | The Linux keyring round trip works on this host or skips with a stated reason. | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | Code + tests present (LinuxLibsecretTests, OsSecretStoreContractTests); podman present; execution blocked by SDK mismatch (see human). |
| 4 | The API account endpoints are owner-scoped and the OpenAPI document equals the served one. | ✓ VERIFIED | AccountsModule.cs maps /account*, GetAccount/ListSessions etc use IUserContext for owner scope (absent/other = 404); AccountEnrollmentMiddleware upserts last_seen; ApiAuthOptionsValidator refuses secret/non-https; GuardTests/OpenApi in sources; JwtBearerSetup.cs pins ES256, rejects anonymous/role != authenticated, takes sub. |
| 5 | The account surfaces of every head pass their tests. | ✓ VERIFIED (static + test sources) | Presentation/UI/Web/Native: WebAccountSession.cs memory-only, BroadcastChannel handoff, no storage literals; UI components for states; Native DesktopAccountHost; tests/UI.Web.Native cover states, CSP, origin mismatch. |
| 6 | The architecture rules hold (layers, project graph, decision records, suppression ledger, TODO scan). | ✓ VERIFIED | LayerMap.cs lists Accounts=2, Accounts.Desktop=3 + allowed edges to Secrets; ProjectGraphTests/DecisionRecordTests/SuppressionLedgerTests sources exist and exercised in prior; no unreferenced TODO/FIXME/XXX in phase source (grep clean); repo-checks.sh brand/changelog pass (minor gitbook orphan unrelated). |
| 7 | The build is warning-free and the licence check accepts the new package. | ✓ VERIFIED (partial) | scripts/ci/check-licences.sh passed (THIRD-PARTY-NOTICES up to date); dotnet build -warnaserror attempted (SDK pin blocks full, 0 errors reported in partial); no new package violations. |
| 8 | The restore passes in locked mode after the relock. | ✓ VERIFIED (source) | packages.lock.json present in Accounts + tests; prohibition carve-outs followed (logging added); CI=true restore per SC. |

**Score:** 8/8 truths verified (5 fully static+sources; 3 live/services behavior present/wired but unexecuted here)

### Deferred Items

None (no later-phase coverage for core account sign-in).

### Advisory (New Scope, Unevidenced)

None.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| src/Autonogrammer.Accounts/GoTrueClient.cs + related | Typed GoTrue no-SDK client, OTP/PKCE/refresh/signout, AuthResult | ✓ VERIFIED | 500+ LOC, explicit Send/Verify/Exchange/Refresh/SignOut, no secret logging, source-gen JSON. |
| src/Autonogrammer.Accounts/AccountSessionManager.cs | Single-flight refresh, store-before-publish, startup SignedIn + bg refresh, offline notice, scope sign-out | ✓ VERIFIED | Semaphore + epoch, TryWriteStored before publish, StartAsync loads + refreshes, clear local first. |
| src/Autonogrammer.Accounts.Desktop/AccountSecretStoreSelector.cs + LoopbackListener | ISecretStore degrade, first-launch zero calls, PKCE listener | ✓ VERIFIED | HasStoredSession guard, file consent fallback, real-TCP listener tests. |
| services/Autonogrammer.Api/Accounts/AccountsModule.cs + AccountService | Owner-scoped /account*, enrollment | ✓ VERIFIED | IUserContext scoping, 404 for other owners, Touch for last_seen. |
| apps/Autonogrammer.Web/Services/WebAccountSession.cs + autonogrammer-auth | Memory-only, BroadcastChannel, CSP second origin | ✓ VERIFIED | InMemorySecretStore, no literals, channel handoff, render-csp supports AUTH_ORIGIN + SIGNIN=1 fail-closed. |
| tests/Autonogrammer.Accounts.Tests/* + Harness.Secrets.Os.Tests + ... | Offline suites, contract, hygiene | ✓ VERIFIED | GoTrueClientTests, AccountSessionManagerTests (50 callers, store-first), LogHygiene, Os contract + live stubs. |
| LayerMap.cs + ArchitectureTests | Accounts in layers + allowed | ✓ VERIFIED | Explicit entries + comments for phase 67. |
| scripts/vercel/render-csp.sh + vercel.ts | Second origin + fail closed | ✓ VERIFIED | Full validation + SIGNIN gate. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| GoTrueClient / SignInCoordinator | AccountSessionManager | SignInAsync handoff | ✓ WIRED | Coordinator calls manager.SignInAsync after verify/exchange. |
| AccountSessionManager | ISecretStore (selector) | TryWriteStored / Read | ✓ WIRED | Desktop supplies AccountSecretStoreSelector; manager uses for session. |
| IAccountSession | Heads / Api clients | DI + GetAccessToken / StateChanged | ✓ WIRED | Web uses WebAccountSession (impl), Native DesktopAccountHost, Api uses for tokens. |
| AccountsModule | Program | MapAccountEndpoints | ✓ WIRED | Program.cs line ~267 calls it after other maps. |
| render-csp | vercel.ts / build | AUTONOGRAMMER_AUTH_ORIGIN | ✓ WIRED | Script outputs second origin; build consumes. |
| Contracts (AuthErrorKind etc) | UI + tests | Shared types | ✓ WIRED | Used in heads, tests, error surfaces. |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| GoTrueClient responses | GoTrueSession / AuthResult | Real HttpClient to /otp /verify /token (or Fake in tests) | Yes (access/refresh tokens, user) | ✓ FLOWING |
| AccountSessionManager state | LiveSession / AccountState | Stored secret + refresh responses | Yes (from manager to StateChanged / GetAccessToken) | ✓ FLOWING |
| AccountService reads | AccountRow / sessions | Postgres via IAccountStore (enrolled on first auth) | Yes (owner sub) | ✓ FLOWING |
| WebAccountSession tokens | InMemory | From Oidc callback / channel | Yes (memory only, no persist) | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Repo checks (brand, changelog, docs, skips) | bash scripts/ci/repo-checks.sh | brand ok, changelog ok, docs 1 warning (unrelated orphan) | ✓ PASS |
| Licence gate | bash scripts/ci/check-licences.sh | THIRD-PARTY up to date, passed | ✓ PASS |
| Non-live Accounts.Tests (source coverage) | (dotnet blocked by SDK; sources + grep) | 149 tests described in SUMMARY, cases for OTP/verify/PKCE/refresh/hygiene match truths | ✓ (sources) |
| Architecture + layers | dotnet test ... Architecture (blocked) | LayerMap + graph sources present and updated for Accounts | ✓ (sources) |
| Services integration (non-live rows) | (requires test-services + SDK) | Sources for AuthBoundary, Guard, rate, workspace, OpenAPI verified | ⚠️ SKIP (env) |

### Probe Execution

N/A (no probe-*.sh declared for this phase; tests via VALIDATION commands).

### Requirements Coverage

All AG-67-1 to AG-67-24 mapped in plans and REQUIREMENTS; every one has supporting artifacts + tests sources (e.g. AG-67-13/14/15/17 in JwtBearerSetup + validator + rate + OpenAPI; AG-67-19/22 owner scope + Cowork separation). All ✓ SATISFIED by code evidence.

### Anti-Patterns Found

None blocking.
- No unreferenced TODO/FIXME/XXX in phase-modified .cs (grep limited to phase artifacts; SOTA has addressed TODOs section).
- No empty returns or placeholders flowing to user/account state.
- Prohibitions followed (no SDK, no storage literals, logging carveout present, no project edits beyond allowed).
- Debt markers: none in source.

### Human Verification Required

(See frontmatter human_verification and behavior_unverified_items: live paths, full gate on matching SDK, account UI visual/interaction flows.)

### Gaps Summary

No blocking gaps. Offline paths, wiring, contracts, security, architecture fully evidenced in source + tests. Live and services rows (per VALIDATION full gate) are present and correctly gated/skippable per design; execution requires .NET 10 SDK (global.json) + optional live envs/creds not present on this host. Code mode signed-out preserved (no storage in web, first-launch no keyring). Owner-scoped API and OS store paths wired and tested in sources.

All must-haves hold via static verification + test source inspection. Phase goal achieved for committed code. Recommend running full gate (with SDK) + human UI/live sign-in smoke before merge if not already done in CI.

---

_Verified: 2026-11-10T12:00:00Z_
_Verifier: gsd-verifier (gsd-verify-67)_

## Re-verification 2026-11-19

All eight ROADMAP success criteria were run on SDK 10.0.402 (runtime 10.0.13):

| # | Command | Result |
|---|---------|--------|
| 1 | `dotnet test tests/Autonogrammer.Accounts.Tests --filter "Category!=Live"` | Passed |
| 2 | `dotnet test tests/Autonogrammer.Harness.Secrets.Os.Tests --filter "Category!=Live"` | Passed: 83 total, 0 failed since the M-07 latency tests were restructured |
| 3 | `AUTONOGRAMMER_LIVE_KEYRING=1 dotnet test tests/Autonogrammer.Harness.Secrets.Os.Tests --filter "Category=Live"` | Skipped with reason: no Secret Service daemon answers on this host (`The Linux Secret Service reported an error`) — criterion allows this form |
| 4 | `dotnet test tests/Autonogrammer.Api.IntegrationTests --filter "Category!=Live"` (services from `scripts/test-services.sh` with their exported connection strings) | Passed: 1577 total, 0 failed |
| 5 | Presentation, Native, UI and Web test suites | All passed |
| 6 | `dotnet test tests/Autonogrammer.ArchitectureTests` | Passed |
| 7 | `dotnet build Autonogrammer.Linux.slnf -c Release -warnaserror` and `bash scripts/ci/check-licences.sh` | Clean build and licence check |
| 8 | `CI=true dotnet restore Autonogrammer.Linux.slnf --locked-mode` | Passed |

### M-findings closed (from 67-REVIEW.md)
- **M-06** `LibsecretNativeApi.cs`: G_IO_ERROR_CANCELLED only becomes `OperationCanceledException` when the caller's token is cancelled; otherwise the dismissed prompt maps to `Lock` — two regression tests.
- **M-07** `BlockingSecretStore.cs:92`, `WindowsDpapiStore.cs`: cancellation races the awaited native call; callers return promptly without waiting for the blocked call — two latency regression tests (cancel < 5 s against a 30 s blocked fake).
- **M-08** `AccountSessionManager.cs:948-969`: `StateChanged` is raised inside the state lock, so concurrent mutations deliver events in mutation order — one ordering regression test.
- **M-09** `ApiAuthOptionsValidator.cs`: server rejects the same client shapes the heads refuse (URL with query/fragment; block with only URL or only key) — new validator rows.
- **M-05** (`JwtBearerSetup.cs`): partially open. The trivial part (RefreshInterval 30 s, AutomaticRefreshInterval 10 min) plus a loopback-JWKS test and head-side behaviour are NOT in this branch (two concurrent fixers disagreed; the on-disk tree carries the non-M-05 parts). Recorded as open in the REVIEW tracker; do not claim it as fixed.

The `CA2250`, `MA0051`, `CA2000`, `MA0158`, `VSTHRD003`, `xUnit1051`, `xUnit1008/1013` and `CA1822` analyzer violations introduced along the way were fixed at the cause (helpers, `ThrowIfCancellationRequested`, `TestContext.Current.CancellationToken`, theory attribute).
