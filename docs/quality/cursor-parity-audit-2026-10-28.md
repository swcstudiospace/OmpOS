---
description: "Row-by-row audit of OmpOS against the Cursor 3.27 inventory: agent, chat, context, hooks, MCP, cloud agents, remote development, workspaces, settings, slash tokens and shortcuts, with status, evidence and the missing piece."
---

# Cursor parity audit against the Cursor 3.27 inventory (written 2026-10-28)

Status: Built (the audit covers every inventory row at commit fbd5266; rows that depend on Wave 3 head wiring must be re-verified after that merge)

Last verified: 2026-10-28

Applies to: v0.15.0

This audit refreshes `docs/quality/cursor-parity-audit-2026-10-26.md` (the previous audit, kept unchanged) against the inventory that milestone v0.15.0 researched on 2026-10-27: `docs/plans/v0.15.0-research/01-cursor-agent-chat.md` (agent, chat, context, tools, review, models; ids AC-001 to AC-194), `02-cursor-cloud-remote.md` (cloud agents, environments, API, automations, integrations, remote development, workspaces, CLI, team and accounts; ids CA, EN, API, AU, IN, RD, WS, CL, AD and AC-01 to AC-08) and `03-cursor-settings-slash.md` (settings S01 to S50, the 92 slash tokens, 55 shortcut rows). The omp side of each row is `04-omp-cursor-map.md`. Every row of the three files appears below with its own id; the slash tokens and shortcut rows have no id in the research, so this audit numbers them SL-001 to SL-092 (catalogue order) and SK-01 to SK-55 (table order). The two id series `AC-0nn` (three digits, file 01) and `AC-0n` (two digits, file 02, accounts) are different rows.

## 1. Audited tree and method

| Item | Value |
|---|---|
| Audited tree | The git worktree `/tmp/w3-docs` at base commit `fbd5266` (`wip(wave2): merged Wave 2 output of fourteen agents, not compiled`). It holds the Wave 1 code (commits `f993650` slash tokens and discovery, `ca7b53d` hooks core, `fd8f335` remote over SSH core, `1c02516` cloud worker bridge and Railway client, `99ca412` multi-repo, `75bee92` web foundation, `7d6d827` provider catalogue, `36734ab` API surface for the web screens, `857b146` brand, `9632a69` licence, `3d0d57c` community files, `3171ee8` Wave 1 fix-up) and the Wave 2 libraries and UI components (checkpoints, mentions and ignore files, MCP configuration import, run modes and permissions files, hooks integration, cloud control plane and cloud UI, remote UI, multi-repo wiring, providers wiring, web client, thread and workspace components, API settings, licence and brand wiring) |
| What the tree is not | Wave 3 agents are wiring hooks, remote, cloud, MCP, run modes, checkpoints, mentions, settings pages and slash handlers into the desktop and web heads in other worktrees at the same time as this audit. Those changes are not in the audited tree. A row whose head wiring is Wave 3 work is therefore marked by what the tree shows: library, model or view present and tested, head not constructing it. Its gap cell names the missing wiring and ends with 'verify after the Wave 3 merge'. This audit must be repeated on the merged tree before any such row is read as MET |
| Never built | The commit message says 'not compiled': the Wave 2 code was written without a build. No `dotnet build` or `dotnet test` has run on this tree. Every statement 'tests exist' means a test class was found by `grep`; none was run, here or by the agents that wrote them (their reports say 'not run') |
| Method | Each row was followed from the inventory row to the code and to a named test class found by `grep` in `src/`, `apps/`, `services/` and `tests/`. User-facing rows were followed into the heads by searching for the construction or use of each view and library type in `apps/OmpOS.Native`, `src/OmpOS.Native.Views`, `apps/OmpOS.Web` and `src/OmpOS.UI`. Facts that exist only in an agent report are cited as `report <Name>` (the reports of the Wave 1 and Wave 2 agents, /tmp/reports/ (one file per agent): changed files, tests, limits and a wiring spec). The omp side comes from `04-omp-cursor-map.md` |
| Web head | `apps/OmpOS.Web` is Blazor WebAssembly over `src/OmpOS.UI` and `src/OmpOS.Presentation`. The public build is a Demo build (ADR-0017): an in-browser `DemoApiHandler` answers every API route from scripted fixtures, so anything shown there is a simulated engine. Rows that are only demonstrated there say so. Plan: `docs/plans/v0.15.0-research/10-web-app-plan.md` |
| Cloud and remote | The cloud control plane (`services/OmpOS.Api/Cloud`), the worker (`services/OmpOS.AgentWorker`) and the Railway client (`src/OmpOS.Sandbox`) are built and tested with fakes; Railway deploys were never done (OD-03, OQ-100). Remote over SSH (`src/OmpOS.Harness.Remote`) has core and UI; it was never run against a real host (`RemoteSshLiveTests` is a guarded Live test with no recorded run) |
| Run by this audit | Read-only commands only: `git`, `grep`, `ls`, and small Python scripts that parse the three inventories, compute the counts below from the tables and check every cited path and class name |
| Not run by this audit | `dotnet build`, `dotnet test`, `./bootstrap.sh`, the node tests of `plugins/ompos-hooks-omp`, any live omp, any model turn |
| Documents | Other agents edit documents in the same worktree while this audit is written. This audit cites code, tests, agent reports and documents as committed at `fbd5266` |

A claim that a test exists means the class was found by `grep`. It does not mean the test was run.

## 2. Status vocabulary

| Word | Meaning |
|---|---|
| MET | Implemented, reachable from the head where the item is user-facing, and covered by named tests |
| MET-LIVE | MET, and also proven against the real omp in a recorded run. No live model turn was ever made beyond the bounded Phase 11 run (OD-07), so only rows proven without a model turn (provider configuration against omp 18.12.6) carry it, and only from the previous audit's records |
| PARTIAL | Part of the item is built or wired; the gap column says what is missing. A library or view that no head constructs is PARTIAL |
| DEFERRED | Not built, because of an external blocker or an owner decision; the blocker is named |
| NOT-STARTED | No product code and no deferral record |
| OMITTED-BY-DECISION | Left out on purpose. The reason is the Fit column of the inventory (omit-no-accounts), a decision recorded in `docs/settings-parity.md`, or a recorded default (OQ-95) |

A status word may carry a qualifier in brackets. The counts use the first word.

## 3. Summary

494 rows in 18 families. Counts are generated by a Python script that reads the tables of this document; none was counted by hand.

| Family | MET | MET-LIVE | PARTIAL | DEFERRED | NOT-STARTED | OMITTED-BY-DECISION | Total |
|---|---|---|---|---|---|---|---|
| Agent and chat (AC-001 to AC-031) | 2 | 0 | 18 | 3 | 7 | 1 | 31 |
| Context and ignore (AC-040 to AC-051) | 0 | 0 | 10 | 0 | 1 | 1 | 12 |
| Rules, skills, subagents and commands (AC-060 to AC-092) | 1 | 0 | 11 | 1 | 2 | 4 | 19 |
| Hooks (AC-100 to AC-104) | 0 | 0 | 4 | 0 | 1 | 0 | 5 |
| MCP and plugins (AC-110 to AC-118) | 0 | 0 | 4 | 0 | 3 | 2 | 9 |
| Tools, terminal, sandbox and browser (AC-120 to AC-130) | 2 | 0 | 4 | 1 | 2 | 2 | 11 |
| Worktrees (AC-131, AC-132) | 0 | 0 | 2 | 0 | 0 | 0 | 2 |
| Tab and inline edit (AC-140 to AC-145) | 0 | 0 | 1 | 0 | 4 | 1 | 6 |
| Review, Bugbot and git (AC-150 to AC-155) | 0 | 0 | 1 | 0 | 3 | 2 | 6 |
| Models, privacy and billing (AC-160 to AC-176) | 3 | 1 | 2 | 0 | 0 | 6 | 12 |
| Cloud agents (CA, EN, API, AU, IN rows and AC-190 to AC-194) | 0 | 0 | 28 | 6 | 52 | 22 | 108 |
| Remote development (RD rows) | 0 | 0 | 4 | 2 | 2 | 1 | 9 |
| Workspaces (WS rows) | 0 | 0 | 11 | 0 | 0 | 2 | 13 |
| CLI (CL rows) | 0 | 0 | 16 | 1 | 2 | 2 | 21 |
| Team, admin and accounts (AD rows and AC-01 to AC-08) | 0 | 0 | 3 | 9 | 3 | 18 | 33 |
| Settings (S01 to S50) | 5 | 0 | 16 | 6 | 9 | 14 | 50 |
| Slash tokens (92) | 16 | 0 | 53 | 5 | 3 | 15 | 92 |
| Shortcuts (55 rows of table 3) | 5 | 0 | 11 | 0 | 33 | 6 | 55 |
| **All rows above** | **34** | **1** | **199** | **34** | **127** | **99** | **494** |

The 92 slash tokens: 16 MET, 53 PARTIAL, 5 DEFERRED, 3 NOT-STARTED, 15 OMITTED-BY-DECISION. 25 of the PARTIAL tokens are PARTIAL only because their Wave 2 handler is not yet composed into the head (the catalogue edits are listed in `report Checkpoints`).

MET-LIVE: 1 row. No new row is MET-LIVE: the only recorded live evidence is the previous audit's run of provider configuration against the real omp (`LiveOmpConfigTests`), carried to AC-163 and not re-run.

No row about the Cursor agent loop (modes, steering, hooks, MCP, cloud, remote, checkpoints) is MET-LIVE, because no live model turn was approved after Phase 11 (OD-07).

## 4. What is not done and why

The honest list. Each line was checked against the tree at `fbd5266`; the rows it affects are in the tables below.

| Not done | Rows | Why | Record |
|---|---|---|---|
| Head wiring of everything Wave 2 built: Hooks page, MCP page, Remote and Cloud pages, Run Mode page, checkpoints, worktree commands, mentions, custom modes, queue, side chat hub, fork | AC-006, AC-015 to AC-021, AC-040 to AC-047, AC-100, AC-110, AC-123, AC-131, AC-132, CA-03, RD-01 to RD-03, S02 to S04, S08, S09, S14, S17, S23, S35, 25 slash tokens | Wave 2 delivered libraries, models and views with a wiring spec each; the heads (`MainView`, `AgentHead`, `SessionEngineSet`, `SettingsWindowFactory`, `WebWorkbench`, `Program.cs`) belong to Wave 3. Grep finds no construction of `HooksService`, `AcpHookTap`, `CheckpointsModel`, `QueuedMessagesModel`, `CustomModeModel`, `SideChatHub`, `WorktreeCommandsModel`, `MultiRootWorkspaceTree`, `RunModePolicy` or `RunModePermissionResolver` outside tests, and no head constructs the page views `HooksPageView`, `McpPageView`, `RemoteEnvironmentsPageView`, `CloudAgentsPageView` or `RunModePageView` | reports HooksIntegration, Checkpoints, Mentions, McpImport, Safety, RemoteUI, CloudUI (each 'WIRING SPEC') |
| ACP form elicitation: omp's `ask` tool, plan approval, MCP elicitation | AC-003, AC-024, AC-113 | omp asks the client for a form through ACP; no code in `src/`, `apps/` or `services/` handles elicitation (grep), so the `ask` tool throws headless and a plan cannot be approved from OmpOS | 04-omp-cursor-map.md rows 8 and 23 |
| Image input, voice input and calls, deeplinks | AC-025, AC-026, AC-029, IN-16 | ACP accepts images and omp lists speech models, but the composer has no attach, microphone or URL handler | 04-omp-cursor-map.md rows 19 and section B2 |
| Embedded browser pane, Design Mode and @Browser | AC-007, AC-044, AC-126, S20, SK-14 to SK-19 | Nothing exists in `src/`, `apps/`, `services/`; ADR-0009 is accepted for Linux only and Windows and macOS are unmeasured | ADR-0009, OQ-51, `docs/runbooks/deferred-and-blocked-2026-10-26.md` |
| Cloud agents live: control plane exists but is not mapped, not deployed, and the worker cannot clone or branch | CA-01, CA-03, CA-30, EN-01 to EN-12, API-01 to API-03 | Cloud endpoints are not registered in `Program.cs`; the worker bridges `omp acp` only (no clone, branch, environment run, git credential helper); git tokens last one hour; stores and secrets are in memory; the Railway operation table has UNVERIFIED entries; Railway deploys are paused and no sign-in authority is configured (OD-03) | reports CloudControlPlane, CloudCore, CloudCore-sandbox; OQ-70, OQ-85, OQ-100 |
| Remote over SSH never run against a real host; no head starts a remote session | RD-01 to RD-07 | The loopback sshd script and live test were written, not started; `SessionEngineSet` does not create remote sessions; a Windows head rejects a remote working directory (`AcpAgentSession` requires a fully qualified path) | report RemoteCore, ADR-0015, OQ-97 |
| Checkpoints, rewind, fork over ACP | AC-017, AC-018, slash tokens /rewind, /restore, /undo, /fork, /branch, /duplicate | omp has no file-level checkpoint and only `unstable_forkSession`; the OmpOS store and models are unwired; the rollback path after a failed rewind is not exercised by any test | 04-omp-cursor-map.md row 9, report Checkpoints |
| Engine-limited modes and commands: Ask, Debug, /goal, /loop, subagent roster | AC-002, AC-004, AC-023, AC-083 | omp ACP advertises modes `default` and `plan` only, and /goal, /loop are terminal-UI or RPC only | 04-omp-cursor-map.md rows 8, 31, section B3 |
| Run Modes, permissions.json and sandbox enforcement | AC-123, AC-124, AC-125, S03 to S05, S14, S15, S17 | The policy, resolver and page are libraries; nothing registers them; omp has no sandbox and its approval mode is process-level; omp's permission request field names are UNVERIFIED | report Safety, OQ-84 |
| Bugbot, Agent Review, PR routing, rollouts, automation triggers, CI auto-fix | AC-150 to AC-153, AU-02 to AU-15, AU-17 to AU-19, CA-14, CA-15 | Not started; `Harness.Automations` is a local engine that no head wires | CX-02 of the previous audit |
| Usage and cost: usage history stays empty; no per-category context counts | AC-021, AC-175 | `MainView.CreateExport` passes an empty `UsageHistory`; ACP gives `usage_update` size and used only | 04-omp-cursor-map.md row 21 |
| The 92-token slash catalogue is partly unwired | section 21 | The Wave 1 catalogue lists every token with an honest status; Wave 2 added 12 handler classes (25 tokens) whose catalogue edits and host composition are Wave 3 | `docs/reference/slash-commands.md`, report Checkpoints |
| Settings window has 11 pages | S rows | Hooks, MCP, Remote, Cloud and Run Mode pages are Wave 2 views not yet in `SettingsSection` (the display flags have a store and no page); `docs/settings-parity.md` is owned by the Settings agent and will be refreshed | `docs/settings-parity.md` |
| Windows, macOS, Android device, iOS runs | SK rows, AC-123, CA-26 | No runners or devices; every Cursor key is listed for Ctrl, macOS keys are unmeasured; PTY terminal is Linux only | OQ-69, OQ-74, `docs/runbooks/local-terminal.md` |
| Any live model turn | AC-001, AC-016, AC-100, AC-110 | Only the bounded Phase 11 run was approved | OD-07, OQ-71, OQ-81 |
| Owner sign-offs and accounts: brand, licence form decisions, GitBook space, Railway deploy, sign-in authority | section 28 | See the owner list at the end | OQ-93 to OQ-104 |

Also not verified: that the merged Wave 3 tree builds, passes its tests and keeps the statuses above.

## 5. Agent and chat (AC-001 to AC-031)

