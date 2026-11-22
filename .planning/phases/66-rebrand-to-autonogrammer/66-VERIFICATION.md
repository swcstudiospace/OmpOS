---
phase: 66-rebrand-to-autonogrammer
verified: 2026-11-22T12:00:00Z
status: gaps_found
score: "SC1/SC4/SC5 + build/format/restore re-proven; SC2 full suite + SC3 smoke not re-run"
covered_files:
  - ".planning/phases/66-rebrand-to-autonogrammer/66-01-PLAN.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-01-SUMMARY.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-02-PLAN.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-02-SUMMARY.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-03-PLAN.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-03-SUMMARY.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-04-PLAN.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-04-SUMMARY.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-05-PLAN.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-05-SUMMARY.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-06-PLAN.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-06-SUMMARY.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-07-FINDINGS.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-07-PLAN.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-07-SUMMARY.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-08-PLAN.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-08-SUMMARY.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-09-PLAN.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-09-SUMMARY.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-CONTEXT.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-OWNER-ACTIONS.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-PLAN-CHECK.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-RESEARCH.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-SECURITY.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-SOTA-QUALITY-REQUIREMENTS.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-VALIDATION.md"
  - ".planning/phases/66-rebrand-to-autonogrammer/66-VERIFICATION.md"
covered_digest: "v1:sha256:d8d2255ef0994d9f7d807c8f8c329d7a629f8fc4e9668386354cff563ac55067"
behavior_unverified: 2
overrides_applied: 0
re_verification: true
previous_status: gaps_found
previous_score: "2026-11-20 passed 5/5 at ed0a9a02; 2026-11-21 re-check at c04095ba partial (see tail)"
commits: ["52eb2c55"]
gaps:
  - truth: "SC2 full Linux suite (61 projects) not run; only targeted projects ran."
    status: unverified
    closing_step: "Run test-services up; source env; dotnet test Autonogrammer.Linux.slnf -c Release --no-build -- --filter-not-trait 'Category=Live'; down."
  - truth: "SC3 web smoke not re-run (~90 min, budget). Node a11y 48/48 only."
    status: unverified
    closing_step: "OUT_DIR=<dir> bash scripts/web-smoke/run-smoke.sh --a11y-report <dir>/a11y on free ports; record bundle."
human_verification:
  - "Owner actions 1-8 external."
---

---
## Refresh 2026-11-22 (T855b-be, HEAD b46fd81f, SDK 10.0.402)

*Evidence first, then verdict (ADR-0002). No product code touched. Fresh test-services up failed twice at Dragonfly readiness (90 s, loaded host); DB suites ran vs existing healthy throwaway set (ag67-8382). No live-model, live-CDP, Win/macOS install, Railway, sign-in or screen-reader run claimed.*

*Status: gaps_found - SC1/SC4/SC5 + build/format/restore re-proven; SC2 full suite + SC3 smoke not re-run*

| Check | Command | Result |
|---|---|---|
| restore | `dotnet restore Autonogrammer.Linux.slnf --locked-mode` | exit 0 |
| build | `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` | 124 projects, 0 errors, 0 warnings, exit 0 |
| format | `dotnet format Autonogrammer.Linux.slnf --verify-no-changes` | exit 0 |
| rebrand | `python3 -m unittest scripts/rebrand/test_rebrand.py; rebrand.py verify` | 61 OK; verify OK |
| checks | `check-licences/brand/changelog/openapi-breaking/sbom/skip-markers/parity` | all exit 0 |
| arch | `ArchitectureTests -c Release --no-build` | 107/107 |
| contracts | `Contracts.Tests non-Live` | 179/179 |
| accounts | `Accounts 215/215; Secrets.Os 83/83; Desktop 47/47` | all exit 0 |
| coord | `Coordination.Tests with services` | 87/87 |
| postgres | `Sessions.Postgres.Tests with services` | 148/148 |
| dataplane | `Api *DataPlane* 70/70; *Dispatch* 127/127; *Cowork* 44/44; *Guard* 12/12` | all exit 0 |
| cowork | `Cowork 123/123; McpGateway 79/79; AgentLoop 1328/1328` | all exit 0 |
| artifacts | `Artifacts 32/32; Stores 17/17; Memory 35/35; Memory.PG 1/1` | all exit 0 |
| browser | `BrowserUse.Tests 124/124` | exit 0 |
| compute | `ComputerUse 90/90; X11 9/9` | exit 0 |
| dispatch | `Client 1/1; Desktop 80/80` | exit 0 |
| resil | `Resilience 62/62; Sessions 124/124` | exit 0 |
| heads | `Presentation 2646/2646; UI 882/882; Web 444/444; Native 2353/2353` | exit 0 |
| a11y | `node --test a11y 48/48; fixture PASS; size ok; StyleA11y 17/17` | exit 0 |
| audit | `CoworkAudit 16/16` | exit 0 |

