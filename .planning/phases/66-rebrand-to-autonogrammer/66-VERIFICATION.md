# Phase 66 Verification: Rebrand to Autonogrammer

Date: 2026-11-05
Verifier: general agent (Verify66Rebrand-2), run in place of gsd-verifier. gsd-verifier returned 403 (billed to a Muse Code subscription). The procedure is the same: each ROADMAP success criterion checked against evidence read in this run.
Repo: the repository root (branch feat/autonogrammer-integration), HEAD 8d01a160. Not pushed.
Scope of this run: read-only (file reads and read-only git). No builds, tests, formatters, lint, service starts, test-services, bootstrap, or rebrand apply/dry-run/verify were run. Recorded logs were used where the assignment says so. No status was changed. Nothing was staged or committed.

## Verdict

Phase 66 is NOT verified complete. ROADMAP status stays `[ ]`.

- C1 (rebrand tool, clean verify, probes): gap. The clean-tree claim holds against the owner baseline. The literal command in the criterion does not, and two runbook probes do not give the stated result.
- C2 (build, format, restore, Linux test set): met at 8d01a160. Live-tagged tests are excluded (external).
- C3 (web smoke under new names): gap. The gate-of-record commit has one failed smoke attempt and one passing rerun.
- C4 (window title): met (headless route, at 8d01a160; Xvfb route recorded at deb1fcdb).
- C5 (licence and brand checks; leak-scan fingerprints): checks met; fingerprints external (owner action 6).

## Ancestry check (66.1 commits)

`git merge-base --is-ancestor` against HEAD 8d01a160:

| Commit | Subject | Ancestor of HEAD | Branches containing it |
| --- | --- | --- | --- |
| 88c1497f | 66.1 commit | no | feat/autonogrammer, feat/autonogrammer-pre-replay |
| e2760e5e | 66.1 commit | no | feat/autonogrammer, feat/autonogrammer-pre-replay |
| 58bdbe1d | 66.1 commit | no | feat/autonogrammer, feat/autonogrammer-pre-replay |
| 9544bbb2 | 66.1 commit | no | feat/autonogrammer, feat/autonogrammer-pre-replay |
| 4af7d83 | rename(66-01) mechanical pass | no | feat/autonogrammer, feat/autonogrammer-pre-replay |

HEAD's tree contains the renamed paths (for example `apps/Autonogrammer.Native/...` from `git ls-files`), so the renamed content is in HEAD through a different history. [INFERENCE: a replay onto the integration line, per ROADMAP SD-10.] The rename commit `4af7d83` is not an ancestor of HEAD. This matters for C5 (see below).

Commits used as references: 2ab63b2b (rebrand verify tree, 19:53:12Z), 8d01a160 (HEAD, 20:09:50Z), deb1fcdb (66-07 evidence commit, 2026-11-04). `git diff --stat deb1fcdb HEAD -- scripts/rebrand` is empty. The rebrand tool has not changed since deb1fcdb.

## Criteria table

Criterion text is quoted from ROADMAP.md lines 1342-1346.

