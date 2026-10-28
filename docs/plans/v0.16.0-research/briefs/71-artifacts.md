# Brief 71: Cowork Artifacts

Status: design brief, milestone v0.16.0. Record: [ADR-0033](../../../decisions/ADR-0033-cowork-artifacts.md). Every name is post-rename (Autonogrammer.*, `services/Autonogrammer.Api`) although the tree on disk still says OmpOS; the external omp names are unchanged.

## Goal

Cowork shows what the agent makes (markdown, code, HTML pages and small apps, SVG, Mermaid, CSV, images) in a panel beside the chat, as immutable hash-chained versions the user can diff, copy, edit, export and list from any signed-in head. Generated content is untrusted: it renders only in-process through renderers that cannot execute it, or in an isolated surface with a restrictive CSP, no network by default, no navigation and no bridge to the app.

## Requirements

- AG-71-1: An artifact has a host-generated id, a fixed kind (Markdown, Code, Html, Svg, Mermaid, Csv, Image), a validated title and media type, and an optional session link.
- AG-71-2: Versions are write-once, numbered from 1, each with a content SHA-256, a parent number and parent hash, an author set by the host, and a change note.
- AG-71-3: Update requires the base version, rejects a stale base with the latest number, and returns the existing version when the new bytes hash the same.
- AG-71-4: Every read re-verifies the hash and the parent link and refuses to render a version that fails.
- AG-71-5: Copy creates a new artifact with `DerivedFrom`; a user edit of a text kind saves a new version with author User.
- AG-71-6: Export writes one version as a file or an artifact as a deterministic zip, with safe defaults (sanitised SVG, neutralised CSV formulas, restricted HTML wrapper) and sanitised entry names.
- AG-71-7: Diff of text kinds uses DiffPlex under line and size caps and is cancellable; binary kinds diff as metadata.
- AG-71-8: The `artifact` tool family (`create`, `update` with `edits`, `read`, `list`) is provided through the host-owned MCP gateway with typed error codes, a write rate limit and `from_file` under the folder grants.
- AG-71-9: Limits (size per kind, versions, artifacts and bytes per account, title) are validated options and fail with typed results, never truncation.
- AG-71-10: Markdown renders through the existing Markdig no-HTML pipeline; Code through read-only AvaloniaEdit or escaped `pre`; neither runs script.
- AG-71-11: SVG passes an in-house XML allowlist sanitiser (DTD prohibited, no external reference, no script, no event attribute) and shows as an inert image.
- AG-71-12: CSV renders from a capped RFC 4180 preview as text cells with formula-like cells marked.
- AG-71-13: Images are validated by magic number, size and pixel caps before display and are never SVG.
- AG-71-14: Html and Mermaid run only in an isolated surface whose CSP is produced by one tested `ArtifactDocumentPolicy`.
- AG-71-15: The desktop surface uses NativeWebView with the post-message bridge off, a private non-persistent store, cancelled navigation and new windows, and no message subscription.
- AG-71-16: The web head uses a sandboxed iframe: `srcdoc` for static Html and Mermaid, and an API-served ticketed render document with its own response CSP for interactive Html.
- AG-71-17: Network access is requested per version, shown to the user, approved per session or per version, default off; storage access is not offered.
- AG-71-18: The surface is destroyed when hidden, stopped or signed out, and every blocked navigation is reported.
- AG-71-19: A local file store is authoritative on desktop; the API keeps the account listing; sync is one setting, off by default (metadata only until the user enables it; the web head shows a "sync is off" state for the artifact list), idempotent by hash, and a divergent number forks to a copy.
- AG-71-20: API routes are owner-scoped (another owner's id is 404), content is always served as an attachment octet-stream, and render tickets are 60 s, version-bound HMACs.
- AG-71-21: The panel is a labelled region with a named toolbar, a polite live region, non-colour diff markers, focus escape controls and no new colour role.
- AG-71-22: `ArtifactBlock` is an additive transcript block that opens the panel and degrades to `UnknownBlock` in old readers.
- AG-71-23: A hostile payload corpus is test data for every kind, every renderer and every tool argument, and every entry has an asserted outcome.
- AG-71-24: Create, update, grant and revoke emit a counter and an audit event (`artifact.created`, `artifact.updated`, `artifact.grant`, `artifact.revoke`, in the closed `cowork_events.kind` list fixed by phase 68 U3) carrying hashes and origins, never content, titles or account ids.

## Depends on

ADR-0030 (account id, signed-in gate, `SessionSurface`), ADR-0031 (`autonogrammer` schema, data source, migration runner, event sink), ADR-0032 (`Autonogrammer.Cowork.Abstractions` L1: `ICoworkToolProvider`, `CoworkToolDescriptor`, taint, folder grants; the `SurfaceHost` shell whose `CoworkDockHost` (brief 69 unit 3) hosts the artifact panel as `IDockPanel` content in a tab strip beside the Browser panel; `ToolUpdateImage` and `ImageBlock`, owned by brief 70 unit 5 and not duplicated here), the rename tool (applied later), ADR-0009 (WebView on Linux), ADR-0010 (gateway route, unverified), ADR-0007 (AvaloniaEdit), ADR-0008 (contract versioning).

## Projects and files owned

New: `src/Autonogrammer.Cowork.Artifacts` (L2, pure, no IO, web-safe) with `tests/Autonogrammer.Cowork.Artifacts.Tests`; `src/Autonogrammer.Cowork.Artifacts.Stores` (L2: file store, Postgres store, sync client) with `tests/Autonogrammer.Cowork.Artifacts.Stores.Tests`. New folders in existing projects: `Autonogrammer.Contracts/Artifacts`, `Autonogrammer.Presentation/Cowork/Artifacts`, `Autonogrammer.Native.Views/Views/Cowork/Artifacts`, `Autonogrammer.UI/Components/Cowork/Artifacts`, `services/Autonogrammer.Api/Cowork/Artifacts`, `apps/Autonogrammer.Web/Services/Artifacts`.
Existing files touched (registry rows are created by the scaffold step, `00-scaffold-manifest.md`; this phase adds code and edits only the rows the manifest marks 'per phase'): `Contracts/Ui/UiBlockConverter.cs`, `Native.Views/ViewMapping.cs` and csproj, `Directory.Packages.props`, `Autonogrammer.slnx`, `Autonogrammer.Linux.slnf`, `LayerMap.cs` (Artifacts and Stores L2; same-layer tuples Stores to Artifacts and UI to Artifacts, each with an `// intentional:` comment), `ArchitectureTests.csproj`, `Api/Program.cs` (two lines), `docs/api/openapi.json`, `scripts/vercel/render-csp.sh`, `vercel.ts`, `scripts/vercel/build-demo.sh`, `scripts/web-smoke/*`, `apps/.../Demo/**`, lock files, `THIRD-PARTY-NOTICES.md`, `scripts/legal/*` (only if OQ-400 admits Mermaid).

## Work units

| Unit | Owner project and files | Tests | Acceptance |
| --- | --- | --- | --- |
| U1 Contracts and core | `Cowork.Artifacts` model, `ArtifactService`, `IArtifactStore`, `InMemoryArtifactStore`, options and validator, export, diff; `Contracts/Artifacts/*`, `ArtifactBlock` | `Artifacts.Tests` Model, Service, Export, Diff | AG-71-1 to 7, 9, 22: stale base, idempotent same hash, integrity failure, copy, zip determinism, diff caps, old reader gets `UnknownBlock` |
| U2 Safe content pipeline | `Cowork.Artifacts/Rendering/**`: `SvgSanitizer`, `CssCheck`, `CsvReader`, `ImageValidator`, `ArtifactDocumentPolicy`, `HtmlDocumentWrapper`, `MermaidDocumentBuilder`, `IMermaidAssetProvider`; `Fixtures/hostile/**` | `Artifacts.Tests/Rendering/**` | AG-71-10 to 14, 23: every corpus entry has its asserted outcome; CSP strings match the ADR text exactly; no network origin reaches `script-src` |
| U3 Stores and sync | `Cowork.Artifacts.Stores`: `FileArtifactStore`, `PostgresArtifactStore` (no migration: M0006 `cowork` is written by phase 68 U3 and carries `artifacts`, `artifact_versions`, `artifact_grants`), `ArtifactSyncService`, `HttpArtifactRemote` | `Stores.Tests` (temp dir; throwaway TimescaleDB) | AG-71-4, 19: atomic write-once files, per-account folders, 409 fork, offline queue, store round-trip against the M0006 tables, content cap 5,242,880 bytes enforced, every query filters by `account_id` |
| U4 Tool provider and API | `Cowork.Artifacts/Tools/**` (`ArtifactToolProvider`, `EditApplier`, write limiter); `Api/Cowork/Artifacts/**` (options `Cowork:Artifacts`, routes under `/cowork/artifacts`, ticket service, `/cowork/artifact-render/{ticket}`, grants, `cowork_disabled` pattern; the OpenAPI transformer lists the render route as `security: []` with its own limiter partition) | `Artifacts.Tests/Tools/**`, `Api.IntegrationTests/Artifacts/**` | AG-71-8, 17, 20, 24: `artifact_create`/`artifact_update` return `Preview.Grant = ("artifact.write", "cowork")` (first write asks once in Manual, Allow always persists, tainted sessions still ask), `edits` unique match, `from_file` outside grants refused, 404 for other owner, ticket expiry and version binding, content never `text/html`, OpenAPI fresh |
| U5 Presentation and desktop | `Presentation/Cowork/Artifacts/**` (panel, list, diff, access-review, `IArtifactSurface` view models and states), `Native.Views/Views/Cowork/Artifacts/**` (panel, `NativeWebViewArtifactSurface`, DataGrid table, image), packages | `Presentation.Tests`, `Native.Tests` (fake surface; automation names; no message subscription) | AG-71-15, 17, 18, 21: states and degraded text per case, grants bound to version, focus escape controls, no colour literal |
| U6 Web head | `UI/Components/Cowork/Artifacts/**`, `wwwroot/styles/artifacts.css`, vendored `wwwroot/lib/mermaid/**` and host script, `Web/Services/Artifacts/**`, Demo fixtures, CSP scripts, smoke page | `UI.Tests`, `Web.Tests`, web smoke | AG-71-16, 18, 21: iframe attributes exact, second `load` destroys frame, static-view label without API origin, axe clean, Demo contract tests |

Order: U1, then U2 U3 U4 in parallel, then U5 U6.

## Success criteria

- `dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release --filter-not-trait "Category=Live"` passes, including every hostile corpus entry.
- `dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Stores.Tests -c Release --filter-not-trait "Category=Live"` passes with `scripts/test-services.sh up` sourced.
- `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Artifact*"` passes and `docs/api/openapi.json` is fresh.
- `dotnet test --project tests/Autonogrammer.Presentation.Tests`, `Native.Tests`, `UI.Tests`, `Web.Tests` with `-- --filter-class "*Artifact*"` pass.
- `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` passes (LayerMap, graph, decision records, suppression ledger).
- `OUT_DIR="$(mktemp -d)" bash scripts/web-smoke/run-smoke.sh` passes with the artifact page: no outbound request, no navigation, no parent access from the hostile fixtures.
- `bash scripts/ci/check-licences.sh` and `python3 scripts/legal/generate-third-party-notices.py --check` exit 0.

## Threat model bullets

- Script from artifact reaching the app (elevation): the one rule of ADR-0033 D1; no artifact-derived markup in the app DOM except Markdig output, escaped text and image data URIs.
- Exfiltration (disclosure): CSP `default-src 'none'`, no network by default, grants per immutable version, origins never reach `script-src`; DNS rebinding stays open.
- Bridge abuse: NativeWebView bridge off, no message subscription, probe fixture; web frame never calls `postMessage`.
- Stored XSS on the API origin: content route is attachment octet-stream with `nosniff`; render document has a CSP `sandbox` header and `frame-ancestors`.
- Ticket theft: 60 s, HMAC, version and hash bound, path segment redacted from logs, no referrer.
- Tampering and rollback: write-once versions, hash and parent re-check on read, server recomputes hashes, CAS update.
- Spoofing: title bidi and control characters rejected; author set by the host; punycode shown in the access dialog.
- Denial of service: size, depth, row, pixel and diff caps, XML character limit, regex timeouts, surface destroyed when hidden, write rate limit.
- Export hazards: SVG, CSV and HTML export default to the restricted form.
- Supply chain: Mermaid pinned by SHA-256 with a hash test; update needs an ADR amendment.

## Degraded and error states

Not found or deleted; integrity failed (not shown); too large; quota exceeded; stale base (reload or save as copy); unknown kind (metadata and export only); surface unavailable with reason (engine missing, platform not supported, API origin not configured, Demo build, ticket route not public, scripts blocked by the inherited CSP: "static view"); scripts disabled by setting; navigation blocked (surface destroyed); network pending, denied or revoked; Mermaid asset missing (source shown); SVG rejected (source shown, findings listed); CSV or diff over cap ("showing first N"); sync off (the default; the web head's artifact list shows "sync is off"), sync pending, failed or diverged; render ticket expired (one automatic refresh); signed out (sign-in required); API offline (retry).

## Cannot be verified on this host

Windows WebView2, macOS WKWebView and Android WebView behaviour (compile-check only); CSP-by-meta, navigation cancel and bridge option on any engine beyond what a private Xvfb run on WebKitGTK shows (ADR-0009 measured load and selector only); Firefox and Safari sandbox behaviour (only headless Chrome 153 here); screen readers (no NVDA, Orca registry or VoiceOver; structural automation-name and axe checks only); cross-origin framing of the real Railway API from the real Vercel origin; live Supabase and Railway; load time of a 5.2 MB inline script; legal admission of EPL-2.0.

## Open questions

| ID | Question | Why it matters | Default taken | Owner | Status |
| --- | --- | --- | --- | --- | --- |
| OQ-400 | May the vendored `mermaid.min.js` 12.2.0, which embeds `elkjs` (EPL-2.0) and unaudited d3 and other libraries, ship? | EPL-2.0 is not on the admitted list; the notices generator only knows MIT, BSD, Apache, PostgreSQL | Admit one unmodified vendored file, enumerate its libraries before commit, extend `scripts/legal`; if refused, ship the Mermaid kind as source view | product owner, legal | open |
| OQ-401 | Is the ticketed API render document for interactive Html in scope before the API has a public origin? | Needs OD-03 and `frame-src` in the web CSP | Static view on the web until then; desktop runs scripts | product owner | open |
| OQ-403 | Do artifact scripts run on open, or only after a per-artifact click? | CPU burn and phishing pages from injected content | `OnOpen`, with Stop, destroy-on-hide and the labelled frame | product owner, A10 | open |
| OQ-404 | Is persistent artifact storage (browser, file, account API) wanted later? | Needs a bridge or a per-artifact origin the platform lacks | None in v0.16.0; `permissions.storage` accepts `none` only | product owner | open |
| OQ-405 | Which contract version carries `ArtifactBlock`? | Nothing else says who sets the number | Additive; the scaffold step bumps `ContractVersion` 1.5.0 to 1.6.0 once and no phase edits `ContractVersion.Current` | orchestrator | open |
| OQ-406 | Does `from_file` use the ADR-0032 folder grants, and does the artifact panel register with `CoworkDockHost` as `IDockPanel` content (tab strip beside Browser)? | Decides the path check and one registration | As assumed; ADR-0032 names `ICoworkToolProvider` in L1 `Autonogrammer.Cowork.Abstractions`, so `Cowork.Artifacts` references it as a lower layer | ADR-0032 owner | open |
| OQ-407 | Are 500 artifacts, 256 MiB per account and 200 versions the right quotas? | Storage cost and abuse bound | These values, as options | product owner | open |
| OQ-408 | When do Windows, macOS and Android runners measure the surface? | Until then those heads show source for Html, Svg and Mermaid when the surface fails | Option `Cowork:Artifacts:Surface` = `Auto`, fallback to source | owner provides runners | open |
| OQ-409 | Is "ask the agent about this selection" in scope? | Needs a composer selection-attachment contract from ADR-0032 | Deferred | ADR-0032 owner | open |
| OQ-410 | Should `read` return image blocks to the model? | Needs tool-result image support in the transcript (report 07 D7) | Metadata only | engineering | open |
| OQ-411 | Can native JavaScript dialogs of the desktop control be suppressed by an option? | They could be mistaken for app dialogs | Prefix script that no-ops them; verify on the live run | engineering | open |

## Index rows

ADR index (`docs/decisions/README.md`): `| [ADR-0033](ADR-0033-cowork-artifacts.md) | Cowork Artifacts: model, versions, tool family and the isolated renderer | Proposed 2026-10-28 | Immutable hash-chained versions; `artifact` tool family; isolated surface with CSP, no bridge, no network by default; srcdoc inherits the page CSP (CSP3 7.8) so interactive web Html needs a ticketed render document; Mermaid 12.2.0 pinned, EPL-2.0 elkjs inside (OQ-400); live artifacts omitted |`
OQ rows: OQ-400 to OQ-401 and OQ-403 to OQ-411 above go into `docs/open-questions.md` (final block OQ-400 to OQ-419, assigned to phase 71 by the design integrator).
