# Dispatch pairing secret storage

Status: Partial; native pairing and platform keyrings are not verified.

Dispatch is not release-ready; the native pairing flow has not been verified end to end. The pairing component requires the concrete operating-system secret store for both the device private key and its credential. It rejects the owner-only file store, account-store selector wrappers, and other `ISecretStore` implementations rather than falling back to files.

On Linux the OS store uses Secret Service, on macOS Keychain, and on Windows DPAPI. Selecting the store does not establish that the keyring is available or unlocked. An unavailable, locked, or denied store refuses pairing; do not work around that failure by placing the device key or credential in the file-backed account store. No platform keyring or native pairing UI was exercised by the local constructor tests.

The PostgreSQL Dispatch store is not wired for production, and M0010 is not registered. Do not enable Dispatch outside Development. An unkeyed command currently stores its command ID as a synthetic idempotency key; a supplied key can collide with it. Resolve that data-contract and migration issue before enabling the durable store; do not treat the local store tests as release approval.

See [ADR-0036](../decisions/ADR-0036-dispatch-relay.md) for the intended relay and pairing design and the [security review](../quality/v0.16.0-security-review.md) for current release blockers.
