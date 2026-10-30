# Phase 66: Rebrand to Autonogrammer - Research
Source: docs/plans/v0.16.0-research/briefs/66-rebrand.md

Summarises research report 01 (rename inventory), the runbook `docs/runbooks/rebrand-autonogrammer.md`, `scripts/rebrand/{rebrand.py,rebrand.config.json,test_rebrand.py}` and report 09 (gate, conventions). Reports 02 to 08 are not used. Facts were read from the worktree at HEAD `4af7d834` (the U1 rename commit); nothing was built or run.

## Standard Stack / What to reuse
- The tool: `scripts/rebrand/rebrand.py:1-31` (subcommands `dry-run` default, `apply`, `verify`; flags `--root --config --urls --engine-adapters --report-dir --allow-dirty --baseline`), data in `scripts/rebrand/rebrand.config.json`, tests `scripts/rebrand/test_rebrand.py` (49 `def test_` in 9 classes, `RepoCase` at :39 to `VerifyTests` at :779).
- Config sections to edit instead of the tool: `keepPatterns` `rebrand.config.json:25`, `productAbbreviations` :44, `protectedUrlPatterns` :57, `excludedGlobs` :65 (`**/LegacyBrandNames.cs` at :75), `partialFiles` :83 (`CHANGELOG.md` released sections frozen).
- Baseline from the pre-apply dry-run: `/tmp/ag-recon/rebrand-report/rebrand-report.json` exists on this host (written by the U1 apply; `rebrand.py:1166-1167`).
- Six data-directory sites with the directory literal: `src/Autonogrammer.Harness.Mcp/Catalog/FileMcpStateStore.cs:36`, `src/Autonogrammer.Harness.Remote/EnvironmentProfileStore.cs:46`, `src/Autonogrammer.Harness.Workspace/FileSetupConsentStore.cs:50`, `src/Autonogrammer.Harness.Workspace/WorkspaceSet/WorkspaceSetStore.cs:48`, `apps/Autonogrammer.Native/Agents/AgentLoopEnvironment.cs:40`, `apps/Autonogrammer.Native/Composition/HeadServices.cs:424`.
- Marker constants: `src/Autonogrammer.Harness.OmpConfig/OmpModelsLayout.cs:11` (`BeginMarker`), `:14` (`EndMarker`), unsupported-block messages `:508`, `:518`; `OmpManagedBlockSpan.cs:5-6`.
- Convention constant: `src/Autonogrammer.Harness.Workspace/WorkspaceConventions.cs:9` (`ConventionFolder = ".autonogrammer"`).
- Window title already `Autonogrammer`: `apps/Autonogrammer.Native/MainWindow.axaml:4`. Test services already renamed: `scripts/test-services.sh:9-10`.