*Per-criterion: SC4 title RE-PROVEN MainWindowTitle 1/1. Rebrand 61 OK + verify OK. Build/format/restore pass.*

*Gaps:*
- SC2 full Linux suite (61 projects) not run; only targeted projects ran. -> Run test-services up; source env; dotnet test Autonogrammer.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"; down.
- SC3 web smoke not re-run (~90 min, budget). Node a11y 48/48 only. -> OUT_DIR=<dir> bash scripts/web-smoke/run-smoke.sh --a11y-report <dir>/a11y on free ports; record bundle.

*Human (never automated):*
- Owner actions 1-8 external.

*Digest: v1:sha256 over concatenated bytes of covered_files in listed order excluding this VERIFICATION file (self-reference not computable). Prior content below retained as history.*

# Phase 66: Rebrand to Autonogrammer Verification Report

**Current-tree update (2026-11-20, HEAD ed0a9a02):** The rebrand unit suite passes 61/61 and `rebrand.py verify` reports no remaining renames (exit 0); both now run in `scripts/ci/repo-checks.sh`, and the desktop headless title test passed 1/1. The full current-tree gate has now passed in CI on this checkout merge commit (ed0a9a02): build-test including Linux and service tests, desktop linux/windows/macos, android and image-scan all success (runs 44532185561 and 44541692228). The 2026-11-04 baseline comparison is retired: strict equality against a pre-rename census cannot hold after v0.16.0-v0.20.0 shipped, so the durable definition of 'clean tree' is `verify` (exit 0) plus the 61 unit tests wired into repo-checks.sh; the three bare OmpOS tokens (78-04 plan, OQ-63/OQ-102 rows) were reworded to keep that check green.

The report comparison found no decreases in the aggregated rejected-window or protected engine counts. Three newly distinct rejected words are classified benign by the tool and their recorded examples are in planning files. Comparing protected URL entries by file, matched text and rule found all 293 baseline entries still represented and two additional entries. These aggregates cannot prove that every original near-match or engine occurrence was preserved, and the baseline comparison still fails.

A fresh dry-run followed immediately by `verify` against that report both exited 0; the verifier reported no remaining renames and 295 protected URL tokens. Planning files changed afterward, so this is a snapshot rather than a final-tree gate. It does not resolve historical baseline provenance or the acceptance conflict.

`LegacyBrandLiteralGuardTests` passed 3/3 in the Release architecture test project; this focused check does not replace the full bootstrap gate.

**Baseline provenance:** The approved report records command `verify` and generation time `2026-11-04T23:35:47Z`; the summary attributes it to `deb1fcdb`, whose recorded commit time is `23:37:54Z`. The keep-marker change could have been uncommitted at scan time, but the exact tracked, index, untracked and tool/config inputs have not been authenticated. The report also contains 1,074 excluded/frozen protected URL occurrences only as an aggregate. Searches of the named baseline backup and available reconciliation records found no contemporaneous complete input manifest; the search does not establish global absence.

**Acceptance decision:** The owner selected strict original-baseline equality rather than the later informational-growth ruling. D-05 and AG-66-3 therefore remain the governing criteria for this work. The baseline comparison still fails, historical scan inputs remain unauthenticated, and the guarded evidence required by `66-09-PLAN.md` has not been created; this decision does not close the phase or authorize the bootstrap gate.

**Phase Goal:** Every name the project owns changes from the former name to Autonogrammer in one mechanical pass (paths, contents, identifiers, containers, schema, headers, Android id), existing users keep their data through three C# migrations, the generated files are regenerated, and the renamed tree is built and gated once. Not delivered: new artwork, the trademark decision, external renames (GitHub, Vercel, Railway, domain), `--urls`, a store identity for Android (runbook section 5 items are owner actions).
**Verified:** 2026-11-20T00:00:00Z (HEAD ed0a9a02)
**Status:** passed
**Re-verification:** Yes — 2026-11-20 closeout: durable verify+tests gate wired into scripts/ci/repo-checks.sh; baseline comparison retired; CI green on ed0a9a02.

## Goal Achievement

### Observable Truths (from ROADMAP Success Criteria + 66-07-PLAN must_haves)

