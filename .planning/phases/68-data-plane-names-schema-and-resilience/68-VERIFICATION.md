---
phase: 68-data-plane-names-schema-and-resilience
verified: 2026-11-03
status: gaps_found
method: static only (read, grep, glob, phase summaries). No builds, tests, containers or git writes were run.
covered_files:
  - .planning/ROADMAP.md (Phase 68, lines 1419-1431)
  - .planning/phases/68-data-plane-names-schema-and-resilience/68-01..68-07-PLAN.md
  - .planning/phases/68-data-plane-names-schema-and-resilience/68-01..68-07-SUMMARY.md
  - .planning/phases/68-data-plane-names-schema-and-resilience/68-PLAN-CHECK.md
---

# Phase 68 verification: Data plane: names, schema and resilience

## Verdict vocabulary

- STATIC-OK: the artifact exists and the truth holds, confirmed by reading the cited lines. Nothing was executed.
- CLAIMED: a phase summary says a run passed. Not re-run here.
- NOT RUN: the summaries show the command was never executed.
- HUMAN NEEDED: needs a gate run, a live service or an owner decision.
- GAP: a confirmed defect, or a deliverable missing against a criterion or truth.

## ROADMAP success criteria

| # | Criterion (ROADMAP 68) | Verdict | Evidence | Notes |
|---|---|---|---|---|
| 1 | Build and locked restore pass; `check-licences.sh` passes with no new package | NOT RUN, HUMAN NEEDED | 68-01-SUMMARY.md:58 (single-project build, 0 warnings, claimed); 68-01-SUMMARY.md:66 ("Not done: a clean `CI=true restore --locked-mode` pass"); 68-06-SUMMARY.md:40 (locked restore failed NU1004, six attempts); 68-07-SUMMARY.md:125 (phase-gate task not run) | The phase gate was never executed end to end. The one package reference (Logging.Abstractions, already pinned) is claimed at 68-01-SUMMARY.md:63. |
| 2 | Whole Linux test set passes against throwaway services | HUMAN NEEDED (subsets CLAIMED) | 68-02-SUMMARY.md:50 (Coordination.Tests 77 passed); 68-03-SUMMARY.md:23-26 (Sessions.Tests 122 passed; Sessions.Postgres.Tests 94 of 94 on run 3; Api.IntegrationTests subset 8 passed); 68-07-SUMMARY.md:123 (ArchitectureTests does not build) | The full set was not run. Subsets only. |
| 3 | No first-party source holds an old physical data name | GAP (verify command defective); exhaustive absence not shown | Literal grep matches the mandated `autonogrammer:v1:` prefix (68-02-SUMMARY.md:22). It also matches instrument names: services/Autonogrammer.Api/Metrics/ApiMetrics.cs:24 (`autonogrammer.sessions.count`) and services/Autonogrammer.Api/Cloud/CloudMetrics.cs:13 (`autonogrammer.cloud.compute.seconds`). Lock-file hits are project names. Corrected check documented in docs/runbooks/data-plane.md (file present) | The roadmap literal cannot return empty; the plan check said so (68-PLAN-CHECK.md:50). 68-07 retired it but did not run the replacement (68-07-SUMMARY.md:66). The old-name scan was truncated at 20 of 45 files, so absence is not shown exhaustively. |
| 4 | Telemetry reaches GreptimeDB database `autonogrammer` (live, podman) | HUMAN NEEDED | .env.example:94 (`X-Greptime-DB-Name=autonogrammer`); 68-06-SUMMARY.md:26-27 (bootstrap-greptime.sh created the database on the throwaway container, claimed); 68-04-SUMMARY.md:65-66 (OtlpListenerTests status "unknown") | Live gated test was not run. Requires a podman host. |
| 5 | ADR-0031 indexed; `DecisionRecordTests` pass | HUMAN NEEDED (ADR present and indexed) | docs/decisions/ADR-0031-data-plane-namespaces-and-resilience.md (file present); docs/SUMMARY.md:121 (indexed); 68-07-SUMMARY.md:123 (ArchitectureTests does not build, so DecisionRecordTests cannot run) | Index is correct; the test run is missing. |

## Plan truths (must_haves)

