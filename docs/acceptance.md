# Acceptance criteria T77d9-req

One criterion per story. Each block carries story, Given, When, Then,
check and automated flag. A block that cannot be checked by a
machine stays out of this file and moves to open questions.

AC-101 story: US-101 Given the tree on origin main at 434e214e When
rebrand verification runs with the frozen baseline Then exit code is 0
and old-name literals under src, services and apps count 0 check: repo-hygiene
automated: true

AC-102 story: US-102 Given the scaffold manifest with 21 source and 19
test projects When the scaffold record is inspected Then registered
project count is 40 and ContractVersion.Current is 1.6.0 check: repo-hygiene
automated: true

AC-103 story: US-103 Given the hosted bundle without demo code When the
no-demo check runs on the built bundle Then exit code is 0 and demo
citations count 0 check: web-smoke automated: true

AC-104 story: US-104 Given both web smoke halves When the scripted
accessibility checks run Then error findings count 0 and the node suite
passes 48 of 48 check: web-smoke automated: true

AC-105 story: US-105 Given CI on current main When the quality jobs
run Then image scan, CodeQL and Scorecard each report success and open
CodeQL alerts count 0 check: ci automated: true

AC-106 story: US-106 Given a first-time email user When sign-in by code
and sign-in by link are exercised Then both paths end in a signed-in
state with 2 of 2 flows passing check: auth-contract automated: true

AC-107 story: US-107 Given migrated schema M0001 through M0010 When the
data-plane gate runs Then 10 of 10 migrations apply with a down step
and old physical names count 0 check: data-contract automated: true

AC-108 story: US-108 Given a window with Code and Cowork services When
the surface switch is changed and Kanban is queried Then sessions of
the other surface visible on the board count 0 check: architecture automated: true

AC-109 story: US-109 Given a Cowork session When chat, steering, resume
from history and refusal of unreadable images are exercised Then
6 of 6 behaviours pass and tool calls without approval grant count 0
check: cowork-contract automated: true

AC-110 story: US-110 Given an artifact version When render, export and
sanitiser checks run Then script executions count 0 and owner-scope
violations count 0 across all 21 render rules check: artifact-contract
automated: true

AC-111 story: US-111 Given browser use without an owner decision When
the operator reviews the phase state Then phase 72 remains Blocked
with 1 open decision and 0 production grants issued check: planning-gate
automated: true

AC-112 story: US-112 Given computer use on the local device When the
provider runs without explicit approval Then blocked actions count is
greater than 0 and unapproved screen reads count 0 check: computer-use-contract
automated: true

AC-113 story: US-113 Given a paired dispatch device When offline
commands are queued and relayed Then 100 percent of queued commands
within the 5 minute window arrive with 0 duplicates check: dispatch-contract
automated: true

AC-114 story: US-114 Given memory writes retried after a failure When
the idempotency suite replays 50 writes Then duplicated memory rows
count 0 and applied writes count 50 of 50 check: memory-contract
automated: true

AC-115 story: US-115 Given Cowork parity A When projects, memory tool
registration and scheduled runs are checked Then scheduled runs that
pass the host count is 3 of 3 and unregistered tools count 0
check: cowork-contract automated: true

AC-116 story: US-116 Given plugin bundles and isolation When the closure
gate runs Then bundle rows with evidence count 14 of 14 and live
isolation proofs without evidence count 0 check: isolation-contract
automated: true

AC-117 story: US-117 Given the full gate at final head When bootstrap
runs on a clean worktree Then exit code is 0 and failed tests count 0
check: full-gate automated: true

AC-118 story: US-118 Given the desktop release candidates When the
asset verifier checks 6 files and the rehearsal is dispatched Then
SHA256SUMS-desktop verifies 6 of 6 and the rehearsal reports success
with 1 of 1 Windows smoke runs passing check: release-gate automated: true

AC-119 story: US-119 Given local main 93 commits behind origin main
with 10 modified files When the branch from origin main is created
Then 10 of 10 files are preserved, main head is unchanged and direct
commits to main count 0 check: git-gate automated: true

AC-120 story: US-120 Given lenses 1 to 6 of phase 25.1 and slices
25.2 to 25.9 When the top gaps are hardened Then new conventions count
0, scope rows removed count 0 and gate failures count 0 check: quality-gate
automated: true

AC-121 story: US-121 Given the pull request to main When CI completes
Then jobs in success state count is equal to total jobs and failed
jobs count 0 before merge check: ci automated: true

AC-122 story: US-122 Given the merged head When STATE.md and
ROADMAP.md are updated Then both files record the merged head commit
and outdated phase lines count 0 check: docs-gate automated: true
