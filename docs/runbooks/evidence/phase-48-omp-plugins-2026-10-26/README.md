# Phase 48 evidence: the four plugins in omp processes (2026-10-26)

Both reports come from investigation probes that ran real `omp` 18.12.5 inside throwaway sandboxes under `/tmp/omp-probe2`. No `session/prompt` was sent, no credential was used, and nothing under `/root/.omp` or this repository was written. The sandbox "polluted" reproduces the shape of the owner's global omp configuration (a `config.yml` `extensions:` entry plus extension symlinks).

| File | Phase 48 criterion | Result |
|------|--------------------|--------|
| `exactly-once-loading.md` | 3 (exactly-once loading) | `--no-extensions` drops the global `extensions:` list and the `extensions/` directory; each `-e` plugin loads once; the launch recipe of `OmpLaunchPlanner` matches the recommended recipe R1. Side effect: the owner's other global extensions do not load in app-started omp (OQ-83). |
| `omp-terminal-tui.md` | 1, 2, 8 (terminal launch, observability) | The interactive omp starts in a PTY with the same arguments, shows the Ultrathink and Substrate status line and the Swarm widget, lists `swarm`, `ultrathink-*`, `substrate` and `gsd-core` in its slash menu, and exits cleanly on two Ctrl+C within about 150 ms. |
| `plugin-connection-report.md` | 4 to 7 (per plugin: registered, active) | The real omp runs the OmpOS plugin extension in ACP and in terminal mode: the connection report lists every expected Ultrathink, Swarm, Substrate and GSD-Core command and tool name; the steering heartbeat (`alive.json`) refreshes every 5 s; the TUI shows no text from the plugin. No steering directive was delivered (that needs a model turn: OD-07). |

These runs are evidence for the omp side only. The application side (planner for terminal launches, terminal panel action, per-plugin and end-to-end checks) is Phase 48 work that is not done yet.

## Live evidence tests (no model turn)

Both tests carry `[Trait("Category", "Live")]` and skip unless `OMPOS_LIVE_OMP=1` is set, omp is on `PATH`, the OS is Linux and every plugin submodule is initialised. They run the real omp in a sandbox (isolated `HOME` and `PI_CODING_AGENT_DIR`, `ULTRATHINK_UPLIFT=0`), never send `session/prompt`, never type a message, and assert that no omp process of the run remains. The gate excludes them by trait. Results are filled in by the run, not by the author of the tests.

| Test | Command (with `OMPOS_LIVE_OMP=1` in the environment) | Asserts | Result |
|------|------------------------------------------------------|---------|--------|
| `OmpOS.Harness.AgentLoop.Tests.Live.LiveOmpHandshakeTests.RealOmpAnnouncesPluginCommandsWithoutAModelTurn` | `dotnet test tests/OmpOS.Harness.AgentLoop.Tests -c Release --no-build -- --filter-trait "Category=Live"` | `omp acp` with the planned arguments and `OMPOS_STEER_DIR`, `OMPOS_CONNECTION_FILE`: within 15 s of `session/new` the connection report exists; `PluginConnectionStageSource` reports Ultrathink, Swarm, Substrate and GSD-Core as Connected; `alive.json` exists and its `lastSeen` is at most 15 s old; report and heartbeat name mode `rpc` and the same pid, which belongs to the run; observed tool and command counts are printed | passed (2026-10-26, together with `LiveOmpSteeringContractTests`, 2 of 2) |
| `OmpOS.Harness.OmpLauncher.Tests.Live.LiveOmpTerminalTests.RealInteractiveOmpConnectsAllFourPluginsAndClosesOnTwoCtrlCWithoutAMessage` | `dotnet test tests/OmpOS.Harness.OmpLauncher.Tests -c Release --no-build -- --filter-trait "Category=Live"` | interactive omp started by `OmpTerminalLauncher` over `LinuxLocalPtyBackend` with `plan.ForTerminal()` (no `acp`, no `--plugin-dir`, no first prompt), the terminal's `ESC[c` query answered with `ESC[?62;22c`: within 180 s the connection report exists; all four plugins Connected; heartbeat fresh, mode `tui`, same pid as the report; two Ctrl+C 150 ms apart exit omp with code 0; no process of the run remains; the number of answered device attributes queries is printed | to be filled by the run |
