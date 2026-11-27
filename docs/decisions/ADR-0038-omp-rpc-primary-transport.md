# ADR-0038: omp RPC primary transport

- Status: Accepted 2026-11-26 (Phase 84 cutover; the cutover commit flips the default `Harness:Adapter` / `EngineTransport` to `Rpc` with `Harness:Adapter=Acp` as the supported rollback)
- Date: 2026-11-23
- Owner: autonogrammer-maintainers
- Deciders: autonogrammer-maintainers
- Scope: milestone v0.17.0 "Omp RPC engine rearchitecture" (phases 79-84 in `.planning/ROADMAP.md`): which omp transport is primary for local engine sessions, which stays for the remote boundary, and what retires. Owner directive (parent brief, task T9013-arch, correlation 901308c5-825d-45fe-8832-36b59c0771ae). Blueprint: `docs/architecture/v0170-transport-retirement-blueprint-T9013-arch.md`.

## Context

Facts read in this pass:

- Today ACP (`omp acp`, `Harness.Acp`) is the production transport everywhere: EngineHost `InProcessEngineHost`, Api (`OmpAcpClient`, `DirectSessionRunner`, `AcpHarnessSessionFactory`), AgentWorker, Native, and `Harness.Remote` (ssh, podman, WSL). `AcpAgentSession` implements `IOmpSession` plus the satellite interfaces (`IOmpLiveSteering`, `IOmpModelControl`, `IOmpSessionCommands`, `IOmpSessionOptions`, `IOmpSessionFork`, `IOmpSessionIdentity`).
- `src/Autonogrammer.Harness.Rpc/` already speaks omp's own JSONL RPC (`omp --mode rpc`): `OmpRpcClient` returns `IOmpSession`, `RpcHarnessSessionFactory` exists, `RpcChunkAssembler` handles `rpc_chunk` reassembly, `RpcEventMapper` maps events, `OmpVersionProbe` enforces a version pin on every start. It has zero production consumers (tests only). Phase 11 recorded `session/update` variants from live RPC turns under OD-07 (`docs/runbooks/evidence/omp-rpc-live-variants-2026-10-21.md`), and `Q-APPROVAL-ADAPTER` records the old disposition "RPC is kept for update recording (Phase 11) only", which this record supersedes.
- RPC is the transport with omp-native session control: the scaffold research records commands `prompt`, `steer`, `follow_up`, `abort`, `get_state` and a `ready` frame advertising protocol versions with `negotiate_protocol` (PROJECT.md Context). ADR-0037 notes RPC has `get_subagents` where ACP emits no roster; OQ-04 records that ACP has no steer. Steering and state reach the heads today only in ACP-approximated form.
- Terminal sessions (`SessionKind.Terminal`) run the omp TUI in a PTY via `PlannedOmpTerminalStarter` over `Harness.OmpLauncher`/`OmpTerminalLauncher`, owned inside Native `SessionEngineSet` — the brief names this ownership a disaster. `Harness.Herdr` has no production relation (isolated in the Phase 79 C4 check). Provisioning (`OmpManagedInstaller`, `OmpLocator`) is used by every path and is not part of any retirement.
- omp 19.1.1 is installed; the codebase was verified against 18.12.3/18.12.6. Every 19.x protocol fact below is a Phase 79 verification target, not a claim: the `ready` frame shape (OQ-520), `negotiate_protocol` v2 (OQ-521), `rpc_chunk` frame caps (OQ-522), the command-set delta (OQ-523), `--no-ui` validity on 19.1.1 (OQ-524), RPC approval semantics (OQ-525), plugin loading under `--mode rpc` (OQ-526).

## Decision

