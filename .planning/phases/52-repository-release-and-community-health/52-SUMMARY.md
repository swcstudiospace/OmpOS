# Phase 52 Summary: Repository, Release and Community Health

Status (recorded 2026-10-28): criteria 1 to 3 are Met with caveats. The settings were applied and read back after the push (`docs/runbooks/evidence/github-setup-2026-10-28/read-back.json`, read 19:53 UTC): GitHub detects the licence as AGPL-3.0 (key `agpl-3.0`), community profile health 100, private vulnerability reporting and automated security fixes enabled, 26 labels (22 ours plus GitHub defaults), no rulesets and no branch protection on main on purpose. The annotated tag v0.15.0 (9d4e0075, pushed 19:53 UTC) and the GitHub Release exist; `release` workflow run 40758275112 succeeded in 31 s. Caveats: `files.issue_template` is null in the community profile JSON although health is 100 (the three issue forms are YAML forms; GitHub did not report them there); the social preview upload and the discussion categories are owner steps (no API); the older milestone tags v0.9.1 to v0.14.0 do not exist, so their CHANGELOG compare links 404; the `ci` workflow runs on GitHub failed at 'Start test services' (see below). Nothing here ran on Windows, macOS or Android; no model turn was made (OD-07).

The record was written after the work. Statuses use: Met, Met (recorded), Partial, Blocked, Not verified.

