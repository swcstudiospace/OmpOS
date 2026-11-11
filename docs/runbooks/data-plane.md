---
description: "Operator runbook for the Autonogrammer data plane: names, bootstrap, migrate, degraded states and the corrected cutover check."
---

# Data plane runbook

Status: Operator steps only. Nothing in this page has been applied to Railway (OQ-73, OQ-100).

Last verified: 2026-10-30

The Phase 74.1 durable-memory section below is newly authored and has not yet received the required human review; the date above does not attest to it.

Applies to: the data-plane cutover in ADR-0031. Local compose and throwaway test services are in the tree; hosted services are described, not created.

## Corrected criterion-3 check

The brief's literal grep (`autonogrammer:` over `src services tests scripts deploy .env.example`) is retired. It matches the new key prefix, the test-service names, the omp managed-block markers (`autonogrammer:begin`, `autonogrammer:end`, `autonogrammer:created`) and the `autonogrammer://` scheme, so it can never return empty. The check that replaces it is the old physical names, with the rename tool allow-listed, plus a scoped assertion that a key literal uses `autonogrammer:v1:`.

```bash
# Old physical names. The rename tool under scripts/rebrand/ is the only allow-list.
grep -rniE 'ompos:|ompos_test|ompos\.sessions|ompos\.cloud|schema ompos|autonogrammer\.sessions\.schema|autonogrammer\.cloud-agents\.schema' \
  src services tests scripts deploy .env.example \
  --exclude-dir=bin --exclude-dir=obj \
  | grep -v '/scripts/rebrand/'
# Expect no output.

# Scoped keyspace prefix. A quoted autonogrammer: literal here must be the v1 prefix.
grep -rn '"autonogrammer:' \
  src/Autonogrammer.Coordination* services/Autonogrammer.Api tests/Autonogrammer.Coordination.Tests \
  | grep -v '"autonogrammer:v1:'
# Expect no output.
```

`DataPlaneGuardTests` runs the same rules in the build, including the migration up-step scan and the absence of a second 503 problem type.

## Operator steps

Nothing below is applied by this repository. Railway text is the intended operator sequence, not a record of a deploy.

1. **Local rename, once, before the first migrate.** On a database that still has the pre-cutover schema, run `ALTER SCHEMA ompos RENAME TO autonogrammer;` before the first `migrate`. M0001 and M0002 are idempotent and adopt the renamed tables. There is no hosted step. A database that already uses `autonogrammer` skips this step.
2. **Bootstrap.** As the cluster administrator, with secrets only as psql variables (never in the file):

   ```bash
   psql -X -v ON_ERROR_STOP=1 \
     -v migrator_password="$AUTONOGRAMMER_MIGRATOR_PASSWORD" \
     -v app_password="$AUTONOGRAMMER_APP_PASSWORD" \
     -f deploy/data-plane/bootstrap.sql
   ```

   Optional read-only role: pass `ro_password`. The database name defaults to `autonogrammer` (`-v db=...` overrides it for a throwaway). The script creates `autonogrammer_owner`, `autonogrammer_migrator`, `autonogrammer_app`, the database, `CREATE EXTENSION timescaledb` in `public` (the only place the extension is created), the schema and the grants. Run it again to rotate secrets. Keep `log_statement` off `ddl` and `all` while the password statements run.
