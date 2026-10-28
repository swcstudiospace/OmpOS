# Hooks

Hooks let your own scripts watch and steer the agent: block a shell command, rewrite a tool's input, format a file after an edit, add context after a tool ran, or ask the agent to keep going when it stops. OmpOS reads the same `hooks.json` files as Cursor, so a project hook written for Cursor runs unchanged.

The design decisions are in [ADR-0016](../decisions/ADR-0016-hooks-execution-model.md). This page is the reference.

> **Status of this release.** The hook library, the loopback bridge, the omp extension, the ACP tap, the hooks service and the Hooks page are built and unit tested. They have not been run against a live omp session, and the application heads do not yet construct the hooks service, so no hook fires in the product until that wiring lands (open question OQ-84). The "Status" column of the event table says what each event can do once it is wired and which part produces it: the omp extension ("extension") or the ACP tap in the host ("ACP"). [Who fires what](#who-fires-what) explains how the two share the work.

## Where hooks are read from

Every file that exists is loaded. All hooks of all files run, in the order below, one after the other. None replaces another.

| Order | Scope | File | Hooks run in |
| --- | --- | --- | --- |
| 1 | Managed | Linux `/etc/ompos/hooks.json`; macOS `/Library/Application Support/OmpOS/hooks.json`; Windows `%ProgramData%\OmpOS\hooks.json` | the directory of the file |
| 2 | Project | `<root>/.ompos/hooks.json`, then `<root>/.cursor/hooks.json` | the workspace root that holds the file |
| 3 | User | `~/.ompos/hooks.json`, then `~/.cursor/hooks.json` | the directory of the file |
| 4 | Claude local | `<root>/.claude/settings.local.json` | the workspace root |
| 5 | Claude project | `<root>/.claude/settings.json` | the workspace root |
| 6 | Claude user | `~/.claude/settings.json` | `~/.claude` |
| 7 | Plugins | hooks declared in a harness plugin manifest | the plugin directory |

- A multi-root workspace reads the project files of every root.
- The same hook (event, command, matcher, working directory) in `.ompos` and `.cursor` runs once.
- User scopes (3 and 6) are not read in remote and hosted sessions.
- Claude files are read for their `hooks` key only, and only when third-party discovery is on (the default). Discovery is not trust: consent still applies.
- There is no team scope. OmpOS never reads Cursor's enterprise paths.
- A managed hook is pre-consented only when its file and the directory that holds it are not writable by group or others. On Windows OmpOS does not read access control lists, so managed hooks there need consent like any other.
- Files are read when a session starts and when the loader is asked to reload. Invalid files become diagnostics on the Hooks page; they never stop the other files.

## The file

```json
{
  "version": 1,
  "hooks": {
    "beforeShellExecution": [
      { "command": ".cursor/hooks/approve-network.sh", "timeout": 30, "matcher": "curl|wget|nc ", "failClosed": true }
    ],
    "stop": [{ "command": "bun run .cursor/hooks/track-stop.ts --stop", "loop_limit": 10 }],
    "preToolUse": [
      { "type": "prompt", "prompt": "Only allow read-only operations. $ARGUMENTS", "timeout": 10 }
    ],
    "afterFileEdit": [
      { "args": ["prettier", "--write", "src/app.ts"], "id": "fmt", "env": { "NO_COLOR": "1" } }
    ]
  },
  "omp": {
    "tool_call": [{ "command": "./hooks/omp-guard.sh", "matcher": "^(bash|write)$" }]
  }
}
```

The file must be UTF-8 JSON of at most 64 KiB and 16 levels deep, without comments or trailing commas, and without a repeated key. `version` is required and must be `1`. Unknown top-level keys and unknown hook keys are kept out of the way with a warning. An event name OmpOS does not know is kept and listed as an unknown event; it never runs and it does not fail the file. At most 256 hooks per file.

### Keys of a hook

| Key | Type | Default | Meaning |
| --- | --- | --- | --- |
| `command` | string | required unless `args` or `type: "prompt"` | A shell string, a script path or a path relative to the hook's working directory. |
| `args` | string[] | none | OmpOS addition. An argument vector started directly, without a shell. Use it when you do not want the shell to interpret anything. `command` and `args` are mutually exclusive. |
| `type` | `"command"` or `"prompt"` | `"command"` | A prompt hook asks a model instead of running a process. |
| `prompt` | string | none | The prompt of a prompt hook. `$ARGUMENTS` is replaced by the input JSON; the input is appended when the placeholder is absent. |
| `model` | string | provider default | Model override of a prompt hook. |
| `timeout` | number (seconds) | 30 | Longer runs are killed. The largest accepted value is 600; a larger value is clamped with a warning. |
| `loop_limit` | integer or `null` | 5 for Cursor files, `null` (no cap) for Claude files | How many follow-ups a `stop` or `subagentStop` hook may trigger per user message. `null` removes the cap. Ignored with a warning on other events. |
| `failClosed` | boolean | `false` | When `true`, a crash, a timeout, a non-zero exit, no output or a start failure blocks the action instead of letting it through. It only matters on events that can block. |
| `matcher` | string | none | A .NET regular expression tested unanchored against the value of the call (see Matchers). An empty string or `*` matches everything. |
| `id` | string | `<source>:<event>:<index>` | A stable name for the Hooks page and the enabled state. Unique within a file. |
| `enabled` | boolean | `true` | `false` lists the hook and never runs it. |
| `description` | string | none | Free text. |
| `env` | object of strings | none | Extra environment variables for this hook only. Names starting with `OMPOS_` or `CURSOR_` and `CLAUDE_PROJECT_DIR` are ignored. |
| `runsIn` | `["local"]`, `["remote"]` or both | both | Where the hook may run. |

A `blocking` key is accepted and ignored: whether a hook guards an action follows from its event.

## What a hook receives and answers

The host starts the hook, writes **one JSON object** on its standard input and closes the pipe. The hook may write **one JSON object** on standard output.

- Standard input holds the common fields `conversation_id`, `generation_id`, `model` (and `model_id`, `model_params` when known), `hook_event_name`, `cursor_version` (for example `ompos-0.15.0`), `workspace_roots`, `user_email` (or `null`) and `transcript_path` (or `null`), then the event's own fields. `workspaceOpen` runs outside a conversation and omits the conversation fields. A string field longer than 1 MiB is cut and followed by `<name>_truncated: true`. An input over 8 MiB is not delivered and the hook counts as failed.
- Standard output is read up to 1 MiB. Longer output is drained and flagged as cut. A cut answer that still parses is used; a cut answer that no longer parses is a failure of the hook, not an invalid response.
- Standard error is kept up to 64 KiB for the log. On exit code 2 it becomes the message when standard output has none.
- A hook that does not read its input is fine: a broken pipe is the hook's own choice.

### Exit codes

| Exit code | Meaning |
| --- | --- |
| 0 | The hook succeeded; standard output is its answer. |
| 2 | Block the action, the same as `"permission": "deny"`. On `beforeSubmitPrompt` it blocks the prompt. On an event that cannot block it is only a logged failure. |
| anything else, a crash, a timeout | The hook failed. The action proceeds, unless the hook has `failClosed: true` and the event can block. |

### The answer

```json
{
  "permission": "allow",
  "user_message": "shown to you",
  "agent_message": "sent to the model when denied",
  "updated_input": { "command": "npm ci" },
  "additional_context": "added to the conversation",
  "followup_message": "sent as the next user message",
  "continue": true,
  "env": { "KEY": "value" },
  "updated_mcp_tool_output": {}
}
```

Each event honours only its own fields (see the table); the others are ignored. The Claude Code forms are also accepted: the nested `hookSpecificOutput` object (`permissionDecision`, `permissionDecisionReason`, `updatedInput`, `additionalContext`) and the legacy top-level `decision` / `reason`.

For a **permission event** an answer that is not valid JSON, or that does not match the event's schema, **blocks the action**. A hook that exits 0 with no output at all is not an invalid answer: it counts as a hook that said nothing, so the action proceeds (and a `failClosed` hook blocks).

## Permission events and failure

The six permission events are `preToolUse`, `subagentStart`, `beforeShellExecution`, `beforeMCPExecution`, `beforeReadFile` and `beforeTabFileRead`.

| Situation | Permission event | Other event |
| --- | --- | --- |
| Valid answer | used | used where honoured |
| Invalid JSON or schema mismatch | blocks | logged and ignored |
| Exit 0, no output | proceeds; blocks with `failClosed` | proceeds |
| Exit 2 | blocks | `beforeSubmitPrompt` blocks; others are a failure |
| Other exit code, crash, executable missing | proceeds; blocks with `failClosed` | ignored |
| Timeout | the process tree is killed; proceeds; blocks with `failClosed` | ignored |
| No consent for the hook | blocks, nothing is started | skipped |
| The hook's source is quarantined | blocks, nothing is started | skipped |

`ask` is honoured by `beforeShellExecution` and `beforeMCPExecution`: OmpOS asks you before the action runs. On `preToolUse` it counts as allow (Cursor does not enforce it either), on `subagentStart` as deny, and on `beforeReadFile` it is an invalid answer. An allow from a hook never bypasses omp's own approval or the ACP permission request: hooks can only tighten.

## Consent and quarantine

A repository can carry hook files, so **nothing runs until you consent to that exact hook**, on the Hooks page. Consent is recorded for the hook's identity: event, scope, command or arguments, shell, matcher, timeout, `failClosed`, `loop_limit`, `env`, `runsIn`, prompt text, its working directory relative to the scope root, and the content of the scripts the command names inside the project. Editing any of them, or editing a consented script, asks again. The absolute path of the checkout is not part of the identity, so the same repository gives the same identity on every machine. Enabling or disabling a hook does not.

A hook that guards an action (a permission event) and has no consent, or whose source is quarantined, **blocks the action** instead of being skipped. Other hooks are skipped. The reason is shown with the denial.

If OmpOS itself fails to start a hook (the shell is missing, the runner throws), the source of the hook is quarantined and stays so across restarts until you lift it with a reason. A hook that merely exits non-zero does not quarantine anything.

## The Hooks page

Settings has a Hooks page. It lists the effective hooks by scope in priority order and, inside a scope, by file. Each row shows the event, the matcher, the command and one state: not available (Tab events), unknown event, quarantined, disabled, needs your approval, running, last run failed, or ready, with the last run's outcome and duration.

- **Run hooks** switches every hook off at once. With it off the page lists nothing and no session starts a bridge or a gate.
- **Review** opens the consent card: the exact command (or the numbered arguments, or the prompt text), whether it runs through a shell, the matcher, the timeout, the names of the environment variables it adds, what happens if it fails, and what changed since the version you approved. **Approve this hook** records consent for exactly that; **Trust all hooks in this folder** does the same for every hook that needs it and later edits ask again.
- A folder you have not trusted is not read: its hook files are only named, with **Trust this folder**. Trusting the folder approves no hook.
- **Enabled** switches one hook on or off in OmpOS's own state. A Cursor or Claude file is never written.
- A quarantined source is listed with its reason; lifting it needs a reason of your own, which is recorded.
- Problems found in a hook file are listed with the file, the place in it and the reason.
- The run log under the list shows the newest runs first, filterable by outcome, with credentials removed from the output, and copies as JSON.

In a conversation a denial shows as `denied by hook <name>: <message>` under the tool call it denied, injected context shows as a collapsed **Hook context** row, and a continuation shows as `Hook continued the conversation (n of loop_limit)`.

## Matchers

The matcher is tested against one value per call:

| Event | Value |
| --- | --- |
| `preToolUse`, `postToolUse`, `postToolUseFailure` | the tool type: `Shell`, `Read`, `Write`, `Grep`, `Delete`, `Task`, `WebSearch`, `MCP:<tool_name>` |
| `subagentStart`, `subagentStop` | the subagent type, such as `generalPurpose`, `explore`, `shell` |
| `beforeShellExecution`, `afterShellExecution` | the full command string |
| `beforeMCPExecution`, `afterMCPExecution` | `tool_name` (an OmpOS choice; Cursor documents no value) |
| `beforeReadFile` | `Read` **Producer:** the extension. ACP fallback: `fs/read_text_file`, with the whole file as `content`; a deny answers the agent with an error and sends no content. |
| `afterFileEdit` | `Write` **Producer:** the extension. ACP fallback: `fs/write_text_file`; `edits` holds the part of the text that changed (common head and tail removed), and the write has already happened. |
| `beforeTabFileRead` / `afterTabFileEdit` | `TabRead` / `TabWrite` |
| `beforeSubmitPrompt` | `UserPromptSubmit` **Producer:** the ACP tap, always, right before `session/prompt` is written: a denial or `continue: false` sends nothing and ends the turn with a refusal. The extension adds the `input` event only for an interactive terminal. |
| `stop` | `Stop` **Producer:** the ACP tap when the extension is not connected, at the end of an ACP turn: a follow-up is sent as the next `session/prompt` inside the same event stream, bounded by `loop_limit`, and only a completed turn continues. With the extension, `session_stop` continues the same omp turn. |
| `afterAgentResponse` / `afterAgentThought` | `AgentResponse` / `AgentThought` |
| `sessionStart`, `sessionEnd`, `preCompact`, `workspaceOpen` | none: only an empty matcher or `*` matches |

The expression is a regular expression that is **not anchored**, so `curl|wget|nc ` matches any command that contains `curl`. Matching is case sensitive. An expression that does not compile never matches and is reported as a warning; a `failClosed` hook with such a matcher blocks. A match that takes more than 100 ms counts as no match. In `omp` section hooks the matcher is tested against `toolName` (for `tool_call` and `tool_result`) or `agent` (for `before_subagent_spawn`).

## Environment

A hook does not inherit your environment. It receives `PATH`, `HOME`, `USER`, `LOGNAME`, `SHELL`, `LANG`, `LANGUAGE`, `LC_*`, `TERM`, `TMPDIR`, `TEMP`, `TMP`, `TZ` and the Windows system variables, plus:

| Variable | Value |
| --- | --- |
| `CURSOR_PROJECT_DIR`, `CLAUDE_PROJECT_DIR`, `OMPOS_PROJECT_DIR` | the primary workspace root |
| `CURSOR_VERSION`, `OMPOS_VERSION` | the compatibility version string, for example `ompos-0.15.0` |
| `CURSOR_USER_EMAIL` | when signed in |
| `CURSOR_TRANSCRIPT_PATH` | when the host knows the transcript file |
| `CURSOR_CODE_REMOTE` | `true` in remote and hosted sessions |
| `CURSOR_PLUGIN_ROOT`, `OMPOS_PLUGIN_ROOT` | the plugin directory, for plugin hooks |
| `OMPOS_HOOK_EVENT`, `OMPOS_SESSION_ID` | the event name and the conversation id |

The environment a `sessionStart` hook returns in `env` is added to every later hook of the session. The `hooks.inheritEnvironment` setting (`InheritEnvironment` in `HookDispatcherOptions`) hands hooks the rest of the parent environment too, except `OMPOS_*` variables.

## Windows

On Windows a `command` runs through `powershell.exe -NoLogo -NoProfile -NonInteractive -Command`, and the JSON arrives on standard input, so a PowerShell script reads it from `$input`. This path is written to Cursor's description and has not been run on Windows.

## Events

Facts are from https://cursor.com/docs/hooks (read 2026-10-27). "Blocks" means a hook can stop or change the action. The **Status** column is what OmpOS can do once the wiring lands:

- **Built**: the dispatcher and the omp extension implement it as Cursor describes it.
- **Partial**: it works with the limit named in the cell.
- **Not available**: OmpOS cannot produce the event; the reason is given. Hooks for it are accepted in `hooks.json` and never run.

| Event | Fires | Fields in the input (after the common ones) | Honoured output | Blocks | Status |
| --- | --- | --- | --- | --- | --- |
| `sessionStart` | A conversation starts | `session_id`, `is_background_agent`, `composer_mode` | `env`, `additional_context` | no | Partial: from omp `session_start`. `env` reaches later hooks; omp has no hook to put `additional_context` into the first prompt, so the host must do that **Producer:** ACP when the extension is not connected (after `session/new` or `session/load`, before the first prompt); the extension otherwise. Either way the `additional_context` is put in front of the next prompt by the host. |
| `sessionEnd` | A conversation ends | `session_id`, `reason`, `duration_ms`, `is_background_agent`, `final_status`, `error_message` | none | no | Partial: from omp `session_shutdown`, reason always `user_close`, the extension waits at most 2 s **Producer:** ACP when the extension is not connected (the session closes or is disposed, reason `user_close`, at most 5 s); the extension otherwise. |
| `preToolUse` | Before any tool | `tool_name`, `tool_input`, `tool_use_id`, `cwd`, `agent_message` | `permission` (allow/deny), `user_message`, `agent_message`, `updated_input` | yes | Built: omp `tool_call`. `tool_name` is mapped to Cursor's names (`bash` to `Shell`, `read` to `Read`, `edit`/`write` to `Write`, `grep`/`find`/`glob` to `Grep`, `task` to `Task`, `mcp__*` to `MCP:<tool>`); `Delete` never occurs **Producer:** the extension. ACP fallback: only for a call omp asks permission for (`session/request_permission`); a deny rejects the request before the approval policy is asked, and `ask` leaves the decision to the ordinary approval prompt. A call omp never asks about (yolo mode, read-tier tools) is invisible to `preToolUse` without the extension. |
| `postToolUse` | After a tool succeeded | `tool_name`, `tool_input`, `tool_output`, `tool_use_id`, `cwd`, `duration` | `additional_context`, `updated_mcp_tool_output` | rewrites output of MCP tools | Built: omp `tool_result` **Producer:** the extension. ACP fallback: observe-only, from a `tool_call_update` that completed. |
| `postToolUseFailure` | After a tool failed | `tool_name`, `tool_input`, `tool_use_id`, `cwd`, `error_message`, `failure_type`, `duration`, `is_interrupt` | `additional_context` | no | Partial: `failure_type` is always `error`, `is_interrupt` always false **Producer:** the extension. ACP fallback: observe-only, from a `tool_call_update` that failed. |
| `subagentStart` | Before a subagent spawns | `subagent_type`, `task`, `subagent_model`, `tool_call_id`, `is_parallel_worker` | `permission` (ask counts as deny), `user_message` | yes | Partial: from omp `before_subagent_spawn`; `task` is empty, `subagent_id`, `parent_conversation_id` and `git_branch` are not sent **Producer:** the extension only. |
| `subagentStop` | After a subagent finished | `subagent_type`, `status`, `task`, `summary`, `duration_ms`, `loop_count`, `modified_files` | `followup_message` (only when `status` is `completed`, bounded by `loop_limit`) | follow-up only | Partial: from the `task` tool result; the follow-up is appended to that result as context for the parent agent, the subagent is not re-run; `message_count`, `tool_call_count`, `description` and `agent_transcript_path` are not sent **Producer:** the extension only. |
| `beforeShellExecution` | Before a shell command | `command`, `cwd`, `sandbox` | `permission` (allow/deny/ask), `user_message`, `agent_message` | yes | Built: omp `bash` tool, `sandbox` is always false **Producer:** the extension. ACP fallback: a permission request of kind `execute`. |
| `afterShellExecution` | After a shell command | `command`, `output`, `duration`, `sandbox` | none | no | Built **Producer:** the extension. ACP fallback: observe-only, from the completed update of an `execute` call. |
| `beforeMCPExecution` | Before an MCP tool | `tool_name`, `tool_input` (JSON string), `mcp_server_name` | `permission` (allow/deny/ask), `user_message`, `agent_message` | yes | Partial: server and tool names are parsed from omp's `mcp__<server>_<tool>`; wrong for a server name that contains `_`; `url` and `command` are not sent **Producer:** the extension only. |
| `afterMCPExecution` | After an MCP tool | `tool_name`, `tool_input`, `mcp_server_name`, `result_json`, `duration` | none | no | Partial: same name limit **Producer:** the extension only. |
| `beforeReadFile` | Before the agent reads a file | `file_path`, `content`, `attachments` | `permission` (allow/deny), `user_message` | yes | Partial: from omp `read`; `content` is the first 256 KiB when the file is a readable regular file, otherwise absent; prompt attachments are not reported |
| `afterFileEdit` | After the agent edited a file | `file_path`, `edits` | none | no | Partial: `edits` is best effort from the tool input and may be empty |
| `beforeSubmitPrompt` | After you send a prompt, before the agent gets it | `prompt`, `attachments` | `continue`, `user_message` | yes | Partial: from omp `input`, which omp fires only for its own interactive terminal; the ACP prompt path needs the host to run the hook before `session/prompt` (a later unit); `attachments` is always empty from the extension |
| `preCompact` | Before the context is compacted | `trigger`, `context_usage_percent`, `context_tokens`, `context_window_size`, `message_count`, `messages_to_compact`, `is_first_compaction` | `user_message` | no (observe only, as in Cursor) | Partial: from omp `session_before_compact` and `auto_compaction_start`; only the numbers omp exposes are sent, `trigger` is inferred **Producer:** the extension only (ACP has no signal for it). |
| `stop` | The agent loop ends | `status`, `loop_count` | `followup_message` (bounded by `loop_limit`) | follow-up only | Partial: from omp `session_stop`, which continues the same turn with the follow-up as context; `status` is always `completed` from the extension. The host can also run it when an ACP turn ends (a later unit) |
| `afterAgentResponse` | After an assistant message | `text` | none | no | Built: omp `message_end` **Producer:** the extension. ACP fallback: the message chunks of a turn, aggregated until the message ends. |
| `afterAgentThought` | After a thinking block | `text`, `duration_ms` | none | no | Partial: from omp `message_end` thinking blocks; `duration_ms` is not available **Producer:** the extension. ACP fallback: the thinking chunks, aggregated, with the time from the first to the last chunk as `duration_ms`. |
| `beforeTabFileRead` | Before Tab reads a file | `file_path`, `content` | `permission` (allow/deny) | yes | **Not available**: OmpOS has no Tab inline-completion engine |
| `afterTabFileEdit` | After Tab edited a file | `file_path`, `edits` with ranges | none | no | **Not available**: same reason |
| `workspaceOpen` | A workspace opened or its folders changed | none (no conversation fields) | `pluginPaths` | no | Not wired yet: the dispatcher supports it; no head fires it and nothing loads the returned plugin directories The hooks service runs it for a head that asks (`IHooksSession.DispatchAsync`); no head does yet. |

Cursor's cloud agents run 14 of the 21 events. OmpOS runs hooks in hosted runs from the checkout and the managed file only, never user hooks; which events a hosted run produces is decided with that wiring.

## Who fires what

Two parts of OmpOS produce hook events, and they never both produce the same event for a session:

- The **omp extension** (`plugins/ompos-hooks-omp`) runs inside omp. It sees every tool call before omp's own approval gate, so it is the only part that can block a call omp would run without asking. The host connects it by starting a loopback bridge per session, with a random token, and passing `-e plugins/ompos-hooks-omp` and three `OMPOS_HOOKS_*` variables to omp.
- The **ACP tap** runs in the host and sees the Agent Client Protocol traffic of the session: the prompt, permission requests, `fs/*` calls, `session/update` and the end of each turn.

When the extension is connected, the tap fires only what the extension cannot see: `beforeSubmitPrompt`, and the context that a `sessionStart` hook added. When it is not connected (the plugin folder is missing, or the bridge could not start) the tap is the fallback for every event it can produce, and the Hooks page and the plan's planning event say so. The tap cannot block what ACP never asks about, so the fallback is weaker, not equivalent.

A hook can only tighten. The approval policy and omp's own approval modes still decide what is allowed; an allow from a hook never bypasses them.

## Loop limits

A `stop` or `subagentStop` hook that returns a non-empty `followup_message` makes the host continue the work with that text. The follow-up is subject to `loop_limit` (default 5 for Cursor files): the hook receives `loop_count`, the number of follow-ups it has already triggered since your last message; at the limit the follow-up is dropped and the log says so. A new message from you resets the counter, and so does a run in which the hook returns no follow-up. A `subagentStop` follow-up is only used when the subagent completed.

## Prompt hooks

A prompt hook sends its text to a model and expects `{ "ok": true }` or `{ "ok": false, "reason": "..." }`. `ok: false` denies with the reason; a model error is a crash of the hook (it fails open unless `failClosed`). While the model call runs, hooks are disabled for that call, so a prompt hook cannot trigger itself. Prompt hooks need a model evaluator from the host and are local by default.

## The `omp` section

`omp` maps omp extension event names to hooks. The hook receives the omp event object plus the common fields and answers with the omp result object, which OmpOS merges key by key (`block`, `cancel`, `handled` and `continue` stay true once any hook sets them) and hands back to omp. Accepted events: `tool_call`, `tool_result`, `input`, `session_stop`, `before_subagent_spawn`, `session_before_compact`, `auto_compaction_start`, `session_start`, `session_shutdown`, `agent_end`, `turn_end`, `message_end`, `tool_approval_requested`, `tool_approval_resolved`. Hooks on `tool_call`, `before_subagent_spawn` and `input` guard an action, so without consent they block it.

## Claude Code settings

From a Claude `settings.json` OmpOS imports `PreToolUse`, `PostToolUse`, `UserPromptSubmit` (as `beforeSubmitPrompt`), `Stop` and `SubagentStop`, command hooks only. Tool names in matchers are translated: `Bash` becomes `Shell` and `Edit` becomes `Write`. `loop_limit` is `null` for these hooks. Other Claude events (for example `Notification`) are reported as not imported.

## The run log

Every hook that is considered leaves a record in `hooks/runs.jsonl` of the session state directory: time, conversation, generation, event, hook id, source, status (`Succeeded`, `Denied`, `Failed`, `TimedOut`, `Skipped`, `Faulted`, `AwaitingConsent`, `PluginQuarantined`, `InvalidResponse`), decision, exit code, duration, your `user_message`, at most 4 KiB of standard output and standard error, and the SHA-256 of the input. The input itself is never stored, and the shell events do not record their matcher value because it is the whole command. The log is two files of at most 5 MiB together; the older one is replaced when the newer fills.

## Where OmpOS differs from Cursor

| Topic | Cursor | OmpOS |
| --- | --- | --- |
| Trust | Project hooks run in a trusted workspace | Nothing runs without consent for the exact hook; a guarding hook without consent blocks; script content is part of the consent |
| Environment | not stated | allow-list, not the parent environment |
| Execution | not stated | sequential, in priority then file order; all matching hooks run even after a deny |
| Default timeout | not stated | 30 s, at most 600 s |
| Exit 2 message | not stated | the answer's message, else standard error |
| No output with exit 0 | listed as a failure for `failClosed` | a hook that said nothing: proceeds, `failClosed` blocks |
| Matcher | a regular expression | a .NET regular expression, unanchored, case sensitive, 100 ms limit |
| Merge | "last wins" on the hooks page, "higher priority wins" on the third-party page | last wins, as the hooks page says |
| Team and enterprise | Cursor dashboard and MDM paths | OmpOS managed file only; no team scope |
| Blocking | by the Cursor agent | by an OmpOS omp extension; a hook can only tighten omp's own approval policy |
| `args` form, `id`, `enabled`, `env`, `runsIn`, `omp` section | none | OmpOS additions |
| Tab events | Cursor Tab | accepted, never fire |

## Troubleshooting

- *A hook does not run.* Check the Hooks page: no consent, quarantined source, `enabled: false`, a matcher that does not match or does not compile, `runsIn`, or an unknown event name. Warnings name the file and the JSON path.
- *Every tool call is blocked.* A guarding hook has no consent, or its source is quarantined; the denial message says which.
- *A hook blocks with an invalid response.* Print exactly one JSON object and nothing else on standard output; send logs to standard error.
- *A hook is slow.* Each hook is a process start plus a loopback call, in sequence. Put a `matcher` on it.
