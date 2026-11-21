---
phase: 75-cowork-parity-projects-memory-schedules
plan: 75-07
subsystem: cowork
tags: [cowork, native, accounts, project-sync]
status: partial_not_shipped
---

# 75-07 Summary

Plan `75-07-PLAN.md` (depends on 75-12; AG-75-1/2/3/6) wires the Native head to the active account's
project/memory services. Re-verified against `origin/main` @ `c04095ba`. **Partially shipped: the Cowork
domain slice exists; the Native-head composition slice does not.**

## Present on the tree

- `src/Autonogrammer.Cowork/Projects/ProjectAccountSync.cs` — account-scoped project/instruction sync
  implementation is on `main`.
- `tests/Autonogrammer.Cowork.Tests/Projects/ProjectAccountSyncTests.cs`,
  `ProjectStoreMigrationTests.cs`, `ProjectValidatorTests.cs` — present.

## Missing (plan's own artifacts, absent on `main`)

- `apps/Autonogrammer.Native/Cowork/CoworkAccountContext.cs` — **absent**. The Native head therefore cannot
  be shown to use the active account and authenticated project/memory services instead of an
  always-signed-out gate; the plan's first truth is **not met** for the Native head.
- `tests/Autonogrammer.Native.Tests/Cowork/CoworkAccountCompositionTests.cs` — **absent**.
  `tests/Autonogrammer.Native.Tests/Cowork/` contains `CoworkHeadTests.cs`, `CoworkSignInButtonTests.cs`,
  `CoworkSignedOutTests.cs`, `CoworkAccountSwitchTests.cs`, `CoworkSessionHostTests.cs`, etc., but no
  account-composition suite, so the "startup sync retrieves global/project instructions and eligible
  scoped memory under current account settings" truth is **unevidenced for the Native head**.
- Account-change invalidation (`CoworkAccountSwitchTests.cs` exists) covers session/account switching at
  the Cowork level only; it does not substitute for the missing composition tests.

## Verified in this session (no product-code change)

- `dotnet restore Autonogrammer.Linux.slnf --locked-mode` — ok.
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` — 0 warnings, 0 errors.
- `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore` — clean.
- `dotnet test tests/Autonogrammer.Presentation.Tests` — 2646/2646 (non-live).

## Next step for the owning lane

Author `CoworkAccountContext` and the Native composition tests, or amend 75-07 to downgrade its truths to
the domain slice only. Route to A05/A06. This doc lane made no code change.
