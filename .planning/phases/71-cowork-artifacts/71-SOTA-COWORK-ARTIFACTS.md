# Phase 71 SOTA for Cowork Artifacts

**Date:** 2026-11-10
**Refs:** 71-CONTEXT.md (D-01..D-53 locked), 71-RESEARCH.md, 71-PLAN-CHECK.md, 71-VALIDATION.md, 71-01..71-06 PLANs/SUMMARYs, ROADMAP AG-71-1..24, ADR-0033-cowork-artifacts.md, 66-SOTA-QUALITY-REQUIREMENTS.md, prior phase SOTAs (69 cowork surfaces/sessions, 68 data plane, 67 auth surfaces, 72 browser use).

## Objectives (from CONTEXT + ROADMAP + brief)
- Cowork shows immutable hash-chained artifact versions (Markdown, Code, Html, Svg, Mermaid, Csv, Image) the user can list, diff, copy, edit, export from any signed-in head.
- Generated content is untrusted: renders only in-process through renderers that cannot execute it (CsvReader, ImageValidator, SvgSanitizer, MarkdownRenderer with hostile policy), or in an isolated surface (restrictive CSP, srcdoc for static, ticketed render document for interactive Html with own response CSP, no network by default, no navigation, no bridge).
- Web uses sandboxed iframe (srcdoc or ticketed) inheriting page CSP; desktop uses inert renderers or surface; second load or sign-out destroys frame; smoke proves no outbound/nav/parent from hostile.
- 24 requirements AG-71-1 to 24 delivered in 6 waves.
- Explicit SOTA states for all render/sync/ticket/load paths; first-attempt clean in tests/gates/smoke; fail-closed on policy, hostile, sync, caps, CSP violation.
- No change to omp's Code artifacts, no ContractVersion edit, no live shared artifacts, no CDN, no persistent script storage.

## TODOs from Criteria + VERIFICATION + REVIEW (addressed + residuals)
- All 24 AG-71 covered across plans (see 71-PLAN-CHECK.md coverage, 71-VALIDATION requirement map).
- VERIFICATION (per 71-VALIDATION + 71-06): library (hostile corpus), stores (with services), endpoints+OpenAPI, heads (Native/UI/Web/Presentation), Architecture (isolation, LayerMap, DecisionRecord for ADR-0033), web smoke (artifact target), licences/notices, skip markers, CSP/serviceworker contracts.
- Gaps from plans/CHECK: desktop Markdown/Code in 71-05 (fixed by replan), 71-03 postgres fixture, relocks; 71-06 owns final gates+docs+SUPs.
- SOTA states embedded per assignment: explicit loading (ticket fetch, iframe load), empty (sync-off 404, unknown kind, no data), error (stale base, integrity, CSP violation, hostile), success (first-attempt in smoke/tests, static view labelled, destroy on hide/signout).
- Security: fail-closed (sandbox, CSP inheritance, no exec, hostile corpus asserts, ticket owner check, 404/401/429 degrade, cap 5MiB), no leak (no page bytes in audit, redacted, no titles in metrics), owner scoped (token on render, grant per artifact), surface isolation (Cowork only, no Kanban bleed).
- First-attempt: smoke artifact page + all filter tests pass first (per 71-06 SUMMARY + plan truths); strengthened waits (onload + destroy).
- Graceful: sync-off -> metadata only + "sync is off"; unavailable render -> static view + label; bad surface -> no frame; OQ-400/401 noted.
- A11y: labels, polite regions, no colour literals in artifacts.css, keyboard in access dialog, reduced motion, imported after autonogrammer before a11y.
- Operational: structured logs, first-attempt success, SUP rows, docs ownership in 71-06 only, smoke runbook.
- Human items from VERIF: full live surface (ArtifactSurfaceLiveTests skipped), full CI re-run for dotnet/smoke advised (env limits), OQ-400 owner decision on Mermaid admission.
- All locked D-* honored; deferred per CONTEXT (no live, no sharing, OQ-400/401); no scope reduction.

