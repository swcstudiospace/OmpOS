---
phase: 75-cowork-parity-projects-memory-schedules
plan: 75-12
subsystem: cowork
tags: [cowork, api, postgres, persistence]
status: shipped
---

# 75-12 Summary

Plan `75-12-PLAN.md` (enabled production project/instruction/memory APIs use the application-role
PostgreSQL data source, not process-local dictionaries; survive host restart; isolated by token subject;
project-scoped saves validate project ownership) is **shipped** on `origin/main` @ `c04095ba`.

## Present on the tree

- `services/Autonogrammer.Api/Cowork/CoworkApiExtensions.cs`,
  `services/Autonogrammer.Api/Cowork/Projects/ProjectsEndpoints.cs`,
  `services/Autonogrammer.Api/Cowork/Projects/PostgresProjectAccountStore.cs` — Postgres-backed
  project/instruction store behind the API.
- `services/Autonogrammer.Api/Cowork/Memory/MemoryEndpoints.cs` —
  memory endpoints.
- `src/Autonogrammer.Cowork.Memory.Postgres/PostgresMemoryStore.cs` —
  durable memory store.
- `tests/Autonogrammer.Api.IntegrationTests/Cowork/CoworkPersistenceTests.cs` — present.
- `tests/Autonogrammer.Cowork.Memory.Postgres.Tests/PostgresMemoryStoreTests.cs` — present
  (DB-backed; requires throwaway services to execute).

## Caveat

The DB-backed suites were not executed by this doc lane (`scripts/test-services.sh` was not started;
starting run-owned services and executing the DB gate belongs to the verification lane — see
`75-VERIFICATION.md`). The store isolation by token subject and the deletion-race ownership guard are
present in `PostgresProjectAccountStore.cs` / `PostgresMemoryStore.cs` as shipped.

## Verified in this session (no product-code change)

- `dotnet restore Autonogrammer.Linux.slnf --locked-mode` — ok.
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` — 0 warnings, 0 errors.
- `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore` — clean.
- `dotnet test tests/Autonogrammer.Presentation.Tests` — 2646/2646 (non-live).

This doc lane made no code change.
