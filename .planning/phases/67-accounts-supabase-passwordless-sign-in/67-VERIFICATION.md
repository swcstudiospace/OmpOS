---
phase: 67-accounts-supabase-passwordless-sign-in
verified: 2026-11-10T12:00:00Z
status: human_needed
score: 8/8 must-haves verified (static code + test sources; live/services execution requires SDK 10 + creds)
covered_files:
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-01-PLAN.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-01-SUMMARY.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-02-PLAN.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-02-SUMMARY.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-03-PLAN.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-03-SUMMARY.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-04-PLAN.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-04-SUMMARY.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-05-PLAN.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-05-SUMMARY.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-06-PLAN.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-06-SUMMARY.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-07-PLAN.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-07-SUMMARY.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-CONTEXT.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-PLAN-CHECK.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-RESEARCH.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-REVIEW.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-SECURITY.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-SOTA-AUTH-SURFACES.md
- .planning/phases/67-accounts-supabase-passwordless-sign-in/67-VALIDATION.md
- src/Autonogrammer.Accounts/*.cs
- src/Autonogrammer.Accounts.Desktop/*.cs
- services/Autonogrammer.Api/Accounts/*.cs
- apps/Autonogrammer.Web/Services/*Account*.cs
- tests/Autonogrammer.Accounts.Tests/*.cs
- tests/Autonogrammer.Harness.Secrets.Os.Tests/*.cs
- tests/Autonogrammer.Accounts.Desktop.Tests/*.cs
- tests/Autonogrammer.Api.IntegrationTests/*Auth*.cs
- tests/Autonogrammer.UI.Tests/*Account*.cs
- tests/Autonogrammer.Web.Tests/*Auth*.cs
- tests/Autonogrammer.Native.Tests/*Account*.cs
- tests/Autonogrammer.ArchitectureTests/LayerMap.cs
- tests/Autonogrammer.ArchitectureTests/ProjectGraphTests.cs
- CHANGELOG.md
- scripts/vercel/render-csp.sh
- scripts/ci/check-licences.sh
- scripts/ci/repo-checks.sh
covered_digest: "v1:sha256:manual-collected-from-evidence (gsd query unavailable in env)"
behavior_unverified: 3
overrides_applied: 0
re_verification: false
gaps: []
deferred: []
advisory: []
behavior_unverified_items:
  - truth: "Live Supabase sign-in path (email code / magic link) exercises against shared project or documented skip reason"
    test: "Run with real publishable key and test email (owner-approved)"
    expected: "End-to-end sign-in, token, account read succeeds or skip with reason in runbook"
    why_human: "Requires live Supabase credentials and network; this env has none; design gates live tests behind env"
  - truth: "Linux keyring round-trip (store/read/list/remove) works via libsecret or skips with stated reason"
    test: "AUTONOGRAMMER_LIVE_KEYRING=1 dotnet test ... --filter-trait Category=Live"
    expected: "Round trip passes or clean skip with reason"
    why_human: "Requires libsecret setup + real keyring; podman present but full test run blocked by .NET SDK pin (global.json 10.0.402 vs installed 8)"
  - truth: "Full services gate for Api.IntegrationTests (AuthBoundary, Guard, rate limit, workspace access, OpenAPI) passes with test-services Postgres/Dragonfly"
    test: "source scripts/test-services.sh; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release --filter-not-trait Category=Live (non-live rows)"
    expected: "All rows pass (including production config rows)"
    why_human: "Requires services containers + SDK 10; non-live code paths and test sources verified statically; podman present but dotnet test blocked by SDK"
human_verification:
  - test: "Desktop and web sign-in UI flows (code entry, link callback, states: SignedOut/AwaitingLink/SigningIn/SignedIn/Error, accessible)"
    expected: "Renders correct states, submits, shows identity/devices/sign-out; no storage of secrets; reload signs out web; Code mode works signed-out"
    why_human: "Visual + interaction + a11y; bUnit + render tests cover but real browser + heads need human or capture"
  - test: "Full gate execution (non-live + live + services) on host with matching .NET 10 SDK and optional live envs"
    expected: "All commands in 67-VALIDATION.md exit 0; live rows either pass or skip with documented reason; services rows use real throwaway DBs"
    why_human: "Current env SDK mismatch (global.json requires 10.0.402, only 8.0 installed); live Supabase/keyring need creds/setup"
---

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
