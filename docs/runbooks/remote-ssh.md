---
description: "Remote environments over ssh: profiles, preflight, what runs where, reconnect, remote files and terminals, and how to read each diagnosis."
---

# Remote environments over ssh: user guide and runbook

Status: Partial (ssh sessions are wired into the desktop head and a live run against a loopback sshd with the real omp 18.12.6 passed on Linux, recorded 2026-10-28; the remote file explorer and editor, the remote terminal pane, the host trust terminal button, remote hook files and remote mention files are built but not wired; no model turn, no real network host, no Windows or macOS)

Last verified: 2026-10-28

Applies to: v0.15.0

Scope: requirement PX-15 (Phase 58 part 1), decision [ADR-0015](../decisions/ADR-0015-remote-environments.md). A session runs on another machine that you reach with your own `ssh`. This page says what you need, how a profile works, what runs where, what is not there yet, and how to read each diagnosis.

**Status of this release** (recorded 2026-10-28, tree 7535859).

- **Built, wired into the desktop head, proven.** `HeadServices` builds the `EnvironmentProfileStore` and `RemoteEnvironmentsHost`; Settings > Remote environments is the `RemoteEnvironmentsPageView` (rendered in the Xvfb smoke, `docs/runbooks/evidence/desktop-app-2026-10-28/settings-16-remote.png`); the environment picker in the rail sets the target of the next New agent; a session on an ssh profile starts through `RemoteSessionResources` and `ReconnectingEngineHost`, with the connection state on the rail row and an interrupted-turn notice, and remote slash discovery (`RemoteSlashDiscoverySource`). Tests: `RemoteTransportSelectionTests`, `HeadServicesWiringTests`, `RemoteViewTests`, `RuntimeEnvironmentsViewTests`, `AgentSessionRuntimeWiringTests` in `OmpOS.Native.Tests`; `SshCommandBuilderTests`, `SshAcpTransportTests`, `SshConfigResolverTests`, `RemoteWorkspaceFileSystemTests`, `RemoteConventionFileSourceTests`, `RemoteSessionResourcesTests`, `RemoteEnvironmentsHostTests`, `EnvironmentProfileStoreTests`, `ReconnectingEngineHostTests`, `RemoteTerminalLauncherTests` in `OmpOS.Harness.Remote.Tests` (520 or more tests on the final tree, 2 skipped because the run was root).
- **Live proof.** A real `omp acp` 18.12.6 was started through a real `sshd` on `127.0.0.1`, passed preflight and opened a session (`RemoteSshLiveTests.ARealOmpOpensASessionThroughARealSshd`, `docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md`). It sent no prompt.
- **Built, not wired.** The remote file explorer and editor (`RemoteWorkspaceFileSystem` is built and tested; `AgentSessionRuntime.RemoteFiles` is exposed and no explorer reads it); the remote terminal pane (the `RemoteTerminalLauncher` commands are built; no head opens one for an ssh session); the host trust terminal button (`HeadServices.TrustTerminalOpener` is never set, so the page answers that no terminal is available and you run `ssh` once in your own terminal); remote hook files (`NoHookFiles`: a remote session runs without OmpOS hooks) and remote mention files (`RemoteMentionFiles`). The user MCP servers and hook files of this machine are skipped for a remote session.
- **Not live-verified.** No model turn over ssh (open question OD-07), no prompt, no reconnect against a real host (fake-ssh tests only), no host over a real network, no ssh agent forwarding, no macOS or Windows desktop head, no Windows or macOS remote.

## What runs where

