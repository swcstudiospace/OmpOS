# Railway deploy of ompos-api, 2026-10-21

Result: **stopped at Step 2.** The `ompos-api` service was not created. Railway refused the create with "Deploys have been paused temporarily". Steps 3 to 7 did not run because they all need the service.

Target: Railway project `Ultrathink` (`67a98752-ba0a-4ae2-a881-8d0e33a89328`), environment `production` (`6cfcb6de-3175-418b-8e20-7817a3d31633`). No secret values appear in this file. Variables are named by name only.

## Pre-flight

| Check | Result |
|-------|--------|
| Railway CLI | `railway 5.71.0`, logged in (account name omitted). |
| Local image for Step 1 | `docker.io/timescale/timescaledb:2.13.1-pg16` present (`podman image exists`). |
| Repository head | Local `HEAD` is `a6484a0`, a docs-only commit on top of the pushed head `dcd0599` (`dcd0599` is an ancestor). `git diff dcd0599 HEAD -- deploy/ src/` is empty, so the uploaded deploy inputs match the pushed head. |
| Project services before any write | Agent, Dragonfly, Gate, GreptimeDB, OpenBot, Postgres, Routines, TimescaleDB, tailscale-forwarder, tailscale-vpn. No `ompos-api`. Environments: `production` only. |

## Step 1: read-only pre-check of the shared TimescaleDB. PASS.

Command shape: `railway run --project 67a98752-… --environment 6cfcb6de-… --service TimescaleDB -- sh -c 'podman run --rm -i -e PGPASSWORD -e PGSSLMODE=require -e PGHOST="$RAILWAY_TCP_PROXY_DOMAIN" -e PGPORT="$RAILWAY_TCP_PROXY_PORT" -e PGUSER -e PGDATABASE docker.io/timescale/timescaledb:2.13.1-pg16 psql -X -v ON_ERROR_STOP=1 -f - < <sql file>'`. The SQL file was in `/tmp`, outside the repo, and was deleted afterwards. The session began with `SET default_transaction_read_only = on`. Only the four specified queries ran.

| Query | Result |
|-------|--------|
| `SET default_transaction_read_only = on` | `SET` |
| 1. `select version();` | `PostgreSQL 16.1 (Ubuntu 16.1-1.pgdg22.04+1) on x86_64-pc-linux-gnu, compiled by gcc (Ubuntu 11.4.0-1ubuntu1~22.04) 11.4.0, 64-bit`. 1 row. |
| 2. TimescaleDB `extversion` | `2.13.1`. 1 row. Meets the 2.13 minimum. |
| 3. `ompos` schema | **0 rows.** Passes the stop condition. |
| 4. Tables `sessions`, `session_events`, `session_tool_approvals` in any schema | **0 rows.** Passes the stop condition. |

The TCP proxy was reachable, and no connection error occurred. The exit code was 0.

## Step 2: create ompos-api. BLOCKED by Railway.

1. `railway add --project … --environment … --service ompos-api --json` failed with `error: unexpected argument '--project' found` (exit 2). `railway add` takes its project from the linked directory, and `railway link` is forbidden, so the CLI path cannot carry the explicit project and environment guardrail.
2. The same create was attempted with the GraphQL mutation `serviceCreate(input: {projectId, environmentId, name: "ompos-api"})`. The schema accepts these fields, and no `source` was given, so the service would have had no image, no database, and no domain. Railway refused it:
   - attempt 0: `Deploys have been paused temporarily`, traceId `6242011328095662797`
   - attempt 1 (after about 20 s): same message, traceId `8263988442392453701`
   - attempt 2 (after about 40 s): same message, traceId `495869283039640416`
   - attempt 3 (after about 60 s): same message, traceId `7566227453196168767`
3. After the refusals, the service list was re-read with `railway status --json`. `ompos-api` is absent, and the list is unchanged from pre-flight.

Domain check: not applicable, because no service exists. No domain was generated, and no domain delete was needed.

Cause: the account-level pause on deploys. `docs/runbooks/hosting.md` (lines 156 and 161) and `.planning/v0.9.2-MILESTONE-AUDIT.md` (line 16, "Railway deploys paused for this account") both record it. The pause is external to the repo. The run did not work around it.

## Step 3: variables. NOT RUN (needs the service).

Preparation only, from names, with no values read into this file:
- TimescaleDB names used by the references are present: `PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`, `PGPASSWORD`.
- Dragonfly name `DFLY_requirepass` is present.
- GreptimeDB has `GREPTIME_USERNAME` and `GREPTIME_PASSWORD` as separate variables. There is no single variable that holds a complete Basic credential. Under the brief's rule the telemetry decision would be `Otel__Exporter=none`, with the reason given here. The OTLP variables would not be set. This has not been applied.

No variable was set.

## Step 4: health check. NOT RUN (needs the service).

## Step 5: deploy. NOT RUN (needs the service). Deployment id: none. Listener line, startup exception, health result, and schema DDL result: none observed.

## Step 6: verify inside the container. NOT RUN.

Blocked because no container exists. `railway ssh` was not attempted, so the tooling timeout was not tested.

## Step 7: prompt streaming. NOT RUN.

Blocked by two things: no deployed API, and the owner's authentication decision. The sentinel authority means no token validates, so no prompt can be submitted. No bypass was attempted, and no token was minted, forged, or used. No model turn ran, and no provider credential was set.

## Runbook

No line in `docs/runbooks/hosting.md` became false in this run. Lines 5 ("does not exist yet"), 156 ("Nothing has been deployed"), and 161 ("Railway refuses deploys ... while they are paused") still hold. The file was not edited.

## Stop summary

- Stop condition hit: Step 2 refused by Railway (account deploys paused), retried 4 times over about 60 s.
- Not a listed stop condition, but a hard refusal. Not worked around.
- Nothing was created, modified, or deleted in Railway. No domain was generated. No variable was written.
- Not committed or pushed.

To resume: the owner needs to lift the Railway deploy pause for this account. Then rerun Step 2 with the same mutation (`serviceCreate` with projectId `67a98752-…`, environmentId `6cfcb6de-…`, name `ompos-api`). The rest of this runbook applies unchanged.
