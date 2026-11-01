---
description: "Runbook for Cowork isolation levels, the probe, degraded states and owner actions."
---

# Cowork isolation

Status: Partial (planner and composition are unit tested; live bubblewrap, macOS and Windows runs are owner actions)

Last verified: 2026-10-30

Applies to: v0.16.0

## Levels

Code is off and gets no wrapper. Cowork is on. A successful Linux or macOS probe reaches filesystem isolation. Windows stays unconfined and says process containment is not applied. Network is unrestricted at every level.

## Probe

`IsolationComposition` runs `IsolationProbe` through `IIsolationProcessRunner` before a Cowork session starts. A failed probe (`bwrap_missing`, `userns_disabled`, `probe_timeout`, `platform_unsupported`) asks Continue without isolation and starts nothing until that answer exists. Consent continues without a wrapper.

## Degraded states

The chip shows the degraded-state sentence and `network: unrestricted`. `~/.omp` stays readable by the agent. Terminals, hooks, remote sessions and Check connection runs are not confined.

## Owner actions

These runs were not executed here:

- Live isolation on a host with user namespaces: set `AUTONOGRAMMER_LIVE_ISOLATION=1` and run `IsolationLiveTests`.
- macOS Seatbelt run.
- Windows job-object run, which this phase does not apply.
- `./bootstrap.sh` stays an owner action. It was not run by this plan.

Hosted API hardening is not this isolation. See [the hosted threat model](../security/hosted-threat-model.md).
