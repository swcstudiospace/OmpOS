---
phase: 74-dispatch-relay
plan: 74-04
subsystem: dispatch-desktop
status: partial
tags: [dispatch, desktop, verifier, dedupe, reconnect, policy, approval]

requires:
  - 74-01 DispatchCanonical, DispatchFrameGuard, frames, enums, vector file, IJitterSource
  - 74-03 device credential format (JWT, exp claim, audience autonogrammer-dispatch-device)
provides:
  - DispatchVerifier, DispatchKeyRing, DispatchDedupeStore (persisted high-water plus 1024 commandIds and nonces)
  - DispatchReconnectLoop, DispatchCloseStatus (close and pre-upgrade classification), DispatchConnector
  - DispatchPolicyStore, DispatchPolicyEvaluator, DispatchApprovalFlow, DispatchCommandRouter
  - DispatchPairing with DispatchKeyStoreUnavailableException
affects: [74-06, 74-09]

actuals:
  tokens: 0
  tasks: 3
  commits: []

key-files:
  created:
    - src/Autonogrammer.Dispatch.Desktop/*.cs (43 files, see the Desktop source glob; AssemblyMarker.cs pre-existed)
    - tests/Autonogrammer.Dispatch.Desktop.Tests/VerifierTests.cs
    - tests/Autonogrammer.Dispatch.Desktop.Tests/DedupeTests.cs
    - tests/Autonogrammer.Dispatch.Desktop.Tests/ReconnectTests.cs
    - tests/Autonogrammer.Dispatch.Desktop.Tests/PolicyTests.cs
    - tests/Autonogrammer.Dispatch.Desktop.Tests/PairingTests.cs
    - tests/Autonogrammer.Dispatch.Desktop.Tests/Support/*.cs (4 files)
  modified:
    - tests/Autonogrammer.Dispatch.Desktop.Tests/Autonogrammer.Dispatch.Desktop.Tests.csproj (one None link to the 74-01 vectors, two Compile links to the Harness.Acp.Tests WebSocket doubles)

requirements-completed: [AG-74-1, AG-74-6, AG-74-9, AG-74-10, AG-74-11]
requirements-open: [AG-74-18]

# Phase 74 plan 04 summary
---

## Verification status

Nothing was built or run in this lane (lane rule: the wave gate compiles and tests once after all lanes land). Every test below is written, not executed.

Tests written, expected to pass once the wave gate runs `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Dispatch.Desktop.Tests -c Release`:
- VerifierTests: all 74-01 vectors rebuild byte-identical; valid frame accepted; forged signature, tampered payload, expired (with skew and clock offset), wrong user, wrong device, unknown kid, out-of-charset commandId, non-command capability, unknown operation, invalid pairing key.
- DedupeTests: duplicate redelivery, restart replays nothing, sequence gap does not stall, nonce reuse refused, 1024-window with high-water still blocking, unreadable file is an error, failed write executes nothing.
- ReconnectTests: jitter sequence and 30 s cap, jitter below window, 60 s stable reset, close-code table, 4009 wait, 4020 advertised delay, upgrade-status table, 429 floor, 401 split on credential expiry; loopback runs for 4001 (revoke ends sessions), 4002, 4010, 4011, pre-upgrade 401/403/426, missing or unavailable device key (no connect), and a forged-then-valid command pair (handler runs exactly once).
- PolicyTests: defaults matrix, kind computer refused, code prompt never carries tool classes, computer_use triple gate, browser_use gate, pause, capability mismatch, 200-character approval text, approve-once 30 min grant, deny-and-pause, approval timeout, policy persistence.
- PairingTests: device key only in the secret store, unavailable store refuses pairing and writes no record, pairing reload, credential exp read.

## What is implemented

- Verifier refuses every bad frame before a handler can run. Canonical bytes come only from `DispatchCanonical`. Signature is raw P-1363 ES256 over those bytes. Expiry uses hub time (local clock plus the welcome offset) with the 30 s skew.
- Dedupe persists the high-water mark and the newest 1024 `(commandId, nonce)` pairs atomically, before the caller acknowledges or executes. A corrupt window is an error, never reset silently.
- Connector: credential read from the secret store on each connect; `AcpWebSocketTransport` reused unchanged; welcome sets the clock offset; a command is verified, admitted, acknowledged `received`, routed, and answered with a `res` frame. Revoke or close 4001 (or a 401 with an unexpired credential) withdraws grants and calls the revocation sink.
- Reconnect: full jitter in `[0, min(30 s, 2^attempt s))`, counter reset after 60 s up, no attempt limit.
- Close codes per D-31 and pre-upgrade statuses per D-54 as tabled in the plan.
- Policy: defaults off except session control; computer_use needs config keys, desktop opt-in and a local approval at the start of each task; approval.decide is refused for every kind in this slice.

## Gaps and blockers (owner: this lane unless noted)

1. **Native handlers not implemented** (`apps/Autonogrammer.Native/Dispatch/*`, `HeadServices` wiring, `DesktopDispatchTests`). Blocked on three unmapped seams: (a) a Code prompt entry point on `SessionEngineSet` / `SessionSlot` (only view-model and turn paths were read); (b) `CoworkSessionHost.SendAsync` throws for an unopened session and `OpenSessionAsync` needs an `AgentRow`, so cowork.task needs create-open-send wired together; (c) no durable OS-backed `ISecretStore` is composed into the Native head yet. Owner: lane 74-04 follow-up or 74-09.
2. **Approval dialog bridge** (`IDispatchApprovalPrompt` implementation over `IComputerApprovalPrompt`) not written. `IComputerApprovalPrompt` offers AllowOnce/Deny only, so "Deny and pause Dispatch" has no dialog path yet. Owner: 74-06 (dialog views).
3. **Hello proof not sent.** `DispatchHelloFrame` requires a device signature over "hello canonical bytes", which the 74-01 contract does not define. The connector never answers a challenge. Owner: contract owner (74-01/74-05). Until then the hub cannot admit this desktop.
4. **Key rotation ingestion absent.** `DispatchWelcomeFrame` carries kids but no signature, so the contract cannot prove a welcome is signed by a pinned key. The ring is built only from pairing keys. Owner: 74-05 / contract.
5. **403 PairedToAnotherAccount not distinguished.** Without the response body, every 403 maps to AccountUnavailable.
6. **Controller device name.** The cmd frame carries no controller name; the approval dialog receives `DispatchConnectorOptions.ControllerName` instead. Owner: 74-09 composition.
7. **Pairing HTTP call** (`POST /dispatch/devices`) is not made by the desktop library; `DispatchPairing` stores results once the caller has them.
8. **Unknown frame types** are ignored rather than answered with `err` and a 1008 close after three.
9. **No evt frames** (`approval_pending` and similar) are sent; only `ack` and `res`.
10. **Handlers run inline** on the receive loop, so a long handler delays revoke handling. Acceptable only while handlers return promptly.
11. **Windows and macOS keystores** are not exercised; all tests use the in-memory store and Linux paths.

## Handover

Items 1 and 2 are the next lane's entry point: implement handlers against the seams named above and add the dialog bridge, then bind the connector in `HeadServices.CreateAsync` (after 74-09's controller-side hunk if it lands first).
