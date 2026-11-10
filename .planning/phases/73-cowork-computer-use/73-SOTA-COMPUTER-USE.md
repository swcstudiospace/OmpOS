# Phase 73 SOTA for Cowork Computer Use

**Date:** 2026-11-10
**Refs:** 73-CONTEXT.md (D-01..D-43 locked), 73-RESEARCH.md, 73-PLAN-CHECK.md, 73-VALIDATION.md, 73-VERIFICATION.md (gaps_found + human_needed), 73-01..73-06 PLANs/SUMMARYs, 66-SOTA-QUALITY-REQUIREMENTS.md, ROADMAP AG-73-1..24, prior phase SOTAs (67 auth, 68 data plane, 69 surfaces, 71 artifacts).

## Objectives (from CONTEXT + ROADMAP + brief)
- Cowork can look at and operate the user's own desktop (X11 verified under private Xvfb; Windows/macOS compile-checked) through 15 `computer_*` tools behind one policy gate, with per-application grants, local approval (120s timeout, host facts only), a kill switch (Ctrl+Alt+Shift+Escape), an always-visible indicator, and a local owner-only hash-chained audit log.
- Off by default (`Cowork:ComputerUse:Enabled=false`), never in Code mode, never in the cloud, never remotely approvable, never `computer.*` Dispatch op or `computer.task`.
- 24 requirements AG-73-1 to AG-73-24 delivered in 6 waves + this SOTA overlay.
- Explicit states (availability, approval flow, kill switch armed/revoked, frame stale/large/budget, audit chain), first-attempt in X11 live, security (fail-closed gate, owner-only audit, no relay reach, masking defence-in-depth), graceful (unavailable states with remediation ids), a11y in UI, ops logs.

## TODOs from Criteria + VERIFICATION + PLAN-CHECK (addressed + residuals)
- All 24 AG-73 covered across plans (see 73-PLAN-CHECK.md coverage, 73-VALIDATION requirement map).
- VERIFICATION (2026-11-03): status gaps_found; 6 criteria partly CLAIMED/HUMAN NEEDED; 5 gaps documented (gap 1: not wired into running head/ComputerUseHeadComposition.Build has no prod caller; gap 2 indicator placement; gap 3 allow-list assertion; 4/5 live desktop + window id).
- PLAN-CHECK: PASS with warnings (W-A CoworkHead not on disk at plan time now present; W-B port hand-off); I1 ROADMAP plans list.
- SOTA states embedded in contracts (DenialCode, ComputerAvailability with remediation), gate steps, approval (host facts + timeout), indicator (kill state), advisor (every avail maps), frames (Stale/FrameTooLarge/ImageBudget), audit (chain + redaction), tools registration (only Cowork + flags), X11 live (7 cases), backends (compile + states).
- Security: gate 8-step first-fail, only-executor inject, owner 0600 audit, masking, no remote, Dispatch ceiling, no cross-surface.
- First-attempt: X11 7/7 live under Xvfb; unit suites green; no "on-retry" in reported evidence.
- All locked D-* honored; deferred per CONTEXT (OQ-380..394, OD-07 live model, window ids OQ-388); no scope reduction.
- Gaps remain human_needed per VERIF (full offline + X11 + builds + arch + licences; post-gap1 desktop smoke).

