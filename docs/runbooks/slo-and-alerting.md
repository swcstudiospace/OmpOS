---
description: "Service level indicators, proposed objectives, alert rules and uptime monitoring for the hosted Autonogrammer API, limited to what the API exports today."
---

# SLOs, alerting and uptime monitoring

Status: Proposed (objectives are proposals without measurements; the API is not deployed; no alert rule or monitor exists)

Review date: 2026-10-29

Owner role: deployment owner (single operator)

Applies to: the hosted API of [Hosting](hosting.md) at the integration commit of the rename

## Purpose

State which service level indicators (SLIs) can be measured from what the API exports today, propose objectives (SLOs) and an error-budget policy for one operator, and say which metrics must be added before the other indicators can be measured. Alert rules appear only for metric names that exist in the code or are named by the framework; every other indicator lists the metric to add.

## Scope

In scope: the API (`services/Autonogrammer.Api`) behind one Railway replica, its readiness endpoint, its session and prompt endpoints and the SSE stream. Out of scope: the static web head on Vercel beyond one probe, the cloud agent metrics (`Cloud:Enabled=false` by default) and model provider availability.

## What the API exports today

All custom instruments belong to one meter and one activity source named `Autonogrammer.Api` (`services/Autonogrammer.Api/Telemetry/ApiTelemetry.cs`). The exporter is chosen by `Otel:Exporter` (`none`, `console`, `otlp`; default `none`), so nothing leaves the process until the owner sets `otlp` and the `OTEL_EXPORTER_OTLP_*` variables ([Hosting](hosting.md), environment variables).

### Metrics

| Instrument | Type, unit | Defined at | Recorded at | Meaning |
|------------|-----------|------------|-------------|---------|
| `autonogrammer.sessions.count` | counter | `Metrics/ApiMetrics.cs` line 24 | `Endpoints/SessionEndpoints.cs` (`metrics.SessionCreated()`) | Sessions created through `POST /sessions` |
| `autonogrammer.stream.latency` | histogram, ms | `Metrics/ApiMetrics.cs` line 25 | `Sse/SessionEventStreamer.cs` line 175 | Time from an event's timestamp to its delivery on the SSE stream, per event |
| `autonogrammer.parser.fallback.rate` | counter | `Metrics/ApiMetrics.cs` line 26 | `Endpoints/SessionEndpoints.cs`, `Endpoints/RunEndpoints.cs` | Prompts that no recognizer claimed |
| `autonogrammer.sandbox.provision.time` | histogram, ms | `Metrics/ApiMetrics.cs` line 27 | `Workflows/ApiSessionRunObserver.cs` | Sandbox provisioning; with `FakeSandboxProvider` it measures a fake |
| `autonogrammer.cloud.compute.seconds`, `autonogrammer.cloud.agents.created`, `autonogrammer.cloud.agents.limit_refused`, `autonogrammer.cloud.agents.idle`, `autonogrammer.cloud.provision.failures`, `autonogrammer.cloud.provision.time` | counters and one histogram | `Cloud/CloudMetrics.cs` | cloud sweeper and endpoints | Only when `Cloud:Enabled=true` |
| ASP.NET Core server metrics | framework | `Program.cs` line 220 (`AddAspNetCoreInstrumentation`) | by the framework | Request count and duration by route and status. The instrument names (`http.server.request.duration`, `http.server.active_requests`) come from the OpenTelemetry semantic conventions and are not defined in this repository [INFERENCE: confirm the names in GreptimeDB before using them] |

### Traces

Spans on `Autonogrammer.Api`: `POST /sessions`, `POST /sessions/{id}/prompts`, `POST /sessions/{id}/runs`, `POST /cloud/agents/{id}/prompts` (the last only with cloud agents enabled) and `harness.run` (`Services/PromptRunner.cs` line 186). ASP.NET Core adds one server span per request.

### Health

| Endpoint | Checks | Status codes |
|----------|--------|--------------|
| `GET /health/live` | process answers (`self`) | 200 |
| `GET /health/ready` | `harness` (the omp file exists; it does not start omp), `repository` (a read of a missing id against the session store), `coordination` (Dragonfly `PING`, or an in-memory stub) | 200 when healthy; 503 when degraded or unhealthy |

Both are anonymous (`Program.cs` lines 265 to 275). Railway runs the platform health check at deploy time only ([Hosting](hosting.md), "Service settings"); it does not watch a running service. The unit api-hosting adds `GET /health/startup` and a draining state (contract C3); that is not read here.

### Session log (SQL)

`session_events` (hypertable, one row per stream event, `payload->>'kind'` is `text_delta`, `ui_block`, `error`, `completed`, `user_message`, `diagnostic` [INFERENCE: the stored JSON uses the discriminator `kind` of `StreamEventPayloadConverter`]) and `sessions` (`status` is the enum name: `Idle`, `Running`, `AwaitingToolApproval`, `Completed`, `Failed`, `Cancelled`, `Interrupted`). These give run outcomes and event timing without new code, at the cost of querying the production database. Schema: [Backup and restore](backup-and-restore.md).

