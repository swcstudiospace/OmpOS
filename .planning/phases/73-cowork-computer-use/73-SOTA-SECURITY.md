# Phase 73 SOTA for Computer Use Security

**Date:** 2026-11-10
**Refs:** ADR-0035 threat model (lines 173-184), 73-CONTEXT.md (D-05..D-17, D-31..D-33), 73-RESEARCH (pitfalls), 73-VERIFICATION (gaps), 73-01..73-07 plans (gate in 02, audit/frames in 03, kill in 02+06, wiring in 07), ComputerUseBoundaryTests, threat register in 73-07-PLAN.

## Objectives
- Every threat in the ADR-0035 table is mitigated in the production-wired path.
- Policy gate (grants, sensitivity, approval, kill, indicator, rate, mask, audit) is the single choke point; no bypass from model, Dispatch, or head composition.
- Local-only, owner-only audit, no secrets in computer sessions (Rule of Two via ADR-0032).
- SOTA: explicit, testable, first-attempt + race safe, no new attack surface from wiring.

## Threat Model Coverage (ADR-0035 table + 73-07 wiring)

| Threat | Mitigation (implemented in 01-06, reached via 07) | Evidence / Test |
|--------|----------------------------------------------------|-----------------|
| Prompt injection via screen | Untrusted envelope on every result; only granted windows visible (VisibleRegionMasker); tiers + denylist + forbidden chords + sensitivity confirmation; rate (60/min burst 10) + step (300) limits; kill switch; audit. Credential titles always ask. | MaskerTests, SensitivityClassifierTests, GateOrderTests, ComputerToolProviderTests, RateLimiterTests |
| Approval spoofing | Host-observed facts only (no model text in decision); security phrase model cannot read; own windows hard-denied + masked; no injection while prompt open (process lock); Deny default + timeout. | ApprovalSerialisationTests, OwnWindowTests, head composition indicator + dialog |
| Kill switch failure | Verified registration on dedicated thread/conn before any actuating action or arm; trip releases all + sets Revoked + cancels + audits; token checked between primitives; 3 stop paths (hotkey, indicator, Cowork stop). | KillSwitchCoordinatorTests, X11 hotkey second-grab case, indicator VM |
| App identity spoof / TOCTOU | Prefer /proc exe + WM_CLASS; re-resolve window after move before button-down; abort on change; denylist wins; grant ends on window identity change. | Gate step 4-5, re-resolve logic in executor, X11 live cases |
| Audit tamper / loss | Owner-only (0600/ACL) append-only JSONL with FileShare.Read; write-ahead before injection (deny on fail); SHA-256 prev chain (genesis); seq + full summary + hashes; no typed/clipboard bytes; 30d + rotation. | AuditLogChainTests, AuditRedactionTests, ComputerAuditLog |
| Remote hijack (Dispatch) | No `computer.*` relay op and no computer.task (R1 + ADR-0036); only as `toolClasses` in cowork.task; local-only approvals (MustBeLocal=true on broker); 30m Dispatch grants; per-device opt-in + AllowDispatchSessions=false default; indicator shows controller device; frames never leave except to user's provider. | D-31..D-33, ComputerToolProvider.ShouldRegisterFor, broker in tools composition |
| Secret leakage | No workspace secrets in any computer_* session (ADR-0032 Rule of Two enforced by profile); credential titles ask; no bytes in logs/audit; masking; opt-in discloses provider residency. | Overlay test in 03, no secret path in 07 wiring |

## Additional SOTA Security Properties (from CONTEXT + query)
- Single choke: IComputerActionGate + ComputerActionExecutor is the ONLY path to any backend primitive (enforced by ComputerUseBoundaryTests; architecture test fails the build on violation).
- Process-wide serial lock during any approval prompt.
- All denials stable DenialCode + message id; first failing step wins (D-06 order).
- Rate/step/failure limits enforced before every primitive.
- Audit also flows to ICoworkToolAudit (FileCoworkToolAudit sink) for the session log.
- No new packages, no unsafe, system DllImport only (exemplar PosixProcessSignal).
- Head composition never constructs backends for wrong platform; Null for web/api/cloud.

## Wiring (73-07) Security
- Production call sites in AgentHead/CoworkHead reach the gate and broker only through the same L1 ports (IComputerUseAccessFacts, ICoworkApprovalBroker with MustBeLocal).
- No bypass added; chrome entry and settings factory are side effects of the same Build that would feed tools.
- Boundary tests updated to cover the new call sites (still assert executor-only and L1-only).

## Residual / Open (documented, not waived)
- Full grant inside a `Full` tier app can still be misused (user granted it).
- Heuristic title-based credential detection (OQ-384); typing still asks by default.
- Indicator may be covered on some WMs/Spaces/full-screen (OQ-386) — gate correctly denies.
- Win/mac unmeasured (compile only); real runner required before claim.
- Live omp honouring computer.enabled=false unverified (OQ-387, OD-07).

All mitigations are present in the core (01-06) and are now reachable from a live desktop Cowork head (73-07). No new trust boundary or secret introduced by the wiring.
