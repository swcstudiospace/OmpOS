---
description: "Reference for file checkpoints and restore, git worktrees, the message queue, custom modes, side chats and fork in Autonogrammer."
---

# Checkpoints, worktrees, the message queue, custom modes, side chats and fork

Status: Partial (libraries, models, views and head adapters are built, wired into the desktop head for local sessions and unit tested; no live omp session or model turn ran; Alt+Enter in the slash palette, best of n without Run Everything, `/vim` and `/show-thinking` are not built)

Last verified: 2026-10-28

Applies to: v0.15.0

This page describes how Autonogrammer undoes an agent's file edits, runs work in git worktrees, queues messages while the agent works, pins a skill as a mode, and opens side chats and forks. Each feature is a library, a presentation model, a view and a head adapter; the last section lists which adapter feeds which model.

> **Status of this release** (recorded 2026-10-28, tree 7535859).
>
> - **Built, wired into the desktop head, proven by tests.** `MainView` hosts the Checkpoints timeline, the Worktrees panel, the queued messages under the composer, the custom-mode chip, the side chat pane and fork for each session; `AgentSessionRuntime` takes the forker, the side-chat engine and the prompt preamble. The head adapters are `CheckpointBackend`, `WorktreeBackend`, `WorktreeSetupConsentAdapter`, `BestOfNHost` and `ConventionModeSkillSource` (`apps/Autonogrammer.Native/Composition/Checkpoints/`), `SessionForker` and `OmpSideChatEngine` (`apps/Autonogrammer.Native/Agents/`). Tests: `CheckpointBackendTests`, `WorktreeBackendTests`, `WorktreeSetupConsentAdapterTests`, `BestOfNHostTests`, `ConventionModeSkillSourceTests`, `CheckpointsTimelineViewTests`, `WorktreeCommandsViewTests`, `QueuedMessagesViewTests`, `CustomModeChipViewTests`, `SideChatPaneViewTests`, `AcpForkTests` (wire method `session/fork`), `AgentSessionRuntimeWiringTests` (`/duplicate` forks), and the library tests in `Autonogrammer.Harness.Worktrees.Tests`.
> - **Built, not wired.** `/fork <n>` from an earlier message is refused: the forker copies the whole conversation (`CanForkFromMessage` is `false`). Alt+Enter on a skill in the slash palette is not built (the mode is chosen from the mode chip). Best of n creates its candidates only in Run Everything. `/vim` and `/show-thinking` are Unavailable in the slash catalogue (no Vim key bindings; the transcript does not draw thinking). No test drives checkpoints, worktrees, side chats or fork in an ssh, container or WSL session: UNVERIFIED there. The web head has none of these features.
> - **Not live-verified.** No live omp session ran a fork, a side chat or a checkpoint turn, and no model turn ran (open question OD-07). Whether omp accepts `--approval-mode always-ask` for the side chat's second omp is **UNVERIFIED**. Nothing ran on Windows, macOS or Android.

Cursor 3.27 documents the same features (checkpoints and restore, worktrees, queued messages, Custom Modes, side chats, `/fork`). omp has no file checkpoints: its `checkpoint` and `rewind` tools only prune the conversation context, and `/branch`, `/fork` and `/tree` run in its terminal UI only. Autonogrammer therefore keeps its own file checkpoints and restores files only; it never changes the conversation.

## File checkpoints

### What is kept

For every agent turn in which a file changed, the checkpoint store keeps, per file: the hash of the content before and after the turn, and a copy of the content before the turn. A turn is one user message and everything the agent did after it. The Changes pipeline feeds the store: after each transcript update the head calls `CheckpointRecorderModel.UpdateAsync` with the same blocks and workspace root it gives `ChangeSetModel.Update`. A turn is recorded again when its edits grow while it streams, and not again while they stay the same.

Files are kept under `<state folder>/checkpoints/<hash of the session id>/` (`index.json` and `blobs/<sha-256>`). Directories are mode 0700 and files 0600 on Linux and macOS. Nothing is kept in memory between calls.

| Limit | Default | When exceeded |
|---|---|---|
| One file's earlier content | 2 MiB | The file is recorded by hash only and cannot be restored |
| Earlier content of one session | 256 MiB | The oldest turns are dropped |
| One turn's earlier content | half of the session limit | Later files of that turn are recorded by hash only |
| Turns per session | 200 | The oldest turns are dropped |
| Files per turn | 500 | Files after the limit are not recorded |

Binary files (content with a NUL byte) are recorded by hash only. A binary file the agent created can still be deleted by a rewind. Paths outside the workspace folders are not recorded.

### Rewind

`CheckpointStore.RewindAsync(session, turn, force)` puts every file that this turn or a later turn changed back to its state before that turn. It follows these rules:

