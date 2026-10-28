# Phase 61 Verification: Settings Parity

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `61-SUMMARY.md`. Tests are hermetic: headless Avalonia, bUnit, no omp, no network.

Recorded totals (2026-10-28, orchestrator facts, final tree before the last wiring): Presentation 2303, Native 2012 (2 skipped), UI 595, Web 262 or more passed. No per-class result is claimed; each row is "inside the passing <Project> run". The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 | `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*SettingsParityTableTests*"` and `scripts/check-settings-parity.sh` | Ids S01 to S50 appear once each with a status of Built, Wired, Partial or Omitted and a note; a row that names a page names a real `SettingsSection`; Built and Wired rows name a page and a key; Omitted rows name neither | Inside the passing Native run. Counted from the file by this record: 6 Built, 2 Wired, 20 Partial, 22 Omitted (50). The shell script has no recorded run and is not in a gate |
| 2 (view models) | `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*HooksSettingsPageTests*" --filter-class "*RunModeSettingsPageTests*" --filter-class "*McpSettingsViewModel*" --filter-class "*DisplaySettingsPageTests*" --filter-class "*WorkspacesSettingsPageTests*" --filter-class "*WorktreesSettingsPageTests*" --filter-class "*IgnoreSettingsPageTests*" --filter-class "*PrivacySettingsPageTests*"` | Each page model's states, edits and defaults | Inside the passing Presentation run |
| 2 (headless) | `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*SettingsWindowTests*" --filter-class "*SettingsWindowHostTests*" --filter-class "*HooksPageViewTests*" --filter-class "*McpPageViewTests*" --filter-class "*RunModePageViewTests*" --filter-class "*CloudAgentsPageViewTests*" --filter-class "*RuntimeEnvironmentsViewTests*" --filter-class "*WorkspacesPageViewTests*" --filter-class "*WorktreesPageViewTests*" --filter-class "*IgnorePageViewTests*" --filter-class "*PrivacyPageViewTests*"` | The window maps each `SettingsSection` to its view in the documented order; each view renders its page model | Inside the passing Native run |
| 2 (web) | `dotnet test tests/OmpOS.UI.Tests -c Release -- --filter-class "*SettingsWebPagesTests*" --filter-class "*SettingsDialogTests*"` and `dotnet test tests/OmpOS.Web.Tests -c Release -- --filter-class "*WebSettingsPagesTests*" --filter-class "*SettingsPagesRenderTests*"` | The web dialog offers its 18 pages in the web order | Inside the passing UI and Web runs |

## Evidence files

| File | Shows |
|------|-------|
| `docs/settings-parity.md` | The 21-page window order and the table S01 to S50 |
| `scripts/check-settings-parity.sh` | The shell check of the same table |
| `tests/OmpOS.Native.Tests/Settings/SettingsParityTableTests.cs` | The test of criterion 1 |
| `docs/runbooks/evidence/web-app-2026-10-28/L1440-10-settings-00-general.png`, `M-11-models-cn-coding-plans.png`, `M-12-models-eu.png` | Web Settings General and Models |

## Known limits of this verification

- The test proves the table is complete and consistent, not that a Built or Wired row works at run time.
- A model turn is needed to see the Display flags, run mode and hooks affect a session (OD-07).
- Labs has no page to test; Worktrees and Ignore have no web page.
