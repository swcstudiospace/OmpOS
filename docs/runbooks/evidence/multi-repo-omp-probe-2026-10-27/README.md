# Multi-repo omp probe, 2026-10-27

Question: does `omp acp` honour additional workspace directories, through a launch flag, through the config overlay, and through
the slash commands `/add-dir`, `/remove-dir`, `/dirs` sent as an ACP prompt, without a model call?

Version under test: `omp/18.12.6` (the `agentInfo.version` in the `initialize` answer of every transcript).

## Method

`probe.py <scenario>` starts `omp acp <args>` and speaks ACP v1 over stdio (`initialize`, `session/new`, `session/prompt`).

Safety, so no model call is possible:

- every omp child runs under `unshare -rn` (an empty network namespace: nothing can leave the machine);
- `HOME=/tmp/multirepo-home`, `PI_CODING_AGENT_DIR=/tmp/multirepo-home/.omp/agent`, the only credential is the fake
  `ANTHROPIC_API_KEY=sk-fake-not-a-key`;
- the only prompts sent are slash commands (`/dirs`, `/add-dir`, `/remove-dir`); no free text ever reached the agent;
- the real `/root/.omp` and the substrate at 127.0.0.1:7410 were never touched.

Fixture folders: `/tmp/multirepo-fixture/{primary,b,c}`; omp starts with `cwd=primary`.

Scenarios (transcripts next to the script, config menus trimmed):

| Scenario | omp arguments after `acp` | Prompts | Transcript |
|---|---|---|---|
| `flag` | `--add-dir b --add-dir c` | `/dirs` | `transcript-flag.json` |
| `slash` | none | `/dirs`, `/add-dir b`, `/dirs`, `/remove-dir b`, `/dirs` | `transcript-slash.json` |
| `overlay` | `--config overlay.yml` with `workspace.additionalDirectories: [b, c]` | `/dirs` | `transcript-overlay.json` |
| `edge` | `--add-dir ../b --add-dir <missing> --add-dir b` | `/dirs`, `/add-dir b/nested`, `/add-dir <missing>`, `/add-dir ../c`, `/dirs` | `transcript-edge.json` |

## Result

(a) `omp acp --add-dir <path>` is accepted after `acp`, repeatable. `/dirs` answers
`Workspace directories:` with `<cwd> (working directory)` followed by every added directory in argument order. The session file
header (second line of the session JSONL) records `"additionalDirectories": [...]` next to `cwd`. `session/new` itself
returned nothing about directories: the only report is `/dirs` and the session header.

(b) `/add-dir <path>` as a `session/prompt` runs without a model call. The only update frame is one `agent_message_chunk`
(`Added <path>.` plus the directory list), followed by the `session/prompt` result `{"stopReason":"end_turn"}`.
`/remove-dir <path>` answers `Removed <path>.` plus the list; `/dirs` answers the list. `add-dir`, `remove-dir` and `dirs` are
in the `available_commands_update` frame omp sends after `session/new`.

(c) `workspace.additionalDirectories` in a `--config` overlay gives the same `/dirs` list and the same session header as the flag.

Edge behaviour (`edge`):

- a relative `--add-dir ../b` resolves against the process working directory and is de-duplicated against the same absolute path;
- a flag directory that does not exist is accepted silently and listed by `/dirs` (no validation at launch);
- `/add-dir <missing>` is refused: `Directory does not exist: <path>`;
- `/add-dir` accepts a directory nested inside an existing one (omp does not forbid nesting).

## Consequence for OmpOS

Use `--add-dir <absolute path>` pairs in the omp arguments (they are the launch mechanism, tested in
`OmpLaunchPlanner`), and never rely on omp to validate: OmpOS validates existence, duplicates and nesting itself
(`WorkspaceSetValidator`). Live add and remove in a running session can use `/add-dir` and `/remove-dir` prompts without a model
call; the reply text is the confirmation. Not probed: what omp does with `--add-dir` when a model turn runs (needs a model key and
owner approval, OD-07).
