# R3. API, auth and backend map (v0.16.0, Autonogrammer)

Scope: `services/OmpOS.Api`, `services/OmpOS.AgentWorker`, `deploy/`, `.env.example`, the Native and Web API clients, secrets storage, the OpenAPI contract, the Railway topology in `docs/handoff.md`. Read-only recon of the worktree `/root/src/repos/ompos-autonogrammer` (branch `feat/autonogrammer`). Nothing was built or run. Paths are repo-relative. `[INFERENCE]` marks anything not read from a file. `[EXTERNAL]` marks a fact from a web search, not from this repo.

## 1. Verdict on "no authentication module exists yet"

**Refuted in substance.** An OIDC bearer module exists, is wired, fail-closed, and tested. What does not exist is a sign-in *client* (no token source in either head), an identity provider configured for any deployment, an account/user concept beyond the JWT `sub`, per-user rate limits, and any security metadata in the OpenAPI document.

Evidence:

| Fact | Where |
|---|---|
| JwtBearer package referenced | `services/OmpOS.Api/OmpOS.Api.csproj:61`; central version 10.0.13 at `Directory.Packages.props:32` |
| Options `Api:Auth` = `Authority`, `Audience`, `ValidIssuer`, `AllowDevelopmentPrincipal` | `services/OmpOS.Api/Options/ApiAuthOptions.cs:10-25` |
| Fail-closed validator: outside Development Authority and Audience required, Authority must be absolute https, dev principal refused | `services/OmpOS.Api/Options/ApiAuthOptionsValidator.cs:27-45` |
| Scheme registration, validator, fallback policy `RequireAuthenticatedUser` | `services/OmpOS.Api/Program.cs:80-89` |
| `JwtBearerSetup` (post-configure): Authority, `MapInboundClaims=false`, `ValidAudience`, optional `ValidIssuer`, rejects a token without `sub` | `services/OmpOS.Api/Security/JwtBearerSetup.cs:27-45` |
| `IUserContext` = `sub` claim (`AuthenticatedUserContext`) or `SingleDevPrincipal` (`local-dev`) behind the opt-in | `Program.cs:148-159`; `Security/AuthenticatedUserContext.cs:20-22`; `Security/SingleDevPrincipal.cs:11` |
| Dev-only middleware that injects `local-dev` for header-less requests, registered only if the opt-in is on | `Program.cs:243-247`; `Middleware/AuthBoundaryMiddleware.cs:29-39` |
| Anonymous routes: `/health/live`, `/health/ready`, `GET /source`, webhook `POST /hooks/automations/{id}` (HMAC-authenticated); Scalar and `/openapi/v1.json` anonymous only in Development | `Program.cs:254-275`; `Endpoints/SourceEndpoints.cs:19`; `Automations/AutomationEndpoints.cs:119-122` |
| Tests: 401 without token, wrong audience, untrusted key, valid token makes `sub` the session owner, health anonymous; Production startup failures name the key | `tests/OmpOS.Api.IntegrationTests/ApiAuthBoundaryTests.cs`, `ApiAuthStartupTests.cs`, `AuthBoundaryTests.cs`, helper `JwtTestHost.cs` (static HMAC key and static OIDC metadata, no network) |
| Changelog line "An OIDC bearer boundary for the API." | `CHANGELOG.md:215` |

### OD-03 text, as recorded (and where it is stale)

- `docs/open-questions.md:52`: "What is the sign-in method for the API? ... Decided autonomously 2026-10-21: OIDC bearer tokens validated by JwtBearer against Api:Auth:Authority and Api:Auth:Audience; fail-closed in production; no public domain until an identity provider is configured. | owner (decided autonomously) | decided; implementation in Phase 12". Narrative at `docs/open-questions.md:448`.
- `docs/handoff.md:144` (owner decision 4), `:176`, `:194` (deploy blocker 1), `:233`: implemented and fail-closed; the only open item is an identity-provider authority for the deployed API.
- `.planning/ROADMAP.md:286`: "Decided 2026-10-21 (OIDC bearer, fail-closed). No identity provider authority is configured for the deployed API | Configure Api:Auth:Authority and Api:Auth:Audience ... Owner".
- `.planning/REQUIREMENTS.md:73` (OD-03 box still unchecked) still says "single dev principal ... is replaced by an authentication provider using the sign-in method the owner selects", and `:13`/`:93` say "owner decision on the sign-in method (OD-03)... not recorded in any source for this milestone". This contradicts the decided state above.
- `docs/open-questions.md:32` (OQ-26) still says "Single dev principal with reserved OwnerId; no-op provider": stale; `.planning/STATE.md:375` records the actual behaviour ("Callers are the JWT subject. local-dev is a Development-only principal").
- Stale code comment: `Security/IUserContext.cs:4-6` ("Phase 2 binds a fixed single-developer principal; replace the registration").
- Stale runbook line: `docs/runbooks/hosting.md:45` says "the API has no authentication (single local dev principal)". Contradicted by `hosting.md:162` and the code.
- Line citations in `docs/handoff.md:144,194` (`Program.cs:73-76`, `:214`) are out of date; the code is now at `Program.cs:80-89`, `:148-159`.
- `ApiAuthOptionsValidator` applies the https rule to `Authority` only; `ValidIssuer` has no validation.

