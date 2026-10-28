# Dossier: git, CI, branch protection, licences (worker w0-discovery-6, 2026-10-26)

All data read-only. Remote state read with `git ls-remote origin` / `gh api` (live); local `origin/*` refs may be stale (noted where they differ). Tokens never printed.

## 1. Four repos: git state

| repo | remote | branch | HEAD | upstream | ahead/behind (`rev-list --left-right --count upstream...HEAD`) | dirty |
|---|---|---|---|---|---|---|
| autonogrammer | https://github.com/swcstudiospace/OmpOS.git | feat/v0.9.2-hardening | bad4e67e4e263c9f7c6a318dbba1ff224d4f52c1 | origin/feat/v0.9.2-hardening | `0 43` | 12 entries (6 M: .planning/ROADMAP.md, STATE.md, phases/22-desktop-heads/22-SUMMARY.md, 24-ios-android-heads/24-SUMMARY.md, 25-cutover/25-SUMMARY.md, v0.10.0-MILESTONE-AUDIT.md; 6 ??: phases/25-cutover/25-VERIFICATION.md, 25.1..25.5 phase dirs). Only .planning/ is dirty. |
| claude-ultrathink | https://github.com/swcstudiospace/claude-ultrathink.git | feat/v1.16-code-quality-sweep | 00ce3e6be70d1277934e39515904b55403b9b56d | origin/feat/v1.16-code-quality-sweep | `0 0` | 4 untracked only: .cursor/ .grok/ .mcp.json .omp/ |
| agent-swarm | https://github.com/swcstudiospace/agent-swarm.git | feat/v2.4-rust-wasm-core | 27d22946e53542579de7ddc4e48d968896e1791a | **origin/main** (git config: branch.remote=origin, merge=refs/heads/main; no remote branch of the local name exists) | `4 28` (4 only on main, 28 only on HEAD) | 142 entries: tracked M in .github, .gitignore, AGENTS.md, Cargo.*, crates/ (27), swarm/ (74 files), tests/ (21), scripts/ (4), omp/test/commands.test.ts (1 line), plan/, web/, docs/ ... Working tree != HEAD. |
| agent-substrate | https://github.com/swcstudiospace/agent-substrate.git | planning/consistency-check-2026-10-24 | 36580a62b1c56e5c3ea77049d7c2e1d23542e40e | origin/planning/consistency-check-2026-10-24 | `0 0` | 2: M .planning/STATE.md, ?? .claude/ |

Default branch of all four = `main` (`git ls-remote --symref origin HEAD` -> `ref: refs/heads/main`).
Remote `main` tips (live `ls-remote`): Autonogrammer `1398227f315cc5827a84e0b7f0a95a39b7ae7708`; claude-ultrathink `7cfd05423e9d87c7b6afd918d2feb58a8b2b645c`; agent-swarm `c900a510db3e50de6e9f8b60453bdded6fdf72df`; agent-substrate `754c51a17a9e6ffbc222d04bdaf4e783b4eca720`.
Remote head counts: Autonogrammer 4 (ci/00057-omega-ci-runners, feat/autonogrammer-scaffold, feat/v0.9.2-hardening=b3f3dfd4..., main); ultrathink 28; swarm 28; substrate 109.

### Autonogrammer unpushed commits (43; first 10 `git log origin/feat/v0.9.2-hardening..HEAD`)
```
bad4e67 fix phase 25.5: failures that stay silent (F-D)
523ac44 fix phase 25.4: build, deploy, and image integrity (F-C)
501c8db fix phase 25.3: bounded child and vendor reads (F-B)
b6e7664 fix phase 25.2: call-site oversized-body test for ApiSessionClient
0ffe092 fix phase 25.2: web head trust and stream bounds (F-A)
59e9dd1 fix(phase 25): test the MCP host close trace through an injectable host
0be414e fix(phase 25): cap queued messages, test the engine dispose trace, focus the toolbar toggles
f8b5faf ci: audit the Android head for vulnerable packages (Phase 24 fold-in)
72000fc docs(v0.10.0): record the kickoff, the answered decisions and the TODO inventory
0bea2bf chore(baseline): commit the follow-up fixes and planning records before the uplift run
```
Remote feat tip b3f3dfd495dd5004f340431d84c91ab352568614 equals local `origin/feat/v0.9.2-hardening` and IS an ancestor of HEAD.

