# ADR-0015: Remote environments over ssh

- Status: Accepted 2026-10-27 (the default taken for OQ-97; answers the ssh half of OQ-75 and Q-HARN-WS in `docs/open-questions.md`)
- Date: 2026-10-27
- Owner: ompos-maintainers
- Deciders: ompos-maintainers (owner direction of 2026-10-27: remote environments and cloud agents are in the v0.15.0 milestone, PX-15)
- Scope: how a session runs on another machine that the user reaches with their own `ssh`: the transport, the profile model, trust and host keys, credential delivery, the launch of omp there, the connection test, the reconnect model, and what is left for later. Cloud workers are decided in ADR-0020, not here. Research: [09-remote-cloud-plan.md](../plans/v0.15.0-research/09-remote-cloud-plan.md) sections A, B, D, E; [04-omp-cursor-map.md](../plans/v0.15.0-research/04-omp-cursor-map.md) row 15 and section B.

## Context

- `omp acp` is an ACP server over stdio only. omp has no session server, no WebSocket mode and no HTTP mode for agent sessions (04-omp-cursor-map.md B and row 15). The WebSocket stub (`WebSocketOmpTransportStub`) therefore cannot reach omp.
- The only seam between an ACP session and the process is `IAcpTransport` (`src/OmpOS.Harness.Abstractions/IAcpTransport.cs`). `OmpAcpClient`, `InProcessEngineHost.ForAcp` and `EngineComposition` all take a `Func<AcpClientOptions, IAcpTransport>`.
- `AcpStdioTransport` runs `<omp> acp <args>` with a local working directory, inherits the parent environment, and faults on any stdout line that is not a JSON object. A login shell that prints a banner on stdout would fault it, and a remote working directory cannot ride `ProcessStartInfo.WorkingDirectory`.
- `AcpClientOptions.ExtraArguments` and `EnvironmentVariables` say that argv is visible to other processes. A provider token must never be in an argument.
- omp keeps its session files on the host that ran it (`session/load` needs the same host). It runs its tools (bash, edit, read) on the machine where it runs.
- Cursor's own Remote SSH runs the agent loop locally and does file IO remotely (C9 in the research). OmpOS differs on purpose: omp runs on the remote machine, so its tools act there.

## Decision

### D1. Transport: `ssh` stdio, one child per session

A remote session is one `ssh` child process that carries `omp acp` on the remote machine, mirroring one `omp acp` process per session (ADR-0012 D5). OmpOS uses the user's own OpenSSH client: it implements no SSH protocol, installs no agent or daemon on the remote machine and adds no listening port. `SshAcpTransport` (`src/OmpOS.Harness.Remote`) implements `IAcpTransport` directly (the stdio transport inserts `acp` first and takes a local working directory, so it cannot be reused) and is plugged in through `SshAcpTransportFactory.Create(...)`, the `Func<AcpClientOptions, IAcpTransport>` the hosts already take. `AcpClientOptions.FsRoot` stays `null`: the remote omp reads its own disk and is never served the local one.

Differences from the local transport, forced by ssh:

- Lines on stdout before the first JSON object (a login banner, rc-file output) are kept as noise (`PreHandshakeNoise`, bounded to 200 lines and 64 KiB) and do not fault. After the first frame the stream is strict again: a line that is not a JSON object faults with a redacted preview.
- The stderr of ssh and of the remote command goes to a bounded buffer (256 lines) used for diagnoses.
- An ssh failure is explained: exit code 255 and the stderr text become a typed `RemoteDiagnosis` (D7).

### D2. Profile model

`OmpOS.Contracts.Environments` defines `EnvironmentKind` (Local, Ssh, Cloud), `EnvironmentProfile` (id, display name, kind, optional `SshProfile`) and `SshProfile` (alias, hostname, port, user, identity file path, jump hosts, allowlisted options, remote workspace root, remote omp path, agent forwarding, keep-alive). Cloud profile data belongs to ADR-0020 and is added to `EnvironmentProfile` additively.