**Consequence for the plan:** OD-03 is not an "owner decision to be made" in the code sense; the existing decision (OIDC bearer) already fits Supabase. The Autonogrammer decision is "which IdP authority", plus the sign-in client work, plus doc and requirement reconciliation.

## 2. Program.cs composition (order matters)

Registration (`services/OmpOS.Api/Program.cs`): fail-fast options (`:47-78`) -> auth (`:80-89`) -> JSON source-gen chain `ApiJsonContext` + `OmpOsJsonContext` (`:92-96`) -> session store `AddApiSessionStore` (`:100`) -> coordination `AddApiCoordination` (`:101`) -> `SessionService`, parser, registries (`:102-105`) -> harness factory by `Harness:Adapter` (`:106-120`) -> `AddSessionWebServices`, `AddCloudAgentServices`, `AddProviderServices`, `AddWorkspaceSettingsServices`, `AddAutomationServices` (`:122-126`) -> run workflow (`DirectSessionRunner`, `FakeSandboxProvider`) and hosted services (`:130-146`) -> `IUserContext` (`:148-159`) -> CORS (`:164-165`) -> ProblemDetails (`:168-181`) -> rate limiter (`:185-195`) -> health checks (`:197-200`) -> OpenAPI (`:202`) -> OTel (`:204-229`).

Pipeline (`:233-290`): `UseExceptionHandler` (non-Development) -> `CorrelationIdMiddleware` -> `UseCors("OmpOS")` -> `UseAuthentication` -> [`AuthBoundaryMiddleware`, dev only] -> `UseRateLimiter` -> `UseAuthorization` -> `CloudDisabledMiddleware` -> `AutomationDisabledMiddleware` -> maps. The rate limiter runs after authentication, so `HttpContext.User` is available to a per-user partition key.

Module pattern to copy (Cloud and Automations): options class + `IValidateOptions` + `ValidateOnStart`; `AddXxxServices()` extension; `MapXxxEndpoints()` with `MapGroup`, `WithTags`, `Produces*`; a disabled-middleware gated by `Xxx:Enabled`; a JSON context appended to the HTTP JSON chain (`Automations/AutomationServiceRegistration.cs`); Postgres store chosen at resolution time by `Sessions:Store` (`Cloud/CloudServiceRegistration.cs:40-44`) with idempotent DDL (`src/OmpOS.Sessions.Postgres/PostgresCloudAgentSchema.cs`, started by `Cloud/CloudAgentStoreStartup.cs:36`).

## 3. Endpoint list (60 paths in `docs/api/openapi.json`, OpenAPI 3.1.1)

All routes require an authenticated user (fallback policy) except those noted in section 1. "Owner" = `sub` claim = `Session.OwnerId`.

| Group | Routes | Owner scoping / notes |
|---|---|---|
| Sessions | `GET,POST /sessions`, `GET /sessions/{id}`, `POST /sessions/{id}/prompts` (Idempotency-Key, rate limited), `GET /sessions/{id}/events` (SSE), `POST /sessions/{id}/tool-approvals/{requestId}`, `GET /sessions/{id}/tool-approvals` | `FindOwnedSessionAsync` compares `session.OwnerId == user.Name`; another owner's session is the same 404 as an absent one (`Endpoints/SessionEndpoints.cs:417-431`). A request body naming `OwnerId` is 400 (`:103-106`). |
| Runs | `POST /sessions/{id}/runs`, `GET .../runs/{runId}`, `.../output`, `POST .../cancel` | `Endpoints/RunEndpoints.cs`; owner-scoped (`CloudRunOwnerScopeTests`). |
| Catalog and Kanban | `GET /board`, `GET /sessions/{id}/card-details`, `POST .../close|reopen|resume`, `GET /workspace`, `GET /events` (user-level SSE) | `Endpoints/CatalogEndpoints.cs:29-57`; `LoadOwnedAsync` filters by owner (`:271-285`). |
| Per-session controls | `.../stages`, `.../context`, `.../steer`, `.../config-options[/{optionId}]`, `.../commands`, `.../changes[/{path}/accept|reject]` | `StageEndpoints`, `SteeringEndpoints`, `SessionConfigEndpoints`, `ChangeReviewEndpoints`; owner-scoped. |
| Workspace and settings (server filesystem) | `GET /workspace/conventions`, `/workspace/tree`, `/workspace/file`, `/settings/runtime`, `/settings/plugins`, `/settings/network`, `/hooks`, `/automations` | **Not user-scoped**: `WorkspaceAccess.Permits` returns true for every authenticated caller when `Api:Workspace:Owners` is empty (`Services/WorkspaceSettings/WorkspaceAccess.cs:58-63`; default `[]`, `WorkspaceSettingsOptions.cs:27`). They read the one server workspace. Multi-user risk, see section 11. |
| Providers | `GET /providers[/{id}]`, `PUT /providers/{id}/credential`, `PUT /models/default`, `POST /providers/custom`, `DELETE /providers/custom/{id}`, `POST /providers/{id}/test` | Gated by `Providers:AllowCredentialWrite`, `AllowConfigWrite`, `AllowLiveTest`, all default false (`Services/Providers/ProvidersOptions.cs:15-21`). Process-global omp config, not per user (OQ-104, `docs/open-questions.md:141`). |
| Cloud agents (`Cloud:Enabled`, default false) | `GET,POST /cloud/agents`, `GET,DELETE /cloud/agents/{id}`, `.../prompts|cancel|stop|wake|events|pull-request`, `GET /cloud/secrets`, `GET,PUT,DELETE /cloud/secrets/{name}`, `GET /cloud/limits` | Owner-scoped, per-owner caps (`Cloud/Options/CloudOptions.cs:112-128`). `CloudAgentEndpoints.cs:41-157`. |
| Automations (`Automations:Enabled`, default false) | `GET,POST /automations`... `/automations/definitions|cron-preview|{id}|{id}/enabled|{id}/webhook-secret|{id}/runs`, `POST /hooks/automations/{id}` | Management routes owner-scoped; receiver anonymous + HMAC. |
| Ops | `/health/live`, `/health/ready` (not in OpenAPI), `GET /source` (AGPL s13), `/openapi/v1.json`, Scalar | see section 1. |

