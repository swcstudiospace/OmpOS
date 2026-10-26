# OmpOS visual parity with the Cursor Agents window, set in the Greptile faces

Status: design specification, DESIGN-1. Written 2026-10-26. No source, test or csproj file was changed to produce it.
Audience: engineers implementing the look. Behaviour is built; this document specifies only appearance, density, tokens, control styles, layout and the order to land them.
Direction (user, 2026-10-26): OmpOS is a Cursor desktop clone on the omp engine, set in DM Sans, Anybody and Space Mono. The default Cursor-style Agents window is the whole product. Beyond Cursor parity there are two additions: expanded model-provider settings and a Kanban tab. There is no other panel.

Marking. Every fact about Cursor or Avalonia carries a source row in Appendix A (read 2026-10-26 unless a different date is shown). Anything not backed by an opened source is marked **[INFERENCE]**. Numbers in the contrast tables were computed on 2026-10-26 with the WCAG 2.x relative-luminance formula, the same formula `ThemeContrastTests` uses.

---

## 0. What the screenshots and the code say is wrong (root causes)

The five "before" screenshots are in `docs/runbooks/evidence/ui-parity-2026-10-26/before/` (01 empty panel, 02 chat session, 03 omp terminal session, 04 Kanban cards, 05 Kanban empty). Reading them next to the code gives these causes. Each one is fixed by a numbered change in section 5.

| # | Observation (screenshot) | Cause (code or platform) | Fix |
|---|---|---|---|
| R1 | Buttons are white boxes with black outlines; hover is invisible; "Tools" toggle is a grey block (01, 02) | Fluent's `Button` and `ToggleButton` control themes set Background, BorderBrush and Foreground on `ContentPresenter#PART_ContentPresenter` per state from resource keys (`ButtonBackground`, `ButtonBackgroundPointerOver`, ...). `App.axaml` sets `Background` on the `Button` itself, which the presenter overrides on `:pointerover`, and it never overrides the keys. | Override the Fluent keys per theme variant (section 3.2). Do not fight the template with presenter selectors. |
| R2 | "Skip to conversation" box is always visible in the top left (all five) | `SkipLink` is a normal docked `Button` in `MainView.axaml` row 0. | Make it an overlay that is transparent until focused (section 4.2). |
| R3 | Kanban column headers and the Kanban empty heading are thin, small, low contrast (04, 05) | `OmpOS.Font.Display` points at `Anybody.ttf`. That file is a variable font whose `fvar` default instance is `wght=100` (Thin) and `wdth=100`. Avalonia cannot change variable axes (Appendix A, rows A14 to A16), so `FontWeight=SemiBold` is ignored and the face is drawn Thin. The headers are also 13 px. I confirmed the axes with fontTools on the shipped file: `wght` min 100, **default 100**, max 900; `OS/2.usWeightClass` 100. | Ship static instances (section 2.4). Anybody only at 20 px and above. |
| R4 | Light theme only, no dark theme at runtime | `App.axaml` has `RequestedThemeVariant="Light"`; no setting changes it. Dark values already exist in `OmpOSTokens.axaml`. | Dark default, System/Light/Dark setting (section 2.6). |
| R5 | Controls are 44 px high, the rail row is 64 px, text is 15 px; the screen looks like a form, not a tool | `OmpOS.Size.TargetMin` is 44 and is set as a local `MinHeight` on almost every control; `Font.Size.Body` is 15. Local values beat any style, so density cannot be reached by styles alone. | Density contract change (section 2.5, open question O1). |
| R6 | The composer is a plain text box with two square buttons and a permanent grey hint sentence (02) | `ComposerView.axaml`: `TextBox`, 44 px `Button`s, `HintLine` always visible above 720 px window height. | Rounded composer card; hint becomes a tooltip (section 4.6). |
| R7 | The pipeline strip, header chips, notice and "Latest:" line stack into four bands before any message (02) | Each of `ThreadHeaderView`, `PanelNoticeView`, `PipelineStripView` is its own 44 px-minimum band. | One 40 px header, one 28 px strip line (section 4.5). |
| R8 | Diff lines would draw in a literal font stack `Cascadia Code,Consolas,Menlo,monospace` | `DiffReviewView.axaml` does not use `OmpOS.Font.Mono`. | Use the token (section 5, change 14). |
| R9 | Blocks built in code (markdown, A2UI, inline completion) and the terminal cursor read colour once at construction | `BlockTokens.Get` and `OmpTerminalView.ApplyCursorColour` read the resource for `ActualThemeVariant` when built. A runtime theme switch leaves them stale. | Rebuild or re-read on `ActualThemeVariantChanged` (section 2.6). |

---

## 1. Principles and what "Cursor-like" means here

### 1.1 What the Cursor Agents window looks like (facts)

All facts below are read from Cursor's own pages and the official screenshots on those pages (Appendix A, rows A1 to A9). Colour values sampled from JPEG thumbnails are approximate and are used only to choose the Light palette (section 2.1).

1. Two-pane layout. A left **agent sidebar** (about 16 % of window width in the official captures), a centre **chat**, and an optional right pane that holds tabs for the diff, the browser and the terminal. The right pane can be hidden ("Hide Panel", Alt+Cmd+B). (A1, A2.)
2. Sidebar content. "New Agent" with a shortcut hint, "Marketplace", then agents grouped under small muted group labels (a repo name in the captures; "In progress" and "Ready for review" in the 3.0 changelog frame). Each row is a status glyph (spinner ring, dot, PR icon), a one-line ellipsised title, an optional second muted line, and a trailing muted time or cloud icon. The selected row is a soft filled pill. The footer holds "Open Workspace", the account row, a filter icon and a settings gear. (A2, A3.)
3. Chat. The user prompt is a rounded card. The agent output has no bubble: muted collapsible rows ("Thought briefly", "Explored 1 file, 1 search"), prose, inline code chips, then compact **diff cards** (file icon, file name, `+n -n` counts, a few tinted code lines). A "Review +34 -7" pill and "Worked for 5m 38s" label sit near the bottom. Summaries use bold headings, bullets and check items. (A2, A3, A4.)
4. Composer. A rounded card pinned to the bottom: a "+" button, the placeholder "Send follow-up", a **model chip** ("Composer 2", speed badge, chevron) and one round action button (microphone, or a filled stop square while running). In the IDE the placeholder reads "Plan, Build, / for commands, @ for context" with an "Agent" mode chip. (A2, A4, A5.)
5. Right pane. Tab icons with a git counter ("+34 -7"), the browser, a terminal. The Changes view shows "Changes PR #n", a Draft chip, a branch pill "cursor/... -> main", "7 Files Changed +93 -28" and one card per file (path, "+n", a "New" badge, revert and select icons). (A4, A6.)
6. Behaviours the visuals must carry: Shift+Tab rotates Plan and Agent modes; while an agent works, Enter twice or "Send now" steers, Tab queues; Cmd or Ctrl+K opens the command palette over conversations; Cmd or Ctrl+F finds in the conversation. (A7, A8.)

Cursor's marketing and docs captures are Light. The **default dark theme language** is taken from the third-party MIT reproduction of Cursor Dark (A10), not from Cursor's own source: editor `#181818`, sidebar `#141414`, input `#1c1c1c`, line highlight `#262626`, text `#E4E4E4` at 92 % alpha, borders `#E4E4E4` at 7 % alpha, list selection 12 % alpha white, accents desaturated blue (`#88C0D0`, `#81A1C1`), diff tints `#3FA266` and `#B80049` at 20 % alpha. Cursor documents `Cursor Dark` as a built-in theme name (A9). Treat the hex values as a close reproduction, not as Cursor's source of truth. **[INFERENCE]**: that the Agents window shell uses the same neutrals as the IDE theme.

What the language comes down to: dark neutral surfaces separated by hairlines, not by shadows; compact 12 to 13 px UI text; 6 to 8 px control radii; 12 px composer radius; muted secondary rows; one accent; small status glyphs instead of badges; keyboard hints in a quiet mono face.

### 1.2 What "Cursor-like" means for OmpOS

| Keep from Cursor | Meaning for OmpOS |
|---|---|
| Hierarchy | Selected rail row, thread header, transcript, composer; everything else is quiet. Same as `46-UI-SPEC.md`, "Hierarchy". |
| Density | Controls 28 or 32 px, UI text 13 px, small text 12 px, rail rows 48 px (two lines). |
| Surfaces | Rail darker than thread (Sunken), thread (Surface), cards and composer one step up (Raised), hairline borders. |
| One accent | `AccentPurple` is the only accent; its fill is the primary button, send, and the running glyph. |
| Message shape | User prompt as a card; agent output as prose; tool calls as one-line muted rows; edits as compact diff cards. |
| Composer | Rounded card, model chip, round send and stop, hint as a tooltip. |

| Deliberately kept from OmpOS | Why |
|---|---|
| WCAG 2.2 AA contrast, enumerated by `ThemeContrastTests` | Every new colour role is checked in section 2.2 and the test classification is extended in section 2.3. |
| Focus indicator: 2 px `AccentPurple` border, `FocusAdorner` null, in both themes | `KeyboardFocusTests`, `FocusVisibleToggleTests`, `KanbanBoardKeyboardTests` assert it. |
| Status is never colour alone | Glyph shape plus words, from the 46 Glyph Table. |
| Reduced motion | The `reduced-motion` window class strips every transition; the only animations stay the running pulse and the skeleton pulse. |
| Keyboard map and tab order | Section 6. |
| Greptile faces | DM Sans for UI and prose, Anybody only for large display headings (>= 20 px, SemiBold or heavier), Space Mono for code, terminal chrome, ids and keyboard hints. |
| Phase 46 and 39 contracts | Status vocabulary, accessible names, Kanban column set, group order in the rail. Only dimensions and colours change. |

What we do not copy: Cursor's logo, wording of marketing copy, its blue accent, the Marketplace row, cloud icons, or anything in the sidebar that has no OmpOS behaviour (no "Marketplace", no cloud-run marker).

---

## 2. Token set

All tokens live in `src/OmpOS.Native.Views/Themes/OmpOSTokens.axaml`. Colour roles stay inside the `Light` and `Dark` theme dictionaries; sizes, fonts and spacing stay outside them (as `PanelMarkupTests` requires of sizes).

### 2.1 Colour roles (hex values)

Existing roles keep their names. New roles are marked NEW. "Role use" is the classification `ThemeContrastTests` needs (section 2.3).

| Role (`OmpOS.Color.*`) | Light | Dark | Role use | Where it is drawn |
|---|---|---|---|---|
| `SurfaceSunken` NEW | `#f0f0ee` | `#141414` | Background | Window top bar, rail, code blocks, Kanban columns, terminal body, settings navigation |
| `Surface` | `#fafaf9` (was `#ffffff`) | `#181818` (was `#18181b`) | Background | Thread, settings content, window |
| `SurfaceRaised` | `#ffffff` (was `#f4f4f5`) | `#1f1f1f` (was `#27272a`) | Background | Cards, composer, user message, flyouts, menus, tooltips, inputs, secondary button rest |
| `Hover` NEW | `#ebebe8` | `#262626` | Background | Pointer-over fill of rows, ghost buttons, tabs, menu items |
| `Selected` NEW | `#e4e4e0` | `#2d2d2d` | Background | Selected rail row, checked toggle, selected tab hover, inline code chip, count pill |
| `SurfaceActive` | `#dcdcd7` (was `#e4e4e7`) | `#333333` (was `#3f3f46`) | Background | Pressed fill, skeleton bars |
| `DiffAdded` NEW | `#e2f4e7` | `#132a1c` | Background | Added line tint |
| `DiffRemoved` NEW | `#fbe6e6` | `#301719` | Background | Removed line tint, danger hover, error banner |
| `TextPrimary` | `#1c1c1a` (was `#1a1a1a`) | `#ececec` (was `#fafafa`) | Text | Default text |
| `TextSecondary` | `#55555a` | `#a3a3a3` (was `#b0b0b8`) | Text, NonText | Muted text, glyph strokes, hover border, scrollbar hover |
| `AccentPurple` | `#6d28d9` | `#a78bfa` | Background, NonText, **Text** (new use: links, accent words) | Primary fill, selected bar, focus border, running glyph |
| `AccentPurpleOn` | `#ffffff` | `#141414` (was `#18181b`) | Text | Label on the accent fill, text selection foreground |
| `FocusRing` | `#5b21b6` | `#c4b5fd` | NonText | Reserved. Keeps its present status (focus is drawn in `AccentPurple`, which the tests assert). |
| `StatusSuccess` | `#166534` | `#4ade80` | Text | Success glyph and text, `+n` counts |
| `StatusWarning` | `#92400e` | `#fbbf24` | Text | Warning glyph and text |
| `StatusError` | `#b3261e` | `#ff9a9a` (was `#fca5a5`) | Text, **NonText** (new use: invalid field border) | Error glyph and text, `-n` counts |
| `Border` NEW | `#e0e0dc` | `#2c2c2c` | **Decorative** (new use, section 2.3) | Hairline dividers, card and chip outlines, secondary button rest border |
| `BorderStrong` NEW | `#6f6f6b` | `#808080` | NonText | Text input outline, checkbox and switch outline, button hover border, scrollbar thumb |

Design decisions.

