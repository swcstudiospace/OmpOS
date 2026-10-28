# Slash commands

OmpOS lists every slash command and alias that Cursor 3.27 documents (92 tokens) and one OmpOS addition, `/update-settings` (93 rows), plus the open-ended forms Cursor allows: a skill name, a custom command file and a subagent name. Each token has a handler kind and an honest status; a token OmpOS cannot run is still listed, disabled, with the reason, so typing it never silently does nothing.

The table below is the same data as the shipped catalogue (`SlashCommandCatalogue.Shipped` in `OmpOS.Presentation`). A test parses this file and fails when the two disagree.

Sources: Cursor's CLI slash-command reference, the Agent chat and Agents Window pages, the built-in skills list and the CLI changelog, read on 2026-10-22 and 2026-10-27 (`docs/plans/vision-research/cursor-slash-commands.md` and `docs/plans/v0.15.0-research/03-cursor-settings-slash.md`). omp's command list was read from omp 18.12.6 itself (`src/slash-commands/builtin-registry.ts`): only commands with an ACP handler are delegated.

## How a typed command is resolved

When you send text that starts with `/`, OmpOS looks at the name (the text up to the first space) in this order and runs the first source that can run it:

1. An exact catalogue token (the table below).
2. A command that omp advertises for the session (`available_commands_update`), such as `/fast` or `/skill:review`.
3. A custom command file found by discovery (`commands/*.md`).
4. A skill found by discovery (`skills/<name>/SKILL.md`).
5. A subagent found by discovery (`agents/*.md`).

A catalogue token that OmpOS cannot run does not hide a lower source that can. When two sources can run the same name, the palette lists both rows, each with a source badge (`Engine`, `OmpOS`, `Cursor`, `Command`, `Skill`, `Subagent`) and, for a file, its scope (`project`, `user` or `built-in`); the first source in the order above is the one that runs. A name that no source knows is sent to omp exactly as typed.

A text that starts with `&` and a space hands the rest to a cloud agent (the CLI form), as `/in-cloud` does: the window asks for the repository, shows what the cloud worker costs and starts the agent only after you confirm. Without the OmpOS API address and a sign-in it says what is missing and sends nothing.

## Handler kinds

| Kind | What happens when the command is sent |
|---|---|
| NativeAction | The window runs a named action through `ISlashActionHost`: open Settings, start a new chat or terminal, switch mode, filter the model picker, open a side chat, open the palette. The desktop head implements the host (`DesktopSlashActionHost`); the web head implements it in a later wave. An action the window cannot run is listed disabled. |
| OmpDelegated | The text goes to omp as the prompt, and omp runs the command. Only commands that omp 18.12.6 handles over ACP are delegated: `rename`, `compact`, `mcp`, `plugins` (alias `plugin`), `wt` (alias `worktree`), `add-dir`, `context`, `usage`, `fast`. A Cursor alias that omp names differently is rewritten (`/summarize` is sent as `/compact`). |
| Skill | A skill file named like the token is run: omp is told to read the `SKILL.md` and follow it (or gets `/skill:<name>` when it advertises the skill). OmpOS ships its own wording for the built-in skills (`assets/skills/<name>/SKILL.md`, listed in `docs/reference/builtin-skills.md`) in the `skills` folder next to the desktop application; a project or user `SKILL.md` of the same name takes over. The remote and API-hosted sessions cannot read that folder, so there a `SKILL.md` of the same name must be installed. |
| CloudHandoff | The task is handed to a cloud agent through the `cloud.handoff` action, after the repository and the cost are confirmed in a dialog. Partial: it needs the OmpOS API address and a sign-in. |
| Unavailable | OmpOS cannot run the command; the reason is in the table. |

### Native actions in the desktop window

