---
description: "How to apply the GitHub presentation, community health and release settings of the Autonogrammer repository with the gh CLI."
---

# GitHub setup runbook (Phase 52)

Status: Built and applied: the settings were applied with `scripts/github/apply-repo-settings.sh --apply` and read back after the push on 2026-10-28, and the tag and the GitHub Release v0.15.0 exist (recorded); the social preview upload and the discussion categories still need the owner

Last verified: 2026-10-28

Applies to: v0.15.0

Applies the GitHub presentation, community health and release settings of `swcstudiospace/OmpOS`. The files live in the repository; the settings need the `gh` CLI signed in with admin rights on the repository (`gh auth status`). Facts about the repository state on 2026-10-27 are in `docs/plans/v0.15.0-research/12-github-gitbook-plan.md`.

**Recorded 2026-10-28:** `scripts/github/apply-repo-settings.sh` was applied to `swcstudiospace/OmpOS`: description, homepage (the web Demo, <https://ompos-web.vercel.app>), 15 topics, Discussions on, wiki off, delete-branch-on-merge, private vulnerability reporting, Dependabot security updates, secret scanning with push protection and 22 labels. Read back after the push (recorded 2026-10-28, `docs/runbooks/evidence/github-setup-2026-10-28/read-back.json`): GitHub detects the licence as AGPL-3.0 (key `agpl-3.0`), the community profile health is 100, private vulnerability reporting and automated security fixes are enabled, secret scanning and push protection are enabled, there are no rulesets and no branch protection on `main` (on purpose). `origin/main` was fast-forwarded to `9d4e0075`, the annotated tag `v0.15.0` points at it, and the `release` workflow (run 40758275112, 31 s) published https://github.com/swcstudiospace/OmpOS/releases/tag/v0.15.0. The `ci` workflow failed for both pushes at 'Start test services' because rootless podman cannot create a user namespace in the runner container (OQ-168); the local `./bootstrap.sh` gate of `e24e6851` is the gate of record. Not done, because GitHub offers no API: the social preview image upload (OQ-170) and the discussion categories (OQ-109) (see "Manual steps"). The older milestone tags `v0.9.1` to `v0.14.0` do not exist (OQ-108).

## What is in the repository

| Path | Purpose |
| --- | --- |
| `CODE_OF_CONDUCT.md`, `SECURITY.md`, `SUPPORT.md`, `CONTRIBUTING.md` | Community health files (the organisation's `.github` repository holds none, so they must live here) |
| `.github/ISSUE_TEMPLATE/*.yml` | Bug, feature and documentation issue forms; `config.yml` disables blank issues and links to private security reporting, Discussions and SUPPORT.md |
| `.github/PULL_REQUEST_TEMPLATE.md` | The pull request checklist, with the lock-file relock note |
| `.github/CODEOWNERS` | Review owners (`@SomeRandmGuyy`) |
| `.github/dependabot.yml` | Weekly grouped updates for NuGet, GitHub Actions and `plugins/gsd-core-omp` (npm) |
| `.github/release.yml` | Categories for GitHub's generated release notes |
| `.github/labels.yml` | The label set (name, colour, description), applied by the settings script |
| `.github/workflows/release.yml` | A pushed `v*` tag creates the GitHub Release from the CHANGELOG section |
| `CHANGELOG.md`, `scripts/ci/check-changelog.sh` | Keep a Changelog file and its validator |
| `scripts/github/apply-repo-settings.sh` | Idempotent `gh` commands for the settings (dry run by default) |
| `scripts/github/read-back.sh` | Prints the settings as JSON for evidence |

## What the scripts do

`scripts/github/apply-repo-settings.sh` prints every command and runs none unless `--apply` is given. Options: `--repo OWNER/NAME`, `--homepage URL`, `--description TEXT`; the environment variables `AUTONOGRAMMER_REPO` and `AUTONOGRAMMER_HOMEPAGE` do the same. With `--apply` it:

1. sets the description and homepage (the homepage defaults to the repository URL until the docs site exists), turns Discussions on, the wiki off, and delete-branch-on-merge on (`gh repo edit`);
2. replaces the topic list with the 15 topics: `autonogrammer`, `omp`, `ai-agents`, `coding-agent`, `agent-workspace`, `cursor-alternative`, `ai-ide`, `dotnet`, `csharp`, `avalonia`, `blazor-webassembly`, `aspnet-core`, `mcp`, `kanban`, `agpl-3` (`PUT repos/{repo}/topics`);
3. turns on private vulnerability reporting, Dependabot alerts and Dependabot security updates;
4. turns on secret scanning and push protection (`PATCH repos/{repo}` with `security_and_analysis`);
5. creates or updates every label in `.github/labels.yml` (`gh label create --force`).

It leaves the merge methods (squash, merge commit, rebase) as they are, and it creates no branch protection and no ruleset, so the owner's direct fast-forward pushes to `main` keep working.

`scripts/github/read-back.sh [--out FILE]` only reads: repository settings, topics, community profile, private reporting state, security updates, labels, rulesets and branch protection, as one JSON document.

## Order

1. Land the files on `main`. Dependabot, issue forms, CODEOWNERS and `SECURITY.md` are read from the default branch, and the licence agent's `LICENSE` must be there for GitHub's licence detection.
2. Dry run: `bash scripts/github/apply-repo-settings.sh`. Read the commands.
3. Apply: `bash scripts/github/apply-repo-settings.sh --apply` (add `--homepage <url>` when the docs site is published).
4. Do the manual steps below.
5. Read back: `bash scripts/github/read-back.sh --out docs/runbooks/evidence/github-setup-<date>/read-back.json` and check the result against the list in "Verify" (the 2026-10-28 read-back and what it holds: `docs/runbooks/evidence/github-setup-2026-10-28/README.md`).
6. Release: see "Tag and release v0.15.0".

## Manual steps (GitHub offers no API)

- **Social preview.** Repository **Settings**, section **Social preview**, **Edit**, **Upload an image**, choose `assets/brand/autonogrammer-social-preview.png` (made by the brand work in Phase 51; use the file's actual name if it differs). PNG, under 1 MB, 1280 by 640 pixels is best, an opaque background is recommended. Confirm with `gh api graphql -f query='{repository(owner:"swcstudiospace",name:"Autonogrammer"){usesCustomOpenGraphImage}}'`, which must return `true`.
- **Discussion categories.** The script turns Discussions on; GitHub seeds default categories, and categories can only be created in the UI: **Discussions**, pencil beside **Categories**, **New category** (an emoji, a title, a description and a format: open-ended, Q&A or announcement). Proposal: Announcements (announcement), Q&A (Q&A, the target of the "Questions and ideas" link), Ideas, Show and tell, Providers and plans, Cloud agents and remote environments.
- **About "Include in the home page".** Gear beside **About**: Releases on once the first release exists; Packages and Deployments off. These toggles have no API.
- **Homepage.** It is the web Demo URL since 2026-10-28. Once the GitBook site or a custom domain exists, run the script again with `--homepage <url>`, or edit the field in the About gear; keep the Demo link in the README.
- **Optional: CodeQL default setup.** Not configured by the script. `codeql.yml` is the advanced setup and runs on `ubuntu-24.04`. A GitHub default setup (`runner_type: "standard"`) no longer conflicts with the runner policy. Do not point it back at `[self-hosted, linux, x64, omega-ci]`.
- **Optional: a minimal ruleset.** Only if the owner wants a floor. This ruleset blocks branch deletion and force pushes on `main` and still allows fast-forward pushes. It is not in the script on purpose:

  ```sh
  gh api -X POST repos/swcstudiospace/OmpOS/rulesets --input - <<'JSON'
  {
    "name": "main-no-delete-no-force-push",
    "target": "branch",
    "enforcement": "active",
    "conditions": { "ref_name": { "include": ["refs/heads/main"], "exclude": [] } },
    "rules": [ { "type": "deletion" }, { "type": "non_fast_forward" } ]
  }
  JSON
  ```

  Add the GitBook app and the owner as bypass actors if GitBook Git Sync is connected. Do not add required pull requests, required status checks, linear history or push restrictions: they reject direct fast-forward pushes.
- **Secrets.** The release workflow needs no secret beyond the job's `GITHUB_TOKEN`. The CI secret `SUBMODULE_TOKEN` (read access to the private `swcstudiospace/substrate`) is separate and is documented in `docs/plugins.md`.

## Dependabot and the lock files

Every project commits `packages.lock.json` and CI restores with `--locked-mode`. Dependabot's NuGet updater edits `Directory.Packages.props` and may not regenerate the lock files, so a Dependabot pull request can fail restore with `NU1004`. Relock it before merging (`CI=true` switches locked mode on, so it is switched off for this command):

```sh
dotnet restore Autonogrammer.Linux.slnf --force-evaluate -p:RestoreLockedMode=false
```

Commit the lock-file changes to the Dependabot branch (`git fetch`, `git checkout <branch>`, relock, commit with `-s`, push). Watch the first Dependabot pull request: if relocking by hand every week is too costly, add a relock job to a workflow or ask Dependabot to ignore the packages that cause trouble. UNVERIFIED: how Dependabot behaves with this repository's lock files; the first pull request resolves it.

## Tag and release v0.15.0

Done 2026-10-28 (recorded): the steps below ran for v0.15.0 and the Release exists; they stay here as the procedure for the next release.

1. `Directory.Build.props` holds `<VersionPrefix>0.15.0</VersionPrefix>` (the release work of Phase 65 sets it; the tag must equal it, and the release workflow fails otherwise).
2. In `CHANGELOG.md`, rewrite `[Unreleased]` into `## [0.15.0] - <date>` with the real contents, open a new empty `## [Unreleased]`, and update the bottom references: `[Unreleased]: https://github.com/swcstudiospace/OmpOS/compare/v0.15.0...HEAD` and `[0.15.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.14.0...v0.15.0`.
3. `bash scripts/ci/check-changelog.sh` must pass, then `./bootstrap.sh` must pass on the release commit.
4. Fast-forward push `main` (`git merge-base --is-ancestor origin/main HEAD` must succeed), then tag and push the tag:

   ```sh
   git tag -a v0.15.0 -m "Autonogrammer v0.15.0"
   git push origin v0.15.0
   ```

5. The `release` workflow runs on the self-hosted pool: it checks the tag format, that the tag equals `VersionPrefix`, that the commit is reachable from `origin/main`, runs the changelog check, takes the `## [0.15.0]` section as the release notes and creates the GitHub Release (a tag with a `-suffix`, such as `v0.15.0-rc.1`, becomes a pre-release and uses the `0.15.0` notes). If the pool is offline, create the release by hand with the same notes: `gh release create v0.15.0 --verify-tag --title "Autonogrammer v0.15.0" --notes-file notes.md`.
6. Verify: `gh release view v0.15.0` and `gh run list --workflow release.yml --limit 1`.

GitHub's automatic "Source code" archives never contain submodules, and `plugins/agent-swarm` and `plugins/agent-substrate` carry no licence file: do not attach an archive that bundles them to the public release without a licence decision (see OQ-93 and the licensing facts in the research file).

The compare links in `CHANGELOG.md` for the milestones before 0.15.0 point to tags that do not exist (the only tag is `v0.15.0`, recorded 2026-10-28; OQ-108): they work once those tags are created. Tagging historical commits is an owner decision; the only commit the records identify for sure is `cbbe22d`, the merge of PR #1 that shipped v0.9.1.

## Verify with the community profile

```sh
gh api repos/swcstudiospace/OmpOS/community/profile --jq '{health_percentage, files: (.files | map_values(. != null))}'
```

Expected: `health_percentage` 100, and every one of `code_of_conduct`, `contributing`, `issue_template`, `pull_request_template` and `license` true, `readme` true. Recorded 2026-10-28: `health_percentage` was 100 while `issue_template` was `null` (the repository has three issue forms under `.github/ISSUE_TEMPLATE/`; UNVERIFIED why GitHub reports no issue template for forms: open `https://github.com/swcstudiospace/OmpOS/issues/new/choose`). The `documentation` field is derived by GitHub and may point to a branch that does not exist; it does not count against the score. If `license` is false, check that `LICENSE` is the unmodified AGPL-3.0 text on `main` (GitHub's detector can take a few minutes). Then check:

| Check | Command | Expect |
| --- | --- | --- |
| Description, homepage, topics, features | `gh repo view swcstudiospace/OmpOS --json description,homepageUrl,repositoryTopics,hasDiscussionsEnabled,hasWikiEnabled,licenseInfo` | the values the script set; `licenseInfo.spdxId` is `AGPL-3.0` |
| Private reporting | `gh api repos/swcstudiospace/OmpOS/private-vulnerability-reporting` | `{"enabled":true}` |
| Secret scanning, push protection | `gh api repos/swcstudiospace/OmpOS --jq .security_and_analysis` | both `enabled` |
| Dependabot security updates | `gh api repos/swcstudiospace/OmpOS/automated-security-fixes` | `{"enabled":true,...}` |
| Labels | `gh label list --repo swcstudiospace/OmpOS --limit 100` | all labels of `.github/labels.yml` |
| Issue forms | open `https://github.com/swcstudiospace/OmpOS/issues/new/choose` | three forms and the contact links, no blank issue |
| Workflow files | the Actions tab | `ci` and `release` listed, no workflow-file errors |
| YAML validity | `python3 -c "import yaml,glob; [yaml.safe_load(open(f)) for f in glob.glob('.github/**/*.y*ml', recursive=True)]"` | no output |
