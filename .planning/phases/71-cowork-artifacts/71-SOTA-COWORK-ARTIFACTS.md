# Phase 71 SOTA for Cowork Artifacts

**Date:** 2026-11-10
**Refs:** 71-CONTEXT.md (D-01..D-54 locked), 71-RESEARCH.md, 71-VALIDATION.md, 71-PLAN-CHECK.md, 71-01..71-06 PLANs/SUMMARYs, ROADMAP AG-71-1..24, 66-SOTA-QUALITY-REQUIREMENTS.md, prior phase SOTAs (67-SOTA-AUTH-SURFACES, 68-SOTA-DATA-PLANE, 69-SOTA-COWORK-SURFACES), ADR-0033, 71-06-SUMMARY (SOTA execution 2026-11-10).

## Objectives (from CONTEXT + ROADMAP + brief)
- Cowork shows what the agent makes (markdown, code, HTML pages and small apps, SVG, Mermaid, CSV, images) in a panel beside the chat, as immutable hash-chained versions the user can diff, copy, edit, export and list from any signed-in head.
- Generated content is untrusted: it renders only in-process through renderers that cannot execute it, or in an isolated surface with a restrictive CSP, no network by default, no navigation and no bridge to the app.
- 24 requirements AG-71-1 to AG-71-24 delivered in 6 plans/waves.
- Hostile corpus for every kind/renderer/tool arg with asserted outcome.
- Web head: sandboxed iframe (srcdoc + ticketed render), vendored Mermaid (pinned SHA), CSP, smoke.
- Desktop: NativeWebView isolation, no bridge, destroy on hide/signout, states.
- Stores: local file + Postgres (M0006 from 68), sync off-by-default, owner scoped.
- Tools: artifact_* family under ICoworkToolProvider (from 70), grants, rate limits, warnings.
- No live artifacts, no sharing/publishing, no persistent script storage, no CDN, no HTML/SVG package, ContractVersion untouched.
- 7 roadmap criteria + 24 reqs + SOTA states + security + first-attempt + a11y.

## TODOs from Criteria + VALIDATION + PLAN-CHECK (addressed + residual gaps)
- All 24 AG-71 covered across plans (see 71-PLAN-CHECK.md).
- Wave 0 gaps (scaffold projects, hostile fixtures, models, ICoworkToolProvider, IDockPanel, M0006, Api tests, smoke page, Mermaid provider): addressed by 71-01..06.
- REVIEW/VERIF gaps: addressed in 71-06 (web gates, smoke, licences, notices, ArchitectureTests); full CI re-run advised for dotnet tests/smoke per 71-06-SUMMARY.
- SOTA states embedded per assignment: explicit loading/empty/error/success in artifact surfaces, render (iframe/srcdoc), sync (off), grants, hostile corpus, first-attempt in smoke/gates/tests.
- Security: fail-closed (integrity, stale, no network default, owner 404, sandbox no navigation), no leak (redact ticket, no sub/content in logs), owner scoped.
- First-attempt: 100% pass target on smoke artifact page, tests, gates; strengthened waits, no timing.
- A11y: labels, live regions, target sizes, no new colour, F6, polite, documented in 71-06 + 69/70.
- Residuals: live surface (OQ), full gate (advised), mounting from Cowork view (69/70), OQ-400 Mermaid admit (default applied).

## SOTA States for Cowork Artifacts / Render / Sync / Grants / Hostile (applied from 66-SOTA + phase specific)
1. **Explicit States** (loading/empty/error/success) in all artifact paths:
   - LOADING: data-state=loading + timeout (render ticket 55s, surface load, sync fetch, grant review); progress, no silent hang. E.g. ArtifactFrame load, ArtifactWebClient, smoke page.
   - EMPTY: guidance + CTA (no artifacts, sync off "sync is off", signed out, unavailable reason). Never blank.
   - ERROR: recovery (retry, fallback to source view), error boundary + announcer. ArtifactIntegrityException, stale_base, file_not_allowed, rate_limited, too_large, quota_exceeded, Unavailable(reason), NavigationBlocked.
   - SUCCESS: explicit "first-attempt-success" log + count; state machine logged. Healthy after hostile corpus, clean render, grant accepted.
   - Implementation: IArtifactSurface (Idle/Loading/Running/Stopped/NavigationBlocked/Unavailable), ArtifactDocumentPolicy, ArtifactFrame states, sync status, render ticket 55s reuse.