| Plan | Truth | Verdict | Evidence |
|---|---|---|---|
| 68-01 | Deadline, bounded jittered retry, breaker and bulkhead on injected TimeProvider | STATIC-OK | src/Autonogrammer.Resilience/ResiliencePipeline.cs:13-16 (contract), :236-244 (retry decision), :297-316 (deadline to timeout); 68-01-SUMMARY.md:46-52 |
| 68-01 | Caller cancellation always passes through; pipeline deadline becomes DataPlaneTimeoutException with component only | STATIC-OK | ResiliencePipeline.cs:236-239 (caller cancel not retried, not a breaker failure), :297-304 (OCE passthrough unless deadline), :316 (`DataPlaneTimeoutException(_policy.Component, Timeout)`, no inner exception) |
| 68-01 | No new package; L1 never references Resilience | CLAIMED | 68-01-SUMMARY.md:58, :63 (61 tests pass, one PackageReference) |
| 68-02 | Every Dragonfly key built by DragonflyKeyspace under `autonogrammer:v1:` with TTL at most 24 h in the same command | STATIC-OK | src/Autonogrammer.Coordination.Dragonfly/DragonflyKeyspace.cs:9-10 (contract), :27 (`MaximumTimeToLive` 24 h), :115-119 (`RequireTimeToLive` throws outside (0, 24 h]) |
| 68-02 | Tenant hash is HMAC-SHA256 keyed by DataPlane pepper | CLAIMED | 68-02-SUMMARY.md:24; src/Autonogrammer.Coordination.Dragonfly/TenantHasher.cs present |
| 68-02 | Dragonfly outages fail closed for leases, idempotency and nonces; open for signals and presence; rate limits fall back in-process | PARTIAL | Closed and open paths claimed at 68-02-SUMMARY.md:25-28. Presence, rl and auth have key builders only, with no store, so their fail modes are not wired (68-02-SUMMARY.md:37). |
| 68-03 | Sessions carry Surface and ClosedAt; ListByOwnerAsync and SetClosedAsync identical in memory and Postgres | CLAIMED | 68-03-SUMMARY.md:33-34; tests 122 (in-memory) and 94 (Postgres) pass, :23-25 |
| 68-03 | Migrations M0001-M0009 apply in order, checksummed, additive with down steps, one advisory lock, migrator role | STATIC-OK | src/Autonogrammer.Sessions.Postgres/Migrations/MigrationRunner.cs:21-23 (checksum rule), :40 (`pg_advisory_xact_lock`), :57-63 (`schema_migrations` with checksum), :126 (`ChecksumOf` SHA-256), :240-241 (mismatch throws before apply). Step files M0001 to M0009 present in the Migrations folder. |
| 68-03 | Runtime role has DML only; erasure deletes owner rows and tenant Dragonfly keys | PARTIAL (GAP on erasure) | deploy/data-plane/bootstrap.sql:11 (runtime role DML only), :114-119 (default privileges), :124 (`GRANT SELECT, INSERT, UPDATE, DELETE`), :127-129 (revoke on dispatch_audit). Erasure reaches only presence and rl keys: 68-02-SUMMARY.md:36 and 68-03-SUMMARY.md:36 |
| 68-04 | `GET /status/data-plane` returns one row per component with state and closed reason only | STATIC-OK | src/Autonogrammer.Contracts/DataPlane/DataPlaneStatus.cs:10 (record of Component, State, Reason); services/Autonogrammer.Api/DataPlane/DataPlaneStatusService.cs:25 (component list), :69-87 (`GetAsync`), :113-119 (state numbering) |
| 68-04 | Store outages are 503 `data_plane_unavailable`; migrate verb and schema-head wait present | CLAIMED | 68-04-SUMMARY.md:106-107 (close-out, no code change). Integration test status "unknown" at :55-77. |
| 68-05 | Ledger sink off by default; Enabled false leaves ledger_outbox empty | STATIC-OK | services/Autonogrammer.Api/Audit/LedgerSinkServiceCollectionExtensions.cs:9 and :13-14 (off unless enabled), :32-35 (`NullAuditLedger` unless `Ledger.Enabled`); registered once at services/Autonogrammer.Api/DataPlane/DataPlaneServiceCollectionExtensions.cs:54 |
| 68-05 | Ledger is a notarisation copy fed by ledger_outbox; only the pump emits; closed field set; no test touches 127.0.0.1:7410 | CLAIMED | 68-05-SUMMARY.md:10, :18, :28 (5 passed, 1 skipped; live test skipped without `AUTONOGRAMMER_LIVE_SUBSTRATE=1`) |
| 68-06 | `test-services.sh up` and `down` start and remove all services on every exit path; `--with-greptime` | CLAIMED | 68-06-SUMMARY.md:13-22 (maxmemory probe and pinned value; up/down round trip, no container left), :26-27 (Greptime up and bootstrap) |
| 68-06 | GateParityTests proves bootstrap and CI start and stop the same services with the same arguments | NOT RUN | 68-06-SUMMARY.md:40-41 (not executed through the test runner; substitute check in a scratch project only) |
| 68-07 | DataPlaneGuardTests fails the build on old names, forbidden literals, up-step violations and `coordination_unavailable` | NOT RUN | tests/Autonogrammer.ArchitectureTests/DataPlaneGuardTests.cs present; 68-07-SUMMARY.md:123 and :125 (not run; project does not build) |
| 68-07 | Corrected criterion-3 check documented; ADR-0031 indexed; runbook present | STATIC-OK | docs/runbooks/data-plane.md present; docs/SUMMARY.md:121 (ADR-0031 indexed) |

