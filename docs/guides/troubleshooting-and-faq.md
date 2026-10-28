---
description: "Troubleshooting and frequently asked questions for OmpOS: starting the app, plugins, providers and keys, the web Demo, remote and cloud agents, and what is not verified."
---

# Troubleshooting and FAQ

Status: Built (symptoms and fixes below are taken from the runbooks and the code); a symptom you hit that is not listed belongs in an issue

Last verified: 2026-10-28

Applies to: v0.15.0

## Frequently asked questions

**What is OmpOS?** A workspace for coding agents on the omp engine, with a desktop app, a web app, cloud agents and remote environments. See [Getting started](getting-started.md) and [Concepts](concepts.md).

**Does OmpOS include a model?** No. omp talks to the provider you configure: [Choose a model provider](providers-guide.md). OmpOS does not sell, proxy or resell inference.

**Is it a Cursor replacement?** It targets the Cursor 3.27 feature set under an open licence, and the [parity audit](../quality/cursor-parity-audit-2026-10-28.md) lists, row by row, what is built, partial or left out. Cursor's accounts, teams and billing are left out on purpose.

**Has it run a real model task?** No live model turn has been run since the bounded Phase 11 capture. The desktop app, the plugins and the omp connection were checked against the real omp without a model turn. Treat everything else as built and tested, not field-proven.

**Which platforms work?** The desktop app on Linux. Windows, macOS, Android and iOS were not run. The web head runs in a browser; its Demo needs nothing else.

**What is the Demo?** The web app answered by a scripted engine inside the browser. It is labelled "Demo", calls no model and keeps nothing. See [Web app guide](web-app-guide.md).

**What licence is it?** AGPL-3.0-or-later. If you run a modified OmpOS as a service, you must offer your users the source: [Licensing](../licensing.md).

**Is the documentation site published?** Not until the owner connects GitBook and a live URL is confirmed: [Publish the docs with GitBook](../runbooks/gitbook-publish.md). The Markdown in `docs/` is readable on GitHub now.

## Starting the app

| Symptom | Likely cause | Fix |
|---|---|---|
| `dotnet` not found | The SDK is not installed or not on `PATH` | Run `./bootstrap.sh` (installs the SDK pinned in `global.json` into `~/.dotnet`), then `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH`. |
| `omp` not found when you send a prompt | The engine is not installed or not on `PATH` | Install omp and check `omp --version`. The app starts no omp process until you send a prompt or open an omp terminal. |
| The app starts but there is no state where you expect it | `OMPOS_STATE_DIR` is set or XDG paths differ | State is under `$OMPOS_STATE_DIR`, else `$XDG_DATA_HOME/OmpOS`, else `~/.local/share/OmpOS`. |
| Restore or build fails after changing a package | Lock files out of date | `dotnet restore --force-evaluate` and commit the lock files. See [Local development](../runbooks/local-dev.md). |

## Plugins

| Symptom | Likely cause | Fix |
|---|---|---|
| A plugin shows Unavailable | The submodule is not initialised | `git submodule update --init --recursive plugins/<name>`. |
| Substrate Unavailable, private repository | No read access to `swcstudiospace/substrate` | Authenticate to GitHub with an account that can read it, then initialise the submodule. Sessions still run without it. |
| Substrate Degraded | Its server is unhealthy, or the token variable is unset | Start the server and set the variable named by `OMPOS_SUBSTRATE_TOKEN_ENV` (default `SUBSTRATE_TOKEN_OMP`). |
| GSD phase unknown | `node` older than 24 or GSD-Core missing | `node --version`; `node plugins/gsd-core-omp/bin/gsd-omp-probe.mjs`. |
| Ultrathink never leaves Running | Planning is in progress | Wait: there is no progress signal before its session file is written. |

More in the [Agent loop runbook](../runbooks/agent-loop.md).

## Providers and keys

| Symptom | Likely cause | Fix |
|---|---|---|
| Models page shows a provider but the agent cannot answer | No key, or the wrong host (plan host versus pay-as-you-go host) | Check the row's endpoint and key variable name on the page; read the vendor's current page. |
| A key typed in the web app is gone after reload | By design: kept in page memory only | Enter it again; see the [threat model](../runbooks/web-key-threat-model.md). |
| API answers 403 `credential_writes_disabled` | Writes are off by default | The operator sets `Providers:AllowCredentialWrite`. |
| A Partial or Unverified catalogue row fails | Some fact in the row was inferred | Verify on the vendor's page; the row links it. |

## Web Demo and local API

| Symptom | Likely cause | Fix |
|---|---|---|
| The API refuses to start | The development principal and in-memory harness are only allowed in Development | Use the command in [Getting started](getting-started.md). |
| The web page cannot reach the API | CORS origin or API origin mismatch | `Api__Cors__Origins__0` must be the page origin; the page reads `Api:BaseUrl` (or `?apiBaseUrl=` on loopback). |
| Replies are echoes | The in-memory harness is a scripted echo, not a model | Expected. |
| A Demo page shows no real data | The Demo is simulated | Expected: see the banner. |

## Remote and cloud

| Symptom | Likely cause | Fix |
|---|---|---|
| A remote profile fails preflight | `ssh` cannot reach the host, host key not trusted, or no omp there | Each diagnosis is explained in [Remote environments](../runbooks/remote-ssh.md). |
| Cloud agents do nothing | `Cloud:Enabled` is false by default and no worker exists | See [Cloud agents](../runbooks/cloud-agents.md): nothing is deployed on Railway. |

## Docs checks

| Symptom | Fix |
|---|---|
| `scripts/ci/check-docs.sh` reports an orphan page | Add the page to `docs/SUMMARY.md` (or put it under an ignored tree such as `docs/plans/`). |
| A pinned screenshot is missing | Warning only; `OMPOS_RELEASE=1` makes it an error. The release step adds the images. |
| GitBook shows a page with raw front matter | An unquoted `description:` containing `:` or `#` fails silently in Git Sync: quote it. |

If none of this helps, see [Contributing, security and support](community.md).
