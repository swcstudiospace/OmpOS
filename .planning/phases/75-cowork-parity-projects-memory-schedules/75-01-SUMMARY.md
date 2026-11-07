---
phase: 75-cowork-parity-projects-memory-schedules
plan: 75-01
subsystem: cowork
tags: [cowork, projects, instructions, standing-context]
requires: []
provides:
  - D-03 project model, validator and local store with folder migration
  - StandingContextBuilder and InstructionLimits
  - HttpProjectApi and ProjectAccountSync with offline read-only fallback
affects: [75-04, 75-06]
tech-stack:
  added: []
  patterns:
    - Folders stay on the device; ProjectSyncRecord has no folders member
    - Standing context takes the memory brief as a string and does not reference Cowork.Memory
key-files:
  created:
    - src/Autonogrammer.Cowork/Projects/CoworkProjectStore.cs
    - src/Autonogrammer.Cowork/Instructions/StandingContextBuilder.cs
    - src/Autonogrammer.Contracts/Cowork/Projects/ProjectContracts.cs
  modified:
    - src/Autonogrammer.Cowork/Sessions/CoworkSessionMetaStore.cs
key-decisions:
  - "The extended store is Autonogrammer.Cowork.Projects.CoworkProjectStore on the same projects.json path. Phase 69 Sessions.CoworkProjectStore remains."
  - "A corrupt projects.json is moved aside and List returns empty."
requirements-completed: [AG-75-1, AG-75-3]
duration: 0
completed: 2026-10-30
---

# Phase 75 Plan 01: Projects and instructions Summary

Extended local project store with D-03 limits, lossless old-file mapping, standing-context builder, and account sync that never serialises folders.

## Accomplishments

- Project create, list, rename, archive and confirmed hard-delete. Folders are local. Missing folder is `FolderMissing`.
- Standing context is byte-stable: global, then project, then folder. Cuts announce `InstructionsTruncated` or `StandingContextOverBudget`. The brief is fenced as untrusted.
- `HttpProjectApi` takes a token supplier, refuses a body that names an account, and enforces the 8 KiB global-instruction cap before send. Offline, 401 and 429 leave the last copy read-only.

## Verification

- `ProjectValidatorTests`, `ProjectStoreMigrationTests`, `StandingContextBuilderTests` and `ProjectAccountSyncTests` passed via `/tmp/ag-bin/ag-dotnet test`.
- `ag-dotnet build src/Autonogrammer.Cowork/Autonogrammer.Cowork.csproj -c Release --no-restore -warnaserror` succeeded with 0 warnings.

## Deviations

- Phase 69 already owns `Sessions/CoworkProjectStore`. This plan's file list required `Projects/CoworkProjectStore`, so the extended store is a second type on the same path rather than an edit of the phase-69 file.

## Self-Check: PASSED
