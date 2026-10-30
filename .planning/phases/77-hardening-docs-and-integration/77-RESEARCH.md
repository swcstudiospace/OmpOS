# Phase 77: Hardening, docs and integration - Research
Source: docs/plans/v0.16.0-research/briefs/77-hardening-and-integration.md

Summarises research report 09 (project recipe, gate and `VersionPrefix` guard, sections 2, 5, 7 and D2), the brief, ADR-0037 D2 and D14, the index and the rebrand runbook; ADR-0030 to ADR-0036 were skimmed by decision headings only. Reports 01 to 08 are not used. Facts were read at HEAD `4af7d834`; nothing was built or run.

## Standard Stack / What to reuse
- Version source: `Directory.Build.props:2` (comment: single version source for the release workflow and CHANGELOG), `:8` (`VersionPrefix` 0.15.0), `:38` (`RestoreLockedMode`); container default `deploy/api.Containerfile:102` (`ARG AUTONOGRAMMER_VERSION=0.15.0`), label use `:108`.
- Changelog gate: `scripts/ci/check-changelog.sh:3-13` (title, `[Unreleased]`, `## [x.y.z] - YYYY-MM-DD`, descending versions, a link reference per version and for Unreleased, `VersionPrefix` needs a matching section or is newer with Unreleased non-empty). `CHANGELOG.md:9` (`[Unreleased]`), `:17` (`## [0.15.0] - 2026-10-28`), `:267-268` (link references to compare URLs).
- Docs gate: `scripts/ci/check-docs.sh:1-18` (runs `scripts/docs/check_gitbook.py`, valid `docs/api/openapi.json`, `AUTONOGRAMMER_RELEASE=1` makes screenshots required); `check_gitbook.py:12-13` (no orphan, one H1, `Status:` within 15 lines `:60`, `:204`; exempt pages `:36-44`).
- Parity model: `docs/quality/cursor-parity-audit-2026-10-28.md` (status vocabulary `:32-41`, summary counts `:45-49`); `docs/parity/session-chat-parity.md` exists; `docs/parity/cowork-parity.md` does not (75-06 creates it).
- Architecture tests: `tests/Autonogrammer.ArchitectureTests/{RepoHygieneTests,DecisionRecordTests,WebShellAccessibilityTests,HeadMotionTests,GateParityTests}.cs`; `RepoHygieneTests.cs:25` uses `ProjectGraphChecker.FindRepoRoot()` to read repo files (the pattern for `CoworkAuditTests`); `tests/Autonogrammer.Native.Tests/ThemeContrastTests.cs`.
- PR template with the DCO checkbox: `.github/PULL_REQUEST_TEMPLATE.md:26`. Release workflow reads the CHANGELOG section on a `v*` tag: `.github/workflows/release.yml:1-9`.

