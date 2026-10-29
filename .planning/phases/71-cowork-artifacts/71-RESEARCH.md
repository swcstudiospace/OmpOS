# Phase 71: Cowork Artifacts - Research
Source: docs/plans/v0.16.0-research/briefs/71-artifacts.md
This file summarises research reports 02, 03, 04, 06 and 07 as cited by ADR-0033, the index reconciliation R3, R4, R6, R7, R10, R14, R15 and the scaffold manifest. Nothing was built or run.

## Standard Stack / What to reuse
- Markdown: Markdig 1.4.2 `DisableHtml()` pipelines at `src/Autonogrammer.Presentation/Services/MarkdownRenderer.cs:22-25` and `src/Autonogrammer.Native.Views/Views/Blocks/MarkdownAstBuilder.cs:30-33`; hostile tests `tests/Autonogrammer.UI.Tests/MarkdownSanitizationTests.cs`, `MarkdownSinkTests.cs` (reuse their payload style for the corpus).
- Inline raster rule: `src/Autonogrammer.GenUi/A2uiSurfaceCatalog.cs:161-171` allows `data:image/png|jpeg|gif` and refuses SVG data URIs.
- Pins already present (re-verified 2026-10-29): Markdig `Directory.Packages.props:30`, DiffPlex 1.9.0 `:31` (now referenced by `src/Autonogrammer.Cowork.Artifacts/Autonogrammer.Cowork.Artifacts.csproj`), AvaloniaEdit and TextMate `:92-95`, `Avalonia.Controls.WebView` 12.1.1 `:99`, and `Avalonia.Controls.DataGrid` 12.1.2 `:27` (written by the scaffold). `src/Autonogrammer.Native.Views/Autonogrammer.Native.Views.csproj` already carries the WebView and DataGrid `PackageReference` lines.
- Gateway cap: `src/Autonogrammer.Harness.McpGateway/McpGatewayServer.cs:34` (`MaxBodyBytes` 1 MiB, request bodies only).
- Owner scoping: `services/Autonogrammer.Api/Endpoints/SessionEndpoints.cs:417-431` (`FindOwnedSessionAsync`, one 404 for absent and foreign).
- Atomic owner-only file write: `apps/Autonogrammer.Native/Settings/FileSettingsStore.cs` (temp 0600, folder 0700, rename); described in report 02 line 178.
- Additive block: `src/Autonogrammer.Contracts/Ui/UiBlock.cs:5-9`, `UiBlockConverter.cs` (`KindToType` line 17, `TypeToKind` line 34; unknown kind gives `UnknownBlock`); version `src/Autonogrammer.Contracts/ContractVersion.cs:9` = 1.6.0 (the scaffold bump; `ContractVersionTests` locks minor 6).
- Web CSP: `vercel.ts:16-25` (`default-src 'self'`, no `frame-src`, `frame-ancestors 'none'`); generator `scripts/vercel/render-csp.sh`, test `tests/Autonogrammer.Web.Tests/RenderCspScriptTests.cs`.
- Smoke harness: `scripts/web-smoke/run-smoke.sh:52` (`SMOKE_TARGETS`), `:67-78` (`wants`, validation loop), `:97-115` (Chrome discovery via `CHROME_BIN`, chromium, google-chrome, Playwright), `:168-176` (owned headless launch, empty profile), `:195-243` (api and demo halves); the a11y modules live in `scripts/web-smoke/a11y/`.
- Vendored library note format: `src/Autonogrammer.UI/wwwroot/lib/xterm/VERSION.txt` (name, package, version, licence, licence-file).
- F6 region cycle: `src/Autonogrammer.Native.Views/MainView.axaml.cs:826`. Gates the panel must pass: `tests/Autonogrammer.Native.Tests/ThemeContrastTests.cs`, `Theme/ColourLiteralTests.cs`, `tests/Autonogrammer.ArchitectureTests/HeadMotionTests.cs`, `tests/Autonogrammer.UI.Tests/CssContractTests.cs`.

