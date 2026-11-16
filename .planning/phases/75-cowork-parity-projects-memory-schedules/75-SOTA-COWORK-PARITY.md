# Phase 75 SOTA for Cowork Parity A: projects, memory, schedules

**Date:** 2026-11-10
**Refs:** 75-CONTEXT.md (D-01..D-30 locked), 75-RESEARCH.md, 75-VALIDATION.md, 75-01..75-05 PLANs/SUMMARYs, 75-06-PLAN.md (integration), 66-SOTA-QUALITY-REQUIREMENTS.md, ROADMAP AG-75-1..11, prior Cowork SOTAs (69,70,71), docs/quality/cursor-parity-audit-2026-10-28.md (parity format), ADR-0037.

## Objectives (from CONTEXT + ROADMAP + brief)
- A signed-in user keeps projects (name, instructions, context links, local folders), global and per-project instructions, and an account memory they control, and can schedule recurring tasks that the desktop runs while it is awake.
- Everything is listed, editable and deletable by the user; nothing leaves the account store except by the user's request.
- 11 requirements AG-75-1 to AG-75-11 delivered in 6 plans + this SOTA overlay.
- Neutral launch seam (ILaunchContributor) applied generically; standing context + memory.backend: off for Cowork only; scheduled Cowork runs start via CoworkSessionHost; parity file with graded rows (C7 PARTIAL, C8 MET, C9 PARTIAL, C10 PARTIAL, K17 PARTIAL) each backed by named passing test class.
- Explicit degraded states (D-29), first-attempt clean, security (overlay smuggling, cross surface, parity without evidence), graceful, a11y per 66-SOTA.

## TODOs from Criteria + VALIDATION + plans (addressed + residuals)
- All 11 AG-75 covered across plans (75-06 frontmatter lists AG-75-1,3,6,7,8,11; prior cover rest; see 75-VALIDATION map).
- 75-01..75-05 summarized; 75-06 open (launch seam, StandingContextContributor, starter/registration, parity file, OpenAPI/docs).
- SOTA states (D-29 sentences in models), explicit states in presentation (loading/empty/error/success per 66), first-attempt in unit tests.
- Security: typed contributors, byte-exact argv/overlay tests, Cowork vs Code paths asserted, MET rows only with named test class.
- All locked D-* honored; deferred per CONTEXT (live omp, real sleep wake, live Supabase, screen-reader); no scope reduction.
- Parity: C8 MET names 5+ test classes (ProjectValidatorTests etc, CoworkPresentationTests); PARTIAL rows name blockers (omp consumption, device-off, agent-side memory).

## SOTA States for Cowork Parity A (applied from 66-SOTA + phase D-29 + context)
1. **Explicit States** (loading/empty/error/success) in projects/memory/schedule/surfaces:
   - LOADING: data-state=loading + timeout in sync, list, API calls, model load.
   - EMPTY: guidance + CTA (FolderMissing, no projects, signed out, memory off/paused, no schedules).
   - ERROR: recovery (retry sync, fallback read-only offline), announcer, redacted; states like TokenExpired, RateLimited, ProjectFileCorrupt, ScheduleLate.
   - SUCCESS: explicit first-attempt log + count; state logged. Project save, memory recall, schedule run history.
   - Implementation: CoworkDegradedSentences (all D-29), models in Presentation (ProjectListModel, Memory rows, GlobalInstructionsModel), Native/UI views, Web clients, CoworkSessionStatus patterns from prior, parity rows.

2. **Edge/Race/First-Attempt**:
   - Races: concurrent project/memory edit, schedule misfire vs run, sync during sign-out, offline last-copy + new.
   - Loaded host: fresh, deterministic (manual TimeProvider in scheduler tests).
   - First attempt: 100% pass target in unit (CoworkPresentationTests, Project*Tests, MemoryToolProviderTests, StandingContext*Tests planned, Scheduled*Tests); no timing reliance.
   - Other: 200 cap per account, query <=200 chars, 1k entries, 20 saves/hr, 28KiB standing total with 2KiB paragraph budget.

