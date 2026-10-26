# Cursor slash commands and their OmpOS mapping

Read date for every row: 2026-10-22. Source codes (S1 to S16) resolve to full URLs with the open result in the Sources section. "Opened yes" means the page was fetched and its text read, either in full or by search of the fetched text.

## Verdict

Cursor documents 86 distinct slash tokens across three sources: 44 in the CLI reference table (36 commands plus 8 aliases), 21 in Agent chat and the Agents Window (`/side`, `/btw` and `/in-cloud`, plus 18 built-in skills; the nineteenth built-in, `/shell`, is counted in the CLI group), and 21 that appear only in the CLI changelog (16 commands plus 5 aliases). Inline edit and Tab document no slash commands. Against the OmpOS roadmap (v0.11.0 and v0.12.0) and the ADE harness plan, 20 tokens map to a planned phase (Phases 26, 27, 28, 29, 31 and 32), 66 have no mapping and are new, and none is existing as a Cursor-compatible command. The repository has a generic slash-command recogniser that tags any leading `/name` prompt as a slash-command intent; a search found no handler for any Cursor command, and a side-chat presentation model exists with no `/side` entry point. Many mappings are partial: the roadmap names a feature, not the command that invokes it. Sixteen CLI changelog commands are absent from the current reference table, so their current availability is unverified.

## Table

Columns: ID; syntax as documented; what it does (paraphrased); surface; source code(s); opened; OmpOS status (existing, planned or new); mapping basis (phase and requirement, or none).

### A. CLI reference table (surface: CLI, the terminal agent)

| ID | Syntax | What it does | Surface | Source | Opened | OmpOS status | OmpOS mapping |
|---|---|---|---|---|---|---|---|
| A01 | `/model [filter]` | Opens the model picker; Tab edits the filter | CLI | S1 | yes | new | No model picker in ROADMAP v0.11.0 or v0.12.0 or ADE plan |
| A02 | `/run-everything [on\|off\|status]` | Toggles Run Everything or shows its state | CLI | S1 | yes | planned | P28, ADE-14: auto-run off by default, opt-in only |
| A03 | `/plan [prompt]` | Switches to Plan mode, shows the plan, or submits a prompt in Plan mode | CLI | S1, S16 | yes | new | Built-in modes not in plan; ADE-17 (P28) covers custom modes from files only |
| A04 | `/ask` | Toggles Ask mode (read-only questions) | CLI | S1, S16 | yes | new | As A03 |
| A05 | `/debug [prompt]` | Toggles Debug mode or submits a prompt in it | CLI | S1 | yes | new | As A03 |
| A06 | `/goal [objective]` | Gives the agent a long-lived objective until complete; documented as rolling out | CLI, Agent chat | S1, S2 | yes | new | No roadmap item. Adjacent only: P32 automations are triggers, not durable objectives |
| A07 | `/logs` | Shows the debug log path and copies it | CLI | S1 | yes | new | None |
| A08 | `/update` | Updates the Cursor Agent CLI | CLI | S1 | yes | new | None |
| A09 | `/max-mode` | Toggles Max Mode on legacy request-based plans | CLI | S1 | yes | new | None |
| A10 | `/rename <name>` | Renames the current chat session | CLI | S1 | yes | new | None |
| A11 | `/clear` | Starts a new chat session | CLI | S1 | yes | new | None; side chat (B01) is not a new session |
| A12 | `/resume` | Opens recent chats and resumes one | CLI | S1, S17 | yes | new | None. Repository recogniser tags `/resume` as a slash-command intent but no handler was found |
| A13 | `/fork` | Forks the current chat into a new session | CLI | S1 | yes | new | None |
| A14 | `/summarize` | Summarises the conversation to reduce context | CLI, cloud Agent | S1, S13, S14 | yes | new | None |
| A15 | `/rewind` | Jumps back to a previous message | CLI | S1 | yes | new | None |
| A16 | `/vim` | Toggles Vim keys | CLI | S1 | yes | new | None |
| A17 | `/line-numbers` | Toggles line numbers in code blocks | CLI | S1 | yes | new | ADE-09 (P27) lists editor highlighting and find only |
| A18 | `/show-thinking` | Toggles the thinking block display | CLI | S1 | yes | new | None |
| A19 | `/status-indicators` | Toggles terminal title status indicators | CLI | S1 | yes | new | None |
| A20 | `/shell [command]` | Enters Shell Mode; the same name is also a built-in skill that runs literal text as a shell command | CLI, Agent chat | S1, S4, S18 | yes | planned | P28, ADE-14: approval per command, default-off auto-run. Repository has `TerminalGate.cs`; ADR-0006 is Proposed |
| A21 | `/about` | Shows CLI version, system and account info; copies them | CLI | S1 | yes | new | None |
| A22 | `/setup-terminal` | Configures terminal newline keybindings | CLI | S1 | yes | new | None |
| A23 | `/help [command]` | Shows help, or details for one command | CLI | S1 | yes | new | None |
| A24 | `/feedback <message>` | Sends feedback to the team | CLI | S1 | yes | new | None |
| A25 | `/open` | Opens the repository's Git root in Cursor | CLI | S1 | yes | new | Adjacent: ADE-10 (P27) opens a folder as workspace root; no reverse open |
| A26 | `/copy-request-id` | Copies the last request ID | CLI | S1 | yes | new | None |
| A27 | `/copy-conversation-id` | Copies the conversation ID | CLI | S1 | yes | new | None |
| A28 | `/logout` | Signs out of Cursor | CLI | S1 | yes | new | None; sign-in is OD-03 |
| A29 | `/quit` | Exits | CLI | S1 | yes | new | None |
| A30 | `/exit` | Exits | CLI | S1 | yes | new | None |
| A31 | `/mcp [list\|list-tools] [identifier]` | Manages MCP servers and lists their tools. Changelog also documents `/mcp login` and a `/mcp list` pager | CLI | S1, S13 | yes | planned | P28, ADE-16 (MCP client, tool registry); P33, CX-04 host-owned MCP gateway |
| A32 | `/plugin [subcommand]` | Manages plugins and marketplaces | CLI | S1 | yes | planned | P31, CX-01 manifests and lifecycle. Marketplace install is not in the roadmap |
| A33 | `/config` | Configures CLI settings interactively | CLI | S1 | yes | new | None |
| A34 | `/copy` | Copies a previous user message | CLI | S1 | yes | new | None |
| A35 | `/sandbox` | Configures sandbox mode and network access | CLI | S1, S16 | yes | new | Adjacent: ADE-14 approvals (P28); no sandbox item |
| A36 | `/bedrock [subcommand]` | Configures Bedrock when the feature is enabled | CLI | S1 | yes | new | None |

