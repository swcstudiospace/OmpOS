---
description: "Runbook for Cowork isolation levels, the probe, degraded states and owner actions."
---

# Cowork isolation

Status: Partial. The Linux namespace self-test mounts `/` read-only before `/bin/true`, so a working user namespace is no longer reported as `userns_disabled`. The session wrapper has not passed the protected live test, and no passing live run is recorded. macOS and Windows runs remain unverified.

Last verified: 2026-10-30

Applies to: v0.16.0

## Levels

Code is off and gets no wrapper. Cowork requests isolation. A successful probe alone does not prove that the session wrapper starts or enforces its mounts; the Linux wrapper currently fails the protected live launch test. Windows stays unconfined and says process containment is not applied. Network is unrestricted at every level.

## Probe

`IsolationComposition` runs `IsolationProbe` through `IIsolationProcessRunner` before a Cowork session starts. A failed probe (`bwrap_missing`, `userns_disabled`, `probe_timeout`, `platform_unsupported`) asks Continue without isolation and starts nothing until that answer exists. Consent continues without a wrapper.

## Degraded states

The chip shows the degraded-state sentence and `network: unrestricted`. `~/.omp` stays readable by the agent. Terminals, hooks, remote sessions and Check connection runs are not confined.

## Owner actions

Outstanding checks:

- Run `IsolationLiveTests` with `AUTONOGRAMMER_LIVE_ISOLATION=1` inside an outer Bubblewrap that gives the run a disposable `/etc` (see the live run section below). The test uses a uniquely owned temporary fixture for both writes and requires a successful allowed write and a denied outside write in the same child; a launch failure is not a pass. Repair the wrapper defects listed below first.
- macOS Seatbelt run.
- Windows job-object run, which this phase does not apply.
- `./bootstrap.sh` stays an owner action. It was not run by this plan.

## Live run and open wrapper defects

Outer environment for the live test. It is not verified: the kernel or the outer policy may refuse a nested user namespace, and the outer sandbox is test infrastructure, not a security boundary. Use the host's `/etc/alternatives`, `/etc/passwd` and `/etc/group`; each must exist on the host.

```
bwrap --dev /dev --proc /proc --ro-bind / / \
  --tmpfs /tmp --tmpfs /etc \
  --ro-bind /etc/alternatives /etc/alternatives \
  --ro-bind /etc/passwd /etc/passwd \
  --ro-bind /etc/group /etc/group \
  -- env AUTONOGRAMMER_LIVE_ISOLATION=1 dotnet test --no-build tests/Autonogrammer.Harness.Isolation.Tests --filter "FullyQualifiedName~IsolationLiveTests"
```

The command assumes the test binaries are already built outside the sandbox; a build inside it needs writable binds for `obj/`, `bin/` and the NuGet cache, which are not listed here.

Open wrapper defects were reported from the protected live run that exposed them. Plan 76-09 does not change the session wrapper, and their present status is unverified: writable binds without destination arguments; `/tmp` mounted after temporary-directory grants; denied targets that are absent directories or socket files; deny-mask precedence; socket aliases; and nested read-only grants. Each needs a separately verified design before the live test can pass.

Hosted API hardening is not this isolation. See [the hosted threat model](../security/hosted-threat-model.md).
