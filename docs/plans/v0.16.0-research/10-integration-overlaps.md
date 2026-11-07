# 10. Integration-line overlaps with milestone v0.16.0 (read-only scout, 2026-10-29)

Source: git diff 82129b9d..f272a2f7 of `feat/autonogrammer-integration` (the other line: hosted real web app, phases 66.2 to 66.4, ADR-0021) compared with the plans of phases 67 to 74. Names are post-rename. Facts were read from git only.

Integration line `82129b9d..f272a2f7` has no new projects, packages or `LayerMap.cs` edits, and `ContractVersion` stays 1.5.0. I read git only; nothing was written or built.

**Five highest-risk conflicts, in order**
1. **`/auth/config` and the web sign-in seam (67).**
   - `GET /auth/config` already returns `{mode, authority, audience}`, with `Cache-Control: no-store`, a limiter of 30/min per IP, and the service worker never caching `/auth/*`.
   - Your D-31 wants `{projectUrl, publishableKey, methods, …}` with `public, max-age=300`.
   - `AuthGate` renders mode `oidc` as a dead-end `SignInUnsupported` view, which your plan must replace.
   - Your D-39 deletes `NoApiTokenProvider`, but the integration still uses it for the Demo build.
2. **`Program.cs` composition and pipeline (67, 68, 69, 70, 74).** Commit 5919c452 rewired it, so your line-number references (`Program.cs:205-227` for OTel and so on) are stale.
3. **Web shell (67, 69).** `TopBar`, `WorkbenchView`, `WorkbenchRoute` and `Index.razor` became route-aware, and tests lock the route list.
4. **Hosting hardening versus WebSocket, SSE and Dispatch (74).** Default request timeout, forwarded headers, security headers and drain timing all interact with a long-lived hub.
5. **Engine environment allow-list and approval ceiling (70, 72).** Both are new and sit in the launch path your Cowork profile will use.

**Integration facts you asked for**
- **Auth modes** (24df2714):
  - `Api:Auth:Mode` is `Oidc` (default) or `OwnerToken`. A policy scheme picks the owner-token handler or JwtBearer per request.
  - Owner-token options are `OwnerToken:{Sha256[], Subject="owner", MaxFailuresPerMinute=10}`. Digests are 64 lowercase hex characters. OwnerToken mode forbids `Authority`, `Audience` and `ValidIssuer`. Failures give 401 with `WWW-Authenticate`, or 429 with `Retry-After`.
  - `AllowedSubjects` is a list of `sub` values. Empty means anyone authenticated; `AllowAnyAuthenticatedUser` and OwnerToken mode bypass it. A miss gives 403 `subject_not_allowed`.
  - `sub` is `JwtBearerSetup.SubjectClaimType`, unchanged.
  - Rate-limit partitions are `RateLimitPartitionKey.For` (`sub:` or `ip:`, 128-character cap) for sessions, creation, providers, workspace settings and automations. `ForAddress` is used for auth-config and owner-token failures.
- **Web head** (fda31104):
  - `IApiTokenProvider.GetTokenAsync` feeds `BearerTokenHandler`, which calls `Expire` on a 401.
  - `OwnerTokenSession` implements both `IApiTokenProvider` and `IAuthSession`. It holds the token in memory only, with `Offer`, `Confirm`, `Expire` and `Clear`. `WebServiceRegistration.cs:34-37` registers it.
  - Also added: `IOwnerTokenConnector`, `IAuthConfigSource`, `AuthConfig(Mode, Authority, Audience)` and `AuthRoutes.ConnectPath="connect"`.
- **Engine environment** (feaa54ec):
  - `EngineEnvironmentMode` is `Inherit|Allowlist`. The API default is `Allowlist` outside Development, otherwise `Inherit`.
  - Built-in names: PATH, HOME, USER, LOGNAME, LANG, TERM, TZ, TMPDIR, SHELL, PI_CODING_AGENT_DIR, NODE_EXTRA_CA_CERTS, SSL_CERT_FILE, SSL_CERT_DIR, and the HTTP(S)_PROXY / NO_PROXY variables in upper and lower case. Built-in prefixes are `LC_` and `OMP_`.
  - Extra names come from `Harness:Acp:Environment:Allow`, with a trailing `*` for a prefix. Explicit `EnvironmentVariables` are applied last and win. `AcpClientOptions` and `McpStdioServerOptions` both default to `Inherit`.
