# gsd-core-omp

Thin, OmpOS-owned omp plugin for the **installed** GSD-Core (`@opengsd/gsd-core`, Kimi layout at `~/.agents/gsd-core`). It copies nothing from GSD-Core.

GSD skills already reach omp through `~/.agents/skills`. What omp's `task` tool cannot see are GSD agent profiles that were never registered (14 of 35 on a stock install). On `session_start` the plugin symlinks those authoritative profiles (`<gsdHome>/agents/<name>.md`, never `*.compact.md`, never a name already present in the user's omp agents dir) into its own `agents/` directory, which omp's task discovery scans. The links are absolute and machine-local, so they are generated and gitignored.

It also registers the `/gsd-core` command, which prints the probe JSON.

## Install

Per launch, no change to `~/.omp`:

```
omp --plugin-dir /abs/path/to/plugins/gsd-core-omp
```

Persistent (symlinks into `~/.omp/plugins/node_modules`):

```
omp plugin link /abs/path/to/plugins/gsd-core-omp
```

To create the agent links without starting omp: `node bin/gsd-omp-link.mjs` (`--dry-run` previews; it prints one JSON object).

## Probe

```
node bin/gsd-omp-probe.mjs        # also: npm run probe
```

Prints one JSON object (`schema: 1`) and exits `0` healthy, `2` degraded, `3` GSD-Core not installed. Offline; the only process it spawns is `node <gsdHome>/bin/gsd-tools.cjs runtime-identity --raw` (5 s timeout). A probe before the links exist is degraded (`agents.missing` lists the unlinked profiles).

Fields: `gsdHome`, `source`, `packageName`, `version`, `runtime`, `node`, `gsdTools`, `skills{dir,gsdCount}`, `agents{authoritative,userRegistered,pluginLinked,missing}`, `omp{pluginLinked,enabled}` (read from `~/.omp/plugins`), `problems[]`.

## Locating GSD-Core

First match wins: `GSD_CORE_HOME` (explicit override, always honoured), `$KIMI_CONFIG_DIR/gsd-core`, `~/.config/agents/gsd-core`, `~/.agents/gsd-core`. The user agents dir is `$PI_CODING_AGENT_DIR/agents`, else `~/.omp/agent/agents`.

## Degradation

Missing GSD-Core: one warning on session start (`npx @opengsd/gsd-core --kimi --global` or set `GSD_CORE_HOME`), no agents linked, nothing thrown. Existing files or foreign symlinks in `agents/` are never overwritten (reported as conflicts), and a profile whose real path leaves `<gsdHome>/agents` is refused.

## OmpOS host features (steering inbox and connection report)

The plugin is loaded (`-e`) into every omp process OmpOS starts, so the two features below are inert until OmpOS sets their environment variable: with it unset (or not an absolute path) they make no fs call, start no timer and write nothing. They start for the top-level session only (`ctx.agent.kind !== "sub"`), never for subagents, and stop on `session_shutdown`.

**Silence rule.** The plugin never writes to stdout or stderr (the TUI owns the screen and ACP owns stdout), never throws into omp, and swallows every fs or delivery failure: a failing disk becomes a `failed` ack or a skipped write.

### Steering inbox (`OMPOS_STEER_DIR`)

**Who uses the inbox.** OmpOS steers a chat (ACP) session through omp's own `_session/steering` request when omp advertises it (`_meta.steering.supported` in `initialize`), so the inbox is not used for those sessions. The inbox is for omp terminals, which have no ACP, and it is the fallback for an omp whose `initialize` lacks the capability (ADR-0012, update 2026-10-26; OQ-81). The paragraph below says why a prompt cannot carry a steer and how the inbox delivers one.

