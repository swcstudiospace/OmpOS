# Phase 66: Rebrand to Autonogrammer - Context
Source: docs/plans/v0.16.0-research/briefs/66-rebrand.md
**Gathered:** 2026-10-28
**Status:** Ready for planning

<domain>
## Phase Boundary

Every name the project owns changes from OmpOS to Autonogrammer in one mechanical pass (paths, contents, identifiers, namespaces, containers, schema, headers, Android id) by `scripts/rebrand/rebrand.py`; existing users keep their data through three C# runtime migrations; the generated files are regenerated; the renamed tree is built and gated once. Requirements AG-66-1 to AG-66-14. Work units U1 to U6 (brief 66): U1 apply and commit, U2 regenerate, U3 three runtime migrations, U4 agent files and docs, U5 test-services isolation, U6 verification and owner-action list. Owner questions OQ-490 to OQ-493 (rows in `docs/open-questions.md:310-313`).

Dependencies: the index phase table (row 66) and brief 66 say "none; applied AFTER the units of phases 67 to 76 are written post-rename" (they use new names and must not run the tool); `.planning/ROADMAP.md:1322` says "Depends on: None" and records the mechanical pass as commit `4af7d83`, and `.planning/ROADMAP.md:1336` makes Phase 66.1 depend on 66 (the scaffold writes post-rename names). The tree follows the ROADMAP: the mechanical pass came first, so the later phases write post-rename names directly and `dry-run` or a replay (phase 77 U7) only finds new old-name content. Phase 77 needs 66 applied. No ADR: physical names are ADR-0031 D1, the owner decision is `docs/runbooks/rebrand-autonogrammer.md` s.1. Plan file: `66-01-PLAN.md` (ROADMAP:1331).

State at gathering (fact, `git log`): commit `4af7d834` "rename(66-01): OmpOS to Autonogrammer (mechanical; scripts/rebrand apply)" is HEAD; it is U1's content commit. The regenerate commit (U2) does not exist, `LegacyBrandNames.cs` does not exist, and `CLAUDE.md` exists as an untracked file. The dry-run baseline is at `/tmp/ag-recon/rebrand-report/rebrand-report.json` (outside the repo). The remaining work of this phase is U2 to U6 plus the checks of AG-66-1 to AG-66-3 on the committed state.

Must NOT do: new artwork, the trademark decision, any external rename (GitHub, Vercel, Railway, domain), `--urls`, a store identity for Android (all owner actions, runbook s.5); no new project, package or contract change; no edit of `scripts/rebrand/` (exceptions go to `rebrand.config.json`); no commit that mixes hand-written code with tool output.

</domain>

<decisions>
## Implementation Decisions

### Ordering and tool behaviour
- **D-01:** The rename is one mechanical pass by `scripts/rebrand/rebrand.py` (Python 3.12, standard library only): paths by `git mv` deepest first, then contents; the tool never commits, refuses a dirty tree and any path collision, and a second run changes nothing. (runbook s.1, s.2; brief 66 Goal)
- **D-02:** Order of work: runbook s.4 order and s.3 (replay, do not merge). Index row 66 and brief 66 order the application after the units of 67 to 76 (they write post-rename names and never run the tool); ROADMAP:1322 and `4af7d834` show U1 already applied first, so the phase ordering of the ROADMAP stands (66, 66.1, then 67 to 76) and any old-name content that later lands is renamed by a replay on a fresh branch (OQ-484 default: replay). (brief 66 Depends on; index row 66; ROADMAP:1322; brief 77 U7)
- **D-03:** Two commits, neither mixing hand-written code: commit 1 `rename: OmpOS to Autonogrammer (mechanical, tools: scripts/rebrand)` (content and paths); commit 2 the regenerate output. (AG-66-5; runbook s.3 step 1)
- **D-04:** Before apply: delete `obj/` and `bin/`; clean tree; `dry-run --report-dir /tmp/ag-recon/rebrand-report` (report outside the repo; the tool refuses a report dir inside `--root`); read `suspect` rows, flags and collisions; dry-run exit 2 means collisions and blocks apply. (AG-66-1; runbook s.2 before-apply 1 to 3)
- **D-05:** `apply` runs with the default `--engine-adapters keep` and without `--urls`, ends with `post-apply verification: clean`; then `verify --baseline <dry-run json>` must pass: nothing left to rename, rejected near-match counts and every `keepPatternCounts` entry (including bare `omp`) equal the baseline, `git grep -c compose` equal before and after. (AG-66-2, AG-66-3)

