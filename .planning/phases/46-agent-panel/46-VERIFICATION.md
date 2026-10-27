# Phase 46 Verification: Default Agent Panel Parity

How to re-run each proof and what it proves. Run from the repository root with the .NET environment set as in `.planning/phases/44-plugin-submodules-launch-contract/44-VERIFICATION.md`. The headless tests use Avalonia's headless platform and scripted engines; they need no display, no omp and no model.

Results recorded on 2026-10-26 are in `46-SUMMARY.md`. Project totals recorded after wave 3 (F6b): `OmpOS.Native.Tests` 1087, `OmpOS.Presentation.Tests` 975, `OmpOS.ArchitectureTests` 46. No per-class counts were recorded.

| Criterion | Command | What it proves |
|-----------|---------|----------------|
| 1 | `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*AgentPanelLayoutTests*" --filter-class "*AgentPanelKeyboardTests*"` | The default window shows exactly the rail and the thread (plus the hidden Kanban host), the one-line connection row sits directly under the header, the composer is there, no control is named like an inspector, no tab control exists inside the panel, and the removed inspector shortcuts do nothing. |
| 1 (screenshot) | See "Screenshots" below. | The same layout drawn by the real application. |
| 2 | `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*AgentPanelSessionTests*" --filter-class "*SessionEngineSetTests*"` and `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*AgentSessionViewModelTests*" --filter-class "*AgentRowStatusMapTests*"` | Selecting a session rebinds the thread and composer; an unselected session keeps running; two sessions get distinct state directories, plans and engines; each row maps its own status. |
| 3 | `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*ComposerViewPanelTests*" --filter-class "*AgentSessionRuntimeSteeringTests*" --filter-class "*SteeringRecorderAdapterTests*"` and `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*SteeringInputModelTests*"` | Enter and Ctrl+Enter steer, Ctrl+Shift+Enter queues a follow-up, a full queue keeps the draft, a failed delivery keeps the text, and an accepted steer is recorded in the Substrate without its text. Uses fake channels and a fake gateway. |
| 4 | `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*AgentPanelStateTests*" --filter-class "*PanelStateViewTests*" --filter-class "*ThreadHeaderViewTests*"` | One test per state: empty, loading, error with retry, streaming, turn error, success, each degraded plugin, a lost session that can be resumed, a closed session, a dense list, long text. |
| 5 | `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*AgentPanelAccessibilityTests*" --filter-class "*PipelineStripViewTests*" --filter-class "*ReducedMotionTests*" --filter-class "*ThemeContrastTests*" --filter-class "*AccessibilityNameTests*" --filter-class "*KeyboardFocusTests*"` | Accessible names on every authored tab stop in each layout, the live region settings, the tab order, the reduced-motion rules and the contrast ratios. |
| 6 | See "Screenshots" below. | Not met: six of the eight states have no recorded screenshot. |
| 7 | `./bootstrap.sh`, or `dotnet build OmpOS.Linux.slnf -c Release` then `dotnet test OmpOS.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"` | Build with warnings as errors, analyzers and every non-live test. Recorded: 4237 passed, 0 failed (F5); every test project passed after wave 3 (F6b). |
| 8 | `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*AgentPanelTerminalSessionTests*" --filter-class "*FontFaceResolutionTests*" --filter-class "*FontResourceTests*" --filter-class "*SettingsFontTests*"` | Every text of the Agents layout with a terminal session is drawn in a font from the `OmpOS.Font.*` tokens; the tokens resolve to DM Sans, Anybody and Space Mono static faces; the Settings window does the same. |

## Screenshots

The recorded screenshots were made by running the real application under Xvfb (method in `docs/runbooks/evidence/ui-parity-2026-10-26/foundation/README.md` and `docs/runbooks/evidence/phase-40-settings-2026-10-26/README.md`; both run omp in a sandbox `HOME`, send no prompt and enter no key):

```sh
mkdir -p /tmp/shot/home/.omp/agent /tmp/shot/state
HOME=/tmp/shot/home PI_CODING_AGENT_DIR=/tmp/shot/home/.omp/agent OMPOS_STATE_DIR=/tmp/shot/state \
  xvfb-run -a -s "-screen 0 1440x900x24" sh -c '
    ./apps/OmpOS.Native/bin/Release/net10.0/OmpOS.Native & APP=$!
    sleep 15; import -window root 01-empty-panel.png
    kill $APP'
```

Recorded: `docs/runbooks/evidence/ui-parity-2026-10-26/before/01-empty-panel.png` to `05-kanban-empty.png` (empty panel, a chat session, an omp terminal session, the Kanban tab with two cards and empty) and `.../foundation/` (the same views after the Phase 50 foundation, Dark, plus Settings Runtime and General). Waiting matters: omp took up to 50 s to draw in the terminal session on this loaded host.

Missing for criterion 6: streaming (needs a model turn), error, long-running list, narrow window, dense list, keyboard focus.

## Known limits of this verification

- The scripted engines (`ScriptedOmp`) stand in for omp in the panel tests. Real omp was started only for the screenshots and the live tests of Phase 48.
- No model turn was run (OD-07). Windows, macOS, Android and the hosted deployments were not run. No screen reader was used.

Update 2026-10-27: the screenshot half of criterion 8 is met: the Phase 50 captures in `docs/runbooks/evidence/ui-parity-2026-10-26/after-dark/` and `after-light/` show DM Sans and Space Mono. Criterion 6 stays not met: the 960x640 captures show the narrow layout of the empty and idle chat, not a Running session.
