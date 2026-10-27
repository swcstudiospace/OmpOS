# R-GITHUB: presenting OmpOS on GitHub and publishing a GitBook site

Research date 2026-10-27. Repo `swcstudiospace/OmpOS` (public, org-owned, org plan `team`, default branch `main`, HEAD `04e80d1d` on the remote when probed). Tags used: **[P]** = probed on this host (read-only `gh api` GET, `gh --help`, local files); **[D]** = vendor documentation opened (URL given); **[I]** = inferred / from memory, not opened; **[U]** = unverified, owner or main agent must check.
No GitBook credential exists on this host: `env` names containing GITBOOK = none **[P]**. Nothing was written to the repository or to GitHub; the only scratch was `/tmp/research/_svgtest/` (removed at the end).

## 0. Facts probed on this host [P]

| Fact | Value |
|---|---|
| Visibility / owner type | public; Organization `swcstudiospace` (plan `team`, 2FA required, 77 public repos) |
| `gh` | 2.45.0, logged in as `SomeRandmGuyy` (GH_TOKEN), scopes incl. `repo`, `workflow`, `admin:org`, `admin:repo_hook`, `write:discussion`, `project`, `delete_repo` |
| Repo features | issues on, projects on, wiki on, discussions OFF, Pages off (`/pages` 404), merge/squash/rebase all allowed, `delete_branch_on_merge` false, auto-merge off |
| Description | "Web, Desktop & Mobile Native Operating System using GSD-Core's USSR Stack on the Omp Engine. An ADE Harness Cursor-like Super Intelligence" |
| homepage / topics / license | empty / `[]` / null |
| Social preview | none custom: GraphQL `usesCustomOpenGraphImage:false`, `openGraphImageUrl` is the auto-generated `opengraph.githubassets.com` card |
| Community profile | health_percentage 50; has README + CONTRIBUTING; missing CoC, LICENSE, issue template, PR template; `documentation` field = `https://github.com/swcstudiospace/OmpOS/tree/master/docs` (a `master` branch that does not exist; derived field, harmless [I]) |
| `.github/` contents | `workflows/ci.yml` only |
| Org `swcstudiospace/.github` repo | exists (public) but holds only `LICENSE` and `README.md`: no org-wide default health files apply, so every community file must live in this repo |
| Labels | 9 GitHub defaults: bug, documentation, duplicate, enhancement, good first issue, help wanted, invalid, question, wontfix |
| Releases / tags | 0 / 0 |
| Protection | `main` not protected (404 "Branch not protected"), repo rulesets `[]` |
| Security | Dependabot alerts enabled (GraphQL `hasVulnerabilityAlertsEnabled:true`, 0 open); Dependabot security updates disabled; secret scanning, push protection, non-provider patterns, validity checks all `disabled`; private vulnerability reporting `{"enabled":false}`; code scanning default setup `state:"not-configured"` (offered languages: actions, csharp, javascript, javascript-typescript, python, typescript; `runner_type:"standard"`) |
| Actions | enabled, `allowed_actions:all`, `sha_pinning_required:false`; one workflow `ci` (`.github/workflows/ci.yml`) runs only on `[self-hosted, linux, x64, omega-ci]` (comment: "never GitHub-hosted"; fork PRs skipped); org has 2 self-hosted runners registered |
| Languages (bytes) | C# 9,850,602; JavaScript 99,327; HTML 35,241; Shell 26,391; TypeScript 8,204; CSS 7,894; Dockerfile 5,127; Python 1,186 |
| `.gitattributes` | one line: `* text=auto eol=lf` |
| Package manifests tracked in OmpOS | NuGet: central (`Directory.Packages.props`, `ManagePackageVersionsCentrally`, `RestorePackagesWithLockFile`, committed `packages.lock.json` per project, CI `--locked-mode`). npm: only `plugins/gsd-core-omp/package.json` (no lock file found); the other `package.json` files are inside submodules |
| Plugins licensing (from `docs/plans/agent-loop/dossier-git-ci.md`, 2026-10-26) | `claude-ultrathink` AGPL-3.0-or-later; `agent-swarm` and `agent-substrate` (PRIVATE) have no licence file; OmpOS has none |
| Versions in repo | Android `ApplicationDisplayVersion` 0.10.0 (`apps/OmpOS.Android/OmpOS.Android.csproj`); no `<Version>` in `Directory.Build.props`; docs mention v0.9.1 (handoff) and milestone v0.14.0: there is no single version source yet, which a release workflow needs |
| Existing brand tokens | `src/OmpOS.Native.Views/Themes/OmpOSTokens.axaml`: Light `AccentPurple #6d28d9`, `FocusRing #5b21b6`, Surface `#fafaf9`, text `#1c1c1a`; Dark `AccentPurple #a78bfa`, `FocusRing #c4b5fd`, Surface `#181818`, text `#ececec` |

## 1. GitHub presentation and community-health items

Columns: item | current state | exact way to set it | source | notes.

