# Phase 35 Context: Code Quality Bar and the Cursor Parity Audit

Milestone v0.12.0. Requirements CX-06, CX-07, CX-08 and AL-20 (criterion 4). Depends on Phases 20 to 33 (the audit covers the final tree). Related records: `.planning/phases/25.1-codebase-quality-audit/25.1-FINDINGS.md`, `.planning/phases/25.9-suppression-and-skip-review/`, `docs/quality/quality-bar.md`, `docs/decisions/`, `docs/open-questions.md`.

This context was written after the work (2026-10-27) from the commits `d963b4d`, `e0db157`, `38086c8` and `7a91ec3`, the code, the tests and the audit document. The phase ran in pieces: the gate and decision record work landed in the commits above (numbered 35-01, 25-09 and 35-03), and criterion 4 was written last, as one document, against the final tree `d75fd88`. The folder is named for what the phase finally holds, the Cursor parity audit and the quality bar, and not for its roadmap title.

## Goal

The codebase meets an enterprise and production bar, measured by repo-enforced checks, with every audit finding fixed or recorded, and the Cursor-clone inventories are audited row by row against the final tree.

Success criteria are in `.planning/ROADMAP.md` (Phase 35, criteria 1 to 4). The criteria table with status is in `35-SUMMARY.md`.

## Decisions

| ID | Decision | Where it is in the code and tests |
|----|----------|-----------------------------------|
| D35-1 | One pinned secret scan runs from `bootstrap.sh` and from CI. The gitleaks version and its SHA-256 live in one file; the scan fails closed (a tarball is used only when its digest matches, a download that is not possible fails the scan). The one escape is `AUTONOGRAMMER_SKIP_SECRET_SCAN=1`, which warns, and CI never sets it. | `scripts/ci/secret-scan.sh`, `scripts/ci/gitleaks.env`, `bootstrap.sh` (line 49), `.github/workflows/ci.yml` (line 102), `GateParityTests` (`BootstrapAndCiRunTheSameSecretScanAndVersionPin`, `GitleaksPinFileCarriesAVersionAndADigestAndNoOtherAssignment`, `SecretScanFailsClosedAndOnlyAnExplicitSkipDisablesIt`) |
| D35-2 | The pinned secret scan found three `generic-api-key` matches, all canary literals in tests (fake tokens that prove a secret never reaches a log, an exception or a serialised form). They are allowlisted by fingerprint in `.gitleaksignore`, not by path; history is not rewritten. | `.gitleaksignore`, `e0db157` |
| D35-3 | Every suppression and skip marker has a recorded decision, and the gate keeps the ledger in step with the tree: a suppression with no kept row, or a kept row with no suppression, fails the build. | `SuppressionLedgerTests`, `SuppressionScanner`, `docs/quality/quality-bar.md` section 2, SUP rows of `docs/open-questions.md`; Phase 25.9 |
| D35-4 | Two decision records close the gaps CX-08 named: plugin isolation (ADR-0013) and the MCP client (ADR-0014). Both stay Proposed until their open points have answers (OQ-84, OQ-52). A test checks that every record names its candidate, licence, source, verification and risks, that the index lists every record, and that each required topic has one. | `docs/decisions/ADR-0013-plugin-isolation.md`, `ADR-0014-mcp-client.md`, `DecisionRecordTests` |
| D35-5 | The two deferred scope items (VS Code extension compatibility; admin, analytics and team controls) ship no stub: no code, view, project file or OpenAPI identifier may name them. Only records under `docs/` and `.planning/` do. | `DeferredScopeTests` |
| D35-6 | Criterion 4 is one document. The audit covers the inventories that already exist (ADE, CX, VX, the 34 rows of `docs/settings-parity.md`); no new inventory is created. Each row has a status word, evidence by file and test name, and a gap. | `docs/quality/cursor-parity-audit-2026-10-26.md` |

## Out of scope

- A new per-file review of the final tree. The repository-wide review of Phase 25.1 has baseline `59e9dd1`; this phase did not repeat it (criterion 1 stays partly met).
- Fixing any gap the audit lists. The audit records them; the phases that own them stay open.
- Running `./bootstrap.sh`, or any build or test, to write the records.