| Piece | Runs on | Notes |
| --- | --- | --- |
| OmpOS window, Agent Panel, approvals | this machine | The head spawns `ssh`; the web head cannot, so ssh environments are desktop only. |
| `ssh` client | this machine | Your own OpenSSH. OmpOS never implements SSH, never prompts for a password and never edits `known_hosts`. |
| `omp acp`, its tools (bash, edit, read, search) and its session files | the remote machine | Commands run with the remote user's rights. Nothing from this machine's disk is served to it. |
| MCP servers that listen on this machine's loopback (the MCP gateway, the substrate) | this machine | Reached from the remote omp through a reverse port forward (`ssh -R`, remote loopback only). |
| Plugins (`-e` entries) | not copied | Used only where you have installed them on the remote machine and mapped the directory; otherwise the session runs without them and the Agent Panel says so. |

This differs from Cursor's Remote SSH, where the agent loop stays on your machine: here omp itself runs remotely.

## Prerequisites

This machine: OpenSSH client (`ssh` on PATH), Linux or macOS desktop (a Windows desktop cannot start a remote session yet, see Limits).

The remote machine: Linux x86_64 (other platforms give a warning; only Linux x86_64 is verified), a POSIX `sh` and a login shell that can run `sh -c` (bash, zsh or sh; fish or csh as login shell is diagnosed and should be changed for non-interactive use), `mktemp`, omp 18.12.0 or newer with Bun (omp starts through `#!/usr/bin/env bun`), and a directory the user can write to. Install omp and Bun yourself; OmpOS never installs anything.

Authentication: keys in ssh-agent or an `IdentityFile` (certificates and `ProxyJump` work as in your ssh configuration). Passwords and passphrase prompts are not supported because every connection runs with `BatchMode=yes`.

## Create a profile from `~/.ssh/config`

1. Put the host in `~/.ssh/config` as usual:

   ```
   Host build-box
     HostName 10.0.0.5
     User dev
     IdentityFile ~/.ssh/id_ed25519
     ProxyJump bastion.example.com
   ```

2. In Remote environments choose the alias from the list. The list shows concrete `Host` names of `~/.ssh/config` and the files it includes (patterns such as `*.internal` and `Match` blocks are not aliases). OmpOS resolves what ssh would really use with `ssh -G -- build-box`, so `Include`, `Host`, `Match` and `ProxyJump` behave exactly as in a terminal. `Match exec` lines in your configuration run their commands during that lookup.
3. Set the remote workspace root (an absolute path such as `/home/dev/work`), and optionally the remote omp path (an absolute path or a plain command name; the default is `omp`, searched on the remote PATH extended by `~/.bun/bin`, `~/.local/bin` and `/usr/local/bin`).
4. Press Test connection. Fix every error it lists (see Troubleshooting) until it passes.

A custom profile can also set host name, port, user, identity file, jump hosts and a few allowlisted ssh options (Compression, ConnectionAttempts, ConnectTimeout, ServerAliveCountMax, AddressFamily, IdentitiesOnly, StrictHostKeyChecking `yes` or `ask`, TCPKeepAlive, PreferredAuthentications, UserKnownHostsFile, HostKeyAlias). Anything else, such as `ProxyCommand`, `LocalCommand` or a forward, belongs in your own `~/.ssh/config`.

### A profile as stored

```json
{
  "id": "build-box",
  "displayName": "Build box",
  "kind": "Ssh",
  "ssh": {
    "alias": "build-box",
    "remoteWorkspaceRoot": "/home/dev/work",
    "remoteOmpPath": "/home/dev/.bun/bin/omp",
    "forwardAgent": false,
    "keepAliveSeconds": 15
  }
}
```

A profile holds references only: there is no field for a password, a passphrase, a key or a token.

## Part 2 status

