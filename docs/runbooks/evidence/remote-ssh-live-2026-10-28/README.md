# Live remote environment run over ssh, 2026-10-28

**Result: passed.** A real `omp acp` was started through a real `sshd` and opened a session; no model was called.

## Method

1. `scripts/remote/loopback-sshd.sh` started `/usr/sbin/sshd` on `127.0.0.1` on a random high port (40491 in this run) with a temporary host key, a temporary client key as the only authorized key, `PasswordAuthentication no`, `UsePAM no`, and printed the `OMPOS_LIVE_ENV` line. The server, the keys and the known-hosts file were deleted when the script exited.
2. `dotnet test --project tests/OmpOS.Harness.Remote.Tests -c Release --no-build --filter-trait "Category=Live"` ran with `OMPOS_LIVE_ENV` exported. Only `RemoteSshLiveTests.ARealOmpOpensASessionThroughARealSshd` is selected by `OMPOS_LIVE_SSH=1`; the live podman test was skipped (`OMPOS_LIVE_PODMAN` unset).
3. Test steps (`tests/OmpOS.Harness.Remote.Tests/RemoteSshLiveTests.cs`): `RemotePreflight` over the live connection had no error diagnosis; `SshAcpTransportFactory` started `ssh ... omp acp --no-extensions` with an isolated `HOME` and `PI_CODING_AGENT_DIR`; `OmpAcpClient.CreateSession` completed the ACP handshake and `session/new`; the advertised capability `loadSession` was `true`, which is what the reconnect path (`session/load`) needs.

## Outcome

```
Test run summary: Passed!
  total: 2   failed: 0   succeeded: 1   skipped: 1   duration: 1m 36s
```

## What this proves and what it does not

- Proves: `SshCommandBuilder` quoting, the BatchMode and strict host-key arguments, the stdin bootstrap, banner tolerance and the stdio tunnel work against OpenSSH 9.6 and omp 18.12.6 on Linux; the preflight reports no error against a healthy host.
- Does not prove: a prompt turn (needs a model credential, OD-07), a host over a real network, Windows or macOS clients, ssh agent forwarding, reconnect after a dropped connection against a real host (covered by fake-ssh tests only), or the remote file view and terminal against a real host.