## Gaps

1. **Phase gate never run (criteria 1 and 2).** 68-07 is `status: halted` (68-07-SUMMARY.md:66). Its final phase-gate task (locked restore, licence check, warning-free build, whole Linux set) was not executed (68-07-SUMMARY.md:125).
2. **ArchitectureTests does not build.** Autonogrammer.UI account files fail MA0048 (68-07-SUMMARY.md:123), owned by another phase. This blocks DataPlaneGuardTests, DecisionRecordTests (criterion 5), SuppressionLedgerTests, ScaffoldRegistrationTests, ImagePinTests and GateParityTests.
3. **Criterion 3's verify command is defective.** It cannot return empty because it matches the mandated `autonogrammer:v1:` prefix and metric instrument names. The replacement check is documented but has not been run.
4. **Erasure does not cover lease, idempotency or auth keys.** The plan text (68-02, 68-03 truths) requires it. The implementer recorded the deviation (68-02-SUMMARY.md:36): the D-11 key grammar has no tenant segment in those keys. This is an unresolved conflict between the plan and ADR-0031's key table. It needs an owner decision, not a silent accept.
5. **Presence, rl and auth stores do not exist.** Only key builders exist (68-02-SUMMARY.md:37), so the degraded behaviour for those domains is not wired or tested against Dragonfly.
6. **Out-of-phase migration registered.** MigrationRegistry.cs:23 registers `M0010_DispatchDeviceState`. ROADMAP.md:1422 assigns "its own M0010" to phase 74, and 68-03-PLAN.md key_links say 74 U2 registers M0010 without touching M0001-M0009. It does not break the "up to the compiled head" truth, but 68-03-SUMMARY.md:35 describes the head as M0009. The integrator must confirm ownership.
7. **Suppression rows unreconciled.** Candidate SUP rows were not flipped (68-07-SUMMARY.md:126), and SuppressionLedgerTests did not run.

## Human verification required

- Run the phase gate from 68-07 Task 2: `bash scripts/ci/repo-checks.sh`, `CI=true restore Autonogrammer.Linux.slnf --locked-mode`, `bash scripts/ci/check-licences.sh`, warning-free `build -warnaserror`, and the whole Linux test set against `scripts/test-services.sh up` (criteria 1, 2).
- Run the corrected criterion-3 check from docs/runbooks/data-plane.md (criterion 3).
- Run `AUTONOGRAMMER_LIVE_GREPTIME=1 dotnet test --project tests/Autonogrammer.Api.IntegrationTests ... --filter-trait "Category=Live"` on a podman host (criterion 4).
- After ArchitectureTests builds, run DataPlaneGuardTests, DecisionRecordTests, SuppressionLedgerTests, ScaffoldRegistrationTests, GateParityTests and ImagePinTests (criterion 5 and plan truths 68-06, 68-07).
- Owner decision on the erasure scope for lease, idempotency and auth keys (gap 4).

## Non-blocking observations

- 68-04 close-out made no code change; its coverage rows are all `status: unknown` (68-04-SUMMARY.md:55-77).
- 68-02 leaves the SUP-019 suppression unchanged on `DragonflySessionSignals.cs` (68-02-SUMMARY.md:57).
