---
description: "Row-by-row audit of OmpOS against the Cursor 3.27 inventory: agent, chat, context, hooks, MCP, cloud agents, remote development, workspaces, settings, slash tokens and shortcuts, with status, evidence and the missing piece."
---

# Cursor parity audit against the Cursor 3.27 inventory (written 2026-10-28)

Status: Built (every inventory row is re-graded at commit 7535859, the final integrated tree of v0.15.0, by reading code and test names; this audit ran no test; the full bootstrap gate passed on `e24e6851`, recorded 2026-10-28 after the audit was written, see the paragraph "Gate" below)

Last verified: 2026-10-28

Applies to: v0.15.0

This audit refreshes `docs/quality/cursor-parity-audit-2026-10-26.md` (the previous audit, kept unchanged) against the inventory that milestone v0.15.0 researched on 2026-10-27: `docs/plans/v0.15.0-research/01-cursor-agent-chat.md` (agent, chat, context, tools, review, models; ids AC-001 to AC-194), `02-cursor-cloud-remote.md` (cloud agents, environments, API, automations, integrations, remote development, workspaces, CLI, team and accounts; ids CA, EN, API, AU, IN, RD, WS, CL, AD and AC-01 to AC-08) and `03-cursor-settings-slash.md` (settings S01 to S50, the 92 slash tokens, 55 shortcut rows). The omp side of each row is `04-omp-cursor-map.md`. Every row of the three files appears below with its own id; the slash tokens and shortcut rows have no id in the research, so this audit numbers them SL-001 to SL-092 (catalogue order) and SK-01 to SK-55 (table order). The two id series `AC-0nn` (three digits, file 01) and `AC-0n` (two digits, file 02, accounts) are different rows.

## 1. Audited tree and method

| Item | Value |
|---|---|
| Audited tree | The repository at commit `7535859` (`docs(64-03): desktop screenshots for the README and guides`), the final integrated tree of v0.15.0. It holds the Wave 1 and Wave 2 libraries and UI components, the Wave 3 wiring of hooks, run modes, mentions and the context ring, MCP, remote ssh, the checkpoint, fork and side-chat adapters, the cloud host and multi-folder sets into the desktop head, the web Settings and the Demo build (`d9ed851`), the later wiring of built-in skills, MCP presets and image attachments (`e7c451e`) and of automations and container and WSL environments (`942d813`), and the record of the live ssh run (`d3f8b6d`) |
| First draft | This audit was first written at commit `fbd5266` (`wip(wave2): merged Wave 2 output of fourteen agents, not compiled`), before the Wave 3 to 6 work. It graded every Wave 2 library that no head constructed as PARTIAL and said so in the gap cell. This version re-grades the rows against the final tree; the differences are in 'What changed since the first draft' |
| Tests of the tree | Recorded 2026-10-28: the tests of every project were green per project on the final tree, with these counts: Presentation 2303, Native 2012 (2 skipped), AgentLoop 1221, Acp 201, Mcp 197, Plugins 429, Remote 520+ (2 skipped as root), Workspace 364, Automations 386, Web 262+, UI 595, Architecture 46, AgentWorker 153, Contracts 137, Sandbox 329; Api.IntegrationTests 1027 of 1027, Coordination 32 and Sessions.Postgres 58 against throwaway podman services. The full `./bootstrap.sh` gate was still running when this audit was written; it later passed on `e24e6851` (recorded 2026-10-28, `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`), and no row below was re-graded because of it |
| Method | Each row was followed from the inventory row to the code and to a named test class found by `grep` in `src/`, `apps/`, `services/` and `tests/`. User-facing rows were followed into the heads by searching for the construction or use of each view and library type in `apps/OmpOS.Native`, `src/OmpOS.Native.Views`, `apps/OmpOS.Web` and `src/OmpOS.UI`. The rows were re-graded by reading code and test names, without running a test. A row moves to MET only when it is implemented, reachable from a head and covered by a named test class that `grep` finds; MET-LIVE needs a recorded live run; a row that stays PARTIAL says in its gap cell what is still missing. Facts that exist only in an agent report are cited as `report <Name>` (/tmp/reports/, one file per agent: changed files, tests, limits and a wiring spec). The omp side comes from `04-omp-cursor-map.md`. The S01 to S50 rows follow `docs/settings-parity.md` and the slash rows follow `docs/reference/slash-commands.md` as they stand |
| Web head | `apps/OmpOS.Web` is Blazor WebAssembly over `src/OmpOS.UI` and `src/OmpOS.Presentation`. The public build is a Demo build (ADR-0017): an in-browser `DemoApiHandler` answers every API route from scripted fixtures, so anything shown there is a simulated engine, with no backend and no secret. Recorded 2026-10-28: it is deployed at https://ompos-web.vercel.app (Vercel production alias, static Demo bundle) and a headless Chrome capture of the live URL showed no console errors and no failed requests; `scripts/web-smoke/smoke-demo.mjs` ran 68 axe runs (light and dark, 1440, 1024 and 390 px, every screen) with 0 violations in the Wave 5 tree. The web Settings has 18 pages (section 20). The web head has no checkpoints, worktrees, queue, side chats, fork, mention picker or custom modes. Plan: `docs/plans/v0.15.0-research/10-web-app-plan.md` |
| Desktop smoke | Recorded 2026-10-28 (`docs/runbooks/evidence/desktop-app-2026-10-28/README.md`): the desktop head on Linux under Xvfb with a sandbox HOME, the real omp 18.12.6 installed and no model turn. 21 Settings pages rendered (the Automations page was added afterwards); two defects were found and fixed (a clipped environment picker header and a Run mode page that opened scrolled down). Nothing ran on Windows, macOS or Android |
| Cloud, remote, containers | The cloud control plane (`services/OmpOS.Api/Cloud`, mapped in `Program.cs` and off until `Cloud:Enabled`), the worker (`services/OmpOS.AgentWorker`), the Railway client (`src/OmpOS.Sandbox`) and the desktop `ApiCloudAgentsHost` are built and tested with fakes; the API is not deployed on Railway (recorded 2026-10-28; OD-03, OQ-100); after the release a staged private worker was deployed to Railway and exercised (health check, JWT gate, `initialize` and `session/new` through the bridge, no model turn; `docs/runbooks/evidence/railway-ompos-worker-2026-10-28.md`). Remote over SSH (`src/OmpOS.Harness.Remote`) ran once live: recorded 2026-10-28, a real omp 18.12.6 opened a session through a loopback sshd (`docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md`; no prompt turn). A live podman test (`PodmanLiveTests`, `OMPOS_LIVE_PODMAN=1`, alpine, real podman, fake omp, no model) passed in the Wave 4 tree and found two defects: the `label=` filter of `PodmanExecCommandBuilder` and BusyBox `realpath` in the remote scripts (regression test `RemoteScriptsBusyBoxTests`). WSL never ran. No live model turn was run (OD-07) |
| Run by this audit | Read-only commands only: `git`, `grep`, `ls`, and small Python scripts that parse the tables of this document, of `docs/settings-parity.md` and of `docs/reference/slash-commands.md`, compute the counts below and check every cited class name and path |
| Not run by this audit | `dotnet build`, `dotnet test`, `./bootstrap.sh`, the node tests of `plugins/ompos-hooks-omp`, any live omp, any model turn |
| Documents | This audit cites code, tests, agent reports and documents as found at `7535859`; section 24 lists the drift between documents and tree |

A claim that a test covers a row means `grep` found the named class. This audit ran none of them.

## 2. Status vocabulary

| Word | Meaning |
|---|---|
| MET | Implemented, reachable from the head where the item is user-facing, and covered by named tests |
| MET-LIVE | MET, and also proven against the real thing in a recorded live run. No live model turn was made beyond the bounded Phase 11 run (OD-07), so only rows proven without a model turn carry it: provider configuration against omp 18.12.6, from the previous audit's records. The loopback ssh run and the live podman test of 2026-10-28 prove transport and container plumbing, and moved no row to MET-LIVE (section 3) |
| PARTIAL | Part of the item is built or wired; the gap column says what is missing. A library or view that no head constructs is PARTIAL |
| DEFERRED | Not built, because of an external blocker or an owner decision; the blocker is named |
| NOT-STARTED | No product code and no deferral record |
| OMITTED-BY-DECISION | Left out on purpose. The reason is the Fit column of the inventory (omit-no-accounts), a decision recorded in `docs/settings-parity.md`, or a recorded default (OQ-95) |

A status word may carry a qualifier in brackets. The counts use the first word.

## 3. Summary

494 rows in 18 families. Counts are generated by a Python script that reads the tables of this document; none was counted by hand.

| Family | MET | MET-LIVE | PARTIAL | DEFERRED | NOT-STARTED | OMITTED-BY-DECISION | Total |
|---|---|---|---|---|---|---|---|
| Agent and chat (AC-001 to AC-031) | 5 | 0 | 16 | 3 | 6 | 1 | 31 |
| Context and ignore (AC-040 to AC-051) | 3 | 0 | 7 | 0 | 1 | 1 | 12 |
| Rules, skills, subagents and commands (AC-060 to AC-092) | 2 | 0 | 10 | 1 | 2 | 4 | 19 |
| Hooks (AC-100 to AC-104) | 0 | 0 | 5 | 0 | 0 | 0 | 5 |
| MCP and plugins (AC-110 to AC-118) | 1 | 0 | 3 | 0 | 3 | 2 | 9 |
| Tools, terminal, sandbox and browser (AC-120 to AC-130) | 2 | 0 | 5 | 1 | 2 | 1 | 11 |
| Worktrees (AC-131, AC-132) | 0 | 0 | 2 | 0 | 0 | 0 | 2 |
| Tab and inline edit (AC-140 to AC-145) | 0 | 0 | 1 | 0 | 4 | 1 | 6 |
| Review, Bugbot and git (AC-150 to AC-155) | 0 | 0 | 2 | 0 | 2 | 2 | 6 |
| Models, privacy and billing (AC-160 to AC-176) | 3 | 1 | 2 | 0 | 0 | 6 | 12 |
| Cloud agents (CA, EN, API, AU, IN rows and AC-190 to AC-194) | 2 | 0 | 30 | 6 | 48 | 22 | 108 |
| Remote development (RD rows) | 2 | 0 | 3 | 2 | 1 | 1 | 9 |
| Workspaces (WS rows) | 0 | 0 | 11 | 0 | 0 | 2 | 13 |
| CLI (CL rows) | 1 | 0 | 15 | 1 | 2 | 2 | 21 |
| Team, admin and accounts (AD rows and AC-01 to AC-08) | 0 | 0 | 3 | 9 | 3 | 18 | 33 |
| Settings (S01 to S50) | 8 | 0 | 20 | 0 | 0 | 22 | 50 |
| Slash tokens (92) | 44 | 0 | 24 | 4 | 6 | 14 | 92 |
| Shortcuts (55 rows of table 3) | 7 | 0 | 11 | 0 | 31 | 6 | 55 |
| **All rows above** | **80** | **1** | **170** | **27** | **111** | **105** | **494** |

The 92 slash tokens: 44 MET, 24 PARTIAL, 4 DEFERRED, 6 NOT-STARTED, 14 OMITTED-BY-DECISION. This equals the catalogue: Built 35 and Delegated 9 (MET; Delegated only for the forwarding), Partial 24, Unavailable 24, without the OmpOS addition `/update-settings` (Built, the 93rd row). No token is PARTIAL for want of a head handler any more: the Wave 2 handler classes are composed by `DesktopSlashActionHost` (`LogsSlashHandler` is not, because OmpOS keeps no log file).

The 50 settings rows follow the S01 to S50 table of `docs/settings-parity.md`: 8 MET (Built 6, Wired 2), 20 PARTIAL, 22 OMITTED-BY-DECISION. The script asserts that every row agrees with that table.

MET-LIVE: 1 row (AC-163), carried from the previous audit's run of provider configuration against the real omp (`LiveOmpConfigTests`) and not re-run. No new row is MET-LIVE. The recorded ssh run of 2026-10-28 (`docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md`) proves the transport, the preflight, the ACP handshake and `session/new` of RD-01 against a real omp 18.12.6 through a real sshd, but RD-01 stays PARTIAL because the remote explorer, editor, terminal, hook files and mention files are not wired for ssh sessions, and the run made no prompt turn. The live podman test (`PodmanLiveTests`) used a fake omp, so it proves container plumbing and cannot give MET-LIVE.

No row about the Cursor agent loop (modes, steering, hooks, MCP, cloud, remote, checkpoints) is MET-LIVE, because no live model turn was approved after Phase 11 (OD-07). The MET rows of that loop rest on unit, integration and fake-omp tests.

## What changed since the first draft

The first draft was written at commit `fbd5266`, before Wave 3 to 6. Of the 494 rows, 82 changed status and 99 more kept their status and got new evidence or a new missing-piece text; the other rows are unchanged. Counts come from a script that compares the tables of the first draft (this file as committed at `7535859`) with these.

| Family | Moved | Main reasons |
|---|---|---|
| Agent and chat | 4 | AC-006, AC-015, AC-017 to MET (custom-mode chip, queue and checkpoint timeline mounted in `MainView`, with view and model tests); AC-025 to PARTIAL (desktop image attach) |
| Context and ignore | 3 | AC-040, AC-042, AC-043 to MET (mention sources, ignore filter and ACP resource send composed in the desktop head) |
| Rules, skills, subagents, commands | 1 | AC-091 to MET (the built-in skill ships) |
| Hooks | 1 | AC-104 to PARTIAL (worker hook subset built); AC-100 stays PARTIAL: wired, no live omp |
| MCP and plugins | 1 | AC-111 to MET (the head reads the files and sends the servers to omp) |
| Tools, terminal, sandbox | 1 | AC-125 to PARTIAL (the `sandbox.json` read boundary is enforced) |
| Review, Bugbot, git | 1 | AC-150 to PARTIAL (`/agent-review` ships as a skill) |
| Cloud agents | 5 | AU-02, AU-05 to MET and AU-03, AU-09, CA-18 to PARTIAL (automations in the API and on the desktop, worker hooks) |
| Remote development | 3 | RD-02, RD-03 to MET (ssh sessions start from the head); RD-05 to PARTIAL (containers wired, live podman test) |
| CLI | 1 | CL-15 to MET (same as AC-006) |
| Settings | 24 | re-aligned to `docs/settings-parity.md`: 13 NOT-STARTED or DEFERRED rows became OMITTED-BY-DECISION, 4 OMITTED rows PARTIAL (S05, S15, S30, S31), 5 rows MET (S04, S12, S17, S28, S40), 2 MET rows PARTIAL (S37, S45) |
| Slash tokens | 34 | the Wave 2 handlers are composed and the catalogue re-cut: 26 PARTIAL tokens became MET, 2 other tokens MET (`/review-security`, `/agent-review`), 5 tokens moved to a not-built status (`/logs`, `/vim`, `/show-thinking`, `/automate`, `/autopilot`), `/cursor` to PARTIAL |
| Shortcuts | 3 | SK-11, SK-36 to MET and SK-27 to PARTIAL (Ctrl+K overlay, mentions, Tab queue) |
| Worktrees, Tab and inline edit, Models, Workspaces, Team | 0 | no status moved; gap text of 15 rows restated |
| **All families** | **82** | MET 34 to 80, PARTIAL 199 to 170, DEFERRED 34 to 27, NOT-STARTED 127 to 111, OMITTED-BY-DECISION 99 to 105, MET-LIVE unchanged at 1 |

Update 2026-10-28 (after the release, no grade moved): a staged private worker was deployed to Railway and exercised (`docs/runbooks/evidence/railway-ompos-worker-2026-10-28.md`: health check, JWT gate, `initialize` and `session/new` through the bridge; no model turn, no clone, no API). The evidence of rows EN-01, CA-01, AC-014, AC-051, AC-104, CA-18, API-01, SL-047 and ADE-07 changed and their gap text was restated (the API was never deployed; the worker image has now run on Railway); none of them is graded differently, because every one still lacks the control plane, a clone, a branch or a model turn. The counts above (MET 80, PARTIAL 170, DEFERRED 27, NOT-STARTED 111, OMITTED-BY-DECISION 105, MET-LIVE 1) are unchanged.

## 4. What is not done and why

Each line was checked against the tree at `7535859`; the rows it affects are in the tables below.

