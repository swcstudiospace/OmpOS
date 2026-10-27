# Running the desktop app

How to run the OmpOS desktop head (`apps/OmpOS.Native`) on Linux, where it keeps its state, and what it writes outside its own directories. Status of each feature: [docs/quality/cursor-parity-audit-2026-10-26.md](../quality/cursor-parity-audit-2026-10-26.md). Plugin details: [agent-loop.md](agent-loop.md), [docs/plugins.md](../plugins.md).

## Prerequisites

- `omp` on `PATH`. No omp process starts until you send a prompt or open an omp terminal.
- The pinned .NET SDK (`./bootstrap.sh` installs it into `~/.dotnet`).
- The plugin submodules (`git submodule update --init --recursive`). A missing or uninitialised plugin does not stop the application: its stage shows Unavailable with the fix text and sessions run as plain omp.
- Node 20 or newer to run the plugin tests; `python3` 3.12 or newer for Swarm and a newer node for GSD tools, per [agent-loop.md](agent-loop.md).
- Optional: a desktop session (X11 or XWayland). Under a bare server use `xvfb-run`, as the screenshot recipe in `docs/runbooks/evidence/ui-parity-2026-10-26/README.md` does.

## Start

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet run --project apps/OmpOS.Native
```

The workspace root is the current directory; there is no open-folder action. The window opens at once in a Loading state and the plugins and saved sessions load behind it.

## Environment variables

Read once at startup by `AgentLoopEnvironment` (`apps/OmpOS.Native/Agents/AgentLoopEnvironment.cs`). Paths may be relative to the current directory.

| Variable | Effect |
|---|---|
| `OMPOS_STATE_DIR` | Root of all application state. Default `$XDG_DATA_HOME/OmpOS` when `XDG_DATA_HOME` is set and absolute, else `~/.local/share/OmpOS`. |
| `OMPOS_PLUGINS_ROOT` | The plugins directory. Default: `plugins/` beside the nearest directory containing `.gitmodules`, searched from the executable and then from the current directory. |
| `OMPOS_SUBSTRATE_URL` | Substrate server address. Must be https, or http on a loopback host, with no user information, query or fragment; otherwise it is not used and the reason is shown. Unset: the Substrate is not used. |
| `OMPOS_SUBSTRATE_TOKEN_ENV` | Names the variable the Substrate token is read from. Default `SUBSTRATE_TOKEN_OMP`. Only the name is kept; the value is read on demand and never printed. |

Also read for the token: `SUBSTRATE_TOKEN_FILE` (first non-empty line of that file) and `SUBSTRATE_TOKENS_DIR`. Live tests use `OMPOS_LIVE_OMP=1`.

## State directory layout

Under the state root:

| Path | What it holds |
|---|---|
| `sessions.json` | The session catalog (`AgentSessionRegistry`). A corrupt file is quarantined and the catalog starts empty. |
| `sessions/<session id>/` | Private state of one session (`SessionStateLayout`): `ultrathink/`, `swarm/` (with `.swarm/live.json`, `events.jsonl`, `gsd-state.md`), `claude-state/`, `substrate-pending/`, `steer/` (steering inbox), `connection/report.json`, `omp-overlay.yml`. |
| `config/settings.json` | The head's preferences (`FileSettingsStore`): theme, motion, terminal auto-run, idle stop and retention. Written atomically with mode 0600 in a 0700 folder. Never holds a key. |
| `secrets/secrets.json` | The OmpOS provider key store (`OwnerOnlyFileSecretStore`): a plain-text, owner-only file that holds the key of a custom provider under its variable name. |

## Where provider keys live (OQ-92)

omp is the engine, so providers and credentials are omp's. The Models page edits only OmpOS-owned blocks of omp's `models.yml` and keeps a custom provider's key in `secrets/secrets.json`, passed to omp as an environment variable. A built-in provider's key is imported with `omp auth import -` over standard input and stored by omp in its `agent.db` (unencrypted, owner-only). OmpOS keeps no copy of that key. The platform secure store is not used.

## What the app writes outside its own directories

- omp's `models.yml` (in omp's agent directory, normally `~/.omp/agent/`): only when you save a custom provider on the Models page. The edit keeps comments, makes a timestamped backup, writes atomically and is validated by omp.
- omp's credential database (`agent.db`): only when you add a built-in provider's key on the Models page.
- Nothing else outside the state directory, the workspace folder and the logs on standard error. The Changes bar writes files in the workspace folder when you press Keep, Undo or Apply review.

App-started omp processes are launched without your global omp extensions (`--no-extensions`, OQ-83) and with the four plugins; the plugins are always on (OQ-80).

## Keyboard shortcuts

The Keyboard page of Settings (Ctrl+,) lists the shortcuts the shell handles, grouped by scope, from `ShortcutCatalogue`; each is raised by `ShortcutCatalogueTests`. Remapping is not built. The main ones:

| Scope | Gesture |
|---|---|
| Application | Ctrl+, Settings; F6 and Shift+F6 next and previous region; Ctrl+1 agent list; Ctrl+2 prompt; Ctrl+F search the transcript; Esc dismiss |
| Agents | Ctrl+N new agent; Ctrl+Shift+T new omp terminal; Ctrl+Shift+A Agents tab; Enter open, Delete close the selected agent |
| Composer | Enter send; Shift+Enter new line; Ctrl+Enter steer now; Ctrl+Shift+Enter follow up; Esc stop the run |
| Kanban | Ctrl+Shift+K Kanban tab; arrow keys move between cards; Enter opens a card; S steers a card |
| Settings | Esc or Ctrl+W close; Home and End first and last page |

Typing `/` in the composer opens the slash palette of the commands omp advertises: arrow keys move, Enter or Tab complete the name, Esc closes. Settings rows and their status: [docs/settings-parity.md](../settings-parity.md).

## Not covered here

Windows, macOS, Android, native Wayland and hosted deployments were not run. No live model turn has been run since the bounded Phase 11 run (OD-07).