### Protected and kept names (never rewritten)
- **D-06:** The external omp engine keeps its names: bare `omp`, `omp acp`, `omp --mode rpc`, `~/.omp` and `$HOME/.omp`, `.omp/`, `OMP_*`, `PI_CODING_AGENT_DIR`, `omp://`, `@oh-my-pi/*`, `oh-my-pi`, `models.yml`, `agent.db`, `omp_result`, `SUBSTRATE_*OMP*`, `FAKE_OMP_*`, `gsd-core-omp`, `-hooks-omp`. Each is a `keepPatterns` entry and counted in the baseline. (runbook s.1; `rebrand.config.json:25-43`)
- **D-07:** First-party identifiers that name the engine integration (`OmpLauncher`, `OmpAcpClient`, `FakeOmp`, `ompProviderId`, the `Harness.OmpConfig` and `Harness.OmpLauncher` projects) stay: `--engine-adapters keep` is the only mode used; `rename` is not recommended because it would call `OmpLauncher` `AutonogrammerLauncher`. Renaming them later is OQ-492 (default no). (runbook s.1, s.2; brief 66 OQ-492; report 01 s.2.3)
- **D-08:** Exactly five product abbreviations are renamed, always, by exact identifier in contents and file names: `OmpApiFactory` to `ApiFactory`, `OmpTelemetry` to `ApiTelemetry`, `AddOmpOpenApi`, `AddOmpWebServices`, `AddOmpWebDemoServices` to `AddAutonogrammer*`. Another `Omp*` that abbreviates the product is added to the explicit map, never a pattern. (runbook s.1, s.6; `rebrand.config.json:44-50`)
- **D-09:** URLs and hostnames that need an external rename first are protected until `--urls`, and every occurrence is listed as an owner action: repo slug `swcstudiospace/OmpOS`, `ompos-*.vercel.app`, `ompos-web` and `ompos-api` Railway or Vercel names (only on lines that mention Vercel or Railway), `ompos.dev`. `--urls` is run only after those renames exist. (runbook s.1, s.5 item 1; `rebrand.config.json:57-63`)
- **D-10:** Words that merely contain the letters (`compose`, `composer`, `decompose`, `composition`, `component`, `company`, `prompt`, `StringComparison`) are never touched; the boundary regex is mandatory (a plain substring replace corrupts 4,756 occurrences). (runbook s.1; report 01 s.1.3)
- **D-11:** Content frozen: `.gitleaksignore`, `LICENSE`, `LICENSES/**`, `docs/runbooks/evidence/**`, `.planning/milestones/**`, recorded frames `tests/*/TestData/frames/**`, `THIRD-PARTY-NOTICES.md`, `docs/api/openapi.json`, `**/packages.lock.json`, released `CHANGELOG.md` sections, `**/LegacyBrandNames.cs`, and the tool, runbook and report 01 themselves. Path frozen only for evidence, milestones and legal files; binaries keep content but their brand-named paths are renamed. (runbook s.1, s.6; `rebrand.config.json:65-81`)
- **D-12:** An exception is a line in `rebrand.config.json` (`excludedGlobs`, `partialFiles`, `binaryExtensions`, `engineAdapters.keepPatterns`, `protectedUrlPatterns`), never a change of the tool or hand-editing of its output; globs are brand-free and a unit test fails on a glob naming a brand segment. (runbook s.6; brief 66 Projects and files owned)