- **Stale files block the rewind.** A file whose content is not what the agent's last edit left has changed since. Without `force` nothing is written and the result lists the stale files (`Blocked`). With `force` they are overwritten. A text file's hash ignores a byte order mark and the difference between CRLF and LF, so re-saving a file with other line endings does not make it stale.
- **Nothing outside the workspace is touched.** Every path is resolved through `WorkspaceRoot` with symbolic links followed, and a file's folder must be one of the folders the store was given. A recorded file whose folder is not open any more is refused.
- **Writes are atomic and undone on failure.** Each file is written through a temporary file in its folder. If a write fails, the files already written are put back. A file that changes between the plan and the write stops the rewind (`Blocked`).
- **Line endings are kept.** If the file uses CRLF and the stored text uses bare LF, the restored text uses CRLF; a byte order mark is kept; the executable bit of an existing file is kept (Linux, macOS).
- **Files the agent created are deleted** only when they still hold what the agent left (or with `force`).
- **Unrestorable files are reported, not failed.** A binary file, a file over the size limit and a damaged stored copy leave the file as it is and are counted in the result.
- **A rewind consumes the turns it undid.** They leave the timeline and their keys are remembered, so the recorder does not record them again from the unchanged transcript. Turn numbers are never reused.

`PlanRewindAsync` gives the same plan without writing: per file `Restore`, `Delete`, `AlreadyAtTarget`, `Stale`, `Refused` or `Unrestorable`, each with a sentence.

### The timeline and the commands

`CheckpointsModel` shows one row per turn (number, prompt, files changed, relative time) with Preview and Rewind. Rewind always asks: it shows how many files are written or deleted, how many are stale and how many cannot be restored, and says that only files are restored and the conversation is not changed. A stale file keeps the confirmation open and writes nothing until the user chooses *Overwrite changed files*.

| Command | What it does |
|---|---|
| `/rewind` | Opens the timeline |
| `/rewind <n>`, `/restore <n>` | Asks to rewind the files to before checkpoint n |
| `/undo` | Asks to rewind the latest checkpoint |