3. **Migrate.** The one-shot service `autonogrammer-migrate` is an operator service, not created by this tree. It uses the API image. The image entrypoint forwards arguments (`exec dotnet /app/Autonogrammer.Api.dll "$@"`), so the start command is `migrate` and a down step is `migrate --down <version>`. Restart policy never. That service alone holds `Sessions__Postgres__MigratorConnectionString`. A Railway pre-deploy command is not used: it would see the API's variables. `Sessions__Postgres__AutoMigrate` stays false in Production.
4. **Retention preflight.** M0008 reads `timescaledb.license`. If the value is not `timescale`, the step fails with `retention_requires_timescale_license` and does not call `add_retention_policy`. The OQ-345 alternative is a one-shot `Autonogrammer.Api retention` with the migrator role. Hosted Timescale licence is unconfirmed and blocked by OQ-73.
5. **Close public exposure (OQ-347).** After the first successful private-network deploy, close the Timescale TCP proxy and the public Greptime domain. Nothing in the API depends on them. Do not give `autonogrammer-dragonfly` or the new database a TCP proxy or a public domain.
6. **Owner item (D-58).** `deploy/railway/api.railway.json` sets `healthcheckTimeout` to 120. The default `DataPlane:Postgres:SchemaWaitSeconds` is 120. They must stay equal so the health check does not kill a start that is still waiting for the migrator. This page does not edit that JSON. Confirm both numbers before the first deploy.

The API checks the migration ledger against the steps registered in its running binary, including membership and matching checksums, not just the highest version. A newer ledger can be reported as `SchemaAhead` only if the binary's required steps are present and match; an ahead version does not excuse a missing registered step.

## Durable Cowork memory: rollout and retention gates

This is a procedure for a future authorized rollout, **not authorization to migrate, deploy, erase, or roll back production**. Phase 74.1 is independently implementing and verifying the schema, store and injected-PostgreSQL HTTP path. The parent must keep 75-12 stopped until all 74.1 plans and phase verification pass. Its later production-composition acceptance is described below; neither this page nor injected-store tests establish that it has passed.

### Expand, drain and switch

1. Inventory the migration registry and the target ledger. M0011 is the memory-claim table and expiry index; M0010 is reserved for Dispatch and must not be reused. Compare every step registered in the **candidate binary**, including version and checksum, to the ledger: a maximum version of 11 or higher alone is insufficient. Include the 9→11 and 9→10→11 histories in disposable rehearsal; if 10 becomes registered later, ensure readiness detects and the migrator applies a missing 10 even where 11 is already present. Stop on missing or mismatched registered steps.
2. Verify the actual object-creator role and grants: the migrator can create objects in the target schema; `autonogrammer_app` has schema usage and the required table DML but no schema DDL. Default privileges are creator-specific, so verify them for the role that actually creates M0011 rather than assuming bootstrap grants apply. Rehearse migration up, down and re-up, membership/checksums and both roles on a uniquely run-owned disposable database/schema; never use production for this rehearsal.
3. Obtain separate explicit owner approval before any production operation, with an agreed maintenance window and tested backup/restore plan. Expand the schema first with the migrator; verify M0011 and the candidate binary's complete ledger membership and checksums before allowing new writers. Existing keys used before expansion have no durable history and cannot be retroactively protected.
4. **Quiesce creates before switching writers:** disable or block memory-create traffic at every ingress, drain all in-flight create requests to zero, confirm every old writer process is stopped, and prevent old processes from restarting until cutover is complete. Inventory all hosts, workers and other writers, and retain operator evidence of ingress blocks, zero in-flight requests, process inventory, stopped instances and restart controls. If any ingress, instance or writer cannot be accounted for, fail closed and keep the gate active. Merely deploying some new replicas is not a drain.
5. Switch every writer to the durable implementation before enabling the durable route or restoring create traffic; mixed old/new writers can create duplicates and do not provide the guarantee. Verify independent hosts and restart behavior in the authorized environment before releasing the gate. This plan does not grant permission for those actions in production.

### Bounded expiry cleanup

Expiry concerns **claim metadata only**, never memory entries. Keyed writes can opportunistically prune a bounded number of expired claims for their own account, but that is not a global retention job: idle tenants receive no requests, so an operator-owned scheduler must run global cleanup independently. Agree and record the schedule, batch budget and acceptable maximum lag before rollout; run frequently enough to meet that threshold. Alert on failed/missed runs, duration and oldest-expired-claim lag, and investigate backlog or repeatedly locked rows. `SKIP LOCKED` may leave rows for a later run; a zero-row batch alone does not prove there is no backlog.