| Not done | Rows | Why | Record |
|---|---|---|---|
| Any live model turn | AC-001, AC-016, AC-018, AC-019, AC-025, AC-040, AC-100, AC-110, AC-123, AC-124, slash tokens with a delegated effect | Only the bounded Phase 11 run was approved; hooks, MCP servers, run modes, fork, side chat, images, skills and mentions are proven by unit, integration and fake-omp tests | OD-07, OQ-71, OQ-81 |
| ACP form elicitation: omp's `ask` tool, plan approval, MCP elicitation | AC-003, AC-024, AC-113 | omp asks the client for a form through ACP; no code in `src/`, `apps/` or `services/` handles elicitation (grep), so the `ask` tool throws headless and a plan cannot be approved from OmpOS | 04-omp-cursor-map.md rows 8 and 23 |
| Voice input and calls, deeplinks, images on web and Android | AC-026, AC-029, IN-16, AC-025 | The desktop composer attaches images (ACP accepts them); there is no microphone or URL handler, and model vision stays Unknown because `ModelVision` is not supplied | 04-omp-cursor-map.md rows 19 and section B2, report ImageInput |
| Embedded browser pane, Design Mode and @Browser | AC-007, AC-044, AC-126, S20, SK-14 to SK-19 | Nothing exists in `src/`, `apps/`, `services/`; ADR-0009 is accepted for Linux only and Windows and macOS are unmeasured | ADR-0009, OQ-51, `docs/runbooks/deferred-and-blocked-2026-10-26.md` |
| Cloud agents live: control plane is mapped but the API is not deployed, and the worker cannot clone or branch | CA-01, CA-03, CA-18, CA-30, EN-01 to EN-12, API-01 to API-03, AC-104 | The routes answer 404 `cloud_disabled` until `Cloud:Enabled`; the worker bridges `omp acp` only (no clone, branch, environment run, git credential helper); the API does not mint the cloud-hooks consent claim, so cloud sessions report `untrusted`; git tokens last one hour; stores and secrets are in memory; the Railway operation table has UNVERIFIED entries; the API is not deployed on Railway (recorded 2026-10-28; a staged private worker was deployed and handshaken on Railway after the release, with no clone and no model turn) and no sign-in authority is configured (OD-03) | reports CloudControlPlane, CloudCore, CloudCore-sandbox, CloudHosted; OQ-70, OQ-85, OQ-100 |
| Remote over ssh: only a loopback run, and files, terminal, hooks and mentions are not wired | RD-01, RD-07, AC-040, AC-100, AC-110 | One live run (recorded 2026-10-28) reached `session/new` with a real omp; there is no remote explorer or editor, no terminal pane, no remote hook or mention files, no MCP servers from the local files, reconnect against a real host was not run, and whether a remote omp gets `--approval-mode` is UNVERIFIED; a Windows head rejects a remote working directory (`AcpAgentSession` requires a fully qualified path) | `docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md`, ADR-0015, OQ-97, `docs/runbooks/remote-containers.md` |
| Container and WSL environments: WSL never ran, no attach button, no terminal pane | RD-05 | Podman environments are wired and one live podman test passed with a fake omp; WSL is Windows-only; the dev-container attach button and the terminal pane of container and WSL sessions are not wired | report WireAutomationsContainers, `docs/runbooks/remote-containers.md` |
| Fork from an earlier message, rollback of a failed rewind, file checkpoints in omp | AC-018, AC-017 | `/fork <n>` is refused (`CanForkFromMessage` is false); no test forces an I/O failure mid-rewind; omp has no file-level checkpoint and only `unstable_forkSession` | 04-omp-cursor-map.md row 9, report Checkpoints |
| Engine-limited modes and commands: Ask, Debug, /goal, /loop, subagent roster | AC-002, AC-004, AC-023, AC-083 | omp ACP advertises modes `default` and `plan` only, and /goal, /loop are terminal-UI or RPC only | 04-omp-cursor-map.md rows 8, 31, section B3 |
| Run modes: hosted Auto-review classifier, verified request field names, remote approval mode, OS sandbox | AC-123, AC-124, AC-125, S05, S15 | Auto-review has no classifier by design; omp's permission request field names are UNVERIFIED (a request without the needed field asks the user); a changed mode reaches a running omp only when its engine is composed again; omp has no sandbox | report Safety, OQ-84 |
| Bugbot, PR routing, rollouts, CI auto-fix and most automation triggers | AC-151 to AC-153, AU-03 (non-GitHub), AU-04, AU-06 to AU-08, AU-10 to AU-19, CA-14, CA-15 | Not built. Cron, signed webhook and GitHub triggers exist in the API (off until `Automations:Enabled`) and cron on the desktop, but no automation ran against a live cloud worker or a live GitHub repository | `docs/reference/automations.md` |
| Usage and cost: usage history stays empty; no per-category context counts | AC-021, AC-175 | `MainView.CreateExport` passes an empty `UsageHistory`; ACP gives `usage_update` size and used only | 04-omp-cursor-map.md row 21 |
| Slash catalogue: 24 tokens Unavailable and 24 Partial | section 21 | Unavailable tokens need Cursor's own backend, are engine-limited, or are not built (`/logs`, `/vim`, `/show-thinking`, `/resume`, `/sandbox`); each Partial token says in its row what it does not do | `docs/reference/slash-commands.md` |
| Settings: 20 PARTIAL rows | S02, S03, S05, S08, S09, S10, S14, S15, S23 to S26, S29 to S31, S35 to S38, S45 | Flags stored and not applied (Vim keys, thinking blocks, running time), read-only lists, no default-environment or activation settings | `docs/settings-parity.md` |
| The web head has none of the desktop-only features | AC-006, AC-015, AC-017 to AC-019, AC-040, AC-100, AC-110, RD-03 | The public web build is a Demo with a simulated engine. Of its 18 Settings pages Models is real over `GET /providers`, Hooks and MCP are read-only lists, and Workspaces, Remote, Cloud, Agents and Run mode are information pages with a desktop notice | ADR-0017, `docs/runbooks/web-demo.md` |
| Wiring without a test of its own | AC-018, AC-019, AC-132, AU-01 | `SessionForker`, `OmpSideChatEngine`, `BestOfNCandidateRunner`, `HeadAutomationSession(s)` (the real UI path of desktop automations) and the slot consent path have no test of their own; their parts are tested through fakes | reports NativeSessionWiring, WireAutomationsContainers |
| Windows, macOS, Android device, iOS runs | SK rows, AC-123, CA-26, RD-05 | No runners or devices; every Cursor key is listed for Ctrl, macOS keys are unmeasured; PTY terminal is Linux only; nothing ran on Windows, macOS or Android | OQ-69, OQ-74, `docs/runbooks/local-terminal.md` |
| Owner sign-offs and accounts: brand, licence form decisions, GitHub social preview, discussion categories, GitBook space, Railway deploy, sign-in authority | section 28 | See the owner list at the end | OQ-93 to OQ-104 |

Gate (recorded 2026-10-28, added after the audit was written): `./bootstrap.sh` exited 0 on a clean worktree of `e24e6851` (12215 tests, 12213 succeeded, 0 failed, 2 skipped; `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`). The audit grades the tree `7535859`; about nine product files changed after it and were not re-graded. The GitHub `ci` workflow failed for both release pushes at 'Start test services' (OQ-168).

## 5. Agent and chat (AC-001 to AC-031)

Source: `01-cursor-agent-chat.md` section 2. Reachability: the desktop head is `apps/OmpOS.Native` with `src/OmpOS.Native.Views` (`MainView`, `AgentSessionRuntime`, `ComposerView`); the web head is `apps/OmpOS.Web` (`WebWorkbench`, `WebSessionRuntime`) over the Demo handler in the public build. omp is the engine over ACP (`04-omp-cursor-map.md`). Cursor findings N1 to N10 of file 01 change three rows: inline edit is retired (AC-144), Memories were removed (AC-066), Max Mode is legacy (AC-165).
Counts (31 rows): MET 5, MET-LIVE 0, PARTIAL 16, DEFERRED 3, NOT-STARTED 6, OMITTED-BY-DECISION 1.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-001 | Agent mode | PARTIAL | `AgentSessionRuntime`, `SessionView`, `EngineSendPathTests`, `SharedScenarioParityTests`, `ParityInventoryTests`; web: `WebSessionRuntime` over `DemoApiHandler`, `AgentSessionRuntimeWiringTests` | Never run with a live model beyond the bounded Phase 11 run (OD-07); the web head shows a simulated engine (ADR-0017 Demo mode) |
| AC-002 | Ask mode (read-only) | DEFERRED | `OptionChipView` (mode chip), `SlashCommandCatalogue` (`/ask` Partial: works only if the engine advertises an ask mode) | omp 18.12.6 ACP modes are `default` and `plan` only (04-omp-cursor-map.md row 8); a read-only emulation process is not built |
| AC-003 | Plan mode | PARTIAL | `OptionChipView` (mode chip), `OptionChipModelTests`, `SlashCommandCatalogueTests` (`/plan` Built, `mode.set`), `PlanBlockView` (src/OmpOS.UI/Components/Blocks/PlanBlockView.razor) | The Approve-and-execute step needs ACP form elicitation: no code in `src/`, `apps/`, `services/` handles it (grep); no editable plan file, no Build in Parallel |
| AC-004 | Debug mode | DEFERRED | `SlashCommandCatalogue` (`/debug` Partial: only if the engine advertises a debug mode) | omp 18.12.6 advertises no debug mode (04-omp-cursor-map.md row 8); no Debug pill |
| AC-005 | Mode switching (Shift+Tab, fresh context per mode) | PARTIAL | `OptionChipView` (mode chip), `ShortcutCatalogue` (no Shift+Tab entry), `ShortcutCatalogueTests` | Mode is switched from the chip only; Shift+Tab rotation is not bound; fresh-context-per-mode not observed |
| AC-006 | Custom Modes (skill pinned per turn) | MET | `CustomModeModel`, `CustomModeChipView` (mounted by `MainView.Panes.cs`), `IModeSkillSource`, `ConventionModeSkillSource`, `IPromptPreamble`, `CustomModeModelTests`, `CustomModeChipViewTests`, `ConventionModeSkillSourceTests`, `AgentSessionRuntimeWiringTests` (the pinned preamble is put in front of a prompt and not in front of a `/` prompt) | Desktop only. A skill is pinned from the mode chip; Alt+Enter on a skill in the slash palette is not built (SK-35); built-in skills are not offered as modes (`ConventionModeSkillSource` has no built-in source); the effect on a model turn was not observed (OD-07) |
| AC-007 | Design Mode (Annotate) | NOT-STARTED | `ADR-0009-browser-pane.md` (accepted for Linux only), `tools/browser-spike` (outside the solution) | No WebView, browser-pane or design-mode code in `src/`, `apps/`, `services/` (grep); Windows and macOS unmeasured (OQ-51) |
| AC-008 | Agents Window | PARTIAL | `AgentRailView`, `AgentPanelViewModel`, `AgentPanelViewModelTests`, `KanbanView`; web: src/OmpOS.UI/Components/Rail/AgentRail.razor, src/OmpOS.UI/Components/Kanban/KanbanView.razor over `DemoApiHandler`, `SessionCatalogOverlayTests` | Agents tab, rail and Kanban exist; the desktop rail also shows cloud agents when `OMPOS_API_BASE_URL` is set and the remote status of ssh sessions; lease holder always unavailable (`HeadKanbanDetailsSource`) |
| AC-009 | Multi-workspace in Agents Window | PARTIAL | `WorkspaceSetPickerView`, `WorkspaceSetPickerViewTests`, `WorkspaceSetsBackend`, `ShellAdaptersTests`, `PendingSessionTarget`, `PendingSessionTargetTests`, `WorkspaceSetSlashHandler`, `MultiRootWorkspaceTreeTests` | Desktop: the rail Folders dialog and `/load-workspace` open a saved set of folders as a new session (the first folder is the workspace, the rest are `--add-dir` roots); the explorer stays single-root (no head constructs `MultiRootWorkspaceTree`); `omp acp --add-dir` ran without a model (docs/runbooks/evidence/multi-repo-omp-probe-2026-10-27/README.md) and was never run in a model turn; the web Workspaces page is an information page with a desktop notice |
| AC-010 | Parallel agents and /multitask | PARTIAL | `SessionEngineSet`, `AgentSessionRegistry`, `SessionEngineSetTests`, `AgentPanelViewModelTests` | Several sessions run side by side; `/multitask` is not one of the 92 catalogued tokens (grep of `SlashCommandCatalogue`) |
| AC-011 | Agent sidebar management (pin, archive, group, unread, switcher) | PARTIAL | `AgentRailView`, `SessionCatalogModel`, `SessionCatalogModelTests`, `AgentRowStatusMapTests` | Rail lists sessions with status; no pin, archive, grouping, unread marks or Ctrl+Tab switcher (no such symbols in `Presentation/AgentPanel`) |
| AC-012 | Command palette in Agents Window (Ctrl+K) | PARTIAL | `SlashPaletteModel`, `TranscriptSearchModel`, `ConversationSearchModel`, `ConversationSearchView`, `ShortcutCatalogue` (Ctrl+K opens conversation search) | The slash palette, chat search and the Ctrl+K conversation search overlay exist; no command palette behind Ctrl+K and no paste-a-PR-URL jump |
| AC-013 | Projects (coordinator agent) | NOT-STARTED | `CloudAgentsViewModel` (cloud agent list only) | No coordinator, task tracker or subscriptions; depends on cloud agents going live |
| AC-014 | Local to cloud handoff, /in-cloud, /autopilot | PARTIAL | `CloudHandoff`, `ICloudHandoff`, `CloudHandoffModel`, `CloudHandoffTests`, `CloudHandoffModelTests`, `CloudHandoffViewTests`, `ShellSlashHandlerTests`, `ApiCloudAgentsHost`, `ApiCloudAgentsHostTests`, `SlashCommandCatalogue` (`/in-cloud` Partial, `/autopilot` Unavailable) | `/in-cloud` and a leading `&` open a dialog that asks for the repository and the cost, then hand off through `ApiCloudAgentsHost`; it needs `OMPOS_API_BASE_URL` and a sign-in, and says what is missing otherwise; the host is tested with fakes only; the API was never deployed on Railway (OQ-100; a staged private worker was, 2026-10-28); `/autopilot` is Unavailable |
| AC-015 | Queued messages | MET | `QueuedMessagesModel`, `QueuedMessagesRegistry`, `QueuedMessagesSteeringAdapter`, `QueuedMessagesView` (mounted under the composer by `MainView.Panes.cs`), `QueuedMessagesModelTests`, `QueuedMessagesViewTests`, `QueuedMessagesSteeringAdapterTests`, `RunInputSchedulerTests` | Tab in the prompt queues the draft behind a running turn and the next message leaves the queue when the run ends; reorder is in the model; no Queued pill (AC-027); not observed with a live turn (OD-07); desktop only |
| AC-016 | Send now / steer | MET | `SteeringInputModel`, `RunInputScheduler`, `AcpSteeringTests`, `EngineRunInputSendTests`, `LiveOmpSteeringContractTests`; shortcuts Ctrl+Enter steer and Ctrl+Shift+Enter follow-up in `ShortcutCatalogue`; web: src/OmpOS.UI/Components/Composer.razor, src/OmpOS.UI/Components/Composer/SteeringHistory.razor | Steering into a running turn and omp-side follow-up delivery were never observed live (no live model turn (OD-07)); contract test used an idle session |
| AC-017 | Checkpoints and restore | MET | `CheckpointStore`, `RewindPlanner`, `CheckpointsModel`, `CheckpointRecorderModel`, `CheckpointsTimelineView` (mounted by `MainView.Panes.cs`), `CheckpointBackend` (apps/OmpOS.Native/Composition/Checkpoints), `CheckpointStoreTests`, `CheckpointsModelTests`, `CheckpointRecorderModelTests`, `CheckpointsTimelineViewTests`, `CheckpointBackendTests`, `TurnChangeSetTests` | Desktop local sessions: each turn is recorded and `/rewind`, `/restore` and `/undo` put the files back, never the conversation (omp has no file-level checkpoint, 04-omp-cursor-map.md row 9); no test forces an I/O failure mid-rewind, so the rollback path is unexercised (report Checkpoints); no test drives it in an ssh, container or WSL session; the web head has none |
| AC-018 | Fork and revert | PARTIAL | `ForkModel`, `ISessionForker`, `SessionForker` (apps/OmpOS.Native/Agents), `ForkSlashHandler`, `ForkModelTests`, `ModelSlashHandlerTests`, `AcpForkTests` (wire method `session/fork`), `AgentSessionRuntimeWiringTests` (`/duplicate` opens and selects the fork) | `/fork`, `/branch` and `/duplicate` fork the whole conversation into a new session; `/fork <n>` from an earlier message is refused (`CanForkFromMessage` is false) and editing an earlier message is not built; revert is AC-017; `SessionForker` has no test of its own and the wire method `session/fork` never ran against a live omp (UNVERIFIED) |
| AC-019 | Side chats (/side, /btw) | PARTIAL | `SideChatPanel`, `SideChatHub`, `SideChatModel`, `ParentSummaryBuilder`, `SideChatPaneView`, `OmpSideChatEngine` (apps/OmpOS.Native/Agents), `SideChatTests`, `SideChatHubTests`, `SideChatModelTests`, `SideChatPaneViewTests`, `ShellSideChatAndSearchTests` | `/side` opens a side chat whose question is answered by a second omp that gets a summary of the conversation as hidden context; `OmpSideChatEngine` has no test of its own, `--approval-mode always-ask` for that omp is UNVERIFIED and no live omp answered; Ask in Side Chat from a selection is not built (SK-13); the mention source of side chats is not supplied |
| AC-020 | Conversation search | MET | `TranscriptSearchModel`, `TranscriptSearchView`, `TranscriptSearchScopeTests`, `TranscriptSearchChatScopeViewTests`, `ShortcutCatalogue` (Ctrl+F, Ctrl+K), `ConversationSearchIndex`, `ConversationSearchView`, `ConversationSearchModelTests`, `ConversationSearchViewTests`, `ConversationSearchIndexTests` | In-chat find (Ctrl+F) and the Ctrl+K conversation search overlay (`ConversationSearchView`, mounted by `MainView.Shell.cs`) reach the desktop head; Ctrl+K is not a command palette (AC-012) |
| AC-021 | Context usage ring and breakdown | PARTIAL | `ContextRingModel`, `ContextRingState`, `ContextRingView` (drawn inside `ComposerView`), `ContextUsageEvent`, `ContextRingModelTests`, `AcpUsageAndResourcesTests`, `AgentSessionRuntimeWiringTests` (a `ContextUsageEvent` feeds the ring) | The ring is fed from the ACP `usage_update` (size and used) in desktop chat sessions; the per-category breakdown tray needs engine counts that ACP does not give (04-omp-cursor-map.md row 21) |
| AC-022 | Summaries and /summarize | PARTIAL | `SlashCommandCatalogue` (`/summarize`, `/compress` Delegated to omp `/compact`), `SlashCommandCatalogueTests` | Forwarded text only; compaction events are not forwarded over ACP (04-omp-cursor-map.md section B3); not observed live |
| AC-023 | /goal and /loop | DEFERRED | `SlashCommandCatalogue` (`/goal`, `/loop` Unavailable) | omp runs both in its terminal UI or RPC only; no ACP handler (04-omp-cursor-map.md row 31) |
| AC-024 | Ask-questions tool | NOT-STARTED | `SlashCommandCatalogue` (not applicable); 04-omp-cursor-map.md row 23 | omp's `ask` tool needs the client to implement ACP form elicitation; no code references elicitation in `src/`, `apps/`, `services/` (grep), so the tool fails headless |
| AC-025 | Image input | PARTIAL | `PromptImage`, `PromptOptions` (Images), `ComposerAttachmentsModel`, `AcpImagePromptTests`, `AgentSessionRuntimeImageTests`, `ComposerImageViewTests`; 04-omp-cursor-map.md row 19 | Desktop composer: attach from a file picker or the clipboard; the send is refused when omp's `initialize` reports `promptCapabilities.image` false (omp 18.12.6 reports true, read from its source); model vision stays Unknown (`ModelVision` is not supplied), so the head cannot tell whether the chosen model reads images; no live turn; web and Android attach not built; a native slash action drops attached images |
| AC-026 | Voice input and voice calls | NOT-STARTED | 04-omp-cursor-map.md section B2 (`speech.models.list`) | No microphone or speech code in `src/` or `apps/` |
| AC-027 | Chat panel pills and tray | NOT-STARTED | `PipelinePill` (omp stage pills, unrelated) | No Goal, Queued, Debug, Canvas or Open Browser pill; the queue is a list under the composer (AC-015), not a pill |
| AC-028 | Chat content actions (table copy, Mermaid, download, Review Changes) | PARTIAL | `ChangesBarView`, `ChangesBarViewTests`, `DiffReviewView`; web: src/OmpOS.UI/Components/Changes/ChangesBar.razor, `ChangesBarTests` | Review Changes list exists; no Markdown-table copy, Mermaid zoom or Download File (no Mermaid symbol in `src/`) |
| AC-029 | Deeplinks (prompt, command, rule) | NOT-STARTED | `MentionUris` (mention URIs only) | No URL handler for an OmpOS scheme in `apps/OmpOS.Native`, `apps/OmpOS.Android` or `apps/OmpOS.Web` |
| AC-030 | Agent resume after crash | PARTIAL | `SessionStatus` (Interrupted), `ReconnectingEngineHost`, `TurnInterruptedEventArgs`, `RemoteStatusDescriber`, `ReconnectingEngineHostTests`, `AgentSessionRuntimeWiringTests` (turn interrupted) | Remote sessions raise Interrupted and a reconnect status through `ReconnectingEngineHost`; a local host crash shows no Resume action; reconnect against a real host was not run (the 2026-10-28 live ssh run only checked that `loadSession` is advertised) |
| AC-031 | Shared transcripts | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md); local export: `TranscriptMarkdownExporter`, `TranscriptExportViewTests` | Needs a Cursor account and Cursor-hosted pages; OmpOS offers local Markdown export only |

