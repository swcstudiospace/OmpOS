# ADR-0016: Hooks execution model

- Status: Accepted 2026-10-27 with the default recorded in [OQ-96](../open-questions.md). It amends decision 4 of [ADR-0013](ADR-0013-plugin-isolation.md). The product owner can still reverse the amendment; the code keeps the no-shell `args` form so a reversal is a configuration change, not a rewrite.
- Date: 2026-10-27
- Owner: autonogrammer-maintainers
- Deciders: autonogrammer-maintainers
- Scope: how Autonogrammer runs Cursor-format hooks (`hooks.json`): which files it reads, how a hook process is started, what it receives and may answer, which omp mechanism enforces a blocking event, what fails open and what fails closed, and where the dispatcher runs. Covers `src/Autonogrammer.Harness.Plugins/Hooks`, `src/Autonogrammer.Harness.AgentLoop/Hooks` and `plugins/autonogrammer-hooks-omp`. The research behind it is `docs/plans/v0.15.0-research/08-hooks-design.md`; the user-facing reference is [docs/reference/hooks.md](../reference/hooks.md). It does not change the consent, quarantine and tier rules of ADR-0013 other than decision 4.

## Context

Facts read on 2026-10-27:

- Cursor's hooks (https://cursor.com/docs/hooks) are commands: a shell string, a script path or a relative path. The host writes one JSON object on the command's standard input and reads one JSON object from its standard output. Exit code 2 blocks, any other non-zero exit fails open unless the hook sets `failClosed`. Cursor lists 21 events and merges the answers of every hook of every source.
- ADR-0013 decision 4 says a hook runs without a shell, with closed standard input and a bounded output. `SystemHookProcessRunner` closes standard input at once and inherits the whole environment (ADR-0013 risk 1). A Cursor hook cannot run under those rules, because it cannot read its input.
- ACP alone cannot enforce a hook. omp announces a tool call to the client after it dispatched it, asks `session/request_permission` only when its approval policy says prompt, and under `yolo` or for read-tier tools asks nothing. An omp extension cannot call the Autonogrammer client over ACP, because ACP has no agent-to-client custom method (research section B).
- omp does expose the points that matter to an extension loaded with `-e`: `tool_call` (result `block`, `reason`, `input`), `tool_result`, `input` (result `handled`), `session_stop` (result `continue`, `additionalContext`), `before_subagent_spawn` (result `block`, `reason`), `session_before_compact` (result `cancel`), `session_start`, `session_shutdown` and `message_end` (`.../@oh-my-pi/pi-coding-agent/src/extensibility/extensions/types.ts` and `shared-events.ts`, omp 18.12.x). Autonogrammer already launches omp with `--no-extensions` and one `-e` per Autonogrammer-owned extension (ADR-0012 D7, `plugins/gsd-core-omp`).
- No head constructs the hook host today (OQ-84). The units of this record are libraries and an extension; wiring them into a head and into the omp launch plan is separate work.

## Candidate chosen

Run Cursor hooks as Cursor defines them, inside a smaller blast radius than Cursor gives them, and enforce blocking events through an Autonogrammer-owned omp extension that asks the host over a per-session loopback endpoint.