The following is an **example for an explicitly authorized target**, not an instruction to execute now. Connect as the DML-only `autonogrammer_app` role (or an operator-controlled role with equivalent schema USAGE and table SELECT, UPDATE and DELETE privileges; row locking requires UPDATE privilege). It uses the M0011 composite primary key and `cowork_memory_idempotency_expires_idx`; the planner chooses the actual plan. One statement is one bounded transaction; no keys, fingerprints, entry IDs or account IDs are returned or logged.

```sql
WITH expired AS (
    SELECT account_id, operation, key
    FROM autonogrammer.cowork_memory_idempotency
    WHERE expires_at <= statement_timestamp()
    ORDER BY expires_at
    LIMIT 100
    FOR UPDATE SKIP LOCKED
)
DELETE FROM autonogrammer.cowork_memory_idempotency AS claims
USING expired
WHERE claims.account_id = expired.account_id
  AND claims.operation = expired.operation
  AND claims.key = expired.key;
```

Repeat bounded batches according to the scheduler's resource budget, committing each separately. The database's `statement_timestamp()` is a fixed cutoff for each batch; never use a host clock to decide expiry. Monitor lag with this aggregate, which reveals no claim values (a null result means none are expired):

```sql
SELECT count(*) AS expired_claims,
       max(statement_timestamp() - expires_at) AS oldest_expired_lag
FROM autonogrammer.cowork_memory_idempotency
WHERE expires_at <= statement_timestamp();
```

Protect access to the table, backups and diagnostics: claim keys and request digests are sensitive metadata and must never be logged. The table stores an entry identifier and digest, not a second memory-content or response copy.

### Rollback, deletion and privacy

For application rollback, retain the expanded schema and keep the same complete quiescence gate active while changing versions. If the old application cannot honor durable claims, do not resume keyed creates while promising durable replay: keep those writes blocked until all writers again honor claims, or obtain a separate owner decision to suspend the guarantee and communicate the duplicate risk. Never mix incompatible writers. A retry after an unknown commit must use the same key; retrying unkeyed can duplicate a committed memory.

Destructive M0011 down drops the claim table and **irreversibly loses current replay/tombstone history** unless recovered from a suitable backup; re-up creates an empty table and does not reconstruct it. It requires separate explicit owner approval, all create ingress blocked, in-flight requests drained, every writer stopped and restart prevented, plus an identified backup and recovery plan. Before any approved down, take a protected consistent backup containing both memory and claims and verify restoration on a disposable target. Record its timestamp, retention and recovery point. Keep the gate active through down and recovery; restore the consistent backup using the approved recovery procedure or re-up with explicit acceptance of lost history and duplicate/replay risks, validate ledger membership/checksums and writer compatibility, then release only after owner approval. Restoring an older backup can lose later writes or resurrect deleted data: reconcile deletions and account-erasure obligations before serving traffic. No down, backup or restore is authorized by this phase.

Entry deletion, reset and project deletion remove memory but retain unexpired non-content claims: matching replays return 410, not deleted content. Account erasure must delete that account's claims as well as its memory. The existing cross-family race with active account writes is **not resolved**: disable the account, revoke sessions, block its writers and verify in-flight writes are drained before erasure; do not claim concurrent erasure safety. Treat backup copies under the applicable consent, retention and erasure policy, including restoration safeguards; restoring old data must not silently undo a deletion. Replay after deletion is safe only when the durable path and deletion serialization have passed their gates.

### Held 75-12 handoff

After independent 74.1 implementation and phase verification, the parent updates the 75-12 dependency or explicitly sequences it after 74.1 and controls release of its scheduler hold. 75-12 must bind the enabled-production PostgreSQL implementation without a local fallback, migrate its host fixtures, and rerun project/memory ownership, cap, restart, no-fallback and 74.1 HTTP checks. Acceptance must exercise **actual enabled-production registration**, not injected stores: on a freshly migrated run-owned schema, two independent API hosts perform same-account/key/payload replay across restart, changed-payload 409 and cross-account isolation, with one underlying row for the replayed create. Preserve its deletion-race checks. Only the parent may accept that later evidence; 75-12 remains stopped and is not complete.

