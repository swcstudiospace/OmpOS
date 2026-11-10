# Phase 73 SOTA for Computer Use States (Cowork)

**Date:** 2026-11-10
**Refs:** 73-CONTEXT.md (D-01..D-43, esp. D-03 states, D-06 gate order, D-13..D-16 limits/kill/indicator/audit), 73-RESEARCH.md, 73-VERIFICATION.md (gaps + plan truths), 73-01..73-07 PLANs, ROADMAP AG-73-1..24, ADR-0035 (states, threat model 173-184), briefs/73-computer-use.md (degraded states).

## Objectives (from CONTEXT + ROADMAP + query)
- Cowork desktop control through 15 `computer_*` tools behind ONE policy gate with per-app grants, local approval, kill switch, always-visible indicator, local audit.
- Off by default; never Code mode, never cloud, never remote approval.
- All states explicit, exhaustive, with one remediation id each; no silent fallback.
- X11 verified; Win/mac compile-checked only.
- SOTA states, a11y, security overlays + verification tests gate (per user assignment).

## TODOs from Criteria + VERIFICATION + REVIEW (addressed by 01-07)
- All 24 AG-73 covered (01-06 core + 07 wiring).
- VERIFICATION gaps 1 (wiring) closed by 73-07 tracer + production call sites.
- Gaps 2-5 (indicator edge, allow-list assertion, live non-X11, window id) documented as OQ or accepted; no suppression.
- Duplicate threat IDs (T-73-02 etc) resolved by scoping 07 to wiring only.
- Full gate (core offline, X11 live under private Xvfb, -warnaserror builds, arch tests, licences) re-verified in 07.

## SOTA States for Computer Use (applied from CONTEXT D-03, D-06, degraded list, ADR)

1. **Explicit Availability / Error / Degraded States** (every path has one, remediation id, no raw titles/bytes):
   - `Available`, `Disabled` (opt-in or Cowork session missing), `UnsupportedPlatform`, `WaylandUnsupported` (clear "sign out and pick Xorg"), `NoDisplay`, `PermissionMissing(ScreenRecording|Accessibility)`, `InputInjectionUnavailable`, `KillSwitchUnavailable`, `IndicatorUnavailable`, `AuditUnavailable`, `ElevatedTarget`, `SecureDesktop`, `StaleFrame`, `FrameTooLarge`, `ImageBudget`, `RateLimited`, `StepLimit`, `UserTookOver`, `ApprovalTimeout`, `NoLocalApprover`, `Revoked`.
   - Each mapped to one `Remediation` id resolved in Presentation (advisor panel with deep link where possible).
   - Implementation: `ComputerAvailability`, `DenialCode`, `IComputerPermissionAdvisor`, status adapter, gate step 1-8.
   - SOTA: first-attempt coverage in tests (DenialCodeCatalogTests, GateOrderTests); no silent fallback (D-03); explicit in every tool result and status.

2. **Grant / Tier / Approval Lifecycle States**:
   - Grant: (session, AppIdentity, tier ViewOnly/ClickOnly/Full, origin Local/Dispatch, expiry, AutoApproveTyping).
   - Catalogue defaults + built-in denylist (no user remove; deny wins).
   - Sensitivity forces ask: credential titles, deletion/purchase, mail/messaging above ViewOnly, clipboard, first action per app, Dispatch origin.
   - Approval: Deny default, 120s timeout -> deny, host-observed facts + crop or text sample + security phrase, choices Deny/Once/Session.
   - No prompt while another open (process lock); no remote decision for per-app prompts.
   - Dispatch grants: 30m max, no AutoApproveTyping, re-ask on sensitive.
   - SOTA: races (concurrent action + grant expiry + window change) covered by serial lock + re-resolve + token checks; explicit in GrantStore, SensitivityClassifier, ApprovalSerialisationTests.

3. **Kill / Revoke / Indicator / Takeover States**:
   - Kill chord (default Ctrl+Alt+Shift+Escape, validated >=2 modifiers) registered on dedicated thread/conn before first action.
   - Trip: ReleaseAll, set Revoked, cancel turn, update indicator, audit.
   - Indicator: top-most non-focusable, visible while armed/acting, gate denies if absent from window list or minimised.
   - User takeover: >24px pointer displacement since executor -> UserTookOver pause.
   - Second grab while armed -> KillSwitchUnavailable.
   - SOTA: verified registration (KillSwitchCoordinatorTests), token between primitives, three stop paths (hotkey, indicator Stop, Cowork stop), explicit state machine.

