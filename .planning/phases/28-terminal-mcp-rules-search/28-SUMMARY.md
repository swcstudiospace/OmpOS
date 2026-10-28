# Phase 28: Terminal, MCP, Rules and Search, Summary

Position 2026-10-27: the audited position is in the Phase 28 Status line of `.planning/ROADMAP.md` and in `docs/quality/cursor-parity-audit-2026-10-26.md`. The text below was written earlier and is kept as history where it conflicts.

**Status: partially complete.** The harness logic for the approval gate, the MCP client, workspace rules, search and setup is done. The terminal control and PTY are not chosen, and no native view is built.

Mac channel: No. The terminal, MCP, search and setup tests run on the Linux gate.

## Success criteria (ROADMAP Phase 28)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Command runs only after approval through ADE-02; auto-run off by default | Partial | ADE-14 approval gate done: src/Autonogrammer.Harness.Terminal/TerminalGate.cs; tests/Autonogrammer.Harness.Terminal.Tests/TerminalGateTests.cs (23 tests). ADR-0006 Proposed. Terminal control and PTY not chosen (ADR R-1 spike open) |
| 2 | MCP client connects to stdio and HTTP servers; one registry; per-session enable | Partial | ADE-16 done: src/Autonogrammer.Harness.Mcp (ModelContextProtocol 2.3.0, revision 2025-11-25); tests/Autonogrammer.Harness.Mcp.Tests/McpToolRegistryTests.cs and McpHttpTests.cs (16 tests). HTTP hosting package not added |
| 3 | Rules, skills and modes load from project files and reload on edit | Partial | ADE-17 to ADE-19 done: src/Autonogrammer.Harness.Workspace (29 tests; WorkspaceCatalogTests.cs). Reload on edit: not confirmed |
| 4 | Text and regex workspace search returns file, line and match | Partial | src/Autonogrammer.Harness.Workspace/WorkspaceSearch.cs; tests/Autonogrammer.Harness.Workspace.Tests/WorkspaceSearchTests.cs. No native search view |
| 5 | Setup file runs before a run; a failed setup stops the run and shows its error | Partial | src/Autonogrammer.Harness.Workspace/SetupRunner.cs; tests/Autonogrammer.Harness.Workspace.Tests/SetupRunnerTests.cs. Setup commands must route through the approval path before real use (ADR-0005 risk 1) |

## Open items

- Route setup commands through the approval path before real use. Owner: engineering. Route: docs/decisions/ADR-0005-workspace-conventions.md (risk 1).
- Choose the terminal control and PTY after the ADR R-1 spike. Owner: owner decision. Route: docs/decisions/ADR-0006-terminal-control.md.
- Add the HTTP hosting package for the MCP server and build native views for terminal, MCP, rules and search. Owner: engineering. Route: ROADMAP Phase 28 criteria 2 and 4.

## Commits

- 9e5913d: ADE-04 to ADE-20 foundations on the gate-green tree (includes ADE-14, ADE-16 and ADE-17 to ADE-19)
