# Dispatch pairing secret storage

Status: Partial; native pairing and platform keyrings are not verified.

Dispatch is not release-ready; the native pairing flow has not been verified end to end. The pairing component requires the concrete operating-system secret store for both the device private key and its credential. It rejects the owner-only file store, account-store selector wrappers, and other `ISecretStore` implementations rather than falling back to files.

On Linux the OS store uses Secret Service, on macOS Keychain, and on Windows DPAPI. Selecting the store does not establish that the keyring is available or unlocked. An unavailable, locked, or denied store refuses pairing; do not work around that failure by placing the device key or credential in the file-backed account store. No platform keyring or native pairing UI was exercised by the local constructor tests.

Dispatch stays off by default (`Dispatch:Enabled=false`). Outside Development, `Dispatch:Enabled=true` starts only when every requirement below is met. If any requirement is missing, the host refuses to start. Leave `Dispatch:Enabled` false unless all of them hold.

Outside Development these are required, and `DispatchOptionsValidator` checks each one:

+- `Dispatch:AllowProduction=true`, the explicit opt-in. Without it dispatch does not start outside Development.
+- `Dispatch:Store=Postgres`, with `Sessions:Store=Postgres` and `Sessions:Postgres:ConnectionString`. The in-memory store is Development only.
+- `Dispatch:ReplicaMode=Single`. Multi-replica mode is refused outside Development.
+- `Coordination:Store=Dragonfly` with `Coordination:Dragonfly:ConnectionString`. The in-memory coordination store is refused.
+- `DataPlane:TenantHashPepper`, a stable secret set before the first start.
+- `Dispatch:HubOrigin`, an absolute https origin with no path, set in configuration. It is never taken from a request header.
+- Distinct signing keys. `Dispatch:Credentials:SigningKeyPem` and `Dispatch:CommandSigning:KeyPem` must each be a PKCS#8 P-256 private key, and they must differ. Equal keys are refused in every environment while dispatch is enabled. Both are secrets: set them in the host, never commit them.

While dispatch is enabled in any environment, `Api:Auth:Mode` must be `Oidc`.

No production verification of this configuration has been performed, so enabling Dispatch outside Development remains unverified.

The Postgres store is selected by `Dispatch:Store`, and migration M0010 (`dispatch_device_state`) is registered in the migration runner. An unkeyed command currently stores its command ID as a synthetic idempotency key; a supplied key can collide with it. Resolve that data-contract issue before enabling the durable store; do not treat the local store tests as release approval.

See [ADR-0036](../decisions/ADR-0036-dispatch-relay.md) for the intended relay and pairing design and the [security review](../quality/v0.16.0-security-review.md) for current release blockers.