4. **Audit / Frame / Budget States**:
   - Audit: owner-only JSONL (computer-use/audit/YYYY-MM-DD.jsonl, 0600), write-ahead before injection (deny on fail), seq + UTC + session + tool + summary + AppIdentity + title80 + decision + denial + origin + outcome + frameSHA + prevSHA (genesis), typed=length+hash only, no clipboard bytes, thumbnails optional+redacted, 30d retention.
   - Frames: per-session, mask non-granted+own (VisibleRegionMasker), CoordinateMap (scale/negative/multi-display), downscale to Vision:LongEdgePx, PNG <= MaxImageBytes (retry scale), budget (MaxImagesPerSession then ImageBudget), settle 400ms, PostActionFrame.
   - SOTA: chain truncation/writer failure denies (AuditLogChainTests); budget and mask pure functions unit-tested with z-order fixtures; no ledger/cowork_events write.

5. **Edge / Race / First-Attempt / TOCTOU**:
   - Re-resolve window after pointer move, before button; abort on identity change (TOCTOU).
   - Serial process-wide lock during approval.
   - Frame invalidation on layout change (StaleFrame).
   - Rate token bucket + step counter + failure pause.
   - First action per app + every sensitive always asks.
   - SOTA: explicit waits, no timing reliance in unit tests; live X11 cases (xdotool/xev/scrot oracles) prove oracle match on first attempt under private Xvfb.

6. **Platform / Backend States** (X11 verified; Win/mac compile-only):
   - X11: XGetImage / XTest / XRandR / EWMH or XQueryTree / scratch keycode for unmapped chars; hotkey on second conn.
   - Win: SendInput (abs+unicode), GDI BitBlt, UIPI/elevation/SecureDesktop, RegisterHotKey.
   - macOS: CGEvent, screencapture, CGWindowList, Carbon hotkey.
   - All report typed unavailable with remediation; no tool registration on bad platform.
   - SOTA: pure mappers (Keysym, SendInputBuilder, CoordinateScale) 100% unit; live oracle agreement on X11.

Implementation locations (core in src/Autonogrammer.Harness.ComputerUse*; head glue in apps/Autonogrammer.Native/Composition/ComputerUse/* + AgentHead/CoworkHead wiring in 73-07): Abstractions for contracts, Policy for gate/grants/kill, Frames/Audit/Tools for pipeline, backends per OS, Presentation for advisor/UI states, head composition for registration.

All states are first-attempt safe, race-covered by design (lock + token + re-resolve + hash), and have remediation guidance. No hidden states or silent best-effort paths.

(Plans 01-07 implement; 07 closes the reachability seam so states are observable from live Cowork.)

## Gap Closure Elevation (2026-11-10, per assignment)
## Gap Closure Elevation (2026-11-10, per 73-08 task 3 + 66 pattern)
### 1. Explicit States (loading/empty/error/success + domain)
- ComputerAvailability: all 11 + derived (StaleFrame, FrameTooLarge, ImageBudget, RateLimited, StepLimit, UserTookOver, ApprovalTimeout, NoLocalApprover, Revoked) each with one Remediation id. Loading (poll 1s, approval 120s, settle 400ms); empty (no-grant guidance + CTA); error (remediation + re-arm); success (first-attempt armed + registered).
- DenialCode: full D-06 8-step order (first failing wins, stable).
- Grant: per-app AppIdentity (host observed), tiers ViewOnly/ClickOnly/Full + Dispatch origin/30m ceiling, no AutoApproveTyping for remote.
- Kill: Armed (registered + indicator visible) / Revoked (trip releases + audits + cancels turn).
- Audit: owner JSONL chain (prev SHA, seq), write-before-inject, redacted (length+SHA for text, no bytes).
- Approval flow, frame, indicator states explicit in VM/gate.

### 2. Edge/Race/First-Attempt
- Races covered: second hotkey=KillSwitchUnavailable, pointer move + re-resolve, layout=Stale, grant during approval, Dispatch expiry, takeover >24px.
- First attempt: X11 7/7 + units + head wire on first creation.
- Loaded host: fresh context, deterministic waits.

### 3-7. Validation/Security/Graceful/Operational/A11y (cross ref other SOTA)
- Sanitized titles <=80, coords mapped, frame_id match.
- Fail closed on every path; owner scoped.
- Remediation for every unavailable; recovery on trip.
- Logs with remediation ids + first-attempt; indicator state.
- A11y names, Deny default, no literals wired (see A11Y SOTA).

Wired via 73-08 head (Build + AddTools + indicator action); observable in Cowork desktop enabled. Matches 66-SOTA-QUALITY-REQUIREMENTS exactly. (elevated)
 (duplicate old elevation removed; full 66 pattern elevation is above at the new section)
