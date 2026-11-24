# omp 19.1.1 re-verification ledger

Status: Current (Phase 79 evidence ledger, verified 2026-11-24; see 79-VERIFICATION.md).

Phase 79 (plan 79-01, requirements RPC-79-1 to RPC-79-4). Every protocol fact below is labeled
**observed-19.1.1** (produced by the installed binary on 2026-11-23) or **verified-18.12.6** (the surface the
two harness clients were verified against), with the command or fixture behind it (AC-301: 0 unlabeled facts).
Anything that cannot be observed without a model turn is an open-question row, never a ledger claim.

Evidence: `docs/runbooks/evidence/omp-19.1.1-protocol-2026-11-23/README.md` (exact argv, exit codes, frame
hashes). Frozen captures conform to `docs/data/contracts/OMP19-protocol-evidence-v1.json` v1.0.0:

- `tests/Autonogrammer.Harness.Rpc.Tests/TestData/frames/rpc-observed-19.1.1-20261123T202249Z.ndjson` (+ `.manifest.json`)
- `tests/Autonogrammer.Harness.Acp.Tests/TestData/frames/acp-observed-19.1.1-20261123T202251Z.ndjson` (+ `.manifest.json`)

No live model turn was taken (OD-07 covers Phase 11 only): the probe sent no `prompt`, no ACP
`authenticate`/`session/new`, ran with a fresh `HOME` under `/tmp` and with credential-shaped environment
variables scrubbed, so no provider credential was reachable even by accident. Both manifests record
`live_model_turn: false`.

## Binary identity

| Fact | Label | Source |
|---|---|---|
| `omp --version` prints `omp/19.1.1`, exit 0 | observed-19.1.1 | evidence README, "Version" |
| Resolved binary `~/.local/bin/omp` (home prefix redacted, rule R1) | observed-19.1.1 | both manifests, `omp_binary_path` |
| Legacy captures are the 18.12.x surface: `rpc-out.ndjson` (18.12.3-era), `rpc-live-20261021T171351Z.ndjson` (18.12.5), `acp-out.ndjson` (agentInfo version 18.12.3) | verified-18.12.6 | `docs/runbooks/evidence/omp-rpc-live-variants-2026-10-21.md`; fixture bytes |

## RPC handshake (OQ-520, OQ-521, OQ-522)

Capture argv (after R1 redaction): `~/.local/bin/omp --mode rpc --no-ui --cwd /tmp/omp79final/rpc/cwd
--session-dir /tmp/omp79final/rpc/sessions --no-session --model opus`. The first four elements are exactly
what `OmpRpcProcess.Start` passes; `--session-dir`/`--no-session` keep the probe ephemeral and `--model opus`
selects a model in the fresh profile (see "Startup without a model" below). Exit 0 on stdin EOF; stderr empty.

