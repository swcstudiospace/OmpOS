# Phase 72 SOTA for Cowork Browser Use

**Date:** 2026-11-10
**Refs:** 72-CONTEXT.md (D-01..D-51 locked), 72-RESEARCH.md, 72-VALIDATION.md, 72-PLAN-CHECK.md, 72-VERIFICATION.md, 72-01..72-08 PLANs/SUMMARYs, ROADMAP AG-72-1..16, ADR-0034-cowork-browser-use.md, 66-SOTA-QUALITY-REQUIREMENTS.md, prior phase SOTAs (69 cowork surfaces, 68 data plane, 67 auth surfaces).

## Objectives (from CONTEXT + ROADMAP + brief)
- The Cowork agent browses via a host-launched, isolated Chromium driven over CDP pipe by PuppeteerSharp 25.15.0, shown as live pane on desktop head (Web/Android show RemoteViewUnavailable per ADR-0036 D13).
- Exposed to omp as `browser_*` tools (9 tools) with vision (screenshots to model), under egress guard, two-stage approvals, visible Stop, hostile-page corpus.
- Full isolation: fresh 0700 profile, env allow-list only, sandbox on, no user data, no bundled Chromium, no relay feed, no upload tool.
- 16 requirements AG-72-1 to 16 delivered in 8 plans/waves.
- Explicit SOTA states for all degraded conditions, loading/empty/error/success in UI, tools, launch, policy; first-attempt clean in tests/gates; fail-closed on policy, approvals, taint.
- No change to omp's browser in Code, no ContractVersion edit, no registry beyond per-phase, no cloud browser.

## TODOs from Criteria + VERIFICATION + REVIEW (addressed + residual gaps)
- All 16 AG-72 covered across plans (see 72-PLAN-CHECK.md, 72-VALIDATION requirement map).
- VERIFICATION (per 72-VERIFICATION.md and 72-08): offline/live suites, architecture gates, build, notices, runbook with live-turn or not-run statement (OD-07 not approved).
- Gaps from plans/CHECK: waves/deps fixed in replan (72-08 depends 72-07); live tests use Assert.Skip + env; no headful without xvfb; host is Linux only (Win/Mac compile-only per OQ-373).
- SOTA states embedded per assignment: explicit loading (launch, screencast, migration not here but schema wait analog in acquirer), error (all 25+ degraded states with user sentence + fix), success (first-attempt in live tests, pane live), empty (no browser, disabled).
- Security: fail-closed (egress denies, navigation gate, approvals stage 2, taint IsUntrusted, MustBeLocal for dispatch/sensitive, download deny default); no leak (audit no page text, logs only origin/enum, redacted); owner scoped (grants per session).
- Sandbox/isolation precedent applied (env scrub, profile 0700, no extensions); graceful (degraded states, RemoteViewUnavailable, ModelCannotSeeImages).
- Deploy: runbook + bootstrap notes only (OQ-73); no Railway mutations.
- Verification/tests/gate: subsets claimed in 72-07/08; full offline + live gated on AUTONOGRAMMER_LIVE_BROWSER + xvfb; Architecture + DecisionRecord + Suppression + full build.
- Human items from VERIF (if any): live omp turn not run (OD-07); Win/Mac unmeasured.

