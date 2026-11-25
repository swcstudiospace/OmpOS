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

## Phase 82 flag surface and MCP channel (Task 1, 2026-11-25, no live turn)

Phase 82 plan 82-01 Task 1 froze what `--mode rpc` on omp 19.1.1 accepts and honours, before any production
code change. Every verdict below cites either the 19.1.1 `--help` text (byte-identical to Phase 79), one of the
handshake-only probes run 2026-11-25, or is recorded "unobserved — refusal stays". No probe sent `prompt`,
`authenticate`, `session/new` or a positional message; each ran with a fresh `HOME` under `/tmp` (plus XDG dirs
inside it), a credential-scrubbed environment (no `API_KEY`/`TOKEN`/`SECRET`/`PASSWORD`/`ANTHROPIC`/`OPENAI`/`_KEY`
variables), a throwaway cwd and `--session-dir` under `/tmp` with `--no-session`, and ended by closing stdin
(EOF → exit 0, never killed). Raw probe output stayed in `/tmp/omp82t1/` scratch and is not committed (rule R5);
only byte counts, exit codes and line hashes are frozen here. The one repeatable capture is the opt-in gated test
`tests/Autonogrammer.Harness.Rpc.Tests/RpcPluginLoadCaptureTests.cs` (same `AUTONOGRAMMER_RPC_LIVE_CAPTURE=1` +
`Category=Live` pattern as `RpcLiveCaptureTests`, skipped by default, excluded by
`--filter-not-trait "Category=Live"`): it starts `omp --mode rpc` with the full planned argv over a fixture
workspace, drives `negotiate_protocol` + `get_state` via `OmpRpcSession`, never sends a prompt, then closes
stdin and asserts the plugin `available_commands_update` commands and the `gsd-core-omp` connection report
(`mode: "rpc"`). It writes its frames to `TestData/frames/rpc-plugin-load-<timestamp>.ndjson` only when run.

### Help (observed-19.1.1)

- argv: `~/.local/bin/omp --help` (fresh `HOME`, credential-scrubbed), exit 0, stdout 19564 bytes,
  sha256 `b4f9b413d56901993fe5f54d9133eaa8f2f23b93050f45634d6647d229aa2dc8` — identical to the Phase 79
  frozen help. Relevant lines, verbatim:
  - `      --mode=<value>                    Output mode: text (default), json, rpc, or rpc-ui`
  - `      --cwd=<value>                     Directory to start in (overrides the launch cwd)`
  - `      --no-ui                           With --mode rpc or rpc-ui: run extensions headless (rpc-ui tool UI remains enabled)`
  - `      --session-dir=<value>             Directory for session storage and lookup`
  - `      --no-session                      Don't save session (ephemeral)`
  - `      --model=<value>                   Model to use (fuzzy match: "opus", "gpt-5.2"; exact with a provider: "openai/gpt-5.2")`
  - `      --no-extensions                   Disable extension discovery (explicit -e paths still work)`
  - `  -e, --extension=<value>               Load an extension file (can be used multiple times)`
  - `      --config=<value>                  Load an extra config.yml-style overlay for this run (repeatable)`
  - `      --skills=<value>                  Comma-separated glob patterns to filter skills (e.g., git-*,docker)`
  - `      --add-dir=<value>                 Add a workspace directory beyond the working directory (repeatable)`
  - `      --append-system-prompt=<value>    Append text or file contents to the system prompt`
  - `      --approval-mode=<value>           Override tools.approvalMode for this session (always-ask|always-ask-cached|write|yolo)`
  - `  --plugin-dir <path>        Load plugin from directory (repeatable)` (Plugin Options section only; not a launch flag)
  - The words `mcp` do not appear anywhere in `--help`: no MCP flag exists at launch.
- argv: `~/.local/bin/omp acp --help`, exit 0, stdout 79 bytes,
  sha256 `8281e3859ff508a2aa842d9a7b0ea30c1d259a7e93f071d619b2587ff936b794`.
- argv: `~/.local/bin/omp --version`, exit 0, stdout `omp/19.1.1`.

### Probes (observed-19.1.1, scratch paths shown as `/tmp/omp82t1/…`, binary path R1-redacted)

Probe client sends only `negotiate_protocol` (`protocolVersion: 2`) and `get_state`, never `prompt`, then
closes stdin. Commands seen in `available_commands_update` are counted by `name`.

