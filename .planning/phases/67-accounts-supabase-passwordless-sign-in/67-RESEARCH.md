# Phase 67: Accounts: Supabase passwordless sign-in - Research
Source: docs/plans/v0.16.0-research/briefs/67-accounts.md

Summarises research reports 03 (API/auth backend), 05 (Supabase auth reference) and 09 (project recipe and conventions), plus ADR-0030 and the index R-items R5, R10, R18. Citations are `file:line` repo-relative; report citations are into `docs/plans/v0.16.0-research/`.

## Standard Stack / What to reuse
- Secret seam: `ISecretStore` four methods `src/Autonogrammer.Harness.Secrets/ISecretStore.cs:35,44,54,62`; its remark says an OS store implements it "once it can be verified on a runner" (`:20`); `SecretString` `src/Autonogrammer.Harness.Secrets/SecretString.cs:21`; `OwnerOnlyFileSecretStore` `OwnerOnlyFileSecretStore.cs:25` (plain text, 0600/0700, no Windows ACLs, no cross-process lock: report 03 line 114).
- `SecretStoreException` is `sealed class ... : Exception` (`SecretStoreException.cs:7`); the `Failure` property is additive and does not exist yet.
- JwtBearer already wired: `services/Autonogrammer.Api/Program.cs:86` (`AddJwtBearer`), setup `Security/JwtBearerSetup.cs:32-40` (`MapInboundClaims=false`, `ValidAudience`, `ValidIssuer`, `OnTokenValidated` rejects a missing `sub`); options and validator `Options/ApiAuthOptions.cs`, `Options/ApiAuthOptionsValidator.cs` (https rule on `Authority` only, report 03 line 34).
- Pipeline order: `UseAuthentication` `Program.cs:242`, `UseRateLimiter` `:249`, `UseAuthorization` `:250`; rate limiter therefore sees `HttpContext.User` (report 03 line 42). The enrollment middleware goes after `:250`.
- Owner-scoping pattern: `FindOwnedSessionAsync` `services/Autonogrammer.Api/Endpoints/SessionEndpoints.cs:417`.
- Module pattern to copy (Cloud, Automations): options + `IValidateOptions` + `ValidateOnStart`, `AddXxxServices`, `MapXxxEndpoints` (report 03 line 44; report 09 line 59).
- Presentation pattern: `ICloudAgentsHost` `src/Autonogrammer.Presentation/Cloud/ICloudAgentsHost.cs:9`; isolation guard `tests/Autonogrammer.Presentation.Tests/PresentationIsolationTests.cs`.
- Existing token seams to replace: `apps/Autonogrammer.Web/Services/IApiTokenProvider.cs:8,13` (comment says sign-in replaces `NoApiTokenProvider`); `apps/Autonogrammer.Native/Composition/HeadServices.cs:44,47` (`AUTONOGRAMMER_API_BASE_URL`, `AUTONOGRAMMER_API_TOKEN`).
- Test fixtures: `tests/Autonogrammer.Api.IntegrationTests/JwtTestHost.cs:30,116` (HMAC `SymmetricSecurityKey`, `HmacSha256`: to move to ES256 with static metadata); `tests/Autonogrammer.Web.Tests/RenderCspScriptTests.cs`; `tests/Autonogrammer.Web.Tests/ClientRouteSurface.cs`; `ManualTimeProvider` in `tests/Autonogrammer.Coordination.Tests/ManualTimeProvider.cs` and `tests/Autonogrammer.Web.Tests/ManualTimeProvider.cs` (copy; no shared test lib).
- Safe-search DllImport precedent `src/Autonogrammer.Sandbox/PosixProcessSignal.cs:16`.

