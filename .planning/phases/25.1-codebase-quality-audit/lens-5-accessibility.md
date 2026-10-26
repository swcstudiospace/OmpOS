# Lens 5: Accessibility (UI heads)

Phase 25.1, report only. Repository state: commit 59e9dd1. Read-only scans: grep, file reads, `git ls-files`, `wc`, and small arithmetic in python for contrast ratios. No build, test, formatter, or package manager ran.

QUALITY_BAR items in this lens: labelled controls; keyboard operable; visible focus; sufficient contrast; reduced motion. Live regions are recorded under "Accessibility (live regions)", because the lens scan asked for them and the five named items do not cover them.

## 1. Findings

Total recorded: 14 (High 0, Medium 6, Low 8). The table is below the 40-row cap, so no further findings are withheld. No item is a security exposure or a correctness failure, so no High finding is recorded. The A2UI image-alt gap is reachable from agent-supplied input, but it is an accessibility failure, so it is rated Medium.

| file:line | QUALITY_BAR item | severity | disposition | evidence (120 characters or fewer) |
|---|---|---|---|---|
| apps/OmpOS.Android/OmpOSAndroidApp.axaml:38 | Reduced motion | Medium | fix | 120 ms Button BrushTransition; no reduced-motion class or override. MainView built without motion (.axaml.cs:28) |
| apps/OmpOS.Native/App.axaml.cs:64 | Reduced motion | Medium | defer | Passes MotionPreference.System (resolves to Full). No UI sets Reduced. OQ-34 open (docs/open-questions.md:69) |
| src/OmpOS.Native.Views/MainView.axaml:42 | Accessibility (live regions) | Medium | defer | 20 LiveSetting uses in 14 files (16 Polite, 4 Assertive). AT-SPI2 reach not confirmed: OQ-39, phase-20 runbook:51 |
| src/OmpOS.UI/Components/Blocks/A2uiSurfaceView.razor:16 | Labelled controls | Medium | fix | alt=@image.Alt; Alt is null when agent JSON omits it (GenUi/A2uiSurfaceCatalog.cs:115). No test covers it |
| apps/OmpOS.Web/wwwroot/index.html:29 | Keyboard operable | Medium | fix | Error-banner dismiss is a span with no role, tabindex or name, so no keyboard path reaches it |
| src/OmpOS.UI/wwwroot/styles/ompos.css:105 | Sufficient contrast (focus ring) | Medium | fix | Focus ring: 5.13:1 on bg, 4.67:1 on surface (computed). AccessibilityTests.cs:91-92 checks only text pairs |
| src/OmpOS.UI/wwwroot/styles/ompos.css:10 | Sufficient contrast (documentation) | Low | fix | Comments say ~5.9:1 (primary-contrast, :10) and ~7:1 (error-contrast, :12). Computed 5.13:1 and 6.54:1 |
| apps/OmpOS.Web/wwwroot/index.html:26 | Accessibility (error alert) | Low | fix | #blazor-error-ui has no role or live region; the message appears on display without an announcement |
| apps/OmpOS.Web/wwwroot/css/app.css:90 | Reduced motion | Low | fix | 0.05 s stroke transition on the loading ring. Outside the ompos.css reduced-motion scope (ompos.css:110-116) |
| apps/OmpOS.Web/wwwroot/css/app.css:10 | Sufficient contrast | Low | defer | Blazor template colours outside tokens: link 5.08:1, primary button 5.18:1 (computed). No test covers them |
| apps/OmpOS.Android/OmpOSAndroidApp.axaml:104 | Visible focus | Low | fix | No ToggleButton:focus-visible override, though shared MainView toggles appear here. Native sets it at App.axaml:116 |
| src/OmpOS.Native.Views/Views/ComposerView.axaml:70 | Sufficient contrast (classification) | Low | fix | TextPrimary used as a non-text Fill (Stop glyph). ThemeContrastTests.cs:65 has no NonText use for it |
| tests/OmpOS.Native.Tests/Theme/ColourLiteralTests.cs:32 | Sufficient contrast (token discipline) | Low | fix | Literal guard scans src/OmpOS.Native.Views/Views only. App.axaml, MainView.axaml, Android axaml unguarded |
| tests/OmpOS.Native.Tests/ThemeContrastTests.cs:86 | Sufficient contrast | Low | defer | 11 excluded pairs rest on "no view draws over AccentPurple". No view-level test enforces it (grep: 0 uses) |

## 2. Scans

