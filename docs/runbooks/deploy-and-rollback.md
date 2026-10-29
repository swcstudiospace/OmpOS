---
description: "Step-by-step hosted deploy of the Autonogrammer API on Railway and the web head on Vercel, with verification commands for each step and the rollback procedure."
---

# Deploy and rollback (hosted)

Status: Draft (every step is marked by what has actually been run; no step has been executed against a real API deployment)

Review date: 2026-10-29

Owner role: deployment owner (single operator)

Applies to: the hosted API of [Hosting](hosting.md); variable names and settings of `deploy/railway/README.md`

## Purpose

Give the order of a first hosted deploy and of every later deploy, the exact command that verifies each step, and the way back when a step fails. It complements [Hosting](hosting.md), which records the topology and the owner decisions.

## Scope

In scope: the Railway services (Postgres, Dragonfly, API) and the Vercel project of the real web head. Out of scope: the Demo build (static, no API; [Web head runbook](web-demo.md)), cloud agents and automations (off), and the desktop heads.

## What has been run so far

Only these facts are recorded. Everything else in this page is a plan.

| Fact | Evidence |
|------|----------|
| The API image builds with podman and runs as uid 10001; `/health/live` and `/health/ready` answered 200 with in-memory stores (2026-10-21, tree before the rename) | [Hosting](hosting.md), "Verification" |
| The Release API ran against local stand-ins (TimescaleDB 2.13.1, Dragonfly v1.27.1, GreptimeDB v1.1.4): health, session and prompt flow, idempotency, lease release, reaper, restart durability, traces and metrics | [smoke-backend-local-2026-10-21.md](evidence/smoke-backend-local-2026-10-21.md) |
| Railway refused creating the API service on 2026-10-21 ("Deploys have been paused temporarily") | [railway-autonogrammer-api-2026-10-21.md](evidence/railway-autonogrammer-api-2026-10-21.md) |
| A private staged worker (not the API) was created, configured with `update-service`, built with `railway up` and health-checked on 2026-10-28, in project `ompos-staging` | [railway-autonogrammer-worker-2026-10-28.md](evidence/railway-autonogrammer-worker-2026-10-28.md) |
| The static Demo web head is live on Vercel (project `ompos-web`, team `swcstudiospace`), built from `scripts/vercel/build-demo.sh` | [Web head runbook](web-demo.md) |
| The real web bundle (no Demo code) ran against the API in owner-token mode and the real `omp acp` with a scripted model, on one machine: sign-in, a streamed answer, an approved tool call, resume with `Last-Event-ID`, the rate limit; the browser flow ended with no console error and no failed request (2026-10-29) | [hosted-web-2026-10-29](evidence/hosted-web-2026-10-29/README.md) |
| The scripted accessibility checks ran against the built Demo and real bundles with zero error findings in their final runs (2026-10-29) | [web-accessibility-2026-10-29](evidence/web-accessibility-2026-10-29/README.md) |

Every step below ends with a status. "Not yet executed against a real deployment" means exactly that. A step that cites evidence says what the evidence covers.

## Preconditions

| Precondition | State |
|--------------|-------|
| Owner decision on the Railway project for the API: a new project, or `Ultrathink` (threat model Q-TM-1) | Open |
| Owner decision on the database role and the shared TimescaleDB (Q-TM-2, Q-BR-1) | Open |
| Units api-auth, api-hosting and engine-isolation of program 66 merged, so that `Api__Auth__Mode=OwnerToken`, the `Hosting__*` and `Harness__Acp__*` names exist in the image | Merged on the branch `feat/autonogrammer-integration`, not yet on `main`: pushing it and opening the pull request need the owner's approval |
| An owner token generated and its digest computed ([Incident response](incident-response.md), "Rotate the owner token") | Not done |
| A separate Vercel project (or an explicit decision to replace the Demo at `https://ompos-web.vercel.app`) for the real web head | Open (Q-DEP-1) |
| A model credential (HOST-04) | Not available; the smoke below cannot complete an agent turn without it |
| Railway CLI logged in (`railway whoami`) and a Railway MCP session | Present on the operator's host on 2026-10-28 (worker evidence) |

Placeholders used below: `$PROJECT`, `$ENVIRONMENT`, `$API_SERVICE` (Railway ids), `$API` (the API origin `https://<host>`), `$WEB` (the web head origin), `$TOKEN` (the owner token, typed into the shell, never stored).

