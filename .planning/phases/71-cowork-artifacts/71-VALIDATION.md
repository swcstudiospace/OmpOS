# Phase 71: Cowork Artifacts - Validation Map

Derived from 71-RESEARCH.md Validation Architecture. Every requirement maps to a test class and location, the command that proves it, and the observable failing signal. Wave 0 gaps list what does not exist yet.

## Requirement map

| Req | Test class and location | Command | Failing signal |
| --- | --- | --- | --- |
| AG-71-1 | ArtifactModelTests, tests/Autonogrammer.Cowork.Artifacts.Tests/Model | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release --filter-not-trait "Category=Live" | non-zero exit, or failed: [1-9] in the summary |
| AG-71-2 | ArtifactServiceTests, tests/Autonogrammer.Cowork.Artifacts.Tests/Service (write-once numbering, parent link, TimeProvider CreatedAt) | same as AG-71-1 | same as AG-71-1, or version 2 created twice for one update |
| AG-71-3 | ArtifactServiceTests (stale base, idempotent same hash) | same as AG-71-1 | same as AG-71-1, or stale_base missing latest_version |
| AG-71-4 | ArtifactServiceTests (integrity refusal) plus FileArtifactStoreTests and PostgresArtifactStoreTests, tests/Autonogrammer.Cowork.Artifacts.Stores.Tests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Stores.Tests -c Release --filter-not-trait "Category=Live" with scripts/test-services.sh up sourced | non-zero exit, or failed: [1-9], or tampered bytes rendering instead of ArtifactIntegrityException |
| AG-71-5 | ArtifactServiceTests (DerivedFrom copy, Author User edit, stale save reload-or-copy) | same as AG-71-1 | same as AG-71-1, or copy sharing the version chain |
| AG-71-6 | ArtifactExportTests, tests/Autonogrammer.Cowork.Artifacts.Tests/Export (zip determinism, safe defaults, sanitised names) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release -- --filter-class "*ArtifactExportTests*" | non-zero exit, or two exports of one artifact hashing differently |
| AG-71-7 | ArtifactDiffTests, tests/Autonogrammer.Cowork.Artifacts.Tests/Diff (caps, cancellation, metadata diff) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release -- --filter-class "*ArtifactDiffTests*" | non-zero exit, or over-cap diff returning lines instead of counts |
| AG-71-8 | ArtifactToolProviderTests and EditApplierTests, tests/Autonogrammer.Cowork.Artifacts.Tests/Tools | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release -- --filter-class "*ArtifactToolProviderTests*" | non-zero exit, or the grant tuple missing from a create or update result |
| AG-71-9 | ArtifactOptionsValidator tests in Service plus store cap test in PostgresArtifactStoreTests (5242880) | same as AG-71-1 and AG-71-4 commands | non-zero exit, or a truncation instead of a typed too_large or quota_exceeded |
| AG-71-10 | CsvReaderTests and ImageValidatorTests, tests/Autonogrammer.Cowork.Artifacts.Tests/Rendering (71-02); ArtifactRendererViewTests, tests/Autonogrammer.Native.Tests (desktop Markdown and Code, 71-05); ArtifactMarkdownHostileTests with Fixtures/hostile/md/script-link.md, tests/Autonogrammer.UI.Tests/Cowork/Artifacts (web Markdown, 71-06) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release -- --filter-class "*CsvReaderTests*" and the Native and UI classes named here | non-zero exit, or a script, anchor or navigation produced from a Markdown or Code artifact |
| AG-71-11 | SvgSanitizerTests, tests/Autonogrammer.Cowork.Artifacts.Tests/Rendering | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release -- --filter-class "*SvgSanitizerTests*" | non-zero exit, or a script element surviving the allowlist walk |
| AG-71-12 | CsvReaderTests (formula-like marking, caps, Showing the first N rows) | same as AG-71-10 | non-zero exit, or a formula cell unmarked in the view |
| AG-71-13 | ImageValidatorTests (magic, 8192 and 50-megapixel caps, never-SVG) | same as AG-71-10 | non-zero exit, or SVG bytes accepted as Image |
| AG-71-14 | ArtifactDocumentPolicyTests (exact CSP strings), tests/Autonogrammer.Cowork.Artifacts.Tests/Rendering | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release -- --filter-class "*ArtifactDocumentPolicyTests*" | non-zero exit, or a CSP string differing by one token from ADR-0033 D7.2 |
| AG-71-15 | ArtifactPanelViewTests, tests/Autonogrammer.Native.Tests, plus ArtifactSurfaceLiveTests with AUTONOGRAMMER_LIVE_ARTIFACT_SURFACE=1 on a private Xvfb | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*ArtifactPanelViewTests*" | non-zero exit, or any message subscription found on the surface wrapper |
| AG-71-16 | ArtifactFrameTests, tests/Autonogrammer.UI.Tests, plus ArtifactRenderRouteTests and ArtifactClientRouteTests (the routes `ArtifactWebClient` calls are documented operations) and DemoArtifactFixturesTests, tests/Autonogrammer.Web.Tests, plus CssContractTests (artifacts.css imported) and ServiceWorkerRulesTests (/cowork prefix, mermaid not precached) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.UI.Tests -c Release -- --filter-class "*ArtifactFrameTests*" and the same for Web.Tests with "*Artifact*" | non-zero exit, or an iframe attribute differing from the exact strings, or a stylesheet beside autonogrammer.css that it does not import |
| AG-71-17 | ArtifactAccessReviewTests, tests/Autonogrammer.Presentation.Tests, plus provider origin validation in ArtifactToolProviderTests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*ArtifactAccessReviewTests*" | non-zero exit, or an origin approved by timeout or default |
| AG-71-18 | ArtifactPanelModelTests (destroy-on-hide) plus smoke artifact page (second load destroys frame) | OUT_DIR="$(mktemp -d)" SMOKE_TARGETS=artifact bash scripts/web-smoke/run-smoke.sh (the full default run adds the api and demo halves) | non-zero exit, or any outbound request, frame navigation or parent access from hostile fixtures |
| AG-71-19 | ArtifactSyncServiceTests, tests/Autonogrammer.Cowork.Artifacts.Stores.Tests (metadata-only default, idempotence, 409 fork, offline queue) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Stores.Tests -c Release -- --filter-class "*ArtifactSyncServiceTests*" | non-zero exit, or any body byte sent while sync is disabled |
| AG-71-20 | ArtifactEndpointsTests and ArtifactRenderTicketTests, tests/Autonogrammer.Api.IntegrationTests/Artifacts (owner 404 on every route, attachment octet-stream pair with nosniff and the middleware CSP and X-Frame-Options DENY on content, render route with its own CSP and no X-Frame-Options, anonymous render under enrollment, `security: []` on the render route only, 403 subject_not_allowed inherited, image PUT of 5.5 MiB accepted under default Hosting options and 6 MiB + 1 refused as too_large) plus the existing SecurityHeadersTests and GuardTests.OpenApi_ServedEqualsCommitted | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Artifact*" then --filter-class "*SecurityHeadersTests*" then --filter-class "*GuardTests*" | non-zero exit, or a content response missing the attachment octet-stream pair, or the render document carrying X-Frame-Options, or a stale docs/api/openapi.json |
| AG-71-21 | ArtifactAccessReviewTests and panel tests (labels, live region, focus escape, no new colour role) | presentation and native artifact-class commands | non-zero exit, or a colour-literal or automation-name gate failing |
| AG-71-22 | ArtifactBlockContractTests, tests/Autonogrammer.Contracts.Tests/Ui (UnknownBlock degradation; ContractVersionTests stays green at minor 6) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Contracts.Tests -c Release -- --filter-class "*ArtifactBlockContractTests*" | non-zero exit, or kind artifact throwing on an old reader |
| AG-71-23 | HostileCorpusTests, tests/Autonogrammer.Cowork.Artifacts.Tests/Rendering (every Fixtures/hostile entry with asserted outcome) | same as AG-71-1 | non-zero exit, or any fixture file without an asserted outcome |
| AG-71-24 | ArtifactAuditTests, tests/Autonogrammer.Api.IntegrationTests/Artifacts (counter on the ApiTelemetry.SourceName meter with kind and result tags, four closed kinds, no content or titles, no ticket substring in any activity tag of a render request) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*ArtifactAuditTests*" | non-zero exit, or any audit payload containing a title or content string, or the ticket in an activity tag |

