---
phase: 80-rpc-engine-transport-production-wiring
verified: 2026-11-24T00:00:00Z
status: passed
score: "3/3 ROADMAP criteria proven; bootstrap 15532/15536 with 1 pre-existing failure proven at base (see Caveat)"
covered_files:
  - ".planning/phases/80-rpc-engine-transport-production-wiring/80-CONTEXT.md"
  - ".planning/phases/80-rpc-engine-transport-production-wiring/80-01-PLAN.md"
  - ".planning/phases/80-rpc-engine-transport-production-wiring/80-01-SUMMARY.md"
behavior_unverified: 0
overrides_applied: 0
re_verification: false
commits: ["99db19ab", "90b69a52", "59a96fb6", "6975c58a"]
human_verification: []
---

# Phase 80 Verification: RPC engine transport production wiring

Verified by the parent orchestrator (independent re-runs, SDK 10.0.402).
No live model turn was run or is claimed (OD-07); prompt-path proof is
scripted replay over frozen captures.

## Evidence (parent-run)

| Check | Result |
|---|---|
| Harness.Rpc.Tests | 96 passed, 0 failed, 1 live-skip |
| Harness.Acp.Tests | 277 passed |
| Harness.ContractTests (shared parity suite) | 27 passed |
| EngineHost.Tests (non-Live) | 49 passed |
| Native.Tests (non-Live) | 2362 passed |
| Api.IntegrationTests (Rpc factory/adapter filter, test Postgres) | 16 passed |
| ArchitectureTests (incl. RpcComposition guard + SUP ledger) | 114 passed |
| `./bootstrap.sh` at 6975c58a | exit 2 — full Linux suite 15532 passed / 1 failed / 3 skipped (see Caveat); all earlier gate stages green (repo-checks, licences, locked restore 123/123, warning-free build, format) |

## Criteria

1. **Shared parity suite (RPC-80-1).** `TransportParityScenarios` runs the
   same scenarios over both factories (27/27). Uncarryable capabilities are
   named, not faked: options set, fork, model-set and any RPC permission
   response have no observed RPC command (OQ-523); the suite asserts the
   named refusals.
2. **Adapter selection (RPC-80-2).** `Harness:Adapter` ∈ {Acp (default),
   Rpc, InMemory} read once per composition at EngineHost (`ForRpc`), Api
   (exhaustive dispatch; silent InMemory fall-through deleted; unknown value
   fails `ValidateOnStart`) and Native (`EngineComposition` transport
   branch, local main sessions only). Scripted RPC-backed sessions start at
   each root in tests (EngineHost 49, Api 16, Native 2362 suites above).
3. **Approvals + event contract (RPC-80-3, RPC-80-4).** RPC sessions are
   deny-by-default; Api refuses startup for Rpc + configured approval kinds
   naming OQ-525; Native refuses approval-configured flows on Rpc; the limit
   is visible in advertised capabilities. `RpcEventMapper` feeds the same
   `UiBlock`/SSE contract (parity suite asserts identical event sequences).

## Caveat (bootstrap not exit 0 — pre-existing, proven)

`./bootstrap.sh` exits 2 solely on
`McpConnectionCheckerTests.StdioEnvironment_FollowsModeBundleAndSpecEnv`
(`ConnectorCheckTimedOut`). This failure is **pre-existing, not a Phase 80
regression**, proven 2026-11-24: the test fails identically (3/3 here, 1/1 in a
clean detached worktree) at base commit `983c4594` — before every Phase 80
commit — and Phase 80 commits touched no `Harness.Mcp` source (lock-file
version strings only). The test spawns a real stdio MCP child and times out
on this host; root cause is host/fixture-level and unassigned. It is recorded
here instead of waived silently; closing it is tracked as a P4.2/P5.1 item.
Every other bootstrap stage and 15532 of 15536 tests pass at HEAD, so the
phase is recorded passed with this caveat, following the v0.15.0
complete-with-caveats precedent (phases 52/58/64/65).

## Gaps carried forward (by design)

OQ-522 chunk-payload cap, OQ-523 prompt-path live shapes, OQ-525 RPC
approval surface (deny-by-default stands), OQ-526 plugin loading parity
(Phase 82). Default adapter flip is Phase 84; remote boundary is Phase 83.
