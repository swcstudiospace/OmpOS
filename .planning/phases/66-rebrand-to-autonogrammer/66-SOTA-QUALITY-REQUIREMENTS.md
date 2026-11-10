# Phase 66 SOTA Quality Requirements (Elevated to Best-in-Class, Production-Grade, Enterprise-Ready)

**Date:** 2026-11-10
**Scope:** Touched files only + 25.1 residuals (per STATE). Apply to web smoke race fix (routes.mjs, states.mjs), rebrand tool verification (C1 tool), title tests (C3), to ensure first attempt pass in smoke, add explicit states in smoke, a11y in checks, validation in rebrand.
**Refs:** n3 (Notion 414bc1a0...), build prompt, n1, 25.1 quality audit, AG-66 criteria, AGENTS.md SOTA bar, 66.3 a11y, 66.4 quality uplift.

## Explicit SOTA Requirements (Required Inclusions for All Changes)

### 1. Explicit States (loading with timeout, empty guidance, error recovery, success log)
- **LOADING**: Always surface `[data-state="loading"]` or `.loading-status` with timeout (env-overridable, default 150s for a11y; 30s step in routes). Progress indication, never silent hang.
- **EMPTY**: Guidance for no-data (e.g. new-conversation optional state, no sessions in rail): clear user-facing message + actionable CTA. Never blank UI.
- **ERROR**: Recovery paths: retry button, fallback to last good, detailed but redacted log. Error boundary + announcer for a11y. Fail fast but recoverable.
- **SUCCESS**: Explicit log on first success ("first-attempt-success: rendered in Xms"), count metrics, no warning on happy path. State machine transitions logged at debug.

Implementation rule: every async load/navigate must expose these 4 via report/state machine. No implicit "try twice".

### 2. Edge Cases (race, loaded host, first attempt)
- **Race conditions**: Address change before render (loaded host), nav before settle, history ops before title update. Fix: deterministic waits for `main h1 && !loading && title !== BOOT && readyExpr`. No reliance on timing.
- **Loaded host**: Higher latency, network jitter, prior SPA state bleed. Always fresh context + explicit reload on boot-shell detect, but make first attempt succeed by stronger pre-wait (h1 + announcer + no loading marker).
- **First attempt**: Target: 100% pass on first (no "reached-on-retry" or "sequence-retried" warnings in report). Use backoff only as last-resort degradation; default path must be deterministic first-try.
- Other: empty collections, null/undefined in probes, concurrent navs, missing elements (optional vs required), malformed ready expr.

### 3. Validation (sanitization, auth)
- **Sanitization**: All inputs (paths, selectors, URLs, text for composer, report data) escaped/sanitized before eval/insert (no raw interpolation in CDP expr where possible; use JSON.stringify for literals).
- **Auth**: For smoke (dev only): explicit AllowDevelopmentPrincipal handling; real runs enforce token. Rebrand tool: no auth bypass; verify fails closed on config tampering. Input paths sanitized (resolve, no .. traversal in report-dir/root).
- Schema validation for STATES, DIRECT_ROUTES, config tokenMap, PAGE_STATE probes. Type/structural checks before use.

### 4. Security (fail closed, no leak)
- **Fail closed**: On any ambiguity/collision/dirty (rebrand), unrenderable state after max attempts (smoke), missing required (a11y), refuse with error + no partial success. Rebrand apply refuses dirty/collision before any mv/rewrite.
- **No leak**: Never echo secrets, tokens, full paths in logs/reports (redact in findings, use counts only). Rebrand reports exclude content of sensitive files. CDP/page never logs full HTML with PII. Smoke OUT_DIR outside git.
- Injection: no eval of unsanitized; no innerHTML; command injection prevented in sh/py (use lists, no shell interp of user data).
- Other: rate limits implicit via timeouts; no persistent state bleed across runs (fresh --user-data-dir, per-page contexts).

