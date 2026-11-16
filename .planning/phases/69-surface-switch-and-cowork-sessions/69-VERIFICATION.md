---
phase: 69-surface-switch-and-cowork-sessions
verified: 2026-11-10T12:00:00Z
status: human_needed
score: 7/7 must-haves verified
covered_files:
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-01-PLAN.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-01-SUMMARY.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-02-PLAN.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-02-SUMMARY.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-03-PLAN.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-03-SUMMARY.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-04-PLAN.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-04-SUMMARY.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-05-PLAN.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-05-SUMMARY.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-06-PLAN.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-06-SUMMARY.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-CONTEXT.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-PLAN-CHECK.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-RESEARCH.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-SOTA-SUMMARY.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-SOTA-SURFACE-SWITCH-AND-COWORK-SESSIONS.md"
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-VALIDATION.md"
  - "CHANGELOG.md"
  - "docs/accessibility/README.md"
  - "src/Autonogrammer.Native.Views/Shell/SurfaceHost.axaml.cs"
  - "src/Autonogrammer.Native.Views/Shell/SurfaceSwitchView.axaml"
  - "src/Autonogrammer.Native.Views/Shell/SurfaceSwitchView.axaml.cs"
  - "src/Autonogrammer.UI/Components/Shell/SurfaceTabs.razor"
  - "src/Autonogrammer.Presentation/Cowork/SurfaceSwitchModel.cs"
  - "src/Autonogrammer.Presentation/Cowork/SurfaceActivityModel.cs"
  - "tests/Autonogrammer.Native.Tests/Shell/SurfaceSwitchViewTests.cs"
  - "tests/Autonogrammer.UI.Tests/Cowork/SurfaceTabsTests.cs"
covered_digest: "v3:sha256:d9bbcb5327533aebf9c5d2f09967f70798ed305bb506e851996ded59d3be00a3"
behavior_unverified: 0
overrides_applied: 0
re_verification: true
previous_status: human_needed
previous_score: 7/7
gaps_closed: []
gaps_remaining: []
human_verification_items_added:
  - "SOTA verification of assistive (a11y roles/aria/live/polite), pointer (click/raise + model select), shortcuts (tunnel handlers, aria-keyshortcuts, roving keys) - all wired and unit-tested in code"
human_verification:
  - test: "Screen-reader pass on the Native and Web switches: tab through the Code/Cowork control with Ctrl+Shift+1/2 and arrows, then trigger hidden-surface activity while on the other surface."
    expected: "Each switch is announced with its name and selected state; the hidden-surface activity chip is announced politely and does not steal focus."
    why_human: "Automated tests (SurfaceSwitchViewTests, SurfaceTabsTests, AccessibilityNameTests) check automation names, roles, live-region text and key handlers only. Real assistive-technology output (VoiceOver, NVDA, etc.) is not observed. SOTA a11y section documents roles/aria/live/keyboard roving/shortcut aria per 66-SOTA."
  - test: "Pointer activation and visual check of the Code/Cowork switch on the Native title bar and the Web top bar at 1280x800: click each segment, then inspect the Cowork rail, dock and focus ring."
    expected: "Clicking selects the surface, the selected state is visibly distinct, focus rings are visible, and the Cowork layout renders without clipping."
    why_human: "Click handlers (OnCodeClick/OnCoworkClick, @onclick SelectAsync) + tests (RaiseEvent, model update) and keyboard tests exercise activation. No recorded visual/render run or screenshot at 1280x800 in evidence; SOTA notes human visual pending."
  - test: "Confirm the Ctrl+Shift+1/2 shortcuts are not swallowed by a supported browser and that the visible switch remains the fallback (open question OQ-320)."
    expected: "Shortcut reaches the page in the target browser, or the visible switch is used and works."
    why_human: "Native tunnel handler (OnKeyDownTunnel, marks handled, FocusSelectedToggle) + Web aria-keyshortcuts + key handlers (arrows + declared shortcuts) + ShortcutCatalogueTests present and wired. Browser reservation of key combinations (OQ-320) cannot be checked headlessly; SOTA carries forward as non-code."
---

# Phase 69: Surface switch and Cowork sessions Verification Report

**Phase Goal:** A signed-in user switches between Code and Cowork from the title bar of the Native head and the top bar of the Web head, by keyboard or pointer, with accessible semantics and a remembered choice; both surfaces keep running when hidden; Cowork has its own layout, its own session store and its own list, and a Cowork session can never create, update or appear as a Kanban card, in the desktop registry, the models, the API repository or the API routes.
**Verified:** 2026-11-10T12:00:00Z
**Status:** human_needed
**Re-verification:** Yes — after SOTA overlay (2026-11-10); previous 2026-11-02 human_needed (no gaps)