## Roadmap criteria commands

1. Library including hostile corpus: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release --filter-not-trait "Category=Live"; fails on non-zero exit or failed: [1-9] or total: 0.
2. Stores: source "$(bash scripts/test-services.sh up)" then /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Stores.Tests -c Release --filter-not-trait "Category=Live"; fails on non-zero exit or failed: [1-9].
3. Endpoints plus OpenAPI fresh: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Artifact*"; fails on non-zero exit or a stale docs/api/openapi.json diff.
4. Heads: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*Artifact*" and the same filter for Native.Tests, UI.Tests, Web.Tests; fails on non-zero exit or failed: [1-9] in any head.
5. Architecture: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release; fails on non-zero exit or LayerMap, graph, decision-record or suppression failures naming Artifacts.
6. Web smoke: OUT_DIR="$(mktemp -d)" bash scripts/web-smoke/run-smoke.sh with the artifact page; fails on any outbound request, navigation or parent access from hostile fixtures.
7. Licences: bash scripts/ci/check-licences.sh and python3 scripts/legal/generate-third-party-notices.py --check; fails on non-zero exit from either.

## Wave 0 gaps (do not exist yet)

- The content of the four scaffold projects (they exist since phase 66.1 with AssemblyMarker.cs only, csproj, lock file; DiffPlex, Npgsql, Contracts and Cowork.Abstractions references already present): src/Autonogrammer.Cowork.Artifacts, src/Autonogrammer.Cowork.Artifacts.Stores, tests/Autonogrammer.Cowork.Artifacts.Tests, tests/Autonogrammer.Cowork.Artifacts.Stores.Tests.
- Fixtures/hostile corpus in the Tests project (71-02).
- FakeArtifactSurface and the Presentation/Cowork/Artifacts models (71-05; Presentation/Cowork itself arrives with phase 69 unit 3).
- ICoworkToolProvider, CoworkSessionContext, Preview.Grant taint rule (phase 70 U1, precondition of 71-04).
- IDockPanel and CoworkDockHost (phase 69 unit 3, precondition of 71-05 registration).
- M0006 artifacts, artifact_versions, artifact_grants tables (phase 68 U3, precondition of 71-03 Task 2).
- Api.IntegrationTests/Artifacts test folder (71-04).
- Artifact page for scripts/web-smoke (71-06) and the bridge-name probe page (71-05 live test).
- Mermaid hash test and IMermaidAssetProvider fake (71-02 builder, 71-06 vendored file).
- DemoArtifactFixtures, its contract and route tests (DemoArtifactFixturesTests, ArtifactClientRouteTests; ClientRouteSurface and OmpApiClientRouteTests.ExpectedRoutes stay untouched) and the artifact smoke target (71-06).