Aliases from the same table:

| ID | Alias | Of | Surface | Source | Opened | OmpOS status | OmpOS mapping |
|---|---|---|---|---|---|---|---|
| A37 | `/auto-run` | A02 | CLI | S1 | yes | planned | P28, ADE-14 |
| A38 | `/new` | A11 | CLI | S1 | yes | new | None |
| A39 | `/new-chat` | A11 | CLI | S1 | yes | new | None |
| A40 | `/newchat` | A11 | CLI | S1 | yes | new | None |
| A41 | `/compress` | A14 | CLI | S1, S13 | yes | new | None |
| A42 | `/sh` | A20 | CLI | S1 | yes | planned | P28, ADE-14 |
| A43 | `/run` | A20 | CLI | S1 | yes | planned | P28, ADE-14 |
| A44 | `/cursor` | A25 | CLI | S1 | yes | new | None |

### B. Agent chat and Agents Window (IDE and cloud), not in the CLI table

| ID | Syntax | What it does | Surface | Source | Opened | OmpOS status | OmpOS mapping |
|---|---|---|---|---|---|---|---|
| B01 | `/side` or `/side <question>` | Opens a side chat with its own transcript; the parent thread is hidden reference context | Agent chat (local and cloud) | S2, S9, S14 | yes | planned | P26, ADE-04. Repository has `SideChat.cs` and `SideChatPanel.cs` (presentation model) but no `/side` entry point |
| B02 | `/btw` | IDE: alias for a side chat. CLI: a read-only side question whose answer streams to an overlay and does not enter history | Agent chat, CLI | S2, S13 | yes | planned | P26, ADE-04. Semantics differ between IDE and CLI; see owner decision OD-b |
| B03 | `/in-cloud` | Hands a task to a cloud subagent that runs on its own VM and branch | Agents Window | S7, S8 | yes | planned | P26, ADE-07 cloud runs. Handoff command not specified in roadmap |
| B04 | `/automate` | Built-in skill: creates Cursor Automations from schedules, Slack, GitHub and other sources | Agent chat | S4 | yes | planned | P32, CX-03 (partial: triggers only, command not specified) |
| B05 | `/autopilot` | Built-in skill: monitors a PR and addresses feedback, conflicts and failing checks | Agent chat, Agents Window | S4, S8 | yes | new | None |
| B06 | `/canvas` | Built-in skill: creates interactive React artifacts beside the conversation | Agent chat | S4 | yes | new | None |
| B07 | `/create-hook` | Built-in skill: creates hooks and updates `hooks.json` | Agent chat | S4 | yes | planned | P31, CX-02 (partial: hooks with consent; authoring command not specified) |
| B08 | `/create-rule` | Built-in skill: creates rules with scope and instructions | Agent chat | S4 | yes | planned | P28, ADE-17 (partial: loads rule files; authoring not specified) |
| B09 | `/create-skill` | Built-in skill: creates Agent Skills and `SKILL.md` | Agent chat | S4 | yes | planned | P28, ADE-17 (partial) |
| B10 | `/create-subagent` | Built-in skill: creates custom subagents | Agent chat | S4 | yes | new | Subagents are not in the plan; ADE §4 agent modes only |
| B11 | `/cursor-blame` | Built-in skill: investigates AI-authored changes and their prompts | Agent chat | S4 | yes | new | None |
| B12 | `/loop` | Built-in skill: runs a prompt or skill repeatedly at an interval | Agent chat | S2, S4 | yes | planned | P32, CX-03 (partial: schedule trigger, not an interval prompt) |
| B13 | `/migrate-to-skills` | Built-in skill: converts dynamic rules and slash commands into skills (Cursor 2.4+) | Agent chat | S4 | yes | new | None |
| B14 | `/review` | Built-in skill: selects and runs the code-review agent | Agent chat | S4 | yes | new | ADE-11 (P27) is per-hunk accept or reject, not AI review |
| B15 | `/review-bugbot` | Built-in skill: review for bugs and regressions with Bugbot | Agent chat | S4 | yes | new | None |
| B16 | `/review-security` | Built-in skill: review for security vulnerabilities with Security Review | Agent chat | S4 | yes | new | None |
| B17 | `/sdk` | Built-in skill: helps build applications with the Cursor SDK | Agent chat | S4 | yes | new | None |
| B18 | `/split-to-prs` | Built-in skill: splits large changes into smaller PRs | Agent chat | S4 | yes | new | None |
| B19 | `/statusline` | Built-in skill: configures the CLI status line | Agent chat (targets CLI) | S4 | yes | new | None |
| B20 | `/update-cli-config` | Built-in skill: updates `~/.cursor/cli-config.json` | Agent chat (targets CLI) | S4 | yes | new | None |
| B21 | `/update-cursor-settings` | Built-in skill: finds and updates a Cursor or VS Code setting | Agent chat | S4 | yes | new | None |

