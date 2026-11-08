---
phase: 74-dispatch-relay
plan: 74-05
subsystem: api
status: complete
completed: 2026-11-08
tags: [dispatch, websocket, postgres, presence, audit]
requires: [74-01, 74-02, 74-03, 74-07, 74-10]
provides:
  - Authenticated outbound device WebSockets with nonce-bound signed hello, heartbeats, delivery, acknowledgements and results
  - Durable Postgres dispatch-store selection, coarse source-prefix persistence, and overdue-command sweeping
  - Desktop pairing key binding and connector-to-hub state reporting
  - Atomic command-state feed events and closed-field rejection auditing
requirements-completed: [AG-74-4, AG-74-5, AG-74-7, AG-74-8, AG-74-10, AG-74-13, AG-74-14]
---

# Phase 74 plan 05 summary

The Dispatch relay connects paired desktops to the API over outbound WebSockets. The desktop signs the hub challenge with its enrolled device key, reports state transitions and heartbeats, and executes a verified command once. A real desktop connector, a loopback Kestrel API host and a session handler completed a command end to end in `DesktopHubIntegrationTests`.

## Implementation

- The hub registers the Postgres store when `Sessions:Store=Postgres`; in-memory storage is restricted to Development. Disabled Dispatch does not eagerly resolve a store. A production-shaped host startup test exercises the disabled case.
- An authenticated device's address is persisted only as `LastSourcePrefix`: IPv4 /24, IPv6 /48, including IPv4-mapped IPv6. No raw address or prefix enters the audit payload.
- The delivery loop transitions a queued row to delivered before sending, so an immediate device acknowledgement cannot race the transition. Delivered, unexpired rows are eligible for redelivery after reconnect. This deliberately differs from the plan's send-before-transition order; redelivery makes a failed send recoverable.
- The sweeper selects only overdue rows, pages them, and commits each applied transition with its feed event. Queued/delivered rows expire at their delivery deadline; approval waits use the approval timeout; received/accepted/running rows use the original receipt time and the configured session or task limit. `ApprovalDecide` and `CodePrompt` take the task limit, as do the other non-session operations.
- M0011 records `received_at` and adds partial sweep indexes. A row already in flight when M0011 runs is backfilled from its current state-entry time: its actual historical receipt time was not stored and cannot be reconstructed.
- The desktop reports `awaiting_approval` before the local prompt and `accepted`/`running` only for an operation with a handler. It emits heartbeats at the negotiated interval. The hub rejects malformed heartbeat status, replies to peer close, answers nonce-store timeouts with 1013, and leaves established sockets open when a presence refresh times out.
- The connector verifies, deduplicates and acknowledges command admission on its receive loop, then executes commands serially through a bounded queue while continuing to receive control frames. Revocation cancels the active handler; handlers must honour their cancellation token. Queue saturation ends the connection and uses the reconnect path. Handler exceptions produce a failed `execution_error` result without stopping later commands.
- Pairing completion transfers the enrolled secret from its temporary enrollment ID to the server-issued device ID before committing the pairing record. Command/result transitions use the closed transition table; invalid transitions leave the row unchanged. Rejected inputs with valid command identifiers are sent to the configured closed-field audit sink.

## Verification

- `./bootstrap.sh` with isolated `AUTONOGRAMMER_TEST_SERVICES_NAME=g7405`: **passed**, SDK 10.0.402, locked restore, Release warnings-as-errors build, **14,883 tests: 14,880 passed, 0 failed, 3 skipped**, format verification, secret scan, and API/demo/artifact browser smoke with axe. Smoke evidence: `/tmp/tmp.RHfCVFfRj1` (the gate ran against the worktree before the implementation commit; its output labelled the then-current HEAD `16696724`).
- `DesktopHubIntegrationTests.DesktopPairsConnectsAndCompletesACommand`: **passed** over an actual loopback WebSocket, covering pairing, challenge signature, state acknowledgements and completed row.
- Service-backed Dispatch integration filter: **111 passed**; Postgres dispatch-store filter: **19 passed**, including the receipt-deadline contract. Services were torn down after the runs.
- Desktop Dispatch suite: **71 passed**, including a manual-clock heartbeat test, signed challenge, prompt acknowledgement behind an executing command, execution-error isolation, and cancellation of an executing handler within two seconds after a revoke frame.
- Architecture suite: **83 passed**. Repo checks and SBOM check passed. The documentation check initially reported one GitBook-outside-root link; it was changed to a GitHub URL and the subsequent check reported **0 errors and 0 warnings**.
- The generated OpenAPI file matched the committed file after the build.

## Deviations and limits

- Existing hub files live directly under `services/Autonogrammer.Api/Dispatch/`, rather than the planned `Dispatch/Hub/` directory. The changes follow the existing layout.
- `Dispatch:Sweeper:Enabled` was added to allow deterministic test-host sweeps; it defaults to enabled when Dispatch itself is enabled.
- The real connector-to-hub test added a project reference from the API integration tests to `Autonogrammer.Dispatch.Desktop`. Its test lockfile and the generated SBOM were refreshed for that dependency graph; no NuGet package was introduced.
- OQ-500 is updated to record the durable and in-memory store-selection rules.
- The receipt-time backfill described above is an approximation for pre-existing in-flight rows. `Dispatch:RunLimits` is measured from `Received` for newly transitioned rows; approval waits have their separate approval timeout.
- The loopback integration uses test authentication and in-memory coordination; it does not establish live Supabase, Railway, or multi-replica availability. Railway long-WebSocket draining remains OQ-420. Native Windows, macOS, Android and physical-device packaging were not exercised here.
- Phase 74 remains open: plan 74-09 has no summary, and a plan summary is not phase-level verification.

## Commit

- `06787cc` — `feat(74-05): complete dispatch relay and device handshake`
- `f8bde92` — `fix(dispatch): process revocation during command execution`
- `5b63395` — `fix(dispatch): acknowledge queued commands and isolate handler failures`