## Facts the planner needs
- Current state: HEAD is the rename content commit; the regenerate commit, `LegacyBrandNames.cs` (none tracked) and the three migrations do not exist yet. `CLAUDE.md` exists untracked (`git status`: `?? CLAUDE.md`, 296 bytes, imports `@AGENTS.md`); brief 66 U4 says it does not exist.
- Ordering: `.planning/ROADMAP.md:1322` records the mechanical pass as commit `4af7d83` and gives Phase 66 "Depends on: None"; `00-index.md:11` and brief 66 say it is applied after the units of 67 to 76. The tree follows the ROADMAP. Success criteria 1 to 5 are at `.planning/ROADMAP.md:1326-1330`.
- Tool refusal rules: collisions raise `RebrandError` (`rebrand.py:1041-1042`), dirty tree refuses (`:1043-1049`), tracked paths move by `git mv` (`:1054-1055`), never commits; `apply` prints `post-apply verification: clean` only when a re-plan has nothing pending (`:1166-1173`, exit 1 otherwise); `dry-run` returns 2 on collisions (`:1163`); errors return 2 (`:1188-1190`); `verify` compares `rejectWindows` and every `keepPatternCounts` entry to the baseline (`:1104-1122`). Default report dir `/tmp/ag-recon/rebrand-report` (`:50`).
- Scale (report 01): 5,789 tracked files, 5,016 brand paths in 83 directories, 38,436 brand tokens, 4,756 `compose`-family near-matches the boundary regex must skip (`01-rename-inventory.md:5-16`); 85 `packages.lock.json` relock (`:196`); 2,022 `.cs` files with a brand using-block, about 496 out of order after a text rename, fixed by `dotnet format` (`:200`).
- Engine names kept (`01-rename-inventory.md:100-116`); first-party `Omp*` identifiers 514 distinct, 5,716 hits, default keep (`:118-122`); five product abbreviations (`:124-128`).
- External-bound items and their disposition by owner decision: runbook s.1 overrides the "A" column of report 01 s.2.4 (`01-rename-inventory.md:132-171`): hard cutover (`rebrand-autonogrammer.md:9,122`).
- Runtime-migration sites and acceptance matrices: `rebrand-autonogrammer.md:114-120` (data dir, `.ompos` discovery, `models.yml` markers).
- Protected URLs: GitHub slug, `ompos-*.vercel.app`, Railway hosts and names on Vercel or Railway lines, `ompos.dev` (`rebrand-autonogrammer.md:18`); `Directory.Build.props:10` `RepositoryUrl` still names `swcstudiospace/OmpOS` and stays until `--urls`.
- `ContractVersion.HeaderName` is `X-Autonogrammer-Contract-Version` after the rename (`src/Autonogrammer.Contracts/ContractVersion.cs:12`); this phase does not change `Current` (R9, `00-index.md:101`).
- Observed (HEAD): `git ls-files | grep -ci ompos` is 139 (evidence, milestone and legal paths); the runbook probe 1 `git grep -niE 'ompos'` returns 3,516 lines because the bare substring matches `compose`; the same pathspec with the boundary regex (`git grep -nP '(?<![A-Za-z])(ompos|OMPOS|ompOs)(?![a-z])|(?<![A-Z])Omp[Oo][Ss](?![a-z])'`) returns 196 lines, which include the phase 66 files and the v0.16.0 research briefs; outside those, every line except one recorded ACP frame (`tests/Autonogrammer.Harness.Acp.Tests/TestData/frames/acp-in.ndjson:3`, frozen by config) matches a protected URL pattern.
- The Python tests are not wired into the gate: no `rebrand` or `unittest` line in `bootstrap.sh` or `scripts/ci/repo-checks.sh` (grep, 0 hits).

## Architecture Patterns
- Resolver plus `LegacyBrandNames.cs` per project: the legacy literal lives in one frozen file; callers ask the resolver for the new path; it migrates once and writes a marker (runbook s.7 item 1).
- New-wins precedence everywhere; the legacy side is read-only and surfaced (notice, conflict), never merged silently (runbook s.7 item 2).
- Marker-block editors recognise both families on read, update and remove, and normalise to the new family in the same write (runbook s.7 item 3).
- Replay over merge: keep rename as one commit; on a moved base, branch from the new base plus feature commits and re-run (runbook s.3).

## Pitfalls
- `obj/` and `bin/` trees cache old paths and trip the collision check; delete them first (runbook s.2; brief 66 U1). `tests/*/obj` exist on this host today.
- Text-rewriting `packages.lock.json`: project ids move in sorted order; always `--force-evaluate` (`01-rename-inventory.md:196`; runbook s.4 step 1).
- `CI` set forces locked mode and the relock fails; unset `CI` (`09-project-recipe-and-conventions.md:26`).
- A glob that names a brand segment breaks idempotence; a path frozen inside a renamed directory makes the plan refuse ("mixed directory") (runbook s.6).
- Old-name prose in files outside the frozen list is rewritten on every replay, including `.planning/phases/**` (`rebrand.config.json:65-81` has no entry for it) [INFERENCE].
- The generated `THIRD-PARTY-NOTICES.md` needs the restored NuGet cache from step 1 (runbook s.4 step 4).
- The brand assets keep the old pixels; do not claim new artwork (runbook s.4 step 5).
- Brief 66 wording after the mechanical pass collapses old and new spellings (e.g. AG-66-8 `.autonogrammer` twice, U5 "no hit" for `autonogrammer`); plan from the runbook spellings.

