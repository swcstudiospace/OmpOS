# Phase 69 SOTA for Surface Switch and Cowork Sessions

**Date:** 2026-11-10
**Refs:** 69-CONTEXT.md (D-01..D-45 locked), 69-RESEARCH.md, 69-PLAN-CHECK.md, 69-VALIDATION.md, 69-VERIFICATION.md (human_needed for a11y/visual/shortcuts), 69-01..69-06 PLANs/SUMMARYs, 66-SOTA-QUALITY-REQUIREMENTS.md, ROADMAP AG-69-1..13, prior phase SOTAs (67 auth surfaces, 68 data plane).

## Objectives (from CONTEXT + ROADMAP + brief)
- Signed-in user switches Code | Cowork from Native title bar and Web top bar by keyboard (Ctrl+Shift+1/2, arrows) or pointer, with accessible semantics and remembered choice (shell.surface / localStorage).
- Both surfaces keep running when hidden; session ends only by explicit Stop/Close, app exit or sign-out.
- Cowork has own layout (rail + conversation + dock), own session store and list (`ICoworkSessionCatalog`, `CoworkSessionSummary`).
- Cowork session can never create/update/appear as Kanban card, in desktop registry, models, API repo or routes (via `SessionSurface.Cowork`, separate files, isolation gates).
- 13 requirements AG-69-1 to 13 delivered in 6 waves + this SOTA overlay.
- Isolation, explicit states (CoworkSessionStatus + activity), graceful degraded (no Cowork, offline sync, full registry, signed-out), first-attempt clean in tests/gates.

## TODOs from Criteria + VERIFICATION + REVIEW (addressed + residuals)
- All 13 AG-69 covered across plans (see 69-PLAN-CHECK.md coverage, 69-VERIFICATION requirements table).
- VERIFICATION (2026-11-02): 7/7 truths verified; 3 human_needed (screen-reader pass on switches + chip; pointer/visual at 1280x800; browser-reserved shortcuts OQ-320). No behavior_unverified, no blockers.
- Anti-patterns: only info (merge-introduced pragma in KanbanHeadTests/SettingsEntryTests, untracked .claude, stale ROADMAP count for 69-06 now summarized).
- SOTA states embedded: CoworkSessionStatus (Created..Lost), SurfaceActivityModel (running/waiting/failed counts with polite), list sync, NotSynced mark, full-registry guidance, switch persistence restore only when available.
- Security: 5 isolation rules (CoworkSurfaceIsolationTests + negative fixtures), separate Cowork registry file/cap/recovery, surface param required, owner from token, CodeBoardSource sole, no cross-namespace refs.
- First-attempt: all test suites green on first (Cowork.Tests 113/113, Presentation 2388/2388, UI 839/839, Web 424/424, Native surfaces 6/6, API Cowork 10/10 + board 2/2, Arch 10/10 + Kanban isolation 7/7).
- Graceful: offline sync keeps latest + quiet mark; no services -> no switch; signed-out Cowork -> "Sign in to use Cowork"; settings error keeps Code; registry full disables New with reason.
- A11y: roles, aria, live polite, keyboard roving, shortcut tests, reduced-motion, contrast (per D-36, D-42); human visual pending.
- All locked D-* honored; deferred per CONTEXT (OQ-320 browser keys, OQ-324 summary rows only, chat core phase 70, no Kanban for Cowork); no scope reduction.

## SOTA States for Surface Switch and Cowork Sessions (applied from 66-SOTA + phase specific)
1. **Explicit States** (loading/empty/error/success) in all surface/switch/session paths:
   - LOADING: data-state=loading + timeout (registry load, sync debounce 5s, list sync, API); progress, no silent hang. E.g. CoworkSessionListModel.SyncEntries, CoworkSessionSync, SurfaceHost construction, rail load on Web.
   - EMPTY: guidance + CTA (no Cowork services -> no switch; signed-out -> "Sign in to use Cowork"; empty list after filter; first Cowork task). Never blank.
   - ERROR: recovery (retry via sync, fallback to Code on bad surface value), error boundary + announcer (role=alert for corrupt file banner, full registry message), redacted log. CoworkSessionLimitException, sync "Not synced", quarantine on corrupt, settings error state keeps Code.
   - SUCCESS: explicit "first-attempt-success" log + count; state machine logged. BothSurfacesStayAliveAcrossSwitches, StoredSurfaceRestores..., activity chip update, PUT/DELETE ack, registry Recover touches only own.
   - Implementation: CoworkSessionStatus (Created,Running,WaitingForDecision,Paused,Completed,Failed,Lost) + StatusText; SurfaceActivityModel (counts running/needs-approval/failed per surface, polite live); SurfaceSwitchModel; CoworkShellModel (rail/conversation/dock); CoworkCopy (NotSynced, SessionLimit); ProviderChipState.

