# omp RPC live top-level frame types, 2026-10-21

Requirement: Q-HARN-UPDATE (which variants a real omp turn emits; this run covers the RPC transport, `omp --mode rpc`).

## Capture

- File: `tests/OmpOS.Harness.Rpc.Tests/TestData/frames/rpc-live-20261021T171351Z.ndjson`.
- Lines: **273**. Every line carries a top-level `type`; no line lacks one.
- Sidecar: `rpc-live-20261021T171351Z.turns.json`, **10** turns, numbered 1 to 10.
- Stop reasons (sidecar): all 10 turns are `completed`. The `prompt_result` frames agree: 10 frames with status `completed` and `sessionSettled: true`. Their acks carry no `agentInvoked` field (see the ack note below).
- Prompt ids 3 to 12, one per turn. Each `prompt_result` id matches its `prompt` response id.
- Omp version: 18.12.5 (`omp --version` on the same PATH, run immediately before the capture; the frames file itself does not carry the version).
- Raw frames are committed as fixtures. They are test data, not a secret store.

**Prompt ack note.** All 10 `prompt` responses have no `data` member (`{"id":"3","type":"response","command":"prompt","success":true}`). The replay test supplies `{"agentInvoked":true}` for each ack and keeps every turn frame verbatim.

## Top-level types

Counted with `jq` over the capture. "New" means the type does not occur at top level in `rpc-out.ndjson` (6 lines: `ready`, `advisor_cost_changed`, `available_commands_update`, 3 × `response`).

| Top-level type | Count | New? | Disposition | Test |
|---|---:|---|---|---|
| `message_update` | 127 | yes | **mapped** `text_delta` (10) to `TextDeltaEvent`, `thinking_delta` (83) to `ThoughtEvent`; block markers `text_start` (10), `text_end` (10), `thinking_start` (7), `thinking_end` (7), 34 in total, **fallback** (`null`) | `OmpRpcLiveVariantContractTests.MessageUpdateMapsDeltasAndFallsBackOnBlockMarkers` |
| `message_start` | 30 | yes | **fallback** (`null`) | `MessageStartReachesFallback` |
| `message_end` | 30 | yes | **fallback** (`null`) | `MessageEndReachesFallback` |
| `agent_start` | 10 | yes | **fallback** (`null`) | `AgentStartReachesFallback` |
| `agent_end` | 10 | yes | **fallback** (`null`) | `AgentEndReachesFallback` |
| `turn_start` | 10 | yes | **fallback** (`null`) | `TurnStartReachesFallback` |
| `turn_end` | 10 | yes | **fallback** (`null`) | `TurnEndReachesFallback` |
| `prompt_result` | 10 | yes | **completion path**, not the mapper: `OmpRpcSession.Dispatch` routes it to `CompletePrompt`, which yields `CompletedEvent("completed")` as the turn's last event | `PromptResultCompletesTheTurnThroughTheSession` |
| `session_settled` | 10 | yes | **fallback** (`null`) | `SessionSettledReachesFallback` |
| `thinking_level_changed` | 10 | yes | **fallback** (`null`) | `ThinkingLevelChangedReachesFallback` |
| `available_commands_update` | 3 | no (1 in `rpc-out.ndjson`) | **fallback** (`null`) | `AvailableCommandsUpdateReachesFallback` |
| `response` | 12 | no | command correlation, not a stream frame | existing `RpcFrameReplayTests` |
| `ready` | 1 | no | handshake, not a stream frame | existing `RpcFrameReplayTests` |

Total: 127 + 30 + 30 + 10 × 6 + 10 + 10 + 3 + 12 + 1 = **273**.

The `rpc-out.ndjson` fixture also contains `advisor_cost_changed` (1), which this capture does not contain. It is unmapped and is not part of this run.

**Nested versus top-level counts.** The brief's list of counts mixed levels. `text_start`, `text_end`, `text_delta`, `thinking_start`, `thinking_delta`, and `thinking_end` are values of `assistantMessageEvent.type` inside `message_update`. They are not top-level types. The top-level counts are the table above.

**No `sessionUpdate` field.** The capture has 0 lines containing `sessionUpdate`. RPC frames discriminate on `type`.

## Mapping decisions

The mapper is `src/OmpOS.Harness.Rpc/RpcEventMapper.cs`. Its `_ => null` branch is the fallback. No mapper change was made in this run, because no newly seen variant fits an existing `HarnessEvent` unambiguously.

- **`message_update`** already maps. The text and thinking deltas fit `TextDeltaEvent` and `ThoughtEvent`. The block markers carry no `delta`, so they fall to `null`, as the mapper's `TryString(inner, "delta", ...)` guard already requires.
- **`prompt_result`** is terminal. It must complete the prompt once. Mapping it in `RpcEventMapper` as well would publish a second completion, so the completion path stays the only route.
- **`turn_end`** is not terminal. A prompt can span several turns, so `CompletedEvent` would end the stream early.
- **`agent_end`** carries the whole message history. It has no `HarnessEvent` shape and duplicates the terminal.
- **`agent_start`, `turn_start`, `message_start`, `message_end`, `session_settled`** are lifecycle or settle markers with no content. No existing event represents them.
- **`thinking_level_changed`** reports a configuration change. `DiagnosticEvent` is for a non-terminal diagnostic raised while a turn runs, which this frame is not, so it stays on the fallback.
- **`available_commands_update`** lists slash commands. No `HarnessEvent` models a command list, so it stays on the fallback.

## Correction to Q-HARN-UPDATE

The row's "Not yet" note says the RPC transport (`omp --mode rpc`) emits no session/update. That is contradicted for one variant, and the row should be corrected.

- The RPC transport emits an `available_commands_update` variant: 1 frame in `rpc-out.ndjson` and 3 frames in this capture (file lines 2, 6, 7).
- It arrives as a `type`-discriminated frame, not as a `session/update` envelope. No frame carries a `sessionUpdate` field.
- The variant reaches the unknown fallback on RPC, as it does on ACP. `AvailableCommandsUpdateReachesFallback` asserts this.

Corrected statement for the row: the RPC transport emits an `available_commands_update` frame (a `type`-discriminated frame, with no `session/update` envelope and no `sessionUpdate` field). The mapper sends it to the fallback. Separately, this run's stop reasons (`completed`) differ from the ACP run's `end_turn`. That is expected, because the two transports use different wire vocabularies.

The open-questions file was not edited. This correction is recorded here for the orchestrator to apply.

## Tests

`tests/OmpOS.Harness.Rpc.Tests/OmpRpcLiveVariantContractTests.cs`, 12 facts. The capture is read from the source tree: a walk up from the test output directory to `OmpOS.slnx`, then `tests/OmpOS.Harness.Rpc.Tests/TestData/frames`. The sidecar is read the same way, because the test project copies only `*.ndjson` to its output, and the project file is not changed.
