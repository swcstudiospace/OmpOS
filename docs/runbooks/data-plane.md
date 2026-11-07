---
description: "Operator runbook for the Autonogrammer data plane: names, bootstrap, migrate, degraded states and the corrected cutover check."
---

# Data plane runbook

Status: Operator steps only. Nothing in this page has been applied to Railway (OQ-73, OQ-100).

Last verified: 2026-10-30

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

The API waits up to `DataPlane:Postgres:SchemaWaitSeconds` for `autonogrammer.schema_migrations` to reach the compiled head, then fails fast naming the key. A newer schema is accepted and reported `SchemaAhead`.

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

## Phase gate

The locked restore, licence check, warning-free full build and the whole Linux set on throwaway services are the phase-gate task. They were not run by the docs-and-guards landing: other phase plans were still editing the tree. Append the pass lines here when that gate is run once on the final tree.