## Facts the planner needs
- GoTrue: `POST /verify` with `type:email` returns a session with no verifier (report 05 line 82); `GET /verify` redirects with `?code=` only with a PKCE challenge, else a URL fragment a loopback listener cannot read (report 05 lines 83, 140); `POST /logout` defaults to scope `global` (report 05 lines 86, 134); refresh tokens are single use, reuse interval 10 s (report 05 line 85); the PKCE `code` lives 5 min, single use (report 05 line 84).
- Email OTP is 8 digits live, email limit 2/h project-wide, email expiry 3600 s (report 05 lines 67, 103-107, 152); first-time users get a `signup` email with no code unless O4 is done (report 05 line 132).
- Auth host CORS preflight allows `apikey,content-type,authorization,x-client-info`, does not expose `Retry-After` (ADR-0030 line 18).
- Issuer `https://xpfoidcvbwmpbwodjvqb.supabase.co/auth/v1`, audience `authenticated`, one ES256 JWKS key (report 05 lines 17-19); JWKS edge cache about 10 min, cache-bust on unknown `kid` is the API's job (report 05 line 97).
- Rate limits are one global bucket per policy today: `Security/SessionRateLimiterPolicy.cs:33-38` (constant partition `"prompts"`); report 03 line 83 lists every policy file to change (`SessionCreationRateLimiterPolicy`, `ProviderRateLimiterPolicy`, `WorkspaceSettingsRateLimiterPolicy`, `AutomationRateLimits`, `Options/ApiRateLimitOptions.cs`).
- `WorkspaceAccess.Permits` allows everyone when `Api:Workspace:Owners` is empty: `Services/WorkspaceSettings/WorkspaceAccess.cs:58-61`.
- OpenAPI today has `"security": null` and no security scheme; committed file is equality-tested: `tests/Autonogrammer.Api.IntegrationTests/GuardTests.cs:64-69`; contract coverage tests `tests/Autonogrammer.Web.Tests/OpenApiCoverageTests.cs` (report 03 lines 90-91). `DeferredScopeTests` forbids the needles `ADE-21`, `ADE-22`, `vscode` in code and `openapi.json` (report 03 line 90).
- `SessionRegistry` registered `Program.cs:104`, used by `Endpoints/CatalogEndpoints.cs:61,80`; `GET /board` is `CatalogEndpoints.cs:32`; both registries are process-local (report 03 line 68).
- CSP: `scripts/vercel/render-csp.sh:6-8` accepts exactly one https API origin (`AUTONOGRAMMER_API_ORIGIN`); output is exactly one line `connect-src 'self' <origin>` (`:8`).
- Web head forbids browser storage APIs: `tests/Autonogrammer.UI.Tests/Security/SessionKeyHolderTests.cs:48-49` (string literals count, because Blazor reaches storage by name through JS interop).
- `LayerMap.SameLayerAllowed` is at `tests/Autonogrammer.ArchitectureTests/LayerMap.cs:65`; every tuple needs an `// intentional:` comment (`:74` onward). Scaffold adds `(Accounts, Harness.Secrets)` and `(Harness.Secrets.Os, Harness.Secrets)` (`00-scaffold-manifest.md` rows 4, 5).
- `ContractVersion.Current` is `1.5.0` today (`src/Autonogrammer.Contracts/ContractVersion.cs:9`); the scaffold step bumps to 1.6.0, this phase never edits it (R9).
- No `Directory.Packages.props` change in this phase: the scaffold pins `System.Security.Cryptography.ProtectedData` 10.0.13 (manifest section 3); `THIRD-PARTY-NOTICES.md` is regenerated by it.
- Conventions: `ILogger<T>? logger = null` with `LoggerMessage.Define` and explicit `EventId`; `CancellationToken` last; `TimeProvider` injected (report 09 lines 60-61); reflection STJ is disabled so every type needs a source-generated context (report 09 line 62); public AND internal members need XML docs (report 09 line 52); NSubstitute is pinned but unused: hand-written fakes (report 09 line 67).