## SOTA States for Browser Use / Launch / Policy / Tools / Vision (applied from 66-SOTA + phase specific)
1. **Explicit States** (loading/empty/error/success) in all browser paths:
   - LOADING: data-state=loading + timeout (launch 30s, page load 30s, wait max 10s, screencast ack); progress, no silent hang. E.g. ChromiumProcessLauncher, ScreencastPump, BrowserPaneViewModel states (Idle/Starting/Live), acquirer dialog, policy gate pause.
   - EMPTY: guidance + CTA (BrowserDisabled, BrowserNotInstalled, ManagedBrowserNotPinned, no sessions, RemoteViewUnavailable on web). Never blank pane or silent fail.
   - ERROR: recovery (retry via user take-over or close, fallback), error boundary + announcer (role=alert), redacted log. All 25 degraded: BrowserTooOld, SandboxUnavailable, ProfilePathUnsafe, FontsMissing, LaunchFailed, Crashed, UserHasControl, ControlRevoked, ModelCannotSeeImages, ImageBudgetExhausted, StaleRef, OriginDenied, BlockedByPolicy (names rule), DownloadDenied, PopupClosed, DialogDismissed, PageTooLarge, NavigationTimeout, RateLimited, SessionLimitReached, EgressGuardUnavailable, NoLocalApprover. 503/closed only on policy fail.
   - SUCCESS: explicit "first-attempt-success" log + count in live tests (PipeConnect, ScreenshotLadder, HostileCorpus, HeadfulSmoke); state machine logged (pane Live, tool result delivered). Healthy after launch + guard + approval + vision.
   - Implementation: BrowserPaneState, BrowserControlState, GetStateAsync reasons, BrowserLimits, PageMarker, degraded table in runbook and pane.

2. **Edge/Race/First-Attempt**:
   - Races: concurrent launch, redirect during gate, screencast ack vs stop, take-over vs agent call, session end vs in-flight. Fix: deterministic waits (ManualTimeProvider analog in tests, ack-driven pump), compound conditions (frame + !loading + approved).
   - Loaded host: fresh profile, env scrub verified in /proc, pinned flags on Chrome 153.
   - First attempt: 100% pass target in PipeConnectLive, HostileCorpusLive, ScreenshotLadderLive, HeadfulSmoke; no "reached-on-retry"; strengthened waits in live classes.
   - Other: oversized DOM, popup storms, dialog loops, rebinding in corpus; all assert host invariants (zero hits on denied, marker unforgeable, IsUntrusted always).

3. **Validation/Sanitization/Auth**:
   - Inputs sanitized (URL no traversal, ref bounds, text cap 20k after filter, manifest sha256/size, env drop all but allow-list).
   - Auth: grants via ICoworkGrantStore (origin, local), stage 1/2, MustBeLocal for dispatch/sensitive; no sub in dispatch credential.
   - Schema: options validated (IValidateOptions, bounds on maxes), manifest rows reviewed, no ContractVersion bump.
   - Contracts: IBrowserPaneFeed/Control additive L0; no bleed to Web (RemoteViewUnavailable).

4. **Security (fail-closed, no leak, owner scoped, least-privilege)**:
   - Fail closed: definitive failures (denied origin, stale ref, budget, policy block, MustBeLocal denied) clear to error + no partial; egress refuses before connect; navigation gate aborts before request; sandbox on; downloads deny or quarantine (0700, 50MiB, exec refuse, MustBeLocal release).
   - No leak: audit (FileCoworkToolAudit) no args/result bytes/page text; logs only enums/origin (LoggerMessage); Preview.Target = URL w/o query; image Description names origin; no OMP_*/secrets in browser env; taint IsUntrusted on every page block/image.
   - Owner scoped: grants per session; LocalOriginGrant user decision; no persistent across sessions unless PersistentProfile (opt-in).
   - Roles/priv: n/a (no DB here); process isolation (harness name, no Process ref in other L2).
   - STRIDE from plans + ADR-0034: mitigated (SSRF by guard+pin+gate, injection by taint+markers+filter+approvals+caps, spoof by chrome indicator, resource by caps+kill, privacy by no audit bytes).
   - A11y: polite/live for states, labels on pane, keyboard (F6, take-over), contrast no new colours; reduced-motion.
   - Graceful: degrade to text-only on no vision, Remote on non-desktop, disabled state on no browser.

