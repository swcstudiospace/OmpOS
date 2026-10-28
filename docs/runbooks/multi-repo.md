---
description: "Multi-repository workspaces: several folders in one session, validation, omp launch, one worktree per git root and .code-workspace import and export."
---

# Multi-repo workspaces runbook

Status: Partial (core library and tests are built; the head picker is Wave 3 work, see the parity audit; no live run)

Last verified: 2026-10-28

Applies to: v0.15.0

Scope: Phase 59 part 1 (PX-16), the core. One session can cover several folders: validated roots, omp launched with all of them, one git worktree per git root for a run, a multi-root explorer, and `.code-workspace` import and export. Status as of 2026-10-27. Native views for the picker are the next wave.

## How it fits together

| Piece | Where | What it does |
| --- | --- | --- |
| `WorkspaceSet`, `WorkspaceSetRoot` | `src/OmpOS.Harness.Workspace/WorkspaceSet/` (namespace `OmpOS.Workspace.Sets`) | The ordered folders of a session. The first root is the primary: omp's working directory. Every other root is an additional directory. A set is immutable (`AddFolder`, `Remove`, `WithPrimary`, `Rename` return new sets). |
| `WorkspaceSetValidator` | same | Errors: more than 16 roots, relative or NUL path, missing local folder, duplicate or nested roots (compared by real path, so two links to one folder are one root), roots in different environments, blank or repeated names, repeated ids. Warning: a local root that is not the top of a git working tree. |
| `CodeWorkspaceFile` | same | Reads and writes VS Code and Cursor `.code-workspace` files. |
| `WorkspaceSetStore` | same | The 20 most recent sets in `OmpOS/workspace-sets.json` under the per-user config directory (a versioned JSON file; a damaged file reads as empty and is moved aside). |
| `OmpLaunchPlanner.Plan(..., additionalDirectories)` | `src/OmpOS.Harness.AgentLoop/Launch/` | Emits one `--add-dir <absolute path>` pair per additional root, after every other argument. `OmpLaunchPlan.AdditionalDirectories` records them. |
| `SessionEngineSet.CreateSlot(..., additionalDirectories)` | `apps/OmpOS.Native/Agents/` | Keeps the list for the slot and passes it to the planner. |
| `WorktreeRunSet` | `src/OmpOS.Harness.Worktrees/` | One worktree per git root for a run, created and closed as a unit. |
| `CursorWorktreesConfig` | same | Reads `.cursor/worktrees.json`. |
| `MultiRootWorkspaceTree`, `WorkspaceSetPicker` | `src/OmpOS.Presentation/Editor/` | The explorer model and the picker model. |

The planner takes plain paths (`WorkspaceSet.AdditionalDirectoryPaths`), not the set type, so `OmpOS.Harness.AgentLoop` and `OmpOS.Presentation` do not reference `OmpOS.Harness.Workspace`. The host maps between them: `WorkspaceSet` to `WorktreeRootSpec` (`Id` or `DisplayName` and `Path`), and a `WorkspaceSetPicker` backend that wraps the validator and `CodeWorkspaceFile`.

The new root type is named `WorkspaceSetRoot` because `OmpOS.Workspace.WorkspaceRoot` already exists (the symlink-safe handle for one local directory) and the two are different things.

## The omp probe (omp 18.12.6)

Evidence: [evidence/multi-repo-omp-probe-2026-10-27/README.md](evidence/multi-repo-omp-probe-2026-10-27/README.md), with the script and four transcripts. All runs were in an empty network namespace with a fake key and only slash-command prompts, so no model call was possible.

- `omp acp --add-dir <path>` works, repeatable. `/dirs` lists the working directory and each added directory; the session file header records `additionalDirectories`.
- `/add-dir`, `/remove-dir` and `/dirs` sent as an ACP `session/prompt` run without a model call. The answer is one `agent_message_chunk` and `stopReason: end_turn`. The three commands are advertised in `available_commands_update`.
- `workspace.additionalDirectories` in a `--config` overlay gives the same result as the flag.
- omp does not validate flag directories (a missing one is listed silently), de-duplicates identical absolute paths, resolves relative paths against the process working directory, and allows nesting. OmpOS therefore validates everything itself.

Decision: the launch mechanism is `--add-dir` arguments (no session-start command list is needed). The overlay also records the directories under `workspace.additionalDirectories`, but only when an overlay is written for another reason (`DisableProjectMcpConfig`); the overlay alone is never what carries them.

## Launch

`ExtraArguments` for a session with two additional roots (paths are passed one by one, never joined into a shell line, so spaces and quotes need no escaping):

