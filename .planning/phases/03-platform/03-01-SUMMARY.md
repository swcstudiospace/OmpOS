# Phase 3 · Plan 01 Summary — Sandbox abstraction, lifecycle, providers, vendor stubs (SBOX-01..05)

**Completed:** 2026-10-20 · **Implementation:** gsd-executor `P3Sandbox` (died mid-run), finished by `P3SandboxFinish` (hang fix), hardened by `P3Fix` (review criticals and warnings) · **Verified by:** orchestrator

## What landed

- `src/OmpOS.Sandbox.Abstractions`: `ISandboxProvider` (provision, attach, exec, stream logs, snapshot, teardown) with explicit `Timeout` and `IdempotencyKey` on every request; `SandboxStatus` covers Requested through Failed; `SandboxProviderOptions` carries the isolation defaults.
- `src/OmpOS.Sandbox`: `SandboxLifecycle` (TTL reaper, orphan sweep, jittered teardown retry with equal jitter, failed provision never leaves Running); `FakeSandboxProvider` (deterministic, failure injection); `LocalPodmanComposeProvider` (argv-only through `IProcessRunner`, ompos labels, compose file cleaned on failure paths); a production `ProcessRunner` with a bounded pipe drain and tree kill; typed stubs `OpenShellProvider` (prerequisite check: podman 5 required, reports `PrerequisiteMissing` on this host's 4.9.3; golden gateway TOML and policy YAML), `AgentOsProvider` (NotAvailable), `RailwaySwarmProvider` (NotAvailable, `Attestation.None`), `AttestedEnclaveProvider` (Unverified, dstack-shaped quote boundary).
- Isolation defaults enforced: host mounts and host secrets rejected with `PolicyDeniedException`; per-sandbox duplicate env targets rejected; default-deny network; landlock `hard_requirement` in the generated OpenShell policy.

## Verification

- `timeout 600 /root/.dotnet/dotnet test tests/OmpOS.Sandbox.Tests` → **63/63 passed, 0 skipped, no hang** (the real `podman-compose config` smoke runs against `deploy/compose.yaml`; it does not start containers).
- Phase 3 verifier (static, pre-fix): 20/20 checks; re-verification pending after the review fixes (see `03-VERIFICATION.md`).
- Phase 3 review criticals CR-01 to CR-04 each have a regression test: failed `up` deletes the compose file and attempts `down`; teardown checks the exit code and raises a typed failure so retry fires; the pipe drain is bounded even when a grandchild holds the pipe; the Temporal teardown guard records status instead of masking.

## Deviations and deferrals (explicit; recorded as open questions)

- The hang was a test that started real containers through `podman-compose up` with an unbounded pipe drain. Fixed: the smoke test now only validates the compose file.
- Compose file is retained on teardown failure so retries can re-run `down`; deleted on success.
- Q-SB-CREDENTIALS: `PerSandboxCredentials` means duplicate-target rejection only; credential issuance needs a secrets layer the milestone does not have.
- Q-SB-WORKER-COMPOSE: only `IProcessRunner` is DI-registered; real provider and activity composition into the worker lands later. Fake activities are opt-in behind `Workflows:EnableFakeActivities`.
- Q-SB-AGENTOS, Q-SB-RAILWAY, Q-SB-ENCLAVE: pre-existing owner-tagged questions for the vendor stubs.
