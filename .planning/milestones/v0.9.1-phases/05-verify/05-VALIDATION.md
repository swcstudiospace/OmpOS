---
phase: "5"
slug: "verify"
status: partial
nyquist_compliant: partial
wave_0_complete: true
created: "2026-10-21"
updated: "2026-10-21"
---

# Phase 5 - Validation Strategy

Scope: the 3 requirements that `.planning/milestones/v0.9.1-ROADMAP.md:92` (Phase 5 `Requirements:` line) assigns to Phase 5: VERIFY-01, VERIFY-02, VERIFY-03.

## Compliance

- Rows: 3. GREEN: 1 (VERIFY-02). MANUAL: 2 (VERIFY-01, VERIFY-03). PARTIAL: 0.
- `nyquist_compliant: partial`. VERIFY-02 is GREEN on the filtered run at a26fe2f. VERIFY-01 stays MANUAL until a clean-clone run at HEAD is recorded (REC-02, open), and VERIFY-03 stays MANUAL until a reviewer signs off, so the body verdict stays partial.
- Gate evidence at HEAD a26fe2f: `docs/runbooks/evidence/gate-a26fe2f-2026-10-21.log` lines 106-111 (`total: 1080`, `failed: 0`, `succeeded: 1080`, `skipped: 0`, `bootstrap: OK`). Per-assembly lines for `OmpOS.ArchitectureTests` are at lines 78 and 95 (`passed`, 14s 050ms).
- Test names and line numbers were confirmed by reading `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs` on 2026-10-21 (line numbers are method declarations). The commands in the Requirement Coverage table were **not executed** in this revision; the VERIFY-02 filtered run is the evidence file cited in its row. A filter that matches zero tests is a failure, not a pass.
- Table cells write the VSTest OR operator as `\|` (GFM escape). Copy commands with a plain `|` inside double quotes.

## Requirement Coverage

