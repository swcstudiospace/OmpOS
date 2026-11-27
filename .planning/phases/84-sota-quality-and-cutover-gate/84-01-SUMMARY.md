---
phase: 84-sota-quality-and-cutover-gate
plan: "01"
status: complete
date: 2026-11-26
flip_commit: 69b844f0
decisions_commit: c3ac51ae
---

# Phase 84: SOTA quality and cutover gate — summary (wave13-1, Tasks 4–7)

The default local adapter is now **Rpc**, flipped in one commit with ADR-0038 Accepted and `Harness:Adapter=Acp` as the supported rollback. No deletions (Task 3 manifest: 0). Owner decisions of 2026-11-26 recorded (OQ-528 init, ADR-0039 A1/B4). Evidence below; Tasks 1–3 evidence lives in `docs/runbooks/evidence/phase-84-preflip-2026-11-26/`.

## Flip

- Commit `69b844f0`: F1–F5 (appsettings `Harness:Adapter=Rpc`; `EngineCompositionOptions`/`SessionEngineSetOptions` `Transport=Rpc`; `.env.example` `Harness__Adapter=Rpc`; ADR-0038 Accepted + index row). Validators unchanged.
- Precondition green: `repo-checks.sh` exit 0, check-docs/sbom/skip-markers 0 post-hygiene (`d8fea8f0`, `9737cf01`) and `184c5500`.
- Compile-check: Api 30 projects 0/0, Native 44 projects 0/0 (`-warnaserror`), exit 0 both.

## Owner-call record (verbatim scope, Task 4)

- **Realm (OQ-528)**: owner `init` — `plugins/agent-realm` already initialised at `f28a3acc319d9805c2d2487c05d12a075a2c8ad7 (v0.3.38)`, clean; recorded closed 2026-11-26, no re-init.
- **ADR-0039 Gate A = A1** (Dispatch ES256 device credential, artifact audience + server-validated session id); **Gate B = B4** (no-network default v0.16.0). Status stays **Proposed** (71-08 Tasks 2/3 code not yet written).
- **SEC-77-01**: open at 0 waivers (unchanged).

## Gate evidence (post-flip, Task 6)

- DecisionRecordTests: 3/3. ContractTests: 32/32. ArchitectureTests full + Api adapter/probe/parity suites: see `docs/runbooks/evidence/phase-84-cutover-2026-11-26/TASK6-postflip.md` (filled from the runs at `69b844f0`).
- Rollback: `HarnessAdapterSelectionTests` (Production + Acp starts; Production + InMemory fails naming key). Secret scan (gitleaks) not run here (needs network download; main agent's full-gate step).

## Deletion manifest / suppression ledger

- Manifest: 0 deletes (TASK3). Ledger: oracle green; 0 new suppressions (TASK2 + hygiene `d8fea8f0`).

## Docs + trackers + close-out (Task 7)

- Docs at new truth: `docs/architecture.md` (engine starts `omp --mode rpc` per session; ACP remote + rollback), `.planning/PROJECT.md` (update line + key-decision row: Accepted 2026-11-26), `.planning/ROADMAP.md` (Phase 84 Complete), `.planning/STATE.md` (current focus), `.planning/LINEAR-MAP.md` (Phase 84 status). `docs/runbooks/hosting.md` recipe intentionally unchanged (explicit Acp for approval-configured flows, OQ-525 — not a default statement).
- Tracker payloads (file-first, 1 per phase, 0 fabricated identifiers, no tracker write attempted): 79, 80, 81, 82, 83, 84 `-TRACKER-PAYLOAD.md` under each phase directory (Linear SPE-37877/37882/37886/37890/37893/37896, P-SPE-2; Notion `700bc1a0-c7ae-812c-abfb-00035fd38be2`).
- Carried identifiers, states unchanged: 66.1, 66.2, 66.3, 66.4, 69 (deferred-human), 68 / 68-09 (SEC-77-07, paused), 74.1 review (open), 75-12 hold, SEC-77-01 (open, 0 waivers). v0.16.0 not archived (ROADMAP stays cumulative).

Refs: SPE-37896 (SC1 SPE-37897, SC2 SPE-37898, SC3 SPE-37899), P-SPE-2.