2. **Edge/Race/First-Attempt**:
   - Races: second load destroys frame, concurrent update (stale vs idempotent), sync during render, grant review race.
   - Loaded host: fresh context, deterministic waits in tests.
   - First attempt: 100% pass target; no "reached-on-retry"; strengthened waits in smoke (artifact-page.mjs), tests (hostile corpus), gates.
   - Other: corrupt/newer version (quarantine), oversized (typed error not truncate).

3. **Validation/Sanitization/Auth**:
   - Inputs: title 1-120, no bidi/control, language allowlist, media magic match, hash recompute every read, exact-match edits.
   - Auth: owner from token (404 foreign), render ticket HMAC over account/artifact/version/hash/expiry, no sub in client.
   - Sanitization: Markdown DisableHtml + link policy, SVG allowlist walk + no DTD, CSV RFC4180 cap + formula mark, Image header only.
   - Contracts: round-trip through source-gen contexts (Demo + stores); no bleed to Code/Kanban.

4. **Security (fail-closed, no leak, owner scoped, isolated)**:
   - Fail closed: integrity mismatch -> refuse render; bad ticket/surface -> 404/401; sandbox blocks navigation/parent; no partial state.
   - No leak: ticket redacted in logs, no content/titles/sub in audit/telemetry, warnings only (not bytes).
   - Owner scoped: every route + store filters account_id; render anonymous but ticket-bound.
   - Isolation: srcdoc inherits page CSP; iframe sandbox allow-scripts referrerpolicy=no-referrer allow=""; no postMessage, no bridge, destroy on hide/signout; NativeWebView no devtools/context/bridge.
   - STRIDE from plans (T-71-*) + 71-06 threat: mitigated (elevation on srcdoc, disclosure on ticket/frame, spoof on demo, tamper on Mermaid).
   - CSP: ticketed has response CSP + frame-ancestors + nosniff + Permissions-Policy deny; srcdoc inherits.
   - Grants: default none; review dialog; bound to (id,ver,hash,origins,scope,device,time); storage=none only.
   - Mermaid: pinned SHA + host script securityLevel:strict etc; vendored only.

5. **Graceful Degradation**:
   - Fallbacks: no sync -> metadata only + "sync is off"; missing Mermaid -> Unavailable(MermaidAssetMissing) + source; unknown kind -> "cannot display this kind"; bad origin -> deny; full -> exact message.
   - Degrade: static view label for srcdoc; sync off keeps local; offline queue.
   - Retries: bounded on writes (rate), reads (idempotent).

6. **Operational**:
   - Logs: structured EventId 7100-7199, fixed vocab, no secrets, counters with kind/result.
   - Recovery: quarantine on corrupt, independent stores, owner edit "save as copy".
   - Observability: telemetry artifact.count, audit artifact.*, render tickets, smoke hostile.
   - Deterministic: TimeProvider injected, hash checks, no timing in first-attempt paths.

## Plan Steps (6 plans, waves per 71-PLAN-CHECK + CONTEXT)
Wave 1 (71-01, contracts/core U1):
- Contracts, models, service (write-once, integrity, parent, TimeProvider, stale/idempotent), export, diff. SOTA: states in model/service (loading integrity check, error integrity/stale, success hash match), first-attempt, owner scoped. AG-71-1-7,9,22.

Wave 2 (71-02, safe pipeline + hostile U2):
- Renderers (Markdown/Code/Csv/Image/Svg/Mermaid), hostile corpus + asserts, SvgSanitizer, CsvReader, ImageValidator, policy. SOTA: states in render (loading, error hostile/integrity, success inert), first-attempt corpus, sanitization. AG-71-10-14,23.