1. **Format.** `hooks.json` with `version: 1` and the Cursor per-hook keys (`command`, `type`, `prompt`, `model`, `timeout`, `loop_limit`, `failClosed`, `matcher`), read from managed, project (`.autonogrammer` then `.cursor`), user (`.autonogrammer` then `.cursor`) and Claude Code settings files (`.claude/settings.local.json`, `.claude/settings.json`, `~/.claude/settings.json`), plus the hooks of harness plugin manifests. Autonogrammer adds `args`, `id`, `enabled`, `description`, `env`, `runsIn` and an `omp` section. All sources run, in priority order, one after the other; none replaces another.
2. **Process.** A `command` is a shell string run by `/bin/sh -c` (Linux, macOS) or PowerShell (Windows). `args` is an argument vector run without a shell. The event JSON is written to standard input on its own task and the pipe is then closed. The child gets a scrubbed environment (an allow-list of the parent's variables, the variables Cursor documents, the `sessionStart` environment and the hook's own `env`), never the host's full environment. Standard output and standard error are drained to the end and kept up to a cap; a cut is reported as truncation and never turns a valid answer into a deny. The whole process tree is killed on timeout or cancellation.
3. **Consent and quarantine are unchanged in rule and order**: enabled, location, matcher, quarantine, consent, all before any spawn. A hook that guards an action (a permission event) and cannot run for want of consent or because its source is quarantined denies the action. Consent identity version 2 hashes the hook without any absolute path, so the same repository has the same identity on a laptop and in a sandbox, and it hashes the content of the scripts the command names inside the project, so editing a consented script voids the consent. Version 1 identity stays in force for plugin manifest hooks and stays readable.
4. **Enforcement.** The extension `autonogrammer-hooks-omp` registers omp handlers and POSTs each event to `HookBridgeServer`, a loopback HTTP endpoint with an ephemeral port and a random bearer token, started for the session. The host runs the dispatcher and answers with the merged decision; the extension maps it to omp's `{ block, reason }`, `{ input }`, `{ handled }` and `{ continue, additionalContext }`. The extension carries no policy and runs no hook command, so consent stays in the host.
5. **Failure policy** as the table in the decision section.

## Licence

This record adds no package. Everything it names:

| Component | SPDX | Source | Verified |
| --- | --- | --- | --- |
| Cursor hooks format | NOASSERTION (a documented file format of a proprietary product; Autonogrammer copies no Cursor code or text, it reads the format description) | https://cursor.com/docs/hooks | Page read 2026-10-27 (`docs/plans/v0.15.0-research/08-hooks-design.md`, source S1) |
| omp (`@oh-my-pi/pi-coding-agent`, the host of the extension) | MIT | https://github.com/can1357/oh-my-pi/blob/main/LICENSE | Same record as ADR-0013, 2026-10-26 |
| `plugins/autonogrammer-hooks-omp` | AGPL-3.0-or-later | https://www.gnu.org/licenses/agpl-3.0.html | Autonogrammer-owned code; `package.json` `license` field |
| `Autonogrammer.Harness.Plugins`, `Autonogrammer.Harness.AgentLoop` | AGPL-3.0-or-later | https://www.gnu.org/licenses/agpl-3.0.html | The project licence of milestone v0.15.0 (see the licence decision of that milestone); recorded here so the new files carry it |

## Decision

