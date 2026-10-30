---
phase: 66-rebrand-to-autonogrammer
plan: "04"
requirements: [AG-66-7]
status: complete-pending-native-confirmation
---

# 66-04 summary: user data directory resolver (AG-66-7)

One shared resolver (`UserDataDirectory`, Autonogrammer.Harness.Abstractions, L1) now serves all six data-directory sites. A legacy directory is moved once and atomically under an exclusive lock file, a marker file `migration.json` records it, and the new/legacy/unreadable/racer semantics of D-14 hold. Windows and macOS move paths are compile-checked only.

## What changed

- NEW `src/Autonogrammer.Harness.Abstractions/UserDataDirectory.cs` (`CurrentName`, `MarkerFileName`, `LockFileName`, `PathFor`, `LegacyPath`, `EnsureCurrent`), `UserDataMigration.cs` (result record `Path, Migrated, Warning`, own file because the repo rule is one type per file), `LegacyBrandNames.cs` (fragment-built legacy directory name, frozen by the glob).
- Four store `DefaultPath` bodies route through `UserDataDirectory.PathFor`: `FileMcpStateStore`, `EnvironmentProfileStore`, `FileSetupConsentStore`, `WorkspaceSetStore`.
- `AgentLoopEnvironment`: `ResolveStateRoot` uses `PathFor` over a shared `ResolveDataHome` (STATE_DIR and XDG_DATA_HOME branches unchanged); new `EnsureStateRootMigrated` (returns null and touches nothing when `AUTONOGRAMMER_STATE_DIR` is set); the parameterless `Resolve()` is the native startup call and runs it first. Migration applies to default roots only.
- `HeadServices`: `ResolveDefaultConfigDirectory(applicationData, stateRoot, loggerFactory)` replaces `DefaultConfigDirectory`; `CreateAsync` calls it through `ResolveConfigDirectory` (an explicit `ConfigDirectory` is used as given). Warnings go to `options.LoggerFactory` (LoggerMessage event 2), else Trace.
- Edges: Workspace -> Abstractions, Native -> Abstractions, plus a test-only edge in Workspace.Tests, Mcp.Tests, Remote.Tests, Native.Tests.
- Tests: `UserDataDirectoryTests` (Abstractions, 12 cases), `UserDataDirectoryMigrationTests` in Workspace.Tests (6), Mcp.Tests (3), Remote.Tests (3), Native.Tests (6), and one `LegacyBrandNames.cs` in each of the five test projects.
- Relocked (only via `restore --force-evaluate -p:RestoreLockedMode=false`): Workspace, Remote, Native.Views, Native, Api, and test locks Workspace.Tests, Remote.Tests, Native.Tests, ArchitectureTests, Api.IntegrationTests. Mcp.Tests lock needed no change.

## Deviations

1. [Rule 3 - blocked by a package rule] `EnsureCurrent` takes `Action<string>? warn` instead of `ILogger?`: Abstractions has no logging package and the plan forbids adding one. A caller with a logger passes a delegate (HeadServices does); with null the warning goes to `Trace.TraceWarning`, never the console. The result record always carries the warning.
2. [Rule 3] `EnsureCurrent` has a fourth optional parameter `enumerateEntries` (default `Directory.EnumerateFileSystemEntries`). The readability probe before the move must be testable here: the host runs as root, which ignores revoked permissions. The real chmod test exists and skips for a privileged process.
3. [Rule 2] A symbolic link inside a legacy directory refuses the copy fallback (cross-volume only); the legacy directory is then left alone with a warning.
4. [Rule 1, found by build] CA2000 on the lock acquisition (fixed with a `using var`, committed early at the orchestrator's request); MA0051 method length of `CreateAsync` (extracted `ResolveConfigDirectory`); CA1849/CA1416 in the new test file.
5. RED honesty: the first Abstractions run failed at the product build (the `UserDataMigration` file already referenced `UserDataDirectory.EnsureCurrent`, which did not exist), not at the test compile. For Mcp.Tests and Remote.Tests no RED is observable: those projects already reach Abstractions transitively and `DefaultPath` was already `Autonogrammer/<file>` before the change, so the shape tests are green before and after; the behaviour they pin is the resolver round trip. Workspace.Tests RED would be the missing edge (compile error).

## Evidence (exercised)

- Abstractions.Tests `UserDataDirectoryTests`: 12 total, 11 passed, 1 skipped (`ALegacyDirectoryWithRevokedAccess...`, host process is root). The unreadable case is covered by the injected-listing tests (UnauthorizedAccess and IO error) that passed. The racer test runs 25 rounds of two concurrent starters; exactly one reports Migrated each round.
- Workspace.Tests 6/6, Mcp.Tests 3/3, Remote.Tests 3/3 passed (`--filter-class *UserDataDirectoryMigrationTests*`).
- Abstractions build `-warnaserror` clean (0 warnings, 0 errors).
- `restore --locked-mode` clean for Workspace, Native, Mcp.Tests, Remote.Tests.
- Legacy-literal hygiene: boundary grep over every new or edited file returns 0.

## Not exercised / open

- Native.Tests migration suite, Native `-warnaserror` build and ArchitectureTests `ProjectGraphTests`: runs were blocked twice by other phases' in-progress files (Accounts.Desktop lock NU1004 from 67, Contracts MA0048 from 67 and 68), then by the dotnet queue. See the final status line in the hand-off message for the last result.
- Cross-volume copy-then-delete path, Windows and macOS: compile-checked only, not run.
- Lock churn left uncommitted because it belongs to other phases: `apps/Autonogrammer.Web`, `src/Autonogrammer.Accounts.Desktop`, `src/Autonogrammer.Dispatch.Desktop` (Logging.Abstractions edge from the Secrets change).
- The Native lock in the working tree changed after my commit (Accounts.Desktop edge, phase 67); that belongs to the 67 commit.

## Commits

- b464d509 resolver, record, legacy names
- 9e58924c six sites, edges, tests
- e193f170 relock of the dependents
- a follow-up commit holds the `HeadServices` method-length fix and this summary