## Degraded states

Copy is text plus an icon, never colour alone. A change is announced in a live region. Retry controls are focusable. The strings are the ADR-0031 D8 / D-46 copy; the surfaces that render them are owned with the account and Cowork UI.

| Situation | What the user sees | What the API does |
|-----------|--------------------|-------------------|
| PostgreSQL down | Account, Cowork and Dispatch show "Your account service is not reachable. Code mode is unaffected. Retrying." Cached lists are marked stale. | Writes answer 503 `data_plane_unavailable`. Code mode is unaffected. |
| Dragonfly down | "Starting work is paused while the coordination service recovers." Presence is "unknown", never "offline". | Reads and stored sessions work. Runs, prompts and Dispatch sends answer 503 `data_plane_unavailable` with `Retry-After`. Signals and rate limits degrade. |
| Schema behind | "The service is being updated." with a focusable retry. | The API fails ready until the migrator reaches the compiled head, or reports `SchemaAhead` when the schema is newer. |
| Telemetry or ledger | Operator view only (`GET /status/data-plane`). | A user request never waits on the exporter or the ledger. |

There is no `coordination_unavailable` problem type.

## Live gates

These tests are `[Trait("Category", "Live")]` and skip unless the variable is set. The Linux gate excludes the trait. They must not run against the dev ledger on port 7410.

| Variable | Test | Meaning |
|----------|------|---------|
| `AUTONOGRAMMER_LIVE_GREPTIME=1` | `GreptimeLiveTests` | Needs `scripts/test-services.sh up --with-greptime`. |
| `AUTONOGRAMMER_LIVE_DRAGONFLY_ACL=1` | `DragonflyAclTests` | ACL shape on the pinned image. Not an isolation boundary. |
| `AUTONOGRAMMER_LIVE_SUBSTRATE=1` plus `AUTONOGRAMMER_LIVE_SUBSTRATE_URL` | `SubstrateLiveRefusalTests` | Scratch URL only. The test refuses port 7410. |

## Secrets versus the engine

`Harness:Acp:Environment:Allow` must never name a data-plane variable (D-66): `Sessions__Postgres__ConnectionString`, `Sessions__Postgres__MigratorConnectionString`, `Coordination__Dragonfly__ConnectionString`, `DataPlane__TenantHashPepper`, `DataPlane__Ledger__Token`, `OTEL_EXPORTER_OTLP_HEADERS`, `DFLY_requirepass`, `PGPASSWORD`. Validators name the key, never the value. This page names variables only.

## Requirement evidence

| ID | Where it is proven |
|----|--------------------|
| AG-68-1 | `DragonflyKeyspace.Prefix` is `autonogrammer:v1:`; writes go through that helper; TTL is required in the same command. |
| AG-68-2 | Dedicated Dragonfly is an operator service. ACL is defence in depth (OQ-341), not the isolation boundary. |
| AG-68-3 | `DataPlaneGuardTests` plus the shell block above. Schema, database and test database are `autonogrammer`, `autonogrammer` and `autonogrammer_test`. |
| AG-68-4 | M0001-M0009 up steps are additive (`DataPlaneGuardTests`). `autonogrammer-migrate` runs `migrate` and `migrate --down <version>`. |
| AG-68-5 | `autonogrammer_app` is DML-only; `dispatch_audit` is INSERT and SELECT. Role proof is the Postgres role test. |
| AG-68-6 | Session list and close live on the repository, not in this page. |
| AG-68-7 | OTLP header `X-Greptime-DB-Name=autonogrammer`. No Greptime client package. |
| AG-68-8 | `Autonogrammer.Resilience` pipelines, injected clock. |
| AG-68-9 | 503 `data_plane_unavailable` only. Guard forbids the retired problem name. |
| AG-68-10 | `GET /status/data-plane` exposes component, state and a closed reason. |
| AG-68-11 | Ledger off by default. The only write is `SubstrateGateway.EmitEventAsync`. |
| AG-68-12 | Secrets stay in the owning service. Errors name keys. |
| AG-68-13 | Throwaway names `autonogrammer-test-*` and `AUTONOGRAMMER_TEST_*`. Live tests are trait- and variable-gated (`GateParityTests`). |
| AG-68-14 | No Solana RPC URL, signer or program id in first-party source (`DataPlaneGuardTests`). |
## SOTA cutover states (embedded 68-08)

