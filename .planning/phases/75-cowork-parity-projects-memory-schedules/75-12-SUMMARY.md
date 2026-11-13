# 75-12 summary: durable Cowork project, instruction and memory APIs

status: passed (success criteria verified on a run-owned disposable PostgreSQL)
verified_on: 2026-11-13
gate: Phase 74.1 verified first (commit 7c1c1a90), as the plan's cross-phase gate requires.

## What the code does

- `AddCoworkProjects` registers `PostgresProjectAccountStore` over the host `NpgsqlDataSource`. `AddCoworkMemory` registers `PostgresMemoryStore` over the same source. `CoworkApiExtensions.cs` contains no `InMemory` registration, so the enabled production path has no in-memory fallback.
- The project store is DML-only against M0009 tables, with the token subject in every query. Creation enforces the 200-project cap under an account advisory lock. Upsert uses `ON CONFLICT ... WHERE account_id`, so a foreign ID yields no row and maps to 404.
- `PostgresMemoryStore.CheckWriteScopeAsync` runs in the same transaction as the insert and takes `FOR KEY SHARE` on the owned project row, which serialises with project deletion. Foreign, missing and delete-won project IDs all return the same `MemoryNotFound` and write no entry. `MemoryEndpoints` maps that to one non-disclosing `project_not_found` 404 body.

## Verification (orchestrator-run, not subagent-run)

| Check | Result |
| --- | --- |
| `Autonogrammer.Linux.slnf -warnaserror` build | rc 0, 0 warnings, 0 errors |
| `PostgresMemoryStoreTests` (includes delete-vs-save races in both orderings, foreign/missing equality, cap race) | rc 0, 35 passed, 0 failed |
| `CoworkPersistenceTests` (restart with two accounts, cross-account 404, cap across two hosts, keyed save vs delete, lost acknowledgement, disabled module) | rc 0, 14 passed, 0 failed |
| `MemoryApiTests` | rc 0, 19 passed, 0 failed |
| Project tests (`*Project*`) | rc 0, 8 passed, 0 failed |

Disposable service: `scripts/test-services.sh` with `AUTONOGRAMMER_TEST_SERVICES_NAME=p7512`, run-owned, torn down after the run. No production database.

## Not claimed

- Replay of a project `PUT` `Idempotency-Key` is process-local (`CoworkIdempotencyCache`, a `ConcurrentDictionary`). Project data, instructions and memory are durable across restart; a replay key from before a restart is not. Persisting project replay needs an idempotency table, which M0009 does not have and this plan did not authorise.
- `CoworkApiExtensions.cs` keeps an `MA0048` suppression for four types. It predates this plan. Splitting the file would need new files outside this plan's list.
- The writer-versus-erasure race in 74.1 remains unresolved; 75-12 does not claim it closed.
- No production migration, deployment or commit to `main`.
