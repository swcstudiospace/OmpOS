# ADR-0015 addendum: container and WSL environments next to ssh

- Status: Accepted 2026-10-28 (an addendum to [ADR-0015](ADR-0015-remote-environments.md); ADR-0015 itself is unchanged)
- Date: 2026-10-28
- Owner: ompos-maintainers
- Scope: how a session runs inside a local podman container (a dev container or any running container) or inside a WSL distribution, with the same ACP transport, files, conventions and terminal as an ssh host. Research: [09-remote-cloud-plan.md](../plans/v0.15.0-research/09-remote-cloud-plan.md) section E, [02-cursor-cloud-remote.md](../plans/v0.15.0-research/02-cursor-cloud-remote.md) section F (rows RD-04 and RD-05). Cloud workers stay in ADR-0020.

## Decision

### A1. Same shape, another client program

ADR-0015 D1 to D9 hold. A container or WSL session is one child process that carries `omp acp` inside the environment, and its stdio is the ACP stream:

| Environment | Client program | Argument list |
| --- | --- | --- |
| ssh host | `ssh` | `-T -o ... -- <destination> 'exec sh -c <script>'` |
| podman container | `podman` | `exec -i [-u <user>] -- <container> sh -c <script>` |
| WSL distribution | `wsl.exe` | `-d <distro> [-u <user>] --exec sh -c <script>` |

`<script>` is the same one-line POSIX bootstrap for all three (`SshCommandBuilder.BuildBootstrap`): extend `PATH`, `umask 077`, read the environment and the small files from stdin into a mode-0600 temporary file, `cd` to the workspace (exit code 71 when it is missing), `exec omp acp`. The quoting is `PosixQuoting`; the arguments are a list and never pass a local shell. Podman and wsl.exe receive the script as one argument, so no extra layer of quoting exists (ssh needs one because the remote login shell parses the line).

`SshAcpTransport` is reused unchanged for framing, noise tolerance, the stderr buffer and redaction. It takes an internal `ExecTransportFlavor` that names the client in messages and chooses the failure rules; `PodmanAcpTransport` and `WslAcpTransport` are thin public wrappers, and `PodmanAcpTransportFactory` / `WslAcpTransportFactory` give the `Func<AcpClientOptions, IAcpTransport>` the hosts already take.

### A2. The exec layer becomes an interface

`IRemoteExec` (`RunAsync`, `ResolveHomeDirectoryAsync`, `WorkspaceRoot`) is the seam under the remote file system and the convention discovery. `RemoteExec` (ssh) implements it; `PodmanRemoteExec` and `WslRemoteExec` are the new implementations. `RemoteWorkspaceFileSystem`, `RemoteConventionFileSource` and `RemoteConventionSources` take `IRemoteExec`, so a container serves its files, its conventions and the stale-content guard with the very scripts the ssh host uses. All three share one process runner (`RemoteExec.RunInvocationAsync`). Podman's own failure exit code 125 is reported as 255 so the shared layers read it as "the environment itself failed", as they read ssh's.

### A3. Profiles

`EnvironmentKind` gains `Container = 3` and `Wsl = 4` (appended; existing numbers never change). `EnvironmentProfile` gains `Container` (`ContainerProfile`: container name or id, remote user, workspace folder, optional podman path, optional omp path) and `Wsl` (`WslProfile`: distribution, user, workspace path, optional omp path). Each kind carries its own details and no other kind's. Names are checked by regex (`[A-Za-z0-9][A-Za-z0-9._-]{0,127}` for a container, 64 for a distribution), paths must be absolute POSIX paths without `..` or control characters, and the podman path must be a plain command name or a full path. Because arguments are a list and the dynamic parts of the script are single-quoted, no shell-metacharacter rule is needed beyond refusing what could be read as an option. There is no field for a secret and no field for a container runtime: the runtime is podman only (the build bans the alternatives). The kind is read by name through `EnvironmentKindJsonConverter`: an unknown name or number deserializes as an undefined kind that the validator rejects, so a profile written by a newer build never makes the whole store unreadable and never runs somewhere else (the word the newer build used is not preserved when this build rewrites the file).

### A4. Loopback services are not forwarded

