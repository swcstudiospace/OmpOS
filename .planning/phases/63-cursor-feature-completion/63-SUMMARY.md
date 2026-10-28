# Phase 63 Summary: Cursor Feature Completion

Status (recorded 2026-10-28): criterion 1 is Partial, criterion 2 is Met with caveats. Every feature named in the two criteria has code and tests in the final tree, and all but the pull request, browser and terminal-output mentions (and side-chat mentions, which are listed by no head) and the sandbox are composed into the desktop head. Checked by grep on the tree of 2026-10-28: `apps/OmpOS.Native/Agents/SessionEngineSet.cs` builds `MentionSourceContext` with `ReadTranscript` but without `ReadTerminalOutput`, `PullRequests` or `SideChats`; `MentionSourceFactory` lists ten sources (file, folder, terminal, chat, side chat, two git diffs, rule, skill, pull request), falls back to `TerminalOutputUnavailable` and `UnavailablePullRequestSource`, and does not include `BrowserMentionSource`; no other file under `src/` or `apps/` constructs `BrowserMentionSource`, `IMentionBrowserProvider` or an `IPullRequestSource`. These three mention sources are therefore built but not composed, and criterion 1 stays Partial. No row is MET-LIVE: no model turn was run (OD-07), so omp's permission request field names, MCP server pickup from `session/new` and image blocks on a real turn are not observed. The web head and Android do not carry these features. The parity audit file of this phase (`docs/quality/cursor-parity-audit-2026-10-28.md`) was first written against the pre-wiring tree `fbd5266`; commit 14681603 re-graded every row at `7535859`, and its rows AC-040 to AC-047 now agree with the tree (the one difference: AC-044 says "no source" for the browser, but `BrowserMentionSource` exists in `src/OmpOS.Presentation/Mentions`, unused).

The record was written after the work. Statuses use: Met, Met (recorded), Partial, Blocked, Not verified. Feature rows use: Built (library or model only), Wired (composed into the desktop head), Partial (missing piece named), Omitted-by-decision.

Recorded test results (2026-10-28, orchestrator): per-project counts on the final tree before the last wiring: Presentation 2303, Native 2012 (2 skipped), AgentLoop 1221, Acp 201, Mcp 197, Workspace 364, Automations 386, Architecture 46; Api.IntegrationTests 1027/1027 with throwaway podman services. The full `./bootstrap.sh` gate then passed on commit e24e6851 (recorded 2026-10-28, `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`): `dotnet test OmpOS.Linux.slnf` 12215 total, 12213 succeeded, 0 failed, 2 skipped, with `OmpOS.Presentation.Tests`, `OmpOS.Native.Tests`, `OmpOS.Harness.Acp.Tests`, `OmpOS.Harness.AgentLoop.Tests` and `OmpOS.Harness.Automations.Tests` each reported passed; the commits after it (9d4e0075, e120714b) are documentation only. The GitHub `ci` runs of main and of the tag failed at 'Start test services' (rootless podman cannot create a user namespace in the runner, OQ-168), so the local gate is the gate of record. The builder reports for this phase say "not run" because they were written before the integration; the counts above are the recorded runs.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | `@` mentions cover files, folders, terminals, chats, git diffs, browser, rules, skills and pull requests, sent to omp as embedded resources | Partial | Mention resolver tests: `MentionParserTests`, `MentionSourceTests`, `MentionComposerModelTests`, `MentionSecretFilesTests`, `MentionPickerModelTests`, `ComposerMentionsTests`, `ComposerMentionViewTests`, `MentionSourceFactoryTests`; `AcpPromptContentTests` for the embedded `resource` blocks; all inside the passing gate of e24e6851. Files, folders, chats, git diffs, rules and skills are Wired. Side chats, pull requests, terminal output and the browser are not composed in any head (grep, see Status): pull requests resolve to an unavailable message (no Git host source), terminal output is "not available in this window", side-chat mentions list nothing (`SessionEngineSet` sets no `SideChats`), and `MentionSourceFactory` does not include the browser source (no browser pane, ADR-0009). The pull request source needs a configured Git host and the browser source needs the browser pane; the terminal-output reader has no external prerequisite and is simply not built into the head. Not observed with a live omp. See the first table |
| 2 | Run mode, `permissions.json`, `sandbox.json`, `.cursorignore`, `.cursor/mcp.json` import, `.cursor/worktrees.json` with worktree commands, file checkpoints with rewind, queue versus send now, conversation search and the context ring exist or are listed as PARTIAL with the missing piece | Met with caveats | Each feature has tests (inside the passing gate of e24e6851) and a status in the second table; eight rows are Wired (run mode Allowlist and Run Everything, `permissions.json`, `.cursor/mcp.json` import, worktree commands, checkpoints, queue, conversation search), and four are listed Partial with the missing piece: Auto-review has no hosted classifier (decision), `sandbox.json` is parsed and applied only as a read boundary in OmpOS's resolver (OS sandbox Omitted-by-decision, omp has none), `.cursorignore` does not bind omp's own tools, the context ring has no per-category counts (ACP gives size and used only). The refreshed audit (`docs/quality/cursor-parity-audit-2026-10-28.md`, tree `7535859`) carries these rows. Caveats: nothing is observed with a live omp (OD-07); omp's permission request field names are UNVERIFIED (OQ-84); `HeadAutomationSession` and the slot consent bridge have no test of their own |