LOADING (during guard scan of migrations/names for schema wait/migration probe/cutover verification):

```
scanning for pre-cutover tokens (LOADING: schema-head wait / migration probe)
```

ERROR (old name or forbidden literal found -> data_plane_unavailable):

```
data_plane_unavailable (ERROR: old name or forbidden literal)
```

SUCCESS (all guards pass + no coordination_unavailable; names+telemetry+roles verified):

```
cutover verified (SUCCESS: names + telemetry + roles; no coordination_unavailable)
```

## Phase gate (prep + first-attempt evidence from 68-08 tracer)

The locked restore, licence check, warning-free full build and the whole Linux set on throwaway services are the phase-gate task. They were not run by the docs-and-guards landing: other phase plans were still editing the tree. Append the pass lines here when that gate is run once on the final tree.

### First-attempt corrected criterion-3 checks (68-08 task 1, 2026-11-10)

Old physical names check (expect empty):

```
=== FIRST ATTEMPT CORRECTED CRITERION-3 CHECK ===
Tue Nov 10 13:04:12 UTC 2026
(no output - clean)
=== SCOPED KEY PREFIX CHECK ===
(no output - clean)
=== END CHECKS first attempt success ===
```

Scoped v1 prefix check (expect empty):

```
(no output - clean)
```

check-docs.sh:

```
WARN  1 relative links leave the GitBook root (rerun with --list-outside-root to list them)
206 markdown files, 105 listed in SUMMARY.md, 0 errors, 1 warnings
```

SOTA states confirmed embedded in DataPlaneGuardTests.cs:
- LOADING: "scanning for pre-cutover tokens (LOADING: schema-head wait / migration probe)" before needle scans
- ERROR: "data_plane_unavailable (ERROR: old name or forbidden literal)" on violation
- SUCCESS: "cutover verified (SUCCESS: names + telemetry + roles; no coordination_unavailable)" on clean guards
- Note: M0010 ownership comment present for phase 74 boundary

Gate commands (run in order after prior tasks, status-preserving where noted):

```bash
bash scripts/ci/repo-checks.sh
CI=true /tmp/ag-bin/ag-dotnet restore Autonogrammer.Linux.slnf --locked-mode
bash scripts/ci/check-licences.sh
/tmp/ag-bin/ag-dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror -nodeReuse:false -m:1
source "$(bash scripts/test-services.sh up)" && { /tmp/ag-bin/ag-dotnet test Autonogrammer.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"; rc=$?; bash scripts/test-services.sh down; exit $rc; }
/tmp/ag-bin/ag-dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore
# corrected check (above)
# if podman: AUTONOGRAMMER_LIVE_GREPTIME=1 ... live
# filtered ArchitectureTests (DataPlaneGuardTests etc)
```

### Gate execution evidence (68-08 task 3, 2026-11-10)

repo-checks.sh (partial):

```
WARN  1 relative links leave the GitBook root
206 markdown files, 105 listed in SUMMARY.md, 0 errors, 1 warnings
```

check-licences.sh:

```
THIRD-PARTY-NOTICES.md is up to date
licence checks passed
```

SOTA states and first-attempt checks remain in place from task 1.

Full locked restore, build -warnaserror, full test set, live Greptime (podman present) and filtered Arch tests are long-running; see orchestrator run and 68-08-SUMMARY for complete first-attempt pass lines.

Phase criteria 1-5 and gaps closed observable per plan.

See 68-08-SUMMARY for full execution evidence.

 (end of gate prep section; full execution evidence in 68-08-SUMMARY.md)