## Architecture Patterns
- Client returns `AuthResult<T>`; only the session manager mutates state; heads see only `IAccountSession`/`IAccountHost` (ADR-0030 D4, D5, D9).
- Store-first rotation plus a cross-process `FileShare.None` lock with adopt-if-rotated (ADR-0030 D5); the lock lives in the L3 desktop adapter, not in the web-safe L2 library.
- API module `services/Autonogrammer.Api/Accounts` (endpoints, service, options) registered next to the existing `AddXxxServices` calls (report 03 lines 133, 152); enrollment is middleware, never blocks.
- Web hand-off: `BroadcastChannel` `autonogrammer-auth` through one allow-listed script `src/Autonogrammer.UI/wwwroot/scripts/autonogrammer-auth.js`; existing scripts sit beside it (`autonogrammer-keys.js`, `autonogrammer-dialog.js`).
- Account menu lives in the window-level `SurfaceHost` chrome slot of phases 69/70, not in a per-view header.

## Pitfalls
- Default logout is global and kills the website session (report 05 F2): `SignOutAsync` has no default scope.
- Supabase.Gotrue defaults to `Global` sign-out and `MagicLink` verify type (report 05 line 126): rejected, do not add it.
- Publishable and secret keys are not JWTs; never send them as bearer (report 05 line 77); refuse `sb_secret_` and `service_role` at config time.
- A fixed 6-digit input fails against the live 8-digit code (report 05 F11).
- Two concurrent PKCE requests for one user can cross-wire; the newer email invalidates the older (report 05 F5): reuse verifier and nonce on resend, the code path is primary.
- A redirect target outside the allow-list silently falls back to the website Site URL (report 05 F6), so the first-time flow stays harmless but confusing.
- `JwtTestHost` uses a symmetric HMAC key (`JwtTestHost.cs:30`); an ES256 pin breaks every existing API test until it moves to an ES256 key with static `OpenIdConnectConfiguration`.
- Adding a project is invisible to the gate unless slnx, slnf, `LayerMap`, ArchitectureTests csproj and locks land together (report 09 lines 9-12): the scaffold owns this (R17); do not register from a unit.
- `GuardTests` equality: any route change needs `docs/api/openapi.json` regenerated and committed; the Demo handler and `ClientRouteSurface` must gain `/auth/config` and `/account*` and lose `/board` in the same change.
- Process-local registries lose the list on restart (report 03 line 68); deleting them is gated on phase 68 U3, so unit 4 cannot start earlier.
- Windows ACLs are not set by the file store, and DPAPI uses fixed entropy: same-user malware can read the token (ADR-0030 Open risks).
- Library code must not log email, code, token, verifier or URL query; a test scans captured logs (brief unit 1).

## Verification commands
From brief 67 "Success criteria" (run by the main agent once after all units land):
- `dotnet test --project tests/Autonogrammer.Accounts.Tests -c Release --filter-not-trait "Category=Live"`
- `dotnet test --project tests/Autonogrammer.Harness.Secrets.Os.Tests -c Release --filter-not-trait "Category=Live"` and `dotnet test --project tests/Autonogrammer.Accounts.Desktop.Tests -c Release --filter-not-trait "Category=Live"`
- `AUTONOGRAMMER_LIVE_KEYRING=1 dotnet test --project tests/Autonogrammer.Harness.Secrets.Os.Tests -c Release --no-build --filter-trait "Category=Live"`
- `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release --filter-not-trait "Category=Live"` (includes `GuardTests` OpenAPI equality)
- `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release`, `... tests/Autonogrammer.Native.Tests ...`, `... tests/Autonogrammer.UI.Tests ...`, `... tests/Autonogrammer.Web.Tests ...`
- `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release`
- `dotnet build Autonogrammer.Linux.slnf -c Release -warnaserror`; `bash scripts/ci/check-licences.sh`; `CI=true dotnet restore Autonogrammer.Linux.slnf --locked-mode`
- [INFERENCE from report 09 lines 96-100] single class: `dotnet test --project tests/Autonogrammer.X.Tests -c Release -- --filter-class "*FooTests*"`.