## Deploy order

Postgres, then Dragonfly, then the API, then verification of health, authentication and CORS, then the web head with the API origin, then the smoke.

### Step 0: rehearse locally

Build the image from the commit to deploy and run it against the local backend stack with the production-shaped options.

```sh
REV="$(git rev-parse HEAD)"
podman build -f deploy/api.Containerfile --build-arg AUTONOGRAMMER_REVISION="$REV" -t autonogrammer-api:local .
podman run -d --rm --name ag-smoke -p 8080:8080 -e PORT=8080 -e ASPNETCORE_ENVIRONMENT=Development -e Harness__Adapter=InMemory autonogrammer-api:local
curl --retry 10 --retry-connrefused --retry-delay 2 -fsS http://127.0.0.1:8080/health/ready
curl -fsS http://127.0.0.1:8080/source
podman rm -f ag-smoke
```

Expect HTTP 200 for both, and `revision` equal to `$REV` in the second. This is the smoke of [Hosting](hosting.md); it was run on 2026-10-21 on an older tree. Re-run it on the commit to deploy.

To rehearse the production-shaped start (Postgres, Dragonfly, owner token), start the backend stack with `podman-compose -f deploy/compose.yaml --profile backend up -d` (the password variables `AUTONOGRAMMER_POSTGRES_PASSWORD` and `AUTONOGRAMMER_DRAGONFLY_PASSWORD` are required), then run the image with the connection strings, `Sessions__Store=Postgres`, `Coordination__Store=Dragonfly`, `Harness__Adapter=Acp`, `Api__Auth__Mode=OwnerToken` and a digest, and repeat the checks of step 5. Compose boots are recorded for the earlier stack (`evidence/ho-02-compose-boot-2026-10-21.log`); the rehearsal with the owner token is not.

Status: image build and Development smoke verified locally on 2026-10-21 (older tree). The same checks with the owner token, without a container, are executed: `scripts/hosted/run-local.sh` (see the first rows of the table above). The rehearsal in a container is not: the Containerfile changes of the security review of 2026-10-29 (read-only `/app`, `DOTNET_EnableDiagnostics=0`) have not been built, and neither has the `HEALTHCHECK`.

### Step 1: choose or create the project and environment

```sh
railway whoami
```

MCP: `list-projects`, or `create-project` for a new project (the worker was created this way on 2026-10-28). Record `$PROJECT` and `$ENVIRONMENT` from `list-services`.

Status: project creation verified for the worker only; not executed for the API.

### Step 2: Postgres

Use the TimescaleDB service of the topology ([Hosting](hosting.md)) or deploy a new one from a template (MCP `search-templates`, `deploy-template`). Check the version and that the API's schema does not yet exist, read-only, from a host that can reach the service (the pattern of the 2026-10-21 pre-check):

```sh
psql "$READONLY_URL" -X -c "SET default_transaction_read_only = on; SELECT version(); SELECT extversion FROM pg_extension WHERE extname = 'timescaledb'; SELECT schema_name FROM information_schema.schemata WHERE schema_name = 'autonogrammer';"
```

Expect PostgreSQL 16, `timescaledb` 2.13.1 or newer, and no `autonogrammer` row on a first deploy. Take a dump first if the schema exists ([Backup and restore](backup-and-restore.md)).

Status: the same checks passed against the shared TimescaleDB on 2026-10-21 (PostgreSQL 16.1, TimescaleDB 2.13.1, no schema); not executed for this deployment.

### Step 3: Dragonfly

Use the existing service, or deploy one. Pin the image by tag or digest instead of `latest` (Q-HOST-DFLY-PIN). Confirm that `DFLY_requirepass` is set on the service.

Verify reachability from the API in step 4: the API pings Dragonfly at start and refuses to start if the ping fails, and `/health/ready` runs the ping again.

Status: not executed.

### Step 4: create and deploy the API service

