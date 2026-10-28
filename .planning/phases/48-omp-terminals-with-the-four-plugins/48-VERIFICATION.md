# Phase 48 Verification: Omp Terminals with the Four Plugins

How to re-run each proof and what it proves. Run from the repository root with the .NET environment set as in `.planning/phases/44-plugin-submodules-launch-contract/44-VERIFICATION.md`. Tests not tagged Live are hermetic: fake omp scripts, fake plugins, temporary directories. Live tests run the real omp in a sandbox, send no prompt, type no message and use no credential.

Results recorded on 2026-10-26 are in `48-SUMMARY.md`. Project totals recorded after wave 3 (F6b): `Autonogrammer.Harness.AgentLoop.Tests` 596, `Autonogrammer.Native.Tests` 1087, `Autonogrammer.Presentation.Tests` 975, `Autonogrammer.ArchitectureTests` 46. The gate-equivalent run passed 4237 tests with 0 failed (F5).

## Gate tests (no omp needed)

| Criterion | Command | What it proves |
|-----------|---------|----------------|
| 1 | `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*OmpTerminalCommandTests*" --filter-class "*OmpLaunchPlannerTests*" --filter-class "*SessionStateLayoutTests*"` | `ForTerminal` returns the plugin arguments of the ACP plan without `acp` and without `--plugin-dir`, no MCP server or token, an optional `--resume` and a last-position prompt, the same environment (including `AUTONOGRAMMER_STEER_DIR`, `AUTONOGRAMMER_CONNECTION_FILE` and the per-session `SWARM_GSD_STATE_FILE`), no empty value, and unsafe resume or prompt text is rejected. |
| 2 | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*TerminalSessionTests*" --filter-class "*OmpTerminalViewModelTests*" --filter-class "*OmpTerminalViewTests*" --filter-class "*OmpTerminalPtyBridgeTests*"` and `dotnet test tests/Autonogrammer.Harness.OmpLauncher.Tests -c Release -- --filter-class "*OmpTerminalLauncherTests*" --filter-class "*OmpTerminalLauncherEnvironmentTests*"` | The terminal starts a (fake) omp with the planned arguments, project directory and environment; a missing omp shows fix text and the head keeps working; closing and disposing leave no omp child. |
| 4 to 7 (degraded paths) | `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*PluginConnectionStageSourceTests*" --filter-class "*PluginConnectionReportTests*" --filter-class "*OmpPluginCatalogTests*"` | A report with every expected name is Connected; missing names degrade only that plugin and are listed in catalog order; nothing registered fails it; no report in 30 s degrades every stage and a late report recovers it; an oversized or unreadable report is Degraded without parsing; names from the report never reach event text; a session never reads another session's report. |
| Steering inbox | `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*FileSteeringChannelTests*"` and `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*LiveSteeringWiringTests*" --filter-class "*AgentSessionRuntimeTerminalTests*" --filter-class "*CardSteeringTests*"` | The directive file schema and ordering, refusal on a stale heartbeat, the 64-directive queue, the 8192-byte text limit, acknowledgement handling, delivery reported only on an acknowledgement, and the terminal and card entry points (with a fake plugin). |
| Session kinds | `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*AgentSessionKindTests*"` | The registry keeps the chat or terminal kind, loads an older file as chats, and never quarantines it for the missing kind. |
| PTY environment scrub | `dotnet test tests/Autonogrammer.Harness.Terminal.Tests -c Release -- --filter-class "*LinuxLocalPtyBackendEnvironmentTests*" --filter-class "*LinuxLocalPtyBackendTests*" --filter-class "*TerminalSessionOptionsTests*"` | The command gets only `PATH`, `HOME`, `LANG`, `TERM`, `PI_CODING_AGENT_DIR`, `OMP_PROFILE` and the caller's variables (the caller wins); other host variables are unset by name through `/usr/bin/env -u`; an invalid name or value is refused before anything starts and the value is never printed. Linux only. |
| 9 | `./bootstrap.sh`, or `dotnet build Autonogrammer.Linux.slnf -c Release` then `dotnet test Autonogrammer.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"` | Build with warnings as errors, analyzers and every non-live test. |

## Live tests (real omp, no model turn)