## Cannot be verified on this host
- Any live Supabase call (needs the owner's publishable key, OQ-301, and owner actions O1, O3, O4); three behaviours stay [INFERENCE] (ADR-0030 Open risks).
- Windows DPAPI and macOS Keychain: compile only.
- Web two-tab hand-off and the CSP in a real browser (headless browser of `scripts/web-smoke/run-smoke.sh`, not run).
- A real deploy: Railway paused, no public API domain (`docs/runbooks/hosting.md`).
- Linux keyring live test is possible (gnome-keyring-daemon 46.1, libsecret 0.21.4 per ADR-0030 line 118) but was not run; it skips with a reason when the collection is locked.

## Validation Architecture
Framework: xunit.v3 4.0.2 + Shouldly 4.3.0 on Microsoft.Testing.Platform (report 09 line 66); `[Trait("Category","Live")]` plus `Assert.Skip` first line for live rows (report 09 line 69).
Quick run: `dotnet test --project tests/Autonogrammer.Accounts.Tests -c Release --filter-not-trait "Category=Live"`. Full run: the command list under Verification commands.

| Requirement | Test project: behaviour (from brief acceptance) |
| --- | --- |
| AG-67-1, 3, 5 | `Accounts.Tests`: `FakeGoTrueHandler` all six calls and every D10 row; first-time hint and link-then-code path |
| AG-67-2 | `Accounts.Desktop.Tests`: loopback listener over real TCP (wrong Host, wrong nonce, oversize line, second callback, timeout, error query); S256 PKCE in `Accounts.Tests` |
| AG-67-4, 24 | `UI.Tests` BroadcastChannel hand-off against a fake channel, origin-mismatch refusal, storage scan green; `Web.Tests` `RenderCspScriptTests` (valid, missing when sign-in on, http, wildcard) |
| AG-67-6, 7, 8, 9 | `Accounts.Tests`: single flight under 50 callers, rotation write order with a failing store, reuse interval on `ManualTimeProvider`, offline start, sign-out scopes, cooldown and 5-attempt rule, no secret/email/code in captured logs; `Native.Tests` startup |
| AG-67-10, 11, 12 | `Harness.Secrets.Os.Tests` `ISecretStore` contract suite against an in-memory libsecret fake and Live keyring; `Accounts.Desktop.Tests` selector degrade paths, first-launch no keyring call |
| AG-67-13, 15, 16, 17 | `Api.IntegrationTests`: ES256 accepted; HS256, wrong issuer, wrong audience, expired (31 s), `is_anonymous` true, role anon rejected; rotated `kid`; per-sub bucket isolation; deny on empty owners; OpenAPI scheme + `GuardTests` |
| AG-67-14, 18, 19, 20, 21, 22 | `Api.IntegrationTests`: `/auth/config` validator rows and anonymous access; enrollment idempotence and 10-minute cache; four routes with two owners (404 scoping); Cowork row absent from board, `/sessions`, `/events`; restart keeps list; `/board` gone; `Web.Tests` Demo and `ClientRouteSurface` moved |
| AG-67-23 | `Presentation.Tests` view-model transitions for five states (fake `IAccountHost`, `ManualTimeProvider`); `Native.Tests` Avalonia.Headless names, focus order, announce throttling, reduced motion, contrast; `UI.Tests` bUnit + axe in `AccessibilityTests` |

Wave 0 gaps (do not exist today): `tests/Autonogrammer.Accounts.Tests`, `Harness.Secrets.Os.Tests`, `Accounts.Desktop.Tests` (scaffold T2 to T4); `FakeGoTrueHandler`, Kestrel `FakeGoTrueHost`, `FakeJwks`/ES256 `JwtTestHost`; in-memory libsecret fake; per-project `ManualTimeProvider` copies; Presentation fake `IAccountHost`; `FakeBroadcastChannel` for the UI tests; the `Accounts`/`/account*` Demo fixtures; the `ISessionRepository` list methods from phase 68 U3 (needed by unit 4 and 5 tests only).