## Service level indicators

| ID | SLI | Good event over valid events | Measurable today | Source or metric to add |
|----|-----|------------------------------|------------------|-------------------------|
| SLI-1 | Readiness availability | Probe of `GET /health/ready` answers 200 within 5 s, over all probes | Yes, with an external probe only | External probe (see "Uptime monitoring"). No metric: a down process emits nothing |
| SLI-2 | Request success ratio | Requests answered with status below 500, over all requests; 429 and 401 are counted good (caller faults), 5xx are bad | Yes, in two ways | Railway edge HTTP metrics (MCP tools `http-error-rate` and `http-requests`, once a public domain carries traffic [INFERENCE]); or the ASP.NET Core server metric, name unverified |
| SLI-3 | Prompt-to-first-event latency | Time from the `user_message` event of a turn to the first following `text_delta`, `ui_block` or `diagnostic` event of the session, at or below the threshold | Partly: by SQL on `session_events`; not as a metric. `autonogrammer.stream.latency` is per-event delivery lag, not this | Add `autonogrammer.run.first_event.latency` (histogram, ms, recorded by the run when it appends its first non-user event) |
| SLI-4 | SSE reconnect rate | Reconnects (requests to `/sessions/{id}/events` with `Last-Event-ID` or `?since=` above 0), over SSE connections | No. The server metric counts requests per route and cannot tell a resume from a first connection | Add `autonogrammer.sse.connections` (counter, tag `resumed` true or false) and `autonogrammer.sse.active` (up-down counter) |
| SLI-5 | Run failure ratio | Turns that end with a `completed` event, over turns that end with a `completed` or `error` event | Yes, by SQL on `session_events` (terminal events); not as a metric | Add `autonogrammer.runs.completed` (counter, tag `outcome` of `completed`, `failed`, `cancelled`) |

A `completed` event with reason cancelled (`SessionService.cs` line 760) counts as completed in the SQL form; use the metric with its `outcome` tag to separate it.

### SQL for SLI-3 and SLI-5

These run against the production database with a read-only role. They have not been executed.

```sql
-- SLI-5: terminal events per day (kind comes from payload->>'kind').
SELECT date_trunc('day', occurred_at) AS day,
       count(*) FILTER (WHERE payload->>'kind' = 'completed') AS completed,
       count(*) FILTER (WHERE payload->>'kind' = 'error')     AS failed
FROM autonogrammer.session_events
WHERE occurred_at >= now() - interval '30 days'
GROUP BY 1 ORDER BY 1;

-- SLI-3: seconds from each user_message to the next event of that session that is not a user_message.
SELECT percentile_cont(0.95) WITHIN GROUP (ORDER BY lag_s) AS p95_s
FROM (
  SELECT u.session_id,
         extract(epoch FROM (
           SELECT min(e.occurred_at) FROM autonogrammer.session_events e
           WHERE e.session_id = u.session_id AND e.seq > u.seq
             AND e.payload->>'kind' IN ('text_delta', 'ui_block', 'diagnostic')
         ) - u.occurred_at) AS lag_s
  FROM autonogrammer.session_events u
  WHERE u.payload->>'kind' = 'user_message' AND u.occurred_at >= now() - interval '7 days'
) t WHERE lag_s IS NOT NULL;
```

## Proposed objectives

The targets are proposals for one replica and one operator. There is no measurement behind them. The omp handshake alone takes about 8 seconds on the reference host (comment in `src/Autonogrammer.Harness.Acp/AcpClientOptions.cs`, 45 s handshake timeout), so the latency target is wide; tighten it after four weeks of data.

| ID | Objective | Window | Error budget |
|----|-----------|--------|--------------|
| SLO-1 | 99.5% of readiness probes succeed (SLI-1) | 30 days, rolling | 0.5% of probes, about 3 h 36 min of a 30-day month at one probe a minute |
| SLO-2 | 99% of requests are not 5xx (SLI-2) | 30 days | 1% of requests |
| SLO-3 | 95% of turns reach a first event within 30 s (SLI-3) | 30 days | 5% of turns |
| SLO-4 | 98% of turns end `completed` (SLI-5), excluding `error` events whose message names a provider fault | 30 days | 2% of turns |
| SLO-5 | No target set for SLI-4 until it is measured | n/a | n/a |

### Error-budget policy

| Condition | Action |
|-----------|--------|
| More than 50% of a budget spent before day 15 | Review the incidents behind it; no new feature deploy until the cause is understood |
| Budget spent | Freeze deploys except fixes for the objective that failed, until the 30-day window recovers or the owner records a decision to change the objective |
| A single incident spends more than 25% of a budget | Post-incident review within 3 working days ([Incident response](incident-response.md)) |
| An objective is met with a wide margin for two windows | Tighten it by one step and record the date here |

## Alert rules