Recorded results (recorded 2026-10-28, orchestrator): `scripts/github/apply-repo-settings.sh --apply` ran (description, homepage = the Demo URL https://ompos-web.vercel.app, 15 topics, discussions on, wiki off, delete-branch-on-merge, private vulnerability reporting, Dependabot alerts and security updates, secret scanning and push protection, 22 labels) and the read-back was saved after the push. The social preview upload, the discussion categories and the GitBook space need the owner. The full `./bootstrap.sh` gate passed on commit e24e6851 (recorded 2026-10-28, exit 0: `dotnet test OmpOS.Linux.slnf` 12215 total, 12213 succeeded, 0 failed, 2 skipped; warnings-as-errors Release build; `dotnet format --verify-no-changes` clean; repository checks, licence check, plugin tests and the web smoke OK); log `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Description, homepage, topics, a licence GitHub detects, security reporting, secret scanning, Dependabot security updates, applied with `gh` and recorded; social preview prepared with the manual step; `gh api` read-back under `docs/runbooks/evidence/` | Met (caveat: owner upload) | Applied 2026-10-28 and read back after the push (`docs/runbooks/evidence/github-setup-2026-10-28/read-back.json`, read 2026-10-28T19:53:50Z): description, homepage https://ompos-web.vercel.app, 15 topics, discussions on, wiki off, delete-branch-on-merge, licence `agpl-3.0` (AGPL-3.0, detected by GitHub), private vulnerability reporting enabled, automated security fixes enabled, secret scanning and push protection enabled, 26 labels, no rulesets, no branch protection on main (on purpose). The script, its dry run and the read-back script exist: `scripts/github/apply-repo-settings.sh`, `scripts/github/read-back.sh`. The social preview image is prepared (`assets/brand/ompos-social-preview.png`, 1280x640, 51609 bytes) and the upload steps are in `docs/runbooks/github-setup.md`; the upload is an owner step (GitHub has no API for it). Still an owner step: the social preview upload and the discussion categories (GitHub has no API for them). Nothing else is missing for this criterion |
| 2 | Community files, issue forms, PR template, `dependabot.yml`, release notes configuration and labels exist and pass the community profile check; `gh api .../community/profile` percentage and a YAML validity test | Met (caveats) | Present: `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `SUPPORT.md`, `.github/CODEOWNERS`, `.github/ISSUE_TEMPLATE/{bug_report,feature_request,documentation,config}.yml`, `.github/PULL_REQUEST_TEMPLATE.md`, `.github/dependabot.yml`, `.github/release.yml`, `.github/labels.yml` (22 labels, counted). All nine `.github` YAML files parse with PyYAML (run 2026-10-28). Community profile read back 2026-10-28: `health_percentage` 100 with readme, licence, contributing, code of conduct and pull request template present. Caveat, reported not hidden: `files.issue_template` is null in the same JSON while health is 100, so GitHub's community profile did not list the issue forms (`.github/ISSUE_TEMPLATE/*.yml` exist in the tree). YAML validity test: there is no test in the repository that parses the `.github` YAML (grep of `tests/` found only text reads of `ci.yml` and the workflows in `GateParityTests`, `WorkflowPinTests`, `RepoHygieneTests`); the proof is the one-off PyYAML parse of all nine files (2026-10-28) and that GitHub accepted them (workflows ran, labels, Dependabot and the community files were read). The criterion is read as met by that, not by a test. `CODEOWNERS` names one handle, `@SomeRandmGuyy`, taken from the research; owner to confirm |
| 3 | `CHANGELOG.md` Keep a Changelog from v0.9.1 to v0.15.0; `Directory.Build.props` one version; v0.15.0 tag and GitHub Release with notes; changelog check script and `gh release view` | Met (caveat: older tags) | `CHANGELOG.md` has eight sections, 0.15.0 (2026-10-28) down to 0.9.1, and an empty `[Unreleased]`; `Directory.Build.props` holds `<VersionPrefix>0.15.0</VersionPrefix>` once. `bash scripts/ci/check-changelog.sh` run on the final tree on 2026-10-28: `CHANGELOG.md is valid (8 versions)`. `.github/workflows/release.yml` exists (runs on a pushed `v*` tag). The annotated tag `v0.15.0` points at 9d4e0075 (pushed 19:53 UTC) and the GitHub Release https://github.com/swcstudiospace/OmpOS/releases/tag/v0.15.0 exists (`gh release view v0.15.0` printed title OmpOS v0.15.0, not draft, 2026-10-28); `release` workflow run 40758275112 on the self-hosted omega-ci runner succeeded in 31 s, notes = the CHANGELOG [0.15.0] section (recorded 2026-10-28). `check-changelog.sh` also runs in the gate through `repo-checks.sh` (bootstrap passed). Caveat: the compare links for the older milestones (v0.9.1 to v0.14.0) point at tags that do not exist, so they 404; the owner may tag them |

## What the phase delivered

- The community health files, three issue forms, the PR template, `CODEOWNERS`, Dependabot, release-note categories and the label set.
- The two GitHub scripts and the setup runbook, applied once on 2026-10-28 and read back after the push.
- The changelog with its checker, and the tag-triggered release workflow that publishes the notes of the matching changelog section.

## Deviations

- The description says Android instead of Mobile (three heads are built: web, desktop, Android).
- 22 labels instead of the 21 requested: `android` and `ignore-for-release` were added because `release.yml` excludes the latter.
- Changelog dates before 0.15.0 are the day of the last recorded work (the milestones overlapped; only 0.9.1 was closed formally); the file says so.
- Code of Conduct reports go through GitHub private reporting; no e-mail address was invented.

## Partial

- None. Owner steps: the social preview upload and the discussion categories; tagging the older milestones v0.9.1 to v0.14.0 (optional).

## Blocked (OQ/OD)

- None blocked. Open owner choices with a default: conduct contact (private report), `FUNDING.yml` (none), `CODEOWNERS` handle, security response targets (5 working days acknowledge, 10 first assessment, 90 days fix), tagging historical milestones.

## Known limits

- `files.issue_template` is null in the community profile JSON while health is 100; the cause is not investigated.
- No test parses the `.github` YAML; the evidence is a one-off PyYAML parse and GitHub's acceptance.
- `release.yml` ran once (run 40758275112, 31 s, success): `gh` works on the omega-ci runner for that run.
- The `ci` workflow on GitHub failed at 'Start test services' on the main push (run 40758215402: `newuidmap: write to uid_map failed: Operation not permitted`, OQ-168) and on the tag push (run 40758275399: "cannot clone: Permission denied / cannot re-exec process"); restore and Release build passed, later steps were skipped. The local `./bootstrap.sh` gate on e24e6851 is the gate of record.
- Dependabot behaviour with the locked NuGet files is not observed; the relock command is in `CONTRIBUTING.md` and the PR template.