### Sibling pin analysis (a submodule pin must be fetchable by a fresh clone)

**claude-ultrathink**: HEAD 00ce3e6b is pushed: remote heads containing exactly that SHA = `refs/heads/feat/v1.16-code-quality-sweep` and `refs/pull/59/head` (PR #59 OPEN, base main, mergeable, not draft). Remote main (7cfd054) is an ancestor of HEAD (`0 11`: HEAD is 11 commits ahead of main). `src/host/omp.ts` and `package.json` differ between main and HEAD (omp.ts +768/-~165, package.json 7 lines), so main tip is NOT equivalent to HEAD for the plugin entry.
Recommended pin: **HEAD 00ce3e6be70d1277934e39515904b55403b9b56d** (pushed). Durability caveat [INFERENCE]: it is reachable only from a PR branch; if PR 59 is squash-merged and the branch deleted, the SHA stays reachable via `refs/pull/59/head` only. Safest durable pin = a main commit after PR 59 merges. Nothing needs pushing now. Fallback if branch vanishes: main tip 7cfd0542... (lacks the omp.ts changes).

**agent-swarm**: HEAD 27d22946 is NOT on any remote branch (`git branch -r --contains HEAD` empty; its SHA is absent from `ls-remote`). 28 unpushed commits vs origin/main; remote main (c900a51, PRs #41/#42 merges) is NOT an ancestor of HEAD (4 main-only commits: c900a51 Merge PR #42 gsd/00066-ci-venv, 23d61d3 fix(ci): install test deps in a venv..., 07552be Merge PR #41, 428445d 00057 - CI: run all jobs on Omega CI runners). The 28 HEAD-only commits (first 10: 27d2294 refactor(route)..., 67ec7d9 feat(sandbox): fuel/epoch..., 41682d7, 1ed03d0, 80f0e19 build(pin): wasmtime 50.0.0, 61880a5 fix(runtime): PrioritySemaphore, 888a124 fix(security): capability tokens..., 5ef7f55 build(lock): Cargo.lock, d87bb8f feat(adapter) B10, 00dc35f feat(adapter) B9) touch only Rust crates/tests/docs: `git diff --name-only c900a51 HEAD -- omp swarm prompts` = **0 files** (committed `omp/`, `swarm/`, `prompts/` are byte-identical to remote main). Both `omp/package.json`, `omp/src/index.ts`, `omp/agents/a01-orchestrator.md`, `swarm/` exist at c900a51 and at HEAD. No `.gitmodules` in swarm.
Recommended pin: **remote main tip c900a510db3e50de6e9f8b60453bdded6fdf72df** (exists on remote; omp/ and swarm/ identical to HEAD; the uncommitted working-tree edits in swarm/ (74 files) and omp/test/commands.test.ts are in no pin either way).
For HEAD to be pinnable the owner must push the 28 commits: `git push origin feat/v2.4-rust-wasm-core` (explicit branch name; NOT bare `git push`, upstream is configured to origin/main and push.default would refuse/mis-target), plus commit or discard the 142 dirty files if they belong in the pin. Not done by me.

**agent-substrate**: HEAD 36580a62 is pushed: `refs/heads/planning/consistency-check-2026-10-24` and `refs/pull/123/head`; PR #123 is **CLOSED, not merged**. Other remote branches whose tips (as of the first `ls-remote`) contain HEAD as ancestor (verified `merge-base --is-ancestor`): `gsd/autonomous-run-2026-10-24` (tip then 34af525; a second `ls-remote --heads` minutes later shows bec07838..., so the branch is still moving; ancestry of the new tip not verified; PR #126 OPEN) and `ship/planning-consistency-2026-10-24` (tip 6246204f; PR #125 MERGED 2026-10-25, landing on main as 754c51a, so main does not contain the HEAD SHA itself: `754c51a...HEAD` = `4 2`). HEAD-only commits are docs(planning) only (36580a6, 6226196); `git diff --stat 754c51a HEAD -- packages/mcp-server packages/projector` = 1 line (packages/mcp-server/package.json zod pin). Main-only commits: 754c51a Autonomously drive the remaining phases (#125), 2876978 Merge PR #124, 4476e45 fix(host): sync package-lock.json with the zod 4.6.7 pin, ade9f06 fix(deps): patch zod, body-parser and undici advisories. `packages/mcp-server`, `packages/projector/omp/substrate-status.ts`, `substrate-skills.ts` exist at both.
Recommended pin: **remote main tip 754c51a17a9e6ffbc222d04bdaf4e783b4eca720** (default branch, includes the zod/body-parser/undici advisory patches; HEAD 36580a6 is also fetchable now but sits on a closed-PR/merged-ship branch that may be deleted). Nothing needs pushing.

`git cat-file -t` confirmed all remote tips quoted are present locally except where noted; no fetch was run (a `git fetch --dry-run` in autonogrammer only listed a new remote branch ci/00057-omega-ci-runners).

## 2. Autonogrammer history vs main
- `git merge-base --is-ancestor origin/main HEAD` -> **NO** (rc=1). merge-base = `cbbe22d77f78a6b6e7dea51e6e2b25f8a83a8b0e` ("Autonogrammer v0.9.1: foundation through verified handoff (phases 1-5) (#1)").
- `git rev-list --left-right --count origin/main...HEAD` = `6 103`. `git log origin/main..HEAD --oneline | wc -l` = **103** (43 of them unpushed). 
- `git log HEAD..origin/main --oneline` (6):
```
1398227 Merge pull request #2 from swcstudiospace/ci/00057-omega-ci-runners
cdac480 CI: gitleaks scans the checked-out commit's history, not every branch
715b5d1 CI: run two test modules at a time on the pool
13461dc Remove a stray empty file
5490155 CI: size MSBuild to the runner's 2 vCPU budget
16b7a53 CI: run every job on the Omega CI runner pool; MAUI head builds net10.0-android on Linux
```
`git cherry HEAD origin/main` marks all 5 non-merge commits `+` (patch not present in HEAD).
- **Fast-forward of main is impossible.** Needs merge/rebase of origin/main into the branch first. `git merge-tree --write-tree --name-only HEAD origin/main` (dry run, nothing written to refs): 5 conflicts: `.github/workflows/ci.yml` (content), `README.md` (content), `apps/Autonogrammer.App/Autonogrammer.App.csproj` (modify/delete: deleted in HEAD (MAUI head removed), modified on main), `docs/open-questions.md` (content), `tests/Autonogrammer.ArchitectureTests/ArchUnitLayerTests.cs` (content).
- main's ci.yml differs materially: `runs-on: [self-hosted, linux, x64, omega-ci]` for build-test and the MAUI/`shells` jobs (HEAD uses `ubuntu-24.04`); `actions/checkout@v7` NOT pinned to SHA (HEAD pins SHA and `WorkflowPinTests` enforces 40-hex pins and no `-latest` runner => a naive merge would fail that test); job guard `if: github.event_name != 'pull_request' || head.repo.full_name == github.repository`; gitleaks runs with `--log-opts="--full-history HEAD"` and `$RUNNER_TEMP` paths; podman "Require" instead of "Install"; `DOTNET_PROCESSOR_COUNT: 2`, `MSBUILDDISABLENODEREUSE: 1`. Org runners `omega-ci-*` (labels self-hosted, Linux, X64, omega-ci) all reported `status: offline` by `gh api orgs/swcstudiospace/actions/runners` at time of check (list truncated at 600 chars).
- PRs: `gh pr list --state all --head feat/v0.9.2-hardening` -> `[]` (none). `--base main`: #1 "Autonogrammer v0.9.1: foundation through verified handoff" [merged], #2 "00057 - CI: run all jobs on Omega CI runners" [merged]. No open PR for main. Local branches: feat/autonogrammer-scaffold, feat/v0.9.2-hardening, main.

## 3. Protection, visibility, auth
- `gh api repos/swcstudiospace/OmpOS/branches/main/protection` -> HTTP 404 `Branch not protected`; `branches/main` `.protected=false`. `repos/.../rulesets` -> `[]`; `orgs/swcstudiospace/rulesets` -> `[]`. Same `protected:false` for main of claude-ultrathink, agent-swarm, agent-substrate. Merge methods all enabled (merge/squash/rebase), delete_branch_on_merge false. Actions: enabled, allowed_actions=all, sha_pinning_required=false, default_workflow_permissions=read, can_approve_pull_request_reviews=true.
- `gh repo view swcstudiospace/OmpOS --json defaultBranchRef,visibility,isPrivate,viewerPermission` -> `{"defaultBranchRef":{"name":"main"},"isPrivate":false,"viewerPermission":"ADMIN","visibility":"PUBLIC"}`.
- claude-ultrathink: PUBLIC, ADMIN. agent-swarm: PUBLIC, ADMIN. **agent-substrate: PRIVATE**, ADMIN.
- `gh auth status`: logged in github.com as `SomeRandmGuyy`, two entries (env GH_TOKEN, active; and hosts.yml), https git protocol, scopes include repo, workflow, admin:org, admin:repo_hook, delete_repo, write:packages. Token masked.
- Consequence: direct push of main is possible for this identity (no protection); required status checks/reviews are not enforced by GitHub.

### What `actions/checkout` with `submodules: recursive` needs when a sibling is private
Facts from the actions/checkout README (via search, not re-verified against the pinned SHA 3d3c42e5...): `submodules: recursive` also checks out nested submodules (siblings have none: `git ls-files -s | grep -c ^160000` = 0 in all three, no .gitmodules). Default `token` is `github.token`, scoped to the workflow's own repository: it cannot read another private repo, even in the same org. So for the private **agent-substrate** submodule the checkout step needs `token:` (fine-grained PAT, Repository permission Contents: read-only on agent-substrate, or classic PAT `repo`, or a GitHub App installation token) or `ssh-key:` (deploy key; a GitHub deploy key attaches to one repository, so with several private repos use a machine user key or a PAT). Public siblings (ultrathink, swarm) clone anonymously over https. With `ssh-key` unset, `git@<github-host>:` URLs are rewritten to https by checkout; use `https://github.com/swcstudiospace/<repo>.git` in .gitmodules anyway (brief). `persist-credentials` defaults true. Pull-request runs from forks do not receive secrets, so a secret PAT is empty there (the main branch ci.yml already guards `pull_request` to same-repo heads). Both existing jobs (`build-test` fetch-depth 0, `android`) call checkout without `submodules`, so submodule dirs would be EMPTY in CI unless `submodules:` is added; nothing under tests/build needs them unless a new test reads plugins/.
- Vercel (search result, not tested; no `.vercel/` or vercel.json in repo, so UNKNOWN whether a Vercel project is linked: `ls ~/.vercel`/`vercel project ls`): Vercel's Build docs state Git submodules work only if publicly reachable over HTTPS; private or SSH submodules fail the Build step. `vercel.ts` buildCommand is `./.dotnet/dotnet publish apps/Autonogrammer.Web ...` and needs no plugin; if a private substrate submodule is added, a Vercel Git-integration clone would fail unless submodule fetch is avoided. UNKNOWN whether Vercel can be told to skip submodules: check Vercel project settings "Git" / `vercel.ts`.

## 4. CI / build surfaces for `plugins/<name>` submodule directories

### .github/workflows (only ci.yml on HEAD; 158 lines, `permissions: contents: read`)
- Triggers: push, pull_request. concurrency group ci-${{github.ref}}.
- Job `build-test` (`runs-on: ubuntu-24.04` on HEAD): `actions/checkout@3d3c42e5... # v7` with `fetch-depth: 0`, no submodules; setup-dotnet; `dotnet restore Autonogrammer.Linux.slnf --locked-mode`; `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror`; podman install; `scripts/test-services.sh up`; `dotnet test Autonogrammer.Linux.slnf ... --coverlet`; `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore`; web smoke (`scripts/web-smoke/run-smoke.sh`); vulnerable packages gate (`dotnet list Autonogrammer.Linux.slnf package --vulnerable --include-transitive --format json` -> `python3 scripts/ci/check-vulnerable-packages.py vuln.json`); gitleaks CLI 8.30.1 pinned sha256: `gitleaks git --redact --exit-code 1 --log-level warn .` (scans parent-repo git history only; a submodule shows up in history as a gitlink `Subproject commit <sha>` line plus .gitmodules text; submodule file contents/history are NOT scanned) [INFERENCE from how gitleaks git reads `git log -p`]; compose validation + boot of backend profile.
- Job `android` (ubuntu-24.04): checkout (no fetch-depth), setup-dotnet, restore/vuln-gate/workload install/build of `apps/Autonogrammer.Android/Autonogrammer.Android.csproj`.
- Architecture test `WorkflowPinTests` enforces SHA pins and no `-latest`; any added `submodules:`/`token:` input is fine, but a new `actions/checkout` must stay SHA-pinned.

### bootstrap.sh (67 lines)
`cd` to its dir; installs SDK per global.json (10.0.402, SHA-512 check); locked restore, Release build `-warnaserror`, `dotnet test ... --filter-not-trait Category=Live`, `dotnet format --verify-no-changes`, web smoke. Never touches git submodules; **a fresh `git clone` without `--recurse-submodules` (or `git submodule update --init`) leaves plugins/* empty and bootstrap.sh will not populate them.**

### Container build
`deploy/api.Containerfile` copies only: `global.json Directory.Build.props Directory.Packages.props .editorconfig BannedSymbols.txt BannedSymbols.NoProcess.txt`, `src/`, `services/Autonogrammer.Api/`, `docs/api/`. A `plugins/` dir would NOT enter the image (no `COPY .`). It WOULD be part of the podman build context unless excluded; `.containerignore` (17 lines) excludes `**/bin **/obj **/TestResults .git .dotnet .nuget **/.vs **/node_modules .env .env.* **/.env **/.env.* **/*.pem .planning .swarm` and does not list `plugins`. Note `.git` there is root-only, a submodule's `.git` is a file in `plugins/<name>/.git`. Change (optional, hygiene): add `plugins` to `.containerignore`.

### vercel.ts / scripts/vercel
vercel.ts (54 lines) runs `scripts/vercel/render-csp.sh`, `buildCommand` = `./.dotnet/dotnet publish apps/Autonogrammer.Web -c Release -o output`, outputDirectory `output/wwwroot`. No plugin dependency. Only risk: Vercel's git clone of a private submodule (see section 3).

### MSBuild
`Directory.Build.props` (41 lines) and `Directory.Packages.props` (96 lines): no wildcard/glob Includes over the repo (only `$(MSBuildThisFileDirectory)BannedSymbols*.txt` AdditionalFiles; test-project conditions by project name). They apply only to projects that live under the repo root; **no sibling tracks any .csproj/.sln/.props/.targets/.cs/global.json/.editorconfig/nuget.config** (`git ls-files` in all three: none), so nothing from submodules joins the build. `Autonogrammer.Linux.slnf` lists projects explicitly. `ProjectGraphChecker` enumerates `*.csproj` only under `src`, `services`, `apps`.

### .gitignore (490 lines), .gitattributes, .editorconfig
- `.gitignore`: no `plugins` / `.omp` / `.swarm` / `.claude` rule (`git check-ignore -v plugins .omp .swarm .claude` -> not ignored; `.swarm/` exists untracked locally, 0 tracked files). Has `node_modules/`, `**/[Pp]ackages/*` (line 208) with `!**/[Pp]ackages/build/` - only affects the parent repo; files tracked by a submodule's own repo are unaffected, but `git check-ignore plugins/agent-substrate/packages/...` would report ignored (cosmetic). A gitlink path `plugins/<name>` itself matches no ignore rule.
- `.gitattributes`: `* text=auto eol=lf` - parent attributes do not apply inside a submodule's own repo.
- `.editorconfig` (400 lines): `root = true`; `[*]` indent_style=space, end_of_line=lf; `[*.json]` indent_size=2; `[*.xml]/.props/.csproj` 2; `[*.cs]/[*.{cs,vb}]` large rule sets; `[tests/**.cs]`. It is path-based so editors apply `[*]`/`[*.json]` to foreign files under plugins/ (siblings track no .editorconfig of their own, so no override). It is only enforced by `dotnet format`, which runs on `Autonogrammer.Linux.slnf` projects => never sees plugins/. No change required; if an editor-level override is wanted add `[plugins/**]` with `ij_formatter_enabled`-style opt-out [INFERENCE], not needed for CI.

### Tests that enumerate the repo tree (grep `EnumerateFiles|GetFiles|GetDirectories|EnumerateDirectories|ls-files` across tests/, src/, services/, apps/)
| test / code | scope | affected by plugins/ ? |
|---|---|---|
| `tests/Autonogrammer.ArchitectureTests/TodoMarkerScanner.cs` (`RepoHygieneTests.TodoMarkersLinkToOpenQuestion`) | walks the **entire repo root** recursively (`Directory.GetDirectories/GetFiles` from `FindRepoRoot()`, root = dir containing Autonogrammer.slnx) | **YES, breaks** |
| `RepoHygieneTests.NoDockerfileOrComposeFileNamed` | `git ls-files` in parent repo (a submodule lists as one gitlink path) | no |
| `RepoHygieneTests.RepoHygiene_NoDockerTooling` | reads 3 fixed files: bootstrap.sh, .github/workflows/ci.yml, deploy/compose.yaml | no |
| `ProjectGraphChecker` (ProjectGraphTests, ArchUnitLayerTests) | `*.csproj` under src/services/apps | no |
| `WorkflowPinTests` | `.github/workflows/*.yml` | no |
| `ImagePinTests` | deploy/compose.yaml, scripts/test-services.sh | no |
| `Autonogrammer.Native.Tests/Theme/ColourLiteralTests` | `*.axaml` under the views dir | no |
| `FontResourceTests`, `TempRepository`, ShellWave7/Terminal tests (/proc), Worktrees/Registry/Herdr/Sandbox tests | temp dirs, /proc, one fixed dir | no |
| `Autonogrammer.Harness.Workspace` (WorkspaceSearch/WorkspaceFiles), `LocalWorkspaceFileSystem` (product code, runtime) | open workspace at runtime | n/a |
| `scripts/web-smoke/run-smoke.sh` | publishes web head and API, no tree glob | no |
No other test reads secrets or banned strings across the tree.

**TodoMarkerScanner detail** (lines 29-44, 80-145): scanned extensions `.cs .razor .csproj .props .targets .sh .yml .yaml .json .slnf .slnx .css .js .ts .tsx .html .axaml`; excluded **path segments** (ordinal, any depth): `.git bin obj node_modules TestResults output .planning docs TestData .nuget`, plus `wwwroot/lib`; `SelfExcludedPaths` = the two test files. Pattern `\b(?:TODO|FIXME|HACK|XXX)\b`; line passes only if it also contains an `OQ-NN`/`Q-UPPER-...` ID whose row in docs/open-questions.md has a non-empty Owner. `plugins` is not excluded and `.git` as a file inside a submodule is skipped by the `.git` segment rule only for the file itself.
Simulation (git ls-files of each sibling at its working HEAD, same extension and segment rules, `grep -w`): marker lines without OQ link: claude-ultrathink **11** lines in 6 files (e.g. src/claude/output.ts:42,371,374; src/decisions/questions.ts:33,37; src/decisions/questions.test.ts:50,54; src/host/grokbot.test.ts:868) of 570 scanned files; agent-swarm **1** (omp/src/bridge.ts:24 comment `"E-XXX: "`) of 115; agent-substrate **5** in 3 files (packages/cli/src/lessons.test.ts:1302,1310; packages/mcp-server/src/lesson-content.ts:35,57; packages/mcp-server/src/memory-write.test.ts:428) of 461. => `TodoMarkersLinkToOpenQuestion` would FAIL (17 offenders) as soon as the three submodules are checked out under the repo root.

### Required changes so submodule dirs are excluded / handled
1. `tests/Autonogrammer.ArchitectureTests/TodoMarkerScanner.cs`: add `"plugins"` to `TodoExcludedScanSegments` (line 40-44) and update the XML doc list at lines 23-27 and the doc comment at 10-28 (vendored third-party submodules are not first-party). No directory named `plugins` exists today (`git ls-files | grep -c -E '(^|/)plugins/'` = 0) and `src/Autonogrammer.Harness.Plugins` does not match (ordinal, whole segment). Add a control test next to `TodoScan_*` (RepoHygieneTests.cs:125-177) planting a marker under `plugins/x/a.ts` expecting no offender. (Alternative: scan `git ls-files` instead of the filesystem - bigger change.)
2. `.github/workflows/ci.yml`: add `submodules: recursive` (and `token: ${{ secrets.<NAME> }}` for private agent-substrate) to checkout steps of jobs that need plugin files (needed only if a test/build reads plugins/); keep the SHA pin and `permissions: contents: read`. For gitleaks decide whether to scan submodule trees separately [policy decision]. Also reconcile with main's ci.yml (runner labels, unpinned `@v7`) at merge time; see section 2.
3. `.containerignore`: add `plugins` (optional hygiene; image not affected).
4. `.gitignore`: nothing required. `.editorconfig`/`dotnet format`/`Directory.*.props`/`bootstrap.sh`: nothing required for correctness; document `git clone --recurse-submodules` / `git submodule update --init --recursive` in README/CONTRIBUTING/docs/runbooks/local-dev.md (no mention of submodules there today: `grep -i submodule` in README, AGENTS.md, CONTRIBUTING.md, docs/ = no hits) and add a bootstrap.sh preflight (`git submodule update --init --recursive` or a check that plugins/* are non-empty) if the runtime needs them.
5. `.gitmodules` must use `https://github.com/swcstudiospace/<repo>.git`; pins per section 1.
6. Vercel: if a Vercel Git integration is linked, the private agent-substrate submodule will fail its clone (per Vercel docs); UNKNOWN: run `vercel project ls` / check dashboard.

## 5. Licences (facts only)
- Autonogrammer: no LICENSE/COPYING/NOTICE tracked at root (`git ls-files | grep -i -E '^(licen|copying|notice)'` empty); `gh api repos/swcstudiospace/OmpOS/license` -> 404; no `PackageLicense*` in any csproj/props; README/AGENTS.md/CONTRIBUTING.md mention licences only of third-party packages.
- claude-ultrathink: `LICENSE` first lines: `GNU AFFERO GENERAL PUBLIC LICENSE` / `Version 3, 19 November 2007` / `Copyright (C) 2007 Free Software Foundation, Inc. <https://fsf.org/>`. Root `package.json`: `"license": "AGPL-3.0-or-later"`. README `## License`: `Copyright (C) 2026 SWC Studio`; "either version 3 of the License, or (at your option) any later version." GitHub detects `AGPL-3.0`. `plugins/omp-token-opt/package.json` (a nested package in that repo) has NO license field.
- agent-swarm: **no LICENSE file** (tracked files matching licence names: only prompts/skills/a14-maintenance/license-recheck.md and a18-supply-chain/license-exception.md, unrelated skill prompts); `package.json`, `omp/package.json`, `pyproject.toml`, `Cargo.toml`: no license field; README has no licence mention; GitHub licence API 404.
- agent-substrate: **no LICENSE file**; all 10 package.json files (root, packages/audit, cli, collectors, evidence, host, ledger, mcp-server, observability, projector) and pyproject.toml: no license field; GitHub licence API 404; repo is PRIVATE.
- Whether the plugins' package.json declare them: only claude-ultrathink root package.json declares (AGPL-3.0-or-later); omp/package.json (swarm) and packages/mcp-server, packages/projector (substrate) declare none. UNKNOWN whether swarm/substrate are proprietary or simply unlicensed: ask the owner (all-rights-reserved is the default absent a licence).
- Linking by submodule (no copying) in factual terms: a submodule stores only a gitlink (path + commit SHA) and `.gitmodules` URL in Autonogrammer's tree; no sibling source is committed into Autonogrammer history, so Autonogrammer's repository itself contains no AGPL text from the siblings. A clone with `--recurse-submodules`, a release archive that includes submodule checkouts, or the container image if it copied plugins/ (it currently does not) would distribute the sibling files under each sibling's own terms, and AGPL-3.0 section 13 network-interaction and source-offer terms attach to the AGPL component (claude-ultrathink) where it is modified/run as a network service. Distributing the three together while two have no declared licence means the third-party reuse terms of swarm/substrate are undefined. Runtime composition: omp loads the plugins as separate extensions in the omp process (settled decision q1), not linked into the C# assemblies. Advisable docs note (not legal advice): add a "Third-party components / licences" section to README (or docs/) listing each submodule, its pinned SHA, its licence (AGPL-3.0-or-later; none declared; none declared), "not copied into Autonogrammer source", how to fetch them, and state Autonogrammer's own licence once the owner chooses one (none exists yet); get the owner to add LICENSE to swarm and substrate or confirm they are proprietary-internal before any distribution of archives/images containing them.

## Blockers / risks for C# Autonogrammer host integration
1. main cannot be fast-forwarded: 5 file conflicts incl. ci.yml (runner pool + unpinned checkout vs HEAD's SHA pins + WorkflowPinTests) and MAUI csproj modify/delete; merge origin/main into the branch before pushing main. Main has no branch protection, so GitHub will not stop a direct/force push; do not force.
2. Pins: swarm HEAD is unpushed (pin c900a51 instead, omp/+swarm/+prompts/ identical); substrate pin 754c51a (PR #123 closed, HEAD branch tail may vanish); ultrathink HEAD 00ce3e6 lives on open PR #59 branch (pinnable now).
3. TodoMarkersLinkToOpenQuestion fails with 17 offenders unless `plugins` is added to TodoExcludedScanSegments.
4. agent-substrate is PRIVATE: CI checkout needs a PAT/App token secret (GITHUB_TOKEN cannot read it); Vercel clone of a private submodule fails per docs; fork PRs get no secret.
5. Omega CI runners (`omega-ci`) all showed offline; main's CI depends on them.
6. Licence gap: Autonogrammer has no LICENSE; swarm and substrate have none; ultrathink is AGPL-3.0-or-later.
7. Local sibling working trees are dirty (swarm 142 files); a pin never contains uncommitted edits, e.g. swarm/ python runtime edits (74 files) and omp/test/commands.test.ts.