## Facts the planner needs
- `git grep -l '\[0.15.0, )' -- '*packages.lock.json'` finds 52 lock files that embed the project version (observed); `find` counts 85 lock files at HEAD; the relock regenerates all of them (`09-project-recipe-and-conventions.md:26,49`), and the 40 scaffold projects add more.
- `VersionPrefix` bump consequences: relock, `0.15.0` default in `deploy/api.Containerfile:102`, a CHANGELOG `[0.16.0]` section (`09:30,49`); only `api.Containerfile` has a version `ARG` on disk (`grep VERSION` in `deploy/worker.Containerfile` finds only `BUN_VERSION` and `OMP_VERSION`), contrary to AG-77-1 which names both.
- Other `0.15.0` strings are prose only: `scripts/docs/check_gitbook.py:23`, `scripts/legal/generate-third-party-notices.py:403`, `.github/ISSUE_TEMPLATE/bug_report.yml:52` (no change needed).
- `ContractVersion.Current` is `"1.5.0"` at HEAD (`src/Autonogrammer.Contracts/ContractVersion.cs:9`); the scaffold bumps it, phase 77 only asserts no later commit touches it (AG-77-4).
- `.env.example` holds 0 lines of `Dispatch__`, `Accounts__`, `DataPlane__`, `Cowork__` today (grep); the AG-77-6 test depends on rows added by phases 67 to 76.
- No new runbook or audit page of this phase exists: `docs/runbooks/` has no `accounts.md`, `data-plane.md`, `cowork.md`, `dispatch.md`, `browser-use.md`, `computer-use.md`, `v0.16.0-owner-actions.md`; `docs/quality/` has no `v0.16.0-*`; `scripts/docs/` holds `check_gitbook.py` and `generate-providers-md.py` only (no `parity_counts.py`).
- Parity rows: ADR-0037 D2 lists C1 to C31 and K01 to K20 with owner units and targets (`docs/decisions/ADR-0037-cowork-parity-remainder.md` D2); D14 sets the format and the 75-06 and 76-06 ownership (D14 at line 144). Covered rows carry target "n/a", which is outside the six-word vocabulary of AG-77-8.
- Owner-action source table form: ADR-0030 D1 (O1, O3 to O7: action and place, needed for, what the product does when not done).
- Open questions: OQ-480 to OQ-485 at `docs/open-questions.md:314-319`; OQ-400 at `:269` (EPL-2.0 `elkjs` inside `mermaid.min.js`); OQ-362 `:238`; OQ-343 `:228`; OQ-420 `:281`; OQ-430 `:291`; OQ-168 `:205`; OQ-73 `:110`.
- Greptile: no configuration file was found in the tree (`ls -a`, `.github`), consistent with brief U8.
- CI: runs steps 5 to 11 of the gate on self-hosted runners with `CI=true` and a vulnerable-package audit (`09:86`); an Android job restores and builds the Android head separately (`.github/workflows/ci.yml:157`).

## Architecture Patterns
- Audit guard as a test, not a reviewer: parse the parity tables, compare counts with `parity_counts.py --check`, and prove each failure with a bad fixture file (brief 77 AG-77-9).
- Counts are computed by a script from the tables, not typed (`docs/quality/cursor-parity-audit-2026-10-28.md:26,45`); no counts script is in the tree (`scripts/docs/` holds `check_gitbook.py` and `generate-providers-md.py`), so `scripts/docs/parity_counts.py` is new.
- One version commit last, relock once; replay over merge for the other line (runbook s.3; report 09 D2 default "bump once, at the end").
- Evidence page per gate run: `docs/runbooks/evidence/<name>-<date>/README.md` with command, date, commit, counts (as `desktop-app-2026-10-28`).

## Pitfalls
- A bump commit that carries anything but version files and locks breaks criterion 3 (ROADMAP:1506); never touch a lock in a later commit (AG-77-2).
- A lock file that restores only without `--locked-mode` means the relock was incomplete: repeat U1 on clean `obj/`, `CI` unset (brief 77 Failure states; `09:26`).
- New pages not listed in `docs/SUMMARY.md` fail the orphan check; pages without a `Status:` line within 15 lines fail (`check_gitbook.py:204`).
- The audit test must handle a bracketed qualifier after the status word as the Cursor audit does (`cursor-parity-audit-2026-10-28.md:43`).
- A MET row names a test class only by what `grep` finds under `tests/`; renamed classes silently demote a row (brief 77 Failure states).
- Merging locks, OpenAPI or notices by hand during U7; regenerate them (brief 77 U7; runbook s.4).
- `NuGetAuditMode all` with warnings-as-errors fails the build on a new advisory (`Directory.Build.props:30-31`), possibly surfacing during the final relock.
- Documentation claiming a feature works that only has fakes behind it (brief 77 U2); a default-off flag is not a mitigation for a High finding (brief 77 Failure states).