| Probe | argv (after R1) | exit | duration | stdout | effect |
|---|---|---|---|---|---|
| basic | `omp --mode rpc --no-ui --cwd <ws> --session-dir <sessions> --no-session --model opus` | 0 | 20052 ms | 47605 B, 4 lines, sha256 `3961d1fd…f6af7` (see line hashes below) | `ready` 168 B, `available_commands_update` 16802 B with 53 commands, negotiate + get_state responses; stderr empty |
| full | basic + `--no-extensions -e <ultrathink> -e <swarm/omp> -e <gsd> --config <overlay> --skills fixture-* --add-dir <adddir> --append-system-prompt <sysprompt> --approval-mode write` with plugin state-dir + `AUTONOGRAMMER_CONNECTION_FILE`/`AUTONOGRAMMER_STEER_DIR` env | 0 | 23322 ms | 56370 B, 4 lines, sha256 `a80703cf…5d87` | `available_commands_update` 17929 B with 60 commands; connection report + `alive.json` written; stderr empty |
| badext | basic + `--no-extensions -e /tmp/omp82t1/fixture/does-not-exist -e <gsd>` | 0 | 7953 ms | 47333 B, 4 lines | `gsd-core` present, bundled `/annotate` `/green` `/review` absent; stderr `Failed to load extension /tmp/omp82t1/fixture/does-not-exist: Failed to load extension: Cannot find module …`, exit still 0 |
| unknown | basic + `--definitely-not-a-real-flag-xyz` | 2 | 6067 ms | 0 B, 0 lines | no `ready`; stderr `Error: unknown flag: --definitely-not-a-real-flag-xyz` + `Run omp --help…` |
| mcpfile | basic with cwd `<ws-mcp>` containing `.omp/mcp.json` = `{"mcpServers": {"probe-srv": {"type": "stdio", "command": "/tmp/omp82t1/fixture/nope-server", "args": []}}}` | 0 | 5078 ms | 47605 B, 4 lines | `ready` reached, stderr empty; `get_state.dumpTools` still the same 11 bundled tools as basic — the file had no handshake-level effect |

Per-line sha256 (line bytes without LF), in frame order (`ready`, `available_commands_update`, negotiate
response, get_state response):

- basic: `0190791e…911`, `0292fe20…d1`, `3501496d…22`, `471873d0…e4` (full values in `/tmp/omp82t1/*.meta.json`).
- full: `0190791e…911` (`ready` identical to basic), `6e82e35f…8b`, `3501496d…22`, `cac9529f…d0`.
- badext: `0190791e…911`, `ac74d1a6…4d`, `3501496d…22`, `47095aea…f`.

Observed effects, compared against basic:

- full adds exactly the 10 plugin commands `gsd-core`, `swarm`, `ultrathink-off`, `ultrathink-on`,
  `ultrathink-quick`, `ultrathink-skip`, `ultrathink-status`, `ultrathink-swarm`, `ultrathink-track`,
  `ultrathink-ui` (60 vs 53), and with `--no-extensions` removes the 3 bundled commands `annotate`, `green`,
  `review` — exactly the side effect `docs/plugins.md` records for the ACP launch.
- The connection report (`AUTONOGRAMMER_CONNECTION_FILE`) was written: `mode: "rpc"`,
  `commandsAvailable: true`, 11 commands (`autoresearch`, `gsd-core`, `swarm`, the 8 `ultrathink-*` names),
  31 tools. `alive.json` in `AUTONOGRAMMER_STEER_DIR` names `"mode": "rpc"`.
- The fixture file's `fixture standing context` string is present in full's `get_state.systemPrompt` and
  absent from basic's.
- **Drift against `docs/plugins.md` (flag rejection, not a silent ignore):** a top-level unknown flag is
  rejected (`unknown` probe, exit 2) — the plan's "omp silently ignores unknown flags" does not hold for launch
  flags on 19.1.1, so argv acceptance on this binary does prove the flag exists. The nonexistent `-e` path is
  still ignored with only a stderr warning (`badext`, exit 0), as `plugins.md` says. Whether an accepted flag
  is *honoured* still needs an observed effect, so the table below separates accepted from honoured.