## Criterion 1: mention kinds

| Feature | Status | Missing piece or evidence |
|---------|--------|---------------------------|
| `@file` | Wired | `FileMentionSource`, `WorkspaceMentionFiles` through the ignore matcher; secret-file names never listed; read at send time, 256 KiB per chip |
| `@folder/` | Wired | `FolderMentionSource` over the same listing |
| Terminals | Partial | `TerminalMentionSource` lists terminals; `MentionSourceFactory.TerminalOutputUnavailable` is the default and `SessionEngineSet` supplies no output reader, so attaching fails with "The output of this terminal is not available in this window" |
| Chats | Wired | `ChatMentionSource` over `CatalogMentionProvider`; `SessionEngineSet` supplies `ReadTranscript` |
| Side chats | Partial | `ChatMentionSource(MentionKind.SideChat, ...)` is in the factory, but `SessionEngineSet` sets no `MentionSourceContext.SideChats` (grep 2026-10-28: no caller sets it), so no side chat is listed |
| Git diffs (working tree, branch) | Wired | `GitDiffMentionSource` over `GitMentionDiffSource` |
| Browser | Partial | `BrowserMentionSource` and `IMentionBrowserProvider` exist in Presentation; no provider and not in the factory list; there is no browser pane (ADR-0009 accepted for Linux only, audit AC-007, AC-126) |
| Rules and skills | Wired | `ConventionMentionSource` for both kinds over `ConventionMentionProvider` (built-in skills are not offered as mentions, by decision) |
| Pull requests | Partial | `PullRequestMentionSource` over `UnavailablePullRequestSource` ("Pull requests need a configured Git host"); no head sets `MentionSourceContext.PullRequests` and no `IPullRequestSource` implementation exists outside the unavailable one |
| Sent as embedded resources | Wired | `AcpPromptContent` builds `resource` blocks (256 KiB each, 1 MiB total, 32 resources, truncation notes); omp advertises `embeddedContext:true` (WireSkillsImages report, omp 18.12.6 source). Chips attach only to a normal send, not to steer or follow-up |

## Criterion 2: features