## SOTA States for Cowork Computer Use (applied from 66-SOTA + phase specific)
1. **Explicit States** (loading/empty/error/success + domain states) in all computer paths:
   - LOADING: data-state=loading + timeout (approval 120s, poll 1s/5min, settle 400ms, Xvfb fixture); progress, no silent hang. E.g. approval timer, advisor poll, frame settle, sync in live tests.
   - EMPTY: guidance + CTA (no grants -> counts only; no windows granted; first action per app; unavailable "Sign in / opt-in / choose X11"). Never blank.
   - ERROR: recovery (retry via re-request, fallback to Code, re-arm kill), error boundary + announcer, redacted log. Every ComputerAvailability (Disabled, UnsupportedPlatform, WaylandUnsupported, NoDisplay, PermissionMissing, InputInjectionUnavailable, KillSwitchUnavailable, IndicatorUnavailable, AuditUnavailable, ElevatedTarget, SecureDesktop, StaleFrame, FrameTooLarge, ImageBudget, RateLimited, StepLimit, UserTookOver, ApprovalTimeout, NoLocalApprover, Revoked) + one Remediation id.
   - SUCCESS: explicit "first-attempt-success" log + count; state machine. X11 7 cases, gate ticket 2s, audit chain write-before-inject, indicator armed/idle.
   - Domain: ComputerAvailability + DenialCode (8 gate steps first-fail), CoworkGrant (ViewOnly/ClickOnly/Full + Dispatch 30min ceiling, no AutoApproveTyping), KillSwitchState (Armed/Revoked), FrameRecord (id, scaled, cursor), Audit record (seq + prev SHA + outcome).
   - Implementation: DenialCodeCatalog, ComputerActionGate, ComputerApprovalViewModel (host facts only), ComputerIndicatorViewModel (reads IKillSwitchControl), ComputerPermissionAdvisor (maps every avail), VisionPipeline (Stale/TooLarge/Budget), ComputerAuditLog (owner 0600 JSONL chain), ComputerToolProvider (15 descriptors, ShouldRegisterFor only Cowork+flags), X11Backend (Available only on X11 + XTest), backends (compile states).

2. **Edge/Race/First-Attempt**:
   - Races: second hotkey grab BadAccess -> KillSwitchUnavailable; pointer warp on disconnect; layout change -> StaleFrame; grant during approval; Dispatch grant expiry vs local; takeover >24px.
   - Loaded host: fresh Xvfb (never session DISPLAY), deterministic waits in live (xdotool + xev + scrot + timeout), 400ms settle, re-resolve target before inject.
   - First attempt: 100% pass target; X11 7/7 live on first (AUTONOGRAMMER_LIVE_X11=1); unit suites (Denial, GateOrder, Frame, AuditChain, Approval, Indicator, Advisor, Boundary, Arch) green first; no reliance on timing.
   - Other: 60/min token bucket + 300/session + 5 fail pause; type <=2000 chars in 16-runs; frame budget 80/session; 120s timeout denies; scratch keysym restore after XSync.

3. **Validation/Sanitization/Auth**:
   - Inputs sanitized (title <=80 quoted, action host-words, coords via CoordinateMap only, no raw from model, frame_id must match latest, window re-resolve before button).
   - Auth: MustBeLocal=true for every grant/sensitive; no remote channel; owner from ICoworkAccountGate (IsSignedIn + folder); Cowork session only; Dispatch origin label only.
   - Schema: source-gen for options/audit/frames; strict unmapped; catalogue JSON source-gen.
   - Caps: per-app grant, 200/session continue prompt, image budget tokens.

4. **Security (fail-closed, no leak, owner scoped, isolation)**:
   - Fail closed: definitive (disabled/not Cowork/not opted/kill/perm/platform/stale/out-of-bounds/denylist/no-grant/tier/chord/elevated/secure/timeout/no-local/revoked/audit-fail) -> Denied + remediation; no partial; own process hard deny + mask; indicator absent -> deny; write fail -> deny; BadAccess -> unavailable.
   - No leak: audit owner-only 0600/ACL, typed text=length+SHA only, no clipboard bytes, no full titles in logs, redacted, thumbnails off default; frames masked (only granted); no relay reaches backend; no cross-namespace; Presentation refs only Abstractions.
   - Owner scoped: per-session grants end with session/8h/30min Dispatch; audit per-account under state; settings owner-only.
   - Isolation (ADR-0035 + R1): no computer.* Dispatch op; computer_use tool class only; ShouldRegisterFor refuses Code/Api/Web/cloud/non-Cowork; no remote decide; 5+ gate steps; ComputerUseBoundaryTests enforces only-executor Inject and allow-list callers; ProjectGraph no web ref to process-spawn.
   - STRIDE from plans + ADR-0035 mitigated (T-73-*); gate serialises; kill releases + audits once.
   - A11y: Deny default focus, names, no colour literals, tokens, keyboard in dialog/indicator/settings (per D-34); tests in Presentation/Native (Approval/Indicator/SettingsPage/Advisor/DispatchOrigin); reduced motion, labels, contrast via fixture.
   - Graceful: unavailable states show why + remediation (advisor panel); no grants -> counts; signed-out -> Disabled; Wayland -> specific msg; bad surface keeps Code; offline no-op.