- **Isolation** (a8190960):
  - `Harness:Acp:WorkspaceRoot` is required outside Development unless worktrees are configured. `EngineWorkspace` makes a per-session directory.
  - `ProcessHardening` sets `PR_SET_DUMPABLE=0` on the API process (Linux; `Hosting:RestrictProcessInspection`, on outside Development).
  - `AcpHarnessSessionFactory` never sets `McpServers`.
- **Approval policy** (1ba14870, 4315ef6d):
  - `Harness:Acp:Approval:{Kinds[], GrantableTools[], DecisionTimeoutSeconds}`. Kinds are the ACP tool kinds; empty means `DenyAll`. `GrantableTools` is the ceiling, and `"*"` (`SessionService.AnyToolCeiling`) allows any tool. An empty ceiling refuses allow decisions.
  - `IToolApprovalSettings` lives in `SessionRuns`; the default decision window is 30 s and the maximum is 3600 s.
- **Pipeline order:**
  1. `ForwardedHeaders` (only if `Mode≠None`), then HSTS (non-Development).
  2. `SecurityHeadersMiddleware`, then `UseRequestTimeouts`.
  3. `UseExceptionHandler` (non-Development), then `CorrelationId` and `CORS`.
  4. `UseApiAuthentication` (adds the dev boundary), then `RateLimiter`, `Authorization`, and the Cloud and Automation disabled middlewares.
  5. Endpoint maps: `MapHostingEndpoints` (`/health/startup`), `MapAuthConfigEndpoints`, then the existing maps.
- **Hosting options and defaults:**
  - `Hosting:*`: `ForwardedHeaders:Mode` (default `None`), `Hsts`, `SecurityHeaders`, `RequestTimeoutSeconds=30` (504 ProblemDetails), `ShutdownTimeoutSeconds=30`, `Sse:HeartbeatSeconds=15`, `JsonLogs`.
  - Kestrel: `MaxRequestBodyBytes=4 MiB`, `RequestHeadersTimeoutSeconds=30`, `KeepAliveTimeoutSeconds=120`, `MinRequestBodyDataRateBytesPerSecond=240`.
  - Named timeout policy `HostingPolicies.NoTimeout`. `SseStreamResult` uses a bounded channel, writes `: keepalive`, and on `ApplicationStopping` writes `retry: 2000`.
  - `StartupGateHostedService` probes `ISessionRepository.GetAsync("__health__")`. A `shutdown` health check carries the `ready` tag.
- **API headers:** non-HTML responses get CSP `default-src 'none'; frame-ancestors 'none'`, `X-Frame-Options: DENY`, `nosniff`, and `no-store` on authenticated responses. Railway uses `/health/ready` with `drainingSeconds: 35`.
- **Web routes** (9463c2ea): `/`, `/kanban`, `/about`, `/sessions/{id}`, `/settings[/{page}]` (all `Index.razor`), plus `/connect` and `NotFound`.
  - `WorkbenchViewKind` is Agents, Kanban, Session, Settings, About.
  - `TopBar` has a `Tabs` fragment, a Source link, `SignOutButton` and `ThemeSwitch`. Sign-out clears the session and navigates to `connect`.
  - `RouteFocus` focuses the `h1`. The page title comes from `ViewTitles`.
- **Service worker** (3881278d): `service-worker.published.js` caches only the build's static assets plus the shell for navigations. It never touches non-GET, cross-origin, `Authorization`, `Range` or SSE requests, `/auth/*`, or API-prefixed non-navigations. It needs `worker-src` and `manifest-src` of `'self'`. `Web.csproj` gained `ServiceWorkerAssetsManifest` and a `ServiceWorker` item.
- **CSP:** `vercel.ts` and `render-csp.sh` only had the AUTONOGRAMMER_* env renames (`AUTONOGRAMMER_API_ORIGIN`, `AUTONOGRAMMER_DEMO`). The policy is `default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; connect-src 'self' <one API origin>; img-src 'self' data:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'`. There is no `worker-src`, `manifest-src` or `form-action`.
- **Numbering:**
  - ADR-0021 is the only new ADR, so 0022–0029 are free and your 0030–0037 do not collide.
  - New open-question IDs are OQ-169 to OQ-183 (OQ-172 to OQ-183 from the 66.2 work; OQ-169 to OQ-171 arrived with the OmpOS-line merge) and the `Q-BR-1..4`, `Q-DEP-1..3`, `Q-DR-1..4` and `Q-TM-1..6` rows. Your OQ-300 and above are clear.