## 6. Context and ignore (AC-040 to AC-051)

Counts (12 rows): MET 3, MET-LIVE 0, PARTIAL 7, DEFERRED 0, NOT-STARTED 1, OMITTED-BY-DECISION 1.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-040 | @ Files and Folders | MET | `MentionParser`, `MentionPickerModel`, `MentionPickerView` and `ComposerView` chips, `MentionSourceFactory`, `WorkspaceMentionFiles`, `AcpPromptContent`, `MentionParserTests`, `MentionPickerModelTests`, `ComposerMentionViewTests`, `MentionSourceFactoryTests`, `WorkspaceMentionFilesListTests`, `WorkspaceMentionFilesReadTests`, `AgentSessionRuntimeWiringTests` (a mention travels as a prompt resource), `AcpUsageAndResourcesTests` | Desktop local chat sessions only: ssh, container and WSL sessions have no mention files (`RemoteMentionFiles`) and the web head has no picker; the embedded-resource send was not observed with a live omp |
| AC-041 | @Terminals | PARTIAL | `IMentionTerminalProvider`, `CatalogMentionProvider`, `CatalogMentionProviderTests` | Terminals are listed by the catalogue provider, but no head supplies their output (`MentionSourceContext.ReadTerminalOutput` is unset), so a pick answers that the output is not available in this window |
| AC-042 | @Chats | MET | `IMentionChatProvider`, `CatalogMentionProvider`, `CatalogMentionProviderTests`, `MentionSourceFactoryTests`, `AgentSessionRuntimeWiringTests` | Other chats of the window are attached from their transcript (`AgentSessionRuntime.TranscriptText`); side-chat mentions are not supplied; desktop only |
| AC-043 | @ Git diffs (working state, branch diff) | MET | `IGitDiffSource`, `GitMentionDiffSource`, `MentionGitDiffKind`, `GitMentionDiffSourceTests`, `MentionSourceTests` | Working-state and branch diffs of the workspace root, read through `IProcessRunner`; local sessions only |
| AC-044 | @Browser | NOT-STARTED | `ADR-0009-browser-pane.md` | Blocked on the browser pane (AC-126); a `BrowserMentionSource` exists in the library and is composed in no head (grep, recorded 2026-10-28) |
| AC-045 | @rule, @skill, @side-chat, # pull request, + menu | PARTIAL | `MentionKind`, `MentionTrigger`, `UnavailablePullRequestSource`, `MentionParserTests`, `ConventionMentionProvider`, `ConventionMentionProviderTests` | `@rule` and `@skill` read the discovered conventions; `#` answers 'needs a configured Git host' (`UnavailablePullRequestSource`); the `+` Attach and Summarize menu is not built; the side-chat source is not supplied |
| AC-046 | .cursorignore | PARTIAL | `IgnoreMatcher`, `PhysicalIgnoreFileSource`, `IgnoreSyntaxTests`, `IgnoreHierarchyTests`, `IMentionIgnoreFilter`, `IgnoreMatcherFactory`, `IgnoreSettingsPage` | Filters @ mentions in the desktop head, with the global patterns and default-list switch of the Ignore page; omp does not honour it for its own tools [INFERENCE in 01]; not tested in a remote session |
| AC-047 | Default ignore list and .gitignore | PARTIAL | `IgnoreDefaults`, `IgnoreDefaultsTests`, `IgnoreLimitsTests` | Same as AC-046 |
| AC-048 | Instant Grep (local search index) | PARTIAL | `WorkspaceSearch`, `WorkspaceSearchView`, `WorkspaceSearchTests`, `WorkspaceSearchViewTests` | User-facing text and regex search is met; no persistent index; the agent searches with omp's own tools |
| AC-049 | Semantic codebase index, .cursorindexingignore | OMITTED-BY-DECISION | previous audit ADE-18 (`WorkspaceSearch`: semantic index out of scope by design); `docs/settings-parity.md` rows 14 and 15 | Sources conflict on whether Cursor still ships it (01-cursor-agent-chat.md N6); nothing built |
| AC-050 | Multi-root workspaces | PARTIAL | `MultiRootWorkspaceTree`, `WorkspaceRootSource`, `CodeWorkspaceFile`, `OmpLaunchPlanner` (`--add-dir`), `OmpLaunchPlannerTests` | Launch carries `--add-dir` (`omp acp --add-dir` ran without a model, docs/runbooks/evidence/multi-repo-omp-probe-2026-10-27/README.md) and a set of folders opens as one session (AC-009); the explorer in the head is single-root (`MultiRootWorkspaceTree` is constructed by no head); `--add-dir` in a model turn not probed |
| AC-051 | Multi-repo cloud environments | PARTIAL | `CreateCloudAgentRequest` (Repositories), `CloudRepository`, `CloudWorkerRepository`, `CloudAgentLifecycleTests` | Worker does not clone the repositories yet (CloudControlPlane report: bootstrap missing); the API was never deployed on Railway (a staged private worker was, 2026-10-28) |

## 7. Rules, skills, subagents and commands (AC-060 to AC-092)

Counts (19 rows): MET 2, MET-LIVE 0, PARTIAL 10, DEFERRED 1, NOT-STARTED 2, OMITTED-BY-DECISION 4.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-060 | Project rules (.mdc, four types) | PARTIAL | `ConventionCatalog`, `ConventionRuleKind`, `ConventionCatalogTests`, `RulesSettingsPage`, `RulesSettingsSource` | Listing is read-only; omp loads `.cursor/rules/*.mdc` itself (04-omp-cursor-map.md row 4); OmpOS feeds no rule to a session; no editor; `/create-rule` ships as a built-in skill (AC-072) |
| AC-061 | AGENTS.md including nested | PARTIAL | `ConventionRuleKind` (AgentsFile), `ConventionItemScanner`, `ConventionCatalogTests` | omp reads AGENTS.md natively (04-omp-cursor-map.md row 4); nested application not verified; not observed live |
| AC-062 | User rules | PARTIAL | `ConventionScope` (user scope), `ConventionCatalogTests`, `RulesSettingsPage` | User-level rule files are listed; Cursor's in-app text box is not built |
| AC-063 | Team rules | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | Dashboard feature of Cursor teams; no accounts or teams |
| AC-064 | Rule import via plugin / GitHub | NOT-STARTED | `PluginsPage`, `OmpPluginCatalog` (four fixed plugins, OQ-80) | No marketplace.json import |
| AC-065 | Fetch Rules tool | PARTIAL | 04-omp-cursor-map.md row 4 (omp `rule://` URLs) | Provided by omp; OmpOS adds nothing and observes only the tool call |
| AC-066 | Memories | OMITTED-BY-DECISION | Fit omit-no-accounts and finding N2 (feature removed) in 01-cursor-agent-chat.md; 04-omp-cursor-map.md row 5 | Nothing to build; omp has its own memory backends |
| AC-070 | Skills (SKILL.md standard) | PARTIAL | `ConventionSkill`, `ConventionFrontmatterParser`, `SkillsSettingsPage`, `ConventionSlashDiscoverySource`, `SlashPaletteDiscoveryTests`, `ConventionCatalogTests` | Discovery and palette listing reach the desktop head (project, user and built-in scopes); auto-selection, progressive loading and invocation are omp's and not observed live; `/create-skill` ships as a built-in skill (AC-072) |
| AC-071 | Skill directories and compat | PARTIAL | `ConventionDirectories` (.ompos .omp .cursor .claude .codex .agents, project and user), `LocalConventionFileSourceTests`, `ConventionSettingsLoader` | OmpOS lists all six; omp itself does not read `.cursor/skills` (04-omp-cursor-map.md row 3), so a listed skill may not load in the engine; remote and cloud sync not built |
| AC-072 | Built-in skills (19) | PARTIAL | `SlashCommandCatalogue` (`Skill` kind), `BuiltinSkillSource`, `BuiltinSkillManifest`, `assets/skills`, `BuiltinSkillSourceTests`, `BuiltinSkillManifestTests`, `BuiltinSkillDiscoveryTests`, `BuiltinSkillsDesktopTests` | Ten skills ship in `assets/skills`: nine Cursor tokens (agent-review, create-hook, create-rule, create-skill, create-subagent, migrate-to-skills, review, review-security, split-to-prs) and the OmpOS addition update-settings; ten built-in tokens stay Unavailable (automate, autopilot, canvas, cursor-blame, loop, review-bugbot, sdk, statusline, update-cli-config, update-cursor-settings); the desktop palette and Skills page list the built-ins, API-hosted and remote sessions carry none; no skill ran with a live model |
| AC-073 | Sync and publish skills | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md); `docs/settings-parity.md` row 5 | Needs Cursor cloud sync and a team marketplace |
| AC-080 | Built-in subagents (Explore, Browser) | OMITTED-BY-DECISION | `docs/settings-parity.md` row 9 (Omitted: delegation is omp and the Swarm plugin); 04-omp-cursor-map.md row 6 | omp ships its own agents (scout, reviewer, task); no OmpOS list or editor |
| AC-081 | Custom subagents | PARTIAL | `ConventionSubagent`, `SlashDiscoveredSubagent`, `ConventionCatalogTests`, `SlashPaletteDiscoveryTests` | Definitions in `.cursor/agents` and other roots are listed and offered as /name; omp deliberately skips `.claude/agents`, `.codex/agents` and `.cursor/agents` (04-omp-cursor-map.md row 6); no Subagents page (S27 Omitted) |
| AC-082 | Subagent model parameters | NOT-STARTED | `ConventionSubagent` (frontmatter is parsed) | No `id[key=value]` model parameter support in the picker or any run path |
| AC-083 | Foreground, background, resume subagents | DEFERRED | 04-omp-cursor-map.md section B3 (ACP emits no subagent roster) and B5 (RPC has `get_subagents`) | Needs omp RPC or an extension; not forwarded over ACP |
| AC-084 | Isolated and cloud subagents | PARTIAL | `WorktreeManager`, `WorktreeManagerTests`, `CloudHandoff`; omp `task.isolation` (04-omp-cursor-map.md row 13) | Worktree library is wired in the hosted API and, on the desktop, behind `/best-of-n` and the Worktrees panel (`WorktreeBackend`); cloud subagent hand-off needs the API address and a sign-in (AC-014) |
| AC-090 | Custom slash commands | MET | `ConventionCommand`, `ConventionSlashDiscoverySource`, `SlashPromptComposer`, `SlashPaletteModel`, `SlashPaletteDiscoveryTests`, `SlashPromptComposerTests`, `CommandsSettingsPage`; web: `WorkspaceSlashDiscoverySource` | Result of a command prompt was not observed with a live model; team commands from a dashboard are omitted |
| AC-091 | Command to skill migration (/migrate-to-skills) | MET | `SlashCommandCatalogue` (`Skill` kind, `/migrate-to-skills` Built), `assets/skills/migrate-to-skills/SKILL.md`, `BuiltinSkillsDesktopTests`, `BuiltinSkillSourceTests` | Ships as a built-in skill found by the desktop palette; a project or user skill of the same name takes over; the result was not observed with a live model |
| AC-092 | Cursor slash commands that matter for parity | PARTIAL | `SlashCommandCatalogue`, `SlashCommandRegistry`, `SlashCommandCatalogueTests`, `SlashCommandReferenceTests` | See section 21 for each of the 92 tokens; per-token counts are in the summary |

## 8. Hooks (AC-100 to AC-104)

Counts (5 rows): MET 0, MET-LIVE 0, PARTIAL 5, DEFERRED 0, NOT-STARTED 0, OMITTED-BY-DECISION 0.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-100 | Agent hook events (21) | PARTIAL | `HookEvents`, `HookDispatcher`, `HooksConfigLoader`, `HookEventsTests`, `HookDispatcherGatingTests`; engine side: `HooksService`, `HookBridgeServer`, `AcpHookTap`, `IAcpHookGate`, `AcpHookTapTests`, `AcpHookGateTests`, `HookBridgeIntegrationTests`, `HooksServiceTests`, `HookExtensionEndToEndTests`; head: `HeadServices`, `SessionEngineSet`, `HeadServicesWiringTests`, `EnginePermissionOrderTests` (a hook deny comes before the approval policy, the run mode and the user), `AgentSessionRuntimeWiringTests` (hook notice); omp extension plugins/ompos-hooks-omp/src/index.mjs and test `bridge.test.mjs` | Wired for local desktop sessions: one `HooksService` per head, a hooks session per session, the loopback bridge in the launch plan and the ACP tap as `AcpClientOptions.HookGate`; ssh, container and WSL sessions get no bridge and no hook files (`NoHookFiles`); no hook ran against a live omp (OD-07); the ACP `afterFileEdit` fallback fires only if omp calls `fs/write_text_file` (UNVERIFIED, report HooksIntegration) |
| AC-101 | Tab and app hooks (workspaceOpen) | PARTIAL | `HookEvents` (beforeTabFileRead, afterTabFileEdit, workspaceOpen catalogued), `HookEventsTests` | Tab hooks never fire (no Tab engine); no head dispatches `workspaceOpen` |
| AC-102 | Hook types and exit codes | PARTIAL | `HookOutputParser`, `HookOutputParserTests`, `HookDispatcherFailurePolicyTests`, `HookDispatcherPromptTests`, `SystemHookCommandRunnerTests` | `IPromptHookEvaluator` has no implementation in any head (prompt hooks cannot run); Windows PowerShell and cmd shells written, never run |
| AC-103 | Hook locations, distribution, hot reload | PARTIAL | `HookSourceLocator`, `HookSourceLocatorTests`, `HooksConfigLoaderTests`, `HookConsentIdentity`, `HookConsentIdentityTests`, `HooksPageViewTests`, `HookRunLogModelTests` | Project, user and Claude settings locations load for local sessions and the Hooks page lists them (rendered in the 2026-10-28 desktop smoke); reload is by call (no file watcher); hook files of an ssh, container or WSL host are not read; MDM and Enterprise cloud distribution omitted (no accounts) |
| AC-104 | Hooks in cloud agents | PARTIAL | `HookLocatorContext`, `CloudHooksSession`, `CloudHookFilter`, `CloudHookBridgeHandler`, `HooksConsentSnapshotReader`, `CloudHooksWorkerTests`, `CloudHookFilterTests` | The worker builds a cloud hook subset behind the consent claim `ompos_hooks_consent`, which the API does not mint yet, so cloud sessions report `untrusted`; `deploy/worker.Containerfile` copies no `plugins/` folder (grep); worker clone bootstrap missing; no hook ran in the staged worker on Railway (2026-10-28) |

## 9. MCP and plugins (AC-110 to AC-118)

Counts (9 rows): MET 1, MET-LIVE 0, PARTIAL 3, DEFERRED 0, NOT-STARTED 3, OMITTED-BY-DECISION 2.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-110 | MCP transports (stdio, SSE, Streamable HTTP) | PARTIAL | `McpServerCatalog`, `McpToolRegistry`, `AcpMcpServerMapper`, `McpPageView`, `McpToolsView`, `McpToolInvokerTests`, `McpHttpTests`, `AcpMcpServerMapperTests`, `AcpMcpServerSessionTests`, `McpServerCatalogTests`, `McpPresetSourceTests`, `McpPresetPageViewTests`, `EngineCompositionOptionsTests`, `HeadServicesWiringTests` | Wired for local desktop sessions: the head offers the catalogue's servers in `session/new` (`ExtraMcpServers`) and Settings > MCP lists, switches and presets them (13 presets); that omp connects to a server is UNVERIFIED (no live omp, no model turn, no preset address contacted); no connection checker is registered, so Connected and Failed never appear; ssh, container and WSL sessions get no servers; the web page is a read-only list |
| AC-111 | MCP config locations and interpolation | MET | `McpConfigLoader` (used by `McpServerCatalog`), `McpLocations`, `McpVariableExpansionTests`, `McpConfigPrecedenceTests`, `McpConfigFormatTests`, `McpConfigValidationTests`, `McpServerCatalogTests` | The head reads the OmpOS, Cursor, Claude Code and VS Code files for every local session and sends the servers to omp, because ACP sessions start with omp's own MCP discovery off (04-omp-cursor-map.md row 10); whether omp then connects is UNVERIFIED (AC-110) |
| AC-112 | MCP OAuth | NOT-STARTED | 04-omp-cursor-map.md row 10 (omp config supports oauth) | No callback listener or sign-in UI in `src/` or `apps/` |
| AC-113 | MCP protocol features (elicitation, apps, images) | NOT-STARTED | 04-omp-cursor-map.md row 10 and 23 | No elicitation handling (grep), no MCP Apps surface |
| AC-114 | MCP approvals and limits | PARTIAL | `RunControlsView`, `EngineToolApprovalBridge`, `EngineSendPathTests`, `McpToolName`, `PermissionsFileReader`, `PermissionsFileTests`, `RunModePermissionResolverTests`, `EnginePermissionOrderTests` | Per-call approval works through the ACP bridge; the `permissions.json` mcpAllowlist and the run mode are enforced by `RunModePermissionResolver` for local desktop sessions; omp's permission request field names and MCP tool naming are UNVERIFIED (report Safety): a request without the needed field asks the user; no admin allowlist |
| AC-115 | MCP install links and extension API | NOT-STARTED | `McpOwnedConfigWriter` (writes OmpOS-owned config) | No deeplink handler, no extension API |
| AC-116 | Plugins (Agent and Cursor formats) | PARTIAL | `PluginsPage`, `PluginsSettingsSource`, `OmpPluginCatalog`, `PluginsPageTests` | Four fixed plugins, no switch (OQ-80); Cursor-format plugin import not built (04-omp-cursor-map.md row 26) |
| AC-117 | Marketplace and team marketplaces | OMITTED-BY-DECISION | Fit omit-no-accounts (use a public marketplace.json instead); `docs/settings-parity.md` row 13 | Marketplaces need accounts and a store |
| AC-118 | Plugin canvases | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | Third-party hosted canvases |

## 10. Tools, terminal, sandbox and browser (AC-120 to AC-130)