## Facts the planner needs
- Document policy strings are fixed: Html scripts-on CSP and the "scripts off", SVG/Image policies are at `docs/decisions/ADR-0033-cowork-artifacts.md:112`; a test must match them exactly (brief 71 U2, `briefs/71-artifacts.md:50`).
- A `srcdoc`/`data:`/`blob:` document inherits the embedding page's CSP (CSP3 7.8), so web `srcdoc` Html cannot run inline script; the ticketed render route is the only interactive web path (`ADR-0033:19,124,128`). The desktop control sends no headers, so its CSP is a `meta` first in the document; `frame-ancestors` and `sandbox` are ignored in a meta (`ADR-0033:123`).
- Render ticket: HMAC-SHA256 over account id, artifact id, version, hash, expiry at most 60 s; two keys of at least 32 random bytes in `Cowork:Artifacts:RenderTicketKeys` (`ADR-0033:130`). The render route is anonymous: OpenAPI `security: []` with its own limiter partition (index R10, `00-index.md:103`).
- Tables and cap: M0006 carries `artifacts`, `artifact_versions`, `artifact_grants`, content `CHECK (octet_length(content) <= 5242880)`, flat names, no RLS; phase 71 writes no migration (`ADR-0033:147`; `00-index.md:89`). Service limits (512 KiB text) sit under the store cap (`ADR-0033:150,235`). Report 04 table-name proposals with an `acct_` prefix are superseded by R3 (`04-data-plane.md:143`).
- Sync default is off, metadata only; OQ-343 and OQ-402 are one decision (`00-index.md:91,57`; `docs/open-questions.md` OQ-343 row).
- Audit kinds are a closed list fixed by 68 U3: `artifact.created`, `artifact.updated`, `artifact.grant`, `artifact.revoke` (`00-index.md:113`); counter name proposed at `04-data-plane.md:158`.
- `artifact_create`/`artifact_update` return `Preview.Grant = ("artifact.write", "cowork")`; tainted sessions still ask (`ADR-0033:79`; R6 `00-index.md:95`).
- `from_file` follows `CoworkSessionContext.TrustedFolders` and the Storage folder, not `ICoworkGrantStore` (`ADR-0033:234`; `00-index.md:75`).
- Registry: `Cowork.Artifacts` is pure and web-safe (no process, no file system), `Stores` takes an `NpgsqlDataSource` from the host; only tuples `Stores -> Artifacts` and `UI -> Artifacts` exist (`00-scaffold-manifest.md:23-24,65`). `Autonogrammer.Web` may not reference a process-spawning assembly (`tests/Autonogrammer.ArchitectureTests/ProjectGraphTests.cs:12`).
- Mermaid: 12.2.0, 5.2 MB, SHA-256 and location at `ADR-0033:132`; embeds `elkjs` (EPL-2.0), licence admission is OQ-400 (`ADR-0033:207-209`). `scripts/legal/*` change only if it is admitted (`briefs/71-artifacts.md:43`).
- `Avalonia.Controls.WebView` has no CDP, no screenshot and no trusted input; it exposes script evaluation, messages, navigation events and environment options (`07-browser-computer-use-feasibility.md:43-44`); ADR-0009 measured load and selector on WebKitGTK under Xvfb only (`ADR-0033:18`).
- Transcript images are owned by brief 70 U5 (`ToolUpdateImage`, `ImageBlock`); this phase does not add them (`briefs/71-artifacts.md:38`; OQ-410 default metadata only).

## Architecture Patterns
- Pure core plus ports: `ArtifactService` over `IArtifactStore`; `InMemoryArtifactStore` for tests; File and Postgres stores in `Stores`; sync client `HttpArtifactRemote` behind a port (brief units U1, U3).
- One policy author: `ArtifactDocumentPolicy` builds every CSP; `HtmlDocumentWrapper` and `MermaidDocumentBuilder` consume it; no CSP literal elsewhere.
- Renderers split by kind: in-process (Markdown, Code, Csv, Image) versus isolated surface (Html, Svg on desktop, Mermaid); `IArtifactSurface` state machine in Presentation with a fake for tests; three implementations (`ADR-0033:120-126`).
- Write-once files: `CreateNew`, temp file, rename, one lock per artifact; head derived from the highest version file (`ADR-0033:146`).
- CAS update: `BaseVersion` compare, same-hash idempotence, 409 on divergent PUT.
- Tool route: provider registered with `CoworkToolHost`; omp sees `mcp__cowork_artifact_*`; errors are tool results with a `code`.

