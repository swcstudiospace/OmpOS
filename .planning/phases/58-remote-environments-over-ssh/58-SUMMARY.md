# Phase 58 Summary: Remote Environments over SSH

Status (recorded 2026-10-28): criteria 1, 2 and 3 are met as recorded runs of the Remote, Presentation and Native test projects, and a live run of a real `omp acp` through a real `sshd` on loopback. The live run opened an ACP session; no prompt turn was made (OD-07), the host was not on a real network, and a dropped connection was never exercised against a real host. PX-15 is therefore Partial at the requirement level: the core path is real, the limits are listed below. Nothing ran on Windows, macOS or Android.

The record was written after the work. Statuses use: Met, Met (recorded), Partial, Blocked, Not verified.

Recorded test results (recorded 2026-10-28, orchestrator): Remote 520+ (2 skipped as root), Presentation 2303, Native 2012 (2 skipped), Architecture 46, Contracts 137 passed on the final tree. The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`. The live ssh run is in `docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md` (`total: 2 failed: 0 succeeded: 1 skipped: 1`, 1 m 36 s; the skipped test is the live podman test, `OMPOS_LIVE_PODMAN` unset in that run). A live podman test passed in the Wave 4 tree against an alpine image (orchestrator fact; the `RemoteScripts` fix is covered by `RemoteScriptsBusyBoxTests`); no log of it is committed.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Profiles from `ssh -G` and the user's ssh config; preflight diagnoses a missing ssh, host key, omp or directory with fix text; no secret in an argument list | Met (recorded) | `SshConfigReaderTests`, `SshConfigResolverTests` (fake `ssh -G`, and the real `ssh -G` with Include, Match and ProxyJump when ssh is present), `RemotePreflightTests`, `RemoteDiagnosisCatalogTests`, `EnvironmentProfileValidatorTests`, `PosixQuotingTests` (hostile values through a real `sh`), `SshCommandBuilderTests`, `CanarySecretTests`, `EnvironmentContractTests` (reflection: no secret-bearing property) inside the passing Remote run; fake `fake-ssh` and `fake-omp` fixtures under `tests/OmpOS.Harness.Remote.Tests/Fixtures`. The minimum omp version of the preflight (18.12.0) is a guess, configurable |
| 2 | `SshAcpTransport` runs `omp acp` remotely, survives a login banner, reconnects with `session/load`, marks the interrupted turn; fake-ssh tests and a recorded loopback run | Met (recorded) | `SshAcpTransportTests` (`ALoginBannerBeforeTheFirstFrameIsTolerated`), `ReconnectingEngineHostTests` (interrupted turn, retry schedule on a manual clock, `session/load` with the stored id), `ReconnectPolicyTests` inside the passing Remote run; `RemoteSshLiveTests.ARealOmpOpensASessionThroughARealSshd` against OpenSSH 9.6 and omp 18.12.6 (preflight no error, ACP handshake and `session/new`, `loadSession` advertised true). Not proven live: a prompt turn, a dropped connection, the reconnect against a real host |
| 3 | Remote file tree and terminal over ssh; New Session offers the environment; fake-ssh and a headless view test | Met (recorded) | `RemoteWorkspaceFileSystemTests`, `RemoteConventionFileSourceTests` (differential local versus remote), `RemoteTerminalLauncherTests` (argv, PTY-less fake-ssh runs), `RemoteExecTests`, `AsyncWorkspaceTreeTests`, `NewSessionTargetModelTests`, `RemoteViewTests` (`ThePickerChoosesValidatesAndRaisesManage`, the Remote page); desktop wiring in `SessionEngineSet.CreateRemoteResourcesAsync` and `MainView.Shell.cs` (`EnvironmentPickerView`). Not run against a real host |

## What the phase delivered

- Contracts, profile store (`environments.json`), `ssh -G` resolution, preflight with diagnosed findings and fix texts, the ssh ACP transport with a stdin secret preamble, the launch rewrite, and the reconnect host with the Interrupted marker (`58-01-PLAN.md`, units 1 to 7).
- The Remote environments page and the New Session environment picker on the desktop head, wired to the session start: a session on an ssh profile builds `RemoteSessionResources` and runs through a `ReconnectingEngineHost` (units 8 and 10).
- Beyond the criteria (ADR-0015 addendum): podman containers (dev containers attached, never started) and WSL distributions with the same transport, files, conventions and terminal, wired into the desktop head by `942d813`; `Settings > Remote` has a Containers and WSL block.
- `scripts/remote/loopback-sshd.sh` (throwaway sshd on a random high port, temporary keys, deleted on exit) and the live test.

## Deviations

- The container and WSL environments are a parallel model over the same profile store (`RuntimeEnvironmentsViewModel`), not an edit of `RemoteEnvironmentsViewModel` (RemoteContainers decision 9).
- `EnvironmentKindJsonConverter` reads an unknown kind as an undefined value that the validator rejects; the original name of an unknown kind is not preserved when an older build rewrites the store. `EnvironmentContractTests.AnUnknownKindIsRejectedWhenReadingJson` was re-expressed for this (RemoteContainers report).
- ADR-0015's addendum is `docs/decisions/addendum-ADR-0015-containers.md` (the `ADR-*` glob of `DecisionRecordTests` would otherwise demand a second number).
- Plugin sync to remote hosts is not built: plugins are mapped or dropped with a Degraded stage.

## Partial

- A remote session was never driven by a prompt on a real host (needs a model credential, OD-07), and never over a real network.
- The reconnect after a dropped connection is covered by fake-ssh tests only.
- The remote file view and the remote terminal were not run against a real host.
- A Windows head cannot start a remote session: `AcpAgentSession` still requires `Path.IsPathFullyQualified(options.WorkingDirectory)` (`src/OmpOS.Harness.Acp/AcpAgentSession.cs` line 982), so a POSIX remote working directory fails there.

## Blocked (OQ/OD)

- Windows heads: the working-directory rule above needs an Acp change and a Windows run (OQ-69 for the host-trust terminal; Windows runner not available).
- A live prompt turn on a remote host: OD-07.
- `wsl.exe` argument fidelity and its error wording on a real Windows machine: UNVERIFIED, `wsl.exe -d <distro> --exec sh -c 'printf "%s\n" "$1"' x 'a b'` must print `a b`.

## Known limits

- OQ-97 is a default, not an answer: ssh stdio is the remote path; omp has no session server. The questions the live run could not settle are proposed in the hand-off.
- `RemoteCommand=none` overriding a configured `RemoteCommand` is UNVERIFIED: `ssh -o RemoteCommand=none -G host` with such a config. `ControlMaster` with `-T` was built but not probed live.
- A loopback MCP server of the local machine is not reachable from inside a container (rootless podman has its own network namespace); it is kept and reported as probably unreachable.
- The remote explorer refreshes on demand; there is no remote watcher.
- The record of the live ssh run is dated 2026-10-28 and ran against OpenSSH 9.6 and omp 18.12.6 on Linux only.