Counts (11 rows): MET 2, MET-LIVE 0, PARTIAL 5, DEFERRED 1, NOT-STARTED 2, OMITTED-BY-DECISION 1.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-120 | Search tools | MET | `WorkspaceSearch`, `WorkspaceSearchView`, `WorkspaceSearchTests`; omp tool calls rendered by `ToolCallBlockView` (src/OmpOS.Native.Views/Views/Blocks) and src/OmpOS.UI/Components/Blocks/ToolCallBlock.razor | Agent-side search is omp's tools; Explore subagent is omp's |
| AC-121 | Read files incl. images | PARTIAL | `DataImage` (src/OmpOS.Native.Views/Views/Blocks/DataImage.cs), `NativeUiBlockRendererTests` | Reading is omp's; image blocks from agents render; the desktop composer can attach images (AC-025) |
| AC-122 | Edit files, diff view, Review Changes, Undo | MET | `DiffReviewView`, `ChangesBarView`, `ChangeSetModel`, `DiffReviewTests`, `ChangesBarViewTests`, `DiffReviewWriterTests`; web: src/OmpOS.UI/Components/Changes/ChangesBar.razor, `ChangesBarTests`, `WebChangeReview` | Edits are omp's; web reads changes through the API (Demo engine in the public build) |
| AC-123 | Terminal tool and Run Modes | PARTIAL | `TerminalPanelView`, `TerminalGate`, `TerminalGateTests`, `AgentsPage`; `RunModePolicy`, `RunModePermissionResolver`, `RunModeHub`, `RunModePolicyHolder`, `RunModePageView`, `RunModePolicyTests`, `RunModePermissionResolverTests`, `RunModePolicyHolderTests`, `RunModeLaunchTests`, `RunModePageViewTests`, `EnginePermissionOrderTests` (hook deny, approval policy, run mode, then the user; Run Everything admits all 10 ACP kinds; a deny rule still applies in Run Everything) | Wired for local desktop sessions: the three run modes map to omp `--approval-mode` and wrap the resolver; there is no hosted Auto-review classifier by design; a changed mode reaches a running omp only when its engine is composed again; whether an ssh, container or WSL omp gets `--approval-mode` is UNVERIFIED; the terminal and its auto-run switch are Linux only (OQ-69); omp's request field names are UNVERIFIED; no live turn |
| AC-124 | permissions.json | PARTIAL | `PermissionsFileReader`, `PermissionsFileEditor`, `EffectivePermissions`, `PermissionsFileTests`, `PermissionTokenParserTests`, `RunModeHub`, `RunModePermissionResolverTests`, `EnginePermissionOrderTests` | Re-read when a file's write time changes and enforced by OmpOS's ACP resolver, not by omp, for local desktop sessions; the Run mode page edits the terminal and MCP allowlists, deny rules are read and not edited (S03); omp's request field names are UNVERIFIED (report Safety) |
| AC-125 | Sandbox (Seatbelt, Landlock, sandbox.json) | PARTIAL | `docs/settings-parity.md` S05 and S15 (sandbox limits are omp's); 04-omp-cursor-map.md row 12 (omp has no sandbox); `SandboxFileReader`, `EffectiveSandbox`, `SandboxFileTests`, `SandboxBoundaryTests`, `RunModePermissionResolverTests` | Only the read boundary of `sandbox.json` exists and it binds the tool calls omp asks OmpOS about; Seatbelt, Landlock and a sandbox network mode are omitted by decision (omp has no sandbox) |
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
| AC-131 | Worktrees (Agents Window) | PARTIAL | `WorktreeManager`, `WorktreeApplier`, `CursorWorktreesConfig`, `WorktreeManagerTests`, `WorktreeApplierTests`, `CursorWorktreesConfigTests`, `AcpHarnessSessionFactory`, `WorktreeBackend`, `WorktreeBackendTests`, `WorktreeSetupConsentAdapterTests`, `WorktreesPageViewTests` | Hosted API: opt-in `Worktrees:Root`. Desktop: worktrees are created by `/best-of-n` and listed in the Worktrees panel and the read-only Worktrees settings page; a normal local run still uses the workspace; no test drives it in an ssh, container or WSL session |
| AC-132 | /worktree, /apply-worktree, /delete-worktree, /best-of-n | PARTIAL | `WorktreeSlashHandler`, `WorktreeCommandsModel`, `BestOfNState`, `ModelSlashHandlerTests`, `WorktreeCommandsModelTests`, `SlashCommandCatalogue` (`/worktree` Delegated to omp `/wt`), `WorktreeCommandsView`, `WorktreeCommandsViewTests`, `BestOfNHost`, `BestOfNHostTests`, `WorktreeBackendTests` | `/worktree` is delegated to omp `/wt`; `/apply-worktree` and `/delete-worktree` are Built; `/best-of-n` is Partial: n candidates of one model, not one per model, and it creates its worktrees only in Run Everything |

## 12. Tab and inline edit (AC-140 to AC-145)

Counts (6 rows): MET 0, MET-LIVE 0, PARTIAL 1, DEFERRED 0, NOT-STARTED 4, OMITTED-BY-DECISION 1.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-140 | Tab completions | PARTIAL | `CompletionClient`, `InlineCompletionBox`, `CompletionClientTests`, `TabSettingsSource`, `TabPage` | `ComposerView.Completion` never assigned: no suggestion appears in the app (previous audit ADE-13); omp has no completion engine (04-omp-cursor-map.md row 32); wire format unconfirmed (OQ-55) |
| AC-141 | Next edit and cross-file jump | NOT-STARTED | none | No predicted-edit engine |
| AC-142 | Tab snooze and per-language off | NOT-STARTED | `TabPage` (read-only status), `docs/settings-parity.md` row 17 | Snooze, global disable and per-extension disable not built |
| AC-143 | Tab privacy and key rules | OMITTED-BY-DECISION | Fit omit-no-accounts (Cursor's own Tab model); `CompletionSettingsLoader` (user-configured provider) | OmpOS uses the provider the user configures |
| AC-144 | Cmd+K inline edit (retired: hand-off to Agent) | NOT-STARTED | `InlineEditView` (mounted as a tool without a model), `InlineEditClient`, `InlineEditTests` (the retired flow) | The replacement (selection becomes a referenced-lines prompt) is not built: Ctrl+Shift+L selection-as-context was left out of the Mentions work (report Mentions deviation 5) |
| AC-145 | Terminal Cmd+K | NOT-STARTED | none | Not documented by Cursor either; not built |

## 13. Review, Bugbot and git (AC-150 to AC-155)

Counts (6 rows): MET 0, MET-LIVE 0, PARTIAL 2, DEFERRED 0, NOT-STARTED 2, OMITTED-BY-DECISION 2.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-150 | Agent Review | PARTIAL | `SlashCommandCatalogue` (`/agent-review` Built, `Skill` kind), `assets/skills/agent-review/SKILL.md`, `BuiltinSkillsDesktopTests`; omp `reviewer` agent (04-omp-cursor-map.md row 17) | Ships as a built-in skill (a Quick or Deep local review that edits nothing unless asked); no review service, no Source Control tab and no BUGBOT.md reader; not run with a live model |
| AC-151 | Bugbot | OMITTED-BY-DECISION | Fit needs-cloud / omit-no-accounts (01-cursor-agent-chat.md); `SlashCommandCatalogue` (`/review-bugbot`: Bugbot is Cursor-hosted) | Cursor-hosted reviewer; a PR reviewer on OmpOS cloud would be new work |
| AC-152 | Bugbot repo config (bugbot.yaml) | NOT-STARTED | none | No reader of `.cursor/config/bugbot.yaml` (grep) |
| AC-153 | PR summaries | NOT-STARTED | none | Depends on a PR reviewer |
| AC-154 | Bugbot API | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | Enterprise Cursor API |
| AC-155 | Review in editor, /review, /split-to-prs, /cursor-blame | PARTIAL | `DiffReviewView`, `ChangesBarView`, `SlashCommandCatalogue` (`/review`, `/split-to-prs` and `/review-security` Built as skills; `/cursor-blame` Unavailable), `BuiltinSkillsDesktopTests` | `/review`, `/split-to-prs` and `/review-security` ship as built-in skills; no AI-authored-change attribution (`/cursor-blame`); no live model run |

## 14. Models, privacy and billing (AC-160 to AC-176)

Source: `01-cursor-agent-chat.md` section 9. OmpOS sells no inference, so billing and Cursor-hosted model routing are OMITTED-BY-DECISION; the model picker and bring-your-own-key shapes are built.
Counts (12 rows): MET 3, MET-LIVE 1, PARTIAL 2, DEFERRED 0, NOT-STARTED 0, OMITTED-BY-DECISION 6.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| AC-160 | Model picker | MET | `ModelPickerModel`, `ModelPickerView`, `ModelPickerModelTests`, `ModelsSettingsSource`; web: src/OmpOS.UI/Components/Composer/ModelPicker.razor | Ctrl+/ cycling and a palette Select Model are not bound; a switch was not observed with a live turn |
| AC-161 | Model parameters (fast, effort, context) | PARTIAL | `OptionChipModel`, `OptionChipModelTests`, `LiveOmpOptionChipsTests` | Thinking level only (omp's `thinking` option); no `id[key=value]` parameters |
| AC-162 | Auto and Cursor Router | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | Cursor inference; OmpOS sells none |
| AC-163 | Bring your own API key | MET-LIVE | `ModelsSettingsSource`, `OmpCredentialStore`, `ModelsApiKeyTests`, `LiveOmpConfigTests`, `ProviderCatalogue`, `ModelsListingViewTests` (103 provider rows), `ProvidersListEndpointTests`, `DemoProvidersRouteTests` | Live proof is the 2026-10-26 audit's recorded omp 18.12.6 run without a model turn, not re-run here; no real-credential call (OQ-71); the web Models page is real over `GET /providers` (the API maps `MapProviderEndpoints`) and is simulated in the Demo build |
| AC-164 | Custom models and endpoints | MET | `CustomProviderDraft`, `ModelsCustomProviderTests`, `OmpModelsFile` | Superset of Cursor: any OpenAI-compatible base URL |
| AC-165 | Max Mode | OMITTED-BY-DECISION | Fit omit-no-accounts (use the context-size parameter instead); `SlashCommandCatalogue` (`/max-mode` Unavailable) | Plan feature of legacy Cursor plans |
| AC-166 | Model access control, hidden models | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | Team dashboard |
| AC-170 | Privacy Mode | MET | `GeneralPage`, `SettingsCopy`, `GeneralPageTests`, `docs/settings-parity.md` row 19, `PrivacySettingsPage`, `PrivacyPageViewTests` | Read-only statement: OmpOS sends nothing itself, so there is no toggle |
| AC-171 | Zero data retention and data controls | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | Cursor dashboard and CMEK |
| AC-172 | Workspace trust | OMITTED-BY-DECISION | `docs/settings-parity.md` row 20 | No restricted mode exists; the hook consent and the run mode's trust flag are their own gates |
| AC-175 | Usage visibility | PARTIAL | `UsageHistory`, `UsageHistoryTests`, `TranscriptExportView`; `/usage` Delegated in `SlashCommandCatalogue` | Usage history is always empty in the app (`MainView.CreateExport` passes none); the context ring consumes omp's `usage_update` (size and used only, 04-omp-cursor-map.md row 21), the history does not |
| AC-176 | Plans and billing | OMITTED-BY-DECISION | Fit omit-no-accounts (01-cursor-agent-chat.md) | OmpOS has no accounts or billing |

## 15. Cloud agents (CA, EN, API, AU, IN rows and AC-190 to AC-194)

Sources: `02-cursor-cloud-remote.md` sections A to E and `01-cursor-agent-chat.md` section 10. Sub-series: CA (36 rows), EN (19), API (12), AU (19), IN (17), AC-190 to AC-194 (5). The control plane is `services/OmpOS.Api/Cloud`; the worker is `services/OmpOS.AgentWorker`; the Railway and static-pool clients are in `src/OmpOS.Sandbox`; the presentation models are `src/OmpOS.Presentation/Cloud`. Nothing here ran against Railway.
Counts (108 rows): MET 2, MET-LIVE 0, PARTIAL 30, DEFERRED 6, NOT-STARTED 48, OMITTED-BY-DECISION 22.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| CA-01 | Cloud agent core (VM, branch, PR) | PARTIAL | `CloudAgentService`, `CloudAgentEndpoints`, `CloudAgentLifecycleTests`, `CloudAgentAccessTests`, `CloudPullRequestTests`, `RailwayCloudEnvironmentClient`, `StaticPoolEnvironmentClient`, `AcpBridgeEndpoint`, `AcpWebSocketTransport`, deploy/worker.Containerfile | Control plane, worker bridge and Railway client built and tested with fakes; the cloud routes are mapped in `Program.cs` of `services/OmpOS.Api` and answer 404 `cloud_disabled` until `Cloud:Enabled` is true (`CloudDisabledTests`); the API was never deployed on Railway (OD-03, OQ-100); a staged private worker was deployed and handshaken after the release (2026-10-28, no clone, no model turn); worker does not yet clone, branch or run the environment file (report CloudControlPlane) |
| CA-02 | Naming (formerly Background Agents) | OMITTED-BY-DECISION | 01-cursor-agent-chat.md and 02-cursor-cloud-remote.md (naming fact, Fit '-') | Not a feature; OmpOS calls them cloud agents |
| CA-03 | Start surfaces (iOS, web, desktop, Slack, GitHub, Linear, API) | PARTIAL | `CloudAgentsPageView`, `NewCloudAgentView`, `CloudAgentsPageViewTests`, `NewCloudAgentViewTests`, `CloudAgentEndpoints`, `ApiCloudAgentsHost`, `ApiCloudAgentsHostTests`, `NotConfiguredCloudHost` | Desktop: the Cloud page of Settings (a not-configured state without `OMPOS_API_BASE_URL`), cloud rows in the rail and `/in-cloud`; the host is tested with fakes only; no iOS, Slack, GitHub-comment or Linear start; the web Cloud page is an information page |
| CA-04 | Repo provider clone (GitHub, GitLab, ADO, Bitbucket) | PARTIAL | `GitHubAppClient`, `PatCredentialSource`, `PullRequestService`, `CloudPullRequestTests`, `FakeGitHubHandler` | GitHub only (App token or PAT); GitLab, Bitbucket, Azure DevOps not built; token expires after 1 h and is not refreshed in a long-lived worker (report CloudControlPlane) |
| CA-05 | Start from scratch (Origin repo) | OMITTED-BY-DECISION | Fit omit-no-accounts (Origin is Cursor-hosted), 02-cursor-cloud-remote.md | Cursor-hosted forge |
| CA-06 | Port forwarding and preview | NOT-STARTED | `docs/runbooks/cloud-agents.md` | No port detection or public domain code; Railway domain or TCP proxy not used |
| CA-07 | Publish to live URL | OMITTED-BY-DECISION | Fit omit-no-accounts (Vercel plugin), 02-cursor-cloud-remote.md | A Railway publish step would be new work |
| CA-08 | Computer use | DEFERRED | `docs/runbooks/cloud-terminals.md`, `CloudSessionHost` | No remote desktop until Railway confirms in writing (OQ-73) |
| CA-09 | Artifacts and demos | NOT-STARTED | none | No artifact storage or PR embedding |
| CA-10 | Remote desktop control | DEFERRED | `docs/runbooks/cloud-terminals.md` | Same blocker as CA-08 (OQ-73) |
| CA-11 | MCP in cloud | NOT-STARTED | `CreateCloudAgentRequest` (no MCP field) | No MCP server selection for cloud agents; worker passes none |
| CA-12 | Cursor Cloud diagnostics MCP | NOT-STARTED | none | Optional; `CloudMetrics` exposes counters only |
| CA-13 | Run events | PARTIAL | `CloudStatusEvent`, `CloudTimelineRow`, `CloudAgentDetailsModelTests`, `CloudAgentEndpoints` (GET events) | Status events only; no setup_started or pr_creation_failed kinds; shown on the desktop Cloud page (`CloudAgentsPageView`) |
| CA-14 | Subscriptions and /subscribe | NOT-STARTED | none | No webhook receiver or wake queue |
| CA-15 | Auto-fix CI failures | NOT-STARTED | none | No CI listener |
| CA-16 | OIDC identity tokens | NOT-STARTED | `WorkerTokenIssuer` (worker auth only) | Optional; not built |
| CA-17 | Agent metadata socket | NOT-STARTED | none | Not built |
| CA-18 | Hooks in cloud | PARTIAL | `HookLocatorContext`, `CloudHooksSession`, `CloudHookFilter`, `CloudHooksWorkerTests` | Same as AC-104: the worker builds a cloud hook subset behind a consent claim the API does not mint yet; the worker image copies no `plugins/` folder (grep); no hook ran in the staged worker on Railway |
| CA-19 | Models in cloud | PARTIAL | `CreateCloudAgentRequest` (Model), `CloudCreateRequestValidator`, `CloudInputRulesTests` | Model id passed through; no curated list or context-size choice |
| CA-20 | Share agents | NOT-STARTED | `CloudAgentAccessTests` (owner scope only) | Owner-only access; no read-only share link |
| CA-21 | Team follow-ups setting | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md` | Team and admin controls deferred (OD-03, OQ-61) |
| CA-22 | Cloud subagents (/in-cloud, /autopilot) | PARTIAL | `CloudHandoff`, `CloudHandoffTests`, `SlashCommandCatalogue` | Same as AC-014: `/in-cloud` is wired and needs the API address and a sign-in; `/autopilot` is Unavailable |
| CA-23 | Move to Cloud and & handoff | PARTIAL | `CloudHandoff`, `CloudHandoffTests`, `ICloudHandoff`, `CloudHandoffModelTests`, `ShellSlashHandlerTests` | The leading `&` and `/in-cloud` open the same cost-confirmed dialog (AC-014); it needs the API address and a sign-in; fakes only |
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
| EN-01 | Ubuntu VMs | PARTIAL | deploy/worker.Containerfile, `WorkerEntrypointScriptTests` | Worker image built under podman (14 of 14 live checks, 2026-10-28) and, after the release, deployed as one staged private worker on Railway and handshaken (`docs/runbooks/evidence/railway-ompos-worker-2026-10-28.md`); no per-task VM is created; Railway has no per-task microVM (02-cursor-cloud-remote.md section K) |
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
| API-01 | Cloud Agents API v1 | PARTIAL | `CloudAgentEndpoints`, `CloudAgentLifecycleTests`, `CloudApiHost`, docs/api/openapi.json | OmpOS's own route shape (`/cloud/agents`), not Cursor's `/v1/agents`; mapped in `Program.cs` and off until `Cloud:Enabled` is true; the API is not deployed (a staged private worker is, 2026-10-28) |
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
| AU-01 | Automations (cloud agents on triggers) | PARTIAL | `AutomationEngine`, `AutomationEndpoints`, `AutomationsHost`, `AutomationsSettingsPage`, `AutomationsViewModelTests`, `AutomationsHostTests`, `LocalSessionAutomationTargetTests`, `LocalAutomationsManagerTests`, `AutomationsPageViewTests`, `AutomationCloudTargetTests`, `AutomationManagementTests`, `AutomationRunEndpointTests`, `HeadServicesWiringTests` | Desktop: Settings > Automations (cron only, webhooks refused) runs a local session with a first-run consent; API: routes off until `Automations:Enabled` is true, cloud target; the desktop cloud target applies the first repository's branch to every repository; `HeadAutomationSession(s)`, the real UI path, has no test; no run against a live cloud worker; no 'no repository' mode, template gallery or `/automate` skill |
| AU-02 | Scheduled trigger | MET | `ScheduledTrigger`, `AutomationSchedule`, `CronExpressionParseTests`, `CronExpressionOccurrenceTests`, `CronTimeZoneTests`, `AutomationSchedulerTests`, `AutomationsHostTests` (a saved definition fires on a fake clock and starts a session), `AutomationRunEndpointTests` | Cron expression with a time zone, never early, one run when at most 5 minutes late; the scheduler starts when the head opens; fires start a cloud agent (API) or a local session (desktop), neither run live |
| AU-03 | Source-control triggers | PARTIAL | `EventTrigger`, `AutomationWebhookReceiverTests`, `AutomationWebhookEndpointTests` | GitHub push and pull request (opened, synchronize, reopened, ready for review, merged) on the signed webhook receiver of the API (docs/reference/automations.md); no GitLab, Bitbucket, comment, label, CI or review events; never run against a live GitHub repository |
| AU-04 | Slack triggers | NOT-STARTED | none | Not built |
| AU-05 | Webhook trigger | MET | `WebhookTrigger`, `WebhookTriggerTests`, `AutomationWebhookReceiverTests`, `AutomationWebhookEndpointTests` | A signed endpoint per automation in the API (HMAC-SHA256, replay window, size cap), off until `Automations:Enabled` is true; the desktop refuses webhook triggers; the web page for it is not registered |
| AU-06 | Linear triggers | NOT-STARTED | none | Not built |
| AU-07 | Sentry triggers | NOT-STARTED | none | Not built |
| AU-08 | PagerDuty triggers | NOT-STARTED | none | Not built |
| AU-09 | Automation tools (PR, comment, Slack, memories) | PARTIAL | 04-omp-cursor-map.md row 5 (omp memory); `AutomationPromptBuilderTests`, `AutomationRunnerTests` | `allowedTools` names pull request create, comment and request reviewers and the target enforces it; no Slack tools, no MCP servers per automation, no Memories |
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

Source: `02-cursor-cloud-remote.md` section F (no primary Cursor page exists for remote development; the inventory marks most rows secondary). The OmpOS design is `ADR-0015-remote-environments.md`: omp runs on the remote host over `ssh host omp acp`. Recorded 2026-10-28: `RemoteSshLiveTests` ran once against a loopback sshd with a real omp 18.12.6 (`docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md`), and `PodmanLiveTests` ran against a real podman with a fake omp.
Counts (9 rows): MET 2, MET-LIVE 0, PARTIAL 3, DEFERRED 2, NOT-STARTED 1, OMITTED-BY-DECISION 1.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| RD-01 | Remote SSH, AI runs locally | PARTIAL | `SshAcpTransport`, `SshAcpTransportTests`, `RemoteLaunchMaterializer`, `RemoteSessionResources`, `RemotePreflight`, `RemoteTransportSelectionTests`, `RemoteSshLiveTests`, docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md, `ADR-0015-remote-environments.md`, `docs/runbooks/remote-ssh.md` | Design differs by decision (OQ-97): omp runs on the remote host over `ssh host omp acp`. Recorded 2026-10-28: a real omp 18.12.6 opened a session through a loopback sshd (preflight, handshake, `session/new`, `loadSession` advertised; no prompt turn, no real network, no Windows or macOS client). Not wired for ssh sessions: remote file explorer and editor, terminal pane, hook files and mention files. Stays PARTIAL for that reason |
| RD-02 | SSH keep-alive | MET | `SshCommandBuilder` (ServerAliveInterval, ServerAliveCountMax 3), `SshCommandBuilderTests`, `ReconnectingEngineHost`, `ReconnectPolicyTests`, `ReconnectingEngineHostTests`, `RemoteTransportSelectionTests` | The builder that produced the command of the live ssh run also sets the keep-alive options; reconnect after a dropped connection is covered by fake-ssh tests only, not against a real host |
| RD-03 | Agents Window remote SSH | MET | `NewSessionTargetModel`, `EnvironmentPickerView`, `RemoteEnvironmentsViewModel`, `PendingSessionTarget`, `NewSessionTargetModelTests`, `RemoteViewTests`, `ShellEnvironmentPickerTests`, `RemoteTransportSelectionTests` (an ssh session connects through the ssh transport and not the local factory), `SessionCatalogOverlayTests` | Desktop: the rail picker lists This computer and the saved environments, the chosen one starts the next session, the rail shows its remote status; the web head has none; limits of the ssh session are in RD-01 |
| RD-04 | Anysphere remote extensions | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md` | VS Code extension compatibility deferred (OQ-60); OmpOS replaces the extension with `Harness.Remote` |
| RD-05 | Dev Containers | PARTIAL | `ContainerSessionResources`, `WslSessionResources`, `PodmanExecCommandBuilder`, `RuntimeEnvironmentsView`, `ContainerSessionSelectionTests`, `RuntimeEnvironmentsViewTests`, `RemoteScriptsBusyBoxTests`, `PodmanLiveTests`; `docs/runbooks/remote-containers.md` | Podman containers and WSL distributions are environments in the Remote page and the New session picker (podman only). Recorded 2026-10-28: `PodmanLiveTests` (`OMPOS_LIVE_PODMAN=1`, alpine, real podman, fake omp, no model) passed in the Wave 4 tree and found two defects, fixed with `RemoteScriptsBusyBoxTests`. WSL is Windows-only and never ran; the dev-container attach button and the terminal pane of container and WSL sessions are not wired; container sessions get no desktop MCP servers and no hook files |
| RD-06 | Remote extension host | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md` | Needs an extension host (Phase 30) |
| RD-07 | Port forwarding and remote terminal | PARTIAL | `RemoteTerminalLauncher`, `RemoteTerminalLauncherTests`, `ReverseForwardAllocator`, `RemoteEnvironmentsHost` | Remote terminal command builders exist and `RemoteEnvironmentsHost` uses them, but the head's `TrustTerminalOpener` is unset, so no terminal pane opens (the page then tells the user to run ssh by hand, report NativeSessionWiring); `-R` forwards serve the hook and MCP bridges only; no user port forwarding |
| RD-08 | Tunnels | NOT-STARTED | none | No Cursor doc either (02-cursor-cloud-remote.md) |
| RD-09 | Extension control (allowlist, MDM) | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor admin portal and MDM |

## 17. Workspaces (WS rows)

Counts (13 rows): MET 0, MET-LIVE 0, PARTIAL 11, DEFERRED 0, NOT-STARTED 0, OMITTED-BY-DECISION 2.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| WS-01 | Multi-root workspace (.code-workspace) | PARTIAL | `CodeWorkspaceFile`, `CodeWorkspaceFileTests`, `WorkspaceSetStore`, `WorkspaceSetStoreTests`, `MultiRootWorkspaceTreeTests`, `WorkspacesSettingsPage`, `WorkspaceSetPickerViewTests` | Desktop: sets are saved and opened from the rail dialog, `/save-workspace`, `/load-workspace` and Settings > Workspaces; the explorer stays single-root (AC-009) |
| WS-02 | Multi-root in Agents Window | PARTIAL | `OmpLaunchPlanner` (`--add-dir`), `OmpLaunchPlannerTests`, `SessionEngineSet`, `WorktreeRunSetTests` | Launch carries extra roots; `omp acp --add-dir` ran without a model (docs/runbooks/evidence/multi-repo-omp-probe-2026-10-27/README.md); omp applies `--add-dir` per process, not per session over ACP (04-omp-cursor-map.md row 14); not probed in a model turn |
| WS-03 | Multi-workspace Agents Window | PARTIAL | `WorkspaceSetPicker`, `WorkspaceSetPickerTests` | See AC-009: the rail dialog opens a set; the explorer is single-root |
| WS-04 | Cursor 3 GA | OMITTED-BY-DECISION | 02-cursor-cloud-remote.md (dated fact, Fit '-') | Not a feature |
| WS-05 | Multi-repo cloud env | PARTIAL | See EN-06 | Same as EN-06 |
| WS-06 | Worktrees (Agents Window) | PARTIAL | See AC-131 | Same as AC-131 |
| WS-07 | worktrees.json (setup scripts) | PARTIAL | `CursorWorktreesConfig`, `CursorWorktreeSetup`, `ConsentedWorktreeSetupRunner`, `CursorWorktreesConfigTests`, `WorktreeSetupRunnerTests`, `WorktreeSetupConsentAdapter`, `WorktreeSetupConsentAdapterTests` | Parsed and run behind a consent gate (`ConsentedWorktreeSetupRunner` is built by `HeadServices`); reached only when a worktree is created (`/best-of-n`); the Windows PowerShell setup path never ran |
| WS-08 | Worktree cleanup and max count | PARTIAL | `WorktreeCapExceededException`, `WorktreeManagerTests` | Cap enforced in `WorktreeManager`; the Worktrees settings page is read-only: no cleanup interval and no editable key |
| WS-09 | /worktree, /apply-worktree, /delete-worktree | PARTIAL | See AC-132 | Same as AC-132 |
| WS-10 | /best-of-n | PARTIAL | `BestOfNState`, `BestOfNHost`, `BestOfNHostTests`, `WorktreeCommandsModelTests` | Per-index candidates of one model, not per-model; the head creates the candidates only in Run Everything (`BestOfNCandidateRunner` has no test of its own) |
| WS-11 | CLI worktrees flags | PARTIAL | omp `omp worktree` (04-omp-cursor-map.md row 13) | Provided by omp's CLI; OmpOS has no CLI flags |
| WS-12 | Subagents | PARTIAL | See AC-084 | Same as AC-084 |
| WS-13 | /multitask | OMITTED-BY-DECISION | 02-cursor-cloud-remote.md (secondary aggregator source); `SlashCommandCatalogue` (not one of the 92 tokens); OQ-95 | No primary documentation row; not in the Cursor 3.27 token list of file 03 |

## 18. CLI (CL rows)

Counts (21 rows): MET 1, MET-LIVE 0, PARTIAL 15, DEFERRED 1, NOT-STARTED 2, OMITTED-BY-DECISION 2.

| Row | Feature | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| CL-01 | CLI modes (agent, plan, ask, debug) | PARTIAL | `SlashCommandCatalogue` (`/plan` Built; `/ask` and `/debug` Partial) | Plan only; omp advertises no ask or debug mode |
| CL-02 | Print and headless | PARTIAL | 04-omp-cursor-map.md section B5 (omp print and RPC modes) | omp provides it; OmpOS adds no CLI |
| CL-03 | Force and yolo | PARTIAL | `AgentsPage` (auto-run switch), omp `--yolo` (04-omp-cursor-map.md row 12) | Run Everything on the Run mode page maps to omp `--approval-mode yolo` (`RunModeLaunchTests`); the terminal auto-run switch stays; OmpOS adds no CLI flag |
| CL-04 | Sessions (ls, resume, fork, rewind) | PARTIAL | `SessionCatalogModel`, `ReloadableSessionCatalog`, `ForkModel` | Rail lists and resumes chats; `/fork`, `/rewind` and `/restore` are wired (AC-017, AC-018); `/resume` has no command form |
| CL-05 | CLI sandbox | DEFERRED | 04-omp-cursor-map.md row 12 | omp has no sandbox; only the read boundary of `sandbox.json` binds tool calls (S05, S15) |
| CL-06 | CLI permissions (cli.json tokens) | PARTIAL | `PermissionToken`, `PermissionTokenParserTests`, `ShellMatchingTests`, `PermissionsFileReader` | Parses Shell, Read, Write, WebFetch and Mcp tokens in OmpOS's permissions.json and enforces them for local desktop sessions (AC-124); does not read cli.json; omp's request field names are UNVERIFIED |
| CL-07 | CLI config (cli-config.json) | OMITTED-BY-DECISION | `docs/settings-parity.md` row 34 | Cursor's CLI configuration is not part of OmpOS Settings; omp's own config is edited through the Models page |
| CL-08 | Global CLI options | PARTIAL | `OmpTerminalCommand`, `OmpTerminalCommandTests` | omp's options; OmpOS builds the terminal command only |
| CL-09 | CLI commands (login, status, models, mcp, worker...) | PARTIAL | `ModelsSettingsSource` (uses `omp models`, `omp token`) | omp's commands; OmpOS wraps a few |
| CL-10 | MCP in CLI | PARTIAL | See AC-110 | Same as AC-110 |
| CL-11 | Rules in CLI | PARTIAL | 04-omp-cursor-map.md row 4 | omp loads `.cursor/rules` and AGENTS.md itself |
| CL-12 | ACP server | PARTIAL | `AcpAgentSession`, `AcpWireMapping`, `OmpOS.Harness.Acp.Tests`, `LiveOmpHandshakeTests` | OmpOS is an ACP client of omp (omp acp), not an ACP server; handshake recorded live without a model turn; Cursor extensions (ask_question, create_plan) not handled |
| CL-13 | CLI slash commands | PARTIAL | `SlashCommandCatalogue` | Per-token statuses in section 21 |
| CL-14 | Shell mode | PARTIAL | `TerminalPanelView`, `SlashCommandCatalogue` (`/shell` Partial) | Opens a terminal; the text after /shell is not run |
| CL-15 | Custom modes via skills | MET | See AC-006 | Same as AC-006 |
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
| AD-15 | MCP allowlist and per-server controls | PARTIAL | `McpServerCatalog`, `McpConsentIdentity`, `PermissionsFileReader`, `McpServerCatalogTests` | Per-user consent (`McpConsentIdentity`) and the permissions.json allowlist are wired for local desktop sessions (AC-114); no admin allowlist |
| AD-16 | Git repo blocklist | DEFERRED | `docs/runbooks/phase-30-compatibility-admin.md` | Admin feature |
| AD-17 | Protected Git scopes | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor org binding |
| AD-18 | Privacy mode and enforcement | DEFERRED | `docs/settings-parity.md` row 19 | Statement only; no admin enforcement |
| AD-19 | Data residency | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | Cursor Enterprise |
| AD-20 | Network and proxy | PARTIAL | `NetworkSettingsPage`, `NetworkDiagnosticsSource`, `NetworkSettingsPageTests` | Diagnostics only; HTTP compatibility switch and proxy settings not built (settings-parity open points) |
| AD-21 | Service accounts | NOT-STARTED | `JwtBearerSetup`, `CloudWorkerGuard` (worker tokens only) | The OmpOS API authenticates with JWT bearer tokens from a sign-in authority that is not configured (OD-03); no service-account or API-key concept |
| AD-22 | Pooled usage and billing groups | OMITTED-BY-DECISION | Fit omit-no-accounts, 02-cursor-cloud-remote.md | No billing |
| AD-23 | Auto Run and Run Modes admin | PARTIAL | `RunModePolicy`, `RunModePolicyTests`, `AgentsPage` | Per-user only: the Run mode page and the policy are wired for local desktop sessions (AC-123); no admin control |
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

Source: `03-cursor-settings-slash.md` table 1. Statuses are those of the S01 to S50 table of `docs/settings-parity.md` (`SettingsParityTableTests` asserts every id once, a real `SettingsSection` for each Built, Wired and Partial row that names a page, and a page and key for each Built and Wired row): Built and Wired are MET, Partial is PARTIAL, Omitted is OMITTED-BY-DECISION. The desktop Settings window holds 22 pages in `SettingsWindowFactory`: Runtime, General, Display, Agents, Run mode, Models, Tab, Rules, Skills, Commands, Hooks, MCP, Plugins, Workspaces, Worktrees, Ignore, Remote, Cloud, Automations (only when the head supplies the manager), Privacy, Network and Keyboard; the Xvfb smoke of 2026-10-28 rendered 21 of them (`docs/runbooks/evidence/desktop-app-2026-10-28/`). The web Settings has 18 pages (`WebSettingsOrder`, `WebSettingsPagesTests`, `SettingsRouteSurfaceTests`): Models is real over `GET /providers` (`DemoProvidersRouteTests`), Hooks and MCP are read-only lists, and Workspaces, Remote, Cloud, Agents and Run mode are information pages with a desktop notice. Rows that restate a repository row: S06, S13 to S15 and S17, S18, S36 to S41, S45, S47, S48.

Counts (50 rows): MET 8, MET-LIVE 0, PARTIAL 20, DEFERRED 0, NOT-STARTED 0, OMITTED-BY-DECISION 22.

| Row | Setting | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| S01 | cli-config.json version | OMITTED-BY-DECISION | `docs/settings-parity.md` S01 | OmpOS does not read or write cli-config.json; its own `config/settings.json` carries `schemaVersion` |
| S02 | editor.vimMode | PARTIAL | `DisplaySettings`, `FileDisplaySettingsStore`, `DisplaySettingsPage`, `DisplayPageView`, `DisplaySettingsPageTests`, `DisplaySettingsTests` | The Display page stores `display.vimMode`; the catalogue lists `/vim` Unavailable and no prompt box applies Vim keys |
| S03 | permissions.allow and deny tokens | PARTIAL | `PermissionTokenParser`, `PermissionsFileEditor`, `EffectivePermissions`, `PermissionTokenParserTests`, `ShellMatchingTests`, `PermissionsFileTests`, `RunModePageViewTests` | The Run mode page edits the terminal and MCP allowlists and shows the tokens read from the files; deny rules are read, not edited; enforced for local desktop sessions (AC-124) |
| S04 | approvalMode | MET | `RunModeSettingsPage`, `RunModePageView`, `RunModePageViewTests`, `RunModeLaunchTests`, `RunModePolicyHolderTests` | The page stores the run mode in `~/.ompos/run-mode.json` and the launch plan maps it to omp `--approval-mode`; a change reaches a running omp only when its engine is composed again; the ssh, container and WSL case is UNVERIFIED (AC-123) |
| S05 | sandbox.mode, networkAccess, readBoundary | PARTIAL | `SandboxFileReader`, `EffectiveSandbox`, `SandboxFileTests`, `SandboxBoundaryTests`, `RunModePermissionResolverTests` | Only the read boundary exists and it binds the tool calls omp asks OmpOS about; no OS sandbox and no sandbox network mode |
| S06 | network.useHttp1ForAgent | OMITTED-BY-DECISION | `docs/settings-parity.md` S06 | omp makes the model calls; OmpOS has no agent traffic of its own to move to HTTP/1.1 |
| S07 | attribution flags | OMITTED-BY-DECISION | `docs/settings-parity.md` S07 | OmpOS creates no commits or pull requests itself, so a toggle would control nothing |
| S08 | notifications, hints, suggestNextPrompt, rewind | PARTIAL | `CheckpointSlashHandler`, `DisplaySettings`, `CheckpointsModelTests`, `DisplaySettingsTests` | `display.rewind` is a switch; notifications, hints and next-prompt suggestions are not built |
| S09 | display.* toggles | PARTIAL | `DisplaySettings`, `DisplaySettingsValues`, `DisplayToggleSlashHandler`, `DisplayPageView`, `DisplaySettingsTests` | Line numbers and status indicators are applied by the views (`/line-numbers` Built, `/status-indicators` Partial); thinking blocks and running time are stored flags only |
| S10 | model, maxMode, channel, statusLine | PARTIAL | `ModelsSettingsSource` (default model is omp's `modelRoles.default`), `ModelPickerModel` | The default model is set through omp; Max Mode, update channel and a status line command are not built |
| S11 | Model slash commands toggle | OMITTED-BY-DECISION | `docs/settings-parity.md` S11; `SlashCommandCatalogue` (`/opus`, `/composer` Partial) | There is no /opus or /composer family to switch off |
| S12 | CLI proxy environment variables | MET | `NetworkSettingsPage`, `NetworkSettingsPageTests`, `NetworkAndKeyboardCopyTests` | The Network page states which proxy settings a diagnostics run uses and that omp keeps its own |
| S13 | Auto-review classifier model | OMITTED-BY-DECISION | `docs/settings-parity.md` S13; Fit N in 03-cursor-settings-slash.md | The classifier is Cursor's hosted model; OmpOS Auto-review is the allowlist, a known-safe read-only list, then ask |
| S14 | permissions.json autoRun instructions | PARTIAL | `PermissionsFileDocument`, `PermissionsFileReader`, `PermissionsFileTests`, `RunModePageViewTests` | The instructions are shown on the Run mode page and not passed to an agent: no classifier consumes them |
| S15 | sandbox.json read boundary | PARTIAL | `SandboxFileReader`, `SandboxFileTests`, `RunModePageViewTests` | Edited on the Run mode page, with the file that controls the value; enforcement as S05 |
| S16 | Team Read Controls | OMITTED-BY-DECISION | `docs/settings-parity.md` S16; Fit N in 03-cursor-settings-slash.md | Team admin dashboard; OmpOS has no teams |
| S17 | Run Modes names and history | MET | `RunMode` (AutoReview, Allowlist, RunEverything), `RunModeSettingsPage`, `RunModePolicyTests`, `RunModePageViewTests` | Auto-review, Allowlist and Run Everything on the Run mode page (rendered in the 2026-10-28 desktop smoke); Auto-review has no hosted classifier (S13) |
| S18 | Remote Control and keep awake | OMITTED-BY-DECISION | `docs/settings-parity.md` S18 | Needs a Cursor account and relay; the OmpOS equivalents are the Remote (ssh) and Cloud pages |
| S19 | Agent Review setting | OMITTED-BY-DECISION | `docs/settings-parity.md` S19; `SlashCommandCatalogue` (`/agent-review` Built as a skill) | No Git and PRs page; review runs as the built-in `/agent-review` and `/review` skills (AC-150, AC-155) |
| S20 | Annotate and Batch edits | OMITTED-BY-DECISION | `docs/settings-parity.md` S20 | No browser Design Mode; it has no settings (AC-007) |
| S21 | Auto Optimize For (Router) | OMITTED-BY-DECISION | `docs/settings-parity.md` S21; Fit N in 03-cursor-settings-slash.md | Cursor inference |
| S22 | Router admin controls | OMITTED-BY-DECISION | `docs/settings-parity.md` S22; Fit N in 03-cursor-settings-slash.md | Team dashboard |
| S23 | Custom Modes (Use as Mode) | PARTIAL | `CustomModeModel`, `CustomModeChipView`, `CustomModeModelTests`, `SkillsSettingsPage` | The Skills page lists skills; a mode is pinned from the chip of a session (AC-006), not set on the page (settings-parity S23 says the slash palette: drift) |
| S24 | SKILL.md frontmatter fields | PARTIAL | `ConventionFrontmatterParser`, `ConventionFrontmatterParserTests`, `ConventionSkill` | Skills are listed; frontmatter fields are not shown or edited; omp decides which fields it honours |
| S25 | Skill discovery directories | PARTIAL | `ConventionDirectories`, `LocalConventionFileSourceTests`, `SkillsSettingsPage` | The slash palette reads the six convention folders at two scopes; the Skills page lists what discovery finds, built-in skills included |
| S26 | Rules frontmatter and matrix | PARTIAL | `ConventionRuleKind`, `ConventionCatalogTests`, `RulesSettingsPage` | Rules are listed read-only; `ConventionRuleKind` classifies .mdc, Markdown, AGENTS.md, CLAUDE.md and cursorrules; the matrix is not shown and there is no editor |
| S27 | Subagent frontmatter | OMITTED-BY-DECISION | `docs/settings-parity.md` S27; `ConventionSubagent`, `ConventionCatalogTests` | Delegation belongs to omp and the Swarm plugin; there is no Subagents page, although discovery lists subagent files as /name (AC-081) |
| S28 | hooks.json entry fields | MET | `HooksConfigParser`, `HooksConfigParserTests`, `HooksSettingsPage`, `HooksSettingsPageTests`, `HooksPageView`, `HooksPageViewTests`, `HooksService` | Hooks, their approval, enable switch and run log on the Hooks page (rendered in the 2026-10-28 desktop smoke); the switch lives in OmpOS state, never in a hooks file; no hook ran against a live omp (AC-100) |
| S29 | hooks.json events (21) | PARTIAL | `HookEvents`, `HookEventsTests`, `CloudHookFilter` | Event coverage follows the Producer column of `docs/reference/hooks.md` (AC-100); the cloud subset is built in the worker and not minted by the API (AC-104) |
| S30 | mcp.json fields and interpolation | PARTIAL | `McpConfigLoader`, `McpConfigFormatTests`, `McpVariableExpansionTests`, `McpPageView`, `McpPageViewTests`, `McpPresetPageViewTests` | Servers, consent, 13 presets and templates on the MCP page (rendered in the 2026-10-28 desktop smoke); OAuth blocks are not passed and there is no connection checker; the web page is a read-only list |
| S31 | Customize per-scope activation | PARTIAL | `CustomizeCatalogPage` | Read-only lists; no activation toggle and no team scope |
| S32 | Menu bar Show Count | OMITTED-BY-DECISION | `docs/settings-parity.md` S32 | No menu bar icon |
| S33 | Reduce Motion | MET | `GeneralPage`, `MotionPreferenceResolverTests`, `ReducedMotionTests`, `GeneralPageTests` | OmpOS has Motion follow system, reduce or full |
| S34 | Chat Width | OMITTED-BY-DECISION | `docs/settings-parity.md` S34 | The transcript column width is fixed by layout tokens |
| S35 | New-agent default environment | PARTIAL | `EnvironmentProfileStore`, `NewSessionTargetModel`, `NewSessionTargetModelTests`, `ShellEnvironmentPickerTests` | The environment picker lists the saved ssh and container environments and records the last used; there is no default-environment setting |
| S36 | Queue versus steer | PARTIAL | `RunInputScheduler`, `SteeringInputModel`, `ShortcutCatalogueTests`, `QueuedMessagesModelTests` | Behaviour is built (Tab queues, Ctrl+Enter steers, Ctrl+Shift+Enter follows up; see AC-015, AC-016); there is no setting for the default |
| S37 | Conversation search | PARTIAL | `TranscriptSearchModel`, `TranscriptSearchScopeTests`, `ConversationSearchViewTests` | The search model and the Ctrl+K overlay are built (AC-020); it is not a setting |
| S38 | Context usage tray | PARTIAL | `ContextRingModel`, `ContextRingModelTests` | The context ring is built (AC-021); it is not a setting |
| S39 | BYOK rules | MET | `ModelsSettingsSource`, `ProviderCatalogue` (Azure OpenAI and AWS Bedrock rows in docs/reference/providers.md), `ModelsApiKeyTests` | Write-only key fields, the provider listing, the plan-rule tick box and the operator's key-entry switch; row 1 of `docs/settings-parity.md` was MET-LIVE in the previous audit; no real-credential call (OQ-71) |
| S40 | Privacy Mode (Legacy) blocks cloud features | MET | `PrivacySettingsPage`, `PrivacyPageViewTests`, `GeneralPage`, `SettingsCopy` | Read-only Privacy page: OmpOS sends nothing itself, only the selected provider receives code context, and there is no Privacy Mode to switch (see CA-29) |
| S41 | Never open PRs as drafts | OMITTED-BY-DECISION | `docs/settings-parity.md` S41; `CreateCloudPullRequestRequest` (Draft), `CloudOptions`, `CloudPullRequestTests` | The Cloud page opens pull requests as drafts and has no switch; a server option `Cloud:PullRequests:NeverDraft` exists in the API |
| S42 | Cloud environment lifecycle | OMITTED-BY-DECISION | `docs/settings-parity.md` S42 | The API has no environment endpoints; the Cloud page hides the lists |
| S43 | Self-hosted worker settings | OMITTED-BY-DECISION | `docs/settings-parity.md` S43 | Cursor worker settings; OmpOS uses ssh environments and cloud agents (CA-32) |
| S44 | Bugbot team settings | OMITTED-BY-DECISION | `docs/settings-parity.md` S44 | Bugbot is not part of OmpOS |
| S45 | Tab settings | PARTIAL | `TabPage`, `TabSettingsSource`, `TabPageTests` | Read-only status; snooze, disable and remap are not built |
| S46 | Terminal tool environment (CURSOR_AGENT, UID mapping) | OMITTED-BY-DECISION | `docs/settings-parity.md` S46 | OmpOS sets no such variable (grep of `src`, `apps`) |
| S47 | Indexing and Docs | OMITTED-BY-DECISION | `docs/settings-parity.md` S47 | Nothing to build |
| S48 | Keyboard shortcut table (no Cmd+K inline edit) | MET | `ShortcutCatalogue`, `ShortcutCatalogueTests`, `KeyboardSettingsPage` | The Keyboard page lists only gestures the shell handles; there is no inline-edit shortcut |
| S49 | Grok Bot app settings | OMITTED-BY-DECISION | `docs/settings-parity.md` S49 | A separate Cursor product |
| S50 | CLI updates | OMITTED-BY-DECISION | `docs/settings-parity.md` S50 | Not applicable: omp updates itself |

## 21. Slash tokens (92)

Source: `03-cursor-settings-slash.md` table 2 and the shipped catalogue `SlashCommandCatalogue` in `src/OmpOS.Presentation/Commands` (registry `SlashCommandRegistry`; reference `docs/reference/slash-commands.md`, which a test keeps equal to the catalogue). Each token is listed so typing it never silently does nothing: a token OmpOS cannot run is shown disabled with its reason. The catalogue holds 93 rows: the 92 Cursor tokens counted here and the OmpOS addition `/update-settings` (Built, a built-in skill), which is not counted. Status words follow the catalogue: Built and Delegated are MET (Delegated only for the forwarding: the effect is omp's and was not observed live), Partial is PARTIAL, and an Unavailable token is DEFERRED, NOT-STARTED or OMITTED-BY-DECISION by its reason. The desktop head runs the catalogue through `DesktopSlashActionHost`, which offers a token first to the handler classes (`SlashActionHandlerHost`: checkpoints, fork, side chat, worktrees, copy, about, quit, feedback, open, changes, workspace sets, display toggles) and then to the shell callbacks (`settings.open`, `chat.new`, `terminal.new`, `mode.set`, `model.pick`, `help.show`, `cloud.handoff`); `DesktopSlashHandlerRoutingTests` and `ShellSlashHandlerTests` cover the routing, and the desktop smoke of 2026-10-28 shows the palette listing 134 commands (the catalogue rows plus the commands omp advertises). The web head implements the same interface as `WebSlashActionHost` for five of those actions (`WebSourcesTests`) and has none of the handlers. The open-ended forms (`/skill-name`, `/custom-command`, `/subagent-name`, the `&` prefix) are not counted: custom commands and skills are AC-090 and AC-070.

Counts (92 rows): MET 44, MET-LIVE 0, PARTIAL 24, DEFERRED 4, NOT-STARTED 6, OMITTED-BY-DECISION 14.

| Row | Token | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| SL-001 | /model | MET | `SlashCommandCatalogue` (NativeAction model.pick), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | None found |
| SL-002 | /run-everything | PARTIAL | `SlashCommandCatalogue` (NativeAction settings.open run-mode, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Opens Settings on the Run mode page, which holds the run-everything choice; the words on, off and status are not read |
| SL-003 | /plan | MET | `SlashCommandCatalogue` (NativeAction mode.set plan), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | Effect not observed with a live model turn |
| SL-004 | /ask | PARTIAL | `SlashCommandCatalogue` (NativeAction mode.set ask, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Works only when the engine advertises an ask mode; omp 18.12.6 advertises default and plan. |
| SL-005 | /debug | PARTIAL | `SlashCommandCatalogue` (NativeAction mode.set debug, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Works only when the engine advertises a debug mode; omp 18.12.6 advertises default and plan. |
| SL-006 | /goal | DEFERRED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | omp runs /goal in its terminal UI and RPC only (04-omp-cursor-map.md row 31) |
| SL-007 | /logs | NOT-STARTED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | OmpOS writes its diagnostics to the standard error stream and keeps no log file or folder to open; `LogsSlashHandler` exists and is composed by no head |
| SL-008 | /update | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Updates Cursor's own CLI |
| SL-009 | /max-mode | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Max Mode is a Cursor plan feature (Fit omit-no-accounts) |
| SL-010 | /rename | MET | `SlashCommandCatalogue` (OmpDelegated /rename), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-011 | /clear | MET | `SlashCommandCatalogue` (NativeAction chat.new), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | None found |
| SL-012 | /resume | NOT-STARTED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | The rail resumes chats; no command form exists |
| SL-013 | /fork | MET | `ForkSlashHandler`, `ModelSlashHandlerTests`, `ForkModelTests`, `AcpForkTests`, `AgentSessionRuntimeWiringTests`; `SlashCommandCatalogue` (NativeAction chat.fork), `SlashCommandCatalogueTests` | Forks the whole conversation into a new session over ACP `session/fork`; `/fork <n>` from an earlier message is refused; `SessionForker` has no test of its own and `session/fork` never ran against a live omp (AC-018) |
| SL-014 | /summarize | MET | `SlashCommandCatalogue` (OmpDelegated /compact), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-015 | /rewind | PARTIAL | `CheckpointSlashHandler`, `ModelSlashHandlerTests`, `CheckpointsModelTests`, `DesktopSlashHandlerRoutingTests`; `SlashCommandCatalogue` (NativeAction chat.rewind, Partial), `SlashCommandCatalogueTests` | Opens the session's Checkpoints timeline, or with a turn number previews the files and asks before restoring them; files only, never the conversation, and only for turns OmpOS recorded (AC-017) |
| SL-016 | /vim | NOT-STARTED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | The OmpOS composer has no Vim key bindings, so the stored `display.vimMode` flag would change nothing (S02) |
| SL-017 | /line-numbers | MET | `DisplayToggleSlashHandler`, `DisplaySettingsTests`, `DesktopSlashHandlerRoutingTests`; `SlashCommandCatalogue` (NativeAction display.lineNumbers), `SlashCommandCatalogueTests` | Shows or hides the line-number gutter of code blocks and saves the choice |
| SL-018 | /show-thinking | NOT-STARTED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | OmpOS does not draw the model's thinking in the transcript, so there is nothing to show or hide (S09) |
| SL-019 | /status-indicators | PARTIAL | `DisplayToggleSlashHandler`, `DisplaySettingsTests`, `DesktopSlashHandlerRoutingTests`; `SlashCommandCatalogue` (NativeAction display.statusIndicators, Partial), `SlashCommandCatalogueTests` | Hides the status glyphs of the session rows and thread header (their words stay for screen readers); Cursor's setting is the terminal title, which OmpOS does not have |
| SL-020 | /shell | PARTIAL | `SlashCommandCatalogue` (NativeAction terminal.new, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests` | Opens a new terminal session; the text after the command is not run. |
| SL-021 | /about | MET | `AboutSlashHandler`, `HostSeamSlashHandlerTests`, `DesktopSlashHandlerRoutingTests`; `SlashCommandCatalogue` (NativeAction app.about), `SlashCommandCatalogueTests` | Opens the About window: version, revision, licence and source |
| SL-022 | /setup-terminal | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Configures the Cursor CLI's newline keys |
| SL-023 | /help | MET | `SlashCommandCatalogue` (NativeAction help.show), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | None found |
| SL-024 | /feedback | PARTIAL | `FeedbackSlashHandler`, `HostSeamSlashHandlerTests`, `ShellSlashHandlerTests`; `SlashCommandCatalogue` (NativeAction app.feedback, Partial), `SlashCommandCatalogueTests` | Opens the OmpOS issue form in the browser with the typed title; nothing is sent from the app |
| SL-025 | /open | PARTIAL | `OpenFileSlashHandler`, `HostSeamSlashHandlerTests`, `DesktopSlashHandlerRoutingTests`; `SlashCommandCatalogue` (NativeAction editor.open, Partial), `SlashCommandCatalogueTests` | Opens a workspace file in the OmpOS editor at the line after the colon; it does not open Cursor |
| SL-026 | /copy-request-id | PARTIAL | `CopySlashHandler`, `HostSeamSlashHandlerTests`, `DesktopSlashHandlerRoutingTests`; `SlashCommandCatalogue` (NativeAction chat.copyRequestId, Partial), `SlashCommandCatalogueTests` | Copies the id of the last turn's request when the engine session reports one; says so when it has none |
| SL-027 | /copy-conversation-id | MET | `CopySlashHandler`, `HostSeamSlashHandlerTests`, `DesktopSlashHandlerRoutingTests`; `SlashCommandCatalogue` (NativeAction chat.copyConversationId), `SlashCommandCatalogueTests` | Copies the omp session id to the clipboard |
| SL-028 | /logout | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | No Cursor account to sign out of |
| SL-029 | /quit | MET | `QuitSlashHandler`, `HostSeamSlashHandlerTests`, `DesktopSlashHandlerRoutingTests`; `SlashCommandCatalogue` (NativeAction app.quit), `SlashCommandCatalogueTests` | Closes the OmpOS window after the sessions have stopped |
| SL-030 | /exit | MET | `QuitSlashHandler`, `HostSeamSlashHandlerTests`, `DesktopSlashHandlerRoutingTests`; `SlashCommandCatalogue` (NativeAction app.quit), `SlashCommandCatalogueTests` | Closes the OmpOS window after the sessions have stopped |
| SL-031 | /mcp | MET | `SlashCommandCatalogue` (OmpDelegated /mcp), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-032 | /plugin | PARTIAL | `SlashCommandCatalogue` (OmpDelegated /plugin (omp /plugins), Partial), `SlashCommandCatalogueTests`, `RuntimeSlashDispatchTests` | Sent to omp as typed; omp's /plugin is an alias of /plugins (list, enable, disable) |
| SL-033 | /config | MET | `SlashCommandCatalogue` (NativeAction settings.open), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | None found |
| SL-034 | /copy | MET | `CopySlashHandler`, `HostSeamSlashHandlerTests`, `DesktopSlashHandlerRoutingTests`; `SlashCommandCatalogue` (NativeAction chat.copy), `SlashCommandCatalogueTests` | Copies from the transcript through the clipboard writer of the shell |
| SL-035 | /sandbox | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | Not built: no sandbox mode switch in OmpOS Settings; sandbox limits are omp's (`docs/settings-parity.md` rows 4 and 26) and only the `sandbox.json` read boundary exists (S05) |
| SL-036 | /bedrock | PARTIAL | `SlashCommandCatalogue` (NativeAction settings.open models, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Opens Settings, whose Models page sets up providers; Bedrock subcommands are not read |
| SL-037 | /auto-run | PARTIAL | `SlashCommandCatalogue` (NativeAction settings.open run-mode, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Opens Settings on the Run mode page; the words on, off and status are not read |
| SL-038 | /new | MET | `SlashCommandCatalogue` (NativeAction chat.new), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | None found |
| SL-039 | /new-chat | MET | `SlashCommandCatalogue` (NativeAction chat.new), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | None found |
| SL-040 | /newchat | MET | `SlashCommandCatalogue` (NativeAction chat.new), `DesktopSlashActionHostTests`, `SlashPaletteEndToEndTests`, `SlashCommandCatalogueTests`, web `WebSlashActionHost` | None found |
| SL-041 | /compress | MET | `SlashCommandCatalogue` (OmpDelegated /compact), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-042 | /sh | PARTIAL | `SlashCommandCatalogue` (NativeAction terminal.new, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests` | Opens a new terminal session; the text after the command is not run. |
| SL-043 | /run | PARTIAL | `SlashCommandCatalogue` (NativeAction terminal.new, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests` | Opens a new terminal session; the text after the command is not run. |
| SL-044 | /cursor | PARTIAL | `OpenFileSlashHandler`, `HostSeamSlashHandlerTests`, `DesktopSlashHandlerRoutingTests`; `SlashCommandCatalogue` (NativeAction editor.open, Partial), `SlashCommandCatalogueTests` | Alias of `/open`: opens a file of the workspace in the OmpOS editor, not in Cursor |
| SL-045 | /side | MET | `SideChatSlashHandler`, `ModelSlashHandlerTests`, `SideChatHubTests`, `DesktopSlashHandlerRoutingTests`; `SlashCommandCatalogue` (NativeAction chats.side.open), `SlashCommandCatalogueTests` | Opens a side chat answered by a second omp that gets a summary of the conversation as hidden context (AC-019); `OmpSideChatEngine` has no test of its own and no live omp answered |
| SL-046 | /btw | PARTIAL | `SlashCommandCatalogue` (NativeAction chats.side.open, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests` | The IDE form: opens a side chat as /side does |
| SL-047 | /in-cloud | PARTIAL | `CloudHandoffModel`, `CloudHandoffModelTests`, `ShellSlashHandlerTests`; `SlashCommandCatalogue` (CloudHandoff cloud.handoff, Partial), `SlashCommandCatalogueTests` | Asks for the repository and your confirmation of the cloud cost, then hands off through `ApiCloudAgentsHost`; needs `OMPOS_API_BASE_URL` and a sign-in, else it says what is missing; fakes only, the API was never deployed on Railway (a staged private worker was, 2026-10-28) |
| SL-048 | /automate | NOT-STARTED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | Creates Cursor Automations, a Cursor cloud service; OmpOS automations are made on the Settings > Automations page and through the API (AU-01), and no `/automate` skill is built |
| SL-049 | /autopilot | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | Cursor's autopilot skill watches a pull request from Cursor's cloud backend; OmpOS hands a one-off task to a cloud agent with `/in-cloud` and has no pull request watcher |
| SL-050 | /canvas | NOT-STARTED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | No canvas tool or pane (AC-129) |
| SL-051 | /create-hook | MET | `SlashCommandCatalogue` (Skill SKILL.md named create-hook), `assets/skills/create-hook/SKILL.md`, `BuiltinSkillsDesktopTests`, `BuiltinSkillSourceTests`, `SlashCommandCatalogueTests` | Ships as a built-in skill found by the desktop palette and Skills page; a project or user skill of the same name takes over; API-hosted and remote sessions carry no built-ins; the result was not observed with a live model (OD-07) |
| SL-052 | /create-rule | MET | `SlashCommandCatalogue` (Skill SKILL.md named create-rule), `assets/skills/create-rule/SKILL.md`, `BuiltinSkillsDesktopTests`, `BuiltinSkillSourceTests`, `SlashCommandCatalogueTests` | Ships as a built-in skill found by the desktop palette and Skills page; a project or user skill of the same name takes over; API-hosted and remote sessions carry no built-ins; the result was not observed with a live model (OD-07) |
| SL-053 | /create-skill | MET | `SlashCommandCatalogue` (Skill SKILL.md named create-skill), `assets/skills/create-skill/SKILL.md`, `BuiltinSkillsDesktopTests`, `BuiltinSkillSourceTests`, `SlashCommandCatalogueTests` | Ships as a built-in skill found by the desktop palette and Skills page; a project or user skill of the same name takes over; API-hosted and remote sessions carry no built-ins; the result was not observed with a live model (OD-07) |
| SL-054 | /create-subagent | MET | `SlashCommandCatalogue` (Skill SKILL.md named create-subagent), `assets/skills/create-subagent/SKILL.md`, `BuiltinSkillsDesktopTests`, `BuiltinSkillSourceTests`, `SlashCommandCatalogueTests` | Ships as a built-in skill found by the desktop palette and Skills page; a project or user skill of the same name takes over; API-hosted and remote sessions carry no built-ins; the result was not observed with a live model (OD-07) |
| SL-055 | /cursor-blame | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Needs Cursor's record of AI-authored changes |
| SL-056 | /loop | DEFERRED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | omp runs /loop in its terminal UI only |
| SL-057 | /migrate-to-skills | MET | `SlashCommandCatalogue` (Skill SKILL.md named migrate-to-skills), `assets/skills/migrate-to-skills/SKILL.md`, `BuiltinSkillsDesktopTests`, `BuiltinSkillSourceTests`, `SlashCommandCatalogueTests` | Ships as a built-in skill found by the desktop palette and Skills page; a project or user skill of the same name takes over; API-hosted and remote sessions carry no built-ins; the result was not observed with a live model (OD-07) |
| SL-058 | /review | MET | `SlashCommandCatalogue` (Skill SKILL.md named review), `assets/skills/review/SKILL.md`, `BuiltinSkillsDesktopTests`, `BuiltinSkillSourceTests`, `SlashCommandCatalogueTests` | Ships as a built-in skill found by the desktop palette and Skills page; a project or user skill of the same name takes over; API-hosted and remote sessions carry no built-ins; the result was not observed with a live model (OD-07) |
| SL-059 | /review-bugbot | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Bugbot is Cursor-hosted (AC-151) |
| SL-060 | /review-security | MET | `SlashCommandCatalogue` (Skill SKILL.md named review-security), `assets/skills/review-security/SKILL.md`, `BuiltinSkillsDesktopTests`, `BuiltinSkillSourceTests`, `SlashCommandCatalogueTests` | Ships as a built-in skill found by the desktop palette and Skills page; a project or user skill of the same name takes over; API-hosted and remote sessions carry no built-ins; the result was not observed with a live model (OD-07) |
| SL-061 | /sdk | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Cursor SDK helper |
| SL-062 | /split-to-prs | MET | `SlashCommandCatalogue` (Skill SKILL.md named split-to-prs), `assets/skills/split-to-prs/SKILL.md`, `BuiltinSkillsDesktopTests`, `BuiltinSkillSourceTests`, `SlashCommandCatalogueTests` | Ships as a built-in skill found by the desktop palette and Skills page; a project or user skill of the same name takes over; API-hosted and remote sessions carry no built-ins; the result was not observed with a live model (OD-07) |
| SL-063 | /statusline | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Cursor CLI status line |
| SL-064 | /update-cli-config | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Edits the Cursor CLI's own file |
| SL-065 | /update-cursor-settings | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Edits Cursor and VS Code settings; /config opens OmpOS Settings |
| SL-066 | /add-dir | MET | `SlashCommandCatalogue` (OmpDelegated /add-dir), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-067 | /auto-review | DEFERRED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | Cursor documents the name only; behaviour unknown |
| SL-068 | /changes | PARTIAL | `ChangesSlashHandler`, `HostSeamSlashHandlerTests`, `DesktopSlashHandlerRoutingTests`; `SlashCommandCatalogue` (NativeAction changes.open, Partial), `SlashCommandCatalogueTests` | Shows the Changes bar with this session's edited files; there is no unified review window with a Session tab |
| SL-069 | /commands | MET | `SlashCommandCatalogue` (NativeAction settings.open commands), `SlashCommandCatalogueTests` | Opens Settings on the Commands page |
| SL-070 | /context | MET | `SlashCommandCatalogue` (OmpDelegated /context), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-071 | /detach | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Cursor CLI persistent sessions |
| SL-072 | /full-conversation | OMITTED-BY-DECISION | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests`; `docs/reference/slash-commands.md` | Cursor CLI redraw mode |
| SL-073 | /load-workspace | MET | `WorkspaceSetSlashHandler`, `HostSeamSlashHandlerTests`, `ShellAdaptersTests`; `SlashCommandCatalogue` (NativeAction workspace.load), `SlashCommandCatalogueTests` | Opens a saved workspace set as a new session; with no name it lists the saved sets (AC-009) |
| SL-074 | /save-workspace | MET | `WorkspaceSetSlashHandler`, `HostSeamSlashHandlerTests`, `ShellAdaptersTests`; `SlashCommandCatalogue` (NativeAction workspace.save), `SlashCommandCatalogueTests` | Saves the session's folders as a named workspace set |
| SL-075 | /rules | MET | `SlashCommandCatalogue` (NativeAction settings.open rules), `SlashCommandCatalogueTests` | Opens Settings on the Rules page |
| SL-076 | /skills | MET | `SlashCommandCatalogue` (NativeAction settings.open skills), `SlashCommandCatalogueTests` | Opens Settings on the Skills page |
| SL-077 | /usage | PARTIAL | `SlashCommandCatalogue` (OmpDelegated /usage, Partial), `SlashCommandCatalogueTests`, `RuntimeSlashDispatchTests` | Sent to omp as typed; omp shows token usage, not Cursor plan meters. |
| SL-078 | /plugins | MET | `SlashCommandCatalogue` (NativeAction settings.open plugins), `SlashCommandCatalogueTests` | Opens Settings on the Plugins page |
| SL-079 | /fast | MET | `SlashCommandCatalogue` (OmpDelegated /fast), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-080 | /opus | PARTIAL | `SlashCommandCatalogue` (NativeAction model.pick opus, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Filters the model picker by opus and selects the model when exactly one matches. |
| SL-081 | /composer | PARTIAL | `SlashCommandCatalogue` (NativeAction model.pick composer, Partial), `SlashCommandCatalogueTests`, `DesktopSlashActionHostTests`, web `WebSlashActionHost` | Filters the model picker by composer and selects the model when exactly one matches. |
| SL-082 | /branch | MET | `ForkSlashHandler`, `ModelSlashHandlerTests`, `ForkModelTests`, `AcpForkTests`, `AgentSessionRuntimeWiringTests`; `SlashCommandCatalogue` (NativeAction chat.fork), `SlashCommandCatalogueTests` | Alias of `/fork` (AC-018) |
| SL-083 | /duplicate | MET | `ForkSlashHandler`, `ModelSlashHandlerTests`, `ForkModelTests`, `AcpForkTests`, `AgentSessionRuntimeWiringTests`; `SlashCommandCatalogue` (NativeAction chat.fork), `SlashCommandCatalogueTests` | Alias of `/fork` (AC-018) |
| SL-084 | /restore | PARTIAL | `CheckpointSlashHandler`, `ModelSlashHandlerTests`, `CheckpointsModelTests`, `DesktopSlashHandlerRoutingTests`; `SlashCommandCatalogue` (NativeAction chat.restore, Partial), `SlashCommandCatalogueTests` | Alias of `/rewind` (files only) |
| SL-085 | /undo | PARTIAL | `CheckpointSlashHandler`, `ModelSlashHandlerTests`, `CheckpointsModelTests`, `DesktopSlashHandlerRoutingTests`; `SlashCommandCatalogue` (NativeAction chat.undo, Partial), `SlashCommandCatalogueTests` | Previews the files of the latest recorded turn and asks before it puts them back; files only, not the conversation |
| SL-086 | /compact | MET | `SlashCommandCatalogue` (OmpDelegated /compact), `RuntimeSlashDispatchTests`, `SlashCommandDispatcherTests` | Delegated: the effect is omp's and was not observed live (OD-07) |
| SL-087 | /agent-review | MET | `SlashCommandCatalogue` (Skill SKILL.md named agent-review), `assets/skills/agent-review/SKILL.md`, `BuiltinSkillsDesktopTests`, `BuiltinSkillSourceTests`, `SlashCommandCatalogueTests` | Ships as a built-in skill found by the desktop palette and Skills page; a project or user skill of the same name takes over; API-hosted and remote sessions carry no built-ins; the result was not observed with a live model (OD-07) |
| SL-088 | /worktree | MET | `SlashCommandCatalogue` (OmpDelegated /worktree (omp /wt)), `SlashCommandCatalogueTests`, `RuntimeSlashDispatchTests` | Delegated: sent to omp as typed; omp's /worktree (alias of /wt) moves the session into a new worktree; the effect is omp's and was not observed live (OD-07) |
| SL-089 | /apply-worktree | MET | `WorktreeSlashHandler`, `ModelSlashHandlerTests`, `WorktreeCommandsModelTests`, `WorktreeBackendTests`; `SlashCommandCatalogue` (NativeAction worktree.apply), `SlashCommandCatalogueTests` | Applies the changes of a worktree run to the main checkout as a patch against their merge base |
| SL-090 | /delete-worktree | MET | `WorktreeSlashHandler`, `ModelSlashHandlerTests`, `WorktreeCommandsModelTests`, `WorktreeBackendTests`; `SlashCommandCatalogue` (NativeAction worktree.delete), `SlashCommandCatalogueTests` | Asks to delete a worktree run and its checkout; nothing is deleted before you confirm in the Worktrees panel |
| SL-091 | /best-of-n | PARTIAL | `WorktreeSlashHandler`, `ModelSlashHandlerTests`, `WorktreeCommandsModelTests`, `BestOfNHostTests`; `SlashCommandCatalogue` (NativeAction worktree.bestofn, Partial), `SlashCommandCatalogueTests` | Runs the task n times in n worktrees with the current model and lists the candidates; it never merges, creates its worktrees only in Run Everything, and Cursor runs one candidate per model |
| SL-092 | /remote-control | DEFERRED | `SlashCommandCatalogue` (Unavailable), `SlashCommandCatalogueTests` | Sign-in authority (OD-03) and OQ-68; no remote worker |

## 22. Shortcuts (55 rows of table 3)

Source: `03-cursor-settings-slash.md` table 3 (Mac keys; Ctrl on Windows and Linux). The bound keys of OmpOS are the 29 gestures of `ShortcutCatalogue` in `src/OmpOS.Native.Views/Views/Shortcuts`, each raised in a headless shell by `ShortcutCatalogueTests` and shown on the Keyboard page; Tab in the prompt (queue behind a running turn) is handled by `ComposerView` and is not in the catalogue. The web head binds Ctrl+Shift+A, Ctrl+Shift+K and Ctrl+, in `src/OmpOS.UI/wwwroot/scripts/ompos-keys.js` and the composer handles Enter, Shift+Enter, Ctrl+Enter and Ctrl+Shift+Enter. Remapping is omitted by design (`docs/settings-parity.md` S48). macOS keys were never exercised.

Counts (55 rows): MET 7, MET-LIVE 0, PARTIAL 11, DEFERRED 0, NOT-STARTED 31, OMITTED-BY-DECISION 6.

| Row | Cursor key and action | Status | Evidence | Gap or blocker |
|---|---|---|---|---|
| SK-01 | General: Cmd+I (Toggle Sidepanel (unless bound to a mode)) | NOT-STARTED | none | No sidepanel toggle: the layout is fixed |
| SK-02 | General: Cmd+L or Cmd+K (Toggle Sidepanel) | NOT-STARTED | none | Ctrl+K opens conversation search, not a sidepanel toggle; Ctrl+L is not in `ShortcutCatalogue` |
| SK-03 | General: Cmd+E (Toggle Agent layout) | NOT-STARTED | `ShortcutCatalogue` (Ctrl+Shift+A and Ctrl+Shift+K switch tabs, a different action) | No Agent layout toggle |
| SK-04 | General: Cmd+. (Mode Menu) | NOT-STARTED | none | The mode chip is mouse and keyboard focus only |
| SK-05 | General: Cmd+/ (Loop between AI models) | NOT-STARTED | none | No model cycling key |
| SK-06 | General: Cmd+Shift+J (Cursor settings) | PARTIAL | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Ctrl+, opens Settings) | OmpOS's Settings key is Ctrl+,; Ctrl+Shift+J is not bound |
| SK-07 | General: Cmd+Shift+Space (Toggle Voice Mode) | NOT-STARTED | none | No voice mode (AC-026) |
| SK-08 | General: Cmd+, (General settings) | MET | `ShortcutCatalogue`, `ShortcutCatalogueTests`, `ShellShortcutEffects`; web: src/OmpOS.UI/wwwroot/scripts/ompos-keys.js (settings), `ShortcutHost` | Ctrl on Windows and Linux; macOS unmeasured |
| SK-09 | General: Cmd+Shift+P (Command palette (Open Agents Window / Open IDE)) | NOT-STARTED | none | No command palette |
| SK-10 | Agents Window: Cmd+P, Cmd+Shift+F (Search files, search all files) | NOT-STARTED | `WorkspaceSearchView` (opened from the tool strip) | No Ctrl+P or Ctrl+Shift+F |
| SK-11 | Agents Window: Cmd+K (Command palette, search across conversations) | MET | `ShortcutCatalogue` (Ctrl+K `SearchConversations`), `ShortcutCatalogueTests`, `ShellShortcutEffects`, `ConversationSearchViewTests` | Ctrl+K opens the conversation search overlay on the desktop; it is not a full command palette (AC-012); the web head has no Ctrl+K |
| SK-12 | Agents Window: Cmd+F (Find in open conversation) | MET | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Ctrl+F SearchTranscript), `ShellShortcutEffects` | Find in the open conversation; web has no Ctrl+F handler in `ompos-keys.js` |
| SK-13 | Agents Window: Shift+Cmd+S (Populate a side chat from the transcript selection) | NOT-STARTED | none | Selection to side chat not built |
| SK-14 | Agents Window: Cmd+Shift+D (Toggle Design Mode (browser focused)) | NOT-STARTED | none | No Design Mode (AC-007) |
| SK-15 | Design Mode: Arrow Up/Down (Highlight parent/child element) | NOT-STARTED | none | No Design Mode |
| SK-16 | Design Mode: Shift+click (Multi-select) | NOT-STARTED | none | No Design Mode |
| SK-17 | Design Mode: Option+click (Add element to chat input) | NOT-STARTED | none | No Design Mode |
| SK-18 | Design Mode: Cmd+L (Add note or text edit to chat input) | NOT-STARTED | none | No Design Mode |
| SK-19 | Design Mode: Cmd+Z, Esc, Return (Undo markup, cancel/exit, send/save note) | NOT-STARTED | none | No Design Mode |
| SK-20 | Chat: Return (Queue message (default)) | PARTIAL | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Enter sends; Ctrl+Shift+Enter follows up), `RunInputSchedulerTests` | Enter sends and Tab queues the draft behind a running turn; the follow-up that waits for the turn is Ctrl+Shift+Enter; the queue and its reorder are AC-015 |
| SK-21 | Chat: Cmd+Return (typing) (Force send) | MET | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Ctrl+Enter steer), `AcpSteeringTests`, `EngineRunInputSendTests` | Not observed in a live turn (OD-07) |
| SK-22 | Chat: Cmd+Shift+Backspace (Cancel generation) | PARTIAL | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Escape stops the run) | Escape, not Ctrl+Shift+Backspace |
| SK-23 | Chat: Cmd+Shift+L (code selected) (Add selection as context) | NOT-STARTED | none | Selection as context not built (report Mentions deviation 5) |
| SK-24 | Chat: Cmd+V, Cmd+Shift+V (clipboard code/log) (Add clipboard as context, or to input box) | NOT-STARTED | none | Clipboard as context not built |
| SK-25 | Chat: Cmd+Return with suggested changes (Accept all changes) | NOT-STARTED | `ChangesBarView` (Apply review button) | No key |
| SK-26 | Chat: Cmd+Shift+Backspace with suggested changes (Reject all changes) | NOT-STARTED | `ChangesBarView` | No key |
| SK-27 | Chat: Tab (Cycle to next message; queue message for after the turn ...) | PARTIAL | `ComposerView` (`QueueRequest`), `QueuedMessagesModelTests`, `QueuedMessagesViewTests` | Tab in the prompt queues the draft behind a running turn; Tab does not cycle to the next message and is not in `ShortcutCatalogue`, so the Keyboard page does not list it |
| SK-28 | Chat: Shift+Tab (Rotate Agent modes) | NOT-STARTED | none | Mode rotation not bound (AC-005) |
| SK-29 | Chat: Cmd+Opt+/ (Model toggle) | NOT-STARTED | none | No model toggle key |
| SK-30 | Chat: Cmd+N or Cmd+R (New chat) | MET | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Ctrl+N NewAgent), `ShellShortcutEffects` | Ctrl+R is not bound |
| SK-31 | Chat: Cmd+T (New chat tab) | NOT-STARTED | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Ctrl+Shift+T opens a terminal, not a chat tab) | No new chat tab key |
| SK-32 | Chat: Cmd+[ , Cmd+] (Previous, next chat) | NOT-STARTED | none | No previous or next chat key (Ctrl+1 focuses the agent list) |
| SK-33 | Chat: Cmd+W (Close chat) | PARTIAL | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Delete closes the focused agent; Ctrl+W closes Settings) | No Ctrl+W close chat |
| SK-34 | Chat: Escape (Unfocus field) | PARTIAL | `ShortcutCatalogue`, `ShortcutCatalogueTests` (Escape dismisses) | Dismisses popups and stops a run; not an unfocus |
| SK-35 | Chat: Option+Enter (Alt+Enter) on a / skill (Use skill as Custom Mode) | NOT-STARTED | `CustomModeModel`, `CustomModeChipView` | No Alt+Enter on a skill; the mode is pinned from the chip (AC-006) |
| SK-36 | Code context: @ (@-mentions) | MET | `MentionParser`, `MentionPickerModelTests`, `ComposerMentionViewTests`, `MentionSourceFactoryTests` | Typing @ opens the picker in desktop chat sessions (AC-040); the web head has no picker |
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