1. Create an empty service (MCP `create-service`), name `autonogrammer-api`.
2. Set the variables of `deploy/railway/README.md` with `set-variables` and `skipDeploys: true`. Minimum for the first deploy:

   | Variable | Value |
   |----------|-------|
   | `PORT` | `8080` |
   | `Sessions__Store` | `Postgres` |
   | `Sessions__Postgres__ConnectionString` | reference built from the Postgres service variables (secret) |
   | `Coordination__Store` | `Dragonfly` |
   | `Coordination__Dragonfly__ConnectionString` | reference built from the Dragonfly password (secret) |
   | `Harness__Adapter` | `Acp` |
   | `Harness__Acp__Environment__Mode` | `Allowlist` (with its allow list) |
   | `Harness__Acp__WorkspaceRoot` | an absolute directory |
   | `Api__Auth__Mode` | `OwnerToken` |
   | `Api__Auth__OwnerToken__Sha256__0` | the digest |
   | `Api__Cors__Origins__0` | `$WEB` (set after step 7, or now if the web origin is known) |
   | `Hosting__ForwardedHeaders__Mode` | `TrustAllSingleHop` (the default `None` makes every client look like the edge: shared throttles and no HSTS) |
   | `Autonogrammer__Debug__LogPrompts` | `false` |

3. Apply the build and deploy settings of `deploy/railway/api.railway.json` with `update-service`: `dockerfilePath` `deploy/api.Containerfile`, `healthcheckPath` `/health/ready`, `healthcheckTimeout` 120, `restartPolicyType` `ON_FAILURE`, `restartPolicyMaxRetries` 3, `drainingSeconds` 35, one replica.
4. Deploy from a clean archive of the commit (the way the worker was deployed). `redeploy` cannot give a service its first deployment, so use the CLI:

   ```sh
   git archive --format=tar HEAD | (rm -rf /tmp/ag-deploy && mkdir /tmp/ag-deploy && tar -x -C /tmp/ag-deploy)
   railway up -p "$PROJECT" -e "$ENVIRONMENT" -s "$API_SERVICE" --ci /tmp/ag-deploy
   ```

   Do not generate a domain.
5. Read the result:

   ```sh
   railway deployment list --service "$API_SERVICE"
   railway logs --service "$API_SERVICE" --lines 200
   ```

   Expect status `SUCCESS`, a listener on `[::]:8080`, and no startup error naming a key. A SIGABRT with exit code 134 is a configuration error (the options validator names the key in the log).

Status: the `create-service`, `set-variables`, `update-service` and `railway up` sequence was executed for the staged worker on 2026-10-28; nothing of this step has been executed for the API.

### Step 5: verify health, authentication and CORS before exposing the API

With no public domain yet, check from inside the container. The API image is `aspnet` on Ubuntu noble; `curl` may not be present, so use bash's `/dev/tcp` [INFERENCE: not run]:

```sh
railway ssh --service "$API_SERVICE"
# inside the container:
req() { exec 3<>/dev/tcp/127.0.0.1/8080; printf '%s\r\nHost: localhost\r\nConnection: close\r\n%s\r\n' "$1" "$2" >&3; head -n 1 <&3; exec 3<&-; }
req 'GET /health/live HTTP/1.1' ''        # expect HTTP/1.1 200
req 'GET /health/ready HTTP/1.1' ''       # expect HTTP/1.1 200
req 'GET /sessions HTTP/1.1' ''           # expect HTTP/1.1 401: authentication is enforced
```

Do not type the token into a command line (shell history); the 401 check is the one that matters here. If any answer is wrong, stop: the API is not exposed yet, so the cost is only a deploy.

Then generate the domain (MCP `generate-domain` with `{projectId, serviceId, environmentId}`), set `$API`, and check from outside:

```sh
curl -sS -o /dev/null -w '%{http_code}\n' "$API/health/ready"                                   # 200
curl -sS -o /dev/null -w '%{http_code}\n' "$API/sessions"                                       # 401
curl -sS -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $TOKEN" "$API/sessions"      # 200
curl -sS -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer wrong" "$API/sessions"       # 401
curl -sSi "$API/auth/config"                                                                    # 200, Cache-Control: no-store, body: mode owner-token, no secret
curl -sS "$API/source"                                                                          # revision equals the deployed commit
curl -sS -o /dev/null -w '%{http_code}\n' "$API/openapi/v1.json"                                # 401 (anonymous only in Development)
curl -sSI "$API/health/live" | grep -i -E 'strict-transport-security|x-content-type-options'    # headers from unit api-hosting
```

CORS (the web origin is allowed, a foreign one is not):

