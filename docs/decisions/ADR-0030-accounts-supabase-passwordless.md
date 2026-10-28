# ADR-0030: Accounts: Supabase passwordless sign-in, a hand-written GoTrue client and the account surface

- Status: Proposed 2026-10-28 (design only; becomes Accepted when the verification route below passes and the owner has answered the live-gated rows)
- Date: 2026-10-28
- Owner: autonogrammer-maintainers
- Deciders: autonogrammer-maintainers
- Scope: how a person signs in to Autonogrammer (milestone v0.16.0, brief `docs/plans/v0.16.0-research/briefs/67-accounts.md`): the sign-in flows of the desktop and web heads against the shared Supabase project, the GoTrue REST client, session refresh and sign-out, token storage per operating system, the API side (token validation, public client configuration, per-user rate limits, OpenAPI security), the account read surface and the sign-in states of the user interface. Not in this record: the Cowork shell (ADR-0032), Dispatch device pairing (ADR-0036), the data plane (ADR-0031) and the session table migration (brief 68), which this record consumes.
- Naming: every name below is the POST-RENAME spelling (Autonogrammer becomes Autonogrammer, for example `Autonogrammer.Api`, `services/Autonogrammer.Api/...`). The repository on disk still uses the old names when this record is written; `path:line` citations are to the pre-rename files at the same relative path. The external omp engine names (`omp`, `omp acp`, `~/.omp`, `OMP_*`, `models.yml`) are untouched.
- Owner decisions taken as given (not reopened): the shared project `xpfoidcvbwmpbwodjvqb` is reused and no dashboard setting is changed by us; sign-in is NOT required for Code mode and IS required for Cowork, Dispatch and the account surface; hard cutover rename without compatibility aliases; the API in `services/Autonogrammer.Api` is extended rather than a service added; Windows and macOS code is compile-checked only.

## Context

No sign-in client exists in either head: the web head registers `NoApiTokenProvider` ("Sign-in (OD-03) replaces this registration", `apps/Autonogrammer.Web/Services/IApiTokenProvider.cs:3-14`) and the desktop head reads a bearer from an environment variable per request (`apps/Autonogrammer.Native/Composition/HeadServices.cs:44-47,439-453`). The API side already has an OIDC bearer module: fail-closed options (`services/Autonogrammer.Api/Options/ApiAuthOptionsValidator.cs:27-45`), the JwtBearer scheme with the subject as owner (`services/Autonogrammer.Api/Program.cs:80-89,148-159`, `Security/JwtBearerSetup.cs:27-45`) and a fallback policy that requires an authenticated user. It does not pin the algorithm, has no `ValidIssuer` rule, no `is_anonymous` or `role` check and no account concept; every rate-limit policy is one global bucket (`Security/SessionRateLimiterPolicy.cs:33-48`); OpenAPI declares no security scheme (`docs/plans/v0.16.0-research/03-api-auth-backend.md` section 7); the session list is an in-process id set and an in-process closed flag that a restart loses (`Services/SessionRegistry.cs:11-22`, `Services/SessionLifecycleStore.cs:11`, `Endpoints/CatalogEndpoints.cs:271-285`). There is no operating system credential store (`src/Autonogrammer.Harness.Secrets/ISecretStore.cs:18-21`), and the web head forbids every browser storage API (`tests/Autonogrammer.UI.Tests/Security/SessionKeyHolderTests.cs:53-96`, `docs/runbooks/web-key-threat-model.md`).

The identity provider is the shared Supabase project of the Spectrum Web Co website, which also serves other apps. Facts that shape the design, from `docs/plans/v0.16.0-research/05-supabase-auth-reference.md` (cited as R5) and from probes made while writing this record on 2026-10-28:

- Issuer `https://xpfoidcvbwmpbwodjvqb.supabase.co/auth/v1`, access-token audience `authenticated`, one ES256 key (R5 1.1). OIDC discovery answers 200 at `/auth/v1/.well-known/openid-configuration` with `issuer` and `jwks_uri` (unauthenticated GET, this record), so the JwtBearer authority mechanism needs no code for key discovery.
- The auth host answers a browser CORS preflight with `access-control-allow-origin: *` and allows the headers `apikey,content-type,authorization,x-client-info` (OPTIONS probe, this record). It does not list `Retry-After` in `access-control-expose-headers`, so a browser cannot read it.
- The website's Send Email hook renders a `signup` email with a link and no code and a `magiclink`/`email` email with a link and a code (R5 1.4, finding F1). Email sending is project-wide at 2 per hour (R5 1.6, F3), and email OTP is 8 digits live (R5 F11).
- `POST /verify` with `type: email` returns a session without a PKCE verifier; `POST /logout` defaults to scope `global`; refresh tokens are single use with a 10 second reuse interval; `GET /verify` redirects with `?code=` only when a PKCE challenge was sent, otherwise with a URL fragment (R5 section 2, F2, F5).
- GoTrue error codes used below exist on `master` in `internal/api/apierrors/errorcode.go` (`otp_expired`, `otp_disabled`, `over_email_send_rate_limit`, `over_request_rate_limit`, `signup_disabled`, `user_banned`, `captcha_failed`, `refresh_token_not_found`, `refresh_token_already_used`, `session_not_found`, `session_expired`, `bad_code_verifier`, `flow_state_not_found`, `flow_state_expired`, `email_address_invalid`, `validation_failed`; read 2026-10-28).

## Decision

### D1. Provider, owner actions and what the product does without them

