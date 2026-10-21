# Phase 2 · Plan 06 Summary — API host, SSE resume, guards, ops (API-01..03, OPS-01..04)

**Completed:** 2026-10-20 · **Implementation:** gsd-executor `P2ApiOps` (parked mid-verification), completed by `P2WireParser` · **Verified by:** orchestrator (fresh test run, live smoke, code inspection)

## What landed

- `services/OmpOS.Api` is a `Microsoft.NET.Sdk.Web` minimal API (`Program.cs`, `Endpoints/`, `Sse/`, `Middleware/`, `Security/`, `Options/`, `Health/`, `Metrics/`, `Telemetry/`, `Harness/`, `OpenApi/`).
- Endpoints: `POST /sessions` (201), `GET /sessions`, `GET /sessions/{id}`, `POST /sessions/{id}/prompts` (202), `GET /sessions/{id}/events` (SSE), `GET /health/live`, `GET /health/ready`, `GET /openapi/v1.json`.
- Parser wired into the prompt path: `PromptParser` runs once per prompt; `RawText` is forwarded; Steer and FollowUp route through the exempt session methods when a session is running; `prompt_accepted` carries the intent kind and diagnostic codes; `ompos.parser.fallback.rate` increments on Fallback. (The first pass had not wired the parser at all; the verifier had missed this because it checked the library in isolation. Fixed in a later pass.)
- SSE: `id:` equals `StreamEvent.Seq`, `event:` equals the payload kind, `retry:` set. `Last-Event-ID` and `?since=` resume exactly once; the stream closes on a terminal event, including when resumed at or past the terminal Seq (review F-01).
- Guards: body limits enforced before buffering; 415 for a non-JSON content type; 413; 400 for an empty prompt; 409 for PromptInFlight; 429 with Retry-After on prompts and on session creation; CORS default-deny with `Api:Cors:Origins`.
- OPS: `HarnessOptions` requires `Harness:Adapter`, validated on start. A missing value fails startup with a message naming the key (verified live: "Harness:Adapter is required but was not provided. Set Harness:Adapter (supported value: InMemory)"). `.env.example` documents every variable.
- Correlation id: inbound value accepted only if it parses as a Guid and is at most 128 characters; otherwise minted (review F-05).
- Redaction: prompts log as redacted length, never the text.
- OpenAPI 3.1 document generated at build into `docs/api/openapi.json`; the served document is equal to the committed one (verified live, normalized JSON equality, openapi 3.1.1).
- OTel: `ompos.sessions.count`, `ompos.stream.latency`, `ompos.parser.fallback.rate` are recorded; `ompos.sandbox.provision.time` is registered and records nothing yet, because the API does not call a sandbox in this milestone.

## Verification (orchestrator, this session)

- `timeout 900 /root/.dotnet/dotnet test tests/OmpOS.Api.IntegrationTests` → **39/39 passed, 0 skipped**, 6.2 s.
- Live smoke against the built binary: `/health/live` 200, `/health/ready` 200, create 201, `text/plain` 415, empty prompt 400, prompt 202, SSE replay with `id: 1..3`, `event: prompt_accepted/text_delta/completed`, served OpenAPI equal to committed.
- Startup with the required key missing fails fast and names it (observed).

## Findings and their closure

- Phase 2 verifier G4 and G5 (API-01..03, OPS-01..04 missing): closed by this plan.
- Phase 2 final review F-01 to F-09: all fixed in code and covered by tests (see `02-REVIEW-FINAL.md`). Summary: F-01 SSE close at settled; F-02 global prompts bucket, no route-keyed partitions; F-03 signal eviction on terminal; F-04 creation limiter; F-05 correlation id validation; F-06 resume clamp and 400 on negatives; F-07 mapper degrades; F-08 exact-instance removal; F-09 OwnerId capped at 256.

## Deviations and deferrals (explicit)

- `ompos.sandbox.provision.time` is registered but not emitted: no sandbox call exists in the API path yet. Recorded in the handoff.
- A UI-driven interactive approval endpoint was not built: the harness has no pending-permission resolution path. Recorded as open question Q-HARN-PERMISSION (see Phase 4 summaries).
- The implementing agent's own final report was not delivered (parked, then completed by a follow-up), so this summary is written from the orchestrator's verification rather than from that agent's report.
