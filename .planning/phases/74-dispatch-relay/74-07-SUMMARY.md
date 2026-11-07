---
phase: 74-dispatch-relay
plan: 74-07
subsystem: api
tags: [dispatch, routes, idempotency, rate-limit, sse, openapi]
requires:
  - 74-01 IDispatchStore, InMemoryDispatchStore and the Dispatch contracts
  - 74-03 DispatchModule, DispatchApiHost, pairing and the connect seam
  - 74-10 IDispatchPresence, IDispatchSignals (in-memory twins)
provides:
  - Device list, get, rename, revoke one and revoke all (owner-scoped; foreign or absent is the same 404)
  - Command enqueue with Idempotency-Key, closed capability and payload checks, queue cap, offline and presence outcomes; list, read and cancel
  - Per-caller command (30/min, burst 10) and read (120/min) limiter policies
  - GET /dispatch/events: account feed over SseStreamResult with resume, one resync and the live tail
  - IDispatchStore.ListCommandsAsync (both stores); DispatchDeviceDto.Presence
  - docs/api/openapi.json regenerated with every Dispatch route
affects: [74-05, 74-08]
actuals:
  tokens: not measured
  tasks: 2
  commits:
    - d965a7f6 feat(74-07) device and command routes (Task 1)
    - d06963cf feat(74-07) account feed over server-sent events (Task 2)
    - ad1d65d3 docs(74-07) open-question rows OQ-496 to OQ-502
    - e338c6f2 fix(74-07) bind dispatch seams as services in route handlers
tech-stack:
  added: []
  patterns:
    - Owner from IUserContext on every controller route; a foreign or absent device or command is the same 404
    - Dispatch store, presence and signal seams are [FromServices] in every route handler (they are registered in Development only)
    - Feed streams account rows only; wake on IDispatchSignals with a 200 ms re-read
key-files:
  created:
    - services/Autonogrammer.Api/Dispatch/Commands/CommandEndpoints.cs
    - services/Autonogrammer.Api/Dispatch/Events/DispatchEventEndpoints.cs
    - services/Autonogrammer.Api/Dispatch/Events/DispatchFeedStreamer.cs
    - tests/Autonogrammer.Api.IntegrationTests/Dispatch/DeviceRouteTests.cs
    - tests/Autonogrammer.Api.IntegrationTests/Dispatch/CommandRouteTests.cs
    - tests/Autonogrammer.Api.IntegrationTests/Dispatch/DispatchRateLimitTests.cs
    - tests/Autonogrammer.Api.IntegrationTests/Dispatch/DispatchFeedTests.cs
    - tests/Autonogrammer.Api.IntegrationTests/Dispatch/DispatchTestClient.cs, TestReply.cs, UnknownDispatchPresence.cs
  modified:
    - services/Autonogrammer.Api/Dispatch/Devices/DeviceEndpoints.cs (device routes; maps the command and feed routes)
    - services/Autonogrammer.Api/Dispatch/DispatchModule.cs (caller identity, Development-only presence and signals, policies, JSON chain)
    - services/Autonogrammer.Api/Dispatch/DispatchRateLimits.cs (command and read policies; four-argument bucket)
    - services/Autonogrammer.Api/Dispatch/Store/InMemoryDispatchStore.cs (ListCommandsAsync)
    - src/Autonogrammer.Sessions.Postgres/PostgresDispatchStore.cs (ListCommandsAsync)
    - src/Autonogrammer.Contracts/Dispatch/IDispatchStore.cs (ListCommandsAsync)
    - src/Autonogrammer.Contracts/Dispatch/DispatchDeviceDto.cs (Presence)
    - tests/Autonogrammer.Api.IntegrationTests/Dispatch/DispatchApiHost.cs (registerServices hook; production authorization result handler)
    - docs/api/openapi.json (regenerated)
    - docs/open-questions.md (OQ-496 to OQ-502)
key-decisions:
  - "IDispatchStore had no account-wide command list. ListCommandsAsync is added to the contract and to both stores (OQ-496)."
  - "Revoke cancels or fails unfinished commands through store calls after the revoke flag is set, not in one store transaction (OQ-497)."
  - "An idempotent replay during a presence outage answers 503, because the store has no idempotency lookup (OQ-498)."
  - "A resync skips the retained history and resumes live from the newest retained identifier (OQ-499)."
  - "Presence and signal seams are registered for Development only; production registration stays open with the 74-05 wiring (OQ-500)."
requirements-completed: [AG-74-3, AG-74-10, AG-74-12, AG-74-16]
---

# Phase 74 plan 07 summary

The signed-in controller surface: owner-scoped device and command routes with idempotent enqueue, the caps and per-caller limits, and the account feed over server-sent events. The HTTPS POST and SSE surface only; no browser WebSocket route exists.

## Gate
- `e338c6f2` (clean worktree, `bootstrap.sh`): `bootstrap: OK`; 14574 tests, 0 failed, 3 skipped; api, demo and artifact web smoke each exit 0.
- An earlier plan gate on `ad1d65d3` failed 10 production-shaped API tests (`Body was inferred but the method does not allow inferred body parameters`). Cause: outside Development the Dispatch seams are unregistered, so the minimal-API binder inferred them as request bodies. Fix: `[FromServices]` on the store, presence and signal parameters of every route handler (`e338c6f2`). The fixed head is the one gated above.

## Tests
- `tests/Autonogrammer.Api.IntegrationTests`, `--filter-class "*Dispatch*"`: 58 passed; 1 not run here (`DispatchDisabledTests` needs `AUTONOGRAMMER_TEST_POSTGRES_CONNECTION`, which the gate sets; it passes in the gate).
- New route tests: device routes 6, command routes 9, rate limits 3, feed 6.
- Format verified (`format --verify-no-changes`) on every changed C# file.

## Deviations
- Plan said the command and feed `Map` calls go in `DispatchModule`. They are in `DeviceEndpoints.MapDispatchEndpoints`, the single entry point that 74-03 established.
- Plan did not say which limiter covers device rename and revoke. They use the read limiter.
- The device list row carries `presence` (`online`, `offline`, `unknown`). `DispatchDeviceDto` gained that field (OQ-501).
- The API builds its `IDispatchStore`, `IDispatchPresence` and `IDispatchSignals` in Development only. The production wiring is open (OQ-500).
- The revoke cascade runs after the revoke flag is committed, not in one transaction (OQ-497).
- Replay during a presence outage is 503, not the original 202 (OQ-498).
- The pairing limiter is 5 per hour, so the 21st-pairing test seeds 20 devices through the store and pairs once through the route.
- The test host now registers the production authorization result handler, so an unlisted subject's 403 carries the `subject_not_allowed` problem as in production.

## Not verified here
- `PostgresDispatchStore.ListCommandsAsync` and the Postgres revoke path run only against a PostgreSQL service. They compile (format pass), and their behaviour is covered by the shared contract only once a service runs.
- Production presence and signal registration (OQ-500).
- The live hub that appends feed events for presence and command changes is 74-05; until it lands the feed carries only what the store is given (the tests append directly).

## Notes for 74-05
- The hub must call `IDispatchPresence.SetAsync` on connect and `RemoveAsync` on disconnect. Revoke already calls `RemoveAsync`.
- The hub's delivery loop should wake on `IDispatchSignals` (this plan publishes on enqueue, cancel and revoke).
- Feed events for presence and command changes are appended with `IDispatchStore.AppendFeedEventAsync`; the feed streamer already reads them.
