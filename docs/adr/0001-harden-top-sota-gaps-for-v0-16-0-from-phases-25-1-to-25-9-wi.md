# ADR-0001. Harden top SOTA gaps for v0.16.0 from phases 25.1 to 25.9 without new conventions

- **Status:** accepted
- **Date:** 2026-11-20
- **Deciders:** A03
- **Task:** T77d9-arch

## Context and Problem Statement

Phases 66-78 close on origin/main 434e214e with no phase verified by a gate run; requirements forbid new conventions and scope shrink, and proof runs in CI only (no local SDK).

## Decision Drivers

- Stack bar in PROJECT.md/STACK.md is fixed; Docker/Newtonsoft/FluentAssertions-8x stay prohibited

## Considered Options

- Expand scope with new testing/a11y tooling
- shrink scope by deferring phases
- reuse 25.1-25.9 hardening (chosen)

## Decision Outcome


### Positive Consequences

- One blueprint, one contract version and one hardening bound for all of A05/A06/A07; no integration surprises.
- Quality work reuses already-audited lenses, so reviewer and maintainer context stays valid.

### Negative Consequences

- The 80 missing operationIds remain until A05 lands the additive 1.1.0 change; the contract check keeps reporting `fail` on structure (breaking stays empty).
- Phase 72 stays blocked; this ADR does not release it.

## Consequences

A05/A06/A07 implement only within the bound; GAP-OP-1 (80 missing operationId) tracked as additive 1.1.0 work; no novel infrastructure so this stays L2.