5. **Graceful Degradation**:
   - Fallbacks: no browser -> NeedsSetup state + CTA; model no images -> ModelCannotSeeImages + chooser; web -> RemoteViewUnavailable; policy block -> OriginDenied/BlockedByPolicy; timeout/rate -> explicit error + resume.
   - Retries: bounded (per-tool 60s, wait 10s, actions 60/m, calls 500/sess); no infinite; user Stop/Close.
   - Cutover/launch: old version -> BrowserTooOld; no manifest -> ManagedBrowserNotPinned; sandbox fail -> SandboxUnavailable (no auto --no-sandbox except test env).

6. **Operational**:
   - Logs: structured, EventIds, fixed vocab (first-attempt-success in live); no page/typed/secret.
   - Recovery: Stop revokes control (ControlRevoked), close wipes profile; EndSessionAsync; test site two-port redirect zero-hit.
   - Observability: pane states, provider GetState, runbook probes, live tests under env + xvfb, Architecture gates (isolation, process spawn).
   - Deploy: runbook + Containerfile entry only; private network; browser.enabled: false in Cowork overlay verified.
   - Deterministic: fresh contexts per test, pinned Chrome 153 for live, no shared state.

## Plan Steps (8 plans, tracer-first, waves per 72-PLAN-CHECK + CONTEXT D-50)
Wave 1 (parallel, file-disjoint):
- 72-01 (Chromium/launch/options/session): locator, acquirer, launch plan, env allow, profile, options validator. SOTA: loading (acquire/launch), error (too old, no install, unsafe profile), success (pipe connect, version >=149, scrubbed env). AG-72-1,2,3,15.
- 72-04 (Contracts/Viewer/Presentation models): IBrowserPaneFeed/Control, Browser*State, ScreencastPump, LocalBrowserPaneFeed, TakeOverController, BrowserPaneViewModel. SOTA: states (Idle/Starting/Live/UserHasControl/ControlRevoked/Remote), first-attempt in pump/model. AG-72-6,7,13.

Wave 2:
- 72-02 (Egress/Policy): EgressGuardProxy, AddressClassifier, NavigationGate, OriginGrants, BrowserUrlPolicy. SOTA: loading (gate pause), error (OriginDenied, BlockedByPolicy, EgressGuardUnavailable), success (pin, zero-hit redirect). AG-72-4,5.
- 72-05 (Pane views): BrowserPaneView (Native), BrowserPane.razor (UI), browser-pane.css, mount in CoworkWorkbench, F6/keyboard. SOTA: a11y (labels, polite, focus, no new colour), states render, Stop outside frame. AG-72-6,7,13.
- 72-06 (Settings/acquisition): BrowserUse page, IBrowserUseSettingsSource, HeadSettings hand-off, acquirer UI. SOTA: empty/CTA for needs setup, dialog for confirm. AG-72-3.

Wave 3:
- 72-03 (Tools/approvals/vision/limits): BrowserToolProvider + 9 tools, BrowserApprovalStage2, PageContentExtractor/PageMarker, ScreenshotPipeline, BrowserLimits, guidance. SOTA: explicit states in results, IsUntrusted + markers + filter + 20k cap, ImageBudget, MustBeLocal, rate/session limits. AG-72-8-12,14,15.

