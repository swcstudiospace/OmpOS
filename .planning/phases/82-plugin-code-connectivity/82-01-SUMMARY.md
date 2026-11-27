# Phase 82 Summary — Plugin code-connectivity (82-01)

Linear: SPE-37890. OQ-526 closure phase.

## What shipped

- **T2 pins**: ACP argv pin tests (plugin `-e`, `--config`, `--skills`, `--add-dir`, `--approval-mode`) and Api plan-less composition reflection pin (`tests/Autonogrammer.Api.IntegrationTests/ApiPlanLessCompositionTests.cs`).
- **T3**: `OmpRpcClientOptions.ExtraArguments` + `OmpRpcProcess` Start overload (transport-words-only + verbatim ArgumentList; null entry refused fail-fast) with 5 tests (`tests/Autonogrammer.Harness.Rpc.Tests/RpcExtraArgumentsTests.cs`).
- **T4 SC1 byte parity**: `tests/Autonogrammer.Native.Tests/Composition/LaunchPlanParityTests.cs` — overlay bytes hash-equal and unchanged after composition on both adapters; resolved environment equal after `EngineEnvironmentPolicy` replay; zero MCP servers (independent, test names it). Production composition (`apps/Autonogrammer.Native/Engine/EngineComposition.cs` `ComposeRpc`) wires `plan.ExtraArguments` into `OmpRpcClientOptions.ExtraArguments`.
- **T5 per-plugin load proof**: `tests/Autonogrammer.Harness.ContractTests/PluginCommandParityTests.cs` — Theory over ultrathink (8 commands) / swarm / substrate / gsd: the same ExpectedCommands frame from `OmpPluginCatalog` drives scripted `AcpAgentSession` and scripted `OmpRpcSession`; identical `IOmpSessionCommands.Commands` asserted. Frames cite the frozen 19.1.1 capture (R-UNSOLICITED).
- **T6 MCP no-channel difference**: recorded in OQ-526 (Phase 82 per-point disposition 2026-11-26, in `docs/open-questions.md`): no MCP carrier observed under `--mode rpc`; `ComposeRpc` keeps the MCP/wrapper/gate refusals.
- **T7 bundle/hook/isolation dispositions** (see §3 of `80-01-SUMMARY.md` for the T7 disposition record; BundleLaunchContributor `OverlayEntries` drop fix per T2 findings).
- **T9 clause-by-clause refusal removal** for proven channels only: `plan.ExtraArguments` and `ConfigOverlayPath` via argv byte-parity. `McpServers` / `HookGate` / `ProcessWrapper` refusals kept.
- **T8 realm owner-gated**: executed only after the owner record (OQ-528: initialised, f28a3acc, clean; 2026-11-26).

## Commits

`1efc341e` (T2/T3), `14127ca9` (T4–T9 incl. lockfiles for ContractTests + InMemory.Tests).

## Evidence (parent-verified)

- `tests/Autonogrammer.Harness.ContractTests`: 32/32 (incl. per-plugin parity).
- `tests/Autonogrammer.Harness.Rpc.Tests`: 101 passed / 103 (2 live-skips).
- `tests/Autonogrammer.Harness.AgentLoop.Tests` (non-Live): 1330/1330.
- Full Linux build after T4–T9 + SUP-888/889: 0 errors (lockfiles regenerated for ContractTests + InMemory.Tests).

## Open items

- MCP servers P7–P9, isolation wrapper P6, hook gate P2: no RPC channel observed (OQ-526 stays open; Phase 83 Task 10 records the exact remainder set in `83-VERIFICATION.md` and `OQ-526-83`).
- Ultrathink plugin command-surface proof relies on scripted parity over the frozen 19.1.1 capture, not a live plugin-load turn (live capture is opt-in only).