1. **Surface order.** Dark: Sunken `#141414` < Surface `#181818` < Raised `#1f1f1f` < Hover `#262626` < Selected `#2d2d2d` < Active `#333333`. Light mirrors it with Raised as the brightest. Because the meaning of `SurfaceRaised` in Light changes from "grey band" to "white card", views that use `SurfaceRaised` for bands (rail, header, columns) look wrong until they are restyled; step 2 of section 5 accepts that interim.
2. **No AccentPurple scale.** The tests multiply every text role by every background, so each new background role multiplies the checks. Hover and pressed on the accent fill use opacity (0.92 and 0.80; both keep the label at 4.5:1 or more, measured below). Selected tint is `Selected` plus the 2 px `AccentPurple` bar that `46-UI-SPEC.md` already specifies. No `AccentPurpleSubtle`, `AccentPurpleHover` or `AccentPurplePressed` role is needed.
3. **Decorative border versus strong border.** WCAG 2.2 SC 1.4.11 asks 3:1 for the visual information needed to identify a control. A button with a text label is identified by the label; a text input is identified by its outline. So `Border` (about 1.2:1, like Cursor's 7 % alpha hairline) draws dividers, card outlines and button outlines; `BorderStrong` (3:1 or more on every background) draws input outlines, switch and checkbox outlines and the scrollbar thumb.
4. **Disabled** is not a colour role. A disabled control gets `Opacity="0.5"` and `Foreground=TextSecondary`; WCAG exempts inactive components.
5. **Terminal palette** is in section 2.7.
6. **Greptile site look.** greptile.com sets warm paper surfaces (`#f8f7f3`, `#fdfcfa`, `#f5f4f0`) and an emerald grid accent (`#10b981`) (A12). The Light palette borrows only the warm off-white of the thread surface (`#fafaf9`), not the green. The accent stays purple, as in the existing contract.

### 2.2 Contrast table

Method: WCAG 2.x relative luminance, sRGB linearisation with the 0.03928 threshold, as in `ThemeContrastTests`. Requirement: 4.5:1 for text pairs, 3:1 for non-text pairs. Every cell is a pair the test product produces after the exclusions of section 2.3. All cells pass.

**Light, text roles over every background (need 4.5:1). Worst pair 4.75.**

| Foreground | SurfaceSunken | Surface | SurfaceRaised | Hover | Selected | SurfaceActive | DiffAdded | DiffRemoved | min |
|---|---|---|---|---|---|---|---|---|---|
| TextPrimary `#1c1c1a` | 14.96 | 16.34 | 17.07 | 14.29 | 13.39 | 12.40 | 14.90 | 14.27 | 12.40 |
| TextSecondary `#55555a` | 6.50 | 7.10 | 7.41 | 6.21 | 5.81 | 5.39 | 6.47 | 6.20 | 5.39 |
| AccentPurple `#6d28d9` | 6.23 | 6.80 | 7.10 | 5.95 | 5.57 | 5.16 | 6.20 | 5.94 | 5.16 |
| StatusSuccess `#166534` | 6.25 | 6.83 | 7.13 | 5.97 | 5.59 | 5.18 | 6.22 | 5.96 | 5.18 |
| StatusWarning `#92400e` | 6.21 | 6.79 | 7.09 | 5.94 | 5.56 | 5.15 | 6.19 | 5.93 | 5.15 |
| StatusError `#b3261e` | 5.73 | 6.26 | 6.54 | 5.47 | 5.13 | 4.75 | 5.70 | 5.47 | 4.75 |

**Light, non-text roles over every background (need 3:1).**

| Foreground | SurfaceSunken | Surface | SurfaceRaised | Hover | Selected | SurfaceActive | DiffAdded | DiffRemoved | min |
|---|---|---|---|---|---|---|---|---|---|
| TextSecondary `#55555a` | 6.50 | 7.10 | 7.41 | 6.21 | 5.81 | 5.39 | 6.47 | 6.20 | 5.39 |
| AccentPurple `#6d28d9` | 6.23 | 6.80 | 7.10 | 5.95 | 5.57 | 5.16 | 6.20 | 5.94 | 5.16 |
| FocusRing `#5b21b6` | 7.87 | 8.60 | 8.98 | 7.52 | 7.05 | 6.53 | 7.84 | 7.51 | 6.53 |
| BorderStrong `#6f6f6b` | 4.42 | 4.83 | 5.05 | 4.22 | 3.96 | 3.67 | 4.40 | 4.22 | 3.67 |
| StatusError `#b3261e` (field border) | 5.73 | 6.26 | 6.54 | 5.47 | 5.13 | 4.75 | 5.70 | 5.47 | 4.75 |

AccentPurpleOn `#ffffff` on AccentPurple `#6d28d9`: **7.10**. Decorative `Border` `#e0e0dc` against Sunken, Surface and Raised: 1.16, 1.27, 1.32 (no requirement; the test asserts at least 1.15 so the line stays visible).

**Dark, text roles over every background (need 4.5:1). Worst pair 4.64.**

| Foreground | SurfaceSunken | Surface | SurfaceRaised | Hover | Selected | SurfaceActive | DiffAdded | DiffRemoved | min |
|---|---|---|---|---|---|---|---|---|---|
| TextPrimary `#ececec` | 15.59 | 15.03 | 13.95 | 12.81 | 11.66 | 10.69 | 12.92 | 14.08 | 10.69 |
| TextSecondary `#a3a3a3` | 7.30 | 7.04 | 6.53 | 6.00 | 5.46 | 5.01 | 6.05 | 6.60 | 5.01 |
| AccentPurple `#a78bfa` | 6.77 | 6.52 | 6.06 | 5.56 | 5.06 | 4.64 | 5.61 | 6.11 | 4.64 |
| StatusSuccess `#4ade80` | 10.57 | 10.19 | 9.46 | 8.68 | 7.90 | 7.25 | 8.76 | 9.55 | 7.25 |
| StatusWarning `#fbbf24` | 11.04 | 10.64 | 9.87 | 9.07 | 8.25 | 7.57 | 9.14 | 9.97 | 7.57 |
| StatusError `#ff9a9a` | 9.07 | 8.74 | 8.12 | 7.45 | 6.78 | 6.22 | 7.52 | 8.19 | 6.22 |

**Dark, non-text roles over every background (need 3:1).**

| Foreground | SurfaceSunken | Surface | SurfaceRaised | Hover | Selected | SurfaceActive | DiffAdded | DiffRemoved | min |
|---|---|---|---|---|---|---|---|---|---|
| TextSecondary `#a3a3a3` | 7.30 | 7.04 | 6.53 | 6.00 | 5.46 | 5.01 | 6.05 | 6.60 | 5.01 |
| AccentPurple `#a78bfa` | 6.77 | 6.52 | 6.06 | 5.56 | 5.06 | 4.64 | 5.61 | 6.11 | 4.64 |
| FocusRing `#c4b5fd` | 9.98 | 9.62 | 8.93 | 8.20 | 7.46 | 6.84 | 8.27 | 9.01 | 6.84 |
| BorderStrong `#808080` | 4.66 | 4.50 | 4.17 | 3.83 | 3.49 | 3.20 | 3.87 | 4.21 | 3.20 |
| StatusError `#ff9a9a` (field border) | 9.07 | 8.74 | 8.12 | 7.45 | 6.78 | 6.22 | 7.52 | 8.19 | 6.22 |

AccentPurpleOn `#141414` on AccentPurple `#a78bfa`: **6.77**. Decorative `Border` `#2c2c2c` against Sunken, Surface, Raised: 1.32, 1.27, 1.18.

**Primary button states.** Opacity is applied to the whole button, so label and fill blend toward the surface together (Surface behind). Rest, hover 0.92, pressed 0.80:

| Variant | Rest | Hover (0.92) | Pressed (0.80) |
|---|---|---|---|
| Light | 7.10 | 6.15 | 4.80 |
| Dark | 6.77 | 5.94 | 4.75 |

All three states keep the label at 4.5:1 or more.

### 2.3 Required edits to `ThemeContrastTests` (same commit as the tokens)

`EachVariantDefinesExactlyTheClassifiedRoles` fails the moment a role exists that the test does not classify, so the test file changes in the same commit as the token file.

1. `RoleUses` adds: `SurfaceSunken`, `Hover`, `Selected`, `DiffAdded`, `DiffRemoved` as `Background`; `BorderStrong` as `NonText`; `Border` as the new `RoleUse.Decorative`; `AccentPurple` gains `Text`; `StatusError` gains `NonText`.
2. `RoleUse.Decorative` = 8. It is not part of `TextForegrounds`, `Backgrounds` or `NonTextForegrounds`, so it enters no product. A new test asserts, for each variant, `ContrastRatio(Border, Surface) >= 1.15` and the same against `SurfaceSunken` and `SurfaceRaised`, so a divider cannot vanish.
3. New `TextExclusions` (reason in parentheses): `AccentPurple` over `AccentPurple` (a role has no contrast against itself); `AccentPurpleOn` over `SurfaceSunken`, `Hover`, `Selected`, `DiffAdded`, `DiffRemoved` (`AccentPurpleOnlyOnAccent`).
4. New `NonTextExclusions`: `BorderStrong` over `AccentPurple` (no view draws a `BorderStrong` over the accent fill); `StatusError` over `AccentPurple` (no field border is drawn over the accent fill); `FocusRing`, `TextSecondary` pairs already excluded.
5. `ThePanelStatusPairsAreCovered` keeps passing unchanged (it asserts pairs that remain in the product).
6. `TheFontTokensNameTheBundledFaces` changes with the font assets (section 2.4).

### 2.4 Typography, in the Greptile faces

Tokens: `OmpOS.Font.Family` (DM Sans), `OmpOS.Font.Display` (Anybody), `OmpOS.Font.Mono` (Space Mono). Fallbacks stay as they are.

**Faces and weights actually used**

| Face | Weights | Where |
|---|---|---|
| DM Sans | 400, 500, 600 | UI chrome, prose, headings, buttons, tabs |
| Anybody | 600 only | Display headings of 20 px and above: empty-state hero (24), settings page title (20). Never below 20 px. Never Thin or Light. |
| Space Mono | 400, 700 | Code, diff, terminal, model and provider identifiers, task and graph ids, endpoint text, keyboard hints (`Ctrl+N`) |

**Size scale (token names are `OmpOS.Font.Size.*` and `OmpOS.Font.LineHeight.*`)**

| Token | Size / line height | Weight | Role |
|---|---|---|---|
| `Small` NEW | 12 / 16 | 400 or 500 | Timestamps, subtitles, chip text, tooltip text, keyboard hints, group labels |
| `Label` (13, line height 18.2 -> 18) | 13 / 18 | 400 default, 500 emphasis | **Default UI text**: buttons, tabs, rail titles, form labels, tool-call rows, settings rows. `TextBlock` default in `App.axaml` becomes `Label`. |
| `Body` (was 15 / 22.5) | **14 / 21** | 400 | Message prose, markdown paragraphs, plan text, empty-state body |
| `Heading` (was 18 / 21.6) | **16 / 22** | 600 | Thread title in empty states, section headings, markdown H2, provider card name |
| `DisplaySm` NEW | 20 / 26 | Anybody 600 | Settings page title |
| `Display` NEW | 24 / 30 | Anybody 600 | Empty-state and error-state hero heading |
| `Mono` NEW | 12 / 18 | Space Mono 400 | Code blocks, diff lines, ids, model chip, terminal chrome |
| `MonoTerminal` NEW | 13 / 17 | Space Mono 400 | The omp terminal cell grid (`Output`) |

Markdown headings inside prose: H1 18/24 600, H2 16/22 600, H3 14/21 600, all DM Sans. Inline code: Space Mono 12 on `Selected`, radius 4, padding 4,1.

Weight tokens: `OmpOS.Font.Weight.Regular` (Normal), `Semibold` (SemiBold) stay; add `OmpOS.Font.Weight.Medium` (Medium = 500).

Letter spacing: none anywhere (0). Cursor-like compactness comes from size, not tracking. No uppercase group labels: sentence case 12 px 500 `TextSecondary`.

**Font assets (change 1 in section 5).** The variable files cannot be styled by weight in Avalonia (R3). Replace them by static instances.

| Family | Ship (path under `Assets/Fonts`) | Source |
|---|---|---|
| DM Sans | `DMSans/DMSans-Regular.ttf`, `DMSans-Medium.ttf`, `DMSans-SemiBold.ttf`; **remove** `DMSans/DMSans.ttf` | Unmodified upstream statics: `googlefonts/dm-fonts`, commit `4412393b7d2de9fe7a92064c2dce9b5af5d7fd26`, path `Sans/fonts/ttf/DMSans-{Regular,Medium,SemiBold}.ttf`. I downloaded them and read the name tables: Regular has nameID 1 `DM Sans`; Medium and SemiBold carry nameID 16 `DM Sans` and nameID 1 `DM Sans Medium` / `DM Sans SemiBold`; `usWeightClass` 400, 500, 600; no `fvar`. `FontResourceTests` accepts a family in nameID 1 or 16. |
| Anybody | `Anybody/Anybody-SemiBold.ttf`; **remove** `Anybody/Anybody.ttf` | Not published as a static by upstream (the `google/fonts` folder holds only `Anybody[wdth,wght].ttf`). Derive from the pinned upstream file with fontTools: `fonttools varLib.instancer Anybody.ttf wght=600 wdth=100 --update-name-table -o Anybody-SemiBold.ttf`. I ran this: output has no `fvar`, `usWeightClass` 600, nameID 16 `Anybody`, nameID 17 `SemiBold`. Under the OFL this is a Modified Version; no Reserved Font Name is declared for Anybody (`docs/plans/vision-research/greptile-font.md` rows 24, 25), so naming it `Anybody` is allowed. The manifest row records the derivation. |
| Space Mono | `SpaceMono/SpaceMono-Regular.ttf` (keep), add `SpaceMono/SpaceMono-Bold.ttf` | Upstream file, `google/fonts` commit `45e3d5269e4a17d823f6d84470bc084dc5ae46f6`, `ofl/spacemono/SpaceMono-Bold.ttf` (nameID 1 `Space Mono`, `usWeightClass` 700). |

Token values change from a file URI to a folder URI so Avalonia loads every weight of the family:

```xml
<FontFamily x:Key="OmpOS.Font.Family">avares://OmpOS.Native.Views/Assets/Fonts/DMSans#DM Sans, Helvetica Neue, Helvetica, Arial, sans-serif</FontFamily>
<FontFamily x:Key="OmpOS.Font.Display">avares://OmpOS.Native.Views/Assets/Fonts/Anybody#Anybody, Helvetica Neue, Helvetica, Arial, sans-serif</FontFamily>
<FontFamily x:Key="OmpOS.Font.Mono">avares://OmpOS.Native.Views/Assets/Fonts/SpaceMono#Space Mono, Menlo, Consolas, Liberation Mono, monospace</FontFamily>
```

`Assets/Fonts/*` must be an `AvaloniaResource` glob (check `OmpOS.Native.Views.csproj`; it already includes the three files). Same commit updates `tests/OmpOS.Native.Tests/Theme/FontManifest.txt` (one row per shipped TTF, with the derivation noted for Anybody), `FontResourceTests` (`FontFiles`, `Families`, `FamilyNotices`: one row per shipped file; the "bytes are the upstream bytes" summary now reads "or the recorded derivation"), and `ThemeContrastTests.TheFontTokensNameTheBundledFaces` (`ShouldStartWith` the folder URIs above). Add the new OFL files exactly as the current ones are shipped.

Risk **[INFERENCE]**: whether Avalonia's embedded collection groups `DM Sans Medium` (nameID 1) under the family `DM Sans` depends on the platform font stack reading nameID 16. Acceptance check below proves it on the headless platform and by screenshot on Windows or macOS. Fallback if a platform fails: rename nameID 1 to `DM Sans` and nameID 2 to the style with fontTools (I tested this recipe on a derived SemiBold: nameID 1 `DM Sans`, nameID 2 `SemiBold`, `usWeightClass` 600, no `fvar`).

Acceptance (change 1): a headless test measures the same string in DM Sans at weights 400 and 600 and asserts the advance widths differ and that `FontManager` resolves `OmpOS.Font.Display` at SemiBold to a face whose weight is 600; after it, screenshot 05 shows "No sessions yet" at 24 px, solid, not Thin.

### 2.5 Radii, borders, spacing, sizes, elevation, motion

**Radius** (`OmpOS.Radius.*`, `CornerRadius`)

| Token | Value | Use |
|---|---|---|
| `Sm` (exists) | 4 | Inline code chip, keyboard chip, skeleton bars, scrollbar thumb |
| `Control` NEW | 6 | Buttons, inputs, tabs, rail rows, menu items |
| `Md` (exists) | 8 | Cards (message, diff, Kanban card), banners |
| `Column` NEW | 10 | Kanban column, provider card |
| `Lg` (exists) | 12 | Composer card, flyouts, popups, dialogs |
| `Pill` NEW | 999 | Chips, count pills, round icon buttons (send, stop) |

Also set Fluent resources `ControlCornerRadius` = 6 and `OverlayCornerRadius` = 10 (section 3.2).

**Borders.** `OmpOS.Border.Hairline` NEW = `Thickness` 1 (all hairlines and card outlines). `OmpOS.Size.FocusBorder` stays 2.

**Spacing** (`Thickness`, with matching `x:Double` gaps; existing names unchanged)

| Token | Value | Gap token |
|---|---|---|
| `Space.Xxs` NEW | 2 | `Gap.Xxs` 2 |
| `Space.Xs` | 4 | `Gap.Xs` 4 |
| `Space.Compact` NEW | 6 | `Gap.Compact` 6 |
| `Space.Sm` | 8 | `Gap.Sm` 8 |
| `Space.Cozy` NEW | 12 | `Gap.Cozy` 12 |
| `Space.Md` | 16 | |
| `Space.Lg` | 24 | |
| `Space.Xl`, `Xxl`, `Xxxl` | 32, 48, 64 | |

**Sizes** (`x:Double`, outside the theme variants)

| Token | Value | Replaces or adds | Test impact |
|---|---|---|---|
| `Size.TargetMin` | **32** (was 44) | Single place that sets the control height of every view that writes `MinHeight={DynamicResource OmpOS.Size.TargetMin}` | See O1: 26 assertions `ShouldBeGreaterThanOrEqualTo(44)` in 13 test files |
| `Size.ControlHeight` NEW | 32 | Buttons, inputs, composer buttons, settings controls | none |
| `Size.ControlHeightSm` NEW | 28 | Toolbar buttons, tabs, segmented, rail nav rows, chips that are buttons | none |
| `Size.IconButton` NEW | 28 | Square icon buttons (close, settings gear, copy) | none |
| `Size.IconButtonSm` NEW | 24 | Icon buttons inside rows and code-block headers (WCAG 2.5.8 AA minimum 24 px) | none |
| `Size.IconSize` | 16 (keep) | Glyph in headers and chips | none |
| `Size.IconSizeSm` NEW | 14 | Glyph in tool-call rows and the pipeline strip | none |
| `Size.SessionRowMinHeight` | **48** (was 64) | Two-line rail row | `PanelMarkupTests` row 40 |
| `Size.ChipHeight` | **20** (was 24) | Status and kind chips | `PanelMarkupTests` row 42 |
| `Size.ThreadContentMaxWidth` | **760** (was 880) | Transcript and composer column | `PanelMarkupTests` row 39 |
| `Size.TitleBarHeight` NEW | 40 | Top bar | none |
| `Size.PipelineStripHeight` NEW | 28 | Pipeline strip line | none |
| `Size.ComposerMinHeight` NEW | 72 | Composer card minimum (two text lines plus footer) | none |
| `Size.ComposerMaxHeight` NEW | 220 | Composer growth limit; the text scrolls after it | none |
| `Size.ScrollBarThumb` NEW | 6 | Visible thumb width at rest | none |
| `Size.SettingsNavWidth` NEW | 220 | Settings left navigation | none |
| `Size.SettingsContentMaxWidth` NEW | 720 | Settings page column | none |
| `Size.KanbanColumnWidth` NEW | 280 (min 240) | Kanban column (same values as today) | none |
| Kept as is | `AgentRailWidth` 280, `SessionListWidth` 240, `ThreadMinWidth` 400, `StageFlyoutWidth` 360, `StageFlyoutMaxHeight` 360, `StageRowMinHeight` 28, `FocusBorder` 2 | | none |

**Elevation.** Surfaces are separated by tone and hairlines, not shadows. Two shadow tokens, defined per variant as `BoxShadows` resources (they are not colour roles, so the colour tests do not scan them; the colour-literal test scans `Views/**` only and the tokens file is outside it):

| Token | Light | Dark | Use |
|---|---|---|---|
| `OmpOS.Shadow.Popup` | `0 8 24 0 #29000000` | `0 8 24 0 #99000000` | Flyouts, menus, combo drop-down, tooltips, dragged Kanban card |
| `OmpOS.Shadow.Card` | `0 1 2 0 #14000000` | `0 0 0 0 #00000000` | Composer card and message cards (Light only) |

**[INFERENCE]** `<BoxShadows x:Key="...">` as a theme-dictionary resource compiles in Avalonia 12.1.4; if it does not, declare the two shadows as `x:String` and convert with `BoxShadows.Parse` in `BlockTokens`.

**Motion** (UI-SPEC Motion stays): one transition, `BrushTransition` on `Background` 100 ms, on `Button`, `ToggleButton`, `ListBoxItem` and `TabStripItem`. Popups and flyouts have no animation. The running glyph pulse (1.2 s) and the skeleton pulse (1.6 s) stay. `Window.reduced-motion` removes the transition from every control class in section 3.

### 2.6 Dark by default, and the theme choice

1. `apps/OmpOS.Native/App.axaml`: `RequestedThemeVariant="Dark"` (was `Light`). This is the value before settings load, so the first frame is dark.
2. Settings > General gets a **Theme** row with three choices, in this order: `System`, `Light`, `Dark`. Default `Dark`. It follows the existing Motion row pattern: a `ThemePreference` enum next to `MotionPreference`, a `Theme` value in `GeneralSettingsValues`, `GeneralPage.SetTheme` and `GeneralPage.SelectedThemeOption`, draft until `SaveAsync` (the page already supports draft and save). Row labels, descriptions and the three option names are new constants in `SettingsCopy` (the views hold no literal copy).
3. Applying it: on save and at start-up after the value is read, `Application.Current.RequestedThemeVariant = preference switch { System => ThemeVariant.Default, Light => ThemeVariant.Light, Dark => ThemeVariant.Dark }`. `ThemeVariant.Default` follows the operating system. Persist in the same store as the motion setting (the `IGeneralSettingsSource` already persists `GeneralSettingsValues`).
4. Live switching. Controls bound with `DynamicResource` follow the variant automatically. Three places do not and must re-read on `ActualThemeVariantChanged` (R9): `Blocks/BlockTokens.Get` call sites that build controls in code (`MarkdownAstBuilder`, `A2uiSurfaceView`, `FileEditBlockView`, `BlockControls`, `InlineCompletionBox`) rebuild their content or bind with `DynamicResource` through `Control.Bind(property, resourceKey)` instead of one-time `Get`; `OmpTerminalView.ApplyCursorColour` and the terminal palette (section 2.7) re-apply.
5. Headless test additions: `ThemePreferenceTests` (default is Dark; saving Light changes `ActualThemeVariant`; System resolves to the platform; a transcript with a markdown block recolours after the switch).

### 2.7 Terminal palette

`Iciclecreek.Terminal.TerminalControl` draws the omp TUI. Set its background to `SurfaceSunken`, foreground to `TextPrimary`, cursor to `TextPrimary` (already done), selection to `Selected`. If the control exposes an ANSI palette, set it from this table (each normal and bright colour is at least 4.5:1 on the terminal background: Dark `#141414`, Light `#f0f0ee`); if it does not, the control's own palette stays and an owner note is recorded (it may fail contrast in Light) **[INFERENCE]: the control's palette API was not opened**.

| ANSI | Dark | ratio | Light | ratio |
|---|---|---|---|---|
| black | `#808080` | 4.66 | `#1c1c1a` | 14.96 |
| red | `#ff7b8e` | 7.44 | `#b3261e` | 5.73 |
| green | `#5fd38a` | 9.80 | `#166534` | 6.25 |
| yellow | `#f1b467` | 10.06 | `#7a4a07` | 6.55 |
| blue | `#7aa2f7` | 7.32 | `#1d4ed8` | 5.87 |
| magenta | `#c4a7ff` | 9.05 | `#6d28d9` | 6.23 |
| cyan | `#6fd0e0` | 10.33 | `#0e6577` | 5.85 |
| white | `#d4d4d4` | 12.43 | `#55555a` | 6.50 |
| bright black | `#8a8a8a` | 5.34 | `#55555a` | 6.50 |
| bright red | `#ff9aa9` | 9.16 | `#c0392b` | 4.77 |
| bright green | `#86e3a8` | 11.90 | `#1b7a3f` | 4.72 |
| bright yellow | `#f6cb8b` | 12.14 | `#8a5a00` | 5.19 |
| bright blue | `#9db9fa` | 9.43 | `#2557d6` | 5.39 |
| bright magenta | `#d6c2ff` | 11.42 | `#7c3aed` | 4.99 |
| bright cyan | `#94dfeb` | 12.29 | `#0f7488` | 4.75 |
| bright white | `#f4f4f4` | 16.75 | `#3f3f46` | 9.15 |

These hexes are terminal data, not UI colour roles. They live in code next to `OmpTerminalView` (not in `Views/*.axaml`, which the colour-literal test scans), selected by `ActualThemeVariant`.

---

## 3. Control style library

### 3.1 Where it lives and how it composes

* New file `src/OmpOS.Native.Views/Themes/OmpOSControls.axaml` (`<Styles>` root). It holds every selector in this section. `App.axaml` includes it after `<FluentTheme DensityStyle="Compact" />` through `<StyleInclude Source="avares://OmpOS.Native.Views/Themes/OmpOSControls.axaml" />`. `DensityStyle="Compact"` is a real `FluentTheme` property (A15); its `Compact.xaml` sets `ComboBoxMinHeight` 24, `ButtonPadding` 6,4, `TabItemMinHeight` 28, `ListBoxItemPadding` 4,2 and similar. Our tokens then override the values we care about.
* The Fluent key aliases of section 3.2 are `StaticResource` entries inside the `Light` and `Dark` theme dictionaries of `OmpOSTokens.axaml`, the same idiom Fluent uses in its own `FluentControlResources.xaml` (A15). They are not `OmpOS.Color.*` keys and not `SolidColorBrush` elements, so `ThemeContrastTests.ReadVariant` ignores them.
* `PanelStyles.axaml` and `KanbanStyles.axaml` stay, but their copies of button, chip and focus chrome are deleted once `OmpOSControls.axaml` supplies them (clean cutover: `Button.primary` and its presenter duplicates, `Button.destructive`, `Button.chip-button`, `Button.strip`, `Border.chip`, the `Window.reduced-motion` blocks in `App.axaml`). Class names stay: **no existing `Classes=` value or `x:Name` is renamed**.
* The shared focus style in `App.axaml` (the `*:focus-visible` blocks) moves into `OmpOSControls.axaml` unchanged in effect: `BorderBrush = AccentPurple`, `BorderThickness = FocusBorder` (2), `FocusAdorner = null`, on `Button`, `ToggleButton`, `TextBox`, `ListBox`, `ListBoxItem`, `ScrollViewer`, plus the new `TabStripItem`, `ComboBox`, `CheckBox`, `RadioButton`, `ToggleSwitch`, `MenuItem`.

### 3.2 Fluent resource aliases (same list in `Light` and `Dark`)

Key names are read from Avalonia's `Fluent` theme sources on master (A15). Tokens on the right are `OmpOS.Color.*` unless stated. Write each as `<StaticResource x:Key="ButtonBackground" ResourceKey="OmpOS.Color.SurfaceRaised" />`. A transparent fill cannot be a `OmpOS.Color.*` element, because `EveryColourRoleIsAHexColour` reads every `OmpOS.Color.*` element as a hex role. Define it once outside the variants as `<SolidColorBrush x:Key="OmpOS.Brush.Transparent" Color="Transparent" />` and alias to it.

| Fluent key (state suffix list) | Alias target |
|---|---|
| `ButtonBackground` | `SurfaceRaised` |
| `ButtonBackgroundPointerOver` | `Hover` |
| `ButtonBackgroundPressed` | `SurfaceActive` |
| `ButtonBackgroundDisabled` | `SurfaceRaised` |
| `ButtonForeground`, `...PointerOver`, `...Pressed` | `TextPrimary` |
| `ButtonForegroundDisabled` | `TextSecondary` |
| `ButtonBorderBrush`, `...Disabled` | `Border` |
| `ButtonBorderBrushPointerOver`, `...Pressed` | `BorderStrong` |
| `AccentButtonBackground`, `...PointerOver`, `...Pressed`, `...Disabled` | `AccentPurple` (the `primary` class is ours, but a stray `Classes="accent"` stays on palette) |
| `AccentButtonForeground` (all states) | `AccentPurpleOn` |
| `AccentButtonBorderBrush` (all states) | `OmpOS.Brush.Transparent` |
| `ToggleButtonBackground`, `...PointerOver`, `...Pressed`, `...Disabled` | `SurfaceRaised`, `Hover`, `SurfaceActive`, `SurfaceRaised` |
| `ToggleButtonBackgroundChecked`, `...CheckedPointerOver`, `...CheckedPressed`, `...CheckedDisabled` | `Selected`, `Selected`, `SurfaceActive`, `Selected` |
| `ToggleButtonForeground` (all unchecked and checked states) | `TextPrimary` (Disabled: `TextSecondary`) |
| `ToggleButtonBorderBrush`, `...Disabled` | `Border` |
| `ToggleButtonBorderBrushPointerOver`, `...Pressed`, `...Checked`, `...CheckedPointerOver`, `...CheckedPressed` | `BorderStrong` |
| `TextControlBackground`, `...PointerOver`, `...Focused`, `...Disabled` | `SurfaceRaised`, `SurfaceRaised`, `SurfaceRaised`, `Surface` |
| `TextControlForeground`, `...PointerOver`, `...Focused` | `TextPrimary` |
| `TextControlForegroundDisabled`, `TextControlPlaceholderForegroundDisabled` | `TextSecondary` |
| `TextControlPlaceholderForeground`, `...PointerOver`, `...Focused` | `TextSecondary` |
| `TextControlBorderBrush` | `BorderStrong` |
| `TextControlBorderBrushPointerOver` | `TextSecondary` |
| `TextControlBorderBrushFocused` | `AccentPurple` |
| `TextControlBorderBrushDisabled` | `Border` |
| `TextControlBorderThemeThickness` / `...Focused` (Thickness) | `1` / `2` (values, not aliases) |
| `ComboBoxBackground`, `...PointerOver`, `...Pressed`, `...Disabled`, `ComboBoxBackgroundUnfocused` | `SurfaceRaised`, `SurfaceRaised`, `SurfaceActive`, `Surface`, `SurfaceRaised` |
| `ComboBoxBorderBrush` / `...PointerOver` / `...Pressed` / `...Disabled` | `BorderStrong` / `TextSecondary` / `TextSecondary` / `Border` |
| `ComboBoxBackgroundBorderBrushFocused` | `AccentPurple` |
| `ComboBoxForeground`, `...Focused`, `...FocusedPressed` | `TextPrimary` |
| `ComboBoxDropDownBackground` / `ComboBoxDropDownBorderBrush` | `SurfaceRaised` / `Border` |
| `ComboBoxDropDownGlyphForeground` (all states) | `TextSecondary` |
| `ComboBoxItemBackgroundPointerOver`, `ComboBoxItemBackgroundSelected`, `...SelectedPointerOver`, `...Pressed`, `...SelectedPressed` | `Hover`, `Selected`, `Selected`, `SurfaceActive`, `SurfaceActive` |
| `ComboBoxItemForeground` (all states) | `TextPrimary` |
| `SystemControlHighlightListLowBrush` (ListBoxItem hover) | `Hover` |
| `SystemControlHighlightListMediumBrush` (ListBoxItem pressed) | `SurfaceActive` |
| `SystemControlHighlightListAccentLowBrush`, `...MediumBrush` (selected, selected hover) | `Selected` |
| `SystemControlHighlightListAccentHighBrush` (selected pressed) | `SurfaceActive` |
| `TabItemHeaderBackgroundUnselected`, `...Selected`, `...SelectedPointerOver`, `...SelectedPressed` | `OmpOS.Brush.Transparent` |
| `TabItemHeaderBackgroundUnselectedPointerOver` / `...UnselectedPressed` | `Hover` / `SurfaceActive` |
| `TabItemHeaderForegroundUnselected`, `...UnselectedPointerOver`, `...UnselectedPressed`, `...Disabled` | `TextSecondary`, `TextPrimary`, `TextPrimary`, `TextSecondary` |
| `TabItemHeaderForegroundSelected`, `...SelectedPointerOver`, `...SelectedPressed` | `TextPrimary` |
| `TabItemHeaderSelectedPipeFill` | `AccentPurple` |
| `TabItemHeaderFontSize` (double), `TabItemHeaderThemeFontWeight` | `13`, `Medium` |
| `TabStripItemMinHeight` (double), `TabStripItemPipeThickness` | `28`, `2` |
| `ScrollBarBackground`, `ScrollBarTrackFill` | `OmpOS.Brush.Transparent` |
| `ScrollBarBackgroundPointerOver`, `ScrollBarTrackFillPointerOver` | `Hover` |
| `ScrollBarThumbBackgroundColor` and the thumb fill at rest | `BorderStrong` (Fluent defines the thumb through `ScrollBarThumbBackgroundColor`; alias the matching brush) |
| `ScrollBarThumbFillPointerOver`, `ScrollBarThumbFillPressed` | `TextSecondary`, `TextPrimary` |
| `ScrollBarSize` (double) | `10` |
| `ToolTipBackground`, `ToolTipBorderBrush`, `ToolTipForeground` | `SurfaceRaised`, `Border`, `TextPrimary` |
| `ToolTipContentThemeFontSize`, `ToolTipContentMaxWidth` (double) | `12`, `360` |
| `ToolTipBorderThemePadding` | `8,4` |
| `FlyoutPresenterBackground`, `FlyoutBorderThemeBrush` | `SurfaceRaised`, `Border` |
| `FlyoutBorderThemePadding`, `FlyoutContentThemePadding` | `12`, `0` |
| `MenuFlyoutPresenterBackground`, `MenuFlyoutPresenterBorderBrush` | `SurfaceRaised`, `Border` |
| `MenuFlyoutItemBackground` (rest) / `...PointerOver` / `...Pressed` | `OmpOS.Brush.Transparent` / `Hover` / `SurfaceActive` |
| `MenuFlyoutItemForeground`, `...PointerOver`, `...Pressed` | `TextPrimary` |
| `MenuFlyoutItemForegroundDisabled`, `MenuFlyoutItemKeyboardAcceleratorTextForeground` (all states) | `TextSecondary` |
| `MenuFlyoutItemThemePaddingNarrow` | `8,4` |
| `ToggleSwitchFillOff` (track fill) | `OmpOS.Brush.Transparent` |
| `ToggleSwitchFillOn`, `...PointerOver`, `...Pressed` | `AccentPurple` |
| `ToggleSwitchKnobFillOff` | `TextSecondary` |
| `ToggleSwitchKnobFillOn` | `AccentPurpleOn` |
| `ToggleSwitchContentForeground` | `TextPrimary` |
| `CheckBoxCheckBackgroundFillChecked` (all states) | `AccentPurple` |
| `CheckBoxCheckBackgroundStrokeUnchecked` (all states) | `BorderStrong` |
| `RadioButtonOuterEllipseCheckedFill`, `RadioButtonCheckGlyphFill` | `AccentPurple`, `AccentPurpleOn` |
| `ControlCornerRadius`, `OverlayCornerRadius` (CornerRadius) | `6`, `10` |
| `ButtonPadding` (Thickness) | `11,0` (1 px less than the visual 12 because the 1 px border adds it; focus section 3.13) |
| `ControlContentThemeFontSize` (double) | `13` |

Suffix completeness: for `ToggleSwitch` (outer stroke keys), `RadioButton` and `CheckBox` (per-state keys) the table names the keys that carry the roles. Every other `...PointerOver`, `...Pressed`, `...Disabled`, `...Checked` variant of a key in the table maps to the same role as its rest key unless the table says otherwise. Complete the list by reading the control's own XAML under `Avalonia.Themes.Fluent/Controls` at the pinned Avalonia version; the acceptance test below walks the keys a control actually requests and fails on any that still resolve to Fluent's grey.

Acceptance (change 3): a headless `ControlStyleTests` creates one of each control in each variant, forces the pseudo-classes (`:pointerover`, `:pressed`, `:disabled`, `:checked`) and asserts the presenter background, foreground and border resolve to the roles in sections 3.3 to 3.12 (compare `SolidColorBrush.Color` with the variant's role). A second test fails if any `ButtonBackground*`, `TextControlBackground*`, `ToggleButtonBackground*` key still resolves to a Fluent value (`Application.TryGetResource` returns a brush that is not one of the OmpOS roles).

### 3.3 Button

Height: `ControlHeight` 32 (`sm`: 28). Radius `Radius.Control` 6. Font: DM Sans `Label` 13, weight Medium (500) for primary, Regular for the others. Content gap 6. Horizontal padding: rest `11,0`, focus-visible `10,0` (the border grows from 1 to 2, so the outer size never changes). Transition `BrushTransition Background 100 ms`.

| Variant | Class | Rest | Hover | Pressed | Disabled | Focus-visible |
|---|---|---|---|---|---|---|
| Secondary (default `Button`) | none | bg `SurfaceRaised`, fg `TextPrimary`, border `Border` 1 | bg `Hover`, border `BorderStrong` | bg `SurfaceActive`, border `BorderStrong` | `Opacity` 0.5, fg `TextSecondary` | border `AccentPurple` 2, padding `10,0` |
| Primary | `primary` (exists) | bg `AccentPurple`, fg `AccentPurpleOn`, border transparent 1, weight 500 | `Opacity` 0.92 | `Opacity` 0.80 | `Opacity` 0.5 | border `TextPrimary` 2 (exists: a purple ring on a purple fill is invisible), padding `10,0` |
| Ghost | `ghost` NEW | bg transparent, fg `TextSecondary`, border transparent 1 | bg `Hover`, fg `TextPrimary` | bg `SurfaceActive`, fg `TextPrimary` | `Opacity` 0.5 | border `AccentPurple` 2 |
| Icon | `icon` NEW (combine with `ghost` or none) | 28 x 28 (`IconButtonSm` 24 with `sm`), padding 0, radius 6, glyph `Path` `Stroke` `TextSecondary`, `StrokeThickness` 1.5, 16 px glyph | glyph `TextPrimary`, bg `Hover` | bg `SurfaceActive` | `Opacity` 0.5 | border `AccentPurple` 2 (size does not change: fixed width and height) |
| Danger | `destructive` (exists; it is the danger variant) | bg transparent, fg `StatusError`, border `Border` | bg `DiffRemoved`, border `BorderStrong` | bg `DiffRemoved`, border `BorderStrong` | `Opacity` 0.5 | border `AccentPurple` 2 |
| Round action | `round` NEW (with `primary` for send, none for stop) | 28 x 28, `Radius.Pill`; send: bg `AccentPurple`, glyph fill `AccentPurpleOn`; stop: bg `SurfaceRaised`, border `BorderStrong`, glyph fill `TextPrimary` | send: opacity 0.92; stop: bg `Hover` | send: 0.80; stop: bg `SurfaceActive` | `Opacity` 0.5 | `TextPrimary` 2 (send), `AccentPurple` 2 (stop) |
| Skip link | `skip-link` NEW | see 4.2 | | | | |

Existing glyph paths (`Send`, `Stop`) keep their geometry; their `Fill` comes from a style (`Button.round Path`) instead of a local value.

### 3.4 TextBox

Single-line height 32 (`sm` 28); multiline `MinHeight` 32. Radius 6. Font `Label` 13. Border 1, `BorderStrong`. Padding rest `9,0` for single-line (vertical centring by `VerticalContentAlignment=Center`) and `9,6` for multiline; focus-visible padding drops by 1 on each side as the border goes to 2 px. Caret `TextPrimary`. `SelectionBrush` `AccentPurple`, `SelectionForegroundBrush` `AccentPurpleOn` (that pair is already a tested text pair). Placeholder `TextSecondary` (the `PART_Watermark` override in `App.axaml` stays, written against `TextControlPlaceholderForeground` instead).

| State | Background | Border | Other |
|---|---|---|---|
| Rest | `SurfaceRaised` | `BorderStrong` 1 | |
| Hover | `SurfaceRaised` | `TextSecondary` 1 | |
| Focus (any input) | `SurfaceRaised` | `AccentPurple` 2 on `:focus-visible` (asserted by tests); on pointer focus the border is `AccentPurple` 1 | |
| Disabled | `Surface` | `Border` 1 | `Opacity` 0.5 |
| Invalid | `SurfaceRaised` | `StatusError` 1 (2 when focused), plus an adjacent error text with a glyph | class `invalid` NEW |
| Read-only | `Surface` | `Border` | text `TextPrimary` |
| Mono variant | same | same | class `mono` NEW: `FontFamily=OmpOS.Font.Mono`, size `Mono` 12 (API keys, URLs, ids) |
| Search variant | same, with an inner-left 14 px magnifier glyph (`TextSecondary`) | | class `search` NEW (the rail filter, model search, settings provider search) |

### 3.5 ComboBox

Same box as TextBox: height 32, radius 6, rest border `BorderStrong`, hover `TextSecondary`, focus `AccentPurple` 2, chevron glyph `TextSecondary` 12 px. Drop-down: `SurfaceRaised`, 1 px `Border`, radius 10, `Shadow.Popup`, padding 4. Items: height 28, padding `8,0`, radius 6, hover `Hover`, selected `Selected` plus a 14 px check glyph (`AccentPurple` stroke 2) at the right, so selection is not colour alone.

### 3.6 ToggleButton: default, segmented, nav

| Class | Geometry | Rest | Hover | Checked | Pressed | Disabled | Focus-visible |
|---|---|---|---|---|---|---|---|
| default ToggleButton | as Secondary button | as Secondary | as Secondary | bg `Selected`, border `BorderStrong`, weight 500, fg `TextPrimary` | bg `SurfaceActive` | opacity 0.5 | `AccentPurple` 2 (asserted for `ConventionsToggle`) |
| `segment` NEW (Sessions/Tools in the rail; System/Light/Dark in Settings) | Pair inside a `Border` track: track bg `Hover`, radius 8, padding 2, height 28; each segment height 24, radius 6, padding `10,0`, `HorizontalAlignment=Stretch` so the two halves are equal | bg transparent, fg `TextSecondary`, weight 400 | fg `TextPrimary` | bg `SurfaceRaised`, 1 px `Border`, fg `TextPrimary`, weight 500 (checked is shown by fill, weight and the accessible toggle state) | bg `SurfaceActive` | opacity 0.5 | `AccentPurple` 2, drawn on the segment |
| `nav` NEW (Tools view rows, Settings navigation) | Left-aligned row, height 28, padding `10,0`, radius `0,6,6,0` | bg transparent, fg `TextSecondary` | bg `Hover`, fg `TextPrimary` | bg `Selected`, fg `TextPrimary`, weight 500, left bar 2 px `AccentPurple` via `BorderThickness 2,0,0,0` | bg `SurfaceActive` | opacity 0.5 | `AccentPurple` 2 on all four sides |

The `nav` radius is `0,6,6,0` so the 2 px left bar stays a straight line, as in the existing selected row.

### 3.7 TabControl and the top tab strip

The top-level tabs are a `TabStrip` of `TabStripItem` (Agents, Kanban). Style class `top-tabs` NEW on that `TabStrip`.

| Part | Spec |
|---|---|
| Strip | Horizontal, items spaced 4, `Margin` 0 |
| Item | Height 28 (`ControlHeightSm`), padding `12,0`, radius 6, text `Label` 13 weight 500 |
| Rest | fg `TextSecondary`, bg transparent |
| Hover | fg `TextPrimary`, bg `Hover` |
| Selected | fg `TextPrimary`, 2 px `AccentPurple` pipe along the bottom edge (Fluent `TabItemHeaderSelectedPipeFill`, `TabStripItemPipeThickness` 2) |
| Pressed | bg `SurfaceActive` |
| Disabled | opacity 0.5 |
| Focus-visible | `AccentPurple` 2 around the item |

Generic `TabControl` (used by none of the OmpOS views today) follows the same item style.

### 3.8 ListBox and ListBoxItem rows

Generic rows (tool lists, model list, MCP, hooks, rules):

| State | Spec |
|---|---|
| Geometry | `MinHeight` `ControlHeightSm` 28, padding `8,4`, radius 6, margin `0,1`, list background transparent, `BorderThickness` 0 |
| Rest | fg `TextPrimary`, bg transparent |
| Hover | bg `Hover` |
| Selected | bg `Selected`, 2 px `AccentPurple` left bar (`BorderThickness 2,0,0,0`, `CornerRadius 0,6,6,0`) |
| Pressed | bg `SurfaceActive` |
| Disabled | opacity 0.5 |
| Focus-visible | item: `AccentPurple` 2; list: `AccentPurple` 2 (asserted); `FocusAdorner` null |

The agent rail row is a specialisation (section 4.3): `ListBoxItem` inside `AgentRailView` sets `MinHeight` `SessionRowMinHeight` 48 and `Padding` `8,6`.

### 3.9 Chips, pills, badges, keyboard hints

| Class | Shape | Spec |
|---|---|---|
| `Border.chip` (exists) | Pill | Height `ChipHeight` 20, radius `Pill`, bg transparent, border 1 `Border`, padding `8,0`; text `Small` 12 weight 500 in the tone colour; glyph 14. The status words and glyph stay together (colour is never the only signal). |
| `Border.chip-flat` NEW | Pill without outline | Same, border 0, bg transparent. Used in rail rows and Kanban cards, where a repeated outline would be noise. |
| `Border.chip-count` NEW | Count pill | Min width 20, height 18, radius `Pill`, bg `Selected`, text `Small` `TextSecondary`. Kanban column counts. |
| `Border.kbd` NEW | Keyboard hint | Radius 4, 1 px `Border`, padding `4,1`, bg `Hover`, text Space Mono `Small` 12 `TextSecondary`. |
| `Border.badge-diff` NEW | Added/removed counts | No fill; `+n` in `StatusSuccess`, `-n` in `StatusError`, Space Mono 12. The signs make colour redundant. |
| Status dot | not used | OmpOS keeps glyph shapes from the 46 Glyph Table; no plain coloured dots. |

### 3.10 Scrollbars

`ScrollBarSize` 10. At rest the thumb is 6 px wide (`Size.ScrollBarThumb`), `BorderStrong`, radius 3; on hover it is `TextSecondary`, on press `TextPrimary`. Track transparent; track hover `Hover`. No arrow buttons. `AllowAutoHide` stays on (Fluent default). Focus: the existing `ScrollViewer:focus-visible` style (2 px `AccentPurple`) stays because the transcript scroller is a tab stop (asserted by `KeyboardFocusTests`).

### 3.11 Tooltips, flyouts, menus, popups

| Control | Spec |
|---|---|
| `ToolTip` | bg `SurfaceRaised`, 1 px `Border`, radius 6, padding `8,4`, text `Small` 12 `TextPrimary`, max width 360, `Shadow.Popup`. Shows after 400 ms (`ToolTip.ShowDelay`). Keyboard hints inside use `Border.kbd`. |
| `Flyout` / `FlyoutPresenter` | bg `SurfaceRaised`, 1 px `Border`, radius 12 (`OverlayCornerRadius` 10 for menus), padding 12, `Shadow.Popup`, no animation |
| `ContextMenu` / `MenuFlyout` | Same container, radius 10, padding 4. Item height 28, padding `8,4`, radius 6, hover `Hover`, pressed `SurfaceActive`, accelerator text `TextSecondary` Space Mono 12, separator 1 px `Border` with margin `4,4`. Destructive item: fg `StatusError`. |
| Popup placement | Anchor 4 px from the source, clamp to the window. Flyouts open upward from the composer (`TopEdgeAlignedLeft`), as the model picker and pipeline strip already do. |

### 3.12 Switch, checkbox, radio

* `ToggleSwitch`: track 36 x 20, radius 10. Off: transparent fill, 1.5 px `BorderStrong` outline, knob `TextSecondary` 12 px. On: `AccentPurple` fill, knob `AccentPurpleOn`. The on/off text label sits to the left (`OffContent`/`OnContent` words from copy constants) so state is not colour alone. Focus-visible: 2 px `AccentPurple` around the track. Disabled opacity 0.5.
* `CheckBox`: 16 px box, radius 4, outline `BorderStrong`, checked fill `AccentPurple` with an `AccentPurpleOn` check.
* `RadioButton`: 16 px ring `BorderStrong`, checked dot `AccentPurple`. Used for the Theme and Motion choices when a description line is needed; the Theme choice uses the `segment` look through `RadioButton.segment` NEW (same visuals as 3.6, keyed to `IsChecked`).

### 3.13 Focus-visible rule (applies to every control)

* Keyboard focus draws a 2 px border in `AccentPurple` on the control itself (primary buttons use `TextPrimary`). `FocusAdorner` is null. This is what the focus tests assert.
* The outer size never changes: the control reserves the 2 px in its padding. Pattern: rest `BorderThickness=1` and padding P; focus-visible `BorderThickness=2` and padding P minus 1 on each side. Where rest already has a transparent 2 px border (existing `chip-button`, `strip`, `kanban-card` frame), nothing changes.
* The composer's ring is drawn by the `PromptInput` `TextBox` itself (it is the card; see 4.6), so the test that tabs to `Prompt` and expects an `AccentPurple` 2 px border keeps passing.
* Pointer focus shows the same border at 1 px on text inputs only (so a click gives feedback), never on buttons.

### 3.14 Class inventory (new or kept, by selector)

`Button`: none, `primary`, `ghost`, `icon`, `round`, `sm`, `destructive`, `skip-link`, `chip-button`, `strip`. `ToggleButton`: none, `segment`, `nav`. `RadioButton`: none, `segment`. `TextBox`: none, `mono`, `search`, `invalid`. `TextBlock`: `muted`, `error` (exist), `small` NEW (Small 12), `strong` NEW (weight 500), `heading` NEW (Heading 16 / 600), `display` NEW (Anybody Display 24 / 600), `display-sm` NEW (20), `mono` NEW, `tone-primary|secondary|success|warning|error` and `Path.tone-*` (exist), `Path.running-pulse` (exists). `Border`: `chip`, `chip-flat`, `chip-count`, `kbd`, `card` NEW, `card-sunken` NEW, `banner` NEW, `popup` NEW, `kanban-column`, `kanban-card` (exist), `skeleton` (exists). `TabStrip.top-tabs` NEW.

`TextBlock.display` carries the font family token and a hard floor: `FontSize` 24, `FontWeight` SemiBold; `display-sm` 20. There is no `display` class below 20 px by construction, and a markup test greps `FontFamily="{DynamicResource OmpOS.Font.Display}"` in `Views/**` and asserts the same element's `FontSize` is not below 20 and the element's `FontWeight` is not Thin or Light (see change 14 acceptance).

---

## 4. Layout per view

All measurements in device-independent pixels. The default window is 1280 x 800 (`MinWidth` 960, `MinHeight` 640, unchanged).

### 4.1 Window layout at 1280 x 800, Dark

```
+----------------------------------------------------------------------------------------------+
| TOP BAR 40  [inset] ompos        [ Agents | Kanban ]                                          |  SurfaceSunken, 1 px Border below
+------------------+-------------------------------------------------+-------------------------+
| RAIL 280         | THREAD (Surface)                                                           |
| SurfaceSunken    | header 40: glyph Title ............ [steering] [Close]                      |
| [Sessions|Tools] | pipeline strip 28: (glyph Ultrathink) (glyph Swarm 3/5) ... Latest: ...    |
| [+ New agent  ^N]|--------------------------------------------------------------------------|
| [> New omp term ]|        content column max 760, centred                                     |
| [ Filter agents ]|        [ user card ]                                                        |
| Needs you        |        Thought briefly                                                     |
|  o title    2 m  |        prose 14/21                                                         |
|    Waiting...    |        Explored 1 file                                                     |
| Running          |        [ diff card  CheckpointsDemo.tsx +1 -1 ]                            |
|  o title    now  |                                                                            |
| ...              |        [ composer card: input ...................  ]                     |
| footer 40 [gear] |        [ mode chip  model chip ........... (send) ]                        |
+------------------+--------------------------------------------------------------------------+
```

Widths: below 1200 px the rail is 240; from 1200 px it is 280 (existing `wide` class). The thread keeps at least 400. Below 720 px window height nothing in the layout changes (the hint is already a tooltip, 4.6).

### 4.2 Window chrome, top bar, skip link

**Top bar** (`DockPanel Top` row, `Border x:Name="TopBar"` NEW wrapping the existing `TopTabs`):

* Height `TitleBarHeight` 40, bg `SurfaceSunken`, 1 px `Border` bottom. Layout: `Grid ColumnDefinitions="Auto,Auto,*,Auto,*,Auto"`: left inset, workspace label, spacer, tabs, spacer, right inset.
* Workspace label: folder glyph 14 (`TextSecondary`) then the workspace folder name, `Label` 13 weight 500, `TextPrimary`, max width 220, ellipsis. **[Source open, O3]**: the name comes from the workspace root if the head exposes one; otherwise it shows the product name.
* Tabs: `TopTabs` as 3.7, centred. `TopTabs.IsVisible` logic (only with a Kanban board) is unchanged; when it is hidden the strip column collapses and the workspace label stays.
* Insets (custom title bar variant, see O2): macOS left 78 (traffic lights); Windows right 138 (caption buttons) **[INFERENCE: read from `Window.WindowDecorationMargin` at run time instead of hard-coding; the property was not opened in the Avalonia 12.1.4 source]**. Without extended client area (Linux, fallback) both insets are 8 and the native title bar sits above the top bar.
* Custom title bar variant, for `MainWindow.axaml`: `ExtendClientAreaToDecorationsHint="True"`, `ExtendClientAreaChromeHints="PreferSystemChrome"`, `ExtendClientAreaTitleBarHeightHint="40"` on macOS and Windows only (set in code from `OperatingSystem.IsMacOS() || IsWindows()` so Linux keeps its native frame). The top bar background handles `PointerPressed` (left button) with `BeginMoveDrag(e)` and double-click toggles maximise, skipping presses that land on the tabs or the workspace label's interactive children. **[INFERENCE]**: Avalonia may already drag from the extended area; verify per platform before adding the handler.
* Window title text stays `OmpOS` (`MainWindow.axaml`); it is not drawn in the bar.

**Skip link** (R2). `SkipLink` stays the first focusable element and keeps its `x:Name`, `Content`, `AutomationProperties.Name` and `Click` handler. It leaves the `DockPanel` flow:

```xml
<Panel>                                   <!-- new root; XAML order keeps SkipLink first in the tab order -->
  <Button x:Name="SkipLink" Classes="skip-link" Content="Skip to conversation"
          AutomationProperties.Name="Skip to conversation" Click="OnSkipLinkClick" />
  <DockPanel> ...existing content, without the SkipLink... </DockPanel>
</Panel>
```

```xml
<Style Selector="Button.skip-link">
  <Setter Property="HorizontalAlignment" Value="Left" />
  <Setter Property="VerticalAlignment" Value="Top" />
  <Setter Property="Margin" Value="8" />
  <Setter Property="ZIndex" Value="100" />
  <Setter Property="Opacity" Value="0" />
  <Setter Property="IsHitTestVisible" Value="False" />
  <Setter Property="Background" Value="{DynamicResource OmpOS.Color.SurfaceRaised}" />
  <Setter Property="BorderBrush" Value="{DynamicResource OmpOS.Color.BorderStrong}" />
</Style>
<Style Selector="Button.skip-link:focus">
  <Setter Property="Opacity" Value="1" />
  <Setter Property="IsHitTestVisible" Value="True" />
</Style>
```

`IsVisible` stays true, so assistive technology still lists it. It appears over the top bar's left corner while focused, with the standard focus border. Acceptance: a headless test reads `Opacity` 0 at rest; after `SkipLink.Focus()` it reads 1; screenshots 01 to 05 show no box at rest; `FocusVisibleToggleTests` still tabs from the skip link.

### 4.3 Agents rail

`Border x:Name="RailRegion"`: bg `SurfaceSunken`, padding `8,8,8,0`, 1 px `Border` on the right edge, width 240 (280 when `wide`). Content order is unchanged (it is the tab order): view toggles, then New agent, New omp terminal, Filter, the list; the footer follows the list.

| Element | Spec |
|---|---|
| View toggles | `segment` pair `SessionsViewToggle` and `ToolsViewToggle`, full width, height 28, margin `0,0,0,8` |
| `NewAgentButton` | `primary`, full width, height 32. Content: label from `AgentPanelCopy.NewAgent`, left aligned, plus a trailing `Border.kbd` showing the Ctrl+N gesture text. The `kbd` text is built from the `KeyGesture` (`new KeyGesture(Key.N, KeyModifiers.Control).ToString()`), not a literal. |
| `NewTerminalButton` | `ghost`, full width, height 28, left aligned, 14 px terminal glyph, trailing `kbd` (Ctrl+Shift+T). |
| `AgentFilter` | `TextBox.search`, height 28, margin `0,0,0,8` |
| `StatePanel` | Heading `Label` 13/500 `TextPrimary`, body `Small` 12 `TextSecondary`; padding `8,12`. |
| Group header | `Small` 12 weight 500 `TextSecondary`, height 24, margin `0,8,0,0`, sentence case; unchanged data and order (`Needs you`, `Running`, `Lost`, `Idle`, `Finished`). |
| Row | See below. |
| Footer | Row of height 40 pinned to the bottom (a `DockPanel Bottom` item after the list in XAML so tab order ends there), 1 px `Border` above, padding `0,0,0,0`; right-aligned `icon` button, accessible name `Settings` (new copy constant), opens the settings window; focus returns to it on close. **[O4: depends on the settings window being wired]**. |

Row anatomy (`AgentRows` item template), `MinHeight` 48, padding `8,6`, radius `Control` 6, margin `0,1`:

```
[glyph 16]  Title (Label 13, 500; 600 when selected)                         [time  Small 12, TextSecondary]
            Status words (Small 12, tone colour) · long-running · task tag (Mono 12)   [Terminal tag chip-flat]
```

* Glyph: the 46 Glyph Table entry for the row status, 16 px, stroke by tone; `AccessibilityView=Raw` (unchanged).
* Title: one line, ellipsis. Time: right aligned on line 1; the quiet-for text replaces it in `StatusWarning` as today. Second line: status words in the tone colour (`tone-*` classes exist), `Long-running` in `TextSecondary`, then the task tag in Space Mono 12 `TextSecondary`. The `Terminal` tag becomes `chip-flat` Mono 12 at the end of line 2.
* The status chip border is dropped inside rows (words plus glyph carry the meaning); the chip outline stays in the thread header and Kanban cards.
* Hover `Hover`; selected `Selected` plus the 2 px `AccentPurple` left bar, title weight 600; pressed `SurfaceActive`; focus-visible 2 px `AccentPurple`.
* Accessible name composition is unchanged (`{title}, {status}, {last activity}...`).

Tools view (`ToolsViewScroll`): group labels (`TextBlock.tool-group`) become `Small` 12 weight 500 `TextSecondary`, margin `8,12,8,4`; toggles become `ToggleButton.nav`, height 28, `Margin 0,1`, left aligned. Tab order unchanged. The `Margin` and `MinHeight=TargetMin` attributes on each toggle are removed (the class supplies them).

### 4.4 Thread region

`ThreadRegion` bg `Surface`. Row order is unchanged (12 rows). The transcript column and the composer share one centred column of `ThreadContentMaxWidth` 760 with 16 px side padding, so prose, cards and composer align.

### 4.5 Thread header, pipeline strip, notice, banners

**Thread header** (`ThreadHeaderView`, `HeaderFrame`): height 40 (grows only when the optional second line is visible), bg `Surface`, padding `16,0`, 1 px `Border` below. Line 1: `TitleText` (`Label` 13 weight 600, one line, ellipsis) then the `StatusChipView` (flat chip, glyph 14 plus words) then flexible space then `SteeringBadge` (chip-flat, info glyph plus `SteeringBadgeLabel`, `Small` 12) then `CloseButton` as `icon` 28 x 28 with the existing visible word replaced by an X glyph; its `AutomationProperties.Name` and `x:Name` are unchanged and a tooltip carries the visible word. `ResumeButton`, `ReopenButton` stay `primary` with the `sm` class (28). `TaskChipButton` stays a `chip-button` wrapping a `chip`. `GraphChipView` is a chip with Space Mono 12 text. When any of Resume, Reopen, TaskChip or GraphChip is visible they wrap to a second line of 28 px under the title (the existing `WrapPanel`); otherwise line 2 collapses.

**Pipeline strip** (`PipelineStripView`, the "plugin connection row"): one compact line of height 28 (`PipelineStripHeight`), bg `Surface`, 1 px `Border` below, padding `16,0`. `PipelineStrip` stays one `Button` (one tab stop, one accessible name from the model) with class `strip`: transparent bg, no border at rest, `HorizontalAlignment=Left`, height 28, padding `6,0`, radius 6; hover `Hover`; focus-visible 2 px `AccentPurple`. Its pills: glyph 14 (`IconSizeSm`) plus text `Small` 12 weight 500, 12 px apart, `NoWrap`. The strip is allowed to wrap onto a second line at narrow widths as today (the line then grows to 52; `MinHeight` only). The `Announcement` live-region `TextBlock` moves into the same row, to the right of the button: `Grid ColumnDefinitions="Auto,*"`, `Small` 12 `TextSecondary`, one line, ellipsis, right aligned, `Latest: ...` text as bound today. Detail flyout (`PipelineTabView`): unchanged width 360 and max height 360, restyled by 3.11.

**Panel notice** (`PanelNoticeView`): `Border.banner` NEW: bg `SurfaceRaised`, 1 px `Border`, radius 8, 3 px left bar in the tone colour (`StatusWarning` for degraded), margin `16,8`, padding `10,8`. Glyph 14, text `Label` 13 wrapping, buttons `Show pipeline` (secondary `sm`), `Resume` (primary `sm`), `Dismiss` (ghost `sm`) right aligned. Height about 40.

**Error banner** (`ErrorBanner`) and **closed notice**: same `banner`, bg `DiffRemoved` for errors with the `StatusError` bar and the Cross glyph; text stays `TextPrimary` (4.5:1 or more over `DiffRemoved`, table above). `StatusLine` (muted, polite): `Small` 12 `TextSecondary`, padding `16,4`.

**Tool header** (`ToolHeader`, when a tool is open): height 36, bg `SurfaceSunken`, 1 px `Border` below, title `Label` 13/600, `CloseToolButton` as `ghost sm` with the existing text.

### 4.6 Transcript and message anatomy

Vertical rhythm: 16 px between turns, 8 px between blocks of one turn. All blocks sit in the 760 px column.

| Block | Container | Contents and styling |
|---|---|---|
| User message (`UserMessageView`) | `Border.card`: bg `SurfaceRaised`, 1 px `Border`, radius 10, padding `12,10`, `Shadow.Card` | `Body` 14/21, `TextPrimary`. Full column width (not a right-aligned bubble). |
| Assistant prose (`MarkdownBlockView`, `MarkdownAstBuilder`) | none (sits on `Surface`) | `Body` 14/21 `TextPrimary`, paragraph gap 8, list indent 20, bullet glyph `TextSecondary`; H1 18/24, H2 16/22, H3 14/21, all 600; inline code Mono 12 on `Selected`; links `AccentPurple` underline on hover and focus; block quote: 2 px `BorderStrong` left bar, text `TextSecondary`; table: 1 px `Border` rules, header row `SurfaceSunken` weight 600, cell padding `8,4`. |
| Code block (`CodeBlockView`) | `Border.card-sunken`: bg `SurfaceSunken`, 1 px `Border`, radius 8 | Header row 28: language label (`Small` 12 `TextSecondary`, left), copy `icon sm` (right, accessible name from copy constants); body padding `12,8`, Space Mono 12/18, horizontal scroll, no wrap. |
| Tool call (`ToolCallBlockView`) | none; one row, height 28, padding `4,0`, radius 6, hover `Hover` | Chevron 12 (`TextSecondary`), glyph 14, then the tool phrase (`Label` 13, `TextSecondary`; the object in Space Mono 12 when it is a path or id), status text right (`Small` 12). While running: the running glyph pulses (existing `running-pulse`). Expanding reveals the tool result block. This replaces the semibold name on a filled band. |
| Tool result (`ToolResultBlockView`) | `Border.card-sunken`, margin `22,0,0,0` | Space Mono 12/18, max height 240, scrolls. |
| Thinking | Same as tool call row, text `Thought briefly` etc. from the model | |
| File edit (`FileEditBlockView`, `DiffPlaceholderView`) | `Border.card`: bg `SurfaceRaised`, 1 px `Border`, radius 8, clip | Header 32: file glyph 14, path `Label` 13 weight 500 (middle ellipsis), `Border.badge-diff` counts, operation text `Small` 12 `TextSecondary` right. Body bg `SurfaceSunken`: lines Space Mono 12/18; gutter 40 px of line numbers (`TextSecondary`, right aligned); sign column 16 px with `+` or `-` (`StatusSuccess` or `StatusError`) so colour is not the only signal; added line bg `DiffAdded`, removed line bg `DiffRemoved`; context lines no tint. Collapsed after 8 lines with a `ghost sm` "Show n more lines" row. |
| Plan (`PlanBlockView`) | `Border.card` | Title `Heading` 16/600, markdown body, footer `primary sm` action as built today. |
| Permission request (`PermissionBlockView`, `ToolApprovalRowView`) | `Border.card` with a 1 px `AccentPurple` border (as `ToolApprovalRowView` has today) | Warning glyph 14 (`StatusWarning`), prompt `Label` 13/600, arguments in an inset `card-sunken` Mono 12, buttons right aligned: `Approve` `primary sm`, `Deny` secondary `sm` (their `x:Name` and accessible names are unchanged); decision text `Small` 12 polite. |
| Error (`ErrorBlockView`) | `Border.banner` with `StatusError` bar and `DiffRemoved` bg | Cross glyph, text `Body`. |
| Unknown (`UnknownBlockView`) | `Border.card-sunken` | Mono 12, `TextSecondary`. |
| A2UI surface (`A2uiSurfaceView`) | `Border.card` | Controls built from the library; labels `Label`, values `Body`. |
| "Worked for ..." and "Review +n -n" | one `Small` 12 `TextSecondary` line; `Review` pill is a secondary `sm` button | Only where the model supplies the text; no new data. |

Transcript scroller: `VerticalScrollBarVisibility` Auto, scroll bar per 3.10. A "scroll to bottom" `round` secondary button (28) appears bottom-right of the column when content overflows **only if the control already exists**; this spec adds no behaviour.

### 4.7 Composer

The composer is one rounded card. The card is the `PromptInput` `TextBox` itself, so its focus ring and its `x:Name` stay what the tests expect, with the footer overlaid inside the same box:

```xml
<Border x:Name="ComposerFrame" Margin="16,8,16,16" MaxWidth="{DynamicResource OmpOS.Size.ThreadContentMaxWidth}">
  <Panel>
    <TextBox x:Name="PromptInput" Classes="composer" AcceptsReturn="True" TextWrapping="Wrap"
             MinHeight="{DynamicResource OmpOS.Size.ComposerMinHeight}" MaxHeight="{DynamicResource OmpOS.Size.ComposerMaxHeight}"
             ... existing placeholder, AutomationProperties.* ... />
    <Grid x:Name="ComposerFooter" VerticalAlignment="Bottom" Margin="8,0,8,8" ColumnDefinitions="Auto,Auto,*,Auto,Auto,Auto">
      <!-- mode chip, ModelPicker chip, spacer, PendingLabel-capable area, FollowUpButton, SendButton, StopButton -->
    </Grid>
  </Panel>
</Border>
```

`TextBox.composer` NEW: radius 12 (`Radius.Lg`), bg `SurfaceRaised`, border 1 `Border` (rest), hover `BorderStrong`, focus-visible `AccentPurple` 2 (padding compensates by 1), padding `12,10,12,44` (44 reserves the footer), `Shadow.Card`, font `Body` 14. The footer sits over the bottom padding: it does not intercept typing because it only occupies the bottom 36 px and its empty column is `IsHitTestVisible=False`.

| Footer item | Spec |
|---|---|
| Mode chip (only when the head supplies modes; Cursor's "Agent" or "Plan") | `ghost sm` 28, label `Label` 13, chevron 12 |
| Model chip (`ModelPickerView.ChipButton`, being added by the model-control work) | `ghost sm` 28, text Space Mono 12 `TextPrimary`, chevron 12 `TextSecondary`, max width 200, ellipsis; flyout per 3.11 (`SearchBox` `TextBox.search`; list rows as generic rows 28; heading rows `Small` 12/500 `TextSecondary`; current model shows a 14 px check) |
| `FollowUpButton` (visible only while a run is active) | secondary `sm` |
| `SendButton` | `round primary`, 28 x 28, send glyph fill `AccentPurpleOn` |
| `StopButton` | `round`, 28 x 28, 12 px square glyph fill `TextPrimary` |
| `PendingLabel` | `Small` 12 `TextSecondary`, inside the footer's free column |
| `HintLine` | `IsVisible=false`. Its text becomes the `ToolTip.Tip` of `PromptInput` and of `SendButton`; the text also stays in `AutomationProperties.HelpText` (exists). Keyboard hints inside the tooltip render as `Border.kbd`. The shell's `HintVisible` logic keeps working and now defaults to false; the 720 px rule is no longer needed. |
| `Suggestions` (inline completion box) | Appears above the card as a `Border.popup` list, rows 28 |

Tab order is unchanged except where the model chip sits (between prompt and send, as the model-control work places it). Min height 72, grows with text to 220, then the text scrolls.

`TerminalSteerView` (the composer of an omp terminal) uses the same card: `MessageInput` is `TextBox.composer` with a footer holding `SendNowButton` (`primary sm`, label from `AgentPanelCopy.SendNow`) and `QueueButton` (secondary `sm`), right aligned. The note under it is the same tooltip rule.

### 4.8 Panel states (empty, loading, error)

`PanelStateView`, `KanbanBoardView` states and the rail `StatePanel` share one recipe: a centred column, max width 420, vertical centre in the region, 12 px gaps.

| State | Content |
|---|---|
| Empty | Heading in **Anybody `Display` 24 / 600** (`TextBlock.display`), body `Body` 14 `TextSecondary` wrapping at 380, then the `primary` button (32 high). The screenshot 01 heading "Start your first agent" and the Kanban "No sessions yet" both use `display`. |
| Loading | Heading `Heading` 16/600, body muted, then skeleton bars (`Border.skeleton`: bg `SurfaceActive`, radius 4, height 12, pulse only under full motion). Never a spinner (existing rule). |
| Error | `Border.banner`: Cross glyph, text `Body`, `Retry` secondary button below. Assertive live region as today. |
| No match (rail) | Heading `Label` 13/500 and body `Small` 12 muted. |

### 4.9 omp terminal view (`OmpTerminalView`)

* Frame: the whole view sits in `Border` radius 8, 1 px `Border`, margin `0,0,0,8`, clip; body bg `SurfaceSunken`.
* Status bar (28 high, bg `SurfaceRaised`, 1 px `Border` below, padding `10,0`): `StatusText` left (`Small` 12, glyph 14 by status), then `RestartButton` (`ghost sm`, word kept) and `CloseButton` (`icon sm` X glyph; accessible name `Close omp terminal` unchanged; tooltip shows `Close`). `StatusText` stays a polite live region.
* Output: `TerminalControl` `FontFamily=OmpOS.Font.Mono`, `FontSize` `MonoTerminal` 13, bg `SurfaceSunken`, fg `TextPrimary`, margin 8, palette per 2.7.
* Unavailable panel: `ScrollViewer` padding 16, `SelectableTextBlock` Space Mono 12 `TextPrimary`, preceded by a Warning glyph and a `Label` 13/600 heading.
* Steer composer below: section 4.7 (`TerminalSteerView`).

### 4.10 Kanban tab

`KanbanBoardView`: padding 16. The board scrolls horizontally; columns are `Border.kanban-column` of width `KanbanColumnWidth` 280 (min 240), 8 px apart.

| Part | Spec |
|---|---|
| Column | bg `SurfaceSunken`, radius `Radius.Column` 10, padding 8, border 2 transparent (kept; drop highlight and focus use it). Drop target (`drop-target`): border `AccentPurple` 2 and bg `Hover`. |
| Column header | Row 28: column name `Label` 13 weight 600 `TextPrimary` in **DM Sans** (the Anybody header class `kanban-column-header` is changed to `FontFamily=OmpOS.Font.Family`, size `Label`, weight 600, no wrap), then `Border.chip-count` with the card count, then flexible space. Header text stays one `TextBlock` so the code-behind that builds it keeps working. |
| Empty column text | `Small` 12 `TextSecondary`, centred in a dashed `BorderStrong` 1 px outline for Backlog and Archived (the drop targets: "Drop a card here" from the existing copy), plain muted text elsewhere ("No tasks"). |
| Card | Outer `Border#Frame` keeps the transparent 2 px ring border (asserted by `KanbanBoardKeyboardTests`); inside it a new `Border.card` (bg `SurfaceRaised`, 1 px `Border`, radius 8, padding `10,8`, `Shadow.Card`). Hover: inner bg `Hover`. Focus-visible: `Frame` border `AccentPurple` 2 (existing). Margin `0,0,0,8`. |
| Card line 1 | `TitleText` `Label` 13 weight 600, wraps to two lines max (ellipsis), `KindTag` `chip` (outlined) right (`Chat`, `Terminal`). |
| Card line 2 | `ModelText`: model and provider in Space Mono 12 `TextSecondary`, one line, ellipsis. |
| Card line 3 | `StatusChip` (outlined chip, glyph plus words) then `ActivityText` `Small` 12 `TextSecondary`. Stale and disconnected words (`tone-warning`) each on their own `Small` 12 line with a Warning glyph. |
| Substrate row | One wrapping `Small` 12 `TextSecondary` block, Space Mono for ids: `Task ...`, `Graph ...`, `Lessons n`, `Lease ...` separated by 8 px gaps (the four `x:Name` fields stay as four `TextBlock`s in one `WrapPanel`; the rows stop being four stacked lines). Unavailable fields read `unavailable` exactly as today. |
| Steer button | `ghost sm` 28, trailing arrow glyph, left aligned under the Substrate block; `IsEnabled` as bound. Flyout per below. |
| Density | A card is about 112 px tall (title 18, model 16, chip row 20, Substrate 2 lines 32, steer 28, padding 16, gaps 6) against about 250 px today. |
| Drag affordance | Only cards in the two drop-accepting columns (Backlog, Archived) are draggable (D39-2). Those cards show a 12 px six-dot grip glyph in `TextSecondary` at the left of line 1, always visible, and `Cursor="Hand"`. Runtime-status cards have no grip and the default cursor. A dragged card is drawn at 85 % opacity with `Shadow.Popup`, and the target column shows the drop style above. |
| Steer flyout | `Flyout` per 3.11, width 320, padding 12: `SteerBox` `TextBox` multiline `MinHeight` 72; below it a right-aligned row `QueueButton` (secondary `sm`) and `SendNowButton` (`primary sm`) (XAML order unchanged: Send now then Queue; use `HorizontalAlignment=Right`); `SteerResult` `Small` 12 polite. |
| Error banner / empty / loading | As 4.5 and 4.8; skeleton columns use the same column style. |
| Live region | `LiveRegion` `Small` 12 `TextSecondary` at the bottom, padding `0,8,0,0`. |

### 4.11 Settings window

A second window (`Window`, class `SettingsWindow` NEW in `Views/Settings/`), opened from the rail footer gear and from Ctrl+, **[O4]**. `46-UI-SPEC.md` says "no second window" for Phase 46; the Settings window is the one explicit exception, and `SettingsWindowModel` already models a window with a close prompt. Same theme and tokens as the main window.

* Size 960 x 680, min 720 x 520, centred over the owner, owned (modal to nothing; the main window stays usable). `Title` `Settings`. Custom title bar follows the main window decision (O2).
* Two columns. **Navigation** (width `SettingsNavWidth` 220, bg `SurfaceSunken`, 1 px `Border` right, padding 8): a `nav` ToggleButton list, one entry per `SettingsSection` in the order `SettingsWindowModel` gives: Runtime, Models, General, Agents, Tab, Plugins, Rules, Skills, Commands. Header above it: `Settings` in `Heading` 16/600 with margin `8,8,8,12`. **Content** (bg `Surface`): a `ScrollViewer` holding one column of `SettingsContentMaxWidth` 720, padding `32,24`, centred.
* Page header: page title in **Anybody `display-sm` 20 / 600**, then a `Body` 14 `TextSecondary` description, margin below 24.
* Section: a `Heading` 16/600 label, margin `0,24,0,8`.
* Setting row (the unit of every form): `Grid ColumnDefinitions="*,Auto"`, min height 56, padding `0,12`, 1 px `Border` below. Left: label `Label` 13/500, help `Small` 12 `TextSecondary` wrapping at 480. Right: the control (switch, segmented, combo 220 wide, text box 240 wide, button). Rows stack without cards, as Cursor's settings do **[INFERENCE]**.
* Footer bar, only on pages with a draft (`EditableSettingsPage`): sticky bottom, 56 high, bg `SurfaceRaised`, 1 px `Border` above, padding `16,0`; left: the dirty note (`Small` 12), right: `Discard` (ghost) and `Save` (`primary`). Disabled while the draft equals the saved values. The close prompt (unsaved changes) is a small modal `Flyout`-style dialog, 400 wide, radius 12, `Shadow.Popup`, buttons `Save` (primary), `Discard` (danger), `Cancel` (secondary), in XAML order matching `SettingsClosePromptChoice`.
* **General page:** rows: Theme (`RadioButton.segment` three segments System, Light, Dark; changing it applies live as a draft preview and persists on Save), Motion (the three options as radio rows with their descriptions), Privacy statement (read-only paragraph, `Body` 14, no control).
* **Runtime, Agents, Tab pages:** setting rows with `ToggleSwitch` for booleans, `TextBox` (Mono for numbers) with unit text for limits, read-only status rows (`Small` 12) for Tab.
* **Plugins page:** four read-only rows (plugin name `Label` 13/500, status chip, version in Space Mono 12).
* **Rules, Skills, Commands pages:** list rows 40 high: name in Space Mono 12 `TextPrimary`, description `Small` 12 `TextSecondary`, scope chip-flat (`User`, `Workspace`).
* **Models page (provider settings):** top row: `TextBox.search` (filter, bound to `SearchText`) and an `Add custom provider` secondary button right aligned. Groups in model order (Local, Cloud, Coding plan, Custom, Built-in omp), each preceded by a `Small` 12/500 `TextSecondary` heading and a count chip. Then one **provider card** per provider.

Provider card (`Border.card`, radius 10, padding 16, 1 px `Border`, bg `SurfaceRaised`, gap 12, width fills the column):

```
[Name  Heading 16/600]  [group chip-flat Mono 12]                          [credential chip]   [Test] [Set API key]
[endpoint  Mono 12 TextSecondary]
[note  Small 12 TextSecondary, wraps]
-- model rows (generic rows 28): [selector Mono 12]            [Default chip]  or  [Use as default ghost sm]
[plan lines  Small 12]   [source: "Source ... retrieved ..." Small 12, link AccentPurple]
```

* Credential chip from `CredentialState`: Present (`StatusSuccess` check glyph, "Key set"), Absent (`StatusWarning` alert glyph, "No key"), Unknown (`TextSecondary` info glyph), NotRequired (`TextSecondary` ring glyph, "No key needed"); the words come from `ModelsCopy.StatusLine`, never a literal in the view.
* `Test` is secondary `sm`; while running it shows the existing text; its result is a chip in a polite live region by `ProviderTestStatus`: Reachable (`StatusSuccess`), NotRunning (`StatusWarning`), Unauthorized (`StatusError`), Failed (`StatusError`), NotTestable (`TextSecondary`).
* **API key flow** (`CanSetApiKey`, `ApiKeyDraft`): the action button reads `Set API key` or `Replace key` (`ApiKeyActionLabel`). Pressing it expands the editor inline inside the card, below the header, with a 1 px `Border` top rule: a `TextBox.mono` with `PasswordChar` `•`, 32 high, width fills; a reveal `icon sm` toggle (eye glyph, accessible name from copy, default hidden); `Save` (`primary sm`) and `Cancel` (`ghost sm`); a validation line under it (`Small` 12, `StatusError` glyph and text, `AutomationProperties.LiveSetting=Polite`). The saved key is never shown again: after saving the card shows only the credential chip. The editor never logs or echoes the value, and the field clears on cancel and on collapse. OAuth providers show `SignInHint` as `Small` 12 text instead of the editor.
* Custom provider rows (`CanEdit`, `CanRemove`): `Edit` (ghost `sm`) and `Remove` (`destructive sm`). `Remove` swaps the card's action row for an inline confirmation: `RemoveQuestion` text, `Remove` (danger), `Cancel` (secondary).
* **Custom provider form** (`BeginAddCustom`, `BeginEdit`; `CustomProviderDraft`): an inline card at the top of the list. Two columns at 720 or wider, one below. Labels above fields (`Small` 12/500), fields 32 high: Name (`TextBox`), Protocol (`ComboBox`), Base URL (`TextBox.mono`), Auth (`ComboBox`), capability checkboxes in a wrap row, optional key editor as above. Invalid fields use the `invalid` class with an error line; the footer bar is the page footer (`Save`, `Discard`). Dirty state shows `Small` 12 text and enables Save.
* Empty search result: the 4.8 no-match recipe. Incomplete data note (`IncompleteNote`): a `banner` with a Warning glyph above the list. Load failure (`LoadFailure`): error banner with `Retry`.

Settings acceptance (change 13): headless tests open the window with the models page in each `ModelsPageState`, assert every action button is reachable by Tab in XAML order, the API key editor `TextBox.PasswordChar` is set, the saved key text is empty after save, and screenshots in both themes show nav, a Local provider card, a Cloud card with the key editor open, and the custom form.

### 4.12 Other tool views

These open in the thread region under the tool header (4.5). They pick up the controls library; only these specifics change.

| View | Change |
|---|---|
| `DiffReviewView` | Hunk heading `Label` 13/600 with a `Border.badge-diff`; line rows Space Mono 12/18 through `OmpOS.Font.Mono` (the literal `Cascadia Code,Consolas,...` stack is removed); added lines bg `DiffAdded`, removed `DiffRemoved`, with a sign column (`+` or `-`); `Accept` is `primary sm`, `Reject` is secondary `sm`; `ResultBox` is `TextBox.mono`, `MaxHeight` 220 (unchanged). |
| `EditorView`, `InlineEditView`, `InlineCompletionBox` | Editor body bg `SurfaceSunken`, Space Mono 12; inline completion is a `Border.popup` list (3.11). |
| `WorkspaceTreeView`, `WorkspaceSearchView` | Generic rows (3.8), `TextBox.search`, match text in Mono 12. |
| `McpToolsView`, `HooksView`, `AgentsView`, `WorkspaceConventionsView`, `TranscriptExportView`, `TranscriptSearchView`, `SideChatListView`, `RunControlsView` | Setting-row and generic-row layouts from 4.11 and 3.8; buttons per 3.3; no new behaviour. |
| `ConfirmBarView` | `Border.banner` above the composer, one 40 px row: question `Label` 13, `primary sm` and secondary `sm` actions right aligned. |

---

## 5. Prioritised change list, with acceptance checks and landing order

The order keeps the tree green: each step lists the tests it must update in the same commit so `dotnet test` stays green at every step. Steps 1 to 4 change no layout; 5 onwards are view by view. Landing order is the numbering. Only step 6 needs the owner's decision O1; steps 1 to 5 and 7 onwards do not depend on it except where marked.

The **visual evidence set** for every view step: re-take screenshots 01 to 05 at 1280 x 800 in Dark (default) and Light, and 01 and 02 at 960 x 640, into `docs/runbooks/evidence/ui-parity-2026-10-26/after-dark/` and `.../after-light/`, using the same method that produced `before/` (its method is not recorded in this repository; record it in `evidence/.../README.md` when first reused).

| # | Priority | Change | Files | Acceptance check | Tests updated in the same commit |
|---|---|---|---|---|---|
| 1 | P0 | Static font instances and folder URIs (2.4) | `src/OmpOS.Native.Views/Assets/Fonts/**` (add 3 DM Sans, 1 Anybody, 1 Space Mono Bold, their OFL files; remove the two variable files), `Themes/OmpOSTokens.axaml` (three `FontFamily` values), `OmpOS.Native.Views.csproj` if the resource glob is per file | Headless: DM Sans 400 vs 600 widths differ; `OmpOS.Font.Display` at SemiBold resolves a weight-600 face. Screenshot 05: "No sessions yet" solid. | `Theme/FontManifest.txt`, `Theme/FontResourceTests.cs` (`FontFiles`, `Families`, `FamilyNotices`), `ThemeContrastTests.TheFontTokensNameTheBundledFaces` |
| 2 | P0 | New colour roles and values (2.1), new size, radius, spacing, shadow, weight tokens (2.5); `Body` 15 -> 14, `Heading` 18 -> 16, `Label` stays 13; `TextBlock` default size -> `Label` | `Themes/OmpOSTokens.axaml`, `apps/OmpOS.Native/App.axaml` (`TextBlock` style) | `ThemeContrastTests` pass with the classification of 2.3 (it is the contrast table in executable form); `PanelMarkupTests` token theory passes | `ThemeContrastTests` (2.3), `AgentPanel/Views/PanelMarkupTests` rows for `SessionRowMinHeight` 48, `ChipHeight` 20, `ThreadContentMaxWidth` 760. Do **not** change `TargetMin` yet. Interim look: views that used `SurfaceRaised` for bands render white until steps 5 to 11; this is accepted. |
| 3 | P0 | Fluent aliases and the control library (3.1 to 3.13); delete the superseded button, chip and focus chrome from `App.axaml` and `PanelStyles.axaml`; `FluentTheme DensityStyle="Compact"` | `Themes/OmpOSTokens.axaml` (aliases), new `Themes/OmpOSControls.axaml`, `apps/OmpOS.Native/App.axaml`, `Views/AgentPanel/PanelStyles.axaml`, `Views/Kanban/KanbanStyles.axaml` | New `ControlStyleTests` (3.2 acceptance) in both variants; existing focus tests still pass (2 px `AccentPurple`, `FocusAdorner` null); screenshots show styled buttons, toggles, inputs with no layout shift | none broken; add `ControlStyleTests` |
| 4 | P0 | Dark default, theme setting, live re-read (2.6) | `apps/OmpOS.Native/App.axaml` (`RequestedThemeVariant="Dark"`), `src/OmpOS.Presentation/Settings/GeneralPage.cs`, `GeneralSettingsValues.cs`, `IGeneralSettingsSource.cs`, `SettingsCopy.cs`, the head's source implementation, `Views/Blocks/BlockTokens.cs` and its callers, `Views/Terminal/OmpTerminalView.axaml.cs` | `ThemePreferenceTests` (default Dark; Light, System, Dark apply; persisted value survives restart in the source); markdown block and terminal recolour on switch | tests for `GeneralPage` extended |
| 5 | P1 | Skip link overlay, top bar, window chrome (4.2) | `MainView.axaml` (root `Panel`, `TopBar`), `MainWindow.axaml`, `MainWindow.axaml.cs` (platform chrome, drag), `Themes/OmpOSControls.axaml` (`skip-link`) | Skip link: opacity 0 at rest, 1 on focus, still first in tab order, still activates; screenshots 01 to 05 have no box; top bar 40 px | `ShellHarness`-based tests keep passing; add `SkipLinkVisibilityTests` |
| 6 | P1 | Density (2.5): `TargetMin` 44 -> 32; every view keeps its `MinHeight={DynamicResource OmpOS.Size.TargetMin}` so one value flips it | `Themes/OmpOSTokens.axaml`; tests only | Rail, header, composer and Kanban show 32 px controls; the WCAG 2.5.8 AA target size (24 x 24) holds for every interactive control (new `DensityTests`: every `Button`, `ToggleButton`, `TextBox`, `TabStripItem`, `ComboBox` in a shell and a Kanban harness has `Bounds` at least 24 x 24; every text button and input has `MinHeight` at least the `TargetMin` token) | **Owner decision O1 first.** Change the 26 assertions `ShouldBeGreaterThanOrEqualTo(44)` in 13 files to a named `MinimumTarget = 24` constant (WCAG 2.5.8 AA) in a shared test helper: `AgentPanel/Shell/AgentPanelTerminalSessionTests` (3), `AgentPanel/Shell/AgentPanelToolsTests` (2), `AgentPanel/Views/AgentRailViewTests` (2), `ComposerViewPanelTests` (3), `ConfirmBarViewTests` (2), `PanelStateViewTests` (1), `PipelineStripViewTests` (2), `PipelineTabViewTests` (1), `ThreadHeaderViewTests` (2), `Kanban/KanbanBoardKeyboardTests` (2), `KanbanBoardViewStateTests` (2), `KanbanSteerFlyoutTests` (2), `ModelPicker/ModelPickerViewTests` (2); retitle the doc comments that say 44 px. |
| 7 | P1 | Agents rail (4.3) | `MainView.axaml` (rail, tools view, footer), `Views/AgentPanel/AgentRailView.axaml`, `Views/AgentPanel/StatusChip.axaml` (flat variant), `Views/AgentPanel/AgentPanelViewText.cs` / `AgentPanelCopy` (new constants: settings button name, key hint wording is built from the gesture) | Screenshot 01 and 02 rail: 48 px two-line rows, group labels, segmented toggles; keyboard order unchanged (`KeyboardFocusTests`); row accessible name unchanged | `AgentRailViewTests` selectors by `x:Name` keep working |
| 8 | P1 | Thread header, pipeline strip, notice, banners, tool header, status line (4.5) | `Views/AgentPanel/ThreadHeaderView.axaml`, `PipelineStripView.axaml`, `PanelNoticeView.axaml`, `PanelStateView.axaml`, `MainView.axaml` (ErrorBanner, ClosedNotice, ToolHeader, StatusLine) | Screenshot 02: header 40 px, strip one 28 px line with the latest text on the right; no 4-band stack | `ThreadHeaderViewTests`, `PipelineStripViewTests` unchanged by name |
| 9 | P1 | Message blocks, markdown, diff card (4.6) | `Views/Blocks/*.axaml`, `Views/Blocks/MarkdownAstBuilder.cs`, `BlockControls.cs`, `BlockTokens.cs` | Block gallery screenshot (one of each block) in both themes; tool-call row 28 px; diff lines carry `+` or `-`; contrast pass is already in 2.2 | block tests keep their `x:Name` lookups |
| 10 | P1 | Composer (4.7), terminal steer, hint to tooltip | `Views/ComposerView.axaml`, `Views/ComposerView.axaml.cs` (hint default), `Views/ModelPickerView.axaml`, `Sessions/TerminalSteerView.axaml`, `MainView.axaml.cs` (`HintVisible` logic) | Screenshot 02: rounded card, round send and stop, model chip; `PromptInput` still shows the 2 px `AccentPurple` ring when tabbed to (`KeyboardFocusTests`); tooltip text equals `ComposerHint` | `ComposerViewPanelTests` hint assertions (visible above 720 px) change to tooltip assertions. **Coordinate with the model-control work (wave 3a) that edits `ComposerView.axaml` and adds the model chip.** |
| 11 | P1 | omp terminal view (4.9) | `Views/Terminal/OmpTerminalView.axaml`, `.axaml.cs` (palette), `Views/Terminal/TerminalPanelView.axaml` | Screenshot 03: framed terminal, status bar, steer composer card | terminal tests keep names |
| 12 | P2 | Kanban (4.10) | `Views/Kanban/KanbanStyles.axaml`, `KanbanBoardView.axaml`, `KanbanCardView.axaml`, the code-behind that builds column headers (class change only) | Screenshots 04 and 05: columns with counts, compact cards, grips only on Backlog and Archived cards; header in DM Sans 13/600; empty heading Anybody 24 | `KanbanBoard*Tests` keep their `Frame`, `SteerTrigger` lookups |
| 13 | P2 | Settings window (4.11): new views, General theme row, provider cards, API key flow | new `Views/Settings/SettingsWindow.axaml(.cs)`, `SettingsGeneralPage.axaml`, `SettingsModelsPage.axaml`, `ProviderCardView.axaml`, `CustomProviderFormView.axaml`, other page views; entry button in the rail footer; `ViewMapping.cs` if pages are mapped there | Section 4.11 acceptance; keyboard map in section 6 extended with Ctrl+, | new tests |
| 14 | P2 | Display headings and empty/error states (4.8); DiffReview mono token; markup guards | `Views/AgentPanel/PanelStateView.axaml`, `Views/Kanban/KanbanBoardView.axaml`, `Views/Diff/DiffReviewView.axaml`, `Themes/OmpOSControls.axaml` | New `TypographyMarkupTests`: every `OmpOS.Font.Display` use has `FontSize` >= 20 and weight SemiBold or heavier; no `FontFamily="Cascadia..."` or other literal family in `Views/**` (`ColourLiteralTests` already guards colour) | `ColourLiteralTests` unchanged |
| 15 | P2 | Visual evidence and regression pass | `docs/runbooks/evidence/ui-parity-2026-10-26/after-dark/`, `after-light/` | The 14-image set exists (5 screens at 1280 x 800 and 2 at 960 x 640, in each theme); `ThemeContrastTests`, `KeyboardFocusTests`, `AccessibilityNameTests` and `ReducedMotionTests` are green | none |

Reduced-motion check inside step 3: `ReducedMotionTests` must still prove that no control in the library carries a transition under `Window.reduced-motion`; the 100 ms `BrushTransition` of 2.5 is added to the list of selectors that class clears (`Button`, `ToggleButton`, `ListBoxItem`, `TabStripItem`, `TextBox`, `ComboBox`, `ScrollViewer`).

---

## 6. What must NOT change

1. **`x:Name` values** (tests and code-behind find controls by them). At minimum: `MainView`: `SkipLink`, `TopTabs`, `AgentsTab`, `KanbanTab`, `RegionGrid`, `RailRegion`, `ThreadRegion`, `SessionsViewToggle`, `ToolsViewToggle`, `RailHost`, `ToolsViewScroll`, `ToolsViewHost`, `AgentsToggle`, `SideChatsToggle`, `SuggestionsToggle`, `EditorToggle`, `ExplorerToggle`, `TerminalToggle`, `DiffToggle`, `InlineEditToggle`, `ConventionsToggle`, `WorkspaceSearchToggle`, `McpToggle`, `ExportToggle`, `ThreadHeaderHost`, `PipelineHost`, `PanelStateHost`, `ClosedNotice`, `ClosedHeading`, `ClosedBody`, `ToolHeader`, `CloseToolButton`, `ToolTitle`, `TranscriptRegion`, `ErrorBanner`, `TranscriptHost`, `AgentsHost`, `ToolHost`, `SearchHost`, `StatusLine`, `RunControlsHost`, `ConfirmBarHost`, `ComposerHost`, `SideChatHost`, `KanbanHost`. `AgentRailView`: `NewAgentButton`, `NewTerminalButton`, `AgentFilter`, `StatePanel`, `StateHeading`, `StateBody`, `AgentRows`, `RowMenu`, `ResumeItem`, `ReopenItem`, `CloseItem`. `ThreadHeaderView`: `HeaderFrame`, `TitleText`, `StatusChipView`, `ResumeButton`, `ReopenButton`, `TaskChipButton`, `TaskChipLabel`, `GraphChipView`, `GraphChipLabel`, `SteeringBadge`, `SteeringBadgeLabel`, `CloseButton`, `TrailingHost`, `PanelNoticeHost`. `PipelineStripView`: `PipelineStrip`, `Pills`, `Details`, `Announcement`. `PanelNoticeView`: `PanelNotice`, `NoticeIcon`, `ShowPipelineButton`, `ResumeButton`, `DismissButton`, `NoticeText`. `PanelStateView`: `PanelError`, `PanelErrorText`, `RetryLoadingButton`, `LoadingBlock`, `LoadingHeading`, `LoadingBody`, `EmptyBlock`, `EmptyHeading`, `EmptyBody`, `StartFirstAgentButton`. `ComposerView`: `PendingLabel`, `PromptInput`, `SendButton`, `FollowUpButton`, `StopButton`, `HintLine`, `Suggestions`. `ModelPickerView`: `ChipButton`, `LabelText`, `ModelFlyout`, `SearchBox`, `ModelList`, `StatusText`. `TerminalSteerView`: `MessageInput`, `SendNowButton`, `QueueButton`. `OmpTerminalView`: `RestartButton`, `CloseButton`, `StatusText`, `Output`, `UnavailablePanel`, `FixText`. Kanban: `Frame`, `TitleText`, `KindTag`, `KindText`, `ModelText`, `StatusChip`, `ActivityText`, `StaleText`, `DisconnectedText`, `ClosedText`, `SubstrateRow`, `TaskField`, `GraphField`, `LessonsField`, `LeaseField`, `SteerButton`, `SteerPanel`, `SteerBox`, `SendNowButton`, `QueueButton`, `SteerResult`, `ErrorBanner`, `BannerText`, `BannerRetryButton`, `LoadingBlock`, `LoadingHeading`, `LoadingBody`, `EmptyBlock`, `EmptyHeading`, `EmptyBody`, `StartSessionButton`, `ErrorBlock`, `ErrorHeading`, `ErrorBody`, `RetryButton`, `ColumnsScroller`, `ColumnsPanel`, `LiveRegion`. New elements may be added; none may be renamed or removed. The inner `Border#Frame` rule of the Kanban card is kept by adding an inner border, not by moving the name.
2. **Accessible names and live regions**: every `AutomationProperties.Name`, `HelpText` and `LiveSetting` value stays word for word. An icon that replaces a visible word (Close, Settings, copy, reveal) keeps or gains an `AutomationProperties.Name` and a tooltip. Glyph `Path`s stay `AccessibilityView=Raw`; row group headers stay raw. Panel views hold no literal copy (`PanelMarkupTests.NoPanelViewHoldsLiteralCopy`): every new visible string is a constant in `AgentPanelCopy`, `AgentPanelViewText`, `KanbanCopy`, `SettingsCopy` or `ModelsCopy`.
3. **Keyboard map and tab order**: Tab order follows XAML order as documented in `MainView.axaml`: skip link, top tabs, rail (view toggles, then session list or tool toggles), thread (header, pipeline strip, panel state, transcript or terminal, run controls, confirm bars, composer prompt, send, follow-up, stop, side chats). Shortcuts: Ctrl+N new agent, Ctrl+Shift+T new omp terminal, Ctrl+Shift+A Agents tab, Ctrl+Shift+K Kanban tab, Ctrl+1 and Ctrl+2 as built, F6 region cycle, Ctrl+F transcript search, Esc stops or closes, Enter send, Shift+Enter newline, Ctrl+Enter steer, Ctrl+Shift+Enter queue follow-up. Arrow keys move the rail selection, the tab strip and the Kanban cards (roving). The settings window adds Ctrl+, to open and Esc to close (with the close prompt) **[O4]**.
4. **Focus indicator**: 2 px `AccentPurple` border, `FocusAdorner` null, no layout shift, on every focusable control in both themes (primary buttons keep `TextPrimary`). `OmpOS.Size.FocusBorder` stays 2.
5. **Colour is never the only signal**: statuses keep glyph plus words; selection keeps bar plus weight; diff lines keep `+` and `-`; checked toggles keep weight and accessible state; switch state has words.
6. **Contrast contract**: every role in 2.1 is covered by `ThemeContrastTests` as classified in 2.3; no view holds a colour literal (`ColourLiteralTests`, allowlist baseline 0); colour roles stay inside the `Light` and `Dark` dictionaries.
7. **Reduced motion**: the `reduced-motion` window class removes all transitions; running and skeleton pulses are added only under full motion (existing view logic); popups have no animation.
8. **Behavioural contracts of Phases 39 and 46**: status vocabulary and group order, Kanban column set and drag rules (only Backlog and Archived accept drops), steering via Send now and Queue, no inspector, one card per session, no invented Substrate data (`unavailable` stays).
9. **Window**: title `OmpOS`, `MinWidth` 960, `MinHeight` 640, initial 1280 x 800; the 1200 px wide-rail rule.
10. **Secrets**: provider API keys are never displayed after entry, never logged, never placed in a tooltip or accessible name.

---

## 7. Open questions for the owner

| # | Question | Default used by this spec | What it changes |
|---|---|---|---|
| O1 | Density. The current contract (Phase 20 and 46 specs, 26 test assertions in 13 files) requires 44 px targets. Cursor-like density needs 28 to 32 px. WCAG 2.2 AA only needs 24 px (SC 2.5.8). May desktop controls drop to 32 (with 28 and 24 for toolbar and icon buttons)? | Yes: `TargetMin` 32; the 44 assertions become the WCAG 24 px minimum. | Step 6. If **no**: keep 44 only for hit areas by wrapping each 28 px visual in a transparent 44 px hit box (icon buttons; rail rows are already 48); the screen is then roomier and less Cursor-like, and the control gallery needs a second pass. |
| O2 | Custom title bar on Windows and macOS (extended client area, top bar doubles as the title bar), native frame on Linux? | Yes on Windows and macOS, native on Linux. | Section 4.2 chrome code; the rest of the layout is identical. |
| O3 | What does the top bar show as the workspace name? | Workspace root folder name if the head exposes it, else `OmpOS`. | Needs one string from the head; if the head has none, drop the label. |
| O4 | Settings entry point: gear in the rail footer plus Ctrl+, opening a separate window (this spec), or an in-app page? `46-UI-SPEC.md` says no second window. | Separate window. | Step 13 and the rail footer. |
| O5 | Primary action colour in Dark: keep the purple fill (this spec) or use a neutral light fill as Cursor does? | Purple (existing "only accent fill" contract). | `AccentPurple`, `AccentPurpleOn` only; no structural change. |
| O6 | Replace the two variable font files by static instances, one of them (Anybody) derived with fontTools under the OFL. Approved? | Yes. | Step 1; the alternative is to leave Anybody Thin, which breaks the "never thin" rule. |
| O7 | Mode chip (Plan, Agent) in the composer: show only when the engine supplies modes? | Yes, hidden otherwise; no new behaviour. | Step 10 footer. |
| O8 | Kanban Steer: always visible (this spec) or only on hover and focus? | Always visible. | Card density and discoverability. |

---

## Appendix A. Sources (all read 2026-10-26 unless noted)

Cursor, primary.

| Row | Source | What I took from it |
|---|---|---|
| A1 | https://cursor.com/docs/agent/agents-window.md | Agents Window definition; features (multi-workspace, diffs view, parallel agents, worktrees); opens with Cmd+Shift+P; Cursor 3 released Apr 2, 2026 |
| A2 | https://cursor.com/blog/cursor-3 (Apr 2, 2026) and its video frames (https://image.mux.com/SUtXsaFq4FV5xh5014P5wAU163tGojX4zoHQH009jWAbI/thumbnail.jpg, https://image.mux.com/FLecyOH6DuHcjjhp4o0100yRg9Btlb5M7Y1nJAQzrPJf8/thumbnail.jpg, https://image.mux.com/biAMUlQFZttEFGwnJAegRjmqBtk02lTV2fQ2MEVfcvpE/thumbnail.jpg, https://image.mux.com/sZQK9gk9ZNqZLeRjCH02QlzVCEeBY5ktQLgkPTmLRjtU/thumbnail.jpg) | Layout (sidebar, chat, right pane), sidebar rows and groups, diff cards, composer, Changes view, tooltips |
| A3 | https://cursor.com/changelog/3-0 and https://image.mux.com/UMJM00fBs7Y4V2V3LFiwrKHVjjZlZKKpMr2TnfxNp4pI/thumbnail.jpg | Sidebar groups "In progress", "Ready for review", two-line rows with time; Design Mode shortcuts; scroll-to-bottom button; agent tabs |
| A4 | https://cursor.com/docs-static/images/agent/file-agents-window-final.png and https://cursor.com/docs-static/images/agent/open-agents-window-final.png | Summary markdown styling, inline code chips, check lists, "4 Files / Review", IDE composer placeholder "Plan, Build, / for commands, @ for context", mode chip "Agent", model chip "Composer 2 Fast", command palette rows |
| A5 | https://cursor.com/docs/agent/overview.md | Agent tools, side chats (`/side`, `/btw`), steering ("Send now", Enter twice, Tab queues), conversation search Cmd or Ctrl+K, find Cmd or Ctrl+F, `/goal` |
| A6 | https://cursor.com/docs/agent/agent-review.md and https://cursor.com/docs/agent/tools/browser.md and https://cursor.com/docs/agent/tools/terminal.md | Review depth, Source Control tab, browser pane (inline or separate window), terminal tool and sandbox |
| A7 | https://cursor.com/docs/agent/plan-mode.md | Plan mode, Shift+Tab rotation, mode picker |
| A8 | https://cursor.com/changelog (index; Oct 16, 2026 top entry) | Current product cadence; no UI measurement |
| A9 | https://cursor.com/help/customization/themes.md | Theme names `Default Dark Modern`, `Cursor Dark`, Auto Detect Color Scheme |

Third-party and secondary.

| Row | Source | Use |
|---|---|---|
| A10 | https://github.com/BioHazard786/cursor-theme-vscode (MIT, "not affiliated with Cursor") and its `themes/cursor-dark.json` at https://raw.githubusercontent.com/BioHazard786/cursor-theme-vscode/main/themes/cursor-dark.json (read 2026-10-26) | Cursor Dark hex values (editor `#181818`, sidebar `#141414`, input `#1c1c1c`, border alpha 7 %, list selection alpha). Reproduction, not Cursor's source. |
| A11 | Light palette sampling: I sampled the JPEG thumbnails in A2 with Pillow (sidebar `#eeeeee`, chat background `#f9f7f8`, cards `#fdfdfd`, divider `#e5e3e4`). JPEG artefacts make these approximate; they guided `#f0f0ee`, `#fafaf9`, `#ffffff`, `#e0e0dc`. | Light palette |
| A12 | https://www.greptile.com/_next/static/chunks/8a34bd95f6f4410b.css (read 2026-10-26: warm neutrals `#f8f7f3`, `#fdfcfa`, `#f5f4f0`, grid colour `#10b981`) and `docs/plans/vision-research/greptile-font.md` (read 2026-10-22 in that file) | Greptile look; type families and OFL rules |
| A13 | Web search result summaries on Cursor 3 ("Glass" codename, consolidated sidebar), 2026-10-26: https://alternativeto.net/news/2026/4/cursor-3-launches-unified-multi-repo-workspace-for-building-software-with-ai-agents, https://dev.to/gabrielanhaia/cursor-3-glass-replaced-composer-with-an-agents-window-1pcg | Secondary context only; no value in this document depends on them |

Avalonia and fonts.

| Row | Source | Use |
|---|---|---|
| A14 | https://github.com/AvaloniaUI/Avalonia/discussions/16355 (maintainer: "Variable font support is not yet implemented", "You can't modify any variable axis"), found by web search 2026-10-26 | R3 root cause (secondary: I read the search summary, not the discussion page) |
| A15 | https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/src/Avalonia.Themes.Fluent/Controls/{Button,ToggleButton,TextBox,ComboBox,ComboBoxItem,ListBoxItem,TabStripItem,ToolTip,MenuFlyoutPresenter,MenuItem,FlyoutPresenter,ScrollBar,ToggleSwitch,CheckBox,RadioButton}.xaml, `Accents/FluentControlResources.xaml`, `Accents/BaseResources.xaml`, `DensityStyles/Compact.xaml`, `FluentTheme.xaml.cs` (master, read 2026-10-26) | Template structure (state brushes on `PART_ContentPresenter`), resource key names, the `StaticResource` alias idiom, `FluentTheme.DensityStyle`. **Master is not guaranteed identical to the pinned 12.1.4**; the acceptance test in 3.2 is the guard. |
| A16 | https://github.com/AvaloniaUI/Avalonia/pull/22179 to #22185 (variable-font "Glyph Outline" series, still open per web search 2026-10-26) | Context: `FontVariations` is not released |
| A17 | https://github.com/googlefonts/dm-fonts (`Sans/fonts/ttf`, commit `4412393b7d2de9fe7a92064c2dce9b5af5d7fd26`), https://github.com/google/fonts/tree/main/ofl/anybody, https://api.github.com/repos/google/fonts/contents/ofl/spacemono | Upstream static files; Anybody ships only as a variable font |
| A18 | fontTools (installed in a temporary virtualenv, not in the repository): read `fvar` axes and name tables of the shipped `DMSans.ttf` (`opsz` 9 to 40 default 9, `wght` 100 to 1000 default 400), `Anybody.ttf` (`wdth` 50 to 150 default 100, `wght` 100 to 900 **default 100**), `SpaceMono-Regular.ttf`; generated and inspected an Anybody SemiBold instance | R3, section 2.4 |

Repository (read 2026-10-26): `src/OmpOS.Native.Views/Themes/OmpOSTokens.axaml`, `apps/OmpOS.Native/App.axaml`, `MainWindow.axaml`, `MainView.axaml`, `Views/AgentPanel/*`, `Views/Kanban/*`, `Views/Terminal/OmpTerminalView.axaml`, `Views/ComposerView.axaml`, `Views/ModelPickerView.axaml`, `Views/Blocks/*`, `Views/Diff/DiffReviewView.axaml`, `Sessions/TerminalSteerView.axaml`, `src/OmpOS.Presentation/Settings/*`, `tests/OmpOS.Native.Tests/{ThemeContrastTests,FocusVisibleToggleTests,KeyboardFocusTests}.cs`, `Theme/{ColourLiteralTests,FontResourceTests}.cs`, `Theme/FontManifest.txt`, `AgentPanel/Views/PanelMarkupTests.cs`, `.planning/phases/46-agent-panel/46-UI-SPEC.md`, `.planning/phases/39-kanban-board/39-CONTEXT.md`, `docs/plans/vision-research/cursor-settings-window.md`, `docs/plans/vision-research/greptile-font.md`, and the five screenshots under `docs/runbooks/evidence/ui-parity-2026-10-26/before/`.

Not verified (called out above as **[INFERENCE]**): the Cursor Agents window in a dark theme (only Light captures are official); Cursor's real pixel sizes (captures are scaled); `BoxShadows` as a theme resource; `Window.WindowDecorationMargin` in 12.1.4; whether Avalonia's embedded font collection reads nameID 16 on every platform; the `Iciclecreek.Terminal` palette API; how the `before/` screenshots were produced.
