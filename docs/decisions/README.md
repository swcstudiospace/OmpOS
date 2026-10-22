# Architecture Decision Records

Index of the decisions governing this milestone. The ADR files below are the
source of truth; this index only lists them with status and in-text amendments.

| ADR | Title | Status | Amendments / notes recorded in the ADR text |
| --- | ----- | ------ | -------------------------------------------- |
| [ADR-0001](ADR-0001-monorepo-layout.md) | Monorepo layout and dependency layers | accepted | Volume deviation: Temporal volume mounts at `/home/temporal` (image user uid 1000), not `/data/temporal.db`; superseded 2026-10-21 with the Temporal removal (also recorded in `docs/runbooks/sandbox.md`) |
| [ADR-0002](ADR-0002-surface-technology.md) | Surface technology | Partially superseded by ADR-0004 | Phase 4 amendment — EngineHost library (2026-10-20): `IEngineHost` seam lives in a dedicated L3 composition library (`src/OmpOS.EngineHost`), not in the MAUI head, so host selection and SSE resume stay unit-testable on Linux. Amendment 2026-10-21: the Temporal part of the workflows bullet is superseded |
| [ADR-0003](ADR-0003-workflow-routing.md) | Workflow routing — direct runner vs Temporal | Superseded (2026-10-21) | Temporal removed; `DirectSessionRunner` in the API is the only run path. See [handoff decision](../handoff.md#temporal-removal-and-railway-topology-2026-10-21). The 2026-10-20 notes (replay fixture, Q-WF-HISTORY) are historical |
| [ADR-0004](ADR-0004-cross-platform-ui.md) | Cross-platform UI: Avalonia | Accepted 2026-10-21, amended (web stays Blazor WebAssembly) | Supersedes the MAUI and Blazor RCL UI stack decisions in ADR-0002. The web head stays Blazor WebAssembly after gate G-WEB failed on Avalonia 12.1.4. Other ADR-0002 decisions stand. |
| [ADR-0005](ADR-0005-workspace-conventions.md) | Workspace conventions: rules, skills, modes, search and setup file | Accepted 2026-10-22 | Layout under `.ompos/` (rules, skills, modes, setup.json). Setup commands run with user privileges and must go through the approval path before a real agent uses them (open risk, ADR text) |
| [ADR-0006](ADR-0006-terminal-control.md) | Terminal control and PTY (ADE-14, ADR R-1) | Proposed 2026-10-22 | Approval gate implemented; control and PTY choice open until an accessibility and ANSI spike passes (candidates and licences in ADR text) |

Conventions: new ADRs take the next number (`ADR-0004`, …); status changes are
recorded in the ADR’s own header, and this index is updated to match.