```sh
curl -sSi -X OPTIONS "$API/sessions" -H "Origin: $WEB" -H 'Access-Control-Request-Method: POST' \
  -H 'Access-Control-Request-Headers: authorization,content-type,idempotency-key' | grep -i '^access-control-allow-origin'   # equals $WEB
curl -sSi -X OPTIONS "$API/sessions" -H 'Origin: https://example.invalid' -H 'Access-Control-Request-Method: POST' \
  | grep -i '^access-control-allow-origin' || echo "no allow-origin header (expected)"
```

Also read the deploy log for the Coordination warning (event 32, store `InMemory` in Production) and for `RateLimitRejected` noise. Remove the domain again if any check fails ([Incident response](incident-response.md), "Contain").

Status: not yet executed against a real deployment. The 401 behaviour, the CORS policy and `/source` are read from the code (`Program.cs`, `Security/ConfigureCorsOptions.cs`, `Endpoints/SourceEndpoints.cs`); `/auth/config` and the security headers belong to units not merged when this page was written.

### Step 6: web head build with the API origin

Use a Vercel project for the real web head. The build fails closed without a valid origin (`scripts/vercel/render-csp.sh`: `https://host[:port]`, no path, wildcard or credentials).

```sh
vercel env add AUTONOGRAMMER_API_ORIGIN production      # value: the API origin, https only
vercel deploy --prod                                    # from a checkout of the same commit; AUTONOGRAMMER_DEMO must be unset
```

Check the deployed headers:

```sh
curl -sSI "$WEB/" | grep -i -E 'content-security-policy|x-frame-options|x-content-type-options'
```

Expect `connect-src 'self' <the API origin>` in the policy and no wildcard. Then set `Api__Cors__Origins__0` on the API to `$WEB` if step 4 did not, and redeploy the API.

Status: the Demo deployment path was executed on 2026-10-28 (`scripts/vercel/build-demo.sh` and `vercel deploy`); a build with `AUTONOGRAMMER_API_ORIGIN` has not been run on Vercel.

### Step 7: smoke

Open `$WEB` in a browser, enter the owner token once when asked, and create a session. In a shell:

```sh
SID="$(curl -fsS -X POST -H "Authorization: Bearer $TOKEN" "$API/sessions" | jq -r .id)"     # the field name id is an inference
curl -fsS -X POST -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -H "Idempotency-Key: smoke-$(date -u +%s)" -d '{"prompt":"say hello"}' "$API/sessions/$SID/prompts"     # 202
curl -fsSN -H "Authorization: Bearer $TOKEN" "$API/sessions/$SID/events"                                  # SSE until a terminal event
```

Expect 201 for the session, 202 for the prompt and an event stream that ends with `completed` or `error`. With no model credential the run ends in `error`; that proves routing, authentication and the store, not an agent turn. A real turn needs a model credential (HOST-04). After the run: `GET "$API/sessions/$SID"` returns 200; the session survives a restart of the API (`restart-service`, then the same GET).

The `scripts/hosted/` smoke scripts of the hosted-e2e unit, when merged, should replace the manual commands.

Status: the same flow ran against local stand-ins on 2026-10-21 (smoke evidence above); not yet executed against a real deployment.

### Step 8: record

Write the observed results, ids and times in `docs/runbooks/evidence/hosted-deploy-<date>.md`, then update the status lines of [Hosting](hosting.md). Set the Railway spend alert (`deploy/railway/README.md`, "Cost controls"). Create the external uptime probe ([SLOs and alerting](slo-and-alerting.md)). Run the first restore drill ([Backup and restore](backup-and-restore.md)) before inviting anyone.

## Rollback

Decide from the symptom: a bad build or bad configuration rolls back the API; a bad schema or data problem is a database restore, not a rollback.

### API on Railway

1. List deployments and find the last good one:

   ```sh
   railway deployment list --service "$API_SERVICE"
   ```

   MCP: `list-deployments` with `{projectId, serviceId, environmentId}`; note the `deploymentId` of the last `SUCCESS` deployment before the bad one.
