# Architecture Research

**Domain:** C#/.NET ACP harness + agent workspace (omp engine) with Generative UI, unified backend, optional durable workflows, layered sandbox control plane
**Researched:** 2026-10-20
**Confidence:** HIGH for protocol facts (ACP v1 docs, omp 18.12.3 bundled `rpc.md`, A2UI v0.9.1 spec, Temporal/.NET/MAUI official docs); MEDIUM for sandbox-vendor shapes (OpenShell v0.1.4 docs, agentOS README/npm, dstack guest-API v1); LOW wherever marked `[INFERENCE]`.

## Standard Architecture

### System Overview

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ SURFACES (apps/)                                                              │
│  ┌───────────────────────┐  ┌──────────────────────────┐  ┌────────────────┐ │
│  │ Blazor WASM (Vercel)  │  │ MAUI Blazor Hybrid       │  │ MAUI Hybrid    │ │
│  │ static publish        │  │ desktop (mac/win)        │  │ mobile (iOS/   │ │
│  │ backend-only          │  │ in-process harness +     │  │ Android)       │ │
│  │                       │  │ spawns omp over stdio    │  │ backend-only   │ │
│  └──────────┬────────────┘  └──────────┬───────────────┘  └───────┬────────┘ │
│             └───────────── shared Razor Class Library (Autonogrammer.Ui) ─┘          │
│               conversation list · session view · composer · block renderer   │
│               A2UI surface renderer · ISessionClient (HTTP+SSE | in-proc)    │
├──────────────────────────────────────────────────────────────────────────────┤
│ BACKEND (services/ — ASP.NET Core on Railway)                                │
│  ┌────────────┐  ┌──────────────────┐  ┌──────────────┐  ┌─────────────────┐ │
│  │ Prompt     │  │ Session Service  │  │ SSE fan-out  │  │ Approval policy │ │
│  │ Parser     │→ │ (one active      │→ │ id:=seq      │  │ hook (deny-by-  │ │
│  │ (Intent-   │  │ prompt, append-  │  │ Last-Event-ID│  │ policy default) │ │
│  │ Envelope)  │  │ only event log)  │  │ resume       │  │                 │ │
│  └────────────┘  └────────┬─────────┘  └──────────────┘  └────────┬────────┘ │
│                           │ IAgentHarness (ACP | omp-RPC | scripted)        │ │
│  ┌────────────────────────┴──────────────────────────────────────┴────────┐ │
│  │ Harness (src/Autonogrammer.Harness): transport → framing → correlation →       │ │
│  │ session state machine → typed events → UiBlock/StreamEvent projection  │ │
│  └────────────────────────┬───────────────────────────────────────────────┘ │
│  ┌────────────────────────┴───────────┐  ┌────────────────────────────────┐ │
│  │ Run orchestration                  │  │ Sandbox control plane          │ │
│  │ DirectSessionRunner (default)      │  │ ISandboxProvider chain:        │ │
│  │ Temporal worker (Workflows:Enabled)│→ │ fake → podman-compose →        │ │
│  │ ISessionRunWorkflow + activities   │  │ agentOS → OpenShell(podman) →  │ │
│  └────────────────────────────────────┘  │ attested-enclave (dstack-shape)│ │
│                                          └────────────────────────────────┘ │
├──────────────────────────────────────────────────────────────────────────────┤
│ ENGINE + ISOLATION (external processes)                                      │
│   omp acp (ACP v1, JSON-RPC 2.0/stdio)   omp --mode rpc (JSONL v1/v2)        │
│   podman/podman-compose · OpenShell gateway/supervisor/sandbox · agentOS VM   │
│   dstack guest agent /var/run/dstack.sock (attestation boundary)             │
├──────────────────────────────────────────────────────────────────────────────┤
│ CONTRACTS (src/Autonogrammer.Contracts) — Session · Message · IntentEnvelope ·       │
│   UiBlock · StreamEvent(seq) · SandboxDescriptor · WorkflowRun · errors      │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| `Autonogrammer.Contracts` | Single source of truth for every wire/domain type; versioned, `System.Text.Json` source-generated, no behaviour | netstandard-free .NET 10 class library, `[JsonPolymorphic]` discriminated unions for `StreamEvent`/`UiBlock` |
| `Autonogrammer.Parser` | Every input → exactly one `IntentEnvelope` via ordered recognizers ending in an unremovable fallback | Pure, allocation-light, no I/O; mirrors omp magic-keyword rules from `omp://magic-keywords.md` |
| `Autonogrammer.Harness.Acp` | ACP v1 client: JSON-RPC 2.0 framing, id correlation, client-side method handlers (`session/request_permission`, `fs/*`, `terminal/*`), `session/update` demux | `Process` + newline-delimited `PipeReader`; `ConcurrentDictionary<id, TaskCompletionSource>`; `Channel<T>` outbound |
| `Autonogrammer.Harness.Rpc` | omp native JSONL client: `ready` → `negotiate_protocol` v2 → `rpc_chunk` reassembly → command/response by `id` → event/side-channel demux (`prompt_result`, `session_settled`, host tools, host URIs, extension UI) | Same pipe skeleton; separate decoder (v2 chunk validator) |
| `Autonogrammer.Harness.Abstractions` | `IAgentHarness`, `IHarnessSession`, explicit state machine, typed failures, scripted in-memory adapter | Contract-test fixture that every adapter must pass |
| `Autonogrammer.Sessions` | Session aggregate, one-active-prompt rule, append-only sequence-numbered event log, `ISessionRepository` (in-memory), tool-approval policy hook | Single-writer per session (actor-style `Channel` loop) |
| `Autonogrammer.GenUi` | Projection: ACP `session/update` kinds and omp RPC events → `StreamEvent` → `UiBlock` (markdown, code, tool-call, diff placeholder, error, a2ui, unknown) | Deterministic reducer; A2UI envelopes passed through with catalog validation |
| `Autonogrammer.Sandbox` | `ISandboxProvider` (provision/attach/exec/stream logs/snapshot/teardown), idempotency keys, TTL, orphan reaper, composable layering | Fake, podman-compose, stubs for agentOS / OpenShell / enclave |
| `Autonogrammer.Workflows` | `ISessionRunWorkflow` + activities; Temporal worker host behind `Workflows:Enabled`; `DirectSessionRunner` otherwise | `Temporalio` 1.20.0 + `Temporalio.Extensions.Hosting` |
| `Autonogrammer.Api` | Minimal API: sessions, prompts, SSE stream, approvals, health, OpenAPI, CORS, rate limiter, correlation middleware | ASP.NET Core 10 `TypedResults.ServerSentEvents` |
| `Autonogrammer.Ui` (RCL) | All Razor components + `ISessionClient` abstraction, render-mode-neutral | Consumed by WASM head and MAUI head |
| Heads (`apps/`) | DI composition per form factor; MAUI desktop registers in-process harness + `ProcessOmpLauncher`; WASM/mobile register HTTP+SSE client | `maui-blazor-web`-style split |

## Recommended Project Structure

