---
phase: 75-cowork-parity-projects-memory-schedules
plan: 75-09
subsystem: cowork
tags: [cowork, acp, first-prompt, durability]
status: partial_not_shipped
---

# 75-09 Summary

Plan `75-09-PLAN.md` (first Cowork prompt contains one exact embedded resource before user text; restart
before send / acknowledged / unknown outcomes never silently lose or duplicate the snapshot) is
**partially shipped** on `origin/main` @ `c04095ba`.

## Present on the tree

- `src/Autonogrammer.Harness.Acp/AcpAgentSession.cs`, `AcpPromptContent.cs` — ACP prompt content path.
- `tests/Autonogrammer.Harness.Acp.Tests/AcpPromptContentTests.cs` — present.
- `tests/Autonogrammer.Native.Tests/Cowork/CoworkSessionHostTests.cs` (26.1 KB) — present and exercised
  in the Native test runs.
- `apps/Autonogrammer.Native/Cowork/CoworkSessionHost.cs` — present.

## Missing (plan's own artifacts, absent on `main`)

- `src/Autonogrammer.Cowork/Sessions/CoworkFirstPromptStore.cs` — **absent**
  (`src/Autonogrammer.Cowork/Sessions/` has no such file).
- `tests/Autonogrammer.Cowork.Tests/Sessions/CoworkFirstPromptStoreTests.cs` — **absent**.
- The durable "snapshot never silently lost or duplicated" truth therefore has **no implementation and no
  dedicated test on `main`**. The ACP content-ordering half is present; the durability half is not.

## Verified in this session (no product-code change)

- `dotnet restore Autonogrammer.Linux.slnf --locked-mode` — ok.
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` — 0 warnings, 0 errors.
- `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore` — clean.
- `dotnet test tests/Autonogrammer.Presentation.Tests` — 2646/2646 (non-live).

## Next step for the owning lane

Implement `CoworkFirstPromptStore` + tests, or downgrade 75-09's truths to ACP ordering only. Route to
A05. This doc lane made no code change.