Scope: tracked `.axaml` and `.cs` under apps/OmpOS.Native, src/OmpOS.Native.Views (Views folder for `.cs`), and apps/OmpOS.Android, plus tracked `.razor` and `.html` under apps/OmpOS.Web (excluding wwwroot/lib). The web UI components live in src/OmpOS.UI, which the web head renders, so the razor and html scans also cover `src/OmpOS.UI/**/*.razor` and `.html`. `tools/` is excluded. Lens target list from `git ls-files`: 98 files (34 `.axaml`, 4 razor or html under apps/OmpOS.Web, the rest `.cs` under the Views folder). Added: the 15 src/OmpOS.UI razor files and the two web stylesheets.

### S1. `<(Button|ToggleButton|TextBox|ListBox|CheckBox|ComboBox|Slider|TabItem|MenuItem)\b`
- Hits: 94 raw. 93 in `.axaml`, 1 in `.cs` (src/OmpOS.Native.Views/Views/Hooks/HooksView.axaml.cs:148, `.OfType<Button>()`). 0 in razor or html, which hold no Avalonia elements. 0 in apps/OmpOS.Native and apps/OmpOS.Android `.axaml` files.
- Fine: the `.cs` hit is a type reference, not an element. The 17 `<Control.Property>` tags are property elements, not controls.
- Per control element (76 elements, scripted tag parse): 65 have a literal `AutomationProperties.Name`; 10 have `AutomationProperties.Name="{CompiledBinding ...}"`; 1 has visible text only (HooksView.axaml:132, `Content="Approve consent"`). 0 have neither. None needed a labelled-parent check.
- Bound names resolve: `AccessibleLabel` (src/OmpOS.Presentation/Agents/AgentRun.cs:38), `AcceptName`/`RejectName` (Diff/DiffHunkRow.cs:39,42), `EnableName`/`DisableName`/`MoveUpName`/`MoveDownName`/`ReviewName` (Hooks/HookRow.cs:117-129).
- Fine: all 76 controls are named or visibly labelled.

### S2. `Focusable="False"|IsTabStop="False"|TabIndex="-1"|tabindex="-1"`
- Hits in lens targets: 0.
- Hits repo-wide (tracked, excluding wwwroot/lib): 1, src/OmpOS.UI/Components/SessionView.razor:6 `<section ... tabindex="-1">`.
- Fine (deliberate): the section is the skip-link target (apps/OmpOS.Web/Pages/Index.razor:17 `href="#ompos-conversation"`). It takes focus when the skip link is used, and stays out of the tab order.
- Positive forms, not suppressions, for context: `Focusable="True"` 5 hits (SideChatListView.axaml:23,35; ConversationListView.axaml:22; WorkspaceTreeView.axaml:15; SessionView.axaml:7). `IsTabStop="True"` 2 hits (SideChatListView.axaml:36; SessionView.axaml:8).

### S3. `#[0-9A-Fa-f]{6}` outside src/OmpOS.Native.Views/Themes/
- Hits in lens targets (Avalonia `.axaml` and `.cs`): 0.
- Hits repo-wide: 74 lines. 25 in `.planning/` docs (fine: documentation). 9 in apps/OmpOS.Web/wwwroot/css/app.css (finding, Low, row 10). 9 in src/OmpOS.UI/wwwroot/styles/ompos.css:5-13 (fine: the web token definitions; comments are finding, row 7). 9 in tests/OmpOS.Native.Tests/Theme/ColourLiteralTests.cs:59-103 (fine: fixtures for the literal-detection rule). 22 in binary `.ttf` font files (fine: false positives, not colour).
- Also run: 3-digit `#[0-9A-Fa-f]{3}` in lens targets: 0. Named colours and `Opacity="0.x"` in lens targets: 0 except `BorderBrush="Transparent"` at src/OmpOS.Native.Views/Views/Completion/InlineCompletionBox.axaml:24 (fine: no text drawn in the border).

### S4. `Animation|Transition|Keyframe|Duration=`
- Hits in lens targets: 18 lines in 2 files.
- apps/OmpOS.Native/App.axaml: 14 lines. Lines 44-47 are the shared 120 ms Button BrushTransition (fine, it is the single intended transition). Lines 125-150 are the five `Window.reduced-motion` overrides for ListBox, ListBoxItem, ScrollViewer, TextBox and Button (fine). The class is set at apps/OmpOS.Native/MainWindow.axaml.cs:60 only when motion is `Reduced` (IsReducedMotion, :67).
- apps/OmpOS.Android/OmpOSAndroidApp.axaml: 4 lines, 38-41, the same 120 ms Button transition (finding, row 1). No reduced-motion class or override exists in the Android app or in its MainView.
- Reduced-motion reachability: apps/OmpOS.Native/App.axaml.cs:64 passes `MotionPreference.System`, which MainWindow does not treat as Reduced (finding, row 2).
- Web (lowercase CSS; this regex is case-sensitive, so these were checked separately): apps/OmpOS.Web/wwwroot/css/app.css:90 `transition` (finding, row 9). src/OmpOS.UI/wwwroot/styles/ompos.css:110-116 `@media (prefers-reduced-motion: reduce)` covers `.ompos-session *` and `.ompos-composer *`, and no other transition or animation exists in ompos.css (fine). src/OmpOS.UI/wwwroot/scripts/ompos-scroll.js:19 sets `scrollTop` without `behavior: smooth` (fine: no scroll animation).

