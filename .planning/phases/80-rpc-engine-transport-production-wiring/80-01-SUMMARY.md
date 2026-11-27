---
phase: 80-rpc-engine-transport-production-wiring
plan: "01"
subsystem: harness-rpc, enginehost, api, native
tags: [omp-rpc, transport-parity, harness-adapter, deny-by-default]
dependency_graph:
  requires: [79-01 protocol contract]
  provides: [rpc-production-transport-opt-in, transport-parity-suite, rpc-composition-arch-guard]
  affects: [82-plugin-code-connectivity, 83-acp-rpc-boundary, 84-cutover]
metrics:
  tasks: 13
  status: complete
---

# Phase 80 Plan 01: RPC engine transport production wiring — Summary

**One-liner:** `Harness.Rpc` is now a selectable production transport behind the
`IOmpSession` family at all three local composition roots (EngineHost, Api,
Native) via `Harness:Adapter` (`Acp` still default); parity is proven by one
shared scripted suite over both transports, approvals stay deny-by-default on
RPC (OQ-525), and no live model turn was run.

## What landed (commits 99db19ab + 90b69a52)

- **Core (Tasks 1–7).** Prompt-path replay fixtures over `FakeRpcTransport`
  from frozen 19.1.1 captures; version pin moved 18.12.x → 19.1.x citing the
  Phase 79 ledger (OQ-524); process-start environment control (complete-env
  dictionary semantics) + stderr-tail start diagnostics; `IOmpLiveSteering`,
  `IOmpSessionIdentity` (sessionFile only, never synthesized),
  `IOmpSessionCommands` (frozen 53-command frame), read-only
  `IOmpModelControl`; options/fork refused as named gaps (OQ-523, no invented
  commands); deny-by-default approval posture (OQ-525).
- **Composition (Tasks 8–11).** `InProcessEngineHost.ForRpc` (lazy start);
  Api `OmpRpcHarnessSessionFactory` + exhaustive Acp/Rpc/InMemory dispatch
  (silent InMemory fall-through deleted), `ValidateOnStart` incl. Rpc +
  approval-kinds startup refusal, readiness Rpc branch; Native
  `EngineComposition` transport branch for local main sessions with loud
  Phase 82/83/OQ-525 refusals. ACP behavior byte-identical where extracted
  (`WorktreeRunSession` shared verbatim).
- **Guards (Tasks 12–13).** `TransportParityScenarios` shared suite over both
  factories; `RpcCompositionArchitectureTests` (Rpc product consumers exactly
  Api/EngineHost/Native; Remote + AgentWorker ACP-only); `.env.example`
  `Harness__Rpc__*` documented; OQ-523/OQ-525 close-out notes (both stay open
  pending observed live-capture evidence).
- **Suppression ledger.** 3 CA2000 fixed at cause (real session leak on start
  failure in test helpers); SUP-878..886 kept rows with source justifications.

## Parent-verified evidence (SDK 10.0.402, this run)

| Suite | Result |
|---|---|
| Harness.Rpc.Tests | 96 passed, 1 live-skip |
| Harness.Acp.Tests | 277 passed |
| EngineHost.Tests (non-Live) | 49 passed |
| Native.Tests (non-Live) | 2362 passed |
| Harness.ContractTests (parity) | 27 passed |
| Api.IntegrationTests (Rpc factory/adapter filter, test Postgres) | 16 passed |
| ArchitectureTests (incl. SUP ledger + Rpc guard) | 114 passed |

Full gate (`./bootstrap.sh`, RPC-80-4) is recorded in `80-VERIFICATION.md`.

## Deviations

- Task 1: plan listed `tool_execution_*` replay frames, but the frozen live
  capture contains none; per the plan's own no-source-no-fixture rule none
  were scripted (tool mapping stays covered by pre-existing factory tests).
- Task 11: `EngineTransport` enum in its own file (MA0048); environment
  resolved through `EngineEnvironmentPolicy` overlay instead of passing
  `plan.Environment` as the complete child environment (would have spawned
  omp without PATH/HOME under Task 3 semantics).
- Lock files: regenerated for the new `Harness.Rpc` references; stale
  project-version strings refreshed 0.17.0 → 0.20.18 in touched closures.