```text
--no-extensions -e <plugin entries> [--config <overlay>] --add-dir /repos/api --add-dir /repos/docs
```

The planner rejects a blank, relative or NUL-containing directory, a repeat, and the workspace root itself. The terminal command (`OmpLaunchPlan.ForTerminal`) keeps the pairs. A single-root session plans exactly what it did before.

## Worktrees for a run

`WorktreeRunSet.CreateAsync(roots, runId)` takes the roots primary first and returns one outcome per root.

- A git root (it directly contains `.git`) gets a worktree through `WorktreeManager`: `<Worktrees:Root>/<runId>-<root name>` on branch `ompos/run/<runId>-<root name>`. The root name is reduced to ASCII letters, digits and hyphens, made unique and cut so the id has at most 64 characters. The run id must leave room for a name.
- A root that is not the top of a git repository (including a subfolder of one) is used in place and reported as a warning (`Warnings`, status `UsedInPlace`).
- Nested or identical roots throw `NestedWorktreeRootException` before anything is created.
- The cap is 25 run worktrees under the manager's root (`WorktreeRunSet.DefaultMaxWorktrees`, the default of Cursor's `cursor.worktreeMaxCount`); a set that would exceed it throws `WorktreeCapExceededException` and creates nothing. `WorktreeManager` itself has no cap: this class adds it.
- If git fails for one root, the worktrees already made are removed and the result is not complete (`IsComplete` false): the failed root is `Failed`, the earlier ones `RolledBack`, the later ones `NotAttempted`.
- `CloseAsync(result)` removes every worktree and branch the set created, tries every root even if one fails, and can be repeated.
- The run starts omp in `PrimaryWorkingPath` and passes `AdditionalWorkingPaths` as the additional directories.

`.cursor/worktrees.json` is read by `CursorWorktreesConfig.Find(worktreePath, projectRoot)` (the worktree first, then the project root, as Cursor does) into the three keys `setup-worktree`, `setup-worktree-unix` and `setup-worktree-windows`, each a command list or a script path inside `.cursor/`. `SetupFor(windows)` applies Cursor's precedence. Running the commands is not wired: the existing `SetupRunner` and `SetupRunGate` run only the OmpOS setup file of one `WorkspaceRoot` and have no entry that takes arbitrary commands, so the commands need their own consent key (repository plus the hash of `worktrees.json`) before anything can run them. `ROOT_WORKTREE_PATH` must then be set to the main checkout.

## `.code-workspace` files

- Import reads JSON with comments and trailing commas. `folders[].path` is resolved against the directory of the file; `name` is the display name. The first folder is the primary. Folders given as `uri` (for example `vscode-remote://`) or without a path are skipped and listed in `Warnings`. A file with no usable folder is rejected.
- Export writes tab-indented JSON. Paths are relative when the folder is at most two levels above the file, absolute otherwise. Exporting into a file that came from an import keeps every other key (settings, extensions, launch, tasks) and every extra key of a folder that stays; `uri` folders stay after the roots. Comments are dropped, because the JSON document model does not carry them.
- A set with a remote root cannot be exported.

## Explorer and picker models (Presentation)

`MultiRootWorkspaceTree.Open(sources)` opens each root on its own: a missing folder or a listing failure sets `WorkspaceRootTree.Error` on that root only. Each root has a `WorkspaceTree`, so the existing single-root tree and its tests are unchanged. `Qualify` and `TryResolve` convert between `(root, relative)` and the root-qualified path `rootName/relative/path` that the UI and prompts use.

`WorkspaceSetPicker` holds the rows (add folder, remove, make primary, move, rename, import, export) and re-validates through `IWorkspaceSetPickerBackend` after every change; each row exposes its findings and `CanStart` says whether the set may start a session.

## Limits

- No cross-environment sets: all roots of a session live in one environment (omp runs in one place). The validator rejects a mix. Remote roots are validated lexically only and cannot be probed.
- No per-root convention merge: the workspace catalog and the rules and skills sources still read one root. The merge waits for the convention catalog of the slash-command work.
- The set is not yet on the session record: `SessionSummary` and `StageObservationScope` still carry one `WorkspaceRoot`, so `SessionEngineSet` keeps the additional directories per slot in memory only and they are not restored after a restart. The contract change belongs to the integration wave.
- The set picker and explorer have no native view yet.
- The setup commands of `.cursor/worktrees.json` are read, not run.
- A root is judged to be a git root by a `.git` entry at its top; a symlinked repository root is resolved before the validator compares roots, but `WorktreeRunSet` compares full paths only.
- The `--add-dir` behaviour during a real model turn was not probed (needs a model key and owner approval).
