---
description: "How to apply the GitHub presentation, community health and release settings of the OmpOS repository with the gh CLI."
---

# GitHub setup runbook (Phase 52)

Status: Built (files are in the repository); the settings are applied by the owner and are not applied by this page

Last verified: 2026-10-28

Applies to: v0.15.0

Applies the GitHub presentation, community health and release settings of `swcstudiospace/OmpOS`. The files live in the repository; the settings need the `gh` CLI signed in with admin rights on the repository (`gh auth status`). Facts about the repository state on 2026-10-27 are in `docs/plans/v0.15.0-research/12-github-gitbook-plan.md`.

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

`scripts/github/apply-repo-settings.sh` prints every command and runs none unless `--apply` is given. Options: `--repo OWNER/NAME`, `--homepage URL`, `--description TEXT`; the environment variables `OMPOS_REPO` and `OMPOS_HOMEPAGE` do the same. With `--apply` it:

1. sets the description and homepage (the homepage defaults to the repository URL until the docs site exists), turns Discussions on, the wiki off, and delete-branch-on-merge on (`gh repo edit`);
2. replaces the topic list with the 15 topics: `ompos`, `omp`, `ai-agents`, `coding-agent`, `agent-workspace`, `cursor-alternative`, `ai-ide`, `dotnet`, `csharp`, `avalonia`, `blazor-webassembly`, `aspnet-core`, `mcp`, `kanban`, `agpl-3` (`PUT repos/{repo}/topics`);
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
5. Read back: `bash scripts/github/read-back.sh --out docs/runbooks/evidence/github-settings-<date>.json` (the orchestrator owns the evidence folder) and check the result against the list in "Verify".
6. Release: see "Tag and release v0.15.0".

## Manual steps (GitHub offers no API)

- **Social preview.** Repository **Settings**, section **Social preview**, **Edit**, **Upload an image**, choose `assets/brand/ompos-social-preview.png` (made by the brand work in Phase 51; use the file's actual name if it differs). PNG, under 1 MB, 1280 by 640 pixels is best, an opaque background is recommended. Confirm with `gh api graphql -f query='{repository(owner:"swcstudiospace",name:"OmpOS"){usesCustomOpenGraphImage}}'`, which must return `true`.
- **Discussion categories.** The script turns Discussions on; GitHub seeds default categories, and categories can only be created in the UI: **Discussions**, pencil beside **Categories**, **New category** (an emoji, a title, a description and a format: open-ended, Q&A or announcement). Proposal: Announcements (announcement), Q&A (Q&A, the target of the "Questions and ideas" link), Ideas, Show and tell, Providers and plans, Cloud agents and remote environments.
- **About "Include in the home page".** Gear beside **About**: Releases on once the first release exists; Packages and Deployments off. These toggles have no API.
- **Homepage.** Once the GitBook site or a custom domain exists, run the script again with `--homepage <url>`, or edit the field in the About gear.
- **Optional: CodeQL default setup.** Not configured by the script. `gh api repos/swcstudiospace/OmpOS/code-scanning/default-setup` shows `runner_type: "standard"`, a GitHub-hosted runner, which conflicts with the repository policy that every job runs on the labelled self-hosted pool (`[self-hosted, linux, x64, omega-ci]`, see `ci.yml`). Enable code scanning only if the default setup can be given that runner label, or write an advanced `codeql.yml` that uses exactly those labels. UNVERIFIED: whether the pool can run the CodeQL bundle (`gh api -X PATCH repos/swcstudiospace/OmpOS/code-scanning/default-setup -f state=configured ...` with a runner label is the command that would resolve it).
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

  Add the GitBook app and the owner as bypass actors if GitBook Git Sync is connected. Do not add required pull requests, required status checks, linear history or push restrictions: they reject direct fast-forward pushes and the runner pool has been offline.
- **Secrets.** The release workflow needs no secret beyond the job's `GITHUB_TOKEN`. The CI secret `SUBMODULE_TOKEN` (read access to the private `swcstudiospace/substrate`) is separate and is documented in `docs/plugins.md`.

## Dependabot and the lock files

Every project commits `packages.lock.json` and CI restores with `--locked-mode`. Dependabot's NuGet updater edits `Directory.Packages.props` and may not regenerate the lock files, so a Dependabot pull request can fail restore with `NU1004`. Relock it before merging (`CI=true` switches locked mode on, so it is switched off for this command):

```sh
dotnet restore OmpOS.Linux.slnf --force-evaluate -p:RestoreLockedMode=false
```

Commit the lock-file changes to the Dependabot branch (`git fetch`, `git checkout <branch>`, relock, commit with `-s`, push). Watch the first Dependabot pull request: if relocking by hand every week is too costly, add a relock job to a workflow or ask Dependabot to ignore the packages that cause trouble. UNVERIFIED: how Dependabot behaves with this repository's lock files; the first pull request resolves it.

## Tag and release v0.15.0

1. `Directory.Build.props` holds `<VersionPrefix>0.15.0</VersionPrefix>` (the release work of Phase 65 sets it; the tag must equal it, and the release workflow fails otherwise).
2. In `CHANGELOG.md`, rewrite `[Unreleased]` into `## [0.15.0] - <date>` with the real contents, open a new empty `## [Unreleased]`, and update the bottom references: `[Unreleased]: https://github.com/swcstudiospace/OmpOS/compare/v0.15.0...HEAD` and `[0.15.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.14.0...v0.15.0`.
3. `bash scripts/ci/check-changelog.sh` must pass, then `./bootstrap.sh` must pass on the release commit.
4. Fast-forward push `main` (`git merge-base --is-ancestor origin/main HEAD` must succeed), then tag and push the tag:

   ```sh
   git tag -a v0.15.0 -m "OmpOS v0.15.0"
   git push origin v0.15.0
   ```

5. The `release` workflow runs on the self-hosted pool: it checks the tag format, that the tag equals `VersionPrefix`, that the commit is reachable from `origin/main`, runs the changelog check, takes the `## [0.15.0]` section as the release notes and creates the GitHub Release (a tag with a `-suffix`, such as `v0.15.0-rc.1`, becomes a pre-release and uses the `0.15.0` notes). If the pool is offline, create the release by hand with the same notes: `gh release create v0.15.0 --verify-tag --title "OmpOS v0.15.0" --notes-file notes.md`.
6. Verify: `gh release view v0.15.0` and `gh run list --workflow release.yml --limit 1`.

GitHub's automatic "Source code" archives never contain submodules, and `plugins/agent-swarm` and `plugins/agent-substrate` carry no licence file: do not attach an archive that bundles them to the public release without a licence decision (see OQ-93 and the licensing facts in the research file).

The compare links in `CHANGELOG.md` for the milestones before 0.15.0 point to tags that do not exist yet (the repository has no tags): they work once those tags are created. Tagging historical commits is an owner decision; the only commit the records identify for sure is `cbbe22d`, the merge of PR #1 that shipped v0.9.1.

## Verify with the community profile

```sh
gh api repos/swcstudiospace/OmpOS/community/profile --jq '{health_percentage, files: (.files | map_values(. != null))}'
```

Expected: `health_percentage` 100, and every one of `code_of_conduct`, `contributing`, `issue_template`, `pull_request_template` and `license` true, `readme` true. The `documentation` field is derived by GitHub and may point to a branch that does not exist; it does not count against the score. If `license` is false, check that `LICENSE` is the unmodified AGPL-3.0 text on `main` (GitHub's detector can take a few minutes). Then check:

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