| # | Fact | 18.12.x recorded | 19.1.1 observed | Drift |
|---|---|---|---|---|
| R-READY | First stdout frame is `ready` with `protocolVersion: 1`, `supportedProtocolVersions: [1, 2]`, `maxFrameBytes: 1048576`, `maxReassembledFrameBytes: 67108864` | verified-18.12.6, `rpc-out.ndjson` line 1 | observed-19.1.1, frozen capture seq 1 (1648 ms after spawn): identical values, **plus** `supportsBashExcludeFromContext: true` | **changed (additive)** |
| R-NEGOTIATE | `negotiate_protocol` with `protocolVersion: 2` answers `{"type":"response","command":"negotiate_protocol","success":true,"data":{"protocolVersion":2}}` | verified-18.12.6, `rpc-out.ndjson` line 4 | observed-19.1.1, seq 4: same shape; the response echoes the request id in the form sent (string `"1"` from the probe; numeric `1` in the old capture) | same |
| R-CAPS | Frame cap 1 MiB per physical frame, 64 MiB per reassembled logical frame, as coded in `OmpRpcFrames.MaxPhysicalFrameBytes` / `MaxReassembledFrameBytes` | verified-18.12.6, `ready` frame + code | observed-19.1.1, `ready` frame values match the coded constants byte for byte | same |
| R-CHUNK | `rpc_chunk` payload cap 256 KiB (`OmpRpcFrames.MaxChunkPayloadBytes`) and derived count cap | verified-18.12.6, code + `RpcChunkReassemblyTests` | No frame over 1 MiB occurred in the handshake (largest 30.5 KiB before redaction), so no `rpc_chunk` was emitted | **unobservable-without-model-turn** (stays verified-18.12.6; OQ-522 chunk-payload half remains open to a large-frame fixture) |
| R-UNSOLICITED | Unsolicited `available_commands_update` frames can arrive around the handshake and must not fault the session | verified-18.12.6, `rpc-out.ndjson` line 3 | observed-19.1.1, seq 3: one `available_commands_update` (53 commands) arrived after the probe sent `negotiate_protocol` and before its response. The old capture lists 701 commands from a fully configured profile; the count difference is profile content, not protocol drift | same (content profile-dependent) |
| R-EOF | Closing stdin lets the process exit 0 with empty stderr | verified-18.12.6, `OmpRpcProcess` contract | observed-19.1.1, exit 0, stderr empty | same |

New-field disposition (R-READY): `ReadyFrame` does not model `supportsBashExcludeFromContext` today and
System.Text.Json ignores it, which `Omp19HandshakeContractTests` proves by deserializing the 19.1.1 line and
replaying the handshake through `OmpRpcSession`. Modeling the field is Phase 80 work if a consumer needs it;
Phase 79 makes no production source change (plan Task 3).

## RPC command surface (OQ-523)

Commands the client sends (one `RpcCommand` type each, `src/Autonogrammer.Harness.Rpc/RpcProtocol`):

| Command | Sent by | 19.1.1 evidence status |
|---|---|---|
| `negotiate_protocol` | `OmpRpcSession.HandshakeAsync` | observed-19.1.1 — confirms v2 (R-NEGOTIATE) |
| `get_state` | `OmpRpcSession.GetStateAsync` / capability refresh | observed-19.1.1 — shape below |
| `nope` (unknown-command probe, `UnknownCommandProbe`) | tests / probe | observed-19.1.1 — `{"type":"response","command":"nope","success":false,"error":"Unknown command: nope"}`, identical to the verified-18.12.6 envelope in `rpc-out.ndjson` line 6 |
| `prompt` (incl. `streamingBehavior: "steer"` for steering, `OmpRpcSession`) | prompt path | unobservable-without-model-turn — verified-18.12.6 shape stands (`rpc-live-20261021T171351Z.ndjson`) |
| `abort` | prompt path | unobservable-without-model-turn — verified-18.12.6 shape stands |
| `abort_and_restore_queue` | queue restore | unobservable-without-model-turn — verified-18.12.6 shape stands |
| `switch_session` | resume path | unobservable-without-model-turn — verified-18.12.6 shape stands |

`get_state` on 19.1.1 (observed-19.1.1, frozen capture seq 6, with `data.systemPrompt` and `data.dumpTools`
replaced by `<redacted>` under rule R4 before freezing): `success: true`; `data` carries every state key the
verified-18.12.6 capture carries **except** `sessionFile`, and adds `configuredThinkingLevel`,
`thinkingLevels`, and `slowModeScope`. The missing `sessionFile` is confounded by the probe's `--no-session`
(ephemeral sessions have no file), so it is recorded as unobservable here, not as a removal. `data.model` for
the selected model (`claude-opus-4-5` / `anthropic`) carries the same key families as the verified-18.12.6
capture plus `webSearch`, `webSearchModel`, `promptCache`; model-catalog contents are data, not protocol.
`steeringMode` and `followUpMode` are both `"one-at-a-time"` (observed-19.1.1), matching verified-18.12.6.

## Launch flags (OQ-524)

