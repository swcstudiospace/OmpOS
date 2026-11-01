# 76-03 Summary

Auto permission mode, deletion protection, MCP rule reading, and folder grants.

## What landed

- `CoworkPermissionMode.Auto` maps to `--approval-mode always-ask-cached` (D-12). Skip stays `always-ask`. Skip without FilesystemIsolated or a per-session acknowledgement is Manual and reports `SkipNeedsAcknowledgement`.
- `CoworkPermissionResolver` runs an allow rule or a short known-safe command inside trusted folders in Auto, refuses a deny rule, and asks otherwise. Provider and MCP calls follow Manual. A tainted Auto session asks. ACP `delete` and `ShellDeleteDetector` matches ask in every mode. MCP allow and deny rules are read through `PermissionsLoader`. Decisions go to `RunModeAuditLog` with `CoworkAutoRule`, `CoworkDeleteProtection`, `CoworkTaintDemotion`, `CoworkSkipGate`, and `CoworkFolderBoundary`.
- `KnownSafeShell` is the public facade over the internal known-safe list. `ShellDeleteDetector` recognises `rm`, `rmdir`, `unlink`, `shred`, `find -delete`, `git clean`, and wrapper prefixes. A deletion built in a script is the named gap `script-file-deletion`.
- `CoworkPathBoundary` refuses `/`, the home directory itself, system directories, and symlinks that resolve into them as `FolderRefused(protected)`. Writes are allowed only inside read-write grants and the scratch directory when grants are supplied.
- Session grants live on `CoworkSessionMetaStore`, project grants on `Sessions/CoworkProjectStore`, and global grants in owner-only atomic `CoworkFolderGrantStore`. Revocation reports `FolderChangeAppliesNextSession`.
- Permissions settings models and `PermissionsPageView` render Manual, Auto (rules), Skip, folder grants, and the fixed limit statements. Settings window wiring belongs to 76-06.

## Tests

Passed via `/tmp/ag-bin/ag-dotnet` Release class filters: `CoworkAutoModeTests` (3), `CoworkSkipAcknowledgementTests` (2), `CoworkResolverMcpRuleTests` (7), `CoworkPathBoundaryTests` (5), `CoworkFolderGrantStoreTests` (2), `ShellDeleteDetectorTests` (9), `KnownSafeShellTests` (2), `PermissionModesModelTests` (2). Existing `CoworkPermissionResolverTests` and `CoworkSessionProfileTests` were re-run after the Auto mapping change.

## Gap

`PermissionsPageViewTests` did not run. `apps/Autonogrammer.Native` does not compile (sibling errors in `CoworkSessionHost`, `CoworkEngineComposer`, and `AgentHead`), and `Autonogrammer.Native.Tests` references that project. `PermissionsPageView` itself builds in `Autonogrammer.Native.Views`.