| Action | Supplied by | Used by |
|---|---|---|
| `mode.set`, `model.pick`, `help.show` | The session's own composer (mode chip, model picker, slash palette) | `/plan`, `/ask`, `/debug`, `/model`, `/opus`, `/composer`, `/help` |
| `chat.new`, `terminal.new`, `settings.open` | The shell (`MainView`) | `/clear` and its aliases, `/shell` and its aliases, `/config`, `/rules`, `/skills`, `/commands`, `/plugins`, `/run-everything`, `/auto-run`, `/bedrock` (each opens its Settings page) |
| `cloud.handoff` | The shell: a confirmation dialog over `ICloudHandoff` | `/in-cloud`, `& <task>` |
| `chat.rewind`, `chat.restore`, `chat.undo` | `CheckpointSlashHandler` over the session's Checkpoints timeline | `/rewind`, `/restore`, `/undo` |
| `chat.fork`, `chats.side.open` | `ForkSlashHandler`, `SideChatSlashHandler` over the session's fork and side chat models | `/fork`, `/branch`, `/duplicate`, `/side`, `/btw` |
| `worktree.apply`, `worktree.delete`, `worktree.bestofn` | `WorktreeSlashHandler` over the Worktrees panel | `/apply-worktree`, `/delete-worktree`, `/best-of-n` |
| `chat.copy`, `chat.copyConversationId`, `chat.copyRequestId` | `CopySlashHandler` over the session and the clipboard | `/copy`, `/copy-conversation-id`, `/copy-request-id` |
| `app.about`, `app.quit`, `app.feedback` | `AboutSlashHandler`, `QuitSlashHandler`, `FeedbackSlashHandler` | `/about`, `/quit`, `/exit`, `/feedback` |
| `editor.open`, `changes.open` | `OpenFileSlashHandler`, `ChangesSlashHandler` | `/open`, `/cursor`, `/changes` |
| `workspace.save`, `workspace.load` | `WorkspaceSetSlashHandler` over the workspace set store | `/save-workspace`, `/load-workspace` |
| `display.lineNumbers`, `display.statusIndicators` | `DisplayToggleSlashHandler` over `DisplaySettings` | `/line-numbers`, `/status-indicators` |

A native command whose action the window cannot run is listed disabled. The web head implements `ISlashActionHost` in a later wave.

## Status