Missing today for Cowork: no `/me` or account route, no device route, no relay route, no artifact route, no browser-use or computer-use route. `rg` of `services/OmpOS.Api` for `SignalR|MapHub|UseWebSockets` found nothing (the API is a WebSocket *client* of workers, `Cloud/CloudAgentHarnessSession.cs:220-227`, `AcpWebSocketTransport`).

## 4. Session and Kanban storage

- `ISessionRepository` selected at resolution time by `Sessions:Store` (`Postgres` | `InMemory`): `Persistence/SessionStoreRegistration.cs:27-40`. Production refuses `InMemory` (`Persistence/SessionStoreStartup.cs:42-45`; handoff `:210`, `:158`).
- Postgres schema (`src/OmpOS.Sessions.Postgres/PostgresSessionSchema.cs:37-79`): schema `ompos`; `sessions(id text PK, owner_id text NOT NULL, created_at, updated_at, status, sandbox_id, messages jsonb)` with index `(status, updated_at)`; `session_events(session_id FK, seq bigint, correlation_id, occurred_at, payload jsonb, PK(session_id, seq, occurred_at))` as a TimescaleDB hypertable on `occurred_at`, 1-day chunks; `session_tool_approvals`. Seq is assigned under a session-row lock. There is no index on `owner_id` and no "list by owner" method (see below). `ISessionRepository` has `ListSessionIdsByStatusAsync` only (`src/OmpOS.Sessions/ISessionRepository.cs`, per the schema remarks).
- **Kanban board is derived, not stored.** `GET /board` builds columns from the caller's sessions via `CatalogProjector` (`Endpoints/CatalogEndpoints.cs:79-112`). The id set is `SessionRegistry`, an in-process `ConcurrentDictionary` filled by `POST /sessions` (`Services/SessionRegistry.cs:9-22`), and the "closed" flag is `SessionLifecycleStore`, also in-process (`Services/SessionLifecycleStore.cs:9-38`). **Both are lost on restart and not shared between replicas even with Postgres**: after a restart `GET /sessions` and `/board` list nothing for sessions that exist in Postgres. Direct `GET /sessions/{id}` still works. [INFERENCE: this is a pre-existing gap that accounts and Dispatch would expose immediately; fix is a Postgres `ListByOwner` plus a persisted closed flag.]
- Cloud agents: `ICloudAgentStore` = `PostgresCloudAgentStore` when `Sessions:Store=Postgres`, else `InMemoryCloudAgentStore` (`Cloud/CloudServiceRegistration.cs:40-44`). Table `cloud_agents` (`PostgresCloudAgentSchema.cs`). **Cloud secrets are in memory only** (`Cloud/Secrets/InMemoryCloudSecretStore.cs`, registered at `CloudServiceRegistration.cs:49-50`), lost on restart.
- `CatalogEventHub` (user-level SSE history) is in-process, 1024-event ring, "another process" positions get one `resync` (`Services/CatalogEventHub.cs:7-15`). Multi-replica fan-out of `/events` is therefore not solved; per-session SSE is solved by DB polling (~200 ms) plus best-effort wake-up signals (handoff `:213`).
- Coordination (`Coordination:Store` Dragonfly|InMemory): run leases, idempotency, session signals. `ISessionSignals` carries a **session id only, no payload** (`src/OmpOS.Coordination/ISessionSignals.cs:7-18`), so it is not a message bus for a relay.

## 5. SSE and Last-Event-ID

- `GET /sessions/{id}/events` (`Endpoints/EventStreamEndpoints.cs:23-97`): `Last-Event-ID` header wins over `?since=`; both non-negative base-10 integers, else 400; `id:` = Seq, `event:` = payload kind, `retry` 15 s; ends on terminal `completed`/`error`.
- `GET /events` (`Endpoints/CatalogEventEndpoints.cs:20-71`): never ends, owner-filtered, `resync` when position unknown.
- Cloud: `GET /cloud/agents/{id}/events` same rules (`Cloud/CloudAgentEndpoints.cs:107-112`).
- Clients send `Authorization: Bearer` on SSE because they use `HttpClient` streaming, not the browser `EventSource` (`apps/OmpOS.Web/Services/SseEventStream.cs:12-20,128-135,268-279`). Any new stream must keep that (an `EventSource` cannot set headers).