Sign-in uses the shared project through Supabase Auth REST (GoTrue), with the publishable key and project URL delivered by the API (D7), never compiled into a head. A valid token from this project is not an Autonogrammer account (R5 F7): the API keeps its own `accounts` row keyed by `sub` and authorises only through it (D7).

Owner actions. We change no dashboard setting; the list below is what the owner may do, each with the behaviour of the product when it is not done.

| # | Action (where) | Needed for | Not done: what the product does |
| --- | --- | --- | --- |
| O1 | Add redirect URLs `http://127.0.0.1:*/**` and `https://<web head origin>/**` (Authentication, URL Configuration; additive, keeps every existing entry) | Magic link on desktop and web | The operator leaves `methods.magicLink` false (the default). Heads send no PKCE challenge and no redirect, run no loopback listener, and offer the code only. |
| O3 | Raise `rate_limit_email_sent` above 2 per hour (Authentication, Rate Limits; shared with the website, number chosen by the owner) | Any real use | A 429 `over_email_send_rate_limit` maps to the state `EmailRateLimited` with its own message (D10). Returning users with a code already in the inbox still sign in. |
| O4 | Show `email_data.token` in the website's `ConfirmSignup` email template (website repo and deploy, not a dashboard toggle) | Code sign-in for first-time email users (F1) | `signupEmailHasCode` stays false (the default). The panel tells new users that the first email has a link, to open it once and then send a new code: the second email is a `magiclink` email with a code. [INFERENCE: opening a signup link whose `redirect_to` is not allow-listed still confirms the user; R5 F6 says GoTrue falls back to the Site URL. This is the first live check, D11.] |
| O5 | Nothing for SMS | - | SMS, Google and X are not built (R5 F4). |
| O6 | Do not enable CAPTCHA | - | A 400 `captcha_failed` maps to `CaptchaRequired`: "Sign-in from apps is unavailable right now". The client cannot solve it (R5 F10). |
| O7 | Leave Site URL, hooks, providers, signing keys, manual linking untouched; keep "Single session per user" off | Devices coexist (R5 O8) | Enabling single-session signs the website out on desktop sign-in; the refresh path treats the resulting `session_not_found` as a normal sign-out (D5). |

Minimum for a first-time user to sign in: O1 or O4. Minimum for any email volume: O3. With none of them done, returning users can sign in by code within the 2 per hour limit and first-time users follow the link-then-new-code path of the O4 row. The owner also supplies the publishable key to the API configuration (a Railway variable, not a dashboard change). R5 O9 (running the read-only `auth-doctor.mjs`) is recommended before and after.

### D2. Desktop sign-in: email code and magic link

Both flows run on one screen and share one email; the person may use either.

1. `POST /otp` `{email, create_user: true}`. When `methods.magicLink` is true the body also carries `code_challenge` (S256 of a 64 byte random verifier, base64url) and `code_challenge_method: "s256"`, and the request carries `redirect_to=http://127.0.0.1:<port>/cb/<nonce>`. A new verifier and nonce are made per attempt (per email address); a resend reuses them, because the newer email invalidates the older token anyway (R5 F5).
2. State `AwaitingLink` shows the address, a code field, a resend button with a 60 second cooldown (`emailCode.resendCooldownSeconds`), a code expiry countdown (`emailCode.expirySeconds`, 3600 live) and "Change email". The code field accepts 6 to 10 digits after removing spaces and hyphens and never assumes a length (R5 F11). Five wrong codes force a resend.
3. Code path: `POST /verify` `{type: "email", email, token}` returns the session. `type: email` verifies a signup token and a magic-link token (R5 section 2). [INFERENCE for the hosted service, to be checked live: a code requested together with a `code_challenge` still verifies through `POST /verify`.]
4. Link path: a one-shot listener on `IPAddress.Loopback` with an ephemeral port receives `GET /cb/<nonce>?code=...`, answers a fixed "you can close this tab" page, closes, and exchanges the code with `POST /token?grant_type=pkce` `{auth_code, code_verifier}`. The listener is a hand-written `TcpListener` reader, not `HttpListener`, so no Windows URL reservation is needed [INFERENCE: `HttpListener` prefix rights for non-administrators were not verified]. Limits: request line at most 2 KiB, headers at most 8 KiB, 5 second read timeout, only `GET`, a `Host` header equal to `127.0.0.1:<port>` (a DNS-rebinding guard), a path nonce compared with `CryptographicOperations.FixedTimeEquals`, a lifetime of 10 minutes, one valid callback. Response headers `Cache-Control: no-store`, `Referrer-Policy: no-referrer`, `Content-Security-Policy: default-src 'none'`. A callback carrying `error` or `error_code` is a `LinkInvalidOrExpired` failure; no query text is shown or logged.
5. A link opened on another device cannot reach the loopback; the code remains. The panel says so in one sentence.

The flow never opens a browser itself: the person opens the link from the mail client.

### D3. Web head sign-in

The web head runs the same two flows with these differences, all from the existing rule that it stores nothing (`docs/runbooks/web-key-threat-model.md`, `SessionKeyHolderTests.cs:53-96`, which forbids `localStorage`, `sessionStorage`, `indexedDB` and `document.cookie` in `apps/Autonogrammer.Web` and `src/Autonogrammer.UI`).