## Integration gates (2026-10-29)

These existing gates lock lists this phase extends (CONTEXT D-42 to D-51). Each runs through /tmp/ag-bin/ag-dotnet or bash exactly as written and fails on non-zero exit.

| Gate | Command | Failing signal |
| --- | --- | --- |
| Skip markers have SUP rows | bash scripts/ci/check-skip-markers.sh | `skip marker without a SUP row: Assert.Skip in tests/Autonogrammer.Native.Tests/ArtifactSurfaceLiveTests.cs` or `SUP-nnn names no live marker` |
| Suppression ledger and deferred tokens | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*SuppressionLedgerTests*" then "*DeferredScopeTests*" | an unlisted suppression, or an offender line naming vscode, ADE-21 or ADE-22 |
| Service worker rules | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Web.Tests -c Release -- --filter-class "*ServiceWorkerRulesTests*" | `API_PATH_PREFIXES in service-worker.published.js must name it` for /cowork, or the mermaid exclusion test failing |
| CSS import contract | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.UI.Tests -c Release -- --filter-class "*CssContractTests*" | `every stylesheet beside autonogrammer.css must be imported by it`, or `holds a colour literal` naming artifacts.css |
| CSP script | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Web.Tests -c Release -- --filter-class "*RenderCspScriptTests*" | an exact-stdout row changing, or the --frame-src rows failing (Demo must print nothing, a bad origin must print nothing and not echo the origin) |
| Contract version | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Contracts.Tests -c Release -- --filter-class "*ContractVersionTests*" | minor not 6 (this phase never edits ContractVersion.cs) |
| Architecture | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release | a LayerMap, graph or decision-record failure naming Native.Views, Presentation, Cowork.Artifacts or Web |
| Smoke third target | OUT_DIR="$(mktemp -d)" SMOKE_TARGETS=artifact bash scripts/web-smoke/run-smoke.sh | non-zero exit, or any outbound request, frame navigation or parent access from a hostile fixture |
| Locked restore of relocked projects | for each of src/Autonogrammer.Native.Views, apps/Autonogrammer.Native, tests/Autonogrammer.Native.Tests, tests/Autonogrammer.Cowork.Artifacts.Stores.Tests: /tmp/ag-bin/ag-dotnet restore <csproj> -p:RestoreLockedMode=true | NU1004 or NU1403 (a lock file not regenerated after a ProjectReference change) |
| Api tests need the throwaway services | cd /root/src/repos/ompos-autonogrammer && source "$(bash scripts/test-services.sh up)" && { <ag-dotnet test ...>; rc=$?; bash scripts/test-services.sh down; exit $rc; } | AUTONOGRAMMER_TEST_POSTGRES_CONNECTION is not set (JwtTestHost) when run bare |
