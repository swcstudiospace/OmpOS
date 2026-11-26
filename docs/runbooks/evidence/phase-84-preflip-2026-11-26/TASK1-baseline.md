# Phase 84 pre-flip baseline — Task 1: executable gate subset (report text; no changes)

Commit: fdafc70b1b418793814d4d3984a51d75ee63d532
`git status --porcelain` before and after: only untracked `.bg-shell/`, `.gsd/`, `.mcp.json` (pre-existing, not mine), the evidence dir itself, and one peer-owned untracked test file (`tests/Autonogrammer.Sessions.Postgres.Tests/Dispatch/DispatchLegacyMigrationRoundTripTests.cs`, appeared during the run; not created by this task). No tracked working-tree file was modified.
Date: 2026-11-26
SDK: dotnet 10.0.402 at `$HOME/.dotnet` (global.json pins `10.0.402`).
Linear: SPE-37896 chain (https://linear.app/swcstudio/issue/SPE-37896/phase-84-sota-quality-and-cutover-gate); SC1 SPE-37897; SC2 SPE-37898; SC3 SPE-37899; Project P-SPE-2.
Notion payload root: 700bc1a0-c7ae-812c-abfb-00035fd38be2

## Commands run and exit codes (verbatim, logs in `logs/`)

| # | Command | Exit | Log |
|---|---|---|---|
| 1 | `bash scripts/ci/check-brand.sh` | 0 | `logs/01-check-brand.log` ("check-brand: ok (28 PNGs, 9 SVGs, 18 other files)") |
| 2 | `bash scripts/ci/check-changelog.sh` | 0 | `logs/02-check-changelog.log` ("CHANGELOG.md is valid (36 versions)") |
| 3 | `bash scripts/ci/check-docs.sh` | **1** | `logs/03-check-docs.log` — orphan pages: `docs/decisions/ADR-0039-cowork-artifact-grant-proof.md`, `docs/quality/71-g1-scope-2026-11-25.md`, `docs/quality/74-g1-scope-2026-11-25.md`; "249 markdown files, 142 listed in SUMMARY.md, 3 errors, 0 warnings" |
| 4 | `bash scripts/ci/check-providers-doc.sh` | 0 | `logs/04-check-providers-doc.log` |
| 5 | `bash scripts/ci/check-skip-markers.sh` | **1** | `logs/05-check-skip-markers.log` — 3 unlisted markers + 7 orphan kept rows (full text in TASK2) |
| 6 | `bash scripts/check-settings-parity.sh` | 0 | `logs/06-check-settings-parity.log` (empty, pass) |
| 7 | `bash scripts/ci/check-release-version.sh` | 0 | `logs/07-check-release-version.log` (VERSION=0.20.18) |
| 8 | `bash scripts/ci/check-version-bump.sh` (no args) | 2 | `logs/08-check-version-bump.log` ("usage: check-version-bump.sh --base <git-ref>") — invocation error, not a gate failure |
| 8b | `bash scripts/ci/check-version-bump.sh --base HEAD` | 1 | `logs/08b-check-version-bump-base-HEAD.log` — "VersionPrefix must rise above the base. Base 0.20.18, head 0.20.18." Expected on a no-version-bump commit; only required on a release commit. Reported as actual state, not waived. |
| 9 | `bash scripts/ci/check-sbom.sh` | **1** | `logs/09-check-sbom.log` — "docs/legal/sbom.spdx.json is stale; run python3 scripts/legal/generate-sbom.py" |
| 10 | `python3 -m unittest scripts/rebrand/test_rebrand.py` | 0 | `logs/10-rebrand-unittest.log` (61 tests OK) |
| 11 | `python3 scripts/rebrand/rebrand.py verify --root .` | 0 | `logs/11-rebrand-verify.log` |
| 12 | `python3 scripts/release/test_verify_desktop_assets.py` | 0 | `logs/12-verify-desktop-assets.log` (58 tests OK) |
| 13 | `dotnet restore Autonogrammer.Linux.slnf --locked-mode` | 0 | `logs/13-dotnet-restore-locked.log` ("All projects are up-to-date for restore") |
| 14 | `bash scripts/ci/check-licences.sh` | 0 | `logs/14-check-licences.log` ("THIRD-PARTY-NOTICES.md is up to date; licence checks passed") |
| 15 | `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` | 0 | `logs/15-dotnet-build-warnaserror.log` ("Build succeeded. 0 Warning(s) 0 Error(s)", 3m29s) |
| 16 | `dotnet test tests/Autonogrammer.ArchitectureTests -c Release --no-build -- --filter-not-trait "Category=Live"` | pending at write time (log `logs/16-arch-tests.log`; verdict appended below) | |

Baseline verdict for Task 5's flip precondition: **the executable subset is NOT fully green** — 3 red gates (check-docs: 3 orphan pages; check-skip-markers: 3 unlisted + 7 orphan rows; check-sbom stale) plus one expected version-bump refusal. The `-warnaserror` build, the locked restore, the licence check, and all other check-*.sh / rebrand / providers / settings-parity / changelog / brand gates are green at `fdafc70b` with ACP as the default. Per the plan's must-have ("if any gate fails at the cutover commit, the default does not flip"), Task 5 cannot flip until these are resolved or the owner explicitly accepts them in Task 4 — this run does not waive them.

## Not run here (non-executable in this assignment)

- `bash scripts/ci/secret-scan.sh` (gitleaks) — `AUTONOGRAMMER_SKIP_SECRET_SCAN=1` NOT set in this run; the scan itself is a CI/live-network step outside the allowed subset. Exact command: `bash scripts/ci/secret-scan.sh` (plus the milestone-diff scan per plan step 10).
- `bash scripts/ci/plugin-node-tests.sh` (gsd-core-omp node:test suite) — submodules not initialized for network fetch; exact command: `bash scripts/ci/plugin-node-tests.sh`.
- `./bootstrap.sh` (full sequence) — includes live test services (`scripts/test-services.sh up/down`), full `dotnet test` over the solution, `dotnet format` verify, and web smoke with axe; per assignment: no formatters, no live services. Exact commands: `bash scripts/test-services.sh up`; `dotnet test Autonogrammer.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"`; `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore`; `OUT_DIR=<tmp> bash scripts/web-smoke/run-smoke.sh`.
- `python3 scripts/ci/check-openapi-breaking.py --base <milestone-base-ref>` — exact command as in the plan.
- Human/owner checks (Task 4 checkpoint): realm OQ-528 call, ADR-0039 Gate A/B calls, SEC-77-01 carry-open — owner-gated, not executable here.
- Live model turn (`AUTONOGRAMMER_RPC_LIVE_CAPTURE=1`, `Category=Live`) — prohibited by the plan; never run.
- Full parity suites beyond the ArchitectureTests run above (`tests/Autonogrammer.Harness.ContractTests`, `tests/Autonogrammer.Api.IntegrationTests` on both adapters) — exact commands: `dotnet test tests/Autonogrammer.Harness.ContractTests -c Release --no-build -- --filter-not-trait "Category=Live"` and `dotnet test tests/Autonogrammer.Api.IntegrationTests -c Release --no-build -- --filter-not-trait "Category=Live"`; recorded as the Task 6 requirement, not run here.

## ArchitectureTests verdict (appended after log 16 completes)

| 16 | `dotnet test tests/Autonogrammer.ArchitectureTests -c Release --no-build -- --filter-not-trait "Category=Live"` | **2** | `logs/16-arch-tests.log` — 124 total: 123 passed, 1 failed (`RepoHygieneTests.RepoHygiene_DocsSkeletonExists` — open-question row needs six columns: `OQ-526-83` in `docs/open-questions.md` has five; that row is not owned by this task and `docs/open-questions.md` edits are forbidden in this run), 0 skipped. `SuppressionLedgerTests`, `RpcCompositionArchitectureTests`, and `DecisionRecordTests` passed (not in the failed list). |
