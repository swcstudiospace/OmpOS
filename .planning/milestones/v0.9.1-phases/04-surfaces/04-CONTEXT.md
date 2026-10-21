# Phase 4: Surfaces - Context

**Gathered:** 2026-10-20
**Status:** Ready for planning
**Mode:** Drafted during Phase 2 execution (orchestrator; subagent infrastructure unavailable — recorded in milestone notes)

<domain>
## Phase Boundary

Shared Razor Class Library chat surface (`OmpOS.UI`) with Generative UI renderer; Blazor WASM web head on Vercel; MAUI Blazor Hybrid desktop/mobile head; vertical slice end-to-end against the in-memory adapter. Requirements UI-01..04, WEB-01..02, DESK-01, MOB-01, SHELL-01 (REQUIREMENTS.md:70-80).

Out of scope: real backend deployments (vertical slice runs against `InMemoryOmpAdapter` in-process), A2UI full spec (fixed component-catalog subset only), real omp prompt turns in tests.
</domain>

<decisions>
## Implementation Decisions

### D-06 Surface technology (ADR-0002) — LOCKED
- `OmpOS.UI` (L2, references Contracts + GenUi only) owns every component; heads are thin hosts.
- Web: Blazor WASM standalone (`apps/OmpOS.Web`, L4), static-publishable, API base URL from configuration.
- Desktop/mobile: one MAUI Blazor Hybrid head (`apps/OmpOS.App`), excluded from `OmpOS.Linux.slnf`; desktop binds an in-process engine host (local `omp acp` stdio or in-memory), mobile registers remote-only host + SSE resume on `Window.Resumed`.

### Phase-4-specific decisions
- **D-08 Renderer purity (UI-04)**: the renderer is a pure fold `UiTree Fold(UiTree, StreamEvent)` mirroring `HarnessEventProjection`'s shape; bUnit tests replay the same 23-event fixture used by `ProjectionReplayTests` and assert identical trees.
- **D-09 Unknown-block UX**: unknown `UiBlock`/`UnknownPayload` render as a collapsed disclosure with raw JSON, never a blank cell or exception (bUnit test with every unknown shape from Phase 1 converter tests).
- **D-10 Streaming states**: one `SessionViewState` enum (LoadingHistory, Empty, Streaming, AwaitingApproval, RecoverableError, FatalError, Reconnecting, Offline, Success) — single source of truth for aria-live announcements (throttled) and the stop/retry affordances.
- **D-11 Vercel target**: static `dotnet publish` output of the WASM head; `vercel.json` with installCommand (dotnet-install), SPA rewrite, CSP + security headers; no server functions. Brotli precompression accepted; WASM caching headers explicit.
- **D-12 Accessibility check tooling**: bUnit assertions for keyboard/focus/labels/aria + axe-core run where tooling exists (published web app, browser-based); MAUI a11y covered by the same bUnit surface because all controls live in the RCL.
</decisions>

<deferred>
## Deferred Items

- macOS MAUI build verification — human-deferred to CI (SHELL-01; see STATE Deferred Items).
- Real backend SSE against a deployed API — Phase 2 integration tests stand in; a deployed Vercel+Railway run is post-milestone.
</deferred>

<research_pointers>
## Verified external facts for 04-RESEARCH (web-verified 2026-10-20)

- bUnit **2.12.5** (2026-10-19) supports net10.0; already pinned in `Directory.Packages.props`. 2.x API names: `BunitContext` (not TestContext), `BunitNavigationManager`, `BunitJSRuntime`. Renders in-process (no real WASM runtime, mocked IJSRuntime); JS-dependent behavior needs E2E (browser smoke covers it).
- Vercel static Blazor WASM: `vercel.json` with `framework: null`, `installCommand` = dotnet-install.sh (`-JsonFile global.json` pins 10.0.402), `buildCommand` = `./.dotnet/dotnet publish -c Release -o output`, `outputDirectory: output/wwwroot`, SPA `rewrites` catch-all to `/index.html` (files-first, so `_framework/*` still serves). Never combine `routes` with `rewrites`/`headers`. Standalone WASM only — no hosted server component.
- RCL-first architecture is the documented MAUI Hybrid best practice (learn.microsoft.com hybrid class-libraries): components platform-agnostic in the RCL, platform services behind DI — matches D-06.
</research_pointers>