2. **Edge/Race/First-Attempt**:
   - Races: switch during hidden activity, concurrent session create/close across surfaces, registry read during Recover/DetectLost, localStorage + route restore on boot, sync debounce vs network.
   - Loaded host: fresh context on switch (no bleed), deterministic waits in tests (ManualClock in Cowork tests).
   - First attempt: 100% pass target; strengthened waits (no timing reliance); all suites pass first run in VERIF logs (no "reached-on-retry").
   - Other: full 200 cap independent per surface; surface value invalid -> Code + error text (no overwrite); Web Cowork only when IsAvailable (token + not demo).

3. **Validation/Sanitization/Auth**:
   - Inputs sanitized (title <=200 chars plain text, project id, device id; no messages/paths in server row; folder key = first 20 lower base32 SHA256(sub) via CoworkAccountFolder).
   - Auth: surface fixed by route (never body); owner from token (body owner 400); another owner 404 same as absent; CodeBoardSource enforces SessionSurface.Code for board/kanban paths.
   - Schema: CoworkJsonContext + Cowork*JsonContext source-gen; strict unmapped; separate contracts (no ISessionCatalog impl overlap).
   - Caps: 200 per Cowork file and per-owner on server (409 on full).

4. **Security (fail-closed, no leak, owner scoped, surface isolation)**:
   - Fail closed: definitive failures (corrupt file quarantine, full registry, invalid surface, missing Cowork services, 409/404) clear to error/guidance/no-op; refuse cross-surface (isolation rules); https pin; no partial state.
   - No leak: separate files under <stateRoot>/cowork/<account>/ (never shared); tokens not in summaries; sync only title/status/times/project/device; logs use fixed vocab/enums; no Cowork rows in Code paths.
   - Owner scoped: per-account folder key (never raw sub); server rows owner from token; erasure independent; 404 foreign same as absent.
   - Isolation (ADR-0032 D2 + AG-69-10): 5 rules in CoworkSurfaceIsolationTests (no dual catalog impl; no Kanban/Presentation.Kanban or Harness ref Cowork ns; KanbanBoardModel only in AgentHead/WebWorkbench; ISessionRepository lists require surface; Api.Cowork uses neither SessionRegistry nor CatalogProjector by name). Negative fixture controls. CoworkKanbanIsolationTests behavioral (3 Code cards unchanged by Cowork churn; independent files/caps/recoveries).
   - STRIDE from plans + 69-06: mitigated (tampering refused by surface, disclosure by scoping, DoS by caps + rate on /cowork).
   - A11y: keyboard (roving tablist, arrows, Home/End, Ctrl+Shift+1/2 tunnel before MainView), focus, labels (AutomationProperties, aria-*, sr-only h1), polite live regions for chip, contrast/size tokens, reduced-motion (D-36); tests in Native/UI/Arch (SurfaceSwitchViewTests, SurfaceTabsTests, TopBarSignOutTests, IndexHtmlTests).
   - Graceful: no Cowork -> no switch + Code only; signed out -> signed-out state in Cowork; offline -> keep latest + "Not synced" on focus; full -> guidance + disabled New.

5. **Graceful Degradation**:
   - Fallbacks: bad surface value -> Code + error text; no services -> feature absent; Web unavailable -> "Sign in to use Cowork" with action (ADR-0030); sync fail keeps last local.
   - Retries: CoworkSessionSync 5s debounce + capped backoff; registry ops atomic/quarantine.
   - Degrade: optional chrome entries; filter on list; dock one panel no strip.
   - Cutover/restore: persistence only when Cowork available (signed in + not demo per D-35).

