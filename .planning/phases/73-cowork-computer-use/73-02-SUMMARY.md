---
phase: 73-cowork-computer-use
plan: 73-02
subsystem: harness
tags: [computer-use, policy, grants, gate]
requires:
  - 73-01
provides:
  - ComputerActionGate
  - ComputerActionExecutor
  - GrantStore
  - AppCatalogue
  - ComputerUseComposition
affects: [73-03, 73-06]
tech-stack:
  added: []
  patterns:
    - One type per file (MA0048); extra files beyond the plan list exist only for that rule
    - ComputerUseComposition.CreateAsync is the composition root; no DI container
    - 73-03 appends Tools, Frames and Audit only through a separate ComputerUseToolsComposition file
key-files:
  created:
    - src/Autonogrammer.Harness.ComputerUse/Policy/ComputerActionGate.cs
    - src/Autonogrammer.Harness.ComputerUse/Executor/ComputerActionExecutor.cs
    - src/Autonogrammer.Harness.ComputerUse/computer-use-apps.json
    - tests/Autonogrammer.ArchitectureTests/ComputerUseBoundaryTests.cs
  modified: []
decisions:
  - Gate reads IComputerObservation so it is not a second IComputerUseBackend reference
  - Composition method is CreateAsync because a synchronous wait is banned
metrics:
  duration: null
  completed: null
---

# Phase 73 Plan 02: Gate, grants, catalogue, executor Summary

The Computer Use L2 project now has the choke point behind the 15 tools: grants and denylist, the D-06 gate, the only injector, and a composition root that does not construct an OS backend.

## What landed

- `GrantStore` implements `IComputerGrantRegistry`. Tiers are ViewOnly, ClickOnly, Full. Only `TryCreateFromUser` stores a grant. Dispatch grants expire at min(command end, 30 minutes), never carry `AutoApproveTyping`, and `RevokeDevice` withdraws them.
- `AppCatalogue` implements `IComputerDenylistStore`. Built-in rows cannot be removed. `list_windows` titles are projected only for granted applications; other windows are category counts. Windows and macOS identifier rows are `Unverified`.
- `ComputerActionGate` evaluates steps 1 to 8. The first failing step wins. Exceptions of `InvalidOperationException` and `ComputerBackendException` map to `InternalError`.
- `ComputerActionExecutor` is the only `InjectAsync` caller. It consumes the 2 second ticket, serialises actions across the approval prompt, re-resolves pointer targets, and pauses on takeover above `TakeoverPx` (default 24).
- `KillSwitchCoordinator` refuses to arm until the registrar confirms. A trip releases input, sets Revoked, cancels the turn and revocation tokens, and writes one audit record.
- `NullComputerUseBackend` reports `UnsupportedPlatform` / `computer.unavailable.platform` and throws `ComputerFailure.Unavailable` elsewhere.
- `ComputerUseBoundaryTests` flags a synthetic second `InjectAsync` caller and a synthetic Web-to-ComputerUse edge, and checks the live project graph.

## Gaps

- Gate exceptions other than `InvalidOperationException` and `ComputerBackendException` are not mapped to `InternalError` (CA1031 forbids a bare `catch (Exception)`).
- MA0048 required extra type files beyond the plan's `files_modified` list. No registry file, csproj, or lock was edited.
- `ComputerUseComposition.CreateAsync` takes a cancellation token. The plan named the method `Create`.
- The live desktop was not driven.