Source: `01-cursor-agent-chat.md` section 2. Reachability: the desktop head is `apps/OmpOS.Native` with `src/OmpOS.Native.Views` (`MainView`, `AgentSessionRuntime`, `ComposerView`); the web head is `apps/OmpOS.Web` (`WebWorkbench`, `WebSessionRuntime`) over the Demo handler in the public build. omp is the engine over ACP (`04-omp-cursor-map.md`). Cursor findings N1 to N10 of file 01 change three rows: inline edit is retired (AC-144), Memories were removed (AC-066), Max Mode is legacy (AC-165).
Counts (31 rows): MET 2, MET-LIVE 0, PARTIAL 18, DEFERRED 3, NOT-STARTED 7, OMITTED-BY-DECISION 1.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-001 | Agent mode | PARTIAL | `AgentSessionRuntime`, `SessionView`, `EngineSendPathTests`, `SharedScenarioParityTests`, `ParityInventoryTests`; web: `WebSessionRuntime` over `DemoApiHandler` | Never run with a live model beyond the bounded Phase 11 run (OD-07); the web head shows a simulated engine (ADR-0017 Demo mode) |
| AC-002 | Ask mode (read-only) | DEFERRED | `OptionChipView` (mode chip), `SlashCommandCatalogue` (`/ask` Partial: works only if the engine advertises an ask mode) | omp 18.12.6 ACP modes are `default` and `plan` only (04-omp-cursor-map.md row 8); a read-only emulation process is not built |
| AC-003 | Plan mode | PARTIAL | `OptionChipView` (mode chip), `OptionChipModelTests`, `SlashCommandCatalogueTests` (`/plan` Built, `mode.set`), `PlanBlockView` (src/OmpOS.UI/Components/Blocks/PlanBlockView.razor) | The Approve-and-execute step needs ACP form elicitation: no code in `src/`, `apps/`, `services/` handles it (grep); no editable plan file, no Build in Parallel |
| AC-004 | Debug mode | DEFERRED | `SlashCommandCatalogue` (`/debug` Partial: only if the engine advertises a debug mode) | omp 18.12.6 advertises no debug mode (04-omp-cursor-map.md row 8); no Debug pill |
| AC-005 | Mode switching (Shift+Tab, fresh context per mode) | PARTIAL | `OptionChipView` (mode chip), `ShortcutCatalogue` (no Shift+Tab entry), `ShortcutCatalogueTests` | Mode is switched from the chip only; Shift+Tab rotation is not bound; fresh-context-per-mode not observed |
| AC-006 | Custom Modes (skill pinned per turn) | PARTIAL | `CustomModeModel`, `IModeSkillSource`, `IPromptPreamble`, `CustomModeModelTests` (Wave 2 library) | No head constructs `CustomModeModel` (grep); no view; no run path reads a pinned mode (previous audit ADE-17); head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-007 | Design Mode (Annotate) | NOT-STARTED | `ADR-0009-browser-pane.md` (accepted for Linux only), `tools/browser-spike` (outside the solution) | No WebView, browser-pane or design-mode code in `src/`, `apps/`, `services/` (grep); Windows and macOS unmeasured (OQ-51) |
| AC-008 | Agents Window | PARTIAL | `AgentRailView`, `AgentPanelViewModel`, `AgentPanelViewModelTests`, `KanbanView`; web: src/OmpOS.UI/Components/Rail/AgentRail.razor, src/OmpOS.UI/Components/Kanban/KanbanView.razor over `DemoApiHandler` | Agents tab, rail and Kanban exist; no cloud or remote environments inside it until Wave 3 wires them; lease holder always unavailable (`HeadKanbanDetailsSource`) |
| AC-009 | Multi-workspace in Agents Window | PARTIAL | `WorkspaceSetPicker`, `MultiRootWorkspaceTree`, `WorkspaceSetSlashHandler` (Wave 2 models, `Presentation.Tests` Editor tests exist) | No open-folder action in the head: the root is fixed at launch (`AgentHeadOptions`); picker is not constructed by any head; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-010 | Parallel agents and /multitask | PARTIAL | `SessionEngineSet`, `AgentSessionRegistry`, `SessionEngineSetTests`, `AgentPanelViewModelTests` | Several sessions run side by side; `/multitask` is not one of the 92 catalogued tokens (grep of `SlashCommandCatalogue`) |
| AC-011 | Agent sidebar management (pin, archive, group, unread, switcher) | PARTIAL | `AgentRailView`, `SessionCatalogModel`, `SessionCatalogModelTests`, `AgentRowStatusMapTests` | Rail lists sessions with status; no pin, archive, grouping, unread marks or Ctrl+Tab switcher (no such symbols in `Presentation/AgentPanel`) |
| AC-012 | Command palette in Agents Window (Ctrl+K) | PARTIAL | `SlashPaletteModel`, `TranscriptSearchModel`, `ConversationSearchModel`, `ConversationSearchView` | The slash palette and chat search exist; no Ctrl+K palette, no paste-a-PR-URL jump (`ShortcutCatalogue` has no Ctrl+K) |
| AC-013 | Projects (coordinator agent) | NOT-STARTED | `CloudAgentsViewModel` (cloud agent list only) | No coordinator, task tracker or subscriptions; depends on cloud agents going live |
| AC-014 | Local to cloud handoff, /in-cloud, /autopilot | PARTIAL | `CloudHandoff`, `ICloudHandoff`, `CloudHandoffTests`, `SlashCommandCatalogue` (CloudHandoff kind) | Catalogue marks both Unavailable (`cloud agents: phase 60`); no host implements the handoff; Railway deploy never done; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-015 | Queued messages | PARTIAL | `QueuedMessagesModel`, `QueuedMessagesRegistry`, `QueuedMessagesSteeringAdapter`, `QueuedMessagesModelTests`; head today: `RunInputScheduler`, `RunInputSchedulerTests` | Queue model with reorder is a Wave 2 library with no view and no head owner (grep); the follow-up key Ctrl+Shift+Enter works today; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-016 | Send now / steer | MET | `SteeringInputModel`, `RunInputScheduler`, `AcpSteeringTests`, `EngineRunInputSendTests`, `LiveOmpSteeringContractTests`; shortcuts Ctrl+Enter steer and Ctrl+Shift+Enter follow-up in `ShortcutCatalogue`; web: src/OmpOS.UI/Components/Composer.razor, src/OmpOS.UI/Components/Composer/SteeringHistory.razor | Steering into a running turn and omp-side follow-up delivery were never observed live (no live model turn (OD-07)); contract test used an idle session |
| AC-017 | Checkpoints and restore | PARTIAL | `CheckpointStore`, `RewindPlanner`, `CheckpointsModel`, `CheckpointRecorderModel`, `CheckpointStoreTests`, `CheckpointsModelTests`, `TurnChangeSetTests` | Library and presentation models only: no view, no `ICheckpointBackend` adapter, nothing records a turn (report Checkpoints WIRING SPEC); omp has no file-level checkpoint (04-omp-cursor-map.md row 9); head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-018 | Fork and revert | PARTIAL | `ForkModel`, `ISessionForker`, `ForkSlashHandler`, `ModelSlashHandlerTests`, `ForkModelTests`, `ScriptedSessionForker` | No ACP forker (omp `unstable_forkSession`) is implemented; `/fork` still Unavailable in the catalogue; revert depends on AC-017 wiring |
| AC-019 | Side chats (/side, /btw) | PARTIAL | `SideChatPanel`, `TranscriptSearchModel`, `SideChatTests`, `ShellSideChatAndSearchTests`; Wave 2: `SideChatHub`, `SideChatModel`, `ParentSummaryBuilder`, `SideChatHubTests` | Today the side chat posts the question but the engine does not answer in it (`/side` Partial); hidden parent context seed (`SideChatHub`) is unwired; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-020 | Conversation search | MET | `TranscriptSearchModel`, `TranscriptSearchView`, `TranscriptSearchScopeTests`, `TranscriptSearchChatScopeViewTests`, `ShortcutCatalogue` (Ctrl+F); Wave 2 index `ConversationSearchIndex` and `ConversationSearchView` exist | Cross-chat search and in-chat find reach the head; Ctrl+K palette form and the on-disk index of past transcripts are not wired (`ConversationSearchModel`, Wave 3) |
| AC-021 | Context usage ring and breakdown | PARTIAL | `ContextRingModel`, `ContextRingState`, `ContextRingView` (drawn inside `ComposerView`), `ContextRingModelTests` | `ComposerViewModel.ContextRing` is a property no head assigns (grep); per-category counts need engine data: ACP gives size and used only (04-omp-cursor-map.md row 21); head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-022 | Summaries and /summarize | PARTIAL | `SlashCommandCatalogue` (`/summarize`, `/compress` Delegated to omp `/compact`), `SlashCommandCatalogueTests` | Forwarded text only; compaction events are not forwarded over ACP (04-omp-cursor-map.md section B3); not observed live |
| AC-023 | /goal and /loop | DEFERRED | `SlashCommandCatalogue` (`/goal`, `/loop` Unavailable) | omp runs both in its terminal UI or RPC only; no ACP handler (04-omp-cursor-map.md row 31) |
| AC-024 | Ask-questions tool | NOT-STARTED | `SlashCommandCatalogue` (not applicable); 04-omp-cursor-map.md row 23 | omp's `ask` tool needs the client to implement ACP form elicitation; no code references elicitation in `src/`, `apps/`, `services/` (grep), so the tool fails headless |
| AC-025 | Image input | NOT-STARTED | `PromptOptions` (text prompt path); 04-omp-cursor-map.md row 19 | ACP accepts image blocks but the composer has no paste, drop or attach for images (grep of `ComposerView`) |
| AC-026 | Voice input and voice calls | NOT-STARTED | 04-omp-cursor-map.md section B2 (`speech.models.list`) | No microphone or speech code in `src/` or `apps/` |
| AC-027 | Chat panel pills and tray | NOT-STARTED | `PipelinePill` (omp stage pills, unrelated) | No Goal, Queued, Debug, Canvas or Open Browser pill; queue has no view |
| AC-028 | Chat content actions (table copy, Mermaid, download, Review Changes) | PARTIAL | `ChangesBarView`, `ChangesBarViewTests`, `DiffReviewView`; web: src/OmpOS.UI/Components/Changes/ChangesBar.razor, `ChangesBarTests` | Review Changes list exists; no Markdown-table copy, Mermaid zoom or Download File (no Mermaid symbol in `src/`) |
| AC-029 | Deeplinks (prompt, command, rule) | NOT-STARTED | `MentionUris` (mention URIs only) | No URL handler for an OmpOS scheme in `apps/OmpOS.Native`, `apps/OmpOS.Android` or `apps/OmpOS.Web` |
| AC-030 | Agent resume after crash | PARTIAL | `SessionStatus` (Interrupted), `ReconnectingEngineHost`, `TurnInterruptedEventArgs`, `RemoteStatusDescriber`, `ReconnectingEngineHostTests` | Interrupted state exists for remote sessions; a local host crash shows no Resume action; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-031 | Shared transcripts | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md); local export: `TranscriptMarkdownExporter`, `TranscriptExportViewTests` | Needs a Cursor account and Cursor-hosted pages; OmpOS offers local Markdown export only |

## 6. Context and ignore (AC-040 to AC-051)

Counts (12 rows): MET 0, MET-LIVE 0, PARTIAL 10, DEFERRED 0, NOT-STARTED 1, OMITTED-BY-DECISION 1.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-040 | @ Files and Folders | PARTIAL | `MentionParser`, `MentionPickerModel`, `IMentionFileSource`, `MentionPickerView` and `ComposerView` chips, `MentionParserTests`, `MentionPickerModelTests`, `ComposerMentionViewTests`, `AcpPromptContent` | `ComposerViewModel.Mentions` is set by no head; file source and ACP embedded-resource send are Wave 3; web head has no mention picker; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-041 | @Terminals | PARTIAL | `IMentionTerminalProvider`, `CatalogMentionProvider`, `CatalogMentionProviderTests` | Provider seam built; no head supplies terminal output; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-042 | @Chats | PARTIAL | `IMentionChatProvider`, `CatalogMentionProvider`, `CatalogMentionProviderTests` | Same: no head supplies the chat catalogue; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-043 | @ Git diffs (working state, branch diff) | PARTIAL | `IGitDiffSource`, `MentionGitDiffKind`, `MentionSourceTests` | Interface only: no implementation of `IGitDiffSource` exists in `src/` or `apps/` (grep) |
| AC-044 | @Browser | NOT-STARTED | `ADR-0009-browser-pane.md` | Blocked on the browser pane (AC-126); no source |
| AC-045 | @rule, @skill, @side-chat, # pull request, + menu | PARTIAL | `MentionKind`, `MentionTrigger`, `UnavailablePullRequestSource`, `MentionParserTests` | `#` answers 'needs a configured Git host'; `+` Attach and Summarize menu not built; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-046 | .cursorignore | PARTIAL | `IgnoreMatcher`, `PhysicalIgnoreFileSource`, `IgnoreSyntaxTests`, `IgnoreHierarchyTests`, `IMentionIgnoreFilter` | Used only to filter @ mentions once wired; omp does not honour it for its own tools [INFERENCE in 01]; global ignore list and Settings toggle not built (settings-parity row 30 still says Omitted); head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-047 | Default ignore list and .gitignore | PARTIAL | `IgnoreDefaults`, `IgnoreDefaultsTests`, `IgnoreLimitsTests` | Same as AC-046 |
| AC-048 | Instant Grep (local search index) | PARTIAL | `WorkspaceSearch`, `WorkspaceSearchView`, `WorkspaceSearchTests`, `WorkspaceSearchViewTests` | User-facing text and regex search is met; no persistent index; the agent searches with omp's own tools |
| AC-049 | Semantic codebase index, .cursorindexingignore | OMITTED-BY-DECISION | previous audit ADE-18 (`WorkspaceSearch`: semantic index out of scope by design); `docs/settings-parity.md` rows 14 and 15 | Sources conflict on whether Cursor still ships it (01-cursor-agent-chat.md N6); nothing built |
| AC-050 | Multi-root workspaces | PARTIAL | `MultiRootWorkspaceTree`, `WorkspaceRootSource`, `CodeWorkspaceFile`, `OmpLaunchPlanner` (`--add-dir`), `OmpLaunchPlannerTests` | The explorer in the head is single-root (`MultiRootWorkspaceTree` not constructed by any head); `--add-dir` not probed in a live turn; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-051 | Multi-repo cloud environments | PARTIAL | `CreateCloudAgentRequest` (Repositories), `CloudRepository`, `CloudWorkerRepository`, `CloudAgentLifecycleTests` | Worker does not clone the repositories yet (CloudControlPlane report: bootstrap missing); Railway deploy never done |

## 7. Rules, skills, subagents and commands (AC-060 to AC-092)

Counts (19 rows): MET 1, MET-LIVE 0, PARTIAL 11, DEFERRED 1, NOT-STARTED 2, OMITTED-BY-DECISION 4.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-060 | Project rules (.mdc, four types) | PARTIAL | `ConventionCatalog`, `ConventionRuleKind`, `ConventionCatalogTests`, `RulesSettingsPage`, `RulesSettingsSource` | Listing is read-only; omp loads `.cursor/rules/*.mdc` itself (04-omp-cursor-map.md row 4); OmpOS feeds no rule to a session; no editor, no /create-rule authoring |
| AC-061 | AGENTS.md including nested | PARTIAL | `ConventionRuleKind` (AgentsFile), `ConventionItemScanner`, `ConventionCatalogTests` | omp reads AGENTS.md natively (04-omp-cursor-map.md row 4); nested application not verified; not observed live |
| AC-062 | User rules | PARTIAL | `ConventionScope` (user scope), `ConventionCatalogTests`, `RulesSettingsPage` | User-level rule files are listed; Cursor's in-app text box is not built |
| AC-063 | Team rules | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | Dashboard feature of Cursor teams; no accounts or teams |
| AC-064 | Rule import via plugin / GitHub | NOT-STARTED | `PluginsPage`, `OmpPluginCatalog` (four fixed plugins, OQ-80) | No marketplace.json import |
| AC-065 | Fetch Rules tool | PARTIAL | 04-omp-cursor-map.md row 4 (omp `rule://` URLs) | Provided by omp; OmpOS adds nothing and observes only the tool call |
| AC-066 | Memories | OMITTED-BY-DECISION | Fit omit-no-accounts and finding N2 (feature removed) in 01-cursor-agent-chat.md; 04-omp-cursor-map.md row 5 | Nothing to build; omp has its own memory backends |
| AC-070 | Skills (SKILL.md standard) | PARTIAL | `ConventionSkill`, `ConventionFrontmatterParser`, `SkillsSettingsPage`, `ConventionSlashDiscoverySource`, `SlashPaletteDiscoveryTests`, `ConventionCatalogTests` | Discovery and palette listing reach the desktop head; auto-selection, progressive loading and invocation are omp's and not observed live; no /create-skill authoring |
| AC-071 | Skill directories and compat | PARTIAL | `ConventionDirectories` (.ompos .omp .cursor .claude .codex .agents, project and user), `LocalConventionFileSourceTests`, `ConventionSettingsLoader` | OmpOS lists all six; omp itself does not read `.cursor/skills` (04-omp-cursor-map.md row 3), so a listed skill may not load in the engine; remote and cloud sync not built |
| AC-072 | Built-in skills (19) | PARTIAL | `SlashCommandCatalogue` (`Skill` kind), `SlashCommandCatalogueTests` | Per-token result in section 21: skills run only if a same-named SKILL.md is installed; OmpOS ships none of Cursor's skill bodies |
| AC-073 | Sync and publish skills | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md); `docs/settings-parity.md` row 5 | Needs Cursor cloud sync and a team marketplace |
| AC-080 | Built-in subagents (Explore, Browser) | OMITTED-BY-DECISION | `docs/settings-parity.md` row 9 (Omitted: delegation is omp and the Swarm plugin); 04-omp-cursor-map.md row 6 | omp ships its own agents (scout, reviewer, task); no OmpOS list or editor |
| AC-081 | Custom subagents | PARTIAL | `ConventionSubagent`, `SlashDiscoveredSubagent`, `ConventionCatalogTests`, `SlashPaletteDiscoveryTests` | Definitions in `.cursor/agents` and other roots are listed and offered as /name; omp deliberately skips `.claude/agents`, `.codex/agents` and `.cursor/agents` (04-omp-cursor-map.md row 6); settings-parity row 9 still says Omitted (drift) |
| AC-082 | Subagent model parameters | NOT-STARTED | `ConventionSubagent` (frontmatter is parsed) | No `id[key=value]` model parameter support in the picker or any run path |
| AC-083 | Foreground, background, resume subagents | DEFERRED | 04-omp-cursor-map.md section B3 (ACP emits no subagent roster) and B5 (RPC has `get_subagents`) | Needs omp RPC or an extension; not forwarded over ACP |
| AC-084 | Isolated and cloud subagents | PARTIAL | `WorktreeManager`, `WorktreeManagerTests`, `CloudHandoff`; omp `task.isolation` (04-omp-cursor-map.md row 13) | Worktree library wired in the hosted API only; cloud subagent wiring (AC-014) pending |
| AC-090 | Custom slash commands | MET | `ConventionCommand`, `ConventionSlashDiscoverySource`, `SlashPromptComposer`, `SlashPaletteModel`, `SlashPaletteDiscoveryTests`, `SlashPromptComposerTests`, `CommandsSettingsPage`; web: `WorkspaceSlashDiscoverySource` | Result of a command prompt was not observed with a live model; team commands from a dashboard are omitted |
| AC-091 | Command to skill migration (/migrate-to-skills) | PARTIAL | `SlashCommandCatalogue` (`Skill` kind: runs SKILL.md named migrate-to-skills when installed) | OmpOS ships no such skill |
| AC-092 | Cursor slash commands that matter for parity | PARTIAL | `SlashCommandCatalogue`, `SlashCommandRegistry`, `SlashCommandCatalogueTests`, `SlashCommandReferenceTests` | See section 21 for each of the 92 tokens; per-token counts are in the summary |

