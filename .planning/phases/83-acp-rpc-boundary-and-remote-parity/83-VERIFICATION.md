# Phase 83 verification inputs (Tasks 6–10 lane, 2026-11-26)

Written by the Tasks 6–10 lane (`wave11-2`). Tasks 1–5 are the parallel lane; where this file cites a
Tasks 1–5 artefact, the verifier should read it from that lane's output, not re-derive it here.

## Per-SC evidence

| SC | Evidence | Owner lane |
| --- | --- | --- |
| SC1 boundary assertions | `tests/Autonogrammer.ArchitectureTests/AcprpcBoundaryTests.cs` (Task 2: Remote→ACP-only, no RPC-over-network, LayerMap, consumer-set freeze) | Tasks 1–5 |
| SC1 runner proof | `DirectSessionRunner` source scan (`AcprpcBoundaryTests`, Task 3) + Api both-adapter fixtures (Task 3) | Tasks 1–5 |
| RPC-83-3 shared plan | `LaunchPlanParityTests`, `PluginCommandParityTests`, `TransportParityScenarios` promoted as permanent coverage (Task 4) | Tasks 1–5 |
| SC2 SSE/resume identity | Serializer parity given identical `HarnessEvent`s: SSE byte-equality + `Last-Event-ID` resume fixtures in `tests/Autonogrammer.Api.IntegrationTests` (Task 5, RPC-83-4) prove the serializers are identical once the `HarnessEvent` sequence is identical — NOT end-to-end byte parity, because production mappers embed different wire shapes verbatim in `PayloadJson` (`RpcEventMapper` `frame.GetRawText()` vs `AcpWireMapping` `update.GetRawText()`, so production SSE `PayloadJson` diverges by construction). PayloadJson mapper-shape divergence is named as OQ-529 (open), in the style of OQ-526; `AcpTaskToolActivitySource` parses `title`/`kind`/`rawInput` out of `PayloadJson`, so tool-activity parsing works only for ACP-shaped `PayloadJson`. The scripted RPC seam hand-aligns `PayloadJson` to the ACP shape, which is why the byte comparison passes. | Tasks 1–5 |
| Remote parity (Task 6) | `tests/Autonogrammer.Harness.Remote.Tests/RemoteTransportSelectionTests.cs` (ssh/podman/WSL each compose their ACP transport type; assembly references Acp, never Rpc) + `tests/Autonogrammer.Native.Tests/Composition/RemoteTransportSelectionTests.cs::AnRpcSelectionWithRemoteResourcesIsRefusedNamingRemote` (RPC+remote refusal names the remote combination) | Tasks 6–10 |
| MCP/gate/wrapper freeze (Tasks 7–8) | `tests/Autonogrammer.Native.Tests/Composition/RpcRefusalFreezeTests.cs` — five tests, one named test per refusal clause + OQ-525 present/deny-by-default | Tasks 6–10 |
| Slice-single/design-multi (Task 9) | `TransportParityScenarios.MultiSession_TwoRpcSessionsFromOneFactory_AreIsolated` (re-run at the gate; verified present, not re-run by this lane) | Tasks 1–5 test, Tasks 6–10 cleanup |


Probe run at execution: `omp --help` and `omp --mode rpc --help` on the installed omp 19.2.1, plus the
`Harness.Rpc` command table and `OmpRpcProcess.Start`. No Task 1 ledger appendix was available to this lane;
the exact appendix citation is the parallel lane's to add.
## Task 1 re-probe verdict (this lane's read, 2026-11-26)
| Point | Verdict | Evidence |
| --- | --- | --- |
| P7–P9 MCP servers | unobserved — refusal stays | `--mode rpc --help` lists no MCP/server spelling; RPC command table (`prompt`, `steer`, `follow_up`, `abort`, `get_state`, `switch_session`, …) has no MCP-shaped command; overlay `mcpServers` key inert; `--hook=<file>` is a CLI extension file, not a session MCP channel |
| P6 process wrapper | unobserved — refusal stays | `OmpRpcProcess.Start(ompPath, cwd, environment, extraArguments)` takes the omp path directly as `FileName`; no wrapper seam |
| P2 hook gate | unobserved — refusal stays | `HookGate` is `IAcpHookGate` (a per-session gate object), and the RPC prompt path carries no gate surface in the command table |

## Refusal-clause inventory (`ComposeRpc`, after this lane)

