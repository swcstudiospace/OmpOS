---
phase: "5"
slug: "verify"
status: partial
nyquist_compliant: partial
wave_0_complete: true
created: "2026-10-21"
updated: "2026-10-21"
---

# Phase 5 — Validation Strategy

Scope: the 3 requirements that ROADMAP.md (Phase 5 `Requirements:` line) and the REQUIREMENTS.md traceability table assign to Phase 5: VERIFY-01, VERIFY-02, VERIFY-03.

## Compliance

- Rows: 3. AUTOMATED: 1 (VERIFY-02). MANUAL: 2 (VERIFY-01, VERIFY-03). PARTIAL: 0.
- `nyquist_compliant: partial`. Not `true`: VERIFY-01 and VERIFY-03 are manual by design.
- Every test name below was confirmed by grep of `tests/**/*.cs` on 2026-10-21. Commands were **not executed** in this revision. A filter that matches zero tests is a failure, not a pass.
- Table cells write the VSTest OR operator as `\|` (GFM escape). Copy commands with a plain `|` inside double quotes.
- The TODO-linkage test (`RepoHygieneTests.TodoMarkersLinkToOpenQuestion`) was not present in the prior draft and the milestone audit (B-4). It is present now (grep). VERIFY-02 is therefore AUTOMATED, not PENDING.

## Requirement Coverage

| Req | Status | Automated command | Confirmed test names (grep-verified) | Manual part and evidence |
|---|---|---|---|---|
| VERIFY-01 | MANUAL | Record check only (the clean-clone run itself is manual): `grep -n "bootstrap: OK\|total: 614\|failed: 0" docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21.log` | None (no test covers the clean clone) | Manual: a fresh `git clone` into an empty temp directory, then `./bootstrap.sh`, run by an operator. Not run by this validation (it is a full build). Evidence: `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21.log` lines 139-144 (`total: 614`, `failed: 0`, `succeeded: 614`, `bootstrap: OK (SDK 10.0.402, locked restore, Release build, tests, format)`); run record `docs/runbooks/local-dev.md` `### Clean-clone bootstrap (VERIFY-01)` (:106-118): commit `8479c7c`, empty SDK root, limits (cloned from the local repository, not the GitHub remote; warm NuGet cache). The working-tree run `docs/runbooks/evidence/bootstrap-gate-2026-10-21.log` (lines 90-95) is a separate record. Who: an operator with a clean machine or temp directory and network access for the SDK install. Repeat when HEAD moves past `8479c7c`. |
| VERIFY-02 | AUTOMATED | `dotnet test tests/OmpOS.ArchitectureTests --filter "FullyQualifiedName~RepoHygieneTests"` (TODO-linkage alone: `dotnet test tests/OmpOS.ArchitectureTests --filter "FullyQualifiedName~RepoHygieneTests.TodoMarkersLinkToOpenQuestion"`) | RepoHygieneTests.cs: `TodoMarkersLinkToOpenQuestion` (:121; scans `src`, `services`, `apps`, `tests` `*.cs` and `*.razor`, excluding `bin`, `obj`, `node_modules`, `wwwroot`, `lib`, `TestData`; fails on a TODO/FIXME/HACK/XXX comment without an open-question ID from docs/open-questions.md), `RepoHygiene_DocsSkeletonExists` (:77; open-questions header row, six columns, non-empty owner and status, unique IDs), `RepoHygiene_NoDockerTooling` (:24), `NoDockerfileOrComposeFileNamed` (:52) | Fact-row coverage is grep-confirmed in `docs/open-questions.md`: OQ-01, OQ-02, OQ-03, OQ-06, OQ-07, OQ-09, OQ-10, OQ-23 (listed as Q-SB-RAILWAY and OQ-23), Q-HARN-UPDATE, each with an owner tag. Zero TODO/FIXME/HACK/XXX comment markers exist in `*.cs`/`*.razor` under `src`, `services`, `apps`, `tests` (grep). The one textual match is the scanner's own regex literal (RepoHygieneTests.cs:114); it has no comment token, so `HasUnlinkedMarker` does not flag it (RepoHygieneTests.cs:181-224). The check is therefore satisfied vacuously until markers appear; it has no positive case. |
| VERIFY-03 | MANUAL | None automated. Supporting structure check: `grep -n "^| AC_" docs/handoff.md` should return eleven rows (AC_1 to AC_11). | None | Manual: a reviewer compares each `docs/handoff.md` verdict (AC_1 to AC_11, rows at lines 39-49; eleven rows confirmed by grep) with the acceptance criteria. Provenance: Q-AC-LIST (docs/open-questions.md) states the original ultrathink specification was not retrieved; verification is against the reconstructed list in `.planning/phases/05-verify/05-CONTEXT.md`. Known stale text the reviewer must correct or accept: `docs/handoff.md:32` says the clean-clone record is "still outstanding", but `docs/runbooks/local-dev.md:106-118` records it; `docs/handoff.md:49` (AC_11) says "TODO-linkage test per 05-VERIFICATION until it lands", but the test now exists at `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs:121`. Who: a reviewer other than the handoff author, and the owner of the ultrathink specification for the AC list. |