The owner is the only recipient. "Notify" means a message the owner sees within minutes (for example a Railway project webhook posted to a chat or email bridge, or the external monitor's own alert). No notification channel is configured.

### Rules the code supports today

Metric names are those of `Metrics/ApiMetrics.cs`. The query form assumes the exporter output lands in GreptimeDB through its OTLP endpoint with dots turned to underscores, as in Prometheus naming, and counters get the `_total` suffix and histograms the `_bucket`, `_sum`, `_count` series [INFERENCE: the translation was not observed; the 2026-10-21 local smoke recorded traces and metrics arriving in GreptimeDB, see `evidence/smoke-backend-local-2026-10-21.md`, but not the table names]. Run `SHOW TABLES` on the GreptimeDB database and correct the names before enabling a rule.

| Rule | Query (Prometheus style) | For | Severity |
|------|--------------------------|-----|----------|
| Slow event delivery | `histogram_quantile(0.95, sum by (le) (rate(autonogrammer_stream_latency_bucket[5m]))) > 2000` | 10 m | SEV3 |
| Parser fallback spike (a sign of unexpected prompt shapes) | `increase(autonogrammer_parser_fallback_rate_total[1h]) > 50` | 0 | SEV4 |

### Rules that need the metric

| Rule | Needs | Expression once the metric exists | Severity |
|------|-------|-----------------------------------|----------|
| Readiness down | external probe (SLI-1) | probe fails 3 times in a row, one minute apart | SEV2 |
| Burn of SLO-2 | server request metric, name unverified; or Railway `http-error-rate` | 5xx ratio above 14.4% over 1 h and above 14.4% over 5 m (burns the 1% budget in about 2 days at that rate) | SEV2 |
| Slow burn of SLO-2 | same | 5xx ratio above 6% over 6 h and 30 m | SEV3 |
| Run failure burst | `autonogrammer.runs.completed` | `sum(rate(autonogrammer_runs_completed_total{outcome="failed"}[15m])) / sum(rate(autonogrammer_runs_completed_total[15m])) > 0.2` | SEV3 |
| First event slow | `autonogrammer.run.first_event.latency` | p95 over 15 m above 30 000 ms | SEV3 |
| SSE reconnect storm | `autonogrammer.sse.connections` | resumed share above 0.5 over 15 m | SEV3 |

### Platform signals that exist without code

| Signal | How to read it | Use |
|--------|----------------|-----|
| Deployment status | Railway MCP `list-deployments`, CLI `railway deployment list` | A failed deploy or a crash loop |
| Service metrics (CPU, memory, disk, network) | Railway MCP `get-service-metrics` | Saturation; the memory limit |
| Edge HTTP metrics | Railway MCP `http-requests`, `http-error-rate`, `http-response-time` | SLI-2 and a latency proxy, with a public domain |
| Webhooks | Railway MCP `create-webhook` (events: deployment, monitor, volume alert) | Route platform events to the owner |

Whether Railway offers metric-threshold monitors on the plan in use is not verified here.

## Uptime monitoring

Use what the platforms include first, then one external probe.

| Layer | Tool | What it covers | Gap |
|-------|------|----------------|-----|
| Railway | Deploy-time health check on `/health/ready` (`healthcheckPath`, `healthcheckTimeout`), restart policy `ON_FAILURE` | A new deployment must answer 200 before it replaces the old one; a crashed process restarts | No watch on a running service; a hung but alive process is not restarted; a failed dependency after deploy is not seen |
| Vercel | The build must succeed before the alias moves; the production alias serves a static bundle | The web head | No probe of the API; no alert |
| External probe | Any free HTTP checker: a hosted free tier of an uptime service, a self-hosted checker, or a scheduled job in the repository's CI. Pick one on interval, alert channel and terms | `GET <api-origin>/health/ready` expecting 200 and `GET <web-origin>/` expecting 200 | Intervals and free-tier limits of any named service are not verified here |

Probe rules:

- Probe `/health/ready` without a token; it is anonymous. Do not send the owner token to a third party.
- Probe at one-minute to five-minute intervals. Each probe runs a database read and a Dragonfly `PING`.
- Require 3 consecutive failures before alerting, to ride out a deploy.
- Record the probe's own location and outages; a probe outage is not an API outage.

## Status page

There is no status page. For one operator the owner writes the status update in the incident communication template ([Incident response](incident-response.md)) and posts it where the users are. A public status page is optional and not required before the first public domain.

## How verified

- Read: `services/Autonogrammer.Api/Metrics/ApiMetrics.cs`, `Cloud/CloudMetrics.cs`, `Telemetry/ApiTelemetry.cs`, `Health/*.cs`, `Coordination/CoordinationRegistration.cs`, `Program.cs` (lines 197 to 229 and 265 to 275), `Sse/SessionEventStreamer.cs`, `Services/PromptRunner.cs`, `src/Autonogrammer.Sessions.Postgres/PostgresSessionSchema.cs`, `src/Autonogrammer.Contracts/Streaming/StreamEventPayloadConverter.cs`, `src/Autonogrammer.Sessions/SessionService.cs`.
- Not done: no metric was observed in GreptimeDB (the API is not deployed, and the local smoke of 2026-10-21 did not record table names); no SQL above was run; no probe was created; no objective has data.
- Open: the notification channel, the probe service and the names of the new metrics are owner decisions.
