---
phase: 74-dispatch-relay
plan: 74-05
subsystem: api
status: partial
updated: 2026-11-08
tags: [dispatch, websocket, postgres, presence, audit]
requires: [74-01, 74-02, 74-03, 74-07, 74-10]
provides:
  - Authenticated outbound device WebSockets with nonce-bound signed hello, heartbeats, delivery, acknowledgements and results
  - Durable Postgres dispatch-store selection, coarse source-prefix persistence, and overdue-command sweeping
  - Desktop pairing key binding and connector-to-hub state reporting
  - Atomic command-state feed events and closed-field rejection auditing
requirements-completed: []
---

# Phase 74 plan 05 summary

The Dispatch relay connects paired desktops to the API over outbound WebSockets. The desktop signs the hub challenge with its enrolled device key, reports state transitions and heartbeats, and deduplicates commands before execution. A real desktop connector and loopback Kestrel hub completed running and queued commands after a forced reconnect.

**Status:** transient-disconnect queue loss is fixed and exercised. Plan 74-05 remains partial pending OQ-510, OQ-504, OQ-516 and OQ-517. The clean-commit gate below predates the review fixes in `12b1b60`; PR #9 remains draft.

## Implementation

- The hub registers the Postgres store when `Sessions:Store=Postgres`; in-memory storage is restricted to Development. Disabled Dispatch does not eagerly resolve a store. A production-shaped host startup test exercises the disabled case.
- An authenticated device's address is persisted only as `LastSourcePrefix`: IPv4 /24, IPv6 /48, including IPv4-mapped IPv6. No raw address or prefix enters the audit payload.
- The delivery loop transitions a queued row to delivered before sending, so an immediate device acknowledgement cannot race the transition. Delivered, unexpired rows are eligible for redelivery after reconnect. This deliberately differs from the plan's send-before-transition order; redelivery makes a failed send recoverable.
- The sweeper selects only overdue rows, pages them, and commits each applied transition with its feed event. Queued/delivered rows expire at their delivery deadline; approval waits use the approval timeout; received/accepted/running rows use the original receipt time and the configured session or task limit. `ApprovalDecide` and `CodePrompt` take the task limit, as do the other non-session operations.
- M0011 records `received_at` and adds partial sweep indexes. A row already in flight when M0011 runs is backfilled from its current state-entry time: its actual historical receipt time was not stored and cannot be reconstructed.
- The desktop reports `awaiting_approval` before the local prompt and `accepted`/`running` only for an operation with a handler. It emits heartbeats at the negotiated interval. The hub rejects malformed heartbeat status, replies to peer close, answers nonce-store timeouts with 1013, and leaves established sockets open when a presence refresh times out.
- The connector verifies, deduplicates and acknowledges command admission on its receive loop, then executes commands serially through a bounded queue while continuing to receive control frames. Up to 32 admitted commands and their in-memory state survive a transient link loss; their IDs are listed in the next hello and outbound state/results wait for a welcomed link. Revocation cancels the active handler, which must honour its cancellation token. Handler exceptions, including unrelated cancellations, produce `execution_error` without stopping later commands.
- Pairing completion transfers the enrolled secret from its temporary enrollment ID to the server-issued device ID before committing the pairing record. Command/result transitions use the closed transition table; invalid transitions leave the row unchanged. Rejected inputs with valid command identifiers are sent to the configured closed-field audit sink.
- Review fixes normalize the hub origin for comparison while signing the exact validated challenge, reject null device-frame fields safely, retain `approval_timeout`, and use the host's configured Postgres resilience pipeline.
- Progress events are capped at 50 per command in the store transaction. Terminal result text is redacted, capped, stored atomically with the result transition, exposed to the owner, and withheld after body retention.
- A full 32-command inbox keeps the link open for revocation and defers later commands without advancing the high-water mark. Link shutdown aborts a heartbeat write that does not finish after cancellation.

## Verification