| Req | Status | Automated command | Confirmed test names (grep-verified) | Manual part and evidence |
|---|---|---|---|---|
| VERIFY-01 | MANUAL | Record check only (the clean-clone run itself is manual): `grep -n "bootstrap: OK\|total: 614\|failed: 0" docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-8479c7c.log` | None (no test covers the clean clone) | Manual: a fresh `git clone` into an empty temp directory, then `./bootstrap.sh`, run by an operator. Not run by this validation (it is a full build). Record checked: `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-8479c7c.log` lines 139-144 (`total: 614`, `failed: 0`, `succeeded: 614`, `bootstrap: OK (SDK 10.0.402, locked restore, Release build, tests, format)`). Run record `docs/runbooks/local-dev.md` `### Clean-clone bootstrap (VERIFY-01)` (:125-144) lists the primary run at `a6fcadd` (log `clean-clone-bootstrap-2026-10-21.log`, 615 total) and re-runs through `dee19dd` (925 tests). No record names `a26fe2f`, the HEAD of this gate. Blocker: an operator clean-clone run at a26fe2f. |
| VERIFY-02 | GREEN | `dotnet test tests/OmpOS.ArchitectureTests --filter "FullyQualifiedName~RepoHygieneTests"` (TODO-linkage alone: `dotnet test tests/OmpOS.ArchitectureTests --filter "FullyQualifiedName~RepoHygieneTests.TodoMarkersLinkToOpenQuestion"`) | `RepoHygieneTests.cs`: `TodoMarkersLinkToOpenQuestion` (:115; asserts `TodoMarkerScanner.FindUnlinkedMarkers` returns no offenders against the owners parsed from `docs/open-questions.md`); positive case `TodoScan_AcceptsLinkedMarkerInTemporaryTree` (:145; `// TODO: tracked by OQ-01` is accepted); negative case `TodoScan_ReportsUnlinkedMarkerInTemporaryTree` (:126; `// TODO: no open question yet` yields one offender at `Sample.cs:1:`); `RepoHygiene_DocsSkeletonExists` (:76); `RepoHygiene_NoDockerTooling` (:23); `NoDockerfileOrComposeFileNamed` (:51). Helper: `tests/OmpOS.ArchitectureTests/TodoMarkerScanner.cs` (rule :11-27; `SelfExcludedPaths` :114-118 keeps `RepoHygieneTests.cs` and `TodoMarkerScanner.cs` out of the scan, so the controls' own fixtures are not flagged). | Filtered run at HEAD a26fe2f: `docs/runbooks/evidence/verify02-todo-linkage-a26fe2f-2026-10-21.log` (lines 4-8: `Test run summary: Passed!`, `total: 1`, `failed: 0`, `succeeded: 1`, `skipped: 0`). The filter `FullyQualifiedName~TodoMarkersLinkToOpenQuestion` is as reported by Main; the log does not record the filter string, so that attribution rests on the report. The one matched test is `TodoMarkersLinkToOpenQuestion`. The two temp-tree controls do not contain that substring and are not in this run; their pass is covered only by the gate assembly pass (`OmpOS.ArchitectureTests passed`, gate log :78 and :95, `failed: 0`), not by name. Marker scan (grep 2026-10-21) of `*.cs` and `*.razor` under `src`, `services`, `apps`, `tests` finds no first-party marker outside the two self-excluded files. Fact rows exist in `docs/open-questions.md` for OQ-01, OQ-02, OQ-03, OQ-06, OQ-07, OQ-09, OQ-10, OQ-23, Q-SB-RAILWAY (:36) and Q-HARN-UPDATE (:43). |
| VERIFY-03 | MANUAL | None automated. Supporting structure check: the verdict table `docs/handoff.md:72-82` holds eleven rows, AC_1 to AC_11 (grep-confirmed 2026-10-21). | None | Manual: a reviewer compares each `docs/handoff.md` verdict (AC_1 to AC_11, rows at lines 72-82) with the acceptance criteria. Provenance: `Q-AC-LIST` (`docs/open-questions.md:44`) is "Recovered in part (2026-10-21)": AC_1 to AC_6 and AC_8 are cited by number in Linear with no verbatim text; AC_7, AC_9, AC_10 and AC_11 are unrecoverable. Verification is against the reconstruction in `.planning/milestones/v0.9.1-phases/05-verify/05-CONTEXT.md` (lines 25-35); `docs/handoff.md:84` states that column 2 is reconstruction, not verbatim text. Stale text from the prior draft is resolved: `docs/handoff.md` no longer says the clean-clone record is "still outstanding", and AC_11 (:82) cites `RepoHygieneTests.TodoMarkersLinkToOpenQuestion`. `.planning/milestones/v0.9.1-phases/05-verify/05-VERIFICATION.md:34` still says the AC_11 evidence cell reads "still pending"; that file is not edited here. Who: a reviewer other than the handoff author, and the owner of the ultrathink specification for the AC list. No reviewer sign-off exists. |

## Manual-Only Verifications

| Req | Reason | Who or what is needed |
|---|---|---|
| VERIFY-01 (clean-clone run) | A full bootstrap from a fresh clone is a manual operator step. This validation does not run `./bootstrap.sh`. No committed clean-clone record names HEAD a26fe2f; the newest is `dee19dd`. | Operator with a clean temp directory and network access; a new record is required when HEAD changes. |
| VERIFY-03 (handoff fidelity) | Judgement that each Implemented, Stubbed or Deferred verdict matches its evidence. Not automatable. | Reviewer other than the author; AC list owner for provenance (Q-AC-LIST). |

## Gap Register (requirement clauses without a test-level result)

| Req | Missing | Evidence |
|---|---|---|
| VERIFY-02 | None. The TODO-linkage test has a filtered pass at HEAD a26fe2f. The two temp-tree controls are named in this file and covered only by the assembly-level gate pass, not by a filtered run. | `verify02-todo-linkage-a26fe2f-2026-10-21.log` lines 4-8; `gate-a26fe2f-2026-10-21.log` :78 and :95; `RepoHygieneTests.cs:115`, `:126`, `:145`. |
| VERIFY-03 | The AC list provenance is partly open: AC_7, AC_9, AC_10 and AC_11 are unrecoverable (Q-AC-LIST). The docs-fidelity claim for architecture, ADRs, runbooks, README and CONTRIBUTING (v0.9.1 ROADMAP Phase 5 criterion 3) was not re-audited here. | `docs/open-questions.md:44`; `.planning/milestones/v0.9.1-ROADMAP.md:96`. |

## Resolved since the previous draft (confirmed by reading at HEAD a26fe2f)

- The TODO-linkage check exists: `RepoHygieneTests.TodoMarkersLinkToOpenQuestion` (`RepoHygieneTests.cs:115`), with the positive case `TodoScan_AcceptsLinkedMarkerInTemporaryTree` (:145) and the negative case `TodoScan_ReportsUnlinkedMarkerInTemporaryTree` (:126), both using the shared helper `TodoMarkerScanner.cs`. The prior "no TODO check" finding is resolved. The filtered run at a26fe2f passes the named test, so VERIFY-02 is GREEN.
- The `Q-SB-OPENSHELL` gap is closed: `docs/open-questions.md:38` has a row for it (owner `ompos-maintainers`, status open, environmental blocker), and OD-11 at `docs/open-questions.md:74` cites it. The source tag at `src/OmpOS.Sandbox/OpenShellProvider.cs:11` is therefore covered.
- `.planning/milestones/v0.9.1-phases/05-verify/05-VERIFICATION.md` exists. Its frontmatter reads `status: passed` (:4), its VERIFY-02 entry reads `PARTIAL` (:11), and its body reads VERIFIED (:58). Those statements conflict. This file adopts none of them; the verdict here comes from the gate log and the files read above.
- `docs/handoff.md` no longer contains "still outstanding" or "TODO-linkage test per 05-VERIFICATION"; AC_11 (:82) names the test.

## Notes

- VERIFY-02 is GREEN on the filtered run at a26fe2f. A control-level result for the two `TodoScan_*` tests would need a filtered run with `FullyQualifiedName~TodoScan_`, recorded in a log; it is not required for GREEN here.
- `nyquist_compliant` stays `partial` until the VERIFY-01 clean-clone record at HEAD (REC-02) and the VERIFY-03 reviewer sign-off exist.
- `docs/handoff.md` is not edited by this file. The stale line in `05-VERIFICATION.md` listed under VERIFY-03 is for its owner.
