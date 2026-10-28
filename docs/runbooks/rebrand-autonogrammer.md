# Runbook: rename OmpOS to Autonogrammer

Status: Tool and runbook written; the rename is not applied to this tree yet (phase 66, briefs/66-rebrand.md, applies it)

One-shot, repeatable rename of everything the project owns. The tool is `scripts/rebrand/rebrand.py` (Python 3.12, standard library only), its data is `scripts/rebrand/rebrand.config.json`, its tests are `scripts/rebrand/test_rebrand.py`. This page, the tool and `docs/plans/v0.16.0-research/01-rename-inventory.md` are the only files the tool never rewrites (they must keep naming the old brand).

## 1. What the tool does

Owner decision: hard cutover of every name we own, with these exceptions only.

| Zone | Policy | Where it lives |
| --- | --- | --- |
| Brand tokens `OmpOS`, `OmpOs`, `Ompos` become `Autonogrammer`; `ompOs`, `ompos` become `autonogrammer`; `OMPOS` becomes `AUTONOGRAMMER` | rewritten in contents and in every path segment | `tokenMap`, `brandRegex` |
| Words that merely contain the letters (`compose`, `composer`, `decompose`, `composition`, `component`, `company`, `prompt`, `StringComparison`, ...) | never touched, listed as rejected near-matches | boundary rules in `brandRegex` |
| External omp engine: bare `omp`, `omp acp`, `omp --mode rpc`, `~/.omp`, `.omp/`, `OMP_*`, `omp://`, `@oh-my-pi/*`, `models.yml`, `agent.db`, `omp_result`, `SUBSTRATE_*OMP*`, `gsd-core-omp`, `-hooks-omp` | kept | `engineAdapters.keepPatterns` |
| First-party identifiers that name the engine integration (`OmpLauncher`, `OmpAcpClient`, `FakeOmp`, `ompProviderId`, ...) | kept by default; renamed only with `--engine-adapters rename` | `engineAdapters.engineAdapterRules` |
| The five product abbreviations `OmpApiFactory`, `OmpTelemetry`, `AddOmpOpenApi`, `AddOmpWebServices`, `AddOmpWebDemoServices` | always renamed (exact identifier match), in contents and file names | `engineAdapters.productAbbreviations` |
| URLs and hostnames that need an external rename first: repo slug `swcstudiospace/OmpOS`, `ompos-*.vercel.app`, `ompos-web`/`ompos-api` Railway or Vercel names (on lines that mention Vercel or Railway), `ompos.dev` | protected until `--urls`; every occurrence is listed as an owner action | `protectedUrlPatterns` |
| Binary files (extension list or NUL byte) | content never touched, brand-named paths still renamed (`git mv`) | `binaryExtensions` |
| Generated or recorded files: `.gitleaksignore`, `LICENSE`, `LICENSES/**`, `docs/runbooks/evidence/**`, `.planning/milestones/**`, recorded frames `tests/*/TestData/frames/**`, `THIRD-PARTY-NOTICES.md`, `docs/api/openapi.json`, `**/packages.lock.json`, released `CHANGELOG.md` sections, `**/LegacyBrandNames.cs` | content frozen (regenerate generated ones, see section 4); path frozen only for the evidence, milestones and legal files | `excludedGlobs`, `partialFiles` |
| Symlinks and submodule gitlinks | never rewritten or renamed by themselves; a symlink inside a renamed directory moves with it | built in |

Everything else is rewritten, including ADRs, `.planning/phases/**`, `docs/plans/**` research, plugin ids and directories (`plugins/ompos-hooks-omp` becomes `plugins/autonogrammer-hooks-omp`), `.ompos` convention dir names, `OMPOS_*` variables, the `X-Ompos-Contract-Version` header, container, image and label names, the PostgreSQL schema, Dragonfly key prefixes, OpenTelemetry instrument names and the Android `ApplicationId`.