### S5. `<img\b[^>]*>` without `alt=` in tracked `.razor` and `.html`
- Hits: 1, src/OmpOS.UI/Components/Blocks/A2uiSurfaceView.razor:16 `<img class="ompos-a2ui-image" src="@image.Url" alt="@image.Alt" />`.
- Literal result: 0 `img` tags without an `alt=` attribute.
- Not fine at runtime: `Alt` is `string?` and comes from agent JSON (src/OmpOS.GenUi/A2uiSurfaceCatalog.cs:105,115). When `alt` is absent, Razor drops the attribute. Native handles the same case with the name "Image" (src/OmpOS.Native.Views/Views/Blocks/A2uiSurfaceView.axaml.cs:46). No test covers a missing alt (tests/OmpOS.UI.Tests/UiBlockRendererTests.cs:133,154 and the Native equivalent always supply alt). Finding, row 4.

### S6. `<input\b` in tracked `.razor` and `.html`
- Hits: 0. The composer is a `<textarea>` (src/OmpOS.UI/Components/Composer.razor:4) labelled `aria-label="Message"` (:5). Fine.
- Also run: `<button` in razor (9 `@onclick` buttons, all `<button>` elements). Each has an `aria-label` or visible text: Index.razor:20, CodeBlockView.razor:8, ErrorBlockView.razor:7, Composer.razor:12,16, ConversationList.razor:13-16 (text content), SessionView.razor:44,48,52. A2uiSurfaceView.razor:29 has visible text but no handler (routed to Lens 1). `@onclick` on non-button elements: 0. `PointerPressed|Tapped|DoubleTapped` in Avalonia: 0 hits, so no pointer-only control. Fine.

### S7. `AutomationProperties\.LiveSetting`
- Hits: 20 in 14 `.axaml` files (16 `Polite`, 4 `Assertive`). Per file: MainView.axaml 2 (:42 Assertive, :62 Polite); Hooks/HooksView.axaml 3 (:14 Polite, :32 and :142 Assertive); Search/WorkspaceSearchView.axaml 2 (:37 Assertive, :44 Polite); SessionView.axaml 2 (:19, :45); Terminal/TerminalPanelView.axaml 2 (:49, :78); and one Polite each in Chats/TranscriptSearchView, Completion/InlineCompletionBox, ComposerView, Diff/DiffReviewView, Editor/EditorView, Export/TranscriptExportView, Inline/InlineEditView, RunControls/ToolApprovalRowView, Workspace/WorkspaceConventionsView.
- Fine as markup. Not confirmed on the platform tree: finding, row 3.
- Web live regions for comparison: `aria-live="polite"` 1 (SessionView.razor:7); `role="status"` 3 (SessionView.razor:32,36,40); `role="alert"` 3 (App.razor:7, Pages/Index.razor:23, Blocks/ErrorBlockView.razor:3).

### Additional scan: the shared Native style set
- App.axaml `:focus-visible` overrides: ListBox, ListBoxItem, ScrollViewer, TextBox, Button, ToggleButton (App.axaml:86-116). Fine. ComboBox and CheckBox have no override and use the FluentTheme default adorner. Not a finding; visibility was not measured.
- Android overrides: ListBox, ListBoxItem, ScrollViewer, TextBox, Button (OmpOSAndroidApp.axaml:80-108). ToggleButton is missing (row 11).

## 3. Gaps

