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

## Layout (deviation from the dossier sketch)

Shared logic lives in `src/gsd-home.mjs` (plain ESM with JSDoc types) instead of `src/gsd-home.ts`, so `src/index.ts` (Bun, loaded by omp) and `bin/*.mjs` (plain Node 24) run the same code instead of duplicating it. `test/gsd-home.test.mjs` runs with `npm test` (`node --test "test/*.test.mjs"`; Node 24 needs the glob, not the directory).

## Licence

No `license` field in `package.json`: OmpOS has no licence yet. This is an owner decision; add the field once the repository licence is chosen. GSD-Core itself is MIT and is only referenced, never copied.