2. Redeploy that deployment's build:

   MCP `redeploy` with `{projectId, environmentId, serviceId, deploymentId: "<last good>"}` (it reuses that deployment's build and does not rebuild). The dashboard action "Rollback" restores the previous deployment's image and its variables (https://docs.railway.com/guides/roll-back-bad-deploy, via the documentation search; the full page was not retrievable). Whether the MCP `redeploy` also restores the variables of that deployment is not stated in its description: read `list-variables` afterwards and fix them by hand if they differ.
3. Verify with the commands of step 5 (health, 401 without a token, 200 with it, `/source` revision equals the rolled-back commit).
4. A deployment older than the plan's image retention cannot be restored by rollback; rebuild from git with `railway up` from the old commit instead.
5. Variables changed together with the bad deploy: set the previous values with `set-variables` and redeploy. Keep the previous values in the operator's record before changing them.

Schema: a rollback does not undo schema statements (no version table, no down migration; [Backup and restore](backup-and-restore.md), "Migrations as built"). Check the release's schema statements before rolling back past them, and restore from the dump taken before the deploy if the old code cannot read the new schema.

Stop everything instead of rolling back: [Incident response](incident-response.md), "Contain".

### Web head on Vercel

1. List deployments: `vercel ls` (project scope).
2. Promote the previous production deployment: `vercel promote <deployment-url-or-id>`. `vercel rollback <deployment-url-or-id>` does the same for the production alias [INFERENCE: Vercel CLI commands recalled, not run here].
3. Verify: `curl -sSI "$WEB/" | grep -i content-security-policy`, and load the page.

If the API origin changed (a new Railway domain), the web head must be rebuilt: the CSP `connect-src` is generated at build time from `AUTONOGRAMMER_API_ORIGIN`, so a promote of an old deployment points at the old origin.

Status: not yet executed against a real deployment.

## Verification checklist

Copy this table into the deploy record and fill the last column.

| Step | Command | Expected | Result |
|------|---------|----------|--------|
| 0 | `curl -fsS http://127.0.0.1:8080/source` against the local image | `revision` is the commit | |
| 2 | read-only `psql` version and extension query | PostgreSQL 16, TimescaleDB 2.13.1 or newer, no `autonogrammer` schema on first deploy | |
| 4 | `railway deployment list --service "$API_SERVICE"` | `SUCCESS` | |
| 4 | `railway logs --service "$API_SERVICE" --lines 200` | listener `[::]:8080`, no key named in an error | |
| 5 | in-container `GET /health/ready` and `GET /sessions` | 200 and 401 | |
| 5 | `curl "$API/health/ready"` | 200 | |
| 5 | `curl "$API/sessions"` without a token, then with a wrong token | 401 and 401 | |
| 5 | `curl -H "Authorization: Bearer $TOKEN" "$API/sessions"` | 200 | |
| 5 | `curl -i "$API/auth/config"` | 200, `no-store`, mode `owner-token`, no secret | |
| 5 | `curl "$API/source"` | `revision` equals the deployed commit | |
| 5 | CORS preflight from `$WEB` and from a foreign origin | allow-origin equals `$WEB`; none for the foreign origin | |
| 6 | `curl -I "$WEB/"` | CSP `connect-src 'self' <API origin>`, no wildcard | |
| 7 | create session, prompt, read events | 201, 202, stream ends with a terminal event | |
| 7 | `restart-service`, then `GET /sessions/$SID` | 200, stored events | |
| 8 | restore drill | passes ([Backup and restore](backup-and-restore.md)) | |

## Questions for the orchestrator

| ID | Question |
|----|----------|
| Q-DEP-1 | Does the real web head get its own Vercel project, leaving the public Demo at `https://ompos-web.vercel.app` untouched? |
| Q-DEP-2 | Which commit of program 66 is the first to deploy, and who runs the first deploy with the Railway MCP and CLI? |
| Q-DEP-3 | Is the `[INFERENCE]` in-container `/dev/tcp` check acceptable, or should the image carry a probe tool? |

## How verified

- Read: [Hosting](hosting.md), `deploy/api.Containerfile`, `deploy/compose.yaml`, `vercel.ts`, `scripts/vercel/render-csp.sh`, `services/Autonogrammer.Api/Program.cs`, `Security/ConfigureCorsOptions.cs`, `Endpoints/SessionEndpoints.cs`, `Endpoints/SourceEndpoints.cs`, the evidence files named above.
- Railway tool contracts read on 2026-10-29: MCP `redeploy`, `set-variables`, `delete-domain`, `update-service`, `get-logs`; CLI pages `railway logs` and the CLI index; the Railway rollback documentation snippets.
- Not done: no command on this page was run in this work. The Vercel CLI commands are recalled, not read.
