---
description: "A tour of the OmpOS desktop app: the Agents window, the thread and composer, the Kanban tab, omp terminals, steering, the Changes bar, slash commands, Settings and keyboard shortcuts."
---

# Desktop app guide

Status: Built on Linux (Avalonia 12.1.4; Wave 2 features such as hooks, MCP, remote and cloud are Partial, see the parity audit); Windows and macOS were not run; no live model turn has been run

Last verified: 2026-10-28

Applies to: v0.15.0

The desktop head (`apps/OmpOS.Native`) is the most complete part of OmpOS. It starts `omp acp` for each chat session and, on request, an embedded omp terminal. Every omp process it starts is launched with the same four plugins: Ultrathink, Substrate, Swarm and GSD-Core. To run it, see [Getting started](getting-started.md).

![The desktop Agents window: the session rail on the left, a thread and the composer.](../assets/screens/desktop-agents-light.png)

*Screenshot of the desktop app on Linux.*

## The Agents window

The app opens on the Agents window. The default theme is Light (White and Purple); System and Dark are in Settings > General.

| Area | What it does |
|---|---|
| Session rail | One row per session with a status glyph; several sessions run at once. Ctrl+N starts a new agent, Ctrl+Shift+T an omp terminal. Enter opens the selected row and Delete closes it. |
| Thread | The transcript of the selected session: your messages, markdown, code blocks, tool calls and results, plans, permission requests and file edits with diffs. A pipeline strip shows the stage events of the agent loop (when a plugin supplies them). |
| Composer | The prompt box. Chips choose the model, the mode (what omp advertises for the session, usually default and plan) and thinking. Enter sends, Shift+Enter inserts a new line. |
| Changes bar | After the agent edits files: Keep, Undo, Review, Keep all and Undo all. Review opens a per-hunk diff. Keep, Undo and Apply review write files in the workspace folder. |
| Kanban tab | One card per session on a board with live status. Ctrl+Shift+K opens it; S steers a card. |

## Steering

A running session takes a steering message from the composer (Ctrl+Enter, "steer now") or from its Kanban card. Chat sessions use omp's native `_session/steering`; omp terminals use the OmpOS inbox in the GSD-Core plugin. The UI reports honestly whether the message was delivered, held for the next turn or failed. An injected delivery into a running real turn needs a model turn and has not been exercised: see [Plugins](../plugins.md).

## Slash commands

Typing `/` in the composer opens the slash palette. It lists the commands omp advertises, the OmpOS catalogue of Cursor's tokens (92 tokens, each with a status and, when OmpOS cannot run it, the reason), and the commands, skills and subagents found in your workspace files. Arrow keys move, Enter or Tab completes, Esc closes. The full list and each token's status: [Slash commands](../reference/slash-commands.md).

## Settings

Press Ctrl+, to open the Settings window. The [Settings guide](settings-guide.md) lists every page. Model providers and keys are on the Models page: [Choose a model provider](providers-guide.md).

![The desktop Settings window.](../assets/screens/desktop-settings-light.png)

*Screenshot of the desktop app on Linux.*

## Keyboard shortcuts

The Keyboard page of Settings lists the shortcuts the shell handles, grouped by scope. Remapping is not built.

| Scope | Gesture |
|---|---|
| Application | Ctrl+, Settings; F6 and Shift+F6 next and previous region; Ctrl+1 agent list; Ctrl+2 prompt; Ctrl+F search the transcript; Esc dismiss |
| Agents | Ctrl+N new agent; Ctrl+Shift+T new omp terminal; Ctrl+Shift+A Agents tab; Enter open, Delete close the selected agent |
| Composer | Enter send; Shift+Enter new line; Ctrl+Enter steer now; Ctrl+Shift+Enter follow up; Esc stop the run |
| Kanban | Ctrl+Shift+K Kanban tab; arrow keys move between cards; Enter opens a card; S steers a card |
| Settings | Esc or Ctrl+W close; Home and End first and last page |

## More features

These are built as libraries and models with tests, and each is described on its own page. Whether the desktop window you run shows each one is recorded in the [parity audit](../quality/cursor-parity-audit-2026-10-28.md); none has run against a live omp session.

| Feature | Page |
|---|---|
| Hooks (`hooks.json`, 21 events) | [Hooks](../reference/hooks.md) |
| MCP servers from your existing files | [MCP servers](../reference/mcp.md) |
| Run modes, `permissions.json`, `sandbox.json` | [Run modes](../reference/run-modes.md) |
| Checkpoints and restore, worktrees, queued messages, custom modes, side chats, fork | [Checkpoints and worktrees](../reference/checkpoints-and-worktrees.md) |
| Remote environments over ssh | [Remote environments](../runbooks/remote-ssh.md) |
| Several folders in one session | [Multi-repo](../runbooks/multi-repo.md) |
| Cloud agents on Railway | [Cloud agents](../runbooks/cloud-agents.md) |
| Local and cloud terminals | [Local terminal](../runbooks/local-terminal.md), [Cloud terminals](../runbooks/cloud-terminals.md) |

## About

The About dialog shows the licence (AGPL-3.0-or-later), the version, the source revision and the source URL, as the licence's network clause asks: [Licensing](../licensing.md).

## State and privacy

All state is under `OMPOS_STATE_DIR` (default `~/.local/share/OmpOS`): the session catalogue, per-session directories, settings and the provider key store (owner-only). The app writes outside its own directories only in two cases: omp's `models.yml` and credential database when you save a provider on the Models page, and workspace files when you press Keep, Undo or Apply review. App-started omp processes run without your global omp extensions. Details: [Run the desktop app](../runbooks/running-the-desktop-app.md).

## Limits

- Linux only. The Windows and macOS builds were not run.
- The browser pane, automations, the Pets view and mobile terminals are not built or not wired: see the [parity audit](../quality/cursor-parity-audit-2026-10-28.md).
- No live model turn has been run since the bounded Phase 11 capture.