| Fact | Label | Source |
|---|---|---|
| `--mode rpc`, `--no-ui`, `--cwd <dir>` are accepted: the handshake above ran with exactly those spellings and exited 0 | observed-19.1.1 | frozen RPC capture argv |
| `--help` documents `--mode=<value>` ("text (default), json, rpc, or rpc-ui"), `--cwd=<value>`, `--no-ui` ("With --mode rpc or rpc-ui: run extensions headless (rpc-ui tool UI remains enabled)"), `--session-dir=<value>`, `--no-session`, `--model=<value>`, `--profile=<value>`; exit 0 | observed-19.1.1 | evidence README, "Help" (full text hashed there) |
| `acp --help` prints the ACP stdio usage, exit 0 | observed-19.1.1 | evidence README, "Help" |

## Startup without a model (observed-19.1.1)

With a fresh isolated `HOME` and **no** `--model`, `omp --mode rpc --no-ui --cwd <tmp>` exits 1 before any
`ready` frame, stderr: `No default model selected. Use /login, set an API key environment variable, or select
a local model with /model or --model.` (full text in the evidence README). `OmpRpcProcess.Start` passes no
`--model`, so production RPC starts depend on the user's profile having a default model selected. Whether
18.12.x behaved the same in an empty profile was not recorded then; this is **not** claimed as drift.
Disposition: Phase 80 decides whether the starter surfaces this stderr tail as the start-failure diagnostic
(it already drains stderr into a bounded tail) — no Phase 79 code change.

## ACP handshake (row ACP-INITIALIZE)

Capture argv: `~/.local/bin/omp acp` (exactly what `AcpStdioTransport` starts). One request was sent —
`initialize` with `protocolVersion: 1` and fs capabilities false/false, the same params `AcpAgentSession`
sends — then stdin was closed. Exit 0; stderr empty.

| # | Fact | 18.12.x recorded | 19.1.1 observed | Drift |
|---|---|---|---|---|
| ACP-INITIALIZE | `initialize` result: `protocolVersion: 1`; `agentInfo {name: "omp", title: "omp", version}`; `authMethods: [{id: "agent", name: "Use existing local credentials", description: "Authenticate via the provider keys/OAuth state already configured under ~/.omp."}]`; `agentCapabilities {loadSession: true, mcpCapabilities {http: true, sse: true}, promptCapabilities {embeddedContext: true, image: true}, sessionCapabilities {list, fork, resume, close}}` | verified-18.12.6, `acp-out.ndjson` line 1 | observed-19.1.1, frozen ACP capture seq 2: identical except `agentInfo.version: "19.1.1"` and an added top-level `_meta: {"steering": {"supported": true}}` | **changed (additive + version string)** |
| ACP-AUTH | The single advertised auth method id is `agent`, which is `AcpClientOptions.OmpAuthMethodId` | verified-18.12.6 | observed-19.1.1, same id/name/description | same — no `AcpClientOptions` change |
| ACP-TIMING | Fresh-profile `initialize` latency | verified-18.12.6: ~8 s (`AcpClientOptions.OmpHandshakeTimeout` comment, 45 s budget) | observed-19.1.1: result at 1322 ms in a fresh `HOME` on this host | faster here, host-dependent — no timeout change |
| ACP-STEERING-META | `_meta` on the initialize result | Absent in the verified-18.12.6 capture (`acp-out.ndjson` line 1) | observed-19.1.1: present, `steering.supported: true`; `AcpFrames.InitializeResult.Meta` already reads `_meta` tolerantly | **changed (additive, already parsed)** |

`authenticate`, `session/new` and everything after them were not exercised: they need local credentials or a
session, and stay verified-18.12.6 behind the existing tests.

## Version pin and probe (OQ-524)

