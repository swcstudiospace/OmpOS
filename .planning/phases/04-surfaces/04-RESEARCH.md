# Phase 4: Surfaces - Research

**Researched:** 2026-10-20 (web-verified facts folded into 04-CONTEXT.md research_pointers; this file is the durable record)
**Confidence:** HIGH for bUnit/Vercel/RCL facts `[CITED]`; a11y tooling `[CITED]`; MAUI specifics `[CITED: learn.microsoft.com hybrid class-libraries]` with macOS build human-deferred.

## Verified Facts

### VF-1 — bUnit 2.x (UI-01..04 tests) — `[CITED: nuget, bunit.dev, GitHub releases]`
- **2.12.5** (2026-10-19), net10.0 supported; **already pinned** in `Directory.Packages.props` (Phase 1). Companion `bunit.generators`/`bunit.web.query` at same version.
- 2.x renames: `TestContext` → `BunitContext`; `Fake*` → `Bunit*` (e.g. `BunitNavigationManager`, `BunitJSRuntime`). Use 2.x names.
- Renders in-process on regular .NET with mocked `IJSRuntime` — no real WASM runtime, no real browser. Component logic + markup assertions are bUnit territory; anything JS/CSS/layout-dependent belongs to the browser smoke (WEB-01).
- Works with xunit.v3 (this repo's runner). JS interop fixes landed by 2.7.2 — we are past it.

### VF-2 — Vercel static Blazor WASM (WEB-02) — `[CITED: vercel guides + dotnet docs]`
- `vercel.json` at repo root: `"framework": null`; `installCommand` installs SDK via `dot.net/v1/dotnet-install.sh` (use `-JsonFile global.json` to pin 10.0.402; install into `./.dotnet`); `buildCommand` calls `./.dotnet/dotnet publish -c Release -o output` **by full path** (PATH doesn't carry across commands); `outputDirectory: "output/wwwroot"`; SPA catch-all `rewrites` to `/index.html` (Vercel serves real files first, so `_framework/*`, `.wasm`/`.webcil`, assets still serve).
- Never mix legacy `routes` with `rewrites`/`headers` (Vercel rejects). Standalone WASM only — hosted/Blazor Web App templates cannot deploy statically.
- Set `DOTNET_CLI_TELEMETRY_OPTOUT=1`; if ICU missing on the build image, `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.
- CSP/security headers via `headers` array in the same `vercel.json` (Blazor needs `wasm-unsafe-eval` in script-src; connect-src covers the API origin).

### VF-3 — RCL-first Hybrid architecture (DESK-01, MOB-01) — `[CITED: learn.microsoft.com aspnet/core/blazor/hybrid/class-libraries-best-practices]`
- Keep all components in the platform-agnostic RCL (`OmpOS.UI`); platform services behind DI interfaces; the MAUI head is a thin BlazorWebHost. The documented template ("Blazor Hybrid + Web solution") adopts exactly this — our heads stay small.
- Desktop in-process engine host = an `IEngineHost` binding `InMemoryOmpAdapter` or a stdio `omp acp` ACP client (Phase 2 `OmpAcpClient`); mobile binds a remote `IEngineHost` (API base URL + SSE resume) and never spawns processes.
- bUnit tests cover the RCL once; no MAUI-specific UI tests exist or are needed.

### VF-4 — Accessibility baseline (UI-03) — `[CITED: WCAG 2.2 AA; axe-core docs]`
- bUnit-assertable: keyboard handlers (Enter/Shift+Enter/Esc), `aria-label`s on icon buttons, focus ring styles present, `aria-live` region present (throttling logic unit-tested), ≥44px targets via CSS tokens, `prefers-reduced-motion` media query in the stylesheet.
- Browser-run: `axe-core` (via Playwright inject or the axe CLI on the published site) run in the Phase 4 browser smoke where tooling exists; violations at `critical`/`serious` block, `moderate` recorded as open question if any.

## Validation Architecture

| Req | Verification |
|---|---|
| UI-01 | bUnit render tests per block variant + composer + conversation list |
| UI-02 | bUnit state-machine tests over `SessionViewState` + scroll behavior flags |
| UI-03 | bUnit a11y assertions + axe-core in browser smoke |
| UI-04 | bUnit replay: 23-event fixture → identical tree; unknown events never throw |
| WEB-01 | `dotnet publish` of Web head + browser smoke (vertical slice vs in-memory adapter) |
| WEB-02 | `vercel.json` schema-valid; publish output contains `index.html` + `_framework/`; headers present |
| DESK-01/MOB-01 | MAUI head compiles under a **Windows/macOS-only path** — on Linux, `dotnet build` of the MAUI project is NOT runnable; verification = project registered, excluded from slnf, RCL tests green, `IEngineHost` registrations unit-tested as plain classes |
| SHELL-01 | slnf exclusion asserted by a RepoHygiene test; README table updated |

## Assumptions Log
- `[INFERENCE]` Vercel build image details (Amazon Linux 2023 today) may drift — VERIFY-02 lists it as an open question; the config is declarative and re-runnable.
- axe-core browser run is best-effort in this environment (headless Chrome available); if the tooling fails, the bUnit a11y assertions are the recorded evidence and the gap is logged.