### C. CLI changelog only (not in the current reference table)

| ID | Syntax | What it does (changelog wording, paraphrased) | Surface | Source | Opened | OmpOS status | OmpOS mapping |
|---|---|---|---|---|---|---|---|
| C01 | `/add-dir <path>` | Adds a directory mid-session; refreshes skills and custom commands (v2026.06.29) | CLI | S13 | yes | new | Adjacent: ADE-10 (P27) single workspace root |
| C02 | `/auto-review` | Named in the Auto-review run-mode entry (v2026.06.22); the command's behaviour is not stated | CLI | S13 | yes | new | Not in ADE-14 or ADE-02 |
| C03 | `/changes` (Ctrl+R) | Unified review UI with a Session tab showing this session's edits (February 2026) | CLI | S13 | yes | planned | P27, ADE-11 (partial: per-hunk accept and reject) |
| C04 | `/commands` | Interactive browser for custom commands, with inline editing (January 2026) | CLI | S13 | yes | new | Custom commands not in plan; ADE §4 "Commands" are palette actions (P20 contract) |
| C05 | `/context` | Shows what fills the context window, by category (v2026.06.09) | CLI | S13 | yes | new | None |
| C06 | `/detach` | Detaches from a persistent session started with `agent persist` (v2026.08.26) | CLI | S13 | yes | new | Adjacent: ADE-08 remote control (P26); not the same |
| C07 | `/full-conversation` | Opts out of recent-turns-only redraw (May 14, 2026) | CLI | S13 | yes | new | None |
| C08 | `/load-workspace` | Reloads a saved multi-directory workspace (v2026.06.22) | CLI | S13 | yes | new | Adjacent: ADE-10 |
| C09 | `/save-workspace` | Saves the current multi-directory set (v2026.06.22) | CLI | S13 | yes | new | Adjacent: ADE-10 |
| C10 | `/rules` | Interactive browser for rules, with inline editing (January 2026) | CLI | S13 | yes | planned | P28, ADE-17 (partial) |
| C11 | `/skills` | Browses skills; flags those that break Agent Skills limits (v2026.10.08; browser introduced January 2026) | CLI | S13 | yes | planned | P28, ADE-17 (partial) |
| C12 | `/usage` | Shows plan usage meters, on-demand spend and billing reset (v2026.07.13) | CLI | S13 | yes | planned | P29, ADE-20 (partial: per-run usage history, not account meters) |
| C13 | `/plugins` | Shows plugin details faster, with each plugin's MCP servers linked into `/mcp` (May 20, 2026) | CLI | S13 | yes | planned | P31, CX-01 (partial, as A32) |
| C14 | `/fast` | Toggles Fast when the current model supports it (v2026.07.06) | CLI | S13 | yes | new | No model switch in plan |
| C15 | `/opus` | Model family shortcut; jumps to the model without the picker (v2026.07.06) | CLI | S13 | yes | new | As A01 |
| C16 | `/composer` | Model family shortcut, as C15 (v2026.07.06) | CLI | S13 | yes | new | As A01 |