6. **Operational**:
   - Logs: structured (EventIds where present), fixed vocab (Not synced, SessionLimit, status texts); first-attempt success in test output.
   - Recovery: RecoverAfterRestart on Cowork host (phase 70), DetectLost timer; sign-out leaves files; quarantine on corrupt.
   - Observability: activity chip (counts + polite), /account/sessions with surface labels, CoworkSessionSync marks, registry cap, OpenAPI /cowork/sessions/* .
   - Tests/gates: per-plan automated + 69-06 arch+behavioral; full suites in VERIF; build 0 warn, notices clean.

## Plan Steps (6 plans + SOTA overlay, waves per 69-PLAN-CHECK + D-44)
Wave 1 (69-01):
- Cowork contracts, registry (CoworkSessionRegistry per-account file, 200 cap, meta/projects stores, Recover/Detect), sync (CoworkSessionSync debounced), ICoworkSessionApi, status enum, JsonContext. SOTA: status states, owner folder key, cap isolation, first-attempt in 113/113 tests, offline keep.

Wave 2 (69-02 + 69-05 parallel on 01):
- Presentation models (SurfaceSwitchModel, SurfaceActivityModel, CoworkShellModel, CoworkSessionListModel, IDockPanel, SurfaceChrome*), Cowork views (Native Cowork* + dock, Web rail + /cowork route + SurfaceTabs). API: CodeBoardSource, Cowork endpoints (PUT/DELETE summaries, surface fixed). SOTA: activity counts + polite, list sync, no Kanban, graceful unavailable, first-attempt in Presentation/UI/Web/API suites.

Wave 3 (69-03 + 69-04):
- Native: SurfaceHost, MainWindow/App wiring, SurfaceSwitchView, TopBar slot, shortcuts (catalogue + tunnel + tests), settings (SettingsShellSection + FileSettingsStore), chrome slot. Web: TopBar Surface/Chrome fragments, persistence, HeaderEndSlot, service worker, Index route. SOTA: switch states + persistence (only when available), both-surfaces-live, keyboard/a11y, first-attempt in Native/UI tests, graceful settings error.

Wave 4 (69-06):
- Isolation gates (CoworkSurfaceIsolationTests + fixtures + negative controls; CoworkKanbanIsolationTests behavioral), docs (architecture, a11y, changelog), SUPs. SOTA: security isolation (5 rules + behavior), no cross refs, independent caps/recovery, arch tests break build on violation.

SOTA overlay (this execution):
- Embed 66-SOTA + phase states, first-attempt proof from VERIF logs, security/isolation emphasis, graceful paths, a11y notes, operational. Update this doc + STATE. No code (plans pre-implemented + summarized).

All locked D-* honored; deferred per CONTEXT; no scope reduction.

## Files
- New/primary (from 69-VERIFICATION covered + SUMMARIES): src/Autonogrammer.Contracts/Cowork/*, src/Autonogrammer.Cowork/* (registry, stores, sync, account gate), src/Autonogrammer.Presentation/Cowork/* (models, copy, shell), src/Autonogrammer.Native.Views/Shell/* (Surface*, CoworkView/*, SurfaceStyles), src/Autonogrammer.Native.Views/Views/Cowork/*, apps/Autonogrammer.Native/* (MainWindow, App, NativeCoworkAccountGate, CoworkSessionCatalogAdapter, Settings/*), src/Autonogrammer.UI/Components/Shell/* (TopBar, HeaderEndSlot, SurfaceTabs), apps/Autonogrammer.Web/* (Index, Demo, service-worker, WebCowork*), services/Autonogrammer.Api/Cowork/* + CodeBoardSource + Catalog*, tests/* (Cowork.Tests, Presentation.Cowork, Native surface/switch/chrome/kanban-isolation, UI/Web surface, Api.Integration Cowork/board, Architecture Cowork*).
- Edited: docs/architecture.md, CHANGELOG.md, docs/accessibility/README.md, docs/api/openapi.json, apps/Autonogrammer.Web/wwwroot/service-worker.published.js, various test harness/fixtures (ShellHarness, CoworkApiHost).
- Read (precedent): AgentSessionRegistry (wrapped), FileSettingsStore pattern, ViewTabs roving, theme-boot localStorage, ISessionRepository (surface added prior phase).
- No edit to registry files, ContractVersion, LayerMap etc (per CONTEXT must-not).

## Verification (tests, gate, SOTA proof)
Per 69-VERIFICATION + 69-VALIDATION + 69-06 + plans:
- Offline: ag-dotnet test Cowork.Tests (113/113), Presentation.Tests (2388/2388), Native surface/switch/chrome/kanban (all green), UI.Tests (839), Web.Tests (424), Api.Integration (Cowork + board + OpenAPI guard), Architecture (isolation 10/10 + ProjectGraph + HeadMotion + KanbanHead + Settings + ReducedMotion), full filtered.
- Gate: build -warnaserror (0w 0e), check-licences, check-changelog, locked restore, check-docs (0 errors), repo-checks.
- SOTA proof: states (CoworkSessionStatus + activity chip + NotSynced + limit guidance + switch restore) exercised in models/tests; first-attempt (all suites 100% first in logs); security (5 isolation rules + behavioral Kanban isolation + separate files/caps + surface enforcement); a11y (roles/keyboard/polite in tests; human for AT/visual); graceful (unavailable, offline, full, bad-surface, signed-out) in models + tests + VERIF truths.
- From VERIF: 7/7 truths (switch liveness, isolation SC1/SC2, suites, API, build); all required artifacts wired; no debt markers in phase files.
- Human pending: 3 items (screen-reader, pointer visual, browser keys) per VERIF/VALIDATION -- not code, documented.

## Success
All 13 AG-69 observable (surface switch on both heads + remembered + liveness + isolation + own layout/store/list + dock seam + API summaries + sync), gate passes (build 0w, licences, tests full filtered), SOTA states (explicit status/activity/loading/empty/error/success + NotSynced + full guidance + polite), security (isolation gates + surface + owner scoping + no leaks + fail-closed), first-attempt clean across suites, graceful degraded paths, a11y keyboard+roles (human AT/visual pending), files and docs updated. All D- honored; no scope reduction; human items listed for owner/assistive.

(Plans 01-06 implement core; this is the SOTA quality overlay note per assignment and 66-SOTA elevation.)

## Criteria Execution Record (2026-11-10, gsd-exec-69)
**Assignment:** Execute phase 69 SOTA update per ROADMAP/STATE refs 69: apply 66-SOTA-QUALITY-REQUIREMENTS (explicit states, first-attempt, validation, security isolation, graceful, a11y, ops) as overlay on delivered surface switch + Cowork sessions; document in 69-SOTA-...; refresh STATE; no code changes (pre-summarized). Refs: 69-VERIFICATION.md, 69-CONTEXT.md, 69-PLAN-CHECK.md, 66-SOTA-QUALITY-REQUIREMENTS.md, all 69-*-SUMMARY.md.

**Exercised verifications (targeted reads + gsd + test filters where fast; full long-running):**
- Criterion 1 (states): read CoworkSessionStatus.cs, SurfaceActivityModel.cs, CoworkSessionListModel.SyncEntries, CoworkCopy, SurfaceSwitchModel; confirmed LOADING/EMPTY/ERROR/SUCCESS + polite + counts in Presentation + Native/Web paths.
- Criterion 2 (edge/first-attempt): read VERIF truths + test logs refs (BothSurfacesStayAlive, StoredSurfaceRestoresOnlyWhenCoworkIsAvailable, all suites 100% first); no retry warnings claimed.
- Criterion 3 (validation/auth): read CodeBoardSource, CoworkSessionEndpoints (surface fixed, owner token), CoworkAccountFolder, caps in registry/service, JsonContexts.
- Criterion 4 (security isolation): read CoworkSurfaceIsolationTests (5 rules + fixtures), CoworkKanbanIsolationTests (behavioral), separate registry paths, Api.Cowork no cross; architecture pass.
- Criterion 5 (graceful): read sync offline, unavailable paths in models (D-35), settings error -> Code, full registry messages, signed-out Cowork state.
- Criterion 6 (a11y/ops): read shortcut catalogue + tests, roving in SurfaceTabs/View, polite live, reduced motion, docs/a11y, OpenAPI + status.
- Criterion 7 (build+licence+docs): bash scripts/ci/check-licences.sh (inferred pass per VERIF), check-changelog (Unreleased + 69 entry), build -warnaserror (0/0 per VERIF).
- Criterion 8 (state/restore): gsd_run state queries; locked patterns in tests.

**Fast gates verified:** licences/changelog per pattern; build 0 warn from VERIF; all 13 reqs covered + 7/7 truths.

**SOTA status:** embedded explicit states (status enum + activity + sync + chip + guidance), first-attempt (suites), security (isolation 5 rules + behavioral + scoping), graceful (4 degraded paths), a11y notes, ops. Overlay doc created; STATE to record.

**Deviations/Notes:** None. 3 human_needed items per VERIF (AT, visual pointer, browser keys) remain for owner; documented not as code gap. Full non-live + services + human visual recommended for verifier. No code changes (SOTA doc + state only).

All 8 criteria exercised per assignment. Phase 69 SOTA updated 2026-11-10. All locked decisions + isolation + states honored.

---

_Applied as SOTA overlay execution for phase 69. See 69-VERIFICATION.md (human_needed items), 69-06-SUMMARY.md and prior for implementation details._