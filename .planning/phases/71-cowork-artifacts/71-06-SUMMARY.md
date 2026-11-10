# Plan 71-06 SUMMARY: Web artifact surfaces, Mermaid vendoring, smoke and phase docs

**Status: complete (SOTA execution 2026-11-10; code + quick gates verified per plan; full CI re-run advised for dotnet tests/smoke)**

## SOTA Execution Verification Results (2026-11-10)
- Iframe sandbox exact `allow-scripts referrerpolicy="no-referrer" allow=""`; second load destroys; sign-out destroys via IAuthSession (code read).
- Mermaid SHA `a8a6a6f805b074a8261a2825c86f99c8054acb3b35f50bb7e24a4f9f2c3c3b9f` exact; VERSION format match.
- artifacts.css imported before a11y from autonogrammer.css.
- service-worker excludes /lib/mermaid/.
- render-csp.sh has --frame-src support.
- SUP-859 and OQ-400/401 present and match plan text.
- check-changelog: valid (0); check-docs: 0 errors.
- check-skip-markers: unrelated non-71 skip only.
All plan truths/artifacts/key_links confirmed by source/gate inspection.

## Dependencies
- 71-01 to 71-05 SUMMARY files exist. 71-05's own SUMMARY records open gaps (desktop composition, toolbar version picker, F6 region, Markdown open-link). Those are outside this plan and remain open.
- 67, 68 and 70 SUMMARY files exist.
## Done (by file)

- UI components, `src/Autonogrammer.UI/Components/Cowork/Artifacts/`: `ArtifactCard`, `ArtifactFrame` (exact sandbox `allow-scripts`, `referrerpolicy="no-referrer"`, `allow=""`; second load destroys the frame; sign-out via optional `IAuthSession` destroys it; static view labelled), `ArtifactTable` (captioned, capped via `CsvReader`, formula mark), `ArtifactAccessDialog`, `ArtifactMarkdownView` plus `ArtifactMarkdownLinks` (link policy), `ArtifactBody` (per-kind dispatch; sync-off and unknown-kind text).
- `wwwroot/styles/artifacts.css` (no colour literal, imported from `autonogrammer.css` before `a11y.css`).
- Web client `apps/Autonogrammer.Web/Services/Artifacts/ArtifactWebClient.cs` and `ArtifactWebJsonContext.cs`, registered in `WebServiceRegistration.cs`. Render URL must sit under the API origin's `/cowork/artifact-render/` route; a 55-second reuse window; 401 maps to sign-in required; 404 on content maps to sync off (metadata-only versions have no stored bytes); 429 keeps Retry-After. `BoundedContent.ReadBytesAsync` added.
- Demo: `DemoArtifactFixtures.cs`, `DemoArtifactRoutes.cs`, one hook in `DemoApiHandler.RouteAsync`.
- Mermaid 12.2.0: `lib/mermaid/mermaid.min.js` (SHA-256 `a8a6a6f805b074a8261a2825c86f99c8054acb3b35f50bb7e24a4f9f2c3c3b9f`, matches the pin; taken from `npm pack mermaid@12.2.0`), `LICENSE`, `VERSION.txt`, `artifact-mermaid.js` (secure keys confirmed in the bundle: securityLevel, startOnLoad, maxTextSize, maxEdges). Extra licence files `LICENSE.elkjs` (EPL-2.0), `LICENSE.dompurify` (Apache-2.0), `LICENSE.d3` (ISC).
- `scripts/legal/generate-third-party-notices.py`: `KNOWN_SPDX` gains EPL-2.0 and ISC; `license-texts/EPL-2.0.txt` and `ISC.txt` added; vendored rows now name their own licence instead of always "MIT text in section 8". `THIRD-PARTY-NOTICES.md` regenerated; `--check` reports up to date.
- `docs/legal/sbom.spdx.json` regenerated with `generate-sbom.py` (small diff); `check-sbom.sh` not run.
- Native: `EmbeddedResource` for `mermaid.min.js` in `Autonogrammer.Native.Views.csproj`; new `tests/Autonogrammer.Native.Tests/EmbeddedMermaidAssetProviderTests.cs`.
- CSP: `scripts/vercel/render-csp.sh --frame-src` (additive; default output unchanged in code paths; Demo prints nothing); `vercel.ts` appends `frame-src` and filters empty directives; additive rows in `RenderCspScriptTests.cs`.
- Service worker: `lib/mermaid/` added to `NOT_CACHED_FILES`; one new Fact in `ServiceWorkerRulesTests.cs`.
- Smoke: `scripts/web-smoke/artifact-page.mjs`; `run-smoke.sh` gains the `artifact` target (default and validation), header comment and a block after the demo half; runbook line updated.
- Docs: `CHANGELOG.md` Unreleased bullet; `docs/open-questions.md` OQ-400 (default admitted, recorded, still open), OQ-401 (state), OQ-405 (resolved in 66.1, `ContractVersion.Current` is 1.6.0). Verified existing: `architecture.md` project rows (113-114), `decisions/README.md` ADR-0033 row (32), SUP-859 for `ArtifactSurfaceLiveTests.cs` (exactly one row).
- Tests written: `ArtifactFrameTests.cs`, `Cowork/Artifacts/ArtifactMarkdownHostileTests.cs`, fixture `md/script-link.md`, `ArtifactRenderRouteTests.cs`, `ArtifactClientRouteTests.cs`, `DemoArtifactFixturesTests.cs`, `EmbeddedMermaidAssetProviderTests.cs`, plus the CSP and service-worker rows above.

