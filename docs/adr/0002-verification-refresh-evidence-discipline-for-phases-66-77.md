# ADR-0002. Verification refresh evidence discipline for phases 66-77

Status: accepted
- **Date:** 2026-11-22
- **Deciders:** A03
- **Task:** T855b-arch

## Context and Problem Statement

Phases 66-77 VERIFICATION files are stale or gaps_found at base b46fd81f; release 0.20.13 must land docs-only with fresh evidence and all-Green CI

## Decision Drivers

ROADMAP success criteria; NFR-C2 digest compliance; no product code change

## Considered Options

- Refresh VERIFICATION files from fresh evidence runs (chosen)
- Re-assert old verdicts without fresh runs
- downgrade open security findings to pass

## Decision Outcome

Chosen option: "Refresh VERIFICATION files from fresh evidence runs; honest gaps_found with exact closing steps; human-only criteria stay human_verification".

### Positive Consequences

- Evidence-first order (runs, then verdicts, then 0.20.13 bump, then PR) gives A05/A06/A07/A08 a single stable input.

### Negative Consequences

- Downstream evidence runs may still record gaps_found; that is honest, not a reopening of the decision.

## Consequences

Evidence, then verdicts, then version bump, then PR; unmeasured NFRs fail closed