```
autonogrammer/
├── src/                                   # layer 1–3 libraries (no hosting)
│   ├── Autonogrammer.Contracts/                   # L1: types only; referenced by everything
│   ├── Autonogrammer.Parser/                      # L2: IntentEnvelope recognizers
│   ├── Autonogrammer.Harness.Abstractions/        # L2: IAgentHarness, state machine, failures, scripted adapter
│   ├── Autonogrammer.Harness.Acp/                 # L3: ACP v1 JSON-RPC client over stdio
│   ├── Autonogrammer.Harness.Rpc/                 # L3: omp JSONL RPC client (v1/v2 framing)
│   ├── Autonogrammer.GenUi/                       # L3: StreamEvent → UiBlock projection, A2UI envelope model
│   ├── Autonogrammer.Sessions/                    # L3: session service, event log, repository, approval hook
│   ├── Autonogrammer.Sandbox/                     # L3: ISandboxProvider + fake + podman-compose + vendor stubs
│   ├── Autonogrammer.Workflows/                   # L3: ISessionRunWorkflow, activities, DirectSessionRunner
│   └── Autonogrammer.Ui/                          # L3: Razor Class Library (shared UI + ISessionClient)
├── services/
│   ├── Autonogrammer.Api/                         # L4: ASP.NET Core host (Railway)
│   └── Autonogrammer.Worker/                      # L4: Temporal worker host (same image, different entrypoint)
├── apps/
│   ├── Autonogrammer.Web/                         # L5: Blazor WebAssembly standalone (Vercel static)
│   └── Autonogrammer.Maui/                        # L5: MAUI Blazor Hybrid head (built in macOS CI job)
├── tests/
│   ├── Autonogrammer.Contracts.Tests/ … one per project
│   ├── Autonogrammer.ContractTests/               # abstract fixtures: harness / sandbox / workflow / repository
│   ├── Autonogrammer.Architecture.Tests/          # dependency-direction assertions (L5→L4→L3→L2→L1)
│   └── Autonogrammer.Integration.Tests/           # SSE resume/dedup, scripted vertical slice
├── docs/                                  # architecture.md, decisions/ADRs, open-questions.md, runbooks/
├── deploy/                                # podman-compose.yml, Containerfile(s), railway.toml, vercel.json
└── Directory.Packages.props / global.json / .editorconfig
```

### Structure Rationale

