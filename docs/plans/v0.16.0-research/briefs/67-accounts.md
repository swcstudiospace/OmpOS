# Brief 67: Accounts (Supabase passwordless, GoTrue client, account surface)

Names are POST-RENAME (OmpOS becomes Autonogrammer); the repo on disk still uses OmpOS paths, cited at the same relative path. Decision record: `docs/decisions/ADR-0030-accounts-supabase-passwordless.md` (read it first; this brief does not repeat its tables).

## Goal
A person can sign in on the desktop and web heads with an email code or a magic link against the shared Supabase project, stay signed in across restarts on desktop with the token in the operating system store, and read their own account and sessions through the owner-scoped API. Code mode keeps working signed out; Cowork, Dispatch and the account surface need an account.

## Requirements
- AG-67-1: The desktop head signs in with an email one-time code (6 to 10 digits) through POST /otp and POST /verify with type email.
- AG-67-2: The desktop head also signs in through a magic link with PKCE and a one-shot loopback listener on 127.0.0.1 with a path nonce, falling back cleanly to the code.
- AG-67-3: First-time email users (F1, signup email without a code) see a specific hint and a working link-then-new-code path whichever owner actions are done.
- AG-67-4: The web head signs in by code or by link, holds tokens and the PKCE verifier in memory only, and hands the returned code to the asking tab over a BroadcastChannel.
- AG-67-5: A hand-written GoTrue client in `Autonogrammer.Accounts` has typed models, source-generated JSON and results (no exceptions) for expected failures, and uses no Supabase SDK.
- AG-67-6: Refresh is single flight, writes the rotated refresh token to the store before publishing it, and adopts a token rotated by another process under a cross-process lock.
- AG-67-7: Startup never waits on the network; a stored session starts SignedIn and refreshes in the background; Offline is a notice, not a sign-out.
- AG-67-8: Sign-out clears local state first, then revokes with scope=local; sign out everywhere (global) and others are separate, confirmed actions.
- AG-67-9: Every failure maps to one of the AuthErrorKind values with its own fixed message and never shows raw exception or server text.
- AG-67-10: `Autonogrammer.Harness.Secrets.Os` implements ISecretStore with libsecret on Linux, Keychain Services on macOS and DPAPI on Windows, reporting typed Unavailable, Locked, Denied.
- AG-67-11: When the OS store fails the session is memory-only with a visible notice; the owner-only file store is used for accounts only after an explicit consent setting, and never for a device private key.
- AG-67-12: A first launch of a Code-only user makes no keyring call (setting `account.hasStoredSession`).
- AG-67-13: The API validates Supabase tokens with ES256 pinned, issuer and audience authenticated, rejects is_anonymous and a role other than authenticated, and takes the owner from sub.
- AG-67-14: GET /auth/config is anonymous and serves project URL, publishable key, enabled methods and timings; the validator refuses a secret key and a non-https URL.
- AG-67-15: Every rate-limit policy partitions by sub; unauthenticated callers share one partition.
- AG-67-16: With open sign-up, WorkspaceAccess denies every caller when Api:Workspace:Owners is empty unless the development principal is on.
- AG-67-17: OpenAPI declares the bearer scheme, security [] on the anonymous operations (`/auth/config`, `/source`, the webhook receiver and `/cowork/artifact-render/{ticket}` with its own limiter partition), scheme `DispatchDevice` on `/dispatch/devices/{id}/connect`, and 401 and 403 responses.
- AG-67-18: The `accounts` row (ADR-0031 M0004, no email stored; `AccountResponse.email` comes from the token's `email` claim) is created on the first authenticated request and `AccountEnrollmentMiddleware` upserts `last_seen_at` only, at most one write per sub per 10 minutes, and never blocks a request.
- AG-67-19: GET /account, /account/sessions, /account/sessions/{id} and /account/board are owner-scoped; an item of another owner is the same 404 as an absent one.
- AG-67-20: GET /board is replaced by GET /account/board and every caller moves in the same change.
- AG-67-21: SessionRegistry and SessionLifecycleStore are deleted and their callers use the repository (brief 68), so a restart keeps the list.
- AG-67-22: A Cowork session appears in /account/sessions and nowhere on the board, /sessions or the catalog event stream.
- AG-67-23: Both heads draw exactly one of SignedOut, AwaitingLink (resend cooldown, expiry countdown, change email), SigningIn (cancelable), SignedIn (identity, devices, sign out) and Error, accessibly.
- AG-67-24: The hosted web CSP gains a second validated connect-src origin for the auth host and the web head refuses a config whose project origin differs.

## Depends on
- Brief 68 (ADR-0031 data plane, owner design-wave-2): `SessionSurface`, `ISessionRepository.ListByOwnerAsync(ownerId, SessionSurface surface, includeClosed, limit, cursor, ct)` (surface required, agreed with design-wave-3), `SetClosedAsync(sessionId, closed, ct)`, `Session.Surface` and `ClosedAt`, migration M0003 and the in-memory twin. Units 4 and 5 need them; units 1, 2, 3, 6 do not.
- ADR-0032 / Cowork shell brief (design-wave-3): `CodeBoardSource` (the only Kanban site), the window-level `SurfaceHost`, whose `HeaderEndSlot`/`SurfaceChromeModel` hosts the account menu built here (and the Devices entry of ADR-0036), and Cowork summary rows via PUT /cowork/sessions/{id}. OQ-320 to OQ-339 belong to ADR-0032 (briefs 69 and 70); this brief uses OQ-300 to OQ-308 (final block OQ-300 to OQ-319).
- ADR-0036 (Dispatch): registers an `IAccountFeature` with key `dispatch`, owns the Devices page and the device key (stored only through the OS store).
- The rename tool has been applied (names above).

## Projects and files owned (layer per `tests/Autonogrammer.ArchitectureTests/LayerMap.cs`)
- NEW `src/Autonogrammer.Accounts` L2 (client, PKCE, session manager, sign-in coordinator, stored session, options), tests `tests/Autonogrammer.Accounts.Tests`. Same-layer edge to `Autonogrammer.Harness.Secrets` (SecretString) in `SameLayerAllowed`.
- NEW `src/Autonogrammer.Harness.Secrets.Os` L2 (same-layer edge to Harness.Secrets), tests `tests/Autonogrammer.Harness.Secrets.Os.Tests`; NEW package `System.Security.Cryptography.ProtectedData` 10.0.13 (MIT) in `Directory.Packages.props`.
- NEW `src/Autonogrammer.Accounts.Desktop` L3 (loopback listener, selector, file lock, settings members), tests `tests/Autonogrammer.Accounts.Desktop.Tests`.
- NEW folder `src/Autonogrammer.Contracts/Accounts/` (AuthConfigResponse, AccountResponse, AccountSessionList, AuthPhase, AuthErrorKind) plus entries in the JSON context (additive; the scaffold step bumps `ContractVersion` to 1.6.0 and this phase does not edit it).
- NEW `src/Autonogrammer.Presentation/Accounts/`, `src/Autonogrammer.Native.Views/Views/Account/`, `src/Autonogrammer.UI/Components/Account/`, `src/Autonogrammer.UI/wwwroot/scripts/autonogrammer-auth.js`, `services/Autonogrammer.Api/Accounts/`, `docs/runbooks/accounts.md`.
- EDITED: `src/Autonogrammer.Harness.Secrets/SecretStoreException.cs` (additive Failure), `services/Autonogrammer.Api/{Program.cs, Security/JwtBearerSetup.cs, Security/*RateLimiterPolicy.cs, Options/ApiAuthOptions*.cs, OpenApi/*, Services/WorkspaceSettings/WorkspaceAccess.cs, Endpoints/{Catalog,Session,Steering}Endpoints.cs, Services/{CatalogChangeDetector,WebAppServiceRegistration}.cs, Cloud/CloudAgentService.cs}`, `apps/Autonogrammer.Native/Composition/HeadServices.cs` and `Settings/FileSettingsStore.cs`, `apps/Autonogrammer.Web/{Program.cs, Services/WebServiceRegistration.cs}`, `scripts/vercel/render-csp.sh`, `vercel.ts`, `docs/api/openapi.json`, Demo handler and `ClientRouteSurface`, `.env.example`, `tests/.../JwtTestHost.cs`. Registration (slnx, slnf, LayerMap, ArchitectureTests csproj, locks, project files, test projects) is done by the scaffold step (00-scaffold-manifest.md); this phase edits only the rows the manifest marks "per phase" (OpenAPI, notices when a package lands, `docs/architecture.md` prose).
- DELETED: `Services/SessionRegistry.cs`, `Services/SessionLifecycleStore.cs`, `apps/.../Services/NoApiTokenProvider.cs`, the head's `AUTONOGRAMMER_API_TOKEN` read.
- Boundary: this brief does not touch `Autonogrammer.Sessions`, `Sessions.Postgres` or the M0003 migration (brief 68).

## Work units (file-disjoint; units 1 and 2 first, 3 in parallel, 4 after brief 68, 5 after 1, 6 after 1 and 5)
1. Accounts core. Owner `Autonogrammer.Accounts` (+ Contracts/Accounts). Files: the new library, its tests (registration is the scaffold step's). Tests: FakeGoTrueHandler covering all six calls and every D10 row; S256 PKCE; single flight under 50 concurrent callers; rotation write order with a failing store; reuse interval on a ManualTimeProvider; offline start; sign-out scopes; cooldown and 5-attempt rules; no secret, email or code in any captured log. Acceptance: AG-67-1 (client side), 3, 5, 6, 7, 8, 9.
2. OS secret stores and desktop adapters. Owner `Autonogrammer.Harness.Secrets.Os` and `Autonogrammer.Accounts.Desktop`. Tests: ISecretStore contract suite run against an in-memory libsecret fake and, Live, against this host's keyring; Windows and macOS compile-only; loopback listener over real TCP (wrong Host, wrong nonce, oversize line, second callback, timeout, error query); file-lock two-process adoption; selector degrade paths. Acceptance: AG-67-2 (listener), 10, 11, 12.
3. API auth hardening. Owner `services/Autonogrammer.Api` Security, Options/ApiAuth*, rate-limiter policies, OpenApi, WorkspaceAccess, JwtTestHost. Tests: ES256 token accepted; HS256, wrong issuer, wrong audience, expired (31 s), `is_anonymous` true, role anon rejected; rotated key id refreshes; per-sub bucket isolation; workspace deny on empty owner list; OpenAPI scheme present and `GuardTests` equality. Acceptance: AG-67-13, 15, 16, 17.
4. Account module and session surface. Owner `services/Autonogrammer.Api/Accounts` + the endpoint and registry call-site migration (depends on brief 68). Tests: /auth/config validator rows and anonymous access; enrollment idempotence and cache; the four routes with two owners (404 scoping); Cowork row absent from board, /sessions and /events; restart keeps the list; `/board` gone and Web client, Demo, `ClientRouteSurface` moved. Acceptance: AG-67-14, 18, 19, 20, 21, 22.
5. Presentation and Native head. Owner `Autonogrammer.Presentation/Accounts`, `Native.Views/Views/Account`, Native composition. Tests: view-model transitions for all five states with a fake IAccountHost and ManualTimeProvider; Avalonia.Headless: names, focus order, announce throttling, reduced motion, contrast gates unchanged; token supplier replaces the env var. Acceptance: AG-67-23 (Native), 7.
6. Web head. Owner `Autonogrammer.UI` account components, the JS module, web services, CSP script. Tests: bUnit states and axe rules in `AccessibilityTests`; RenderCspScriptTests for the second origin (valid, missing when sign-in is on, http, wildcard); the storage scan stays green; the BroadcastChannel hand-off against a fake channel; origin-mismatch refusal. Acceptance: AG-67-4, 23 (Web), 24.

## Success criteria (exact commands; names post-rename)
- `dotnet test --project tests/Autonogrammer.Accounts.Tests -c Release --filter-not-trait "Category=Live"` passes.
- `dotnet test --project tests/Autonogrammer.Harness.Secrets.Os.Tests -c Release --filter-not-trait "Category=Live"` and `dotnet test --project tests/Autonogrammer.Accounts.Desktop.Tests -c Release --filter-not-trait "Category=Live"` pass.
- `AUTONOGRAMMER_LIVE_KEYRING=1 dotnet test --project tests/Autonogrammer.Harness.Secrets.Os.Tests -c Release --no-build --filter-trait "Category=Live"` stores, reads, lists and removes a throwaway item in this host's keyring, or skips with a stated reason.
- `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release --filter-not-trait "Category=Live"` passes, including `GuardTests` OpenAPI equality.
- `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release`, `... tests/Autonogrammer.Native.Tests ...`, `... tests/Autonogrammer.UI.Tests ...` and `... tests/Autonogrammer.Web.Tests ...` pass.
- `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` passes (LayerMap, project graph, ADR structure, suppression ledger, TODO scan).
- `dotnet build Autonogrammer.Linux.slnf -c Release -warnaserror` is clean; `bash scripts/ci/check-licences.sh` accepts the new package.
- `CI=true dotnet restore Autonogrammer.Linux.slnf --locked-mode` passes after the relock.

## Threat model
- Token theft at rest: OS store per platform; the file fallback is consented, visible and never holds a device key; stolen refresh token equals sign-in until revoked.
- Token or code in transit and in logs: tokens only as SecretString; no email, code, verifier or URL query in a log or an error; fixed copy per AuthErrorKind.
- Local web page or process hitting the loopback listener: loopback bind, nonce path, Host check, one callback, 10 minute life, tiny limits, no echo.
- Stolen PKCE code: useless without the in-memory verifier; web BroadcastChannel carries only the code, same origin.
- Algorithm confusion and wrong-project tokens: ES256 pin, issuer and audience, role and is_anonymous checks, owner is sub, accounts keyed by sub (any project user holds a valid token).
- Malicious API or config redirecting sign-in: https and shape validation, web origin pinned to the build, publishable-only key check.
- Refresh reuse ending both sessions: single flight plus cross-process lock plus store-first rotation.
- Shared-project blast radius: per-sub limits, open sign-up exposure limited by deny-by-default workspace routes and cloud off by default; revocation gap until token expiry.
- Enumeration: /otp answers the same for known and unknown addresses; the UI says nothing about existence.

## Degraded and error states
Each is a state or notice with its own sentence: EmailRateLimited, TooManyRequests, CodeInvalidOrExpired, LinkInvalidOrExpired, LinkOpenedInWrongTab, EmailInvalid, SignUpsClosed, CaptchaRequired, AccountBlocked, MethodUnavailable, SessionEnded, NetworkUnavailable, ServiceUnavailable, ConfigUnavailable, AccountRejected, Unexpected; notices LoopbackUnavailable, Offline, SessionNotRemembered (with Unavailable, Locked or Denied reason), SignOutNotConfirmed, ServiceChanged. No owner action done: code for returning users, link-then-new-code for new users. Code mode is never gated.

## Cannot be verified on this host
- Any live Supabase call: needs the owner's publishable key (OQ-301) and owner actions O1, O3, O4; three behaviours are [INFERENCE] (ADR-0030 Open risks).
- Windows DPAPI and macOS Keychain: compile only.
- The web two-tab hand-off and the CSP in a real browser: needs the headless browser of the web smoke script, not run here.
- The Linux keyring live test is possible (gnome-keyring-daemon 46.1 and libsecret 0.21.4 are present) but was not run in this design pass.
- Real deploy: Railway paused, no public API domain.

## Open questions (default; owner)
- OQ-300: Who performs O1 (redirect URLs), O3 (email limit, which number) and O4 (signup template)? Default: none assumed; zero-action behaviour of ADR-0030 D1. Owner.
- OQ-301: Where does the owner supply the publishable key and project URL? Default: Railway variables `Api__Auth__Client__PublishableKey` and `__ProjectUrl`. Owner.
- OQ-302: What is the default hosted API URL and web origin for packaged builds? Default: none, the head says "not configured" (Code unaffected). Blocked by OD-03. Owner.
- OQ-303: Should the web head offer "stay signed in" (refresh token in sessionStorage through a third allow-listed script)? Default: no, memory only. Owner.
- OQ-304: Open sign-up or an allow-list of sub or email? Default: open sign-up. Owner.
- OQ-305: Account deletion and data export for the accounts row and sessions? Default: not in v0.16.0. Owner.
- OQ-306: Replace GET /board by /account/board (clean cutover, default) or keep both? Maintainers.
- OQ-307: Windows Credential Manager instead of DPAPI, and macOS signing and entitlements for the Keychain? Default: DPAPI, unsigned prompts accepted. Maintainers.
- OQ-308: Same person by email here and Google on the website is two users (R5 F8); enable linking later? Default: accept. Owner.
(The ADR text cites OQ-303 to OQ-305; these are the final numbers, so brief and ADR agree.)

## Index rows
ADR index row for `docs/decisions/README.md`:
`| [ADR-0030](ADR-0030-accounts-supabase-passwordless.md) | Accounts: Supabase passwordless sign-in, a hand-written GoTrue client and the account surface | Proposed 2026-10-28 | Shared project xpfoidcvbwmpbwodjvqb; code and PKCE link; OS secret store per platform with a consented file fallback; ES256-pinned JwtBearer; GET /auth/config; /account routes replace GET /board; live Supabase blocked on the owner's key and actions O1, O3, O4 |`
OQ rows for `docs/open-questions.md` (columns: ID, question, context, current default, owner, status), status `open` for all of OQ-300 to OQ-308 with the owners and defaults above; OQ-301 and OQ-302 are `open (blocked external)`.
