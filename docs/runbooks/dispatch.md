---
description: "Dispatch relay and pairing secret storage: what it does, how to enable it, options, verification, degraded states and owner actions."
---

# Dispatch

Status: Off by default and not release-ready. The relay, pairing and stores are built and unit/integration-tested offline; native pairing, platform keyrings and any production enablement are not verified. Do not enable outside Development (see "Why Dispatch stays off").

Last verified: 2026-11-24 (code and configuration read against this tree; no suite run for this page)

Applies to: [ADR-0036](../decisions/ADR-0036-dispatch-relay.md) (Proposed), v0.16.0 (phase 74)

## What it does

Dispatch is the account-bound relay in `Autonogrammer.Api`: a signed-in client (phone, web app or another desktop) sends commands to one of the account's registered desktops, and the desktop dials out to the hub to receive them (`DispatchConnector` in `Autonogrammer.Dispatch.Desktop`). Commands are signed frames (a command-signing key distinct from the device-credential key), queue for at most the configured `QueueTtl`, and actuating work needs approval at the desktop; a Computer Use command additionally needs the desktop's Computer Use opt-in and is always approved locally (see [computer use](computer-use.md)). Devices pair once and then authenticate with a device credential (P-256, issuer `autonogrammer-dispatch`, default 30-day lifetime). The schema is migration M0005 (`dispatch_commands`, `dispatch_events`, `dispatch_audit`), with device state in M0010 and sweep indexes in M0011.

Related runbooks: [Cowork](cowork.md), [Accounts](accounts.md), [data plane](data-plane.md), [v0.16.0 owner actions](v0.16.0-owner-actions.md). Release blockers are tracked in the [security review](../quality/v0.16.0-security-review.md).

## Pairing secret storage

The pairing component (`DispatchPairing`) requires the concrete operating-system secret store (`OsSecretStore`) for both the device's P-256 private key and its credential, under the secret names `autonogrammer.dispatch.device-key.<device-id>` and `autonogrammer.dispatch.device-credential.<device-id>`. Any other `ISecretStore` implementation — the owner-only file store, account-store selector wrappers, test fakes outside the test constructor — is rejected with `DispatchKeyStoreUnavailableException` rather than falling back to files. The non-secret pairing record (device, account and hub keys) is a separate state file.

On Linux the OS store uses Secret Service through libsecret, on macOS Keychain Services, and on Windows DPAPI. Selecting the store does not establish that the keyring is available or unlocked. An unavailable, locked, or denied store refuses pairing; do not work around that failure by placing the device key or credential in the file-backed account store. No platform keyring or native pairing UI was exercised by the local constructor tests.

## How to enable

`Dispatch:Enabled` defaults to `false` (`services/Autonogrammer.Api/appsettings.json`); while off, the dispatch routes do not answer and the account menu does not list the feature. Read "Why Dispatch stays off" before changing it anywhere.

Development only:

1. Use OIDC sign-in: `Dispatch:Enabled=true` is refused at startup unless `Api:Auth:Mode=Oidc` (validator: "Dispatch:Enabled requires Api:Auth:Mode=Oidc.").
2. Provide both P-256 PKCS#8 private keys as host secrets, never committed: `Dispatch__Credentials__SigningKeyPem` and `Dispatch__CommandSigning__KeyPem` (see `.env.example`). Startup validation names the key, never the value, when one is missing or not a P-256 private key.
3. Outside Development the validator additionally requires `Sessions:Store=Postgres`, a coordination store other than InMemory, and `Dispatch:HubOrigin` as an absolute https origin with no path (it is never taken from a request header). `Dispatch:ReplicaMode=Multi` requires `Coordination:Store=Dragonfly` with its connection string and the Postgres session store.
4. The store is selected on first use: `PostgresDispatchStore` when `Sessions:Store=Postgres`, otherwise the in-memory store in Development only; outside Development without Postgres, resolving the store fails.
5. Pair each desktop while its sign-in is fresh: pairing rejects a full sign-in older than `Dispatch:Pairing:MaxSignInAge` (default 10 minutes).

## Why Dispatch stays off

The durable store's idempotency projection is not resolved (phase 74 gap 74-11): `PostgresDispatchStore.InsertCommandAsync` stores `IdempotencyKey ?? CommandId` in the `idempotency_key` column, which is unique per (account, device) since M0005. An unkeyed command therefore occupies a synthetic key that a later supplied key can collide with, conflating keyed and unkeyed identity; the policy for any legacy rows written that way is an undecided owner action. Resolve that data contract before enabling the durable store anywhere that matters; do not treat the local store tests as release approval.

## Options

Configuration section `Dispatch` (environment form uses `__`, for example `Dispatch__Enabled`). Defaults below are from `DispatchOptions` and `appsettings.json`; where the startup validator caps a value, the cap is noted.