### Hard cutover and the three runtime migrations (U3)
- **D-13:** Hard cutover by owner decision: environment variables (`OMPOS_*` becomes `AUTONOGRAMMER_*`), the contract and webhook headers, container, image and label names, the git branch prefix, the mention URI scheme, the `ompos_hooks_consent` claim and the PostgreSQL schema have no fallback; producer and consumer change in the same release. Only three kinds of user-owned state get code. (runbook s.1, s.7 last paragraph) The "A" (alias) column of report 01 s.2.4 and its `compatAliases` list are NOT adopted.
- **D-14:** Data directory (AG-66-7): one shared resolver returns the `Autonogrammer` directory for the six sites (`FileMcpStateStore.cs:36`, `EnvironmentProfileStore.cs:46`, `FileSetupConsentStore.cs:50`, `WorkspaceSetStore.cs:48`, `AgentLoopEnvironment.cs:40`, `HeadServices.cs:424`); a legacy `OmpOS` directory is moved once, atomically (same volume, else copy then delete), leaving a marker file; a new file is never overwritten; the legacy directory is never read afterwards; both present means new wins, legacy untouched, warning shown; two processes on first start perform one migration. (runbook s.7 item 1)
- **D-15:** Convention directories (AG-66-8): discovery order `.autonogrammer`, legacy `.ompos` (read-only, deprecated), `.omp` (engine, unchanged), for `mcp.json`, `hooks.json`, `rules/`, `skills/`, `commands/`, `agents/`, `run-mode.json`, `environment.json`, `permissions.json`, `sandbox.json`, `setup.json`, `ignore`, plus the system hook paths and `EnvironmentConfigFile.cs`. A legacy-only directory is read with a one-time notice and a documented move command (`git mv` inside a repo); nothing is ever written into a legacy directory; legacy-only `permissions.json` and `sandbox.json` are still enforced and shown as a conflict, never silently ignored; new wins and a conflict is reported, not merged silently. (runbook s.7 item 2; brief 66 Degraded and error states)
- **D-16:** `models.yml` markers (AG-66-9): `OmpModelsLayout.cs` and `OmpManagedBlockSpan.cs` read, update and remove both `# autonogrammer:begin/end <id>` and the legacy `# ompos:begin/end <id>`; a legacy block is rewritten under the new markers in the same write (never both, no duplicated provider entries); removal deletes both families; legacy plus new for one id means new wins and legacy is removed; unmanaged bytes and comments are preserved byte for byte. (runbook s.7 item 3)
- **D-17:** Old-name literals exist only in files named `LegacyBrandNames.cs`, one per project that needs them (Workspace, Mcp, Remote, AgentLoop, Plugins, OmpConfig, Native, Api); a test fails on any other old-name literal under `src/`, `services/`, `apps/`. OQ-491 default: an architecture test over source files. (AG-66-10; brief 66 OQ-491)
- **D-18:** The legacy `.ompos` read path is kept one minor release after 0.16.0, then removed (OQ-490 default); the notice move command is a documented command plus a settings button (OQ-493 default). (brief 66 OQ-490, OQ-493)

### Regenerate (U2), docs (U4), isolation (U5), verification (U6)
- **D-19:** Regenerate in this order, then commit 2: 1 `dotnet restore Autonogrammer.Linux.slnf --force-evaluate -p:RestoreLockedMode=false` (Android restored on its own; lock files are never text-rewritten); 2 `dotnet format Autonogrammer.Linux.slnf --no-restore` (re-sorts the usings of about 500 files); 3 build `services/Autonogrammer.Api -c Release --no-restore` and review that only brand strings differ in `openapi.json`; 4 `python3 scripts/legal/generate-third-party-notices.py` then `bash scripts/ci/check-licences.sh`; 5 `python3 scripts/brand/build_brand.py` then `bash scripts/ci/check-brand.sh`. (runbook s.4; brief 66 U2)
- **D-20:** After commit 2: `dotnet restore --locked-mode`, `dotnet build ... -warnaserror`, `dotnet format ... --verify-no-changes --no-restore` and the Linux test set are green, and `docs/api/openapi.json` equals the served document. (AG-66-6)
- **D-21:** `AGENTS.md` and `README.md` use the new name (GSD marker blocks kept as generated; repo URLs stay until `--urls`); `AGENTS.md` gains a short "Code/Cowork and accounts" section; `CLAUDE.md` holds only the import `@AGENTS.md` and one sentence; `CHANGELOG.md` gets the rename entry under `## [Unreleased]` (rename, three migrations, env var and schema hard cutover, Android `ApplicationId`), released sections unchanged. (AG-66-11; brief 66 U4)
- **D-22:** `scripts/test-services.sh` creates only `autonogrammer-test-*` containers (`POSTGRES_NAME=autonogrammer-test-postgres`, `DRAGONFLY_NAME=autonogrammer-test-dragonfly`, DB `autonogrammer_test`, user `autonogrammer`, `AUTONOGRAMMER_TEST_*` exports) and `down` removes only its own two; the tool already does this, no config change. Run `up`/`down` only when no other session uses those names. (AG-66-12; brief 66 U5; ADR-0031 D1)
- **D-23:** The desktop head window title is `Autonogrammer` (`apps/Autonogrammer.Native/MainWindow.axaml:4`), checked by a launch under a private Xvfb display (never `:99`) or an Avalonia.Headless assertion. (AG-66-13)
- **D-24:** The owner-action list of runbook s.5 is delivered with the result, each item marked open; nothing in the tree claims an external rename happened. Items: external renames then `apply --urls`; `AUTONOGRAMMER_*` in Vercel, Railway, GitHub secrets and `.env` files; podman/PostgreSQL/Dragonfly/GreptimeDB state under old names (deploy API and worker together); Android `ApplicationId` `com.swcstudiospace.ompos` to `com.swcstudiospace.autonogrammer` (new store identity); legal wording `NOTICE:41` and `docs/licensing.md`; `.gitleaksignore` fingerprints for relocated frames; three submodules; artwork and trademark. (AG-66-14; runbook s.5; brief 66 Owner actions)

