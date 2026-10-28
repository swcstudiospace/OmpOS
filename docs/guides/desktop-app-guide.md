---
description: "A tour of the Autonogrammer desktop app: the Agents window, the thread and composer, the Kanban tab, omp terminals, steering, the Changes bar, slash commands, Settings and keyboard shortcuts."
---

# Desktop app guide

Status: Built on Linux (Avalonia 12.1.4; hooks, MCP, run modes, mentions, checkpoints, remote, containers, cloud and automations are wired into the head and tested with fakes, so they are Partial: no live model turn has run); Windows and macOS were not run

Last verified: 2026-10-28

Applies to: v0.15.0

The desktop head (`apps/Autonogrammer.Native`) is the most complete part of Autonogrammer. It starts `omp acp` for each chat session and, on request, an embedded omp terminal. Every omp process it starts is launched with the same four plugins: Ultrathink, Substrate, Swarm and GSD-Core. To run it, see [Getting started](getting-started.md).

![The desktop Agents window on Linux: the session rail on the left, an empty new session and the slash palette listing 134 commands above the composer.](../assets/screens/desktop-agents-light.png)

*Screenshot of the real desktop app on Linux (Xvfb, sandbox profile, no model turn). The banner reports degraded pipeline stages because the plugin submodules were not checked out in that sandbox. More shots and the defects the run found: [desktop evidence](../runbooks/evidence/desktop-app-2026-10-28/README.md).*

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

A running session takes a steering message from the composer (Ctrl+Enter, "steer now") or from its Kanban card. Chat sessions use omp's native `_session/steering`; omp terminals use the Autonogrammer inbox in the GSD-Core plugin. The UI reports honestly whether the message was delivered, held for the next turn or failed. An injected delivery into a running real turn needs a model turn and has not been exercised: see [Plugins](../plugins.md).

## Slash commands

Typing `/` in the composer opens the slash palette. It lists the commands omp advertises, the Autonogrammer catalogue of Cursor's tokens (93 rows: Built 36, Partial 24, Delegated 9, Unavailable 24, each with a status and, when Autonogrammer cannot run it, the reason), the ten [built-in skills](../reference/builtin-skills.md) and the commands, skills and subagents found in your workspace files. Arrow keys move, Enter or Tab completes, Esc closes. The full list and each token's status: [Slash commands](../reference/slash-commands.md).

## Settings

Press Ctrl+, to open the Settings window. The [Settings guide](settings-guide.md) lists every page. Model providers and keys are on the Models page: [Choose a model provider](providers-guide.md).

![The desktop Settings window on the Models page with the 21 pages in its left list.](../assets/screens/desktop-settings-light.png)

*Screenshot of the real desktop app on Linux. The window overlaps the main window because the capture ran without a window manager.*

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

These are wired into the desktop head and covered by unit and wiring tests with fake engines; none has run against a live omp session with a model, and each page names what is not wired. The row-by-row status is in the [parity audit](../quality/cursor-parity-audit-2026-10-28.md).

| Feature | State | Page |
|---|---|---|
| Hooks (`hooks.json`, 21 events) | Wired per session; a hook deny stops a tool call before the approval policy (`EnginePermissionOrderTests`) | [Hooks](../reference/hooks.md) |
| MCP servers from your files, and 13 presets | Wired; presets are added disabled from Settings > MCP servers | [MCP servers](../reference/mcp.md) |
| Run modes, `permissions.json`, `sandbox.json` | Wired to `--approval-mode`, a bash overlay and the permission order | [Run modes](../reference/run-modes.md) |
| `@` mentions, context ring, image attachments | Mentions reach the prompt as resources; images go as ACP image blocks when omp declares support | [Slash commands](../reference/slash-commands.md), [Concepts](concepts.md) |
| Checkpoints and rewind, worktree commands, queued messages, custom modes, side chats, fork | Mounted in the window; rewind restores files only | [Checkpoints and worktrees](../reference/checkpoints-and-worktrees.md) |
| Remote environments over ssh | Wired; a live loopback run passed; no remote file explorer, terminal pane, hook files or mentions | [Remote environments](../runbooks/remote-ssh.md) |
| Containers (podman) and WSL | Wired; a live podman test passed on Linux; WSL never ran | [Containers and WSL](../runbooks/remote-containers.md) |
| Several folders in one session | Workspace sets dialog and `--add-dir` | [Multi-repo](../runbooks/multi-repo.md) |
| Cloud agents on Railway | Wired when `AUTONOGRAMMER_API_BASE_URL` is set; fakes only; no deployed control plane | [Cloud agents](../runbooks/cloud-agents.md) |
| Automations | Settings page and cron scheduler; consent on the first run | [Automations](../reference/automations.md) |
| Local and cloud terminals | Built (Linux) | [Local terminal](../runbooks/local-terminal.md), [Cloud terminals](../runbooks/cloud-terminals.md) |

## About

The About dialog shows the licence (AGPL-3.0-or-later), the version, the source revision and the source URL, as the licence's network clause asks: [Licensing](../licensing.md).

## State and privacy

All state is under `AUTONOGRAMMER_STATE_DIR` (default `~/.local/share/Autonogrammer`): the session catalogue, per-session directories, settings and the provider key store (owner-only). The app writes outside its own directories only in two cases: omp's `models.yml` and credential database when you save a provider on the Models page, and workspace files when you press Keep, Undo or Apply review. App-started omp processes run without your global omp extensions. Details: [Run the desktop app](../runbooks/running-the-desktop-app.md).

## Limits

- Linux only. The Windows and macOS builds were not run.
- The browser pane, the Pets view and mobile terminals are not built or not wired: see the [parity audit](../quality/cursor-parity-audit-2026-10-28.md).
- No live model turn has been run since the bounded Phase 11 capture.