The setting `rewind` (Cursor's `rewind` key) switches the three commands off.

## Worktree commands

`WorktreeCommandsModel` works over `WorktreeManager`, `WorktreeApplier` and the setup runner of `Autonogrammer.Harness.Worktrees`. A run's worktree is `<operator root>/<run id>` on branch `autonogrammer/run/<run id>`; the cap is Cursor's `cursor.worktreeMaxCount` (25).

- **Start** (`/worktree`, the *New worktree* control) creates a worktree and runs its `.cursor/worktrees.json` setup behind consent. A name is turned into a valid run id; a blank name gives `wt-<UTC date>-<time>`. omp's own `/worktree` moves the live session into a worktree and stays a delegated command; the native start is for the Worktrees panel and for best-of-n.
- **Review** shows the run's changes as a patch: committed and uncommitted changes and new files, against the commit the run started from (the common ancestor with the checkout). Ignored files are left out.
- **Apply** (`/apply-worktree [run] [merge]`) applies the patch to the open checkout and leaves the changes uncommitted. Git writes the patch to a private temporary file and applies it from there, so files that are not UTF-8 and binary files are applied byte for byte.
  - *Clean* (default): the patch is applied only if it fits. If not, nothing is written and the files that do not fit are listed (a conflict result).
  - *Merge three ways* (the word `merge`): git merges the patch into the files that changed since. A clean merge is staged; files that conflict hold conflict markers and are listed.
- **Delete** (`/delete-worktree [run]`) shows what would be lost (changed files that were not applied) and deletes only after confirmation.
- **Best of n** (`/best-of-n <n> <task>`) creates n worktrees (1 to 8), calls `IBestOfNHost.RunAsync` for each with the same prompt, and lists the results (finished or failed, files changed, what the agent said). The user reviews each, applies the best and discards the rest after confirmation. The agent runs need live sessions, so the head implements `IBestOfNHost`; Cursor runs one candidate per model, and Autonogrammer passes the candidate number so the host may choose a model per candidate.

### Setup commands and consent

`.cursor/worktrees.json` is looked for in the worktree, then in the project root (as Cursor does). The setup for the operating system is `setup-worktree-unix` or `setup-worktree-windows`, else `setup-worktree`; it is a list of shell commands or a script path inside `.cursor/`. `ConsentedWorktreeSetupRunner` runs nothing until `IWorktreeSetupConsent` answers yes for this exact setup: the identity is the repository plus the SHA-256 of the file and, for a script entry, of the script. Changing either asks again. The host keeps the answer in the setup consent store it already has, keyed by the path `<repository>/.cursor/worktrees.json`, so it never replaces the approval of `.autonogrammer/setup.json`. `ShellWorktreeSetupExecutor` runs each command through `/bin/sh -c` (PowerShell on Windows, not verified) with `ROOT_WORKTREE_PATH` set to the main checkout, stopping at the first failure.

## Message queue

`QueuedMessagesModel` holds the messages a user sends while the agent works, in send order, at most 16 (the run scheduler's own cap). A message can be edited, removed, moved up or down, dragged to a place (`MoveTo`) or sent now. Send now delivers it into the running turn as a steer through `SteeringInputModel`; the message leaves the queue only when the delivery was accepted, so a failed delivery loses nothing. When the run ends, the head calls `QueuedMessagesSteeringAdapter.DispatchNextAsync`, which starts the first message as a new turn and puts it back at the front if the start fails. Queues live in memory, one per session (`QueuedMessagesRegistry`); they are not saved, because a queued message means nothing after a restart. `SteeringInputModel` and `RunInputScheduler` are unchanged: a message queued here is not given to the scheduler's follow-up queue.

## Custom modes

`CustomModeModel` lists the user-invocable skills of the workspace (the same discovery as the slash palette) and pins one as the session's mode: **Use as Mode**. While a mode is pinned, `IPromptPreamble.Apply` puts the skill's body in front of every prompt as

```
<mode name="review">
…skill body…
</mode>

…the user's prompt…
```

until the user exits the mode. The mode chip shows `Mode: <name>` with the skill's `icon` and `color` (`default`, `green`, `cyan`, `blue`, `purple`, `magenta`, `orange`, `yellow`, `red`, `brand`; anything else is `default`). **Alt+Enter** (Option+Enter on macOS) on a skill highlighted in the slash palette is not built; choose the skill in the chip. A body longer than 20,000 characters, an empty body and an unreadable skill are refused with a sentence and keep the mode that was pinned.

## Side chats and fork

A **side chat** (`/side`, `/btw`) is a child conversation. `SideChatHub.OpenAsync` takes a read-only summary of the parent at that moment (`ParentSummaryBuilder`: what was asked and answered, the plan, the files edited and the errors, newest last, cut from the front to fit 6,000 characters) and gives it to `ISideChatEngine` as hidden context. The side chat's messages stay in its own conversation; nothing is added to the parent's transcript, and closing it never touches the parent. Side chats do not nest.

**Fork and duplicate** (`/fork`, `/branch`, `/duplicate`) go through `ISessionForker`. omp advertises `sessionCapabilities.fork` and the Agent Client Protocol request `unstable_forkSession`, which forks the whole session; a forker that cannot fork from a message says so (`CanForkFromMessage`) and `/fork <n>` is then refused with a sentence. The original chat is not changed.

## Display switches

`/line-numbers` and `/status-indicators` flip switches of `DisplaySettings` (with `on`, `off`, `toggle` or `status`). `/vim` and `/show-thinking` are Unavailable (see [Slash commands](slash-commands.md)): `DisplaySettings` holds a Vim flag, but no composer reads it. The switches are saved in `display.json`, a small versioned JSON file Autonogrammer owns, next to the other Autonogrammer settings; a missing or damaged file reads as the defaults. Defaults: Vim off, everything else on.

## What the head provides

| Model | Needs | Provided by |
|---|---|---|
| `CheckpointRecorderModel`, `CheckpointsModel` | `ICheckpointBackend` over `CheckpointStore` | `CheckpointBackend` |
| `WorktreeCommandsModel` | `IWorktreeBackend` over `WorktreeManager`, `WorktreeApplier`, `IWorktreeSetupRunner`; `IBestOfNHost` | `WorktreeBackend`, `WorktreeSetupConsentAdapter`, `BestOfNHost` (over `BestOfNCandidateRunner`) |
| `QueuedMessagesModel` | `SteeringInputModel` and a way to start a turn (`QueuedMessagesSteeringAdapter`) | `MainView`: Tab queues while a run is active, the next message is sent when the run ends, Stop clears the queue |
| `CustomModeModel` | `IModeSkillSource` over `ConventionCatalog`; every prompt sent through `IPromptPreamble.Apply` | `ConventionModeSkillSource`; `AgentSessionRuntime.PromptPreamble` |
| `SideChatHub` | `ISideChatEngine` (a second omp session) | `OmpSideChatEngine` (a second omp with `--approval-mode always-ask` that admits no tool kind) |
| `ForkModel` | `ISessionForker` over `session/fork` | `SessionForker` over the engine's `session/fork` (`IOmpSessionFork`) |
| Slash handlers | `ISlashSessionContext`, `IClipboardWriter`, `IAboutLauncher`, `IFolderOpener`, `IApplicationLifetime`, `IUrlLauncher`, `IEditorOpener`, `IChangesBarOpener`, `IWorkspaceSetsBackend`, `IDisplaySettingsStore` | the adapters of `src/Autonogrammer.Native.Views/Shell/`; `IFolderOpener` is not provided (there is no log folder to open) |

`SlashActionHandlerHost` routes the actions of these handlers and passes every other action to the head's own `ISlashActionHost`.