- `./bootstrap.sh` with isolated `AUTONOGRAMMER_TEST_SERVICES_NAME=g7405`: **passed**, SDK 10.0.402, locked restore, Release warnings-as-errors build, **14,883 tests: 14,880 passed, 0 failed, 3 skipped**, format verification, secret scan, and API/demo/artifact browser smoke with axe. Smoke evidence: `/tmp/tmp.RHfCVFfRj1` (the gate ran against the worktree before the implementation commit; its output labelled the then-current HEAD `16696724`).
- Clean checkout of committed head `fbb8d93`, `./bootstrap.sh` with isolated `g7405-clean`: **passed**, with **14,891 tests: 14,888 passed, 0 failed, 3 skipped**, locked restore, warnings-as-errors build, format, secret scan, and API/demo/artifact browser smokes with axe. Smoke evidence: `/tmp/tmp.iFc3j5PnQn`. Build-time Development logs warned that process inspection was unrestricted and that the signup email lacked a code; those are runtime-configuration warnings, not compiler warnings.
- `DesktopHubIntegrationTests.DesktopPairsReconnectsAndCompletesRunningAndQueuedCommands`: **passed** over an actual loopback WebSocket, covering pairing, challenge signature, a forced disconnect, reconnect and completed rows for both commands.
- Earlier service-backed Dispatch integration filter: **111 passed**; Postgres dispatch-store filter: **19 passed**. After the reconnect and paged-settlement changes, focused tests passed against both in-memory and Postgres stores, including settlement past 100 rows.
- Desktop Dispatch suite: **72 passed**, including manual-clock heartbeat, signed challenge, immediate acknowledgement behind an executing command, offline result retention, handler fault isolation, and cancellation of an executing handler after a revoke frame.
- After clean gate `fbb8d93`, handler cancellation mapping was corrected in `a719127`. The focused test passed all three cases: an I/O failure, an unrelated handler cancellation, and actual revocation cancellation. The full clean gate above predates this correction.
- Review-fix verification: 35 desktop reconnect tests passed; the saturated-inbox test exercised revocation and verified the high-water mark stayed at 32. A real loopback socket test stalled a write and exercised the bounded heartbeat shutdown; two transport close/abort tests passed.
- Review-fix verification: the hub integration exercised the owner GET of a redacted, 4,096-character result; null fields, approval timeout and actual Kestrel origin handling passed. Shared in-memory and live Postgres tests passed for result persistence, retention and 64 concurrent progress writes capped at 50. A configured open Postgres circuit refused a Dispatch store read as `circuit_open`.
- Architecture suite: **83 passed**. Repo checks and SBOM check passed. The documentation check initially reported one GitBook-outside-root link; it was changed to a GitHub URL and the subsequent check reported **0 errors and 0 warnings**.
- The first clean-checkout bootstrap stopped at the history secret scan on the fixed RFC 6455 example WebSocket nonce in `06787cc`. It is not a credential. `4cd7404` replaces the current test value with a generated nonce and allowlists only that historical fingerprint. The history scan and clean bootstrap subsequently passed.
- The generated OpenAPI file matched the committed file after the build.

## Deviations and limits

- Existing hub files live directly under `services/Autonogrammer.Api/Dispatch/`, rather than the planned `Dispatch/Hub/` directory. The changes follow the existing layout.
- `Dispatch:Sweeper:Enabled` was added to allow deterministic test-host sweeps; it defaults to enabled when Dispatch itself is enabled.
- The real connector-to-hub test added a project reference from the API integration tests to `Autonogrammer.Dispatch.Desktop`. Its test lockfile and the generated SBOM were refreshed for that dependency graph; no NuGet package was introduced.
- OQ-500 is updated to record the durable and in-memory store-selection rules.
- OQ-515 is resolved by paged in-flight reconciliation. OQ-516 records the remaining crash-time durability and result-receipt gap: the inbox is in memory, and a successful socket write does not prove the hub committed the result.
- OQ-517 records the missing cancellation path: the sweeper fails an overdue command in the hub but does not yet stop the device task. No cancellation guarantee is claimed.
- The receipt-time backfill described above is an approximation for pre-existing in-flight rows. `Dispatch:RunLimits` is measured from `Received` for newly transitioned rows; approval waits have their separate approval timeout.
- The loopback integration uses test authentication and in-memory coordination; it does not establish live Supabase, Railway, or multi-replica availability. Railway long-WebSocket draining remains OQ-420. Native Windows, macOS, Android and physical-device packaging were not exercised here.
- Phase 74 remains open: plan 74-09 has no summary, and a plan summary is not phase-level verification.

## Commit

- `06787cc` — `feat(74-05): complete dispatch relay and device handshake`
- `f8bde92` — `fix(dispatch): process revocation during command execution`
- `5b63395` — `fix(dispatch): acknowledge queued commands and isolate handler failures`
- `9ccebf5` — `fix(dispatch): retain outstanding commands across reconnects`
- `4cd7404` — `test(dispatch): avoid static websocket nonce scan false positive`
- `a719127` — `fix(dispatch): isolate unrelated handler cancellations`
- `12b1b60` — `fix(dispatch): harden device protocol and bounded progress`
