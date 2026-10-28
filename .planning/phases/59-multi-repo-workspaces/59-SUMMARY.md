# Phase 59 Summary: Multi-repo Workspaces

Status (recorded 2026-10-28): criteria 1 to 3 partial. The library layer is built and covered by the passing gate: a validated workspace set, `.code-workspace` import and export, launch arguments through omp's `--add-dir` (probed against omp 18.12.6 with no model call), one worktree per git root with real git in temporary repositories, and convention merge across roots. The desktop head has a picker, a store, `/save-workspace` and `/load-workspace`, and starts a session whose launch carries one `--add-dir` pair per additional root. Two pieces are not in any head (re-checked by grep on the tree of the release head 9d4e0075 plus documentation commits): `MultiRootWorkspaceTree` is declared only in `src/OmpOS.Presentation/Editor/MultiRootWorkspaceTree.cs` and no head binds it, and `WorktreeRunSet.CreateAsync` has no caller outside `OmpOS.Harness.Worktrees` (the Worktrees settings source reads only `WorktreeRunSet.DefaultMaxWorktrees`). The heads discover conventions from the primary root only. Test results: the `./bootstrap.sh` gate on commit e24e6851 exited 0 (recorded 2026-10-28, `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`; `dotnet test OmpOS.Linux.slnf` 12215 total, 12213 succeeded, 0 failed, 2 skipped); `OmpOS.Harness.Worktrees.Tests` is in `OmpOS.Linux.slnf` and its log line reads passed (24 s). v0.15.0 is tagged and released at 9d4e0075. No live model turn ran with additional directories (OD-07).

## Criteria

| Criterion | Status | Evidence |
|-----------|--------|----------|
| 1. A set of several roots (local and remote) is validated, launched through omp's additional-directory support, and shown in the explorer with one tree per root | Partial | Validation: `WorkspaceSetValidatorTests`, `WorkspaceSetTests`, `WorkspaceSetStoreTests` (Workspace run, 364). Launch arguments: `MultiRootLaunchTests` (one `--add-dir` pair per additional root in order, after the plugin and overlay arguments; paths with spaces and shell characters stay one argument; duplicates, the primary root, relative and NUL paths are rejected; overlay records the directories). Recorded probe: `docs/runbooks/evidence/multi-repo-omp-probe-2026-10-27/` (README, `probe.py`, four transcripts; omp 18.12.6 under `unshare -rn`, fake key, slash prompts only). Explorer model: `MultiRootWorkspaceTreeTests`. Head: `WorkspaceSetPickerViewTests`, `WorkspacesSettingsSourceTests`, `StartOnRootsAsync` starts a session on the roots. Missing: no head binds `MultiRootWorkspaceTree`, so the explorer is single-root; `WorkspaceSetValidator` refuses roots in different environments, so a set mixing local and remote roots is not accepted (a remote-only set is validated without a probe); a remote set launched with `--add-dir` on the remote host is untested |
| 2. Worktrees per git root for a run, closed with it; `.code-workspace` files import and export | Partial | `WorktreeRunSetTests` (every git root gets its own worktree and branch with the primary first; a file written in one worktree stays out of the other; closing removes every worktree and branch and can be repeated; a non-git root or a subfolder is used in place; nested and duplicate roots are refused before anything is created; the cap counts existing worktrees; a failing root rolls back; real git in temporary directories), `CursorWorktreesConfigTests`, `CodeWorkspaceFileTests` (file round trip, tab-indented JSON), `WorkspaceSetPickerViewTests` (import and export through the backend); `OmpOS.Harness.Worktrees.Tests` is in `OmpOS.Linux.slnf` and passed in the gate log of e24e6851. Missing: `WorktreeRunSet.CreateAsync` is not called by any session start in the desktop or API head; the per-run worktree set is a library |
| 3. Rules, skills and commands merge across roots with per-root diagnostics | Partial | `ConventionCatalogTests.AnEarlierWorkspaceRootWinsASharedName` and the duplicate diagnostics of `ConventionCatalogTests` (inside the passing Workspace run). Missing: `SessionEngineSet.SlashDiscoveryFor` and `ConventionSettingsLoader` pass one root, so no head shows a merge across roots; hooks and MCP receive all roots (`HooksSessionRequest.WorkspaceRoots`) |

## What the phase delivered

- The probe of what omp does with additional directories, recorded with its script and transcripts, and the decision it supports: `--add-dir` pairs are the launch mechanism and OmpOS validates existence, duplicates and nesting itself because omp does not.
- `OmpOS.Workspace.Sets`: set, root, validator, store, `.code-workspace` import and export; `WorktreeRunSet` and `CursorWorktreesConfig`; the explorer and picker models.
- Desktop wiring: the picker window, the Workspaces Settings page, `/save-workspace` and `/load-workspace`, and a new session that carries the additional directories into `omp acp`.

## Deviations

- `WorkspaceSetRoot` is the type name because `OmpOS.Workspace.WorkspaceRoot` already names the symlink-safe handle for one local directory.
- The planner takes plain paths, not the set type, so `OmpOS.Harness.AgentLoop` and `OmpOS.Presentation` do not reference `OmpOS.Harness.Workspace`; the host maps between them.
- Live add and remove in a running session through `/add-dir` and `/remove-dir` prompts works without a model call (probe) but no OmpOS code sends them; `/add-dir` is catalogued as delegated to omp, `/remove-dir` and `/dirs` are not catalogue tokens.
- `docs/runbooks/multi-repo.md` still says the head picker is future work; the tree has the picker. The audit rows AC-009, AC-050 and WS-01 to WS-03 were written on the Wave 2 tree and predate the picker wiring.

## Partial

- Criterion 1: single-root explorer in the heads; no mixed local and remote set; remote `--add-dir` untested.
- Criterion 2: the worktree set is not started by a session.
- Criterion 3: no head merges conventions across roots.

## Blocked (OQ/OD)

- OD-07: behaviour of `--add-dir` during a real model turn was not probed; omp applies the directories per process, not per ACP session.
- Proposed OQ: where does a multi-root session's root list persist (registry record or settings), and does `session/load` after a restart re-send `--add-dir`? omp restores `additionalDirectories` from the session header on resume: UNVERIFIED: `omp acp --resume <id>` with and without `--add-dir`.
- Proposed OQ: should `.cursor/worktrees.json` setup commands run through a new consent key (repository path plus file hash) shared with `SetupConsentKey`?
- Proposed OQ: should the explorer and the prompt use root-qualified `name/relative` paths for ACP tool-call locations (omp reports absolute paths)?

## Known limits

- The set limit is 16 roots; nested roots are refused by the validator although omp allows them.
- A damaged store file reads as empty and is moved aside.
- Worktree ids are cut to fit 64 characters and root names are reduced to safe segments (`WorktreeRunSetTests`).
- Linux only; no Windows, macOS or Android run.
