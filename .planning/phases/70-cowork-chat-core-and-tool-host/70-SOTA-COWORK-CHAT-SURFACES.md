# Phase 70 SOTA for Cowork Chat Core and Tool Host

**Date:** 2026-11-10
**Refs:** 70-CONTEXT.md (D-01..D-40 locked), 70-RESEARCH.md, 70-VERIFICATION.md (gaps_found 2026-11-03; steering, native compile, build hygiene), 70-PLAN-CHECK-INTEGRATION.md, 66-SOTA-QUALITY-REQUIREMENTS.md, ROADMAP AG-70-1..14, existing 70-01..70-06 PLANs/SUMMARYs, ADR-0032.

## Objectives (from CONTEXT + ROADMAP)
- Signed-in user holds Cowork conversation with local omp over ACP (streaming, stop, retry, steer, history, file/image attachments) under Cowork profile (distinct system prompt, permissions, folder boundary) vs Code.
- One governed seam (tool host) for Artifacts (ADR-0033), Browser Use (ADR-0034), Computer Use (ADR-0035) to hand tools to omp, with approvals, caps, audit, images to model+transcript.
- Six units delivered across 70-01..06.

## TODOs from Criteria + VERIFICATION (addressed or noted)
- All AG-70 covered (union of plan frontmatter).
- VERIFICATION gaps:
  - Steering no-op: fixed (current code delivers via LiveSteering.SteerAsync when run active, falls back to SubmitAsNewTurn; tests assert _session/steering and prompt hand-over; CoworkSessionHost.SteerAsync + SendOrSteerAsync implement per D-08).
  - Native compile: still requires .NET 10 + full head setup (env limit); *Cowork* tests filtered to library surface only.
  - Warning-free: pragmas for CA1031/MA0051 retained with justification (provider faults fixed-text, 8-step seam); SUP rows in 70-01/06 SUMMARYs; converted one instance to [SuppressMessage] for hygiene (SOTA).
  - ProjectGraphTests: not run in this env (headless Linux, no full slnf); claimed in plan.
  - SSE image bound: deferred (U-1, 3 MiB > 1 MiB line); noted as open risk.
  - Terminal: NotSupported by design (per plan).
- SOTA overlay applies 66 quality bar + phase specific (explicit states in Cowork models/views, a11y in composer/approvals/transcript, first-attempt, graceful, security fail-closed, no-leak, owner boundaries).

## SOTA States for Cowork (applied from 66-SOTA + phase specific)
1. **Explicit States** (loading/empty/error/success) in chat flows:
   - LOADING: data-state + timeout (150s a11y); progress in composer, provider chips (Available/Needs setup/Unavailable).
   - EMPTY: guidance + CTA (no sessions, first Cowork prompt hint).
   - ERROR: recovery (retry last, steer fallback), error boundary + announcer; fixed failure texts from host (no secrets).
   - SUCCESS: "first-attempt-success" log + count on turn; explicit origin Local.
   - Implementation: CoworkConversationModel (5+ states), CoworkComposerViewModel, ProviderStatusChip, CoworkApprovalQueue; AgentSessionRuntime reuse with no-op stage/context.

2. **Edge/Race/First-Attempt**:
   - Races: steer during run vs hand-over (SendOrSteerAsync checks IsRunActive then LiveSteering; tests cover promptRequired/NotRunning paths); address/loaded host handled by UI thread affine + explicit origin set.
   - First attempt: 100% target via deterministic waits in runtime (inherited); no timing reliance in host (lease + call token serializes).
   - Other: model no-images turns attachments/tools off with changeable message (D-29); over-cap images refuse before send.

3. **Validation/Sanitization/Auth**:
   - Inputs: JSON object <=256 KiB, descriptor names validated (D-14), image caps (3 MiB/2 per result, 10 MiB payload -> placeholder).
   - Auth: Cowork uses ICoworkAccountGate (R8); origin from lease only (model origin arg ignored, T-70-01-01 mitigated).
   - Schema: source-gen where used; strict in folds.

4. **Security (fail-closed, no leak, owner scoped, taint, audit)**:
   - Fail closed: unoffered refused before preview; policy/approval deny -> fixed failure; timeout=denial; over-cap=failure never truncate; gateway refuses __ names and cross-session; ByProviderGate only Allow when Available.
   - No leak: audit entries no args/result bytes; provider exceptions logged without args; untrusted results wrapped in labelled envelope (D-19); CSP not widened unless data: needed for images (same change).
   - Taint: first IsUntrusted text/image permanently taints; Auto/Skip then ask every actuating; host state only.
   - Owner: CoworkPathBoundary refuses account/secrets/ssh; env holds provider keys only; Storage folder boundary.
   - STRIDE from 70-01 threat model + D-*: mitigated (T-70-01-01..05); hash-chained local FileCoworkToolAudit (D-26, pattern from ADR-0035).
   - A11y: focus order (transcript, composer, approvals), accessible names, reduced motion, no colour literals, labels, axe/wcag via CssContract/StyleAccessibilityTests (AG-70-11); Native a11y not executed (compile gate).
   - Graceful: gateway down -> session starts without Cowork tools + reason message; provider Unavailable shows on chip; model no-images degrades attachments; one call at a time per lease.

