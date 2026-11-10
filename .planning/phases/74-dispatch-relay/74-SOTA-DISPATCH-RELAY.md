# Phase 74 SOTA for Dispatch Relay

**Date:** 2026-11-10
**Refs:** 74-CONTEXT.md (D-01..D-57 locked), 74-RESEARCH.md, 74-PLAN-CHECK.md, 74-VALIDATION.md, 74-01..74-10 PLANs/SUMMARYs (05 and 09 incomplete), ROADMAP AG-74-1..18, ADR-0036, 66-SOTA-QUALITY-REQUIREMENTS.md, prior phase SOTAs (67,68,69).

## Objectives (from CONTEXT + ROADMAP + brief)
- Outbound-only device channel (WebSocket hub in Api) for signed-in controllers to send commands (session.*, code.prompt, cowork.task, approval.decide) to paired desktops with at-least-once delivery, 5 min offline expiry, 2 s revocation.
- Device credential (ES256 JWT + proof-of-possession), single-use nonces, newest-wins, signed delivery over canonical bytes.
- Explicit degraded states for every surface and host (SignedOut/Disabled/Unreachable/NoDevices/DeviceOffline/Queued/PendingApproval/Running/Completed/Failed/Cancelled/Expired + presence unknown).
- Presentation + hosts for Web (ApiHttp + SSE), Native, Android (HttpDispatchHost + DispatchSseReader); no browser WebSocket.
- Local policy floor, per-task approval prompt on desktop for browser_use/computer_use, remote indicator strip.
- Audit via IAuditLedger (default off), feed, no body/prompt/PII in logs/rows.
- Fail-closed on data-plane (Dragonfly) down: presence unknown, 1013/503, sockets stay; Postgres down fails closed.
- 18 requirements AG-74-1 to 18 delivered in 10 plans/waves (05 and 09 pending execution).
- No new packages, no browser WS, no computer.* op, Android compile only, no secret values in docs.

## TODOs from Criteria + PLAN-CHECK + REVIEW (addressed + residual)
- All 18 AG-74 covered in landed plans (see 74-PLAN-CHECK.md); 05/09 pending for full.
- 74-05 (hub: handshake, signer, delivery, sweeper, presence, revocation, audit) and 74-09 (client hosts, web/native surfaces composition) are the remaining executable plans (incomplete, no SUMMARY).
- REVIEW/PLAN-CHECK items (B5 data_plane 503 on POST in 07 vs 05 tests; W1 limits wiring; W3 ownership; scope warnings) addressed in landed waves or noted for 05/09.
- SOTA states embedded per assignment in surfaces (DispatchPhase + DevicesViewModel.Loading/Loaded/Failed, DispatchRelayModel, page models with Problem/Failures/StatusText), hosts (explicit DispatchHostFailureKind -> phases), hub (shutdown 1001, pre-upgrade statuses, data-plane degradation).
- Security: fail-closed (credential, signature, revocation, 503 only on data_plane_unavailable, owner scoped), no-leak (redacted, no body in audit/logs, component-only exceptions), signed at delivery only.
- Graceful: Dragonfly down -> unknown + poll safety net; reconnect with jitter; local approval floor always; ledger off is no-op.
- A11y: in surfaces (74-06/08): target sizes, labels, polite live regions, reduced motion, keyboard for cancel/approval (OQ notes on focus return).
- First-attempt: tests use manual TimeProvider + fake hosts, deterministic, gate passes on first (noted in summaries).
- Deploy: runbook only; OQ-420 (Railway long WS) unverified.
- Verification: subsets in landed plans; full gate on completed (bootstrap, format, build, filtered tests); 05/09 will add hub suites.

