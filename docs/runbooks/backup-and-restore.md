---
description: "What state the hosted Autonogrammer API keeps, how to back it up and restore it, proposed recovery targets, a restore drill, and how schema migrations work as built."
---

# Backup, restore and migrations

Status: Draft (no backup or restore has been run; the API and its database are not deployed; the commands are untested)

Review date: 2026-10-29

Owner role: deployment owner (single operator)

Applies to: the hosted API of [Hosting](hosting.md) with `Sessions:Store=Postgres`

## Purpose

List every place the hosted deployment keeps state, say which of it is worth backing up and how, propose recovery targets, give a restore drill, and describe exactly how the schema changes at upgrade time in the code as built.

## Scope

In scope: the session store in Postgres (TimescaleDB), the Dragonfly coordination store, the omp agent directory and workspace folders on volumes, and the configuration in Railway variables. Out of scope: the GreptimeDB telemetry store, the Vercel project (a static bundle rebuilt from git), and the cloud agent worker volumes ([Cloud agents](cloud-agents.md)).

## State inventory

| State | Where | Durable | Holds | Back up |
|-------|-------|---------|-------|---------|
| Session log | Postgres schema `autonogrammer`: `sessions` (header row per session, `owner_id`, `status`, `messages` jsonb), `session_events` (TimescaleDB hypertable on `occurred_at`, one-day chunks, primary key `(session_id, seq, occurred_at)`, `payload` jsonb), `session_tool_approvals` | Yes | Prompts, agent output, tool calls and results, file edits, tool approval decisions | Yes: dump and volume snapshot |
| Cloud agent records | Postgres table `cloud_agents` (created when cloud agents are enabled [INFERENCE: `CloudAgentStoreStartup` is part of the cloud services]) | Yes | Agent records; no secret values | With the database |
| Run leases and idempotency records | Dragonfly keys `autonogrammer:lease:run:<session>` (30 s lifetime, renewed every 10 s) and `autonogrammer:idem:<sha256>` (60 s while pending, 24 h once completed, with the response JSON); pub/sub channel `autonogrammer:signals` | Ephemeral by design | Lease owner ids; the response body of a replayable request | No |
| Process memory of the API | `SessionRegistry`, `SessionLifecycleStore`, `RunRegistry`, in-memory cloud secrets | No | The session list and board index; run handles | No. After a restart `GET /sessions` and the board are empty or wrong until sessions are touched again (ADR-0031, Context); `GET /sessions/{id}` reads the database |
| omp agent directory | `~/.omp` of the API user, or `PI_CODING_AGENT_DIR`, or `Providers:AgentDirectory`. The API image creates no home directory and defines no volume for uid 10001 [INFERENCE: `useradd --no-create-home`], so where omp keeps it in the hosted container is open | Only if the owner mounts a volume | Provider credentials, `models.yml`, omp's own session files and settings | Credentials: no (recreate at the provider). Settings: optional |
| Workspace folders | `Api:Workspace:Root`, `Worktrees:Root`, `Worktrees:Repository`, and the engine's working directory | Only if on a volume | Checked-out code and agent edits | Only if the agent's work is not in git |
| Configuration | Railway service variables of the API, Postgres and Dragonfly | Yes, in Railway | Connection strings, passwords, the owner token digest, the OTLP header, provider keys | Yes: a record of names and non-secret values in git (`deploy/railway/README.md`); secret values in the operator's password manager |
| Source and images | Git repository; Railway keeps deployments for the plan's retention period | Yes | The code and past builds | Git is the backup; a rollback target older than the retention period cannot be restored by rollback ([Deploy and rollback](deploy-and-rollback.md)) |

Schema as built, for the restore checks:

| Table | Key columns |
|-------|-------------|
| `autonogrammer.sessions` | `id text primary key`, `owner_id`, `created_at`, `updated_at`, `status` (enum name as text), `sandbox_id`, `messages jsonb`; index `sessions_status_updated_at_idx` |
| `autonogrammer.session_events` | `session_id references sessions(id)`, `seq bigint`, `correlation_id`, `occurred_at timestamptz`, `payload jsonb`; hypertable |
| `autonogrammer.session_tool_approvals` | primary key `(session_id, request_id)`, `tool_name`, `granted_tools jsonb`, `decision` (`Allow` or `Deny`), timestamps |
| `autonogrammer.cloud_agents` | `id`, `owner_id`, `session_id`, `status`, `idempotency_key`, `record jsonb` |

