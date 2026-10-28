# Runbook: containers and WSL as environments

Status: written 2026-10-28 (ADR-0015 addendum). Everything below is built and unit-tested against fake `podman` and `wsl` scripts; a real podman run is the Live test `PodmanLiveTests` (`OMPOS_LIVE_PODMAN=1`), not run yet. The desktop head is wired: the Remote page has a Containers and WSL section (rows with Test, Start container, Edit and Delete; add a running container from podman's list, by name, or a WSL distribution on Windows), the New session picker lists these profiles (WSL only where it is supported, which is Windows), and a session on a container or distribution starts omp through `ContainerSessionResources` or `WslSessionResources` like ssh does through `RemoteSessionResources`. Such sessions get no desktop MCP servers and no local hook files. The terminal pane and the dev container attach button are not wired.

## What runs where

| Piece | Runs on | Notes |
| --- | --- | --- |
| OmpOS window, Agent Panel, approvals | this machine | The head spawns `podman` or `wsl.exe`; the web head cannot. |
| `podman` / `wsl.exe` | this machine | Your own client. The container runtime is podman only. |
| `omp acp`, its tools (bash, edit, read, search) and its session files | inside the container or the distribution | Commands run with the rights of the chosen user. |
| MCP servers on this machine's loopback | this machine | Not reachable from inside: they are kept and reported as probably unreachable (no `ssh -R` exists here). Start such a server inside, or use an address the environment can reach. |
| Plugins (`-e` entries) | not copied | As for ssh: only plugins installed inside and mapped are used. |

OmpOS only attaches. It never builds, creates or starts a container on its own (the one exception is the Start container button, `podman start`, which you press), never installs omp, and never stores a secret.

## Prerequisites

- Container: podman on PATH (or its full path in the profile), a running container, omp 18.12.0 or newer and Bun inside it (omp starts through `#!/usr/bin/env bun`; OmpOS also looks in `~/.bun/bin`, `~/.local/bin` and `/usr/local/bin`), `sh` and `mktemp`, and a workspace folder inside the container that the chosen user can write.
- WSL: Windows with `wsl.exe`, an installed distribution with the same tools. Not usable on Linux or macOS. A Windows head cannot start the session yet (the ACP working-directory check wants a local path; see the limits).

## Add a container

1. Start the container yourself (podman or the devcontainer CLI). `podman ps` must list it.
2. In Remote environments choose Add a running container. The list shows the running containers with their image; a container made by the devcontainer CLI is marked "dev container", one you already added is marked "already added". Select one and press Add.
3. Check the form: the container name, the user (empty uses the container's default user), the workspace folder inside the container (for a dev container the folder is pre-filled with `/workspaces/<project folder name>`), and optionally the podman path on this machine and the omp path inside.
4. Save, then press Test connection and fix every error it lists (see Troubleshooting).

A container can also be added by name (Add a container by name) when it is not running yet.

### A profile as stored

```json
{
  "id": "container-devbox",
  "displayName": "Dev box",
  "kind": "Container",
  "container": {
    "container": "devbox",
    "remoteUser": "vscode",
    "workspaceFolder": "/workspaces/app",
    "ompPath": "/home/vscode/.bun/bin/omp"
  }
}
```

```json
{
  "id": "wsl-ubuntu-24.04",
  "displayName": "Ubuntu",
  "kind": "Wsl",
  "wsl": { "distro": "Ubuntu-24.04", "user": "dev", "workspacePath": "/home/dev/app" }
}
```

A profile holds references only: there is no field for a password, a token, an environment or another container runtime. A kind written by a newer OmpOS (an unknown name or number) is kept in the file and shown as unusable instead of making the page unreadable.

## Dev containers

OmpOS reads `.devcontainer/devcontainer.json` (or `.devcontainer.json`; comments and trailing commas are allowed) and finds the container the devcontainer CLI created for the project folder through its `devcontainer.local_folder` label (`podman ps -a --filter label=devcontainer.local_folder=<folder>`).

| Outcome | What OmpOS says |
| --- | --- |
| One running container | Offers the profile (name, `remoteUser` or `containerUser`, `workspaceFolder`). |
| No container | "No container exists for this project yet ... start it with the devcontainer CLI or podman, then attach." OmpOS never runs `devcontainer up`. |
| Stopped container | The `podman start <name>` fix and the Start container button. |
| Several running containers | Pick one from the list of running containers. |
| No definition | Add the container by name instead. |
| Unreadable definition, or a `workspaceFolder` with a variable OmpOS cannot resolve | The text names `devcontainer.json` and what to set. Only `${localWorkspaceFolderBasename}` and `${localWorkspaceFolder}` are filled in. |

## Add a WSL distribution (Windows)

Add a WSL distribution, enter the distribution name exactly as `wsl --list` prints it, an optional user, and an absolute workspace path inside the distribution (for example `/home/dev/app`). On another system the button is replaced by a note and stored WSL profiles are kept but not offered.

## What a session does

`podman exec -i [-u user] -- <container> sh -c '<bootstrap>'` or `wsl.exe -d <distro> [-u user] --exec sh -c '<bootstrap>'`. The bootstrap is the one the ssh session uses: it reads provider tokens and small files from stdin (never from an argument), changes into the workspace folder and starts `omp acp`. The explorer, the editor, slash-command discovery and the terminal use the same exec layer as an ssh host (`IRemoteExec`), so containment, the stale-content guard and the one-line scripts are identical. The terminal is `podman exec -it` or `wsl.exe` in a local pseudo-terminal and carries no secret.

## Secrets

Nothing secret appears in a podman or wsl.exe argument list, in the profile, in a log or in a message: tokens travel on stdin, known token values are removed from stderr, noise and failure texts, and the same canary tests as for ssh run against both clients.

## Troubleshooting by diagnosis

| Diagnosis | Meaning | Fix |
| --- | --- | --- |
| `ContainerRuntimeMissing` | podman is not installed or not startable | Install podman or set its full path in the profile. |
| `ContainerRuntimeUnavailable` | podman is installed but its service does not answer | On macOS and Windows run `podman machine start`; on Linux check the user session and `podman ps`. |
| `ContainerMissing` | No container has this name or id | Check `podman ps -a`; pick a running container from the list. |
| `ContainerNotRunning` | The container exists and is stopped | Press Start container or run `podman start <name>`, then test again. |
| `ContainerUserMissing` | The user is not in the container's passwd | Choose a user that exists, or leave the user empty. |
| `DirectoryMissing` | The workspace folder does not exist inside | Create it or fix the folder; a dev container mounts at `/workspaces/<folder>`. |
| `DirectoryNotWritable` | The user cannot write the folder | In rootless podman your user is root inside; set the user that owns the files. |
| `OmpMissing`, `OmpTooOld`, `BunMissing` | omp or Bun is not inside, or omp is older than 18.12.0 | Install or update them inside (put them in the dev container definition), or set the omp path. |
| `WslMissing` | wsl.exe is not available | Windows only; run `wsl --install` as administrator. |
| `WslDistroMissing` | No distribution has this name | Check `wsl --list --verbose`. |
| `WslUserMissing` | The user is not in the distribution | Choose a user that exists, or leave it empty. |

The exact words wsl.exe prints for a missing distribution or user are UNVERIFIED on a real Windows machine (the rules match "There is no distribution with the supplied name" and "There is no user"; wsl.exe writes UTF-16, which OmpOS reads tolerantly). Resolve with `wsl.exe -d NoSuchDistro --exec true` on Windows and compare.

## Limits

- Windows head and WSL: the ACP client requires a local absolute working directory (`AcpAgentSession`, `Path.IsPathFullyQualified`), so a POSIX workspace path fails on a Windows head until the Acp owner relaxes the check.
- No reverse forwards: loopback MCP servers are not reachable from inside.
- Dev containers are attached, never created.
- A container that exits while a session runs drops the connection: the host reconnects like an ssh drop and the diagnosis says "not running".
- Podman over a remote socket and containers on an ssh host are not built.

## Try it on this machine (podman, no model)

```sh
export OMPOS_LIVE_PODMAN=1
export OMPOS_LIVE_PODMAN_IMAGE=<a fully qualified base image with sh, sed, cat, chmod, mktemp>
dotnet test tests/OmpOS.Harness.Remote.Tests --filter "Category=Live&FullyQualifiedName~PodmanLiveTests"
```

The test starts one container with a unique name, installs the fixture fake omp in it (no real omp, no model), runs the connection test, an ACP session, the file system, the stopped-container path, and removes the container.

## Evidence

Not yet recorded: no test of this work was run when it was written (the fix-up pass compiles and runs them), and the live test needs your image. Real podman 4.9.3 was asked by hand for `podman exec -i -- <container> sh -c ...` (works, stdin reaches the script), `-u <missing user>` (exit 255, "unable to find user ... no matching entries in passwd file"), exec into a stopped container (exit 255, "can only create exec sessions on running containers"), `container inspect --format '{{.State.Status}}' -- <name>`, `start -- <missing>` (exit 125) and `ps --filter label=...`; the fake podman reproduces those words.
