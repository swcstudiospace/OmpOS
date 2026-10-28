# Phase 61 Summary: Settings Parity

Status (recorded 2026-10-28): both criteria are Partial. `docs/settings-parity.md` tracks the 50 rows S01 to S50 by id, and `SettingsParityTableTests` asserts that every id is present once with a status; 22 of the 50 are Omitted with a reason, 8 are Built or Wired and 20 are Partial (the missing piece is stated; counts re-checked from the file on the final tree: unchanged). Nine of the ten new pages exist in the desktop Settings window and a web counterpart exists for seven of them; re-check of the tree: there is no Labs page or view in either head (no match for "labs" in `apps` or `src`), and `Worktrees` and `Ignore` exist only as desktop pages (`WorktreesPageView.axaml`, `IgnorePageView.axaml`, `WorktreesSettingsPage.cs`, `IgnoreSettingsPage.cs`; no web `.razor` for either). Settings changes were not exercised with a live model turn (OD-07), and nothing ran on Windows, macOS or Android. The `./bootstrap.sh` gate on commit e24e6851 exited 0 (recorded 2026-10-28) and its `repo-checks: ok` step includes `scripts/check-settings-parity.sh`.

The record was written after the work. Statuses use: Met, Met (recorded), Partial, Blocked, Not verified.

Recorded totals (2026-10-28): the gate on commit e24e6851 passed `OmpOS.Native.Tests` (16 m), `OmpOS.Web.Tests` (4 m 16 s) and `OmpOS.UI.Tests` (37 s) inside `dotnet test OmpOS.Linux.slnf` (12215 total, 12213 succeeded, 0 failed, 2 skipped). Earlier per-project runs: Presentation 2303, Native 2012 (2 skipped), UI 595, Web 262 or more. No per-class result is claimed.

## Status counts (counted from `docs/settings-parity.md`, section "v0.15.0")

| Status | Rows | Ids |
|--------|------|-----|
| Built | 6 | S12, S17, S28, S33, S40, S48 |
| Wired | 2 | S04, S39 |
| Partial | 20 | S02, S03, S05, S08, S09, S10, S14, S15, S23, S24, S25, S26, S29, S30, S31, S35, S36, S37, S38, S45 |
| Omitted | 22 | S01, S06, S07, S11, S13, S16, S18, S19, S20, S21, S22, S27, S32, S34, S41, S42, S43, S44, S46, S47, S49, S50 |
| Total | 50 | S01 to S50, each once |

The older table of the same file (34 rows of 2026-10-22: 8 kept as a page, 4 shown inside a page, 22 omitted) is a different inventory and is unchanged.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Each of the 50 settings rows is built, wired to omp, or listed as omitted with the reason, in `docs/settings-parity.md`; a test that every row id has a status | Partial | The test exists: `SettingsParityTableTests` (`EveryRowIdIsPresentOnceWithAStatus`: ids S01 to S50 once, status one of Built, Wired, Partial, Omitted, note not empty; `RowsNameExistingPagesAndKeys`: a page names a real `SettingsSection`, Built and Wired name a page and a key, Omitted names neither), inside the Native run that passed in the e24e6851 gate; `scripts/check-settings-parity.sh` does the id check from a shell and is now called by `scripts/ci/repo-checks.sh`, which `bootstrap.sh` and `.github/workflows/ci.yml` run (the gate log shows `repo-checks: ok`). 30 rows are Built, Wired or Omitted with a reason; 20 rows are Partial, so the criterion's three outcomes cover 30 of 50 rows and the Partial rows state what is missing instead |
| 2 | The new pages (Hooks, MCP, Cloud agents, Remote, Workspaces, Privacy, Worktrees, Ignore, Run mode, Labs) exist in the desktop and the web head; view model tests and headless tests per page | Partial | Desktop: Hooks, MCP, Cloud, Remote, Workspaces, Privacy, Worktrees, Ignore, Run mode (and Display, Automations) are `SettingsSection` values mapped to views by `SettingsWindowTests`; view model tests `HooksSettingsPageTests`, `McpSettingsViewModel*Tests`, `RunModeSettingsPageTests`, `WorkspacesSettingsPageTests`, `WorktreesSettingsPageTests`, `IgnoreSettingsPageTests`, `PrivacySettingsPageTests`, `DisplaySettingsPageTests`; headless tests `HooksPageViewTests`, `McpPageViewTests`, `RunModePageViewTests`, `CloudAgentsPageViewTests`, `RuntimeEnvironmentsViewTests`, `WorkspacesPageViewTests`, `WorktreesPageViewTests`, `IgnorePageViewTests`, `PrivacyPageViewTests`, all inside the Native, Presentation, UI and Web projects that passed in the e24e6851 gate. Web: Hooks (read-only list), MCP (files), Run mode, Workspaces, Remote, Cloud agents and Privacy (information pages with a desktop notice) in `WebSettingsOrder`; Worktrees and Ignore are not offered in the web (re-checked: no web view file). Labs: no page in either head. Missing deliverables, not an external prerequisite |

## What the phase delivered

- The 21-page desktop window order (Runtime, General, Display, Agents, Run mode, Models, Tab, Rules, Skills, Commands, Hooks, MCP, Plugins, Workspaces, Worktrees, Ignore, Remote, Cloud, Privacy, Network, Keyboard); pages whose service the head does not supply are left out; Worktrees, Ignore, Cloud (its not-configured state) and Privacy are always present.
- Display, Workspaces, Worktrees, Ignore and Privacy pages with their views and sources; the About button on General; the proxy note on Network; `HeadSettings.OpenAsync(section)` so `/settings` and palette actions open a named page.
- The 50-row table, the shell check and the test.
- An 18-page web Settings dialog (Phase 54).

## Deviations

- The builder's report counted 4 Built, 2 Wired, 17 Partial, 27 Omitted; the tree has 6, 2, 20, 22. The tree wins (the report predates later edits of the table).
- A fourth status, Partial, was added to the three the criterion names.
- Labs does not exist: no inventory row names a Labs page and Cursor's Beta (row 16 of the older table) is Omitted for lack of a verified source.
- Worktrees and Ignore are desktop-only pages; the web head shows information pages for Run mode, Workspaces, Remote, Cloud agents and Privacy.

## Partial

- Criterion 1: 20 rows Partial. Examples from the table: S02 and S09 flags are stored but no prompt box applies vim keys and thinking blocks and running time are stored flags only; S05 only the read boundary exists and it binds the tool calls omp asks OmpOS about; S10 Max Mode, update channel and status line are not built.
- Criterion 2: no Labs page; web pages for Run mode, Workspaces, Remote, Cloud agents and Privacy are information pages; no Worktrees or Ignore page in the web.

## Blocked (OQ/OD)

- The Cloud page works against a hosted API only after the sign-in authority (OD-03) exists and a Railway deploy is within the granted authority (OQ-100); nothing of OmpOS is deployed on Railway (recorded 2026-10-28).
- Rows that need a live agent turn (applying display flags in the transcript, run mode effects) are OD-07.

## Known limits

- `scripts/check-settings-parity.sh` now runs in the gate through `scripts/ci/repo-checks.sh`; it checks ids as the test does.
- The test checks ids, statuses and page and key naming, not whether a Built row's behaviour works.
- Open points of the builders: whether S02 and S09 flags are to be applied by the composer, transcript and status line; whether attribution text (S07) becomes a prompt rule.
- Display, Worktrees and Ignore have no web page.