| Clause | Before | After |
| --- | --- | --- |
| Remote | "remote sessions stay on ACP until Phase 83" | "remote sessions stay on ACP: RPC has no verified remote endpoint (Phase 83, ADR-0038 D2)" — behaviour unchanged |
| Plan MCP servers | "(OQ-526)" only | names the Phase 83 re-probe 2026-11-26 (help + command table + overlay inert) and OQ-526 |
| User MCP servers | "(OQ-526)" only | names the Phase 83 re-probe 2026-11-26 and OQ-526 |
| Hook gate | "(OQ-526)" only | names the Phase 83 re-probe 2026-11-26 (no prompt-path gate surface) and OQ-526 |
| Process wrapper | "(OQ-526)" only | names the Phase 83 re-probe 2026-11-26 (`Start` takes the omp path directly) and OQ-526 |
| OverlayEntries without path | "(OQ-526)" | **unchanged** (out of Tasks 7–8 scope; not re-pointed) |
| OQ-525 approval-capable flow (both clauses) | — | **byte-identical** (untouched; pinned by `RpcRefusalFreezeTests.ComposeRpc_ApprovalCapableFlow_RefusesNamingOq525`) |

No refusal clause was removed; no carrier was invented; no bearer value is in argv, fixtures, or logs.

## OQ-526 remainder disposition (exact remainder set)

Still open: MCP servers P7–P9 (plan + user seams), isolation wrapper P6, hook gate P2, overlay-entries-without-path.
Each names its surviving `ComposeRpc` clause above and its pinning test in `RpcRefusalFreezeTests`.
Closed for proven points: none in this remainder — the OQ-526 row must stay open.
Already-proven-equal points (plugin `-e` argv, `--config` overlay bytes, resolved environment) are the Phase 82
per-point disposition, unchanged by this phase.

## ADR-0038 pointer

`docs/decisions/ADR-0038-omp-rpc-primary-transport.md` still reads **Proposed** (verified this pass);
Phase 84 accepts it at the cutover gate. This phase does not change its status. `Harness:Adapter` and
`EngineTransport` defaults stay `Acp` (no default flip).

## Slice-single cleanup record (Task 9)

- `src/Autonogrammer.EngineHost/InProcessEngineHost.cs`: header now states one-at-a-time binding is this host's
  admission; multi-session is one host per session (SessionEngineSet slot). Comments only.
- `services/Autonogrammer.Api/Harness/OmpRpcHarnessSessionFactory.cs`: summary now states fresh client/process
  per run and that admission is the `DirectSessionRunner` run lease. Comments only.
- `apps/Autonogrammer.Native/Agents/SessionEngineSet.cs`: "one `omp acp` child each" → "one omp child each,
  over its composed transport"; slots named as this head's admission. Comments only.
- Grep: no `static` shared mutable session/client state in any touched file (only static methods returning
  fresh collections and `LoggerMessage` sources); the only `one session` hits are per-engine descriptions in
  `EngineComposition.cs` ("for one session", "names of one session"), not single-session-workaround folklore.
- Retained, not removed: host single-binding, Api run lease, `SessionEngineSet` slots — documented, not deleted.

## Gates this lane could not close

1. The Task 1 verdict appendix landed mid-lane (`docs/architecture/omp-19-reverification.md:284+`) and its
   all-unobserved table matches this lane's freeze exactly. `docs/open-questions.md` (OQ-526 text) remains
   Tasks 1–5 lane-owned and this lane was instructed not to edit it: the disposition above is the text that
   row should carry; the refusal messages already cite the Phase 83 re-probe date.
2. `83-CONTEXT.md` status line is a Task 4 lane file; it should flip to Executed citing this remainder.
3. Api sub-item of Task 6(3) (`Harness:Adapter=Rpc` does not alter remote registration): verified read-only —
   `services/Autonogrammer.Api/Program.cs` registers no remote transport factory (remote composition is
   Native-side); no Api test was added because `tests/Autonogrammer.Api.IntegrationTests` is Tasks 3/5 lane-owned.
4. Test runs: this lane ran no tests (build-only remit). The verifier runs the suites in the plan's
   `verification` block, plus the two-session isolation scenario re-run. Build evidence this lane: 
   `tests/Autonogrammer.Harness.Remote.Tests` ✅, `tests/Autonogrammer.Native.Tests` ✅,
   `services/Autonogrammer.Api` ✅ (all `-c Release`, 0 errors/0 warnings on the last build of each);
   `tests/Autonogrammer.Harness.ContractTests` ❌ at handover — `PluginCommandParityTests.cs(23)` and
   `TransportParityScenarios.cs(37)` fail with CS1570 (badly formed XML `summary`) — Tasks 1–5 lane files,
   untouched by this lane, reported for that lane to fix.