- MCP candidates, in the plan's order: (a) an `mcp` block in the `--config` overlay — the overlay is settings
  only and cannot declare servers (ADR-0010 Q1, source-verified: `grep -n mcpServers src/config/settings.ts` is
  empty, and no code reads `mcpServers` from settings). An `mcpServers:` overlay key is merged and inert. The
  `config` subcommand did not reflect the overlay either (`config get mcp.enableProjectConfig` stayed `true`
  with `--config` before the subcommand; `--config` after `config get` is rejected). (b) omp's own
  project/user MCP configuration files — `mcpfile` shows a project `.omp/mcp.json` server has no
  handshake-level effect under `--mode rpc`; whether MCP discovery is deferred until a prompt (which would
  need a live turn) is unobservable by rule, so the claim is "no channel observed", not "MCP never loads".
  (c) an RPC command — none observed: `--help` has no MCP flag, ADR-0010 records
  `grep -rn -i mcp src/modes/rpc` = 4 lines, all `memcpy`/`mcp_prompt` label, and no frozen RPC frame carries
  an MCP payload. The `mcp` name inside the 53/60-command lists comes from the `/mcp` slash command, not a
  server carrier.

### Verdict table (P1–P12)

| Point | Flag / channel | Accepted under `--mode rpc` | Honoured (observed effect) | Disposition |
|---|---|---|---|---|
| P1 plugin `-e` entries | `--no-extensions` + `-e <dir>` after the transport words | observed-19.1.1 (help + full/badext reached `ready`, exit 0) | observed-19.1.1 (17929-byte commands frame; +10 plugin commands, −3 bundled commands, report `mode: rpc`; bad-ext ignored on stderr) | Task 3 forwards the plan arguments; Task 5 proves per-plugin load |
| P2 hooks bridge | same `-e` channel + `AUTONOGRAMMER_HOOKS_*` env | observed as a channel (P1) | unobserved — no hooks-bridge entry or bridge env was probed (no bridge endpoint exists in the fixture) | refusal reason stays until Tasks 5/7 prove it; else OQ-526 difference |
| P3 settings overlay | `--config <path>` + 0600 overlay file | observed-19.1.1 (full reached `ready`, exit 0, stderr empty) | unobserved — no overlay-key effect is visible in the handshake (`config get` did not read it back; `get_state` carries no overlay field) | Task 3 forwards argv; Task 4 claims overlay *byte* parity only |
| P4 bundle skills | overlay `skills.customDirectories=` + `--skills <csv>` | observed-19.1.1 (full reached `ready`, exit 0) | unobserved — no skill enumeration reaches the handshake (`dumpTools` holds the same 11 tools in basic and full) | Task 7 decides |
| P5 Cowork standing context | `--append-system-prompt <file>` (+ overlay/prompt fields) | observed-19.1.1 | observed-19.1.1 for the flag (fixture string present in full `get_state.systemPrompt`, absent in basic) | Task 7 decides |
| P6 isolation wrapper | host-side `ProcessWrapper`, not omp argv | — | unobserved (host process-start semantics; not probed) | Task 7; refusal stays until then |
| P7 substrate MCP | `session/new.mcpServers` on ACP; no RPC carrier found | — | no channel observed (help has no MCP flag; overlay cannot declare servers; project `mcp.json` has no handshake effect; no RPC MCP command; deferred-to-prompt discovery unobservable by rule) | record difference in OQ-526 (Task 6) |
| P8 Cowork gateway MCP | same gap as P7 | — | no channel observed (same probes) | record difference in OQ-526 (Task 6) |
| P9 Native user MCP | same gap as P7 | — | no channel observed (same probes) | record difference in OQ-526 (Task 6) |
| P10 per-session environment | complete-env child environment | observed-19.1.1 (report + `alive.json` written at the exact env-named paths) | observed-19.1.1 | Task 4 diff proof |
| P11 run-mode argv + bash overlay | `--approval-mode <write>` + overlay YAML | observed-19.1.1 (help values; full reached `ready` with `write`) | unobserved — approval behaviour needs a prompt and stays OQ-525; bash overlay is P3 | Tasks 3/9 argv mechanics only |
| P12 `--add-dir` | `--add-dir <path>` | observed-19.1.1 (full reached `ready`, exit 0) | unobserved — no handshake field carries workspace roots (`get_state` has no `additionalDirectories` key) | Task 3 forwards argv; Task 4 parity |

Consequences for later tasks: Tasks 3/4/5 may implement the argv channel for P1, P3, P11 and P12 (flag
existence and acceptance are observed; *honouring* of P3/P4/P12 is claimed only where the table says observed).
P2, P4–P6 need the Task 5/7 captures; P7–P9 are recorded as the phase's named transport difference unless a
Task 6 capture observes a carrier — an unobserved candidate is never implemented on spec.