Properties guaranteed by tests (`python3 -m unittest scripts/rebrand/test_rebrand.py`): dry-run writes nothing into the repository; apply refuses on any path collision (two sources to one target, case-insensitive; target exists in the tree or on disk; file/directory clash; a directory whose entries disagree about its new name) and on a dirty tree; moves are `git mv`, deepest first; nothing is ever committed; UTF-8 BOM, CRLF/LF mix and file modes are preserved; non-UTF-8 files are left alone and reported; a second run changes nothing.

## 2. Usage

```sh
cd /path/to/worktree                       # must be a git work tree, clean unless --allow-dirty
python3 scripts/rebrand/rebrand.py                       # same as: dry-run
python3 scripts/rebrand/rebrand.py dry-run --report-dir /tmp/ag-recon/rebrand-report
python3 scripts/rebrand/rebrand.py apply                 # git mv + rewrite, no commit
python3 scripts/rebrand/rebrand.py verify --baseline /tmp/ag-recon/rebrand-report/rebrand-report.json
```

| Flag | Meaning |
| --- | --- |
| `--root DIR` | repository root (default `.`) |
| `--config FILE` | token and exclusion data (default `rebrand.config.json` next to the script) |
| `--urls` | also rewrite the protected URLs and hostnames (only after the external renames exist) |
| `--engine-adapters {keep,rename}` | `keep` (default): engine-integration identifiers stay. `rename`: `Omp*` identifiers and `omp*` camel prefixes become `Autonogrammer*`/`autonogrammer*`; bare `omp`, `OMP_*`, `omp://`, `~/.omp`, `models.yml` and every other keep pattern still stay. Not recommended: it renames `OmpLauncher` to `AutonogrammerLauncher`, which misdescribes the class |
| `--report-dir DIR` | where `rebrand-report.md` and `rebrand-report.json` go (default `/tmp/ag-recon/rebrand-report`; refused when inside `--root`) |
| `--allow-dirty` | apply on a tree with uncommitted changes (the diff will mix) |
| `--baseline FILE` | `verify`: the JSON a dry-run wrote before apply |

Reading the dry-run report (`rebrand-report.md`): headline numbers; every distinct matched casing with count, file count and two examples; counts per zone, per kind of occurrence (env var, header, dot directory, kebab/CSS, namespace, identifier, prose) and per excluded class; every rejected `ompos` near-match with its reason (`benign` = compose family, `suspect` = needs a human: for example `IOmpOS` with an uppercase left edge, or a casing outside the token map); the engine census (kept tokens, kept first-party `Omp*` identifiers, product abbreviations); the path plan (all moves, collision check); every protected URL; owner actions; ambiguous items. Exit code 2 means collisions.

Before apply:

1. Commit or stash everything. Delete `obj/` and `bin/` trees (they cache the old paths; a leftover `src/Autonogrammer.X/obj` also trips the collision check).
2. Run `dry-run`, read the `suspect` rows, the flags section and the collision section. Fix by editing the config, not the tool.
3. Run `apply`. It moves paths with `git mv` (staged), rewrites contents, then re-plans and reports `post-apply verification: clean`.
4. Run `verify --baseline <dry-run json>`: proves nothing is left to rename and that the rejected near-match counts and every engine keep-pattern count (including bare `omp`) are identical to before. `git grep -c compose` before and after stays equal.

Acceptance probes after apply (all cheap):

```sh
git grep -niE 'ompos' -- . ':!docs/runbooks/evidence' ':!.planning/milestones' ':!THIRD-PARTY-NOTICES.md' ':!docs/api/openapi.json' ':!**/packages.lock.json' ':!scripts/rebrand' ':!.gitleaksignore' ':!CHANGELOG.md' ':!**/LegacyBrandNames.cs' ':!tests/*/TestData/frames' ':!docs/runbooks/rebrand-autonogrammer.md' ':!docs/plans/v0.16.0-research/01-rename-inventory.md'
git ls-files | grep -i ompos          # only evidence and milestone paths remain
git grep -c 'omp acp'                 # unchanged outside excluded zones
```

