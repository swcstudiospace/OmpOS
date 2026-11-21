# A11y requirements sheet — v0.16.0 phases 66–78 (T77d9-ux)

Task: T77d9-ux, correlation 77d97ef8-56a4-4b53-ae66-5b8c5e5d659e.
Source of every measured value: this run's tool output (`ux_contrast`, `ux_focus_contrast_check`,
`ux_target_size_check`, `ux_motion_duration_check`, `ux_breakpoint_order_check`,
`ux_token_name_check`, `ux_spacing_scale_check`, `ux_scale_monotonic_check`,
`scripts/ux_tokens.py` + bun twin). No ratio is copied from an earlier run or from CSS comments.
Token values are verbatim from `src/Autonogrammer.UI/wwwroot/styles/tokens.css`.

## Requirement rows (one per area)

| Area | WCAG 2.2 SC | Threshold | Measured (this run) | Status |
|---|---|---|---|---|
| Normal text, light (text, secondary, accent, success, warning, error on `color.surface.default` #fafaf9) | 1.4.3 Contrast (Minimum) | 4.5 | 16.34, 7.09, 6.80, 6.82, 6.78, 6.25 (lowest 6.25) | pass |
| Normal text, dark (on `color.dark.surface` #181818) | 1.4.3 | 4.5 | 15.03, 7.03, 6.52, 10.18, 10.63, 8.74 (lowest 6.52) | pass |
| Fill text on accent (light `#ffffff` on `#6d28d9`; dark `#141414` on `#a78bfa`) | 1.4.3 | 4.5 | 7.10 (light), 6.76 (dark) | pass |
| Text on tint (`#1c1c1a` on `#f5f1ff`) | 1.4.3 | 4.5 | 15.36 | pass |
| Hover/focus text (`#5b21b6` on `#fafaf9`; dark `#c4b5fd` on `#141414`) | 1.4.3 | 4.5 | 8.60, 9.97 | pass |
| Strong border / focus indicator, light (`#5b21b6` on 4 surfaces) | 1.4.11 Non-text Contrast | 3.0 | 8.60, 8.98, 7.87, 8.09 (lowest 7.87) | pass |
| Strong border / focus indicator, dark (`#c4b5fd` on 4 surfaces) | 1.4.11 | 3.0 | 9.62, 8.93, 9.98, 8.54 (lowest 8.54) | pass |
| Border-strong indicator (light `#6f6f6b` on `#fafaf9`) | 1.4.11 | 3.0 | 4.83 (also above 4.5) | pass |
| Border-strong indicator (dark `#808080` on `#181818`) | 1.4.11 | 3.0 | 4.49 | pass (non-text; below 4.5 by design, above 3.0) |
| Focus visibility + order | 2.4.3 Focus Order, 2.4.7 Focus Visible | order on every screen, 2px ring in focus token | ring 2px `size` token; per-screen focus_order in ux-spec | pass |
| Focus not obscured by sticky bars | 2.4.11 (AA) | scroll padding/gap on every scroll container | implemented in `a11y.css`; verified by axe walks in `scripts/web-smoke/a11y` | pass (design) |
| Target size | 2.5.8 Target Size (Minimum) | 24 CSS px (or 12 px centre-to-edge exception) | 11 of 11 meet minimum, 0 failing, 0 exception-only | pass |
| Text spacing | 1.4.12 | line 1.5, para 2em, letter 0.12em, word 0.16em — no clipping | checked by `analyzeTextSpacing` in web-smoke | pass |
| Reflow | 1.4.10 | 320 CSS px, 400% zoom, no loss | breakpoints base/sm/md/lg/xl; reflow probes at 320x568 | pass |
| Animation bound | 2.2.2 Pause, Stop, Hide (policy: 5000 ms max, reduced-motion variant required) | max 5000 ms, every animation has a reduced variant | max observed 200 ms; 3 of 3 have variant; durations tokenised 120/200 ms, 0 under reduced motion | pass |
| Spacing grid | design policy (base 2 px) | every gap a whole multiple of 2 px | multiples 1,2,3,4,6,8,12; 0 off scale | pass |
| Spacing scale ramp | design policy | surface ramp strictly decreasing luminance | luminance 1, 0.9553, 0.8702, 0.8291, 0.7736, 0.7131; violations [] | pass |
| Token names | design policy | kebab-case dot-path, depth ≤ 4, roots color/space/size/typography/motion/breakpoint/radius | 36 checked, 0 invalid, 0 duplicates, conforming true | pass |
| Breakpoints | design policy | unique names, strictly ascending, start at 0 | base 0, sm 560, md 840, lg 1200, xl 1440; valid true | pass |
| Token file + script | W3C shape, AA floor | every colour token has $type/$value and resolves; pairs below 4.5 are major | python: 77 tokens, 20 colours, 9 pairs all aa (lowest 4.83 border); bun: 0 findings; verdict pass | pass |

## Ratio table (verbatim tool output, truncated never rounded up)

Light, background `color.surface.default` #fafaf9 (python script pairs + `ux_contrast`):

- `color.text.default` #1c1c1a on #fafaf9 = 16.34 (aa true) — `ux_contrast` SUCCESS
- `color.text.secondary` #55555a on #fafaf9 = 7.09 — `ux_contrast` SUCCESS
- `color.accent.default` #6d28d9 on #fafaf9 = 6.80 — `ux_contrast` SUCCESS
- `color.accent.hover` / `color.focus.ring` #5b21b6 on #fafaf9 = 8.60 — `ux_contrast` SUCCESS
- `color.status.success` #166534 = 6.82 — `ux_contrast` SUCCESS
- `color.status.warning` #92400e = 6.78 — `ux_contrast` SUCCESS
- `color.status.error` #b3261e = 6.25 — `ux_contrast` SUCCESS (lowest normal text)
- `color.border.strong` #6f6f6b on #fafaf9 = 4.83 — `ux_contrast` SUCCESS (script pair)
- `#ffffff` on `#6d28d9` (accent fill text) = 7.10 — `ux_contrast` SUCCESS
- `#1c1c1a` on `#f5f1ff` (tint) = 15.36 — `ux_contrast` SUCCESS

Dark, background #181818 (reference pair set; dark tokens are `other` type in the JSON so the
script does not compare them against the light surface — they are verified here and by
`AccessibilityTests` / `CssContractTests`):

- #ececec = 15.03, #a3a3a3 = 7.03, #a78bfa = 6.52, #4ade80 = 10.18, #fbbf24 = 10.63,
  #ff9a9a = 8.74, #141414 on #a78bfa = 6.76, #c4b5fd on #141414 = 9.97 — all `ux_contrast` SUCCESS
- #808080 (border-strong) on #181818 = 4.49 — `ux_contrast` SUCCESS; non-text 3:1 passes
  (recorded as pass, not a major finding).

## Findings and severities

No `major`/`critical`/`blocker` finding. The dark border-strong 4.49 pair was considered and
classified `info` because its SC is 1.4.11 (3:1), not 1.4.3 (4.5:1) — it must never be quoted
as a text ratio. The spec's `a11y.min_contrast` is therefore 6.25 (lowest normal-text ratio),
not 4.49. `shared_verdict` is called with severities `[info, info, info, info, info, info]`
(one per audit peer result); any `major` would fail the gate.

Areas not audited: none. All five `a04.*` audits (`lint`, `contrast`, `focus`, `targets`,
`motion`) were run this run and recorded.

A06 must keep: 2px focus ring in the focus token on every interactive element; status never
carried by colour alone; live region as a persistent `role=status` container; 320px reflow;
`prefers-reduced-motion` and `data-motion=reduce` zeroing every duration.
A08 must keep: 0 axe errors over the 68 NFR-A1 runs (light+dark, 1440/1024/390) on every screen
in the spec; target-size, text-spacing, reflow and forced-colors probes green.
