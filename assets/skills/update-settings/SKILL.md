---
name: update-settings
description: Explain and change OmpOS settings safely, using the Settings window first and editing settings files only with a backup and your approval.
disable-model-invocation: false
user-invocable: true
---

# Update settings

Answers "how do I change X" for OmpOS and, when asked, makes the change. The text the user typed after the command says which setting. This skill replaces Cursor's `update-cursor-settings` and `update-cli-config`: OmpOS has no Cursor configuration file, so settings live in the places below.

## 1. Where each setting lives

| Setting | Place | How to change |
| --- | --- | --- |
| Theme, motion, terminal auto-run, idle stop and retention | Settings window, `config/settings.json` under the state directory | Settings window (type `/config` or open Settings). Edit the file only if the window is not available. |
| Models, providers and credentials | omp's own configuration (`~/.omp/agent/models.yml`, its credential store) | The Models page. Never write keys into any file yourself. |
| MCP servers | `.ompos/mcp.json` in the workspace (OmpOS writes this one); Cursor, Claude and VS Code MCP files are read only | The MCP page: add from a template or a preset. |
| Hooks | `.ompos/hooks.json`, `~/.ompos/hooks.json` | `/create-hook` or the Hooks page. |
| Rules, skills, commands, subagents | `.ompos/` folders (and the other convention folders OmpOS reads) | `/create-rule`, `/create-skill`, `/create-subagent`. |
| Per-session choices (mode, model, working directory) | The conversation | Slash commands such as `/plan`, `/ask`, `/model`, and the composer. |

The state directory is `$OMPOS_STATE_DIR` when set; otherwise `$XDG_DATA_HOME/OmpOS` when that is an absolute path; otherwise `~/.local/share/OmpOS` on Linux. Use the platform's application data folder when the user runs another system.

## 2. Answer first

1. Work out which setting the user means. If more than one fits, list them and ask.
2. Say where it lives and how to change it in the Settings window, in one or two sentences. **Prefer the Settings window**: it validates values and keeps members the file does not know.
3. Only when the user asks you to make the change, or the window cannot be used, go on to step 3.

## 3. Edit a settings file

`config/settings.json` is UTF-8 JSON. Known members: `schemaVersion` (leave as is), `motion` (`system`, `reduced` or `full`), `theme` (`system`, `light` or `dark`), `terminal.autoRun` (true or false), `runtime.idleStopHours` and `runtime.retentionDays` (whole numbers). Members you do not recognise belong to a newer version and must be kept exactly.

1. Read the current file. If it is missing, the defaults apply: say so and create it only with the members the user asked for.
2. If it is not valid JSON, or has a `schemaVersion` you do not know, **stop** and tell the user. Do not repair or replace it.
3. Show the exact change (old value and new value) and ask for a yes.
4. Make a backup copy next to the file (`settings.json.bak-<date>`) and tell the user its name.
5. Change only the requested member and keep every other member and its order. Keep the file owner-only (mode 0600) and the folder private.
6. Re-read the file and confirm it parses and shows the new value. Say that the running app picks the change up when it re-reads settings (usually on reopening the Settings window), and restart if it does not.

## 4. Safety rules

- Never write an API key, token or password into any file, and never print one. Credentials are added on the Models page, which hands them to omp.
- Never edit omp's `models.yml` or credential store by hand from this skill. Point to the Models page.
- Never edit files outside the OmpOS state directory, the workspace's `.ompos` folder and the user's `~/.ompos` folder.
- Never change settings that affect other people (a shared repository's `.ompos` files) without saying so and getting a yes.
- If the user wants a Cursor, VS Code or terminal-emulator setting, say that this skill does not touch those applications.