The first command lists only protected URLs until `--urls` is used.

## 3. Re-running on a newer base

The tool holds no state, so it can be replayed whenever the other branch (the pre-rename `OmpOS` line) moves. It renames only what still matches; already renamed content is not touched, and nothing it writes can match again (the replacement contains no `omp`).

Preferred: replay, do not merge.

1. Keep the rename as one commit (or none) on top of the feature work: `rename: OmpOS to Autonogrammer (mechanical, tools: scripts/rebrand)`, and regenerate-step changes in a second commit.
2. When the base moves: `git fetch`, create a fresh branch from the new base plus the un-renamed feature commits (drop the old rename commits instead of merging them), run `dry-run`, read the report (new tokens appear as new rows, new directories as new path moves), then `apply`, regenerate (section 4), commit.
3. Content written by other branches after the rename already uses the new names; the tool leaves it alone. Content still using the old names is renamed. Both can be present in one tree and the result is the same.

If a merge is unavoidable (renamed branch absorbs new `OmpOS` commits): merge, then run `apply`; the tool only needs a clean tree. Path renames made by git's rename detection are reconciled by the collision check (a destination that already exists makes apply refuse and tell you which path).

New literals of the old name in new code are allowed only where the config freezes them: `**/LegacyBrandNames.cs` (see section 6), `docs/runbooks/evidence/**`, recorded frames. Anywhere else the next run will rewrite them.

## 4. Regenerate steps, in this order (after apply, before the commit that must be green)

The tool never rewrites generated files. Their old-name tokens are produced again by the commands below.

1. **Relock.** With `obj/` and `bin/` deleted before apply and `CI` unset (SDK 10.0.402, `DOTNET_ROOT` as in `bootstrap.sh`):
   `dotnet restore Autonogrammer.Linux.slnf --force-evaluate -p:RestoreLockedMode=false`.
   85 `packages.lock.json` files change: project ids move (`"ompos.contracts"` becomes `"autonogrammer.contracts"`) and the sorted key order changes, so never text-rewrite them. CI runs `--locked-mode`; the Android project is restored on its own.
2. **`dotnet format`.** `dotnet format Autonogrammer.Linux.slnf --no-restore`. It also re-sorts `using` directives: `.editorconfig` sets `dotnet_sort_system_directives_first = true`, and `Autonogrammer` sorts before `Microsoft` where `OmpOS` sorted after it (the inventory simulated about 500 `.cs` files out of order). The tool does not sort usings. CI gate: `dotnet format ... --verify-no-changes --no-restore`.
3. **OpenAPI.** Build the API (`dotnet build services/Autonogrammer.Api -c Release --no-restore`), which regenerates `docs/api/openapi.json` (title `Autonogrammer.Api | v1`, eight doc strings; `GuardTests`/`OpenApiCoverageTests` require served equals committed). Review the diff: only brand strings may change.
4. **Notices.** `python3 scripts/legal/generate-third-party-notices.py` (needs the restored NuGet cache from step 1; paths such as `src/OmpOS.UI/wwwroot/lib/xterm` become `src/Autonogrammer.UI/...`, sorted tables may reorder). Gate: `bash scripts/ci/check-licences.sh`.
5. **Brand assets.** `scripts/brand/build_brand.py` and `scripts/ci/check-brand.sh` are rewritten by the tool (file list and `name == "Autonogrammer"`); run `python3 scripts/brand/build_brand.py` so `site.webmanifest` and the asset manifest match the renamed files. The renamed PNG/ICO/ICNS files keep the old pixels: new artwork is a design task, not a rename.

Then run `bootstrap.sh` (full gate). Also add the rename entry under `## [Unreleased]` in `CHANGELOG.md` (released sections are frozen) and update `docs/architecture.md`/`docs/SUMMARY.md` only if a page moved.

## 5. Owner actions (cannot be done by the tool)