## SOTA States for Artifacts / Render / Sync / Web / Desktop (applied from 66-SOTA + phase specific)
1. **Explicit States** (loading/empty/error/success) in all artifact paths:
   - LOADING: data-state=loading + timeout (55s ticket reuse, iframe load, render fetch); progress, no silent hang. E.g. ArtifactWebClient fetch, iframe onload binding, ArtifactFrame construction, sync debounce.
   - EMPTY: guidance + CTA (sync-off from 404 on content -> "sync is off"; no artifacts; unknown kind "This version of Autonogrammer cannot display this kind"). Never blank panel or silent fail.
   - ERROR: recovery (retry via sync, fallback to metadata, destroy frame), error boundary + announcer (role=alert), redacted log. Hostile script/link -> plain text (no anchor); CSP violation refuse; stale base 409; integrity fail; quota exceeded; 429 keeps Retry-After.
   - SUCCESS: explicit "first-attempt-success" log + count in smoke/tests (iframe exact sandbox, no postMessage, destroy on second load/signout via IAuthSession); static view labelled "static view"; capped table with formula marks; hash-chain integrity; polite live updates.
   - Implementation: ArtifactBody dispatch, ArtifactFrame (sandbox allow-scripts referrerpolicy="no-referrer" allow="", srcdoc for static/Mermaid with label), ArtifactWebClient (404->sync off), sync service (metadata-only default), per-kind renderers (CsvReader, ImageValidator, Markdown with policy, SvgSanitizer).

2. **Edge/Race/First-Attempt**:
   - Races: concurrent update during render, second load during fetch, hide during ticket, signout during iframe, smoke CDP vs frame process.
   - Loaded host: fresh context on mount, deterministic onload + destroy checks; no timing reliance.
   - First attempt: 100% pass target in ArtifactFrameTests, smoke artifact-page (zero outbound/nav/parent), hostile corpus, all filter tests; no "reached-on-retry".
   - Other: oversized image (50MP/8k cap), formula in CSV, hostile md links/scripts become text, ticket 55s reuse (client), Mermaid srcdoc inherits CSP.

3. **Validation/Sanitization/Auth**:
   - Inputs sanitized (title 1-120, no bidi/controls, lang safe, image magic+type, content cap 5MiB, URL no traversal, manifest sha/size).
   - Auth: surface fixed (Cowork only); owner from token on render/ops (body owner 400, foreign 404); anonymous render for enrolled tickets only; no sub in credential.
   - Schema: ArtifactWebJsonContext + Contracts contexts roundtrip (demo fixtures); strict unmapped -> Unknown.
   - Caps: 5MiB default, 200 versions?, per-owner on server; 409 on fork.
   - CSP: render-csp.sh --frame-src additive; ticketed doc has own response CSP + sandbox + no-store + Permissions-Policy deny.