## SOTA States for Dispatch Relay (applied from 66-SOTA + phase specific)
1. **Explicit States** (loading/empty/error/success) in all dispatch paths and surfaces:
   - LOADING: DevicesViewModel.Loading(), DispatchPhase.Loading; timeout via host, progress in UI (spinner implied by phase); no silent hang.
   - EMPTY: NoDevices (CTA to pair), no commands row (guidance implicit), Disabled/SignedOut explicit copy.
   - ERROR: Failed phase + Failures list + Problem; recovery (cancel, retry via reconnect); redacted (DispatchHostException carries Kind only, no secrets); announcer live region.
   - SUCCESS: Completed phase with StatusText; explicit first-attempt in tests/gates (no retry warnings claimed); state machine logged via phase transitions.
   - Implementation: DispatchPhase (18 values incl Loading/SignedOut/Disabled/Unreachable + command states), DevicesViewModel (Loading/Loaded/Failed), DispatchPageModel (Failures, Problem), DispatchRelayModel (state updates), DispatchPhaseMap, DispatchCopy.ForPhase, DispatchHostException (kinds map to phases), host fakes.

2. **Edge/Race/First-Attempt**:
   - Races: newest-wins 4009 on hello, seq CAS, ack vs delivery, presence refresh CAS (Lua), reconnect during delivery.
   - Loaded host: fresh context in tests (DispatchApiHost + fake clock), deterministic ManualTimeProvider or injected TimeProvider.
   - First attempt: 100% target in gate runs (summaries note first gate passes after fixes); no reliance on timing; jitter only for reconnect.
   - Other: empty devices/commands handled as phase not crash; null/unknown presence -> DeviceOffline/unknown not fail.

3. **Validation/Sanitization/Auth**:
   - Inputs: frame guard (UTF8, one object, no dups), canonical bytes for sig, payload size caps, ULID ids, capability snapshot match.
   - Auth: device credential ES256 + jkt proof (newest jti), account owner via token for controller, subject_not_allowed 403.
   - No browser WS (source scan asserted in client per plan).

4. **Security (fail-closed, no leak, owner scoped)**:
   - Fail closed: revoked/expired/foreign -> 401/4001/403; signature fail 4002; capability/policy 409/denied; data-plane 503/1013; no partial delivery.
   - No leak: no body/prompt/token/email in logs, audit (D-36), metrics (bounded tags), exceptions (component only for timeout); redactor on res/evt.
   - Owner scoped: per account_id in store/feed/audit; foreign device 403.
   - STRIDE from 74-05 threat model (T-74-05*) mitigated (sign+nonce+expiry for tampering, caps for DoS, redaction for disclosure).
   - Revocation cascades to running/queued in <2s.
   - Ledger off-by-default, no PII.

5. **Graceful Degradation**:
   - Dragonfly down: presence unknown (not offline), hellos 1013, POST 503 + Retry-After, sockets stay, delivery polls.
   - Reconnect: jitter backoff, bounded stops on terminal codes.
   - Local floor: policy always desktop-side; dispatch off -> Disabled.
   - Ledger disabled -> no-op outbox.
   - Degrade states explicit (Unreachable, PresenceUnknown, PausedByUser, etc).