## Pitfalls
- Web `srcdoc` inherits the page CSP: do not promise interactive Html there (`ADR-0033:19`).
- Meta CSP cannot carry `frame-ancestors` or `sandbox`; the navigation filter and control options do that part on desktop (`ADR-0033:123`).
- Do not sanitise HTML or use an HTML sanitiser for SVG: parser mismatch is mutation XSS (`ADR-0033:104`); do not add `Svg.Skia` (SkiaSharp 4.148.0 vs 3.119.4, `ADR-0033:105`).
- The gateway cap is 1 MiB on requests; images over that must use `from_file`; the API PUT allows 6 MiB for images (`ADR-0033:76,150`).
- Hash exact stored bytes; the server must recompute and never trust the client hash (`ADR-0033:58`).
- Title checks reject, never strip, bidi and control characters (`ADR-0033:37`).
- A script can trap focus and burn CPU; mitigations are skip link, Stop and destroy-on-hide, a recorded residual risk (`ADR-0033:163,228`).
- Native JS dialogs may look like app dialogs; the prefix script that no-ops them can be undone by an iframe realm (`ADR-0033:229`).
- Inline 5.2 MB script in `NavigateToString` is unmeasured (`ADR-0033:232`); DNS rebinding of an approved host stays open (`ADR-0033:230`).
- The ticket is a path segment: redact it from logs and create the frame with no referrer (`ADR-0033:130`).
- Manifest row 8 says brief 71 U3 "says otherwise" about migrations; brief 71 U3 says none (`00-scaffold-manifest.md:24` vs `briefs/71-artifacts.md:51`): follow the brief and R3.
- Phase code must not edit `ContractVersion.Current`, slnx, slnf, LayerMap rows or locks outside "per phase" rows (R9, R17).

## Verification commands
- `dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release --filter-not-trait "Category=Live"` (`briefs/71-artifacts.md:60`).
- `source "$(bash scripts/test-services.sh up)"` then `dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Stores.Tests -c Release --filter-not-trait "Category=Live"` (line 61; the `up` form is the one `bootstrap.sh` uses, report 09 section 5 step 8).
- `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Artifact*"` and `docs/api/openapi.json` fresh (line 62).
- `dotnet test --project tests/Autonogrammer.Presentation.Tests|Native.Tests|UI.Tests|Web.Tests -c Release -- --filter-class "*Artifact*"` (line 63).
- `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` (line 64); `-- --filter-class "*ProjectGraphTests*"` and `"*DecisionRecordTests*"` for a quick check.
- `OUT_DIR="$(mktemp -d)" bash scripts/web-smoke/run-smoke.sh` with the artifact page (line 65).
- `bash scripts/ci/check-licences.sh` and `python3 scripts/legal/generate-third-party-notices.py --check` (line 66).
- Live desktop surface on a private Xvfb, never the main display: `AUTONOGRAMMER_LIVE_ARTIFACT_SURFACE=1 xvfb-run -a dotnet test --project tests/Autonogrammer.Native.Tests -c Release --no-build --filter-trait "Category=Live"` (`ADR-0033:221`).

## Cannot be verified on this host
Windows WebView2, macOS WKWebView, Android WebView (compile only); CSP-by-meta, navigation cancel and bridge option on any engine beyond a private Xvfb run on WebKitGTK; Firefox and Safari sandbox behaviour (only headless Chrome 153 here); screen readers (structural automation-name and axe checks only); the real Railway API framed from the real Vercel origin; live Supabase and Railway; load time of the 5.2 MB inline script; legal admission of EPL-2.0 (OQ-400). Source: `00-index.md:16`, `briefs/71-artifacts.md:87`.

