---
phase: 66-rebrand-to-autonogrammer
verified: 2026-11-10T13:00:00Z
status: gaps_found
score: "not recomputed for current tree"
covered_files:
- .planning/phases/66-rebrand-to-autonogrammer/66-07-PLAN.md
- .planning/phases/66-rebrand-to-autonogrammer/66-07-SUMMARY.md
- .planning/phases/66-rebrand-to-autonogrammer/66-OWNER-ACTIONS.md
- .planning/phases/66-rebrand-to-autonogrammer/66-VERIFICATION.md
- ROADMAP.md
- .planning/STATE.md
- scripts/web-smoke/a11y/routes.mjs
- tests/Autonogrammer.Native.Tests/Branding/MainWindowTitleTests.cs
- scripts/rebrand/rebrand.py
- scripts/rebrand/test_rebrand.py
- scripts/ci/check-licences.sh
- scripts/ci/check-brand.sh
- scripts/ci/repo-checks.sh
covered_digest: "v1:sha256:phase66-verif-20261110"
behavior_unverified: 2
overrides_applied: 0
re_verification: true
previous_status: gaps_found
previous_score: "2/5 (approx from prior C1/C3 gaps)"
gaps_closed:
  - "C3: web smoke first attempt (race fixed by wait main h1 + non-boot title in routes.mjs runStep; smoke json passed:true, a11yRoutes:true, failures:0, exit:0)"
  - "C1: rebrand tool (61 tests OK, verify OK clean)"
gaps_remaining:
  - "The checksum-matched historical rebrand baseline no longer matches the current near-match and protected engine counts; the cause has not been established."
  - "The full current-tree Linux, service and web gate has not passed; the last non-Live solution run had failures."
advisory: []
behavior_unverified_items:
  - "Current-tree rebrand baseline count changes require investigation."
  - "Current-tree full service and web gate remains unverified."
coincidental_reliance_items: []
human_verification: []
gaps:
  - truth: "Rebrand verification preserves the approved baseline."
    status: failed
    reason: "The baseline comparison reports changed near-match and engine keep-pattern counts; its cause is unverified."
  - truth: "The final current tree passes the Linux, service and web gate."
    status: unverified
    reason: "The last non-Live solution run failed and the full guarded bootstrap has not run on this tree."
deferred:
  - truth: "External owner actions 1-8 (repo/domain rename, env vars, state/containers, Android id, legal, gitleaks fingerprints post-rename, submodules, brand artwork) remain open per 66-OWNER-ACTIONS.md"
    addressed_in: "owner action (external, per runbook s.5 and plan D-24)"
    evidence: "66-OWNER-ACTIONS.md lists all Status: open with proving commands; ROADMAP goal explicitly notes external renames are owner actions"
---

# Phase 66: Rebrand to Autonogrammer Verification Report

**Current-tree update:** The rebrand unit suite passed 61/61, `rebrand.py verify` without a baseline reported no remaining renames, the desktop headless title test passed 1/1, and the brand and licence checks passed. Verification against the recorded, checksum-matched 2026-11-04 baseline failed: rejected near-match counts and four engine keep-pattern counts differ. The cause is not established; growth or substring damage cannot be inferred from counts alone. These focused checks do not refresh the complete Phase 66 gate: the last non-Live solution run had failures, the web and service gate has not been rerun on the current tree, and the recorded full-gate safety holds remain. The historical `passed` verdict below applies to its recorded earlier tree, not this checkout.

The report comparison found no decreases in the aggregated rejected-window or protected engine counts. Three newly distinct rejected words are classified benign by the tool and their recorded examples are in planning files. Comparing protected URL entries by file, matched text and rule found all 293 baseline entries still represented and two additional entries. These aggregates cannot prove that every original near-match or engine occurrence was preserved, and the baseline comparison still fails.

A fresh dry-run followed immediately by `verify` against that report both exited 0; the verifier reported no remaining renames and 295 protected URL tokens. Planning files changed afterward, so this is a snapshot rather than a final-tree gate. It does not resolve historical baseline provenance or the acceptance conflict.

`LegacyBrandLiteralGuardTests` passed 3/3 in the Release architecture test project; this focused check does not replace the full bootstrap gate.

**Baseline provenance:** The approved report records command `verify` and generation time `2026-11-04T23:35:47Z`; the summary attributes it to `deb1fcdb`, whose recorded commit time is `23:37:54Z`. The keep-marker change could have been uncommitted at scan time, but the exact tracked, index, untracked and tool/config inputs have not been authenticated. The report also contains 1,074 excluded/frozen protected URL occurrences only as an aggregate. Searches of the named baseline backup and available reconciliation records found no contemporaneous complete input manifest; the search does not establish global absence.

**Acceptance decision:** The owner selected strict original-baseline equality rather than the later informational-growth ruling. D-05 and AG-66-3 therefore remain the governing criteria for this work. The baseline comparison still fails, historical scan inputs remain unauthenticated, and the guarded evidence required by `66-09-PLAN.md` has not been created; this decision does not close the phase or authorize the bootstrap gate.

**Phase Goal:** Every name the project owns changes from the former name to Autonogrammer in one mechanical pass (paths, contents, identifiers, containers, schema, headers, Android id), existing users keep their data through three C# migrations, the generated files are regenerated, and the renamed tree is built and gated once. Not delivered: new artwork, the trademark decision, external renames (GitHub, Vercel, Railway, domain), `--urls`, a store identity for Android (runbook section 5 items are owner actions).
**Verified:** 2026-11-10T13:00:00Z (HEAD 919ebed1)
**Status:** gaps_found for the current tree; the earlier pass below is historical.
**Re-verification:** Yes — after C3 gap closure and C1 confirmation; previous gaps_found on C1/C3

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