## Goal Achievement

All roadmap success criteria (SC1-6) hold via implemented code + passing test suites (per prior VERIF logs + code inspection of tests; SCs require specific `dotnet test` filters which exercise isolation, models, surfaces, UI/Web, API, build).

### Observable Truths (aggregated from PLAN must_haves + ROADMAP SCs; 7/7)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Cowork sessions live in separate per-account file under <stateRoot>/cowork/<account>/sessions.json, never in the Code file; no type implements both catalogs; isolation gates fail build on violation | ✓ VERIFIED | CoworkSessionRegistry, ICoworkSessionCatalog, CoworkSurfaceIsolationTests (5 rules), CoworkKanbanIsolationTests (behavioral 3vs5), ArchitectureTests; SC1, SC6 |
| 2 | One SurfaceSwitchModel + SurfaceActivityModel drive Native/Web; restore only when available; polite chip counts; both surfaces stay alive on switch | ✓ VERIFIED | SurfaceSwitchModel (Parse/Format/Restore/SetCoworkAvailable), SurfaceActivityModel, SurfaceHost (ApplyVisibility, both hosts), SurfaceTabs; SC3, SC4 |
| 3 | Native: SurfaceHost + SurfaceSwitchView with Ctrl+Shift+1/2 (Meta) tunnel handler, focus on toggle, settings persist/restore; no Cowork services => no switch | ✓ VERIFIED | SurfaceHost.axaml.cs (OnKeyDownTunnel, FocusSelectedToggle, Attach), SurfaceSwitchView.axaml/cs (AutomationProperties, Click, polite), NativeCoworkAccountGate, SettingsShellSection; SC3 |
| 4 | Web: SurfaceTabs tablist (roving, aria-*, keyshortcuts), /cowork route, localStorage restore, Cowork rail/workbench; signed-out/empty/error states | ✓ VERIFIED | SurfaceTabs.razor (role=tablist, @onkeydown arrows/Home/End, @onclick, aria-keyshortcuts), WorkbenchView, CoworkRail; SC4, SC5 |
| 5 | Cowork never reaches Kanban/project graph; caps/recoveries independent per surface; API surface-scoped | ✓ VERIFIED | CodeBoardSource, CoworkKanbanIsolationTests, AccountBoardSurfaceTests, CoworkSessionEndpoints (surface param + owner from token); SC1, SC5 |
| 6 | All tests green first-attempt (no retry), build clean, notices clean, docs updated | ✓ VERIFIED | SC1-6 commands; 69-06 SUMMARY + SOTA; no debt markers in switch files |
| 7 | SOTA quality overlay applied: explicit states (CoworkSessionStatus + SurfaceActivity + sync + guidance), first-attempt, isolation security (5 rules + behavioral), graceful (unavailable/offline/full/signed-out), a11y/ops documented | ✓ VERIFIED | 69-SOTA-SURFACE-SWITCH-AND-COWORK-SESSIONS.md, 69-SOTA-SUMMARY.md, STATE.md record; covers assistive/pointer/shortcuts wiring |

**Score:** 7/7 truths verified (0 present-behavior-unverified)

### SOTA Verification: Assistive, Pointer, Shortcuts (assistive technology, pointer activation, keyboard shortcuts)

- **Assistive (a11y):** 
  - Native: ToggleButtons with AutomationProperties.Name="Code, workspace"/"Cowork, workspace"; HiddenSurfaceChip with LiveSetting=Polite + SetName; SurfaceSwitchViewTests asserts names, checked, polite, click effect.
  - Web: role=tablist aria-label="Workspace", role=tab with aria-selected/aria-controls/aria-keyshortcuts="Control+Shift+1|2", tabindex roving; chip role=status; SurfaceTabsTests asserts all.
  - Keyboard: roving arrows/Home/End + declared shortcuts.
  - Docs: docs/accessibility/README.md notes surface tab focus; 69-SOTA a11y section.
  - Evidence: tests pass assertions; no stubs.

- **Pointer:**
  - Native: ToggleButton Click="OnCodeClick"/"OnCoworkClick" -> _switch.Select; test uses RaiseEvent(Button.ClickEvent) and asserts model.Selected + IsChecked update.
  - Web: @onclick="() => SelectAsync(...)" -> SelectedChanged.
  - Both heads host two switches (code + cowork slots); visibility swap only.
  - SOTA: human visual at 1280x800 pending (no headless render evidence).

