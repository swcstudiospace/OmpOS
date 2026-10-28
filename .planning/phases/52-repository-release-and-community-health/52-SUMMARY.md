# Phase 52 Summary: Repository, Release and Community Health

Status (recorded 2026-10-28): criteria 1 to 3 are Partial. The files exist and the repository settings were applied on 2026-10-28; what is missing is a saved `gh api` read-back, the owner's social preview upload and discussion categories, the community profile percentage, and the v0.15.0 tag and GitHub Release (they belong to Phase 65 and do not exist yet). Nothing here ran on Windows, macOS or Android; no model turn was made (OD-07).

The record was written after the work. Statuses use: Met, Met (recorded), Partial, Blocked, Not verified.

Recorded results (recorded 2026-10-28, orchestrator): the GitHub settings were applied with `scripts/github/apply-repo-settings.sh` (description, homepage = the Demo URL https://ompos-web.vercel.app, 15 topics, discussions on, wiki off, delete-branch-on-merge, private vulnerability reporting, Dependabot security updates, secret scanning and push protection, 22 labels). The social preview upload, the discussion categories and the GitBook space need the owner. The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Description, homepage, topics, a licence GitHub detects, security reporting, secret scanning, Dependabot security updates, applied with `gh` and recorded; social preview prepared with the manual step; `gh api` read-back under `docs/runbooks/evidence/` | Partial | Applied 2026-10-28 (fact above, recorded by the orchestrator). The script, its dry run and the read-back script exist: `scripts/github/apply-repo-settings.sh`, `scripts/github/read-back.sh`. The social preview image is prepared (`assets/brand/ompos-social-preview.png`, 1280x640, 51609 bytes) and the upload steps are in `docs/runbooks/github-setup.md`; the upload is an owner step (GitHub has no API for it). Missing: no read-back JSON exists under `docs/runbooks/evidence/` (a listing on 2026-10-28 shows none), so the applied state is the orchestrator's record, not a saved file; that GitHub detects the licence as AGPL-3.0 was not read back (UNVERIFIED: `gh repo view swcstudiospace/OmpOS --json licenseInfo`). To close: `bash scripts/github/read-back.sh --out docs/runbooks/evidence/github-readback-<date>.json` |
| 2 | Community files, issue forms, PR template, `dependabot.yml`, release notes configuration and labels exist and pass the community profile check; `gh api .../community/profile` percentage and a YAML validity test | Partial | Present: `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `SUPPORT.md`, `.github/CODEOWNERS`, `.github/ISSUE_TEMPLATE/{bug_report,feature_request,documentation,config}.yml`, `.github/PULL_REQUEST_TEMPLATE.md`, `.github/dependabot.yml`, `.github/release.yml`, `.github/labels.yml` (22 labels, counted). All nine `.github` YAML files parse with PyYAML (run 2026-10-28). Missing: the community profile health percentage was not read (UNVERIFIED: `gh api repos/swcstudiospace/OmpOS/community/profile --jq .health_percentage`), and there is no YAML validity test in the suite (the runbook gives a one-line command). `CODEOWNERS` names one handle, `@SomeRandmGuyy`, taken from the research; owner to confirm |
| 3 | `CHANGELOG.md` Keep a Changelog from v0.9.1 to v0.15.0; `Directory.Build.props` one version; v0.15.0 tag and GitHub Release with notes; changelog check script and `gh release view` | Partial | `CHANGELOG.md` has eight sections, 0.15.0 (2026-10-28) down to 0.9.1, and an empty `[Unreleased]`; `Directory.Build.props` holds `<VersionPrefix>0.15.0</VersionPrefix>` once. `bash scripts/ci/check-changelog.sh` run on the final tree on 2026-10-28: `CHANGELOG.md is valid (8 versions)`. `.github/workflows/release.yml` exists (runs on a pushed `v*` tag). Missing: the tag `v0.15.0` and the GitHub Release do not exist yet: the release is pending (Phase 65), so `gh release view v0.15.0` cannot pass. The compare links for the older milestones point at tags that do not exist (the repository has none) |

## What the phase delivered

- The community health files, three issue forms, the PR template, `CODEOWNERS`, Dependabot, release-note categories and the label set.
- The two GitHub scripts and the setup runbook, applied once on 2026-10-28.
- The changelog with its checker, and the tag-triggered release workflow that publishes the notes of the matching changelog section.

## Deviations

- The description says Android instead of Mobile (three heads are built: web, desktop, Android).
- 22 labels instead of the 21 requested: `android` and `ignore-for-release` were added because `release.yml` excludes the latter.
- Changelog dates before 0.15.0 are the day of the last recorded work (the milestones overlapped; only 0.9.1 was closed formally); the file says so.
- Code of Conduct reports go through GitHub private reporting; no e-mail address was invented.

## Partial

- Criterion 1: no saved read-back; social preview upload and discussion categories are owner steps.
- Criterion 2: community profile percentage and the YAML test.
- Criterion 3: the tag and the Release (Phase 65).

## Blocked (OQ/OD)

- None blocked. Open owner choices with a default: conduct contact (private report), `FUNDING.yml` (none), `CODEOWNERS` handle, security response targets (5 working days acknowledge, 10 first assessment, 90 days fix), tagging historical milestones.

## Known limits

- `release.yml` assumes `gh` on the self-hosted runner (UNVERIFIED: `gh --version` on an `omega-ci` runner); it was never run.
- `check-changelog.sh` is called by `release.yml` only, not by `bootstrap.sh` or `ci.yml`.
- Nested `security_and_analysis` field syntax and `--enable-wiki=false` depend on the installed `gh`; the settings were applied and recorded by the orchestrator, not re-read here.
- Dependabot behaviour with the locked NuGet files is not observed; the relock command is in `CONTRIBUTING.md` and the PR template.