Aliases from the changelog:

| ID | Alias | Of | Surface | Source | Opened | OmpOS status | OmpOS mapping |
|---|---|---|---|---|---|---|---|
| C17 | `/branch` | A13 | CLI | S13 | yes | new | None |
| C18 | `/duplicate` | A13 | CLI | S13 | yes | new | None |
| C19 | `/restore` | A15 | CLI | S13 | yes | new | None |
| C20 | `/undo` | A15 | CLI | S13 | yes | new | None |
| C21 | `/compact` | A14 | CLI, cloud Agent | S13, S14 | yes | new | None |

Totals: 36 primary commands plus 8 aliases in A (44 tokens); 21 tokens in B; 16 commands plus 5 aliases in C (21 tokens). Total 86 distinct tokens. Status: 20 planned (A02, A20, A31, A32, A37, A42, A43, B01, B02, B03, B04, B07, B08, B09, B12, C03, C10, C11, C12, C13); 66 new; 0 existing.

Not counted as commands: `/skill-name` (placeholder for any skill; S4, S13); `/mcp login` and `/mcp list` (subcommands; S13); `/plan <prompt>` (A03 form); `/model sonnet` (A01 form; S13); `/tmp` (an example path in S13, not a command); `&` prefix that hands a message to a Cloud Agent in the CLI (S16); `@` mentions in Agent chat (S3).

Surface results: inline edit (S10) documents Cmd or Ctrl+K, an Opt or Alt+Return question mode, typed "do it", and Cmd or Ctrl+L to switch to Agent; it documents zero slash commands. Tab (S11) documents accept, reject and word-by-word keys; zero slash commands. The Agent `chat` surface is documented as the chat input (S2, S3); no separate `chat` commands page exists (see Unverified).

### User-defined command mechanisms (as the docs state them)

| ID | Mechanism | Location (as documented) | Format (as documented) | Source | Opened | OmpOS status | OmpOS mapping |
|---|---|---|---|---|---|---|---|
| U01 | Agent Skills | Project: `.agents/skills/`, `.cursor/skills/`, and nested project folders. User: `~/.agents/skills/`, `~/.cursor/skills/`. Compatibility: `.claude/skills/`, `.codex/skills/`, `~/.claude/skills/`, `~/.codex/skills/` | A folder containing `SKILL.md` with YAML frontmatter: `name` (required; lowercase, digits, hyphens; must match the folder), `description` (required), optional `paths`, `disable-model-invocation`, `icon`, `color`, `metadata`. `user-invocable: false` hides a skill from `/` autocomplete (release notes, v2026.07.06) | S4, S3, S14 | yes | planned | P28, ADE-17: rule, skill and mode files load from the project and reload on change |
| U02 | Commands (Agent chat `/` commands) | Plugin: `commands/` folder inside a plugin. User and workspace command files: location not stated in any page opened | Plugin: `.md`, `.mdc`, `.markdown` or `.txt` with YAML frontmatter `name` (kebab-case) and `description`. Plugin manifest field `commands` takes a path or list of paths | S5, S6 | yes | new | Not in plan. Owner decision OD-e |
| U03 | Plugin manifest | `.cursor-plugin/plugin.json` | JSON manifest; `commands`, `skills`, `rules`, `agents` take paths | S6 | yes | planned | P31, CX-01 manifest validation |
| U04 | Custom Modes | Any skill used with Option+Enter or Alt+Enter | Skill frontmatter `icon` and `color` style the mode badge | S3, S4 | yes | planned | P28, ADE-17 (agent modes from project files) |
| U05 | Migration | `/migrate-to-skills` converts dynamic rules and commands to skills with `disable-model-invocation: true` | Skill format | S4 | yes | new | None |

