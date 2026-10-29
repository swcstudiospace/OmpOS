---
description: "Incident response for the hosted Autonogrammer API on Railway: severities, roles, the first fifteen minutes, token rotation, containment, communication and review templates."
---

# Incident response

Status: Draft (no step below has been executed against a real deployment; the API is not deployed)

Review date: 2026-10-29

Owner role: deployment owner (single operator)

Applies to: the hosted API of [Hosting](hosting.md) with the options of the [hosted threat model](../security/hosted-threat-model.md)

## Purpose

Give the operator one page to open when the hosted API misbehaves: how to classify it, what to look at in the first fifteen minutes, how to contain it, how to rotate the owner token, what to tell users and how to review it afterwards.

## Scope

In scope: the Railway services of the hosted API (API, Postgres, Dragonfly), the Vercel web head, and the secrets in their environments. Out of scope: incidents of the model provider, of GitHub, and of the desktop heads.

Every command below is shown as the Railway MCP tool call or the Railway CLI command that exists today. Nothing here has been run against a real API service, because none exists yet ([Hosting](hosting.md), "Status"). Mark each step "verified" in the review after it works once.

## Severities

There is no on-call rota. The response targets are the operator's own commitment and are proposals.

| Severity | Definition | Examples | Start working within |
|----------|------------|----------|----------------------|
| SEV1 | Confirmed or suspected compromise, data exposure or data loss | Owner token or provider key leaked; runs nobody started; unexpected tool calls; database data deleted | Immediately; containment before diagnosis |
| SEV2 | The service is down or unusable for its single user | `/health/ready` fails; every prompt fails; crash loop | 30 minutes |
| SEV3 | Degraded | Slow first event, elevated failure ratio, SSE drops, one feature broken | Next working day |
| SEV4 | Minor, no user impact | Log noise, a warning at startup | Backlog |

When unsure between two levels, take the higher one until facts say otherwise.

## Roles

The deployment has one operator, who holds every role. The roles still matter because each has its own task and the operator should say which hat is on in the notes.

| Role | Task | With a second person |
|------|------|----------------------|
| Incident owner | Decides containment, rollback and when the incident ends | The operator |
| Investigator | Reads logs, metrics, the session log; keeps the timeline | A second person, read-only access to Railway |
| Communicator | Posts the updates of the template below | Either |
| Scribe | Writes the timeline while it happens | Either |

## First 15 minutes

Open a private note and write the time. Do not put tokens, keys or prompt text in it.

### Minute 0 to 2: classify

1. Write what was seen, by whom, and when it started.
2. Pick a severity. If the cause may be a leaked secret or unrequested runs, treat it as SEV1 and go to "Contain" first.

### Minute 2 to 7: look

Use the IDs of the project, environment and API service (the `list-services` MCP tool or the dashboard gives them).

| Question | Railway MCP | Railway CLI |
|----------|-------------|-------------|
| Is the service up, and which deployment is live? | `environment-status`, `list-deployments` with `{projectId, serviceId, environmentId}` | `railway deployment list` |
| What do the logs say? | `get-logs` with `{projectId, serviceId, environmentId, types: ["deploy"], filter: "@level:error", limit: 200}` | `railway logs --service <name> --lines 200 --filter "@level:error"` |
| Did the last deploy fail to build or start? | `get-logs` with `types: ["build"]` and the `deploymentId`; `get-deployment-diagnosis` for a failed deployment | `railway logs --build` |
| Are requests failing at the edge? | `http-error-rate`, `http-requests`, `http-response-time`; `get-logs` with `types: ["http"]` and `filter: "@status:500"` | `railway logs --http --filter "@httpStatus:500..599"` |
| CPU, memory, disk | `get-service-metrics` | dashboard |
| Is the database up? | `describe-service` and `get-logs` for the Postgres and Dragonfly services | `railway logs --service <name>` |
| What does the API itself say? | none | `curl -sS -o /dev/null -w '%{http_code}\n' https://<api-origin>/health/ready` (anonymous). 200 healthy; 503 means one of `harness`, `repository`, `coordination` failed; read the logs for which |
| Which build is running? | none | `curl -sS https://<api-origin>/source` (anonymous) returns the licence, version and `revision` |

Useful log lines in this tree: the rate limiter writes a warning `RateLimitRejected` (event id 1) with method and path; the coordination startup writes a warning when the store is `InMemory` in Production and when the Dragonfly `PING` fails at start; options validation failures name the key (`Sessions:Postgres:ConnectionString`, `Api:Auth:Authority`) and never print the value.