| Status | Meaning |
|---|---|
| Built | OmpOS runs the command as Cursor describes it. |
| Delegated | omp runs the command; OmpOS only forwards the text. |
| Partial | OmpOS runs part of it, or only under the condition the reason states. |
| Unavailable | OmpOS does not run it; the reason says why (needs Cursor's own backend, retired, or not built). |

## Tokens and aliases

| Token | Group | What Cursor does | Handler kind | Handler | Status | Reason |
|---|---|---|---|---|---|---|
| `/model` | CLI | Opens the model picker; Tab edits the filter. | NativeAction | `model.pick` | Built | Filters the composer's model picker by the text after the command and selects the model when exactly one matches. |
| `/run-everything` | CLI | Toggles Run Everything or shows its state. | NativeAction | `settings.open run-mode` | Partial | Opens Settings on the Run mode page, which holds the run-everything choice; the words on, off and status are not read. |
| `/plan` | CLI | Switches to Plan mode, or submits a prompt in Plan mode. | NativeAction | `mode.set plan` | Built | Switches the session to the engine's plan mode; text after the command is sent as the first prompt in that mode. |
| `/ask` | CLI | Toggles Ask mode (read-only questions). | NativeAction | `mode.set ask` | Partial | Works only when the engine advertises an ask mode; omp 18.12.6 advertises default and plan. |
| `/debug` | CLI | Toggles Debug mode or submits a prompt in it. | NativeAction | `mode.set debug` | Partial | Works only when the engine advertises a debug mode; omp 18.12.6 advertises default and plan. |
| `/goal` | CLI | Gives the agent a long-lived objective until it is complete. | Unavailable | - | Unavailable | omp 18.12.6 runs /goal in its terminal UI and RPC only; it has no ACP handler. |
| `/logs` | CLI | Shows the debug log path and copies it. | Unavailable | - | Unavailable | OmpOS writes its diagnostics to the standard error stream and keeps no log file or log folder to open. |
| `/update` | CLI | Updates the Cursor Agent CLI. | Unavailable | - | Unavailable | Updates Cursor's own CLI; OmpOS has no Cursor CLI to update. |
| `/max-mode` | CLI | Toggles Max Mode on legacy request-based plans. | Unavailable | - | Unavailable | Max Mode is a Cursor plan feature. |
| `/rename` | CLI | Renames the current chat session. | OmpDelegated | `/rename` | Delegated | Sent to omp as typed; omp renames the session (an empty title generates one). |
| `/clear` | CLI | Starts a new chat session. | NativeAction | `chat.new` | Built | Starts a new chat in the desktop head. |
| `/resume` | CLI | Opens recent chats and resumes one. | Unavailable | - | Unavailable | The agent rail lists and resumes chats; there is no command form in this build. |
| `/fork` | CLI | Forks the current chat into a new session. | NativeAction | `chat.fork` | Built | Forks the whole conversation into a new session through the engine's session fork; the new session opens in the rail. Forking from an earlier message is refused until the engine supports it, and a terminal session cannot be forked. |
| `/summarize` | CLI | Summarises the conversation to reduce context. | OmpDelegated | `/compact` | Delegated | Sent to omp as /compact, which compacts the conversation. |
| `/rewind` | CLI | Jumps back to a previous message. | NativeAction | `chat.rewind` | Partial | Opens this session's Checkpoints timeline; with a turn number it previews the files and asks before it puts them back. It restores the files the agent changed, not the conversation text, and only for turns OmpOS recorded. |
| `/vim` | CLI | Toggles Vim keys in the prompt. | Unavailable | - | Unavailable | The OmpOS composer has no Vim key bindings yet, so the setting would change nothing. |
| `/line-numbers` | CLI | Toggles line numbers in code blocks. | NativeAction | `display.lineNumbers` | Built | Shows or hides the line-number gutter of code blocks; the choice is saved. |
| `/show-thinking` | CLI | Toggles the thinking block display. | Unavailable | - | Unavailable | OmpOS does not draw the model's thinking in the transcript, so there is nothing to show or hide. |
| `/status-indicators` | CLI | Toggles terminal title status indicators. | NativeAction | `display.statusIndicators` | Partial | Shows or hides the status glyphs of the session rows and thread header; their words stay for screen readers. Cursor's setting is the terminal title, which OmpOS does not have. |
| `/shell` | CLI | Enters Shell Mode; also a built-in skill that runs literal text as a shell command. | NativeAction | `terminal.new` | Partial | Opens a new terminal session; the text after the command is not run. |
| `/about` | CLI | Shows CLI version, system and account info; copies them. | NativeAction | `app.about` | Built | Opens the About window: OmpOS version, revision, licence and where to get the source. OmpOS has no Cursor account. |
| `/setup-terminal` | CLI | Configures terminal newline keybindings. | Unavailable | - | Unavailable | Configures the Cursor CLI's newline keys. |
| `/help` | CLI | Shows help, or details for one command. | NativeAction | `help.show` | Built | Opens the slash palette, filtered by the text after the command. |
| `/feedback` | CLI | Sends feedback to the team. | NativeAction | `app.feedback` | Partial | Opens the OmpOS issue form in the browser with the typed title; nothing is sent from the app. |
| `/open` | CLI | Opens the repository's Git root in Cursor. | NativeAction | `editor.open` | Partial | Opens a file of the workspace in the OmpOS editor, at the line after the colon; it does not open Cursor. |
| `/copy-request-id` | CLI | Copies the last request ID. | NativeAction | `chat.copyRequestId` | Partial | Copies the id of the last turn's request when the engine session reports one; says so when it has none. |
| `/copy-conversation-id` | CLI | Copies the conversation ID. | NativeAction | `chat.copyConversationId` | Built | Copies this session's omp session id to the clipboard. |
| `/logout` | CLI | Signs out of Cursor. | Unavailable | - | Unavailable | Signs out of a Cursor account; OmpOS has no Cursor account. |
| `/quit` | CLI | Exits. | NativeAction | `app.quit` | Built | Closes the OmpOS window after the sessions have stopped. |
| `/exit` | CLI | Exits. | NativeAction | `app.quit` | Built | Closes the OmpOS window after the sessions have stopped. |
| `/mcp` | CLI | Manages MCP servers and lists their tools. | OmpDelegated | `/mcp` | Delegated | Sent to omp as typed; omp manages MCP servers. |
| `/plugin` | CLI | Manages plugins and marketplaces. | OmpDelegated | `/plugin (omp /plugins)` | Partial | Sent to omp as typed; omp's /plugin is an alias of /plugins (list, enable, disable). Marketplace subcommands are omp's separate /marketplace. |
| `/config` | CLI | Configures CLI settings interactively. | NativeAction | `settings.open` | Built | Opens the Settings window on its first page. |
| `/copy` | CLI | Copies a previous user message. | NativeAction | `chat.copy` | Built | Copies the last response to the clipboard; 'user' copies your last message instead. |
| `/sandbox` | CLI | Configures sandbox mode and network access. | Unavailable | - | Unavailable | Not built: no sandbox mode switch in OmpOS Settings. |
| `/bedrock` | CLI | Configures Bedrock when the feature is enabled. | NativeAction | `settings.open models` | Partial | Opens Settings on the Models page, which sets up providers; Bedrock subcommands are not read. |
| `/auto-run` | CLI | Alias of /run-everything. | NativeAction | `settings.open run-mode` | Partial | Alias of /run-everything: opens Settings on the Run mode page; the words on, off and status are not read. |
| `/new` | CLI | Alias of /clear: starts a new chat session. | NativeAction | `chat.new` | Built | Starts a new chat in the desktop head. |
| `/new-chat` | CLI | Alias of /clear: starts a new chat session. | NativeAction | `chat.new` | Built | Starts a new chat in the desktop head. |
| `/newchat` | CLI | Alias of /clear: starts a new chat session. | NativeAction | `chat.new` | Built | Starts a new chat in the desktop head. |
| `/compress` | CLI | Alias of /summarize. | OmpDelegated | `/compact` | Delegated | Sent to omp as /compact, which compacts the conversation. |
| `/sh` | CLI | Alias of /shell. | NativeAction | `terminal.new` | Partial | Opens a new terminal session; the text after the command is not run. |
| `/run` | CLI | Alias of /shell. | NativeAction | `terminal.new` | Partial | Opens a new terminal session; the text after the command is not run. |
| `/cursor` | CLI | Alias of /open. | NativeAction | `editor.open` | Partial | Alias of /open: opens a file of the workspace in the OmpOS editor. |
| `/side` | Chat | Opens a side chat with its own transcript; the parent thread is hidden reference context. | NativeAction | `chats.side.open` | Built | Opens a side chat with its own transcript. The question is answered by a separate engine session that gets a summary of this conversation as hidden context, so the parent thread is not changed. |
| `/btw` | Chat | In the IDE an alias for a side chat; in the CLI a read-only side question. | NativeAction | `chats.side.open` | Partial | The IDE form: opens a side chat as /side does. The CLI overlay form is not built. |
| `/in-cloud` | Chat | Hands a task to a cloud subagent that runs on its own VM and branch. | CloudHandoff | `cloud.handoff` | Partial | Asks for the repository and your confirmation of the cloud cost, then starts a cloud agent with the task. Needs the OmpOS API address and a sign-in; without them the command says what is missing. |
| `/automate` | Built-in skill | Built-in skill: creates Cursor Automations from schedules, Slack, GitHub and other sources. | Unavailable | - | Unavailable | Creates Cursor Automations, a Cursor cloud service. |
| `/autopilot` | Built-in skill | Built-in skill: monitors a PR and addresses feedback, conflicts and failing checks. | Unavailable | - | Unavailable | Cursor's autopilot skill watches a pull request from Cursor's cloud backend; OmpOS hands a one-off task to a cloud agent with /in-cloud and has no pull request watcher. |
| `/canvas` | Built-in skill | Built-in skill: creates interactive React artifacts beside the conversation. | Unavailable | - | Unavailable | Cursor renders canvas artifacts in its own pane. |
| `/create-hook` | Built-in skill | Built-in skill: creates hooks and updates hooks.json. | Skill | `SKILL.md named create-hook` | Built | Ships with OmpOS as a built-in skill; a project or user skill of the same name takes over. |
| `/create-rule` | Built-in skill | Built-in skill: creates rules with scope and instructions. | Skill | `SKILL.md named create-rule` | Built | Ships with OmpOS as a built-in skill; a project or user skill of the same name takes over. |
| `/create-skill` | Built-in skill | Built-in skill: creates Agent Skills and SKILL.md. | Skill | `SKILL.md named create-skill` | Built | Ships with OmpOS as a built-in skill; a project or user skill of the same name takes over. |
| `/create-subagent` | Built-in skill | Built-in skill: creates custom subagents. | Skill | `SKILL.md named create-subagent` | Built | Ships with OmpOS as a built-in skill; a project or user skill of the same name takes over. |
| `/cursor-blame` | Built-in skill | Built-in skill: investigates AI-authored changes and their prompts. | Unavailable | - | Unavailable | Needs Cursor's record of AI-authored changes and their prompts. |
| `/loop` | Built-in skill | Built-in skill: runs a prompt or skill repeatedly at an interval. | Unavailable | - | Unavailable | omp runs /loop in its terminal UI only, and OmpOS has no scheduler for repeating prompts. |
| `/migrate-to-skills` | Built-in skill | Built-in skill: converts dynamic rules and slash commands into skills. | Skill | `SKILL.md named migrate-to-skills` | Built | Ships with OmpOS as a built-in skill; a project or user skill of the same name takes over. |
| `/review` | Built-in skill | Built-in skill: selects and runs the code-review agent. | Skill | `SKILL.md named review` | Built | Ships with OmpOS as a built-in skill; a project or user skill of the same name takes over. |
| `/review-bugbot` | Built-in skill | Built-in skill: reviews for bugs and regressions with Bugbot. | Unavailable | - | Unavailable | Bugbot is a Cursor-hosted reviewer. |
| `/review-security` | Built-in skill | Built-in skill: reviews for security vulnerabilities with Security Review. | Skill | `SKILL.md named review-security` | Built | Ships with OmpOS as a built-in skill; a project or user skill of the same name takes over. |
| `/sdk` | Built-in skill | Built-in skill: helps build applications with the Cursor SDK. | Unavailable | - | Unavailable | Helps build with the Cursor SDK. |
| `/split-to-prs` | Built-in skill | Built-in skill: splits large changes into smaller PRs. | Skill | `SKILL.md named split-to-prs` | Built | Ships with OmpOS as a built-in skill; a project or user skill of the same name takes over. |
| `/statusline` | Built-in skill | Built-in skill: configures the CLI status line. | Unavailable | - | Unavailable | Configures the Cursor CLI status line. |
| `/update-cli-config` | Built-in skill | Built-in skill: updates ~/.cursor/cli-config.json. | Unavailable | - | Unavailable | Edits the Cursor CLI's own configuration file; use /update-settings for OmpOS settings. |
| `/update-cursor-settings` | Built-in skill | Built-in skill: finds and updates a Cursor or VS Code setting. | Unavailable | - | Unavailable | Edits Cursor and VS Code settings; use /update-settings for OmpOS settings. |
| `/update-settings` | Built-in skill | Built-in skill: explains and changes OmpOS settings safely. | Skill | `SKILL.md named update-settings` | Built | Ships with OmpOS as a built-in skill; a project or user skill of the same name takes over. |
| `/add-dir` | CLI | Adds a directory mid-session; refreshes skills and custom commands. | OmpDelegated | `/add-dir` | Delegated | Sent to omp as typed; omp adds the directory to the session. OmpOS re-reads conventions when you refresh. |
| `/auto-review` | CLI | Named in the Auto-review run-mode entry; Cursor does not state its behaviour. | Unavailable | - | Unavailable | Cursor documents the name only, not what it does. |
| `/changes` | CLI | Unified review UI with a Session tab showing this session's edits. | NativeAction | `changes.open` | Partial | Shows the Changes bar with this session's edited files; there is no unified review window with a Session tab. |
| `/commands` | CLI | Interactive browser for custom commands, with inline editing. | NativeAction | `settings.open commands` | Built | Opens Settings on the Commands page. |
| `/context` | CLI | Shows what fills the context window, by category. | OmpDelegated | `/context` | Delegated | Sent to omp as typed; omp shows context usage. |
| `/detach` | CLI | Detaches from a persistent session started with agent persist. | Unavailable | - | Unavailable | Cursor CLI persistent sessions (agent persist) have no OmpOS equivalent. |
| `/full-conversation` | CLI | Opts out of recent-turns-only redraw. | Unavailable | - | Unavailable | A Cursor CLI redraw mode. |
| `/load-workspace` | CLI | Reloads a saved multi-directory workspace. | NativeAction | `workspace.load` | Built | Opens a saved workspace set as a new session; with no name it lists the saved sets. |
| `/save-workspace` | CLI | Saves the current multi-directory set. | NativeAction | `workspace.save` | Built | Saves the session's folders as a named workspace set that /load-workspace and the workspace dialog reopen. |
| `/rules` | CLI | Interactive browser for rules, with inline editing. | NativeAction | `settings.open rules` | Built | Opens Settings on the Rules page. |
| `/skills` | CLI | Browses skills; flags those that break Agent Skills limits. | NativeAction | `settings.open skills` | Built | Opens Settings on the Skills page. |
| `/usage` | CLI | Shows plan usage meters, on-demand spend and billing reset. | OmpDelegated | `/usage` | Partial | Sent to omp as typed; omp shows token usage, not Cursor plan meters. |
| `/plugins` | CLI | Shows plugin details, with each plugin's MCP servers linked into /mcp. | NativeAction | `settings.open plugins` | Built | Opens Settings on the Plugins page. |
| `/fast` | CLI | Toggles Fast when the current model supports it. | OmpDelegated | `/fast` | Delegated | Sent to omp as typed; omp toggles fast mode. |
| `/opus` | CLI | Model family shortcut: jumps to the model without the picker. | NativeAction | `model.pick opus` | Partial | Filters the model picker by opus and selects the model when exactly one matches. |
| `/composer` | CLI | Model family shortcut, as /opus. | NativeAction | `model.pick composer` | Partial | Filters the model picker by composer and selects the model when exactly one matches. |
| `/branch` | CLI | Alias of /fork. | NativeAction | `chat.fork` | Built | Alias of /fork: forks the whole conversation into a new session. |
| `/duplicate` | CLI | Alias of /fork. | NativeAction | `chat.fork` | Built | Alias of /fork: duplicates the whole conversation into a new session. |
| `/restore` | CLI | Alias of /rewind. | NativeAction | `chat.restore` | Partial | Alias of /rewind: opens the Checkpoints timeline, or previews and confirms restoring the files of turn n. Files only, not the conversation text. |
| `/undo` | CLI | Alias of /rewind. | NativeAction | `chat.undo` | Partial | Previews the files of the latest recorded turn and asks before it puts them back. Files only, not the conversation text. |
| `/compact` | CLI | Alias of /summarize. | OmpDelegated | `/compact` | Delegated | Sent to omp as typed; omp compacts the conversation. |
| `/agent-review` | Chat | On-demand local review, Quick or Deep. | Skill | `SKILL.md named agent-review` | Built | Ships with OmpOS as a built-in skill: a Quick or Deep local review that edits nothing unless asked; a project or user skill of the same name takes over. |
| `/worktree` | Chat | Runs the rest of the chat in a separate checkout. | OmpDelegated | `/worktree (omp /wt)` | Delegated | Sent to omp as typed; omp's /worktree (alias of /wt) moves the session into a new worktree with its changes and takes an optional branch. |
| `/apply-worktree` | Chat | Brings worktree changes into the main checkout. | NativeAction | `worktree.apply` | Built | Applies the changes of a worktree run to the main checkout as a patch against their merge base; 'merge' uses a three-way merge and lists conflicts. Opens the Worktrees panel with the outcome. |
| `/delete-worktree` | Chat | Deletes the isolated checkout. | NativeAction | `worktree.delete` | Built | Asks to delete a worktree run and its checkout; nothing is deleted until you confirm in the Worktrees panel. |
| `/best-of-n` | Chat | Runs one worktree per model and compares the results; never merges. | NativeAction | `worktree.bestofn` | Partial | Runs the task n times, each in its own worktree with the current model, and lists the candidates for you to compare; it never merges. Cursor runs one candidate per model; OmpOS runs n candidates of one model. |
| `/remote-control` | Chat | Hands the local session to a worker on this machine. | Unavailable | - | Unavailable | Needs a Cursor remote worker; OmpOS remote sessions arrive with the remote and cloud phases. |

## Open-ended forms

| Form | What it does | Handler kind | Status |
|---|---|---|---|
| `/<skill-name>` | Runs a skill found in a `skills/<name>/SKILL.md`. A skill with `user-invocable: false` is hidden. | Skill | Built |
| `/<command-name> [arguments]` | Runs a custom command file from a `commands` folder, including `.cursor/commands`. The body is the prompt; `$ARGUMENTS`, `$@`, `$1`, `$@[2]` and `$@[2:3]` are replaced by the typed arguments, and arguments are appended when the body has no placeholder. | OmpDelegated (expanded by OmpOS) | Built |
| `/<subagent-name> <task>` | Hands the task to a subagent defined in an `agents` folder. omp does not read `.cursor/agents`, so OmpOS sends the definition with the task. | OmpDelegated (composed by OmpOS) | Built |
| `& <task>` | The CLI prefix that hands the task to a cloud agent. | CloudHandoff | Partial: needs the API address and a sign-in |

## Automatic discovery of workspace conventions

OmpOS reads commands, skills, rules and subagents from every convention directory, at the workspace root and in the user's home folder, and shows them in the slash palette and on the Settings Commands, Skills and Rules pages (each entry shows its file and scope; files that cannot be used are listed as problems).

Directories, highest precedence first: `.ompos`, `.omp`, `.cursor`, `.claude`, `.codex`, `.agents`. A project item beats a user item; within a scope the order above decides; when two items share a name the winner is kept and the loser is listed as a problem.

| What | Where | Format |
|---|---|---|
| Commands | `<dir>/commands/*.md` | Optional frontmatter `description`, `argument-hint`; the body is the prompt template. |
| Skills | `<dir>/skills/<name>/SKILL.md` (and flat `.ompos/skills/<name>.md`) | Frontmatter `name`, `description`, `paths`, `disable-model-invocation`, `user-invocable`, `icon`, `color`. Nested `<folder>/<dir>/skills` scopes are found below the workspace root (four levels deep at most). |
| Rules | `<dir>/rules/**/*.mdc` and `*.md`; `AGENTS.md`, `CLAUDE.md`, `.cursorrules` | Frontmatter `description`, `globs`, `alwaysApply`. Nested rule folders and nested `AGENTS.md` and `CLAUDE.md` are found too. |
| Subagents | `<dir>/agents/*.md` | Frontmatter `name`, `description`, `model`, `readonly`, `is_background`; the body is the subagent's instructions. |
| Hooks and MCP | `<dir>/hooks.json`, `<dir>/mcp.json`, `.mcp.json` | Listed with path and scope only; the hooks and MCP phases read them. |

Limits: a file larger than 256 KiB, a file that is not UTF-8, frontmatter that cannot be read, and a symbolic link that leaves the workspace root (or the home folder) each produce a problem and no item; discovery never fails because of one bad file. The frontmatter reader is small and bounded (200 lines); YAML anchors, aliases and tags are never interpreted.

Discovery runs when a session starts and again, at most every five seconds, when the slash palette opens. The Settings pages read the files again each time they open. The files are read through `IConventionFileSource`, so the same discovery serves a local disk today and an SSH host or a cloud file tree later.