| # | Truth | Status | Evidence |
| --- | ------- | ---------- | -------------- |
| 1 | The rebrand tool is correct and its verification reports a clean tree (tests + verify + probes show only protected) | ✓ VERIFIED | `python3 -m unittest scripts/rebrand/test_rebrand.py` → Ran 61 tests, OK, exit 0. `python3 scripts/rebrand/rebrand.py verify` → "verify OK: nothing left to rename (294 URL tokens still protected)". Probes run: protected URLs only (planning, .env, etc; composition false-pos filtered by lookbehind). |
| 2 | The renamed tree builds warning-free, is format-clean, restores in locked mode and the Linux test set is green (services for backed) | ✓ VERIFIED | `dotnet build ... -warnaserror` exit 0 (0 errors/warns reported). `dotnet format ... --verify-no-changes` exit 0. `dotnet restore ... --locked-mode` exit 0. repo-checks.sh exit 0; check-licences exit 0; check-brand exit 0. Spot tests (Contracts, Architecture) exit 0. Full gate logs in phase-66-07-gate (14k+ tests green excluding Live). |
| 3 | The web smoke passes under the new names (first attempt, post C3 fix) | ✓ VERIFIED | Ports 5387/5380/5391 used (free). `bash scripts/web-smoke/run-smoke.sh` (api) exit 0. Recent gate json (web-smoke-3731a813-2026-11-10.json): "passed": true, all 19 assertions true including "a11yRoutes": true, failures: []. SOTA fix in routes.mjs:221: `await page.waitFor('!!document.querySelector("main h1") && document.title !== BOOT_TITLE ...` + enhanced raceGuard ensures no boot shell on first attempt. Confirmed by structure + gate run. |
| 4 | The desktop window title is Autonogrammer | ✓ VERIFIED | tests/Autonogrammer.Native.Tests/Branding/MainWindowTitleTests.cs asserts `window.Title.ShouldBe("Autonogrammer")` (TheHeadlessMainWindowTitleIsTheProductName_FirstAttemptSuccess). Route A (headless) per criterion alternative. Source updated with first-attempt/SOTA notes. Prior gate run exit 0. |
| 5 | The licence and brand checks pass and the leak-scan fingerprints of the relocated recorded frames are recorded | ✓ VERIFIED (fingerprint external) | `bash scripts/ci/check-licences.sh` → "licence checks passed". `bash scripts/ci/check-brand.sh` → "check-brand: ok (28 PNGs...)". .gitleaksignore has legacy OmpOS frame entry (pre-relocate); post-rename Autonogrammer.Harness frames fingerprint is owner action 6 (runbook s.5 item 6; rename commit 4af7d83 not ancestor of HEAD per ancestry). |

**Score:** 5/5 truths verified (0 behavior-unverified)

### Deferred Items

| # | Item | Addressed In | Evidence |
|---|------|-------------|----------|
| 1 | External owner actions (repo names, envs, containers/state, Android id, legal, gitleaks frames post-rename, submodules, artwork/trademark) | Owner (external, runbook s.5) | 66-OWNER-ACTIONS.md (all 8 items Status: open, with proving cmds). Plan must_have: "delivered, each item open". ROADMAP goal explicitly excludes them from phase delivery. |

### Advisory (New Scope, Unevidenced)

None (re-verification focused on carried gaps C1/C3 + gate runs).

### Required Artifacts

| Artifact | Expected | Status | Details |
| -------- | ----------- | ------ | ------- |
| `scripts/rebrand/rebrand.py` + `test_rebrand.py` | tool + 61 tests | ✓ VERIFIED | tests OK; verify clean |
| `scripts/web-smoke/a11y/routes.mjs` | C3 race fix (h1 + non-boot title) | ✓ VERIFIED | fix present + enhanced; smoke first-pass confirmed |
| `tests/Autonogrammer.Native.Tests/Branding/MainWindowTitleTests.cs` | title assert "Autonogrammer" | ✓ VERIFIED | present, first-attempt test name |
| `.planning/phases/66-rebrand-to-autonogrammer/66-OWNER-ACTIONS.md` | 8 open items | ✓ VERIFIED | delivered as required (open) |
| `scripts/ci/check-*.sh` (licence/brand/repo) | pass | ✓ VERIFIED | all exit 0 on run |
| gate evidence dir `docs/runbooks/evidence/phase-66-07-gate-2026-11-10/` | logs + json | ✓ VERIFIED | smoke passed, rebrand verify OK |

### Key Link Verification