- **Contracts and registry:** no new `Contracts` types; `AuthConfigResponse` is internal to `Api/Security` and `AuthConfig` lives in `UI/Auth`. No new projects, packages or `LayerMap.cs` edits. Only `Web.csproj` changed, plus rename churn.

**Phase 67 (accounts, GoTrue, JWT, `/auth/config`, `/account`, web sign-in, CSP)**
- `Endpoints/AuthConfigEndpoints.cs` and `Security/AuthConfigResponse.cs` (24df2714) already exist with the shape above. Keep `mode` (the integration runbook asks for this), add your fields to the same record, and replace `no-store` with your `max-age=300`. The service worker excludes `/auth/*` from caching anyway.
- `Options/ApiAuthOptions.cs` and `ApiAuthOptionsValidator.cs` (24df2714) now have `Mode`, `OwnerToken` and `AllowedSubjects`. Add `Api:Auth:Client` as a third block. Validate it in Oidc mode only and forbid it in OwnerToken mode, in line with the existing mutual-exclusion rule.
- Your D-32 conflicts: `RateLimitPartitionKey` partitions unauthenticated callers by IP, not one shared bucket. Keep the IP partition and amend D-32. With `ForwardedHeaders:Mode=None`, all callers behind the edge collapse to one IP, so set the mode in Railway.
- `Security/ApiAuthenticationExtensions.cs` holds `AddApiAuthentication` and `UseApiAuthentication`. Put your enrollment middleware after `UseAuthorization` in `Program.cs`. Your JWKS and ES256 work lands in `JwtBearerSetup` (unchanged) and the validator.
- `AllowedSubjects` is a config allow-list; your accounts-row check (D-02) is separate. Stack both, or retire `AllowedSubjects` for `Oidc`.
- Web (fda31104): give the Supabase session its own `IApiTokenProvider` plus `IAuthSession` implementation, selected in `WebServiceRegistration.cs:34-37` by `/auth/config` mode. Add an oidc branch in `AuthGate.razor`'s `SignInUnsupported` case. Do not delete `NoApiTokenProvider`, because the Demo build needs it. Replace `OwnerTokenSession` only in oidc mode; keep owner-token mode working for the hosted single-operator deployment.
- `SignOutButton` resolves `IAuthSession` optionally and navigates to `/connect`. Reuse that pattern for the account menu.
- Service worker: `/auth/` is never cached, including navigations, so put your callback route outside `/auth/`. Add `/account` to `API_PATH_PREFIXES`.
- `render-csp.sh` and `vercel.ts` only had renames, so your `AUTONOGRAMMER_AUTH_ORIGIN` addition has no conflict. Add the Supabase origin to `connect-src` only.
- `scripts/web-smoke/size-budget.mjs` (9f7e76d8) is a payload budget gate; auth JS and account components will count against it.

**Phase 68 (data plane, telemetry, resilience, migrations, `DragonflyKeyspace`)**
- OTel moved out of `Program.cs` to `Hosting/ApiTelemetryExtensions.cs` (0d31942d). Its resource is service `autonogrammer-api` and source `Autonogrammer.Api` (matches D-05). Extend `AddApiTelemetry` rather than adding a second `AddOpenTelemetry`.
- `StartupGateHostedService` calls `ISessionRepository.GetAsync("__health__")`. If your resilience decorator or circuit breaker wraps the repository, a tripped breaker will hold the startup gate closed.
- Readiness: Railway's `healthcheckPath` is `/health/ready` (`deploy/railway/api.railway.json`), and `/health/startup` is new. This contradicts your D-43 ("`/health/live` is Railway's path"). Choose one and edit the other.
- `Program.cs` now ends with `app.Start()` and `WaitForShutdown()`. Your `migrate` verb must branch before `builder.Build()`.
- `DragonflyKeys.cs` and `Sessions.Postgres` are untouched; the `autonogrammer:` prefix is already in the rename. `deploy/compose.yaml` and `api.Containerfile` were edited (a9e? 98812c3f, 087c39e4) with `autonogrammer-*` names, digest-pinned images and Railway README variable rows. Reconcile `deploy/data-plane/` and its guard tests with those.
- Open questions `Q-HOST-GREPTIME-AUTH` and `Q-HOST-DFLY-PIN` already exist from the OmpOS line.

