# Requirements specification T855b-req

Status: Record (requirements specification for T855b-req)

## 1. Goal

Refresh GSD verifications for phases 66 through 77, including
66.2, 66.3, 66.4 and 74.1, with fresh evidence gathered per the
ROADMAP success criteria. Record each result honestly as passed
with evidence or as gaps_found with the exact steps that close
each gap. Write the two missing 77 SUMMARY files from PLAN files
and code evidence. Keep human-only criteria in human_verification.
Land version 0.20.13 through a pull request merged only when
every CI job reports success. This work stays docs-only: no
product code diff.

Baseline, verified by reads on worktree
/root/src/worktrees/auto-78, branch docs/verify-66-77-refresh,
HEAD b46fd81f: VersionPrefix is 0.20.12 in Directory.Build.props;
CHANGELOG.md carries a 0.20.12 section dated 2026-11-21;
global.json pins SDK 10.0.402; CI uses scripts/test-services.sh
backing on Dragonfly. Observed VERIFICATION states: 66 gaps_found;
66.1 passed; 67 human_needed; 68 gaps_found; 69 human_needed;
70 gaps_found; 71 gaps_found; 72 gaps_found; 73 gaps_found;
74 gaps_found; 74.1 gaps_found; 75 gaps_found; 76 gaps_found;
77 gaps_found with verdict text; 78 PASS. Brief numbers that the
tree superseded (67/69 described as stale and gaps_found,
74.1/77/78 described as missing, two 77 SUMMARY files described
as missing): commit a483dc90 added 77 and 78 SUMMARY and
VERIFICATION files, and the tree now carries a SUMMARY for every
77 PLAN. Downstream records the observed state in each refreshed
file and creates any 77 SUMMARY the plan count check finds
missing. Phase 66 records 4 gaps. Evidence for every refreshed
file comes from a fresh run: targeted dotnet build and test
runs, repo-checks, and web smoke against test-services
(Dragonfly) on SDK 10.0.402.

## 2. Sequencing and dependencies

Order of delivery: US-205, US-208, US-201, US-202, US-203,
US-204, US-206, US-207, US-209. US-208 produces evidence.
US-201 and US-202 consume that evidence. US-206 updates
STATE.md and version sources after evidence exists. US-207
opens the pull request. US-209 closes the refresh with the
traceability record. A01 schedules in this order.

## 3. Traceability

US-201 maps to phases 66, 68, 70, 71, 72, 73, 74, 75, 76.
US-202 maps to phases 66.2, 66.3, 66.4, 67, 69, 74.1, 77.
US-203 maps to phase 77. US-204, US-205, US-208, US-209, US-206
and US-207 are refresh-wide outcomes with evidence in the
refreshed VERIFICATION files, the STATE.md update, the version
sources and the pull request. Every US and every NFR links to
at least one criterion or artifact in traceability-T855b.md.
Unknown links: 0.

## 4. Non-functional requirements

NFR-P2 kind perf: the web smoke run completes within 600 s
wall clock on SDK 10.0.402 and records 0 timeouts across all
refreshed phases verified_by A08.load.

NFR-S2 kind security: gitleaks reports 0 findings across the
full refresh diff and 0 secret literals appear in any refreshed
VERIFICATION or SUMMARY file verified_by A10.sast.

NFR-A2 kind a11y: web smoke reports 0 axe errors in light mode
and dark mode at 1440, 1024 and 390 px widths verified_by
A08.a11y.

NFR-I2 kind i18n: 100 percent of user-facing strings touched
by the refresh resolve from the English resource table, with 0
hardcoded literals added verified_by A08.lint.

NFR-C2 kind compliance: every refreshed VERIFICATION file
carries a covered_digest in v1 sha256 form, a created date
within the refresh window, and the commit the evidence ran on,
with 0 files carrying a stale digest verified_by A10.compliance.

## 5. Assumptions

A1: Worktree /root/src/worktrees/auto-78 at b46fd81f is the
implementation checkout; main checkout reads inform this spec
only and carry VersionPrefix 0.17.0 for a different line.
A2: SDK 10.0.402, dotnet and scripts/test-services.sh backing
on Dragonfly are available on the refresh host.
A3: Phases with human_needed status keep human criteria in
human_verification, with evidence and steps recorded and no
automated pass claimed for those criteria.
A4: Missing count for 77 SUMMARY files is 0 at b46fd81f per
the plan count check; downstream creates each SUMMARY the
check finds missing and records the count in the refresh log.
A5: CI on the refresh branch runs every job; merge to main
waits for success on all jobs.

## 6. Out of scope

Architecture design, product code, schema design, IaC and
release commands belong to downstream agents. Scheduling
belongs to A01; security risk acceptance belongs to A10. This
spec defines what and how well, never how.

## 7. Stability

Identifiers US-201 to US-209, criteria 201 to 209, NFR-P2,
NFR-S2, NFR-A2, NFR-I2 and NFR-C2 are stable across revisions
and are never reused for another meaning. They do not collide
with US-101 to US-122 or NFR-P1 to NFR-C1 of task T77d9-req.