Rows of `docs/quality/cursor-parity-audit-2026-10-26.md` that map to rows of this audit, with the status then and now. The mapping is not one to one: the new inventory splits and renames rows. 'Old' is the previous audit's final-tree status at `d75fd88`; 'New' is the status in this version, at `7535859`.

| Previous row | Old | New row | New | What changed |
|---|---|---|---|---|
| ADE-01 session chat | PARTIAL | AC-001 | PARTIAL | Unchanged: no live model turn; the web head adds Demo parity only |
| ADE-03 steering | MET | AC-016 | MET | Unchanged; the queue (AC-015) is now MET on the desktop |
| ADE-04 side chats and transcript search | MET | AC-019, AC-020 | PARTIAL, MET | Split: the side chat is wired (`SideChatHub`, a second omp answers) but its engine has no test of its own and no live omp answered; cross-chat search is unchanged and the Ctrl+K overlay is mounted |
| ADE-05 agents window | PARTIAL | AC-008 | PARTIAL | The rail now shows cloud agents and the remote status of ssh sessions; the lease holder is still unavailable |
| ADE-06 worktree per run | PARTIAL | AC-131, WS-06 | PARTIAL | The desktop creates worktrees behind `/best-of-n` and has a Worktrees panel; a normal local run still uses the workspace |
| ADE-07 cloud runs | PARTIAL | CA-01, API-01 | PARTIAL | Control plane, worker bridge, Railway client and cloud UI were added and the routes are mapped in `Program.cs` (off until `Cloud:Enabled`); the API is not deployed (a staged private worker is, 2026-10-28) |
| ADE-08 remote control | PARTIAL | CA-24 | DEFERRED | Re-read: Cursor's Remote Control is a local loop handed to the cloud; OmpOS has none (OD-03, OQ-68) |
| ADE-10 open folder | PARTIAL | AC-009, WS-03 | PARTIAL | A saved set of folders opens as a new session (rail Folders dialog, `/load-workspace`); the root of the open window is still fixed and the explorer is single-root |
| ADE-11 diff review | MET | AC-122 | MET | Unchanged; web adds `ChangesBar` |
| ADE-12 inline edit | PARTIAL | AC-144 | NOT-STARTED | Cursor retired the flow (finding N1); the replacement hand-off is not built |
| ADE-13 inline completion | PARTIAL | AC-140 | PARTIAL | Unchanged |
| ADE-14 terminal, auto-run | MET | AC-123 | PARTIAL | The row is now Run Modes: wired and tested for local desktop sessions; omp's request field names are UNVERIFIED and Auto-review has no classifier |
| ADE-15 browser pane | NOT-STARTED | AC-007, AC-126 | NOT-STARTED | Unchanged |
| ADE-16 MCP client | PARTIAL | AC-110, AC-111 | PARTIAL, MET | The head reads the configuration files and offers the servers in `session/new`; that omp connects is UNVERIFIED |
| ADE-17 rules, skills, modes | PARTIAL | AC-006, AC-060, AC-070, AC-071 | MET, PARTIAL, PARTIAL, PARTIAL | Discovery over six directories at two scopes reaches Settings and the palette; the custom-mode chip is wired (AC-006); ten built-in skills ship; no run path reads rules |
| ADE-18 text and regex search | MET | AC-048 | PARTIAL | The Cursor row is now an index-backed Instant Grep; the user search is unchanged |
| ADE-20 export and usage history | PARTIAL | AC-175 | PARTIAL | Unchanged: usage history empty (the context ring is fed from `usage_update`, AC-021) |
| ADE 21, ADE 22 extensions, admin | DEFERRED | RD-04, RD-06, AD rows | DEFERRED | Unchanged |
| CX-01 to CX-03 hooks, automations | PARTIAL | AC-100 to AC-103, AU-01 | PARTIAL | Hooks are wired into the desktop head for local sessions and automations into the desktop (cron) and the API; no live omp, no live cloud worker |
| CX-05 remote development | DEFERRED | RD-01 to RD-03 | PARTIAL, MET, MET | ssh sessions start from the head and one live loopback run is recorded (2026-10-28); remote files, terminal, hooks and mentions are not wired for ssh |
| VX-02 slash palette | PARTIAL | SL-001 to SL-092 | see section 3 | One registered command became 92 catalogued tokens with handler kinds and statuses |
| Settings row 1 models | MET-LIVE | AC-163, AC-164, S39 | MET-LIVE, MET, MET | Live proof is not re-run |
| Settings rows 7, 8, 10 rules, skills, commands | MET | AC-060, AC-070, AC-090 | PARTIAL, PARTIAL, MET | The inventory rows ask for more (rule types, nested rules, skill loading) |
| Settings row 12 hooks | DEFERRED | AC-100, S28 | PARTIAL, MET | The Hooks page is registered and wired; no hook ran against a live omp |
| Settings row 3 Run Mode | PARTIAL | S04, S17, AC-123 | MET, MET, PARTIAL | The Run mode page is registered and the policy is wired for local sessions |
| Settings row 21 keyboard | MET | S48, SK rows | MET | 29 catalogued gestures (25 in the first draft) |

