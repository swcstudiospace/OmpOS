# OD-07 live `session/update` variants, 2026-10-21

Requirement: OD-07 (unseen session/update variants recorded from live omp turns and frozen as fixtures; Q-HARN-UPDATE).

## Run

- omp version: `omp/18.12.5` (`omp --version`).
- Spawn: `omp acp` with the working directory set to an empty scratch directory outside the repo. The driver was a throwaway Python script (`/tmp/omp-live/run.py`, not committed).
- Turns run: **10** `session/prompt` requests, the owner's cap. Every turn ended with `stopReason: end_turn`.
- Agent-initiated requests: one `session/request_permission`, answered `cancelled`. The client advertised no fs or terminal capabilities.
- Inbound frames: 154 lines, comprising 141 `session/update` notifications, 12 responses (`initialize`, `session/new`, and 10 prompts), and 1 request.
- Model: the configured default (`anthropic/claude-haiku-5-5`, read from the `config_option_update` `currentValue`).
- Raw frames stay in `/tmp/omp-live/frames.jsonl` and are not committed. They contain session content.

**Mode deviation.** The assignment named the `--mode rpc` client. `session/update` is not emitted there. `OmpRpcProcess` spawns `omp --mode rpc --no-ui --cwd`, and `RpcEventMapper` handles `type`-discriminated frames (`message_update`, `tool_execution_*`). No `session/update` appears under `src/OmpOS.Harness.Rpc/`. The `session/update` mapper is `AcpWireMapping.MapSessionUpdate`, keyed on `sessionUpdate`, and is reached through `omp acp`. The capture therefore used `omp acp`.

## Variants

Mapping is decided by `AcpWireMapping.MapSessionUpdate` (`src/OmpOS.Harness.Acp/AcpWireMapping.cs`). Its `_ => null` branch is the fallback.

| Variant (`sessionUpdate`) | Count | Mapping | Test |
|---|---:|---|---|
| `agent_message_chunk` | 32 | mapped, `TextDeltaEvent` | `AcpAgentSessionTests.PromptStreamMapsUpdatesInWireOrderAndEndsWithStopReason` (scripted frame in `tests/OmpOS.Harness.Acp.Tests/`) |
| `agent_thought_chunk` | 75 | mapped, `ThoughtEvent` | same as above |
| `tool_call` | 1 | mapped, `ToolCallEvent` | same as above |
| `tool_call_update` | 1 | mapped, `ToolCallUpdateEvent` | same as above |
| `plan` | 0 | mapped, `PlanEvent` | not observed in this run; the same existing test covers it |
| `available_commands_update` | 1 | **fallback** (`null`) | `OmpLiveVariantContractTests.AvailableCommandsUpdateReachesFallback` |
| `session_info_update` | 11 | **fallback** (`null`) | `OmpLiveVariantContractTests.SessionInfoUpdateReachesFallback` |
| `config_option_update` | 10 | **fallback** (`null`) | `OmpLiveVariantContractTests.ConfigOptionUpdateReachesFallback` |
| `usage_update` | 10 | **fallback** (`null`) | `OmpLiveVariantContractTests.UsageUpdateReachesFallback` |

The four fallback variants are the distinct discriminators that the mapper does not handle. No `HarnessEvent` type models usage, config options, available commands, or session info (`src/OmpOS.Harness.Abstractions/`), so none can be mapped unambiguously. Each is asserted to reach the fallback. The turn then yields only `CompletedEvent("end_turn")`, with no mapped event and no `DiagnosticEvent`. A `DiagnosticEvent` is emitted only on a decode failure.

## Frozen fixtures

Location: `tests/OmpOS.Harness.ContractTests/Fixtures/`. Each file is one `session/update` notification, with `sessionId` replaced by the placeholder `fixture-session`.

- `session-update-available-commands-update.json`: the smallest real frame was 164,667 bytes. I kept two built-in commands, `model` and `switch`, and dropped the user-installed `skill:*` names. Result: 319 bytes.
- `session-update-config-option-update.json`: the smallest real frame was 95,354 bytes. I kept the `mode` and `thinking` options verbatim. The `model` option's 739-entry list of configured providers was reduced to one placeholder entry, and its `currentValue` was replaced with `example/model-fixture`. Result: 1,044 bytes.
- `session-update-session-info-update.json` and `session-update-usage-update.json`: verbatim except for the `sessionId` placeholder.

## Verification status

- The contract tests were **not built or run**. The contract forbids `dotnet build` and test runs in this unit.
- Frame counts and discriminators come from the live capture, enumerated with a script over `frames.jsonl`.
- The fixture JSON parses, and each fixture's `sessionUpdate` matches its test name. The tests assert this at runtime.

## Blocker and open items

1. **Build blocker (outside ownership).** `OmpLiveVariantContractTests` uses `OmpAcpClient` and `AcpClientOptions` from `OmpOS.Harness.Acp`. `tests/OmpOS.Harness.ContractTests/OmpOS.Harness.ContractTests.csproj` references only `OmpOS.Harness.Abstractions`. It needs this line, which I did not add because the unit is limited to new files:
   `<ProjectReference Include="../../src/OmpOS.Harness.Acp/OmpOS.Harness.Acp.csproj" />`
   Until it is added, the test project will not compile.
2. No `src/` mapping change is required. The four fallback variants are correctly unmapped.
3. `plan` was not observed live. It is already mapped and covered by the existing Acp test.
4. The RPC-mode client produces no `session/update`. Any live-variant coverage of `RpcEventMapper` needs its own capture, outside this unit.