| Piece | State | Where |
| --- | --- | --- |
| Remote file system (list, read with a 1 MiB cap, stat, write with a stale-content guard, create, delete) | built, tested (`RemoteWorkspaceFileSystemTests`), scripts also run by hand; **not wired**: no explorer or editor of a session uses it | `RemoteWorkspaceFileSystem` over `RemoteExec`; the explorer models take it through `IAsyncWorkspaceFileSystem` |
| Discovery of slash commands, skills, rules and subagents on the remote workspace | built, tested (`RemoteConventionFileSourceTests`), wired: the remote slash palette uses it | `RemoteConventionFileSource`, `RemoteConventionSources.CreateAsync`, `RemoteSlashDiscoverySource` |
| Remote terminals (login shell, omp CLI, host trust) | built, tested (`RemoteTerminalLauncherTests`, fake ssh, no real PTY); **not wired** to a terminal pane or to the Trust host button | `RemoteTerminalLauncher` |
| Profile store (`environments.json`, atomic, owner-only) | built, tested, wired | `EnvironmentProfileStore` |
| Remote environments page and New session picker | wired: the page is in the Settings window, the picker in the rail | `OmpOS.Presentation.Remote`, `OmpOS.Native.Views.Remote`, `SettingsWindowFactory` |
| Real ssh and real omp | run once on Linux over a loopback sshd (see Status above); no real PTY, no real network host | `RemoteSshLiveTests` |

### Remote files

Every operation is one short POSIX script run through your own `ssh` (`ssh -T`, `BatchMode=yes`, `--` before the host, one single-quoted `sh -c` line; no secret is ever on a command line). Before it touches a path the remote machine resolves the real path of the workspace root and of the path (`realpath`, else `readlink -f`, else `cd -P`) and refuses the operation when the real path is not inside the root, so a symbolic link that leads outside is not listed, not read and not written. Listings use shell globbing, so they work with busybox, dash, bash and zsh (`sh`), with GNU or BSD tools; a directory is cut at 5000 entries. A read keeps at most the cap you give (1 MiB by default) and the remote side stops reading there. A write reads the file and its signature (modification time and size), compares the text with the text you based the edit on, then one script copies the file mode to a temporary file in the same directory, writes the new bytes to it, checks the signature again and renames it over the file: a concurrent change wins, nothing is half-written, a UTF-8 byte order mark is kept. Create uses a no-clobber link or move, so a file that appeared meanwhile is never replaced.

### Convention discovery on the remote workspace

Commands, skills, rules and subagents (the six directories `.ompos`, `.omp`, `.cursor`, `.claude`, `.codex`, `.agents`, at the remote workspace and in the remote home, which is `printf %s "$HOME"` there) are found through the same ssh exec layer as the local ones, with the same precedence and diagnostics. Each folder listing is one ssh call, so discovery bounds itself to depth 3, 300 folders and 1000 entries per folder (`RemoteConventionSources.DiscoveryOptions`). Set `SshClientOptions.ControlPathDirectory` to share one connection and make it much faster. A symbolic link that leaves the root becomes an `EscapesRoot` diagnostic; a file the remote user cannot read an `Unreadable` one; a host that does not answer is a problem in the result, never an exception.

### Remote terminal

Three commands are built for the terminal pane (a local pseudo-terminal running `ssh -tt`; Linux only until OQ-69): a login shell in the workspace (`exec "${SHELL:-/bin/sh}" -l`), the omp command line (`exec omp ...` with the plugin arguments and `--resume <id>` of the same launch an ACP session uses) and the host trust terminal. No head opens them yet: an ssh session has no terminal pane on the desktop. The terminal is interactive, so ssh may prompt in it. A terminal has no private channel: provider tokens and the settings overlay file of the launch are left out (the page lists them), so the remote omp must be logged in on its own (`omp login` there).

### Host key first trust

The first connection to a host is meant to happen in an interactive terminal on purpose: the fingerprint is shown by ssh itself and you decide. The Remote environments page offers "Trust host in a terminal" exactly when the connection test reports an unknown host key; a changed host key offers nothing but the explanation. In this release the button cannot open that terminal (`HeadServices.TrustTerminalOpener` is not set by the shell), so the page tells you that no terminal is available: run `ssh <host>` once in your own terminal and answer the question there.

## First connection and host key trust

