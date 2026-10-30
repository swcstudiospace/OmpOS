---
description: "Install, build and run Autonogrammer: the desktop app on Linux, the web head against a local API, and the Demo with a simulated engine."
---

# Getting started

Status: Built on Linux (desktop and web run from a clean clone); Windows, macOS, Android and a hosted deployment of the API were not run

Last verified: 2026-10-28

Applies to: v0.15.0

Autonogrammer is a workspace for coding agents whose engine is [omp](https://github.com/can1357/oh-my-pi). You open the **desktop app** or the **web app**, start agents, watch them work, steer them and review their file changes. This page gets you to a running app. Pick the path that fits.

| I want to | Go to |
|---|---|
| Use the desktop app on Linux | [Desktop](#desktop-app-linux) |
| See the web app without any backend | [Demo](#the-demo-no-backend) |
| Run the web app against a local API | [Web with a local API](#web-app-with-a-local-api) |
| Build and test the whole repository | [Local development](../runbooks/local-dev.md) |

> **Be aware.** No live model turn has been run since a bounded capture in Phase 11. Everything below starts, renders and exchanges messages with the real omp, but "an agent finished a real coding task with a model" is not something the project has demonstrated. The web Demo uses a simulated engine and says so on screen. Nothing was run on Windows, macOS or Android, and neither the API nor a cloud agent a user can start is deployed to Railway (a private staged worker is, see [Cloud agents](../runbooks/cloud-agents.md)).

## Requirements

| Need | For | Notes |
|---|---|---|
| `bash`, `curl`, `git`, `python3` on `PATH` | everything | |
| `omp` on `PATH` | desktop app, real API runs | The engine. The app starts no omp process until you send a prompt or open an omp terminal. |
| Plugin submodules | desktop app | `git clone --recurse-submodules`, or `git submodule update --init --recursive`. `plugins/agent-substrate` is private: without access that plugin reports Unavailable and sessions still run. |
| .NET SDK 10.0.402 | building from source | `./bootstrap.sh` installs the SDK pinned in `global.json` into `~/.dotnet` when it is missing. |
| Node 20 or newer | plugin tests only | GSD tools need a newer node, see [Agent loop](../runbooks/agent-loop.md). |
| `podman` and `podman-compose` | local sandbox provider only | Podman is the only supported container runtime. |

You also need access to at least one model provider for real work: see [Choose a model provider](providers-guide.md).

## Desktop app (Linux)

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
git submodule update --init --recursive
dotnet run --project apps/Autonogrammer.Native
```

The workspace root is the current directory. The app opens on the Agents window; the default theme is Light (System and Dark are in Settings, General). Press Ctrl+, for Settings. The full tour is the [Desktop app guide](desktop-app-guide.md); state, environment variables and shortcuts are in [Run the desktop app](../runbooks/running-the-desktop-app.md).

The main environment variables:

| Variable | What it does |
|---|---|
| `AUTONOGRAMMER_STATE_DIR` | Root of all application state: session catalogue, per-session directories, settings, provider key store. Default `$XDG_DATA_HOME/Autonogrammer`, else `~/.local/share/Autonogrammer`. |
| `AUTONOGRAMMER_PLUGINS_ROOT` | The plugins directory. Default: `plugins/` next to the repository root. |
| `AUTONOGRAMMER_SUBSTRATE_URL` | Address of the Substrate server (https, or http on a loopback host). Unset means the Substrate is not used. |
| `AUTONOGRAMMER_SUBSTRATE_TOKEN_ENV` | The name of the variable the Substrate token is read from (default `SUBSTRATE_TOKEN_OMP`). |

## The Demo (no backend)

The quickest look: open <https://ompos-web.vercel.app>. It is the web head with its HTTP client answered inside the browser by a scripted engine (a static Vercel deployment, recorded 2026-10-28). It needs no API, no omp and no model, and nothing you type leaves the page.

To build and serve the same bundle yourself:

```sh
scripts/vercel/build-demo.sh        # publishes with -p:AutonogrammerDemo=true into output/demo
scripts/web-demo/serve-demo.sh      # http://127.0.0.1:5190/ (loopback only)
```

It shows a workspace named `acme-shop` and six scripted agents in different states. Sending a prompt gets a reply labelled "Demo". Details and limits: [Web head runbook](../runbooks/web-demo.md) and [Web app guide](web-app-guide.md).

![The Autonogrammer web app in the Demo build: the agent rail, a streaming thread and the composer, in the Light theme.](../assets/screens/web-agents-light.png)

*Screenshot of the Demo build: a simulated engine produces the thread.*

## Web app with a local API

The API refuses the development principal and the in-memory harness outside Development, so this setup is loopback only. The in-memory harness answers with an echo, not a model.

```sh
ASPNETCORE_ENVIRONMENT=Development Harness__Adapter=InMemory Sessions__Store=InMemory Coordination__Store=InMemory \
  Api__Auth__AllowDevelopmentPrincipal=true Api__Cors__Origins__0=http://127.0.0.1:5180 \
  dotnet run --project services/Autonogrammer.Api --urls http://127.0.0.1:5187
dotnet publish apps/Autonogrammer.Web -c Release -o /tmp/autonogrammer-web
python3 -m http.server 5180 --bind 127.0.0.1 --directory /tmp/autonogrammer-web/wwwroot
```

Open `http://127.0.0.1:5180/`. The page reads its API origin from `wwwroot/appsettings.json` (`Api:BaseUrl`, default `http://127.0.0.1:5187`).

## Build and test everything

```sh
./bootstrap.sh
```

One command installs the pinned SDK when missing, runs the secret scan, the plugin node tests, a locked restore, a warnings-as-errors Release build, the tests (live tests excluded), `dotnet format --verify-no-changes` and the web smoke with an axe accessibility check. See [Local development](../runbooks/local-dev.md).

## Android

The Android head (`apps/Autonogrammer.Android`) builds in CI with warnings as errors. It has not been run on a device or emulator: see [Android and iOS terminals](../runbooks/android-ios-terminals.md).

## Where next

- [Concepts](concepts.md): sessions, agents, the engine and the four plugins.
- [Choose a model provider](providers-guide.md) and the [Settings guide](settings-guide.md).
- [Troubleshooting and FAQ](troubleshooting-and-faq.md).
- [Status and roadmap](status-and-roadmap.md): what is built, partial or blocked.
