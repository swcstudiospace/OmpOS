# Phase 65 Verification: Integration and Release v0.15.0

```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `65-SUMMARY.md`. A row marked "no recorded run" has a command but no result.

Recorded totals (2026-10-28, orchestrator, final tree before the last wiring): Presentation 2303, Native 2012 (2 skipped), AgentLoop 1221, Acp 201, Mcp 197, Plugins 429, Remote 520+ (2 skipped as root), Workspace 364, Automations 386, Web 262+, UI 595, Architecture 46, AgentWorker 153, Contracts 137, Sandbox 329; Api.IntegrationTests 1027/1027, Coordination 32, Sessions.Postgres 58 (throwaway podman services). The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 | `git worktree add /tmp/clean-final <final-commit> && cd /tmp/clean-final && ./bootstrap.sh 2>&1 \| tee docs/runbooks/evidence/bootstrap-gate-2026-10-28-<sha>.log` | Restore, build with warnings as errors, analyzers, format check, every non-live test, docs and settings checks, web smoke where the script runs it | Not recorded in this record: the gate was running; the orchestrator records the log. No 2026-10-28 log in the tree when written |
| 2 | `git fetch origin && git merge-base --is-ancestor origin/main <final-commit> && echo fast-forward` | The push is a fast-forward | Holds for the locally known `origin/main` (`04e80d1`) against `7535859`; no push done |
| 2 | `git tag -l v0.15.0` and `gh release view v0.15.0` | The tag and the GitHub Release exist | No tag, no release when written |
| 3 | Read `65-SUMMARY.md` (Final report) | Verified and owner items are separated, each with its evidence or its missing piece | Met |

## Evidence files

| File | What it holds |
|------|---------------|
| `docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md` | The live ssh run with the real omp 18.12.6 |
| `docs/runbooks/evidence/web-app-2026-10-28/` | Web Demo screenshots (light and dark at 1440 and 1024) |
| `docs/runbooks/evidence/multi-repo-omp-probe-2026-10-27/` | The `--add-dir` probe, transcripts and script |
| `docs/runbooks/evidence/ui-parity-2026-10-26/` | Phase 50 screenshots |
| `docs/runbooks/evidence/bootstrap-gate-2026-10-27-d75fd88.log`, `-d93dd11.log` | The latest gate logs that exist; the 2026-10-28 log is the orchestrator's |

## Known limits of this verification

- Criteria 1 and 2 are not closed by this record.
- Per-project counts predate the last wiring commits.
- No model turn (OD-07), no Windows, macOS or Android run, no Railway deploy, no sign-in authority (OD-03).