## 6. Rate limits and CORS

- Policies: `SessionPrompts` (`Api:RateLimit:PromptsPerMinute`, 60) and `SessionCreation` (`SessionsPerMinute`, 60), plus `Providers:RequestsPerMinute` (60), `Api:Workspace:RequestsPerMinute` (120), automation policies. 429 with Retry-After (`Security/RateLimitRejection.cs`).
- **Every policy is one global token bucket** (constant partition keys `"prompts"`, `"sessions"`, `"providers"`, `"automations"`, `"automation-webhooks"`): `Security/SessionRateLimiterPolicy.cs:33-48`, `SessionCreationRateLimiterPolicy.cs:31-38`, `Services/Providers/ProviderRateLimiterPolicy.cs:32-36`, `Automations/AutomationRateLimits.cs:12-13`. The class remark at `SessionRateLimiterPolicy.cs:8-12` says "A per-session fairness upgrade is blocked on real auth", and `Options/ApiRateLimitOptions.cs:10` says "per session" while the code is global. With accounts, one user can exhaust the budget of all. Seam: change `GetPartition` to key on `httpContext.User` `sub` (auth runs before the limiter).
- CORS (`Security/ConfigureCorsOptions.cs:27-44`): policy `OmpOS`; no origins => `WithMethods("GET","POST")` only (so cross-origin PUT/DELETE denied); with origins `AllowAnyHeader().AllowAnyMethod()`. No `AllowCredentials` (bearer in header does not need it). `Api:Cors:Origins` default `[]` (`appsettings.json:3-5`). Preflight test `tests/OmpOS.Api.IntegrationTests/ApiCorsPreflightTests.cs`.
- Web CSP: `vercel.ts:16-25` builds `connect-src 'self' <one origin>` from `OMPOS_API_ORIGIN` through `scripts/vercel/render-csp.sh:6-62`, which accepts **exactly one** https origin. A browser sign-in that talks to Supabase directly (token exchange, refresh) needs a second `connect-src` origin; the script must be extended, and `frame-ancestors 'none'` is already set.

## 7. OpenAPI contract and tests

- Generated at build into `docs/api/openapi.json` (`OmpOS.Api.csproj:8-10`, `OpenApiGenerateDocumentsOnBuild`, `--openapi-version OpenApi3_1`), served at `/openapi/v1.json` (`OpenApi/OpenApiConfig.cs:13-23`, transformers `FixedServersTransformer`, `IdempotencyKeyOperationTransformer`).
- Contract tests: `tests/OmpOS.Api.IntegrationTests/GuardTests.cs:58-74` (served document DeepEquals the committed file; any route change requires regenerating and committing it); `tests/OmpOS.Web.Tests/OpenApiCoverageTests.cs` and `Settings/SettingsRouteSurfaceTests.cs` (every route the web client or Demo calls is documented); `CloudRunOpenApiTests`, `WorkspaceSettingsOpenApiTests`, `ProvidersHostContractTests`, `Cloud/CloudDisabledTests`; `tests/OmpOS.ArchitectureTests/DeferredScopeTests.cs:13-18,86-90` scans code and `openapi.json` for the needles `ADE-21`, `ADE-22` and `vscode` (case-insensitive, `.vscode` folder excepted): a new module must not contain these strings.
- **Spec gap:** the committed document has `"security": null` and no `components.securitySchemes`; zero operations declare security, and only one operation (webhook receiver) documents a 401 (queried from `docs/api/openapi.json`). The ADE-07 change request expected "no run path has `security: []`" (`docs/api/ADE-07-run-contract-change-request.md:107`). A bearer scheme and a 401/403 response should be added through a document transformer (the same mechanism as `IdempotencyKeyOperationTransformer`); this changes the committed file and the Web contract tests.
- Demo build: `apps/OmpOS.Web/Demo/DemoApiHandler.cs` answers every route from fixtures; new account/Dispatch routes used by the web head need Demo fixtures and `DemoFixturesContractTests`.

## 8. services/OmpOS.AgentWorker, deploy, env names

AgentWorker (layer 3, own host): JwtBearer fallback policy; verifies ES256/RS256 tokens signed by the API (`WorkerApp.cs:32-57`, `Configuration/WorkerAuthOptions.cs`, `Security/WorkerJwtValidator`), serves only `GET /acp` (WebSocket) and health (`Bridge/AcpBridgeEndpoint.cs:19-37`); one connection per session (409 otherwise); no outbound traffic, no DB, no telemetry (Railway Serverless sleep rule, `WorkerApp.cs:12-16`). Issuer on the API side: `Cloud/Workers/WorkerTokenIssuer.cs:9-14`: ES256, `aud=ompos-worker`, `sub`=session id, `exp` <= 15 min, key from `Cloud:Worker:SigningKeyPem`. **This is the existing precedent for a long-lived-connection relay**: API-signed short token + WebSocket upgrade + one-attachment rule. Public-domain use is blocked until OD-03 passes (`docs/decisions/ADR-0020-cloud-agents-on-railway.md:62,99`).