## Validation Architecture
Framework: xunit.v3 with Shouldly under Microsoft.Testing.Platform, `coverlet.MTP`, `TrxReport` (`00-scaffold-manifest.md:9`). Projects: `tests/Autonogrammer.Cowork.Artifacts.Tests`, `.Stores.Tests`, existing `Api.IntegrationTests`, `Presentation.Tests`, `Native.Tests`, `UI.Tests`, `Web.Tests`, `ArchitectureTests`. Quick run: `dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release --filter-not-trait "Category=Live"`. Full run: every command in "Verification commands" plus the repo gate (`bootstrap.sh`, report 09 section 5).

| Requirement | Test location (brief unit) |
| --- | --- |
| AG-71-1, 2, 3, 4 (core), 5, 6, 7, 9 | `Artifacts.Tests/Model`, `Service`, `Export`, `Diff` (U1): stale base, idempotent same hash, integrity failure, copy, zip determinism, diff caps, limits as typed results |
| AG-71-22 | `Artifacts.Tests` plus `Contracts` JSON test: old reader gets `UnknownBlock` (U1) |
| AG-71-10 to 14, 23 | `Artifacts.Tests/Rendering/**` (U2): every `Fixtures/hostile/**` entry has an asserted outcome; CSP strings equal ADR text; no origin in `script-src`; SVG, CSS, CSV, image caps |
| AG-71-4 (stores), 19 | `Stores.Tests` (U3): atomic write-once files, per-account folders, 409 fork, offline queue, M0006 round trip, 5,242,880 cap, `account_id` in every query |
| AG-71-8, 17, 20, 24 | `Artifacts.Tests/Tools/**`, `Api.IntegrationTests/Artifacts/**` (U4): grant preview, `edits` unique match, `from_file` outside grants refused, owner 404, ticket expiry and version binding, content never `text/html`, OpenAPI fresh, audit kinds without content |
| AG-71-15, 17, 18, 21 | `Presentation.Tests`, `Native.Tests` (U5): fake surface, no message subscription, access-review states, focus escape controls, no colour literal |
| AG-71-16, 18, 21 | `UI.Tests`, `Web.Tests`, web smoke (U6): exact iframe attributes, second `load` destroys frame, "static view" label, axe clean, Demo contract tests |
Test class names beyond these folders are not named by the brief [INFERENCE].

Wave 0 gaps (do not exist yet): the content of the four scaffold projects (they exist since 66.1 as `AssemblyMarker.cs` plus csproj and lock file only); `Contracts/Artifacts/*`; the `Fixtures/hostile/**` corpus and its copy-to-output item in the test csproj; `FakeArtifactSurface`; Postgres store fixture bound to the M0006 tables (phase 68 U3; connection from `AUTONOGRAMMER_TEST_POSTGRES_CONNECTION`, exported by `scripts/test-services.sh up`); `Api.IntegrationTests/Artifacts/**`; artifact page for `scripts/web-smoke` (`artifact-page.mjs` plus the `artifact` target); probe page for `NativeWebView` bridge names; Mermaid hash test and `IMermaidAssetProvider` fake; hostile Mermaid fixtures (`%%{init}%%`); Demo artifact fixtures with their own contract and route tests (`ClientRouteSurface` and `OmpApiClientRouteTests.ExpectedRoutes` are not extended, D-46); SUP rows for every `Assert.Skip` this phase adds (D-49).

## Integration facts (2026-10-29, verified against the tree)