## 24. Document drift

Drift found between the records and the audited tree. This audit edits no other file, so each item is reported, not fixed. 'Base' means the document as found at `7535859`.

| Where | Drift | Owner |
|---|---|---|
| `docs/settings-parity.md` "What exists" | Says the window has eleven pages and lists eleven; the v0.15.0 section of the same file lists 21 and omits Automations, which is the 22nd page when the head supplies the manager | Settings agent |
| `docs/settings-parity.md` S02, S23, S36 | S02 says `/vim` flips the flag, but the catalogue lists `/vim` Unavailable; S23 says a mode is chosen from the slash palette, but it is the chip of a session (Alt+Enter in the palette is not built); S36 says Enter steers, the catalogue binds Ctrl+Enter to steer and Enter to send | Settings agent |
| `docs/reference/slash-commands.md` | Says the web head implements `ISlashActionHost` in a later wave; `WebSlashActionHost` exists | Slash agents |
| `docs/runbooks/deferred-and-blocked-2026-10-26.md` row 'Phase 34 Remote development' | Base text: Blocked (external), 'No stub ships'; `Harness.Remote`, its UI, ADR-0015 (OQ-97 default) and a recorded live loopback ssh run now exist. The banner of the page now says so; the dated row is kept as recorded | Documentation agents (banner added after this audit) |
| `docs/reference/automations.md` Cursor parity table | Calls AU-04, AU-06 to AU-08 and AU-10 Omitted; this audit grades them NOT-STARTED or DEFERRED because no decision record names them | Automations agent |
| `docs/quality/cursor-parity-audit-2026-10-26.md` section 4 | It lists hooks, automations and the MCP registry as unwired and remote development as deferred with no stub; the previous audit stays as the dated record | none (dated) |
| `docs/plans/v0.15.0-research/01-cursor-agent-chat.md` row AC-144 and finding N1 | Research says OmpOS ADE-12 clones a retired Cursor flow; `InlineEditView` is still mounted as a tool in `MainView` without a model | Owner of ADE-12 |