`deploy/`:
- `api.Containerfile`: SDK `10.0.402-noble`, runtime `aspnet:10.0.13-noble`; copies only `global.json`, props, `src/`, `services/OmpOS.Api/`, `docs/api/` (`:20-24`); `dotnet restore --locked-mode` (`:27`), so **new NuGet packages need updated `packages.lock.json`** and a new project outside `src/` or `services/OmpOS.Api/` needs a `COPY` line; ships Bun 1.4.3 + omp 18.12.5 (`:42-89`); uid 10001; binds `[::]:${PORT:-8080}` (`:120`); `ASPNETCORE_ENVIRONMENT=Production` (`:110`).
- `compose.yaml`: local profile `backend`: TimescaleDB 2.13.1-pg16, Dragonfly v1.27.1, GreptimeDB v1.1.4, all bound to 127.0.0.1, passwords required from env; the API itself is not in compose. `worker.Containerfile`, `worker-entrypoint.sh` for the worker.

`.env.example` variable NAMES (values never recorded): `OMPOS_ENV`, `Harness__Adapter`, `Api__Cors__Origins__0`, `Api__MaxPromptLength`, `Api__MaxRequestBodyBytes`, `Api__RateLimit__PromptsPerMinute`, `Otel__Exporter`, `OmpOS__Debug__LogPrompts`, `Api__Auth__Authority`, `Api__Auth__Audience`, `Api__Auth__ValidIssuer`, `Api__Auth__AllowDevelopmentPrincipal`, `Sessions__Store`, `Sessions__Postgres__ConnectionString`, `Coordination__Store`, `Coordination__Dragonfly__ConnectionString`, `PORT`, `OMPOS_POSTGRES_USER`, `OMPOS_POSTGRES_DB`, `OMPOS_POSTGRES_PASSWORD`, `OMPOS_DRAGONFLY_PASSWORD`, `Sandbox__Provider`, `Harness__Acp__OmpPath`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_EXPORTER_OTLP_HEADERS`, `OMPOS_WORKER_JWT_PUBLIC_KEY_PEM`, `OMPOS_WORKER_JWT_ISSUER`, `OMPOS_WORKER_PORT`, `Worker__Auth__PublicKeysPem__0`, `Worker__Auth__Issuer`, `Worker__Auth__MaxTokenLifetime`, `Worker__SessionId`, `Worker__WorkspaceRoot`, `Worker__PingInterval`, `Worker__Omp__Path`, `Worker__Omp__ExtraArguments__0`, `OMPOS_VOLUME_PATH`, `OMPOS_RUN_UID`, `OMPOS_RUN_GID`, `RAILWAY_RUN_UID`. Gaps: no names for `Cloud__*`, `Automations__*`, `Providers__*`, `Api__Workspace__*` (they exist in code, section 3); `docs/runbooks/hosting.md:96-111` env table omits the `Api__Auth__*` names (they appear only in deploy checklist item 2 `hosting.md:174`).

## 9. Railway topology (`docs/handoff.md:200-237`, `docs/runbooks/hosting.md:25-48`)

Project `Ultrathink`, environment `production`, private networking `SERVICE.railway.internal`. Existing: TimescaleDB (`timescale-postgis-ssl:pg16-ts2.13`, session log schema `ompos`), Dragonfly (unpinned `latest`, Q-HOST-DFLY-PIN), GreptimeDB `v1.1.4` (OTLP, auth on), plain Postgres (kept, Q-PG-RETIRE). **`ompos-api` is "Planned, not created"; nothing of OmpOS is deployed** (`hosting.md:15`, `:166`; OQ-100 `open-questions.md:137`). Temporal and any worker service removed (handoff `:206`); session runs execute in the API process via `DirectSessionRunner`. No public domain for the API until the IdP exists (handoff `:207`); Vercel hosts the web head (Demo at `https://ompos-web.vercel.app`, `hosting.md:41`). Railway deploys are recorded as paused (`hosting.md:166`, `docs/runbooks/deferred-and-blocked-2026-10-26.md:37-39`); acceptable-use of remote desktops is unresolved (OQ-73, `open-questions.md:107`). These two facts gate Computer Use / Browser Use streaming and Dispatch on Railway.

## 10. How the heads call the API

**Web head (Blazor WASM)**: one scoped `ApiHttp` over `HttpClient(BearerTokenHandler(IApiTokenProvider, inner))` (`apps/OmpOS.Web/Services/WebServiceRegistration.cs:32-37`). `IApiTokenProvider.GetTokenAsync` is the declared sign-in seam (`Services/IApiTokenProvider.cs:3-14`: "Sign-in (OD-03) replaces this registration"); default `NoApiTokenProvider` returns null (`Services/NoApiTokenProvider.cs`), registered with `TryAddScoped` so a prior registration wins. `BearerTokenHandler` sets `Authorization: Bearer` when a token exists (`Services/BearerTokenHandler.cs:21-31`). Typed client `IOmpApiClient`/`OmpApiClient`, `ApiSessionClient`; SSE via `SseEventStream` + `SseParser` with Last-Event-ID resume and exponential backoff; 401 maps to `ApiProblemKind.Unauthorized` (`ApiProblemKind.cs:9-10`) but there is no re-auth flow. API origin from `wwwroot/appsettings.json` `Api:BaseUrl` (default loopback), override only for loopback hosts (`Program.cs:11-18`, `ApiBaseUrlResolver`). No auth package in `OmpOS.Web.csproj`; only localStorage use is theme/motion (`Services/Settings/WebGeneralSettingsSource.cs`). Token must never be logged or persisted per the interface contract.