Wave 4:
- 72-07 (Site + 19-page corpus + live/headful): LocalTestSite, Corpus/*, HostileCorpusLiveTests, HeadfulSmokeLiveTests, ScreenshotLadderLive, inventory. SOTA: first-attempt in live (env + xvfb), all corpus invariants (no unapproved nav, marker unforgeable, IsUntrusted, caps, taint). AG-72-16 + all prior.

Wave 5:
- 72-08 (Composition/docs/gates): BrowserUseComposition, AgentHead call, shortcut Stop chord + arm, runbook, architecture, changelog, notices, full suites. SOTA: overlay check (browser.enabled false), capability gated, guidance budget, all states in tests, full gate. AG-72-6,8,14,15 + phase criteria.

All locked D-* honored; deferred per CONTEXT (no relay, no upload, no cookie import, no bundle, OQ-360-377 defaults); no scope reduction.

## Files
- New: src/Autonogrammer.Harness.BrowserUse/* (Chromium, Egress, Policy, Tools, Content, Vision, Viewer, Options), tests/Autonogrammer.Harness.BrowserUse.Tests/* (Support, Site, Corpus), src/Autonogrammer.Contracts/Cowork/Browser/*, src/Autonogrammer.Presentation/Cowork/Browser/* + Settings, src/Autonogrammer.Native.Views/Views/Browser/* + Settings, src/Autonogrammer.UI/Components/Cowork/BrowserPane*, apps/Autonogrammer.Native/Cowork/BrowserUseComposition.cs, tests/*Browser*, docs/runbooks/browser-use.md, scripts/browser/pin-chromium.sh, chromium-manifest.json slot.
- Edited: Directory.Packages.props (Puppeteer), slnx/slnf, LayerMap, ProjectGraph, ArchitectureTests, AgentHead, Keyboard*, Shortcuts/*, HeadSettings*, CoworkWorkbench.razor, autonogrammer.css, docs/* (architecture, SUMMARY, CHANGELOG, open-questions), runbook, notices if drift.
- Read (precedent): phase 69/70 Cowork seams, ADR-0032 contracts.
- Deploy: runbook + overlay entry only.
- Verification: Harness.BrowserUse.Tests (offline + live), Presentation/Native/UI/Architecture, full Linux build, notices, secret-scan, live under env+xvfb.

## Verification (tests, gate, SOTA proof)
Per 72-VALIDATION + ROADMAP + 72-07/08:
- Offline: ag-dotnet test Harness.BrowserUse.Tests -c Release --filter-not-trait "Category=Live"; Presentation, Native, UI, Architecture (BrowserUseIsolation, ProjectGraph, DecisionRecord, Scaffold).
- Gate: CI locked restore, licences, build -warnaserror, full Linux slnf, format, repo-checks, secret-scan.
- Live: AUTONOGRAMMER_LIVE_BROWSER=1 CHROME_BIN=... /tmp/ag-bin/ag-dotnet test ... --filter-trait "Category=Live"; xvfb-run for HeadfulSmokeLiveTests.
- SOTA proof: states in pane/model/provider/launch/guard (loading/error/success), first-attempt clean (live classes 100% first), no-leak (audit/logs), fail-closed (egress/gate/approval/taint/caps), isolation (no Harness ref from views/UI, process spawn only in list), a11y (axe + Native + bUnit + labels/polite), overlay (browser.enabled false).
- 08 closes: full suites + docs + chord + composition.
- From VERIF: post-fix all criteria 1-5 + truths observable; human items (live omp, Win/Mac) recorded as not run / compile only.

## Success
All 16 AG-72 observable (pipe launch + Puppeteer 25.15, env/argv/profile/sandbox, locate/acquire checksum, egress+pin+gate, screencast desktop (no relay), take-over+F6, 9 tools+grounding+vision, image ladder+strip, vision chooser, taint+markers+filter, approval matrix+deny/quarantine, indicator+Stop, Dispatch rules, validated limits+clean audit, site+19 corpus live/headful), gate passes (locked, build, full suites, notices), SOTA states (explicit 25+ degraded + loading/empty/success for launch/policy/tools/vision/pane), security (fail-closed egress/gate/approval/taint, no leaks in audit/logs, owner grants), files (runbook + docs), verification complete per 72-08/VERIF. Deferred items stated honestly.

(Plans 01-08 implement; 08 is integration close; this SOTA is the quality overlay per assignment pattern from 66/68/69/74.) All D- honored; gaps closed or deferred with owner.

---
_Applied per user assignment for phase 72 SOTA update execution: synthesize from CONTEXT/ADR/brief/VALIDATION/PLANS, embed explicit states (25 degraded + 4 base), first-attempt, fail-closed, a11y, security, graceful, operational for browser use. Refs 72. Create SUMMARY for SOTA work._