Source: `src/Autonogrammer.Sessions.Postgres/PostgresSessionSchema.cs`, `PostgresCloudAgentSchema.cs`. Connections set the search path `autonogrammer,public`.

## Recovery targets (proposals)

| Item | Target | Reason |
|------|--------|--------|
| RPO of the session log | 24 hours (nightly logical dump) with a Railway volume backup schedule as a second copy | One operator; the log is history, not a system of record for other systems. Tighten when real users depend on it |
| RTO of the API | 4 hours: redeploy the last good image (minutes) plus a database restore (depends on size) | Single replica; the platform rebuilds from git |
| RPO of Dragonfly | Not applicable | Ephemeral state |
| RPO of configuration | Zero for names (git), the age of the last password-manager edit for secrets | |

The targets have no measurement behind them. The restore drill produces the first measured RTO.

## What to back up and how

### 1. Logical dump of the database (works with any Postgres host)

The Railway TimescaleDB service of the hosting topology runs `timescale-postgis-ssl:pg16-ts2.13`; local parity uses `docker.io/timescale/timescaledb:2.13.1-pg16` with the digest pinned in `deploy/compose.yaml`. Use that image for the client tools so the `pg_dump` major version matches (PostgreSQL 16.1 and TimescaleDB 2.13.1 were read on 2026-10-21, `evidence/railway-autonogrammer-api-2026-10-21.md`, Step 1).

The shared TimescaleDB holds other schemas besides `autonogrammer` (ADR-0031, Context). A whole-database dump includes them. Decide before the first dump whether that is acceptable (Q-BR-1). The reason to dump the whole database: with TimescaleDB, a dump limited to one schema (`pg_dump -n autonogrammer`) leaves out the hypertable chunks, which live in `_timescaledb_internal` [INFERENCE: from how TimescaleDB stores chunks; not tested here].

From a machine with the Railway CLI, run the client inside the project's variable scope (the pattern of the 2026-10-21 pre-check, which reached the database through the service's TCP proxy variables):

```sh
railway run --project <project-id> --environment <environment-id> --service TimescaleDB -- sh -c '
  podman run --rm \
    -e PGPASSWORD -e PGSSLMODE=require -e PGHOST="$RAILWAY_TCP_PROXY_DOMAIN" -e PGPORT="$RAILWAY_TCP_PROXY_PORT" -e PGUSER -e PGDATABASE \
    -v "$PWD:/backup:Z" \
    docker.io/timescale/timescaledb:2.13.1-pg16@sha256:1b2b5508848bbbbd671e02aeafd75fefb57771168f0782b6842bfb2f66af444b \
    pg_dump --format=custom --no-owner --file="/backup/autonogrammer-$(date -u +%Y%m%dT%H%M%SZ).dump"'
```

Rules:

- The dump contains prompts, outputs and file contents. Encrypt it before it leaves the machine (for example with `age` or `gpg`), store it outside the repository, and delete old copies on the schedule of [Data retention](../privacy/data-retention.md).
- The command needs the service's public TCP proxy. The 2026-10-21 pre-check found one on the shared TimescaleDB; whether to keep a public proxy on a database that holds session data is an open security question (threat model, T3.11). The alternative is to run `pg_dump` from a shell inside the Railway network (`railway ssh`); that path is not verified here.
- Write the dump size, the time and the checksum (`sha256sum`) next to it.

### 2. Portable export of the session tables

This path survives a TimescaleDB version change because the API recreates the schema itself on start. It copies rows, not the catalog.

```sh
psql "$DATABASE_URL" -c "\copy (SELECT * FROM autonogrammer.sessions) TO 'sessions.csv' CSV HEADER"
psql "$DATABASE_URL" -c "\copy (SELECT * FROM autonogrammer.session_events ORDER BY session_id, seq) TO 'session_events.csv' CSV HEADER"
psql "$DATABASE_URL" -c "\copy (SELECT * FROM autonogrammer.session_tool_approvals) TO 'session_tool_approvals.csv' CSV HEADER"
```

Restore: start the API once against an empty database so that it creates the schema (see "Migrations"), stop it, then `\copy ... FROM` the three files in the order sessions, session_events, session_tool_approvals (the last two reference `sessions`).

### 3. Railway volume backups