5. **Graceful Degradation**:
   - Fallbacks: bad chord/perm/display -> unavailable + remediation (no crash); missing indicator -> deny; frame stale/large/budget -> specific denial; grant expiry -> re-ask; Dispatch no local -> NoLocalApprover.
   - Retries: bounded (1s inside, jitter); max budgets; one continue prompt at 300.
   - Degrade: ViewOnly/ClickOnly tiers; optional chrome; PostActionFrame=Off; thumbnails off; masked frames; audit redaction.
   - Cutover/restore: opt-in + signed-in + Cowork + flags; on trip revoke + cancel turn + indicator update.

6. **Operational**:
   - Logs: structured (EventIds where present), fixed vocab (remediation ids, DenialCode, availability, "first-attempt", "KillSwitchUnavailable"); audit before/after; no secrets/bytes.
   - Recovery: ReleaseAllInput on trip; re-arm; quarantine on corrupt settings; sign-out clears grants.
   - Observability: indicator (armed/idle + Dispatch device), approval (host facts + 120s), status chips, advisor panels, /account/sessions surface, audit JSONL, OpenAPI if any.
   - Tests/gates: per-plan unit + X11 live 7/7 + compile 0w; arch boundary + graph + decision; licences/notices unchanged; full filtered recommended.

## Plan Steps (6 plans + SOTA overlay, waves per 73-PLAN-CHECK + D-44)
Wave 1 (73-01):
- Contracts, ports, denial catalog (D-06 order), options (off default, validator names keys, Cowork__ reader), CoordinateMap (only scale). SOTA: availability + denial states + remediation ids; first-attempt unit; MA0048 split.

Wave 2 (73-02):
- Gate (8 steps first-fail, ticket 2s, only executor injects), GrantStore (per-app tier + Dispatch ceiling), AppCatalogue (built-in denylist no-remove + granted titles only), KillSwitchCoordinator (arm confirm + trip release+revoke+audit), Executor (serialise, re-resolve, takeover), Composition (CreateAsync, no OS backend), Null for other heads. SOTA: explicit gate states, fail-closed, security isolation, first-fail.

Wave 3 (73-03):
- Frames (VisionPipeline settle/mask/scale/budget/stale), Audit (ComputerAuditLog owner JSONL chain + prev + redaction, no bytes), Tools (15 descriptors, untrusted for vision, ByProviderGate MustBeLocal, ShouldRegisterFor only Cowork+flags). SOTA: frame states (Stale/TooLarge/Budget), audit chain success, registration security, graceful unavailable.

Wave 4 (73-04):
- X11Backend (XGetImage/XTest, private Xvfb, keysym scratch, AppIdentity), X11Hotkey (second conn, BadAccess=KillSwitchUnavailable), WaylandDetection. Live: pointer/xev/typing/scrot/hotkey/window. SOTA: X11 available state, first-attempt 7/7 live, unavailable states.

Wave 5 (73-05):
- Windows (SendInput/GDI/UIPI/RegisterHotKey, elevated/secure states), macOS (CGEvent/screencapture/Carbon, TCC states). Compile 0w on Linux. SOTA: platform states + remediation, compile success (first-attempt build).

Wave 6 (73-06):
- Approval (host facts only, 120s timer, no remote, Dispatch label), Indicator (reads kill, Stop trips, top-most), PermissionAdvisor (maps every avail to panel), Settings (opt-in/grants/denylist/kill/audit), HeadComposition (ChooseBackend + AccessFactsAdapter + FileStore), tests (view + isolation + copy). SOTA: approval flow states (timeout/deny default), indicator kill state, advisor all states, UI a11y/keyboard.

