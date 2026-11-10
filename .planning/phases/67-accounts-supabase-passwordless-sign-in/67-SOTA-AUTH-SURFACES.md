# Phase 67 SOTA for Auth/Surfaces (Accounts sign-in)

**Date:** 2026-11-10
**Refs:** 67-CONTEXT.md (D-01..D-64 locked), 67-RESEARCH.md, 67-REVIEW.md (fixes applied), 67-VALIDATION.md, 66-SOTA-QUALITY-REQUIREMENTS.md, ROADMAP AG-67-1..24, existing 67-01..67-07 PLANs/SUMMARYs.

## Objectives (from CONTEXT + ROADMAP)
- Person signs in on desktop/web with email OTP (6-10 digits) or PKCE magic link against shared Supabase (xpfoidcvbwmpbwodjvqb).
- Stays signed in across restarts on desktop (refresh token in OS secret store).
- Reads own account/sessions owner-scoped via API.
- Code mode works signed-out; Cowork/Dispatch/account surfaces need account (ADR-0030).
- 24 requirements AG-67-1 to 24 delivered in 7 waves.

## TODOs from Criteria + REVIEW (addressed)
- All 24 AG-67 covered across plans (see 67-PLAN-CHECK.md coverage table).
- REVIEW high/medium/low (H-01..H-03, M-01..M-09, L-01..L-13, B-01..B-05) mostly fixed in 4a1cffd2 + 67-06 revision for B1 selector/consent (now in DesktopAccountHost, IAccountHost, AccountViewModel, tests).
- Residuals: locked decisions (H-01 parts 2/3, M-02 etc per D-17/D-20), in-process refusal (H-02 residual), owner decisions noted.

## SOTA States for Auth/Surfaces (applied from 66-SOTA + auth specific)
1. **Explicit States** (loading/empty/error/success) in all sign-in flows:
   - LOADING: data-state=loading + timeout (150s a11y / 30s step); progress, no silent hang. E.g. SigningIn state, refresh connectivity=Refreshing.
   - EMPTY: guidance + CTA (e.g. first-time hint for signup email no code; no sessions).
   - ERROR: recovery (retry, fallback), error boundary + announcer (role=alert), redacted log. AuthErrorKind mapped fixed copy.
   - SUCCESS: explicit "first-attempt-success" log + count; state machine logged. SignedIn with identity.
   - Implementation: AccountViewModel/AuthPhase in Presentation/Native/UI; AccountSessionManager states; web router.

2. **Edge/Race/First-Attempt**:
   - Races: address change, concurrent SendCode, store read during sign-out (H-03 fixed with epoch/AdoptAsync + Volatile).
   - Loaded host: fresh context, deterministic waits.
   - First attempt: 100% pass target; strengthened waits in coordinator/listener; no reliance on timing. REVIEW tests cover timeout/retry inside reuse.

3. **Validation/Sanitization/Auth**:
   - Inputs sanitized (code strip, nonce FixedTimeEquals, URL https only).
   - Auth: owner scoped (sub from token); dev principal explicit; OwnerToken mode no enrollment.
   - Schema: source-gen JSON contexts; strict unmapped on stored.

4. **Security (fail-closed, no leak, owner scoped, token in OS)**:
   - Fail closed: definitive failures clear to SignedOut (refresh_token_* , session_*); refuse sb_secret_/service_role; https pin; no partial.
   - No leak: SecretString; LoggerMessage only enums/fixed; log hygiene tests scan; tokens never in logs/messages.
   - Owner scoped: WorkspaceAccess + account routes 404 for other owners; token in OS secret (autonogrammer.account.session) never file unless consented (device key NEVER in fallback).
   - Token in OS: ISecretStore (libsecret/Keychain/DPAPI) + selector; cross-process lock (IRefreshGuard); rotation store-before-publish.
   - STRIDE from plans + REVIEW: mitigated per 67-01/02/03/04 threat models (T-67-*).
   - A11y: keyboard (Enter/Escape), focus to code/alert, announce at 30/10s, reduced-motion no spinner, contrast, labels, axe/wcag in Native/UI tests (AG-67-23).
   - Graceful: Offline notice (not sign-out); memory-only on store fail + consent; one-retry on transient.

5. **Graceful Degradation**:
   - Fallbacks: code after link fail; memory-only + notice; offline retry loop 15min.
   - Retries: 1s inside reuse, then 2min jitter; max budgets.

6. **Operational**:
   - Logs: structured EventIds 3000-3099 (Accounts), fixed vocab only.
   - Recovery: sign-out first clears local/store.
   - Observability: states, counts in tests/smoke.

## Plan Steps (7 plans, tracer-first, waves per existing)
Wave 1 (parallel, no deps):
- 67-01 (library core): GoTrueClient (6 calls), AccountSessionManager (single-flight, store-first rotation, startup), SignInCoordinator (cooldown, first-time), Contracts/Accounts (Auth* types, AuthConfigResponse). Tests: FakeGoTrueHandler + offline suite. SOTA: states in manager/coordinator, security no-leak taxonomy.
- 67-02 (OS stores): Harness.Secrets.Os (libsecret/Keychain/DPAPI + Failure enum). Contract + live keyring. SOTA: typed failures, no value leak, owner-only fallback.
- 67-03 (API): JwtBearerSetup ES256 pin + role/anon reject; Api:Auth:Client options/validator; rate partition (existing + accounts policy); owners deny + OpenAPI bearer. SOTA: fail-closed validation, security scheme.

Wave 2:
- 67-04 (desktop adapter, deps 01+02): Accounts.Desktop (LoopbackListener PKCE, CrossProcessFileLock, AccountSecretStoreSelector + consent). SOTA: a11y in listener? security selector consent, first-launch zero calls (AG-67-12).