1. **Amendment of ADR-0013 decision 4.** A hook runs with: a working directory fixed by its scope (the workspace root for project and Claude project files, the file's own directory for user and managed files, the plugin directory for plugin hooks), a shell string or an `args` vector, an open standard input that carries the event JSON and is then closed, a scrubbed environment, bounded output and a timeout (default 30 s, at most 600 s). Its whole process tree is killed on timeout or cancellation. The shell is used only for the `command` form. The `args` form keeps the original no-shell guarantee for people who want it.
2. **Environment.** The child receives the allow-list `PATH`, `HOME`, `USER`, `LOGNAME`, `SHELL`, `LANG`, `LANGUAGE`, `LC_*`, `TERM`, `TMPDIR`, `TEMP`, `TMP`, `TZ` and the Windows system variables, plus `CURSOR_PROJECT_DIR`, `CLAUDE_PROJECT_DIR`, `AUTONOGRAMMER_PROJECT_DIR`, `CURSOR_VERSION`, `AUTONOGRAMMER_VERSION`, `CURSOR_USER_EMAIL` and `CURSOR_TRANSCRIPT_PATH` when known, `CURSOR_CODE_REMOTE=true` in remote sessions, `CURSOR_PLUGIN_ROOT` and `AUTONOGRAMMER_PLUGIN_ROOT` for plugin hooks, `AUTONOGRAMMER_HOOK_EVENT` and `AUTONOGRAMMER_SESSION_ID`. A hook's `env` and the `sessionStart` environment are added on top and cannot set `AUTONOGRAMMER_*`, `CURSOR_*` or `CLAUDE_PROJECT_DIR`. The option `InheritEnvironment` restores Cursor-like inheritance of the rest of the parent environment, but never of `AUTONOGRAMMER_*`.
3. **Order and merge.** Hooks run sequentially in priority then file order, and every matching hook runs even after a deny. The merged permission is deny over ask over allow whatever the source; `user_message` and `agent_message` are joined with a newline in run order; for the other fields the last responding hook wins, which means a lower-priority source overrides a higher-priority one (Cursor's own page says so; its third-party page says the opposite, and the owner default O3 follows the hooks page).
4. **Failure policy.**

   | Case | Permission events | Other events |
   | --- | --- | --- |
   | Exit 0 and a valid answer | the answer is used | the answer is used where the event honours it |
   | Exit 0 and invalid JSON or a schema mismatch | deny (`InvalidResponse`) | logged, output ignored |
   | Exit 0 and no output | allow; deny when `failClosed` | allow; `failClosed` has no effect |
   | Exit 0, output cut at the capture limit and no longer parseable | a failure of the hook: allow; deny when `failClosed` | a failure: ignored |
   | Exit 2 | deny; the message is the stdout JSON message, else standard error | an event that can block (`beforeSubmitPrompt`) blocks; any other is a logged failure |
   | Any other exit code, crash, `args` executable missing | allow, logged; deny when `failClosed` | ignored |
   | Timeout | the tree is killed; allow; deny when `failClosed` | ignored |
   | No consent | deny (`ConsentMissing`), nothing is spawned | skipped |
   | Quarantined source | deny (`PluginQuarantined`), nothing is spawned | skipped |
   | The runner itself fails, or the shell is missing | the source is quarantined and the hook denies | the source is quarantined, the hook is skipped |
   | Loopback host unreachable (seen by the extension) | allow; deny for events in `failClosedEvents` (a `failClosed` hook exists for the event, or a managed permission hook does) | allow |

   The permission events are `preToolUse`, `subagentStart`, `beforeShellExecution`, `beforeMCPExecution`, `beforeReadFile` and `beforeTabFileRead`. Plugin manifest hooks keep their old rule: an exit 0 with output that is not a decision is an allow.
5. **`ask`.** Honoured for `beforeShellExecution` and `beforeMCPExecution`: the host asks the user through its approval prompt before answering the bridge, and no prompt means deny. For `preToolUse` it is accepted and not enforced, as in Cursor. For `subagentStart` it is a deny. For `beforeReadFile` it is an invalid response. A hook can only tighten: an allow never bypasses omp's own approval policy or the ACP gate.
6. **Managed hooks** (`/etc/autonogrammer/hooks.json`, `/Library/Application Support/Autonogrammer/hooks.json`, `%ProgramData%\Autonogrammer\hooks.json`) are pre-consented only when the file and its directory are not writable by group or others. On Windows Autonogrammer does not read access control lists, so managed hooks are not pre-consented there. Cursor's own enterprise paths are never read. There is no team scope.
7. **Bridge.** One request per connection on `127.0.0.1`, an ephemeral port, a bearer token compared in constant time before the body is parsed, a body cap (8 MiB), a header cap, a loopback peer check, a handler timeout (120 s), at most 64 concurrent requests, and a startup file (`AUTONOGRAMMER_HOOKS_STARTUP_FILE`) that lists `failClosedEvents`, `activeEvents`, `activeOmpEvents` and `requestTimeoutMs`. The URL and token reach omp only as environment variables. Request bodies and the token are never logged.
8. **Log.** Every considered hook leaves a record in an append-only run log (two files, 5 MiB together) holding the event, hook, status, decision, exit code, duration and the SHA-256 of the input. It never holds an input payload. Excerpts of standard output and standard error are cut at 4 KiB.
9. **Where hooks run.** In the process that owns the omp child: the head for a desktop session, the API or sandbox process for a hosted run, never in a browser. User-scope hooks do not run in remote and hosted sessions. Hosted runs obey the consent rule of ADR-0013 and do not auto-trust project hooks.
10. **What ACP alone cannot do** and the extension therefore does: block or rewrite a tool call before it runs (`preToolUse`, `beforeShellExecution`, `beforeMCPExecution`, `beforeReadFile`), see calls that never reach `session/request_permission`, block a subagent spawn, and feed context back to the model after a tool result. `afterAgentResponse`, `afterAgentThought` and the edit and shell outputs can also be observed from ACP `session/update`; that tap is a later unit and only observes.
11. **Not supported, not stubbed.** `beforeTabFileRead` and `afterTabFileEdit` are accepted in `hooks.json` and never fire, because Autonogrammer has no Tab inline-completion engine. Team hooks and Cursor's enterprise paths do not exist in Autonogrammer.

## Verification route

Run by the orchestrator after the merge: `dotnet test tests/Autonogrammer.Harness.Plugins.Tests` and `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests` (filter `FullyQualifiedName~Hooks`), which hold the .NET tests of `Autonogrammer.Harness.Plugins.Tests` (loader, locator, parser, consent identity, dispatcher failure policy table, merge, loop limit, prompt hooks, environment scrub with canary secrets, the real `/bin/sh` command runner including stdin delivery, truncation, timeout and tree kill), of `Autonogrammer.Harness.AgentLoop.Tests` (the bridge server: token, caps, loopback, concurrency, fail-open and fail-closed), and `node --test plugins/autonogrammer-hooks-omp/test/*.test.mjs` (event mapping and fail policy against a fake bridge). Not run in this record: any live omp session with the extension loaded. That needs `HOME=<scratch> PI_CODING_AGENT_DIR=<scratch>/.omp/agent` with fake keys and no model turn, and an `omp acp --no-extensions -e plugins/autonogrammer-hooks-omp` launch whose hooks log shows a denied `tool_call`; it stays open until the planner passes the environment variables (OQ-84 closure).

## Open risks

1. **Repository code runs on the user's machine.** A cloned repository can carry `.cursor/hooks.json` and `.claude/settings.json`. Mitigation: nothing runs without consent for the exact hook identity, there is no auto-trust, a guarding hook without consent denies instead of passing, the script content is part of the identity, and the environment is scrubbed. A hook can still do anything the user can; the consent text must say so.
2. **Consent covers named scripts only by heuristic.** The identity hashes the files a command names by path inside the project. A script reached through a variable, a glob or an interpreter's search path is not hashed; its command text still is.
3. **Path swap between consent and spawn** is not guarded for files outside the project or for the interpreter itself (ADR-0013 risk 8).
4. **Owner of a managed file is not checked on Linux and macOS**, only the mode of the file and its directory, because .NET has no portable owner API. On Windows the check is not made at all.
5. **Ambiguities in Cursor's documentation** are resolved by default and recorded in `docs/reference/hooks.md`: sequential execution, the 30 s default timeout, the message on exit 2, unanchored matcher regular expressions, no-output meaning a failed hook rather than an invalid response, and the merge direction.
6. **Latency.** Each hook is a process start plus a loopback call. The extension skips events that have no hook and awaits only answers it uses, but a slow `preToolUse` hook slows every tool call.
7. **The bridge extension relays model-controlled tool input.** The host treats it as untrusted: it is JSON data for the hook's standard input, never interpolated into a shell string.
8. **`ask` outlives the extension's patience only when the host's prompt does.** The extension waits up to `requestTimeoutMs` (the handler timeout plus 10 s) for an awaited answer; a longer approval fails by the rule of the table.
9. **MCP server and tool names of a `tool_call`** are parsed from omp's `mcp__<server>_<tool>` name (the event carries no server field), which is wrong for server names that contain an underscore. `tool_result` details are preferred when present.
10. **Remote workspaces.** If omp runs on another machine the loopback bridge cannot be reached; the dispatcher must run beside omp (design 3.2). Not built here.
11. **No live run.** The extension was tested against a fake omp API only.

## Owner decisions needed

- Confirm the amendment of ADR-0013 decision 4 (shell strings, open standard input, scrubbed environment). OQ-96 records the default taken.
- Confirm the merge direction of non-permission fields (lower priority overrides higher, Cursor's hooks page) against the opposite sentence on its third-party page.
- Confirm that a guarding hook without consent denies (ADR-0013 rule) instead of being skipped with a prompt, now that a cloned repository can bring one.
