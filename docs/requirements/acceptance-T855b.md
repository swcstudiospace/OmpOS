# Acceptance criteria T855b-req

Status: Record (acceptance list for T855b-req, criteria 201 to 209)

One criterion per story. Each block carries story, Given, When,
Then, check and an automated flag. Blocks without a machine
check do not appear in this file.

AC-201 story: US-201 Given phases 66, 68, 70, 71, 72, 73, 74,
75 and 76 with gaps_found VERIFICATION files When evidence
from targeted build and test runs, repo-checks and web smoke is
refreshed on the current tree Then 9 of 9 files carry a current
v1 sha256 covered_digest, fresh evidence entries and a passed
or gaps_found state check: verification-refresh automated: true

AC-202 story: US-202 Given phases 66.2, 66.3, 66.4, 67, 69,
74.1 and 77 with stale or missing VERIFICATION state When the
refresh runs on the current tree Then 7 of 7 phases carry a
VERIFICATION file with a current v1 sha256 covered_digest and
0 files carry a stale digest check: verification-refresh
automated: true

AC-203 story: US-203 Given every 77 PLAN file When the plan
count check compares PLAN files to SUMMARY files Then SUMMARY
files count equals PLAN files count and each new SUMMARY cites
its PLAN and code evidence lines check: docs-gate automated:
true

AC-204 story: US-204 Given criteria that only a human can
confirm When a refreshed VERIFICATION file is inspected Then
each such criterion appears under human_verification with
evidence and exact closing steps and 0 human criteria appear
as automated passes check: verification-refresh automated:
false

AC-205 story: US-205 Given the refresh branch diff against
b46fd81f When changed files are listed by kind Then product
code files under src, services and apps changed count 0 and
all changed paths sit under docs or .planning check: git-gate
automated: true

AC-206 story: US-206 Given the completed refresh When
Directory.Build.props, CHANGELOG.md and STATE.md are inspected
Then VersionPrefix is 0.20.13, a 0.20.13 CHANGELOG section
exists and STATE.md records the refresh position with 0 files
at 0.20.12 check: docs-gate automated: true

AC-207 story: US-207 Given the pull request to main When CI
completes Then jobs in success state count equals total jobs,
failed jobs count 0 and the merge commit lands only after the
final success report check: ci automated: true

AC-208 story: US-208 Given worktree /root/src/worktrees/auto-78
at b46fd81f with SDK 10.0.402 When targeted dotnet build and
test runs, repo-checks and web smoke run against test-services
backed by Dragonfly Then exit code 0 for each command is
recorded with timestamps and commit hash b46fd81f on each
refreshed file check: evidence-run automated: true

AC-209 story: US-209 Given each refreshed VERIFICATION file
When the crash-checker inspects state, evidence and gaps Then
9 of 9 gaps_found-refresh files and 7 of 7 stale-refresh files
state passed with evidence or gaps_found with exact closing steps,
phase 66 records 4 gaps and gaps without steps count 0 check:
verification-refresh automated: false
