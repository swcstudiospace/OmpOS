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

<!-- BEGIN to be filled by the run -->
Status: NOT RUN. The author did not execute the test.

- Date and host:
- omp version (`omp --version`):
- Submodule pins (`git submodule status plugins/`):
- Command exit code and test summary line:
- Sent methods:
- `available_commands_update` notifications:
- Distinct commands announced:
- First 40 command names:
- omp pids found during the run:
- Processes still carrying the run's `PI_CODING_AGENT_DIR` after the close (expected none):
- Wall time:
- Deviations or skips (reason):
<!-- END to be filled by the run -->