**Phases 69 and 70 (SurfaceHost, Cowork sessions, tool host, launch profile, approvals)**
- `TopBar.razor` (9463c2ea) has a `Tabs` fragment, a Source link, `SignOutButton` and `ThemeSwitch`. Your `Surface` fragment goes before `Tabs`; your account menu (`HeaderEndSlot`) replaces `SignOutButton`.
- `WorkbenchView.razor` now branches on `IsAbout`, `_sessionMissing` and `IsResolvingSession`, and renders a single `h1` per view. Your planned "replace the view-panel body" needs a `Cowork` `WorkbenchViewKind`, a `WorkbenchRoute` segment `cowork`, and a `[Page]` line in `Index.razor`. `IndexRoutesTests.ThePageDeclaresEveryWorkbenchAddress` asserts the exact route list. `scripts/web-smoke/a11y/routes.mjs` has a direct-route list that needs the same update.
- `Program.cs` registers `SessionRegistry` and the run services as before. Your D-38 migration of those callers is untouched by the integration.
- `AcpHarnessSessionFactory` builds `AcpClientOptions` with `EnvironmentMode` and `EnvironmentAllowList` and never sets `McpServers`. For Cowork, set `EnvironmentMode=Allowlist` explicitly (the library default is `Inherit`) and pass provider keys through `EnvironmentVariables`. Your D-06 keys-only intent already works with that mechanism.
- `McpStdioServerOptions` gained `EnvironmentMode` and `EnvironmentAllowList`. Servers the tool host launches should set these too. `Harness.McpGateway` has no behavioural change (`McpGatewayToolNames.Compose` kept).
- The `Harness:Acp:Approval` ceiling and `IToolApprovalSettings` serve the API's routed run. Your Cowork approval queue is local, so keep the two separate and avoid reusing the `Harness:Acp:Approval` keys.

**Phase 72 (Browser Use, process spawning)**
- The only `ProcessStartInfo` in `src` is still `AcpStdioTransport.StartProcess`, and `Directory.Build.props:52`, `BannedSymbols.NoProcess.txt` and `LayerMap.cs` are unchanged. Your `Harness.BrowserUse` plan is unaffected.
- `EngineEnvironmentPolicy` is public in `Harness.Acp`, but your L2 rule forbids that reference. Keep your own Chromium allow-list. It differs deliberately: your D-07 drops `OMP_*` and proxy variables, which the engine policy keeps.
- `ProcessHardening` applies only to the API process. A host-launched desktop Chromium is unaffected.

**Phase 73 (Computer Use)**
- No overlap beyond the shared approval and environment facts above. `ToolApprovalEndpoints` and the ceiling rule are unchanged apart from the wildcard.

**Phase 74 (Dispatch relay)**
- No `UseWebSockets` exists in `services/Autonogrammer.Api` yet (the `AgentWorker` service has its own). Add it after `UseHostingHardening` and before authentication.
- Exempt `/dispatch/devices/{id}/connect` and the controller SSE route from the default 30 s timeout with `.WithRequestTimeout(HostingPolicies.NoTimeout)`. Build the SSE feed on `SseStreamResult`, which gives keep-alive and the `retry: 2000` frame on stop.
- Drain: `ShutdownTimeoutSeconds=30` is below Railway's `drainingSeconds=35`. Your hub's close code 1001 must complete inside 30 s.
- The `DispatchDevice` credential has no `sub` claim. `RateLimitPartitionKey.For` would fall back to IP for it, so use a device-keyed partition.
- `SecurityHeadersMiddleware` adds `X-Frame-Options: DENY` and a CSP to every response. That is harmless to the upgrade response, but test it.
- Dispatch's own `Dispatch:*` options sit next to the new `Hosting:*` section and do not collide.
- Pairing freshness is unaffected, since JwtBearer is untouched.