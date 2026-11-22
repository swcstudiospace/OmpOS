---
phase: 75-cowork-parity-projects-memory-schedules
plan: 75-10
subsystem: cowork
tags: [cowork, memory, tool-host, gateway]
status: partial_not_shipped
---

# 75-10 Summary

Plan `75-10-PLAN.md` (memory tools operate with the calling session's account/project/mode/grants;
gateway lease coupled to session lifetime; saves never rewrite settings or reverse concurrent
pause/disable) is **partially shipped** on `origin/main` @ `c04095ba`.

## Present on the tree

- `src/Autonogrammer.Cowork.Memory/MemoryToolProvider.cs`, `InMemoryMemoryStore.cs` — memory tools.
- `src/Autonogrammer.Cowork.Memory.Postgres/PostgresMemoryStore.cs` — durable store.
- `tests/Autonogrammer.Cowork.Memory.Tests/MemoryToolProviderTests.cs` — present.
- `apps/Autonogrammer.Native/Cowork/CoworkToolServer.cs`, `CoworkHead.cs`, `CoworkSessionHost.cs`,
  `BrowserUseComposition.cs` — Native wiring surface.
- `src/Autonogrammer.Cowork/Tools/CoworkToolHost.cs` — tool host.

## Missing (plan's own artifacts, absent on `main`)

- `apps/Autonogrammer.Native/Cowork/MemoryProviderRegistration.cs` — **absent**: the Native registration
  step is not on `main`, so "gateway calls have an active CoworkToolHost lease" is unevidenced for the
  Native head.
- `tests/Autonogrammer.Native.Tests/Cowork/CoworkMemoryGatewayTests.cs` — **absent**: no gateway test
  demonstrates the lease/lifetime coupling or the "saving never rewrites account settings" invariant.

## Verified in this session (no product-code change)

- `dotnet restore Autonogrammer.Linux.slnf --locked-mode` — ok.
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` — 0 warnings, 0 errors.
- `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore` — clean.
- `dotnet test tests/Autonogrammer.Presentation.Tests` — 2646/2646 (non-live).

## Next step for the owning lane

Author `MemoryProviderRegistration` + `CoworkMemoryGatewayTests`, or downgrade the plan's truths.
Route to A05/A06. This doc lane made no code change.
