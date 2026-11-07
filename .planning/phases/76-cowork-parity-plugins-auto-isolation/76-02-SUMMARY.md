---
phase: 76-cowork-parity-plugins-auto-isolation
plan: 02
subsystem: mcp
tags: [mcp, connectors, permissions, avalonia]

requires:
  - phase: 66.1
    provides: MCP catalog, IMcpConnectionChecker, and McpClientConnector
provides:
  - Explicit McpConnectionChecker with tool lists and fixed failure text
  - Three-state connector matrix persisted as Mcp(server:tool) allow and deny tokens
  - Connectors cards, check source, and recording checker
affects: [76-03, 76-06]

actuals:
  tokens: 12000
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - "Connection checks run only from Check connection, through McpServerCatalog.CheckAsync"
    - "Tool policy is Mcp(server:tool) or Mcp(server:*) in permissions.allow and permissions.deny"

key-files:
  created:
    - src/Autonogrammer.Harness.Mcp/Catalog/McpConnectionChecker.cs
    - src/Autonogrammer.Presentation/Settings/Connectors/ConnectorMatrixModel.cs
    - apps/Autonogrammer.Native/Composition/Connectors/ConnectorPermissionStore.cs
  modified:
    - src/Autonogrammer.Harness.Mcp/Catalog/McpConnectionResult.cs
    - src/Autonogrammer.Harness.AgentLoop/Safety/PermissionsFileEditor.cs
    - apps/Autonogrammer.Native/Composition/HeadServices.cs

key-decisions:
  - "Checker failures are fixed strings ConnectorCheckFailed, ConnectorCheckTimedOut, and ConnectorSignInNeeded; logs name the exception type only"
  - "Plugin origins use BundleEnvironmentMode even when EnvironmentMode is Inherit"
  - "ConnectorsPageView is a UserControl, not wired into SettingsSection, which 76-06 owns"

patterns-established:
  - "RecordingConnectionChecker remembers the last tool list so the catalog gate stays the only check entry"

requirements-completed: [AG-76-5, AG-76-8]

coverage:
  - id: D1
    description: Explicit connection check returns the fixture tool list or a named failure, and does not run at catalog construction
    requirement: AG-76-5
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.Mcp.Tests/McpConnectionCheckerTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Three-state matrix round-trips Mcp tokens, including server-level Blocked and Always allow
    requirement: AG-76-8
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Presentation.Tests/Settings/ConnectorMatrixModelTests.cs
        status: pass
    human_judgment: false
  - id: D3
    description: SetPermissionTokens writes permissions.allow and permissions.deny and PermissionsLoader reads them back
    requirement: AG-76-8
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.AgentLoop.Tests/Safety/PermissionsFileEditorTokenTests.cs
        status: pass
    human_judgment: false
  - id: D4
    description: Connectors cards, secret canary, and catalog gate
    requirement: AG-76-5
    verification:
      - kind: automated_ui
        ref: tests/Autonogrammer.Native.Tests/Settings/ConnectorsPageViewTests.cs
        status: unknown
    human_judgment: true
    rationale: Native.Tests could not be built while sibling Cowork files in apps/Autonogrammer.Native failed to compile. The view, canary, and check-source tests are written and were not executed.

duration: 90min
completed: 2026-10-30
status: complete
---

# Phase 76 Plan 02: Connectors checker and permission matrix Summary

**Explicit MCP connection checks return tool names or fixed failure text, and the three-state matrix persists Mcp(server:tool) allow and deny rules.**

## Performance

- **Duration:** 90 min
- **Started:** 2026-10-30T04:00:00Z
- **Completed:** 2026-10-30T05:30:00Z
- **Tasks:** 3
- **Files modified:** 20

## Accomplishments

- McpConnectionChecker connects, lists tools, and disconnects only when asked. Timeout and cancellation are covered. Stdio allow-list, inherit, plugin bundle mode, and spec env are covered by the fixture canary tool.
- ConnectorMatrixModel maps Always allow, Needs approval, and Blocked to Mcp tokens. Server Blocked denies every tool; server Always allow yields to a per-tool deny. The page states that Auto does not trust readOnlyHint.
- ConnectorPermissionStore writes ~/.autonogrammer/permissions.json through PermissionsFileEditor.SetPermissionTokens. HeadServices passes a RecordingConnectionChecker into the catalog.

## Task Commits

1. **Task 1-3: checker, matrix, cards** - this commit (feat)

## Files Created/Modified

- `src/Autonogrammer.Harness.Mcp/Catalog/McpConnectionChecker.cs` - explicit check
- `src/Autonogrammer.Presentation/Settings/Connectors/` - matrix, cards, check source
- `apps/Autonogrammer.Native/Composition/Connectors/` - store, recorder, catalog adapter
- `src/Autonogrammer.Native.Views/Views/Settings/Connectors/` - cards view

## Decisions Made

- Failure text never includes the exception message, so headers, arguments, environment values, and URL paths stay out of the result and the log.
- ConnectorsPageView is not registered in the settings window. 76-06 owns that wiring.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Split extra types so MA0048 passes**
- **Found during:** Task 2
- **Issue:** The analyzer requires one type per file.
- **Fix:** ConnectorToolRow, ConnectorStateLabels, ConnectorServerListing, and ConnectorCheckSnapshot are their own files.
- **Files modified:** Presentation Settings/Connectors
- **Verification:** Presentation matrix tests passed
- **Committed in:** this commit

**Total deviations:** 1
**Impact on plan:** Required for the warning-as-error build. No behaviour change.

## Issues Encountered

Native.Tests (ConnectorsPageViewTests, ConnectorSecretCanaryTests, ConnectorCheckSourceTests) were not run. `apps/Autonogrammer.Native` did not compile because sibling Cowork files failed analysis and XML-doc checks. Checker, matrix, and permission-token tests passed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

76-03 can read the Mcp allow and deny tokens this plan writes. 76-06 can host ConnectorsPageView.

---
*Phase: 76-cowork-parity-plugins-auto-isolation*
*Completed: 2026-10-30*