| # | Item | Current | Exact method | Source | Notes |
|---|---|---|---|---|---|
| 1 | Description | set (see §0), long and jargon-heavy | `gh repo edit swcstudiospace/OmpOS -d "<text>"` = `PATCH /repos/swcstudiospace/OmpOS {"description":...}` | gh `--help` [P] | Proposal: "Open-source agent workspace on the omp engine: Cursor-style agents, Kanban, hooks, cloud agents on Railway, desktop, web and Android. AGPL-3.0." Keep under the ~350-character UI limit [I]. |
| 2 | Homepage (About "Website") | empty | `gh repo edit -h <url>` = `PATCH ... {"homepage":...}` | gh `--help` [P] | Set to the published GitBook URL (or the custom domain) once it exists; until then the web-head URL if one is deployed. |
| 3 | Topics | none | `gh repo edit --add-topic t1 --add-topic t2 ...` or `gh api -X PUT repos/swcstudiospace/OmpOS/topics -f 'names[]=ompos' ...` (REST `PUT /repos/{o}/{r}/topics`, body `{"names":[...]}`) | rules: https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/classifying-your-repository-with-topics [D]; endpoint [I] | Rules [D]: lowercase letters, digits, hyphens; at most 50 characters each; at most 20 topics. **Proposed 15:** `ompos`, `omp`, `ai-agents`, `coding-agent`, `agent-workspace`, `cursor-alternative`, `ai-ide`, `dotnet`, `csharp`, `avalonia`, `blazor-webassembly`, `aspnet-core`, `mcp`, `kanban`, `agpl-3` |
| 4 | Social preview image | none custom | **No REST or GraphQL setter exists** (REST update-repository has no field; GraphQL exposes only read fields `openGraphImageUrl`/`usesCustomOpenGraphImage`) **[D+P]**. Manual: repo main page → Settings → "Social preview" → Edit → "Upload an image..." | https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/customizing-your-repositorys-social-media-preview [D] | PNG, JPG or GIF, under 1 MB; at least 640x320, 1280x640 for best display; PNG transparency supported but GitHub recommends a solid background. Only a public repo's image is shared. Commit the source PNG in the repo (e.g. `assets/brand/social-preview-1280x640.png`) so the owner has the file to upload. |
| 5 | README banner and logo, light and dark | README starts with `# OmpOS` text only | `<picture>` with `<source media="(prefers-color-scheme: dark)" srcset=...>` and `<source media="(prefers-color-scheme: light)" ...>` plus an `<img>` fallback (block below) | https://github.blog/changelog/2022-05-19-specify-theme-context-for-images-in-markdown-beta/ [D via search]; "The `<picture>` HTML element is supported": https://docs.github.com/en/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax [D] | A 2022 community report said repo-relative `srcset` paths in `<picture>` were not branch-rewritten; verify render after push, fallback to absolute `https://raw.githubusercontent.com/swcstudiospace/OmpOS/main/<path>` **[U]**. The older `#gh-dark-mode-only` suffix works but needs no reload; `<picture>` needs a reload on theme switch [D via search]. |
| 6 | Badges | none | Markdown in README under the banner | - | Native, no third party: CI `https://github.com/swcstudiospace/OmpOS/actions/workflows/ci.yml/badge.svg` (workflow name `ci`); release `https://img.shields.io/github/v/release/swcstudiospace/OmpOS` and license `https://img.shields.io/github/license/swcstudiospace/OmpOS` (shields.io is third-party [I]); static badges for ".NET 10", "AGPL-3.0", "Docs: GitBook". The CI runner pool was offline on 2026-10-26 (dossier), so a CI badge may read "failing/queued": check before adding. |
| 7 | Issue forms | none | `.github/ISSUE_TEMPLATE/1-bug.yml`, `2-feature.yml`, `3-docs.yml`, `config.yml`; schema keys `name`, `description`, `title`, `labels`, `assignees`, `body[]` with `type: markdown|input|textarea|dropdown|checkboxes`, each with `id`, `attributes`, `validations.required` | https://docs.github.com/en/communities/using-templates-to-encourage-useful-issues-and-pull-requests/configuring-issue-templates-for-your-repository [D] | Files sort alphanumerically, YAML before Markdown; number-prefix to order. **Security is not an issue form** (public issues leak vulnerabilities): put it in `config.yml` `contact_links` pointing at `https://github.com/swcstudiospace/OmpOS/security/advisories/new` (requires PVR, row 10). `config.yml`: `blank_issues_enabled: false` (maintainers still see "Maintainers only" blank issue [D]), `contact_links` for Security, Discussions Q&A and the GitBook site. Bug form fields worth having: head (desktop/web/android/api), OS, OmpOS version/commit, omp version (`omp --version`), plugin availability (Ultrathink/Swarm/Substrate/GSD-Core), provider used, repro steps, logs (render: shell). Labels in forms must already exist (row 15). |
| 8 | PR template | none | `.github/pull_request_template.md` (also allowed: root, `docs/`, or several under `.github/PULL_REQUEST_TEMPLATE/` selected with `?template=`) | https://docs.github.com/en/communities/using-templates-to-encourage-useful-issues-and-pull-requests/creating-a-pull-request-template-for-your-repository [D] | Checklist from CONTRIBUTING.md: `dotnet format` clean, lock files relocked (`dotnet restore --force-evaluate`), tests, CHANGELOG entry, docs updated, TODO policy, parity-audit row, no secrets. The CONTRIBUTING rule "Refs SPE-xxxxx Linear trailer" is internal: mark it "maintainers only" in the template. |
| 9 | CODEOWNERS | none | `.github/CODEOWNERS` (also root or `docs/`): last matching pattern wins; guard the file itself with `/.github/ @owner` | https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners [D] | Owners must be real users/teams with write access: the owner supplies handles (I did not list org teams). Suggested paths: `*`, `/.github/`, `/plugins/`, `/docs/api/openapi.json`, `/gitbook/`. It only blocks merges when a ruleset/branch protection requires code-owner review (row 23: not recommended), otherwise it just auto-requests reviewers. |
| 10 | SECURITY.md + private vulnerability reporting | no file; PVR disabled | File in root, `docs/` or `.github/`; enable PVR: `gh api -X PUT repos/swcstudiospace/OmpOS/private-vulnerability-reporting` (`GET` returns `{"enabled":bool}`; `DELETE` disables) | file locations: https://docs.github.com/en/code-security/getting-started/adding-a-security-policy-to-your-repository [D via search]; endpoints: https://docs.github.com/en/rest/repos/repos [D] | Content: supported versions (latest tag + `main`), report via the advisory form, what is in scope (provider-key store and web key handling, sandbox escape, session/run API auth and tool approvals, plugin isolation, hooks/remote-env execution), out of scope, coordinated disclosure window (owner decides; do not invent numbers). Link it from README and CONTRIBUTING. |
| 11 | CODE_OF_CONDUCT | none | `CODE_OF_CONDUCT.md` in root/`.github/`/`docs/`; text from the Contributor Covenant **2.1** | markdown source: https://www.contributor-covenant.org/version/2/1/code_of_conduct/code_of_conduct.md (linked as "text/markdown" from https://www.contributor-covenant.org/version/2/1/code_of_conduct/ , opened) [D] | The page keeps the placeholder `[INSERT CONTACT METHOD]` under Enforcement: the owner must supply a real contact (not invented here). Keep the Attribution section. The licence of the text is CC BY 4.0 [I]. |
| 12 | SUPPORT.md | none | `.github/SUPPORT.md` (shown on the New issue screen) | GitHub docs "Adding support resources to your project" [I, page not opened] | Point to GitBook docs, Discussions (Q&A category), and the bug/feature forms; state that there is no SLA. |
| 13 | FUNDING.yml | none | omitted (not asked) | - | - |
| 14 | CONTRIBUTING | exists, 52 lines (`CONTRIBUTING.md`) | edit in place | [P] | Covers branches, commit subject `{type}({phase}-{plan}): ...` + Linear trailer, `dotnet format`, lock files, adding a project, plugin submodules, TODO policy. **Gaps for external contributors:** no "getting started / prerequisites" pointer (README has it), no licence statement (inbound = AGPL-3.0, add DCO or an explicit "by contributing you license under AGPL-3.0"), no issue-vs-discussion guidance, no PR process, no CoC/SECURITY link, the GSD/Linear workflow is internal and should be flagged as maintainers-only, no description of the private `agent-substrate` submodule limitation for outside contributors (CI warns when `SUBMODULE_TOKEN` is absent). |
| 15 | Labels | 9 defaults | `gh label create <name> -c <hex> -d <text> [--force]` (loop below) | gh `--help` [P] | Proposed set below; also what `.github/release.yml` and dependabot reference. |
| 16 | Discussions | disabled | `gh repo edit --enable-discussions` (REST `has_discussions` on `PATCH /repos/{o}/{r}`, default false) | gh `--help` [P]; REST field: https://docs.github.com/en/rest/repos/repos [D] | **Categories cannot be created by API** (GraphQL `DiscussionCategory` is read-only; no REST discussions endpoints) [D via search: https://docs.github.com/en/discussions/managing-discussions-for-your-community/managing-categories-for-discussions]. Manual: Discussions tab → pencil beside "Categories" → New category (emoji + title + description + format open-ended / Q&A / announcement); max 25. GitHub seeds default categories on enabling [I]. Proposal: Announcements (announcement), Q&A (Q&A), Ideas, Show and tell, "Providers and plans", "Cloud agents and remote environments", General. After creation, ids are readable: `gh api graphql -f query='{repository(owner:"swcstudiospace",name:"OmpOS"){discussionCategories(first:25){nodes{id name slug}}}}'`. |
| 17 | dependabot.yml | none | `.github/dependabot.yml`, `version: 2`, `updates[]` per ecosystem (block below) | https://docs.github.com/en/code-security/dependabot/ecosystems-supported-by-dependabot/supported-ecosystems-and-repositories [D via search: values `nuget`, `npm`, `github-actions`] | **Risk [D via search + P]:** CI restores with `--locked-mode` and every project commits `packages.lock.json`; Dependabot's NuGet updater edits `Directory.Packages.props` and community reports are mixed on whether it regenerates lock files (some say it runs `dotnet restore --force-evaluate`, one repo saw NU1004). Test with the first Dependabot PR before relying on it. Submodule contents are not scanned: only `plugins/gsd-core-omp` is a tracked npm project. Fork/Dependabot PRs: `ci.yml` skips fork PRs, Dependabot PRs come from the same repo and will run on the self-hosted pool. |
| 18 | `.github/release.yml` | none | file `.github/release.yml` with `changelog.exclude.{labels,authors}` and `changelog.categories[{title,labels[]}]`; `labels: ['*']` is the catch-all | https://docs.github.com/en/repositories/releasing-projects-on-github/automatically-generated-release-notes [D] | Block below. Used only when releases are created with `--generate-notes` / "Generate release notes". |
| 19 | Release workflow (tag → GitHub Release) | none; 0 tags | `.github/workflows/release.yml`, trigger `push: tags: ['v*.*.*']`, `permissions: contents: write`, `gh release create "$GITHUB_REF_NAME" --verify-tag --title ... --notes-file <CHANGELOG section>` or `--generate-notes` | `gh release create --help` [P] | Block below. **Constraints:** (a) runs on `[self-hosted, linux, x64, omega-ci]` like ci.yml (policy comment), (b) GitHub's auto "Source code" zip/tar.gz never include submodules (verified by multiple reports [D via search: https://github.com/orgs/community/discussions/6003]), and `agent-substrate` is private and unlicensed, so a "full source" asset including it must NOT be attached to a public AGPL release without the owner's licence decision, (c) there is no single version source; decide one (e.g. `<Version>` in `Directory.Build.props`) and have the workflow fail when tag != version, (d) signing/notarisation/Android keystore are not on this host **[U]**, so binaries (Linux `dotnet publish`, web `wwwroot`, unsigned APK) are optional assets, (e) after the first tag, GitHub immutable-release settings are an owner choice **[U]**. |
| 20 | Code scanning (CodeQL) | not configured | `gh api -X PATCH repos/swcstudiospace/OmpOS/code-scanning/default-setup -f state=configured -f query_suite=default -f 'languages[]=csharp' -f 'languages[]=javascript-typescript' -f 'languages[]=python' -f 'languages[]=actions'`; alternative advanced setup = a `codeql.yml` workflow | GET probed [P]; availability: free for public repos, C# built with no-build mode [D via search: https://docs.github.com/en/code-security/code-scanning/enabling-code-scanning/configuring-default-setup-for-code-scanning] | **Conflict with repo policy:** default setup uses `runner_type:"standard"` (GitHub-hosted) while CI forbids GitHub-hosted runners; the GET shows `runner_type`/`runner_label` fields, so a labelled self-hosted runner (`runner_type=labeled`, `runner_label=omega-ci`) is the policy-compatible choice [I: field names read from the GET response, PATCH values not tried]. Scanning the plugin submodules is out of scope. |
| 21 | Secret scanning + push protection | both disabled | `gh api -X PATCH repos/swcstudiospace/OmpOS --input - <<< '{"security_and_analysis":{"secret_scanning":{"status":"enabled"},"secret_scanning_push_protection":{"status":"enabled"}}}'` (also `secret_scanning_non_provider_patterns`, `secret_scanning_validity_checks`) | field list [P+D: https://docs.github.com/en/rest/repos/repos]; free on public repos [D via search: https://github.blog/news-insights/product-news/push-protection-is-generally-available-and-free-for-all-public-repositories/] | The 2024 "on by default" change covers only new personal-account public repos; org repos (this one) must be enabled by hand; org defaults for new repos are also off [P]. A per-user push protection is on by default for the pusher on public repos [D via search]. The repo already has a fail-closed `scripts/ci/secret-scan.sh` in CI; this adds server-side blocking. |
| 22 | Dependabot alerts and security updates | alerts on, security updates off | `gh api -X PUT repos/swcstudiospace/OmpOS/automated-security-fixes` (`DELETE` disables); alerts: `PUT .../vulnerability-alerts` | https://docs.github.com/en/rest/repos/repos [D] | Security-update PRs hit the same lock-file risk as row 17. |
| 23 | Branch protection / rulesets | none (404), `rulesets: []`, org rulesets `[]` (dossier) | **Recommendation: enable nothing that blocks the owner's pushes.** Every default branch protection option (required PR, required status checks, linear history, restrict pushes) would reject the owner's direct fast-forward pushes or the GitBook app's commits; the CI runner pool was offline on 2026-10-26 so required checks would deadlock merges. If the owner later wants a floor, the only low-risk ruleset is `deletion` + `non_fast_forward` on `refs/heads/main` (blocks branch deletion and force pushes, still allows fast-forward pushes) via `POST /repos/{o}/{r}/rulesets` with `enforcement:"active"`; add the GitBook app and the owner as bypass actors. | rulesets: https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets [D via CODEOWNERS page link]; GitBook push requirement: gitbook troubleshooting (below) [D] | GitBook Git Sync pushes straight to the synced branch and "requires the GitBook app to push changes without restrictions, including during setup"; with protection the bot must bypass (`gitbook-com`). |
| 24 | About sidebar | description/website/topics as above | Set via REST/gh (rows 1-3). **"Include in the home page" toggles (Releases, Packages, Deployments) are UI-only**: no REST or GraphQL field; tracked in cli/cli#8755 (blocked) [D via search]. Manual: gear beside "About". Feature flags with API: `has_issues`, `has_wiki`, `has_projects`, `has_discussions`. | https://github.com/cli/cli/issues/8755 [D via search] | Recommended state: Releases ON (after first release), Packages OFF, Deployments OFF; wiki OFF once GitBook is live (`gh repo edit --enable-wiki=false`) to avoid a second doc surface; Discussions ON; Projects keep (owner uses it). Optional: `--delete-branch-on-merge`. |
| 25 | LICENSE (AGPL-3.0) | none (`/license` 404) | root `LICENSE` with the unmodified GNU AGPL-3.0 text from https://www.gnu.org/licenses/agpl-3.0.txt [I, not opened] so GitHub's detector reports `AGPL-3.0`; add SPDX `AGPL-3.0-only` or `-or-later` consistently (the sibling `claude-ultrathink` uses `AGPL-3.0-or-later`, copyright "SWC Studio" [P via dossier]); add `<PackageLicenseExpression>` in `Directory.Build.props`; per-file headers optional | dossier §5 [P] | **Open licensing facts for the owner:** `agent-swarm` and private `agent-substrate` have no licence; OmpOS links them only as submodule gitlinks, but any archive that bundles them redistributes unlicensed code. AGPL-3.0 §13 (network use) applies to the hosted `services/OmpOS.Api` and the web head. Fonts: OFL texts already ship in `src/OmpOS.Native.Views/Assets/Fonts/*/OFL.txt`; keep them (OFL needs the notice with redistribution). |
| 26 | Language bar accuracy | JS 99 KB etc. | `.gitattributes` lines (below) | https://github.com/github-linguist/linguist/blob/main/docs/overrides.md [D] | Bootstrap under `apps/OmpOS.Web/wwwroot/lib/` is vendored JS; mark `linguist-vendored`; mark evidence and plans `linguist-documentation`. Keep the existing `* text=auto eol=lf`. |
| 27 | Repository merge settings | all three merge methods on | `gh repo edit --enable-squash-merge ... --delete-branch-on-merge` | gh `--help` [P] | Optional; no impact on fast-forward pushes. |

### 1.1 Proposed file bodies (for the main agent to create; nothing created by this scout)

README header (put at the very top of `README.md`; paths assume `assets/brand/`):
```html
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/brand/banner-dark-1280x320.png">
    <source media="(prefers-color-scheme: light)" srcset="assets/brand/banner-light-1280x320.png">
    <img alt="OmpOS: the agent workspace on the omp engine" src="assets/brand/banner-light-1280x320.png" width="100%">
  </picture>
</p>
```

`.github/dependabot.yml`:
```yaml
version: 2
updates:
  - package-ecosystem: nuget
    directory: "/"
    schedule: { interval: weekly }
    labels: [dependencies, nuget]
    groups:
      nuget-minor-patch: { update-types: [minor, patch] }
  - package-ecosystem: github-actions
    directory: "/"
    schedule: { interval: weekly }
    labels: [dependencies, github-actions]
  - package-ecosystem: npm
    directory: "/plugins/gsd-core-omp"
    schedule: { interval: weekly }
    labels: [dependencies, javascript]
```
(`ci.yml` pins actions by full SHA with a version comment, which Dependabot understands [I]. If Dependabot NuGet PRs fail NU1004, either add a lock-refresh job or gate `RestoreLockedMode` on `DEPENDABOT_JOB_ID` being empty, a workaround reported in the wild [D via search].)

`.github/release.yml`:
```yaml
changelog:
  exclude:
    labels: [ignore-for-release]
  categories:
    - { title: "Breaking changes", labels: [breaking-change] }
    - { title: "Features", labels: [enhancement] }
    - { title: "Providers and plans", labels: [area/providers] }
    - { title: "Fixes", labels: [bug] }
    - { title: "Documentation", labels: [documentation] }
    - { title: "Dependencies", labels: [dependencies] }
    - { title: "Other changes", labels: ["*"] }
```

`.github/ISSUE_TEMPLATE/config.yml`:
```yaml
blank_issues_enabled: false
contact_links:
  - name: Report a security vulnerability (private)
    url: https://github.com/swcstudiospace/OmpOS/security/advisories/new
    about: Do not open a public issue for vulnerabilities.
  - name: Questions and ideas
    url: https://github.com/swcstudiospace/OmpOS/discussions
    about: Use Discussions for how-to questions and proposals.
```

`.github/workflows/release.yml` (shape; pins must be copied from `ci.yml`):
```yaml
name: release
on:
  push:
    tags: ['v[0-9]+.[0-9]+.[0-9]+*']
permissions:
  contents: read
jobs:
  release:
    runs-on: [self-hosted, linux, x64, omega-ci]
    permissions:
      contents: write
    steps:
      - uses: actions/checkout@<same SHA as ci.yml>   # v7
        with: { fetch-depth: 0 }
      - run: ./bootstrap.sh            # same gate as CI; fail the release on red
      - name: Notes from CHANGELOG.md
        run: |
          v="${GITHUB_REF_NAME#v}"
          awk -v v="$v" '$0 ~ "^## \\[" v "\\]" {f=1; next} /^## \[/ {f=0} f' CHANGELOG.md > notes.md
          test -s notes.md
      - name: Create the release
        env: { GH_TOKEN: "${{ github.token }}" }
        run: |
          pre=""; case "$GITHUB_REF_NAME" in *-*) pre="--prerelease";; esac
          gh release create "$GITHUB_REF_NAME" --verify-tag --title "OmpOS $GITHUB_REF_NAME" --notes-file notes.md $pre
          # optional assets: gh release upload "$GITHUB_REF_NAME" <file> --clobber
```

Labels (run once; colours follow the brand purple family and neutral greys):
```sh
while IFS='|' read -r n c d; do gh label create "$n" -c "$c" -d "$d" -R swcstudiospace/OmpOS --force; done <<'E'
bug|b3261e|Something is broken
enhancement|6d28d9|New feature or improvement
documentation|0ea5e9|Docs, GitBook, README, changelog
question|a78bfa|Needs an answer, not a change
needs-triage|e4e4e0|Not yet reviewed
needs-repro|fbbf24|Waiting for a reproduction
blocked|6f6f6b|Blocked by something outside the issue
breaking-change|b3261e|Changes a public contract or behaviour
security|b3261e|Security-relevant (never put details in public issues)
dependencies|0366d6|Dependency update
github-actions|2b3137|Workflow or action update
nuget|0366d6|NuGet update
javascript|f7df1e|npm update
ignore-for-release|e4e4e0|Excluded from generated release notes
priority/p0|b3261e|Drop everything
priority/p1|f97316|Next
priority/p2|facc15|Soon
priority/p3|d4d4d4|Someday
area/desktop|c4b5fd|Avalonia desktop head
area/web|c4b5fd|Blazor WebAssembly head
area/android|c4b5fd|Android head
area/api|c4b5fd|services/OmpOS.Api
area/engine-omp|c4b5fd|omp engine integration, ACP, steering
area/plugins|c4b5fd|Ultrathink, Swarm, Substrate, GSD-Core
area/settings|c4b5fd|Settings window and parity rows
area/slash-commands|c4b5fd|Slash command palette and discovery
area/hooks|c4b5fd|Hooks
area/cloud-agents|c4b5fd|Cloud agents on Railway
area/remote-env|c4b5fd|Remote environments
area/multi-repo|c4b5fd|Multi-repo workspaces
area/providers|c4b5fd|Model providers and coding plans
area/docs|0ea5e9|Documentation site
area/ci|c4b5fd|CI and release
E
```
(keep the existing `good first issue`, `help wanted`, `duplicate`, `invalid`, `wontfix`.)

`.gitattributes` additions:
```gitattributes
apps/OmpOS.Web/wwwroot/lib/** linguist-vendored
docs/runbooks/evidence/** linguist-documentation
gitbook/** linguist-documentation
```

One-shot repo settings (idempotent; the owner may run after review):
```sh
gh repo edit swcstudiospace/OmpOS --enable-discussions --enable-wiki=false --delete-branch-on-merge \
  --add-topic ompos --add-topic omp --add-topic ai-agents --add-topic coding-agent --add-topic agent-workspace \
  --add-topic cursor-alternative --add-topic ai-ide --add-topic dotnet --add-topic csharp --add-topic avalonia \
  --add-topic blazor-webassembly --add-topic aspnet-core --add-topic mcp --add-topic kanban --add-topic agpl-3
gh api -X PUT repos/swcstudiospace/OmpOS/private-vulnerability-reporting
gh api -X PUT repos/swcstudiospace/OmpOS/automated-security-fixes
gh api -X PATCH repos/swcstudiospace/OmpOS --input - <<'J'
{"security_and_analysis":{"secret_scanning":{"status":"enabled"},"secret_scanning_push_protection":{"status":"enabled"}}}
J
```
Manual-only afterwards: social preview upload (row 4), discussion categories (row 16), "Include in the home page" toggles (row 24).


## 2. Logo and banner production on this host

### 2.1 Toolchain probes (all in `/tmp/research/_svgtest/`, nothing outside; removed afterwards) [P]

| Tool | Result |
|---|---|
| `google-chrome` | 153.0.8010.36. Headless works as root with `--headless=new --no-sandbox --disable-gpu`. `--screenshot` gave a 128x64 PNG for a 128x64 SVG with exact colours; `--force-device-scale-factor=2 --window-size=640,160` gave a 1280x320 PNG. `--print-to-pdf` with `--no-pdf-header-footer` and `@font-face src:url(file:///...ttf)` (plus `--allow-file-access-from-files`) embedded the three bundled faces (pdffonts: `Anybody-SemiBold`, `DMSans-Medium`, `SpaceMono-Bold`, all TrueType subsets). No system copies of the fonts (`fc-list` finds none), so `@font-face` is mandatory. |
| ImageMagick `convert` 6.9.12-98 | SVG format is `SVG (XML 2.9.14)` and `MSVG`; the delegate line `svg => rsvg-convert` exists but **`rsvg-convert` is not installed**, so IM falls back to its internal MSVG renderer. It rendered a plain rect+circle correctly (corner pixel `srgb(124,58,237)`, circle centre white) but **failed on the outlined-text SVG** (`unbalanced graphic context push-pop`, `non-conforming drawing primitive definition 'use'`) and returned a wrong-size image. Verdict: use IM only for PNG→ICO/resize/montage, never to rasterise SVG. `convert t.png t2.ico` works (wrote a valid `MS Windows icon resource`). `identify`, `optipng` present. |
| `cairosvg` 2.9.0 (CLI `/usr/local/bin/cairosvg` and Python module), `cairocffi` 1.7.1, Pillow 12.3.0, `reportlab` 5.0.1, `brotli` 1.2.0 | `cairosvg lock.svg -o out.png -s 2` rasterised the outlined-text SVG correctly (960x240, inspected visually). This is the recommended SVG→PNG path. |
| `pdftocairo` (poppler), `gs` | `pdftocairo -svg in.pdf out.svg` converts Chrome's PDF to an SVG whose text is **glyph outlines**: 0 `<text>`, 32 `<path>`, glyphs defined as `<g id="glyph-N-M"><path d=.../></g>` and placed with `<use>`. No font needed to display it. Size is in pt (640 CSS px → `width="480" height="120" viewBox="0 0 480 120"`), so keep the `viewBox` and set `width/height` as needed. |
| Python `fontTools` | **NOT installed** in `/usr/bin/python3`, nor in the `.venv`s of `ai-agency`, `grok-*`, `hermes-engineering-board` or `docs-scraper` (the brief assumed it was present). `pip3 list` shows brotli but no fonttools. Installing is outside this scout's read-only rules. If the owner wants direct TTF→SVG-path conversion or WOFF2 files for GitBook, create a venv and `pip install fonttools brotli` (main agent decision). |
| `node` v24.18.0, `bun` 1.4.0, `python3` 3.12.3 | present. Global npm modules: no `opentype.js`, `svgo`, `sharp`, `resvg` (global list checked). `typescript`, `pnpm`, `vercel`, `pyright` exist. |
| Not found by `which` | `rsvg-convert`, `inkscape`, `magick`, `potrace`, `svgo`, `icotool`, `pdf2svg`, `mutool`, `fontforge`, `woff2_compress`, `hb-view`, `resvg`. No `.icns` writer (`png2icns`, `iconutil`) was tested: macOS `.icns` needs macOS or `libicns` **[U]**. |
| Bundled fonts (`src/OmpOS.Native.Views/Assets/Fonts`) | Static TTFs, TrueType outlines (`glyf`, no `fvar`, `GSUB` present): DMSans-Regular / Medium / SemiBold (about 78 KB each), Anybody-SemiBold (65 KB), SpaceMono-Regular / Bold (about 99 KB). One `OFL.txt` per family. Note: no DM Sans Bold, no Anybody weights other than SemiBold. |

### 2.2 Method (vector first, text as outlines)

1. Author the **mark** directly as SVG geometry (no text): square `viewBox="0 0 512 512"`, flat shapes, purple `#6d28d9` on white (light) and `#a78bfa` on `#181818` (dark), taken from the existing Avalonia tokens so desktop, web and docs match. It must stay legible at 16 px (one or two shapes, no hairlines).
2. Author each **text-bearing asset** (wordmark lockup, banner, social card) as an HTML page using `@font-face` pointing at the bundled TTFs (`OmpOS` in Anybody SemiBold, tagline in DM Sans Medium, version/licence chips in Space Mono Bold), with an explicit background `div` and the mark inlined.
3. Chrome `--print-to-pdf` (set `@page { size: <w>px <h>px; margin: 0 }`) → `pdftocairo -svg` → **outlined SVG** (verified above). Post-check: `grep -c "<text"` must be 0 and `fonts` must not be referenced.
4. Rasterise **from the outlined SVG** with `cairosvg -s <scale>` (or from the HTML with Chrome `--screenshot --force-device-scale-factor`), then `optipng -o7`. Compose `.ico` with IM from the PNG set.
5. Commit sources under `assets/brand/` (SVG masters + generated PNGs) and a small script `scripts/brand/build.sh` documenting the exact commands, so the files are reproducible. Recommended single source of truth for colours: the tokens file above.
6. A transparent PNG is fine for logos, but the social preview and Android adaptive background should be opaque.

### 2.3 Export matrix

| Asset | Size / format | Used by | Notes |
|---|---|---|---|
| `logo-mark.svg`, `logo-mark-dark.svg` | 512x512 viewBox, SVG | everywhere (master) | geometry only |
| `logo-mark-512.png` | 512x512 PNG, transparent | GitBook, Play listing, docs | from SVG via cairosvg |
| `logo-lockup-light.svg`, `logo-lockup-dark.svg` | about 4:1, e.g. viewBox 960x240, outlined text | README/GitBook | wordmark "OmpOS" Anybody SemiBold |
| `logo-lockup-light-1200x300.png`, `-dark-` | 1200x300 PNG | GitBook site logo (light and dark uploads) | GitBook: upload one per mode, "at least 600 px wide" recommended [D: https://gitbook.com/docs/manage-your-site/customization/icons-colors-and-themes, offline skill ref 093] |
| GitBook site icon | 132x132 PNG | GitBook favicon/title icon | [D, same page] |
| `banner-light-1280x320.png`, `banner-dark-1280x320.png` (+ SVG masters) | 1280x320 | README `<picture>` | light = white background, purple type; dark = `#181818`, `#a78bfa` |
| `social-preview-1280x640.png` | 1280x640 PNG, opaque, under 1 MB | GitHub Settings → Social preview | [D: social preview doc]; `optipng`; upload is manual |
| `favicon.svg` | SVG | modern browsers (`<link rel="icon" type="image/svg+xml">`) | |
| `favicon-16.png`, `favicon-32.png`, `favicon.ico` (16, 32, 48 in one file) | PNG / ICO | web head, GitBook fallback | IM: `convert f16.png f32.png f48.png favicon.ico` [I: multi-image form not run, single-image ICO was] |
| `apple-touch-icon.png` | 180x180, opaque | iOS home screen | |
| `icon-192.png`, `icon-512.png`, `icon-maskable-512.png` | 192, 512, 512 (maskable keeps the mark inside the central 80% [I]) | web manifest (`manifest.webmanifest`), PWA | replaces the Blazor-template `favicon.png`/`icon-192.png` in `apps/OmpOS.Web/wwwroot/`; `index.html` currently has only `<link rel="icon" type="image/png" href="favicon.png" />` and `<title>OmpOS.Web</title>` [P] |
| Android launcher (legacy) | mipmap mdpi 48, hdpi 72, xhdpi 96, xxhdpi 144, xxxhdpi 192 px [I] | `apps/OmpOS.Android/Resources/mipmap-*` | currently only `Resources/values/styles.xml` exists; `[Activity]` has no `Icon`, csproj has no icon [P] |
| Android adaptive | foreground, background (flat purple) and monochrome layers, all 108x108 dp (px at 1x,1.5x,2x,3x,4x = 108, 162, 216, 324, 432); keep the logo between 48 and 66 dp, the inner 66 dp is the never-clipped safe zone; `res/mipmap-anydpi-v26/ic_launcher.xml` with `<foreground>`, `<background>`, `<monochrome>` | Android head | [D: https://developer.android.com/develop/ui/views/launch/icon_design_adaptive] ; wire with `Icon = "@mipmap/ic_launcher"` on the activity/application [I] |
| Play/store icon | 512x512 PNG [I] | listing | not needed for a sideloaded APK |
| Avalonia window icon | 256 PNG (and 32, 48) or `.ico`, embedded as an `AvaloniaResource` and set via `Window.Icon`/`WindowIcon` [I] | `apps/OmpOS.Native` | currently no icon in `MainWindow.axaml` or the csproj [P] |
| Windows exe icon | `.ico` with 16, 24, 32, 48, 64, 128, 256 via `<ApplicationIcon>` [I] | Native head publish | |
| Linux desktop entry | 256 and 512 PNG + `.desktop` `Icon=` [I] | packaging | |
| macOS | `.icns` 16 to 1024 | Native head publish | tool not on host **[U]** |
| `.github/` and docs | `assets/brand/` copies referenced from README, `gitbook/.gitbook/assets/` copies for GitBook | | keep one master, copy in the build script |

Fonts in the docs site: GitBook custom fonts accept **only `.woff`/`.woff2`**, need regular **and bold** for the standard font and for the monospace font, one standard + one monospace family in total [D: offline skill ref 093]. So GitBook can carry DM Sans (standard) and Space Mono (monospace, Regular + Bold are bundled); **Anybody cannot be a GitBook body font**, it can only appear in logo/banner images. DM Sans has no bundled Bold (SemiBold 600 is the nearest; upstream Bold would be an OFL download, not fetched). Converting the bundled TTFs to WOFF2 needs `fonttools`+`brotli` (see §2.1). Custom-font availability by plan must be checked in the GitBook UI (the offline plans page lists "Customizable logo, theme and fonts" under Premium and "Custom fonts" under Ultimate; the live pricing page opened 2026-10-27 renamed the tiers, see §3.6) **[U]**.


## 3. GitBook: repository layout, configuration surface, validation, owner steps

Sources: offline skill `skill://docs-gitbook` (references 063, 064, 065, 069, 070, 073, 074, 093, 100, 128, 134, 167; scraped 2026-08-22) cross-checked against live pages opened 2026-10-27: https://gitbook.com/docs/skill/write-docs.md and https://www.gitbook.com/pricing. Anything not in those pages is marked [U] or [I].

### 3.1 Exact repository layout for GitHub Git Sync

| Element | Requirement | Source |
|---|---|---|
| Repo / branch | One repository and one branch GitBook syncs with (GitBook creates the branch if missing). Two-way: merging a change request in GitBook commits to GitHub; commits to GitHub import into GitBook. Pull requests and change requests are NOT synced to each other. | https://gitbook.com/docs/docs-as-code/git-sync/enabling-github-sync (ref 070), troubleshooting (ref 074) |
| Project directory | Wizard field "Project directory": the folder that holds the site's `docs.yaml`; empty = repo root. **Recommended: `gitbook`** so the engineering docs in `docs/` (plans, dossiers, evidence logs) are never published. | ref 070 ("If your documentation lives in a subdirectory... GitBook stores your site's `docs.yaml` file in this directory") |
| Content mapping | "assign each space to a directory"; `./` = relative to the project directory, `/` = from the repo root. **Recommended: one space "Documentation" mapped to `./`** (so everything lives under `gitbook/`). | ref 070 |
| `docs.yaml` | Created/updated by GitBook when it saves the content mapping; schema `$schema: https://api.gitbook.com/openapi.yaml#/components/schemas/GitSyncSiteConfig`, `site.title`, `site.structure[]` of `type: section | section-group | space`, each with stable `key`, `title`, `path`, `children`, `content.directory`, `content.language`, optional `description`, `icon`, `localizedTitle`, `hidden`, `default: true`. Do NOT hand-author it; let the first sync write it, then commit what GitBook produces **[I: its exact minimal form for a single space was not shown in the docs]**. | ref 069 |
| `.gitbook.yaml` | Optional, lives in the space's mapped directory (`gitbook/.gitbook.yaml`). Keys: `root` (default `./`), `structure.readme` (default `README.md`), `structure.summary` (default `SUMMARY.md`), `redirects` (`old/path: new/page.md`, space-scoped). Paths inside are relative to `root`. | ref 069 |
| Landing page | `README.md` in the root = first page. "When Git Sync is enabled, manage README.md files in your repository"; editing them in GitBook can create conflicts or duplicate pages. | ref 069, write-docs.md |
| Navigation | `SUMMARY.md` in the root. If absent GitBook infers navigation from folders and `.md` files; GitBook rewrites `SUMMARY.md` when navigation changes in the app. | ref 069 |
| `SUMMARY.md` grammar | `# Summary` title; `## Heading` = page group; `* [Title](path.md)` list items, nested by indentation = child pages; optional nav label: `* [Page main title](page.md "Navigation label")`; relative `.md` paths; **each Markdown file may appear once** (a page has one URL per space); keep link text in sync with the page's `#` title; GitBook may re-serialise (lint for validity, not for style). | ref 069; https://gitbook.com/docs/skill/write-docs.md |
| Assets | `.gitbook/assets/` under the root holds uploaded images and files (referenced like `../../.gitbook/assets/x.svg` from a nested page, as in the Hints reference, ref 023). Max 100 MB per file in Git Sync (ref 074). Keep images small; screenshots from `docs/runbooks/evidence/` are large and must be copied, not referenced. | refs 023, 074, write-docs.md |
| Other `.gitbook/` entries | `.gitbook/vars.yaml` (space variables, `space.vars.<name>`), `.gitbook/includes/` (reusable content, `{% include %}`), tags; page variables go in front matter `vars:`. | https://gitbook.com/docs/skill/write-docs.md |
| Front matter | Top of file, valid YAML: `description` (always quote values containing `:` or `#`: unquoted ones cause **silent** Git Sync failures), `icon` (Font Awesome name), `hidden: true`, `vars:`, `layout: {width: default|wide, tableOfContents: {visible}, pagination: {visible}}`, covers and `if:` conditions. | write-docs.md |
| Blocks | Markdown plus `{% hint %}`, `{% tabs %}{% tab %}`, `{% stepper %}`, `{% columns %}`, `{% updates %}` (changelog), `{% file %}`, `{% include %}`, `<details>`, `<table data-view="cards">`, always closed. | write-docs.md, refs 015-023 |
| Links | Relative `.md` links inside the space; cross-space links need `https://app.gitbook.com/s/<spaceId>/<path>`; moved pages get automatic redirects. | write-docs.md |
| Protected branch | `main` is unprotected today. If protection is ever added, GitBook (`gitbook-com`) must be allowed to bypass "Require a pull request" and "Restrict who can push" or sync fails with `Missing permissions to push to the refs/heads/main protected branch`. | ref 074 |
| Monorepo note | Content is only read from the mapped directory; `root` does not expose siblings, so pages cannot embed `../docs/...` files outside `gitbook/` **[I: inferred from "root only applies inside the mapped space directory"]**. Hence curated copies, not links, of the engineering docs. | ref 073 |
| Orphans | Whether a `.md` not listed in `SUMMARY.md` is published or ignored is not stated in the docs **[U]**: the checker below reports orphans as warnings so none exist. | - |

Resulting tree to create (proposal; nothing created by this scout):
```
gitbook/
  .gitbook.yaml              # root: ./   structure: {readme: README.md, summary: SUMMARY.md}
  .gitbook/assets/           # logos, banner, screenshots (copied, optimised)
  .gitbook/vars.yaml         # product_name, current_version, repo_url ...
  README.md                  # landing
  SUMMARY.md
  getting-started/  using-ompos/  concepts/  settings-and-providers/  slash-commands/
  hooks/  cloud-agents/  remote-environments/  multi-repo/  architecture/
  api-reference/  runbooks/  contributing/  changelog.md  licensing.md
  docs.yaml                  # written by GitBook at first sync (commit it)
```

### 3.2 What lives in files versus only in the GitBook UI

| Setting | In files (Git Sync) | UI / API only | Source |
|---|---|---|---|
| Pages, structure, landing, redirects (space level) | yes: `.md`, `SUMMARY.md`, `README.md`, `.gitbook.yaml` | | ref 069 |
| Site title, section/space tree, hidden spaces, icons | yes: `docs.yaml` (written by GitBook) | | ref 069 |
| Variables | space: `.gitbook/vars.yaml`; page: front matter | creating via Library UI is also possible | write-docs.md, ref 013 |
| Reusable content | `.gitbook/includes/` | | write-docs.md |
| Theme (Clean, Muted default for all sites; Bold and Gradient listed Premium/Ultimate in the offline plan text), primary colour, tint, corner and depth style | no | Site → Customize | ref 093 |
| Logo (light + dark upload, at least 600 px wide), icon (132x132, used as favicon), title shown on site | no (title partly via `docs.yaml`) | Customize | ref 093 |
| Fonts (standard + monospace; custom `.woff/.woff2`, regular AND bold each) | no | Customize → Font family / Custom fonts | refs 093, 128 |
| Custom domain (CNAME at DNS, SSL automatic; Cloudflare proxy must be off; CAA `0 issue "pki.goog"` if CAA used) or custom subdirectory | no | Site → Settings → Domain and URL | ref 030 |
| Site-level redirects, SEO, social sharing image, PDF export, analytics cookie, integrations (Google Analytics etc.), AI Search/Assistant | no | Site settings | refs 035, 037, 090, 095 |
| Site visibility (public by default) and "Publish" | no | site header **Publish**; Settings → Audience | ref 034 |
| "Edit on GitHub" link | no | Configure tab toggle (section must be connected to a repo) | ref 092 |
| OpenAPI specs | spec file lives in repo, but **registration must be done in UI/API/MCP/CLI, "not embedded in markdown"** | yes | write-docs.md, ref 167 |
| Git Sync connection itself (repo, branch, direction, project directory, commit template, fork previews) | no | Site → Git Sync | ref 070 |

### 3.3 OpenAPI reference for `docs/api/openapi.json`

Facts [P]: OpenAPI **3.1.1**, title `OmpOS.Api | v1`, version `1.0.0`, 9 paths / 10 operations (`GET /sessions`, `POST /sessions`, `GET /sessions/{id}`, `GET .../events`, `POST .../prompts`, `POST .../runs`, `GET .../runs/{runId}`, `GET .../output`, `POST .../cancel`, `POST .../tool-approvals/{requestId}`), tags `SessionEndpoints`, `RunEndpoints`, `EventStreamEndpoints`, `ToolApprovalEndpoints`, `servers: [{"url":"/"}]`, **no `securitySchemes`**, only `components.schemas`. A "Test it" runner needs a real server URL and auth scheme; neither is in the spec and no public API hostname appears in `docs/runbooks/hosting.md` **[P]**; add `servers` and security when the hosted API URL exists.

| Route | Exact steps | Needs token? | Source |
|---|---|---|---|
| A. URL source (no token) | GitBook UI → sidebar **OpenAPI** → **Add specification** → name `ompos-api` → "URL" = `https://raw.githubusercontent.com/swcstudiospace/OmpOS/main/docs/api/openapi.json`. Probed: HTTP 200, `Access-Control-Allow-Origin: *`, `Cache-Control: max-age=300` [P]. URL sources refresh automatically every 6 hours, or **Check for updates**. | no | ref 167 (UI path, 6 h refresh, CORS note) |
| B. File source via CLI (CI) | `npm install @gitbook/cli -g` (Node 18+), `export GITBOOK_TOKEN=<personal API token from https://app.gitbook.com/account/developer>`, `gitbook openapi publish --spec ompos-api --organization <orgId> docs/api/openapi.json`. Same command updates; against a URL it forces a refresh. GitHub Actions example uses `npx -y @gitbook/cli@latest openapi publish --spec "$GITBOOK_SPEC_NAME" --organization "$GITBOOK_ORGANIZATION_ID" <path>` with `GITBOOK_TOKEN` as a secret. | yes | refs 065, 167 |
| C. File source via REST | `curl -X POST -H "Authorization: Bearer $GITBOOK_TOKEN" -F slug=ompos-api -F file=@docs/api/openapi.json https://api.gitbook.com/v1/orgs/$ORG_ID/openapi`; update with `PATCH /v1/orgs/{orgId}/openapi/{specId}` | yes | ref 167 |
| Insert into docs | In the space table of contents: **Add new... → OpenAPI Reference** → pick the spec: one page per tag plus an optional models page, kept updated when the spec changes; or press `/` → OpenAPI to embed one operation/schema. Resulting pages are Markdown with `{% openapi ... %}` / `{% openapi-schemas spec="ompos-api" ... %}` blocks and should be committed from the Git Sync export **[I: generated pages sync back to the repo; exact `src` attribute for a stored spec is produced by the UI, do not hand-write it]**. | no | ref 167 |
| Supported versions | Swagger 2.0, OpenAPI 3.0, 3.1 (webhooks need 3.1) | | refs 009, 167 |

Recommendation: route A first (no secret, spec stays single-sourced in the repo); move to B only if the spec must be versioned per release.

### 3.4 Offline validation

`scripts/docs/check_gitbook.py` below was written and **run on this host** against a synthetic tree (catches: duplicate page in SUMMARY.md, missing file, non-list line, nesting jump, orphan page, broken relative link/image, unbalanced `{% %}` blocks and fences, unclosed front matter, unquoted `description:` with `:`/`#`, files over 100 MB; exit code 1 on error; clean tree returned 0). It is stdlib-only Python 3 and does not need PyYAML. Anchors are only warnings because GitBook's slug rules are not documented here. Usage: `python3 scripts/docs/check_gitbook.py gitbook`. Add it to the existing gate (`bootstrap.sh`) and optionally a `docs-check` workflow on the same self-hosted pool, and `python3 -m json.tool docs/api/openapi.json >/dev/null` for the spec. Not offline-checkable: whether GitBook accepts a block, the final slug of a page, theme/domain/plan limits.

```python
#!/usr/bin/env python3
"""Offline structure check for a GitBook Git Sync content root (stdlib only).
Usage: check_gitbook.py <gitbook-dir>   (the directory that holds .gitbook.yaml / SUMMARY.md)
Exit 1 on any ERROR. Checks: .gitbook.yaml root/readme/summary, SUMMARY.md grammar and indentation,
every linked page exists, no page listed twice, no orphan .md, relative links/images/anchors,
front matter closed, ``` fences and {% %} blocks balanced, files under 100 MB."""
import os, re, sys
base = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else '.')
errors, warns = [], []
def err(m): errors.append(m)
def warn(m): warns.append(m)

cfg = {'root': './', 'readme': 'README.md', 'summary': 'SUMMARY.md'}
cp = os.path.join(base, '.gitbook.yaml')
if os.path.exists(cp):
    sect = None
    for line in open(cp, encoding='utf-8'):
        s = line.split('#', 1)[0].rstrip()
        if not s.strip(): continue
        m = re.match(r'^(\s*)([A-Za-z_]+):\s*(.*)$', s)
        if not m: warn('.gitbook.yaml: cannot parse line: ' + s); continue
        indent, key, val = len(m.group(1)), m.group(2), m.group(3).strip().strip('"\'')
        if indent == 0: sect = key
        if indent == 0 and key == 'root' and val: cfg['root'] = val
        elif sect == 'structure' and indent > 0 and key in ('readme', 'summary') and val: cfg[key] = val
        elif indent == 0 and key not in ('root', 'structure', 'redirects'): warn('.gitbook.yaml: unknown top-level key ' + key)
root = os.path.normpath(os.path.join(base, cfg['root']))
if not os.path.isdir(root): err('root directory missing: ' + root); sys.exit(1)
readme = os.path.normpath(os.path.join(root, cfg['readme']))
summary = os.path.normpath(os.path.join(root, cfg['summary']))
if not os.path.isfile(readme): err('landing page missing: ' + os.path.relpath(readme, base))
if not os.path.isfile(summary): err('SUMMARY.md missing: ' + os.path.relpath(summary, base)); 

item = re.compile(r'^(\s*)[*-]\s+\[([^\]]*)\]\(([^)\s]*)(?:\s+"([^"]*)")?\)\s*$')
listed = {}
if os.path.isfile(summary):
    prev, unit = 0, None
    for n, line in enumerate(open(summary, encoding='utf-8'), 1):
        if not line.strip() or re.match(r'^#{1,6}\s', line): prev = 0 if line.startswith('#') else prev; continue
        m = item.match(line.rstrip('\n'))
        if not m: err(f'SUMMARY.md:{n}: not a heading or "* [Title](path.md)" item: {line.strip()[:70]}'); continue
        ind = m.group(1).replace('\t', '    '); width = len(ind)
        if width and unit is None: unit = width
        if width and unit and width % unit: err(f'SUMMARY.md:{n}: indentation {width} is not a multiple of {unit}')
        level = width // unit if unit else 0
        if level > prev: 
            if level - prev > 1: err(f'SUMMARY.md:{n}: nesting jumps more than one level')
        prev = level
        tgt = m.group(3).split('#')[0]
        if re.match(r'^[a-z]+://', m.group(3)) or not tgt: continue
        full = os.path.normpath(os.path.join(os.path.dirname(summary), tgt))
        if full in listed: err(f'SUMMARY.md:{n}: {tgt} already listed at line {listed[full]} (a page may appear once)')
        listed[full] = n
        if not os.path.isfile(full): err(f'SUMMARY.md:{n}: missing file {tgt}')
        elif not full.startswith(root + os.sep): err(f'SUMMARY.md:{n}: {tgt} is outside the root')
md_files = []
for dp, dn, fn in os.walk(root):
    dn[:] = [x for x in dn if x not in ('.git', 'node_modules')]
    for f in fn:
        p = os.path.join(dp, f)
        if os.path.getsize(p) > 100 * 1024 * 1024: err('over the 100 MB Git Sync file limit: ' + os.path.relpath(p, base))
        if f.lower().endswith('.md'): md_files.append(p)
for p in md_files:
    if p not in listed and p != readme and p != summary: warn('orphan page (not in SUMMARY.md): ' + os.path.relpath(p, base))

def slug(h):
    h = re.sub(r'[`*_]|<[^>]+>', '', h).strip().lower()
    return re.sub(r'\s+', '-', re.sub(r'[^\w\s-]', '', h))
link = re.compile(r'(!?)\[[^\]]*\]\(<?([^)\s>]+)>?(?:\s+"[^"]*")?\)')
html = re.compile(r'<(?:img|source)\b[^>]*?(?:src|srcset)="([^"]+)"')
for p in md_files:
    rel = os.path.relpath(p, base)
    txt = open(p, encoding='utf-8').read()
    lines = txt.split('\n')
    if lines and lines[0].strip() == '---':
        if '---' not in [l.strip() for l in lines[1:]]: err(rel + ': front matter is not closed')
        else:
            end = [l.strip() for l in lines[1:]].index('---') + 1
            for fl in lines[1:end]:
                mm = re.match(r'^description:\s*([^"\'>|\s].*)$', fl)
                if mm and (': ' in mm.group(1) or ' #' in mm.group(1)): err(rel + ': unquoted description with : or # (silent Git Sync failure): ' + fl[:60])
    if txt.count('```') % 2: err(rel + ': unbalanced ``` fences')
    opens = re.findall(r'\{%\s*([a-z-]+)\b', txt)
    stack = []
    for t in opens:
        if t.startswith('end'):
            if not stack or stack.pop() != t[3:]: err(rel + ': {% ' + t + ' %} has no matching opener')
        elif t not in ('else', 'elsif', 'include', 'embed'): stack.append(t)
    if stack: err(rel + ': unclosed {% ' + stack[-1] + ' %}')
    body = re.sub(r'```.*?```', '', txt, flags=re.S)
    anchors = {slug(m.group(1)) for m in re.finditer(r'^#{1,6}\s+(.*)$', body, flags=re.M)}
    targets = [m.group(2) for m in link.finditer(body)] + html.findall(body)
    for t in targets:
        if re.match(r'^([a-z][a-z0-9+.-]*:|//)', t, re.I): continue
        path, _, frag = t.partition('#')
        if not path:
            if frag and slug(frag) not in anchors and frag not in anchors: warn(f'{rel}: anchor #{frag} not found in the page')
            continue
        path = path.split('?')[0]
        full = os.path.normpath(os.path.join(root if path.startswith('/') else os.path.dirname(p), path.lstrip('/') if path.startswith('/') else path))
        if not os.path.exists(full): err(f'{rel}: broken link/image {t}')
        elif frag and full.endswith('.md'):
            h = {slug(m.group(1)) for m in re.finditer(r'^#{1,6}\s+(.*)$', open(full, encoding='utf-8').read(), flags=re.M)}
            if slug(frag) not in h: warn(f'{rel}: anchor #{frag} not found in {path}')
for w in warns: print('WARN ', w)
for e in errors: print('ERROR', e)
print(f'{len(md_files)} pages, {len(listed)} listed, {len(errors)} errors, {len(warns)} warnings')
sys.exit(1 if errors else 0)
```

### 3.5 CI interplay

* GitBook Git Sync commits to the synced branch; every such commit triggers `ci.yml` (`on: push`) on the self-hosted pool. Add `paths-ignore: ['gitbook/**']` to the `push` trigger **or** accept the load. (`ci.yml` has no paths filter today [P].)
* Editing surface: to avoid GitBook commits racing the owner's fast-forward pushes, treat GitHub as the only editing surface and do not merge change requests in GitBook; if a GitBook commit lands anyway, the owner's next push needs `git pull --rebase` first **[I]**.
* GitBook's GitHub pull-request preview (a commit status with a site-preview link; refs 072 and `git-sync-previews`) works only if the integration has the PR read permission; setups from before 2022-01 must reinstall (ref 074).

### 3.6 Plans and cost (live page opened 2026-10-27, https://www.gitbook.com/pricing)

| Plan | Price | Relevant features |
|---|---|---|
| Free ("For individuals") | 0 | Sync with GitHub or GitLab (Git Sync "included in the Free, Essential, Ultimate, and Enterprise plans"), auto-generated interactive API docs, preview deployments, "Advanced customization" listed |
| Essential | from $65 per site/month (annual) + $12 per extra user/month | All Free plus custom domain, analytics and feedback, site redirects, reusable content |
| Ultimate | from $249 per site/month (annual) | sections/groups, search across docs, authenticated access, AI Assistant etc. |
| Enterprise | custom | Git Sync IP allowlisting (not needed for github.com) |

Custom domain: "not included in the Free plan; included in the Essential, Ultimate, and Enterprise plans" (same page). The offline skill (scraped 2026-08-22) uses older names (Basic $0 / Premium $65 / Ultimate $249) and lists "Customizable logo, theme and fonts" under Premium: whether Free allows an uploaded logo, custom fonts and non-default themes today must be checked in the Customize screen **[U]**; whether Free permits an organisation-owned site ("For individuals") is also **[U]**. Therefore a `*.gitbook.io` URL is the guaranteed Free outcome; `docs.<domain>` needs Essential.

### 3.7 Owner checklist: what only a human with the GitBook account can do (no token on this host)

1. Sign up / sign in at https://app.gitbook.com with GitHub. If "Potential duplicated accounts" appears, unlink the GitHub identity from the other GitBook account first (ref 070).
2. Create an organisation and a **site** "OmpOS" (Home → create a docs site, ref 034). Pick the plan (§3.6).
3. Install the **GitBook GitHub app** (https://github.com/apps/gitbook-com) on the `swcstudiospace` organisation (requires an organisation owner; the `gh` identity has `admin:org`, ownership not probed), grant access to repository `OmpOS` only.
4. Land the `gitbook/` content on `main` first (so the first import has something real). In the site: **Git Sync → GitHub**, repository `swcstudiospace/OmpOS`, branch `main`, **initial direction GitHub → GitBook** (use "Swap direction" if GitBook shows the other way; a non-empty GitBook space is replaced), **Project directory `gitbook`**, content mapping: the space → `./`, **Sync**. Only administrators and creators can configure Git Sync (ref 064).
5. Commit the `docs.yaml` GitBook writes into `gitbook/`.
6. Customize: theme and primary colour `#6d28d9` (tint chosen from the suggestions or the neutral `#fafaf9`/`#181818`), logo light/dark (`logo-lockup-*-1200x300.png`), icon 132x132, fonts (DM Sans standard, Space Mono monospace as `.woff2` regular + bold), site title, social/OG image, SEO.
7. **OpenAPI**: route A (§3.3), then Add new → OpenAPI Reference into the "API reference" section; commit the generated pages.
8. Settings → **Edit on GitHub** toggle on; **Preview**, then **Publish** (sites are public by default; Settings → Audience changes it).
9. (Essential+) Settings → General → Domain and URL → custom domain `docs.<domain>`; add the CNAME GitBook shows at the DNS provider (not an A record; Cloudflare proxy off); wait up to a few hours; SSL is automatic.
10. Copy the live URL into GitHub: `gh repo edit swcstudiospace/OmpOS -h <url>`; add it to README, SUPPORT.md and `config.yml` contact links.
11. Optional: create a personal API token (https://app.gitbook.com/account/developer), store `GITBOOK_TOKEN` and `GITBOOK_ORGANIZATION_ID` as GitHub Actions secrets for route B.


## 4. Documentation inventory and proposed GitBook information architecture

### 4.1 Inventory of the repository's current docs [P] (line counts; `.planning/` is internal and not published)

Repository root: `README.md` (97 lines; run/bootstrap/repo map/status/docs list), `CONTRIBUTING.md` (52), `AGENTS.md` (256, agent instructions; internal), `.env.example` (API configuration variables). No `CHANGELOG.md`, `LICENSE`, `SECURITY.md`, `CODE_OF_CONDUCT.md`, `SUPPORT.md`, `CODEOWNERS`. `docs/` has 240 evidence files (about 7.2 MB, mostly screenshots/JSON/logs under `docs/runbooks/evidence/`), 11 top-level entries on the remote.

| Path | Lines | Nature | Publish to GitBook? |
|---|---|---|---|
| `docs/architecture.md` | 146 | layers, projects | yes, edit for readers |
| `docs/plugins.md` | 176 | the four plugins, submodules, private-repo access | yes (trim CI secret details) |
| `docs/settings-parity.md` | 88 | the 34 Cursor settings rows and the eleven Settings pages, with "Omitted" rows (MCP, Hooks, Indexing, @Docs) | yes, basis of Settings pages; status columns change fast |
| `docs/decisions/ADR-0001..0014` + `README.md` | 29-279 each | decision records | yes, as "Architecture decision records" (copy; keep numbering) |
| `docs/api/openapi.json` | 1015 | OpenAPI 3.1.1 of `OmpOS.Api` | register as spec (§3.3), do not copy as a page |
| `docs/api/ADE-07-run-contract-change-request.md` | 148 | internal change request | no (internal) |
| `docs/runbooks/running-the-desktop-app.md` | 74 | run, state dir, shortcuts | yes |
| `docs/runbooks/local-dev.md` | 152 | bootstrap, local dependencies | yes (Contributing / Getting started) |
| `docs/runbooks/agent-loop.md` | 136 | four plugins, health, probes | yes |
| `docs/runbooks/cloud-terminals.md` | 72 | cloud terminals on Railway, sleep/restore | yes (Cloud agents) |
| `docs/runbooks/hosting.md` | 170 | hosting runbook (HOST-01), Railway | yes, after removing internal hostnames (it names `greptimedb.railway.internal`) |
| `docs/runbooks/local-terminal.md`, `android-ios-terminals.md`, `phase-22-desktop-heads.md`, `sandbox.md`, `web-key-threat-model.md` | 50, 44, 63, 26, 37 | terminals, platform heads, sandbox, browser key threat model | yes, curated |
| `docs/runbooks/phase-20-avalonia-foundation.md`, `phase-30-compatibility-admin.md` | 92, 63 | phase status records | no (history) |
| `docs/runbooks/deferred-and-blocked-2026-10-26.md` | 39 | blockers table | no, but feeds "Roadmap/limits" |
| `docs/runbooks/evidence/**` | 240 files | recorded runs, screenshots (`ui-parity-2026-10-26/after-light|after-dark`, settings-1..11, slash palette, kanban) | no as a tree; copy a few optimised screenshots into `gitbook/.gitbook/assets/` |
| `docs/quality/cursor-parity-audit-2026-10-26.md` | 546 | row-by-row audit | publish a summarised "Parity status" page; keep the audit internal |
| `docs/quality/quality-bar.md` | 276 | audit of 2026-10-22 | no |
| `docs/plans/vision-research/cursor-slash-commands.md` | 206 | Cursor slash commands and OmpOS mapping (research, 2026-10-22) | rewrite as user doc; source of truth should be generated from `src/OmpOS.Presentation/Commands/SlashCommandRegistry.cs` |
| `docs/plans/vision-research/cursor-settings-window.md` | 142 | Cursor settings sections | rewrite with `settings-parity.md` |
| `docs/plans/vision-research/providers-eu-cn.md`, `providers-local-us.md` | 134, 152 | provider and plan research | rewrite as Providers pages |
| `docs/plans/vision-research/railway-cloud-desktop.md` | 153 | Railway cloud desktop research | rewrite as Cloud agents pages |
| `docs/plans/vision-research/greptile-font.md`, `herdr.md`, `pets-*.md` | 85, 127, 163, 69 | research | no (brand page may cite fonts + OFL) |
| `docs/plans/ui-parity/cursor-agents-window-parity.md` | 904 | visual parity spec | no |
| `docs/plans/agent-loop/*` (dossiers, design) | 21-334 | design and discovery | `agent-loop-design.md` condensed into Concepts; dossiers no |
| `docs/plans/ade-harness-plan.md`, `cursor-parity-research.md` | 141, 94 | plans | no |
| `docs/parity/session-chat-parity.md` | 68 | inventory | no |
| `docs/handoff.md` | 225 | "v0.9.1 handoff": stale (current work is milestone v0.14.x) | no, mark superseded |
| `docs/open-questions.md` | 314 | every unverified external fact with owner | no (internal); consider a short public "Known limits" |

Link health of the existing Markdown: 151 relative links in the repo root and `docs/` (excluding `plugins/`, `.planning/`, evidence), 0 broken [P].

### 4.2 Proposed GitBook information architecture (single space "Documentation", content under `gitbook/`)

Existing files are curated copies; internal phase/OQ/Linear jargon must be stripped. "New" means no source exists and the feature status in `README.md`/the parity audit says the head does not implement it yet (the page must state what is built and what is not, per the AGENTS/README claims policy).

| Section | Page (file) | Source | Action |
|---|---|---|---|
| (landing) | `README.md` | root `README.md` intro, banner, status table | rewrite; banner via `.gitbook/assets` |
| Getting started | `getting-started/install.md` | README Prerequisites; `docs/runbooks/local-dev.md` | curate |
| | `getting-started/run-the-desktop-app.md` | `docs/runbooks/running-the-desktop-app.md` | copy and curate |
| | `getting-started/run-the-web-app.md` | README; `docs/runbooks/hosting.md`; `apps/OmpOS.Web` | new (web head is a bare chat page today per the brief) |
| | `getting-started/android.md` | `docs/runbooks/android-ios-terminals.md`, `phase-22-desktop-heads.md` | curate |
| | `getting-started/first-session.md` | `docs/runbooks/agent-loop.md` (probe without a model turn) | curate |
| Using OmpOS | `using-ompos/desktop.md` | running-the-desktop-app, `ui-parity-2026-10-26` screenshots | new text + screenshots |
| | `using-ompos/web.md` | - | new |
| | `using-ompos/terminals.md` | `docs/runbooks/local-terminal.md`, `cloud-terminals.md` | curate |
| | `using-ompos/keyboard-shortcuts.md` | running-the-desktop-app (shortcuts), Settings > Keyboard | curate |
| Concepts | `concepts/agent-loop.md` | `docs/plans/agent-loop/agent-loop-design.md`, `runbooks/agent-loop.md` | condense |
| | `concepts/plugins.md` | `docs/plugins.md`, ADR-0012, ADR-0013 | curate |
| | `concepts/steering.md` | README ("steered live from the composer or its Kanban card"); `.planning/phases/49-live-steering/49-SUMMARY.md` (internal) | new, written from the code and summary |
| | `concepts/kanban.md` | README; `.planning/phases/46-agent-panel` | new |
| | `concepts/sessions-runs-and-approvals.md` | `docs/api/openapi.json` tags, ADR-0008 | new |
| | `concepts/sandbox.md` | `docs/runbooks/sandbox.md` | curate |
| Settings and providers | `settings/overview.md` | `docs/settings-parity.md` | curate (the 34 rows) |
| | `settings/<page>.md` x 11 (Runtime, General, Agents, Models, Rules, Skills, Commands, Plugins, Network, Keyboard, Tab) | `settings-parity.md`, `evidence/phase-40-*`, screenshots settings-1..11 | one page per Settings page; **new content for the owner's target "ALL Cursor settings"** |
| | `providers/index.md`, `providers/eu.md`, `providers/us.md`, `providers/cn.md`, `providers/coding-plans.md`, `providers/local.md` | `plans/vision-research/providers-eu-cn.md`, `providers-local-us.md`, `evidence/phase-40-omp-providers-2026-10-26/omp-provider-surface.md` | rewrite; generate tables from the provider registry so docs cannot drift |
| | `providers/keys-and-security.md` | `docs/runbooks/web-key-threat-model.md` | curate |
| Slash commands | `slash-commands/index.md` + per-group pages | `plans/vision-research/cursor-slash-commands.md`; code `SlashCommandRegistry` | rewrite; generate list from code |
| Hooks | `hooks/overview.md`, `hooks/reference.md` | none; `settings-parity.md` row 12 says hooks are not wired (OQ-84) | **new, blocked until a head wires the hook host**: publish only what exists |
| Cloud agents (Railway) | `cloud-agents/overview.md`, `cloud-agents/railway-setup.md` | `runbooks/cloud-terminals.md`, `runbooks/hosting.md`, `plans/vision-research/railway-cloud-desktop.md` | curate and extend |
| Remote environments | `remote-environments/overview.md` | `cloud-terminals.md`, `android-ios-terminals.md`; no dedicated doc | new |
| Multi-repo | `multi-repo/overview.md` | no source found; absent from audit files grepped | new, status to be confirmed against code |
| Architecture | `architecture/overview.md`, `architecture/layers.md`, `architecture/decisions/*.md` (ADR-0001..0014) | `docs/architecture.md`, `docs/decisions/` | copy |
| API reference | `api-reference/index.md` + generated OpenAPI pages (4 tag pages) | `docs/api/openapi.json` (§3.3) | generated by GitBook |
| Runbooks | `runbooks/local-dev.md`, `agent-loop.md`, `hosting.md`, `sandbox.md` | `docs/runbooks/*` | curate (avoid duplicates of Getting started: one canonical page each) |
| Contributing | `contributing/index.md`, `contributing/plugin-submodules.md`, `contributing/docs.md` | `CONTRIBUTING.md`, `docs/plugins.md` | copy; add docs-contribution rules and the checker |
| Changelog | `changelog.md` (use `{% updates %}`) | new root `CHANGELOG.md` | single source: either root `CHANGELOG.md` with the GitBook page generated by script, or the other way; the release workflow reads root `CHANGELOG.md` so keep that canonical |
| Licensing | `licensing.md` | new `LICENSE` (AGPL-3.0), `src/OmpOS.Native.Views/Assets/Fonts/*/OFL.txt`, plugin licence facts | new; must state each plugin's own terms and the font OFL notices |
| Project | `status.md` ("What works today") | `docs/quality/cursor-parity-audit-2026-10-26.md` summary, README Status | summarise |

Suggested `SUMMARY.md` skeleton (page groups via `##`, nesting by two spaces):
```
# Summary

* [OmpOS](README.md)

## Get started

* [Install](getting-started/install.md)
* [Run the desktop app](getting-started/run-the-desktop-app.md)
* [Run the web app](getting-started/run-the-web-app.md)
* [Android](getting-started/android.md)
* [First session](getting-started/first-session.md)

## Use OmpOS

* [Desktop](using-ompos/desktop.md)
* [Web](using-ompos/web.md)
* [Terminals](using-ompos/terminals.md)
* [Keyboard shortcuts](using-ompos/keyboard-shortcuts.md)

## Concepts

* [Agent loop](concepts/agent-loop.md)
* [Plugins](concepts/plugins.md)
* [Steering](concepts/steering.md)
* [Kanban](concepts/kanban.md)

## Configure

* [Settings](settings/overview.md)
  * [Runtime](settings/runtime.md)
* [Providers and plans](providers/index.md)
  * [EU](providers/eu.md)
  * [US](providers/us.md)
  * [CN](providers/cn.md)
* [Slash commands](slash-commands/index.md)
* [Hooks](hooks/overview.md)

## Agents everywhere

* [Cloud agents on Railway](cloud-agents/overview.md)
* [Remote environments](remote-environments/overview.md)
* [Multi-repo](multi-repo/overview.md)

## Reference

* [Architecture](architecture/overview.md)
* [API reference](api-reference/index.md)
* [Runbooks](runbooks/local-dev.md)

## Project

* [Contributing](contributing/index.md)
* [Changelog](changelog.md)
* [Licensing](licensing.md)
```

### 4.3 Cross-cutting findings for planning

1. The repo has **no LICENSE and no tag**, but the README, GitBook and release workflow all assume AGPL-3.0 and versions: LICENSE + a single version source + `CHANGELOG.md` must land before the first release workflow run.
2. `agent-swarm` and the private `agent-substrate` submodule are unlicensed; AGPL-3.0 on OmpOS does not license them, and GitHub's release archives omit submodules anyway: publish no bundle containing them without the owner's decision.
3. Fonts: `Assets/Fonts` is guarded by `FontResourceTests` ("folder holds only manifest fonts", manifest hash) per the parity audit AL-23; WOFF2 conversions for GitBook or the web head must live outside that folder (for example `assets/brand/fonts/`). The same rows say Anybody appears only at 20 px and above, SemiBold or heavier: apply that rule to the banner and logo too.
4. The `[self-hosted, omega-ci]`-only CI policy collides with CodeQL default setup (GitHub-hosted `standard` runners) and any new release/docs workflow; each needs the same runner labels, and the pool was reported offline on 2026-10-26 (2 org runners registered now, status not probed).
5. Dependabot NuGet + committed `packages.lock.json` + `--locked-mode` is a known failure mode: test with the first PR.
6. GitHub features that **cannot** be done by API and need the owner at a browser: social preview image, discussion categories, About "Include in the home page" toggles; GitBook sign-in, GitHub-app install, Git Sync wizard, theme/logo/fonts/domain/publish.
7. Docs hygiene: stale `docs/handoff.md` (v0.9.1), `community/profile.documentation` pointing at a nonexistent `master`, and many internal terms (OQ-nn, phases, Linear trailers) must be stripped from published pages.

## 5. Gaps and unverified items

* `fontTools` is absent (brief assumed present); WOFF2 conversion and direct TTF→path conversion need it; the Chrome→PDF→`pdftocairo` route works without it.
* ImageMagick cannot be trusted for SVG; `rsvg-convert` is missing. Use cairosvg or Chrome.
* No GitBook token or account: nothing about the live GitBook UI could be exercised; the theme/font/plan gating by tier is [U].
* Pages not opened: GitHub "Adding support resources" doc, GNU AGPL text URL, REST `PUT /topics` page (endpoint named from `gh` behaviour and search), Avalonia `Window.Icon` docs (the Avalonia page I tried had no matching text).
* CodeQL default-setup `runner_type=labeled` PATCH values not tried (read-only rules).
* Owner decisions needed: contact address for the Code of Conduct, security response targets, CODEOWNERS handles, GitBook plan/domain, licence form (`-only` vs `-or-later`) and the plugin licensing question.