## 8. Hooks (AC-100 to AC-104)

Counts (5 rows): MET 0, MET-LIVE 0, PARTIAL 4, DEFERRED 0, NOT-STARTED 1, OMITTED-BY-DECISION 0.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-100 | Agent hook events (21) | PARTIAL | `HookEvents`, `HookDispatcher`, `HooksConfigLoader`, `HookEventsTests`, `HookDispatcherGatingTests`; engine side: `AcpHookTap`, `HookBridgeServer`, `HooksService`, `AcpHookTapTests`, `HookBridgeIntegrationTests`; omp extension plugins/ompos-hooks-omp/src/index.mjs and test `bridge.test.mjs` | No head, API or composition constructs `HooksService`, `AcpHookTap` or `HookDispatcher` (grep of `apps/`, `Native.Views`, `services/`); never run against a live omp (report HooksCore UNVERIFIED); head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-101 | Tab and app hooks (workspaceOpen) | PARTIAL | `HookEvents` (beforeTabFileRead, afterTabFileEdit, workspaceOpen catalogued), `HookEventsTests` | Tab hooks never fire (no Tab engine); workspaceOpen firing on folder open is Wave 3 (HooksIntegration WIRING SPEC item 5) |
| AC-102 | Hook types and exit codes | PARTIAL | `HookOutputParser`, `HookOutputParserTests`, `HookDispatcherFailurePolicyTests`, `HookDispatcherPromptTests`, `SystemHookCommandRunnerTests` | `IPromptHookEvaluator` has no implementation (prompt hooks cannot run); Windows PowerShell and cmd shells written, never run |
| AC-103 | Hook locations, distribution, hot reload | PARTIAL | `HookSourceLocator`, `HookSourceLocatorTests`, `HooksConfigLoaderTests`, `HookConsentIdentity`, `HookConsentIdentityTests` | Project, user and Claude settings locations load; reload is by call (no file watcher); MDM and Enterprise cloud distribution omitted (no accounts) |
| AC-104 | Hooks in cloud agents | NOT-STARTED | `HookLocatorContext` (remote flag only) | The cloud worker image does not load the hooks extension (grep of `services/OmpOS.AgentWorker`); no cloud subset |

## 9. MCP and plugins (AC-110 to AC-118)

Counts (9 rows): MET 0, MET-LIVE 0, PARTIAL 4, DEFERRED 0, NOT-STARTED 3, OMITTED-BY-DECISION 2.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-110 | MCP transports (stdio, SSE, Streamable HTTP) | PARTIAL | `McpToolRegistry`, `McpToolInvokerTests`, `McpHttpTests`, `AcpMcpServerMapper`, `AcpMcpServerMapperTests`, `McpToolsView` | The registry is empty in the app (`MainView.CreateMcpTools` connects none); `session/new` does not receive configured servers; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-111 | MCP config locations and interpolation | PARTIAL | `McpConfigLoader`, `McpLocations`, `McpVariableExpansionTests`, `McpConfigPrecedenceTests`, `McpConfigFormatTests` | Library only; no head constructs the loader; omp reads .cursor/mcp.json itself for its own sessions only when MCP is enabled (04-omp-cursor-map.md row 10: ACP sessions start with MCP off) |
| AC-112 | MCP OAuth | NOT-STARTED | 04-omp-cursor-map.md row 10 (omp config supports oauth) | No callback listener or sign-in UI in `src/` or `apps/` |
| AC-113 | MCP protocol features (elicitation, apps, images) | NOT-STARTED | 04-omp-cursor-map.md row 10 and 23 | No elicitation handling (grep), no MCP Apps surface |
| AC-114 | MCP approvals and limits | PARTIAL | `RunControlsView`, `EngineToolApprovalBridge`, `EngineSendPathTests`; Wave 2: `McpToolName`, `PermissionsFileReader`, `PermissionsFileTests` | Per-call approval works through the ACP bridge; permissions.json mcpAllowlist and Run Modes are library-only; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-115 | MCP install links and extension API | NOT-STARTED | `McpOwnedConfigWriter` (writes OmpOS-owned config) | No deeplink handler, no extension API |
| AC-116 | Plugins (Agent and Cursor formats) | PARTIAL | `PluginsPage`, `PluginsSettingsSource`, `OmpPluginCatalog`, `PluginsPageTests` | Four fixed plugins, no switch (OQ-80); Cursor-format plugin import not built (04-omp-cursor-map.md row 26) |
| AC-117 | Marketplace and team marketplaces | OMITTED-BY-DECISION | Fit omit-no-accounts (use a public marketplace.json instead); `docs/settings-parity.md` row 13 | Marketplaces need accounts and a store |
| AC-118 | Plugin canvases | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | Third-party hosted canvases |

## 10. Tools, terminal, sandbox and browser (AC-120 to AC-130)

