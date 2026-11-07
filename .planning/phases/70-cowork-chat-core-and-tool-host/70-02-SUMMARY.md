---
phase: 70-cowork-chat-core-and-tool-host
plan: 70-02
subsystem: infra
tags: [mcp, gateway, cowork, image]

requires:
  - phase: 70-01
    provides: ICoworkToolHost, CoworkToolResult, CoworkTextBlock, CoworkImageBlock
provides:
  - Gateway Start overload that serves Cowork tools beside registry grants
  - Result mapper from Cowork blocks to MCP text and image content, with an untrusted envelope
affects: [70-04, cowork-tool-server]

actuals:
  tokens: 9000
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - Registry grants resolve before the Cowork host; names containing __ never reach the host
    - Untrusted Cowork content is prefixed with McpGatewayCoworkResults.UntrustedEnvelopePrefix

key-files:
  created:
    - src/Autonogrammer.Harness.McpGateway/McpGatewayCoworkResults.cs
    - tests/Autonogrammer.Harness.McpGateway.Tests/CoworkGatewayTests.cs
    - tests/Autonogrammer.Harness.McpGateway.Tests/CoworkGatewayImageTests.cs
  modified:
    - src/Autonogrammer.Harness.McpGateway/McpGatewayServer.cs
    - src/Autonogrammer.Harness.McpGateway/McpGatewayDispatcher.cs

key-decisions:
  - "The two-argument Start delegates to the three-argument overload with a null host so existing callers stay on registry-only routing."
  - "A Cowork host fault uses the existing exception filter plus InvalidOperationException and InvalidDataException, mapped to fixed text. No general catch."

patterns-established:
  - "tools/list appends ListOfferedAsync names verbatim after registry Compose names."
  - "An ungranted Cowork name is refused after ListOfferedAsync and before InvokeAsync."

requirements-completed: [AG-70-8]

coverage:
  - id: D1
    description: Gateway lists registry names then Cowork names and routes calls with session isolation and __ exclusion
    requirement: AG-70-8
    verification:
      - kind: integration
        ref: tests/Autonogrammer.Harness.McpGateway.Tests/CoworkGatewayTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: A real ModelContextProtocol client receives image bytes, mime type, mixed blocks, and the untrusted envelope
    requirement: AG-70-8
    verification:
      - kind: integration
        ref: tests/Autonogrammer.Harness.McpGateway.Tests/CoworkGatewayImageTests.cs
        status: pass
    human_judgment: false

duration: 40min
completed: 2026-10-30
status: complete
---

# Phase 70 Plan 02 Summary

**The MCP gateway serves Cowork tools beside registry grants and delivers image bytes through a real MCP client.**

## Performance

- **Duration:** 40 min
- **Started:** 2026-10-30T00:00:00Z
- **Completed:** 2026-10-30T00:40:00Z
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments

- `McpGatewayServer.Start` keeps the two-argument entry and adds the Cowork host overload.
- `tools/list` and `tools/call` route registry grants first, then session-scoped Cowork tools, and never send a `__` name to the host.
- `McpGatewayCoworkResults` maps text and `ImageContentBlock.FromBytes`, prefixing untrusted results with a fixed envelope.

## Task Commits

1. **Task 1-3: routing tests, gateway routing, real-client image test** - one `feat(70-02)` commit

## Files Created/Modified

- `src/Autonogrammer.Harness.McpGateway/McpGatewayServer.cs` - three-argument Start
- `src/Autonogrammer.Harness.McpGateway/McpGatewayDispatcher.cs` - list and call routing
- `src/Autonogrammer.Harness.McpGateway/McpGatewayCoworkResults.cs` - result mapper
- `tests/Autonogrammer.Harness.McpGateway.Tests/CoworkGatewayTests.cs` - routing, isolation, access
- `tests/Autonogrammer.Harness.McpGateway.Tests/CoworkGatewayImageTests.cs` - real client image bytes

## Decisions Made

- Host faults stay on an extended exception filter with fixed text. No general catch, so no open-questions suppression row.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 70-04 can start one gateway per head with an empty registry and an `ICoworkToolHost`.

---
*Phase: 70-cowork-chat-core-and-tool-host*
*Completed: 2026-10-30*