**Native head (Avalonia desktop)**: the engine is local/in-process (`InProcessEngineHost`, `ReconnectingEngineHost`). `RemoteEngineHost` (SSE client of the API, `src/OmpOS.EngineHost/RemoteEngineHost.cs:26-85`, takes a pre-configured `HttpClient`) is **not constructed anywhere in `apps/`** (rg found no `new RemoteEngineHost`). The only API consumer is the Cloud page: `ApiCloudAgentsHost` polls `/cloud/agents` every 15 s with `Func<CancellationToken, ValueTask<string?>> bearerToken` (`apps/OmpOS.Native/Cloud/ApiCloudAgentsHost.cs:41,58-69,107,367-371`). The head reads `OMPOS_API_BASE_URL` and the token from the env var `OMPOS_API_TOKEN` per request (`apps/OmpOS.Native/Composition/HeadServices.cs:43-47,439-453`). So the Native seam is a `Func` delegate fed by an env var; there is no sign-in UI. Android head (`apps/OmpOS.Android`) has no API code.

**Native secret storage today**: `src/OmpOS.Harness.Secrets` has `ISecretStore` (`SetAsync/GetAsync/RemoveAsync/ListNamesAsync`, `SecretString` that never serialises), `InMemorySessionSecretStore`, and `OwnerOnlyFileSecretStore` (plain-text JSON, dir 0700 and file 0600 on Unix, write-temp-then-rename, **no ACL hardening on Windows**, no cross-process lock): `OwnerOnlyFileSecretStore.cs:6-24,158-161,273-276,339-342`. **No OS credential store implementation exists** (Windows Credential Manager, macOS Keychain, Linux Secret Service); the interface remarks say one is added "once it can be verified on a runner for that platform" (`ISecretStore.cs:18-21`). The Native head composes `OwnerOnlyFileSecretStore` under `<StateRoot>/secrets/` for provider keys (`apps/OmpOS.Native/Settings/Models/ModelsSettingsComposition.cs:39-41`). Refresh tokens for an account would land in that plain-text file unless an OS-backed `ISecretStore` is built (design decision, section 12).

## 11. Seams for Supabase JWT validation, account, device registry, relay hub

### 11.1 Supabase JWT validation (ES256 JWKS, audience `authenticated`)

Facts from the repo: the existing module already does asymmetric validation via OIDC discovery from `Api:Auth:Authority`; the `Audience` option maps to `ValidAudience`; `sub` becomes the owner; `MapInboundClaims=false` keeps the raw `sub`, `aud`, `role`, `email` names.
[EXTERNAL] Supabase publishes JWKS at `https://<ref>.supabase.co/auth/v1/.well-known/jwks.json`, OIDC discovery at `.../auth/v1/.well-known/openid-configuration`, issuer `https://<ref>.supabase.co/auth/v1`, access-token `aud` = `authenticated`, alg ES256 for projects on asymmetric signing keys; HS256 legacy projects have an empty JWKS; JWKS is edge-cached about 10 minutes (source: Supabase docs and a GitHub issue, retrieved by web search; not verified against a live project).
[INFERENCE] Setting `Api__Auth__Authority=https://<ref>.supabase.co/auth/v1`, `Api__Auth__Audience=authenticated`, `Api__Auth__ValidIssuer=<same>` should work with **zero code change** because JwtBearer appends `/.well-known/openid-configuration`. Needs a live smoke against the real project; not run here. What does need code:
- Pin accepted algorithms to ES256 (set `TokenValidationParameters.ValidAlgorithms`) and require signed tokens (`RequireSignedTokens`); the current setup does not restrict algorithms.
- `aud=authenticated` is shared by every user token of that Supabase project, so audience alone does not scope tokens to this API; consider an issuer check (already supported by `ValidIssuer`), a custom claim, or an `is_anonymous`/`role` check. [INFERENCE]
- Reject anonymous-sign-in tokens if desired (claim name `is_anonymous` per Supabase docs, not verified here).
- Account-existence and revocation: JWTs stay valid until `exp`; a deleted or banned user keeps access for the token lifetime unless the API checks a store.
- `sub` is a UUID string; existing `local-dev` sessions are not migrated (owner_id stays text, no change needed).

Files this touches: `services/OmpOS.Api/Security/JwtBearerSetup.cs`, `Options/ApiAuthOptions.cs` + `ApiAuthOptionsValidator.cs` (validate `ValidIssuer`, add optional algorithm/claim options), `.env.example`, `docs/runbooks/hosting.md` (env table + `:45` fix), `tests/OmpOS.Api.IntegrationTests/JwtTestHost.cs` (switch the test signer to an ES256 key and a JWKS-shaped static configuration), `ApiAuthBoundaryTests.cs`/`ApiAuthStartupTests.cs`.

### 11.2 Account endpoint (`/me`, profile, sign-out/delete)