## Verification commands
- `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*CoworkAudit*"` (brief 77 Success criteria).
- `python3 scripts/docs/parity_counts.py --check`; `bash scripts/ci/check-docs.sh`; `bash scripts/ci/check-changelog.sh`; `python3 scripts/docs/check_gitbook.py .`.
- `dotnet restore Autonogrammer.Linux.slnf --locked-mode` after the bump; relock: `dotnet restore Autonogrammer.Linux.slnf --force-evaluate -p:RestoreLockedMode=false`; `bash scripts/ci/check-licences.sh`.
- `python3 scripts/rebrand/rebrand.py verify --baseline <dry-run json>` and the three probes of runbook s.2 (U7).
- `./bootstrap.sh` (gate order in `09:73-86`); `dotnet list Autonogrammer.Linux.slnf package --vulnerable --include-transitive` through `scripts/ci/check-vulnerable-packages.py`; `bash scripts/ci/secret-scan.sh`.
- `git log -S'<VersionPrefix>0.16.0' -- Directory.Build.props` finds one commit; `git diff --stat` of it lists version files and locks only (AG-77-1, criterion 3).

## Cannot be verified on this host
Every live run (Supabase publishable key and tokens, Railway deploys paused, a real omp turn: OD-07 not approved, Dragonfly and Postgres behaviour on Railway); Windows and macOS runners and heads (compile only); Android build and a real phone; real screen-reader behaviour (NVDA, VoiceOver, TalkBack, Orca); WebView2 and WKWebView rendering and Firefox or Safari sandbox behaviour; legal admission of the vendored Mermaid file with its EPL-2.0 `elkjs` (OQ-400); a pinned Chrome for Testing (OQ-362); two-replica routing of Dispatch; whether GitHub shows a Greptile review (index row 77; brief 77 "Cannot be verified"). The security review is a read, not a penetration test; the accessibility pass has no assistive technology.

## Validation Architecture
- Frameworks: xunit v3 on Microsoft Testing Platform with Shouldly (`09:66`); Python standard library for `parity_counts.py`; bUnit in `UI.Tests`, Avalonia.Headless in `Native.Tests`; axe-core in `scripts/web-smoke` (brief 77 U5).
- Quick run: `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*CoworkAudit*"` plus `python3 scripts/docs/parity_counts.py --check`.
- Full run: `./bootstrap.sh`, then CI green on the PR.
- Requirement map: AG-77-1, 2 -> `git log -S`, `--locked-mode` restore, bump-commit diff stat; AG-77-3 -> `check-changelog.sh`; AG-77-4 -> `ContractVersion.cs` history and `Contracts.Tests`; AG-77-5 -> `check_gitbook.py` / `check-docs.sh`; AG-77-6, 7 -> `CoworkAuditTests` (variable and project-row coverage); AG-77-8, 9 -> `CoworkAuditTests` with one bad fixture per failure, `parity_counts.py --check`; AG-77-10, 11 -> `CoworkAuditTests` reading the findings table, named regression tests per finding; AG-77-12 -> one integration test per module in `Api.IntegrationTests`; AG-77-13 -> `UI.Tests`, `Native.Tests`, `WebShellAccessibilityTests`, `HeadMotionTests`, `ThemeContrastTests`, axe in web smoke; AG-77-14 -> `./bootstrap.sh` and the evidence page; AG-77-15 -> `verify --baseline`, probes, `RepoHygieneTests`, `DecisionRecordTests`, LayerMap tests, `GuardTests` OpenAPI equality; AG-77-16 -> CI and the PR template; AG-77-17 -> manual review of `v0.16.0-owner-actions.md`.
- Wave 0 gaps (do not exist yet): `scripts/docs/parity_counts.py`; `CoworkAuditTests.cs` and its fixtures (empty cell, bad status, empty Evidence, empty Gap on non-MET, MET without a found class, MET-LIVE without a run path, counts mismatch, missing variable, missing project row, open or accepted High/Critical finding); `docs/parity/cowork-parity.md` (75-06); the six runbooks, owner-actions page, security review, accessibility pass and gate evidence page; the default-configuration boot tests per module; the new accessibility tests for each surface of U5; `.env.example` rows from phases 67 to 76.
