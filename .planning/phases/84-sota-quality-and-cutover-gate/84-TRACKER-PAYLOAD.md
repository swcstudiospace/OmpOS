# Tracker payload — Phase 84 (SOTA quality and cutover gate)

Authored as a file first (Phase 84 Task 7, RPC-84-4). No tracker write was attempted by the authoring assignment; the sync below is the payload text to post verbatim. 0 identifiers fabricated.

- Linear: parent [SPE-37896](https://linear.app/swcstudio/issue/SPE-37896/phase-84-sota-quality-and-cutover-gate), SC1 SPE-37897 (one-commit flip), SC2 SPE-37898 (close-out names carried identifiers), SC3 SPE-37899 (payloads); project P-SPE-2.
- Notion: payload root `700bc1a0-c7ae-812c-abfb-00035fd38be2`.

Payload text:

> Phase 84 status: cutover flip landed 2026-11-26 — `Harness:Adapter` / `EngineTransport` defaults flipped Acp→Rpc at F1-F5 in one commit (69b844f0), ADR-0038 Accepted same commit, `Harness:Adapter=Acp` kept as the supported rollback (validators unchanged). Task 3 deletion manifest: 0 deletes. Owner decisions recorded 2026-11-26: OQ-528 closed (realm `init`, already initialised at `f28a3acc`); ADR-0039 Gate A=A1 + Gate B=B4 decided (status stays Proposed). SEC-77-01 carried open at 0 waivers; v0.16.0 not archived. Evidence: `docs/runbooks/evidence/phase-84-preflip-2026-11-26/` (Tasks 1-3), `docs/runbooks/evidence/phase-84-cutover-2026-11-26/` (Tasks 4-7), `84-VERIFICATION.md`.

Sync status: SYNCED 2026-11-27: Linear comment posted to parent issue (0 tracker errors).
