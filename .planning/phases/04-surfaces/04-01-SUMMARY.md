# Phase 4 · Plan 01 Summary — Shared chat surface RCL and Generative UI renderer (UI-01..UI-04)

**Completed:** 2026-10-21 · **Implementation:** gsd-executor `P4UiRcl`; review fixes `P4Fix` and `P4Approval`; browser-driven fixes `P4Fix2` · **Verified by:** orchestrator (final integrated gate, browser smoke)

## What landed

- `src/OmpOS.UI` (Razor class library, shared by the web head and the MAUI head): conversation list, session view, composer, `UiBlockRenderer` with views for markdown, code (copy control), tool-call card, permission card, plan panel, file-diff placeholder, error block, A2UI surface, unknown-block fallback, and user turns.
- `src/OmpOS.GenUi`: `UiFolder` is a pure fold over `StreamEvent`s. `prompt_accepted` maps to a `UserMessageBlock`, so the user's own turn is rendered. The A2UI v0.9.1 subset is restricted to the fixed catalog, and `SurfaceComponent` carries the image policy.
- Markdown: `src/OmpOS.UI/Services/MarkdownRenderer.cs` uses Markdig with `DisableHtml()` and advanced extensions, and keeps anchor `href` only for http, https, mailto and relative references. Raw HTML never reaches the output.
- Images: only inline raster `data:` URIs (png, jpeg, gif, webp) render as `img`. Every other image source renders as a link (`rel="noopener noreferrer nofollow"`), so the browser never fetches agent-chosen hosts.
- Approval: the permission card shows the agent-provided options verbatim, read-only, with a policy note. The view model never enters the awaiting-approval state from a permission event. Interactive approval is deferred as Q-HARN-PERMISSION.

## Verification

- `OmpOS.UI.Tests`: **91 passed, 0 failed**, from the worker run, consistent with the final gate.
- The UI-01 audit in `P4Fix2`'s report maps every UI-01 item to a file and a test name. Two items were actually broken and are fixed: markdown rendered as plain text, and the copy control was an unbound callback.
- The final browser run (see plan 02) exercised the markdown path, the image policy, the script escape and the user turn against the real API.

## Deviations and deferrals

- Interactive approval is not implemented. The awaiting-approval state exists and renders only from a server session status that the harness never emits (Q-HARN-PERMISSION).
- The approval domain methods remain in the Sessions layer with no network route; see the Phase 4 security record.
