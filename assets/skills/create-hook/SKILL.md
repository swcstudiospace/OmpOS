---
name: create-hook
description: Create a hook script and add its entry to hooks.json so it runs on an agent lifecycle event.
disable-model-invocation: false
user-invocable: true
---

# Create a hook

Hooks are your own scripts that watch or steer the agent: block a shell command, rewrite a tool input, format a file after an edit, add context, or ask the agent to continue when it stops. This skill creates the script and the `hooks.json` entry that runs it. The text the user typed after the command says what the hook should do.

## 1. Pin down the hook

If the request leaves any of these open, ask once, in a single message, and then continue:

- **What should happen**, in one sentence (for example "block `rm -rf` outside the workspace" or "run prettier after every edit").
- **Which event.** Choose from the list below. When two events fit, prefer the one that can block if the hook must stop something, and the one that only observes if it just reacts.
- **Where it lives.** Default: the project file `.ompos/hooks.json` in the workspace root. A personal hook goes to `~/.ompos/hooks.json`. Do not write a Cursor or Claude file unless the user asks for it.
- **Fail-open or fail-closed.** Default is fail-open (an error lets the action through). Set `"failClosed": true` only when the user wants a broken hook to block the action, and only on an event that can block.

Events: `sessionStart`, `sessionEnd`, `preToolUse`, `postToolUse`, `postToolUseFailure`, `subagentStart`, `subagentStop`, `beforeShellExecution`, `afterShellExecution`, `beforeMCPExecution`, `afterMCPExecution`, `beforeReadFile`, `afterFileEdit`, `beforeSubmitPrompt`, `preCompact`, `stop`, `afterAgentResponse`, `afterAgentThought`, `workspaceOpen`. The events that guard an action are `preToolUse`, `subagentStart`, `beforeShellExecution`, `beforeMCPExecution` and `beforeReadFile`. Read `docs/reference/hooks.md` in the OmpOS repository, or the project's copy, when you need the fields an event sends or answers with. Do not use the Tab events: OmpOS accepts them and never fires them.

## 2. Read before you write

1. Read the target `hooks.json` if it exists. Keep every existing entry, key and event exactly as it is; you only add to it.
2. If the file exists but is not valid JSON, stop and tell the user. Do not rewrite it.
3. Look for an existing hook with the same event and purpose. If there is one, show it and ask whether to replace it or add a second one.

## 3. Write the script

Put the script next to the file it belongs to: `.ompos/hooks/<name>.sh` for a project hook, `~/.ompos/hooks/<name>.sh` for a user hook. Use a language the machine already has; a POSIX shell script with `jq` is fine when `jq` is installed, otherwise use `python3` or `node`. Rules for every script:

- It reads **one JSON object from standard input** and may write **one JSON object to standard output**. Read stdin completely before it decides.
- It exits `0` on success. Exit `2` blocks the action (the same as `"permission": "deny"`); anything else is a failure.
- On a guarding event, standard output must be valid JSON or empty. An answer that is not valid JSON blocks the action, so never print debugging text to stdout. Write diagnostics to stderr.
- Answer fields: `permission` (`allow`, `deny`, `ask`), `user_message` (shown to the user), `agent_message` (sent to the model when denied), `updated_input`, `additional_context`, `followup_message` (only `stop` and `subagentStop`), `continue`, `env` (only `sessionStart`). An event honours only its own fields.
- It must not print secrets, tokens or environment dumps, and must not make network calls the user did not ask for.
- Make it executable (`chmod +x`) and keep it short enough to read in one screen.

## 4. Add the entry

Add an object under the event name in the `hooks` object. If the file does not exist, create it as `{"version": 1, "hooks": {}}` first. The file must stay UTF-8 JSON with no comments, no trailing commas and no repeated keys, and below 64 KiB.

| Key | Meaning |
| --- | --- |
| `command` | The script path (relative to the directory the hook runs in) or a shell string. Use `args` instead when no shell should interpret anything: an argument vector. `command` and `args` are mutually exclusive. |
| `type` | `"command"` (default) or `"prompt"` (asks a model; give `prompt` and optionally `model`). |
| `timeout` | Seconds; default 30, at most 600. Set a short value (5 to 10) for hooks that run on every tool call. |
| `matcher` | A regular expression tested unanchored and case sensitively against the call (for shell events, the command). Omit it to match everything. |
| `loop_limit` | Only for `stop` and `subagentStop`: how many follow-ups the hook may trigger. Default 5. |
| `failClosed` | See step 1. |
| `id` | A stable, unique name inside the file, for example `block-rm-rf`. Always set one. |
| `description` | One line saying what the hook does. |

Project hooks run in the workspace root; user hooks run in the directory of the file. Use paths relative to that directory.

## 5. Check and report

1. Re-read the file you wrote and confirm it parses and still holds every earlier entry.
2. Feed the script a sample input by hand (a small JSON object on stdin) and show the output. Do not run a hook that has side effects outside the workspace; describe the test instead.
3. Tell the user that **nothing runs until they approve the exact hook on the Hooks page** (Settings, Hooks), and that editing the script asks for approval again. Name the file and the script you created.

## Never

- Never delete or rewrite an existing hook without asking.
- Never create a hook that uploads file content, prompts or environment variables to a remote host.
- Never set `failClosed` on a hook that has not been tested with a sample input.
