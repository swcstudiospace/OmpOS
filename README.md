![OmpOS](assets/brand/ompos-banner.png)

**OmpOS is an agent workspace for desktop, web and Android on the omp engine, with the Cursor feature set under an open licence.**

[![License: AGPL-3.0-or-later](https://img.shields.io/badge/license-AGPL--3.0--or--later-6d28d9)](LICENSE)
[![CI](https://github.com/swcstudiospace/OmpOS/actions/workflows/ci.yml/badge.svg)](https://github.com/swcstudiospace/OmpOS/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10-512bd4)](global.json)
[![Avalonia 12](https://img.shields.io/badge/Avalonia-12-6d28d9)](https://avaloniaui.net/)
[Docs: docs/README.md](docs/README.md)

# OmpOS

OmpOS is a Cursor-style agents workspace whose engine is **omp**. The desktop head (`apps/OmpOS.Native`, Avalonia) opens on an Agents window: a session rail, a thread and a composer, plus a Kanban tab with one card per opened session. Every omp process the application starts, a chat session through `omp acp` or an embedded omp terminal, is launched with four plugins: Ultrathink, Substrate, Swarm and GSD-Core. A running session can be steered live from the composer or from its Kanban card. The Settings window (Ctrl+,) configures model providers through omp's own configuration. The application starts in the Dark theme and can follow System or Light.

The same repository holds a Blazor WebAssembly web head, an Android head, an ASP.NET Core API (session runs execute in-process; no worker service) and sandboxed agent execution. The desktop head is the one that is built out; see Status.

## Prerequisites

- `bash`, `curl`, `git`, `python3` on `PATH`.
- `omp` on `PATH` (the engine). The application starts no omp process until you send a prompt or open an omp terminal.
- The plugin submodules: `git clone --recurse-submodules`, or `git submodule update --init --recursive` in an existing clone. `plugins/agent-substrate` is private; without access that plugin reports Unavailable and sessions still run.
- Node 20 or newer for the plugin tests (`scripts/ci/plugin-node-tests.sh`); GSD tools need a newer node, see [docs/runbooks/agent-loop.md](docs/runbooks/agent-loop.md).
- .NET is not a prerequisite: `./bootstrap.sh` installs the SDK pinned in `global.json` into `~/.dotnet` when it is missing.
- `podman` plus `podman-compose` are optional (only the local sandbox provider needs them). Podman is the only supported container runtime.

## Run the desktop head (Linux)

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
git submodule update --init --recursive
dotnet run --project apps/OmpOS.Native
```

The workspace root is the current directory. The head reads these environment variables (`apps/OmpOS.Native/Agents/AgentLoopEnvironment.cs`):

| Variable | What it does |
|---|---|
| `OMPOS_STATE_DIR` | Root of all application state (session catalog, per-session directories, settings, provider key store). Default: `$XDG_DATA_HOME/OmpOS`, else `~/.local/share/OmpOS`. |
| `OMPOS_PLUGINS_ROOT` | The plugins directory. Default: `plugins/` next to the repository root (found from the executable or the current directory). |
| `OMPOS_SUBSTRATE_URL` | Address of the Substrate server (https, or http on a loopback host). Unset means the Substrate is not used. |
| `OMPOS_SUBSTRATE_TOKEN_ENV` | The NAME of the variable the Substrate token is read from (default `SUBSTRATE_TOKEN_OMP`). The token itself is never logged or stored. |

More detail, including the state directory layout and what the application writes outside its own directories: [docs/runbooks/running-the-desktop-app.md](docs/runbooks/running-the-desktop-app.md).

## Bootstrap and the gate

```sh
./bootstrap.sh
```

One command builds and tests everything from a clean clone. It installs the pinned SDK when missing, runs the pinned secret scan (`scripts/ci/secret-scan.sh`, which fails closed), the node tests of `plugins/gsd-core-omp`, a locked restore, a warnings-as-errors Release build, the test suite (`Category=Live` excluded), `dotnet format --verify-no-changes`, and the web smoke with its axe check. Any failing step exits non-zero. See [docs/runbooks/local-dev.md](docs/runbooks/local-dev.md).

- Run the tests: `dotnet test OmpOS.Linux.slnf -c Release`.
- Verify formatting: `dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore`.
- Live tests against the real omp (no model turn) are opt-in: `OMPOS_LIVE_OMP=1`, trait `Category=Live`.

## Status

The Agent Panel, the Kanban tab, omp terminals with the four plugins, live steering, the Settings window with model providers and the Cursor-style visual parity (Dark and Light) are built and verified on Linux without a model turn. Not built or not wired into a head: the browser pane, hooks, automations, the plugin host, herdr, the MCP gateway, the Pets view, remote and mobile terminals and usage history. No live model turn has been run since the bounded Phase 11 run, and nothing was run on Windows, macOS, Android or a hosted deployment. The row-by-row audit is [docs/quality/cursor-parity-audit-2026-10-26.md](docs/quality/cursor-parity-audit-2026-10-26.md); the planning records are in `.planning/`.

## Repo map

| Path | Contents |
| ---- | -------- |
| `OmpOS.slnx` | Solution (all projects, including the Android head) |
| `OmpOS.Linux.slnf` | Solution filter for Linux and CI (everything except the Android head) |
| `Directory.Build.props` | Compiler, analyzer, determinism and lock-file policy for every project |
| `Directory.Packages.props` | Central package versions |
| `global.json` | Pinned .NET SDK plus the test runner mode |
| `.editorconfig` | Single rule source for formatting and code style |
| `bootstrap.sh` | One-command bootstrap and gate |
| `.env.example` | Every configuration variable of the API, documented |
| `src/` | Libraries (`Contracts`, `Parser`, `Harness.*`, `Sandbox`, `Sessions`, `GenUi`, `Presentation`, `Native.Views`, `UI`, ...) |
| `services/` | Hosts (`Api`) |
| `apps/` | Heads: `Native` (Avalonia desktop), `Web` (Blazor WebAssembly), `Android` (Avalonia) |
| `plugins/` | The four omp plugins: submodules `claude-ultrathink`, `agent-swarm`, `agent-substrate`, and the tracked `gsd-core-omp`; see [docs/plugins.md](docs/plugins.md) |
| `scripts/` | CI scripts (`scripts/ci/`: secret scan, plugin node tests), web smoke, Vercel helpers |
| `tests/` | Per-project suites, contract fixtures and `ArchitectureTests` |
| `docs/` | Architecture, ADRs, open questions, plans, runbooks |
| `docs/quality/` | The quality bar and the Cursor parity audit |
| `docs/runbooks/` | Operator and developer runbooks |
| `docs/runbooks/evidence/` | Recorded runs and screenshots (for example `ui-parity-2026-10-26/`, `phase-48-omp-plugins-2026-10-26/`) |
| `.planning/` | Roadmap, requirements, state and per-phase records |
| `deploy/` | `compose.yaml` for podman-compose |
| `.github/workflows/` | `ci.yml` (build, test, format, audit, scan, compose, Android) |

## What is verified where

| Surface | Verifier | Scope |
| ------- | -------- | ----- |
| Linux (CI pool) | `ci.yml` job `build-test` | Everything in `OmpOS.Linux.slnf`, the plugin node tests, the secret scan and the web smoke |
| Linux dev box | `./bootstrap.sh` | The same scope; the Android head is outside the Linux filter |
| Linux (CI pool) | `ci.yml` job `android` | `apps/OmpOS.Android`: restore and `-warnaserror` build; no device or emulator |
| Windows, macOS, iOS | none | Not built or run; they need a Windows 11 runner and the Mac channel (OQ-69, OQ-74) |

Restoring the full `OmpOS.slnx` needs the .NET Android workload (`dotnet workload install android`); the Linux filter does not.

## Docs

- [docs/runbooks/running-the-desktop-app.md](docs/runbooks/running-the-desktop-app.md): run the desktop head, state layout, shortcuts.
- [docs/runbooks/agent-loop.md](docs/runbooks/agent-loop.md): the four plugins, health and probes.
- [docs/quality/cursor-parity-audit-2026-10-26.md](docs/quality/cursor-parity-audit-2026-10-26.md): what is built, wired and not done.
- [docs/settings-parity.md](docs/settings-parity.md): the 34 Cursor settings rows and the eleven Settings pages.
- [docs/architecture.md](docs/architecture.md), [docs/decisions/](docs/decisions/): layers and decision records.
- [docs/open-questions.md](docs/open-questions.md): every unverified external fact, with owner.
- [docs/runbooks/local-dev.md](docs/runbooks/local-dev.md), [docs/runbooks/sandbox.md](docs/runbooks/sandbox.md): bootstrap, local dependencies, sandbox providers.
- [CONTRIBUTING.md](CONTRIBUTING.md): branches, commits, formatting, lock files, TODO policy.

## License

OmpOS is licensed under the GNU Affero General Public License, version 3 or (at your option) any later version (SPDX: `AGPL-3.0-or-later`). Copyright (C) 2026 SWC Studio. See [LICENSE](LICENSE) for the full text and [NOTICE](NOTICE) for the notice and third-party pointers; third-party licences are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Network use is distribution for this licence (AGPL section 13): if you run a modified OmpOS as a service that other people use over a network, you must offer those users the Corresponding Source of your version. OmpOS makes this easy: the API answers an anonymous `GET /source` with the licence, the source URL and the exact revision, and the web and desktop About pages link to the same source at <https://github.com/swcstudiospace/OmpOS>. Details for users and operators: [docs/licensing.md](docs/licensing.md).

Contributions are accepted under the same licence with a Developer Certificate of Origin sign-off (`git commit -s`); see [CONTRIBUTING.md](CONTRIBUTING.md). The plugin submodules under `plugins/` and the omp engine are separate programs with their own terms.