Railway documents scheduled volume backups for a Postgres service: they snapshot everything on the attached volume, are incremental and copy-on-write, and are billed at the volume rate for the data unique to each snapshot (https://docs.railway.com/guides/postgres-backups-restores, section "Enable scheduled volume backups"). The same page documents point-in-time recovery for the Railway-managed Postgres service. Whether the volume backups and point-in-time recovery apply to the `timescale-postgis-ssl` template service is not verified [INFERENCE: the page describes the standard Postgres service; check the Backups tab of the TimescaleDB service]. A volume snapshot of a running database is crash-consistent at best; keep the logical dump as the copy you trust.

### 4. Variables

Record the variable names, the non-secret values and the owner of each secret in `deploy/railway/README.md`. Keep secret values in the operator's password manager. `railway variable list --service <name>` prints values, including secrets; run it only on a trusted terminal and never paste its output into a ticket or a chat.

## Restore

### Database lost or corrupted

1. Stop writes: scale the API down or remove the domain ([Incident response](incident-response.md), "Contain").
2. Create the target: a new Postgres service from the same template, or a scratch database, with TimescaleDB 2.13 or newer.
3. Restore a dump:

   ```sh
   psql "$TARGET_URL" -c "CREATE EXTENSION IF NOT EXISTS timescaledb;"
   psql "$TARGET_URL" -c "SELECT timescaledb_pre_restore();"
   pg_restore --no-owner --dbname "$TARGET_URL" autonogrammer-<stamp>.dump
   psql "$TARGET_URL" -c "SELECT timescaledb_post_restore();"
   ```

   The pre and post restore calls are TimescaleDB's documented procedure for restoring a dump that contains hypertables [INFERENCE: recalled from the TimescaleDB documentation, not read again here]. Restore into the same TimescaleDB version as the dump, or a newer one.
4. Point the API at the target by changing `Sessions__Postgres__ConnectionString`, then redeploy. The start-up schema step runs; it finds every object and changes nothing.
5. Run the checks of the drill below.

### Dragonfly lost

Nothing to restore. Redeploy or restart the Dragonfly service, then restart the API so it reconnects (the start-up `PING` fails fast if it is unreachable). Effects, from the code: run leases vanish, so a run in flight loses its lease at the next renewal and ends; the reaper settles sessions whose lease lapsed; idempotency records vanish, so a client retry within 24 hours of a prompt may run it twice [INFERENCE: from `DragonflyIdempotencyStore`].

### Volume lost (omp directory, workspace)

Recreate the volume, mount it, enter the provider credentials again at the provider's console or through the API (`Providers:AllowCredentialWrite` stays off unless needed), and clone the workspace from git.

## Restore drill

Run the drill once before the first public exposure and then once a quarter (proposal). Use a scratch Postgres on the operator's machine, never the production service.

1. Take a dump as in "Logical dump". Record the start time.
2. Start a scratch database:

   ```sh
   podman run -d --name ag-restore -e POSTGRES_PASSWORD=<throwaway> -p 127.0.0.1:55432:5432 \
     docker.io/timescale/timescaledb:2.13.1-pg16@sha256:1b2b5508848bbbbd671e02aeafd75fefb57771168f0782b6842bfb2f66af444b
   podman exec ag-restore psql -U postgres -c "CREATE DATABASE restore_test"
   ```

3. Restore as above into `restore_test` (`timescaledb_pre_restore`, `pg_restore`, `timescaledb_post_restore`).
4. Check the contents:

   ```sql
   SELECT count(*) FROM autonogrammer.sessions;
   SELECT count(*) FROM autonogrammer.session_events;
   SELECT hypertable_schema, hypertable_name, num_chunks FROM timescaledb_information.hypertables;
   SELECT session_id, min(seq), max(seq), count(*) FROM autonogrammer.session_events GROUP BY 1 ORDER BY 4 DESC LIMIT 5;
   ```

   Pass when the counts equal those taken from the source at the dump time (take them in step 1), the hypertable is listed, and `max(seq) - min(seq) + 1` equals `count(*)` per session (the repository promises a strictly monotonic Seq with no gaps, `ISessionRepository` remarks).
5. Check the API against the copy. The API returns a session only to its owner, so in the scratch copy only, give one session to the development principal: `UPDATE autonogrammer.sessions SET owner_id = 'local-dev' WHERE id = '<known id>'` (`SingleDevPrincipal.DevName`). Start `autonogrammer-api` (podman build per [Hosting](hosting.md)) with `ASPNETCORE_ENVIRONMENT=Development`, `Harness__Adapter=InMemory`, `Sessions__Store=Postgres`, `Sessions__Postgres__ConnectionString` pointing at the scratch database, `Coordination__Store=InMemory` and `Api__Auth__AllowDevelopmentPrincipal=true`. `GET /sessions/<known id>` must return 200 with the stored messages; `GET /sessions/<known id>/events` must replay the stored events.
6. Record the elapsed time (the measured RTO), the dump size and any deviation in `docs/runbooks/evidence/restore-drill-<date>.md`, then remove the scratch container (`podman rm -f ag-restore`).

## Migrations as built

How the schema changes today, from `src/Autonogrammer.Sessions.Postgres/PostgresSessionSchema.cs`, `PostgresCloudAgentSchema.cs` and `services/Autonogrammer.Api/Persistence/SessionStoreStartup.cs`:

- At every start, before the host serves any request, `SessionStoreStartup.StartingAsync` calls `PostgresSessionSchema.EnsureCreatedAsync`. It opens one transaction, takes `pg_advisory_xact_lock`, runs the statements `CREATE SCHEMA IF NOT EXISTS`, `CREATE EXTENSION IF NOT EXISTS timescaledb`, `CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS` and `create_hypertable(..., if_not_exists => TRUE)`, and commits. Several instances starting together serialize on the lock.
- In Production, `Sessions:Store=InMemory` is refused at start; so is any store other than Postgres.
- **There is no version table.** Nothing records which statements ran or in which release. The only way to know the schema shape is to inspect it (`\d autonogrammer.*`).
- **A statement that already ran is skipped by name, not by content.** Changing a column type or a constraint in a later release does not reach an existing database. The code comments say a later change adds statements guarded the same way (`ADD COLUMN IF NOT EXISTS`), so a database created by any earlier release converges to the same shape.
- The runtime role and the DDL role are the same credential (ADR-0031, Context), and `CREATE EXTENSION` needs a privileged role.
- There is no down migration and no dry run.

What this means for an upgrade and a rollback:

| Situation | Consequence |
|-----------|-------------|
| Deploy a release that adds a column or table | The new release adds it at start. A failure rolls the batch back (one transaction) and the host does not start. With a health check configured, Railway keeps the old deployment serving until the new one is live (https://docs.railway.com/guides/jupyter-server-team, section "Create the project files"); that behaviour was not observed on this service |
| Roll back the image after a release added columns | The old code meets a newer schema. Additive columns and tables are tolerated by queries that name their columns. A release that added a `NOT NULL` column without a default breaks inserts of the old code. Review each release's schema statements before relying on a rollback |
| Restore an old dump into a database used by a newer release | The newer release adds what is missing at start; rows written by the newer release before the restore are gone |
| Upgrade TimescaleDB | Not handled by the API. Run `ALTER EXTENSION timescaledb UPDATE` as the operator, in a fresh session, after a dump (Q-DATA-TSDB-VERSION is open in [Hosting](hosting.md)) |

Take a dump before every deploy that changes a schema statement. The data-plane decision of the v0.16.0 milestone, [ADR-0031](../decisions/ADR-0031-data-plane-namespaces-and-resilience.md) (status Proposed), describes a migration tool with a version table; it is not built, and this page describes the code as built.

## Questions for the orchestrator

| ID | Question |
|----|----------|
| Q-BR-1 | May a whole-database dump include the Substrate data that shares the TimescaleDB service, or must the API get its own database first? |
| Q-BR-2 | Does the Railway Backups feature, including point-in-time recovery, apply to the `timescale-postgis-ssl:pg16-ts2.13` template service? |
| Q-BR-3 | Where is the encrypted dump stored (location, key holder, retention)? |
| Q-BR-4 | Where will the omp agent directory of the hosted API live (`HOME`, `PI_CODING_AGENT_DIR`) and is it on a volume? |

## How verified

- Read: the schema classes and `ServiceCollectionExtensions.cs` of `src/Autonogrammer.Sessions.Postgres`, `SessionStoreStartup.cs`, `src/Autonogrammer.Coordination/CoordinationDefaults.cs`, `src/Autonogrammer.Coordination.Dragonfly/DragonflyKeys.cs`, `deploy/compose.yaml`, `deploy/api.Containerfile`, [Hosting](hosting.md), ADR-0031, `docs/runbooks/evidence/railway-autonogrammer-api-2026-10-21.md`.
- Railway documentation consulted on 2026-10-29: the backups page (volume backups and point-in-time recovery for Postgres, via the documentation search tool; the full page was not retrievable) and the rollback guide snippets.
- Not verified: no dump, restore or snapshot was run; the drill commands are untested; the Timescale restore procedure is recalled, not rechecked; the Railway backup features were not checked against the template service.