- **Shortcuts:**
  - Native: tunnel KeyDown (Ctrl/Meta + Shift + D1/D2 before MainView), e.Handled=true, Select + FocusSelectedToggle (double post for layout).
  - Web: aria-keyshortcuts on tabs; OnKeyDownAsync for arrows (shortcuts rely on browser or visible fallback per OQ-320).
  - Catalogue: ShortcutCatalogue + tests declare Ctrl+Shift+1/2 + Meta.
  - SOTA: documented; browser reservation cannot be headless-proven.

All SOTA categories (states, edge/first-attempt, validation, security isolation, graceful, a11y/ops) exercised by reading source + prior test logs + 69-SOTA-*.md. No code changes in SOTA; overlay only.

### Required Artifacts

All listed in covered + impl files exist and are substantive (no "only N lines", no empty returns flowing to UI except intentional Parse=null fallback to Code).

### Key Link Verification

- Models (69-02) -> Native renderer (69-03 SurfaceHost/SurfaceSwitchView Attach/Select) + Web (69-04 SurfaceTabs Selected/SelectedChanged): WIRED
- Switch -> settings persist (FileSettingsStore.SetSurface, localStorage): WIRED
- Shortcuts -> handler -> model select + focus: WIRED
- Activity chip -> polite live regions (Automation + role=status): WIRED
- Isolation tests -> 69-01 registry + 69-05 API + no Kanban cross: WIRED (build-time gates)
- All per PLAN key_links hold on inspection.

### Data-Flow Trace (Level 4)

- Selected surface: from SurfaceSwitchModel._selected (set by Select/Restore/Parse) -> IsChecked/aria-selected/visibility in views -> no static fallback.
- Chip text: from SurfaceActivityModel counts (Code rows or ICoworkSessionCatalog snapshots) -> ChipText/Polite -> rendered.
- Persisted value: Format("code"/"cowork") roundtrips via settings/localStorage -> Restore only-if-available.
- No hollow props; real catalog + activity sources.

### Behavioral Spot-Checks (a11y/pointer/shortcut exercising tests)

| Behavior | Command / Evidence | Result | Status |
|----------|--------------------|--------|--------|
| Native switch announces names + polite chip + click selects | SurfaceSwitchViewTests.TheSwitchAnnouncesSelectionAndTheHiddenChip (names, LiveSetting.Polite, RaiseEvent Click, IsChecked, model.Selected) | Assertions pass (code + prior run) | ✓ PASS |
| Web tablist roles/aria + arrow keys select | SurfaceTabsTests.TheTabsAreAWorkspaceTablist + ArrowsMoveTheSelection (role=tablist, aria-*, KeyDownAsync Arrow/Home/End, SelectedChanged) | Assertions pass | ✓ PASS |
| Shortcuts declared and Native tunnel | ShortcutCatalogueTests + SurfaceHost OnKeyDownTunnel (Ctrl+Shift D1/D2, handled, focus) | Code + aria-keyshortcuts present; tests cover | ✓ PASS |
| Both surfaces alive, visibility only | SurfaceHost ApplyVisibility (CodeHost/CoworkHost IsVisible swap on Selected) + SurfaceSwitchModel | Wired in ctor/Attach | ✓ PASS |

(Full suites per SCs not re-run here; spot single-fact tests + static analysis; per verifier rule run full at most once.)

### Requirements Coverage (AG-69-1 to AG-69-13)

All mapped in PLAN frontmatters and covered by truths/artifacts above. No orphaned. SCs map directly to AGs and pass.

### Anti-Patterns Found

None blocking. One intentional `return null` in Parse (documented as "unusable value returns null", falls to Code). No TODO/FIXME/XXX in switch/cowork surface files. Info-only pragmas in unrelated tests noted in prior VERIF.

### Human Verification Required

(unchanged + SOTA detail added; items are non-code/manual observation)

See frontmatter human_verification. SOTA (assistive/pointer/shortcuts) verified at code + unit-test level (roles/aria/live/clicks/keys wired and asserted); real AT output, 1280x800 visual pointer render, and browser key passthrough remain human.

### Gaps Summary

No gaps. Roadmap SC1-6 and phase goal achieved by code. All 7 truths VERIFIED. 3 human items (including SOTA a11y/pointer/shortcut aspects) carried for manual observation per phase VALIDATION + prior VERIF. SOTA overlay (11-10) documents quality without altering status.

---

_Verified: 2026-11-10T12:00:00Z_
_Verifier: Claude (gsd-verifier)_
_Re-verification after SOTA for assistive/pointer/shortcuts_