- The browser calls the auth host directly (CORS verified above). Tokens and the PKCE verifier live in memory only. A reload signs the person out; "stay signed in" is NOT built (open question OQ-303, default no).
- The link redirects to the head's own origin root: `redirect_to=https://<web head origin>/`. The returning page has `?code=` in its URL. Because the verifier lives in the memory of the tab that asked, the returning tab posts the code over a same-origin `BroadcastChannel` named `autonogrammer-auth` and waits 3 seconds for an acknowledgement from the asking tab, which exchanges it (`POST /token?grant_type=pkce`) and shows `SignedIn`. The returning tab scrubs the query with `history.replaceState` before doing anything else and shows "You are signed in in your other tab". With no acknowledgement it shows `LinkOpenedInWrongTab`: use the code from the email. `BroadcastChannel` is not a storage API; the new module `src/Autonogrammer.UI/wwwroot/scripts/autonogrammer-auth.js` contains no storage token (the storage scan stays unchanged and green).
- CSP: `scripts/vercel/render-csp.sh:25-62` accepts exactly one origin. It gains an optional second variable `AUTONOGRAMMER_AUTH_ORIGIN` (the same `https://host[:port]` validation, fail closed when sign-in is enabled and it is missing) and prints `connect-src 'self' <api origin> <auth origin>`; `vercel.ts` and `RenderCspScriptTests` follow. The Demo build keeps `connect-src 'self'` (ADR-0017). The web head reads the same origin from `wwwroot/appsettings.json` (`Auth:Origin`, written at build from the same variable) and refuses an `/auth/config` whose `projectUrl` origin differs, so a compromised API cannot send sign-in to another host and the CSP cannot silently block it. The desktop head trusts the `projectUrl` served over https by the API it was told to use.
- `Retry-After` is unreadable cross-origin, so the web head uses the 60 second default after a 429.

### D4. A hand-written GoTrue client, no Supabase SDK

`Autonogrammer.Accounts` (new L2 library, portable to Blazor WebAssembly, no process, no file system, no native code) holds the client. The six calls are plain `HttpClient` requests with the header `apikey: <publishable key>`; user-scoped calls add `Authorization: Bearer <access token>`. The publishable key is never sent as a bearer (R5 section 2.4).

| Call | Request | Success |
| --- | --- | --- |
| `SendEmailOtpAsync` | `POST /otp` as D2 | 200 `{}` (same answer for known and unknown users) |
| `VerifyEmailOtpAsync` | `POST /verify` `{type:"email",email,token}` | session |
| `ExchangeCodeAsync` | `POST /token?grant_type=pkce` | session |
| `RefreshAsync` | `POST /token?grant_type=refresh_token` | session |
| `SignOutAsync(scope)` | `POST /logout?scope=local\|global\|others`; the scope parameter is required in the C# signature, there is no default | 204 |
| `GetUserAsync` | `GET /user` | identity (used only by tests and the live check) |

Design rules:

