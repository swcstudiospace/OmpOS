# Phase 59 Verification: Multi-repo Workspaces

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `59-SUMMARY.md`. Tests are hermetic: headless Avalonia, no omp, no network. The worktree tests need `git` on the path and create temporary repositories. Only the probe used the real omp 18.12.6, in an empty network namespace with a fake key and slash-command prompts only.

Recorded totals (recorded 2026-10-28): the `./bootstrap.sh` gate on commit e24e6851 exited 0 (`docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`): `dotnet test Autonogrammer.Linux.slnf` 12215 total, 12213 succeeded, 0 failed, 2 skipped; its log lists `Autonogrammer.Harness.Worktrees.Tests` as passed (24 s), and `Autonogrammer.Linux.slnf` contains that test project. Earlier per-project runs before the last wiring: Workspace 364, AgentLoop 1221, Presentation 2303, Native 2012 (2 skipped), Architecture 46. v0.15.0 is tagged and released at 9d4e0075 (documentation-only commits follow). The commands below were not re-run by the author of this record. "Inside the passing <Project> run" means the project passed as a whole, not that the class was run alone.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 (validate) | `dotnet test tests/Autonogrammer.Harness.Workspace.Tests -c Release -- --filter-class "*WorkspaceSetTests*" --filter-class "*WorkspaceSetValidatorTests*" --filter-class "*WorkspaceSetStoreTests*"` | The set keeps the first root primary; the validator reports a missing local folder, duplicate and nested roots by real path, more than 16 roots, relative or NUL paths, roots in different environments and blank or repeated names; the store round trips and sets a damaged file aside | Inside the passing Workspace run (364) |
| 1 (launch) | `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*MultiRootLaunchTests*"` | One `--add-dir <absolute path>` pair per additional root, in order, after the plugin and overlay arguments; a single-root session gets none; spaces and shell characters stay one argument; invalid and repeated directories are rejected; the overlay records the directories | Inside the passing AgentLoop run (1221) |
| 1 (probe) | `python3 docs/runbooks/evidence/multi-repo-omp-probe-2026-10-27/probe.py <flag\|slash\|overlay\|edge>` (needs omp 18.12.6, `unshare -rn`, a throwaway `HOME`) | `omp acp --add-dir` is accepted and repeatable; `/dirs`, `/add-dir` and `/remove-dir` run without a model call; the overlay key gives the same list; omp does not validate flag directories and allows nesting | Recorded 2026-10-27: four transcripts in `docs/runbooks/evidence/multi-repo-omp-probe-2026-10-27/` (`transcript-flag.json`, `-slash.json`, `-overlay.json`, `-edge.json`); not re-run for this record |
| 1 (explorer model) | `dotnet test tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*MultiRootWorkspaceTreeTests*" --filter-class "*WorkspaceSetPickerTests*"` | One tree per root, primary first; the picker adds, removes, renames, imports, exports and starts | Inside the passing Presentation run (2303). No head binds `MultiRootWorkspaceTree` |
| 1 (picker view) | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*WorkspaceSetPickerViewTests*" --filter-class "*WorkspacesSettingsSourceTests*" --filter-class "*WorkspacesPageViewTests*"` | The picker window adds a folder, removes with the button or Delete, makes a folder primary, imports and exports with warnings and failures shown, disables Start until the set is valid, and names every control | Inside the passing Native run |
| 1 (remote set) | none | A set on a remote host launched with `--add-dir` there | Not verified. UNVERIFIED: a loopback `sshd` run like `docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md` with a two-folder remote set, then `/dirs` |
| 2 | `dotnet test tests/Autonogrammer.Harness.Worktrees.Tests -c Release -- --filter-class "*WorktreeRunSetTests*" --filter-class "*CursorWorktreesConfigTests*"` and `dotnet test tests/Autonogrammer.Harness.Workspace.Tests -c Release -- --filter-class "*CodeWorkspaceFileTests*"` | A worktree and branch per git root, closed together and idempotently; non-git roots used in place; nested, duplicate and over-cap sets refused before anything is created; a failing root rolls back; `.cursor/worktrees.json` read; `.code-workspace` file round trip | Inside the passing Worktrees run of the e24e6851 gate (recorded 2026-10-28). Grep: `WorktreeRunSet.CreateAsync` has no caller outside `Autonogrammer.Harness.Worktrees`, so no session starts it |
| 3 | `dotnet test tests/Autonogrammer.Harness.Workspace.Tests -c Release -- --filter-class "*ConventionCatalogTests*"` | An earlier workspace root wins a shared name and the loser is a diagnostic with its origin | Inside the passing Workspace run. No head passes more than one root |

## Evidence files

| File | What it shows |
|------|---------------|
| `docs/runbooks/evidence/multi-repo-omp-probe-2026-10-27/README.md` | Method, safety (empty network namespace, fake key, slash prompts only), the four scenarios and what omp 18.12.6 does with additional directories |
| `docs/runbooks/evidence/multi-repo-omp-probe-2026-10-27/probe.py` | The ACP script |
| `docs/runbooks/evidence/multi-repo-omp-probe-2026-10-27/transcript-{flag,slash,overlay,edge}.json` | The recorded exchanges |
| `docs/runbooks/multi-repo.md` | How the pieces fit (its status line is stale about the picker) |

## Known limits of this verification

- No model turn with additional directories (OD-07); the probe did not run one.
- No remote multi-root run, no Windows, macOS or Android run.
- Per-run worktrees, the multi-root explorer and head-side convention merge are not wired, so no head-level test exists for them.
- The Worktrees project passed in the e24e6851 gate; the classes were not run alone.
