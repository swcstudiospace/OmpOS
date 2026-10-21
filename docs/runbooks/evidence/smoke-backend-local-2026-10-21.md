# Local backend smoke, 2026-10-21

Scope: the real `OmpOS.Api` binary (Release build from this tree, .NET 10.0.13 runtime) against production-matched
stand-ins for the three Railway services. Recorded so the Railway deploy can be compared against a known-good local run.

## Stand-ins

| Role | Image | Notes |
| --- | --- | --- |
| Session store | `timescale/timescaledb:2.13.1-pg16` | PostgreSQL 16.1, TimescaleDB 2.13.1. Railway runs `timescale-postgis-ssl:pg16-ts2.13`; local has no TLS and no PostGIS. |
| Coordination | `dragonflydb/dragonfly:v1.27.1` | `requirepass` set. Railway runs `ghcr.io/dragonflydb/dragonfly:latest` (unpinned). |
| Telemetry | `greptime/greptimedb:v1.1.4` | Standalone, no user provider. Railway enables `static_user_provider`, so OTLP there also needs `Authorization: Basic`. |

Configuration (secrets omitted): `ASPNETCORE_ENVIRONMENT=Production`, `Sessions__Store=Postgres`,
`Coordination__Store=Dragonfly`, `Otel__Exporter=otlp`, `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf`,
`OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:14000/v1/otlp`, and the pipeline header in `OTEL_EXPORTER_OTLP_HEADERS`
(see finding 1).

## Results

| Check | Observed |
| --- | --- |
| `GET /health/live` | 200 |
| `GET /health/ready` | 200, `Healthy` (repository, harness and coordination checks) |
| `POST /sessions` | 201 with an `id` |
| First prompt with `Idempotency-Key` | 202, `Location: /sessions/{id}/events` |
| Same key and same body | 202, byte-identical body, same `Location` |
| Same key, different body | 409 `/problems/idempotency-key-reused` |
| Run settles | status `Completed`; SSE delivers `prompt_accepted`, `text_delta`, `completed` |
| TimescaleDB rows | 3 events for the run in `ompos.session_events` |
| Hypertable | `ompos.session_events` is a hypertable with one dimension (`occurred_at`) |
| Lease after settle | `ompos:lease:run:{id}` absent |
| Held lease, new prompt | 409 `Prompt already in flight.` after 2.07 s; event count unchanged (no 202, no events) |
| Lease released, new prompt | 202 |
| Stale `Running` session with no lease | reaper settled it `Failed` within about 4 s; terminal event `Prompt execution failed. Detail: run owner lost` |
| Restart durability | session status and all 6 events still readable after restarting the API |
| Traces in GreptimeDB | `opentelemetry_traces` holds 8 spans after the fix |
| Metrics in GreptimeDB | ASP.NET Core metric tables present |
| Wrong Postgres password | startup fails with `28P01`; neither the wrong nor the real password appears in the output |

## Findings

1. **Pipeline header must be in the general OTLP header variable.** With the pipeline header only in
   `OTEL_EXPORTER_OTLP_TRACES_HEADERS`, GreptimeDB rejected every trace export with `400 Pipeline is required for this API.`
   The same request with the header succeeds (200), so the server is correct and this exporter did not apply the
   traces-specific variable. Moving `X-Greptime-Pipeline-Name=greptime_trace_v1` into `OTEL_EXPORTER_OTLP_HEADERS` fixed
   traces. Metrics accept the header too (200 with and without it). Railway must set the general variable only.
2. **Startup failure aborts the process.** A missing or wrong backing-store credential ends in an unhandled exception and
   SIGABRT (exit 134). Startup stays fail-fast, and the restart policy handles recovery.

## Not verified here

- Railway private networking over IPv6 (`*.railway.internal`), TLS on the TimescaleDB template, GreptimeDB Basic
  authentication, and the deployed image. These are verified only by the Railway deploy.
- The real omp harness and sandbox provider. The hosted runtime uses the InMemory harness and the fake sandbox (HOST-04 stays open).