| # | Criterion | Outcome | Evidence (file:line or log line) |
| --- | --- | --- | --- |
| 1a | `python3 -m unittest scripts/rebrand/test_rebrand.py` passes | gap (evidence): not re-run; the cited log is not on disk | 66-07-SUMMARY.md:70 records "Ran 61 tests, OK, exit 0" at deb1fcdb and cites `/tmp/ag-recon/v66-07-*.log`. No log in `/tmp/ag-recon/` contains "Ran 61 tests" (checked this run). `scripts/rebrand` unchanged since deb1fcdb (git diff empty). |
| 1b | `rebrand.py verify --baseline <baseline>` reports clean | met against the owner baseline; criterion's literal path is stale (see 1c) | `/tmp/ag-recon/rebrand-verify-r4.log:1`: "verify OK: nothing left to rename (293 URL tokens still protected)". The log does not record the `--baseline` flag. The report it wrote, `/tmp/ag-scratch/rebrand-verify-r4/rebrand-report.json` (generatedAt 2026-11-05T19:53:07Z, command `verify`), matches the owner baseline `/root/src/backups/rebrand-baseline-post-decision-2026-11-04/rebrand-report.json` (sha256 6e55ba161d8bc58943700a7e4680d3b5b551d643b44fb10a5a6a3f54c6fe5068, verified this run) on all 17 `engine.keepPatternCounts` keys, on `rejectWindows`, and on `urlProtected` (293 = 293). The comparison is `verify_against` at `scripts/rebrand/rebrand.py:1186-1192`. Not re-run at HEAD (HEAD is one commit after the r4 tree; that commit changes one test file, `McpConnectionCheckerTests.cs`). |
| 1c | Criterion's baseline path `/tmp/ag-recon/rebrand-report/rebrand-report.json` | gap: wrong baseline for the owner ruling | That file is a verify report from 2026-11-03T03:10:00Z, the pre-exclusion snapshot. Its keep-pattern counts for the acp command token and the bare-engine census differ from the owner baseline (the acp count is 390 at 66-07-SUMMARY.md:143). Read from the verify code (rebrand.py:1189-1192) and the two JSON files, this path would report keep-pattern differences against the current tree. [INFERENCE: derived from reading the code and both JSON files; not run.] The earlier failure is in /tmp/ag-recon/rebrand-verify-r3.log: a keep-pattern count moved from 8398 to 8402. |
| 1d | Runbook s.2 probe 1 "lists protected URLs only" | gap: the stated result does not hold | `docs/runbooks/rebrand-autonogrammer.md` (s.2, after the usage block). The literal probe, with the runbook's excludes, returns 4,944 lines. That count includes the near-match family (words that contain the legacy stem inside another word), because the pattern has no word boundary. With a word boundary: 489 lines, of which 322 are not GitHub-slug URLs. Examples: `.env.example:110`, `.env.example:111`, `.env.example:116` (owner-kept in `scripts/rebrand/rebrand.config.json`, `.env.example` glob), and `.planning/ROADMAP.md:13` (owner-kept glob). |
| 1e | Runbook probe 2: 14 pins | met (count observed this run) | `git ls-files \| grep -c 'LegacyBrandNames.cs$'` = 14 (matches 66-07-SUMMARY.md:27-42). Pin-count test: `tests/Autonogrammer.ArchitectureTests/LegacyBrandLiteralGuardTests.cs:55` (`LegacyBrandNamesPinCountMatchesTheTrackedFiles`); pass is recorded in 66-07, not re-run. |
| 1f | Runbook probe 3: `git grep -c 'omp acp'` "unchanged outside excluded zones" | gap: no same-scope pre-apply figure is recorded | `git grep -c 'omp acp'` at HEAD: 467 occurrences in 200 files. The baseline's keep count 390 is a different scope. The recorded same-scope figure is absent; the keep-count check in 1b is the stronger recorded equivalent. |
| 2 | Renamed tree builds warning-free, is format-clean, restores in locked mode; Linux test set green with service-backed tests | met (at 8d01a160, rerun) | `/tmp/ag-recon/gate-8d01a160-rerun.log`: restore "All projects are up-to-date" (:221); "Build succeeded. 0 Warning(s) 0 Error(s)" (:347-349); totals "total: 14501, failed: 0, skipped: 3" (:535-538); smoke lines (:546-549); "bootstrap: OK (SDK 10.0.402, locked restore, Release build, tests, format, web smoke with axe)" (:550). `bootstrap.sh:60` locked restore; `:62` `-warnaserror`; `:82` `test-services.sh up`; `:88` tests with `--filter-not-trait "Category=Live"`; `:89` `dotnet format --verify-no-changes`. Service-backed projects in the rerun log: Cowork.Artifacts.Stores.Tests passed (4s 938ms); Sessions.Postgres.Tests passed (1m 36s 520ms). Phase 66-07 Task 3 (`/tmp/ag-recon/t3/test-*.log`, read this run): Api.IntegrationTests 1437/0 failed, Coordination 87/0, Cowork.Artifacts.Stores 13/0, Sessions.Postgres 107/0 (1,644 total). Skips: rerun log :448, :459, :466 (each guarded for a privileged process). |
| 2-caveat | Live-tagged tests | external: owner Live ruling | `bootstrap.sh:88` excludes `Category=Live`. Live-tagged tests: `tests/Autonogrammer.Cowork.Memory.Postgres.Tests/PostgresMemoryStoreTests.cs:14`, `tests/Autonogrammer.Coordination.Tests/DragonflyAclTests.cs:14`, `tests/Autonogrammer.Api.IntegrationTests/DataPlane/GreptimeLiveTests.cs:20`, `SubstrateLiveRefusalTests.cs:16` (66-07-SUMMARY.md:135, 149). |
| 3 | Web smoke passes under new names with non-colliding ports | gap | Rerun PASS: `gate-8d01a160-rerun.log:546-549` (api, demo, artifact smoke exit=0). First attempt at the same commit FAIL: `/tmp/ag-recon/gate-8d01a160.log:688` "api smoke exit=1". JSON `/tmp/tmp.0MJPC6sqdt/web-smoke-8d01a160-2026-11-05.json`: `assertions.a11yRoutes: false`; `failures: ["a11yRoutes: in-app @ inapp: the in-app sequence landed on the boot shell on both attempts ..."]`; `passed: false`. The race is documented in `scripts/web-smoke/a11y/routes.mjs` (lines 15-16, "gate run 6 at load 68, and two re-runs"). Ports for the gate-of-record run: JSON `app` 127.0.0.1:5180 and `apiOrigin` 127.0.0.1:5187 (defaults), not the criterion's example ports; no free-port check recorded for them. Clean earlier run at deb1fcdb on 5387/5380/5391: `/tmp/ag-recon/v66-07-smoke.log:7-9` (api and demo; not artifact). The rerun JSON files are no longer on disk (`/tmp/tmp.THjA7UUSzi` absent); only the log lines remain. |
| 4 | Desktop window title is `Autonogrammer` | met | Route A (the criterion's no-display alternative): `tests/Autonogrammer.Native.Tests/Branding/MainWindowTitleTests.cs:14-23` asserts `window.Title.ShouldBe("Autonogrammer")`. `apps/Autonogrammer.Native/MainWindow.axaml:4` `Title="Autonogrammer"`. Gate-of-record: `gate-8d01a160-rerun.log:473` Autonogrammer.Native.Tests passed (2m 43s). Route A log at deb1fcdb: `/tmp/ag-recon/v66-07-title-routeA.log` (total 1, failed 0). Route B (Xvfb `:131`, one visible window `^Autonogrammer$`) is recorded at deb1fcdb in 66-07-SUMMARY.md:82 and was not re-run. Native changes deb1fcdb to HEAD: only `<IsTrimmable>false</IsTrimmable>` in the csproj (git diff, 2 insertions), which has no title effect. |
| 5a | `check-licences.sh` and `check-brand.sh` pass | met (at 8d01a160, rerun) | `gate-8d01a160-rerun.log:212` "check-brand: ok (28 PNGs, 9 SVGs, 18 other files)"; `:223` "licence checks passed". |
| 5b | `.gitleaksignore` fingerprints for relocated recorded frames, added after the rename commit | external: owner action 6 | `git grep -n 'Autonogrammer.Harness.Rpc.Tests/TestData/frames/rpc-live' -- .gitleaksignore` returns no lines (exit=1). The relocated frames are present: `tests/Autonogrammer.Harness.Rpc.Tests/TestData/frames/rpc-live-20261021T171351Z.ndjson` (git ls-files). Precondition: rename commit `4af7d83` exists on feat/autonogrammer but is not an ancestor of HEAD. |

## Gap list for a later implementation run (by criterion)

1. C1a: Re-run `python3 -m unittest scripts/rebrand/test_rebrand.py` at HEAD in the verifier run and keep the log in a path that exists. The 66-07 log citation does not resolve.
2. C1b/1c: Repoint the criterion's and the runbook's `--baseline` to the owner baseline (`/root/src/backups/rebrand-baseline-post-decision-2026-11-04/rebrand-report.json`), or regenerate a baseline the owner accepts. Then re-run `verify --baseline` at HEAD and record the command in the log.
3. C1d: Correct runbook s.2 probe 1 so it does what its sentence says: word-bounded pattern (`-w`) with the owner-kept zones excluded, or change the claim to match the output. Current literal output is 4,944 lines.
4. C1f: Record a same-scope pre-apply `git grep -c 'omp acp'` figure, or drop probe 3 in favour of the keep-count check.
5. C3: Remove the boot-shell race from the `a11yRoutes` check in `scripts/web-smoke/a11y/routes.mjs` (or make the in-app sequence deterministic). Then run the web smoke at one commit with a clean first run, record the ports and a free-port check (`ss -ltn`), and keep the JSON output.
6. C4: none repo-side. Optional: re-run Route B (Xvfb) at HEAD.
7. C5b: Add the `.gitleaksignore` fingerprints for the relocated frames once the rename commit is on the integration line. This is owner action 6.

## External items (owner step that closes each)

- Ship hold: no PR, merge, tag, or push until Phase 66 and the milestone are verified (owner decision 2026-11-05).
- Live-tagged tests and the CI Live filter (owner ruling; see table row 2-caveat).
- Q-HOST-DFLY-PIN: owner decision (not assessed this run).
- M-23 (OQ-34), M-24 (OQ-39), M-35 (image policy): owner decisions (not assessed this run).
- Podman `:latest` pins: owner decision (not assessed this run).
- Acceptance of ADR-0010 and ADR-0030 to ADR-0037 (all Proposed): owner.
- OD-03, OD-06, OD-07: owner (not assessed this run).
- Owner actions 1 to 8 (see below).

## Owner actions (`66-OWNER-ACTIONS.md`): all open, none claimed done

Each item's `Status: open` line was confirmed this run. Proving commands were run read-only where the repo allows it. Host-state commands (podman, volumes, secrets) were not run.

| # | Action | Status | Current proving result (this run) | Closing step |
| --- | --- | --- | --- | --- |
| 1 | Repository, hosted project and domain names | open | `git grep 'swcstudiospace/Omp[O]S'` on README.md, Directory.Build.props, .github/ISSUE_TEMPLATE/config.yml: 7 lines | Owner renames the GitHub repo, the Vercel hosts, the Railway service and project, and registers the new domain. Owner runs `apply --urls`. Re-run returns 0 lines. |
| 2 | Environment variables `OMP[O]S_` to `AUTONOGRAMMER_` | open | `git grep 'OMP[O]S_'` on docs, README.md, AGENTS.md: 45 lines | Owner renames the prefix in Vercel, Railway, GitHub secrets, and every `.env`. Re-run returns 0 lines. |
| 3 | Local and hosted state (containers, volumes, secrets, PG schema, Dragonfly prefixes, Greptime queries, branches, JWT issuer and audience) | open | The probe finds 4 lines in services and tests, including the LegacyBrandNames.cs file in services/Autonogrammer.Api (line 10) and the one in services/Autonogrammer.AgentWorker (line 10): the worker issuer constant, an owner-kept pin. Host state not inspected. | Owner recreates or migrates each state item. Owner deploys API and worker together for the issuer change. Hard cutover, no alias. |
| 4 | Android `ApplicationId` | open | The probe finds 2 lines in apps/Autonogrammer.Android: the csproj at line 7 and Properties/AndroidManifest.xml at line 2 | Owner changes the ApplicationId. This is a new store identity. Re-run returns 0 lines. |
| 5 | Legal text (NOTICE, docs/licensing.md) | open | Command matches the repo URL form in NOTICE:20 and docs/licensing.md:21-24 (closed by item 1). NOTICE:41 already reads "Autonogrammer". | Owner confirms the wording. Item 1 removes the URL matches. |
| 6 | `.gitleaksignore` fingerprints for relocated frames | open | `exit=1` (no lines) | Add the fingerprints once the rename commit is on the integration line. |
| 7 | Submodules (agent-substrate, agent-swarm, claude-ultrathink) | open | `git submodule status` lists all three at pinned commits. The engine-token probe in the submodules returns 0 matches, and the legacy-named directory was not checked. | Owner changes the three submodules in their own repos, then advances the parent pins. |
| 8 | Brand artwork and trademark decision | open | `git ls-files -s apps/Autonogrammer.Native/Assets/Brand`: 4 tracked files. No pre-apply blob ids are recorded to compare. Trademark matches: NOTICE:42, docs/licensing.md:113. | Owner supplies new artwork and the trademark decision. The blob ids change. |

## Contradictions with recorded statuses

- ROADMAP.md:1318 says "plan 66-07 has not run ... there is no VERIFICATION file". 66-07-SUMMARY.md exists (Partial), and this artifact is now the first VERIFICATION file. The roadmap line is stale.
- 66-07-SUMMARY.md: its task table (lines 91-99) marks Task 3 "Blocked", and Tasks 1-2 "Partial". Its correction block (lines 137-150) supersedes that table and records Task 3 as passed. This artifact uses the correction block and the Task 3 logs read this run.
- docs/plans/queue-and-verdicts-2026-11-05.md section 3 (lines 76-99) covers 66.1 to 66.4 only. It has no phase 66 row, so there is no contradiction for phase 66. Statuses for 66.1 to 66.4 were not evaluated here.

## Substitution

This run used a general agent in place of gsd-verifier. The gsd-verifier agent returned 403 (billed to a Muse Code subscription). The procedure is the same. No status was changed.

## Owner questions (default taken in brackets)

1. Repoint the criterion's `--baseline` path to the post-decision owner baseline? [yes; the owner ruled it the keep-count reference, 66-07-SUMMARY.md:143]
2. Rewrite runbook s.2 probe 1 (word-bounded, excluded zones) or change its claim? [rewrite the claim; the tool's verify is the operative check]
3. Accept the headless title test (Route A) at 8d01a160 as C4's evidence? [yes; the criterion names it as an alternative]
4. Does a rerun pass count as C3 met when the first attempt at the same commit failed? [no; counted as gap until the race is fixed]
5. Is "full Linux test run" satisfied by the bootstrap gate with the Live filter? [yes, with the Live ruling recorded as external]
6. Add the relocated-frame fingerprints on feat/autonogrammer now or after the rename commit reaches the integration line? [after the rename commit reaches the integration line, per runbook s.5 item 6]