## Owner decisions

OD-a. Command naming. Should OmpOS mirror Cursor's `/` names, pass through omp's own command list, or do both? Options: (1) Cursor-compatible names; (2) omp's native commands unchanged (a recorded RPC frame fixture in the repository shows omp advertising its commands through `available_commands_update`, for example `/security`); (3) both, with a namespace prefix for one of the two.

OD-b. `/btw` meaning. Options: (1) side chat as in the IDE, under ADE-04; (2) an ephemeral read-only overlay as in the CLI; (3) both, under two names.

OD-c. Run mode naming and scope. `/run-everything` and `/auto-run` map to ADE-14. `/auto-review` is not in the plan. Options: (1) add Auto-review to ADE-14; (2) defer it; (3) exclude it.

OD-d. Cursor-hosted built-in skills (`/in-cloud`, `/autopilot`, `/automate`, `/review-bugbot`, `/review-security`) depend on Cursor services. Options: (1) build OmpOS equivalents on the hosted API (P26 or P32); (2) exclude them; (3) defer until the OD-03 sign-in decision is made.

OD-e. Custom command files. Cursor documents commands only inside plugins (`commands/`), and the user and workspace locations are not stated in the pages read. Options: (1) adopt the skill format only; (2) also adopt the `commands/` format; (3) defer until Cursor documents the user and workspace locations.

OD-f. Built-in modes (`/plan`, `/ask`, `/debug`) and `/goal` have no roadmap item. Options: (1) add them to ADE-17 (P28) or a new phase; (2) keep them as backlog without a phase; (3) exclude them.

OD-g. The repository already has a side-chat presentation model ahead of Phase 26. Options: (1) keep it and wire `/side` in Phase 26; (2) remove it until Phase 26; (3) keep it and record it as partial ADE-04 work.

OD-h. Model shortcuts (`/model`, `/opus`, `/composer`, `/fast`, `/max-mode`). No model picker is in the roadmap. Options: (1) add a model-selection item to a phase; (2) defer; (3) exclude.

## Unverified

1. User and workspace custom command file locations and formats (U02). The docs call them commands but do not give a path.
2. Sixteen changelog-only commands (C01 to C16) are absent from the current CLI reference table, and so are their aliases C17 to C21. Their present availability is unverified: the changelog is the only source.
3. `/goal` availability: documented as rolling out and gated (S1, S2, S13). Its behaviour in the IDE is unverified.
4. `/in-cloud` and `/autopilot`: each has one or two sentences of documentation (S7, S8). Their behaviour is unverified beyond those sentences.
5. Built-in skill behaviour: the 19 entries in S4 have one-line descriptions only. Their behaviour is unverified.
6. `/compact` in the IDE: only cloud-agent and CLI evidence was found (S13, S14). IDE behaviour unverified.
7. The `chat` surface as a separate command surface: `https://cursor.com/docs/agent/chat/commands.md` returned HTTP 404. A first fetch of `https://cursor.com/docs/agent/chat/commands` returned the Skills help page instead. No dedicated chat-commands page was found.
8. Negative results for inline edit and Tab rest on the two short help pages (S10, S11). Undocumented commands may exist.
9. Cursor changelog index (S15): only the first page of entries (October 2026) was in the fetched HTML. Entries such as `/changelog/08-19-26`, `/changelog/remote-control-local-agents` and `/changelog/design-mode-improvements`, which the ADE plan cites, were not opened.
10. ADE plan discrepancy: ADE-04 cites a "3.11" changelog index entry for `/side`, `/btw` and transcript search. The release notes I searched (S14) place side chats at 3.23 (cloud), and no "3.11" entry was found. The cited entry was not located.
11. `/update`, `/logs`, `/about` and the other A-group rows are verified only by the one-line description in S1. Behaviour is unverified.
12. Repository evidence is limited to `git grep` and reading single files. No build, test or run was performed, as instructed.

