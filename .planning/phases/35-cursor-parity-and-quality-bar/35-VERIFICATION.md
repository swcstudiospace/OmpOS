# Phase 35 Verification: Code Quality Bar and the Cursor Parity Audit

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-27 are in `35-SUMMARY.md`. A row marked "no recorded run" has a command but no result. The commands below were not re-run by the author of this record, except the node suite.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 | Read `.planning/phases/25.1-codebase-quality-audit/25.1-FINDINGS.md` and `docs/quality/quality-bar.md` section 2 | The Phase 25.1 register (130 rows) and the 156 decided suppression and skip rows | The register exists with dispositions for High and Medium rows. No new per-file review of the final tree exists; the baseline is `59e9dd1` |
| 2 | `./bootstrap.sh` | Pinned SDK, secret scan, locked restore, plugin node tests, warnings-as-errors build, every non-live test, `dotnet format --verify-no-changes`, the web smoke | No recorded run for `d75fd88` |
| 2 | `dotnet test tests/OmpOS.ArchitectureTests -c Release -- --filter-class "*GateParityTests*" --filter-class "*SuppressionLedgerTests*" --filter-class "*ArchUnitLayerTests*" --filter-class "*ProjectGraphTests*"` | Bootstrap and CI run the same pinned secret scan; the scan fails closed and only an explicit skip disables it; every suppression has a kept row and no kept row is orphaned; the layer map and project graph hold | Inside the passing Architecture run (46 tests, F10) |
| 2 | `bash scripts/ci/plugin-node-tests.sh` | The 69 node tests of `plugins/gsd-core-omp` pass (node 20 or newer required by the script) | Run by the audit at `d75fd88` with node v24.18.0: 69 pass, 0 fail |
| 3 | `dotnet test tests/OmpOS.ArchitectureTests -c Release -- --filter-class "*DecisionRecordTests*"` | Every record names its candidate, licence, source, verification and risks; the index lists every record; each required topic has a record | Inside the passing Architecture run (F10) |
| 3 | `ls docs/decisions` | ADR-0001 to ADR-0014 and the index | 14 records and `README.md` |
| 4 | Read `docs/quality/cursor-parity-audit-2026-10-26.md` | One row per inventory item with status, evidence and gap; counts per table in its section 3; the not-done list in section 4; the rows with no owner in section 17 | The document exists; every cited test class and method was found by grep, none was re-run. 55 of 83 open rows cite no OQ, OD or Q id |
| 4 | `dotnet test tests/OmpOS.ArchitectureTests -c Release -- --filter-class "*DeferredScopeTests*"` | The two deferred items name no code, view, project file or OpenAPI identifier | Inside the passing Architecture run (F10) |

## Evidence files

| File | What it shows |
|------|---------------|
| `docs/quality/cursor-parity-audit-2026-10-26.md` | The audit: method, statuses, summary counts, the not-done list, every table, the re-verification log and the document drift. |
| `docs/quality/quality-bar.md` | The Phase 25.9 decision table and counts, with the gate rows of 2026-10-22 (row 4 corrected). |
| `.planning/phases/25.9-suppression-and-skip-review/` | The suppression review record. |
| `docs/decisions/README.md` | The index of ADR-0001 to ADR-0014 with their status. |

## Known limits of this verification

- No build, test or `./bootstrap.sh` was run for this record; the whole-solution totals are the main agent's report (F10), and no log is committed.
- The audit is a requirement-level audit read from the code and the tests. It is not a per-file code review.
- Linux only; Windows, macOS, Android and hosted deployments were not run. No model turn.
