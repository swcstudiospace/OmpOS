# ompos-hooks-omp

omp extension that forwards omp events to the OmpOS hook bridge (Cursor-compatible hooks) and maps the
bridge's decisions back to omp results. Plain ESM, no dependencies, Node >=24 or Bun.

The wire contract is the one implemented by `HookBridgeServer` in `src/OmpOS.Harness.AgentLoop/Hooks/`.

## Install / use

The host (OmpOS) starts omp with the extension and the environment below:

```sh
omp -e /path/to/plugins/ompos-hooks-omp/src/index.mjs
```

Or install the package directory as a plugin (`omp.extensions` and `pi.extensions` both point at `./src/index.mjs`).

## Environment

| Variable | Meaning |
| --- | --- |
| `OMPOS_HOOKS_URL` | `http://127.0.0.1:<port>/v1/hook`. Only `127.0.0.1`, `localhost`, `[::1]` over `http:` are accepted; any other URL makes the extension inert (nothing is sent). |
| `OMPOS_HOOKS_TOKEN` | Bearer token sent as `Authorization: Bearer ...`. Never logged, never in errors. |
| `OMPOS_HOOKS_STARTUP_FILE` | JSON file `{version, failClosedEvents, activeEvents, activeOmpEvents, requestTimeoutMs?}`, read lazily on the first event and cached. |

With `OMPOS_HOOKS_URL` or `OMPOS_HOOKS_TOKEN` unset the extension is inert: handlers return immediately, no file or network access.
Only the top-level session posts (`ctx.agent.kind === "sub"` is skipped). Registering handlers touches neither fs nor network.

## Events

Subscribed: `tool_call`, `tool_result`, `input`, `session_start`, `session_shutdown`, `session_stop`,
`before_subagent_spawn`, `session_before_compact`, `message_end`, `auto_compaction_start`, `agent_end`, `turn_end`,
`tool_approval_requested`, `tool_approval_resolved`. Cursor payloads and omp-result mapping follow the contract
(`tool_call` posts `preToolUse`, then `beforeShellExecution` / `beforeMCPExecution` / `beforeReadFile`; the first deny
short-circuits; `updated_input` is applied, last wins). Tool name map: bash->`Shell`, read->`Read`, edit/write/ast_edit->`Write`,
grep/ast_grep/find/glob->`Grep`, task->`Task`, web_search->`WebSearch`, `mcp__<server>_<tool>`->`MCP:<tool>`.

Cursor events are skipped when absent from `activeEvents` (a missing/unreadable/malformed startup file means all Cursor
events are active). Events of the `kind: "omp"` section are posted only when listed in `activeOmpEvents`
(deviation: a missing startup file means no omp-section posts, to avoid posting every omp event to a host that has no omp hooks).

## Fail policy

Timeouts: POSTs whose response is awaited and applied (`preToolUse`, `beforeShellExecution`, `beforeMCPExecution`,
`beforeReadFile`, `subagentStart`, `beforeSubmitPrompt`, `stop`, `subagentStop`, `postToolUse`, `preCompact` via
`session_before_compact`, and `kind:"omp"` posts for `tool_call`, `tool_result`, `input`, `session_stop`,
`before_subagent_spawn`, `session_before_compact`) use `requestTimeoutMs` from the startup file (default 130000 when the
file is missing/unreadable/invalid or the field is not an integer; clamped to 1000..900000). Fire-and-forget observers
(`sessionStart`, `afterShellExecution`, `afterMCPExecution`, `afterFileEdit`, `afterAgentResponse`, `afterAgentThought`,
`postToolUseFailure`, the `preCompact` of `auto_compaction_start`, omp observers) keep 5 s, and `session_shutdown`
(`sessionEnd` and its omp post, awaited) 2 s.
Unreachable, timeout, non-200 and invalid JSON all count as failed. A failed blockable event (`preToolUse`,
`beforeShellExecution`, `beforeMCPExecution`, `beforeReadFile`, `subagentStart`, `beforeSubmitPrompt`) listed in
`failClosedEvents` denies (`OmpOS hook host unreachable; <event> is configured fail-closed`; for `input` -> `{handled:true}`);
everything else continues as allowed. The extension never throws into omp and never logs payloads or the token.

## Tests

`npm test` (or `node --test plugins/ompos-hooks-omp/test/*.test.mjs` from the repo root). Injected fetch/clock/env/readFile;
one loopback `node:http` end-to-end file.

## UNVERIFIED

- MCP server/tool names for `tool_call`: `ToolInfo.sourceInfo` carries only `source: "mcp"`, not the server name, so
  the name is parsed from `mcp__<server>_<tool>` (first `_` after the prefix; wrong for server names containing `_`). For
  `tool_result` the extension prefers `details.serverName` / `details.mcpToolName` (set by `src/mcp/tool-bridge.ts`).
  UNVERIFIED: `omp` with an MCP server whose name contains `_`, then inspect the `tool_call` `mcp_server_name`.
- `preCompact.trigger` for `session_before_compact`: the event has no trigger field; the extension reports `auto` when an
  `auto_compaction_start` fired just before, else `manual`. UNVERIFIED: ordering of `auto_compaction_start` vs `session_before_compact` in `src/session/session-maintenance.ts` at runtime.
- File path field names of the `read`/`edit`/`write` tool inputs (`path`, also `file_path`, `filePath`) and edit text fields
  (`old_string`/`new_string`, `oldText`/`newText`, `content`). UNVERIFIED: `grep -n "ReadToolInput\|EditToolInput" src/extensibility/extensions/types.ts` per omp version.
- Thinking block field name `thinking` in assistant messages (`message_end`). UNVERIFIED: inspect a real `message_end` event.
- No live omp session was run.
