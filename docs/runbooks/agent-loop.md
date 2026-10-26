# Agent loop runbook

How to run the OmpOS native head with the Ultrathink, Swarm, Substrate and GSD-Core plugins, how to check a launch without spending a model turn, and what is known not to work. Plugin facts: [docs/plugins.md](../plugins.md). Decision: [ADR-0012](../decisions/ADR-0012-agent-loop-plugins.md).

## Run the app with the plugins

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
git submodule update --init --recursive
dotnet run --project apps/OmpOS.Native
```

Prerequisites: `omp` on `PATH`, the pinned SDK (`./bootstrap.sh` installs it into `~/.dotnet`), `python3` >= 3.12 for Swarm, `node` >= 24 for GSD tools. Host requirements per plugin are in [plugins.md](../plugins.md#host-requirements).

`OMPOS_PLUGINS_ROOT` points OmpOS at a plugins directory. When it is unset, OmpOS looks for `plugins/` next to the repository root, discovered from the executable or the current directory:

```sh
OMPOS_PLUGINS_ROOT=$PWD/plugins dotnet run --project apps/OmpOS.Native
```

If a plugin is not initialised the panel still opens. Its stage shows Unavailable with the fix text, and sessions run as plain omp.

### GSD-Core probe

```sh
node plugins/gsd-core-omp/bin/gsd-omp-probe.mjs
```

Prints one JSON object (`schema: 1`) and exits:

| Exit code | Meaning |
| --------- | ------- |
| `0` | healthy |
| `2` | degraded: for example agent profiles not yet linked (`agents.missing` lists them) |
| `3` | GSD-Core is not installed |

The probe is offline. The only process it starts is `node <gsdHome>/bin/gsd-tools.cjs runtime-identity --raw` with a 5 s timeout. To create the agent links without starting omp: `node plugins/gsd-core-omp/bin/gsd-omp-link.mjs` (`--dry-run` previews).

## Plugin health

OmpOS resolves each plugin without executing it (it reads `package.json` `omp.extensions`) and reports one of four states.

| Health | Cause | Fix |
| ------ | ----- | --- |
| Ready | Directory present, manifest valid, entry files exist | none |
| SubmoduleNotInitialized | The `plugins/<name>` directory is empty or absent | `git submodule update --init --recursive plugins/<name>` |
| EntryMissing | An extension entry named by the manifest does not exist (wrong pin, partial checkout) | `git submodule status plugins/<name>`; re-run `git submodule update --init --recursive plugins/<name>`; check the pin with `git ls-remote <url> <sha>` |
| ManifestInvalid | `package.json` is unreadable or has no `omp.extensions` | Check the pin; do not edit the sibling inside OmpOS |

Stage symptoms:

| Symptom in the panel | Likely cause | Fix |
| -------------------- | ------------ | --- |
| Substrate Unavailable, private repository | No read access to `swcstudiospace/substrate` | Authenticate to GitHub with access to it, then initialise the submodule |
| Substrate Degraded, server unhealthy | `GET /healthz` failed or the token env var is unset | Start the substrate server; set `SUBSTRATE_TOKEN_OMP` or `SUBSTRATE_TOKEN_FILE` by name; OmpOS omits an unhealthy server so `session/new` still succeeds |
| Swarm Unavailable | `plugins/agent-swarm` missing | Initialise the submodule; check `python3 --version` |
| Ultrathink never leaves Running | Planning is still in progress, or no session file was written | Wait for the file under `<session>/ultrathink/sessions/`; there is no progress signal before it exists |
| GSD phase unknown | `node` older than 24 or GSD-Core not installed | `node --version`; `node plugins/gsd-core-omp/bin/gsd-omp-probe.mjs` and read `problems` |
| Plugin commands appear twice | Something added `--plugin-dir` to the launch | Remove it: use `-e` only |

## Verify a launch plan without a model turn

The plan is correct when `omp acp` completes `initialize` and `session/new` with the extension commands registered. This needs no credentials and sends no `session/prompt`. The method comes from the live probe in [dossier-omp-loading.md](../plans/agent-loop/dossier-omp-loading.md).

The probe script `/tmp/omp-probe/probe.mjs` was written for that investigation and is not committed to this repository. It is an ACP v1 NDJSON client:

```sh
node /tmp/omp-probe/probe.mjs <label> [--settle ms] [--env K=V ...] -- <omp acp flags>
```

It starts `omp acp` with `HOME` and `PI_CODING_AGENT_DIR` set to an empty sandbox under `/tmp/omp-probe` and the per-session variables from [plugins.md](../plugins.md#launch-contract), sends `initialize` and `session/new`, and records the commands and models reported. Environment overrides: `PROBE_CWD` (session working directory), `PROBE_NO_STRACE=1` (skip `strace`). It never sends `session/prompt`. Output goes to `/tmp/omp-probe/out.<label>.json`.

Example for the five extensions:

```sh
node /tmp/omp-probe/probe.mjs plan -- --no-extensions \
  -e $PWD/plugins/claude-ultrathink \
  -e $PWD/plugins/agent-swarm/omp \
  -e $PWD/plugins/agent-substrate/packages/projector/omp/substrate-status.ts \
  -e $PWD/plugins/agent-substrate/packages/projector/omp/substrate-skills.ts \
  -e $PWD/plugins/gsd-core-omp
```

Check in the output: `session/new` succeeded; the command list includes `swarm`, `substrate` and the `ultrathink-*` commands; no command is listed twice. omp ignores nonexistent `-e` paths silently, so a missing plugin shows up only as missing commands. Without credentials `initialize` reports `authMethods` `["agent"]` and `session/new` succeeds with zero models, which is enough to enumerate commands.

In the test suite the same check is the exact-argument and environment assertions on the launch planner; those need no omp process.

## Known limits

- No live Ultrathink progress. Its session file is written once planning finishes ([OQ-67](../open-questions.md)).
- ACP shows no widgets. Under `omp acp` extensions see `ctx.mode` `rpc` with a no-op UI context; `ui.notify` only reaches omp's debug log. Stage visibility comes from files and HTTP, so the Swarm status bar and Ultrathink insight bar never mount.
- Loopback model discovery. Every `omp acp` start probes `127.0.0.1:11434`, `:8080` and `:1234` (ollama, llama.cpp, lm-studio) and logs `model discovery failed`. Whether this can be disabled is UNKNOWN: read `src/config/model-discovery.ts` in the installed `@oh-my-pi/pi-coding-agent` package, then try a `--config` overlay.
- Substrate status extension polling. With `SUBSTRATE_URL` unset it polls `127.0.0.1:7410`. OmpOS omits both substrate extensions when Substrate is disabled or unhealthy.
- An intermittent outbound HTTPS connection (TCP/443 to Cloudflare addresses) was seen in 2 of about 45 probe runs and not reproduced in 13 reruns. Its purpose is UNKNOWN ([OQ-65](../open-questions.md)).
- Steering is recorded in Substrate, not delivered by it ([OQ-66](../open-questions.md)).
- Whether omp follows symlinked `agents/*.md` for `gsd-core-omp` is unconfirmed ([OQ-64](../open-questions.md)).
- Behaviour of `/ultrathink-status`, `/swarm` and `/substrate` when executed was not verified; only their registration was.
- Whether several concurrent `omp acp` processes share one `agent.db` safely was not stress-tested.
- Never run tests against the substrate on `127.0.0.1:7410`. Use fakes.
