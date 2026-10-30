---
description: "What personal and sensitive data the hosted Autonogrammer API stores, how long it keeps it as built, how to export and delete it with SQL today, and what is not possible."
---

# Data inventory and retention (hosted API)

Status: Draft (describes the code as built; it is a technical statement, not a privacy policy and not legal advice; the API is not deployed)

Review date: 2026-10-29

Owner role: deployment owner (single operator); privacy decisions are the owner's

Applies to: the hosted API of [Hosting](../runbooks/hosting.md) with `Sessions:Store=Postgres` and `Coordination:Store=Dragonfly`

## Purpose

Say plainly what the hosted API keeps, for how long, where, and what the operator can export or delete today. The operator uses it to answer a user's request about their data and to decide retention settings before the first user is admitted.

## Scope

In scope: the hosted API and the stores it writes to. Out of scope: the model provider's own retention (the prompt and the files the agent reads are sent to the provider the owner configured; read that provider's terms), the Demo build of the web head (no backend, no stored data), cloud agents and automations (off by default), the desktop heads, and Railway's and Vercel's own account and billing data.

The program is single-operator. There is one subject (the owner token's `Api:Auth:OwnerToken:Subject`, default `owner`), so "user" below means the operator and anyone the operator lets use the token. If an OIDC authority is configured instead (OD-03, not decided), `owner_id` holds the token's `sub` claim, which is personal data about that user.

## Inventory

| Data | Where | Personal or sensitive | Retention as built | Removable today |
|------|-------|-----------------------|--------------------|-----------------|
| Prompts the user typed | `autonogrammer.session_events.payload` (kind `user_message`, field `text`) and `autonogrammer.sessions.messages` (jsonb) | Yes: free text, may contain anything the user typed or pasted, including secrets | Forever. The code has no time limit, no purge and no TimescaleDB retention policy | By SQL (below); no API |
| Agent replies | Same tables (kind `text_delta`, messages) | Yes: derived from prompts and files | Forever | By SQL |
| Tool calls, tool results, file edits and diffs | `session_events.payload` (kind `ui_block`: blocks `tool_call` with arguments, `tool_result`, `file_edit`, `diff`) | Yes: file contents, command lines, command output; may contain secrets the tools printed | Forever | By SQL |
| Tool approval decisions | `autonogrammer.session_tool_approvals` | Low: tool name, decision, times | Forever | By SQL |
| Owner identifier | `sessions.owner_id`, `cloud_agents.owner_id` | The configured subject; personal only with OIDC | Forever | By SQL |
| Session timestamps and status | `sessions`, `session_events.occurred_at` | Behavioural metadata | Forever | By SQL |
| Owner token | Never stored. Only its SHA-256 digest is configured (`Api__Auth__OwnerToken__Sha256__0`) as a Railway variable | The digest is not personal data and cannot be reversed for a token of 32 or more random bytes | Until rotated | Rotate ([Incident response](../runbooks/incident-response.md)) |
| Provider keys | Railway variables `AUTONOGRAMMER_PROVIDER_<ID>_KEY`, and the omp agent directory (`models.yml`, credentials), where omp keeps them unencrypted | Secret | Until revoked | Revoke at the provider; delete the variable; delete the directory |
| Run leases | Dragonfly `autonogrammer:lease:run:<session>` | No | 30 seconds unless renewed | Expire by themselves |
| Idempotency records | Dragonfly `autonogrammer:idem:<sha256 of scope and key>`; a completed record holds the response JSON | Low: session id, accepted prompt response | 60 seconds pending, 24 hours completed (`CoordinationDefaults`) | Expire by themselves; `DEL` by key |
| Application logs | Railway deploy logs (stdout) | Low: request method and path, session ids, correlation ids, rate-limit warnings, error text. Prompts are logged as `<redacted len=N>` | The platform's retention for the plan [INFERENCE: not read] | Not by the operator |
| Traces and metrics | GreptimeDB when `Otel:Exporter=otlp`; span tags include `autonogrammer.session.id` | Low | The GreptimeDB database's setting [INFERENCE: unknown] | By GreptimeDB, not by this repository |
| Backups and dumps | Wherever the operator stores them ([Backup and restore](../runbooks/backup-and-restore.md)); Railway volume snapshots | All of the above | The operator's schedule | By deleting the copy |
| Browser state | Nothing persisted: the web head keeps the token in memory, with no cookie, `localStorage`, `sessionStorage` or IndexedDB | n/a | Until the page closes | n/a |
| Static host logs | Vercel access logs for the web head | IP address, user agent [INFERENCE] | Vercel's retention | Not by the operator |

Evidence: `src/Autonogrammer.Sessions.Postgres/PostgresSessionSchema.cs`; `src/Autonogrammer.Contracts/Streaming/*` and `Ui/UiBlockConverter.cs` (payload shapes); `src/Autonogrammer.Coordination/CoordinationDefaults.cs`; `src/Autonogrammer.Coordination.Dragonfly/DragonflyKeys.cs`; `services/Autonogrammer.Api/Options/DebugOptions.cs`, `Services/PromptRedactor.cs`; `docs/decisions/ADR-0031-data-plane-namespaces-and-resilience.md` ("no retention exists"). The column names are from the schema; the JSON field names inside `payload` are inferred from the contract types and must be checked on real rows before a query depends on them.

## Logging policy

- `Autonogrammer__Debug__LogPrompts` is `false` in `appsettings.json` and must stay `false` on the hosted API. With it off, a prompt is logged as `<redacted len=N>`, its length only.
- If it is switched on for a diagnosis, switch it off in the same working session, redeploy, and treat every log line from that window as containing prompts: the platform's retention applies to them and the operator cannot delete them.
- Tool arguments and outputs are not covered by that switch: they are part of the session log in the database, not of the application log.
- Never log a token, key or connection string. The code names the configuration key in an error and never the value; keep any added logging to the same rule.
- Do not paste prompts, session payloads or dumps into an issue, a chat or a public place.

## Export, today

The API has no export endpoint and no per-user export. The session is readable through the API by its owner:

- `GET /sessions/{id}` returns the session with its messages.
- `GET /sessions/{id}/events` replays the event log as server-sent events from the start (no `Last-Event-ID`). The stream ends at the first terminal event (`completed` or `error`), so a session with several turns needs `?since=<last seq>` calls to read the rest.
- `GET /sessions` lists sessions, but from an in-memory index that is empty after a restart.

For a complete export, use SQL with a read-only role. These statements have not been executed.

```sh
SID='<session id>'
psql "$READONLY_URL" -At -c "SELECT row_to_json(s)::text FROM autonogrammer.sessions s WHERE id = '$SID'" > session-$SID.json
psql "$READONLY_URL" -At -c "SELECT payload::text FROM autonogrammer.session_events WHERE session_id = '$SID' ORDER BY seq" > events-$SID.jsonl
psql "$READONLY_URL" -At -c "SELECT row_to_json(a)::text FROM autonogrammer.session_tool_approvals a WHERE session_id = '$SID'" > approvals-$SID.jsonl
```

For everything of one owner, replace the session filter with `session_id IN (SELECT id FROM autonogrammer.sessions WHERE owner_id = '<owner>')`. Deliver the files over a channel the owner trusts, and delete the local copies after.

## Delete, today

There is no delete endpoint: the session store has no delete operation (`ISessionRepository`; the cloud agent endpoint says "the session log stays: the session store has no delete"). Deletion is SQL against production by the operator. Stop runs on the session first (no session in status `Running` or `AwaitingToolApproval`), take a dump ([Backup and restore](../runbooks/backup-and-restore.md)), then run in one transaction. Order matters: the child tables reference `sessions`.

```sql
BEGIN;
DELETE FROM autonogrammer.session_tool_approvals WHERE session_id = '<id>';
DELETE FROM autonogrammer.session_events          WHERE session_id = '<id>';
DELETE FROM autonogrammer.sessions                WHERE id = '<id>';
COMMIT;
```

For all sessions older than a date, select the ids from `sessions.updated_at` instead. For an owner, select on `owner_id`. If cloud agents were ever enabled, also `DELETE FROM autonogrammer.cloud_agents WHERE session_id = '<id>'` (no foreign key links it).

After a delete:

- The API's in-memory indexes may still list the id until the process restarts; restart the API.
- Idempotency records for the session expire within 24 hours; nothing links them to a session id, so there is no per-session purge.
- The data stays in every dump and volume snapshot taken before the delete, until that copy ages out. Tell the requester the real figure (the operator's dump retention).
- The model provider may have kept the prompts. The operator cannot delete them from here.

## What is not possible today

| Not possible | Why | What would make it possible |
|--------------|-----|-----------------------------|
| Delete through the API, or by the user themself | No delete operation exists in the store or the endpoints | An authenticated delete endpoint and a repository method (not planned in program 66) |
| Automatic expiry of old sessions | No TTL in the code and no TimescaleDB retention policy. A retention policy on `session_events` would drop whole one-day chunks and leave sessions with a partial log, which breaks the no-gap promise of the repository and the replay of the stream | A design that expires a session as a unit |
| Redact one event or one message | The log is append-only by design; nothing updates an event row | A redaction operation with an audit trail |
| Per-user export through the API | One identity; no export endpoint | An export endpoint |
| Purge of one session's idempotency record | The key is a hash of scope and key; nothing maps it back | Wait 24 hours |
| Delete application logs or telemetry | They live in Railway and GreptimeDB | The platform's own controls |
| Prove that a deleted session is gone from backups | Dumps are opaque copies | Short dump retention |

## Recommended defaults

These are proposals for the owner to accept or change before users are admitted.

| Setting | Proposal |
|---------|----------|
| Session retention | 90 days after `updated_at`, enforced by a scheduled manual job of the SQL above until the product has a delete operation |
| Dump retention | 30 days, encrypted, outside the repository |
| `Autonogrammer__Debug__LogPrompts` | `false` |
| `Otel__Exporter` | `none` until the telemetry store has a stated retention and access list |
| `Providers__AllowCredentialWrite`, `Providers__AllowConfigWrite` | `false` (the defaults) |
| Who may hold the owner token | One person |
| Notice to users | State that prompts, replies, tool output and file contents are stored without time limit, shared with the configured model provider, and deletable only on request to the operator |

## Questions for the orchestrator

| ID | Question |
|----|----------|
| Q-DR-1 | Which retention does the owner choose for sessions and for dumps (the proposals above are placeholders)? |
| Q-DR-2 | Is a delete or export endpoint wanted for the hosted API? It needs new code and is outside this program's units. |
| Q-DR-3 | What are Railway's log retention and GreptimeDB's retention for the plan and database in use? |
| Q-DR-4 | Is the owner legally required to publish a privacy notice for the intended audience? This page does not answer that. |

## How verified

- Read: the Postgres schema classes, the stream and UI contract converters, `CoordinationDefaults.cs`, `DragonflyKeys.cs`, `DebugOptions.cs`, `PromptRedactor.cs`, `appsettings.json`, [Cloud agents](../runbooks/cloud-agents.md) (session store has no delete), ADR-0031, [Web key threat model](../runbooks/web-key-threat-model.md) (memory-only key).
- Searched the Postgres and Dragonfly projects for `retention`, `DELETE FROM`, `TRUNCATE` and TTL settings. The only `DELETE FROM` is the cloud agent row delete; the only expiry is in Dragonfly.
- Not verified: no SQL above was executed; JSON field names inside payloads are inferred; Railway, GreptimeDB and Vercel retention are not read; nothing was run against a deployment.
