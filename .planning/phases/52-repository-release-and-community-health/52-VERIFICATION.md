# Phase 52 Verification: Repository, Release and Community Health

How to re-run each proof and what it proves. Run from the repository root. The .NET environment is not needed (no .NET project changed); for completeness:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `52-SUMMARY.md`.

Recorded totals: this phase has no .NET test. The GitHub settings were applied on 2026-10-28 and recorded by the orchestrator. Two shell checks and one YAML parse were run by the author of this record on the final tree on 2026-10-28. The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 settings applied | `bash scripts/github/apply-repo-settings.sh` (dry run), then `--apply` | The description, homepage, 15 topics, discussions, wiki, branch deletion, private reporting, Dependabot security updates, secret scanning, push protection and 22 labels are set | Applied 2026-10-28 (recorded by the orchestrator). The dry run lists 22 labels and 15 topics |
| 1 read-back | `bash scripts/github/read-back.sh --out docs/runbooks/evidence/github-readback-<date>.json` | The live state of the repository, topics, community profile, private reporting, security fixes, labels, rulesets | No saved read-back file exists; not run for this record |
| 1 licence detected | `gh repo view swcstudiospace/OmpOS --json licenseInfo` | GitHub detects AGPL-3.0 from `LICENSE` | Not run (UNVERIFIED) |
| 1 social preview | Owner step in `docs/runbooks/github-setup.md`, image `assets/brand/ompos-social-preview.png` (1280x640) | The image is ready; the upload is manual | Owner step, not done |
| 2 files and YAML | `python3 -c "import yaml,glob; [yaml.safe_load(open(f)) for f in glob.glob('.github/**/*.y*ml', recursive=True)]"` and `bash -n scripts/github/apply-repo-settings.sh scripts/github/read-back.sh` | The nine `.github` YAML files parse; both scripts are syntactically valid | Run on 2026-10-28: nine files parsed, no error; both scripts passed `bash -n` |
| 2 community profile | `gh api repos/swcstudiospace/OmpOS/community/profile --jq '{health_percentage, files: (.files \| map_values(. != null))}'` | GitHub finds the code of conduct, contributing, issue and PR templates, licence and readme | Not run (UNVERIFIED) |
| 3 changelog | `bash scripts/ci/check-changelog.sh` | Header, `[Unreleased]`, strict dates, descending versions, link references, `VersionPrefix` consistent | Run on 2026-10-28: `CHANGELOG.md is valid (8 versions)` |
| 3 release | `gh release view v0.15.0` and `gh run list --workflow release.yml --limit 1` | The tag exists, the workflow ran, the Release has the changelog notes | Not possible yet: no tag, no Release (Phase 65) |

## Evidence files

| File | What it shows |
|------|---------------|
| `docs/runbooks/github-setup.md` | Contents, script behaviour, order, manual steps, tag and release steps, the community profile checks |
| `.github/workflows/release.yml` | The release workflow |
| `CHANGELOG.md` | The eight sections |

No `docs/runbooks/evidence/` file records the GitHub state.

## Known limits of this verification

- The applied settings are the orchestrator's record; no read-back JSON was saved.
- The community profile percentage, licence detection and the Release were not observed.
- `release.yml` was never run; `gh` on the runner is UNVERIFIED (`gh --version` on an `omega-ci` runner).
- The owner's social preview upload and the discussion categories are not done.