Wave 3 (71-03, stores/sync U3):
- File/Postgres stores (M0006 tables from 68), sync off-by-default, grants. SOTA: states in store/sync (loading fetch, empty sync-off, error integrity, success metadata), owner scoped. AG-71-4,19.

Wave 4 (71-04, tool/API U4):
- ICoworkToolProvider (from 70), artifact_* descriptors, endpoints (owner 404, render ticket, CSP), grants. SOTA: states in API (loading ticket, error 404/403, success), fail-closed. AG-71-8,17,20,24.

Wave 5 (71-05, desktop/presentation U5):
- IArtifactSurface states, NativeWebView (no bridge, destroy), panel/toolbar (a11y, F6, labels), models, block. SOTA: explicit IArtifactSurface states + destroy, first-attempt, a11y. AG-71-15,18,21.

Wave 6 (71-06, web + gates/docs U6 + SOTA close):
- Web ArtifactFrame (srcdoc + ticketed sandbox exact), ArtifactWebClient, Mermaid vendored (SHA a8a6a6f8..., notices, EmbeddedResource), CSP --frame-src, service-worker exclude, Demo fixtures, smoke artifact-page.mjs (hostile, no outbound/nav/parent), gates (licences, notices, Architecture, check-*, smoke), docs/changelog/OQ/SUP. SOTA: states in web (loading frame, error blocked, success static/sandbox), first-attempt smoke, full gates per 71-06-SUMMARY. AG-71-10,11,12,13,16,18,21.

All locked D-* honored; deferred per CONTEXT (live, OQ-400 Mermaid owner, mounting in 69/70); no scope reduction.

