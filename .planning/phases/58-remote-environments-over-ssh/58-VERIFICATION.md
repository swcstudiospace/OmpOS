# Phase 58 Verification: Remote Environments over SSH

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `58-SUMMARY.md`. A row marked "no recorded run" has a command but no result. The unit tests are hermetic: a fake `ssh` and a fake `omp` (POSIX `sh` scripts under `tests/Autonogrammer.Harness.Remote.Tests/Fixtures`), headless Avalonia, no network. The fake scripts need `sh`, `sed`, `stat -c`, `mktemp` and GNU coreutils, so the tests are Linux only in practice. Only the live run used a real `sshd` and the real omp 18.12.6, and it sent no prompt.

Recorded totals (recorded 2026-10-28, orchestrator): Remote 520+ (2 skipped as root), Presentation 2303, Native 2012 (2 skipped), Architecture 46, Contracts 137 passed on the final tree. The full `./bootstrap.sh` gate on a clean worktree of commit e24e6851 exited 0 (recorded 2026-10-28: 12215 tests, 12213 succeeded, 0 failed, 2 skipped; format clean; repository checks and licence check OK); release head 9d4e0075 is tagged v0.15.0, later commits are documentation only. GitHub `ci` runs 40758215402 (main) and 40758275399 (tag) failed at 'Start test services' (rootless podman cannot create a user namespace in the runner container, OQ-168); the local gate is the gate of record. The commands below were not re-run by the author of this record.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 | `dotnet test tests/Autonogrammer.Harness.Remote.Tests -c Release -- --filter-class "*SshConfigReaderTests*" --filter-class "*SshConfigResolverTests*" --filter-class "*RemotePreflightTests*" --filter-class "*RemoteDiagnosisCatalogTests*" --filter-class "*EnvironmentProfileValidatorTests*" --filter-class "*PosixQuotingTests*" --filter-class "*SshCommandBuilderTests*" --filter-class "*CanarySecretTests*" --filter-class "*EnvironmentContractTests*"` | Host aliases and `ssh -G` values become a profile (the real `ssh -G` with Include, Match and ProxyJump when ssh is installed); each missing piece (ssh, host key, omp, directory) is diagnosed with a fix text; hostile values are quoted through a real `sh`; canary secrets are in no argument list, stderr tail, frame preview or message; no contract property can carry a secret | Inside the passing Remote run (2 tests skipped as root, orchestrator fact) |
| 2 | `dotnet test tests/Autonogrammer.Harness.Remote.Tests -c Release -- --filter-class "*SshAcpTransportTests*" --filter-class "*ReconnectingEngineHostTests*" --filter-class "*ReconnectPolicyTests*" --filter-class "*RemoteLaunchMaterializerTests*"` | `omp acp` runs through the fake ssh; a login banner is kept as noise; a mid-turn drop yields an interrupted turn (never a completed one), five attempts at 1, 2, 4, 8 and 16 s on a manual clock, then `session/load` with the stored id | Inside the passing Remote run |
| 2 (live) | `scripts/remote/loopback-sshd.sh` prints an `AUTONOGRAMMER_LIVE_ENV` line; export it, then `dotnet test --project tests/Autonogrammer.Harness.Remote.Tests -c Release --no-build --filter-trait "Category=Live"` | A real `omp acp` starts through a real `sshd` (OpenSSH 9.6) on loopback, the preflight has no error, the ACP handshake and `session/new` complete, `loadSession` is advertised true | `Test run summary: Passed! total: 2 failed: 0 succeeded: 1 skipped: 1`, 1 m 36 s (`docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md`); the skipped test is `PodmanLiveTests` |
| 3 | `dotnet test tests/Autonogrammer.Harness.Remote.Tests -c Release -- --filter-class "*RemoteWorkspaceFileSystemTests*" --filter-class "*RemoteConventionFileSourceTests*" --filter-class "*RemoteTerminalLauncherTests*" --filter-class "*RemoteExecTests*" --filter-class "*RemoteSessionResourcesTests*" --filter-class "*EnvironmentProfileStoreTests*"` | List, signed read, replace and create over ssh; the remote convention files equal the local ones over every scope; the remote terminal argv and backend; the one-call composer | Inside the passing Remote run |
| 3 | `dotnet test tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*AsyncWorkspaceTreeTests*" --filter-class "*NewSessionTargetModelTests*" --filter-class "*RemoteEnvironmentsViewModelTests*" --filter-class "*RemoteProfileEditorTests*" --filter-class "*RemoteStatusDescriberTests*"` and `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*RemoteViewTests*"` | The async tree opens and expands a remote folder; the picker offers the environment and refuses a blocked one; the Remote page lists config hosts without contacting any, shows test outcomes and findings in words, and every control is named and large enough | Inside the passing Presentation and Native runs |

## Beyond the criteria (containers and WSL)

| Item | Command | What it proves | Recorded result |
|------|---------|----------------|-----------------|
| Container and WSL environments | `dotnet test tests/Autonogrammer.Harness.Remote.Tests -c Release -- --filter-class "*ContainerRuntimeHygieneTests*" --filter-class "*DevContainerResolverTests*" --filter-class "*PodmanContainerListerTests*" --filter-class "*WslPreflightTests*" --filter-class "*ContainerProfileValidatorTests*" --filter-class "*EnvironmentKindWireTests*" --filter-class "*RemoteScriptsBusyBoxTests*"` and `dotnet test tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*RuntimeEnvironmentsViewModelTests*"` | Podman and WSL argument shapes, dev container resolution (never started), unknown environment kinds rejected by the validator, no source file names another container runtime, the busybox shell variant of the remote scripts | Inside the passing Remote and Presentation runs |
| Live podman | `export AUTONOGRAMMER_LIVE_PODMAN=1 AUTONOGRAMMER_LIVE_PODMAN_IMAGE=<image with sh, sed, cat, chmod, mktemp>` then `dotnet test tests/Autonogrammer.Harness.Remote.Tests -c Release -- --filter-trait "Category=Live"` | The container path against a real podman | Passed in the Wave 4 tree against an alpine image (orchestrator fact, recorded 2026-10-28); no log committed |
| Desktop wiring | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*RuntimeEnvironmentsViewTests*"` | The Containers and WSL block of the Remote page | Inside the passing Native run |

## Evidence files

| File | What it shows |
|------|---------------|
| `docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md` | The method, the outcome and what the live run proves and does not prove |
| `scripts/remote/loopback-sshd.sh` | The throwaway sshd used for the live run |
| `docs/runbooks/remote-ssh.md`, `docs/runbooks/remote-containers.md` | The user and operator guides, including the UNVERIFIED items |

## Known limits of this verification

- No prompt turn (OD-07), no real network host, no dropped connection against a real host, no remote file view or terminal against a real host.
- Linux only: no Windows, macOS or Android run. A Windows head rejects a POSIX remote working directory (`AcpAgentSession.cs` line 982).
- UNVERIFIED: `ssh -o RemoteCommand=none -G host` with a configured `RemoteCommand`; `wsl.exe -d <distro> --exec sh -c 'printf "%s\n" "$1"' x 'a b'` on Windows; which omp release first had the ACP behaviour the preflight needs (the 18.12.0 minimum is a guess).
- No log of the per-project runs is committed; the `./bootstrap.sh` log is the orchestrator's.