Fixed after this audit: the page count and the rows S02, S23 and S36 of `docs/settings-parity.md`, the sentence on the web action host in `docs/reference/slash-commands.md`, and the Omitted labels of AU-04, AU-06 to AU-08 and AU-10 in `docs/reference/automations.md` (they now read Not built). The remaining rows are left as dated records or belong to the owner named in the table.

Not audited: ADR bodies beyond their status, `docs/api/`, `.planning/**`.

## 25. Final counts by status per family

| Family | MET | MET-LIVE | PARTIAL | DEFERRED | NOT-STARTED | OMITTED-BY-DECISION | Total |
|---|---|---|---|---|---|---|---|
| Agent and chat (AC-001 to AC-031) | 5 | 0 | 16 | 3 | 6 | 1 | 31 |
| Context and ignore (AC-040 to AC-051) | 3 | 0 | 7 | 0 | 1 | 1 | 12 |
| Rules, skills, subagents and commands (AC-060 to AC-092) | 2 | 0 | 10 | 1 | 2 | 4 | 19 |
| Hooks (AC-100 to AC-104) | 0 | 0 | 5 | 0 | 0 | 0 | 5 |
| MCP and plugins (AC-110 to AC-118) | 1 | 0 | 3 | 0 | 3 | 2 | 9 |
| Tools, terminal, sandbox and browser (AC-120 to AC-130) | 2 | 0 | 5 | 1 | 2 | 1 | 11 |
| Worktrees (AC-131, AC-132) | 0 | 0 | 2 | 0 | 0 | 0 | 2 |
| Tab and inline edit (AC-140 to AC-145) | 0 | 0 | 1 | 0 | 4 | 1 | 6 |
| Review, Bugbot and git (AC-150 to AC-155) | 0 | 0 | 2 | 0 | 2 | 2 | 6 |
| Models, privacy and billing (AC-160 to AC-176) | 3 | 1 | 2 | 0 | 0 | 6 | 12 |
| Cloud agents (CA, EN, API, AU, IN rows and AC-190 to AC-194) | 2 | 0 | 30 | 6 | 48 | 22 | 108 |
| Remote development (RD rows) | 2 | 0 | 3 | 2 | 1 | 1 | 9 |
| Workspaces (WS rows) | 0 | 0 | 11 | 0 | 0 | 2 | 13 |
| CLI (CL rows) | 1 | 0 | 15 | 1 | 2 | 2 | 21 |
| Team, admin and accounts (AD rows and AC-01 to AC-08) | 0 | 0 | 3 | 9 | 3 | 18 | 33 |
| Settings (S01 to S50) | 8 | 0 | 20 | 0 | 0 | 22 | 50 |
| Slash tokens (92) | 44 | 0 | 24 | 4 | 6 | 14 | 92 |
| Shortcuts (55 rows of table 3) | 7 | 0 | 11 | 0 | 31 | 6 | 55 |
| **All rows above** | **80** | **1** | **170** | **27** | **111** | **105** | **494** |

