# Phase 79: omp 19.1.1 re-verification and protocol contract - Context
Source: task T9013-arch (correlation 901308c5-825d-45fe-8832-36b59c0771ae), owner directive "Omp RPC engine rearchitecture", upstream spec `docs/requirements/spec-T9013-req.md` (US-301, US-302), ADR-0038 (Proposed), blueprint `docs/architecture/v0170-transport-retirement-blueprint-T9013-arch.md`
**Gathered:** 2026-11-23
**Status:** Planned. Wave 1 execute slice; verification and fixture freeze only, no production wiring change.

<domain>
## Phase Boundary

omp 19.1.1 is installed; both clients (`Harness.Rpc`, `Harness.Acp`) were verified against 18.12.3/18.12.6. This phase freezes the protocol contract both transports will be held to: the RPC handshake (ready frame, `negotiate_protocol`), chunking caps, the command surface (`prompt`, `steer`, `follow_up`, `abort`, `get_state` and friends), the ACP handshake for comparison, and the version pin/probe behaviour. Every fact is recorded **observed-19.1.1** or **verified-18.12.6** from recorded output or scripted fixtures; scripted (non-live) fixtures are frozen into the test projects from that evidence. No live model turn (OD-07 covered Phase 11 only), no pin move without the recorded evidence, no composition change.

Not delivered: any production wiring (Phase 80), any compatibility fix beyond recording the delta as an OQ-520..525 row, any deletion.
</domain>

<decisions>
## Implementation Decisions

- **D-01:** The evidence ledger is `docs/architecture/omp-19-reverification.md`: one row per protocol fact (RPC ready frame, negotiate v1/v2, `rpc_chunk` and frame caps, each command, `--no-ui` and launch flags, ACP `initialize` counterparts, `OmpVersionProbe` accept/refuse behaviour), each labeled observed-19.1.1 or verified-18.12.6 with the command or fixture behind it. Unlabeled or unverifiable facts become OQ rows, never ledger claims.
- **D-02:** Fixture freeze: the scripted transports in `tests/Autonogrammer.Harness.Rpc.Tests` and `tests/Autonogrammer.Harness.Acp.Tests` are updated only from ledger rows, so fixtures mirror observed reality; a fixture change cites its ledger row. Fixtures stay scripted — no live process, no model turn.
- **D-03:** Drift report: each 18.12.6-verified assumption that 19.1.1 contradicts is listed with both outputs and a disposition (client fix in Phase 80, pin change with evidence, or OQ). The version pin's accepted range is a decision recorded here, from evidence, not edited silently in code.
- **D-04:** Carried forward unchanged: deferred-human 66.1-66.4 and 69, paused 68 (68-09 SEC-77-07), 74.1 review, 75-12 hold, blocker SEC-77-01.
</decisions>

<verification_posture>
## What "done" means here

The ledger exists with 0 unlabeled facts; both targeted test projects pass against the frozen fixtures (`--filter-not-trait "Category=Live"`); every delta has a disposition. A found incompatibility fully recorded is a successful outcome; passing over it is the failure.
</verification_posture>

<canonical_refs>
## Canonical References

- `docs/decisions/ADR-0038-omp-rpc-primary-transport.md`
- `docs/architecture/v0170-transport-retirement-blueprint-T9013-arch.md`
- `docs/open-questions.md` (OQ-520 to OQ-525)
- `docs/runbooks/evidence/omp-rpc-live-variants-2026-10-21.md` (Phase 11 archival record)
- `.planning/REQUIREMENTS.md` (RPC-79-1 to RPC-79-4)
</canonical_refs>