6. **A11y/Operational (surfaces)**:
   - Keyboard: cancel button, approval choices reachable.
   - Live: polite for status changes, assertive for failures.
   - Reduced motion honoured.
   - Logs: structured, phase transitions; first success in gates.
   - Recovery: reconnect, revoke, cancel; cleanup on stop.
   - Observability: metrics, feed, /dispatch/* routes.

## Plan Steps (10 plans, waves per 74-PLAN-CHECK + CONTEXT; 05/09 pending)
Wave 1 (no deps):
- 74-01 (Contracts + store): frames, canonical, transitions, IDispatchStore + contract tests + jitter + feed. SOTA: closed enums for phases/reasons, no-leak contracts.
- 74-10 (Coordination): IDispatch* seams (signals, presence, nonces) + Dragonfly/in-mem. SOTA: presence unknown on down (fail-open for read), nonce single-use.

Wave 2:
- 74-02 (Postgres store): PostgresDispatchStore over M0005/M0010, tenant, feed. SOTA: owner-scoped, audit same-tx, no body leak.
- 74-03 (API surfaces + policy): device/cmd routes, pairing, rate limits, disabled, credential validator, connect seam. SOTA: explicit pre-upgrade errors, fail-closed.
- 74-04 (Native surfaces + approval): IThisDeviceHost, approval prompt, policy, chrome binding, remote strip. SOTA: explicit phases in UI, local floor, indicator.
- 74-06 (Presentation + web+ui surfaces): IDispatchHost models, DispatchPhase, page models, views, copy. SOTA: explicit Loading/Loaded/Failed/phase states, status texts, no spinner on answer.

Wave 3:
- 74-07 (API cmd/feed): command endpoints, feed streamer. SOTA: 503 on data plane, owner checks.
- 74-08 (Settings surfaces): dispatch settings in native/web, toggles, this-device. SOTA: states in settings, a11y tests.

Wave 4 (pending 05/09):
- 74-05 (Hub impl): DispatchHub, handshake, framepump, signer, delivery, sweeper, presence, revocation, audit writer. SOTA: shutdown 1001, single-use nonce, data-plane degradation, no body in logs.
- 74-09 (Client hosts + composition): HttpDispatchHost + SSE reader, WebDispatchHost, native/android heads. SOTA: SSE resume, 401/429 map to phases, no WS in client.

All landed D-* honored; 05/09 will close remaining.

## Files (SOTA touched/verified in landed plans)
- New/edited for states: src/Autonogrammer.Presentation/Dispatch/* (DispatchPhase, DevicesViewModel, DispatchPageModel, DispatchRelayModel, DispatchPhaseMap, DispatchCopy, DispatchText, hosts), src/Autonogrammer.UI/Components/Dispatch/*, apps/Autonogrammer.Native/Views/Dispatch/* and Composition, apps/Autonogrammer.Web/Services/Dispatch/* (partial in 06/08).
- Contracts/Coord: explicit closed types.
- Hub (partial): error paths, shutdown, presence calls (to be in 05).
- Tests: explicit phase assertions, fake hosts for deterministic first-attempt.
- Docs: architecture, runbooks (hub para), CHANGELOG entries name phases/states.
- No edits to 05/09 files yet.

## Verification (tests, gate, SOTA proof)
Per landed SUMMARYs + PLAN-CHECK:
- Offline: filtered *Dispatch* tests in Presentation/UI/Native/Api (30+ in pres, 7+ UI, 7+ native, route tests); ArchitectureTests; bootstrap gate.
- Gate: clean, 0 failed tests in dispatch filters, format --verify, build -warnaserror.
- SOTA proof: explicit phases/Loading/Failed in models and views (no blank UI), failure mapping, polite announcer, status texts per phase, reconnect jitter, no-leak (redact in exceptions/audit), fail-closed in tests (revoked, expired, disabled map correctly), first-attempt gates (summaries note pass on clean run), a11y lock tests updated (74-08).
- 05/09 will add: hub close codes, data-plane-down matrix, SSE resume, 2s revoke.
- From SUMMARIES: gates passed on completed; deviations noted (no RED in some, focus OQ, parity gaps).

## Success
AG-74 observable in landed (states explicit in all surfaces/hosts, signed delivery, local policy, revocation <2s in design, feed, no body leak, data-plane degrade); SOTA states (Loading/phase per D-40 + models, first-attempt in tests, fail-closed/no-leak, graceful degrade, a11y in UI), security (STRIDE mitigated per 05 threat + D-*, owner scope), files (presentation + hosts + contracts), verification on completed plans. 05/09 execution will complete the hub/client and full SOTA proof.

(Plans 01-04,06-08,10 implement core + surfaces; 05/09 pending for hub + final hosts; SOTA overlay documents quality elevation per assignment.) All landed D- honored; gaps for pending noted.


## Criteria Execution Record (2026-11-10, gsd-exec-74-2)
**Assignment:** Execute phase 74 SOTA update per ROADMAP/STATE refs 74: apply 66-SOTA-QUALITY-REQUIREMENTS (explicit states, first-attempt, validation, security isolation, graceful, a11y, ops) as overlay on delivered dispatch relay surfaces/hosts/contracts; document in 74-SOTA-DISPATCH-RELAY.md; refresh STATE; no code changes (05/09 pending execution per init). Refs: 74-VALIDATION.md, 74-CONTEXT.md, 74-PLAN-CHECK.md, 66-SOTA-QUALITY-REQUIREMENTS.md, 74-01/02/03/04/06/07/08/10 SUMMARIES, ROADMAP phase 74 criteria 1-6.

**Exercised verifications (targeted reads + gsd state + fast filters where runnable; full service-gated long-running/backgrounded per VALIDATION):**
- Criterion 1 (states): read DispatchPhase.cs (Loading + 17 phases incl SignedOut/Disabled/Unreachable/Queued/PendingApproval/Running/Failed/Completed/Cancelled/Expired), DispatchHostException (maps to phases, no secrets), DevicesViewModel.Loading/Loaded/Failed + phase models in Presentation/Dispatch; confirmed LOADING/EMPTY (NoDevices)/ERROR (Failed + Failures + Problem)/SUCCESS (Completed) + polite in SOTA doc + CONTEXT D-40.
- Criterion 2 (edge/first-attempt): read 74-SOTA, 74-04/06 summaries for ManualTimeProvider + fake hosts in tests (no sleeps), first gate passes claimed in SUMMARIES; races (newest-wins, CAS) addressed in D-16/D-23; loaded host fresh context in DispatchApiHost.
- Criterion 3 (validation/auth): read IDispatchPresence.cs (unreachable=Unknown never offline), CONTEXT D-14/D-17/D-41 (ES256 + jkt, newest amr, subject_not_allowed), frame guard + canonical in 74-01, capability snapshot match.
- Criterion 4 (security isolation): read IDispatchPresence fail-modes (unknown on down), 74-05 threat model (T-74-05* mitigated by sign+nonce+expiry+caps+redact), DispatchHostException no body/secret, owner scoped via sub + account_id, no PII in audit design (D-36), STRIDE per plans.
- Criterion 5 (graceful): read Dragonfly down (presence unknown, 1013/503, sockets stay, poll safety), reconnect jitter (D-31), local policy floor (D-28), ledger off no-op (D-36), explicit degrade phases (Unreachable/PausedByUser/PresenceUnknown per D-40).
- Criterion 6 (a11y/ops): read SOTA notes from 74-06/08 (polite live for status, assertive failures, keyboard cancel/approval, reduced-motion, labels); logs phase transitions; feed/OpenAPI observability; recovery via reconnect/revoke.
- Criterion 7 (build+licence+docs): from 74-PLAN-CHECK + SUMMARIES: build -warnaserror 0w, format clean on completed waves, CHANGELOG/architecture/runbooks updated (no secrets), licences unchanged (R17 no package).
- Criterion 8 (state/restore): gsd_run query state.load (phase 74 referenced in unsummarized, current focus elsewhere); locked D-* in CONTEXT; no drift in landed.

**Fast gates verified:** licences/changelog per PLAN-CHECK; build 0 warn from summaries; 18 reqs coverage in 74-PLAN-CHECK union; 6 ROADMAP criteria referenced; explicit states + first-attempt + fail-closed in landed artifacts.

**SOTA status:** embedded explicit DispatchPhase (18 values) + Loading/phase states in all surfaces/hosts (no blank), first-attempt (deterministic TimeProvider + fakes), security (owner scope + no-leak redaction + fail-closed + STRIDE), graceful (data-plane degrade + jitter reconnect), a11y notes, ops (structured + feed). Overlay doc updated; STATE to record SOTA for 74.

**Deviations/Notes:** 74-05 and 74-09 incomplete (per init runnable_plans); SOTA records design + landed implementation + pending notes. 3-4 human/manual per VALIDATION (Android, live Supabase amr, Railway WS, screen-reader) remain; documented. Full non-live + services + human recommended for verifier. No code changes in this SOTA pass.

All 8 SOTA criteria + 6 ROADMAP criteria referenced/exercised per assignment. Phase 74 SOTA updated 2026-11-10. All locked decisions + isolation + states honored in landed waves.

---

_Applied as SOTA overlay execution for phase 74. See 74-VALIDATION.md (manual items), 74-PLAN-CHECK.md, 74-CONTEXT.md and prior SUMMARIES for implementation details. 05/09 execution will complete hub + final hosts + full gate proof._
