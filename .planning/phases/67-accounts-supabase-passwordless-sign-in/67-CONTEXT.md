# Phase 67: Accounts: Supabase passwordless sign-in - Context
Source: docs/plans/v0.16.0-research/briefs/67-accounts.md
**Gathered:** 2026-10-28
**Status:** Ready for planning

Milestone v0.16.0 (Autonogrammer). Decision record: `docs/decisions/ADR-0030-accounts-supabase-passwordless.md` (Proposed 2026-10-28). Index R-items applied: R5, R8, R9, R10, R11, R14, R17, R18 (`docs/plans/v0.16.0-research/briefs/00-index.md` section 4.2). Names are post-rename; every repo path cited below was opened and exists with this spelling on disk.

<domain>
## Phase Boundary

**Delivers.** A person signs in on the desktop and web heads with an email one-time code or a PKCE magic link against the shared Supabase project (ref `xpfoidcvbwmpbwodjvqb`), stays signed in across restarts on desktop (refresh token in the OS store), and reads their own account and sessions through owner-scoped API routes. Code mode keeps working signed out; Cowork, Dispatch and the account surface need an account (ADR-0030 header).

**Requirements.** AG-67-1 to AG-67-24 (brief 67). **Work units** (file-disjoint; 1 and 2 first, 3 in parallel, 4 after phase 68 U3, 5 after 1, 6 after 1 and 5):
1. Accounts core (`Autonogrammer.Accounts`, `Contracts/Accounts`): AG-67-1 client side, 3, 5, 6, 7, 8, 9.
2. OS secret stores and desktop adapters (`Autonogrammer.Harness.Secrets.Os`, `Autonogrammer.Accounts.Desktop`): AG-67-2 listener, 10, 11, 12.
3. API auth hardening (`services/Autonogrammer.Api` Security, Options/ApiAuth*, rate limiters, OpenApi, WorkspaceAccess, JwtTestHost): AG-67-13, 15, 16, 17.
4. Account module and session surface (`services/Autonogrammer.Api/Accounts` plus the registry call-site migration): AG-67-14, 18, 19, 20, 21, 22.
5. Presentation and Native head (`Presentation/Accounts`, `Native.Views/Views/Account`, Native composition): AG-67-23 Native, 7.
6. Web head (`UI` account components, `autonogrammer-auth.js`, web services, CSP script): AG-67-4, 23 Web, 24.

**Depends on** (index phase table): phase 68 (units 4 and 5 need U3: `surface` column, `ListByOwnerAsync`, `SetClosedAsync`, `Session.Surface`, `ClosedAt`, M0003; units 1, 2, 3, 6 do not); phases 69 and 70 (the `SurfaceHost` chrome slot `HeaderEndSlot`/`SurfaceChromeModel` that hosts the account menu). The scaffold step (`00-scaffold-manifest.md`) creates and registers every new project before this phase.

**Must NOT do.** Touch `Autonogrammer.Sessions`, `Autonogrammer.Sessions.Postgres` or the M0003 migration (phase 68); edit `ContractVersion.Current` (R9); edit registry files (slnx, slnf, `LayerMap.cs`, locks, project files) except rows the manifest marks "per phase" (R17); run the rebrand tool (phase 66); gate Code mode behind sign-in; build SMS, Google, X, CAPTCHA, stay-signed-in, account deletion or export; change any Supabase dashboard setting.
</domain>

<decisions>
## Implementation Decisions

