# Phase 35 Summary: Code Quality Bar and the Cursor Parity Audit

Status (recorded 2026-10-27): criteria 2 and 3 met; criterion 1 partly met; criterion 4 partly met. The audit exists and covers every row of the inventories; what is missing is a new per-file review of the final tree (criterion 1) and an owner named for each open row (criterion 4). `./bootstrap.sh` was not run for this record.

The record was written after the work. Statuses use: Met, Met (recorded), Partly met, Not verified.

Recorded test results (2026-10-27): whole-solution test runs at `d75fd88` passed (F10, as reported by the main agent: Native 1450, Presentation 1133, Acp 132, EngineHost 46, Architecture 46, Api.IntegrationTests 210, Coordination 32; every other project 0 failures). No log of that run is committed. The audit itself ran one command, `node --test plugins/gsd-core-omp/test/*.test.mjs`: 69 tests, 69 pass, 0 fail (node v24.18.0).

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | A repository-wide review produces per-file findings; each is fixed or recorded with an owner and an open-question link | Partly met | The review is the Phase 25.1 register (`25.1-FINDINGS.md`: 130 rows after reconciliation, 4 High, 35 Medium, 91 Low, High and Medium with dispositions), fix phases 25.2 to 25.9 (25.4 keeps a blocked residual: GHCR `:latest`), and the SUP rows of `docs/open-questions.md`. Its baseline is `59e9dd1`, before Phases 31 to 33 and 36 to 50 landed, and no new per-file review of the final tree was run. The Low rows have no consolidated disposition table. Eight kept SUP rows (SUP-157 to SUP-164) came after the Phase 25.9 table |
| 2 | Analyzer, warnings as errors, formatter, architecture and secret-scan checks are enforced in CI and in bootstrap, and every suppression has a row in `docs/open-questions.md` | Met (recorded) | `Directory.Build.props` (`TreatWarningsAsErrors`, `EnableNETAnalyzers`, `AnalysisLevel` latest-all); `bootstrap.sh` (`-warnaserror` build at line 57, tests at 74, `dotnet format --verify-no-changes` at 75, the secret scan at 49, the plugin node tests at 53) and `.github/workflows/ci.yml` (lines 64, 91, 102, 104); `tests/Autonogrammer.ArchitectureTests` (`ArchUnitLayerTests`, `ProjectGraphTests`, `GateParityTests`, `SuppressionLedgerTests`); the whole-solution runs above. `./bootstrap.sh` itself is not recorded for `d75fd88` |
| 3 | Decision records exist for the editor, terminal, MCP, browser pane and plugin isolation choices | Met | ADR-0007 (editor, Accepted), ADR-0006 (terminal, Proposed), ADR-0010 and ADR-0014 (MCP gateway and MCP client, Proposed), ADR-0009 (browser pane, Accepted for Linux only), ADR-0013 (plugin isolation, Proposed); `DecisionRecordTests` (`EveryDecisionRecordNamesCandidateLicenceSourceVerificationAndRisks`, `ReadmeIndexListsEveryAdr`, `RequiredDecisionTopicsHaveARecord`) |
| 4 | The audit covers ADE 01 to ADE 22, CX-01 to CX-08, VX-01 to VX-10 and the 34-row table; no new inventory is created; each row is verified, planned or recorded with an owner | Partly met | `docs/quality/cursor-parity-audit-2026-10-26.md`: one row per inventory item with status, evidence and gap, and the criteria of Phases 26 to 50 audited the same way (221 rows in the summary). Every row is verified. An owner is recorded only through an OQ, OD or Q id cited in the gap column; 55 of the 83 rows that are PARTIAL, DEFERRED or NOT-STARTED cite none (section 17 of the audit lists them) |

## Other things this phase delivered

| Item | Status | Evidence |
|------|--------|----------|
| Phase 25.9 suppression and skip review | Met | `.planning/phases/25.9-suppression-and-skip-review/` |
| Deferred-scope guard: the two deferred items ship no stub | Met | `DeferredScopeTests` |
| Stale records brought in line with the final tree | Met | Section 18 of the audit lists each correction |

## Not verified (stated, not claimed)

- `./bootstrap.sh` at `d75fd88`, `dotnet format --verify-no-changes` and the web smoke at that commit.
- A new per-file review of the final tree.
- Windows, macOS, Android and hosted deployments.
- A live model turn.

## Open items

1. A per-file review of the code written after the Phase 25.1 baseline (Phases 31 to 33 and 36 to 50), if the owner wants criterion 1 closed.
2. An owner for each of the 55 rows without an open-question link, or a decision that they are plain implementation work of their phases.
3. The 8 kept SUP rows after the Phase 25.9 table (SUP-157 to SUP-164) are in `docs/open-questions.md`; the decision table of `quality-bar.md` has a note, not rows.
4. The ADRs still Proposed: ADR-0006, ADR-0010, ADR-0013, ADR-0014 (OQ-84, OQ-52, OQ-69).