1. Rename the GitHub repository `swcstudiospace/OmpOS`, the Vercel project (`ompos-web*.vercel.app`), and the Railway service/project names; register `ompos.dev`'s replacement domain. Then run `apply --urls` (or re-run after the next base move). Every occurrence is in the report under "Protected URLs"; GitHub redirects the old repo URL, Vercel and Railway do not. The `NOTICE:20` AGPL source offer, `Directory.Build.props` `RepositoryUrl`, `README.md`, `CHANGELOG.md` compare links, `.github/ISSUE_TEMPLATE/config.yml` and `scripts/ci/check-licences.sh`'s repo-URL grep all follow `--urls`.
2. Rename environment variables in Vercel project settings, Railway service variables, GitHub repository secrets and every developer `.env` (`OMPOS_*` becomes `AUTONOGRAMMER_*`; the list is `git grep -ho 'AUTONOGRAMMER_[A-Z0-9_]*' | sort -u` after apply).
3. Local and hosted state carrying the old names: podman containers, volumes and secrets (`ompos-postgres`, `ompos-dragonfly`, `ompos-greptimedb`, worker image `ompos-agent-worker`, label `io.ompos.cloud-agent`, secret `ompos-{id}-credential`), the PostgreSQL schema `ompos` and its advisory-lock key, Dragonfly key prefixes, GreptimeDB queries on `ompos.*` metrics, git branches `ompos/cloud/*`, JWT issuer `ompos-api` and audience `ompos-worker` (deploy API and worker together). Hard cutover means no alias: recreate or migrate.
4. Android `ApplicationId` `com.swcstudiospace.ompos` becomes `com.swcstudiospace.autonogrammer`: a new store identity; installed apps will not upgrade.
5. Legal text: `NOTICE:41` ("grants no right to use the name ... or its logo") and `docs/licensing.md` get the new name mechanically; confirm the wording.
6. After the rename commit exists, add `.gitleaksignore` fingerprints for the relocated recorded frames (`tests/Autonogrammer.Harness.Rpc.Tests/TestData/frames/rpc-live-...ndjson` and similar). The old lines name history paths and stay untouched.
7. The three submodules (`plugins/agent-substrate`, `agent-swarm`, `claude-ultrathink`) are not in the tree. If they read `OMPOS_*` or `.ompos`, change them in their own repositories before cutover (gate G9 of the inventory).
8. New brand artwork and the trademark decision for `Autonogrammer` are design/legal tasks.

## 6. Exceptions policy

* An exception is a line in `scripts/rebrand/rebrand.config.json`, never a code change: `excludedGlobs` (content and/or path), `partialFiles`, `binaryExtensions`, `engineAdapters.keepPatterns`, `protectedUrlPatterns`.
* Globs are brand-free (`tests/*/TestData/frames/**`, not `tests/OmpOS.*/...`): they are evaluated against old and already renamed paths, which is what keeps a re-run from rewriting a frozen file. A pattern without `/` matches at the repository root only; use `**/name` for any depth. A unit test fails when a glob names a brand segment.
* Freeze content only for recorded or generated material. Freeze a path only when the file is history (evidence, milestone archive, licences); freezing a path inside a directory that is renamed makes the plan refuse ("mixed directory").
* Product-abbreviation identifiers are an explicit map, not a pattern. Add an entry when review finds another `Omp*` that abbreviates the product (the report lists every kept first-party `Omp*` identifier with counts so reviewers can scan it).
* Every kept engine token is counted in the baseline (`keepPatternCounts`), and `verify --baseline` fails when one changes.
* The one legitimate place for old-name literals in code is `**/LegacyBrandNames.cs` (section 7). Any other new occurrence is renamed on the next run.

## 7. C# runtime-migration tasks (hard cutover, existing users keep their data)

The rename switches every name the program reads. Three kinds of user-owned state already exist under the old names and need code, not text replacement. Keep all old literals in ONE file per project, named `LegacyBrandNames.cs` (frozen by the config), so the tool and a re-run never rewrite them. Paths below are the post-rename paths; the inventory (section 2.4 of report 01) has the pre-rename ones.

