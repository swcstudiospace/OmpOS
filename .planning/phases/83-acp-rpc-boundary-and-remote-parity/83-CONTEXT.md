# Phase 83: ACP/RPC boundary and remote parity - Context
Source: task T9013-arch, ADR-0038 D2/D5, ADR-0015 (remote over SSH), ADR-0020 (cloud bridge)
**Gathered:** 2026-11-23
**Status:** Planned (Waves 2+: plan-and-return). Depends on Phases 80 and 82.

<domain>
## Phase Boundary

Fix the transport boundary in code and tests: ACP remains the remote transport (`Harness.Remote` over ssh `omp acp`, podman and WSL; WebSocket-bridged ACP per its recorded stub/RFD status), RPC stays local-process only, and both consume one shared launch plan so sessions cannot drift by where omp runs. `DirectSessionRunner` (Api) becomes transport-agnostic — it consumes `IHarnessSessionFactory`/`IOmpSession`, never an ACP concrete type. SSE sequence/resume semantics (`id:` = sequence, `Last-Event-ID`) are proven unchanged across transports.

Not delivered: RPC over any network transport (no verified remote RPC endpoint exists; inventing one is prohibited), any change to remote auth, host-key or credential handling.
</domain>

<decisions>
## Implementation Decisions

- **D-01:** Boundary assertions live in `tests/Autonogrammer.ArchitectureTests`: `Harness.Remote` references the ACP client and not `Harness.Rpc`; `DirectSessionRunner`'s file names no `Acp*` concrete type; LayerMap rows for both harness projects stay harness-layer with no upward edge.
- **D-02:** Shared launch plan: the launch composition (contributors, overlays, MCP set, approval policy defaults) is built once and handed to either transport factory; Phase 82's plan-diff tests move here as permanent architecture coverage. Permanent suite: `tests/Autonogrammer.Native.Tests/Composition/LaunchPlanParityTests.cs` (canonical — argv/overlay/environment byte parity modulo the enumerated transport delta, plus the once-vs-twice test proving one `OmpLaunchPlanner.Plan` output is handed to both factories as the identical instance); `tests/Autonogrammer.Harness.ContractTests/TransportParityScenarios.cs` and `PluginCommandParityTests.cs` cross-link to it for their command/event halves. No live trait, no fixture self-skip.
- **D-03:** SSE parity: a scripted session producing an identical event sequence over each local transport yields byte-equal SSE frames and the same resume point in the Api integration fixtures; any difference is a phase-stopping defect, not a mapping note.
</decisions>

<verification_posture>
## What "done" means here

ArchitectureTests pass with the new boundary rules, the Api integration fixtures pass on both adapters, and the remote projects' existing tests pass unmodified in behaviour. No live remote host is claimed (loopback fixtures per ADR-0015 precedent).
</verification_posture>

<canonical_refs>
## Canonical References

- `docs/decisions/ADR-0038-omp-rpc-primary-transport.md`, ADR-0015, ADR-0020
- `tests/Autonogrammer.ArchitectureTests/LayerMap.cs`
- `.planning/REQUIREMENTS.md` (RPC-83-1 to RPC-83-4)
</canonical_refs>