5. **Graceful Degradation**:
   - Fallbacks: steer falls to new turn; unavailable tools listed but not offered; image over cap -> placeholder.
   - Retries: bounded in runtime (inherited from Code); host timeouts 120s map to denial.
   - Degrade: optional image tools off; non-destructive grants in ByPolicy.

6. **Operational**:
   - Logs: structured via ILogger (fixed vocab in host); events raised for Started/ApprovalRequested/Completed/Failed/Denied (D-23).
   - Recovery: sign-out stops engines after confirm; rows persist; resume from history.
   - Observability: provider status chips, approval queue, audit JSONL; tests prove 8-step order, caps, taint, MustBeLocal.

## Plan Steps (6 plans, tracer-first, waves per existing)
Wave 1 (70-01 tool host contract):
- Tool host, 8-step, caps, taint, default gate, file grant/audit, fake + contract base. SOTA: explicit order proof, fail-closed results, tamper-detect audit, ByProviderGate broker rule per D-24.

Wave 2 (70-02 gateway):
- McpGateway extension for cowork_*, disjoint names, image results via real client. SOTA: session isolation, refusal before host, image bytes/mime parity.

Wave 3 (70-03 profile/permissions):
- CoworkSessionProfile (no-ext, ask mode, add-dir, prompt, overlay), CoworkPermissionResolver, path boundary, env. SOTA: owner-only overlay/prompt, always-ask delete, mcp__cowork_* allow, tainted overrides.

Wave 4 (70-04 native host):
- CoworkSessionHost as IAgentPanelHost, CoworkEngineComposer, launch. SOTA: explicit origin, steer delivery (now implemented), gateway health before session/new, degraded message on fail.

Wave 5 (70-05 images):
- ToolUpdateContent + Image, parity folds (GenUi + Sessions), render/export/web, caps. SOTA: same ImageBlock, alt text, placeholder, CSP co-change.

Wave 6 (70-06 views/composition):
- Cowork*Model, .razor/.axaml views, chips, queue, approval card, head composition. SOTA: explicit states, focus/keyboard/a11y, reduced motion, no literals, model no-image degrade.

## Files
- src/Autonogrammer.Cowork.Abstractions/*, src/Autonogrammer.Cowork/* (host, chat, tools)
- src/Autonogrammer.Harness.McpGateway (Cowork results)
- src/Autonogrammer.Contracts/Ui (ToolUpdate* + ImageBlock)
- apps/Autonogrammer.Native/Cowork/* + composition
- src/Autonogrammer.Presentation/Cowork/*, src/Autonogrammer.UI (views, models)
- tests/Autonogrammer.Cowork.Tests/*, .Native.Tests/Cowork/*, .Sessions.Tests, .Presentation.Tests, .UI.Tests
- 70-SOTA-*, 70-VERIFICATION updated

## Verification (tests, gate)
Per 70-VERIFICATION + success:
- Cowork.Tests full, McpGateway.Tests (incl image), filtered Native *Cowork*, Presentation/UI/Sessions (image parity), Architecture ProjectGraph (when runnable).
- Build: /tmp/ag-bin/ag-dotnet build ... -warnaserror (pragmas justified or attributed).
- SOTA proof: states in models, steer delivery tests, first-attempt paths, a11y contracts, security (no-leak, taint, owner), 8-step + caps in contract tests, gateway image parity.
- From VERIF: post-SOTA steering delivered + asserted; hygiene note on remaining pragmas.

All locked D-* honored; deferred items (SSE bound, terminal, full Native gate) recorded; no scope reduction.

## Success
AG-70-1..14 observable, SOTA states/a11y/security/steer in Cowork flows, tool host governed seam, images parity, no leaks, graceful degraded, audit local hash-chain. Gate passes on runnable slices.

(Plans 01-06 already implement core; this + gap closures + 66-SOTA overlay is the SOTA update.)

## Deviations / Notes
- Native full build/test requires env with .NET 10 + Avalonia deps (per AGENTS.md dev box limits); filtered execution + static verification used.
- Steering was GAP in VERIF 2026-11-03; now implemented per current source + tests.
- Image SSE limit remains open risk (deferred U-1).