## Manual-Only Verifications

| Req | Reason | Who or what is needed |
|---|---|---|
| VERIFY-01 (clean-clone run) | A full bootstrap from a fresh clone is a manual operator step. This validation does not run `./bootstrap.sh`. The committed record is for `8479c7c`. | Operator with a clean temp directory and network access; a new record is required when HEAD changes. |
| VERIFY-03 (handoff fidelity) | Judgement that each Implemented, Stubbed or Deferred verdict matches its evidence. Not automatable. | Reviewer other than the author; AC list owner for provenance (Q-AC-LIST). |

## Gap Register (requirement clauses without an automated test)

| Req | Missing | Evidence |
|---|---|---|
| VERIFY-02 | None for the marker-gate cases. The positive case now exists: an unlinked marker is reported (one offender) and a linked marker is accepted (no offenders). | `TodoScan_ReportsUnlinkedMarkerInTemporaryTree` (expects one offender) and `TodoScan_AcceptsLinkedMarkerInTemporaryTree` (expects none), both in `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs`, using the shared helper `tests/OmpOS.ArchitectureTests/TodoMarkerScanner.cs`. Present and exercised by the gate run; not claimed as passed in this file. |
| VERIFY-02 | Source tag `Q-SB-OPENSHELL` (src/OmpOS.Sandbox/OpenShellProvider.cs:11) has no row in docs/open-questions.md. The TODO check does not cover it because it is not a TODO marker, but the VERIFY-02 "every unverified external fact" clause is not met for that tag. | grep of `docs/` for `Q-SB-OPENSHELL` returns no match. Also recorded under Phase 3 SBOX-04. |
| VERIFY-03 | The AC list provenance is open (Q-AC-LIST). The docs-fidelity claim for architecture, ADRs, runbooks, README and CONTRIBUTING (ROADMAP Phase 5 criterion 3) was not re-audited here. | `docs/open-questions.md` Q-AC-LIST row; `.planning/ROADMAP.md:96`. |

## Resolved since the previous draft (confirmed by grep at HEAD)

- The milestone audit B-4 and the prior VERIFY-02 note said `RepoHygieneTests` had no marker check. `RepoHygieneTests.TodoMarkersLinkToOpenQuestion` now exists (RepoHygieneTests.cs:121), and the positive and negative cases are `TodoScan_AcceptsLinkedMarkerInTemporaryTree` and `TodoScan_ReportsUnlinkedMarkerInTemporaryTree` in the same file, using the shared helper `TodoMarkerScanner.cs`. These are present and exercised by the gate run. VERIFY-02 moves from UNSATISFIED/PENDING to AUTOMATED.
- `.planning/phases/05-verify/05-VERIFICATION.md` exists (status `gaps_found`, dated 2026-10-21). Its VERIFY-02 PARTIAL finding states that no TODO check exists; that finding predates `TodoMarkersLinkToOpenQuestion` and is stale on this point. It is not edited here.

## Notes

- Commands here were not run in this revision. Before this file can move to `validated`, run the VERIFY-02 command and confirm a non-zero match count and a pass.
- `nyquist_compliant` stays `partial` until the VERIFY-01 operator record and the VERIFY-03 reviewer sign-off exist.
- `docs/handoff.md` is not edited by this file. The stale lines listed under VERIFY-03 are for the handoff owner or reviewer.