New: `services/OmpOS.Api/Accounts/` (module: `AccountEndpoints.cs`, `AccountService`, options), registered in `Program.cs` (new `AddAccountServices()` after `:126` and `MapAccountEndpoints()` after `:290`); contracts in `src/OmpOS.Contracts/Accounts/` with JSON context entries; optional Postgres table `accounts` in a new `PostgresAccountSchema` (pattern `PostgresCloudAgentSchema.cs`, started like `CloudAgentStoreStartup`); `IUserContext` may grow `Email`/`DisplayName` (touches `Security/IUserContext.cs`, `AuthenticatedUserContext.cs`, `SingleDevPrincipal.cs`, `Program.cs:148-159`, `tests/OmpOS.Api.IntegrationTests/AuthBoundaryTests.cs`). Plus `docs/api/openapi.json` (regenerated, `GuardTests` equality), `OpenApiCoverageTests`, Demo fixtures (`apps/OmpOS.Web/Demo/*`), `apps/OmpOS.Web/Services/IOmpApiClient.cs` + `OmpApiClient.cs`, Native client.

### 11.3 Device registry (desktops that Dispatch controls)

New table `devices(id, owner_id, name, platform, public_key, last_seen, revoked_at)` and endpoints (`POST /devices` register, `GET /devices`, `DELETE /devices/{id}`). Owner scoping pattern = `FindOwnedSessionAsync`. Device identity: the machine authenticates with the user's Supabase token at pairing and then holds a device credential (API-signed token modelled on `WorkerTokenIssuer`, ES256, short life); [INFERENCE] needs its own signing key variable (new `Dispatch__...` names, none exist). OQ-68 (`open-questions.md:105`) is the open pairing decision ("no pairing or remote code exists in src, services or apps"). Files: new `Devices/` module as above, `src/OmpOS.Sessions.Postgres` (new schema file, `ServiceCollectionExtensions.cs`), `Cloud/Workers/WorkerTokenIssuer.cs` as template, Native head (new pairing UI under `apps/OmpOS.Native`, `src/OmpOS.Native.Views`, `src/OmpOS.Presentation`), `.env.example`.

### 11.4 Relay hub (mobile/web -> user's desktop)

Today nothing relays. Reusable: per-session SSE with Last-Event-ID (server to client), `AcpWebSocketTransport` (`src/OmpOS.Harness.Acp`) as WebSocket client, the worker `/acp` bridge as server precedent, `CatalogEventHub` for user-level events. Gaps:
- Desktop heads are behind NAT and must **dial out** to the API (or a relay), so the hub needs a WebSocket (or long-lived SSE+POST) endpoint in the API with `app.UseWebSockets()` (not called in `Program.cs`), plus `JwtBearerEvents.OnMessageReceived` or a `Sec-WebSocket-Protocol` carrier for browsers that cannot set `Authorization` on a WebSocket (not present; `JwtBearerSetup.cs:35-44` only sets `OnTokenValidated`). [INFERENCE]
- Multi-replica routing: a connection lives on one replica, so commands from another replica must go through Dragonfly pub/sub with payloads. `ISessionSignals` has no payload (`ISessionSignals.cs:12,18`), so a new payload channel is needed in `src/OmpOS.Coordination` (+ `.Dragonfly`, `LayerMap` entry if a new project, `tests/OmpOS.Coordination.Tests`). Until then a single replica (current plan, `ompos-api` not yet created) avoids this.
- Authorisation: relay frames carry owner check (device.owner == user) on every hop; per-user rate limit (section 6); audit log (GreptimeDB OTLP or the Solana-devnet ledger, the ledger code is plugin-side, outside this recon).
- Railway/Vercel limits: a Railway idle sleep or request timeout may close sockets [INFERENCE, unverified]; OQ-73 concerns remote-desktop acceptable use.
Files: new `services/OmpOS.Api/Relay/` (hub, endpoints, options, `RelayDisabledMiddleware` following `CloudDisabledMiddleware`), `Program.cs` (`UseWebSockets` before `UseAuthentication`, maps), `Security/JwtBearerSetup.cs`, `src/OmpOS.Coordination*`, `src/OmpOS.Contracts/Relay/`, Web and Native clients, `docs/api/openapi.json` (WebSocket routes appear only as a GET upgrade), `tests/OmpOS.Api.IntegrationTests` (new fixtures, `JwtTestHost`), Demo handler.

### 11.5 Cross-cutting files any of the above must touch