- A profile holds references only. It has no property that can carry a password, passphrase, key material or token; a reflection test asserts it.
- `SshProfileRules` and `EnvironmentProfileValidator` run before any value reaches an argument list or a remote shell: alias `^[A-Za-z0-9._@][A-Za-z0-9._@-]{0,127}$` (never starts with `-`), host, user and jump-host patterns, absolute POSIX remote paths without control characters or `..`, and an option allowlist (Compression, ConnectionAttempts, ConnectTimeout, ServerAliveCountMax, AddressFamily, IdentitiesOnly, StrictHostKeyChecking limited to `yes` and `ask`, TCPKeepAlive, PreferredAuthentications, UserKnownHostsFile, HostKeyAlias). Nothing that runs a command, adds a forward or turns host key checking off can be set.
- Host aliases come from `~/.ssh/config`. `SshConfigReader` lists concrete `Host` names (following `Include`) for the picker. `SshConfigResolver` resolves an alias with `ssh -G -- <alias>`; ssh itself evaluates `Include`, `Host` and `Match`. Note that `ssh -G` runs `Match exec` commands of the user's own configuration.
- Profiles are stored in the user's settings as JSON through the source-generated `EnvironmentJsonContext`.

### D3. Trust, host keys and authentication

- Every non-interactive ssh call uses `-T`, `-o BatchMode=yes`, `ConnectTimeout`, `ServerAliveInterval` (15 s) and `ServerAliveCountMax=3`, `ExitOnForwardFailure=yes` when a forward exists, `LogLevel=ERROR`, `ForwardAgent=no` unless the profile asks, `ForwardX11=no`, `PermitLocalCommand=no` and `RemoteCommand=none` (a `RemoteCommand` in the user's configuration would otherwise conflict with ours). Connection sharing is off (`ControlMaster=no`) unless a socket directory is configured.
- `StrictHostKeyChecking` is never set by OmpOS and never `no` or `accept-new`. With `BatchMode=yes` an unknown host key makes ssh fail at once; the failure is diagnosed as `HostKeyUnknown`. OmpOS never edits `known_hosts` and never answers a prompt.
- First-time trust happens in an interactive terminal tab: `SshCommandBuilder.BuildHostTrustTerminal` builds `ssh -tt ... -- <host> true` without BatchMode, and the user compares the fingerprint and types `yes`. Later connections use BatchMode.
- Authentication is only what OpenSSH provides (ssh-agent, identity files, certificates, `ProxyJump`, GSSAPI). OmpOS handles no password and no passphrase; a password or passphrase prompt is a failed connection (`AuthFailed`).
- Agent forwarding is off by default; a per-host toggle explains that root on the remote machine can use a forwarded agent. Unattended git on the remote machine should use a deploy key or a short-lived token.
- Finding: a command-line `-o SendEnv=-*` does not clear the `SendEnv LANG LC_*` of the system configuration (checked with `ssh -G` on OpenSSH 9.6: ssh keeps command-line values and then adds the configuration file values). OmpOS therefore does not pass `SendEnv`; ssh may send the locale names the system configuration lists. Nothing OmpOS sends travels that way.

### D4. The remote command

The last ssh argument is one line for the remote login shell: `exec sh -c '<script>'`. The script is POSIX and is single-quoted as a whole, so a login shell that is not POSIX (fish, csh) still hands it to `sh`. Every dynamic value in it (working directory, omp path, arguments, state directory, file paths, marker) is single-quoted by `PosixQuoting.Quote`, which refuses NUL and line breaks. Tests execute the generated line with a real `sh` against hostile values (spaces, quotes, `$()`, backticks, `;`, `&&`, globs) and assert the value arrives as one word and nothing else runs.

The script appends `~/.bun/bin`, `~/.local/bin` and `/usr/local/bin` to `PATH` (a non-interactive ssh session often has a shorter `PATH` than a login shell and omp starts through `#!/usr/bin/env bun`), sets `umask 077`, prepares the per-session directory, consumes the stdin preamble (D5), runs `cd '<root>' || exit 71` and `exec '<omp>' acp <args>`. Exit code 70 means the session directory or temporary file could not be created and 71 means the workspace directory is missing; both map to diagnoses.

### D5. Credential and file delivery: ssh stdin, never argv

Environment variables (provider tokens included) and small files (the settings overlay) are written to the ssh child's stdin before the first protocol frame, in sections that end with a random marker line. The remote script reads the environment section into a `mktemp` file created under `umask 077` (mode 0600), sources it, and removes it before `exec`; file sections are written to their paths (directories mode 0700, files mode 0600). The argument list therefore holds no secret by construction; `SshInvocation.ToString`, `RemoteLaunch.ToString` and `SshStdinPreamble.ToString` print names and sizes only. `SshAcpTransport` also removes known secret values from the stderr buffer, the stdout noise, malformed-frame previews and failure messages. Environment names that would hijack the bootstrap shell (`PATH`, `IFS`, `ENV`, `BASH_ENV`, `LD_*`, `DYLD_*` and similar) are refused. Canary tests assert that a token given to the materializer reaches the remote omp and appears in no argument, printed form, stderr buffer or message.

Provider credentials are delivered this way (BYOK) or the remote machine is already logged in (`omp login` there). Sharing an omp auth broker through `-R` is possible later and not built.

### D6. Launch of omp on the remote machine

`RemoteLaunchMaterializer` rewrites an `OmpLaunchPlan` made for this machine:

- Local plugin paths (`-e <path>`) do not exist there. OmpOS does not copy plugins. An entry under a `RemotePluginMapping` (the user says where the plugin directory is installed remotely) is rewritten; every other entry is left out, `LoadedPluginIds` is emptied when any entry was left out, and a `Degraded` stage event says so. The session runs without those plugins.
- The settings overlay (`--config <local file>`) is read locally, delivered as a remote file in the session directory `~/.ompos/sessions/<id>/`, and the argument points at it.
- MCP servers and environment URLs on this machine's loopback (`127.0.0.1`, `localhost`, `[::1]`) are rewritten to `127.0.0.1:<remote port>` with a reverse forward (`-R 127.0.0.1:<remote>:<host>:<local>`). The remote port is derived from the session id inside 41000 to 42999, so a session keeps its ports across reconnects. Addresses with user information are never forwarded or sent. Non-loopback addresses are left alone.
- Local-only environment variables (`HOME`, `PI_CODING_AGENT_DIR`, the XDG directories, `SUBSTRATE_TOKENS_DIR`) are dropped and reported; values under the local session directory move to the remote session directory.

### D7. Connection test (preflight)

`RemotePreflight` runs `ssh -V` and one short non-interactive remote probe that prints `OMPOS_PROBE key=value` lines (shell arithmetic, home, `uname -sm`, directory exists and writable, `mktemp`, omp path and `omp --version`, bun). It returns a `RemotePreflightReport` of `RemoteDiagnosis` records (kind, severity, summary, fix text, bounded detail): SshMissing, HostUnreachable (also a timeout), HostKeyUnknown, HostKeyChanged, AuthFailed, ShellNotPosix, DirectoryMissing, DirectoryNotWritable, OmpMissing, OmpTooOld (default minimum 18.12.0, configurable; UNVERIFIED which omp release first had the ACP behaviour OmpOS relies on), BunMissing, RequiredToolMissing, and UnsupportedPlatform as a warning (only Linux x86_64 is verified; a Windows remote machine is not supported). It never installs anything. The report types live in Contracts so a settings page can show them without referencing the harness.

### D8. Reconnect

- A dropped ssh ends `ReceiveAsync` with `DisconnectedHarnessException`; the in-flight turn fails. `ReconnectingEngineHost` (`src/OmpOS.EngineHost`) wraps an `InProcessEngineHost` created with the new `resumeAfterFault` option, reports the turn as interrupted (a retryable `ErrorEvent`, never a `CompletedEvent`; event `TurnInterrupted` with the additive `SessionStatus.Interrupted`), waits per `ReconnectPolicy` (1, 2, 4, 8 and 16 seconds, cap 30, five attempts, on an injected `TimeProvider`), and loads the stored engine session with `session/load` through a new ssh connection. After the last failed attempt the state is `Unreachable`; the next prompt starts another round. A connection that dropped while idle is repaired by the next prompt through the same resume path, without an error.
- The first connection is not retried: a failure there is a configuration problem and is reported with its diagnosis.
- A turn that was running is not resumed: omp's behaviour for a client that vanishes mid-turn is UNVERIFIED. `session/load` replays the stored history; the user sends the prompt again.
- Not done: a true detach (the agent keeps running while the laptop sleeps). A supervisor with a socket bridge on the remote machine would be needed. omp may keep running after the ssh child dies; whether it exits on stdin EOF is UNVERIFIED (resolve with the live test below).

### D9. Heads

SSH environments are desktop-only: the web head cannot spawn ssh (it attaches to cloud environments through the API). `AcpAgentSession` requires `AcpClientOptions.WorkingDirectory` to be fully qualified for the local OS, so on Windows a remote POSIX directory such as `/home/dev/work` is rejected; remote sessions run on Linux and macOS desktop heads until that check allows a remote working directory (recorded as a follow-up in the report; `Harness.Acp` was not changed by this unit).

## What remains

- Remote file system view for the tree, editor and diff (`RemoteWorkspaceFileSystem`, U7) and the remote terminal and omp CLI terminal over `ssh -tt` (U8). Both reuse `SshCommandBuilder` and `PosixQuoting`.
- Settings page for remote environments (profiles list, test connection, trust terminal, diagnoses).
- Plugin sync to the remote machine, devcontainers, an omp auth broker through `-R`, a detach supervisor, ControlMaster connection sharing (built, not probed against a real server), and the Windows head.
- Railway sandboxes or workers as ssh hosts (E15) are just profiles once reachable; cloud workers with the bridge are ADR-0020.

## Consequences

- No new native dependency: ssh is the user's. A host without OpenSSH has no remote environments (diagnosis `SshMissing`).
- The remote omp runs commands with the ssh user's rights. The ACP permission flow (deny by default except edit, delete, move, execute, read, search) applies unchanged; the trust boundary is the remote user account.
- Secrets are file-in-memory on the remote machine for a few milliseconds (a 0600 file) and in the process environment of omp for the session; they are not on any command line.
- `SessionStatus` gains `Interrupted` (additive). The hosted API enum and `docs/api/openapi.json` change when regenerated, and the contract version policy of ADR-0008 and `ContractVersion` are decided by the orchestrator.

## Alternatives rejected

- An SSH client library (SSH.NET or similar): adds a protocol implementation to audit and bypasses the user's ssh configuration, agent and `ProxyJump`. Rejected; `ssh -G` already evaluates the user's configuration.
- sshfs/FUSE for files: not on Windows, an extra install.
- A WebSocket bridge started on the remote host: needs an installed OmpOS component and an open port. That is the cloud worker design (ADR-0020), not this one.
- Importing `omp ssh` host definitions: format undocumented (`ssh.json`); optional later after `omp ssh --help` is read.
- Using omp RPC mode instead of ACP: Q-APPROVAL-ADAPTER keeps ACP.

## Verification

Written and not run by the unit that wrote it (no builds or tests are run by implementation agents): unit tests with a fake `ssh` (`tests/OmpOS.Harness.Remote.Tests/Fixtures/fake-ssh`) and a fake `omp`; a real `ssh -G` test for `Include`, `Match` and `ProxyJump`; the remote bootstrap script was also exercised by hand under `dash` and `bash` (environment delivery, file mode 0600, removal of the temporary file, exact byte round trip) during design. The live test (`Category=Live`, `OMPOS_LIVE_SSH=1`) against `scripts/remote/loopback-sshd.sh` and a real `omp acp` is for the orchestrator; its result and the stdin-EOF behaviour of omp belong in `docs/runbooks/evidence/`.

## Candidate chosen

The user's own OpenSSH client (`ssh`) run as a child process per session: `ssh -G` evaluates the user's configuration, `ssh -T host omp acp` carries ACP over stdio, and OmpOS adds no SSH library (decisions D1 to D4; alternatives in the rejected section above).

## Licence

OmpOS adds no package for this record. Components it relies on:

| Component | SPDX | Source | Verified |
| --- | --- | --- | --- |
| OpenSSH client (the user's installed `ssh`, not bundled, not linked) | BSD-2-Clause and ISC among others; the OpenSSH LICENCE file lists the mix | https://github.com/openssh/openssh-portable/blob/master/LICENCE | UNVERIFIED: the file was not re-read by the fix-up agent |
| `OmpOS.Harness.Remote` and `OmpOS.Harness.Remote.Tests` | AGPL-3.0-or-later | https://www.gnu.org/licenses/agpl-3.0.html | OmpOS-owned code, the project licence of milestone v0.15.0 (ADR-0018) |
| `Microsoft.Extensions.Logging.Abstractions` (existing central package) | MIT | https://github.com/dotnet/runtime/blob/main/LICENSE.TXT | Already in `Directory.Packages.props` |

## Verification route

`dotnet test --project tests/OmpOS.Harness.Remote.Tests -c Release --no-build --filter-not-trait "Category=Live"` runs the fake-ssh and fake-omp tests and the real `ssh -G` tests. The live test is `OMPOS_LIVE_SSH=1 dotnet test --project tests/OmpOS.Harness.Remote.Tests -c Release` against `scripts/remote/loopback-sshd.sh`; its result belongs in `docs/runbooks/evidence/`. The layer and reference rules are checked by `tests/OmpOS.ArchitectureTests`.

## Open risks

- ControlMaster/ControlPersist with `-T` was built but not probed against a real server; a persisting master could keep stdout open and stall a read.
- The stdin-EOF behaviour of a remote `omp acp` is unprobed.
- `session/load` against a real remote omp after a reconnect is unprobed.
- The Windows desktop head requires a fully qualified working directory in `AcpAgentSession`, which a remote path such as `/home/dev/work` fails; a Harness.Acp change is needed there.
- The remote user account is the trust boundary: omp commands run with that user's rights.