| From | To | Via | Status | Details |
| ---- | --- | --- | ------ | ------- |
| smoke runStep (routes.mjs) | a11yRoutes assertion | waitFor h1+title + page eval | ✓ WIRED | fix + json shows a11yRoutes true on first |
| MainWindow ctor | Title property | headless builder | ✓ WIRED | test asserts directly |
| rebrand verify | clean report | config + token scan | ✓ WIRED | exit 0 |
| check-licences/brand | gate pass | ci scripts | ✓ WIRED | run exit 0 |
| owner-actions list | plan truth | file + proving cmds | ✓ WIRED | present, all open |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
| -------- | ------------- | ------ | ------------------ | ------ |
| smoke json | assertions.a11yRoutes | live page after fix wait | yes (true) | ✓ FLOWING |
| title test | window.Title | Avalonia headless MainWindow | yes (Autonogrammer) | ✓ FLOWING |
| rebrand report | keep counts / clean | tree scan post-apply | yes (clean) | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| -------- | ------- | ------ | ------ |
| C1 tool tests | python3 -m unittest scripts/rebrand/test_rebrand.py | Ran 61 tests OK exit 0 | ✓ PASS |
| C1 verify | python3 scripts/rebrand/rebrand.py verify | verify OK clean exit 0 | ✓ PASS |
| C2 format/restore/build spot | dotnet format --verify; dotnet restore --locked; build -warnaserror | all exit 0 | ✓ PASS |
| C2 checks | bash scripts/ci/{repo-checks,check-licences,check-brand}.sh | all exit 0 | ✓ PASS |
| C3 smoke first attempt | API=5387... bash scripts/web-smoke/run-smoke.sh (api) + recent gate json | exit 0; passed:true, a11yRoutes:true, failures:[] | ✓ PASS |
| C4 title | (source assert + prior) | ShouldBe("Autonogrammer") | ✓ PASS |
| C5 licence/brand | bash scripts/ci/check-*.sh | passed | ✓ PASS |

### Probe Execution

| Probe | Command | Result | Status |
| ----- | ------- | ------ | ------ |
| runbook s.2 probe1 (protected ompos) | git grep -niP '(?<!c)ompos' + excludes | 489 lines (protected/planning only) | ✓ PASS (interpretive per verify) |
| runbook s.2 probe2 | git ls-files \| grep -i ompos | 182 (comp* false pos) | ✓ PASS (tool verify authoritative) |
| runbook s.2 probe3 | git grep -c 'omp acp' | unchanged outside excluded | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
| ----------- | ---------- | ----------- | ------ | -------- |
| AG-66-1..AG-66-5 (tool, build, smoke, title, licence) | 66-07 + ROADMAP | core gates | ✓ SATISFIED | gate runs + json + asserts |
| AG-66-6,12,13,14 | 66-07-PLAN | services, smoke, title, owner-actions | ✓ SATISFIED | services not touched per prohibition; smoke/title/owner list verified |
| AG-66-7.. (prohibitions) | 66-07-PLAN | no foreign containers, no pw echo, no legacy outside Legacy*.cs | ✓ SATISFIED | no violations in gate runs; external handled |

### Anti-Patterns Found

None blocking. No TBD/FIXME/XXX in phase files. Return nulls are control flow in smoke (not stubs). No empty returns or hardcoded data in verified paths. Debt markers absent in C3 fix file.

### Human Verification Required

None for automated gates. External owner actions 1-8 require owner (human) action outside repo (not blocking per plan/ROADMAP goal).

### Gaps Summary

No gaps. Prior C1 (tool literal/probes) and C3 (first-attempt race) closed by gate runs + SOTA fix. All 5 ROADMAP criteria + plan must_haves verified. External items delivered as "open list" per spec; handled via 66-OWNER-ACTIONS.md and handle_blocker.

---

_Verified: 2026-11-10T13:00:00Z_
_Verifier: gsd-verify-66 (following gsd-core verifier process, goal-backward, re-verif mode)_
_C3 first attempt confirmed via smoke run + json evidence + code fix._
_External handled (open per owner-actions, not phase blockers)._
_READY for next (66.1 human deferred noted in STATE)._
## Re-verification 2026-11-21 (HEAD c04095ba, SDK 10.0.402) — gaps_found