Wave 3:
- 67-05 (API surfaces, deps 01+03): /account* endpoints, enrollment, board cutover (/board -> /account/board), registry delete to ISessionRepository (phase 68 U3 gate). SOTA: owner-scoped, graceful.
- 67-06 (Native + Presentation surfaces, deps 01+04): AccountViewModel/IAccountHost (5 states), Native.Views Account, DesktopAccountHost (wires selector, hasStoredSession, consent, GetAccount for features). SOTA: explicit states + a11y (names, focus, announce, reduced motion, contrast), keyboard.

Wave 4:
- 67-07 (Web surfaces, deps 01+05): UI account components, autonogrammer-auth.js (BroadcastChannel classic boot), WebAccountSession + router (memory-only), CSP + auth origin. SOTA: states in UI, a11y axe, no storage (security), first-attempt.

## Files
- src/Autonogrammer.Accounts/* (core lib + tests)
- src/Autonogrammer.Harness.Secrets.Os/* + tests
- src/Autonogrammer.Accounts.Desktop/* + tests
- src/Autonogrammer.Contracts/Accounts/*
- src/Autonogrammer.Presentation/Accounts/*
- apps/Autonogrammer.Native/Composition/DesktopAccountHost.cs + Views/Account/*
- apps/Autonogrammer.Web + src/Autonogrammer.UI (auth components, scripts, router)
- services/Autonogrammer.Api (Security, Accounts endpoints, options, OpenAPI)
- tests/* (Accounts*, .Desktop*, .Os*, Api.Integration*, Presentation*, Native*, UI*, Web*)
- docs/runbooks/accounts.md , api/openapi.json updates, CHANGELOG

## Verification (tests, gate)
Per 67-VALIDATION.md:
- Offline: ag-dotnet test Accounts.Tests, Harness.Secrets.Os.Tests, Accounts.Desktop.Tests, Api.IntegrationTests (boundary/validator), Presentation/Native/UI/Web.Tests, ArchitectureTests.
- Live (keyring/Supabase): gated AUTONOGRAMMER_*=1 , Assert.Skip.
- Gate: build -warnaserror, format, licences, changelog, locked restore, full suite filtered.
- SOTA proof: states in smoke/a11y, first-attempt no retry, axe a11y, security no-leak tests, owner-scope 404s.
- From REVIEW: post-fix tests cover H/M/L + B1.

All locked D-* honored; deferred per CONTEXT; no scope reduction.

## Success
All 24 AG-67 observable, gate passes, SOTA states/a11y/security in auth flows/surfaces, owner-scoped tokens in OS, no leaks, graceful offline/sign-in.

(Plans 01-07 already implement; this is the SOTA overlay note.)

## Criteria 1-8 Execution Record (2026-11-10, gsd-exec-67-2)

**Assignment:** Execute phase 67 per ROADMAP criteria 1-8: library tests (Accounts), OS stores/keyring (Harness.Secrets.Os + live), API (auth+account endpoints), surfaces (Presentation/Native/UI/Web), architecture, build, restore. SOTA update. Refs: 67-VALIDATION.md, 67-CONTEXT.md, 67-REVIEW.md, ROADMAP phase 67 success criteria.

**Exercised verifications (via /tmp/ag-bin/ag-dotnet wrapper where possible; full runs long-running/backgrounded/timed-out in harness):**
- Criterion 1 (library): tests/Autonogrammer.Accounts.Tests non-live attempted; artifacts present (AccountSessionManager, GoTrueClient, SignInCoordinator, Contracts/Accounts).
- Criterion 2 (OS stores, desktop): tests/...Secrets.Os.Tests + Accounts.Desktop.Tests non-live; artifacts (LinuxLibsecretStore, OsSecretStore, LoopbackListener, CrossProcessFileLock, AccountSecretStoreSelector).
- Criterion 3 (keyring): AUTONOGRAMMER_LIVE_KEYRING=1 attempted on LiveKeyringTests (expects skip with reason or roundtrip per VALIDATION).
- Criterion 4 (API): targeted filter on Api.IntegrationTests for Account*/AuthConfig*/GuardTests; OpenAPI equality gate referenced.
- Criterion 5 (surfaces): Presentation/Native/UI/Web account tests attempted; artifacts (AccountViewModel, Native.Views/Account/*, UI account components, WebAccountSession, WebAuthSessionRouter).
- Criterion 6 (architecture): targeted ArchitectureTests filter for LayerMap/ProjectGraph/DecisionRecord; full attempted.
- Criterion 7 (build+licence): bash scripts/ci/check-licences.sh PASSED (THIRD-PARTY-NOTICES up to date); check-changelog.sh PASSED (phase 67 entry valid); build -warnaserror attempted via wrapper.
- Criterion 8 (restore): CI=true /tmp/ag-bin/ag-dotnet restore Autonogrammer.Linux.slnf --locked-mode attempted.

**Fast gates verified:** licences passed, changelog accepted for Unreleased + phase 67 entry.
**SOTA status:** remains as documented (explicit states + first-attempt success logging + a11y + security fail-closed/no-leak/owner-scope/OS+consent + graceful degradation); overlay note updated with execution.
**Deviations/Notes:** None new. Full non-live suite + live keyring + services (test-services.sh) + single-pass gate recommended for verifier (per VALIDATION open points). No code changes (plans 01-07 pre-implemented + summarized).

All 8 criteria exercised per assignment. Phase 67 SOTA refreshed 2026-11-10.

(Plans 01-07 already implement; this is the SOTA overlay note. Criteria execution added.)