| Fact | Label | Source |
|---|---|---|
| `OmpVersion.Parse` parses `omp/19.1.1` to 19.1.1 (the pattern is version-agnostic) | observed-19.1.1 string + `Omp19HandshakeContractTests.Version1911ParsesButStaysOutsideThePinnedLine` | frozen manifests record `omp_version: 19.1.1` from the same binary |
| The pin is still `18.12.x` (`OmpVersion.PinnedMajor/PinnedMinor`); `OmpRpcClient` therefore refuses the installed 19.1.1 with `omp 19.1.1 is outside the pinned 18.12.x range.` before any spawn | code, `OmpPinMismatchTests` (refuses 19.0.0) + the new parse test | — |

**Pin decision (D-03):** the handshake evidence above shows the 19.1.1 RPC surface is 18.12.x-compatible at
the handshake and read-only-command level, with two additive fields. Phase 79 records that evidence and does
**not** move the pin: per the phase boundary, compatibility fixes and the pin change land with this ledger as
their citation in Phase 80, where the prompt-path commands also get their fixtures. Moving the pin here,
before `prompt`/`abort` are observed on 19.1.1, would assert more than the evidence reaches.

## Unobservable without a model turn (stays open)

| Row | Why it stays open |
|---|---|
| OQ-522 (chunk-payload half) | No `rpc_chunk` was emitted: nothing in the handshake exceeds the 1 MiB physical cap. Needs a large-frame fixture (a >1 MiB `get_state`/turn frame) |
| OQ-523 (prompt-path half) | `prompt`, `abort`, `abort_and_restore_queue`, `switch_session` need a turn or a session; recorded above as verified-18.12.6 shapes behind the interface |
| OQ-525 | No permission/approval frame occurred before any prompt; whether RPC has an approval surface is unobservable in a handshake. RPC stays deny-by-default, approvals stay on ACP |

## Current production wiring (RPC-79-4; AC-302, AC-303)

Recorded 2026-11-23 with `grep -rl "Autonogrammer.Harness.Acp" --include="*.csproj" src apps services tests`
and the Rpc twin, plus `grep -rln "OmpRpcClient\|OmpRpcSession\|OmpRpcProcess" src apps services --include="*.cs"`:

- **ACP is the production transport.** Production projects referencing `Autonogrammer.Harness.Acp`:
  `Autonogrammer.Harness.Mcp`, `Autonogrammer.EngineHost`, `Autonogrammer.Harness.Remote`,
  `Autonogrammer.Dispatch.Desktop`, `Autonogrammer.Harness.AgentLoop`, `apps/Autonogrammer.Native`,
  `services/Autonogrammer.AgentWorker`, `services/Autonogrammer.Api` — every production group, none on RPC
  (AC-302).
- **RPC has zero production consumers (AC-303).** `Autonogrammer.Harness.Rpc` is referenced only by test
  projects (`Autonogrammer.Harness.Rpc.Tests`, `Autonogrammer.Harness.Agent.Tests`,
  `Autonogrammer.ArchitectureTests`), and the type grep over `src apps services` outside the Rpc project
  itself returns nothing.
- This plan changed no production source file, so the wiring above is today's wiring unchanged.

## Fixtures frozen from this ledger

| Fixture | Ledger rows | Tests |
|---|---|---|
| `rpc-observed-19.1.1-20261123T202249Z.ndjson` + manifest (8 frames, directions mixed, `ledger_refs` OQ-520..OQ-524) | R-READY, R-NEGOTIATE, R-CAPS, R-UNSOLICITED, R-EOF, command table | `Omp19HandshakeContractTests` (5 tests: ready/caps/new-field, command shapes, session replay, manifest digests, version pin) |
| `acp-observed-19.1.1-20261123T202251Z.ndjson` + manifest (2 frames, `ledger_refs` ACP-INITIALIZE) | ACP-INITIALIZE, ACP-AUTH, ACP-TIMING, ACP-STEERING-META | `Acp19HandshakeContractTests` (2 tests: initialize result, manifest digests) |

The 18.12.x fixtures are untouched and their replay tests still pass unchanged, so both surfaces stay pinned
side by side for Phase 80's parity work.