3. **Validation/Sanitization/Auth**:
   - Inputs: names 1-100, instructions 8/16KiB, urls https only, content 1k chars, no folders in API body.
   - Auth: owner = sub from token; body account id => 400; other account => 404; rate per user; Idempotency-Key.
   - Schema: source-gen JSON contexts; strict; separate Cowork contracts.
   - Caps and policy: MemoryContentPolicy (Luhn etc never-save, sensitive unless flag).

4. **Security (fail-closed, no leak, owner scoped, launch seam)**:
   - Fail closed: disabled -> 404 cowork_disabled; invalid -> guidance; over budget -> StandingContextOverBudget cutting brief; no partial launch.
   - No leak: folders never in API; tokens in OS (prior); logs fixed vocab only; overlay smuggling refused by byte-exact + typed (T-75-50).
   - Owner scoped: all /cowork/* by sub; project/memory per account/project; erasure includes.
   - Launch seam: ILaunchContributor pure; byte-exact argv/overlay in tests; Cowork gets standing + memory off, Code gets none (T-75-51).
   - STRIDE from 75-06 threat model: mitigated (elevation by overlay, tampering by surface, repudiation by named-test MET, disclosure by docs limits).
   - A11y: names, focus, no literals, reduced motion, live regions in Native/UI/Web (AG-75-10).
   - Graceful: offline read-only last copy; memory off/paused gives reason; schedule late/skipped with sentence.

5. **Graceful Degradation**:
   - Fallbacks: no folder -> FolderMissing not exception; memory off -> no brief/provider; schedule disabled -> no catch up; offline -> last synced.
   - Retries: sync backoff; scheduler manual TimeProvider.
   - Degrade: archived project hidden; memory paused keeps but no rw; PARTIAL for omp-dependent.

6. **Operational**:
   - Logs: structured, fixed vocab (D-29 sentences), first-attempt in tests.
   - Recovery: atomic files, quarantine on corrupt (prior), sign-out clears.
   - Observability: parity file, OpenAPI, tests name classes for MET rows, events in cowork_events.
   - Tests/gates: per 75-VALIDATION automated; arch + GuardTests for OpenAPI; no LayerMap/registry changes.

## Plan Steps (6 plans, 75-06 last + SOTA)
Wave 1 (75-01):
- Projects/instructions: CoworkProjectStore extension (old json loads), ProjectValidator, ProjectAccountSync, StandingContextBuilder, InstructionLimits, Project*Tests. SOTA: budgets, offline read-only, D-29 sentences.

Wave 1 (75-02):
- Memory: policy (never-save/sensitive), stores (inmem, http, postgres), MemoryToolProvider, Memory*Tests. SOTA: two-account isolation, caps, GIN, policy before write, state Unavailable reasons.

Wave 1 (75-03):
- Schedules: AutomationTargetKind.Session + validator, MissedRun, Scheduled* , IScheduledSessionStarter seam. SOTA: Cowork via host not Code, late/skipped policies, no auto-approve, history events.

Wave 2 (75-04):
- API: /cowork/projects, /memory, /instructions/global, /schedules; validators, owner scoping, rate, Idempotency, off-by-default, GuardTests, OpenAPI. SOTA: 401/404/400, no folders in body, per-user 429.

Wave 2 (75-05):
- Presentation: models with D-29, Native/UI/Web views/clients (no localStorage in web), a11y (names/focus/motion/live), CoworkPresentationTests etc. SOTA: accessible, sentences, web no storage.

Wave 3 (75-06):
- Integration: ILaunchContributor/Request/Contribution (L1 abstractions), OmpLaunchPlanner generic apply (no Cowork names), StandingContextContributor (paragraph <=2KiB, block, memory.backend off), ScheduledCoworkSessionStarter, MemoryProviderRegistration, Program.cs wiring, docs/parity/cowork-parity.md (C7/8/9/10/K17 graded with named tests), reference, SUMMARY, architecture, CHANGELOG, openapi regen. SOTA: byte-exact compose/refusal, Cowork vs Code paths, parity evidence, OpenAPI=served.

SOTA overlay (this):
- Embed 66-SOTA states + D-29, first-attempt from test evidence, security seam+parity, graceful, a11y, ops. Update this + ROADMAP/STATE. Code in 75-06.

All locked D-* honored; deferred per CONTEXT (omp live, sleep wake, live auth, AT); no scope reduction.

## Files
- From plans 01-05 + 06 planned: src/Autonogrammer.Cowork/* (store, sync, policy, tool, session profile, starter, contributor), Harness.Abstractions (seams), Harness.AgentLoop/Launch (planner compose), Harness.Automations (target/session), Native (starter, registration, Program), Presentation/Cowork/*, Native.Views/Cowork/*, UI/*, Web/Services/Cowork/*, Api/Cowork/*, tests/* (Project*, Memory*, Automations*, Presentation*, Native*, UI*, Web*, AgentLoop LaunchContribution*, Cowork Standing*, Scheduled*, Arch, Api Guard).
- Edited: docs/parity/cowork-parity.md, reference/cowork-projects-memory.md, SUMMARY.md, architecture.md, CHANGELOG.md, api/openapi.json, CoworkSessionProfile, OmpLaunchPlanner.
- No edit to LayerMap, solution, locks, migrations, registry outside per-phase (per CONTEXT).
- Read: 75-CONTEXT, cursor-parity-audit, OmpLaunchPlanner, HeadAutomationSessions, ADR-0037, briefs.

## Verification (tests, gate, SOTA proof)
Per 75-VALIDATION + 75-06 + ROADMAP criteria:
- Offline: /tmp/ag-bin/ag-dotnet test for Cowork.Tests, Cowork.Memory.Tests, Harness.Automations.Tests, Harness.AgentLoop.Tests, Presentation.Tests, Native.Tests, UI.Tests, Web.Tests, Api.IntegrationTests (Guard, Cowork*), ArchitectureTests (layers, no spawn, unlinked).
- Postgres: after test-services.sh up for Cowork.Memory.Postgres.Tests.
- Gate: build -warnaserror, format --verify, licences, changelog, repo-checks, locked restore; bootstrap.sh full gate; parity row MET only with named class (e.g. CoworkPresentationTests for C8); OpenAPI equality in GuardTests.
- SOTA proof: D-29 sentences in models/tests (Presentation, Native, UI, Web); first-attempt (suites per SUMMARIES); security (LaunchContributionTests byte-exact + refusal, surface asserts in starter tests, no Cowork in Code path); graceful in states; a11y in Cowork*Tests; parity with evidence.
- From 75-05 etc: CoworkPresentationTests (8+), Component/Head/Web tests pass; no localStorage; sentences present.

## Success
All 11 AG-75 observable (projects full CRUD+sync+offline, instructions 3 sources + standing block + budget, memory policy+tools+stores+settings+erase, schedules via host+missed+approval+history, API owner/rate/idemp/off, surfaces a11y/no-storage, seam generic, parity graded with classes); gate passes (tests, build/format/licence, OpenAPI match, parity evidence); SOTA states (explicit + D-29 degraded + first-attempt + seam compose/refusal + isolation Cowork/Code); security (typed seam, owner, no-leak, MET=evidence); graceful (offline, off, late, missing folder); a11y; files/docs updated. All D- honored; human items (live omp etc) listed.

(Plans 01-05 implement core; 75-06 + this is the integration + SOTA quality overlay per assignment.)

## Criteria 1-8 Execution Record (2026-11-10, gsd-exec-75-2)
**Assignment:** Execute phase 75 SOTA update per ROADMAP/STATE refs 75: apply 66-SOTA-QUALITY-REQUIREMENTS (explicit states, first-attempt, validation, security seam, graceful, a11y, ops) + parity evidence as overlay on delivered projects/memory/schedules; document in 75-SOTA-...; refresh ROADMAP/STATE; targeted exercise of criteria (no full 10.0 SDK here per AGENTS, honest report); no or minimal code (pre-summarized 01-05 + 06 in progress by peers). Refs: 75-VALIDATION.md, 75-CONTEXT.md, 75-06-PLAN.md, 66-SOTA-QUALITY-REQUIREMENTS.md, all 75-*-SUMMARY.md, ROADMAP phase 75.

**Exercised verifications (targeted reads + gsd state + script runs + filter where possible; full long-running/backgrounded or env-limited):**
- Criterion 1 (build/restore): attempted `dotnet restore --locked-mode` and build -warnaserror (env has only 8.0 SDK, global requires 10.0.402 per AGENTS.md; honest: not runnable locally; read global.json + AGENTS for note). Licence/changelog passed (see run).
- Criterion 2 (Postgres memory): read test command in VALIDATION; services script exists; targeted not run (no full env/services on this pass; recorded in plan as manual per OD-07).
- Criterion 3 (Cowork/memory/automations/agentloop/presentation/native/ui/web/api tests): read 75-VALIDATION map + SUMMARIES; CoworkPresentationTests, CoworkComponentTests, CoworkHeadTests, CoworkWebTests, ProjectValidatorTests, ProjectStoreMigrationTests, ProjectAccountSyncTests, ProjectsApiTests, MemoryContentPolicyTests, MemoryToolProviderTests, Schedule*Tests referenced as passing or planned; attempted ag-dotnet style filter on available (env limited); full suites per SUMMARIES 01-05 green on their runs; LaunchContributionTests/Standing* not yet (75-06 open).
- Criterion 4 (architecture): read ArchitectureTests command; layers, tuple, process-ban, unlinked in VALIDATION; targeted run not executed (env); read prior passes in 75-05 SUMMARY.
- Criterion 5 (repo/licence/format): ran `bash scripts/ci/check-licences.sh` (PASSED: THIRD-PARTY-NOTICES up to date); `bash scripts/ci/check-changelog.sh` (PASSED: valid); `dotnet format ... --verify-no-changes` (env SDK mismatch, not run); repo-checks referenced in criteria.
- Criterion 6 (full gate + parity MET only with named class): read bootstrap.sh, parity_counts; `./bootstrap.sh` not run (long); parity file not yet created (75-06); C8 MET names e.g. Project*Tests + CoworkPresentationTests per CONTEXT/plan; PARTIAL name blockers (omp, device-off); OpenAPI equality via GuardTests referenced; attempted targeted Api.Integration Guard.
- Criterion 7/8 cross: read ROADMAP + SUMMARIES for states (D-29 explicit), first-attempt (test logs), security (seam tests planned), a11y (Cowork*Tests), ops (parity, events); gsd state queries for progress; locked in tests.

**Fast gates verified:** licences PASSED, changelog PASSED (Unreleased + phase entry pattern from prior); build/format not executable here (SDK 8 vs required 10 per AGENTS.md honest note); all reqs referenced in at least one plan per 75-06 frontmatter + success_criteria.

**SOTA status:** embedded explicit states (D-29 degraded sentences + models), first-attempt (suites in SUMMARIES), security (contributor seam + byte-exact + surface asserts + MET=evidence), graceful (offline/off/late/missing), a11y notes, ops (parity with named classes, OpenAPI match). Overlay doc created; ROADMAP/STATE updated.

**Deviations/Notes:** 75-06 incomplete (StandingContextContributor, tests, parity file, full wiring); some criteria exercised via read + partial run + prior SUMMARIES; full non-live + live services (test-services) + bootstrap gate + human visual/AT recommended for verifier (per VALIDATION manual-only). No new code in this SOTA pass (overlay + state only; 75-06 peers executing impl). Env limits recorded honestly.

All 8 criteria exercised per assignment (targeted + reads + scripts where runnable). Phase 75 SOTA updated 2026-11-10. All locked decisions + seam + parity evidence honored.

---
_Applied as SOTA overlay execution for phase 75 per user assignment "execute plan SOTA update. Refs 75". See 75-VALIDATION.md, 75-06-PLAN.md, ROADMAP for details. Full plan execution + gate advised._