An ssh session carries local loopback MCP servers to the remote machine with `ssh -R`. A container or distribution has no ssh connection to carry a forward (a rootless container has its own network namespace; WSL reaches the Windows loopback only in mirrored mode). `RemoteLaunchOptions.CarryLoopbackServices = false` (set by `ContainerSessionResources` and `WslSessionResources`) keeps a loopback address as it is, adds no forward and reports a degraded step: "unlikely to be reachable". The launch builders refuse a launch that still carries a reverse forward.

### A5. Connection test

`ContainerPreflight` checks, each with a typed `RemoteDiagnosis` and fix text: podman present (`podman --version`), the container exists and its state (`podman container inspect --format '{{.State.Status}}'`), the user exists, the workspace folder exists and is writable, omp and Bun, the platform. A stopped container gets the `podman start <name>` fix; `StartAsync` runs `podman start` only when the user pressed Start container, and the test never starts anything. `WslPreflight` checks wsl.exe, the distribution (`wsl.exe --list --quiet`, UTF-16 text read tolerantly), the user and the same probe. New diagnosis kinds (appended): `ContainerRuntimeMissing`, `ContainerRuntimeUnavailable`, `ContainerMissing`, `ContainerNotRunning`, `ContainerUserMissing`, `WslMissing`, `WslDistroMissing`, `WslUserMissing`; the wording for the kinds shared with ssh (directory, omp, Bun) names the container or distribution (`ContainerDiagnosisCatalog`).

### A6. Dev containers

`DevContainerResolver` reads `.devcontainer/devcontainer.json` (or `.devcontainer.json`; JSON with comments and trailing commas) for the name, `workspaceFolder` (with `${localWorkspaceFolderBasename}` and `${localWorkspaceFolder}` filled in; default `/workspaces/<folder name>`), `remoteUser` (then `containerUser`) and how the container is made, then asks `podman ps -a --filter label=devcontainer.local_folder=<folder> --format json` for the containers the devcontainer CLI labelled with that folder. Outcomes: found (one running container gives the profile), not created, not running, ambiguous, runtime problem, invalid or missing definition. **Starting a dev container from its definition (`devcontainer up`) is out of scope**: OmpOS never builds, creates or starts a dev container on its own; the answer says "start it with the devcontainer CLI or podman, then attach".

### A7. Presentation

Framework-neutral models in `OmpOS.Presentation.Remote`: `RuntimeEnvironmentsViewModel` (rows, forms, test, start), `RuntimeEnvironmentRow`, `ContainerProfileEditor`, `WslProfileEditor`, `RunningContainerPicker` over the `IContainerLister` seam (`podman ps --format json`, only identities kept, never the environment), `IContainerEnvironmentsHost`, three new statuses (`RuntimeUnavailable`, `NotRunning`, `NotFound`), and container and WSL options in `NewSessionTargetModel` (WSL only where the head says it exists). Views for these models are a later wiring item.

## What remains

- A Windows head cannot start a WSL session until the ACP working-directory check accepts a POSIX path (the open question of ADR-0015); the WSL argument shapes are covered by tests that run on any system, the real wsl.exe is UNVERIFIED (`wsl.exe -d <distro> --exec sh -c 'printf "%s\n" "$1"' x 'a b'` must print `a b`).
- Views, registration and the New session wiring (see the wiring spec in the report of this work).
- Attaching to a container through a remote podman socket, and containers on an ssh host (podman over ssh), are not built.
- Live podman test: written (`OMPOS_LIVE_PODMAN=1`), not run.

## Consequences

- One transport, one bootstrap and one exec seam serve ssh, podman and WSL; a fix to quoting, secrets or noise handling applies to all three.
- The environment kind enum grows by append only; a store written by a build with more kinds still reads.
- A secret never reaches a podman or wsl.exe argument: the same canary tests as for ssh run against both.

## Alternatives rejected

- A second transport class per client: duplicates 500 lines of framing and redaction.
- Running omp on this machine with its file view redirected into the container: the tools (bash, edit) would act on the wrong machine, as ADR-0015 rejected for ssh.
- Starting or building dev containers from OmpOS: needs the devcontainer CLI, builds images and runs arbitrary lifecycle scripts; the user keeps that step.
- A runtime option: the project uses podman only.
