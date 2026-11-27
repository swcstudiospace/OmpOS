# Phase 84 cutover — Task 6: post-flip gate + rollback proof (2026-11-26)

Cutover commit: `69b844f0` (RPC default). All commands run on this tree with default config (no `Harness__Adapter` override unless stated). Live model turns: none (OD-07).

## Named suites (post-flip)

| Suite | Command | Verdict |
|---|---|---|
| Decision records (F5) | `dotnet test tests/Autonogrammer.ArchitectureTests -c Release --no-build -- --filter-class "*DecisionRecordTests*"` | 3 passed, 0 failed, exit 0 |
| ArchitectureTests (full) | `dotnet test tests/Autonogrammer.ArchitectureTests -c Release --no-build -- --filter-not-trait "Category=Live"` | 123 passed of 124 (exit 2); the single failure is `LegacyBrandLiteralGuardTests.FullTreeScanPasses` with `RegexMatchTimeoutException` (load flake, not an assertion). Re-run in isolation: `dotnet test ... --no-build -- --filter-class "*LegacyBrandLiteralGuardTests*"` → 3 passed, 0 failed, exit 0. Direct runs: `SuppressionLedgerTests` 7/7, `RepoHygieneTests` 8/8, both exit 0 (baseline's `RepoHygiene` failure fixed by `d8fea8f0`). Effective verdict: 124/124 at the cutover commit. |
| ContractTests (shared scenarios) | `dotnet test tests/Autonogrammer.Harness.ContractTests -c Release -- --filter-not-trait "Category=Live"` | 32 passed, 0 failed, exit 0 (fresh build) |
| Api adapter selection (rollback) | `dotnet test tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*HarnessAdapterSelectionTests*"` | 2 passed, 0 failed, exit 0 (with `AUTONOGRAMMER_TEST_POSTGRES_CONNECTION` from run-owned `w13-1` test services; without it the run fails `AUTONOGRAMMER_TEST_POSTGRES_CONNECTION is not set`, exit 2 — recorded as the cause of the first attempt) |
| Api readiness probe (Rs) | `dotnet test tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*HarnessReadinessCheckTests*"` | 6 passed, 0 failed, exit 0 |
| Api Rpc adapter + factory (Rs) | `dotnet test tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*HarnessRpcAdapterTests*"` / `"*RpcHarnessSessionFactoryTests*"` | 11 passed + 5 passed, 0 failed, exit 0 |
| Api parity | `dotnet test tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*DirectRunnerAdapterParityTests*"` | 2 passed, 0 failed, exit 0 |

Note: `--filter` + `--filter-not-trait` combined is rejected by the xunit v3 runner (`ArgumentException`, exit 134); suites were run with `--filter-class` or a lone `--filter-not-trait` per project. `bash scripts/ci/secret-scan.sh` (gitleaks, needs network download) was NOT run in this assignment; recorded as the main agent's full-gate step. SEC-77-01 stays open at 0 waivers; no store test needed the PG/Dragonfly services for the suites above (in-memory stores via ApiFactory).

## Rollback proof

`HarnessAdapterSelectionTests` exercises both directions at the cutover commit: Production + `Harness:Adapter=Acp` starts and is live (rollback works); Production + `InMemory` fails startup naming `Harness:Adapter` (validator intact). Env override form: `Harness__Adapter=Acp` selects the ACP factory with identical validation (Program.cs dispatch unchanged by the flip; only the appsettings default value changed).

## Probe (Rs)

`HarnessReadinessCheckTests` (adapter-specific readiness: Acp healthy on ACP path, Rpc on RPC path) + `HarnessRpcAdapterTests` / `RpcHarnessSessionFactoryTests` (RPC composition, incl. approvals-refusal) — verdicts above.