Need `omp` on `PATH`, Linux, every plugin submodule initialised and `AUTONOGRAMMER_LIVE_OMP=1`. They are excluded from the gate by the trait.

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 4 to 8 (engine session) | `AUTONOGRAMMER_LIVE_OMP=1 dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release --no-build -- --filter-trait "Category=Live"` | `omp acp` started with the planned arguments and `AUTONOGRAMMER_STEER_DIR`, `AUTONOGRAMMER_CONNECTION_FILE`: within 15 s of `session/new` the connection report exists; `PluginConnectionStageSource` reports Ultrathink, Swarm, Substrate and GSD-Core Connected; the heartbeat is at most 15 s old; report and heartbeat name mode `rpc` and the same pid, which belongs to the run; no `session/prompt` is sent; no omp process of the run remains. | Passed on 2026-10-26 against omp 18.12.6 (`LiveOmpHandshakeTests`). The printed tool and command counts of that run were not copied into this record. |
| 4 to 8 (omp terminal) | `AUTONOGRAMMER_LIVE_OMP=1 dotnet test tests/Autonogrammer.Harness.OmpLauncher.Tests -c Release --no-build -- --filter-trait "Category=Live"` | Interactive omp started by `OmpTerminalLauncher` over `LinuxLocalPtyBackend` with `plan.ForTerminal()` (no `acp`, no `--plugin-dir`, no first prompt; the terminal's `ESC[c` query is answered with `ESC[?62;22c`): within 180 s the report exists; all four plugins Connected; heartbeat fresh, mode `tui`, same pid as the report; two Ctrl+C exit omp with code 0 once the TUI is quiet; no process of the run remains. | Passed on 2026-10-26 against omp 18.12.6 (`LiveOmpTerminalTests`). |
| Steering contract (Phase 49) | the same `Category=Live` filter on `tests/Autonogrammer.Harness.AgentLoop.Tests` (`LiveOmpSteeringContractTests`) | omp advertises `_meta.steering.supported`, authenticates with the `agent` method, and answers an idle `_session/steering` request with `promptRequired`. | Recorded as verified in OQ-81 (`docs/open-questions.md`); the run output is not stored in the evidence folder. |
| OmpConfig (Phase 40) | `AUTONOGRAMMER_LIVE_OMP=1 dotnet test tests/Autonogrammer.Harness.OmpConfig.Tests -c Release --no-build -- --filter-trait "Category=Live"` | The omp configuration library against the real omp. | 4 of 4 passed on 2026-10-26. |

## Probes (evidence of the omp side)

The probes ran real omp 18.12.5 (ACP version string) in throwaway sandboxes under `/tmp`; nothing under `/root/.omp` or the repository was written. Their reports are in `docs/runbooks/evidence/phase-48-omp-plugins-2026-10-26/`:

| File | What it shows |
|------|---------------|
| `exactly-once-loading.md` | Which launch recipes load each plugin once with a polluted global configuration (R1 recommended); with `--no-extensions` omitted swarm and substrate load twice. |
| `omp-terminal-tui.md` | The interactive omp with the planned arguments starts without credentials, shows the plugin status lines and slash commands, and exits on two Ctrl+C about 150 ms apart. |
| `plugin-connection-report.md` | The connection report and heartbeat in ACP (`rpc`) and TUI (`tui`) mode: 28 tools and 425 commands in the TUI report (27 tools in ACP), the eight `ultrathink-*` commands, `swarm` and its eight `swarm_*` tools, `substrate`, `gsd-core`; the TUI shows no text from the Autonogrammer plugin. |
| `acp-auth-and-steering.md` | omp's ACP `initialize` answer (`authMethods` `agent`, `_meta.steering.supported`) and the `_session/steering` outcomes from source. |

To repeat the application screenshots (Xvfb), use the method in `docs/runbooks/evidence/ui-parity-2026-10-26/foundation/README.md`. Waiting matters: omp took up to 50 s to draw in the terminal session on this loaded host.

## Known limits of this verification

- No model turn (OD-07): nothing shows what a plugin does when a model answers, or an `injected` steering outcome.
- Linux only. Windows, macOS, Android and hosted deployments were not run.
- A tight double Ctrl+C (50 ms apart) did not exit omp in one probe run; 150 ms did. The live test waits until the TUI is quiet.
- The Substrate extension files are loaded in the live tests only because the planner is given an endpoint; no Substrate server answers.

Update 2026-10-27: criterion 8 has a recorded run: the four flyout screenshots `app-chat-plugins-flyout-*.png` and `app-terminal-plugins-flyout-*.png` and the "Application evidence" section of the evidence README (live, no model turn; two sessions of one sandbox; Substrate group Degraded with HTTP 403 on the brief).