| Key | Default | Meaning |
| --- | --- | --- |
| `Dispatch:Enabled` | `false` | Whether dispatch routes answer. Requires `Api:Auth:Mode=Oidc`. |
| `Dispatch:Pairing:MaxSignInAge` | `00:10:00` | Newest acceptable full sign-in age at pairing. |
| `Dispatch:Credentials:Issuer` | `autonogrammer-dispatch` | The `iss` claim of device credentials. |
| `Dispatch:Credentials:Lifetime` | 30 days | Device credential lifetime (validator cap 90 days). |
| `Dispatch:Credentials:SigningKeyPem` | empty | Secret: P-256 PKCS#8 PEM that signs device credentials. Required when enabled; set as `Dispatch__Credentials__SigningKeyPem` in the host, never commit it. |
| `Dispatch:CommandSigning:KeyPem` | empty | Secret: P-256 PKCS#8 PEM that signs command frames. Distinct from the credential key. Required when enabled; set as `Dispatch__CommandSigning__KeyPem` in the host, never commit it. |
| `Dispatch:MaxPayloadBytes` | `65536` | Largest command payload in bytes (validator cap 262144). |
| `Dispatch:QueueTtl` | `00:05:00` | How long a queued command stays deliverable (validator cap 5 minutes). |
| `Dispatch:ClockSkew` | `00:00:30` | Clock skew applied to credential lifetime (validator cap 2 minutes). |
| `Dispatch:Limits:MaxQueuedPerDevice` | `100` | Queued commands per device. |
| `Dispatch:Limits:DevicesPerAccount` | `20` | Registered devices per account. |
| `Dispatch:Limits:FeedRetention` | 30 days | Retention of the user feed (`dispatch_events`). |
| `Dispatch:Limits:BodyRetention` | 7 days | Retention of a terminal command's body before it is nulled. |
| `Dispatch:RunLimits:Task` | `04:00:00` | Run limit of a dispatched task. |
| `Dispatch:RunLimits:Session` | `00:01:00` | Run limit of session and cancel operations. |
| `Dispatch:CommandApprovalTimeout` | `00:05:00` | How long a local approval may wait. |
| `Dispatch:Sweeper:Enabled` | `true` | Whether the background sweeper expires queued/delivered commands. Tests turn it off and sweep directly. |
| `Dispatch:ReplicaMode` | `Single` | `Multi` requires Dragonfly coordination and the Postgres store. |
| `Dispatch:HubOrigin` | empty | Hub origin returned at pairing; empty uses the request host. Required, absolute and https, outside Development. |

Test-only: `AUTONOGRAMMER_LIVE_KEYRING=1` opts the live Secret Service round-trip test in (`Category=Live`). Never set it in production.

## How to verify

Offline (no credential, no live device):

```sh
dotnet test --project tests/Autonogrammer.Dispatch.Desktop.Tests -c Release -- --filter-not-trait "Category=Live"
dotnet test --project tests/Autonogrammer.Dispatch.Client.Tests -c Release -- --filter-not-trait "Category=Live"
dotnet test --project tests/Autonogrammer.Harness.Secrets.Os.Tests -c Release -- --filter-not-trait "Category=Live"
```

The API-side dispatch suites run as part of `Autonogrammer.Api.IntegrationTests` and the database-backed store suites need the throwaway services (see [local development](local-dev.md)). The live keyring round-trip needs a desktop session with a running Secret Service daemon and `AUTONOGRAMMER_LIVE_KEYRING=1`; it has no recorded pass (see "What cannot be verified").

## Degraded states

| State | Behaviour | Operator action |
| --- | --- | --- |
| `Dispatch:Enabled=false` (default) | Routes do not answer; feature not listed | None; this is the supported state |
| Invalid `Dispatch` configuration | Startup fails naming the key (never a secret value) | Fix the named key; see "How to enable" steps 1-3 |
| Keyring unavailable, locked or denied | Pairing refused (`DispatchKeyStoreUnavailableException`) | Unlock the OS keyring; never fall back to a file store |
| Sign-in older than `Pairing:MaxSignInAge` | Pairing refused | Sign in again and pair within the window |
| Device credential expired (default 30 days) | Device authentication fails | Re-pair or rotate the credential from the desktop |
| Device offline past `QueueTtl` | Command expires; the sweeper moves it to a terminal state | Re-send once the desktop is connected |
| Approval window unavailable or timed out | Command refused and audited; never auto-approved | Approve at the desktop within `CommandApprovalTimeout` |

## Owner actions

See [v0.16.0 owner actions](v0.16.0-owner-actions.md). Dispatch-specific: decide the 74-11 idempotency projection and the legacy `dispatch_commands` row policy (keep fail-closed, or confirm with a redacted production count that none exist) before any production enablement; accept ADR-0036 (currently Proposed); run the native pairing flow and the Windows/macOS credential stores on real machines. This runbook performs none of them.

## What cannot be verified

- Native pairing end to end and any platform keyring (Linux Secret Service, macOS Keychain, Windows DPAPI) at runtime; the constructor tests prove only that non-OS stores are rejected.
- A real phone/Android controller against the relay (phase 74 human item).
- Production behaviour of the durable Postgres store: Dispatch has never been enabled outside Development, and the 74-11 idempotency projection above is unresolved.