- Typed models: wire records are `internal` with snake_case naming (`JsonNamingPolicy.SnakeCaseLower`), read leniently (unknown members skipped, because GoTrue adds fields) through `GoTrueJsonContext`; the stored session uses a second context with `UnmappedMemberHandling = Disallow`. Reflection serialisation is disabled in this repository (`Directory.Build.props` `JsonSerializerIsReflectionEnabledByDefault false`), so every type is source generated.
- Results, not exceptions, for expected outcomes: `AuthResult<T>` carries a value or an `AuthFailure(AuthErrorKind Kind, TimeSpan? RetryAfter, int? HttpStatus, string? ServerCode)`. `ServerCode` is kept only when it is in the fixed vocabulary above, so a hostile body cannot inject text into a log. Server `msg` text is never kept, shown or logged. Programming errors throw (`ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`).
- Tokens cross the library as `SecretString` (`src/Autonogrammer.Harness.Secrets/SecretString.cs`, redacting `ToString`), which needs one new same-layer edge `Autonogrammer.Accounts` to `Autonogrammer.Harness.Secrets` in `LayerMap.SameLayerAllowed` with an `// intentional:` comment (precedent: `LayerMap.cs:82-83`).
- Options are typed and validated: `GoTrueOptions` (project URL absolute https with no userinfo, query or fragment; key non-empty and not a secret key: a value starting `sb_secret_`, or a JWT whose `role` is `service_role`, is refused before use) is built from `AuthConfigResponse` by `GoTrueOptions.Create(...)`, which returns a validation result naming the field and never the value. The API side options follow the repository pattern (`IValidateOptions<T>` plus `ValidateOnStart`, `docs/plans/v0.16.0-research/09-project-recipe-and-conventions.md` section 4).
- Time: `TimeProvider` everywhere; a 15 second per-request timeout made with `TimeProvider.CreateCancellationTokenSource`; `CancellationToken` is the last parameter of every I/O method; library code uses `ConfigureAwait(false)`. No automatic HTTP retry on `/otp`, `/verify` and `/token?grant_type=pkce` (not idempotent, and a retry would burn the one-per-minute email budget); the single retry on a lost refresh response is in D5.
- Logging: optional `ILogger<T>? logger = null`, messages through `LoggerMessage.Define` with explicit `EventId` values 3000 to 3099, never an email address, code, token, verifier or URL query.
- Rejected: `Supabase.Gotrue` 8.5.1 (MIT, https://www.nuget.org/packages/Supabase.Gotrue/8.5.1; nuspec read 2026-10-28). It defaults sign-out to `Global` and email verification to the `MagicLink` type (R5 2.6, F2), would add `System.IdentityModel.Tokens.Jwt` 7.5.1 (its nuspec) to a Blazor WebAssembly bundle that does not need it, and its serialiser is [INFERENCE] not source generated. Six calls do not justify a dependency whose defaults we would wrap.

### D5. Session manager: refresh, rotation, offline start, sign-out

`AccountSessionManager` (in `Autonogrammer.Accounts`) is the only holder of the session and implements `IAccountSession` (`State`, `StateChanged`, `GetAccessTokenAsync(ct)`, `SignOutAsync(scope, ct)`).

- Single flight: one `SemaphoreSlim`-guarded in-flight refresh task is shared by every caller; a caller's cancellation stops its wait, not the shared refresh, which is bound to the manager's lifetime token. A token is refreshed on demand when its remaining life is below `max(60 s, 20 percent of its lifetime)`; there is no background timer, so an idle app makes no request.
- Rotation: the new refresh token is written to the secret store BEFORE the new session is published to callers. If the write fails, the in-memory session is used and the state carries the notice `SessionNotRemembered`; the old token is consumed either way, which is why the order matters (a crash between the response and the write leaves the old token, usable only inside the 10 second reuse interval, R5 section 2).
- Lost response: a transport failure on a refresh is retried once after 1 second, inside the reuse interval, then every 2 minutes up to 15 minutes with jitter while the state stays `SignedIn` with connectivity `Offline`.
- Two processes: refresh runs under a cross-process file lock (`FileShare.None`, in the desktop adapter, D6) and re-reads the store after taking it; if the stored refresh token differs from the one in memory another process rotated it, and this one adopts the stored session instead of refreshing (otherwise the second refresh would present a revoked token outside the reuse interval and end the session for both).
- Definitive failure (`refresh_token_not_found`, `refresh_token_already_used`, `session_not_found`, `session_expired`, `user_banned`): re-read the store once as above; if nothing newer exists, clear the store and the memory, move to `SignedOut` with the notice `SessionEnded`. Never an exception to the caller.
- Offline start: startup never waits on the network. If a stored session exists it is read, the state is `SignedIn` immediately with the identity stored beside the token (email and `sub`), connectivity `Refreshing` while one refresh with a 5 second budget runs, then `Online` or `Offline`. A stored session whose recorded issuer differs from the configured project is discarded with the notice `ServiceChanged`. Code mode needs no session at all.
- Sign-out: the local state and the stored secret are cleared FIRST (the person's intent), then a best-effort `POST /logout?scope=local` with a 5 second budget; if the access token is expired one refresh is attempted for it. A failed revoke leaves the notice `SignOutNotConfirmed`, never a signed-in state. "Sign out everywhere" (`scope=global`, which also signs the website out) and "sign out other devices" (`scope=others`) are separate menu entries with a confirmation that names that effect.
- Stored shape: one secret named `autonogrammer.account.session` holding `{schema:1, issuer, refreshToken, userId, email}`. The access token is not stored.

### D6. Token storage: an operating system store per platform

`Autonogrammer.Harness.Secrets.Os` (new L2 library; layer edge to `Autonogrammer.Harness.Secrets` added to `SameLayerAllowed`) implements the existing `ISecretStore` (`src/Autonogrammer.Harness.Secrets/ISecretStore.cs:24-63`) per operating system:

| OS | Store | Notes |
| --- | --- | --- |
| Linux | Secret Service through libsecret 0.21.4 (`libsecret-1.so.0`), the non-variadic `secret_password_storev_sync`, `lookupv_sync`, `clearv_sync`, `searchv_sync` and `secret_retrievable_get_attributes`, with a GLib hash table of attributes `{app: "autonogrammer", name: <name>}` | Loaded at run time with `NativeLibrary.TryLoad`, so a missing library is a typed unavailable state, not a crash. Calls run off the caller's thread with a `GCancellable` bound to the token. The Secret Service has no per-application isolation: any process of the same unlocked session can read the item (stated in D7 threats). |
| macOS | Keychain Services (`SecItemAdd`, `SecItemCopyMatching`, `SecItemUpdate`, `SecItemDelete`) over CoreFoundation, generic password items, service `Autonogrammer`, account = name | Not `/usr/bin/security`: its `add-generic-password` puts the secret on argv. An unsigned build may prompt on every launch; a denied prompt maps to `Denied`. |
| Windows | DPAPI (`ProtectedData`, scope `CurrentUser`, fixed application entropy) over one file per name in the per-user local application data folder, atomic replace | Protects against other users and offline disk theft, not against malware running as the same user. Credential Manager was considered; DPAPI is managed code with no native structs to marshal, which matters because this code cannot be run on this host. |

Binding style is `DllImport` or function pointers over `IntPtr`, `int` and `long` only, strings marshalled by hand and the native copy zeroed after use, `[DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]` as in `src/Autonogrammer.Sandbox/PosixProcessSignal.cs:15-16`; no `AllowUnsafeBlocks` is introduced. Failures are `SecretStoreException` (the existing type, `src/Autonogrammer.Harness.Secrets/SecretStoreException.cs`) which gains an additive `Failure` property: `Unavailable` (no service or no library), `Locked`, `Denied`, `Corrupt`, `Other`. Messages never contain a value.

Selection (`AccountSecretStoreSelector`, in `Autonogrammer.Accounts.Desktop`, L3):

1. The head asks the selector only when the non-secret setting `account.hasStoredSession` in `settings.json` is true or a sign-in just succeeded, so a first launch of a person who only uses Code never touches the keyring.
2. The operating system store is used when it works. When it does not (`Unavailable`, `Denied`, or `Locked` after the person dismissed the unlock prompt), the session is kept in memory only, and the account panel shows the notice `SessionNotRemembered` with the specific reason and one action: "Remember sign-in on this device without system protection".
3. That action sets `account.allowFileSecretStore = true` and switches to the repository's `OwnerOnlyFileSecretStore` (plain text with 0600/0700 permissions and no Windows ACL hardening, `docs/plans/v0.16.0-research/03-api-auth-backend.md` section 10). It is the only way the file store is used for accounts, it is a visible degraded state and it can be withdrawn.
4. A device private key (ADR-0036, `autonogrammer.dispatch.device-key.<deviceId>`) is NEVER stored in the file fallback: Dispatch reports `KeyStoreUnavailable` instead.

Verifiable on this Linux host: a `gnome-keyring-daemon` (46.1) runs here (two processes, `--components=secrets` and `--components=pkcs11,secrets`), `libsecret-1.so.0` 0.21.4 is installed and exports every symbol above (`nm -D`), and the session bus is `unix:path=/run/user/0/bus`. A `Category=Live` test (`AUTONOGRAMMER_LIVE_KEYRING=1`) stores, reads, lists and removes a throwaway item under a unique `app` attribute and skips with the reason when the collection is locked. The Windows and macOS code is compile-checked only.

### D7. API side

Everything stays in `services/Autonogrammer.Api` (no new service: there is no trust-boundary reason, the API already is the owner-scoped boundary).

- Token validation reuses the existing module. `Api:Auth:Authority` = `https://xpfoidcvbwmpbwodjvqb.supabase.co/auth/v1`, `Audience` = `authenticated`, `ValidIssuer` = the same string. Changes in `Security/JwtBearerSetup.cs` and `Options/ApiAuthOptions*.cs`: `ValidAlgorithms` pinned to `ES256` (`Api:Auth:Algorithms`, validated to a subset of `ES256` and `RS256`, default `["ES256"]`), `RequireSignedTokens`, `ValidateLifetime`, `ClockSkew` 30 seconds, `ValidIssuer` required outside Development and validated as absolute https, and in `OnTokenValidated` the claims `is_anonymous` (reject when true) and `role` (must be `authenticated`) are checked in addition to `sub`. `MapInboundClaims` stays false. An unknown `kid` triggers a metadata refresh through the JwtBearer default [INFERENCE: `RefreshOnIssuerKeyNotFound` defaults to true; a test with a rotated key proves it].
- Owner is `sub`, unchanged: `IUserContext.Name` (`Security/AuthenticatedUserContext.cs:20-22`). `user_metadata` never decides access (R5 1.5).
- Accounts: the `accounts` table is migration M0004 of [ADR-0031](ADR-0031-data-plane-namespaces-and-resilience.md) (keyed by `sub`, created by the migration runner, no email column), with an in-memory twin. `AccountResponse.email` is read from the validated token's `email` claim and is never stored. `AccountEnrollmentMiddleware` (after `UseAuthorization`) upserts `last_seen_at` only, creating the row on the first authenticated request of a `sub` and then at most every 10 minutes (an in-process cache), skips the development principal and never blocks a request on a store failure (it logs and continues, the row is created on the next call). Open sign-up (any confirmed user of the project may have an account) is the default; an allow-list is open question OQ-304.
- Server-filesystem endpoints: `Services/WorkspaceSettings/WorkspaceAccess.cs:58-63` permits every caller when `Api:Workspace:Owners` is empty. With open sign-up that exposes `/workspace/*`, `/settings/*` and `/hooks` to every account, so `Permits` returns false when the list is empty unless the development principal is on.
- Public client configuration: `GET /auth/config`, anonymous, `Cache-Control: public, max-age=300`, one global rate-limit bucket (the remote address behind the Railway edge is [INFERENCE] the proxy's), body `AuthConfigResponse` `{projectUrl, publishableKey, methods:{emailCode, magicLink}, emailCode:{expirySeconds, resendCooldownSeconds}, signupEmailHasCode}` bound from `Api:Auth:Client` (`ProjectUrl`, `PublishableKey`, `MagicLinkEnabled` default false, `EmailCodeEnabled` default true, `EmailOtpExpirySeconds` 3600, `ResendCooldownSeconds` 60, `SignupEmailHasCode` default false). The validator refuses a key starting `sb_secret_` or a JWT with role `service_role`, a non-https `ProjectUrl` and `MagicLinkEnabled` without `EmailCodeEnabled`, and logs a warning when `MagicLinkEnabled` and `SignupEmailHasCode` are both false (first-time users then follow the O4 row path).
- Per-user rate limits: every policy (`Security/SessionRateLimiterPolicy.cs:33-48`, the creation, provider, workspace and automation policies) partitions by the `sub` claim, with one shared partition for unauthenticated callers. The limiter runs after authentication (`Program.cs:249`), so a partition exists only for a token that validated; a hostile client cannot create partitions. A new `Accounts:RequestsPerMinute` (default 120) covers the account routes.
- OpenAPI: a document transformer adds the security scheme `bearerAuth` (`http`, `bearer`, `bearerFormat: JWT`) with a document-level requirement, `security: []` on the anonymous operations (`/auth/config`, `/source`, the webhook receiver which keeps its HMAC description, and `GET /cowork/artifact-render/{ticket}` of ADR-0033 with its own limiter partition) and the scheme `DispatchDevice` (not `bearerAuth`) on `/dispatch/devices/{id}/connect` of ADR-0036 and `401` and `403` responses; `docs/api/openapi.json` is regenerated (`GuardTests.cs:58-74` compares it with the served document), the Demo handler and `ClientRouteSurface` gain the new routes.
- Revocation gap: a bearer stays valid until `exp` (3600 s by default) after sign-out or ban (`docs/plans/v0.16.0-research/03-api-auth-backend.md` 11.1). Accepted for v0.16.0; ADR-0036 decides a freshness rule for pairing (the newest non-refresh `amr` entry of the principal, not `iat`, because every refresh issues a new `iat`) and records the `session_id` claim on the device row; Computer Use approvals are local-only (ADR-0035 D12).

### D8. The account surface and its use of sessions

Routes, all behind the fallback policy and owner-scoped by `sub`:

| Route | Answer |
| --- | --- |
| `GET /account` | `AccountResponse {id, email, createdAt, lastSeenAt, features[]}`; `features` lists the module keys that registered an `IAccountFeature` (`cowork` here, `dispatch` by ADR-0036), so the menu shows only what exists |
| `GET /account/sessions` | `AccountSessionList {items[SessionSummary]}`: the caller's Code sessions and Cowork sessions merged from two typed repository calls, each row labelled with `surface`, newest first, capped by the repository |
| `GET /account/sessions/{id}` | one `SessionSummary`; another owner's session and an absent one are the same 404 as `FindOwnedSessionAsync` (`Endpoints/SessionEndpoints.cs`, `docs/plans/v0.16.0-research/03-api-auth-backend.md` section 3) |
| `GET /account/board` | the Kanban board of the caller's Code sessions; it REPLACES `GET /board` (clean cutover: the Web client, Demo handler, `ClientRouteSurface` and OpenAPI move in the same change; no alias remains) |

Collections list only the caller's rows; an item route answers 404 for another owner. No unfiltered list exists: the session repository work (brief 68, ADR-0031) provides `ListByOwnerAsync(ownerId, SessionSurface surface, includeClosed, limit, cursor, ct)` with `surface` required (agreed with ADR-0032), `SetClosedAsync(sessionId, closed, ct)` (true when the flag changed), the migration M0003 (`surface` column, `closed_at`, the `(owner_id, updated_at desc)` index) and the in-memory twin; this record consumes them and surfaces no cursor in v0.16.0 (one page per surface, `includeClosed` true). The discriminator is named `SessionSurface` (`Code`, `Cowork`) and not `SessionKind` because `Autonogrammer.Contracts.Agents.SessionKind` (`Chat`, `Terminal`) already exists (`src/Autonogrammer.Contracts/Agents/SessionKind.cs:4`).

Rules fixed here: (1) a Cowork session is never a Kanban card: `/account/board`, `/sessions` and the catalog event stream read Code only, through the single board source of ADR-0032 (`CodeBoardSource`); a test creates a Cowork session and asserts it appears in `/account/sessions` and nowhere else. (2) The in-process `SessionRegistry` and `SessionLifecycleStore` are deleted; their callers (`Program.cs`, `Cloud/CloudAgentService.cs`, `Endpoints/CatalogEndpoints.cs`, `Endpoints/SessionEndpoints.cs`, `Endpoints/SteeringEndpoints.cs`, `Services/CatalogChangeDetector.cs`, `Services/WebAppServiceRegistration.cs`, `tests/.../Cloud/CloudApiHost.cs`) move to the repository calls, so a restart no longer empties the list (`03-api-auth-backend.md` finding 2). (3) Automations, forks and cloud agents create Code sessions explicitly.

### D9. The five user interface states (Native and Web)

A view model in `Autonogrammer.Presentation` (`AccountViewModel` over an `IAccountHost`, the pattern of `ICloudAgentsHost`; Presentation must not reference `Accounts`, `PresentationIsolationTests.cs:17-60`) draws exactly one state at a time. `AuthPhase` and `AuthErrorKind` are in `Autonogrammer.Contracts.Accounts` so both libraries share them.

| State | Shows | Notes |
| --- | --- | --- |
| `SignedOut` | email field, "Send code", "Continue without signing in" only where Cowork asks for sign-in | Code mode never shows a sign-in prompt and works signed out |
| `AwaitingLink` | address, code field (`autocomplete=one-time-code`, numeric input mode), resend button with cooldown, expiry countdown, "Change email", the link sentence, the first-time-user hint when `signupEmailHasCode` is false | The change-email action closes the listener and returns to `SignedOut` with the address kept |
| `SigningIn` | indeterminate progress, "Cancel" | Cancel returns to `AwaitingLink` (code check) or `SignedOut` (restore) |
| `SignedIn` | account menu: identity, Account, Devices (only when `features` has `dispatch`), Sign out, Sign out everywhere, notices (`Offline`, `SessionNotRemembered`, `SignOutNotConfirmed`, `ServiceChanged`) | Offline is a notice on this state, not a sign-out |
| `Error` | one message per `AuthErrorKind` (D10), the retained address, a primary action, and a return target (`AwaitingLink` or `SignedOut`) | A wrong code is an `Error` that returns to the code field |

Accessibility: every control has an automation name; focus moves to the code field on `AwaitingLink` and to the message on `Error` (`role=alert` on the web); the resend and expiry countdowns update visually every second but announce only at 30 s, 10 s and when available, so a screen reader is not flooded; Enter submits and Escape cancels; no new colour roles (existing tokens, so `ThemeContrastTests` and `CssContractTests` stay green); the spinner is static under reduced motion. Copy lives in one catalogue (`AccountCopy`) keyed by enum value, so no raw exception or server text can reach a screen.

### D10. Error taxonomy

| Cause | `AuthErrorKind` | State and recovery |
| --- | --- | --- |
| 429 `over_email_send_rate_limit` on `/otp` | `EmailRateLimited` | `Error`; explains the shared hourly limit; retry after about an hour |
| 429 `over_request_rate_limit` | `TooManyRequests` | `Error` with the wait from `Retry-After` (desktop) or 60 s |
| 403 `otp_expired` on `/verify` | `CodeInvalidOrExpired` | `Error` returning to the code field; counts toward the five attempts |
| callback `error`, `bad_code_verifier`, `flow_state_*` | `LinkInvalidOrExpired` | `Error` returning to `AwaitingLink`; code still works |
| returning tab without acknowledgement (web) | `LinkOpenedInWrongTab` | message on the returning tab; code path |
| 400 `validation_failed`, `email_address_invalid` | `EmailInvalid` | `Error` returning to `SignedOut` |
| 422 or 400 `signup_disabled` | `SignUpsClosed` | `Error`, no retry |
| 400 `captcha_failed` | `CaptchaRequired` | `Error`, no retry (O6) |
| `user_banned` | `AccountBlocked` | `Error`, no retry |
| `otp_disabled`, `email_provider_disabled`, `feature_disabled` | `MethodUnavailable` | `Error`; the operator must change the project |
| refresh failures of D5 | `SessionEnded` | `SignedOut` with a notice |
| `HttpRequestException`, timeout, DNS | `NetworkUnavailable` | during sign-in an `Error` with retry; during refresh `SignedIn` plus `Offline` |
| 5xx, `hook_timeout` | `ServiceUnavailable` | `Error` with retry |
| `/auth/config` unreachable or invalid | `ConfigUnavailable` | `Error`: the Autonogrammer service cannot be reached; Code mode unaffected |
| loopback bind failure | `LoopbackUnavailable` | not an error state: a notice, code only |
| the API answers 401 after a valid sign-in (rejected claims, `is_anonymous`) | `AccountRejected` | `Error`; signs out locally |
| anything else | `Unexpected` | `Error` with a fixed sentence; details only in logs with a correlation id |

### D11. Tests, fakes and what is gated

- Fakes (no network, no Supabase): `FakeGoTrueHandler` (an `HttpMessageHandler` with the six routes, OTP issue and expiry on a `ManualTimeProvider`, S256 verifier checking, refresh rotation with the 10 second reuse interval, scope-aware logout, scripted error bodies, 429 with and without `Retry-After`), a Kestrel `FakeGoTrueHost` for the loopback end-to-end and the API integration tests, and `FakeJwks`: an in-test ES256 key, tokens minted with chosen `iss`, `aud`, `role`, `is_anonymous`, `sub`, `session_id`, `exp`, and a static `OpenIdConnectConfiguration` (the existing `JwtTestHost` already uses static metadata with no network, `docs/plans/v0.16.0-research/03-api-auth-backend.md` section 1; it moves from an HMAC key to ES256).
- Hand-written fakes shipped as `Fake*`/`InMemory*` follow the repository convention (no NSubstitute).
- Live-gated, blocked on the owner's publishable key and unpaused hosting: `Category=Live` and `AUTONOGRAMMER_LIVE_SUPABASE=1` with the key and, optionally, a short-lived access token and a refresh token supplied by the owner in environment variables, covering: `/auth/v1/settings` reachable, a real access token passes the API validator (ES256, issuer, audience), a real refresh rotates once, and the three [INFERENCE] items (code verifies after a PKCE request; the signup link confirms the user without an allow-listed redirect; the `*` port pattern of O1). No live test sends an email: the shared quota is 2 per hour. The owner's manual sign-in check is a runbook (`docs/runbooks/accounts.md`).

## Blocked, and by what

- Live Supabase calls: the publishable key (owner) and the owner actions O1, O3, O4.
- Live API validation against the real project: Railway deploys are paused and no public API domain exists (`docs/runbooks/hosting.md`, OD-03 wording to be reconciled by the documentation unit).
- Windows DPAPI and macOS Keychain code: no Windows or macOS host; compile-checked only.
- Web two-tab link hand-off and the web CSP: needs a headless browser; the web smoke script (`scripts/web-smoke/run-smoke.sh`) has one but is not run by this design pass.
- Account deletion and data export: not designed here (OQ-305).

## Consequences

- Both heads depend on the API being reachable once to learn the project URL and key; a signed-out Code user needs neither. The desktop caches nothing from `/auth/config` on disk.
- `GET /board` disappears in favour of `GET /account/board` and `AUTONOGRAMMER_API_TOKEN` (now `AUTONOGRAMMER_API_TOKEN`) stops being read by the head; the session token replaces it (`NoApiTokenProvider` is deleted).
- Two new L2 projects (`Autonogrammer.Accounts`, `Autonogrammer.Harness.Secrets.Os`), one L3 project (`Autonogrammer.Accounts.Desktop`), two same-layer allow-list entries, one new package (`System.Security.Cryptography.ProtectedData` 10.0.13), a relock of every dependent lock file, `THIRD-PARTY-NOTICES.md`, the `docs/architecture.md` table and `CHANGELOG.md`.
- The web head is signed out after every reload. This is the price of the no-storage rule; OQ-303 records the opt-in alternative.
- The shared project is one user table, one email hook and one rate-limit pool for every app on it (R5 F3, F7). The same person signing in by email here and by Google on the website is two users (R5 F8); this is not hidden.

## Candidate chosen

A hand-written GoTrue REST client (`Autonogrammer.Accounts`) over the six calls of D4 with email one-time code and PKCE magic link, a single-flight rotating session manager, an operating-system secret store per platform (`Autonogrammer.Harness.Secrets.Os`: libsecret on Linux, Keychain Services on macOS, DPAPI on Windows) with the owner-only file store only as a consented degraded fallback, the existing JwtBearer module hardened for the Supabase issuer (ES256, audience `authenticated`, `is_anonymous` rejected, owner = `sub`) with a public `GET /auth/config`, and the owner-scoped account routes `GET /account`, `/account/sessions` and `/account/board` in `services/Autonogrammer.Api/Accounts`.

## Licence

| Component | SPDX | Source | Verified |
| --- | --- | --- | --- |
| `Autonogrammer.Accounts`, `Autonogrammer.Accounts.Desktop`, `Autonogrammer.Harness.Secrets.Os`, the API account module | AGPL-3.0-or-later | https://www.gnu.org/licenses/agpl-3.0.html | Own code, the project licence (ADR-0018) |
| `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.13 (existing central package, `Directory.Packages.props:32`) | MIT | https://github.com/dotnet/aspnetcore/blob/main/LICENSE.txt | Already in the repository |
| `System.Security.Cryptography.ProtectedData` 10.0.13 (new, Windows head only) | MIT | https://www.nuget.org/packages/System.Security.Cryptography.ProtectedData/10.0.13 | nuspec `license type="expression"` MIT read 2026-10-28; version exists on nuget.org (10.0.14 is also published; 10.0.13 matches the repository's Extensions pin) |
| libsecret 0.21.4 (Linux system library, loaded at run time, not distributed with the application, no code copied) | LGPL-2.1-or-later | https://gitlab.gnome.org/GNOME/libsecret | Source header read at https://github.com/GNOME/libsecret/blob/master/libsecret/secret-password.c ("version 2.1 of the licence or (at your option) any later version"); installed version from `dpkg -l` on this host |
| GLib (`libglib-2.0.so.0`, hash table calls, loaded by libsecret) | LGPL-2.1-or-later | https://gitlab.gnome.org/GNOME/glib | `SPDX-License-Identifier: LGPL-2.1-or-later` in `glib/ghash.c`, read 2026-10-28 |
| Apple Security and CoreFoundation frameworks (macOS system APIs, no code taken) | not applicable (operating system API) | https://developer.apple.com/documentation/security/keychain_services | Not verified on a Mac |
| Supabase Auth (GoTrue) REST API (hosted service, no code taken; the server is open source) | MIT (server source) | https://github.com/supabase/auth/blob/master/LICENSE | LICENSE read 2026-10-28; the hosted project is subject to Supabase terms |
| `Supabase.Gotrue` 8.5.1 (REJECTED, not added) | MIT | https://www.nuget.org/packages/Supabase.Gotrue/8.5.1 | nuspec read 2026-10-28 |

## Verification route

`dotnet test --project tests/Autonogrammer.Accounts.Tests -c Release --filter-not-trait "Category=Live"` (client, PKCE, single-flight refresh, rotation, offline start, sign-out, error taxonomy, fake GoTrue); `dotnet test --project tests/Autonogrammer.Accounts.Desktop.Tests -c Release --filter-not-trait "Category=Live"` (loopback listener over real TCP, store selector, cross-process lock); `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release --filter-not-trait "Category=Live"` (ES256 and `is_anonymous` rejection, `/auth/config`, account routes and 404 scoping, per-sub rate limits, OpenAPI freshness); `dotnet test --project tests/Autonogrammer.Presentation.Tests`, `tests/Autonogrammer.Native.Tests` and `tests/Autonogrammer.UI.Tests` for the five states and accessibility; `AUTONOGRAMMER_LIVE_KEYRING=1 dotnet test --project tests/Autonogrammer.Harness.Secrets.Os.Tests -c Release --no-build --filter-trait "Category=Live"` against the keyring of this host; `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` for the layer map and this record's structure. The live Supabase rows of D11 need the owner's key and are not part of the gate.

## Open risks

- Three hosted behaviours are [INFERENCE] until a live run: a code requested with a PKCE challenge still verifies through `POST /verify`; the O4-less first-time path (open the signup link, then request a new code) confirms the user; `http://127.0.0.1:*/**` matches an ephemeral port (R5 open item c).
- The shared project's email hook, 2 per hour limit and user table belong to the website and other apps; a change there changes this product's sign-in without a release.
- Any process of the same user session can read a Secret Service item, and on Windows any process of the same user can call DPAPI with the fixed entropy: the stored refresh token is as safe as the user session. A stolen refresh token is a full sign-in until revoked.
- A bearer stays valid until expiry after sign-out or ban (D7).
- The web head signs out on reload, and two tabs that are both awaiting a link can race for one code (the loser fails with `LinkInvalidOrExpired`) [INFERENCE: a failed exchange with a wrong verifier does not consume the code].
- The Windows and macOS stores are unrun; an unsigned macOS build may prompt on every launch.
- Open sign-up gives every confirmed user of the shared project an Autonogrammer account; cost and abuse controls are the per-user limits and the disabled-by-default cloud module only.
- A crash between a refresh response and the secret store write loses the session when it is longer than the 10 second reuse interval.