### Claude's Discretion
- Name and project of the shared data-directory resolver, the marker file name, and the lock mechanism for the concurrent first start (brief: "one shared resolver", "marker file", "one migration").
- Wording of the one-time legacy notice and where the settings button sits (OQ-493: command plus button).
- Whether the AG-66-10 guard is placed in `tests/Autonogrammer.ArchitectureTests` (OQ-491 default) or a project test.
- Which fixtures and fake-home helpers the U3 tests share; split of U3 into three commits.

</decisions>

<specifics>
## Specific Ideas

- Legacy spellings (frozen in the runbook, s.7): user data directory `OmpOS`; convention directory `.ompos` and `~/.ompos`; system hook paths `/Library/Application Support/OmpOS/hooks.json`, `%ProgramData%\OmpOS`, `/etc/ompos/hooks.json`; markers `# ompos:begin <id>` and `# ompos:end <id>`.
- Web smoke with non-colliding ports: `API_PORT=5287 WEB_PORT=5280 DEMO_PORT=5291 bash scripts/web-smoke/run-smoke.sh` after `ss -ltn` (brief 66 Success criteria).
- The runbook probe 1 (`git grep -niE 'ompos'`) also matches `compose` and lists thousands of lines (3,516 observed at HEAD); a boundary-aware pattern (the `brandRegex` of the config) lists protected URLs (and one recorded frame the tool freezes); see 66-RESEARCH.md.
- A replay of the tool also rewrites old-name literals inside `.planning/phases/**` (not in `excludedGlobs`, `rebrand.config.json:65-81`); keep legacy spellings out of prose there or freeze by config [INFERENCE].

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Decision record and phase brief
- `docs/plans/v0.16.0-research/briefs/66-rebrand.md` - requirements AG-66-1 to AG-66-14, units U1 to U6, owner actions, OQ-490 to OQ-493 (parts read post-rename: legacy and new spellings collapsed, use the runbook for the old spellings).
- `docs/runbooks/rebrand-autonogrammer.md` - s.1 zones, s.2 usage and probes, s.3 replay, s.4 regenerate order, s.5 owner actions, s.6 exceptions policy, s.7 the three migrations (never rewritten by the tool).
- `docs/plans/v0.16.0-research/01-rename-inventory.md` - rename inventory: s.2 classification, s.2.4 external-bound items, s.3 path plan, s.4 exclusions, s.5 config; frozen.
- `docs/plans/v0.16.0-research/briefs/00-index.md` - phase table row 66, R17 (scaffold owns registries); `docs/plans/v0.16.0-research/briefs/00-scaffold-manifest.md` - runs after or writes post-rename names; `.planning/ROADMAP.md:1319-1331` (Phase 66 criteria 1 to 5) and `.planning/REQUIREMENTS.md:621` (AG-66 block).
- `docs/decisions/ADR-0031-data-plane-namespaces-and-resilience.md` D1 - physical names (`autonogrammer:v1:` prefix, schema, roles, test container names).

### Tool
- `scripts/rebrand/rebrand.py` (1194 lines), `scripts/rebrand/rebrand.config.json` (data), `scripts/rebrand/test_rebrand.py` (49 tests in 9 classes).

