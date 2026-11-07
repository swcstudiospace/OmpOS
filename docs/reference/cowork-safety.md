---
description: "Reference for Cowork Auto rules, deletion protection, folder grants and the audit of isolation limits."
---

# Cowork safety

Status: Partial (Auto rules, deletion protection and folder grants are implemented; live isolation runs are owner actions)

Last verified: 2026-10-30

Applies to: v0.16.0

## Auto rules

Auto is labelled Auto (rules). It does not trust `readOnlyHint`. A call the rules do not settle still asks.

## Deletion protection

Delete protection asks in every mode, including Skip. The permissions page states that.

## Folder grants

Grants are read through `CoworkFolderGrantSource`. A read-write grant becomes an isolation bind. A read-only grant does not. A grant change applies to the next session.

## Limits

`~/.omp` stays readable by the agent's shell because omp needs that directory writable (D-21). Only the local ACP omp child is confined. Terminals, hooks, remote sessions and Check connection runs are not. Hosted API hardening is an environment allow-list plus a per-session workspace and is not this isolation. See [the hosted threat model](../security/hosted-threat-model.md).

Network is unrestricted. The isolation chip always says `network: unrestricted`.

## Audit

Cowork decisions are recorded on the run-mode audit log when a sink is registered. A failed probe does not start the session until the user answers Continue without isolation. Consent continues without a wrapper and reports level None to `CoworkPermissionResolver`.