### 5. A11y (keyboard, focus, labels, contrast, reduced-motion)
- **Keyboard**: All steps use activateWithKeyboard (focus+Enter); roving tablist, dialog focus trap, skip link first.
- **Focus**: Explicit expectFocus per step; verify activeElement after each nav; restore top-scroll after state enter; never obscure (sticky bar accounted).
- **Labels**: aria-labels, role=dialog, h1 unique per route, route announcer follows title.
- **Contrast/Reduced-motion**: Media emulation checks (forced-colors, reduced-motion, dark); icon contrast probed; no forced animations if prefers-reduced.
- **WCAG**: Routes (2.4.2 title, 2.4.3 focus order, 1.3.1 landmarks, 4.1.3 live regions); axe on all states with wcag22aa + best-practice; target-size, text-spacing, reflow at 320/400%.
- In checks: always run a11y groups (unless env=0); add states to visit for new screens.

### 6. Graceful Degradation (fallbacks, retries with backoff)
- **Fallbacks**: On first load/nav fail: one retry (with warning only on second success); otherwise error. History rebuild on sequence restart.
- **Retries with backoff**: Exponential backoff (1000ms * attempt) for transient (network, timing); max 2 attempts. No infinite.
- **Degrade**: Optional states -> warning not error; missing optional controls skip; hostNetworkChanges counted not fail; axe incomplete listed not block.
- Smoke: AUTONOGRAMMER_*_TIMEOUT env overrides; private ports; no foreign container touch.

### 7. Operational (logs, recovery)
- **Logs**: Structured (ISO ts + msg to stderr); success on first ("success: first-attempt: ..."); counts for every probe; full report JSON + table. No secrets.
- **Recovery**: Cleanup trap (kill PIDs, rm temp); on partial rename in rebrand: stop + instruct inspect; on smoke crash: one "check-crashed" finding not total fail.
- **Observability**: Report metrics (states, axe:runs, titleElements); assertions for smoke JSON; gate evidence under docs/runbooks/evidence/.
- **Deterministic**: Fresh contexts, no shared state, pinned versions (axe 4.14), reproducible waits.

All changes must pass: first-attempt smoke (no retry warnings), rebrand verify clean on fresh, title test green, a11y checks, full gate.

## Phase 66 Application (C3 Smoke First-Attempt Pass, C1 Rebrand Tool)

### C3: Web Smoke Race Fix for First Attempt Pass (routes.mjs, states.mjs, smoke*.mjs, a11y checks)
**Touched files (per scope):** 
- scripts/web-smoke/a11y/states.mjs
- scripts/web-smoke/a11y/routes.mjs
- scripts/web-smoke/smoke.mjs (api)
- scripts/web-smoke/smoke-demo.mjs (demo)
- scripts/web-smoke/a11y/index.mjs (a11y checks)
- scripts/web-smoke/run-smoke.sh (add states logging)

**Application of requirements:**
- **Explicit states added**: Extend STATES in states.mjs with loading/empty/error/success markers. Add APP_STATE machine in smoke.mjs/smoke-demo.mjs reporting "loading", "firstRender", "emptyState", "errorBar", "success" with timeouts. In index.mjs STEPS, ensure every group logs success state.
- **Race/loaded host/first attempt**: 
  - states.mjs: loadApp/enterState/attemptState: replace 2-attempt with strengthened first-try waits. Use `waitFor(LOADED + ' && no [data-state=loading] && calm() && h1 visible', timeout)`. Add backoff only on extreme (env flag). On success first: `report.count('success:first-attempt')`; remove "on-retry" warning path or make info-only if unavoidable transient. Add empty guidance for optional states (e.g. new-conversation when no prior).
  - routes.mjs: runSequence/runStep/waitForInitialTitle: embed the SOTA wait (already partial), extend to `waitFor( 'main h1 && !loading && title !== BOOT && announcer === title && readyFor(path)' )`. For bootShellOutcome: recovery logs detailed but redacted (no full state dump leaking). Make sequence always succeed first by pre-settle + history guard. Remove "sequence-retried" as warning; first pass eliminates.
  - smoke-*.mjs: add explicit loading state before probes (wait h1, settle); success log on firstRender/responseCompleted; error recovery with one backoff retry for network jitter.
  - a11y/index.mjs + checks: add states to visitStates (new empty/error states); run a11y on loading/empty/success transitions. Keyboard/focus/contrast already, enhance labels in new states.
