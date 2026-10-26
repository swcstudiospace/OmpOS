# Phase 44 criterion 6: live omp handshake, no model turn (2026-10-26)

Criterion: the real `omp acp`, started with the arguments and environment that `OmpLaunchPlanner` plans for the four built-in plugins, loads the plugin commands inside an isolated home, answers `initialize` and `session/new`, and is gone after the close. No `session/prompt` is ever sent, so no model is contacted.

## Test

`tests/OmpOS.Harness.AgentLoop.Tests/Live/LiveOmpHandshakeTests.cs`, class trait `Category=Live` (the gate excludes it).

What it does, in order:

1. Skips with a reason when `omp` is not on `PATH`, when the OS is not Linux (the process tree is found through `/proc`), or when a plugin submodule under `plugins/` is not initialised (the reason carries the fix command).
2. Resolves the real plugins under the repository `plugins/` directory and plans a launch: `Profile = Isolated`, `IsolatedHomeDirectory` inside a fresh temp directory (`HOME` and `PI_CODING_AGENT_DIR` point there, so nothing under the real home is read or written), Substrate disabled, Ultrathink, Swarm and GSD enabled. It adds `ULTRATHINK_UPLIFT=0` to the planned environment.
3. Starts the real omp through `AcpStdioTransport` with the planned arguments and environment, in an empty workspace directory outside every repository.
4. Drives raw frames: `initialize` (protocol version 1), then `session/new` with `mcpServers: []`. Only if `session/new` answers error `-32000` does it call `authenticate` with method `agent` and retry once. It collects notifications for 6 seconds after `session/new`. The production `AcpAgentSession` drops `available_commands_update`, so it is not used. The driver throws if asked to send `session/prompt`.
5. Asserts: the planned arguments begin `--no-extensions -e` and hold no `--plugin-dir`; some omp process of the run has the command line `acp --no-extensions -e ...` and the environment entries `ULTRATHINK_UPLIFT=0` and the isolated `HOME`; `session/new` succeeded with a `sessionId`; the sent methods do not include `session/prompt`; the announced commands include `swarm`, `gsd-core` and at least one `ultrathink-*`.
6. Closes the client (stdin EOF, 2 s grace, then the whole process tree is killed) and polls `/proc` for up to 20 seconds; no process may still carry the run's unique `PI_CODING_AGENT_DIR` entry.
7. Prints the sent methods, the notification count, the distinct command count, the first 40 command names and the pids of the run to the test output.

Expected command names (from `dossier-omp-loading.md`, run `i2`): `substrate`, `swarm` and eight `ultrathink-*` (`ultrathink-off`, `-on`, `-quick`, `-skip`, `-status`, `-track`, `-swarm`, `-ui`) with 471 commands in all; this run omits the substrate projector, so `substrate` is not expected, and adds `gsd-core` from `plugins/gsd-core-omp`.

## Run

Build first, then run only the live trait (repository root, .NET environment set as in `CONTRIBUTING.md`):

```
OMPOS_LIVE_OMP=1 dotnet test tests/OmpOS.Harness.AgentLoop.Tests -c Release --no-build -- --filter-trait "Category=Live"
```

The test project must reference `OmpOS.Harness.Abstractions` and `OmpOS.Harness.Acp` (the test uses `AcpStdioTransport`).

Measured omp startup to `session/new` is 17 to 97 s depending on host load (`AcpLiveSmokeTests`); each request is bounded at 300 s.

## Result

<!-- BEGIN result recorded 2026-10-26 -->
Status: PASSED on 2026-10-26 against the real omp 18.12.6 on Linux, after the test was reworked in Phase 48 (commit `3603ba4`). The reworked test plans all four plugins (Substrate switched on against an address nothing listens on) and, besides the raw frame checks described in steps 4 to 7 above, reads the plugin connection report and the heartbeat: it asserts that all four plugins are Connected, that the heartbeat is fresh and names mode `rpc` with the pid of the run, that no `session/prompt` was sent, and that no omp process of the run remains. The detailed result is in `docs/runbooks/evidence/phase-48-omp-plugins-2026-10-26/README.md` (Live evidence tests).

- Date and host: 2026-10-26, this development host (Linux).
- omp version: 18.12.6.
- Test summary: the live trait run passed. The command exit code and the printed counts (distinct commands, first 40 names, pids, wall time) were not copied into this record.
- Submodule pins: as in `docs/plugins.md`.
- Not verified: a model turn; Windows and macOS.
<!-- END result recorded 2026-10-26 -->
