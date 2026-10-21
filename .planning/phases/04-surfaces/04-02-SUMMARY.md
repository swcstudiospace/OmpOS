---
requirements-completed: [WEB-01, WEB-02]
---

# Phase 4 · Plan 02 Summary — Blazor WebAssembly web head and Vercel static deploy (WEB-01, WEB-02)

**Completed:** 2026-10-21 · **Implementation:** gsd-executor `P4Web`; review fix `P4Fix` (incremental SSE, reconnect bound, origin validation, CSP); `P4Fix3` (web JSON decoding) · **Verified by:** orchestrator, in a real browser

## What landed

- `apps/OmpOS.Web`: a standalone Blazor WebAssembly app hosting `OmpOS.UI`. The API origin comes from `wwwroot/appsettings.json` (`Api:BaseUrl`, default `http://127.0.0.1:5187`) and is overridable by `?apiBaseUrl=`, with validation: absolute http or https only, and http only for loopback.
- `SseEventStream` yields each frame as it arrives (per-event `yield`), with bounded reconnect, `Last-Event-ID` resume and a high-water sequence mark.
- Contract types decode through `OmpOsJsonContext` (the Contracts context, identical wire options); `WebJsonContext` keeps only the web-only request DTOs.
- `vercel.json`: dotnet-install `installCommand`, `dotnet publish` build, `output/wwwroot`, SPA rewrite, and security headers. The CSP is `connect-src 'self' http://127.0.0.1:5187`, `img-src 'self' data:`, `script-src 'self' 'wasm-unsafe-eval'`, `object-src 'none'`, `frame-ancestors 'none'`.

## Browser verification (WEB-01)

The published Release head was run against the InMemory API in an owned headless Chrome with an empty profile. Full record: `docs/runbooks/local-dev.md`, section "Vertical slice"; screenshot: `docs/runbooks/evidence/web-vertical-slice-smoke.webp`.

Result after all fixes, across load, send and reload: no error bar, no console errors after load, bold markdown rendered, remote image rendered as a link with no `img` element, script tag inert, user turn shown as escaped text, stored history replayed identically.

## Defects the browser run found and this plan fixed

1. `JsonSerializerIsReflectionDisabled` crash on first render: the repo-wide reflection switch broke Blazor's `HeadOutlet` JS interop. The web head overrides the switch to true (Q-WEB-REFLECTION).
2. `/` rendered "nothing at this address": the trimmer removed `Pages/Index.razor`, because routes are discovered by reflection. `OmpOS.Web` roots its assembly with `TrimmerRootAssembly`. A stray leading space before `@page` was also removed.
3. Unstyled page: `_content/OmpOS.UI/styles/ompos.css` shipped but was never linked.
4. A 404 for a missing `OmpOS.Web.styles.css` link with no scoped CSS. The link is removed.
5. `NoMetadataForType` on reload once a session had streamed events: contract types were decoded with a context that lacked the payload types. Regression test `SessionListEventsTests` uses a fixture captured from the live API and fails on the unfixed code.

## Verification

- `OmpOS.Web.Tests`: **29 passed, 0 failed**. `dotnet build apps/OmpOS.Web -warnaserror` is clean.

## Deviations and deferrals

- Before a production deploy, the production API origin must be added to `connect-src`. Only the dev loopback origin is listed.
- The Vercel build output path `output/` is ignored by git (`/output/`).
