# Phase 80: RPC engine transport production wiring - Context
Source: task T9013-arch, ADR-0038 (Proposed), owner directive, Phase 79 protocol contract
**Gathered:** 2026-11-23
**Status:** Planned (Waves 2+: plan-and-return). Executes after Phase 79 freezes the protocol contract.

<domain>
## Phase Boundary

Make `Harness.Rpc` a real production transport: `RpcHarnessSession` carries prompt, steer, follow_up, abort and get_state through the `IOmpSession` family at parity with `AcpAgentSession`, sessions become selectable through a `Harness:Adapter` option (`Acp` default in this phase, `Rpc` opt-in), and the three composition roots (EngineHost via its injectable `Func<IOmpSession>` factory, Native, Api `DirectSessionRunner` path) compose the selected factory. ACP remains fully wired; nothing switches the default here — that is Phase 84.

Not delivered: RPC as default, any remote-path change (Phase 83), any terminal change (Phase 81), approval-granting on RPC beyond deny-by-default until OQ-525 closes.
</domain>

<decisions>
## Implementation Decisions

- **D-01:** Parity is proven by shared scripted-fixture tests: the same scenario set (prompt streams to completion, steer lands in the running turn, follow_up queues, abort settles, get_state answers, resume by session identity) runs against both session factories. A capability RPC cannot carry is named in the verification with the observed protocol gap — never faked with a local approximation presented as parity.
- **D-02:** Selection seam: one `Harness:Adapter` option read once at composition per host; `InProcessEngineHost`'s existing factory delegate is the desktop seam, the Api composes the same choice through its harness session factory registration. Unknown values fail fast at startup (existing config discipline); the option is documented in `.env.example` terms/config reference per house rules.
- **D-03:** Approvals: RPC sessions resolve permission asks through the same deny-by-default policy surface as ACP (`AcpApprovalPolicy` equivalent); if OQ-525 finds no RPC permission request, RPC sessions run with approvals denied and that limit is user-visible in the session state, not silent.
- **D-04:** Event mapping: `RpcEventMapper` output feeds the same `UiBlock` contract and SSE sequence; no head learns which transport produced a block.
</decisions>

<verification_posture>
## What "done" means here

Parity fixtures pass on both transports; each composition root starts an RPC-backed scripted session in tests; locked restore, warning-free build and `./bootstrap.sh` pass. A real omp process is exercised only through the existing gated fixtures (no model turn).
</verification_posture>

<canonical_refs>
## Canonical References

- `docs/decisions/ADR-0038-omp-rpc-primary-transport.md`
- `src/Autonogrammer.Harness.Abstractions/IOmpSession.cs`, `src/Autonogrammer.EngineHost/InProcessEngineHost.cs`
- `docs/open-questions.md` (OQ-04, OQ-523, OQ-525)
- `.planning/REQUIREMENTS.md` (RPC-80-1 to RPC-80-4)
</canonical_refs>
