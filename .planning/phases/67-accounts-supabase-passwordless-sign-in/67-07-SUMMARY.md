---
phase: 67-accounts-supabase-passwordless-sign-in
plan: "07"
status: complete
requirements: [AG-67-4, AG-67-23, AG-67-24]
---

# Plan 67-07 summary: web account UI, memory-only hand-off, session router, second CSP origin

## What changed

- Prerequisite gate: 67-01 and 67-05 summaries were present. 67-06 summary landed during this plan (`99803218`, `e01a2c03`). Account copy and `IAccountHost` were read, not edited.
- New account UI: `SignInPanel`, `AccountState`, `AccountMenu` (built and bUnit-tested, not placed; `TopBar` has no `HeaderEndSlot`).
- Classic boot script `autonogrammer-auth.js` scrubs the callback query before Blazor, posts over `BroadcastChannel` `autonogrammer-auth`, and waits 3 seconds. No storage API literals.
- `WebAuthSessionRouter` forwards to `OwnerTokenSession` until an OIDC config with a client block is accepted, then to `WebAccountSession` (memory-only store, 60 second 429 cooldown, no `Retry-After`). Demo still uses `NoApiTokenProvider`.
- A client block whose `projectUrl` origin differs from `Auth:Origin`, or a client block with no built origin, is refused before sign-in.
- `render-csp.sh` accepts optional `AUTONOGRAMMER_AUTH_ORIGIN` and fails closed when `AUTONOGRAMMER_SIGNIN=1` and the origin is missing. Demo still prints `connect-src 'self'`.
- CHANGELOG Unreleased phase 67 entry. Threat model records the no-storage rule for the auth origin.

## Verification

- `bash scripts/ci/check-changelog.sh`: valid.
- `render-csp.sh` with neither auth variable prints `connect-src 'self' <api>`; with a valid auth origin appends it; Demo ignores both.
- Targeted `ag-dotnet test` for Web and UI could not finish: `src/Autonogrammer.Contracts/Dispatch/*` (plan 74-01, not this plan) fails MA0048 and CA5394, so Contracts does not build. A peer lock-file change also required `--property:RestoreLockedMode=false`.

## Gaps

- UI and Web test projects were not green in this run because of the Dispatch contract analyzer errors above.
- `AccountMenu` is not in the top bar (phase 69).
- `DemoWorld.Account` carries a local CA1822 suppression so the web project builds; the method stays an instance method for `DemoApiHandler`.
- Extra type files `AuthCallbackStatus.cs`, `AuthCallbackResult.cs`, `AuthChannelEnvelope.cs` exist because MA0048 requires one type per file.