- Pipeline of `services/Autonogrammer.Api/Program.cs` today: `AddEngineIsolation` (:54), `AddApiAuthentication` (:80), `AddAutomationServices` (:117), limiter registration (:162-174), `AddApiTelemetry` (:183), `AddHostingHardening` (:185), `Build` (:187), `ProcessHardening.ApplyIfEnabled` (:190), `UseHostingHardening` (:192: forwarded headers, HSTS, `SecurityHeadersMiddleware`, `UseRequestTimeouts`), exception handler, `CorrelationIdMiddleware`, CORS, `UseApiAuthentication`, `UseRateLimiter`, `UseAuthorization` (:206), `CloudDisabledMiddleware` (:207), `AutomationDisabledMiddleware` (:208), endpoint maps (:232-248). Phases 67 to 70 and 74 edit the same file; anchor by symbol, not by line.
- `GET /auth/config` is `Endpoints/AuthConfigEndpoints.cs` (limiter `auth-config` 30 per minute per address, `Cache-Control: no-store`); `AuthGate`, `IAuthSession`, `OwnerTokenSession`, `IApiTokenProvider`, `WebServiceRegistration.cs:34-37` are the sign-in seam of phase 67. The artifact code touches only `IApiTokenProvider` (through `ApiHttp`) and `IAuthSession.Changed`/`HasToken`.
- Headers: `SecurityHeadersMiddleware` (`Hosting/SecurityHeadersMiddleware.cs:41-48`) forces `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, a fixed `Permissions-Policy` and, for non-HTML, CSP `default-src 'none'; frame-ancestors 'none'`; `no-store` for authenticated responses without their own `Cache-Control` (:51-55). Kestrel `MaxRequestBodyBytes` defaults to 4 MiB (`HostingKestrelOptions.cs:10`), `Api:MaxRequestBodyBytes` to 1 MiB (`ApiOptions.cs:22`); the default request timeout is 30 s and `HostingPolicies.NoTimeout` exists for streams.
- Limiter pattern: keyed `IRateLimiterPolicy<string>` singletons plus `AddOptions<RateLimiterOptions>().Configure<IServiceProvider>` inside a module registration (`Automations/AutomationServiceRegistration.cs:71-81`, `Services/Providers/ProvidersServiceRegistration.cs:35`); partition keys `RateLimitPartitionKey.For` and `.ForAddress` (`Security/RateLimitPartitionKey.cs`).
- `IUserContext.Name` is the validated `sub` and throws when none exists (`Security/AuthenticatedUserContext.cs`); `Api:Auth:AllowedSubjects` is enforced by the fallback policy (`AllowedSubjectAuthorizationHandler`); test hosts: `JwtTestHost.CreateFactory()`/`IssueToken(subject)`, `Security/OwnerTokenTestHost.cs`, `Hosting/HostingTestHost.cs`, `AllowedSubjectTests.cs` (the 403 `subject_not_allowed` pattern), `CloudRunOwnerScopeTests.cs` (the two-principal 404 pattern).
- Telemetry: `Hosting/ApiTelemetryExtensions.cs` is the single `AddOpenTelemetry`; `Cloud/CloudMetrics.cs` creates its meter with `meterFactory.Create(ApiTelemetry.SourceName)`; EventIds in use are 0 to 199 only.
- OpenAPI: `docs/api/openapi.json` has no `security` member today; `GuardTests.OpenApi_ServedEqualsCommitted` compares served and committed; `scripts/ci/check-openapi-breaking.py` compares with the base branch; additive routes are not breaking.
- Web: head project `apps/Autonogrammer.Web` (`Services/`, `Demo/`, `Pages/Index.razor`, `wwwroot/service-worker.published.js`); Demo routing hook `Demo/DemoApiHandler.cs:225` (`DemoSettingsRoutes.TryRouteAsync`) and `internal static bool Match(string[] segments, params string?[] pattern)`; `DemoTestHost` (`Http`, `Recorder`, `Interceptor`) and `OpenApiCoverageTests.ReadDocumentedOperationsAsync` are the models for an artifact client route test; CSS entry `src/Autonogrammer.UI/wwwroot/styles/autonogrammer.css` imports every sheet (`CssContractTests`); CSP: `vercel.ts:16-25` has no `frame-src`, `worker-src` or `manifest-src`; the service worker precaches every `.js` of the build (`service-worker.published.js:25`).
- Gates that fail on an unextended list: skip-marker SUP rows (`scripts/ci/check-skip-markers.sh`, last row SUP-239), `SuppressionLedgerTests`, `DeferredScopeTests` (tokens `ADE-21`, `ADE-22`, `vscode`), `ServiceWorkerRulesTests.EveryApiRouteRootIsKnownToTheWorker`, `RenderCspScriptTests` (exact stdout), `OmpApiClientRouteTests.ExpectedRoutes`, `SecurityHeadersTests`, `ContractVersionTests`.
- Every test or build command in this phase runs through `/tmp/ag-bin/ag-dotnet` (same arguments as `dotnet`, report 09 section 5); the `dotnet test ...` lines under "Verification commands" are the argument forms.