If the process exits at start, `docs/runbooks/evidence/smoke-backend-local-2026-10-21.md` (finding 2) records that a startup failure aborts with SIGABRT (exit code 134), so a crash loop with 134 is a configuration error, not a memory fault.

### Minute 7 to 12: decide

| Finding | Decision |
|---------|----------|
| Last deployment is less than a few hours old and the failure began after it | Roll back ([Deploy and rollback](deploy-and-rollback.md)) |
| A dependency (Postgres or Dragonfly) is down | Check the dependency's own logs and volume; restart it; the API recovers by itself because `/health/ready` re-probes. No rollback |
| Secret leak or unexpected runs | Contain, then rotate (below) |
| Out of memory or CPU | Raise the limit with `update-service` (see `deploy/railway/README.md`), then find the cause |
| Cause unknown | Restart once (`restart-service` or `railway restart`); if it recurs, roll back to the last known good deployment |

### Minute 12 to 15: communicate and note

Post the first update ([template](#communication-template)). Record the decision and the time.

## Contain

Pick the least drastic step that stops the harm. Each step is reversible.

| Step | Effect | How | Undo |
|------|--------|-----|------|
| 1. Cancel one run | Stops one agent turn | `POST /sessions/{id}/runs/{runId}/cancel` with the bearer token | n/a |
| 2. Remove public exposure | The API is reachable only from the private network; the web head cannot reach it | MCP `list-domains` then `delete-domain` with `{projectId, serviceId, environmentId, domain}`; or the dashboard, service settings, networking | `generate-domain` (a new random hostname; update the web head's API origin, [Deploy and rollback](deploy-and-rollback.md)) |
| 3. Stop the engine | No `omp acp` child can start, so no agent runs; the data stays | CLI `railway down --service <name>` removes the latest successful deployment and keeps the service, its variables and volume (Railway CLI documentation) | `railway redeploy`, or `railway up` to build again; a rollback target may be gone, so note the deployment id first |
| 4. Revoke the owner token | The token no longer works | Rotate (next section) with no overlap | n/a |
| 5. Revoke the provider key | The model key is dead | At the provider's console. Then replace the variable or the omp credential | New key |

Order for a suspected compromise: remove the domain (2), stop the engine (3), rotate the token (4), revoke the provider key (5), then investigate. Preserve evidence before step 3 or step 4 changes anything:

```sh
railway logs --service <api-service> --since 24h --json > /secure/place/incident-<date>.jsonl
```

The file may contain request paths and session ids. Prompts are logged as `<redacted len=N>` unless `Autonogrammer__Debug__LogPrompts` was set to `true`. Keep the file out of the repository.

## Rotate the owner token

The token is a random string of at least 32 bytes, typed by the operator once and never stored by the API; only its SHA-256 digest is configured (`Api__Auth__OwnerToken__Sha256__0`, contract C2). These steps depend on unit api-auth; the digest rule (SHA-256 of the UTF-8 bytes of the token, lowercase hexadecimal) matches that unit's worktree when this page was written and must be re-checked after the merge.

1. Generate a new token on the operator's own machine:

   ```sh
   NEW_TOKEN="$(openssl rand -base64 48 | tr '+/' '-_' | tr -d '=\n')"
   ```

   Keep the shell variable; do not write it to a file.

2. Compute its digest:

   ```sh
   printf '%s' "$NEW_TOKEN" | sha256sum | cut -d ' ' -f1
   ```

3. Set the variable.
   - Planned rotation, with overlap: set `Api__Auth__OwnerToken__Sha256__1` to the new digest, keep `__0`, redeploy, switch the browser to the new token, then delete `__0` and redeploy again. The unit's validator checks the number and the format of the digests, not their positions (seen in the unit's unmerged worktree). `set-variables` only sets; delete a variable with `railway variable delete Api__Auth__OwnerToken__Sha256__0 --service <api-service>` or in the dashboard.
   - Compromise, no overlap: replace `Api__Auth__OwnerToken__Sha256__0` with the new digest and delete every other `Api__Auth__OwnerToken__Sha256__*` variable.

   MCP: `set-variables` with `{projectId, serviceId, environmentId, variables: {"Api__Auth__OwnerToken__Sha256__0": "<digest>"}}`. CLI: `railway variable set Api__Auth__OwnerToken__Sha256__0=<digest> --service <api-service>`.

4. Redeploy so the process reads it: `set-variables` redeploys the service unless `skipDeploys` is set; otherwise `redeploy` (MCP) or `railway redeploy`.

5. Verify:

   ```sh
   curl -sS -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $NEW_TOKEN" https://<api-origin>/sessions    # expect 200
   curl -sS -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $OLD_TOKEN" https://<api-origin>/sessions    # expect 401
   ```

6. Clear the token from the shell (`unset NEW_TOKEN OLD_TOKEN`) and from the browser tab that held the old one.

The digest can live in a plain Railway variable because a token of 32 or more random bytes cannot be recovered from it. The token itself must never be written to a Railway variable, a log, a URL or the repository.

Other secrets: the Postgres and Dragonfly passwords belong to their services (change them there, then the API's connection-string variables, then redeploy); the OTLP header and the provider key are API variables (`OTEL_EXPORTER_OTLP_HEADERS`, `AUTONOGRAMMER_PROVIDER_<ID>_KEY`); see `deploy/railway/README.md`.

## Investigate a suspected abuse or leak

All queries are read-only and have not been executed. Run them with a read-only role; do not use the API's own credential.

```sh
# Sessions touched in the window
psql "$READONLY_URL" -c "SELECT id, owner_id, status, created_at, updated_at FROM autonogrammer.sessions WHERE updated_at >= TIMESTAMPTZ '<start>' ORDER BY updated_at"
# Tool calls in the window (the discriminator names are an inference from the contract converters)
psql "$READONLY_URL" -c "SELECT session_id, seq, occurred_at FROM autonogrammer.session_events WHERE occurred_at >= TIMESTAMPTZ '<start>' AND payload->>'kind' = 'ui_block' AND payload->'block'->>'kind' = 'tool_call' ORDER BY occurred_at"
```

Look for sessions the operator did not start, tool calls that read the environment or the omp directory, and outputs that contain connection strings. Note that the log can only show what the engine reported, not what a compromised process did.

## Communication template

```text
Subject: [Autonogrammer hosted] <SEV> <one-line impact> - <status: investigating | identified | monitoring | resolved>

Time (UTC): <timestamp>
Impact: <who or what cannot do what, since when>
Cause: <known cause, or "not yet known">
Action taken: <rollback, domain removed, token rotated, ...>
Next update: <time, or "when the state changes">
Needs from users: <re-enter the token / nothing>
```

Send the first update within 15 minutes of declaring SEV1 or SEV2, then at every state change. If personal or confidential data may have been exposed, the privacy statement ([Data retention](../privacy/data-retention.md)) and the owner's legal obligations decide who must be told; that decision is the owner's and is not made here.

## Post-incident review template

Hold the review within three working days of a SEV1 or SEV2. No blame; describe systems, not people.

```text
Title:
Date, duration, severity:
Summary (three sentences):
Impact (users, data, money):
Timeline (UTC): detection, declaration, containment, resolution
Root cause and contributing factors:
What went well:
What went badly:
Where we got lucky:
Error budget spent (SLO ids, see slo-and-alerting.md):
Action items: id | action | owner | due | tracked in
Docs to update (runbook steps that were wrong or missing):
```

Mark in this runbook which steps worked ("verified on <date>") and fix the ones that did not. Record the incident in the incident log the owner keeps; the repository holds no incident log.

## How verified

- Read: [Hosting](hosting.md), [Cloud agents](cloud-agents.md), `deploy/api.Containerfile`, `services/Autonogrammer.Api/Program.cs`, `Security/RateLimitRejection.cs`, `Coordination/CoordinationStartup.cs`, `docs/runbooks/evidence/smoke-backend-local-2026-10-21.md`.
- Tool contracts read on 2026-10-29: the Railway MCP tool schemas `get-logs`, `redeploy`, `set-variables`, `delete-domain`, `update-service`; the Railway CLI pages `railway logs` (flags `--service`, `--lines`, `--since`, `--filter`, `--json`, `--build`, `--http`) and the CLI index (`railway deployment list`, `railway redeploy`, `railway restart`, `railway down`, `railway variable set`).
- Not verified: the HTTP log filter field name `@httpStatus` is taken from the `railway logs` filter syntax example, not from a run; the behaviour of `railway down` (removes the latest successful deployment) is from the CLI documentation; the digest rule and the contiguity of the digest list belong to a unit that was not merged when this page was written; no step was executed.
