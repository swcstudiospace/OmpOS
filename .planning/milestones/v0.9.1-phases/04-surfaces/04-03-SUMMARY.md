---
requirements-completed: [SHELL-01]
---

# Phase 4 · Plan 03 Summary — Engine hosts and MAUI Blazor Hybrid head (DESK-01, MOB-01, SHELL-01)

**Completed:** 2026-10-21 · **Implementation:** gsd-executor `P4EngineShell`; review fixes `P4Fix`, `P4Approval`, `P4Fix2` · **Verified by:** orchestrator (EngineHost tests; inspection for the MAUI head)

## What landed

- `src/OmpOS.EngineHost` (layer L3, the composition root that binds the concrete adapters; see ADR-0002 amendment): `IEngineHost` with an in-process implementation for desktop and a remote implementation for mobile. The remote implementation reconnects the SSE stream with sequence resume.
- `apps/OmpOS.App`: .NET MAUI Blazor Hybrid head (`net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`) that shares `OmpOS.UI`. `SessionHost.razor` hosts the surface.
- Excluded from `OmpOS.Linux.slnf`. The full `OmpOS.slnx` restore needs the .NET MAUI workloads; on Linux it fails with NETSDK1147 for `maui-tizen`, which is documented in `README.md`.

## Verification

- `tests/OmpOS.EngineHost.Tests`: **10 passed, 0 failed**.
- `apps/OmpOS.App` **cannot be built on Linux**. Its evidence is inspection only: the head shares `OmpOS.UI`, and its approval wiring was removed with the interactive approval path.
- The macOS build job is **human-deferred**: no macOS runner is configured.

## Inferred changes (not built on Linux)

- `apps/OmpOS.App/wwwroot/index.html` links `_content/OmpOS.UI/styles/ompos.css`.
- `apps/OmpOS.App/OmpOS.App.csproj` sets `JsonSerializerIsReflectionEnabledByDefault=true` for the same HeadOutlet reason as the web head (Q-WEB-REFLECTION).

## Deviations and deferrals

- Mobile is remote-only by design, and window-state persistence, single-instance guard and crash-safe recovery are behind interfaces. Their concrete platform implementations are not built on Linux.
- Interactive approval is not available on any head (Q-HARN-PERMISSION).