| Concern | File |
|---|---|
| Composition and pipeline order | `services/OmpOS.Api/Program.cs` (`:80-89`, `:148-159`, `:240-290`) |
| New options with fail-fast validation | `services/OmpOS.Api/Options/*`, `appsettings.json` (defaults off) |
| JSON source-gen | `Serialization/ApiJsonContext.cs`, or a module context appended like `AutomationServiceRegistration` |
| Rate-limit per user | `Security/*RateLimiterPolicy.cs`, `Services/Providers/ProviderRateLimiterPolicy.cs`, `Services/WorkspaceSettings/WorkspaceSettingsRateLimiterPolicy.cs`, `Automations/AutomationRateLimits.cs`, `Options/ApiRateLimitOptions.cs` |
| Persistence | `src/OmpOS.Sessions.Postgres` (new `*Schema`, `ServiceCollectionExtensions.cs`), in-memory twin in `src/OmpOS.Sessions` or `services/OmpOS.Api` |
| Owner list for server filesystem endpoints | `Services/WorkspaceSettings/WorkspaceAccess.cs:58-63`, `Api:Workspace:Owners` |
| OpenAPI + security scheme | `OpenApi/OpenApiConfig.cs`, new transformer, `docs/api/openapi.json`, `tests/OmpOS.Api.IntegrationTests/GuardTests.cs`, `tests/OmpOS.Web.Tests/OpenApiCoverageTests.cs`, Demo handler/fixtures |
| Layer rules for any new project | `tests/OmpOS.ArchitectureTests/LayerMap.cs` (one line) + ProjectReference in `OmpOS.ArchitectureTests.csproj`, `OmpOS.slnx`, `OmpOS.Linux.slnf`, `deploy/api.Containerfile` COPY, `packages.lock.json` (locked restore) |
| Web CSP for IdP origin | `vercel.ts`, `scripts/vercel/render-csp.sh`, `tests/.../RenderCspScriptTests` |
| Heads | `apps/OmpOS.Web/Services/IApiTokenProvider.cs` (+ new provider, `WebServiceRegistration.cs:33`), `apps/OmpOS.Native/Composition/HeadServices.cs:43-47,439-453`, `apps/OmpOS.Native/Cloud/ApiCloudAgentsHost.cs`, `src/OmpOS.Harness.Secrets` (OS credential store) |
| Docs and records | `docs/open-questions.md` (OD-03, OQ-26, OQ-68, OQ-104), `docs/handoff.md:144,194,233`, `docs/runbooks/hosting.md:45,96-111,162`, `.planning/REQUIREMENTS.md:13,73,93`, `.env.example`, `CHANGELOG.md` |

## 12. Findings that change the plan

1. Auth validation exists; the Supabase work is configuration plus hardening plus the missing sign-in clients, not a new module (confidence: code read; live Supabase smoke not done).
2. Process-local `SessionRegistry` and `SessionLifecycleStore` make `GET /sessions` and `/board` wrong after any restart or with 2+ replicas; fix before accounts ship (Cowork chat history depends on it).
3. All rate limits are global, not per user (documentation says per session). A multi-user launch needs per-`sub` partitions.
4. Server-filesystem endpoints (`/workspace/*`, `/settings/*`, `/hooks`) are readable by any signed-in user when `Api:Workspace:Owners` is empty: unsafe for open sign-up; default should become deny or per-user workspace.
5. Cloud secrets are in-memory (lost on restart); provider credential writes are process-global and disabled by default (OQ-104).
6. Native stores secrets in a plain-text owner-only file with no Windows ACLs; no OS credential store exists, so a refresh token needs a new `ISecretStore` implementation or an accepted risk.
7. The API is not deployed and Railway deploys are paused; OQ-73 (remote desktop acceptable use) is unresolved. Browser Use and Computer Use streaming and a public Dispatch relay depend on these.
8. OpenAPI declares no security scheme; the committed file is equality-tested, so adding one is a coordinated change.

## 13. Decisions needed and gating

| # | Decision | Needed for | Default proposed [INFERENCE] | Gate |
|---|---|---|---|---|
| D1 | IdP authority: Supabase project URL, issuer, audience `authenticated` | Accounts | Reuse OD-03 OIDC module; set the three `Api__Auth__*` variables | Owner supplies project ref; live smoke; Railway deploy unpaused for any public exposure (`hosting.md:166`) |
| D2 | Extra claim checks beyond audience (issuer pin, ES256 only, reject anonymous users) | Accounts | Pin ES256 + issuer; reject `is_anonymous` | None, code change |
| D3 | Reconcile OD-03/OQ-26 wording and stale line citations | Doc hygiene, REQUIREMENTS gate on HOST-* | Mark OD-03 decided in `.planning/REQUIREMENTS.md:13,73,93`, fix `hosting.md:45` | None |
| D4 | Persist session list and closed flag (Postgres `ListByOwner`, `closed` column) | Chat/Cowork history, Kanban | Add to `ISessionRepository` and schema with `owner_id` index | Schema migration approval (writes to shared TimescaleDB only on deploy) |
| D5 | Per-user rate-limit partitions and limits | Open sign-up | Partition by `sub`, keep current numbers | None |
| D6 | Workspace/settings endpoints for multi-user: deny by default or per-user roots | Open sign-up | `Api:Workspace:Owners` required when more than one user | Owner policy decision |
| D7 | Device pairing model and device credential (OQ-68) | Dispatch | Pair with a user token, then an API-signed device token (ES256, new key variable) | Owner decision on OQ-68; key management |
| D8 | Relay transport (WebSocket vs SSE+POST), browser auth carrier, single vs multi-replica | Dispatch | WebSocket, API single replica first, Dragonfly payload pub/sub later | Railway behaviour for long-lived sockets unverified; OQ-73 |
| D9 | Native secret storage for refresh tokens (OS credential store vs plain-text file) | Native sign-in | Build `ISecretStore` per OS, verified per runner | Runner availability per platform |
| D10 | Web CSP: second `connect-src` origin for Supabase Auth | Web sign-in | Extend `render-csp.sh` to accept an optional IdP origin | None |
| D11 | OpenAPI security scheme + 401/403 responses | Contract honesty | Add document transformer, regenerate | Coordinated change with Web contract tests and Demo |
| D12 | Cloud secrets durability (in-memory today) | Cowork cloud agents | Postgres-backed encrypted store; key management open | Owner decision on KMS |