omp's ACP agent cancels a running turn when a new `session/prompt` arrives, so live steering cannot be sent as a prompt. The inbox hands text to the running session with `pi.sendUserMessage(text, { deliverAs })`, where `deliverAs` is the directive's `mode` unchanged (omp 18.12: `steer` queues at the next boundary while streaming, `followUp` runs after the current turn, `aside` injects at the next step boundary without interrupting a running tool batch; an idle session starts a turn). `nextTurn` exists for `sendMessage` only and is not offered.

Layout under the absolute directory `OMPOS_STEER_DIR`:

| Path | Writer | Content |
| --- | --- | --- |
| `inbox/<id>.json` | OmpOS (tmp file + rename; `*.tmp` is ignored) | directive |
| `acks/<id>.json` | plugin (atomic, mode 0600; directories 0700) | ack |
| `done/<id>.json` | plugin | the directive file, moved here before delivery (a crash can lose a directive but never repeat it) |
| `alive.json` | plugin, every 5 s (atomic) | `{"v":1,"pid","startedAt","lastSeen","mode"}`; `mode` is omp's run mode (`tui`/`rpc`/`json`/`print`) |

Directive v1: `{"v":1,"id":"[A-Za-z0-9_-]{1,64}","mode":"steer"|"followUp"|"aside","text":string,"createdAt":ISO-8601,"source":"ompos"}`. `id` must equal the file name stem; `text` is non-blank and at most 8192 UTF-8 bytes; the file is at most 16 KiB. Symlinks and non-regular files are ignored; at most 64 regular files are examined per pass (the rest wait), in file-name order. The inbox is watched with `fs.watch` plus a 500 ms poll fallback (both unref'd and cleared on stop).

Ack v1: `{"v":1,"id","status":"delivered"|"rejected"|"failed","reason":null|"<code>","mode":<mode|null>,"at":ISO,"idle":true|false|null}`. Reason codes: `invalid-json`, `invalid-schema`, `too-large`, `bad-id`, `duplicate`, `blank-text`, `deliver-error`. `idle` is `ctx.isIdle()` at delivery (null when unknown). A duplicate id (already in `acks/` or `done/`) is never delivered twice; the first outcome stays authoritative, so `rejected`/`duplicate` is written only when no ack exists yet. A file whose name is not a valid id cannot carry an ack and is moved to `done/invalid-*.json`. OmpOS treats `alive.json` with `lastSeen` older than 15 s as "not serving" and must not rely on the inbox then.

### Connection report (`OMPOS_CONNECTION_FILE`)

Absolute file path. The plugin writes (atomic, mode 0600) `{"v":1,"at":ISO,"pid","mode":string|null,"omp":{"version":null},"tools":[sorted names from pi.getAllTools()],"commands":[sorted names from pi.getCommands()],"commandsAvailable":boolean}`. The report lists raw names only: the host (C#) owns the expected-name table per plugin and decides which plugins are connected. `omp.version` is always `null` because the extension API does not expose it; OmpOS reads it from `omp --version`. `commandsAvailable` is `false` (and `commands` empty) if the API lacks `getCommands`.

omp emits `session_start` after every extension factory has run, so the first report is written there. Tools that register later (MCP) are caught by re-checks on `before_agent_start` and a 1 s one-shot timer after `session_start`; the file is rewritten (same path) only when the tool or command list changed, at most once per 2 s (a change inside the gap is rewritten when the gap ends).

## Layout (deviation from the dossier sketch)

Shared logic lives in `src/gsd-home.mjs` (plain ESM with JSDoc types) instead of `src/gsd-home.ts`, so `src/index.ts` (Bun, loaded by omp) and `bin/*.mjs` (plain Node 24) run the same code instead of duplicating it. `test/gsd-home.test.mjs` runs with `npm test` (`node --test "test/*.test.mjs"`; Node 24 needs the glob, not the directory).

## Licence

`package.json` declares `AGPL-3.0-or-later`, the licence of OmpOS (see `LICENSE` at the repository root). GSD-Core itself is MIT and is only referenced, never copied.
