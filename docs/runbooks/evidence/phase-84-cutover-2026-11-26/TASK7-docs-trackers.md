# Phase 84 cutover — Task 7: docs sweep + tracker payloads + close-out (2026-11-26)

## Reference-docs sweep (RPC-84-3)

| File | Change (same sentence everywhere: Rpc = local default; Acp = remote + rollback) |
|---|---|
| `docs/architecture.md` | omp bullet: "Autonogrammer starts `omp --mode rpc` per chat session by default (Phase 84 cutover, ADR-0038 Accepted) ... `omp acp` remains the remote-boundary transport and the supported rollback (`Harness:Adapter=Acp`)." |
| `.planning/PROJECT.md` | update line + key-decision row → Accepted 2026-11-26, Rpc default, Acp rollback |
| `.planning/ROADMAP.md` | Phase 84 Plans + progress paragraph + table row → Complete (flip landed 2026-11-26) |
| `.planning/STATE.md` | new current-focus line (flip landed, decisions, SEC-77-01) |
| `.planning/LINEAR-MAP.md` | Phase 84 status paragraph (no Linear status changed) |
| `docs/runbooks/hosting.md` | intentionally UNCHANGED: explicit `Harness__Adapter=Acp` recipe for approval-configured flows (OQ-525 requires Acp there) — a recipe, not a default statement |

Grep for stale default claims after sweep: `docs/handoff.md` only matches double-transport history rows (AC_4 replay table), no "ACP is production default" sentence remains. DecisionRecordTests + ArchitectureTests green (TASK6-postflip.md); `bash scripts/ci/repo-checks.sh` exit 0 at close-out (this file's verify line).

## Tracker payloads (RPC-84-4, file-first)

| Phase | File | Linear identifiers |
|---|---|---|
| 79 | `.planning/phases/79-omp-1911-reverification-and-protocol-contract/79-TRACKER-PAYLOAD.md` | SPE-37877, P-SPE-2 |
| 80 | `.planning/phases/80-rpc-engine-transport-production-wiring/80-TRACKER-PAYLOAD.md` | SPE-37882, SPE-37883, SPE-37884, SPE-37885, P-SPE-2 |
| 81 | `.planning/phases/81-terminal-engine-retirement/81-TRACKER-PAYLOAD.md` | SPE-37886, SPE-37887, SPE-37888, SPE-37889, P-SPE-2 |
| 82 | `.planning/phases/82-plugin-code-connectivity/82-TRACKER-PAYLOAD.md` | SPE-37890, SPE-37891, SPE-37892, P-SPE-2 |
| 83 | `.planning/phases/83-acp-rpc-boundary-and-remote-parity/83-TRACKER-PAYLOAD.md` | SPE-37893, SPE-37894, SPE-37895, P-SPE-2 |
| 84 | `.planning/phases/84-sota-quality-and-cutover-gate/84-TRACKER-PAYLOAD.md` | SPE-37896, SPE-37897, SPE-37898, SPE-37899, P-SPE-2 |

Notion root (all): `700bc1a0-c7ae-812c-abfb-00035fd38be2`. Sync attempts: none (authored-only, per the "files first" rule); tracker errors: none to store; identifiers fabricated: 0.

## Close-out

Flip landed (`69b844f0`), not refused. Carried identifiers, states unchanged: 66.1, 66.2, 66.3, 66.4, 69 (deferred-human); 68 / 68-09 (SEC-77-07, paused); 74.1 review (open); 75-12 hold; SEC-77-01 (open, 0 waivers). v0.16.0 not archived. Full record: `.planning/phases/84-sota-quality-and-cutover-gate/84-VERIFICATION.md`.
Refs: SPE-37896, SPE-37897, SPE-37898, SPE-37899.
