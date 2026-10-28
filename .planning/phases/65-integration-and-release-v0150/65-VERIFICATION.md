# Phase 65 Verification: Integration and Release v0.15.0

```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `65-SUMMARY.md`. The gate was run by the orchestrator; the push, tag and release facts were read back with `git ls-remote` and `gh` (read-only) when this record was updated.

Recorded gate (2026-10-28, orchestrator): `OMPOS_TEST_PARALLEL_MODULES=3 OMPOS_SMOKE_PUBLISH_TIMEOUT=2400 OMPOS_SMOKE_DEMO_TIMEOUT=3600 ./bootstrap.sh` on a clean git worktree of `e24e6851554ae6ea710a9841920030269f96d2be` exited 0: 12215 tests, 12213 succeeded, 0 failed, 2 skipped (two tests that need a non-root user). The log is `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 | `git worktree add /tmp/clean-final <final-commit> && cd /tmp/clean-final && ./bootstrap.sh 2>&1 \| tee docs/runbooks/evidence/bootstrap-gate-2026-10-28-<sha>.log` | Pinned secret scan, plugin node tests, repository checks (brand, changelog, docs, providers, settings parity), locked restore, licence check, warnings-as-errors build, every non-live test, format check, web smoke with axe | Met: exit 0 on `e24e6851`; 12215 total, 12213 succeeded, 0 failed, 2 skipped, 25 minutes; format clean; Demo smoke 6 configurations, 68 axe runs, 0 violations (recorded 2026-10-28). The log header records the two earlier failed runs (`083ac50`: 8 tests; `75bc55b`: a stress-test timeout at load average 96 that cascaded) |
| 2 | `git fetch origin && git merge-base --is-ancestor 04e80d1 9d4e0075 && echo fast-forward` | The push is a fast-forward | Met: holds; `git ls-remote origin refs/heads/main` returns `9d4e00758589aa8deab7a68ad60074b664b88e9a` |
| 2 | `git ls-remote --tags origin v0.15.0` and `gh release view v0.15.0` | The tag and the GitHub Release exist | Met: the annotated tag (object `8d4f22d1`) peels to `9d4e0075`; the Release `OmpOS v0.15.0` is published (not a draft, 2026-10-28T19:53:25Z) by `release` run 40758275112 (success, 31 s): https://github.com/swcstudiospace/OmpOS/releases/tag/v0.15.0 |
| 3 | Read `65-SUMMARY.md` (Final report) | Verified, unverified and owner items are separated, each with its evidence or its missing piece | Met |
| caveat | `gh run view 40758215402 --repo swcstudiospace/OmpOS` and `gh run view 40758275399 --repo swcstudiospace/OmpOS` | The `ci` workflow on GitHub | Failed both times at 'Start test services' (`newuidmap: write to uid_map failed: Operation not permitted`; `cannot clone: Permission denied`); restore and build passed, the rest was skipped (OQ-168). The local gate is the gate of record |

## Evidence files

| File | What it holds |
|------|---------------|
| `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log` | The gate log of the final commit |
| `docs/runbooks/evidence/github-setup-2026-10-28/read-back.json` and `README.md` | The GitHub settings read back after the push |
| `docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md` | The live ssh run with the real omp 18.12.6 |
| `docs/runbooks/evidence/cloud-worker-container-2026-10-28/README.md` | The worker image under podman, 14 of 14 checks |
| `docs/runbooks/evidence/web-app-2026-10-28/` | Web Demo screenshots, the live capture and the README |
| `docs/runbooks/evidence/multi-repo-omp-probe-2026-10-27/` | The `--add-dir` probe, transcripts and script |
| `docs/runbooks/evidence/ui-parity-2026-10-26/` | Phase 50 screenshots |

## Known limits of this verification

- The gate ran on `e24e6851`; the commits after `9d4e0075` change documentation and evidence only.
- The `ci` workflow on GitHub has not passed (OQ-168).
- No model turn (OD-07), no Windows, macOS or Android run, no Railway deploy, no sign-in authority (OD-03), no GitBook space.
