---
phase: 77-hardening-docs-and-integration
plan: "11"
subsystem: default-config, dispatch-pairing, accounts
tags: [default-config, pairing-store, accounts-enabled, security-gap, 77-11]
dependency_graph:
  requires: [77-03]
  provides: [dispatch-pairing-enforcement, accounts-enabled-default-off]
  affects: [77-10, security-release-gate]
tech_stack:
  added: []
  patterns: [reject-real-file-store-before-writes, default-false Enabled switch after auth, per-key validator isolation, honest open-High retention]
key_files:
  created:
    - tests/Autonogrammer.Api.IntegrationTests/DefaultConfigTests.cs
  modified:
    - src/Autonogrammer.Dispatch.Desktop/DispatchPairing.cs
    - services/Autonogrammer.Api/Accounts/AccountsOptions.cs
    - services/Autonogrammer.Api/Accounts/AccountsModule.cs
    - docs/quality/v0.16.0-security-review.md
decisions:
  - "SEC-77-01 (phase 72) and SEC-77-02 (75-12) stay open; an open High keeps the security release gate red (D-15/D-16) — this plan does not close them"
  - "DispatchPairing public ctor accepts only the sealed OsSecretStore; internal test-only ctor cannot be used by production callers (reflection-asserted)"
  - "Accounts:Enabled default false; operators of supported installs must set Accounts:Enabled=true before the changed default ships (explicit transition, never silent compat)"
metrics:
  tasks: 3
  status: gaps_found
---

# Phase 77 Plan 11: Default-Config Hardening Summary

**Status:** gaps_found — the real fixes shipped and their named regression tests pass, but SEC-77-01 and SEC-77-02 remain open by independent ownership, and platform keyrings/native pairing were never exercised. The security review stays red, correctly.

## Tasks Executed

**Task 1 (tracer, tdd):** `src/Autonogrammer.Dispatch.Desktop/DispatchPairing.cs`: public constructor coerces its `ISecretStore` argument to the concrete sealed `OsSecretStore` and rejects the file-backed `OwnerOnlyFileSecretStore`, `AccountSecretStoreSelector` wrappers and arbitrary implementations **before** `EnrollDeviceKeyAsync` / `CompleteAsync` write anything. Internal test-only ctor keeps deterministic doubles available; `Autonogrammer.Dispatch.Desktop.csproj` gained a direct ProjectReference to `Harness.Secrets.Os`. Tests: `tests/Autonogrammer.Dispatch.Desktop.Tests/PairingTests.cs` (real-file-store rejection + genuine OS-store usable path), `ReconnectTests.cs`.

**Task 2 (auto, tdd):** `services/Autonogrammer.Api/Accounts/AccountsOptions.cs` + `AccountsModule.cs` + `Program.cs`: default-false `Accounts:Enabled` switch enforced for the four mapped routes (`GET /account`, `/account/sessions`, `/account/sessions/{id}`, `/account/board`) after auth, returning the 404 disabled problem. `DefaultConfigTests.cs` replaces the old enabled-by-default assertion with four disabled cases + anonymous-production 401 check; `OmpApiClient.GetBoardAsync` route test in `OmpApiClientRouteTests.cs`.

**Task 3 (auto, tdd):** Validator isolation: valid two-key P-256 baseline starts; each unsafe variant (multi-replica in-memory coordination, missing credential key, missing command-signing key) fails for its own expected config key. Enabled-behavior fixtures (`AccountEnrollmentTests`, `BoardCutoverTests`, `CatalogEndpointTests`, `WebApp*Tests`, `AccountBoardSurfaceTests`, `RateLimitPartitionTests`) explicitly enable accounts.

## Verification Performed

- Shipped commits on origin/main: `34ade052` (test(77): 77-11 default-config tests, verified on run-owned Postgres), `374e2514` (feat(77): Dispatch in Production behind explicit opt-in, owner-approved)
- Files on HEAD: `DefaultConfigTests.cs` EXISTS, `PairingTests.cs` EXISTS (4.5K)
- Recorded earlier results (68 focused passes incl. `PairingTests` local, `ReconnectTests` 28/28, locked `Autonogrammer.Linux.slnf` restore) — retained from execution record; **not re-run in this batch** (run-owned PostgreSQL + full integration gate out of batch scope; see 77-VERIFICATION gaps)
- `docs/quality/v0.16.0-security-review.md` status line on HEAD reads **Open findings; not release-ready**

## Remaining Gaps

1. SEC-77-01 (High, phase 72: browser/tool policy) and SEC-77-02 (phase 75-12) stay open; the security audit fails while any High is open (by design).
2. Real OS keyring + native pairing UI never exercised (no live desktop run); cannot be claimed from ctor tests.
3. Operators must still set `Accounts:Enabled=true` for existing installs — transition is explicit, not automatic.

**Status:** gaps_found.
