# Architecture Decision Records

Index of the decisions governing this milestone. The ADR files below are the
source of truth; this index only lists them with status and in-text amendments.

| ADR | Title | Status | Amendments / notes recorded in the ADR text |
| --- | ----- | ------ | -------------------------------------------- |
| [ADR-0001](ADR-0001-monorepo-layout.md) | Monorepo layout and dependency layers | accepted | Volume deviation: Temporal volume mounts at `/home/temporal` (image user uid 1000), not `/data/temporal.db`; also recorded in `docs/runbooks/sandbox.md` |
| [ADR-0002](ADR-0002-surface-technology.md) | Surface technology | accepted | Phase 4 amendment — EngineHost library (2026-10-20): `IEngineHost` seam lives in a dedicated L3 composition library (`src/OmpOS.EngineHost`), not in the MAUI head, so host selection and SSE resume stay unit-testable on Linux |
| [ADR-0003](ADR-0003-workflow-routing.md) | Workflow routing — direct runner vs Temporal | Accepted (2026-10-20) | Replay fixture grounded in a real server export (`tests/OmpOS.Workflows.Tests/TestData/histories/session-run.json`); Q-WF-HISTORY noted resolved in-text; “Open Questions: None outstanding for workflow routing” |

Conventions: new ADRs take the next number (`ADR-0004`, …); status changes are
recorded in the ADR’s own header, and this index is updated to match.