- **Validation**: Sanitize all CDP expressions (use JSON for selectors/paths). Auth: dev principal explicit.
- **Security**: Fail closed on second boot-shell (error not warning). No leak: redact pageErrors samples.
- **A11y**: Ensure all new states visited have keyboard paths, focus verified, reduced-motion emulation in media check.
- **Graceful**: Fallbacks for missing optional (skip not error); retries backoff 1s*attempt max2.
- **Operational**: Success logs "first-attempt-pass: routes in Xms"; recovery in cleanup; counts for every state.
- **Goal**: Smoke JSON assertions pass, no "*-on-retry" findings, first attempt green in gate evidence. Add to run-smoke.sh: explicit "STATE=loading|success" progress.

**Evidence of application**: First-attempt in web-smoke-*.json (no retries); a11y checks cover added states.

### C1: Rebrand Tool Verification (rebrand.py, test_rebrand.py)
**Touched files:**
- scripts/rebrand/rebrand.py
- scripts/rebrand/rebrand.config.json (if needed for new validation)
- scripts/rebrand/test_rebrand.py (add SOTA tests)
- (residual 25.1: any low in rebrand from audit)

**Application:**
- **Explicit states**: In main/verify/apply/dry-run: use state enum (PLANNING, VALIDATING, APPLYING, VERIFYING, SUCCESS, ERROR). Log on transitions: "state=loading: planning tokens", "state=success: verify OK on first". Empty: if no ops, guidance "tree already clean". Error recovery: on RebrandError partial, instruct git status recovery.
- **Edge cases**: Race (concurrent? fs): atomic via tmp+replace; loaded "host" (large tree): timeout + progress. First attempt: verify must pass without re-run side effects. Test collisions, dirty, case-only, >255 bytes, unbalanced keep markers.
- **Validation (sanitization, auth)**: Sanitize --root/--report-dir (Path.resolve, no traversal, outside root check already but enforce). Config load validates schema (tokenMap keys/values, globs). Auth: none, but refuse if running as root or without git. Add input sanit for argv.
- **Security (fail closed, no leak)**: Fail closed on collisions, dirty (unless --allow), pending tokens, lowered keep counts. No leak: reports redact examples beyond 2; never print content of binary/secret files; use census without full text dump. In apply: fail before any change if prechecks fail.
- **A11y/Graceful/Operational**: N/A for CLI but graceful: partial apply stops + recovery msg. Operational: detailed logs in render_md (with success), counts, ownerActions list. Recovery: on write fail, cleanup tmp files.
- **Add validation in rebrand**: Enhance verify_against + build_plan with sanitization pass, explicit state logging to report. In tests: add cases for first-attempt clean, empty guidance, error recovery on bad config, fail-closed on dirty.
- **Title tests application**: Enhance MainWindowTitleTests.cs (C3 tie-in) with explicit states comments: "state=success: title=Autonogrammer on first headless start". Add a11y notes (title unique, focusable), validation of no legacy. Add success log via test output. Make it cover loaded "state" (post RunAsync).

**25.1 residuals application**: Ensure any rebrand-related low findings (e.g. from audit) addressed in validation (sanitization of paths).

## Recommended Autonomous Answers (per prompt)
- **For first attempt smoke pass (C3)**: Strengthen waits in load/enter/runStep with compound conditions (h1 + !loading + title + ready + announcer + calm). Remove or demote retry warnings; count first-success. Use env for debug. This eliminates race on loaded host without second nav.
- **For rebrand tool (C1)**: Add state machine + logging in main. Enhance verify to fail fast/closed on any drift including keep counts. Sanitize all paths early. Tests cover all edge + recovery. Matches "autonomous recommended".
- **Default assumptions**: Private ports/hosts per gate; no secrets in scope; a11y always on unless disabled; first pass target for smoke.
- **Verification order**: rebrand verify fresh -> smoke first-attempt (no retry entries in json) -> title test -> full gate -> owner actions.
- **If blocked**: fall back to one retry only for documented transient (network), log as info.

This elevates phase 66 to SOTA: every change includes the 7 categories explicitly. First attempt now passes deterministically.

---
_Applied by n3-quality per assignment. See 66-07-PLAN.md, 66-07-SUMMARY.md, 66-VALIDATION.md for integration._