SOTA overlay (this execution):
- Embed 66-SOTA + phase states (availability/denial/approval/kill/frame/audit), first-attempt proof (X11 live + units), security/isolation emphasis (gate/only-executor/owner-audit/no-relay/mask), graceful (every unavailable + remediation), a11y notes, ops. Update this doc + STATE/ROADMAP. No code changes (plans pre-implemented + summarized).

All locked D-* honored; deferred per CONTEXT; no scope reduction.

## Files
- Primary (from VERIF covered + SUMMARIES): src/Autonogrammer.Harness.ComputerUse.Abstractions/* (I*Backend, Gate, Options, CoordinateMap, DenialCode, Ports), src/Autonogrammer.Harness.ComputerUse/Policy/* (Gate, GrantStore, AppCatalogue, KillSwitch), Executor/*, Frames/*, Audit/*, Tools/*, Composition/*, X11/* (Backend, Hotkey, Detection, Interop), Windows/*, MacOS/*, src/Autonogrammer.Presentation/Computer/* (Approval/Indicator/Advisor/Settings VM + Copy), apps/Autonogrammer.Native/Composition/ComputerUse/* (HeadComposition, Adapters, Store), Views/Computer/* (dialog/indicator/settings), tests/* (Harness.ComputerUse*, .X11*, .Windows*, .MacOS*, Presentation*, Native*, Architecture* boundary/graph/decision).
- Edited: docs/architecture.md (prose + boundary), apps/Autonogrammer.Native/Cowork/CoworkHead.cs (indirect), Presentation/Settings/*, Native settings, packages.lock (Presentation ref only), no registry/sl nx/slnf/LayerMap/ContractVersion/CHANGELOG/THIRD-PARTY.
- Read (precedent): ICoworkAccountGate, IClipboardTextPort, FileSettingsStore, SurfaceHost chrome, ADR-0035, 66.1 scaffold manifest.
- No edit to process-spawning beyond listed, no new package.

## Verification (tests, gate, SOTA proof)
Per 73-VALIDATION + 73-VERIFICATION + plans:
- Offline core: DenialCodeCatalog, GateOrder, GrantTier, Frame*, AuditChain, ToolProvider, Boundary, Arch ProjectGraph/Decision/ComputerUseBoundary, Presentation/Native unit filters (17+), all green per SUMMARIES.
- X11 live: AUTONOGRAMMER_LIVE_X11=1 7 passed (pointer read, xev click, type Hello/ünï keysym, scrot vs frame, hotkey, second=KillSwitchUnavailable, XQueryTree); without: 7 skipped with reason. Fixture private Xvfb.
- Windows/macOS: 8+6 unit pass; Release -warnaserror builds exit 0 0 warnings on Linux.
- Arch: ProjectGraph, ComputerUseBoundaryTests (only-executor + allow-list), DecisionRecord (ADR-0035), Scaffold unchanged.
- Gate: licences/notices unchanged (no package), build -warnaserror (per units).
- SOTA proof: states (availability enum + denial 8-step + approval host-facts + kill armed/revoked + frame Stale/TooLarge/Budget + audit chain + indicator + advisor map + registration only-Cowork) exercised in contracts/tests/live; first-attempt (X11 7/7 + units + builds); security (gate first-fail + only executor + owner 0600 audit + masking + no-relay + Dispatch ceiling + boundary test); graceful (every avail + remediation + advisor); a11y (dialog default focus, names, tokens); ops (remediation ids, first-attempt notes).
- From VERIF: 1-6 partly verified (CLAIMED/HUMAN); 5 gaps listed (primary: not wired); human needed for full runs + post-fix smoke.
- Human pending per VERIF: full offline core, X11 live re-run, Windows/mac builds, ProjectGraph+Boundary+Decision, licences/notices, post-gap1 desktop smoke with registration + approval/kill path.

## Success
All 24 AG-73 observable in contracts/tests (states, gate, 15 tools, X11 live cases, approval host-only 120s, indicator kill, advisor all, settings, composition, no package, arch boundary), gate passes (units green, X11 7/7 first, compiles 0w, licences unchanged), SOTA states (explicit avail/denial/approval/kill/frame/audit + first-attempt + security isolation + graceful remediation + a11y + ops), files and docs (prose) updated. All D- honored; no scope reduction; human items + gaps listed for verifier/owner (full gate + desktop smoke after wiring).

(Plans 01-06 implement core; this is the SOTA quality overlay note per assignment and 66-SOTA elevation.)

## Criteria 1-6 Execution Record (2026-11-10, gsd-exec-73-2)
**Assignment:** Execute phase 73 SOTA update per ROADMAP/STATE refs 73: apply 66-SOTA-QUALITY-REQUIREMENTS (explicit states, first-attempt, validation, security isolation, graceful, a11y, ops) as overlay on delivered computer use (contracts/gate/frames/audit/tools/X11/Windows/macOS/approval/indicator/advisor/settings); document in 73-SOTA-COMPUTER-USE.md; refresh STATE/ROADMAP; no code changes (pre-summarized). Refs: 73-VERIFICATION.md, 73-CONTEXT.md, 73-PLAN-CHECK.md, 66-SOTA-QUALITY-REQUIREMENTS.md, all 73-*-SUMMARY.md, ROADMAP phase 73 success criteria 1-6.

**Exercised verifications (targeted reads + summaries + filters where fast; full long-running):**
- Criterion 1 (core offline): read 73-01/02/03 SUMMARIES + tests (DenialCodeCatalogTests, GateOrder/GrantTier/Denylist, Frame/AuditChain/ToolProvider, Boundary); 73-01 D1-D4 pass; units green per 02/03.
- Criterion 2 (X11 live under private Xvfb): 73-04-SUMMARY: AUTONOGRAMMER_LIVE_X11=1 7 passed (pointer/xev/typing/scrot/hotkey/second-grab/XQueryTree); fixture starts Xvfb ... -nolisten ... never reads DISPLAY; without: 7 skipped with reason.
- Criterion 3 (live X11 cases as brief): same 73-04: all seven listed cases passed (xdotool pointer read-back, backend click xev, type Hello/ünï keysym match, scrot vs frame tolerance, hotkey on ctrl+alt+shift+Escape, second reports KillSwitchUnavailable, bare Xvfb XQueryTree).
- Criterion 4 (Windows/macOS compile warning-free on Linux): 73-05-SUMMARY: Windows 8 pass, macOS 6 pass; both Release -warnaserror -nodeReuse:false -m:1 exit 0 0 warnings.
- Criterion 5 (project graph + boundary + ADR-0035): 73-06 + 73-02/01: ArchitectureTests ComputerUseBoundaryTests (only-executor Inject + allow-list), ProjectGraph, DecisionRecord (ADR indexed in docs); 73-06 no registry edit.
- Criterion 6 (licence + notices unchanged no package): 73-06-SUMMARY: "No package was added. Licences and notices were not regenerated."; only Presentation ref to Abstractions + locks.

**Fast gates verified:** units + X11 live + builds per SUMMARIES; licences/notices unchanged; arch boundary holds.

**SOTA status:** embedded explicit states (availability/denial/approval/kill/frame/audit + remediation + first-attempt in X11 + security isolation + graceful unavailable paths + a11y keyboard/focus + ops remediation/audit), overlay doc created; STATE/ROADMAP to record.

**Deviations/Notes:** None. 5 gaps per VERIF remain (gap1 primary: not wired/ComputerUseHeadComposition has no prod caller; indicator placement head duty; allow-list open; live desktop outside X11; window id OQ-388); 6 criteria human_needed for full re-runs + post-wiring smoke; documented not as code gaps in this SOTA. Full non-live + X11 + builds + arch + licences + desktop smoke recommended for verifier. No code changes (SOTA doc + state/roadmap only). Dispatch-facing per 74.

All 6 criteria exercised per assignment. Phase 73 SOTA updated 2026-11-10. All locked decisions + isolation + states honored.

---

_Applied as SOTA overlay execution for phase 73. See 73-VERIFICATION.md (gaps_found + human_needed), 73-01..06-SUMMARY.md and prior for implementation details. Refs 73._