### Provider, ownership and owner actions
- **D-01:** Sign-in uses the shared project through Supabase Auth REST (GoTrue); publishable key and project URL are delivered by the API (`GET /auth/config`), never compiled into a head. (ADR-0030 D1, D7)
- **D-02:** A valid project token is not an Autonogrammer account: authorisation goes through the API's own `accounts` row keyed by `sub`; `user_metadata` never decides access. (ADR-0030 D1, D7; report 05 F7)
- **D-03:** No dashboard setting is changed by us. Owner actions O1 (redirect URLs `http://127.0.0.1:*/**` and `https://<web origin>/**`), O3 (raise `rate_limit_email_sent` above 2/h) and O4 (show the code in the website's ConfirmSignup email) stay the owner's; each has a defined not-done behaviour (D-04). (ADR-0030 D1)
- **D-04:** Zero-action behaviour: `methods.magicLink` false (no PKCE challenge, no redirect, no loopback listener, code only); `signupEmailHasCode` false (new users are told to open the first email's link once, then request a new code; the second email is a `magiclink` email with a code); 429 `over_email_send_rate_limit` maps to `EmailRateLimited`; 400 `captcha_failed` maps to `CaptchaRequired`. SMS, Google and X are not built (O5, O6). Single-session-per-user stays off (O7). (ADR-0030 D1; AG-67-3)

### Desktop sign-in
- **D-05:** `POST /otp {email, create_user:true}`; when `methods.magicLink` is true the body also carries `code_challenge` (S256 of a 64-byte random verifier, base64url) and `code_challenge_method:"s256"`, and the request carries `redirect_to=http://127.0.0.1:<port>/cb/<nonce>`. A new verifier and nonce per attempt per email; a resend reuses them. (ADR-0030 D2.1)
- **D-06:** `AwaitingLink` shows the address, a code field accepting 6 to 10 digits after removing spaces and hyphens (never assume a length; live OTP is 8), resend button with 60 s cooldown (`emailCode.resendCooldownSeconds`), expiry countdown (`emailCode.expirySeconds`, 3600 live) and "Change email"; five wrong codes force a resend. (ADR-0030 D2.2; report 05 F11)
- **D-07:** Code path is `POST /verify {type:"email", email, token}`. That a code requested together with a `code_challenge` still verifies is [INFERENCE], first live check. (ADR-0030 D2.3)
- **D-08:** Link path: hand-written `TcpListener` on `IPAddress.Loopback`, ephemeral port, `GET /cb/<nonce>?code=...`; request line at most 2 KiB, headers at most 8 KiB, 5 s read timeout, only `GET`, `Host` equal `127.0.0.1:<port>`, nonce compared with `CryptographicOperations.FixedTimeEquals`, 10 minute life, one valid callback; response headers `Cache-Control: no-store`, `Referrer-Policy: no-referrer`, `Content-Security-Policy: default-src 'none'`; a callback with `error` or `error_code` is `LinkInvalidOrExpired`; no query text shown or logged; exchange by `POST /token?grant_type=pkce {auth_code, code_verifier}`. Not `HttpListener` (no Windows URL reservation). (ADR-0030 D2.4)
- **D-09:** The flow never opens a browser; a link opened on another device cannot reach the loopback and the panel says so in one sentence. (ADR-0030 D2.5)

### Web head
- **D-10:** The web head calls the auth host directly; tokens and the PKCE verifier live in memory only; a reload signs out; no `localStorage`, `sessionStorage`, `indexedDB` or `document.cookie`; "stay signed in" is not built. (ADR-0030 D3; OQ-303)
- **D-11:** Link redirect is `redirect_to=https://<web origin>/`; the returning tab posts the `?code=` over a same-origin `BroadcastChannel` named `autonogrammer-auth`, waits 3 s for an acknowledgement from the asking tab (which exchanges it), scrubs the query with `history.replaceState` first and shows "You are signed in in your other tab"; no acknowledgement shows `LinkOpenedInWrongTab`. `autonogrammer-auth.js` contains no storage token. (ADR-0030 D3; AG-67-4)
- **D-12:** `scripts/vercel/render-csp.sh` gains optional `AUTONOGRAMMER_AUTH_ORIGIN` (same `https://host[:port]` validation, fail closed when sign-in is enabled and it is missing) and prints `connect-src 'self' <api origin> <auth origin>`; `vercel.ts` and `RenderCspScriptTests` follow; the Demo build keeps `connect-src 'self'` (ADR-0017). The web head reads `Auth:Origin` from `wwwroot/appsettings.json` and refuses an `/auth/config` whose `projectUrl` origin differs; the desktop trusts the https `projectUrl` of the API it was told to use. `Retry-After` is unreadable cross-origin, so the web head uses 60 s after a 429. (ADR-0030 D3; AG-67-24)

### GoTrue client (`Autonogrammer.Accounts`)
- **D-13:** New L2 library, portable to Blazor WebAssembly (no process, no file system, no native code), six plain `HttpClient` calls with header `apikey: <publishable key>`, user calls add `Authorization: Bearer <access token>`, the publishable key is never a bearer: `SendEmailOtpAsync`, `VerifyEmailOtpAsync`, `ExchangeCodeAsync`, `RefreshAsync`, `SignOutAsync(scope)` (scope required, no default), `GetUserAsync` (tests and live check only). No Supabase SDK (`Supabase.Gotrue` 8.5.1 rejected). (ADR-0030 D4)
- **D-14:** Wire records are `internal`, snake_case (`JsonNamingPolicy.SnakeCaseLower`), lenient through `GoTrueJsonContext`; the stored session uses a second context with `UnmappedMemberHandling = Disallow`; all source generated (reflection is off, `Directory.Build.props`). (ADR-0030 D4)
- **D-15:** `AuthResult<T>` carries a value or `AuthFailure(AuthErrorKind Kind, TimeSpan? RetryAfter, int? HttpStatus, string? ServerCode)`; `ServerCode` is kept only from the fixed vocabulary of ADR-0030 (`otp_expired`, `otp_disabled`, `over_email_send_rate_limit`, `over_request_rate_limit`, `signup_disabled`, `user_banned`, `captcha_failed`, `refresh_token_not_found`, `refresh_token_already_used`, `session_not_found`, `session_expired`, `bad_code_verifier`, `flow_state_not_found`, `flow_state_expired`, `email_address_invalid`, `validation_failed`); server `msg` text is never kept, shown or logged; programming errors throw. (ADR-0030 D4)
- **D-16:** Tokens cross the library as `SecretString`; one `SameLayerAllowed` entry `Accounts` to `Harness.Secrets` with an `// intentional:` comment (scaffold step). `GoTrueOptions.Create(...)` validates project URL (absolute https, no userinfo, query or fragment) and refuses a key starting `sb_secret_` or a JWT with `role` `service_role`; the result names the field, never the value. (ADR-0030 D4; manifest row 5)
- **D-17:** `TimeProvider` everywhere, 15 s per-request timeout via `TimeProvider.CreateCancellationTokenSource`, `CancellationToken` last, `ConfigureAwait(false)`; no automatic retry on `/otp`, `/verify`, `/token?grant_type=pkce`; optional `ILogger<T>? logger = null` with `LoggerMessage.Define` EventIds 3000 to 3099, never an email, code, token, verifier or URL query. (ADR-0030 D4)

### Session manager
- **D-18:** `AccountSessionManager` implements `IAccountSession` (`State`, `StateChanged`, `GetAccessTokenAsync(ct)`, `SignOutAsync(scope, ct)`); one `SemaphoreSlim`-guarded in-flight refresh shared by all callers, bound to the manager's lifetime token; refresh on demand when remaining life is below `max(60 s, 20 percent of lifetime)`; no background timer. (ADR-0030 D5; AG-67-6)
- **D-19:** Rotation writes the new refresh token to the secret store BEFORE publishing the session; a failed write keeps the in-memory session and sets notice `SessionNotRemembered`. A lost refresh response is retried once after 1 s, then every 2 min up to 15 min with jitter while the state stays `SignedIn` with connectivity `Offline`. (ADR-0030 D5)
- **D-20:** Refresh runs under a cross-process file lock (`FileShare.None`, desktop adapter), re-reads the store after taking it and adopts a session whose refresh token differs from memory. Definitive failures (`refresh_token_not_found`, `refresh_token_already_used`, `session_not_found`, `session_expired`, `user_banned`): re-read once, else clear store and memory and go `SignedOut` with notice `SessionEnded`; never an exception. (ADR-0030 D5)
- **D-21:** Startup never waits on the network: a stored session starts `SignedIn` with the stored identity, connectivity `Refreshing` while one 5 s refresh runs, then `Online` or `Offline`; a stored `issuer` that differs from the configured project is discarded with `ServiceChanged`. (ADR-0030 D5; AG-67-7)
- **D-22:** Sign-out clears local state and stored secret first, then best-effort `POST /logout?scope=local` (5 s; one refresh attempt if the access token is expired); failure leaves `SignOutNotConfirmed`, never a signed-in state. "Sign out everywhere" (`global`, also signs the website out) and "sign out other devices" (`others`) are separate confirmed menu entries naming the effect. (ADR-0030 D5; report 05 F2)
- **D-23:** One secret `autonogrammer.account.session` holding `{schema:1, issuer, refreshToken, userId, email}`; the access token is not stored. (ADR-0030 D5)

### OS secret stores and selection
- **D-24:** `Autonogrammer.Harness.Secrets.Os` implements the existing `ISecretStore`: Linux libsecret 0.21.4 (`libsecret-1.so.0`, non-variadic `secret_password_storev_sync`, `lookupv_sync`, `clearv_sync`, `searchv_sync`, attributes `{app:"autonogrammer", name:<name>}`, loaded with `NativeLibrary.TryLoad`, calls off the caller's thread with a `GCancellable`); macOS Keychain Services over CoreFoundation (generic password, service `Autonogrammer`, account = name; not `/usr/bin/security`); Windows DPAPI (`ProtectedData`, `CurrentUser`, fixed entropy, one file per name in per-user local application data, atomic replace). (ADR-0030 D6; AG-67-10)
- **D-25:** Bindings use `DllImport` or function pointers over `IntPtr`, `int`, `long` only, hand-marshalled strings zeroed after use, `[DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]`, no `AllowUnsafeBlocks`. `SecretStoreException` gains an additive `Failure` {`Unavailable`, `Locked`, `Denied`, `Corrupt`, `Other`}; messages never contain a value. New package `System.Security.Cryptography.ProtectedData` 10.0.13 (MIT, scaffold step). (ADR-0030 D6; manifest row 4)
- **D-26:** `AccountSecretStoreSelector` (`Autonogrammer.Accounts.Desktop`, L3) is asked only when `account.hasStoredSession` is true or a sign-in just succeeded (first launch of a Code-only user makes no keyring call). When the OS store fails (`Unavailable`, `Denied`, `Locked` after dismissal) the session is memory-only with notice `SessionNotRemembered` and the action "Remember sign-in on this device without system protection", which sets `account.allowFileSecretStore = true` and switches to `OwnerOnlyFileSecretStore`. A device private key (`autonogrammer.dispatch.device-key.<deviceId>`) is NEVER in the file fallback; Dispatch reports `KeyStoreUnavailable`. (ADR-0030 D6; AG-67-11, 12)

### API side
- **D-27:** JwtBearer keeps the existing module: `Api:Auth:Authority` = `https://xpfoidcvbwmpbwodjvqb.supabase.co/auth/v1`, `Audience` `authenticated`, `ValidIssuer` the same string. Changes: `ValidAlgorithms` pinned to `ES256` (`Api:Auth:Algorithms`, subset of `ES256` and `RS256`, default `["ES256"]`), `RequireSignedTokens`, `ValidateLifetime`, `ClockSkew` 30 s, `ValidIssuer` required outside Development and validated absolute https, and in `OnTokenValidated` `is_anonymous` true is rejected and `role` must be `authenticated`, besides `sub`; `MapInboundClaims` stays false. That an unknown `kid` refreshes metadata by JwtBearer default is [INFERENCE] proven by a rotated-key test. (ADR-0030 D7; AG-67-13)
- **D-28:** Owner is `sub` (`IUserContext.Name`), unchanged. (ADR-0030 D7)
- **D-29:** The `accounts` table is M0004 of phase 68 (keyed by `sub`, no email column) with an in-memory twin. `AccountResponse.email` is read from the validated token's `email` claim and never stored. `AccountEnrollmentMiddleware` (after `UseAuthorization`) upserts `last_seen_at` only: creates the row on the first authenticated request of a `sub`, then at most once per 10 minutes per `sub` (in-process cache), skips the development principal and never blocks a request on a store failure. Open sign-up is the default. (ADR-0030 D7; R5; AG-67-18; OQ-304)
- **D-30:** `WorkspaceAccess.Permits` returns false when `Api:Workspace:Owners` is empty unless the development principal is on. (ADR-0030 D7; AG-67-16)
- **D-31:** `GET /auth/config` is anonymous, `Cache-Control: public, max-age=300`, one global limiter bucket; body `AuthConfigResponse {projectUrl, publishableKey, methods:{emailCode, magicLink}, emailCode:{expirySeconds, resendCooldownSeconds}, signupEmailHasCode}` bound from `Api:Auth:Client` (`ProjectUrl`, `PublishableKey`, `MagicLinkEnabled` false, `EmailCodeEnabled` true, `EmailOtpExpirySeconds` 3600, `ResendCooldownSeconds` 60, `SignupEmailHasCode` false). The validator refuses `sb_secret_` keys, `service_role` JWTs, a non-https `ProjectUrl` and `MagicLinkEnabled` without `EmailCodeEnabled`, and warns when `MagicLinkEnabled` and `SignupEmailHasCode` are both false. (ADR-0030 D7; AG-67-14)
- **D-32:** Every rate-limit policy partitions by the `sub` claim with one shared partition for unauthenticated callers (the limiter runs after authentication); new `Accounts:RequestsPerMinute` default 120 (option section per R11). (ADR-0030 D7; AG-67-15; R11)
- **D-33:** OpenAPI: transformer adds scheme `bearerAuth` (`http`, `bearer`, `JWT`) with a document-level requirement; `security: []` on `/auth/config`, `/source`, the webhook receiver (keeps its HMAC description) and `GET /cowork/artifact-render/{ticket}` (own limiter partition); scheme `DispatchDevice` on `/dispatch/devices/{id}/connect`; `401` and `403` responses; `docs/api/openapi.json` regenerated; Demo handler and `ClientRouteSurface` gain the routes. (ADR-0030 D7; R10; AG-67-17)
- **D-34:** Revocation gap accepted (a bearer stays valid until `exp`). Pairing freshness is decided by ADR-0036: newest non-refresh `amr` entry within 10 minutes, not `iat`; OQ-430 stays open. (ADR-0030 D7; R18)

### Account surface and sessions
- **D-35:** Routes behind the fallback policy, owner-scoped by `sub`: `GET /account` (`AccountResponse {id, email, createdAt, lastSeenAt, features[]}`, `features` lists module keys that registered an `IAccountFeature`: `cowork` here, `dispatch` by ADR-0036), `GET /account/sessions` (`AccountSessionList {items[SessionSummary]}`, Code and Cowork merged from two typed repository calls, each row labelled `surface`, newest first), `GET /account/sessions/{id}` (another owner's and an absent item are the same 404 as `FindOwnedSessionAsync`), `GET /account/board`. (ADR-0030 D8; AG-67-19)
- **D-36:** `GET /board` is replaced by `GET /account/board` with no alias: Web client, Demo handler, `ClientRouteSurface`, OpenAPI move in the same change. (ADR-0030 D8; AG-67-20; OQ-306)
- **D-37:** A Cowork session is never a Kanban card: `/account/board`, `/sessions` and the catalog event stream read Code only through `CodeBoardSource` (ADR-0032); a test asserts a Cowork session appears in `/account/sessions` and nowhere else. Automations, forks and cloud agents create Code sessions explicitly. (ADR-0030 D8; AG-67-22)
- **D-38:** `SessionRegistry` and `SessionLifecycleStore` are deleted; callers (`Program.cs`, `Cloud/CloudAgentService.cs`, `Endpoints/CatalogEndpoints.cs`, `SessionEndpoints.cs`, `SteeringEndpoints.cs`, `Services/CatalogChangeDetector.cs`, `Services/WebAppServiceRegistration.cs`, `tests/.../Cloud/CloudApiHost.cs`) move to `ISessionRepository.ListByOwnerAsync(ownerId, SessionSurface surface, includeClosed, limit, cursor, ct)` (surface required) and `SetClosedAsync`; this phase surfaces no cursor (one page per surface, `includeClosed` true). The discriminator is `SessionSurface`, never `SessionKind`. (ADR-0030 D8; AG-67-21; index section 2)
- **D-39:** `NoApiTokenProvider` is deleted and the head no longer reads `AUTONOGRAMMER_API_TOKEN`; the session token replaces it. (ADR-0030 Consequences)

### User interface states and errors
- **D-40:** `AccountViewModel` over an `IAccountHost` in `Autonogrammer.Presentation` (pattern of `ICloudAgentsHost`); Presentation must not reference `Accounts`. `AuthPhase` and `AuthErrorKind` live in `Autonogrammer.Contracts.Accounts`. Exactly one of `SignedOut`, `AwaitingLink`, `SigningIn` (cancelable), `SignedIn` (identity, Account, Devices only when `features` has `dispatch`, Sign out, Sign out everywhere, notices `Offline`, `SessionNotRemembered`, `SignOutNotConfirmed`, `ServiceChanged`) and `Error` is drawn. Offline is a notice on `SignedIn`, not a sign-out. (ADR-0030 D9; AG-67-23)
- **D-41:** Accessibility: every control has an automation name; focus moves to the code field on `AwaitingLink` and to the message on `Error` (`role=alert` on web); countdowns update visually every second and announce only at 30 s, 10 s and when available; Enter submits, Escape cancels; no new colour roles (`ThemeContrastTests`, `CssContractTests` stay green); spinner static under reduced motion. All copy lives in one `AccountCopy` catalogue keyed by enum value. (ADR-0030 D9)
- **D-42:** Error taxonomy is closed: `EmailRateLimited`, `TooManyRequests`, `CodeInvalidOrExpired`, `LinkInvalidOrExpired`, `LinkOpenedInWrongTab`, `EmailInvalid`, `SignUpsClosed`, `CaptchaRequired`, `AccountBlocked`, `MethodUnavailable`, `SessionEnded`, `NetworkUnavailable`, `ServiceUnavailable`, `ConfigUnavailable`, `AccountRejected` (API 401 after valid sign-in, signs out locally), `Unexpected`; `LoopbackUnavailable` is a notice, not an error. The cause-to-kind-to-recovery table is ADR-0030 D10. (ADR-0030 D10; AG-67-9)

### Tests and seams
- **D-43:** Fakes ship per repo convention (no NSubstitute): `FakeGoTrueHandler` (six routes, OTP issue and expiry on a `ManualTimeProvider`, S256 checking, refresh rotation with 10 s reuse interval, scope-aware logout, scripted errors, 429 with and without `Retry-After`), a Kestrel `FakeGoTrueHost`, and `FakeJwks` (in-test ES256 key; `JwtTestHost` moves from HMAC to ES256). Live rows (`Category=Live`, `AUTONOGRAMMER_LIVE_SUPABASE=1`) never send an email. (ADR-0030 D11)
- **D-44:** The Native head implements Cowork's port `ICoworkAccountGate` over `IAccountSession` (no tuple); `Dispatch.Desktop` uses `IAccountSession` and `ISecretStore` only and never references `Accounts.Desktop`. (R8; R14; manifest row 19)

### Claude's Discretion
Brief and ADR leave open: internal file and type layout inside `Autonogrammer.Accounts` beyond the named types; the wording of each `AccountCopy` sentence (one fixed sentence per enum value); view layout, spacing and the icon set of the five states on both heads; the "you can close this tab" page text of the loopback listener; the on-disk folder and file name used by the Windows DPAPI store; the jitter function of the refresh retry; names of test classes and helper types; the exact `Accounts` option class names. Everything named in D-01 to D-44 is fixed.
</decisions>

<specifics>
## Specific Ideas

- Three hosted behaviours are [INFERENCE] until a live run (ADR-0030 Open risks): a code requested with a PKCE challenge still verifies through `POST /verify`; the O4-less first-time path (open the signup link, then request a new code) confirms the user; `http://127.0.0.1:*/**` matches an ephemeral port (report 05 open item c).
- The two-tab web race is accepted: both tabs awaiting a link race for one code, the loser gets `LinkInvalidOrExpired`.
- Email OTP is 8 digits live although the docs say six (report 05 F11, line 152); never hard-code a length.
- The same person by email here and by Google on the website is two users (report 05 F8); the product does not hide this (OQ-308).
- The runbook `docs/runbooks/accounts.md` (new, this phase) carries the owner's manual sign-in check; `auth-doctor.mjs` before and after is recommended (report 05 O9).
</specifics>

<canonical_refs>
## Canonical References

### Decision records and briefs
- `docs/decisions/ADR-0030-accounts-supabase-passwordless.md` - D1 to D11, owner actions, error taxonomy, licence table, verification route.
- `docs/plans/v0.16.0-research/briefs/67-accounts.md` - requirements AG-67-1..24, units, success criteria, threat model, OQ-300..308.
- `docs/plans/v0.16.0-research/briefs/00-index.md` - phase table (row 67), naming rules section 2, R5, R8, R9, R10, R11, R14, R17, R18.
- `docs/plans/v0.16.0-research/briefs/00-scaffold-manifest.md` - rows 4, 5, 19 and T2 to T4 (projects, layers, tuples, package), section 3 (existing-file edits), section 4 (order).
- `docs/decisions/ADR-0031-data-plane-namespaces-and-resilience.md` - M0003 and M0004 (consumed), `ListByOwnerAsync` signature, D4 repository changes.
- `docs/decisions/ADR-0032-cowork-shell-sessions-chat-and-tool-host.md`, `ADR-0036-dispatch-relay.md` - `CodeBoardSource`, `SurfaceHost` slot, `IAccountFeature`, device key, pairing freshness.

### Research
- `docs/plans/v0.16.0-research/05-supabase-auth-reference.md` - GoTrue REST (section 2, lines 75-126), findings F1 to F11 (lines 130-152), flows (156-182), owner actions O1 to O9 (186-200).
- `docs/plans/v0.16.0-research/03-api-auth-backend.md` - existing bearer module (section 1), rate limits (section 6, line 83), OpenAPI gap (line 91), session storage gap (line 68), seams (section 11), decisions D1 to D12.
- `docs/plans/v0.16.0-research/09-project-recipe-and-conventions.md` - options and validator pattern (line 59), logging (61), tests (65-69), gate (section 5), targeted commands (section 6).
- `docs/runbooks/web-key-threat-model.md`, `docs/runbooks/hosting.md` - no-storage rule; hosting variable table and OD-03 wording to reconcile.

### Source this phase touches (verified to exist)
- `src/Autonogrammer.Harness.Secrets/{ISecretStore,SecretStoreException,SecretString,OwnerOnlyFileSecretStore}.cs`
- `services/Autonogrammer.Api/{Program.cs,Security/JwtBearerSetup.cs,Security/SessionRateLimiterPolicy.cs,Security/SessionCreationRateLimiterPolicy.cs,Options/ApiAuthOptions.cs,Options/ApiAuthOptionsValidator.cs,OpenApi/OpenApiConfig.cs,Services/WorkspaceSettings/WorkspaceAccess.cs,Services/SessionRegistry.cs,Services/SessionLifecycleStore.cs,Endpoints/CatalogEndpoints.cs}`
- `apps/Autonogrammer.Native/Composition/HeadServices.cs`, `apps/Autonogrammer.Native/Settings/FileSettingsStore.cs`, `apps/Autonogrammer.Web/Services/{IApiTokenProvider,NoApiTokenProvider,BearerTokenHandler,WebServiceRegistration}.cs`
- `scripts/vercel/render-csp.sh`, `vercel.ts`, `docs/api/openapi.json`, `src/Autonogrammer.Presentation/Cloud/ICloudAgentsHost.cs` (pattern)
- Tests: `tests/Autonogrammer.Api.IntegrationTests/{JwtTestHost,GuardTests,ApiAuthBoundaryTests,ApiAuthStartupTests,AuthBoundaryTests}.cs`, `tests/Autonogrammer.UI.Tests/{AccessibilityTests,CssContractTests,Security/SessionKeyHolderTests}.cs`, `tests/Autonogrammer.Web.Tests/{RenderCspScriptTests,ClientRouteSurface,OpenApiCoverageTests}.cs`, `tests/Autonogrammer.Presentation.Tests/PresentationIsolationTests.cs`, `tests/Autonogrammer.ArchitectureTests/{LayerMap,ProjectGraphTests}.cs`
</canonical_refs>

<code_context>
## Code Context

### Reusable Assets
- `ISecretStore` (SetAsync, GetAsync, RemoveAsync, ListNamesAsync) and `SecretString` (`src/Autonogrammer.Harness.Secrets/ISecretStore.cs:24-62`, `SecretString.cs:21`); `OwnerOnlyFileSecretStore` (`:25`) is the consented fallback.
- Existing JwtBearer module: `JwtBearerSetup.cs:32-40` (MapInboundClaims false, audience, issuer, subject check); `AuthenticatedUserContext` gives `sub`.
- `FindOwnedSessionAsync` (`services/Autonogrammer.Api/Endpoints/SessionEndpoints.cs:417`) is the 404-scoping pattern for the account item route.
- `ManualTimeProvider` exists in `tests/Autonogrammer.Coordination.Tests/` and `tests/Autonogrammer.Web.Tests/`; copy per test project (no shared test library).

### Established Patterns
- Options class plus `IValidateOptions<T>` plus `ValidateOnStart`, failures name the key (report 09 line 59); `AddXxxServices()` and `MapXxxEndpoints()` module shape (report 03 line 44).
- Presentation view model over a host interface (`ICloudAgentsHost`); `PresentationIsolationTests` forbids Presentation referencing `Accounts`.
- Web head storage scan (`SessionKeyHolderTests`) must stay green; `DllImport` safe-directories attribute precedent `src/Autonogrammer.Sandbox/PosixProcessSignal.cs:16`.

### Integration Points
- Created by the scaffold step (do not create or register): `src/Autonogrammer.Accounts`, `src/Autonogrammer.Harness.Secrets.Os`, `src/Autonogrammer.Accounts.Desktop` and their three test projects, `LayerMap` lines and two tuples, the `ProtectedData` package pin, `Contracts/Accounts/` folder, `ContractVersion` 1.6.0 (today `src/Autonogrammer.Contracts/ContractVersion.cs:9` reads 1.5.0).
- Created by phase 68: `SessionSurface`, `ISessionRepository.ListByOwnerAsync`/`SetClosedAsync` (today `ISessionRepository.cs` has only `ListSessionIdsByStatusAsync`, line 68), `Session.Surface`/`ClosedAt`, M0003, M0004.
- Created by phases 69 and 70: `SurfaceHost`, `HeaderEndSlot`/`SurfaceChromeModel`, `CodeBoardSource`, `ICoworkAccountGate`. Created by phase 74: `IAccountFeature` key `dispatch`, Devices page.
- Edited here: `Program.cs` pipeline (`UseAuthentication` line 242, `UseRateLimiter` 249, `UseAuthorization` 250; the enrollment middleware goes after 250), `HeadServices.cs:44,47` env names, `FileSettingsStore.cs` (settings `account.hasStoredSession`, `account.allowFileSecretStore`).
</code_context>

<deferred>
## Deferred Ideas and omitted-by-decision items

- SMS, Google, X sign-in: not built (ADR-0030 D1 O5; report 05 F4, D4: phone provider off, hook is AU-only).
- CAPTCHA: O6 says do not enable; `captcha_failed` maps to `CaptchaRequired` (ADR-0030 D1).
- Web "stay signed in" (refresh token in `sessionStorage` through a third allow-listed script): not built; OQ-303 default no.
- Windows Credential Manager and macOS signing and entitlements: OQ-307 default DPAPI, unsigned prompts accepted.
- Account deletion and data export of the `accounts` row and sessions: not in v0.16.0 (OQ-305). Erasure machinery of phase 68 (AG-68-14) exists but has no sign-in-side route here.
- Identity linking (email here vs Google on the website): accept two users (OQ-308; report 05 D6).
- Allow-list of `sub` or email instead of open sign-up: OQ-304 default open sign-up.
- Custom URL scheme for mobile deep links (report 05 O2): not needed for desktop loopback; Android is phase 74.
- A per-request revocation check (bearer valid until `exp` after sign-out or ban): accepted gap (ADR-0030 D7).
- Open questions kept open with defaults: OQ-300 (owner actions O1/O3/O4; none assumed), OQ-301 (publishable key via `Api__Auth__Client__PublishableKey` and `__ProjectUrl`; blocked external), OQ-302 (no default hosted API URL; head says "not configured"; blocked by OD-03), OQ-303, OQ-304, OQ-305, OQ-306 (replace `/board`), OQ-307, OQ-308. Block OQ-300 to OQ-319 (index section 3).
</deferred>

---

*Phase: 67-accounts-supabase-passwordless-sign-in*
*Context gathered: 2026-10-28*