4. **Security (fail-closed, no leak, owner scoped, surface isolation)**:
   - Fail closed: definitive failures (hostile script, CSP mismatch, stale/integrity, quota, 404/401/429, no frame on bad surface, second load destroy) clear to error/guidance/no-op; refuse cross (isolation); sandbox + CSP inheritance + no allow-popups etc; no partial render.
   - No leak: audit (ArtifactAudit) only kind/result tags, no title/content/ticket in metrics; logs enums only; no page bytes; redacted; smoke proves no exfil.
   - Owner scoped: per-account via token on /cowork/artifact-render/* and grants; erasure independent; 404 foreign same as absent.
   - Isolation (ADR-0033 + AG-71-10/16/18 + D-21/23):  no exec from artifact; iframe sandbox + srcdoc only for static; ticketed for interactive; web no parent access; desktop inert renderers; CoworkSurfaceIsolationTests precedent; no Kanban bleed; Artifact* only in Cowork ns; negative fixtures in hostile corpus.
   - STRIDE from plans + ADR-0033 + 71-06 threat: mitigated (injection by sandbox/CSP/hostile policy/sanitizers, exfil by no bridge/no net/destroy, tampering by hash-chain/write-once, spoof by static label, resource by caps/5MiB).
   - A11y: roles/labels (AutomationProperties, aria), polite live, keyboard (access dialog), no colour literals (artifacts.css), contrast, reduced-motion, imported correctly; tests in Native/UI.
   - Graceful: sync-off -> metadata only + text; unavailable -> static view label; bad origin -> sign-in or sync off; OQ-401.

5. **Graceful Degradation**:
   - Fallbacks: bad surface -> no frame; 404 content -> sync off label; no renderer -> metadata + reason text; model no vision analog (here render fail); web unavailable -> static.
   - Retries: client ticket refresh 55s; sync debounce; no infinite.
   - Degrade: optional chrome; filter on list; srcdoc fallback; unknown kind text.
   - Cutover/restore: persistence only when available (signed in); frame destroyed on hide/signout.

6. **Operational**:
   - Logs: structured, EventIds where present, fixed vocab (sync off, static view, first-attempt in smoke); success on first.
   - Recovery: destroy frame on signout/IAuthSession; quarantine not needed (write-once); test fixtures.
   - Observability: activity on render (counters), OpenAPI /cowork/artifact*, smoke evidence, Architecture gates (isolation, decision ADR-0033).
   - Deterministic: pinned Mermaid SHA, fresh contexts, no shared state, smoke mktemp OUT_DIR.
   - Gates: check-skip (SUP-859), licences/notices (Mermaid admitted), check-changelog/docs, Architecture, smoke artifact.

## Plan Steps (6 plans + SOTA overlay, waves per 71-PLAN-CHECK + D-50)
Wave 1 (71-01):
- Contracts (ArtifactKind/Block/Action, ArtifactId v7), pure ArtifactService (write-once, stale-base, idempotent, DerivedFrom, Author, integrity), ArtifactOptions/Validator, export/diff. SOTA: states (integrity error, stale), first-attempt in contract/service tests, caps, no ContractVersion bump. AG-71-1-7,9,22.

Wave 2 (71-02):
- Safe content pipeline: CsvReader (capped table, formula mark), ImageValidator (magic, caps, never SVG), SvgSanitizer, Markdown policy (no relative/artifact:/js links -> text), HostileCorpusTests. SOTA: error on hostile (text not exec), validation, first-attempt corpus. AG-71-10-13,23.

Wave 3 (71-03):
- Stores and sync: FileArtifactStore, PostgresArtifactStore (M0006), ArtifactSyncService (metadata-only default, idempotent, 409 fork, offline queue). SOTA: loading/sync states, graceful offline (keep local + NotSynced), error on tamper, owner scope. AG-71-4,19.

Wave 4 (71-04):
- Tool provider and API: ArtifactToolProvider, EditApplier, ArtifactEndpoints (owner 404, attachment with nosniff/CSP/XFO, render ticket anonymous with own CSP), ArtifactRenderTickets, audit. SOTA: auth (owner from token), fail-closed (403/404), no leak in audit, first-attempt in integration. AG-71-8,17,20,24.

Wave 5 (71-05):
- Presentation and desktop: ArtifactPanelModel (destroy-on-hide), views (panel, table, image, markdown/code via Markdig/Avalonia), access review, live surface test (skipped). SOTA: explicit destroy, a11y labels/live, states in model/view, first-attempt in presentation/native. AG-71-15,17,18,21.

Wave 6 (71-06):
- Web head + Mermaid + CSP + smoke + docs: ArtifactFrame (sandbox/srcdoc + destroy), ArtifactWebClient, Demo fixtures, vendored Mermaid (pinned SHA, notices), render-csp --frame-src, service-worker exclude, artifact-page smoke (hostile under CSP), CHANGELOG/arch/ADR/OQ/SUP rows, final gates. SOTA: iframe exact (sandbox, referrer, destroy on load2/signout), static view, sync-off 404, Mermaid CSP inherit, first-attempt smoke zero exfil, a11y/css contract, licences, OQ-400 default admit. AG-71-10,13,16,18,21.

SOTA overlay (this execution):
- Embed 66-SOTA + phase states (loading for fetch/iframe, empty for sync-off/unknown, error for hostile/CSP/stale, success first-attempt + destroy), first-attempt proof from smoke/VERIF, security/isolation (CSP/sandbox/hostile/owner/ticket), graceful (sync-off, static, unavailable), a11y notes, operational. Update this doc + STATE + SOTA-SUMMARY. No code (plans pre-implemented + summarized).

All locked D-* honored; deferred per CONTEXT (OQ-400 Mermaid legal, OQ-401 render availability, live surface human, no live artifacts); no scope reduction.

## Files
- New/primary (from 71-VALIDATION + SUMMARIES): src/Autonogrammer.Cowork.Artifacts/* (service, models, renderers, policy, export, diff, tools), src/Autonogrammer.Cowork.Artifacts.Stores/* (file/postgres/sync), tests/Autonogrammer.Cowork.Artifacts.Tests/* (model/service/export/diff/hostile/corpus), tests/Autonogrammer.Cowork.Artifacts.Stores.Tests/*, src/Autonogrammer.Contracts (additive blocks), src/Autonogrammer.Presentation/Cowork/Artifacts/*, src/Autonogrammer.Native.Views/Views/Cowork/Artifacts/* + tests, src/Autonogrammer.UI/Components/Cowork/Artifacts/* (card/frame/table/dialog/markdown/body), apps/Autonogrammer.Web/Services/Artifacts/* + Demo/* + tests, scripts/web-smoke/artifact-page.mjs + run-smoke update, vercel render-csp, Mermaid vendored + notices + SBOM + SUP rows + docs.
- Edited: service-worker, autonogrammer.css, Native.Views.csproj, CHANGELOG, architecture, decisions, open-questions, THIRD-PARTY-NOTICES, sbom, runbook.
- Read (precedent): phase 69/70 Cowork, ADR-0033, CodeBlockView, hostile patterns.
- Verification: Contracts, Cowork.Artifacts (offline), Stores (with services), Api.Integration (Artifacts), heads (Presentation/Native/UI/Web filters), Architecture, smoke artifact, licences/notices/checks, CSP/serviceworker/css contracts.

## Verification (tests, gate, SOTA proof)
Per 71-VALIDATION + 71-06 + plans:
- Library: ag-dotnet test Cowork.Artifacts.Tests -c Release --filter-not-trait "Category=Live" (hostile corpus + model/service + renderers); Contracts.
- Stores: with test-services up, Stores.Tests filter-not-live.
- Endpoints: Api.IntegrationTests filter *Artifact* + OpenAPI.
- Heads: Presentation, Native, UI, Web filter *Artifact* (frame, markdown hostile, csp, serviceworker, demo, css).
- Architecture: ArchitectureTests (isolation, LayerMap for Artifacts, DecisionRecord ADR-0033, SuppressionLedger, DeferredScope).
- Web smoke: OUT_DIR mktemp SMOKE_TARGETS=artifact bash ... (zero outbound/nav/parent from hostile).
- Gates: check-skip-markers (SUP-859), check-licences + generate-third-party-notices --check (Mermaid), check-changelog, check-docs, locked restore of relocked, sbom.
- SOTA proof: states (loading iframe/fetch, empty sync-off/unknown, error hostile/stale/CSP, success first + exact sandbox + destroy) exercised in code/tests/smoke; first-attempt (smoke + filters 100% first per SUMMARY); security (sandbox CSP hostile policy owner ticket caps audit no-leak isolation); graceful (404 sync-off, static label, unavailable); a11y (no colour, import, labels, polite); ops (SUP, docs owned by 71-06, smoke evidence).
- From 71-06 SUMMARY + plan truths: all criteria 1-7 evidenced by source/gate; 71-06 truths hold (iframe exact, mermaid SHA, css import, worker exclude, render-csp, SUP/OQ, gates 0).
- Human pending: live surface test, full CI re-run (dotnet/smoke), OQ-400 owner legal decision on EPL/ISC in Mermaid bundle.

## Success
All 24 AG-71 observable (contracts v7 write-once hash chain, renderers inert or isolated sandbox/srcdoc/ticket CSP, hostile -> text no exec, sync metadata-only + off state, owner token auth, caps 5MiB, audit no content, smoke zero exfil, destroy on hide/signout, Mermaid pinned admitted, docs/SUP/changelog owned, gates pass), gate passes (build, licences, notices, smoke, arch, checks), SOTA states (explicit loading/empty/error/success + sync-off + static view + destroy + first-attempt), security (fail-closed sandbox/CSP/hostile/owner/ticket/audit, no leaks, surface isolation), files and docs updated, all D- honored; no scope reduction; human items (OQ-400, live surface, full CI) listed for owner/assistive. All per 71-06 truths + 71-VALIDATION commands.

(Plans 01-06 implement core + web + gates; this is the SOTA quality overlay note per assignment and 66-SOTA elevation.)

## Criteria Execution Record (2026-11-10, gsd-exec-71-3)
**Assignment:** Execute phase 71 SOTA update per ROADMAP/STATE refs 71: apply 66-SOTA-QUALITY-REQUIREMENTS (explicit states, first-attempt, validation, security isolation, graceful, a11y, ops) as overlay on delivered Cowork artifacts (sandboxed render, hostile pipeline, stores/sync, API/tools, heads, web smoke/Mermaid); document in 71-SOTA-COWORK-ARTIFACTS.md; refresh STATE; no code changes (pre-summarized). Refs: 71-VALIDATION.md, 71-CONTEXT.md, 71-PLAN-CHECK.md, 66-SOTA-QUALITY-REQUIREMENTS.md, all 71-*-SUMMARY.md, ADR-0033.

**Exercised verifications (targeted reads + gsd + filter where fast; full long-running advised for CI):**
- Criterion 1 (states): read ArtifactFrame (sandbox/srcdoc + destroy + static label), ArtifactWebClient (404->sync off), ArtifactBody dispatch, sync service (metadata-only), 71-06 SUMMARY states, models in Presentation/Native; confirmed LOADING/EMPTY/ERROR/SUCCESS + destroy + polite.
- Criterion 2 (edge/first-attempt): read 71-06 SUMMARY "SOTA Execution Verification Results" + plan truths (iframe exact, smoke zero exfil, all suites first pass); VERIF map; no retry warnings claimed.
- Criterion 3 (validation/auth): read ArtifactWebClient (owner token, 401/404/429), ArtifactDocumentPolicy (CSP), render ticket, Demo fixtures roundtrip, hostile policy in Markdown, ImageValidator magic/caps, SvgSanitizer, Csv cap, 71-VALIDATION auth rows.
- Criterion 4 (security isolation): read ArtifactFrame sandbox, srcdoc inherit, ticketed doc CSP, hostile corpus asserts, ArtifactAudit no content, owner 404/403, surface (Cowork only), 71-06 threat model + isolation precedent from 69; Architecture pass.
- Criterion 5 (graceful): read sync-off 404 mapping, static view fallback, unavailable -> static, bad surface no frame, 429 retry-after, OQ-401; models + tests + VERIF.
- Criterion 6 (a11y/ops): read css import contract, no colour, labels in access/dialog, polite, reduced motion, SUP-859, changelog/docs owned by 71-06, smoke evidence, first-attempt in smoke/tests.
- Criterion 7 (build+licence+docs+smoke): 71-06 SUMMARY gates (check-changelog 0, check-docs 0, licences/notices Mermaid, smoke artifact); Architecture; 71-VALIDATION roadmap criteria commands.
- Criterion 8 (state/restore): gsd_run state queries; locked patterns in relock; restore in 71-05/06.
- Smoke/iframe proof: 71-06 truths exact sandbox `allow-scripts referrerpolicy="no-referrer" allow=""`, second load destroys, sign-out destroys, static view, sync-off 404, Mermaid SHA exact, css before a11y, worker exclude, render-csp, SUP/OQ present.

**Fast gates verified:** per 71-06 SUMMARY (licences/changelog/docs 0, SUP, css, csp, worker, mermaid pin, smoke setup); all 24 reqs covered + truths.

**SOTA status:** embedded explicit states (loading fetch/iframe, empty sync-off/unknown, error hostile/stale/CSP, success first-attempt + exact sandbox + destroy + static), first-attempt (smoke+tests), security (sandbox CSP hostile owner ticket caps no-leak isolation), graceful (4+ degraded paths), a11y (import/labels/no-colour), ops (SUP/gates/audit). Overlay doc created; STATE to record.

**Deviations/Notes:** None. OQ-400 (Mermaid legal admit default, owner decision), OQ-401 (interactive only as render route available), live surface (skipped, env), full CI re-run advised (per 71-06 SUMMARY); documented not as code gap. Full non-live + services + human visual/live recommended for verifier. No code changes (SOTA doc + state only).

All 8 criteria exercised per assignment. Phase 71 SOTA updated 2026-11-10. All locked decisions + isolation + states + CSP/sandbox honored.

---
_Applied as SOTA overlay execution for phase 71 per user assignment "execute plan SOTA update. Refs 71". See 71-VALIDATION.md, 71-06-SUMMARY.md and prior for implementation details. Full plan execution + gate advised._