| Feature | Status | Missing piece or evidence |
|---------|--------|---------------------------|
| Run mode: Auto-review | Partial | Allowlist, then a short known-safe read-only command list, then ask (`KnownSafeCommands`); the hosted classifier is not built (decision: no model provider choice, `docs/reference/run-modes.md`); `allow_instructions` and `block_instructions` are kept and shown, not enforced |
| Run mode: Allowlist | Wired | `RunModePolicy`, `RunModePermissionResolver`, `RunModePolicyHolder`, `RunModeHub`, `RunModePageViewTests`; omp's permission request field names UNVERIFIED |
| Run mode: Run Everything | Wired | `RunModeLaunchMapper` maps to omp's approval mode at session start; widens only the kinds it names; denies still stand |
| `permissions.json` | Wired | `PermissionsFileReader`, `PermissionsLoader`, `PermissionTokenParser`; `PermissionsFileTests`, `EffectivePermissionsTests`. Deny rules are an OmpOS addition (Cursor's file has none); trust flag is an OmpOS addition |
| `sandbox.json` | Partial | `SandboxFileReader`, `SandboxLoader`, `EffectiveSandbox`: the read boundary is enforced in OmpOS's resolver (`SandboxBoundaryTests`); no OS-level sandbox because omp has none (Omitted-by-decision, audit AC-125) |
| `.cursorignore` | Partial | `IgnoreMatcher` (`.cursorignore`, `.ompos/ignore`, gitignore) bounds mentions and the Ignore page; it does not bind omp's own tools |
| `.cursor/mcp.json` import | Wired | `McpConfigLoader` (six files plus `~/.ompos/mcp.json`), `McpServerCatalog`, `AcpMcpServerMapper`; `SessionEngineSet` passes `HeadServices.Mcp.OfferedServers()` to `session/new`; remote and cloud sessions do not get them. No connection checker, no OAuth (omp's `/mcp reauth`); that omp connects to the servers is UNVERIFIED |
| `.cursor/worktrees.json` and worktree commands | Wired | `CursorWorktreesConfig` (Phase 59), `WorktreeCommandsModel`, `WorktreeBackend`, consent-gated setup runner; Worktrees page is read-only; Windows PowerShell setup path UNVERIFIED |
| File checkpoints with rewind | Wired | `CheckpointStore`, `RewindPlanner`, `CheckpointBackend`, `CheckpointsTimelineView`; rollback after a failed mid-rewind write has no test; omp has no file-level checkpoint, OmpOS keeps its own |
| Queue versus send now | Wired | `QueuedMessagesModel`, `QueuedMessagesSteeringAdapter`, composer follow-up and steer; Tab-to-queue shortcut (SK-27) not built |
| Conversation search | Wired | `ConversationSearchIndex`, `ConversationSearchModel`, `ConversationSearchView` created in `MainView.Shell.cs` |
| Context ring | Partial | `ContextRingModel` and view in the composer from ACP `usage_update` (size and used); no per-category counts (audit AC-021, AC-175) |

## Beyond the criteria

Image attach (omp `promptCapabilities.image`, `AcpImagePromptTests`, `AgentSessionRuntimeImageTests`; web and Android not done; a native slash action handled with images attached drops them), built-in skills (nine slash rows Built) and 13 MCP presets, automations on the desktop (cron only; webhooks refused) with the API automations endpoints, container and WSL sessions, and custom modes.

## What the phase delivered

The 15 units of `63-01-PLAN.md`: mention model, wire format and wiring; the ignore matcher; the run mode policy and resolver; MCP import and catalog; checkpoints; worktree commands; queue; conversation search and context ring; image input; built-in skills; automations; container and WSL sessions; and the settings pages that expose them.

## Deviations

- Mention picker is an inline card above the prompt, not a caret-anchored popup (headless host unreliable; the slash palette set the pattern).
- Steer and follow-up sends carry text only; chip content attaches to a normal send.
- Auto-review adds a known-safe read-only list to the allowlist so the mode differs from Allowlist.
- `permissions.json` deny rules and a project trust flag are OmpOS additions.
- The MCP loader reads its own six locations rather than `ConventionCatalog.McpFiles`.
- Desktop automations run cron only; the cloud target applies the first repository's branch to all repositories.
- Selection as context and paste as context (shortcut table) are not built.

## Partial

Terminal-output mentions (no output reader), pull request mentions (no Git host source), side-chat mentions (no list supplied), browser mentions (no pane), Auto-review classifier, `sandbox.json` beyond the read boundary, `.cursorignore` on omp's own tools, context ring categories, MCP connection check and OAuth, and every live behaviour.

## Blocked (OQ/OD)

OD-07 (no live model turn). OQ-84 (omp permission field names UNVERIFIED), OQ-71, OQ-81. The pull request source needs a configured Git host and the browser source needs the browser pane (ADR-0009, OQ-51); no open question names the terminal-output reader, which is a missing piece of the head.

## Known limits

- `HeadAutomationSession` (the approval card and turn in a live session) has no test; only its seam is tested. No live omp, podman, `wsl.exe` or cloud run for these features.
- The reference pages `docs/reference/builtin-skills.md`, `checkpoints-and-worktrees.md` and `mcp.md` and the audit were refreshed against tree `7535859` (14681603, ddae10be); their banners list what is still not wired. The first draft of the audit described `fbd5266` and is superseded.
- Nothing was run on Windows, macOS or Android.
