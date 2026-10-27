# R-CURSOR-SETTINGS: Cursor settings, slash commands and shortcuts, diffed against the repository research

Research date: 2026-10-27. Read-only. Every URL below was opened (read tool) on that date unless marked 'not opened'. Current Cursor app version in the primary release notes: 3.27 (https://cursor.com/docs/release-notes.md); current CLI version in the changelog: v2026.10.22 (https://cursor.com/docs/cli/changelog.md).
Baseline read first: docs/plans/vision-research/cursor-settings-window.md (34 rows, 2026-10-22), docs/plans/vision-research/cursor-slash-commands.md (86 tokens: A01-A44, B01-B21, C01-C21), docs/settings-parity.md (34 rows, 11 built pages).
Limits: no primary page enumerates the desktop Settings window; settings below come from the pages that name them. cursor.* keys found in primary docs: only cursor.worktreeCleanupIntervalHours and cursor.worktreeMaxCount (already repo row 25); also workbench.reduceMotion (new, release notes 3.27), security.workspace.trust.enabled and extensions.installCooldownHours (already rows 20, 29). Not opened: https://cursor.com/docs/customizing/aws-bedrock.md, https://cursor.com/docs/enterprise/network-configuration.md, https://cursor.com/docs/reference/third-party-hooks.md, https://cursor.com/docs/cloud-agent/security-network.md. Changelog-only slash tokens (C group) were checked by pattern search of the CLI changelog, not read line by line.
OmpOS fit: H = head UI / settings window; E = engine (omp config); P = plugin or hook layer; X = hosted API / cloud; N = not applicable (Cursor account, billing, Cursor-hosted product). [INFERENCE] marks my guess.

## Table 1. Settings not in the repository's 34 rows, or whose documented behaviour changed

| ID | Page / where | Option | Values and default | What it does | Primary URL | OmpOS fit |
|---|---|---|---|---|---|---|
| S01 | CLI cli-config.json (global ~/.cursor/cli-config.json; project <project>/.cursor/cli.json takes permissions only; env CURSOR_CONFIG_DIR, XDG_CONFIG_HOME) | version | number, current 1 | Schema version | https://cursor.com/docs/cli/reference/configuration.md | E |
| S02 | cli-config.json | editor.vimMode | bool, false | Vim keys in the prompt | same | H |
| S03 | cli-config.json | permissions.allow / permissions.deny | string[]; tokens Shell(cmd), Read(glob), Write(glob), WebFetch(domain), Mcp(server:tool); deny beats allow | Permission lists | https://cursor.com/docs/cli/reference/permissions.md | E/H (repo row 3 names only Run Mode) |
| S04 | cli-config.json | approvalMode | allowlist, auto-review, unrestricted | CLI form of Run Mode | configuration.md | H |
| S05 | cli-config.json | sandbox.mode, sandbox.networkAccess, sandbox.readBoundary | readBoundary: system (default) or workspace | Sandbox overrides | configuration.md | E |
| S06 | cli-config.json | network.useHttp1ForAgent | bool, false | HTTP/1.1 + SSE for proxies (CLI form of HTTP Compatibility Mode, repo row 18) | configuration.md | H (repo says the HTTP switch is not built) |
| S07 | cli-config.json | attribution.attributeCommitsToAgent, attribution.attributePRsToAgent | bool, true each | Made-with-Cursor trailer on commits / footer on PRs | configuration.md | H/P [INFERENCE: OmpOS needs its own text] |
| S08 | cli-config.json | notifications, hints, suggestNextPrompt, rewind | bool each; rewind enables /rewind and the /undo /restore aliases | Terminal notification, hints, next-prompt suggestion, rewind | configuration.md ; https://cursor.com/docs/cli/changelog.md | H |
| S09 | cli-config.json | display.showLineNumbers, display.showThinkingBlocks, display.showStatusIndicators, display.showStatusLineRunningTime | bool each | Display toggles (match /line-numbers, /show-thinking, /status-indicators) | configuration.md | H |
| S10 | cli-config.json | model, maxMode, channel, hasChangedDefaultModel; statusLine (changelog) | object / bool / string | Model, persisted Max Mode, update channel, custom status-line command | configuration.md ; changelog.md | E |
| S11 | CLI /config | Model slash commands toggle | on by default | Disables /opus /composer family shortcuts | https://cursor.com/docs/cli/changelog.md (v2026.07.06) | H |
| S12 | CLI proxy | HTTP_PROXY, HTTPS_PROXY, NODE_USE_ENV_PROXY=1, NODE_EXTRA_CA_CERTS | env vars | Corporate proxy and CA trust | configuration.md | H (Network page note) |
| S13 | Settings > Agents > Approvals & Execution (changed vs repo rows 2, 3) | Auto-review classifier model | Gemini 3.5 Flash Lite, Haiku fallback; Claude 4.5 Haiku must stay allowed | Run Mode classifier; repo row 2 names only Haiku | https://cursor.com/docs/agent/security/run-modes.md | N [INFERENCE] |
| S14 | permissions.json (~/.cursor/permissions.json and <project>/.cursor/permissions.json; merged; team dashboard config overrides both) | autoRun.allow_instructions[], autoRun.block_instructions[] | plain-English sentences | Steers Auto-review (repo row 3 names custom instructions, not the file, keys or merge rule) | run-modes.md | E/H |
| S15 | sandbox.json (~/.cursor and <project>/.cursor; project wins; replaces the Settings value) | readBoundary (system/workspace), additionalReadPaths[] (folders, globs, ~) | | Per-machine or per-project read boundary; Settings shows which file controls the value | run-modes.md | E |
| S16 | Team dashboard: Security & automation (also per organization group) | Read Controls | System (member choice), Workspace (forced) | Admin read policy | run-modes.md | N |
| S17 | Run Modes history | Mode names | 3.6+: Auto-review (default), Allowlist, Run Everything. Before 3.5: Run in Sandbox, Ask Every Time, Run Everything; Ask Every Time deprecated in 3.5, Run in Sandbox became Allowlist + sandbox | Rename | https://cursor.com/help/ai-features/terminal.md | H |
| S18 | Settings > Agents > Remote Control (Agents Window only; Cursor 3.9.8+) | Enable Remote Control; Keep this computer awake | toggles; needs admin enable (Dashboard > Cloud Agents > Self-Hosted) and cloud data storage allowed | Hands a local session to a cloud loop, tools stay on your machine; /remote-control | https://cursor.com/docs/cloud-agent/mobile.md | X/H (primary source for repo row 28) |
| S19 | Settings > Git & PRs > Pull Requests (3.11+; before that Settings > Agents) | Agent Review | automatic after commit or manual; depth Quick or Deep; reads BUGBOT.md | Local review of working changes; /agent-review | https://cursor.com/docs/agent/agent-review.md | H/P |
| S20 | Browser toolbar, Agents Window | Annotate (Design Mode), Batch edits switch | Batch edits off by default; Cmd+Shift+D; needs 3.24.18+ | Point-and-note UI edits with element details and screenshot; Dictate; CSS inspector | https://cursor.com/docs/agent/design-mode.md | H (repo ADE-15 lists no settings) |
| S21 | Model picker > Auto > Optimize For | Cost, Balance, Intelligence | Cost = old Auto; Router on Teams/Enterprise only | Cursor Router mode; SDK id auto-smart, param optimize_for (cost, balanced, intelligence) | https://cursor.com/docs/cursor-router.md | N (Cursor inference) [INFERENCE] |
| S22 | Team dashboard (Router) | Enable Cursor Router; Routing preferences (disable up to 2 modes); Underlying model (hidden default); Impose Auto (Soft or Hard, off by default) | | Admin controls for Auto | cursor-router.md | N |
| S23 | Skills > Custom Modes | Use as Mode (Option+Enter or Alt+Enter on a / skill); frontmatter icon, color (default, green, cyan, blue, purple, magenta, orange, yellow, red, brand) | | Skill stays in context every turn until exited; Agents Window and CLI only | https://cursor.com/docs/agent/prompting.md ; https://cursor.com/docs/skills.md | H/E (ADE-17 covers custom modes from files) |
| S24 | SKILL.md frontmatter | paths (glob list or comma string; legacy globs), disable-model-invocation, metadata; changelog adds user-invocable: false | | Scope to files; slash-only; hide from / | skills.md ; changelog.md | E/H |
| S25 | Skill discovery | .agents/skills/, .cursor/skills/, ~/.agents/skills/, ~/.cursor/skills/, compatibility .claude/skills/, .codex/skills/, ~/.claude/skills/, ~/.codex/skills/; nested .cursor/skills scoped to its folder | | ~/.agents/skills is not copied to cloud agents, remote SSH or self-hosted workers | skills.md | H (repo reads only .ompos/skills) |
| S26 | Rules | Frontmatter alwaysApply, description, globs; .mdc required (plain .md ignored); 4 types; nested AGENTS.md applies when the agent edits in a subdirectory (CLI v2026.10.22) | | Rule matrix | https://cursor.com/docs/rules.md ; changelog.md | H (repo row 7 has types, not the matrix) |
| S27 | Subagents frontmatter | name, description, model (inherit default), readonly (false), is_background (false); falls back to parent model if the pinned model is blocked (v2026.10.22) | | Custom subagents; /name invokes one; /in-cloud for cloud subagents | https://cursor.com/docs/subagents.md | H/E |
| S28 | hooks.json (~/.cursor/hooks.json, <project>/.cursor/hooks.json; auto-reload; PowerShell on Windows) | Hook entry fields | type command or prompt (command); timeout (platform default); loop_limit (5, null = no cap); failClosed (false); matcher regex | Per-hook options; exit code 2 denies | https://cursor.com/docs/hooks.md | P (repo row 12 lists no fields) |
| S29 | hooks.json events | sessionStart, sessionEnd, preToolUse, postToolUse, postToolUseFailure, subagentStart, subagentStop, beforeShellExecution, afterShellExecution, beforeMCPExecution, afterMCPExecution, beforeReadFile, afterFileEdit, beforeSubmitPrompt, preCompact, stop, afterAgentResponse, afterAgentThought, beforeTabFileRead, afterTabFileEdit, workspaceOpen (21) | | Cloud agents run command hooks only and 14 of them (not sessionStart/End, MCP before/after, Tab hooks, workspaceOpen); user hooks not available in cloud; parent_agent_id added to subagent hooks in 3.27 | hooks.md ; release-notes.md | P |
| S30 | mcp.json (.cursor/mcp.json, ~/.cursor/mcp.json) | command, args, env, envFile (stdio only), url, headers, auth.CLIENT_ID / auth.CLIENT_SECRET; interpolation ${env:NAME}, ${userHome}, ${workspaceFolder}, ${workspaceFolderBasename}, ${pathSeparator}, ${/} | | MCP server config | https://cursor.com/docs/mcp.md | E/H |
| S31 | Customize (rules, skills, commands, hooks) | Per-scope activation (user, workspace, team); rule toggle Always / Agent Decides / Manual | | One page for all extension types | https://cursor.com/docs/customize-cursor.md | H |
| S32 | Settings (macOS menu bar) | Show Count (unread agent count by the menu bar icon) | on by default | Menu bar badge | https://cursor.com/docs/release-notes.md (3.27) | H [INFERENCE: tray badge] |
| S33 | Settings (appearance) | workbench.reduceMotion | on/off | Reduce Motion in Cursor's own UI even when the OS preference is off | release-notes.md (3.27) | H (OmpOS has Motion follow system/reduce/full) |
| S34 | Settings (Agents Window) | Chat Width | value type not documented | Chat column width; bubbles scale with it | release-notes.md (3.27) | H |
| S35 | Settings (Agents Window) | New-agent default environment | Last Used (named; other values not documented) | Environment for New Chat | release-notes.md (3.27) | H/X |
| S36 | Chat input | Queue vs steer | Return queues by default; Send now or Return twice steers at next tool call; Tab queues after the turn (CLI: Enter steers at a safe boundary, Enter again interrupts) | Follow-up delivery | https://cursor.com/docs/agent/overview.md | H |
| S37 | Agents Window | Conversation search | Cmd/Ctrl+K palette across chats; Cmd/Ctrl+F in chat; local index | Cross-chat search | https://cursor.com/help/ai-features/conversation-search.md | H |
| S38 | Chat input context ring | Context usage tray | System prompt, Tools, Rules, Skills, MCP, Subagents, Summarized conversation, Conversation | Context breakdown | https://cursor.com/docs/agent/prompting.md | H |
| S39 | Settings > Models (changed vs repo row 1) | BYOK rules | OpenAI (non-reasoning chat models), Anthropic, Google, Azure OpenAI, AWS Bedrock (keys or IAM role via dashboard); chat only, not Tab; Teams/Enterprise still pay the Cursor Token Rate of 0.25 USD per 1M tokens on eligible models; ZDR does not apply to own keys | Key handling | https://cursor.com/help/models-and-usage/api-keys.md | H |
| S40 | Settings > General > Privacy Mode (changed) | Privacy Mode (Legacy) | blocks Projects, Cloud Agents and Remote Control (they store code in the cloud) | Cloud features depend on privacy mode | https://cursor.com/docs/agent/projects.md ; https://cursor.com/docs/cloud-agent/mobile.md | H |
| S41 | Cloud dashboard / Origin settings (changed vs repo row 24) | Never open PRs as drafts; Cloud Agents open PRs ready for review by default (Oct 2026) | | PR draft policy | release-notes.md (Week of Oct 19, 2026) | X |
| S42 | Cloud Agents dashboard (beyond repo row 24) | Environments: Update with Agent, New Setup Run, Restore, Builds tab (pin active build, trigger, start from build); CLI agent env deploy --env <id> | | Environment lifecycle and build pinning | https://cursor.com/docs/cloud-agent/settings.md ; changelog.md | X |
| S43 | Self-hosted workers | agent worker start; --worker-dir, --management-addr (/healthz /readyz /metrics), --label, --labels-file, --idle-release-timeout; workers run under the team chosen at agent login or agent team switch | | Worker settings | https://cursor.com/docs/cli/reference/parameters.md ; changelog.md | X (owner's remote-environments direction) |
| S44 | Bugbot team settings | Public repositories (auto-review of outside contributors; new teams start off) | | Bugbot scope | release-notes.md (Week of Oct 19, 2026) | N |
| S45 | Tab | Settings > Tab; status-bar Snooze, Disable globally, Disable per extension; remap Accept Cursor Tab Suggestions | | Unchanged vs repo row 17 | https://cursor.com/help/ai-features/tab.md | H |
| S46 | Terminal tool | CURSOR_AGENT env var; Linux sandbox maps UID to 0; CURSOR_ORIG_UID, CURSOR_ORIG_GID hold host IDs | | Shell prompt hook, sandbox identity | https://cursor.com/docs/agent/tools/terminal.md ; run-modes.md | E |
| S47 | Indexing & Docs (confirms repo rows 14, 15) | Index New Folders toggle removed; network diagnostic no longer checks Codebase Indexing | | Nothing to build | release-notes.md | N |
| S48 | Shortcut docs (changed vs repo row 21) | Inline edit help page https://cursor.com/help/ai-features/inline-edit.md returns HTTP 404; default tables have no Cmd+K inline edit, Cmd+K toggles the sidepanel | | Repo row 21 lists Cmd+K inline edit as an AI default: stale | https://cursor.com/docs/reference/keyboard-shortcuts.md | H (fix OmpOS shortcut table) |
| S49 | Grok Bot app (separate Cursor product, not the editor) | Panes General, Computer, Usage & Billing, Updates; Appearance; Auto-review switch + Custom Rules; Execution on this computer; Route traffic through this computer | | Not in repo | https://cursor.com/docs/grok-bot/settings.md | N |
| S50 | CLI updates | agent update, --disable-auto-update, channel | | Update control | changelog.md | N |

## Table 2. Slash tokens that exist today

Re-opened: https://cursor.com/docs/cli/reference/slash-commands.md (36 commands + 8 aliases, identical to repo A01-A44), https://cursor.com/docs/skills.md (19 built-in skills, identical to repo B04-B21 plus /shell), https://cursor.com/docs/agent/overview.md (/side /btw /goal), https://cursor.com/docs/agent/agents-window.md (/in-cloud /autopilot), CLI changelog, release notes, and the pages named per row.

### 2a. Missing from cursor-slash-commands.md (6)

| Token | Where | Status | Primary URL | Note |
|---|---|---|---|---|
| /agent-review | Agents Window input | NEW | https://cursor.com/docs/agent/agent-review.md | On-demand local review, Quick or Deep; other starts: auto after commit, Source Control tab |
| /worktree | IDE chat | NEW (repo row 25 mentions it only as prose) | https://cursor.com/docs/configuration/worktrees.md | Runs the rest of the chat in a separate checkout |
| /apply-worktree | IDE chat | NEW | worktrees.md | Brings worktree changes into the main checkout |
| /delete-worktree | IDE chat | NEW | worktrees.md | Deletes the isolated checkout |
| /best-of-n | IDE chat | NEW | worktrees.md | /best-of-n sonnet,gpt,composer <task>; one worktree per model; compares only, never merges |
| /remote-control | Agents Window input | NEW | https://cursor.com/docs/cloud-agent/mobile.md | Hands the local session to a worker on this machine; needs 3.9.8+ and the Settings > Agents toggle |

Open-ended, not counted: /skill-name for any skill, /<custom-command> from markdown command files, /<subagent-name> <task> (example /verifier, https://cursor.com/docs/subagents.md), and the & prefix in the CLI (hands the live conversation to a Cloud Agent, https://cursor.com/docs/cli/overview.md).

### 2b. Confirmed unchanged (86)

| Group | Tokens | Status |
|---|---|---|
| A, CLI table (44) | /model /run-everything /plan /ask /debug /goal /logs /update /max-mode /rename /clear /resume /fork /summarize /rewind /vim /line-numbers /show-thinking /status-indicators /shell /about /setup-terminal /help /feedback /open /copy-request-id /copy-conversation-id /logout /quit /exit /mcp /plugin /config /copy /sandbox /bedrock; aliases /auto-run /new /new-chat /newchat /compress /sh /run /cursor | present in the current table; /goal still Rolling out |
| B (21) | /side /btw /in-cloud /automate /autopilot /canvas /create-hook /create-rule /create-skill /create-subagent /cursor-blame /loop /migrate-to-skills /review /review-bugbot /review-security /sdk /split-to-prs /statusline /update-cli-config /update-cursor-settings | present |
| C, changelog (21) | /add-dir /auto-review /changes /commands /context /detach /full-conversation /load-workspace /save-workspace /rules /skills /usage /plugins /fast /opus /composer; aliases /branch /duplicate /restore /undo /compact | re-seen in changelog: /skills /usage /fast /opus /composer /add-dir /detach /changes /commands /rules /plugins /undo /restore /context /btw; not re-seen by pattern: /branch /duplicate /compact /full-conversation /load-workspace /save-workspace /auto-review (older entries; no removal notice found) |

### 2c. Removed or renamed

| Item | Finding | URL |
|---|---|---|
| Removed or renamed tokens | None found. No repo token is absent from today's pages | https://cursor.com/docs/cli/reference/slash-commands.md |
| /side on cloud agents | Help page says local-only; release notes say side chats now work on cloud agents via /side. Treat as supported | https://cursor.com/help/ai-features/side-chats.md ; https://cursor.com/docs/release-notes.md |
| Mode commands | /ask <prompt> and /plan <prompt> now send the prompt after switching mode in the Agents Window | https://cursor.com/docs/release-notes.md |
| Custom modes | / skill + Enter attaches to one message; Option/Alt+Enter or Use as Mode keeps it active | https://cursor.com/docs/agent/prompting.md |

## Table 3. Default shortcuts (Mac keys; Ctrl on Windows/Linux)

Sources: https://cursor.com/docs/reference/keyboard-shortcuts.md (R), https://cursor.com/help/customization/keyboard-shortcuts.md, https://cursor.com/docs/agent/agents-window.md, https://cursor.com/docs/agent/design-mode.md, https://cursor.com/help/ai-features/side-chats.md, https://cursor.com/help/ai-features/conversation-search.md, https://cursor.com/docs/cli/using.md. Remap with Cmd+R then Cmd+S.

| Scope | Key | Action | URL |
|---|---|---|---|
| General | Cmd+I | Toggle Sidepanel (unless bound to a mode) | R |
| General | Cmd+L or Cmd+K | Toggle Sidepanel | R |
| General | Cmd+E | Toggle Agent layout | R |
| General | Cmd+. | Mode Menu | R |
| General | Cmd+/ | Loop between AI models | R |
| General | Cmd+Shift+J | Cursor settings | R |
| General | Cmd+Shift+Space | Toggle Voice Mode | R |
| General | Cmd+, | General settings | R |
| General | Cmd+Shift+P | Command palette (Open Agents Window / Open IDE) | R ; agents-window.md |
| Agents Window | Cmd+P, Cmd+Shift+F | Search files, search all files | agents-window.md |
| Agents Window | Cmd+K | Command palette, search across conversations | https://cursor.com/docs/agent/overview.md |
| Agents Window | Cmd+F | Find in open conversation | overview.md |
| Agents Window | Shift+Cmd+S | Populate a side chat from the transcript selection | side-chats.md |
| Agents Window | Cmd+Shift+D | Toggle Design Mode (browser focused) | design-mode.md |
| Design Mode | Arrow Up/Down | Highlight parent/child element | design-mode.md |
| Design Mode | Shift+click | Multi-select | design-mode.md |
| Design Mode | Option+click | Add element to chat input | design-mode.md |
| Design Mode | Cmd+L | Add note or text edit to chat input | design-mode.md |
| Design Mode | Cmd+Z, Esc, Return | Undo markup, cancel/exit, send/save note | design-mode.md |
| Chat | Return | Queue message (default) | R |
| Chat | Cmd+Return (typing) | Force send | R |
| Chat | Cmd+Shift+Backspace | Cancel generation | R |
| Chat | Cmd+Shift+L (code selected) | Add selection as context | R |
| Chat | Cmd+V, Cmd+Shift+V (clipboard code/log) | Add clipboard as context, or to input box | R |
| Chat | Cmd+Return with suggested changes | Accept all changes | R |
| Chat | Cmd+Shift+Backspace with suggested changes | Reject all changes | R |
| Chat | Tab | Cycle to next message; queue message for after the turn while the agent works | R ; overview.md |
| Chat | Shift+Tab | Rotate Agent modes | R |
| Chat | Cmd+Opt+/ | Model toggle | R |
| Chat | Cmd+N or Cmd+R | New chat | R |
| Chat | Cmd+T | New chat tab | R |
| Chat | Cmd+[ , Cmd+] | Previous, next chat | R |
| Chat | Cmd+W | Close chat | R |
| Chat | Escape | Unfocus field | R |
| Chat | Option+Enter (Alt+Enter) on a / skill | Use skill as Custom Mode | prompting.md |
| Code context | @ | @-mentions | R |
| Code context | / | Shortcut commands | R |
| Code context | Cmd+Shift+L | Add selection to Chat | R |
| Code context | Cmd+L or Cmd+K | Add selection to new chat | R |
| Code context | Cmd+Return | Search codebase in chat | R |
| Code context | Select code, Cmd+C, Cmd+V or Cmd+Shift+V | Add copied reference, or copied text | R |
| Tab | Tab | Accept suggestion | https://cursor.com/help/ai-features/tab.md |
| Tab | Cmd+Right | Accept next word | tab.md |
| Tab | Escape | Reject | tab.md |
| Editor | VS Code defaults | https://code.visualstudio.com/docs/getstarted/keybindings is the baseline | https://cursor.com/help/customization/keyboard-shortcuts.md |
| Inline edit | none documented | Reference lists no inline-edit section; inline-edit help page is 404 | https://cursor.com/help/ai-features/inline-edit.md (404) |
| Terminal (editor) | none documented | No Cursor terminal shortcut listed; VS Code defaults apply [INFERENCE] | R |
| CLI | Shift+Tab | Rotate Agent, Plan, Ask | cli/using.md |
| CLI | Shift+Enter, Ctrl+J, Option+Enter | Newline (Option+Enter after /setup-terminal; Ctrl+J for tmux) | cli/using.md ; https://cursor.com/docs/cli/reference/terminal-setup.md |
| CLI | Ctrl+D (twice) | Exit | cli/using.md |
| CLI | Ctrl+R | Review changes (/changes); i adds follow-up | cli/using.md |
| CLI | Ctrl+C | Pauses a /goal; cancels; exits Shell Mode | overview.md ; https://cursor.com/docs/cli/shell-mode.md |
| CLI | Ctrl+O | Expand truncated output | shell-mode.md |
| CLI | Enter, Enter twice | Steer at a safe boundary, interrupt the turn | overview.md |
| CLI | Tab (approval prompt) | Add command to allowlist | shell-mode.md |

## Count

Tokens in repository: 86 (A 44, B 21, C 21). Tokens today: 92 (86 + /agent-review, /worktree, /apply-worktree, /delete-worktree, /best-of-n, /remote-control). Delta +6 added, 0 removed, 0 renamed. Open-ended /skill-name, custom commands and /<subagent> are not counted. Settings: 50 rows in Table 1 (S06, S13, S14, S15, S17, S18, S36-S41, S45, S47, S48 restate or change a repo row; the rest are new). Shortcuts: 55 rows in Table 3.
