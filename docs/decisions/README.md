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
| [ADR-0007](ADR-0007-editor-control.md) | Editor control: AvaloniaEdit (ADE-09) | Accepted 2026-10-22 | Licences and grammar sources recorded in the ADR; token colours and contrast on the Fluent surface not yet measured |
| [ADR-0008](ADR-0008-contract-versioning.md) | Contract versioning policy (R-11) | Accepted 2026-10-22 | Applies to presentation and harness plugin contracts |
| [ADR-0011](ADR-0011-logging-split.md) | Logging split by layer | Accepted 2026-10-26 | Services and harness log through ILogger; the native head uses Trace with a stderr listener set at startup; the web head uses ILogger. Residual: the desktop composition passes no logger, so service ILogger output is discarded there. ADR-0009 and ADR-0010 are not listed here; this index predates them. |

Conventions: new ADRs take the next number (`ADR-0004`, …); status changes are
recorded in the ADR’s own header, and this index is updated to match.