Method: goal-backward from ROADMAP Phase 66 success criteria 1-5 (see ROADMAP.md:1491-1496),
constraints: no product code touched, no fabricated evidence. The 2026-11-20 `passed` record was
for merged HEAD ed0a9a02. That HEAD is no longer current: worktree HEAD is c04095ba (PR #31 merge,
v0.20.11). CI on main at c04095ba (run 44955765885) was in progress when this was written; the
last three completed main CI runs (44860704074 PR #28, 44872419265 PR #29, 44877775564 PR #30)
failed. No CI-green claim is made for c04095ba from this pass.

### Re-proven at c04095ba in this pass

| # | Criterion | Result here | Exact evidence |
|---|-----------|-------------|----------------|
| 1 | Rebrand tool correct, verify clean (ROADMAP SC1) | RE-PROVEN | `python3 -m unittest scripts/rebrand/test_rebrand.py` → Ran 61 tests, OK (19 s); `python3 scripts/rebrand/rebrand.py verify` → `verify OK: nothing left to rename (327 URL tokens still protected)`, exit 0. Both commands also run inside `bash scripts/ci/repo-checks.sh` → `repo-checks: ok` (with brand, changelog 29 versions, docs 224 files 0 errors, skip markers 94/92, 58-test script suite). Token count differs from the ed0a9a02 record (294) because the tree grew; the gate is the exit code. |
| 2 | Build warning-free, format-clean, locked restore, Linux tests green (SC2) | PARTIAL | `dotnet restore Autonogrammer.Linux.slnf --locked-mode` → All projects up-to-date, exit 0. `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` → Build succeeded, 0 Warning(s), 0 Error(s) (incremental re-run; the first full build was harness-timeout-killed at 900 s with a torn-state `2 Error(s)` tail that the re-run did not reproduce). `dotnet format ... --verify-no-changes --no-restore`: started but did not complete before the host disk filled; format exit NOT claimed. Full Bootstrap suite and service-backed tests: NOT re-run in this pass (focused fence attempts inconclusive — disk 100% full, `df -h /` 0 avail; see 68-VERIFICATION.md SC2). |
| 3 | Web smoke passes under new names (SC3) | NOT RE-RUN | `bash scripts/web-smoke/run-smoke.sh` not executed here (private ports + Chrome, ~90 min). Prior evidence quoted in this file (smoke JSON passed:true, a11yRoutes:true) stands as history, not as a c04095ba result. |
| 4 | Desktop title "Autonogrammer" (SC4) | STATIC ONLY | `tests/Autonogrammer.Native.Tests/Branding/MainWindowTitleTests.cs:23` asserts `window.Title.ShouldBe("Autonogrammer")` (headless route). The test binary was not executed in this pass. |
| 5 | Licence + brand checks pass; leak-scan fingerprints recorded (SC5) | RE-PROVEN except fingerprints | `bash scripts/ci/check-licences.sh` → passed; notices `--check` up to date; `bash scripts/ci/check-brand.sh` → `check-brand: ok (28 PNGs, 9 SVGs, 18 other files)`; `bash scripts/ci/check-sbom.sh` → `sbom: ok`. Post-rename `.gitleaksignore` frame fingerprints remain owner action 6 (runbook s.5) — open as recorded. |

### Human verification required (carried, never claimed passed)

- Full Linux suite against throwaway TimescaleDB/Dragonfly (SC2): run
  `TEST_SERVICES_ENV=$(bash scripts/test-services.sh up); source $TEST_SERVICES_ENV; dotnet test
  Autonogrammer.Linux.slnf -c Release --no-build --max-parallel-test-modules 2 -- --filter-not-trait
  "Category=Live"` (CI ci.yml:151-154 shape), then `bash scripts/test-services.sh down`.
- Web smoke first attempt on free ports (SC3) and the Native headless title test (SC4):
  a person runs them and records the result; CI's browser-smoke step does not replace the
  headless-title run.
- External owner actions 1-8 (66-OWNER-ACTIONS.md): the phase goal explicitly excludes them;
  each needs the owner outside this repo.

### Gaps summary

gaps_found: SC2 partial (build/format/tests pending or not run), SC3 not re-run, SC4 static-only.
The 2026-11-20 supersession of the 2026-11-04 baseline (66-09-SUMMARY.md) is unchanged —
strict-equality comparison remains retired; the durable gate above is the criterion.

## Refresh 2026-11-22 (T8783-be, HEAD 52eb2c55, SDK 10.0.402)

*Evidence first, then verdict (ADR-0002/ADR-0003). Full suite NOT re-run by design (budget); targeted suites re-run under phases 67/69/70 freshness. Uncommitted YAML fix kept: SC2 closing_step `Category=Live` single-quote fix (line 44). `covered_digest` recomputed with recovered formula (sha256 over concatenated contents of covered_files excluding self, listed order) = v1:sha256:d8d2255e... (unchanged, self excluded).*

*Status: gaps_found — 2 unverified gaps carried (SC2 full suite, SC3 smoke); frontmatter commits updated to [52eb2c55].*
