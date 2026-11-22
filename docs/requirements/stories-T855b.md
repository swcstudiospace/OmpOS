# User stories T855b-req

Status: Record (user stories US-201 to US-209 for T855b-req)

Every story carries a stable identifier, role, capability,
estimate of 1 to 8 points, and one acceptance criterion in
acceptance-T855b.md. The backlog holds 9 stories, so ordering
uses MoSCoW with WSJF computed as supplementary input. All
stories are Must. Sequencing for A01: US-205, US-208, US-201,
US-202, US-203, US-204, US-206, US-207, US-209.

## US-201 — Refresh gaps_found verifications with fresh evidence

As a maintainer, I want phases 66, 68, 70, 71, 72, 73, 74, 75
and 76 re-verified with fresh evidence from targeted build and
test runs, repo-checks and web smoke so that every VERIFICATION
file records the current tree state with a current digest.

Estimate: 8
MoSCoW: Must

## US-202 — Refresh stale and missing verifications

As a maintainer, I want phases 66.2, 66.3, 66.4, 67, 69, 74.1
and 77 re-verified with fresh evidence and current digests so
that no refreshed file carries a stale digest or a missing file.

Estimate: 8
MoSCoW: Must

## US-203 — Write missing phase 77 SUMMARY files

As a maintainer, I want each missing 77 SUMMARY file written
from its PLAN file and code evidence so that every 77 PLAN has
a matching SUMMARY file.

Estimate: 3
MoSCoW: Must

## US-204 — Keep human-only criteria in human_verification

As a maintainer, I want human-only criteria recorded under
human_verification with observed evidence and exact closing
steps so that no human criterion reads as an automated pass.

Estimate: 2
MoSCoW: Must

## US-205 — Docs-only refresh with no product code diff

As a maintainer, I want the refresh restricted to docs and
planning files so that the measured surface stays documentation
and planning only.

Estimate: 2
MoSCoW: Must

## US-206 — Bump 0.20.12 to 0.20.13 and update STATE.md

As a maintainer, I want VersionPrefix, CHANGELOG.md and
STATE.md updated to version 0.20.13 so that the refresh lands
on the recorded version with the recorded position.

Estimate: 2
MoSCoW: Must

## US-207 — Open a pull request merged only on all-green CI

As a maintainer, I want one pull request to main merged only
when every CI job reports success so that main holds verified
work at version 0.20.13.

Estimate: 2
MoSCoW: Must

## US-208 — Produce fresh evidence on SDK 10.0.402 with test-services

As a maintainer, I want targeted dotnet build and test runs,
repo-checks and web smoke executed on SDK 10.0.402 against
test-services backed by Dragonfly so that refreshed files cite
fresh, reproducible evidence.

Estimate: 8
MoSCoW: Must

## US-209 — Record each result as passed or gaps_found with exact steps

As a maintainer, I want each refreshed VERIFICATION file to
state passed with evidence or gaps_found with the exact steps
that close each gap, with phase 66 recording its 4 gaps, so
that the next run starts from an honest position.

Estimate: 5
MoSCoW: Must