Counts (11 rows): MET 2, MET-LIVE 0, PARTIAL 4, DEFERRED 1, NOT-STARTED 2, OMITTED-BY-DECISION 2.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-120 | Search tools | MET | `WorkspaceSearch`, `WorkspaceSearchView`, `WorkspaceSearchTests`; omp tool calls rendered by `ToolCallBlockView` (src/OmpOS.Native.Views/Views/Blocks) and src/OmpOS.UI/Components/Blocks/ToolCallBlock.razor | Agent-side search is omp's tools; Explore subagent is omp's |
| AC-121 | Read files incl. images | PARTIAL | `DataImage` (src/OmpOS.Native.Views/Views/Blocks/DataImage.cs), `NativeUiBlockRendererTests` | Reading is omp's; image blocks from agents render; no image attach (AC-025) |
| AC-122 | Edit files, diff view, Review Changes, Undo | MET | `DiffReviewView`, `ChangesBarView`, `ChangeSetModel`, `DiffReviewTests`, `ChangesBarViewTests`, `DiffReviewWriterTests`; web: src/OmpOS.UI/Components/Changes/ChangesBar.razor, `ChangesBarTests`, `WebChangeReview` | Edits are omp's; web reads changes through the API (Demo engine in the public build) |
| AC-123 | Terminal tool and Run Modes | PARTIAL | `TerminalPanelView`, `TerminalGate`, `TerminalGateTests`, `AgentsPage`; Wave 2: `RunModePolicy`, `RunModePermissionResolver`, `RunModeSettingsPage`, `RunModePageView`, `RunModePolicyTests`, `RunModePageViewTests` | Terminal and auto-run switch are Linux only (OQ-69); Run Mode page and resolver are not registered anywhere (report Safety WIRING SPEC); Auto-review classifier model call not built; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-124 | permissions.json | PARTIAL | `PermissionsFileReader`, `PermissionsFileEditor`, `EffectivePermissions`, `PermissionsFileTests`, `PermissionTokenParserTests` | Library only; enforcement is OmpOS's ACP resolver once wired, not omp's; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-125 | Sandbox (Seatbelt, Landlock, sandbox.json) | OMITTED-BY-DECISION | `docs/settings-parity.md` rows 4 and 26 (sandbox limits are omp's); 04-omp-cursor-map.md row 12 (omp has no sandbox) | Drift: Wave 2 `SandboxFileReader`, `EffectiveSandbox`, `SandboxFileTests` now parse sandbox.json but enforce nothing; the Settings agent owns the refresh |
| AC-126 | Browser tool | NOT-STARTED | `ADR-0009-browser-pane.md`; omp's `browser` tool (04-omp-cursor-map.md row 11) | No embedded browser pane (grep); the agent's browser calls appear only as tool blocks |
| AC-127 | Web search and fetch | PARTIAL | 04-omp-cursor-map.md row 18 (omp `web_search`, ACP kind fetch) | Engine-provided; no provider or key settings UI in OmpOS |
| AC-128 | Image and video generation | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | Needs a hosted image model |
| AC-129 | Canvases | NOT-STARTED | `A2uiSurfaceView` (agent-generated UI surfaces, a different feature) | No canvas tool, tab or publish |
| AC-130 | Computer use | DEFERRED | `docs/runbooks/cloud-terminals.md` | No remote desktop until Railway confirms its acceptable-use position in writing (OQ-73) |

## 11. Worktrees (AC-131, AC-132)

Rows of `01-cursor-agent-chat.md` section 6; the WS rows of file 02 are in the workspaces family.
Counts (2 rows): MET 0, MET-LIVE 0, PARTIAL 2, DEFERRED 0, NOT-STARTED 0, OMITTED-BY-DECISION 0.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-131 | Worktrees (Agents Window) | PARTIAL | `WorktreeManager`, `WorktreeApplier`, `CursorWorktreesConfig`, `WorktreeManagerTests`, `WorktreeApplierTests`, `CursorWorktreesConfigTests`, `AcpHarnessSessionFactory` | Wired into the hosted API only (opt-in `Worktrees:Root`); the desktop head's local runs use no worktree; `WorktreeCommandsModel` unwired; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AC-132 | /worktree, /apply-worktree, /delete-worktree, /best-of-n | PARTIAL | `WorktreeSlashHandler`, `WorktreeCommandsModel`, `BestOfNState`, `ModelSlashHandlerTests`, `WorktreeCommandsModelTests`, `SlashCommandCatalogue` (`/worktree` Delegated to omp `/wt`) | Catalogue still lists apply, delete and best-of-n Unavailable; best-of-n candidates are per index, not per model; head wiring is Wave 3 work: verify after the Wave 3 merge |

## 12. Tab and inline edit (AC-140 to AC-145)

Counts (6 rows): MET 0, MET-LIVE 0, PARTIAL 1, DEFERRED 0, NOT-STARTED 4, OMITTED-BY-DECISION 1.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-140 | Tab completions | PARTIAL | `CompletionClient`, `InlineCompletionBox`, `CompletionClientTests`, `TabSettingsSource`, `TabPage` | `ComposerView.Completion` never assigned: no suggestion appears in the app (previous audit ADE-13); omp has no completion engine (04-omp-cursor-map.md row 32); wire format unconfirmed (OQ-55) |
| AC-141 | Next edit and cross-file jump | NOT-STARTED | none | No predicted-edit engine |
| AC-142 | Tab snooze and per-language off | NOT-STARTED | `TabPage` (read-only status), `docs/settings-parity.md` row 17 | Snooze, global disable and per-extension disable not built |
| AC-143 | Tab privacy and key rules | OMITTED-BY-DECISION | Fit omit-no-accounts (Cursor's own Tab model); `CompletionSettingsLoader` (user-configured provider) | OmpOS uses the provider the user configures |
| AC-144 | Cmd+K inline edit (retired: hand-off to Agent) | NOT-STARTED | `InlineEditView`, `InlineEditClient`, `InlineEditTests` (the retired flow, unwired) | The replacement (selection becomes a referenced-lines prompt) is not built: Ctrl+Shift+L selection-as-context was left out of the Mentions work (report Mentions deviation 5) |
| AC-145 | Terminal Cmd+K | NOT-STARTED | none | Not documented by Cursor either; not built |

## 13. Review, Bugbot and git (AC-150 to AC-155)

Counts (6 rows): MET 0, MET-LIVE 0, PARTIAL 1, DEFERRED 0, NOT-STARTED 3, OMITTED-BY-DECISION 2.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-150 | Agent Review | NOT-STARTED | `SlashCommandCatalogue` (`/agent-review` Unavailable); omp `reviewer` agent (04-omp-cursor-map.md row 17) | No review service or Source Control tab; no BUGBOT.md reader |
| AC-151 | Bugbot | OMITTED-BY-DECISION | Fit needs-cloud / omit-no-accounts (01-cursor-agent-chat.md); `SlashCommandCatalogue` (`/review-bugbot`: Bugbot is Cursor-hosted) | Cursor-hosted reviewer; a PR reviewer on OmpOS cloud would be new work |
| AC-152 | Bugbot repo config (bugbot.yaml) | NOT-STARTED | none | No reader of `.cursor/config/bugbot.yaml` (grep) |
| AC-153 | PR summaries | NOT-STARTED | none | Depends on a PR reviewer |
| AC-154 | Bugbot API | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | Enterprise Cursor API |
| AC-155 | Review in editor, /review, /split-to-prs, /cursor-blame | PARTIAL | `DiffReviewView`, `ChangesBarView`, `SlashCommandCatalogue` (`/review` and `/split-to-prs` run a SKILL.md if installed; `/cursor-blame` Unavailable) | No bundled review skills; no AI-authored-change attribution |

## 14. Models, privacy and billing (AC-160 to AC-176)

Source: `01-cursor-agent-chat.md` section 9. OmpOS sells no inference, so billing and Cursor-hosted model routing are OMITTED-BY-DECISION; the model picker and bring-your-own-key shapes are built.
Counts (12 rows): MET 3, MET-LIVE 1, PARTIAL 2, DEFERRED 0, NOT-STARTED 0, OMITTED-BY-DECISION 6.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-160 | Model picker | MET | `ModelPickerModel`, `ModelPickerView`, `ModelPickerModelTests`, `ModelsSettingsSource`; web: src/OmpOS.UI/Components/Composer/ModelPicker.razor | Ctrl+/ cycling and a palette Select Model are not bound; a switch was not observed with a live turn |
| AC-161 | Model parameters (fast, effort, context) | PARTIAL | `OptionChipModel`, `OptionChipModelTests`, `LiveOmpOptionChipsTests` | Thinking level only (omp's `thinking` option); no `id[key=value]` parameters |
| AC-162 | Auto and Cursor Router | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | Cursor inference; OmpOS sells none |
| AC-163 | Bring your own API key | MET-LIVE | `ModelsSettingsSource`, `OmpCredentialStore`, `ModelsApiKeyTests`, `LiveOmpConfigTests`, `ProviderCatalogue`, `ModelsListingViewTests` (103 provider rows) | Live proof is the 2026-10-26 audit's recorded omp 18.12.6 run without a model turn, not re-run here; no real-credential call (OQ-71); web: `ApiModelsSettingsSource` talks to API providers endpoints not yet mapped in `Program.cs` |
| AC-164 | Custom models and endpoints | MET | `CustomProviderDraft`, `ModelsCustomProviderTests`, `OmpModelsFile` | Superset of Cursor: any OpenAI-compatible base URL |
| AC-165 | Max Mode | OMITTED-BY-DECISION | Fit omit-no-accounts (use the context-size parameter instead); `SlashCommandCatalogue` (`/max-mode` Unavailable) | Plan feature of legacy Cursor plans |
| AC-166 | Model access control, hidden models | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | Team dashboard |
| AC-170 | Privacy Mode | MET | `GeneralPage`, `SettingsCopy`, `GeneralPageTests`, `docs/settings-parity.md` row 19 | Read-only statement: OmpOS sends nothing itself, so there is no toggle |
| AC-171 | Zero data retention and data controls | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | Cursor dashboard and CMEK |
| AC-172 | Workspace trust | OMITTED-BY-DECISION | `docs/settings-parity.md` row 20 | No restricted mode exists; Wave 2 `HookDispatcher` consent and `RunModePolicy` trust flag are their own gates |
| AC-175 | Usage visibility | PARTIAL | `UsageHistory`, `UsageHistoryTests`, `TranscriptExportView`; `/usage` Delegated in `SlashCommandCatalogue` | Usage history is always empty in the app (`MainView.CreateExport` passes none); omp's usage_update is not consumed (04-omp-cursor-map.md row 21) |
| AC-176 | Plans and billing | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | OmpOS has no accounts or billing |

## 15. Cloud agents (CA, EN, API, AU, IN rows and AC-190 to AC-194)

Sources: `02-cursor-cloud-remote.md` sections A to E and `01-cursor-agent-chat.md` section 10. Sub-series: CA (36 rows), EN (19), API (12), AU (19), IN (17), AC-190 to AC-194 (5). The control plane is `services/OmpOS.Api/Cloud`; the worker is `services/OmpOS.AgentWorker`; the Railway and static-pool clients are in `src/OmpOS.Sandbox`; the presentation models are `src/OmpOS.Presentation/Cloud`. Nothing here ran against Railway.
Counts (108 rows): MET 0, MET-LIVE 0, PARTIAL 28, DEFERRED 6, NOT-STARTED 52, OMITTED-BY-DECISION 22.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| CA-01 | Cloud agent core (VM, branch, PR) | PARTIAL | `CloudAgentService`, `CloudAgentEndpoints`, `CloudAgentLifecycleTests`, `CloudAgentAccessTests`, `CloudPullRequestTests`, `RailwayCloudEnvironmentClient`, `StaticPoolEnvironmentClient`, `AcpBridgeEndpoint`, `AcpWebSocketTransport`, deploy/worker.Containerfile | Control plane, worker bridge and Railway client built and tested with fakes; Railway deploy never done (OD-03, OQ-100); worker does not yet clone, branch or run the environment file (report CloudControlPlane); cloud endpoints are not yet registered in `Program.cs` of `services/OmpOS.Api` (Wave 3) |
| CA-02 | Naming (formerly Background Agents) | OMITTED-BY-DECISION | 01-cursor-agent-chat.md and 02-cursor-cloud-remote.md (naming fact, Fit '-') | Not a feature; OmpOS calls them cloud agents |
| CA-03 | Start surfaces (iOS, web, desktop, Slack, GitHub, Linear, API) | PARTIAL | `CloudAgentsPageView`, `NewCloudAgentView`, `CloudAgentsPageViewTests`, `NewCloudAgentViewTests`, `CloudAgentEndpoints` | Desktop page and API exist; the page is not in any head (`SettingsSection`, `MainView` do not construct it); no iOS, Slack, GitHub-comment or Linear start; head wiring is Wave 3 work: verify after the Wave 3 merge |
| CA-04 | Repo provider clone (GitHub, GitLab, ADO, Bitbucket) | PARTIAL | `GitHubAppClient`, `PatCredentialSource`, `PullRequestService`, `CloudPullRequestTests`, `FakeGitHubHandler` | GitHub only (App token or PAT); GitLab, Bitbucket, Azure DevOps not built; token expires after 1 h and is not refreshed in a long-lived worker (report CloudControlPlane) |
| CA-05 | Start from scratch (Origin repo) | OMITTED-BY-DECISION | Fit omit-no-accounts (Origin is Cursor-hosted), 02-cursor-cloud-remote.md | Cursor-hosted forge |
| CA-06 | Port forwarding and preview | NOT-STARTED | `docs/runbooks/cloud-agents.md` | No port detection or public domain code; Railway domain or TCP proxy not used |
| CA-07 | Publish to live URL | OMITTED-BY-DECISION | Fit omit-no-accounts (Vercel plugin), 02-cursor-cloud-remote.md | A Railway publish step would be new work |
| CA-08 | Computer use | DEFERRED | `docs/runbooks/cloud-terminals.md`, `CloudSessionHost` | No remote desktop until Railway confirms in writing (OQ-73) |
| CA-09 | Artifacts and demos | NOT-STARTED | none | No artifact storage or PR embedding |
| CA-10 | Remote desktop control | DEFERRED | `docs/runbooks/cloud-terminals.md` | Same blocker as CA-08 (OQ-73) |
| CA-11 | MCP in cloud | NOT-STARTED | `CreateCloudAgentRequest` (no MCP field) | No MCP server selection for cloud agents; worker passes none |
| CA-12 | Cursor Cloud diagnostics MCP | NOT-STARTED | none | Optional; `CloudMetrics` exposes counters only |
| CA-13 | Run events | PARTIAL | `CloudStatusEvent`, `CloudTimelineRow`, `CloudAgentDetailsModelTests`, `CloudAgentEndpoints` (GET events) | Status events only; no setup_started or pr_creation_failed kinds; view unwired |
| CA-14 | Subscriptions and /subscribe | NOT-STARTED | none | No webhook receiver or wake queue |
| CA-15 | Auto-fix CI failures | NOT-STARTED | none | No CI listener |
| CA-16 | OIDC identity tokens | NOT-STARTED | `WorkerTokenIssuer` (worker auth only) | Optional; not built |
| CA-17 | Agent metadata socket | NOT-STARTED | none | Not built |
| CA-18 | Hooks in cloud | NOT-STARTED | `HookLocatorContext` (remote flag only) | No cloud hook subset shipped to the worker |
| CA-19 | Models in cloud | PARTIAL | `CreateCloudAgentRequest` (Model), `CloudCreateRequestValidator`, `CloudInputRulesTests` | Model id passed through; no curated list or context-size choice |
| CA-20 | Share agents | NOT-STARTED | `CloudAgentAccessTests` (owner scope only) | Owner-only access; no read-only share link |
| CA-21 | Team follow-ups setting | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md` | Team and admin controls deferred (OD-03, OQ-61) |
| CA-22 | Cloud subagents (/in-cloud, /autopilot) | PARTIAL | `CloudHandoff`, `CloudHandoffTests`, `SlashCommandCatalogue` | Same as AC-014: catalogue Unavailable, no host |
| CA-23 | Move to Cloud and & handoff | PARTIAL | `CloudHandoff`, `CloudHandoffTests`, `ICloudHandoff` | Model only; `&` prefix not handled; head wiring is Wave 3 work: verify after the Wave 3 merge |
| CA-24 | Remote Control (local loop to cloud) | DEFERRED | `docs/settings-parity.md` row 28 | Sign-in authority (OD-03) and second-device decision (OQ-68) |
| CA-25 | Projects | NOT-STARTED | none | See AC-013 |
| CA-26 | Mobile (iOS, Android PWA) | OMITTED-BY-DECISION | Fit omit-no-accounts for the native iOS app, 02-cursor-cloud-remote.md; OmpOS heads `apps/OmpOS.Android` and `apps/OmpOS.Web` | Android head builds locally only; iOS needs a Mac (Phase 24 criteria 1 to 3); web is the PWA-like path |
| CA-27 | Cloud settings: defaults (model, repo, base branch) | PARTIAL | `CloudOptions`, `CloudOptionsValidatorTests`, `CloudAgentsOptions` | Operator options only; no per-user default page |
| CA-28 | Cloud settings: security | NOT-STARTED | none | No summary or follow-up policy settings |
| CA-29 | Privacy Mode requirement | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor plan rule |
| CA-30 | Data lifecycle (archive, delete, idle) | PARTIAL | `CloudIdleSweeper`, `CloudOrphanScanner`, `CloudOrphanTests`, `InMemoryCloudAgentStore` | Idle sweep and orphan report built; transcripts survive agent delete (`ISessionRepository` has no delete); in-memory store loses state on restart (OQ-85) |
| CA-31 | Signed commits | NOT-STARTED | `GitHubAppClient` | Commits are not signed by an OmpOS key |
| CA-32 | Self-hosted My Machines (outbound worker) | NOT-STARTED | `SshAcpTransport` (the OmpOS analogue is SSH, see RD rows) | OmpOS remote environments run omp on the remote host, not an outbound worker |
| CA-33 | Self-hosted Team Pools | PARTIAL | `StaticPoolEnvironmentClient`, `StaticPoolEnvironmentOptions` | Static pool of workers only; no labels, queue, autoscaling or session tokens |
| CA-34 | Partner hosts for workers | NOT-STARTED | `CloudContainer` (Railway and static pool only) | No Lambda, Modal, Daytona or Kubernetes templates |
| CA-35 | Worker flags | NOT-STARTED | none | Not built |
| CA-36 | Pricing | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | The cost sentence is shown instead: `NewCloudAgentModel` requires `CostConfirmed` |
| EN-01 | Ubuntu VMs | PARTIAL | deploy/worker.Containerfile, `WorkerEntrypointScriptTests` | Worker image defined, never built or deployed by this programme; Railway has no per-task microVM (02-cursor-cloud-remote.md section K) |
| EN-02 | Agent-driven setup | NOT-STARTED | none | No setup agent or first Build |
| EN-03 | environment.json | PARTIAL | `EnvironmentConfigFile`, `EnvironmentConfigResolver`, `EnvironmentConfigFileTests` | Parsed and resolved by the API; the worker does not execute install, start or terminals |
| EN-04 | Image-build environment (Cursor's build-file option) | PARTIAL | `EnvironmentBuild`, `EnvironmentConfigFileTests` | Parsed only; no image build step |
| EN-05 | Resolution order | PARTIAL | `EnvironmentConfigResolver`, `EnvironmentConfigFileTests` | Personal and team saved environments do not exist; file only |
| EN-06 | Multi-repo environments | PARTIAL | `CloudWorkerRepository`, `CloudWorkerVariables`, `CreateCloudAgentRequest` | Variables carry the repositories; worker clone missing; PRs per repo not built |
| EN-07 | Builds (snapshots) | NOT-STARTED | none | No build or snapshot pipeline |
| EN-08 | Startup commands and terminals | PARTIAL | `EnvironmentTerminal`, `EnvironmentConfigFileTests` | Parsed; not run on boot |
| EN-09 | AGENTS.md cloud section | PARTIAL | omp reads AGENTS.md (04-omp-cursor-map.md row 4) | No cloud run observed |
| EN-10 | Nested container runtime in the agent | NOT-STARTED | none | Not built |
| EN-11 | Tailscale and Cloudflare Tunnel | NOT-STARTED | none | Not built |
| EN-12 | Secrets (env, runtime, build) | PARTIAL | `CloudSecretResolver`, `InMemoryCloudSecretStore`, `CloudSecretRedactor`, `CloudSecretTests`, `CloudSecretRedactorTests`, `WorkerSecretRedactorTests` | In-memory store (lost on restart, not shared); no Build secret type; no UI page |
| EN-13 | AWS IAM role assumption | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor-side role assumer |
| EN-14 | TOTP and login secrets | NOT-STARTED | none | Not built |
| EN-15 | Network access modes | NOT-STARTED | `NetworkPolicyRule` (hosted sandbox provider policy, not cloud agents) | No egress proxy for cloud agents |
| EN-16 | Git egress proxy IP list | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor-published IPs |
| EN-17 | Private connectivity | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Enterprise contact-sales feature |
| EN-18 | Customer-managed keys | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Enterprise feature |
| EN-19 | Environment versioning | NOT-STARTED | none | No version history |
| API-01 | Cloud Agents API v1 | PARTIAL | `CloudAgentEndpoints`, `CloudAgentLifecycleTests`, `CloudApiHost`, docs/api/openapi.json | OmpOS's own route shape (`/cloud/agents`), not Cursor's `/v1/agents`; cloud endpoints are not yet registered in `Program.cs` of `services/OmpOS.Api` (Wave 3) |
| API-02 | Agents endpoints (create, list, get, delete, archive) | PARTIAL | `CloudAgentEndpoints`, `CloudAgentAccessTests`, `CloudInputRulesTests` | create, list, get, delete, stop and wake exist; no archive, unarchive or usage route |
| API-03 | Runs (follow-up, stream, cancel) | PARTIAL | `CloudAgentEndpoints` (prompts, events, cancel), `CloudAgentHarnessSession` | Prompt, event stream and cancel exist; no per-run stream or run list; cancel is per API instance |
| API-04 | Artifacts | NOT-STARTED | none | No artifact route |
| API-05 | Environments API | NOT-STARTED | `EnvironmentConfigFile` | No environment, build or secret-per-environment CRUD; secrets route exists (`CloudAgentEndpoints`) |
| API-06 | Workers and pools API | NOT-STARTED | `StaticPoolEnvironmentClient` | No pool, claim or session-token routes |
| API-07 | Misc (/me, /models, /repositories) | PARTIAL | `CatalogEndpoints` (session catalog), `CloudAgentEndpoints` (GET limits) | No model or repository listing route |
| API-08 | Webhooks (statusChange) | NOT-STARTED | none | No webhook sender (grep) |
| API-09 | TypeScript SDK | NOT-STARTED | `IOmpApiClient` (web client, not an SDK) | No published SDK |
| API-10 | Agent SDK | OMITTED-BY-DECISION | Fit omit-no-accounts [INFERENCE], 02-cursor-cloud-remote.md | Cursor product |
| API-11 | Admin, Analytics, AI Code Tracking, Origin APIs | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md` | Admin and analytics deferred (OD-03, OQ-61) |
| API-12 | Terraform provider | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor product |
| AU-01 | Automations (cloud agents on triggers) | PARTIAL | `AutomationEngine`, `AutomationsViewModel`, `AutomationsViewModelTests`, `WorkspaceAutomationsSource` | Local engine and list view; nothing wires `Harness.Automations` into a head or cloud (previous audit CX-02); no cloud-run triggers |
| AU-02 | Scheduled trigger | PARTIAL | `ScheduledTrigger`, `AutomationSchedule` | Library only |
| AU-03 | Source-control triggers | NOT-STARTED | `EventTrigger` (generic events) | No GitHub or GitLab event receiver |
| AU-04 | Slack triggers | NOT-STARTED | none | Not built |
| AU-05 | Webhook trigger | NOT-STARTED | none | Not built |
| AU-06 | Linear triggers | NOT-STARTED | none | Not built |
| AU-07 | Sentry triggers | NOT-STARTED | none | Not built |
| AU-08 | PagerDuty triggers | NOT-STARTED | none | Not built |
| AU-09 | Automation tools (PR, comment, Slack, memories) | NOT-STARTED | 04-omp-cursor-map.md row 5 (omp memory) | No tool grants per automation |
| AU-10 | Run as, share, visibility | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md` | Needs accounts (OD-03) |
| AU-11 | Bugbot PR reviewer | NOT-STARTED | `SlashCommandCatalogue` (`/review-bugbot` Unavailable); omp `reviewer` agent (04-omp-cursor-map.md row 17) | No PR-event reviewer; see AC-151 |
| AU-12 | Bugbot config file | NOT-STARTED | none | No reader |
| AU-13 | Bugbot rules (BUGBOT.md) | NOT-STARTED | none | No reader |
| AU-14 | Bugbot Autofix | NOT-STARTED | none | Not built |
| AU-15 | Bugbot API | NOT-STARTED | none | Not built |
| AU-16 | Bugbot pricing | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor billing |
| AU-17 | Security Agents | NOT-STARTED | omp `security-reviewer` agent and `/security` (04-omp-cursor-map.md row 17) | No OmpOS gate or scanner UI |
| AU-18 | PR Routing and Approval | NOT-STARTED | none | Not built |
| AU-19 | Rollouts | NOT-STARTED | none | Not built |
| IN-01 | GitHub app | PARTIAL | `GitHubAppClient`, `InstallationTokenCache`, `GitHubHostClient`, `CloudPullRequestTests` | OmpOS's own GitHub App credential path; no app registered or installed (owner action); no CI re-run or branch-protection read |
| IN-02 | GitHub Enterprise Server | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor Enterprise App |
| IN-03 | GitLab | NOT-STARTED | `IGitHostClient` (GitHub only implements it) | Not built |
| IN-04 | Bitbucket | NOT-STARTED | `IGitHostClient` | Not built |
| IN-05 | Azure DevOps | NOT-STARTED | `IGitHostClient` | Not built |
| IN-06 | Slack | NOT-STARTED | none | No Slack app |
| IN-07 | Linear | NOT-STARTED | none | Delegation from Linear not built (the Linear MCP is a connector of the maintainers, not product code) |
| IN-08 | Jira | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Atlassian marketplace app |
| IN-09 | Microsoft Teams | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Microsoft marketplace app |
| IN-10 | Notion | NOT-STARTED | none | Not built |
| IN-11 | Sentry and PagerDuty | NOT-STARTED | none | See AU-07, AU-08 |
| IN-12 | Tailscale | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor-joined tailnet device |
| IN-13 | JetBrains and Xcode | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor IDE integrations |
| IN-14 | Cursor Blame | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Enterprise attribution |
| IN-15 | Origin (Cursor git forge) | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor-hosted forge |
| IN-16 | Deep links | NOT-STARTED | `MentionUris` (unrelated) | No URL scheme handler; see AC-029 |
| IN-17 | Required domains | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor domains; OmpOS lists its own provider hosts in `NetworkDiagnosticsSource` |
| AC-190 | Cloud Agents (summary row of file 01) | PARTIAL | See CA-01 | Same blockers as CA-01 |
| AC-191 | Self-hosted workers and pools | NOT-STARTED | See CA-32 and CA-33 | Same as CA-32 |
| AC-192 | Automations (summary row of file 01) | PARTIAL | See AU-01 | Same as AU-01 |
| AC-193 | Cursor CLI | PARTIAL | `OmpTerminalCommand`, `OmpTerminalCommandTests`, `LiveOmpTerminalTests`; the CLI is omp | OmpOS starts omp in its terminal pane (recorded live without a model turn); the CLI features are omp's, not OmpOS code |
| AC-194 | Cursor SDK and Cloud Agents API | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | Cursor product; OmpOS API is API-01 |

## 16. Remote development (RD rows)

Source: `02-cursor-cloud-remote.md` section F (no primary Cursor page exists for remote development; the inventory marks most rows secondary). The OmpOS design is `ADR-0015-remote-environments.md`: omp runs on the remote host over `ssh host omp acp`.
Counts (9 rows): MET 0, MET-LIVE 0, PARTIAL 4, DEFERRED 2, NOT-STARTED 2, OMITTED-BY-DECISION 1.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| RD-01 | Remote SSH, AI runs locally | PARTIAL | `SshAcpTransport`, `SshAcpTransportTests`, `RemoteLaunchMaterializer`, `RemoteSessionResources`, `RemotePreflight`, `ADR-0015-remote-environments.md`, `docs/runbooks/remote-ssh.md` | Design differs by decision (OQ-97): omp runs on the remote host over `ssh host omp acp`, files stay remote; never run against a real host by this programme (`RemoteSshLiveTests` is a guarded Live test; no recorded run); `RemoteEnvironmentsPageView` and `EnvironmentPickerView` are not in any head; head wiring is Wave 3 work: verify after the Wave 3 merge |
| RD-02 | SSH keep-alive | PARTIAL | `SshCommandBuilder` (ServerAliveInterval, ServerAliveCountMax 3), `SshCommandBuilderTests`, `ReconnectingEngineHost`, `ReconnectPolicyTests` | Same as RD-01; not run live |
| RD-03 | Agents Window remote SSH | PARTIAL | `NewSessionTargetModel`, `EnvironmentPickerView`, `RemoteEnvironmentsViewModel`, `NewSessionTargetModelTests`, `RemoteViewTests` | Session target picker built; `SessionEngineSet` does not start a remote session yet; head wiring is Wave 3 work: verify after the Wave 3 merge |
| RD-04 | Anysphere remote extensions | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md` | VS Code extension compatibility deferred (OQ-60); OmpOS replaces the extension with `Harness.Remote` |
| RD-05 | Dev Containers | NOT-STARTED | `RemotePreflight` (ssh hosts only); report RemoteCore ('devcontainers: next wave') | No container attach |
| RD-06 | Remote extension host | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md` | Needs an extension host (Phase 30) |
| RD-07 | Port forwarding and remote terminal | PARTIAL | `RemoteTerminalLauncher`, `RemoteTerminalLauncherTests`, `ReverseForwardAllocator` | Remote terminal command builders exist and are not shown in a pane; `-R` forwards serve the hook and MCP bridges only; no user port forwarding |
| RD-08 | Tunnels | NOT-STARTED | none | No Cursor doc either (02-cursor-cloud-remote.md) |
| RD-09 | Extension control (allowlist, MDM) | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor admin portal and MDM |

## 17. Workspaces (WS rows)

Counts (13 rows): MET 0, MET-LIVE 0, PARTIAL 11, DEFERRED 0, NOT-STARTED 0, OMITTED-BY-DECISION 2.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| WS-01 | Multi-root workspace (.code-workspace) | PARTIAL | `CodeWorkspaceFile`, `CodeWorkspaceFileTests`, `WorkspaceSetStore`, `WorkspaceSetStoreTests`, `MultiRootWorkspaceTreeTests` | Library and models; no head opens or saves a workspace set (`WorkspaceSetSlashHandler` unwired); head wiring is Wave 3 work: verify after the Wave 3 merge |
| WS-02 | Multi-root in Agents Window | PARTIAL | `OmpLaunchPlanner` (`--add-dir`), `OmpLaunchPlannerTests`, `SessionEngineSet`, `WorktreeRunSetTests` | Launch carries extra roots; omp applies `--add-dir` per process, not per session over ACP (04-omp-cursor-map.md row 14); not probed in a live turn |
| WS-03 | Multi-workspace Agents Window | PARTIAL | `WorkspaceSetPicker`, `WorkspaceSetPickerTests` | See AC-009: no open-folder action in the head |
| WS-04 | Cursor 3 GA | OMITTED-BY-DECISION | 02-cursor-cloud-remote.md (dated fact, Fit '-') | Not a feature |
| WS-05 | Multi-repo cloud env | PARTIAL | See EN-06 | Same as EN-06 |
| WS-06 | Worktrees (Agents Window) | PARTIAL | See AC-131 | Same as AC-131 |
| WS-07 | worktrees.json (setup scripts) | PARTIAL | `CursorWorktreesConfig`, `CursorWorktreeSetup`, `ConsentedWorktreeSetupRunner`, `CursorWorktreesConfigTests`, `WorktreeSetupRunnerTests` | Parsed and run behind a consent gate in the library; no head calls the runner; head wiring is Wave 3 work: verify after the Wave 3 merge |
| WS-08 | Worktree cleanup and max count | PARTIAL | `WorktreeCapExceededException`, `WorktreeManagerTests` | Cap enforced in `WorktreeManager`; no cleanup interval and no Settings key (settings-parity row 25 Omitted) |
| WS-09 | /worktree, /apply-worktree, /delete-worktree | PARTIAL | See AC-132 | Same as AC-132 |
| WS-10 | /best-of-n | PARTIAL | `BestOfNState`, `WorktreeCommandsModelTests` | Per-index candidates, not per-model; host seam `IBestOfNHost` has no implementation |
| WS-11 | CLI worktrees flags | PARTIAL | omp `omp worktree` (04-omp-cursor-map.md row 13) | Provided by omp's CLI; OmpOS has no CLI flags |
| WS-12 | Subagents | PARTIAL | See AC-084 | Same as AC-084 |
| WS-13 | /multitask | OMITTED-BY-DECISION | 02-cursor-cloud-remote.md (secondary aggregator source); `SlashCommandCatalogue` (not one of the 92 tokens); OQ-95 | No primary documentation row; not in the Cursor 3.27 token list of file 03 |

## 18. CLI (CL rows)

Counts (21 rows): MET 0, MET-LIVE 0, PARTIAL 16, DEFERRED 1, NOT-STARTED 2, OMITTED-BY-DECISION 2.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| CL-01 | CLI modes (agent, plan, ask, debug) | PARTIAL | `SlashCommandCatalogue` (`/plan` Built; `/ask` and `/debug` Partial) | Plan only; omp advertises no ask or debug mode |
| CL-02 | Print and headless | PARTIAL | 04-omp-cursor-map.md section B5 (omp print and RPC modes) | omp provides it; OmpOS adds no CLI |
| CL-03 | Force and yolo | PARTIAL | `AgentsPage` (auto-run switch), omp `--yolo` (04-omp-cursor-map.md row 12) | omp's flag; OmpOS exposes only the terminal auto-run switch |
| CL-04 | Sessions (ls, resume, fork, rewind) | PARTIAL | `SessionCatalogModel`, `ReloadableSessionCatalog`, `ForkModel` | Rail lists and resumes chats; /resume, /fork, /rewind commands see slash section |
| CL-05 | CLI sandbox | DEFERRED | 04-omp-cursor-map.md row 12 | omp has no sandbox; settings-parity rows 4 and 26 |
| CL-06 | CLI permissions (cli.json tokens) | PARTIAL | `PermissionToken`, `PermissionTokenParserTests`, `ShellMatchingTests`, `PermissionsFileReader` | Parses Shell, Read, Write, WebFetch and Mcp tokens in OmpOS's permissions.json; not wired; does not read cli.json; head wiring is Wave 3 work: verify after the Wave 3 merge |
| CL-07 | CLI config (cli-config.json) | OMITTED-BY-DECISION | `docs/settings-parity.md` row 34 | Cursor's CLI configuration is not part of OmpOS Settings; omp's own config is edited through the Models page |
| CL-08 | Global CLI options | PARTIAL | `OmpTerminalCommand`, `OmpTerminalCommandTests` | omp's options; OmpOS builds the terminal command only |
| CL-09 | CLI commands (login, status, models, mcp, worker...) | PARTIAL | `ModelsSettingsSource` (uses `omp models`, `omp token`) | omp's commands; OmpOS wraps a few |
| CL-10 | MCP in CLI | PARTIAL | See AC-110 | Same as AC-110 |
| CL-11 | Rules in CLI | PARTIAL | 04-omp-cursor-map.md row 4 | omp loads `.cursor/rules` and AGENTS.md itself |
| CL-12 | ACP server | PARTIAL | `AcpAgentSession`, `AcpWireMapping`, `OmpOS.Harness.Acp.Tests`, `LiveOmpHandshakeTests` | OmpOS is an ACP client of omp (omp acp), not an ACP server; handshake recorded live without a model turn; Cursor extensions (ask_question, create_plan) not handled |
| CL-13 | CLI slash commands | PARTIAL | `SlashCommandCatalogue` | Per-token statuses in section 21 |
| CL-14 | Shell mode | PARTIAL | `TerminalPanelView`, `SlashCommandCatalogue` (`/shell` Partial) | Opens a terminal; the text after /shell is not run |
| CL-15 | Custom modes via skills | PARTIAL | See AC-006 | Same as AC-006 |
| CL-16 | Cloud handoff & | PARTIAL | See CA-23 | Same as CA-23 |
| CL-17 | Sudo prompting | NOT-STARTED | none | Not built |
| CL-18 | CI and GitHub Actions | PARTIAL | omp print mode (04-omp-cursor-map.md section B5) | No OmpOS action or cookbook |
| CL-19 | CLI authentication | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | No Cursor login; provider keys go to omp |
| CL-20 | Worker mode | NOT-STARTED | See CA-32 | Same as CA-32 |
| CL-21 | CLI plugins | PARTIAL | `SlashCommandCatalogue` (`/plugin` Delegated), `PluginsPage` | omp `/plugins`; marketplace separate |

## 19. Team, admin and accounts (AD rows and AC-01 to AC-08)

Sources: `02-cursor-cloud-remote.md` sections I and J. OmpOS has no accounts, teams or billing and no sign-in authority (OD-03). Rows whose Fit is omit-no-accounts are OMITTED-BY-DECISION; the other admin rows are DEFERRED to the admin and analytics item (`docs/runbooks/phase-30-compatibility-admin.md`).
Counts (33 rows): MET 0, MET-LIVE 0, PARTIAL 3, DEFERRED 9, NOT-STARTED 3, OMITTED-BY-DECISION 18.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AD-01 | Roles and seats | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md` | Roles need accounts (OD-03); billing omitted |
| AD-02 | SSO (SAML) | DEFERRED | `docs/runbooks/hosting.md` | Needs the sign-in authority (OD-03) |
| AD-03 | SCIM | OMITTED-BY-DECISION | Fit omit-no-accounts [INFERENCE], 02-cursor-cloud-remote.md | Enterprise identity provisioning |
| AD-04 | Organizations and groups | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | No teams |
| AD-05 | MDM policies | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor client policy |
| AD-06 | Workspace Trust | OMITTED-BY-DECISION | `docs/settings-parity.md` row 20 | No restricted mode; Fit says needs-ui |
| AD-07 | Audit log | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md`; `HookRunLog` (hook runs only) | Admin audit deferred (OD-03) |
| AD-08 | OpenTelemetry export | NOT-STARTED | `CloudMetrics` | No OTLP export of agent events |
| AD-09 | Usage analytics | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md` | Admin analytics deferred (OQ-61) |
| AD-10 | AI code tracking | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor Enterprise API |
| AD-11 | Spend limits | OMITTED-BY-DECISION | Fit omit-no-accounts [INFERENCE: reusable as budget caps on BYOK keys], 02-cursor-cloud-remote.md | OmpOS has no billing |
| AD-12 | Team rules and marketplaces | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md` | Needs teams; see AC-063 |
| AD-13 | Model access control | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md` | Admin allowlist deferred |
| AD-14 | BYOK restriction | OMITTED-BY-DECISION | Fit omit-no-accounts (OmpOS is BYOK-first), 02-cursor-cloud-remote.md | Contradicts the product model |
| AD-15 | MCP allowlist and per-server controls | PARTIAL | `McpServerCatalog`, `McpConsentIdentity`, `PermissionsFileReader`, `McpServerCatalogTests` | Per-user consent and permissions.json allowlist as libraries; no admin allowlist; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AD-16 | Git repo blocklist | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md` | Admin feature |
| AD-17 | Protected Git scopes | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor org binding |
| AD-18 | Privacy mode and enforcement | DEFERRED | `docs/settings-parity.md` row 19 | Statement only; no admin enforcement |
| AD-19 | Data residency | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor Enterprise |
| AD-20 | Network and proxy | PARTIAL | `NetworkSettingsPage`, `NetworkDiagnosticsSource`, `NetworkSettingsPageTests` | Diagnostics only; HTTP compatibility switch and proxy settings not built (settings-parity open points) |
| AD-21 | Service accounts | NOT-STARTED | `JwtBearerSetup`, `CloudWorkerGuard` (worker tokens only) | The OmpOS API authenticates with JWT bearer tokens from a sign-in authority that is not configured (OD-03); no service-account or API-key concept |
| AD-22 | Pooled usage and billing groups | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | No billing |
| AD-23 | Auto Run and Run Modes admin | PARTIAL | `RunModePolicy`, `RunModePolicyTests`, `AgentsPage` | Per-user only; no admin control; head wiring is Wave 3 work: verify after the Wave 3 merge |
| AD-24 | Other admin settings | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md` | Admin dashboard deferred |
| AD-25 | Security posture (SOC 2, trust centre) | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor's certification; OmpOS has `docs/runbooks/web-key-threat-model.md` for its own web risk |
| AC-01 | Individual plans | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | OmpOS has no plans |
| AC-02 | Teams and Enterprise plans | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | No plans |
| AC-03 | Legacy plans | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | No plans |
| AC-04 | The X Subscription | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor and Grok bundle |
| AC-05 | Usage-based add-ons | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | No billing |
| AC-06 | Billing mechanics | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | No billing |
| AC-07 | API keys | NOT-STARTED | `JwtBearerSetup`; provider keys are another feature (`ModelsApiKeyTests`) | No OmpOS API keys for CLI, SDK or cloud agents API; the API takes JWT bearer tokens (OD-03 pending) |
| AC-08 | Accounts needed | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | OmpOS has no account or billing system (OD-03 pending) |

## 20. Settings (S01 to S50)

Source: `03-cursor-settings-slash.md` table 1: the settings that are not among the repository's 34 rows, or whose documented behaviour changed. Statuses agree with `docs/settings-parity.md` as it stands in the tree (34 rows; 8 kept as a page, 4 shown inside a page, 22 omitted; eleven pages: Runtime, General, Agents, Models, Tab, Rules, Skills, Commands, Plugins, Network, Keyboard). That document is owned by the Settings agent and will be refreshed after the Wave 3 merge; where Wave 1 and Wave 2 code makes a row of it stale, the gap cell says 'drift' and the row keeps the status the document gives. Rows that restate a repository row: S06 (row 18), S13 to S15 and S17 (rows 2 to 4), S18 (row 28), S36 to S41, S45 (row 17), S47 (rows 14, 15), S48 (row 21).

Counts (50 rows): MET 5, MET-LIVE 0, PARTIAL 16, DEFERRED 6, NOT-STARTED 9, OMITTED-BY-DECISION 14.

| Row | Setting | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| S01 | cli-config.json version | OMITTED-BY-DECISION | `docs/settings-parity.md` row 34 | Cursor CLI configuration is not part of OmpOS Settings |
| S02 | editor.vimMode | PARTIAL | `DisplaySettings`, `FileDisplaySettingsStore`, `DisplayToggleSlashHandler`, `DisplaySettingsTests` | Flag is stored by the Wave 2 handler; the composer does not apply Vim keys; no Settings page; head wiring is Wave 3 work: verify after the Wave 3 merge |
| S03 | permissions.allow and deny tokens | PARTIAL | `PermissionTokenParser`, `PermissionTokenParserTests`, `ShellMatchingTests`, `EffectivePermissions` | `docs/settings-parity.md` row 3 says Run Mode and lists have no backing (drift: Wave 2 library exists, unwired); the Settings agent owns the refresh |
| S04 | approvalMode | PARTIAL | `AgentsPage`, `AgentsPageTests`, `RunModeSettingsPage`, `RunModePageViewTests` | `docs/settings-parity.md` row 3: only the terminal auto-run switch is built; the Run Mode page is a Wave 2 view with no registration; head wiring is Wave 3 work: verify after the Wave 3 merge |
| S05 | sandbox.mode, networkAccess, readBoundary | OMITTED-BY-DECISION | `docs/settings-parity.md` row 4 | Drift: `SandboxFileReader` and `SandboxFileTests` now parse sandbox.json; nothing enforces it |
| S06 | network.useHttp1ForAgent | NOT-STARTED | `docs/settings-parity.md` row 18 and Open points (HTTP compatibility switch not built) | No backing; `NetworkSettingsPage` only diagnoses |
| S07 | attribution flags | NOT-STARTED | none | No commit trailer or PR footer setting |
| S08 | notifications, hints, suggestNextPrompt, rewind | PARTIAL | `CheckpointSlashHandler` (rewind, undo, restore aliases), `CheckpointsModelTests` | Only rewind has code (Wave 2, unwired); no setting for the others |
| S09 | display.* toggles | PARTIAL | `DisplaySettings`, `DisplaySettingsValues`, `DisplayToggleSlashHandler`, `DisplaySettingsTests` | Flags exist; code blocks, transcript and status bar do not read them yet; head wiring is Wave 3 work: verify after the Wave 3 merge |
| S10 | model, maxMode, channel, statusLine | PARTIAL | `ModelsSettingsSource` (default model is omp's `modelRoles.default`), `ModelPickerModel` | Model only; Max Mode omitted, no channel or statusLine |
| S11 | Model slash commands toggle | NOT-STARTED | `SlashCommandCatalogue` (`/opus`, `/composer` always listed) | No switch |
| S12 | CLI proxy environment variables | NOT-STARTED | `NetworkSettingsPage` | Diagnostics only; proxy and CA settings not built |
| S13 | Auto-review classifier model | OMITTED-BY-DECISION | `docs/settings-parity.md` row 3 (Auto-review classifier not built); Fit N in 03-cursor-settings-slash.md | Cursor-hosted classifier; OmpOS would need its own model call |
| S14 | permissions.json autoRun instructions | PARTIAL | `PermissionsFileDocument` (AllowInstructions, BlockInstructions), `PermissionsFileReader`, `PermissionsFileTests` | Read and edit library only; no Auto-review classifier consumes them; head wiring is Wave 3 work: verify after the Wave 3 merge |
| S15 | sandbox.json read boundary | OMITTED-BY-DECISION | `docs/settings-parity.md` row 4 | Same drift as S05 |
| S16 | Team Read Controls | DEFERRED | `docs/settings-parity.md` row 31 (admin dashboard); Fit N in 03-cursor-settings-slash.md | Admin controls deferred (Phase 30, OD-03) |
| S17 | Run Modes names and history | PARTIAL | `RunMode` (AutoReview, Allowlist, RunEverything), `RunModePolicyTests`, `RunModeSettingsPage` | Enum and page exist, unregistered (report Safety WIRING SPEC); `docs/settings-parity.md` row 3 predates them |
| S18 | Remote Control and keep awake | DEFERRED | `docs/settings-parity.md` row 28 | Sign-in authority (OD-03) and OQ-68 |
| S19 | Agent Review setting | NOT-STARTED | `SlashCommandCatalogue` (`/agent-review` Unavailable) | No review service (AC-150) |
| S20 | Annotate and Batch edits | NOT-STARTED | `docs/settings-parity.md` row 27 | Browser pane not started (ADE-15) |
| S21 | Auto Optimize For (Router) | OMITTED-BY-DECISION | Fit N in 03-cursor-settings-slash.md | Cursor inference |
| S22 | Router admin controls | OMITTED-BY-DECISION | Fit N in 03-cursor-settings-slash.md | Team dashboard |
| S23 | Custom Modes (Use as Mode) | PARTIAL | `CustomModeModel`, `CustomModeModelTests` | See AC-006 |
| S24 | SKILL.md frontmatter fields | PARTIAL | `ConventionFrontmatterParser`, `ConventionFrontmatterParserTests`, `ConventionSkill` | Parsed and listed; omp decides which fields it honours |
| S25 | Skill discovery directories | PARTIAL | `ConventionDirectories`, `LocalConventionFileSourceTests`, `SkillsSettingsPage` | `docs/settings-parity.md` row 8 still says `.ompos/skills` only (drift: six directories at two scopes since Wave 1); see AC-071 |
| S26 | Rules frontmatter and matrix | PARTIAL | `ConventionRuleKind`, `ConventionCatalogTests`, `RulesSettingsPage` | `docs/settings-parity.md` row 7 says rule types are not built; `ConventionRuleKind` classifies .mdc, Markdown, AGENTS.md, CLAUDE.md and cursorrules; no editor |
| S27 | Subagent frontmatter | OMITTED-BY-DECISION | `docs/settings-parity.md` row 9 (delegation is omp and Swarm); `ConventionSubagent`, `ConventionCatalogTests` | Drift: Wave 1 discovery parses subagent files and lists them as /name (AC-081); the Settings agent owns the refresh |
| S28 | hooks.json entry fields | DEFERRED | `docs/settings-parity.md` row 12 (no head wires the hook host, OQ-84); `HooksConfigParser`, `HooksConfigParserTests`, `HooksSettingsPage`, `HooksSettingsPageTests` | Drift: the parser and the Hooks page now exist as a library and a Wave 2 view that no head registers; head wiring is Wave 3 work: verify after the Wave 3 merge |
| S29 | hooks.json events (21) | PARTIAL | `HookEvents`, `HookEventsTests` | See AC-100; cloud subset (14 events) not built |
| S30 | mcp.json fields and interpolation | OMITTED-BY-DECISION | `docs/settings-parity.md` row 11 (MCP is a tool surface, not a setting); `McpConfigLoader`, `McpConfigFormatTests`, `McpVariableExpansionTests`, `McpPageViewTests` | Drift: loader and a Wave 2 MCP page exist; `SettingsSection` has no MCP value yet; head wiring is Wave 3 work: verify after the Wave 3 merge |
| S31 | Customize per-scope activation | OMITTED-BY-DECISION | `docs/settings-parity.md` row 6 (umbrella omitted: scope filter needs teams) | `CustomizeCatalogPage` lists user and workspace items without a toggle |
| S32 | Menu bar Show Count | NOT-STARTED | none | No tray or badge code |
| S33 | Reduce Motion | MET | `GeneralPage`, `MotionPreferenceResolverTests`, `ReducedMotionTests`, `GeneralPageTests` | OmpOS has Motion follow system, reduce or full |
| S34 | Chat Width | NOT-STARTED | none | No setting |
| S35 | New-agent default environment | PARTIAL | `EnvironmentProfileStore`, `NewSessionTargetModel`, `NewSessionTargetModelTests` | Last-used remote profile is recorded; picker unwired; head wiring is Wave 3 work: verify after the Wave 3 merge |
| S36 | Queue versus steer | PARTIAL | `RunInputScheduler`, `SteeringInputModel`, `ShortcutCatalogueTests` | Keys differ (Enter sends, Ctrl+Enter steers, Ctrl+Shift+Enter follows up); see AC-015, AC-016 |
| S37 | Conversation search | MET | `TranscriptSearchModel`, `TranscriptSearchScopeTests` | See AC-020 |
| S38 | Context usage tray | PARTIAL | `ContextRingModel`, `ContextRingModelTests` | See AC-021 |
| S39 | BYOK rules | MET | `ModelsSettingsSource`, `ProviderCatalogue` (Azure OpenAI and AWS Bedrock rows in docs/reference/providers.md), `ModelsApiKeyTests` | Row 1 of `docs/settings-parity.md` was MET-LIVE in the previous audit; no real-credential call (OQ-71) |
| S40 | Privacy Mode (Legacy) blocks cloud features | OMITTED-BY-DECISION | Fit H in 03-cursor-settings-slash.md; `docs/settings-parity.md` row 19; see CA-29 | OmpOS has no Cursor plan rule |
| S41 | Never open PRs as drafts | DEFERRED | `docs/settings-parity.md` row 24; `CreateCloudPullRequestRequest` (Draft), `CloudOptions`, `CloudPullRequestTests` | Drift: a server option `Cloud:PullRequests:NeverDraft` exists in the API; no Settings page; OD-03 |
| S42 | Cloud environment lifecycle | DEFERRED | `docs/settings-parity.md` row 24 | OD-03 and paused hosted phases |
| S43 | Self-hosted worker settings | DEFERRED | `docs/settings-parity.md` row 24 | Same; see CA-32 |
| S44 | Bugbot team settings | OMITTED-BY-DECISION | Fit N in 03-cursor-settings-slash.md | Cursor-hosted |
| S45 | Tab settings | MET | `TabPage`, `TabSettingsSource`, `TabPageTests` | `docs/settings-parity.md` row 17: read-only status; no snooze or remap |
| S46 | Terminal tool environment (CURSOR_AGENT, UID mapping) | NOT-STARTED | `PtyProcessRunner` | No OmpOS agent marker variable (grep of `src`, `apps`) |
| S47 | Indexing and Docs | OMITTED-BY-DECISION | `docs/settings-parity.md` rows 14 and 15 | Nothing to build |
| S48 | Keyboard shortcut table (no Cmd+K inline edit) | MET | `ShortcutCatalogue`, `ShortcutCatalogueTests`, `KeyboardSettingsPage` | The catalogue lists only gestures with an observed effect, so it carries no stale inline-edit key |
| S49 | Grok Bot app settings | OMITTED-BY-DECISION | Fit N in 03-cursor-settings-slash.md | Separate Cursor product |
| S50 | CLI updates | OMITTED-BY-DECISION | Fit N in 03-cursor-settings-slash.md; `docs/settings-parity.md` row 34 | omp updates itself |

## 21. Slash tokens (92)

Source: `03-cursor-settings-slash.md` table 2 and the shipped catalogue `SlashCommandCatalogue` in `src/OmpOS.Presentation/Commands` (registry `SlashCommandRegistry`; reference `docs/reference/slash-commands.md`, generated from the catalogue). Each token is listed so typing it never silently does nothing: a token OmpOS cannot run is shown disabled with its reason. The desktop head runs the catalogue through `DesktopSlashActionHost` (actions `settings.open`, `chat.new`, `terminal.new`, `mode.set`, `model.pick`, `chats.side.open`, `help.show`); the web head implements the same interface as `WebSlashActionHost` for five of those actions (`WebSourcesTests`). The open-ended forms (`/skill-name`, `/custom-command`, `/subagent-name`, the `&` prefix) are not counted: custom commands and skills are AC-090 and AC-070. Delegated tokens (omp runs them) are MET only for the forwarding; their effect is omp's and was not observed live.

Counts (92 rows): MET 16, MET-LIVE 0, PARTIAL 53, DEFERRED 5, NOT-STARTED 3, OMITTED-BY-DECISION 15.

| Row | Token | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| SL-001 | /model | MET | `SlashCommandCatalogue` (NativeAction model.pick), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | None found |
| SL-002 | /run-everything | PARTIAL | `SlashCommandCatalogue` (NativeAction settings.open agents, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Opens Settings, whose Agents page holds the terminal auto-run switch; the words on, off and status are not read |
| SL-003 | /plan | MET | `SlashCommandCatalogue` (NativeAction mode.set plan), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | Effect not observed with a live model turn |
| SL-004 | /ask | PARTIAL | `SlashCommandCatalogue` (NativeAction mode.set ask, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Works only when the engine advertises an ask mode; omp 18.12.6 advertises default and plan. |
| SL-005 | /debug | PARTIAL | `SlashCommandCatalogue` (NativeAction mode.set debug, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Works only when the engine advertises a debug mode; omp 18.12.6 advertises default and plan. |
| SL-006 | /goal | DEFERRED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | omp runs /goal in its terminal UI and RPC only (04-omp-cursor-map.md row 31) |
| SL-007 | /logs | PARTIAL | `LogsSlashHandler` (Wave 2, `HostSeamSlashHandlerTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: Cursor CLI debug log; OmpOS keeps no such file. |
| SL-008 | /update | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Updates Cursor's own CLI |
| SL-009 | /max-mode | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Max Mode is a Cursor plan feature (Fit omit-no-accounts) |
| SL-010 | /rename | MET | `SlashCommandCatalogue` (OmpDelegated /rename), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-011 | /clear | MET | `SlashCommandCatalogue` (NativeAction chat.new), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | None found |
| SL-012 | /resume | NOT-STARTED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | The rail resumes chats; no command form exists |
| SL-013 | /fork | PARTIAL | `ForkSlashHandler` (Wave 2, `ModelSlashHandlerTests`, `ForkModelTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: omp runs /fork in its terminal UI only; forking over ACP is not wired in the ... |
| SL-014 | /summarize | MET | `SlashCommandCatalogue` (OmpDelegated /compact), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-015 | /rewind | PARTIAL | `CheckpointSlashHandler` (Wave 2, `ModelSlashHandlerTests`, `CheckpointsModelTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: omp's /branch (alias /rewind) runs in its terminal UI only; there is no rewind ... |
| SL-016 | /vim | PARTIAL | `DisplayToggleSlashHandler` (Wave 2, `DisplaySettingsTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: The OmpOS composer has no Vim mode. |
| SL-017 | /line-numbers | PARTIAL | `DisplayToggleSlashHandler` (Wave 2, `DisplaySettingsTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: Not built: no line-number switch for code blocks. |
| SL-018 | /show-thinking | PARTIAL | `DisplayToggleSlashHandler` (Wave 2, `DisplaySettingsTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: Not built: no command form for the thinking-block display. |
| SL-019 | /status-indicators | PARTIAL | `DisplayToggleSlashHandler` (Wave 2, `DisplaySettingsTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: Terminal title indicators belong to Cursor's CLI. |
| SL-020 | /shell | PARTIAL | `SlashCommandCatalogue` (NativeAction terminal.new, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests` | Opens a new terminal session; the text after the command is not run. |
| SL-021 | /about | PARTIAL | `AboutSlashHandler` (Wave 2, `HostSeamSlashHandlerTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: Cursor CLI version and account information; OmpOS has no Cursor account. |
| SL-022 | /setup-terminal | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Configures the Cursor CLI's newline keys |
| SL-023 | /help | MET | `SlashCommandCatalogue` (NativeAction help.show), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | None found |
| SL-024 | /feedback | PARTIAL | `FeedbackSlashHandler` (Wave 2, `HostSeamSlashHandlerTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: Sends feedback to Cursor's team. |
| SL-025 | /open | PARTIAL | `OpenFileSlashHandler` (Wave 2, `HostSeamSlashHandlerTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: Opens the Git root in the Cursor editor. |
| SL-026 | /copy-request-id | PARTIAL | `CopySlashHandler` (Wave 2, `HostSeamSlashHandlerTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: Cursor request ids; omp has none. |
| SL-027 | /copy-conversation-id | PARTIAL | `CopySlashHandler` (Wave 2, `HostSeamSlashHandlerTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: Cursor conversation ids; omp's /session shows the omp session. |
| SL-028 | /logout | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | No Cursor account to sign out of |
| SL-029 | /quit | PARTIAL | `QuitSlashHandler` (Wave 2, `HostSeamSlashHandlerTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: OmpOS is a window, not a terminal program; close the window. |
| SL-030 | /exit | PARTIAL | `QuitSlashHandler` (Wave 2, `HostSeamSlashHandlerTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: OmpOS is a window, not a terminal program; close the window. |
| SL-031 | /mcp | MET | `SlashCommandCatalogue` (OmpDelegated /mcp), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-032 | /plugin | PARTIAL | `SlashCommandCatalogue` (OmpDelegated /plugin (omp /plugins), Partial), `SlashCommandCatalogueTests`, `RuntimeSlashDispatchTests` | Sent to omp as typed; omp's /plugin is an alias of /plugins (list, enable, disable) |
| SL-033 | /config | MET | `SlashCommandCatalogue` (NativeAction settings.open), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | None found |
| SL-034 | /copy | PARTIAL | `CopySlashHandler` (Wave 2, `HostSeamSlashHandlerTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: omp's /copy runs in its terminal UI only; copy from the transcript. |
| SL-035 | /sandbox | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | settings-parity rows 4 and 26: sandbox limits are omp's |
| SL-036 | /bedrock | PARTIAL | `SlashCommandCatalogue` (NativeAction settings.open models, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Opens Settings, whose Models page sets up providers; Bedrock subcommands are not read |
| SL-037 | /auto-run | PARTIAL | `SlashCommandCatalogue` (NativeAction settings.open agents, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Opens Settings, whose Agents page holds the terminal auto-run switch; the words on, off and status are not read |
| SL-038 | /new | MET | `SlashCommandCatalogue` (NativeAction chat.new), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | None found |
| SL-039 | /new-chat | MET | `SlashCommandCatalogue` (NativeAction chat.new), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | None found |
| SL-040 | /newchat | MET | `SlashCommandCatalogue` (NativeAction chat.new), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | None found |
| SL-041 | /compress | MET | `SlashCommandCatalogue` (OmpDelegated /compact), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-042 | /sh | PARTIAL | `SlashCommandCatalogue` (NativeAction terminal.new, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests` | Opens a new terminal session; the text after the command is not run. |
| SL-043 | /run | PARTIAL | `SlashCommandCatalogue` (NativeAction terminal.new, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests` | Opens a new terminal session; the text after the command is not run. |
| SL-044 | /cursor | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Opens the Cursor editor |
| SL-045 | /side | PARTIAL | `SlashCommandCatalogue` (NativeAction chats.side.open, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests` | Opens a side chat with its own transcript and posts the typed question there; the engine does not answer in it yet. |
| SL-046 | /btw | PARTIAL | `SlashCommandCatalogue` (NativeAction chats.side.open, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests` | The IDE form: opens a side chat as /side does |
| SL-047 | /in-cloud | PARTIAL | `CloudHandoff`, `CloudHandoffTests`, `SlashCommandCatalogue` (CloudHandoff kind, Unavailable) | No host implements the hand-off (`cloud agents: phase 60`); Railway deploy never done; head wiring is Wave 3 work: verify after the Wave 3 merge |
| SL-048 | /automate | DEFERRED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | Automations are not wired into any head (AU-01) |
| SL-049 | /autopilot | PARTIAL | `CloudHandoff`, `CloudHandoffTests`, `SlashCommandCatalogue` (CloudHandoff kind, Unavailable) | No host implements the hand-off (`cloud agents: phase 60`); Railway deploy never done; head wiring is Wave 3 work: verify after the Wave 3 merge |
| SL-050 | /canvas | NOT-STARTED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | No canvas tool or pane (AC-129) |
| SL-051 | /create-hook | PARTIAL | `SlashCommandCatalogue` (Skill kind), `SlashPaletteDiscoveryTests`, `ConventionSlashDiscoverySource` | Runs only if a SKILL.md of that name is installed; OmpOS ships none |
| SL-052 | /create-rule | PARTIAL | `SlashCommandCatalogue` (Skill kind), `SlashPaletteDiscoveryTests`, `ConventionSlashDiscoverySource` | Runs only if a SKILL.md of that name is installed; OmpOS ships none |
| SL-053 | /create-skill | PARTIAL | `SlashCommandCatalogue` (Skill kind), `SlashPaletteDiscoveryTests`, `ConventionSlashDiscoverySource` | Runs only if a SKILL.md of that name is installed; OmpOS ships none |
| SL-054 | /create-subagent | PARTIAL | `SlashCommandCatalogue` (Skill kind), `SlashPaletteDiscoveryTests`, `ConventionSlashDiscoverySource` | Runs only if a SKILL.md of that name is installed; OmpOS ships none |
| SL-055 | /cursor-blame | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Needs Cursor's record of AI-authored changes |
| SL-056 | /loop | DEFERRED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | omp runs /loop in its terminal UI only |
| SL-057 | /migrate-to-skills | PARTIAL | `SlashCommandCatalogue` (Skill kind), `SlashPaletteDiscoveryTests`, `ConventionSlashDiscoverySource` | Runs only if a SKILL.md of that name is installed; OmpOS ships none |
| SL-058 | /review | PARTIAL | `SlashCommandCatalogue` (Skill kind), `SlashPaletteDiscoveryTests`, `ConventionSlashDiscoverySource` | Runs only if a SKILL.md of that name is installed; OmpOS ships none |
| SL-059 | /review-bugbot | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Bugbot is Cursor-hosted (AC-151) |
| SL-060 | /review-security | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Cursor-hosted reviewer; omp has its own /security |
| SL-061 | /sdk | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Cursor SDK helper |
| SL-062 | /split-to-prs | PARTIAL | `SlashCommandCatalogue` (Skill kind), `SlashPaletteDiscoveryTests`, `ConventionSlashDiscoverySource` | Runs only if a SKILL.md of that name is installed; OmpOS ships none |
| SL-063 | /statusline | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Cursor CLI status line |
| SL-064 | /update-cli-config | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Edits the Cursor CLI's own file |
| SL-065 | /update-cursor-settings | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Edits Cursor and VS Code settings; /config opens OmpOS Settings |
| SL-066 | /add-dir | MET | `SlashCommandCatalogue` (OmpDelegated /add-dir), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-067 | /auto-review | DEFERRED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | Cursor documents the name only; behaviour unknown |
| SL-068 | /changes | PARTIAL | `ChangesSlashHandler` (Wave 2, `HostSeamSlashHandlerTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: No command form yet: open the Changes panel from the tool strip. |
| SL-069 | /commands | PARTIAL | `SlashCommandCatalogue` (NativeAction settings.open commands, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Opens Settings for the Commands page |
| SL-070 | /context | MET | `SlashCommandCatalogue` (OmpDelegated /context), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-071 | /detach | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Cursor CLI persistent sessions |
| SL-072 | /full-conversation | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Cursor CLI redraw mode |
| SL-073 | /load-workspace | PARTIAL | `WorkspaceSetSlashHandler` (Wave 2, `HostSeamSlashHandlerTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: Not built: omp's /add-dir, /remove-dir and /dirs manage a session's directories. |
| SL-074 | /save-workspace | PARTIAL | `WorkspaceSetSlashHandler` (Wave 2, `HostSeamSlashHandlerTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: Not built: omp's /add-dir, /remove-dir and /dirs manage a session's directories. |
| SL-075 | /rules | PARTIAL | `SlashCommandCatalogue` (NativeAction settings.open rules, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Opens Settings for the Rules page |
| SL-076 | /skills | PARTIAL | `SlashCommandCatalogue` (NativeAction settings.open skills, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Opens Settings for the Skills page |
| SL-077 | /usage | PARTIAL | `SlashCommandCatalogue` (OmpDelegated /usage, Partial), `SlashCommandCatalogueTests`, `RuntimeSlashDispatchTests` | Sent to omp as typed; omp shows token usage, not Cursor plan meters. |
| SL-078 | /plugins | PARTIAL | `SlashCommandCatalogue` (NativeAction settings.open plugins, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Opens Settings for the Plugins page |
| SL-079 | /fast | MET | `SlashCommandCatalogue` (OmpDelegated /fast), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-080 | /opus | PARTIAL | `SlashCommandCatalogue` (NativeAction model.pick opus, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Filters the model picker by opus and selects the model when exactly one matches. |
| SL-081 | /composer | PARTIAL | `SlashCommandCatalogue` (NativeAction model.pick composer, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Filters the model picker by composer and selects the model when exactly one matches. |
| SL-082 | /branch | PARTIAL | `ForkSlashHandler` (Wave 2, `ModelSlashHandlerTests`, `ForkModelTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: omp runs /fork in its terminal UI only; forking over ACP is not wired in the ... |
| SL-083 | /duplicate | PARTIAL | `ForkSlashHandler` (Wave 2, `ModelSlashHandlerTests`, `ForkModelTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: omp runs /fork in its terminal UI only; forking over ACP is not wired in the ... |
| SL-084 | /restore | PARTIAL | `CheckpointSlashHandler` (Wave 2, `ModelSlashHandlerTests`, `CheckpointsModelTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: omp's /branch (alias /rewind) runs in its terminal UI only; there is no rewind ... |
| SL-085 | /undo | PARTIAL | `CheckpointSlashHandler` (Wave 2, `ModelSlashHandlerTests`, `CheckpointsModelTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: omp's /branch (alias /rewind) runs in its terminal UI only; there is no rewind ... |
| SL-086 | /compact | MET | `SlashCommandCatalogue` (OmpDelegated /compact), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-087 | /agent-review | NOT-STARTED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | No review service (AC-150) |
| SL-088 | /worktree | PARTIAL | `SlashCommandCatalogue` (OmpDelegated /worktree (omp /wt), Delegated), `SlashCommandCatalogueTests`, `RuntimeSlashDispatchTests` | Sent to omp as typed; omp's /worktree (alias of /wt) moves the session into a new worktree with its changes and takes an optional ... |
| SL-089 | /apply-worktree | PARTIAL | `WorktreeSlashHandler` (Wave 2, `ModelSlashHandlerTests`, `WorktreeCommandsModelTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: omp 18.12.6's /wt has no apply step. |
| SL-090 | /delete-worktree | PARTIAL | `WorktreeSlashHandler` (Wave 2, `ModelSlashHandlerTests`, `WorktreeCommandsModelTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: omp 18.12.6's /wt has no delete step. |
| SL-091 | /best-of-n | PARTIAL | `WorktreeSlashHandler` (Wave 2, `ModelSlashHandlerTests`, `WorktreeCommandsModelTests`), `SlashActionHandlerHostTests`; catalogue row is `SlashCommandCatalogue` Unavailable | Catalogue row and head host composition are Wave 3 edits; until then the token is listed disabled: Not built: running one worktree per model needs a multi-model run. |
| SL-092 | /remote-control | DEFERRED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | Sign-in authority (OD-03) and OQ-68; no remote worker |

## 22. Shortcuts (55 rows of table 3)

Source: `03-cursor-settings-slash.md` table 3 (Mac keys; Ctrl on Windows and Linux). The bound keys of OmpOS are the 25 gestures of `ShortcutCatalogue` in `src/OmpOS.Native.Views/Views/Shortcuts`, each raised in a headless shell by `ShortcutCatalogueTests` and shown on the Keyboard page; the web head binds Ctrl+Shift+A, Ctrl+Shift+K and Ctrl+, in `src/OmpOS.UI/wwwroot/scripts/ompos-keys.js` and the composer handles Enter, Shift+Enter, Ctrl+Enter and Ctrl+Shift+Enter. Remapping is omitted by design (`docs/settings-parity.md` row 21). macOS keys were never exercised.

Counts (55 rows): MET 5, MET-LIVE 0, PARTIAL 11, DEFERRED 0, NOT-STARTED 33, OMITTED-BY-DECISION 6.

| Row | Cursor key and action | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| SK-01 | General: Cmd+I (Toggle Sidepanel (unless bound to a mode)) | NOT-STARTED | none | No sidepanel toggle: the layout is fixed |
| SK-02 | General: Cmd+L or Cmd+K (Toggle Sidepanel) | NOT-STARTED | none | Ctrl+L and Ctrl+K are not in `ShortcutCatalogue` |
| SK-03 | General: Cmd+E (Toggle Agent layout) | NOT-STARTED | `ShortcutCatalogue` (Ctrl+Shift+A and Ctrl+Shift+K switch tabs, a different action) | No Agent layout toggle |
| SK-04 | General: Cmd+. (Mode Menu) | NOT-STARTED | none | The mode chip is mouse and keyboard focus only |
| SK-05 | General: Cmd+/ (Loop between AI models) | NOT-STARTED | none | No model cycling key |
| SK-06 | General: Cmd+Shift+J (Cursor settings) | PARTIAL | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Ctrl+, opens Settings) | OmpOS's Settings key is Ctrl+,; Ctrl+Shift+J is not bound |
| SK-07 | General: Cmd+Shift+Space (Toggle Voice Mode) | NOT-STARTED | none | No voice mode (AC-026) |
| SK-08 | General: Cmd+, (General settings) | MET | `ShortcutCatalogue`, `ShortcutCatalogueTests`, `ShellShortcutEffects`; web: src/OmpOS.UI/wwwroot/scripts/ompos-keys.js (settings), `ShortcutHost` | Ctrl on Windows and Linux; macOS unmeasured |
| SK-09 | General: Cmd+Shift+P (Command palette (Open Agents Window / Open IDE)) | NOT-STARTED | none | No command palette |
| SK-10 | Agents Window: Cmd+P, Cmd+Shift+F (Search files, search all files) | NOT-STARTED | `WorkspaceSearchView` (opened from the tool strip) | No Ctrl+P or Ctrl+Shift+F |
| SK-11 | Agents Window: Cmd+K (Command palette, search across conversations) | NOT-STARTED | `ConversationSearchView` (not bound), `ShortcutCatalogue` (Ctrl+F only) | No Ctrl+K palette (AC-012) |
| SK-12 | Agents Window: Cmd+F (Find in open conversation) | MET | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Ctrl+F SearchTranscript), `ShellShortcutEffects` | Find in the open conversation; web has no Ctrl+F handler in `ompos-keys.js` |
| SK-13 | Agents Window: Shift+Cmd+S (Populate a side chat from the transcript selection) | NOT-STARTED | none | Selection to side chat not built |
| SK-14 | Agents Window: Cmd+Shift+D (Toggle Design Mode (browser focused)) | NOT-STARTED | none | No Design Mode (AC-007) |
| SK-15 | Design Mode: Arrow Up/Down (Highlight parent/child element) | NOT-STARTED | none | No Design Mode |
| SK-16 | Design Mode: Shift+click (Multi-select) | NOT-STARTED | none | No Design Mode |
| SK-17 | Design Mode: Option+click (Add element to chat input) | NOT-STARTED | none | No Design Mode |
| SK-18 | Design Mode: Cmd+L (Add note or text edit to chat input) | NOT-STARTED | none | No Design Mode |
| SK-19 | Design Mode: Cmd+Z, Esc, Return (Undo markup, cancel/exit, send/save note) | NOT-STARTED | none | No Design Mode |
| SK-20 | Chat: Return (Queue message (default)) | PARTIAL | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Enter sends; Ctrl+Shift+Enter follows up), `RunInputSchedulerTests` | Enter sends, the follow-up that waits for the turn is Ctrl+Shift+Enter; queue reorder unwired (AC-015) |
| SK-21 | Chat: Cmd+Return (typing) (Force send) | MET | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Ctrl+Enter steer), `AcpSteeringTests`, `EngineRunInputSendTests` | Not observed in a live turn (OD-07) |
| SK-22 | Chat: Cmd+Shift+Backspace (Cancel generation) | PARTIAL | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Escape stops the run) | Escape, not Ctrl+Shift+Backspace |
| SK-23 | Chat: Cmd+Shift+L (code selected) (Add selection as context) | NOT-STARTED | none | Selection as context not built (report Mentions deviation 5) |
| SK-24 | Chat: Cmd+V, Cmd+Shift+V (clipboard code/log) (Add clipboard as context, or to input box) | NOT-STARTED | none | Clipboard as context not built |
| SK-25 | Chat: Cmd+Return with suggested changes (Accept all changes) | NOT-STARTED | `ChangesBarView` (Apply review button) | No key |
| SK-26 | Chat: Cmd+Shift+Backspace with suggested changes (Reject all changes) | NOT-STARTED | `ChangesBarView` | No key |
| SK-27 | Chat: Tab (Cycle to next message; queue message for after the turn ...) | NOT-STARTED | none | Tab is not message cycling or queueing |
| SK-28 | Chat: Shift+Tab (Rotate Agent modes) | NOT-STARTED | none | Mode rotation not bound (AC-005) |
| SK-29 | Chat: Cmd+Opt+/ (Model toggle) | NOT-STARTED | none | No model toggle key |
| SK-30 | Chat: Cmd+N or Cmd+R (New chat) | MET | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Ctrl+N NewAgent), `ShellShortcutEffects` | Ctrl+R is not bound |
| SK-31 | Chat: Cmd+T (New chat tab) | NOT-STARTED | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Ctrl+Shift+T opens a terminal, not a chat tab) | No new chat tab key |
| SK-32 | Chat: Cmd+[ , Cmd+] (Previous, next chat) | NOT-STARTED | none | No previous or next chat key (Ctrl+1 focuses the agent list) |
| SK-33 | Chat: Cmd+W (Close chat) | PARTIAL | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Delete closes the focused agent; Ctrl+W closes Settings) | No Ctrl+W close chat |
| SK-34 | Chat: Escape (Unfocus field) | PARTIAL | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Escape dismisses) | Dismisses popups and stops a run; not an unfocus |
| SK-35 | Chat: Option+Enter (Alt+Enter) on a / skill (Use skill as Custom Mode) | NOT-STARTED | `CustomModeModel` (unwired) | No Alt+Enter on a skill (AC-006) |
| SK-36 | Code context: @ (@-mentions) | PARTIAL | `MentionParser`, `MentionPickerModelTests`, `ComposerMentionViewTests` | Typing @ opens the picker once wired; head wiring is Wave 3 work: verify after the Wave 3 merge |
| SK-37 | Code context: / (Shortcut commands) | MET | `SlashPaletteEndToEndTests`, `SlashPaletteModelTests`; web: src/OmpOS.UI/Components/Composer/SlashPalette.razor | Typing / opens the palette |
| SK-38 | Code context: Cmd+Shift+L (Add selection to Chat) | NOT-STARTED | none | See row 23 |
| SK-39 | Code context: Cmd+L or Cmd+K (Add selection to new chat) | NOT-STARTED | none | See row 23 |
| SK-40 | Code context: Cmd+Return (Search codebase in chat) | NOT-STARTED | none | Not built |
| SK-41 | Code context: Select code, Cmd+C, Cmd+V or Cmd+Shift+V (Add copied reference, or copied text) | NOT-STARTED | none | Not built |
| SK-42 | Tab: Tab (Accept suggestion) | PARTIAL | `InlineCompletionBox`, `InlineCompletionBoxTests`, `CompletionAcceptanceTests` (Tab accepts) | Box is never fed: no provider is constructed (AC-140) |
| SK-43 | Tab: Cmd+Right (Accept next word) | NOT-STARTED | `InlineCompletionBox` (Tab and Escape only) | No word-wise accept |
| SK-44 | Tab: Escape (Reject) | PARTIAL | `InlineCompletionBox`, `InlineCompletionBoxTests` (Escape rejects) | Same as row 42 |
| SK-45 | Editor: VS Code defaults (https://code.visualstudio.com/docs/getstarted/keybindings ...) | PARTIAL | `FindBarKeyboardTests`, `EditorTabsTests` | Editor has its own find and tab keys; VS Code defaults are not adopted |
| SK-46 | Inline edit: none documented (Reference lists no inline-edit section; inline-edit help ...) | OMITTED-BY-DECISION | 01-cursor-agent-chat.md finding N1; `docs/settings-parity.md` row 21 | Nothing documented to match; inline edit is retired |
| SK-47 | Terminal (editor): none documented (No Cursor terminal shortcut listed; VS Code defaults apply ...) | OMITTED-BY-DECISION | 03-cursor-settings-slash.md (none documented) | Nothing documented to match |
| SK-48 | CLI: Shift+Tab (Rotate Agent, Plan, Ask) | OMITTED-BY-DECISION | 04-omp-cursor-map.md (the CLI is omp) | Cursor CLI key; omp's TUI keys are its own |
| SK-49 | CLI: Shift+Enter, Ctrl+J, Option+Enter (Newline (Option+Enter after /setup-terminal; Ctrl+J for ...) | PARTIAL | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Shift+Enter newline) | Ctrl+J and Alt+Enter are not bound |
| SK-50 | CLI: Ctrl+D (twice) (Exit) | OMITTED-BY-DECISION | 04-omp-cursor-map.md (the CLI is omp) | Cursor CLI key |
| SK-51 | CLI: Ctrl+R (Review changes (/changes); i adds follow-up) | NOT-STARTED | `ChangesBarView` (opened from the tool strip) | No Ctrl+R |
| SK-52 | CLI: Ctrl+C (Pauses a /goal; cancels; exits Shell Mode) | OMITTED-BY-DECISION | 04-omp-cursor-map.md row 31 | Cursor CLI key; /goal is omp's |
| SK-53 | CLI: Ctrl+O (Expand truncated output) | OMITTED-BY-DECISION | 04-omp-cursor-map.md (the CLI is omp) | Cursor CLI key |
| SK-54 | CLI: Enter, Enter twice (Steer at a safe boundary, interrupt the turn) | PARTIAL | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Ctrl+Enter steers, Escape stops) | Different keys from Enter, Enter twice |
| SK-55 | CLI: Tab (approval prompt) (Add command to allowlist) | NOT-STARTED | `ToolApprovalRowView` | No Tab-to-allowlist |

## 23. Re-verification log against the previous audit

Rows of `docs/quality/cursor-parity-audit-2026-10-26.md` that map to rows of this audit, with the status then and now. The mapping is not one to one: the new inventory splits and renames rows. 'Old' is the previous audit's final-tree status at `d75fd88`.

| Previous row | Old | New row | New | What changed |
|---|---|---|---|---|
| ADE-01 session chat | PARTIAL | AC-001 | PARTIAL | Unchanged: no live model turn; the web head adds Demo parity only |
| ADE-03 steering | MET | AC-016 | MET | Unchanged; Wave 2 adds the queue model (AC-015, PARTIAL) |
| ADE-04 side chats and transcript search | MET | AC-019, AC-020 | PARTIAL, MET | Split: the new inventory expects the engine to answer in the side chat with hidden parent context; that is the unwired `SideChatHub`. Cross-chat search is unchanged |
| ADE-05 agents window | PARTIAL | AC-008 | PARTIAL | Unchanged: the registry feeder gap stands; the rail and Kanban carry real sessions |
| ADE-06 worktree per run | PARTIAL | AC-131, WS-06 | PARTIAL | `Harness.Worktrees` grew (apply, patch, setup runner, run sets); still wired only in the hosted API |
| ADE-07 cloud runs | PARTIAL | CA-01, API-01 | PARTIAL | Control plane, worker bridge, Railway client and cloud UI models were added; not mapped, not deployed |
| ADE-08 remote control | PARTIAL | CA-24 | DEFERRED | Re-read: Cursor's Remote Control is a local loop handed to the cloud; OmpOS has none (OD-03, OQ-68) |
| ADE-10 open folder | PARTIAL | AC-009, WS-03 | PARTIAL | `WorkspaceSetPicker` added; the head still has no open-folder action |
| ADE-11 diff review | MET | AC-122 | MET | Unchanged; web adds `ChangesBar` |
| ADE-12 inline edit | PARTIAL | AC-144 | NOT-STARTED | Cursor retired the flow (finding N1); the replacement hand-off is not built |
| ADE-13 inline completion | PARTIAL | AC-140 | PARTIAL | Unchanged |
| ADE-14 terminal, auto-run | MET | AC-123 | PARTIAL | The row is now Run Modes: the terminal is met on Linux, the Run Mode page and resolver are unwired |
| ADE-15 browser pane | NOT-STARTED | AC-007, AC-126 | NOT-STARTED | Unchanged |
| ADE-16 MCP client | PARTIAL | AC-110 | PARTIAL | Configuration import, catalog and page added; the registry is still empty in the app |
| ADE-17 rules, skills, modes | PARTIAL | AC-006, AC-060, AC-070, AC-071 | PARTIAL | Discovery over six directories at two scopes (`ConventionCatalog`) reaches Settings and the palette; no run path reads rules or a mode |
| ADE-18 text and regex search | MET | AC-048 | PARTIAL | The Cursor row is now an index-backed Instant Grep; the user search is unchanged |
| ADE-20 export and usage history | PARTIAL | AC-175 | PARTIAL | Unchanged: usage history empty |
| ADE 21, ADE 22 extensions, admin | DEFERRED | RD-04, RD-06, AD rows | DEFERRED | Unchanged |
| CX-01 to CX-03 hooks, automations | PARTIAL | AC-100 to AC-103, AU-01 | PARTIAL | Hooks core, bridge, ACP tap, service, omp extension and Hooks page were built; still no head wires them |
| CX-05 remote development | DEFERRED | RD-01 to RD-03 | PARTIAL | `Harness.Remote` core and UI exist since `fd8f335`; never run against a real host (ADR-0015, OQ-97 default) |
| VX-02 slash palette | PARTIAL | SL-001 to SL-092 | see section 3 | One registered command became 92 catalogued tokens with handler kinds and statuses |
| Settings row 1 models | MET-LIVE | AC-163, AC-164, S39 | MET-LIVE, MET, MET | Provider catalogue grew to 103 rows; live proof is not re-run |
| Settings rows 7, 8, 10 rules, skills, commands | MET | AC-060, AC-070, AC-090 | PARTIAL, PARTIAL, MET | The inventory rows ask for more (rule types, nested rules, skill loading) |
| Settings row 12 hooks | DEFERRED | AC-100, S28 | PARTIAL | The Hooks page exists as a Wave 2 view, unregistered |
| Settings row 3 Run Mode | PARTIAL | S04, S17, AC-123 | PARTIAL | `RunModeSettingsPage` built, unregistered |
| Settings row 21 keyboard | MET | S48, SK rows | MET | Unchanged: 25 catalogued gestures |

## 24. Document drift

Drift found between the records and the audited tree. This audit edits no other file, so each item is reported, not fixed. 'Base' means the document as committed at `fbd5266`.

| Where | Drift | Owner |
|---|---|---|
| `docs/settings-parity.md` row 10 and the Commands page row of the page table | Says the page lists 'the OmpOS catalogue (one command, `side`)'; `SlashCommandsSettingsSource` now lists the whole `SlashCommandRegistry` of 92 tokens plus discovered commands | Settings agent |
| `docs/settings-parity.md` rows 7, 8 and the Rules and Skills page rows | Say the pages list `.ompos/rules` and `.ompos/skills`; the pages load `ConventionCatalog` over six directories at two scopes (`ConventionDirectories`) | Settings agent |
| `docs/settings-parity.md` rows 9, 11, 12 | Say there is no Subagents list, no MCP page and no Hooks page; Wave 2 built `McpPageView`, `HooksPageView` and `ConventionSubagent` discovery (none registered in the head yet) | Settings agent |
| `docs/settings-parity.md` rows 3, 4, 26 | Say the Run Mode choice and sandbox policy have no backing; Wave 2 added `RunModePolicy`, `PermissionsFileReader` and `SandboxFileReader` as libraries | Settings agent |
| `docs/reference/slash-commands.md` | Says 'The web head implements `ISlashActionHost` in a later wave'; `WebSlashActionHost` exists at `fbd5266`. Its table still shows 25 tokens Unavailable whose Wave 2 handlers exist | Slash agents |
| `docs/runbooks/deferred-and-blocked-2026-10-26.md` row 'Phase 34 Remote development' | Base text: Blocked (external), 'No stub ships'; `Harness.Remote`, its UI and ADR-0015 (OQ-97 default) now exist, with no real-host run | Documentation agents |
| `docs/quality/cursor-parity-audit-2026-10-26.md` section 4 | It lists hooks, automations and the MCP registry as unwired and remote development as deferred with no stub; since then the hooks core, the remote core and the MCP configuration libraries were built (still unwired, remote never run). The previous audit stays as the dated record | none (dated) |
| `docs/plans/v0.15.0-research/01-cursor-agent-chat.md` row AC-144 and finding N1 | Research says OmpOS ADE-12 clones a retired Cursor flow; `InlineEditView` is still mounted without a model in `MainView` | Owner of ADE-12 |

Not audited: ADR bodies beyond their status, `docs/api/`, `.planning/**`.

## 25. Final counts by status per family

| Family | MET | MET-LIVE | PARTIAL | DEFERRED | NOT-STARTED | OMITTED-BY-DECISION | Total |
|---|---|---|---|---|---|---|---|
| Agent and chat (AC-001 to AC-031) | 2 | 0 | 18 | 3 | 7 | 1 | 31 |
| Context and ignore (AC-040 to AC-051) | 0 | 0 | 10 | 0 | 1 | 1 | 12 |
| Rules, skills, subagents and commands (AC-060 to AC-092) | 1 | 0 | 11 | 1 | 2 | 4 | 19 |
| Hooks (AC-100 to AC-104) | 0 | 0 | 4 | 0 | 1 | 0 | 5 |
| MCP and plugins (AC-110 to AC-118) | 0 | 0 | 4 | 0 | 3 | 2 | 9 |
| Tools, terminal, sandbox and browser (AC-120 to AC-130) | 2 | 0 | 4 | 1 | 2 | 2 | 11 |
| Worktrees (AC-131, AC-132) | 0 | 0 | 2 | 0 | 0 | 0 | 2 |
| Tab and inline edit (AC-140 to AC-145) | 0 | 0 | 1 | 0 | 4 | 1 | 6 |
| Review, Bugbot and git (AC-150 to AC-155) | 0 | 0 | 1 | 0 | 3 | 2 | 6 |
| Models, privacy and billing (AC-160 to AC-176) | 3 | 1 | 2 | 0 | 0 | 6 | 12 |
| Cloud agents (CA, EN, API, AU, IN rows and AC-190 to AC-194) | 0 | 0 | 28 | 6 | 52 | 22 | 108 |
| Remote development (RD rows) | 0 | 0 | 4 | 2 | 2 | 1 | 9 |
| Workspaces (WS rows) | 0 | 0 | 11 | 0 | 0 | 2 | 13 |
| CLI (CL rows) | 0 | 0 | 16 | 1 | 2 | 2 | 21 |
| Team, admin and accounts (AD rows and AC-01 to AC-08) | 0 | 0 | 3 | 9 | 3 | 18 | 33 |
| Settings (S01 to S50) | 5 | 0 | 16 | 6 | 9 | 14 | 50 |
| Slash tokens (92) | 16 | 0 | 53 | 5 | 3 | 15 | 92 |
| Shortcuts (55 rows of table 3) | 5 | 0 | 11 | 0 | 33 | 6 | 55 |
| **All rows above** | **34** | **1** | **199** | **34** | **127** | **99** | **494** |

## 26. Top gaps

In the order they limit the claim 'feature-complete against Cursor 3.27':

1. Nothing of Wave 2 is wired: hooks, MCP, Run Modes, checkpoints, mentions, queue, custom modes, side-chat engine, fork, worktree commands, remote, cloud pages and the new settings pages exist as libraries, models and views only (section 4, row 1). The merged tree must be rebuilt and this audit repeated.
2. No live model turn: every agent-behaviour row (steering, hooks, MCP servers, slash delegation, skills, plans) rests on contract tests and fakes (OD-07).
3. ACP form elicitation is not implemented, so omp's `ask` tool, plan approval and MCP elicitation cannot work (AC-003, AC-024, AC-113).
4. Cloud agents: not mapped in `Program.cs`, not deployed, the worker cannot clone or branch, git tokens expire after an hour, secrets and runs are in memory (CA-01, EN rows, API rows).
5. Remote over SSH has never touched a real host and no head starts a remote session (RD rows).
6. Engine limits: no Ask or Debug mode, no file-level checkpoint, no /goal or /loop over ACP, no subagent roster (AC-002, AC-004, AC-017, AC-023, AC-083).
7. No browser pane, Design Mode, image input, voice, canvases or deeplinks (AC-007, AC-025, AC-026, AC-029, AC-129).
8. No pull-request reviewer (Agent Review, Bugbot-like), no automation triggers beyond the local engine (AC-150, AU rows).
9. Usage history is empty and the context ring has no per-category counts (AC-021, AC-175).
10. Most shortcuts of table 3 are unbound (SK rows: 33 NOT-STARTED).

## 27. Riskiest items

Items where a wrong or half-wired merge would do harm, not merely leave a gap:

1. The audited tree was never compiled (commit message 'not compiled'). the Wave 2 merge commit changes 1274 files (109409 insertions) that were never compiled; warnings-as-errors and analyzer traps are listed in each report's 'Fix-up agent' section.
2. Hooks (AC-100 to AC-103): they can deny tool calls. Consent v2 hashes the scripts a command names heuristically; the managed-file pre-consent does not check root ownership (report HooksCore, ADR-0016); `IPromptHookEvaluator` has no implementation; the omp extension was only tested against fakes.
3. Run Modes and permissions (AC-123, AC-124): the resolver wraps omp's permission requests whose field names are UNVERIFIED (report Safety); Run Everything must widen only the kinds it names; omp's approval mode is per process.
4. Mentions (AC-040 to AC-047): they put file contents into prompts. The secret-file filter, the ignore filter and the size caps must be in the send path when wired; `.cursorignore` does not bind omp's own tools.
5. Cloud (CA, EN rows): a deployed worker today would start without a repository; the one-hour git token goes stale; Railway operations marked UNVERIFIED in `RailwayOperations`; remote desktop is blocked by Railway's acceptable-use text (OQ-73); Railway bills compute while a worker is awake.
6. Remote over SSH: ControlMaster with `-T` and stdin EOF behaviour are unprobed (a hang is possible); agent forwarding lets the remote root use the user's agent (warned in the profile editor); catalog MCP servers would run on the remote host with local paths (report McpImport).
7. Checkpoints (AC-017): the rollback path after a failed mid-rewind write has no test (report Checkpoints).
8. The web Demo build is a simulated engine; any screenshot or demo must not be read as proof of the engine, MCP, hooks or cloud (ADR-0017).
9. Status drift: `docs/settings-parity.md` and `docs/reference/slash-commands.md` already disagree with the tree (section 24); a reader of those alone will undercount.

## 28. What needs the owner

| Need | What is missing | Record |
|---|---|---|
| Windows and macOS runs | A Windows 11 24H2 runner and the Mac channel: PTY terminal, Avalonia heads, remote `Path.IsPathFullyQualified` fix, macOS keys, browser-pane measurement | OQ-69, OQ-51, OQ-74, `docs/runbooks/local-terminal.md` |
| A live model turn | A recorded approval to spend model credentials: steering into a running turn, hooks through the omp extension, MCP servers sent in `mcpServers`, skill and slash delegation, `--add-dir`, remote `session/load` | OD-07, OQ-71, OQ-81 |
| Railway deploy | Authority to deploy the API and workers, Railway's written position on remote desktops, volume or transcript storage, GitHub App registration and installation, the sign-in authority for a public API | OD-03, OQ-70, OQ-73, OQ-100, OQ-104 |
| GitBook account | Someone with a GitBook account to connect the space; a custom domain is a paid feature | OQ-101 |
| Brand and look sign-off | The White-and-Purple brand and the rendered look have no recorded owner answer; the Light default is a recorded default | OQ-94, OQ-87 to OQ-91 |
| Licence form decisions | What 'AGL 3.0' names (answered by default as AGPL-3.0-or-later), the licence of `agent-swarm` and `agent-substrate`, the private-submodule token | OQ-93, OQ-102, OQ-63 |
| Cursor-compatibility decisions | Whether Cursor's own file locations are read by default (OQ-103); the hooks execution model (OQ-96); remote design (OQ-97); which features the inventory leaves out (OQ-95); coding-plan provider rows (OQ-98); public Demo build (OQ-99) | OQ-95 to OQ-99, OQ-103 |
| Accounts and admin | Whether OmpOS ever gets accounts, teams or billing; every AD row and the DEFERRED admin rows wait on it | OD-03, OQ-61 |

See also `docs/runbooks/deferred-and-blocked-2026-10-26.md` for the standing blocked list and `docs/open-questions.md` for OQ-93 to OQ-104.