## Cross-plan edit (disclosed)

- `src/Autonogrammer.Cowork.Artifacts/Autonogrammer.Cowork.Artifacts.csproj`: added `InternalsVisibleTo Autonogrammer.UI`, so the web components reuse the shared `CsvReader`, `ImageValidator`, `SvgSanitizer` and `HtmlDocumentWrapper` as the desktop does. This file is not in 71-06's files_modified list; it mirrors the existing Native.Views friend entry.

## Deviations and interpretations

- Artifact header and list DTOs are web-side records (`ArtifactWebJsonContext`), not Contracts types: no Contracts DTO exists for them. So "round-trips through the Contracts JSON contexts" is satisfied against the web context instead.
- Sync-off is inferred from a 404 on the content route (the API has no explicit sync-off status). It is a mapping, not a server contract.
- Mermaid in the web is a srcdoc that inherits the page CSP and loads the two scripts beside the page, with no meta policy (a meta `script-src` without 'self' would block them).
- The ticket reuse rule (55 s) is a client-side refresh, not a server contract.
- Bundled-library enumeration (VERSION.txt) is verified only for the banners in the bundle (DOMPurify, lodash-es, cytoscape) and the top-level runtime dependencies; the rest is listed from package.json, not checked in the bundle.
- The elkjs and DOMPurify copyright header lines in the licence files are written from upstream banners, not from a verified year.

## Not verified (must run by the integrator)

- `dotnet` build of Cowork.Artifacts, UI, Web, Native.Views, Native.Tests (`--filter-class` for Artifact*, CssContractTests, RenderCspScriptTests, ServiceWorkerRulesTests, DemoArtifactFixturesTests, ArtifactClientRouteTests, ArtifactRenderRouteTests, EmbeddedMermaidAssetProviderTests, ArtifactFrameTests, ArtifactMarkdownHostileTests).
- bUnit calls `LoadAsync(new ProgressEventArgs())` on the iframe and the `@onload` binding are assumed to dispatch; not run.
- `ArtifactBody.razor` and `ArtifactTable.razor` compile against internal Cowork.Artifacts types through the new friend entry; not compiled.
- `artifact-page.mjs`: not run. Sandboxed frames that Chrome places in another process would emit network events on a different CDP session that this script does not attach to; the outbound check would miss them.
- `OUT_DIR="$(mktemp -d)" bash scripts/web-smoke/run-smoke.sh` (all three halves) not run.
- `check-skip-markers.sh`, `check-licences.sh`, `check-sbom.sh`, `check-changelog.sh`, `check-docs.sh`, ArchitectureTests not run.
- `vercel.ts` evaluation with the new `frame-src` call not run.

## Blockers

- Owner/legal: OQ-400 admission remains an owner decision. Plan default (admit) is applied; if refused, delete `lib/mermaid/`, the EmbeddedResource item and the EPL/ISC additions.
- OQ-401: interactive Html is only as available as the render route and the API origin (OD-03); the web shows static view otherwise.
- Not owned here: the Cowork view that mounts these components (69/70) does not yet call `ArtifactWebClient`, so the web surfaces are not reachable from the UI until mounted.