### Sites of the migrations (paths verified on disk)
- `src/Autonogrammer.Harness.Mcp/Catalog/FileMcpStateStore.cs`, `src/Autonogrammer.Harness.Remote/EnvironmentProfileStore.cs`, `src/Autonogrammer.Harness.Workspace/FileSetupConsentStore.cs`, `src/Autonogrammer.Harness.Workspace/WorkspaceSet/WorkspaceSetStore.cs`, `apps/Autonogrammer.Native/Agents/AgentLoopEnvironment.cs`, `apps/Autonogrammer.Native/Composition/HeadServices.cs` (data directory).
- `src/Autonogrammer.Harness.Workspace/WorkspaceConventions.cs`, `.../Conventions/ConventionDirectories.cs`, `src/Autonogrammer.Harness.AgentLoop/Safety/SafetyFileLocations.cs`, `.../FileRunModeStore.cs`, `src/Autonogrammer.Harness.Mcp/Config/McpLocations.cs`, `src/Autonogrammer.Harness.Plugins/Hooks/HookSourceLocator.cs`, `services/Autonogrammer.Api/Cloud/Environment/EnvironmentConfigFile.cs` (convention directories).
- `src/Autonogrammer.Harness.OmpConfig/OmpModelsLayout.cs`, `.../OmpManagedBlockSpan.cs` (markers).
- Gate and generators: `scripts/test-services.sh`, `scripts/legal/generate-third-party-notices.py`, `scripts/ci/check-licences.sh`, `scripts/brand/build_brand.py`, `scripts/ci/check-brand.sh`, `bootstrap.sh`; tests `tests/Autonogrammer.ArchitectureTests`, `tests/Autonogrammer.Api.IntegrationTests` (`GuardTests`), `tests/Autonogrammer.Web.Tests` (`OpenApiCoverageTests`).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `rebrand.py` subcommands `dry-run` (default), `apply`, `verify --baseline`; reports `rebrand-report.md` and `.json` (`scripts/rebrand/rebrand.py:1-31`).
- Existing per-site stores already centralise their path (`Path.Combine(configuration, "Autonogrammer", ...)`, e.g. `FileMcpStateStore.cs:36`), so the resolver replaces six constants.
- `OmpModelsLayout.BeginMarker` and `EndMarker` (`OmpModelsLayout.cs:11,14`) are the single place the marker text lives; `OmpManagedBlockSpan` documents the marker lines (`OmpManagedBlockSpan.cs:5-6`).

### Established Patterns
- Frozen zones and exceptions are config data; the tool holds no state (runbook s.6, s.3).
- Tests use temp directories and fake homes, no real `omp`; Live tests are gated and skipped with a reason (report 09 s.4).
- A project that needs old-name literals holds them in `LegacyBrandNames.cs` (excluded from rewriting by `rebrand.config.json:75`).

### Integration Points
- Commit 1 at HEAD is the U1 content commit (`4af7d834`); U2 to U6 start from it. `docs/runbooks/rebrand-autonogrammer.md` is already listed in `docs/SUMMARY.md:69`.
- `.gitleaksignore` fingerprints for relocated frames are added after the rename commit exists (runbook s.5 item 6).
- Phase 77 U7 replays this tool on a new base and re-runs the regenerate steps (brief 77).

</code_context>

<deferred>
## Deferred Ideas

- New brand artwork (renamed PNG, ICO and ICNS keep the old pixels), the trademark decision, the replacement domain: design and legal tasks (runbook s.4 step 5, s.5 item 8).
- `apply --urls` and every external rename (GitHub repo, Vercel project, Railway services, domain): owner actions, not done here (OMITTED here by decision; runbook s.5).
- Android `ApplicationId` change is a new store identity; installed apps will not upgrade (runbook s.5 item 4).
- Renaming `OmpLauncher` and other engine-adapter identifiers (`--engine-adapters rename`): OQ-492, default no.
- Alias or compatibility window for env vars, headers, labels, branch prefix, schema (report 01 s.2.4 "A"): not built, hard cutover by owner decision (runbook s.1).
- Open questions, defaults: OQ-490 (legacy `.ompos` read path kept one minor release after 0.16.0; owner), OQ-491 (guard is an architecture test; architect), OQ-492 (no rename; owner), OQ-493 (command and settings button; owner).

</deferred>

---

*Phase: 66-rebrand-to-autonogrammer*
*Context gathered: 2026-10-28*