A new host fails the connection test with "The host key of this machine is not trusted yet". OmpOS does not trust it for you. Connect once with your own `ssh` in a terminal (the page's Trust host button is meant to open an interactive `ssh -tt` for this, but it is not wired in this release), compare ssh's own fingerprint with the one your administrator published and only then type `yes`. After that, connections run non-interactively with ssh's normal host key checking. If the key later changes, the test reports "The host key differs from the one stored in known_hosts"; do not connect until you know why (`ssh-keygen -R <host>` removes the old entry once you are sure).

## Secrets

- Provider tokens for the remote omp are delivered through ssh's standard input, written by the remote shell to a mode 0600 temporary file, sourced and deleted before omp starts. They are never in a command line on either machine. Alternatively, log in on the remote machine once (`omp login` there) and send nothing.
- Agent forwarding is off. If you turn it on for a host, anyone with root on that host can use your ssh agent as you while the connection is up. Prefer a deploy key or a short-lived token for unattended git on the remote machine.
- The stderr buffer, the failure messages and the diagnoses are scrubbed of the secret values OmpOS delivered.

## Reconnect

If the connection drops while a turn runs, the turn is shown as interrupted (not finished, not failed by the model). OmpOS waits 1, 2, 4, 8 and 16 seconds between five attempts, opens a new ssh connection and loads the stored omp session again. Then send the prompt again: a turn that was running is not resumed. After five failed attempts the host is shown as unreachable; the next prompt tries again. A connection that dropped while you were idle is repaired by your next prompt. omp's own state stays on the remote machine, so the same host must be used. Keep ssh alive with `ServerAliveInterval` (OmpOS sets 15 seconds, three misses). Whether the remote omp exits when ssh dies is not verified; a stale remote process is possible after a long outage.

## Limits

- No Windows remote machine (a POSIX shell is required). A Windows desktop head cannot start a remote session yet: `AcpAgentSession` rejects a working directory that is not fully qualified for the local OS, and `/home/dev/work` is not on Windows.
- The remote file view reads files up to 1 MiB, refuses binary files, and needs `realpath` or `readlink -f` (or a plain `cd -P`) on the remote machine to prove a path stays inside the workspace; without any of them every path through a symbolic link is refused. It has no file watcher: refresh the folder to see remote changes.
- The remote terminal is a local terminal running `ssh -tt`; it needs the Linux PTY backend (OQ-69), so Windows and macOS heads cannot open it, and no head opens it for an ssh session yet. Provider tokens are not sent to it (see "Remote terminal").
- No detach: closing the laptop ends the connection; the turn is interrupted.
- Plugins are not synchronised; MCP servers on a non-loopback address must be reachable from the remote machine themselves.
- Devcontainers and Railway sandboxes as hosts are not designed here. Cloud workers are described in ADR-0020.
- Reverse forward ports are chosen from 41000 to 42999 per session; a port that is already taken on the remote machine fails the connection (`ExitOnForwardFailure=yes`) instead of silently losing the MCP server.

## Troubleshooting by diagnosis

| Diagnosis | Meaning | Fix |
| --- | --- | --- |
| SshMissing | `ssh` cannot be started. | Install the OpenSSH client and put it on PATH, or set its full path. |
| HostUnreachable | Name not resolved, connection refused, timeout, no route, or the test took longer than the limit (60 s). | Check host name, port, VPN; try `ssh <alias> true` in a terminal. |
| HostKeyUnknown | First connection; non-interactive ssh may not ask. | Use the host trust terminal and compare the fingerprint. |
| HostKeyChanged | The key differs from `known_hosts`. | Find out why; `ssh-keygen -R <host>` only when the change is expected. |
| AuthFailed | The host refused what ssh could offer without a prompt. | `ssh-add` your key, or set `IdentityFile` for the host. Passwords are not supported. |
| ShellNotPosix | The login shell did not answer the POSIX probe. | Make the user's login shell bash, zsh or sh for non-interactive sessions. |
| DirectoryMissing | The remote workspace root does not exist. | Create it or change the profile. |
| DirectoryNotWritable | The remote user cannot write there. | Fix permissions or choose a directory you own. |
| OmpMissing | omp not found on the remote PATH. | Install omp, or set the remote omp path to its full path. |
| OmpTooOld | The version is below 18.12.0 (configurable). | Update omp the way you installed it. |
| BunMissing | `omp --version` failed because Bun is not on the remote PATH. | Install Bun as the same user (`~/.bun/bin` is searched). |
| RequiredToolMissing | `mktemp` is missing. | Install coreutils. |
| UnsupportedPlatform (warning) | Not Linux x86_64. | May work; report problems. |
| Unknown | Anything else; the first lines of the tool's output are shown. | Run `ssh <alias> true` and read the message. |

A banner printed by `~/.bashrc` on every non-interactive login is tolerated until the first protocol frame; more than 200 lines or 64 KiB of text before it ends the connection with a message naming the remote rc files.

## Try it without a second machine

`scripts/remote/loopback-sshd.sh` starts a throwaway sshd on 127.0.0.1 (random high port, temporary host key, one temporary client key, `StrictModes no`, `PasswordAuthentication no`, `UsePAM no`) and prints the `ssh -p <port> -i <key> ...` command that logs in plus an `OMPOS_LIVE_ENV` line. It runs until you stop it and removes everything it created. Export the variables of that line and run the live test, which opens a real `omp acp` through the real ssh with an isolated home and sends no prompt (no model is contacted). It was run on 2026-10-28 and passed ([evidence](evidence/remote-ssh-live-2026-10-28/README.md)):

```sh
scripts/remote/loopback-sshd.sh &                      # note the printed OMPOS_LIVE_ENV line
export OMPOS_LIVE_SSH=1 OMPOS_LIVE_SSH_PORT=... OMPOS_LIVE_SSH_KEY=... OMPOS_LIVE_SSH_KNOWN_HOSTS=... OMPOS_LIVE_SSH_USER=...
dotnet test tests/OmpOS.Harness.Remote.Tests --filter-trait "Category=Live"
```

The run of 2026-10-28 is recorded in `docs/runbooks/evidence/remote-ssh-live-2026-10-28/`. Whether the remote omp exits when the ssh connection closes was not measured by it.

## Evidence

| Check | Status |
| --- | --- |
| Unit tests with a fake ssh and a fake omp (quoting, argument list, banner, drop, diagnoses, canary secrets, reconnect schedule) | ran on the final tree with the rest of `OmpOS.Harness.Remote.Tests` (520 or more tests, 2 skipped as root), recorded 2026-10-28 |
| `ssh -G` resolution (`SshConfigResolverTests`) for `Include`, `Match`, `ProxyJump` | part of the same run |
| Remote bootstrap script under `dash` and `bash` by hand | run once during design: environment and file delivery and cleanup worked |
| Remote file and convention scripts by hand on the design host (GNU coreutils): listing under `dash`, `bash` and `sh`; signed read, signature-checked replace and no-clobber create under `dash`, with symlink escape, dangling link, FIFO and hostile names | run once: behaved as designed. macOS and zsh not run; BusyBox is covered by `RemoteScriptsBusyBoxTests` (written after the live podman run found a BusyBox `realpath` defect) |
| Remote file system, convention discovery (differential against the local source), terminal argv, profile store, models and views tests | ran on the final tree |
| Desktop wiring of ssh sessions (transport selection, rail status, interrupted notice) | `RemoteTransportSelectionTests` (4), `HeadServicesWiringTests`, `AgentSessionRuntimeWiringTests`; not exercised against a real omp through the desktop head |
| Live test against a loopback sshd and a real omp | PASSED 2026-10-28 (`docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md`): OpenSSH 9.6, omp 18.12.6, `loadSession` advertised |
| Prompt turn over ssh, real network host, macOS remote, Windows desktop head | NOT RUN (a model turn needs a credential, OD-07) |