1. **User data directory.** The per-user config/state directory `OmpOS` (for example `~/.local/share/OmpOS`, `README.md:107`) holds `mcp-state.json`, `environments.json`, `setup-consent.json`, `workspace-sets.json`. Sites: `src/Autonogrammer.Harness.Mcp/Catalog/FileMcpStateStore.cs`, `src/Autonogrammer.Harness.Remote/EnvironmentProfileStore.cs`, `src/Autonogrammer.Harness.Workspace/FileSetupConsentStore.cs`, `src/Autonogrammer.Harness.Workspace/WorkspaceSet/WorkspaceSetStore.cs`, `apps/Autonogrammer.Native/Agents/AgentLoopEnvironment.cs`, `apps/Autonogrammer.Native/Composition/HeadServices.cs`. Task: one shared resolver that returns the `Autonogrammer` directory; when it does not exist and the legacy `OmpOS` directory does, move it once (atomic rename, same volume; copy then delete otherwise), leave a marker file recording the migration, never overwrite an existing new file, and never read the legacy directory afterwards. Acceptance: tests with a fake home covering move, already migrated, both present (new wins, legacy untouched, warning), unreadable legacy, concurrent first start (two processes, one migration).
2. **`.ompos` legacy discovery.** Project and user convention directories (`.ompos/`, `~/.ompos`: `mcp.json`, `hooks.json`, `rules/`, `skills/`, `commands/`, `agents/`, `run-mode.json`, `environment.json`, `permissions.json`, `sandbox.json`, `setup.json`, `ignore`) become `.autonogrammer/`. User repositories already contain `.ompos`. Sites: `src/Autonogrammer.Harness.Workspace/WorkspaceConventions.cs`, `Conventions/ConventionDirectories.cs`, `src/Autonogrammer.Harness.AgentLoop/Safety/SafetyFileLocations.cs`, `FileRunModeStore.cs`, `src/Autonogrammer.Harness.Mcp/Config/McpLocations.cs`, `src/Autonogrammer.Harness.Plugins/Hooks/HookSourceLocator.cs` (also the system hook files `/Library/Application Support/OmpOS/hooks.json`, `%ProgramData%\OmpOS`, `/etc/ompos/hooks.json`), `services/Autonogrammer.Api/Cloud/Environment/EnvironmentConfigFile.cs`. Task: discovery order `.autonogrammer`, then legacy `.ompos` (read-only, deprecated), then `.omp` (engine, unchanged); a convention directory that exists only under the legacy name is read and surfaces a one-time notice with a documented command or button that moves it (`git mv` inside a repo); the tool never writes into a legacy directory; permissions, safety and hook files keep their precedence rules when both exist (new wins, a conflict is reported, not merged silently). Acceptance: tests per file kind for new only, legacy only, both, neither, and for the safety files (`permissions.json`, `sandbox.json`) that a legacy-only file is never silently ignored (it is still enforced).
3. **`models.yml` marker block.** The program writes managed blocks into the user's omp `models.yml` between `# ompos:begin <id>` and `# ompos:end <id>` (`src/Autonogrammer.Harness.OmpConfig/OmpModelsLayout.cs`, `OmpManagedBlockSpan.cs`). The rename changes the written markers to `# autonogrammer:begin/end <id>`. Task: the parser recognises both marker families on read, on update and on removal; a block found under the legacy markers is rewritten under the new markers in the same write (never both, no duplicated provider entries); removal deletes legacy and new blocks; the legacy marker text lives only in `LegacyBrandNames.cs`. Acceptance: tests with a models.yml holding only legacy blocks, only new blocks, one of each for different ids, and a legacy plus a new block for the same id (new wins, legacy removed), plus byte-for-byte preservation of unmanaged content and comments.

Everything else in the inventory's "A" (alias) column is a pure hard cutover by the owner decision: environment variables, the contract and webhook headers, labels, the branch prefix, the mention URI scheme, the `ompos_hooks_consent` claim and the PostgreSQL schema have no fallback; their consumers and producers change together in the same release.
