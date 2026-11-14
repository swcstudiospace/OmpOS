# 75-07 summary: account context from the production Native head

status: code complete; gates not run by the worker (orchestrator runs `ag-dotnet test` per the plan's verify commands)

## What the code does

- `AgentHead` no longer uses an always-signed-out gate. `AgentHead.CoworkGate(services)` returns `NativeCoworkAccountGate` over `services.AccountHost.Session`, and is used for the Cowork head and the Computer Use provider. A head built without services is signed out.
- `HeadServices` builds one `CoworkAccountContext` beside `ArtifactsComposition`, exposes it internally as `CoworkAccount`, and disposes it with the head. The API client now has `BaseAddress` set to the configured API origin; before this change it had none, so relative `/cowork/...` routes could not resolve.
- `CoworkAccountContext` keeps one account graph keyed by the account folder key: `CoworkProjectStore`, `ProjectAccountSync`, `CoworkSessionMetaStore` and `HttpMemoryStore`. Sign-out or an account switch retires the graph. Retiring cancels in-flight calls, and a late completion returns `CoworkAccountSnapshot.Unavailable` instead of another account's data. `IsCurrent(snapshot)` tells consumers whether a snapshot still belongs to the active account.
- `SnapshotAsync(sessionId, ct)` runs `SyncAsync`, reads the session record for the project and memory-off flag, resolves the project from the local store (archived or missing projects contribute no instructions), reads memory settings now, and recalls with `MemoryScope(accountKey, projectId, useAccountMemory, sessionId)`. The brief is built with `MemoryBriefBuilder`.
- `AccountProjectApi` is the `IProjectApi` the head uses. `HttpProjectApi` takes a synchronous token supplier, so the adapter reads `IAccountSession.GetAccessTokenAsync` per call and builds a transport call on that token. The token is never stored. Signed out, it sends nothing.
- `AccountBearer` returns the active account's token only. There is no fallback to the environment token, unlike `HeadServices.ResolveTokenAsync`.
- `ProjectAccountSync` gained an optional `GlobalInstructionsCache`, so an offline start serves the same account's last global instructions read-only (D-06, D-07). The cache file is `global-instructions.json` in the account folder. The two-argument constructor is unchanged.
- `CoworkHead.CreateAsync` and `CoworkSessionHostOptions` take the context as an optional `AccountContext`, for 75-08 to consume.

## Tests written (not run)

- `tests/Autonogrammer.Native.Tests/Cowork/CoworkAccountCompositionTests.cs`: the production `AgentHead` path with an injected account and scripted authenticated API (bearer and bodies checked); two-account switch; sign-out sends nothing; in-flight cancellation on switch; offline start from the same account's copy; 401 and 429 degraded states; token rotation; memory off, disabled or paused sends no recall; recall scope for project, account-memory flag and no-project sessions.
- `tests/Autonogrammer.Cowork.Tests/Projects/ProjectAccountSyncTests.cs`: `GlobalInstructionsAreKeptInTheAccountFolderForAnOfflineStart`.

## Not claimed

- Open sessions are not torn down on account switch by this plan. `CoworkSessionHost` is not in `files_modified`. 75-08 and 75-13 must check `IsCurrent` and stop or refuse sessions of a retired account.
- Memory failures from `HttpMemoryStore.GetSettingsAsync` read as disabled (it returns off on any failure). An offline start therefore reports `MemoryDisabled`, not `Offline`.
- `Autonogrammer.Native.csproj` gained a `ProjectReference` to `Autonogrammer.Cowork.Memory`. `packages.lock.json` has no project-reference entries, so no lock change is expected, but `RestoreLockedMode` is on and the orchestrator's restore should confirm it.
- The Computer Use access facts now read the real account gate for signed-in users. Previously they always saw signed out. This is intended by the plan but changes behaviour.

## Files

Created: `src/Autonogrammer.Cowork/Projects/GlobalInstructionsCache.cs`, `GlobalInstructionsDocument.cs`, `GlobalInstructionsJsonContext.cs`; `apps/Autonogrammer.Native/Cowork/AccountBearer.cs`, `AccountProjectApi.cs`, `CoworkAccountSnapshot.cs`, `CoworkAccountContext.cs`; `tests/Autonogrammer.Native.Tests/Cowork/CoworkAccountCompositionTests.cs`; this file.

Changed: `apps/Autonogrammer.Native/Autonogrammer.Native.csproj`, `Agents/AgentHead.cs`, `Composition/HeadServices.cs`, `Cowork/CoworkHead.cs`, `Cowork/CoworkSessionHostOptions.cs`; `src/Autonogrammer.Cowork/Projects/ProjectAccountSync.cs`; `tests/Autonogrammer.Cowork.Tests/Projects/ProjectAccountSyncTests.cs`.

Not changed (outside the list): `HttpProjectApi.cs`, `CoworkSessionHost.cs`, `packages.lock.json`.
