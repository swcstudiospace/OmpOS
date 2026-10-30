---
phase: 75-cowork-parity-projects-memory-schedules
plan: 75-04
subsystem: api
tags: [cowork, projects, memory, rate-limit, disabled]
requires: [75-01, 75-02]
provides:
  - Per-account /cowork/projects and /cowork/instructions/global routes
  - Per-account /cowork/memory routes with store caps, pause, reset, delete and export
  - AddCoworkProjects, MapCoworkProjects, AddCoworkMemory, MapCoworkMemory
  - CoworkDisabledMiddleware 404 cowork_disabled
affects: [75-06]
tech-stack:
  added: []
  patterns:
    - Owner is always the token sub; a body that names an account is 400
    - Modules stay off until Cowork:Projects:Enabled or Cowork:Memory:Enabled
    - Program.cs and openapi.json are left for 75-06
key-files:
  created:
    - services/Autonogrammer.Api/Cowork/Projects/ProjectsEndpoints.cs
    - services/Autonogrammer.Api/Cowork/Memory/MemoryEndpoints.cs
    - services/Autonogrammer.Api/Cowork/CoworkDisabledMiddleware.cs
    - services/Autonogrammer.Api/Cowork/CoworkApiExtensions.cs
    - tests/Autonogrammer.Api.IntegrationTests/Cowork/ProjectsApiTests.cs
    - tests/Autonogrammer.Api.IntegrationTests/Cowork/MemoryApiTests.cs
    - tests/Autonogrammer.Api.IntegrationTests/Cowork/CoworkApiBoundaryTests.cs
  modified: []
key-decisions:
  - "In-memory project rows and IMemoryStore (InMemoryMemoryStore unless already registered) back the routes. 75-06 can replace the memory store before AddCoworkMemory."
  - "Recall uses IMemoryStore.RecallAsync, so the in-memory word search and the Postgres plainto_tsquery store stay the search implementation."
  - "Tests build a JWT host that calls the extensions. Program.cs is not edited."
requirements-completed: [AG-75-2]
duration: 0
completed: 2026-10-30
status: complete
coverage:
  - id: D1
    description: Per-account project and instruction routes refuse an owner in the body, isolate rows, and enforce desktop limits.
    requirement: AG-75-2
    verification:
      - kind: integration
        ref: tests/Autonogrammer.Api.IntegrationTests/Cowork/ProjectsApiTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Memory routes enforce caps, pause, reset hard delete, per-entry delete and JSON export with the same account boundary.
    requirement: AG-75-2
    verification:
      - kind: integration
        ref: tests/Autonogrammer.Api.IntegrationTests/Cowork/MemoryApiTests.cs
        status: pass
    human_judgment: false
  - id: D3
    description: Each module answers 404 cowork_disabled on its own, and rate-limited answers carry Retry-After.
    requirement: AG-75-2
    verification:
      - kind: integration
        ref: tests/Autonogrammer.Api.IntegrationTests/Cowork/CoworkApiBoundaryTests.cs
        status: pass
    human_judgment: false
---

# 75-04 Summary

Cowork project, global-instruction and memory routes are per token subject. A body that names an account or a folder is 400. Another account's row is 404. Creates honour Idempotency-Key by replaying the first result. Limits match the desktop: name 100, instructions 16 KiB, global 8 KiB, 20 https links, 200 projects, memory 1,000 entries, 1,000 characters, topic 120, query 200, 20 saves per session per hour.

Both modules default to disabled. `CoworkDisabledMiddleware` answers 404 `cowork_disabled` for that module only. Per-user token buckets reject with Retry-After. `AddCoworkProjects`, `MapCoworkProjects`, `AddCoworkMemory` and `MapCoworkMemory` are the only wiring. Program.cs and openapi.json are unchanged for 75-06.

## Tests

- `ProjectsApiTests`: 8 passed
- `MemoryApiTests`: 11 passed, including `ResetHardDelete_RemovesProjectMemory`
- `CoworkApiBoundaryTests`: 4 passed
- `ag-dotnet build services/Autonogrammer.Api/Autonogrammer.Api.csproj -c Release --no-restore -warnaserror -nodeReuse:false -m:1` succeeded

## Gaps

- Routes are not mapped until 75-06 calls the extensions and registers the middleware after authorization.
- The API memory store is in-memory unless a host registers `IMemoryStore` first. Postgres is not wired here.
- `*Cowork*` also matches older tests that need `AUTONOGRAMMER_TEST_POSTGRES_CONNECTION`. Those were not part of this plan's three classes.
