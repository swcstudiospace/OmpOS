# Phase 52 Verification: Repository, Release and Community Health

How to re-run each proof and what it proves. Run from the repository root. The .NET environment is not needed (no .NET project changed); for completeness:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `52-SUMMARY.md`.

Recorded totals: this phase has no .NET test. The GitHub settings were applied on 2026-10-28 and recorded by the orchestrator. The GitHub settings were read back after the push (`docs/runbooks/evidence/github-setup-2026-10-28/read-back.json`). Two shell checks and one YAML parse were run on 2026-10-28. The full `./bootstrap.sh` gate passed on commit e24e6851 (recorded 2026-10-28, exit 0: `dotnet test Autonogrammer.Linux.slnf` 12215 total, 12213 succeeded, 0 failed, 2 skipped; warnings-as-errors Release build; `dotnet format --verify-no-changes` clean; repository checks, licence check, plugin tests and the web smoke OK); log `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 settings applied | `bash scripts/github/apply-repo-settings.sh` (dry run), then `--apply` | The description, homepage, 15 topics, discussions, wiki, branch deletion, private reporting, Dependabot security updates, secret scanning, push protection and 22 labels are set | Applied 2026-10-28; the read-back JSON confirms description, homepage, 15 topics, discussions on, wiki off, delete-branch-on-merge, security fixes enabled, 26 labels (22 ours plus GitHub defaults) |
| 1 read-back | `bash scripts/github/read-back.sh --out docs/runbooks/evidence/github-readback-<date>.json` | The live state of the repository, topics, community profile, private reporting, security fixes, labels, rulesets | Saved: `docs/runbooks/evidence/github-setup-2026-10-28/read-back.json`, read 2026-10-28T19:53:50Z (private reporting enabled, automated security fixes enabled, no rulesets, no branch protection on main) |
| 1 licence detected | `gh repo view swcstudiospace/OmpOS --json licenseInfo` | GitHub detects AGPL-3.0 from `LICENSE` | Read back: `license.key` `agpl-3.0`, `spdx_id` AGPL-3.0 |
| 1 social preview | Owner step in `docs/runbooks/github-setup.md`, image `assets/brand/autonogrammer-social-preview.png` (1280x640) | The image is ready; the upload is manual | Owner step, not done |
| 2 files and YAML | `python3 -c "import yaml,glob; [yaml.safe_load(open(f)) for f in glob.glob('.github/**/*.y*ml', recursive=True)]"` and `bash -n scripts/github/apply-repo-settings.sh scripts/github/read-back.sh` | The nine `.github` YAML files parse; both scripts are syntactically valid | Run on 2026-10-28: nine files parsed, no error; both scripts passed `bash -n` |
| 2 community profile | `gh api repos/swcstudiospace/OmpOS/community/profile --jq '{health_percentage, files: (.files \| map_values(. != null))}'` | GitHub finds the code of conduct, contributing, issue and PR templates, licence and readme | Read back: `health_percentage` 100; readme, licence, contributing, code of conduct and PR template present; `files.issue_template` is null (reported, not hidden: the issue forms exist in `.github/ISSUE_TEMPLATE/` but GitHub did not list them) |
| 3 changelog | `bash scripts/ci/check-changelog.sh` | Header, `[Unreleased]`, strict dates, descending versions, link references, `VersionPrefix` consistent | Run on 2026-10-28: `CHANGELOG.md is valid (8 versions)` |
| 3 release | `gh release view v0.15.0` and `gh run list --workflow release.yml --limit 1` | The tag exists, the workflow ran, the Release has the changelog notes | Tag v0.15.0 (9d4e0075) and the Release exist; `release` run 40758275112 succeeded in 31 s (recorded 2026-10-28) |

## Evidence files

| File | What it shows |
|------|---------------|
| `docs/runbooks/github-setup.md` | Contents, script behaviour, order, manual steps, tag and release steps, the community profile checks |
| `.github/workflows/release.yml` | The release workflow |
| `CHANGELOG.md` | The eight sections |

Also: `docs/runbooks/evidence/github-setup-2026-10-28/read-back.json` (the GitHub state after the push) and `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log` (the gate runs `check-changelog.sh` through `repo-checks.sh`).

## Known limits of this verification

- No test in the repository parses the `.github` YAML; the evidence is a one-off PyYAML parse of nine files (2026-10-28) and GitHub's acceptance of them.
- `files.issue_template` is null in the community profile while health is 100.
- The owner's social preview upload and the discussion categories are not done; the older milestone tags do not exist (their compare links 404).
- GitHub CI (`ci` workflow) failed at 'Start test services': main-push run 40758215402 (`newuidmap: write to uid_map failed: Operation not permitted`, OQ-168) and tag-push run 40758275399 ("cannot clone: Permission denied"); the local gate is the gate of record.