## Sources

Opened on 2026-10-22 unless noted.

| Code | URL | Opened | Note |
|---|---|---|---|
| S1 | https://cursor.com/docs/cli/reference/slash-commands.md | yes | CLI slash-command table, 36 rows and 8 aliases |
| S2 | https://cursor.com/docs/agent/overview.md | yes | Side chats, `/goal`, `/loop`, queued messages, checkpoints |
| S3 | https://cursor.com/docs/agent/prompting.md | yes | `@` mentions; `/` invokes a skill; Custom Modes |
| S4 | https://cursor.com/docs/skills.md | yes | 19 built-in skills; skill locations; frontmatter table |
| S5 | https://cursor.com/docs/customize-cursor.md | yes | Commands defined as markdown files invoked with `/` |
| S6 | https://cursor.com/docs/reference/plugins.md | yes | Plugin `commands/` folder; command frontmatter; manifest fields |
| S7 | https://cursor.com/docs/agent/agents-window.md | yes | `/in-cloud` (one sentence) |
| S8 | https://cursor.com/docs/subagents.md | yes | `/autopilot` on a PR (one sentence) |
| S9 | https://cursor.com/help/ai-features/side-chats.md | yes | `/side` opens an empty side chat or sends a question |
| S10 | https://cursor.com/help/ai-features/inline-edit.md | yes | Inline edit keys; no slash commands |
| S11 | https://cursor.com/help/ai-features/tab.md | yes | Tab keys; no slash commands |
| S12 | https://cursor.com/help/customization/keyboard-shortcuts.md | yes | Shortcut table |
| S13 | https://cursor.com/docs/cli/changelog.md | yes | CLI changelog; source of C-group commands and aliases |
| S14 | https://cursor.com/docs/release-notes.md | yes (fetched; searched by pattern, not read line by line) | Release notes; side chats, `/summarize`, `/compact` on cloud agents, `user-invocable` |
| S15 | https://cursor.com/changelog | yes (first page only) | Changelog index, October 2026 entries |
| S16 | https://cursor.com/docs/cli/overview.md | yes | CLI modes; `/plan`, `/ask`, `&` cloud handoff |
| S17 | https://cursor.com/docs/cli/using.md | yes | `/resume` mention |
| S18 | https://cursor.com/docs/cli/shell-mode.md | yes | Shell Mode limits |
| S19 | https://cursor.com/llms.txt | yes | Documentation index |
| S20 | https://cursor.com/docs/agent/chat/commands.md | no, HTTP 404 | Dedicated chat-commands page not found |
| S21 | https://cursor.com/docs/inline-edit/overview.md | no, HTTP 404 | Used the help page (S10) instead |
| S22 | https://cursor.com/docs/cli/reference/commands.md | no, HTTP 404 | Not found |
| OmpOS-1 | /root/src/repos/ompos/.planning/ROADMAP.md, lines 480-554 (v0.11.0) and 556-619 (v0.12.0) | read | Phases 26 to 35 |
| OmpOS-2 | /root/src/repos/ompos/docs/plans/ade-harness-plan.md | read | ADE-01 to ADE-22; sections 2 to 6 |
| OmpOS-3 | /root/src/repos/ompos/src/OmpOS.Parser/Recognizers/SlashCommandRecognizer.cs | read | Tags any leading `/name args` as SlashCommand; a search found no handler for Cursor commands |
| OmpOS-4 | /root/src/repos/ompos/src/OmpOS.Presentation/Chats/SideChat.cs and SideChatPanel.cs | read | Side-chat model; no `/side` parser |
| OmpOS-5 | /root/src/repos/ompos/docs/decisions/ADR-0006-terminal-control.md | read | Status Proposed, 2026-10-22 |
| OmpOS-6 | /root/src/repos/ompos/tests/OmpOS.Harness.Rpc.Tests/RpcPromptStreamTests.cs:53 | grep | omp answers its own local `/help` (agentInvoked false) |