## 26. Top gaps

In the order they limit the claim 'feature-complete against Cursor 3.27':

1. No live model turn: every agent-behaviour row (steering, hooks, MCP servers, slash delegation, skills, plans, images, fork, side chat, checkpoints) rests on unit, integration and fake-omp tests (OD-07).
2. ACP form elicitation is not implemented, so omp's `ask` tool, plan approval and MCP elicitation cannot work (AC-003, AC-024, AC-113).
3. Cloud agents: routes mapped but the API not deployed (only a staged private worker is, 2026-10-28), the worker cannot clone or branch, the consent claim for cloud hooks is not minted, git tokens expire after an hour, secrets and runs are in memory (CA-01, EN rows, API rows).
4. Remote over ssh has one loopback live run and no remote explorer, editor, terminal, hook files or mention files; containers ran against a real podman with a fake omp; WSL never ran (RD rows).
5. Engine limits: no Ask or Debug mode, no file-level checkpoint in omp, no /goal or /loop over ACP, no subagent roster (AC-002, AC-004, AC-017, AC-023, AC-083).
6. No browser pane, Design Mode, voice, canvases or deeplinks; image attach exists on the desktop only, with model vision unknown (AC-007, AC-025, AC-026, AC-029, AC-129).
7. No pull-request reviewer (Bugbot-like), no automation triggers beyond cron, signed webhook and GitHub, and no automation run against a live cloud worker (AC-150 to AC-153, AU rows).
8. Run modes: no Auto-review classifier, omp's permission request field names unverified, a changed mode does not reach a running omp (AC-123, AC-124).
9. Usage history is empty and the context ring has no per-category counts (AC-021, AC-175).
10. Most shortcuts of table 3 are unbound (SK rows: 31 NOT-STARTED).

## 27. Riskiest items

Items where a wrong or half-wired state would do harm, not merely leave a gap:

1. The full `./bootstrap.sh` gate had not finished when this audit was written; it passed on `e24e6851` (recorded 2026-10-28, `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`), which is the proof that counts. The GitHub `ci` workflow did not pass (OQ-168).
2. Hooks (AC-100 to AC-103): they can deny tool calls. Consent v2 hashes the scripts a command names heuristically; the managed-file pre-consent does not check root ownership (report HooksCore, ADR-0016); `IPromptHookEvaluator` has no implementation; the omp extension ran only against fakes and a fake omp, never a live omp.
3. Run Modes and permissions (AC-123, AC-124): the resolver wraps omp's permission requests whose field names are UNVERIFIED (report Safety); Run Everything must widen only the kinds it names (`EnginePermissionOrderTests` checks the order and that a deny rule still stops a command); omp's approval mode is per process, and whether a remote omp gets `--approval-mode` is UNVERIFIED, so it may run with its own default.
4. Mentions (AC-040 to AC-047): they put file contents into prompts. The secret-file filter (`MentionSecretFilesTests`), the ignore filter and the size caps are in the send path of the desktop head; `.cursorignore` does not bind omp's own tools.
5. Cloud (CA, EN rows): the deployed staged worker (2026-10-28) would start without a repository; the one-hour git token goes stale; Railway operations marked UNVERIFIED in `RailwayOperations`; remote desktop is blocked by Railway's acceptable-use text (OQ-73); Railway bills compute while a worker is awake.
6. Remote over ssh: agent forwarding lets the remote root use the user's agent (warned in the profile editor); reconnect after a dropped connection was only tested with fake ssh; a Windows head rejects a remote working directory.
7. Checkpoints (AC-017): the rollback path after a failed mid-rewind write has no test (report Checkpoints).
8. The web Demo build is a simulated engine; any screenshot or demo must not be read as proof of the engine, MCP, hooks or cloud (ADR-0017).
9. Automations (AU rows): the webhook receiver is a public endpoint (rate limited, size capped, HMAC-signed; off until `Automations:Enabled`); the desktop runs a prompt in a new session after one first-run consent, and the real UI path of that run (`HeadAutomationSession(s)`) has no test.
10. Status drift: some documents already disagree with the tree (section 24); a reader of those alone will mis-state the status.

## 28. What needs the owner

| Need | What is missing | Record |
|---|---|---|
| Windows and macOS runs | A Windows 11 24H2 runner, the Mac channel and an Android device: PTY terminal, Avalonia heads, remote `Path.IsPathFullyQualified` fix, macOS keys, WSL environments, browser-pane measurement; nothing ran on Windows, macOS or Android | OQ-69, OQ-51, OQ-74, `docs/runbooks/local-terminal.md` |
| A live model turn | A recorded approval to spend model credentials: steering into a running turn, hooks through the omp extension, MCP servers sent in `mcpServers`, skill and slash delegation, images, `--add-dir`, fork, side chat, remote `session/load` | OD-07, OQ-71, OQ-81 |
| Railway deploy | Authority to deploy the API and workers (the API is not deployed; a staged private worker was deployed on 2026-10-28; whether to keep the `ompos-staging` project is OQ-171), Railway's written position on remote desktops, volume or transcript storage, GitHub App registration and installation, the sign-in authority for a public API | OD-03, OQ-70, OQ-73, OQ-100, OQ-104 |
| GitHub and GitBook | Applied 2026-10-28: description, homepage (the Demo URL), 15 topics, discussions on, wiki off, secret scanning, 22 labels. Still the owner's: the social preview image upload, the discussion categories, and a GitBook account to connect the space (a custom domain is a paid feature) | OQ-101, `docs/runbooks/github-setup.md` |
| Brand and look sign-off | The White-and-Purple brand and the rendered look have no recorded owner answer; the Light default is a recorded default | OQ-94, OQ-87 to OQ-91 |
| Licence form decisions | What 'AGL 3.0' names (answered by default as AGPL-3.0-or-later), the licence of `agent-swarm` and `agent-substrate`, the private-submodule token | OQ-93, OQ-102, OQ-63 |
| Cursor-compatibility decisions | Whether Cursor's own file locations are read by default (OQ-103); the hooks execution model (OQ-96); remote design (OQ-97); which features the inventory leaves out (OQ-95); coding-plan provider rows (OQ-98); public Demo build (OQ-99) | OQ-95 to OQ-99, OQ-103 |
| Accounts and admin | Whether OmpOS ever gets accounts, teams or billing; every AD row and the DEFERRED admin rows wait on it | OD-03, OQ-61 |

See also `docs/runbooks/deferred-and-blocked-2026-10-26.md` for the standing blocked list and `docs/open-questions.md` for OQ-93 to OQ-104.
