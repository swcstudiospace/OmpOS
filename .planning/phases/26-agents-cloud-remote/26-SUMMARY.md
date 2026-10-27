# Phase 26: Agents, Worktrees, Cloud and Remote, Summary

Position 2026-10-27: this record was written before the Agents window, side chats and search views and the cloud run contract existed in the tree. The audited position is in the Phase 26 Status line of `.planning/ROADMAP.md` and in `docs/quality/cursor-parity-audit-2026-10-26.md`. The text below is kept as history.

**Status: partially complete.** Side chats, transcript search and the Agents window are in the UI. The worktree manager exists but is not wired into agent runs. Cloud runs and remote control are not started.

Mac channel: No. Worktree, transcript and Agents tests run on the Linux gate. The remote-control check uses a second client on the Linux or Windows host.

## Success criteria (ROADMAP Phase 26)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Side chat opens as its own chat; transcript search across chats | Met | ADE-04 done: src/OmpOS.Presentation/Chats, views composed in MainWindow; tests/OmpOS.Native.Tests/Chats/SideChatAndSearchViewTests.cs; tests/OmpOS.Presentation.Tests/Chats/ |
| 2 | Agents window lists runs, filters by status, opens and closes runs | Partial | ADE-05 done: src/OmpOS.Presentation/Agents, composed; tests/OmpOS.Native.Tests/Agents/AgentsViewTests.cs. Open and close and the three-run transition test: not confirmed |
| 3 | Each run in its own git worktree; removed when the run closes | Partial | ADE-06 manager done: src/OmpOS.Harness.Worktrees; tests/OmpOS.Harness.Worktrees.Tests/WorktreeManagerTests.cs (19 real-git tests). NOT wired into agent runs |
| 4 | Cloud run shows status and reviewable output on completion | Not started | ADE-07 NOT STARTED. Needs a contract change request for run status and diff. Live run BLOCKED: Railway deploys paused |
| 5 | Second device sends a prompt to a local run and gets status; run stays local | Not started | ADE-08 NOT STARTED. OD-03 gated; authority not configured |

## Open items

- Wire the worktree manager into agent runs. Owner: engineering. Route: ROADMAP Phase 26 criterion 3.
- Contract change request for run status and diff in the hosted run contract. Owner: owner (approval) and engineering. Route: ROADMAP Phase 26 criterion 4.
- Resume Railway deploys and record one live cloud run in the runbook. Owner: owner. Route: docs/runbooks/hosting.md.
- OD-03 sign-in decision and remote-control authority. Owner: owner. Route: docs/open-questions.md (OD-03).

## Commits

- 4eda147: side chats with transcript search (ADE-04) and Agents window with status filter (ADE-05)
- 9e5913d: ADE-04 to ADE-20 foundations on the gate-green tree (includes the ADE-06 worktree manager)
