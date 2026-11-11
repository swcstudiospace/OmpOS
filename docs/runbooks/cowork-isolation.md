---
description: "Runbook for Cowork isolation levels, the probe, degraded states and owner actions."
---

# Cowork isolation

Status: Partial. The Linux namespace probe is fixed locally, but the real Bubblewrap wrapper fails its positive-control launch; do not treat it as verified filesystem isolation. macOS and Windows runs remain unverified.

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

- Repair the Linux wrapper, then run `IsolationLiveTests` with `AUTONOGRAMMER_LIVE_ISOLATION=1` in a safe disposable environment. The test uses a uniquely owned temporary fixture for both writes and requires a successful allowed write and a denied outside write in the same child; a launch failure is not a pass.
- macOS Seatbelt run.
- Windows job-object run, which this phase does not apply.
- `./bootstrap.sh` stays an owner action. It was not run by this plan.

Hosted API hardening is not this isolation. See [the hosted threat model](../security/hosted-threat-model.md).