- **`src/` numbered layers:** the architecture test can assert "apps → services → libraries → contracts" mechanically because every project sits in one named layer; `Autonogrammer.Contracts` has zero project references so WASM, MAUI and the API deserialize identical shapes.
- **Harness split into `Abstractions` / `Acp` / `Rpc`:** the two engines share a process/pipe skeleton but *not* framing, correlation or event vocabularies (JSON-RPC 2.0 vs. omp's custom JSONL; see §Pattern 1–2). Keeping them in separate assemblies lets the vertical slice ship with only `Abstractions` + the scripted adapter and keeps `omp` binary dependencies out of WASM.
- **`Autonogrammer.Ui` as an RCL with an `ISessionClient` seam:** this is the Microsoft `maui-blazor-web` pattern (shared RCL, per-head DI for device-specific services) — [learn.microsoft.com MAUI Blazor Web App tutorial](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app?view=aspnetcore-10.0) (HIGH).
- **`services/` vs `apps/`:** Vercel hosts only static output; everything long-lived runs on Railway (PROJECT.md constraint).

## Architectural Patterns

### Pattern 1: ACP v1 client as a JSON-RPC 2.0 multiplexer over stdio

**What:** One `Process` per `omp acp`; stdout is a newline-delimited stream of JSON-RPC 2.0 messages of three kinds — responses to our requests (matched by `id`), *agent→client requests* that we must answer (`session/request_permission`, `fs/read_text_file`, `fs/write_text_file`, `terminal/create|output|wait_for_exit|kill|release`), and notifications (`session/update`). Our side also sends notifications (`session/cancel`, `$/cancel_request`).

**Exact method/payload facts (all HIGH, from [agentclientprotocol.com/protocol/v1](https://agentclientprotocol.com/protocol/v1/overview)):**

| Direction | Method | Key params / result | Source |
|---|---|---|---|
| C→A request | `initialize` | `{ protocolVersion: 1, clientCapabilities: { fs: { readTextFile, writeTextFile }, terminal: bool, session?: { notices: {}, compaction: {} }, elicitation? }, clientInfo }` → `{ protocolVersion, agentCapabilities: { loadSession, promptCapabilities: { image, audio, embeddedContext }, mcpCapabilities, sessionCapabilities: { close?, resume?, delete?, additionalDirectories? } }, agentInfo, authMethods }` | [initialization](https://agentclientprotocol.com/protocol/v1/initialization) |
| C→A request | `authenticate` | `{ methodId }` (only if `authMethods` non-empty) | [schema#authenticate](https://agentclientprotocol.com/protocol/v1/schema) |
| C→A request | `session/new` | `{ cwd (absolute), mcpServers: [{ name, command, args, env }] }` → `{ sessionId }` | [session-setup](https://agentclientprotocol.com/protocol/v1/session-setup) |
| C→A request | `session/load` | `{ sessionId, cwd, mcpServers }`; agent **replays** history via `session/update` before responding; only if `agentCapabilities.loadSession` | same |
| C→A request | `session/resume` | same params, **no** replay; only if `sessionCapabilities.resume` present | same |
| C→A request | `session/close` | `{ sessionId }`; cancels work + frees resources; only if `sessionCapabilities.close` | [schema#session/close](https://agentclientprotocol.com/protocol/v1/schema) |
| C→A request | `session/prompt` | `{ sessionId, prompt: ContentBlock[] }` → `{ stopReason }` where `stopReason ∈ end_turn, max_tokens, max_turn_requests, refusal, cancelled` | [prompt-turn](https://agentclientprotocol.com/protocol/v1/prompt-turn) |
| C→A notification | `session/cancel` | `{ sessionId }`; client SHOULD pre-mark unfinished tool calls `cancelled`, **MUST** answer pending `session/request_permission` with `cancelled`; agent MUST finish with `stopReason: "cancelled"` | same |
| both, notification | `$/cancel_request` | `{ requestId }`; receiver MAY cancel, MUST still answer the original request (result or error `-32800`) | [schema — Protocol Level](https://agentclientprotocol.com/protocol/v1/schema) |
| A→C notification | `session/update` | `{ sessionId, update: { sessionUpdate: <kind>, … } }`; kinds: `user_message_chunk`, `agent_message_chunk`, `agent_thought_chunk`, `tool_call`, `tool_call_update`, `plan`, `available_commands_update`, `current_mode_update`, `config_option_update`, `session_info_update`, `usage_update`, `notice`, `compaction_update`, `compaction_summary_chunk` | [prompt-turn#session-updates](https://agentclientprotocol.com/protocol/v1/prompt-turn) |
| A→C request | `session/request_permission` | `{ sessionId, toolCall: ToolCallUpdate, options: [{ optionId, name, kind ∈ allow_once|allow_always|reject_once|reject_always }] }` → `{ outcome: { outcome: "selected", optionId } | { outcome: "cancelled" } }` | [tool-calls#requesting-permission](https://agentclientprotocol.com/protocol/v1/tool-calls) |
| A→C request | `fs/read_text_file` | `{ sessionId, path (absolute), line?, limit? }` → `{ content }` | [file-system](https://agentclientprotocol.com/protocol/v1/file-system) |
| A→C request | `fs/write_text_file` | `{ sessionId, path, content }` → `{}`; client MUST create file if missing | same |
| A→C request | `terminal/create` | `{ sessionId, command, args?, env?: [{name,value}], cwd?, outputByteLimit? }` → `{ terminalId }` (returns immediately) | [terminals](https://agentclientprotocol.com/protocol/v1/terminals) |
| A→C request | `terminal/output`, `terminal/wait_for_exit` (`→ { exitCode, signal }`), `terminal/kill`, `terminal/release` | all `{ sessionId, terminalId }`; after release the id is invalid; a terminal can be embedded in a tool call (`{ type: "terminal", terminalId }`) and the client SHOULD keep showing it after release | same |

Tool call shape: `tool_call` carries `toolCallId`, `title` (required), optional `name`, `kind ∈ read|edit|delete|move|search|execute|think|fetch|switch_mode|other`, `status ∈ pending|in_progress|completed|failed`, `content: ToolCallContent[]` (`{type:"content"}`, `{type:"diff", path, oldText?, newText}`, `{type:"terminal", terminalId}`), `locations`, `rawInput`, `rawOutput`; `tool_call_update` requires only `toolCallId` — every other field is a patch ([tool-calls](https://agentclientprotocol.com/protocol/v1/tool-calls), HIGH).

**omp-specific ACP facts (HIGH, `omp://approval-mode.md` §ACP sessions):** `omp acp` resolves approval via the normal settings precedence; ACP defines no per-session approval field, so per-session yolo means launching a separate `omp acp --approval-mode yolo` (or `--config` overlay) process. For `bash`, `allow` runs without a card, `deny` blocks without a card, `prompt` → `session/request_permission`; "Always allow" covers later ordinary bash calls in that session. Generic prompts use form elicitation when the client advertises `elicitation.form`; a rejected/cancelled/unsupported prompt rejects the tool call.

**When to use:** portable engine integration (any ACP agent, not just omp), and the path the mobile-free desktop head spawns in-process.

**Trade-offs:** ACP gives rich, typed, well-specified tool-call UI semantics but no steering/follow-up queue, no `get_state`, no host tools; those are omp-RPC-only.

**Example (C# skeleton — illustrative, not generated code):**
```csharp
// Outbound requests: id → completion; inbound frames demuxed by shape.
sealed class AcpConnection : IAsyncDisposable
{
    readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    readonly Channel<JsonRpcMessage> _outbound = Channel.CreateUnbounded<JsonRpcMessage>(new() { SingleReader = true });
    long _nextId;

    public async Task<TResult> RequestAsync<TResult>(string method, object @params, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        await _outbound.Writer.WriteAsync(JsonRpcMessage.Request(id, method, @params), ct);
        using var reg = ct.Register(() => _outbound.Writer.TryWrite(JsonRpcMessage.Notification("$/cancel_request", new { requestId = id })));
        return (await tcs.Task).Deserialize<TResult>(AcpJson.Options)!;   // -32800 → OperationCanceledException
    }

    // Reader loop: if frame has "method" && "id" → agent→client request → dispatch to IAcpClientHandlers, write response with same id
    //              if frame has "method" && no "id" → notification (session/update) → push to per-session Channel<SessionUpdate>
    //              if frame has "id" && ("result" || "error") → _pending[id].SetResult/SetException
}
```

### Pattern 2: omp native RPC (JSONL) client with v2 chunk reassembly and side-channel demux

**What:** `omp --mode rpc` is **not** JSON-RPC; it is one JSON object per line on stdin/stdout. All facts below are HIGH, from the bundled `omp://rpc.md` (omp 18.12.3).

- **Ready frame first:** `{ type:"ready", protocolVersion:1, supportedProtocolVersions:[1,2], maxFrameBytes:1048576, maxReassembledFrameBytes:67108864 }`. Physical stdout frames are capped at 1 MiB including newline; inbound frames are never chunked and SHOULD stay under the advertised limit.
- **Negotiate v2 immediately:** `{ id:"protocol-1", type:"negotiate_protocol", protocolVersion:2 }`. After success, oversized logical frames arrive as an uninterrupted sequence of `{ type:"rpc_chunk", chunkId, index, count, byteLength, data(base64 of UTF-8 JSON) }`; clients MUST validate `chunkId/index/count/byteLength`, reject interleaved/interrupted sequences, enforce the 64 MiB limit, concatenate in index order, decode strict UTF-8, parse one object. In v1, oversized responses become `success:false, error:"RPC response exceeded the transport limit"` and oversized events are elided; a >64 MiB v2 frame yields `rpc_frame_error` for events.
- **Correlation:** every command accepts optional `id`; responses echo it (`{ id?, type:"response", command, success, data? | error, code? }`). Malformed JSON → `command:"parse"` failure **without** id (recoverable; loop continues). Match by `id`, never by order.
- **Prompt completion is two-phase:** `prompt` is acknowledged once *admitted* (started, queued, or routed to an extension command) — not when the turn ends. Then exactly one of: success response with `data.agentInvoked:false` (finished locally), or a later `{ type:"prompt_result", id, agentInvoked:true, status ∈ completed|aborted|error, error?:{message,provider?,model?,httpStatus?,retryable}, sessionSettled }` — always written *after* the response for that id. A failed provider turn is still `success:true`; use `prompt_result.status`. Wait on `prompt_result` to present the answer; wait on `{ type:"session_settled" }` (or `get_state.isSettled`) before recycling a sandbox.
- **Command vocabulary (subset the harness needs):** prompting `prompt{message,images?,streamingBehavior?:"steer"|"followUp"}`, `steer`, `follow_up`, `remove_queued_message{message,queue}`, `promote_queued_message`, `abort`, `abort_and_prompt`, `abort_and_restore_queue`, `new_session{parentSession?}`, `open_session{sessionDir,provider?,modelId?}`; state `get_state`, `set_fast_mode`, `set_slow_mode`, `goal{op}`, `set_ask_dialog`, `get_available_commands`, `get_entries{since?}`, `get_tree`, `set_todos`, `set_host_tools`, `set_host_uri_schemes`, `set_subagent_subscription{level}`, `set_event_filter{events|null,messageUpdates?:"full"|"delta"}`; model `set_model{provider,modelId}`, `set_thinking_level{level}`; `compact`; `bash{command}`/`abort_bash`; messages `get_messages_page{cursor?,limit≤256}` (errors carry `code: session_busy | stale_cursor`); `fork` (fails `session_busy` while streaming).
- **Outbound frame categories to demux (15):** `ready`, `response`, `AgentSessionEvent` (`agent_start/agent_end`, `turn_start/turn_end`, `message_start/message_update/message_end` with `messageId`, `tool_execution_start/update/end`, `tool_stream_update`, `auto_compaction_*`, `auto_retry_*`, `model_changed`, `thinking_level_changed`, `notice`, `goal_updated`, `queue_update{steering,followUp}`, …), `extension_ui_request`, `host_tool_call`/`host_tool_cancel`, `host_uri_request`/`host_uri_cancel`, `extension_error`, `available_commands_update{commands}`, `prompt_result`, `session_settled`, subagent frames (`subagent_lifecycle/progress/event`), `btw_delta/btw_record`, `command_output`/`session_info_update`/`config_update`, `rpc_frame_error`, live-voice frames. `set_event_filter` only filters session events; everything else always flows.
- **Host tools (agent→host):** after `set_host_tools{tools:[{name,label,description,parameters(JSON Schema),hidden?,loadMode?,readsSkillUris?}]}` → `{toolNames}`, omp emits `{ type:"host_tool_call", id, toolCallId, toolName, arguments }`; host streams `{ type:"host_tool_update", id, partialResult:{content:[…]} }` and finishes with `{ type:"host_tool_result", id, result:{content:[{type:"text",text}]}, isError? }`; abort arrives as `{ type:"host_tool_cancel", id, targetId }`. Host URIs are symmetric: `set_host_uri_schemes{schemes:[{scheme,description,writable,immutable}]}` → `host_uri_request{id,operation:"read"|"write",url,content?}` → `host_uri_result{id,content?,contentType?,notes?,immutable?,isError?,error?}`; built-in schemes (`local://`, `skill://`, `artifact://`, `mcp://`, …) are reserved.
- **Extension UI sub-protocol:** `extension_ui_request{id, method ∈ select|confirm|input|editor|ask|cancel|notify|setStatus|setWidget|setTitle|set_editor_text|open_url, …}`; replies `extension_ui_response{id, value|confirmed|cancelled:true,timedOut?|answers:[{id,selectedOptions,customInput?}]}`. `ask` only after `set_ask_dialog{enabled:true}`. With `--mode rpc --no-ui`, tools needing approval **fail closed**; use `--mode rpc-ui --no-ui` to answer tool dialogs while keeping extensions headless.
- **Shutdown:** close stdin, keep reading stdout until EOF (pending output is delivered; a client that stops reading can delay exit indefinitely); exit 0 normally, 1 on latched persistence failure after a `notice{source:"session-persistence"}` frame.

**How a C# client should multiplex it:**

```csharp
// One reader loop; frames classified by `type`, then routed to three sinks.
switch (frame.Type)
{
    case "ready":               ready.TrySetResult(frame.To<ReadyFrame>()); break;
    case "rpc_chunk":           if (chunker.TryAppend(frame, out var logical)) Route(logical); break;
    case "response":            pending[frame.Id!].SetResult(frame); break;              // command sink
    case "prompt_result":       prompts[frame.Id!].Complete(frame.To<PromptResult>()); break;
    case "session_settled":     session.MarkSettled(); break;
    case "host_tool_call":      _ = hostTools.ExecuteAsync(frame.To<HostToolCall>(), ct); break;   // request sink (we answer)
    case "host_tool_cancel":    hostTools.Cancel(frame.TargetId!); break;
    case "host_uri_request":    _ = hostUris.ResolveAsync(frame.To<HostUriRequest>(), ct); break;
    case "extension_ui_request": _ = ui.AnswerAsync(frame.To<ExtensionUiRequest>(), ct); break;
    case "rpc_frame_error":     events.Write(StreamEvent.Degraded(frame)); break;
    default:                    events.Write(ProjectSessionEvent(frame)); break;            // event sink → StreamEvent
}
```
Rules the harness enforces: (1) only one `prompt` in flight per session (the session service rejects a second before `prompt_result`), (2) `abort` is the cancellation primitive — never close stdin to cancel, (3) retain streamed `message_end` messages because `agent_end.messages` may be compacted (`messageCount` added), (4) pin `set_event_filter` to known event names so unknown omp events never break the reducer, (5) prefer `messageUpdates:"delta"` to cut frame volume.

**When to use:** desktop in-process harness and backend harness when omp-native features (steering/follow-up queue, host tools, host URIs, model/thinking switches, `get_state`, forks) are wanted.

### Pattern 3: Session service = single-writer actor + append-only sequence-numbered log + SSE fan-out

**What:** Each session is owned by one in-process writer (a `Channel`-driven loop) that (a) enforces **one active prompt** (`PromptRejected(SessionBusy)` until the harness reports completion — ACP `session/prompt` response / omp `prompt_result`), (b) appends every harness event as `StreamEvent { SessionId, Seq (monotonic, gapless), Kind, Payload, CorrelationId, At }` to `ISessionEventLog`, (c) publishes to subscribers. The SSE endpoint replays from `Last-Event-ID + 1` then tails live events; `id:` on each SSE message is the `Seq`.

**Standards facts (HIGH):** ASP.NET Core 10 ships `TypedResults.ServerSentEvents(IAsyncEnumerable<SseItem<T>>)`; `SseItem<T>` carries event type, id and `ReconnectionInterval` ([Minimal API responses](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0#server-sent-events-sse)). Browsers' `EventSource` resend the last `id` as the `Last-Event-ID` request header on reconnect ([WHATWG HTML §server-sent events](https://html.spec.whatwg.org/multipage/server-sent-events.html)); Blazor WASM and MAUI use `HttpClient` streaming and must send the header themselves.

**Repository boundary:** `ISessionRepository` (sessions + event log) with an in-memory implementation; `SessionStatus ∈ Idle | Prompting | AwaitingApproval | Cancelling | Settled | Faulted` lives in Contracts. The approval hook `IToolApprovalPolicy.DecideAsync(ApprovalRequest) → Allow | Deny | AskUser` sits between the harness's `session/request_permission` (or omp `extension_ui_request`) and the UI; default policy is deny-by-policy, with `AskUser` producing an `approval_requested` `StreamEvent` and an `/approvals/{id}` endpoint feeding the answer back.

**Correlation IDs:** API middleware mints/propagates `X-Correlation-ID` → `Activity.Current` → every `StreamEvent.CorrelationId`, every harness log line, and (when enabled) Temporal workflow `Memo`/search attribute.

```csharp
// SSE endpoint sketch
app.MapGet("/sessions/{id}/events", (SessionId id, HttpContext http, ISessionEventLog log, CancellationToken ct) =>
{
    var from = long.TryParse(http.Request.Headers["Last-Event-ID"], out var last) ? last + 1 : 0;
    async IAsyncEnumerable<SseItem<StreamEvent>> Stream([EnumeratorCancellation] CancellationToken c)
    {
        await foreach (var e in log.ReadFromAsync(id, from, c))          // replay then live tail, gapless by Seq
            yield return new SseItem<StreamEvent>(e, e.Kind) { EventId = e.Seq.ToString(), ReconnectionInterval = TimeSpan.FromSeconds(2) };
    }
    return TypedResults.ServerSentEvents(Stream(ct));
});
```
Client side keeps `lastSeq` and drops any event with `Seq <= lastSeq` (duplicates are expected across reconnects).

**Trade-offs:** single-writer per session is simple and ordering-safe; it means a session is pinned to one API instance. That is fine at this scale (see Scaling); with multiple Railway replicas, route by session id or move the log to a shared store.

### Pattern 4: Generative UI projection — protocol events → `StreamEvent` → `UiBlock`, with A2UI as a pass-through block

**What:** The reducer in `Autonogrammer.GenUi` is the only place that understands both engines. It emits `StreamEvent`s (append-only, protocol-neutral) and the UI folds them into `UiBlock`s.

| Source (ACP `sessionUpdate`) | Source (omp RPC event) | `StreamEvent.Kind` | `UiBlock` effect |
|---|---|---|---|
| `agent_message_chunk{messageId?,content}` | `message_update{messageId, assistantMessageEvent: text_delta}` / `message_end` | `assistant_delta` / `assistant_end` | append to `MarkdownBlock(messageId)`; fenced code → `CodeBlock` |
| `agent_thought_chunk` | `message_update … thinking_delta` | `thought_delta` | collapsible `ThoughtBlock` (hidden by default) |
| `user_message_chunk` (replay on `session/load`) | `get_entries`/`get_messages_page` | `user_message` | `UserBlock` |
| `tool_call{toolCallId,title,kind,status,content,locations,rawInput}` | `tool_execution_start{toolCallId,toolName,args}` | `tool_call_started` | new `ToolCallBlock` (status pending/in_progress, icon by `kind`) |
| `tool_call_update{toolCallId, status?, content?, rawOutput?}` | `tool_execution_update` / `tool_stream_update` / `tool_execution_end` | `tool_call_updated` / `tool_call_finished` | patch block; `content[type=diff]` → `DiffBlock` placeholder; `type=terminal` → terminal pane |
| `plan{entries:[{content,priority,status}]}` | `todo_reminder` / `set_todos` state / `goal_updated` | `plan_updated` | `PlanBlock` (replace-whole) |
| `available_commands_update` | `available_commands_update{commands}` | `commands_updated` | composer slash-command palette (not a transcript block) |
| `current_mode_update` / `config_option_update` | `model_changed` / `thinking_level_changed` | `session_config_changed` | header chips |
| `usage_update{used,size,cost?}` | `get_state.contextUsage` | `usage_updated` | footer meter |
| `notice{severity,title,description?}` | `notice{level,message,source}` | `notice` | toast/banner, never in history |
| `compaction_update` / `compaction_summary_chunk` | `auto_compaction_start/end` | `compaction` | collapsed `CompactionBlock` |
| `session/request_permission` (request) | `extension_ui_request{method:ask|confirm|select}` | `approval_requested` | `ApprovalCard` with options; answer → policy hook |
| `session/prompt` result `{stopReason}` | `prompt_result{status}` + `agent_end` | `turn_ended{stopReason}` | re-enable composer; `cancelled` keeps partial output |
| — | `queue_update{steering,followUp}` | `queue_updated` | queued-message chips (omp only) |
| — | `rpc_frame_error` / unknown event | `degraded` / `unknown` | `UnknownBlock` fallback (never throw) |

**A2UI (HIGH, [A2UI v0.9.1 spec](https://a2ui.org/specification/v0.9.1-a2ui/)):** the agent emits a stream of JSON envelopes `{ "version":"v0.9.1", <createSurface|updateComponents|updateDataModel|deleteSurface>: {...} }` with MIME `application/a2ui+json`. `createSurface{surfaceId, catalogId (string id, conventionally URI, not resolvable), theme?, sendDataModel?}` must precede updates; `surfaceId`+`catalogId` are fixed for the surface's life. `updateComponents{surfaceId, components:[{id, component, …}]}` is a **flat adjacency list** (children referenced by id; placeholders allowed for not-yet-arrived children → progressive rendering). `updateDataModel{surfaceId, path?: JSON Pointer (default "/"), value?}` (omitted value = delete key). `deleteSurface{surfaceId}`. Client→server: `action{name, surfaceId, sourceComponentId, timestamp, context}`, plus `error`; capabilities are exchanged out-of-band via transport metadata (`a2uiClientCapabilities{supportedCatalogIds, inlineCatalogs}`). The basic catalog id is `https://a2ui.org/specification/v0_9_1/catalogs/basic/catalog.json`.

Projection rule: any `agent_message_chunk` / `tool_call_update.content` whose content is recognised as an A2UI envelope (or carries the MIME type via `_meta`) becomes `StreamEvent.Kind = a2ui_message` with the raw envelope; the UI maintains an `A2uiSurfaceStore` (surfaceId → components map + data model) and renders through a **catalog-bound Blazor renderer** (basic catalog first; unknown component → `UnknownBlock`). `action` messages are posted to the backend as a new prompt turn carrying the action JSON (A2UI is transport-agnostic; v1.0 adds `actionResponse`, out of scope).

**Trade-offs:** owning `StreamEvent` decouples both protocols from the UI and makes the scripted adapter trivial; the cost is a mapping table that must be kept in sync when omp adds events — mitigated by `set_event_filter` pinning and the `unknown` fallback.

### Pattern 5: Sandbox control plane as layered providers with idempotent provision/teardown and reapers

**What:** `ISandboxProvider { ProvisionAsync(SandboxSpec, IdempotencyKey) → SandboxDescriptor; AttachAsync; ExecAsync; StreamLogsAsync; SnapshotAsync; TeardownAsync(retry) }` with uniform timeouts, TTL, an orphan reaper (lists provider resources tagged with `autonogrammer.session=<id>` and tears down anything past TTL or without a live session), and a *composable* chain where an outer provider wraps an inner one.

| Layer | What is verified | Integration shape | Confidence |
|---|---|---|---|
| **podman-compose (local)** | Required by PROJECT.md; `deploy/podman-compose.yml` validated in CI | shell out to `podman`/`podman-compose`, label resources, `podman ps --filter label=…` for reaper | HIGH (constraint) |
| **Rivet agentOS** | `@rivet-dev/agentos-core` = library VM inside a trusted Node process (V8 isolates + Wasm, "not a microVM", no nested virt); hosted `@rivet-dev/agentos` controls a durable VM over RivetKit/HTTP (`POST /gateway/{vm_id}/action/v1.<group>.<action>`); API groups `vm, config, process, terminal, filesystem (mount/unmount/listMounts), javascript, python, network, software, cron`; mounts `host_dir`, `chunked_s3`, SQLite root; `permissions.network.default:"deny"` with allow rules ([README](https://github.com/rivet-dev/agentos), [architecture](https://rivet.dev/agentos/docs/architecture/)). `@rivet-dev/agentos-sandbox` 0.4.2 exists on npm depending on `dockerode` and `sandbox-agent` ([npm](https://www.npmjs.com/package/@rivet-dev/agentos-sandbox)) — this is the "external sandbox mount / ACP agents inside" bridge PROJECT.md refers to. | Because the runtime is a **Node library**, the .NET provider talks to it either via the hosted HTTP gateway (preferred for Railway) or via a small Node sidecar process the provider spawns; omp runs *inside* the VM as an ACP agent only if the agentOS image can host it — `[INFERENCE]`, unverified | MEDIUM (packages/API verified; ACP-in-VM path LOW) |
| **NVIDIA OpenShell** | v0.1.4 docs: gateway (control plane, sandbox lifecycle, policy, credentials, logs) → compute driver provisions **workload + separate supervisor** → supervisor confirms isolation boundary, starts agent, connects back; per-run gateway JWT + sandbox JWT ("generation"); supervisor disconnect freezes the agent. Podman driver: Podman 5.x, cgroups v2, `systemctl --user start podman.socket`, gateway TOML `[openshell] version = 2` + `[openshell.gateway] compute_driver = "podman"`, options under `[openshell.drivers.podman]` (`socket_path, network_name, sandbox_runtime_image, supervisor_image, image_pull_policy, stop_timeout_secs, grpc_endpoint`). Policy YAML `version: 1` with `filesystem_policy{include_workdir, read_only, read_write}`, `landlock`, `process{run_as_user, run_as_group}`, `network_policies` (live-reloadable, per-binary endpoint rules with `allow`/`deny_rules`), `network_middlewares`; unknown fields rejected; ≤4 MiB. Lifecycle: `sandbox create [--from image] [--detach] [--restart-policy never|on-failure|always] [--no-keep] -- <main process>`; exit 0 → `Completed`, nonzero → `Error/MainProcessFailed`; `sandbox connect` replays ≤1 MiB output, auto-reattaches ≤60 s. ([architecture](https://docs.nvidia.com/openshell/latest/about/architecture), [runtimes](https://docs.nvidia.com/openshell/latest/how-it-works/sandboxes/runtimes.md), [policy schema](https://docs.nvidia.com/openshell/latest/how-it-works/policies/schema.md), [sandboxes](https://docs.nvidia.com/openshell/latest/how-it-works/sandboxes/overview)) | Provider shells out to the `openshell` CLI (sandbox create/exec/logs/stop/delete; policy set) against a gateway configured for podman; `SandboxDescriptor` records sandbox name + generation; the reaper lists sandboxes by name prefix. Programmatic gateway API exists (gRPC `grpc_endpoint`) but its contract is not documented in the pages read — keep CLI-shaped | MEDIUM (CLI/config verified; gRPC surface LOW) |
| **Attested-enclave boundary (dstack-shaped)** | dstack ≥0.6 guest agent: internal Unix socket `/var/run/dstack.sock` (app-only) serving `dstack.guest.v1` at `/v1/{IssueCert,GetKey,Attest,AttestGpu,Info,Version}` (prpc: `POST` JSON or protobuf; `GET` query); external `Worker` service at `/prpc/v1/{Health,…}` never hands out keys; `/v1/Version` is the cheapest liveness probe; a pre-0.6 agent answers `/v1/...` with non-JSON 404 ([guest-api-v1.md](https://github.com/Dstack-TEE/dstack/blob/master/docs/guest-api-v1.md)). Apps are Docker-compose-shaped CVMs with RA reports binding image hash/args/env ([overview](https://docs.phala.com/dstack/overview)). PROJECT.md records an Oct 8–12 2026 guest-agent incident. | `IAttestationBoundary { GetQuoteAsync(nonce) ; VerifyAsync(report) }` over the v1 `Attest`/`Info` shapes, with a no-op implementation; **no provisioning code** — the enclave is where the inner provider's workload runs, not something Autonogrammer creates | MEDIUM (API verified; Autonogrammer integration LOW) |

Composition order (outer→inner): `EnclaveBoundary(OpenShell(podman)(agentOS(omp)))` as the target, but each layer is independently stubbable; the vertical slice uses `FakeSandboxProvider` only.

**Trade-offs:** CLI-shaped providers are easy to verify by contract tests and avoid fabricating vendor SDKs; they are slower and need the binary on the host image. The agentOS layer needs Node on the Railway image or the hosted gateway — a real cost that argues for making OpenShell(podman) the *first* real provider and agentOS second.

### Pattern 6: Temporal .NET worker topology — optional, boundary-only

**What (HIGH, Temporal .NET docs):** workflows are `[Workflow]` classes with one `[WorkflowRun]` async method; workflow code must be deterministic — no I/O, no `Task.Run`/thread pool, no `DateTime.Now`/`Random` (use `Workflow.UtcNow`, `Workflow.Random`, `Workflow.DelayAsync`), `.editorconfig` relaxations for `*.workflow.cs` ([workflow basics](https://docs.temporal.io/develop/dotnet/workflows/basics)). Activities are `[Activity]` methods, should be idempotent (one instance shared across invocations), payload ≤2 MB per arg / 4 MB per gRPC message, and must set `StartToCloseTimeout` or `ScheduleToCloseTimeout`; long activities call `ActivityExecutionContext.Current.Heartbeat(details)` with `HeartbeatTimeout` set, because **cancellation is only delivered on heartbeat** ([activity basics](https://docs.temporal.io/develop/dotnet/activities/basics), [timeouts](https://docs.temporal.io/develop/dotnet/activities/timeouts)). Workers: `new TemporalWorker(client, new TemporalWorkerOptions("task-queue"){…}.AddWorkflow<T>().AddAllActivities(instance))`, `ExecuteAsync(ct)`, `GracefulShutdownTimeout`; all workers on a task queue must register the same types ([run a worker](https://docs.temporal.io/develop/dotnet/workers/run-worker-process)). `Temporalio` 1.20.0 + `Temporalio.Extensions.Hosting` for `IHostedService` registration (version per PROJECT.md).

**Topology for Autonogrammer:**

```
[Workflow] SessionRunWorkflow(RunInput{SessionId, SandboxSpec, PromptRef, CorrelationId})
   ├─ activity ProvisionSandbox(spec, idempotencyKey)   StartToClose 10m, Heartbeat 30s
   ├─ activity RunPrompt(sessionId, promptRef)          StartToClose 2h, Heartbeat 15s  ← streams events into the session log; heartbeats carry lastSeq
   ├─ activity CollectArtifacts(sessionId)              StartToClose 10m
   └─ activity TeardownSandbox(descriptor)              StartToClose 10m, RetryPolicy max 5, in `finally` (CancellationType = WaitCancellationCompleted)
task queue: "autonogrammer-session-runs" (one queue; split "autonogrammer-sandbox" later only if sandbox ops need different worker images)
signals: Cancel  |  queries: Status  |  updates: none
```
**When a path must go through Temporal:** (1) anything that owns a sandbox lifetime (provision → run → teardown must survive API restarts and always tear down), (2) multi-step runs longer than an HTTP request/SSE connection where retries and timers matter, (3) scheduled/cron runs. **What must not:** the interactive chat turn on the desktop head (in-process harness, no server), approvals (human latency belongs in the session service; the workflow only waits on a signal if the run is durable), and the vertical slice (runs via `DirectSessionRunner`, same `ISessionRunWorkflow` interface, no server).

**Trade-offs:** Temporal buys crash-safe teardown and visibility; it costs a server, determinism discipline, and payload limits (never put transcripts in activity args — pass `SessionId`/`Seq` cursors).

### Pattern 7: Shared RCL + per-head DI (MAUI Blazor Hybrid, Blazor WASM)

**What (HIGH, [MAUI Blazor Web App tutorial](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app?view=aspnetcore-10.0)):** `dotnet new maui-blazor-web -I WebAssembly` yields MAUI head + Blazor Web App (+ `.Client`) + shared RCL. The RCL holds `Routes.razor` and all components; MAUI's `MainPage.xaml` points `BlazorWebView`'s `RootComponent ComponentType` at `shared:Components.Routes`. MAUI apps always run interactively and throw on explicit `@rendermode`; the tutorial's `InteractiveRenderSettings.ConfigureBlazorHybridRenderModes()` nulls render modes in the hybrid head. Device-specific behaviour goes behind interfaces declared in the RCL (`IFormFactor` example) with implementations registered per head: `AddSingleton<IFormFactor, FormFactor>()` in `MauiProgram`, `AddScoped<…>` in the web project, and a separate implementation in the `.Client` project when WASM is used.

**Autonogrammer composition:** Autonogrammer ships a *standalone* Blazor WASM app (static on Vercel) rather than a Blazor Web App server, so the RCL must contain zero server-only dependencies. RCL seams: `ISessionClient` (HTTP+SSE → backend | in-process → `SessionService`), `IApprovalPresenter`, `IPlatformInfo`, `IFileAccess` (desktop only: backs ACP `fs/*`), `ITerminalHost` (desktop only: backs `terminal/*`). Heads:

| Head | `ISessionClient` | Harness | Notes |
|---|---|---|---|
| Blazor WASM (Vercel) | `HttpSessionClient` (`HttpClient` + manual SSE parser with `Last-Event-ID`) | none (browser) | API base URL from `appsettings.{env}.json` at publish time |
| MAUI desktop (macOS/Windows) | `InProcessSessionClient` wrapping `SessionService` + `AcpHarness`/`RpcHarness` | spawns `omp acp` / `omp --mode rpc` via `Process` | `IFileAccess`/`ITerminalHost` real; mobile stubs absent |
| MAUI mobile (iOS/Android) | `HttpSessionClient` | none | reconnect with sequence resume; MAUI `Connectivity` drives backoff |

**Trade-offs:** one RCL guarantees identical UI, but anything in it must compile for WASM (no `System.Diagnostics.Process`, no file system) — enforce with the architecture test (RCL may reference only Contracts + GenUi).

## Data Flow

### Request Flow (web surface, backend harness)

```
[User types prompt in Composer (RCL)]
    ↓ POST /sessions/{id}/prompts { text, attachments }   (X-Correlation-ID minted by middleware)
[Autonogrammer.Api] → [Parser: IntentEnvelope] → [SessionService.AcceptPrompt]
    ↓ rejects with 409 SessionBusy if a prompt is active; else appends StreamEvent{prompt_accepted, Seq=n}
[DirectSessionRunner | Temporal SessionRunWorkflow]
    ↓ (optional) ISandboxProvider.ProvisionAsync(idempotencyKey = sessionId+promptId)
[IAgentHarness]  ACP: session/prompt{sessionId, prompt:[ContentBlock]}      RPC: {id, type:"prompt", message}
    ↓ stdout frames
[Harness reader loop] → typed events → [GenUi reducer] → StreamEvent{Seq=n+1…}
    ↓ append to ISessionEventLog (single writer) → publish
[SSE endpoint GET /sessions/{id}/events, id:=Seq] ──► [HttpSessionClient] ──► [UiBlock fold] ──► Razor render
    ↑ reconnect with Last-Event-ID → replay from Seq+1, client drops Seq ≤ lastSeq
[Agent needs approval]  ACP: session/request_permission → IToolApprovalPolicy → Allow/Deny immediately
                         or AskUser → StreamEvent{approval_requested} → UI ApprovalCard → POST /approvals/{id}
                         → harness answers { outcome: { outcome:"selected", optionId } }
[Turn ends]  ACP: session/prompt result {stopReason}   RPC: prompt_result{status} (+ session_settled)
    ↓ StreamEvent{turn_ended} → SessionStatus=Idle → composer re-enabled → (workflow) teardown activity
```

### Request Flow (desktop in-process)

Identical from `SessionService` down, but `InProcessSessionClient` subscribes directly to the event log (no HTTP/SSE), and the harness runs in the MAUI process, spawning `omp` with `cwd` = chosen workspace; `fs/*` and `terminal/*` are served by MAUI platform services.

### State Management

```
[ISessionEventLog (append-only, Seq)]
    ↓ subscribe(fromSeq)
[SessionViewModel in RCL] ←→ [UiBlock reducer (pure fold over StreamEvent)] → [Razor components]
    ↑ user actions → ISessionClient (prompt / cancel / approve / a2ui action) → backend or in-proc service
```
UI state is derived, never authoritative: reloading a session = replaying the log. Cancel = `session/cancel` (ACP) or `abort` (RPC); partial output stays because it is already in the log.

### Key Data Flows

1. **Resume after disconnect:** client holds `lastSeq`; on reconnect sends `Last-Event-ID: lastSeq`; server replays `(lastSeq, now]` from the log then tails; duplicates filtered by `Seq`. Works through Vercel/Railway edges without sticky sessions because the log, not the connection, is the source of truth.
2. **Cancellation chain:** UI → `DELETE /sessions/{id}/prompts/current` → SessionService sets `Cancelling` → harness sends `session/cancel` / `abort` → pending `session/request_permission` answered `cancelled` (ACP MUST) → harness waits for `stopReason:"cancelled"` / `prompt_result{status:"aborted"}` → `turn_ended`. Temporal: workflow `Cancel` signal → activity cancellation delivered on next heartbeat.
3. **Host tool round-trip (omp RPC only):** backend registers `set_host_tools` for Autonogrammer-owned tools (e.g., `autonogrammer_render_surface` producing A2UI); omp emits `host_tool_call` → `IHostToolRegistry` executes → `host_tool_update`/`host_tool_result`; cancellation via `host_tool_cancel{targetId}`.
4. **Sandbox lifetime:** `ProvisionAsync` with idempotency key (safe to retry) → descriptor persisted on the session → run → `TeardownAsync` with retry; reaper sweeps provider-tagged resources every N minutes for TTL-expired or ownerless sandboxes.

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| 0-1k users | Single API instance + in-memory/SQLite event log; `DirectSessionRunner`; one `omp` process per active session (watch RSS); SSE fan-out in-process. |
| 1k-100k users | Durable event log (Postgres on Railway: `session_events(session_id, seq, …)` with PK `(session_id, seq)`); session affinity by hashing session id to an API replica or move the single-writer to the Temporal worker; Temporal on for every sandboxed run; OpenShell gateway HA per its Kubernetes docs. |
| 100k+ users | Split harness workers from the API (API only serves SSE from the log); per-tenant sandbox pools; backpressure on `omp` output (it spools to disk under reader lag — read continuously). |

### Scaling Priorities

1. **First bottleneck:** one `omp` process per session — memory and provider API concurrency. Fix: session TTL + idle teardown driven by `session_settled`/`isSettled`, sandbox pooling.
2. **Second bottleneck:** SSE connection count per API instance and the in-memory log. Fix: externalise the log (Postgres) and make the SSE endpoint a pure reader so any replica can serve any session.

## Anti-Patterns

### Anti-Pattern 1: Matching omp RPC responses by order

**What people do:** assume the next `response` frame belongs to the last command sent.
**Why it's wrong:** `prompt` is acknowledged on admission while other commands keep flowing; `prompt_result` arrives much later; `parse` failures have no id. `rpc.md` is explicit: correlate by `id`.
**Do this instead:** every command gets a unique `id`; keep a pending map; treat `prompt` as two-phase (`response` then `prompt_result`).

### Anti-Pattern 2: Treating `agent_end.messages` as the transcript

**What people do:** discard streamed messages and read the final `agent_end`.
**Why it's wrong:** for oversized `agent_end`, omp strips messages already delivered in `message_end` frames and adds `messageCount`; in v1 fields may be elided.
**Do this instead:** append every `message_end`/`tool_execution_end` to the session log as it arrives; `agent_end` only signals yield.

### Anti-Pattern 3: Closing stdin (or killing the process) to cancel a turn

**What people do:** tear down the pipe when the user hits stop.
**Why it's wrong:** stdin close is *shutdown*; pending UI/host requests are rejected and the session is disposed. Partial output and the session are lost.
**Do this instead:** ACP `session/cancel`; omp `abort` (or `abort_and_prompt`); keep reading stdout; only close stdin to end the session, and keep draining until EOF.

### Anti-Pattern 4: Letting the UI be the source of truth for streaming state

**What people do:** build the transcript from live SSE deltas only.
**Why it's wrong:** reconnects duplicate or skip chunks; mobile backgrounding drops connections; no replay on reload.
**Do this instead:** sequence-numbered append-only log + `Last-Event-ID` replay + client-side `Seq` dedup (PROJECT.md decision).

### Anti-Pattern 5: Auto-answering `session/request_permission` with `allow_always` to "make it work"

**What people do:** grant everything to avoid building the approval UI.
**Why it's wrong:** ACP permission is the only client-side gate; omp's `bash` critical commands and destructive edits prompt *by design* even in yolo. Granting everything is the sandbox escape.
**Do this instead:** deny-by-policy default + explicit `AskUser` path; for unattended runs launch a dedicated `omp acp --approval-mode …` process inside the sandbox boundary.

### Anti-Pattern 6: Putting harness I/O or transcripts inside Temporal workflow code / activity payloads

**What people do:** stream omp output through workflow state or pass full transcripts to activities.
**Why it's wrong:** workflows must be deterministic (no I/O); payloads are capped at 2 MB/4 MB and bloat history.
**Do this instead:** activities do the I/O and write to the session log; workflow passes ids/cursors; heartbeats carry `lastSeq`.

### Anti-Pattern 7: Fabricating vendor SDK calls for agentOS / OpenShell / enclave

**What people do:** invent a `.NET` client for a gRPC surface that is not documented.
**Why it's wrong:** PROJECT.md forbids invented vendor facts; the documented surfaces are the `openshell` CLI + TOML/YAML, the agentOS hosted HTTP gateway/Node library, and the dstack prpc guest API.
**Do this instead:** CLI/HTTP-shaped providers behind `ISandboxProvider`, each with a contract test and an `docs/open-questions.md` entry.

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| `omp acp` | spawn per session, JSON-RPC 2.0 over stdio, `initialize` with `fs`/`terminal` caps reflecting the head (desktop: true; backend: false unless sandbox-backed) | approval mode via launch flags/`--config` overlay; `loadSession` capability gates `session/load` |
| `omp --mode rpc` | spawn per session, JSONL v1 → negotiate v2, `--no-ui` for headless backend, `rpc-ui --no-ui` when the host answers tool dialogs | set `set_event_filter` to the known set; `set_host_tools` for Autonogrammer tools |
| OpenShell gateway (podman driver) | `openshell` CLI from the provider; gateway TOML `[openshell] version=2`, `[openshell.gateway] compute_driver="podman"`; policy YAML v1 per sandbox | Podman 5.x + cgroups v2 + user socket on the host; sandbox JWT generations mean a restart invalidates attachments |
| Rivet agentOS | hosted HTTP gateway (`/gateway/{vm_id}/action/v1.*`) or Node sidecar with `@rivet-dev/agentos-core`; `@rivet-dev/agentos-sandbox` for external sandbox mounts | Node runtime required; ACP-agent-inside-VM path unverified (open question) |
| dstack (attested enclave) | `IAttestationBoundary` over `/v1/Attest`, `/v1/Info`, `/v1/Version` on `/var/run/dstack.sock` (inside CVM) and `/prpc/v1/Health` externally | Autonogrammer never provisions CVMs in this milestone; interface + no-op only |
| Temporal server | `Temporalio` client + worker on task queue `autonogrammer-session-runs`; behind `Workflows:Enabled` | local dev via `temporal server start-dev` in podman-compose profile |
| Vercel | static Blazor WASM publish; `vercel.json` headers (CSP, CORS none) + env-driven API base URL | no server functions |
| Railway | API + worker images built with `podman build`; health endpoints for readiness | config-as-code only this milestone |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| Heads ↔ RCL | DI interfaces (`ISessionClient`, `IApprovalPresenter`, `IPlatformInfo`, `IFileAccess`, `ITerminalHost`) | RCL may reference only Contracts + GenUi |
| RCL ↔ API | HTTP JSON + SSE (`id:`=Seq) | OpenAPI document is the contract; Contracts types shared, not duplicated |
| API ↔ Sessions | direct calls, single writer per session | 409 on second active prompt |
| Sessions ↔ Harness | `IAgentHarness` events (`Channel<HarnessEvent>`) + commands | explicit state machine; typed failures (`TransportClosed`, `ProtocolViolation`, `Timeout`, `AgentError`) |
| Harness ↔ GenUi | typed protocol events → `StreamEvent` | the only module aware of both ACP and omp RPC vocabularies |
| Sessions ↔ Workflows | `ISessionRunWorkflow` (Direct or Temporal) | workflow passes ids/cursors only |
| Workflows ↔ Sandbox | `ISandboxProvider` with idempotency keys | reaper runs as hosted service in API or worker |
| Everything ↔ Contracts | compile-time reference | architecture test forbids reverse references |

## Suggested Build Order

Dependencies drive the order; each step is independently testable without omp, Temporal or cloud credentials unless noted.

1. **Contracts + Parser** — `StreamEvent(Seq)`, `UiBlock`, `IntentEnvelope`, `SessionStatus`, error types; parser recognizers with the unremovable fallback. Everything else compiles against these.
2. **Harness.Abstractions + scripted adapter + contract-test fixture** — state machine, typed failures, deterministic in-memory adapter that emits a canned ACP-shaped event sequence (message chunks, tool_call/update, plan, permission request, stop reason).
3. **Sessions + GenUi** — single-writer session service, in-memory repository/event log, one-active-prompt rule, approval policy hook, reducer `StreamEvent → UiBlock` (markdown/code/tool-call/diff/error/unknown + `a2ui` pass-through with basic-catalog validation).
4. **API + SSE** — Minimal API, correlation middleware, `TypedResults.ServerSentEvents` with `id:`=Seq and `Last-Event-ID` replay, approvals endpoint, health/OpenAPI/CORS/rate limiter; integration test for resume/dedup.
5. **RCL + Blazor WASM head (vertical slice)** — conversation list, session view, composer, block renderer, A2UI surface renderer, streaming UX, accessibility baseline; `HttpSessionClient`; slice runs end-to-end on the scripted adapter.
6. **Harness.Acp (real `omp acp`)** — JSON-RPC framing, `initialize` caps, `session/new|load`, `session/prompt` + updates, `session/request_permission` → policy hook, `fs/*`/`terminal/*` handlers, `session/cancel`/`$/cancel_request`; verified against the installed omp.
7. **Harness.Rpc (real `omp --mode rpc`)** — ready/negotiate v2/`rpc_chunk` decoder, id correlation, two-phase prompt completion, `session_settled`, event filter, host tools/URIs, extension UI; shares step 2's contract tests.
8. **Sandbox providers** — `ISandboxProvider` + fake + podman-compose provider + reaper; interface-conforming stubs for OpenShell (CLI-shaped, podman driver config and policy YAML v1 checked in), agentOS, and the dstack-shaped attestation boundary; open-questions entries.
9. **Workflows** — `ISessionRunWorkflow`, `DirectSessionRunner`, Temporal workflow/activities with heartbeats and `finally` teardown, worker host behind `Workflows:Enabled`; `Temporalio.Testing` time-skipping tests.
10. **MAUI head** — RCL reuse, `InProcessSessionClient`, desktop `ProcessOmpLauncher` + `IFileAccess`/`ITerminalHost`, mobile backend-only; built in the macOS CI job.
11. **Platform hardening + docs** — security headers, outbound network policy, `.env.example`, runbooks, ADRs, final implemented/stubbed/deferred report.

Steps 1–5 form the required vertical slice (no omp/Temporal/cloud); 6–7 need the local `omp` binary; 8–10 need podman / Temporal dev server / macOS CI respectively.

## Open Questions (carry into `docs/open-questions.md`)

- **agentOS as an ACP host for omp:** `@rivet-dev/agentos-sandbox` 0.4.2 depends on `sandbox-agent`, but the README/docs read today do not document running an ACP agent *inside* a VM nor an agentOS-native ACP transport. LOW — needs a spike against the hosted gateway.
- **OpenShell programmatic API:** the gateway exposes `grpc_endpoint`; its proto/contract was not found in the pages read. Keep the provider CLI-shaped until verified. LOW.
- **ACP `session/load` support in `omp acp`:** PROJECT.md lists `session/load|list|close|delete` as implemented by omp; the ACP spec gates `session/load` on `agentCapabilities.loadSession` — the harness must read the capability at runtime rather than assume it. MEDIUM.
- **A2UI client→server `action` transport for Autonogrammer:** spec is transport-agnostic; the proposed "post action as a prompt turn" is `[INFERENCE]`, to be decided in the GenUi phase (v1.0 `actionResponse` may change this). MEDIUM.
- **Railway multi-replica SSE:** single-writer-per-session needs session affinity or an external log before scaling past one instance. Design accepted; implementation deferred. MEDIUM.

## Sources

- ACP v1: [overview](https://agentclientprotocol.com/protocol/v1/overview), [initialization](https://agentclientprotocol.com/protocol/v1/initialization), [session-setup](https://agentclientprotocol.com/protocol/v1/session-setup), [prompt-turn](https://agentclientprotocol.com/protocol/v1/prompt-turn), [tool-calls](https://agentclientprotocol.com/protocol/v1/tool-calls), [file-system](https://agentclientprotocol.com/protocol/v1/file-system), [terminals](https://agentclientprotocol.com/protocol/v1/terminals), [schema](https://agentclientprotocol.com/protocol/v1/schema) — HIGH (primary spec, read 2026-10-20)
- omp 18.12.3 bundled docs: `omp://rpc.md` (transport/framing, correlation, command schema, prompt payload, yield vs settled, event stream, extension UI, host tool, host URI, error model, compact flows), `omp://approval-mode.md` §ACP sessions, `omp://cli-reference.md` (`--mode rpc|acp|rpc-ui`, `--no-ui`) — HIGH (engine primary source)
- A2UI v0.9.1: [specification](https://a2ui.org/specification/v0.9.1-a2ui/), [a2ui.org versions table](https://a2ui.org/), [repo](https://github.com/a2ui-project/a2ui) — HIGH
- NVIDIA OpenShell v0.1.4: [architecture](https://docs.nvidia.com/openshell/latest/about/architecture), [sandbox runtimes (podman driver)](https://docs.nvidia.com/openshell/latest/how-it-works/sandboxes/runtimes.md), [policy schema](https://docs.nvidia.com/openshell/latest/how-it-works/policies/schema.md), [manage sandboxes](https://docs.nvidia.com/openshell/latest/how-it-works/sandboxes/overview), [README](https://github.com/NVIDIA/OpenShell) — MEDIUM/HIGH
- Rivet agentOS: [README](https://github.com/rivet-dev/agentos), [docs intro](https://rivet.dev/agentos/docs/), [architecture](https://rivet.dev/agentos/docs/architecture/), [`@rivet-dev/agentos-sandbox` on npm](https://www.npmjs.com/package/@rivet-dev/agentos-sandbox) — MEDIUM
- Phala dstack: [overview](https://docs.phala.com/dstack/overview), [guest API v1](https://github.com/Dstack-TEE/dstack/blob/master/docs/guest-api-v1.md), [repo](https://github.com/Dstack-TEE/dstack) — MEDIUM
- Temporal .NET: [workflow basics](https://docs.temporal.io/develop/dotnet/workflows/basics), [activity basics](https://docs.temporal.io/develop/dotnet/activities/basics), [activity execution](https://docs.temporal.io/develop/dotnet/activities/execution), [timeouts & heartbeats](https://docs.temporal.io/develop/dotnet/activities/timeouts), [run a worker](https://docs.temporal.io/develop/dotnet/workers/run-worker-process) — HIGH
- ASP.NET Core 10 SSE: [Minimal API responses — Server-Sent Events](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0); `Last-Event-ID`: [WHATWG HTML server-sent events](https://html.spec.whatwg.org/multipage/server-sent-events.html) — HIGH
- MAUI Blazor Hybrid + Web App + RCL: [learn.microsoft.com tutorial](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app?view=aspnetcore-10.0) — HIGH
- Project context: `/root/src/repos/autonogrammer/.planning/PROJECT.md` (verified facts, constraints, key decisions)

---
*Architecture research for: Autonogrammer — C#/.NET ACP harness + agent workspace*
*Researched: 2026-10-20 · Tracking: ultrathink n3 SPE-17751 (https://linear.app/swcstudio/issue/SPE-17751)*
