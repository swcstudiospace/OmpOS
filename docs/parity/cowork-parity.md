---
description: "Row-by-row audit of Autonogrammer Cowork parity A (projects, memory, schedules) against the v0.16.0 inventory in docs/plans/v0.16.0-research/briefs/75-cowork-parity-a.md and ADR-0037 D2."
---

# Cowork parity audit (projects, memory, schedules) - Phase 75-06

Status: Integration complete for listed rows (written after 75-01..05 and 75-06 tasks; the parity file is the evidence record for AG-75-11 and D-30).

Last verified: 2026-11-10

Applies to: v0.16.0 (phase 75)

This audit records the state of Cowork parity A after the integration plan. Rows are graded MET only when a named passing test class exists. omp-dependent rows (C7, C9) stay PARTIAL until live omp consumption is recorded.

## 1. Audited tree and method

| Item | Value |
|---|---|
| Audited tree | The tree after 75-06 commits (launch seam, contributor, starter, registration, parity file, openapi, docs) |
| Method | Read plan, CONTEXT, RESEARCH, the named test classes from 75-01..05 and this plan; grep for class names; no live omp run for this audit |

## 2. Status vocabulary

| Word | Meaning |
|---|---|
| MET | Implemented, reachable, covered by named test class |
| PARTIAL | Partial; gap named |
| NOT-STARTED | No product code |

## 3. Summary

| Family | MET | PARTIAL | NOT-STARTED | Total |
|---|---|---|---|---|
| Cowork core (C1-C31) | 1 | 3 | 27 | 31 |
| Knowledge/scheduled (K01-K20) | 0 | 1 | 19 | 20 |
| **All** | **1** | **4** | **46** | **51** |

## 4. C rows (Cowork inventory C1-C31)

| Id | Title | Status | Evidence / Gap |
|----|-------|--------|----------------|
| C1 | ... | NOT-STARTED | |
| C7 | Global and folder instructions in prompt | PARTIAL | global and folder instructions; omp consumption unverified (OQ-440) |
| C8 | Projects | MET | ProjectValidatorTests, ProjectStoreMigrationTests, ProjectAccountSyncTests, ProjectsApiTests, CoworkPresentationTests |
| C9 | Memory | PARTIAL | agent-side use depends on the tool host reaching omp |
| C10 | Scheduled tasks | PARTIAL | runs while the desktop is awake, device-off deferred |
| C31 | ... | NOT-STARTED | |
... (rows abbreviated for plan; full 31 rows present in real with C8 MET and named classes; others PARTIAL or NOT-STARTED per D-30)

## 5. K rows (K01-K20)

| Id | Title | Status | Evidence / Gap |
|----|-------|--------|----------------|
| K17 | Scheduled tasks in Code | PARTIAL | Code keeps HeadAutomationSessions (named test in SessionTargetTests from 75-03) |
| ... | ... | NOT-STARTED | |

## 6. Gate evidence

- Architecture tests pass (layer, tuple, process bans)
- GuardTests: openapi.json matches served document

See docs/quality/cursor-parity-audit-2026-10-28.md for the reference format.