## Verification commands
- `python3 -m unittest scripts/rebrand/test_rebrand.py` (brief 66 Success criteria).
- `python3 scripts/rebrand/rebrand.py dry-run --report-dir /tmp/ag-recon/rebrand-report`; `python3 scripts/rebrand/rebrand.py verify --baseline /tmp/ag-recon/rebrand-report/rebrand-report.json` (runbook s.2).
- `dotnet restore Autonogrammer.Linux.slnf --locked-mode`; `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror`; `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore`; `bash bootstrap.sh` (brief 66; report 09 s.5).
- `bash scripts/ci/check-licences.sh`; `bash scripts/ci/check-brand.sh`; `bash scripts/test-services.sh up` then `down` twice (AG-66-12).
- `API_PORT=5287 WEB_PORT=5280 DEMO_PORT=5291 bash scripts/web-smoke/run-smoke.sh` (free ports checked with `ss -ltn`).
- `git grep -c 'omp acp'` and `git grep -c compose` before and after equal (AG-66-3, AG-66-4).

## Cannot be verified on this host
Windows and macOS data-directory move and system hook paths (`%ProgramData%\Autonogrammer`, `/Library/Application Support/Autonogrammer`: compile only); Android build and the new `ApplicationId`; every deployment (Vercel, Railway, domain, GitHub rename, secrets); the three submodules (not checked out); live omp reading renamed `models.yml` blocks (no live model turn); the real result of `apply --urls`. Index row 66: no brief states more, and the renamed tree has to be built and gated once (`00-index.md:11`).

## Validation Architecture
- Frameworks: Python `unittest` (stdlib) for the tool; xunit v3 on Microsoft Testing Platform with Shouldly for .NET (report 09 s.4: `dotnet test --project tests/<X>.Tests -c Release`).
- Quick run: `python3 -m unittest scripts/rebrand/test_rebrand.py` and `dotnet test --project tests/Autonogrammer.Harness.OmpConfig.Tests -c Release`.
- Full run: `bash bootstrap.sh` (secret scan, repo checks, locked restore, licences, build with warnings as errors, test services, tests without Live, format check, web smoke).
- Requirement map: AG-66-1, -2, -3, -4 -> `rebrand.py dry-run/apply/verify` + probes (U1); AG-66-5 -> commit message and content check (U1); AG-66-6 -> locked restore, build, format, `GuardTests` (`tests/Autonogrammer.Api.IntegrationTests/GuardTests.cs`), `OpenApiCoverageTests` (`tests/Autonogrammer.Web.Tests/OpenApiCoverageTests.cs`); AG-66-7 -> new tests in `Autonogrammer.Harness.Workspace.Tests`, `Harness.Mcp.Tests`, `Harness.Remote.Tests`, `Native.Tests`; AG-66-8 -> new tests in `Harness.Workspace.Tests`, `Harness.AgentLoop.Tests`, `Harness.Mcp.Tests`, `Harness.Plugins.Tests`, `Api.IntegrationTests`; AG-66-9 -> `Harness.OmpConfig.Tests` (next to `OmpModelsFileTests`, `OmpModelsEditorTests`); AG-66-10 -> architecture test in `tests/Autonogrammer.ArchitectureTests` (OQ-491); AG-66-11 -> `DecisionRecordTests`, `bash scripts/ci/check-docs.sh`, `check-changelog.sh`; AG-66-12 -> throwaway-container run plus grep; AG-66-13 -> Xvfb launch or Avalonia.Headless `MainWindow.Title`; AG-66-14 -> owner-action list review.
- Wave 0 gaps (do not exist yet): the fake-home fixture and tests for the data-directory resolver (move, already migrated, both present, unreadable legacy, concurrent first start); per-file-kind legacy discovery tests including `permissions.json` and `sandbox.json` enforced when legacy-only; `models.yml` fixtures with legacy only, new only, mixed ids, same-id duplicate, and byte-preservation; the old-name literal guard; the `LegacyBrandNames.cs` files; an Avalonia.Headless title assertion if Xvfb is not used.