1. **RPC is the primary local engine transport.** Local sessions (desktop EngineHost, Native, and the API's in-process runner) compose `Harness.Rpc` behind the existing `IOmpSession` surface, selectable through a `Harness:Adapter` option with ACP as the fallback during the milestone and RPC as the default only after the Phase 84 cutover gate.
2. **ACP is retained for the remote boundary.** `Harness.Remote` (ssh `omp acp`, podman, WSL) and any WebSocket-bridged ACP keep `Harness.Acp`; the remote contract, SSE resume semantics and approval default-deny policy do not change.
3. **Parity is contractual, not aspirational.** Before RPC composes anywhere in production (Phase 80), `RpcHarnessSession` must carry prompt, steer, follow_up, abort and get_state through the `IOmpSession` family with the same scripted-fixture tests that pin the ACP session, or name the capability it cannot carry in the phase verification. Approvals on RPC stay deny-by-default until OQ-525 closes.
4. **The PTY terminal engine and Herdr retire in Phase 81.** `PlannedOmpTerminalStarter`, the `SessionKind.Terminal` creation path and `Harness.Herdr` are deleted; persisted terminal records render read-only and non-startable (OQ-527). `Harness.OmpLauncher` provisioning stays.
5. **One shared launch plan.** Both transports consume the same launch composition (`ILaunchContributor`: plugin bundle overlays, `ConfigOverlayWriter --config`, MCP gateway servers) so plugin behaviour cannot fork by transport; Phase 82 proves plugins load under `--mode rpc` and Phase 83 asserts the shared plan in the architecture tests.
6. **Protocol facts are frozen by evidence (Phase 79).** The handshake, version pin/probe behaviour and command surface are recorded observed-19.1.1 vs verified-18.12.6 before Phase 80 starts; an unverified fact is an interface, a stub and an open-question row, never an invented mapping. This record moves to Accepted only at the Phase 84 cutover gate.

## Candidate chosen

Promote the in-tree `Harness.Rpc` (`OmpRpcClient`/`OmpRpcSession` over `omp --mode rpc`, custom C#) to primary local transport behind `IOmpSession`, and keep the in-tree `Harness.Acp` client for the remote boundary. Alternatives considered: (a) ACP everywhere, retire Rpc — rejected: it contradicts the owner directive for this milestone, and it permanently approximates the steer and state surfaces that RPC carries natively (OQ-04; ADR-0037 on `get_subagents`); (b) a TypeScript omp SDK or sidecar for the RPC protocol — rejected: the constraint is C#/.NET 10 only, and a second runtime would split the harness across a process and language boundary the in-tree client already covers; (c) keep the PTY TUI as the interactive local engine — rejected: its ownership inside `SessionEngineSet` is the named defect, and a TUI screen is not a session contract any head can test.

## Licence

No third-party component is adopted by this record. Both transports are this repository's own code, licensed `AGPL-3.0-or-later` (ADR-0018; licence text https://www.gnu.org/licenses/agpl-3.0.html). omp stays an external process located or installed by the existing provisioning path; its own licence terms are unchanged by this record.

## Verification route

Phase 79 freezes the protocol with no live model turn: the recorded 19.1.1 handshake and command fixtures in `docs/architecture/omp-19-reverification.md`, `dotnet test --project tests/Autonogrammer.Harness.Rpc.Tests -c Release --no-build --filter-not-trait "Category=Live"` and the same command for `tests/Autonogrammer.Harness.Acp.Tests`. Phase 80 proves parity with shared scripted-fixture tests over both factories; Phase 83 gates the boundary with `tests/Autonogrammer.ArchitectureTests` (LayerMap, adapter composition); each phase ends with the locked restore, warning-free build and its named test projects, full gate `./bootstrap.sh` at Phases 80, 81 and 84. `DecisionRecordTests` covers this record and its index row.

## Open risks

- omp 19.1.1 may have changed the RPC handshake, caps or command set against the 18.12.6-verified client; OQ-520 to OQ-524 must close or be stubbed before Phase 80 composes anything.
- RPC approval semantics are unverified (OQ-525): if `--mode rpc` cannot surface a permission request equivalent to ACP's `session/request_permission`, RPC sessions stay deny-by-default and approval-capable flows stay on ACP until a verified mapping exists.
- Plugins, skills overlays, `--config` and MCP servers may load differently under `--mode rpc` than under `omp acp` (OQ-526); Phase 82 is the proof, and a silent difference would fork plugin behaviour by transport.
- Users may hold persisted terminal session records; retiring the starter without the OQ-527 read-only state would strand them.
- The milestone label v0.17.0 collides with the already-shipped 0.17.0 product release named in Phase 78; the label is a programme name here and authorises no version change.
- SEC-77-01 stays an open blocker for the programme; nothing in this record waives or closes it.