- Contrast cannot be proven by grep. `tests/OmpOS.Native.Tests/ThemeContrastTests.cs` parses `src/OmpOS.Native.Views/Themes/OmpOSTokens.axaml` and covers the pairs below. I did not run it; the gate result is recorded in 25-SUMMARY.md. I computed the minimum ratios with python from the same token values.
  - Covered, text (4.5:1), per variant: 6 text roles over 4 backgrounds (`RoleUses` at ThemeContrastTests.cs:63-75), minus 8 excluded pairs = 16 pairs. Minimum: Light 5.15 (StatusError on SurfaceActive); Dark 4.85 (TextSecondary on SurfaceActive).
  - Covered, non-text (3:1), per variant: 3 non-text roles over 4 backgrounds, minus 3 excluded = 9 pairs. Minimum: Light 5.60 (AccentPurple on SurfaceActive); Dark 3.84 (AccentPurple on SurfaceActive).
  - Not covered, text: TextPrimary, TextSecondary, StatusError, StatusSuccess, StatusWarning on AccentPurple (Light 1.00 to 2.45; Dark 1.26 to 2.61). AccentPurpleOn on Surface, SurfaceRaised, SurfaceActive (Light 1.00 to 1.27; Dark 1.00 to 1.70). These are excluded because "no view draws text over AccentPurple" and AccentPurpleOn is drawn only over AccentPurple. Grep supports the first: AccentPurple is used as BorderBrush (15) and Fill (ComposerView.axaml:46), never as Background. AccentPurpleOn has 0 uses outside the token file, the test and planning docs. The exclusion is enforced by comment only (row 14).
  - Not covered, non-text: FocusRing on AccentPurple (Light 1.26; Dark 1.47), TextSecondary on AccentPurple (Light 1.09; Dark 1.26). AccentPurple on AccentPurple is identity. FocusRing has 0 uses in views (App.axaml:116 uses AccentPurple for focus, as the comment at App.axaml says).
  - Not classified: TextPrimary as a non-text mark, at ComposerView.axaml:70 (Stop glyph Fill) and InlineCompletionBox.axaml:16 (CaretBrush). It passes anyway: 17.40:1 Light, 16.97:1 Dark (computed). Row 12.
  - Web head: `tests/OmpOS.UI.Tests/AccessibilityTests.cs:91-92` checks 2 pairs from `src/OmpOS.UI/wwwroot/styles/ompos.css`: fg/bg (17.40:1) and surface-fg/surface (15.83:1). Not covered: `--ompos-primary-contrast` on `--ompos-primary` (5.13:1), `--ompos-error-contrast` on `--ompos-error` (6.54:1), and `--ompos-focus-ring` on `--ompos-bg` (5.13:1) and on `--ompos-surface` (4.67:1) (row 6). Both contrast tokens have 0 uses in CSS; the focus ring has one (ompos.css:105). Blazor template colours in apps/OmpOS.Web/wwwroot/css/app.css are not covered (row 10).
- Reduced motion, OS value: `MotionPreference.System` resolves to Full on Avalonia. OQ-34 (docs/open-questions.md:69) records that no platform reader exists. Reduced is reachable only in tests (tests/OmpOS.Native.Tests/ReducedMotionTests.cs).
- Reduced motion, FluentTheme internals: FluentTheme control templates may define transitions for controls that the App.axaml reduced-motion overrides do not name (for example CheckBox and ComboBox). grep cannot see inside the theme. [INFERENCE] Not verified.
- Live regions on the platform tree: OQ-39 (docs/open-questions.md:74) is open. The phase-20 runbook (line 51) says the LiveSetting node was not confirmed on AT-SPI2. Requires a live host.
- Keyboard operability at runtime: grep cannot prove it. Coverage exists for some flows: tests/OmpOS.Native.Tests/KeyboardFocusTests.cs (9 facts), AccessibilityNameTests.cs (5 facts), FocusVisibleToggleTests.cs. Web: AccessibilityTests.cs. Not checked: whether every control is reachable by Tab.
- Web axe-core run: `.github/workflows/ci.yml:65` and `scripts/web-smoke/run-smoke.sh:35` pin axe-core 4.14.0 for the published-app smoke. Not run or read in this lens.
- Out of lens, routed: the A2UI web button at src/OmpOS.UI/Components/Blocks/A2uiSurfaceView.razor:29 has no click handler. That is a correctness item for Lens 1.
- Out of lens, routed to Lens 3: `OmpOS.Color.FocusRing` and `OmpOS.Color.AccentPurpleOn` are defined but not used by any view.
- Out of scope: `tools/gweb/*` and `tools/browser-spike/fixtures/index.html` (not in the lens targets). Landmark and heading structure is not one of the named items and was not assessed.

## 4. Method

I started from the lens targets and the scan patterns in the task. I then read the files behind each hit rather than trusting the count: each Avalonia control's start tag and its inner text (scripted), each web component's markup, the shared style files in both heads, and the contrast and accessibility tests. Where a scan reported zero hits, I checked the scope it covered before accepting zero. Two scope decisions: I included `src/OmpOS.UI` razor and CSS, because the web head renders those components and a scan of apps/OmpOS.Web alone would miss the composer, the conversation list and the session view; and I checked the other UI heads' contrast and motion declarations directly, because the scans in the task did not reach them. For contrast I used python arithmetic over the token values, so each ratio is computed, not measured in a browser or by running the tests. Every finding cites a line I read in this session.