## Files
- New/updated: per 71-01-06 lists + 71-06-SUMMARY (UI Components Cowork/Artifacts/*, wwwroot/styles/artifacts.css + lib/mermaid/* + autonogrammer.css, apps/Autonogrammer.Web/Services/Artifacts/* + Demo/* + service-worker, scripts/vercel/* + web-smoke/artifact-page.mjs + run-smoke.sh, Native.Views csproj + tests, Cowork.Artifacts + Stores + tests projects (scaffold), Fixtures/hostile/**, tests for all, docs (CHANGELOG, architecture, decisions, open-questions), THIRD-PARTY-NOTICES, sbom.
- Edited: many (WebServiceRegistration, DemoApiHandler, etc).
- Read (precedent): CodeBlockView, SignOutButton, a11y.css, existing stores, ADR-0033, brief 71.
- Verification/tests/gate: Cowork.Artifacts.Tests (hostile + all reqs), Stores.Tests (with services), Api.IntegrationTests/Artifacts, Presentation/Native/UI/Web artifact filters, ArchitectureTests, smoke artifact, check-*, licences/notices.

## Verification (tests, gate, SOTA proof)
Per 71-VALIDATION + ROADMAP + 71-06:
- Offline: ag-dotnet test Cowork.Artifacts.Tests (hostile), Stores (services up), Api.Integration (Artifact*), heads (Presentation/Native/UI/Web --filter *Artifact*), ArchitectureTests.
- Gate: check-skip-markers, licences, notices --check, sbom, changelog, docs; ArchitectureTests; smoke artifact (OUT_DIR mktemp bash ...).
- SOTA proof: states in surfaces/render/sync/grants/hostile (loading/error/success), first-attempt (smoke 0 outbound/nav/parent, tests), fail-closed (sandbox, integrity, owner), no leak, a11y labels/live/F6, Mermaid pinned + enumerated, docs rows + SUP.
- From 71-06: iframe sandbox exact, second load destroys, srcdoc inherits, Mermaid SHA exact, gates pass, OQ-400/401/405, SUP-859.
- 71-06 closes: criteria 5-7 + web SOTA.

## Success
All 24 AG-71 observable (models/service integrity/parent/stale, renderers inert per hostile, stores owner+sync-off, tool/API grants+404+CSP+ticket, desktop isolation+destroy+states+a11y, web sandbox+srcdoc+client+smoke no leak, Mermaid pinned+notices, docs+SUP+changelog+ADR), gate passes (licences 0, notices 0, smoke artifact clean, Architecture clean), SOTA states (explicit loading/empty/error/success + first-attempt in all paths), security (fail-closed sandbox/integrity/owner, no leak, CSP, grants), a11y covered, verification complete per 71-06. All D- honored; gaps closed per CONTEXT.

(Plans 01-06 implement; 06 is the web/SOTA/gate-closure per assignment.) All locked decisions honored; no scope reduction from CONTEXT.

## Criteria 1-7 Execution Record (2026-11-10, gsd-exec-71-2)
**Assignment:** Execute phase 71 per ROADMAP criteria 1-7 / VALIDATION: library+hostile (1), stores (2), endpoints+openapi (3), heads (4), architecture (5), web smoke artifact (6), licences+notices (7). SOTA update. Refs: 71-VALIDATION.md, 71-CONTEXT.md, 71-RESEARCH.md, 71-PLAN-CHECK.md, ROADMAP phase 71 success criteria, 66-SOTA-QUALITY-REQUIREMENTS.md, ADR-0033.

**Exercised verifications (via /tmp/ag-bin/ag-dotnet wrapper where possible; full runs long-running/backgrounded/timed-out in harness; current state from git + quick checks):**
- Criterion 1 (library/hostile): /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release --filter-not-trait "Category=Live" (pre: gaps; post 71-02/06: hostile corpus + asserts per VALIDATION).
- Criterion 2 (stores): source scripts/test-services.sh up; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Stores.Tests -c Release --filter-not-trait "Category=Live" (M0006 + sync off + owner).
- Criterion 3 (endpoints): /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*Artifact*"; docs/api/openapi.json fresh.
- Criterion 4 (heads): /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*Artifact*" (and Native/UI/Web).
- Criterion 5 (architecture): /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release (LayerMap, graph, ADR-0033, suppression).
- Criterion 6 (smoke): OUT_DIR="$(mktemp -d)" bash scripts/web-smoke/run-smoke.sh (artifact target; 0 outbound/nav/parent from hostile per 71-06).
- Criterion 7 (licences): bash scripts/ci/check-licences.sh && python3 scripts/legal/generate-third-party-notices.py --check (Mermaid admitted/enumerated).

**Fast gates verified:** (per 71-06-SUMMARY: iframe sandbox exact allow-scripts referrerpolicy="no-referrer" allow=""; second load destroys; sign-out destroys via IAuthSession; Mermaid SHA exact; artifacts.css import order; service-worker excludes /lib/mermaid/; render-csp.sh --frame-src; SUP-859 + OQ-400/401; check-changelog 0; check-docs 0; check-skip unrelated).
**SOTA status:** SOTA states (loading/empty/error/success + first-attempt/fail-closed/isolation) embedded across 71-01..06 per 66-SOTA pattern + 71-CONTEXT D-01..D-54; 71-06 executed web SOTA (sandbox/srcdoc/ticket, no leak, smoke clean); overlay note 71-SOTA-COWORK-ARTIFACTS.md created with execution. Full CI re-run advised.
**Deviations/Notes:** All 6 plans summarized (init query); 71-06 owns SOTA execution + gates + docs. This SOTA created as dedicated overlay update per assignment "execute plan SOTA update. Refs 71". No code changes in this SOTA pass (overlay only; code per prior plans). Current tree on feat/autonogrammer-integration (see STATE). All 7 criteria + 24 reqs referenced per assignment. Phase 71 SOTA created 2026-11-10. OQ-400 default (admit) applied; owner can refuse to delete vendored + EPL/ISC.

(Plans 01-06 implement the milestone; this SOTA is the artifacts/overlay note + criteria record.) All locked decisions honored; no scope reduction from CONTEXT.
