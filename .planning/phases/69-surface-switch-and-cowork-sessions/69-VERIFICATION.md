---
phase: 69-surface-switch-and-cowork-sessions
verified: 2026-11-28T05:05:00Z
status: human_needed
score: "unit pass; assistive/visual human"
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
  - ".planning/phases/69-surface-switch-and-cowork-sessions/69-VERIFICATION.md"
covered_digest: "v1:sha256:651c9f1e202dc4a928246510c24016cb43bd79700c503f39ac628fa0d7ccccac"
behavior_unverified: 0
overrides_applied: 0
re_verification: true
previous_status: human_needed
previous_score: "human_needed"
commits: ["994f02b0"]
human_verification:
  - "Screen-reader pass Native+Web incl. hidden-surface announcement."
  - "Pointer/visual check 1280x800."
  - "Ctrl+Shift+1/2 vs browser reservation (OQ-320)."
---

---
## Refresh 2026-11-22 (T855b-be, HEAD b46fd81f, SDK 10.0.402)

*Evidence first, then verdict (ADR-0002). No product code touched. Fresh test-services up failed twice at Dragonfly readiness (90 s, loaded host); DB suites ran vs existing healthy throwaway set (ag67-8382). No live-model, live-CDP, Win/macOS install, Railway, sign-in or screen-reader run claimed.*

*Status: human_needed - unit pass; assistive/visual human*

| Check | Command | Result |
|---|---|---|
| restore | `dotnet restore Autonogrammer.Linux.slnf --locked-mode` | exit 0 |
| build | `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` | 124 projects, 0 errors, 0 warnings, exit 0 |
| format | `dotnet format Autonogrammer.Linux.slnf --verify-no-changes` | exit 0 |
| rebrand | `python3 -m unittest scripts/rebrand/test_rebrand.py; rebrand.py verify` | 61 OK; verify OK |
| checks | `check-licences/brand/changelog/openapi-breaking/sbom/skip-markers/parity` | all exit 0 |
| arch | `ArchitectureTests -c Release --no-build` | 107/107 |
| contracts | `Contracts.Tests non-Live` | 179/179 |
| accounts | `Accounts 215/215; Secrets.Os 83/83; Desktop 47/47` | all exit 0 |
| coord | `Coordination.Tests with services` | 87/87 |
| postgres | `Sessions.Postgres.Tests with services` | 148/148 |
| dataplane | `Api *DataPlane* 70/70; *Dispatch* 127/127; *Cowork* 44/44; *Guard* 12/12` | all exit 0 |
| cowork | `Cowork 123/123; McpGateway 79/79; AgentLoop 1328/1328` | all exit 0 |
| artifacts | `Artifacts 32/32; Stores 17/17; Memory 35/35; Memory.PG 1/1` | all exit 0 |
| browser | `BrowserUse.Tests 124/124` | exit 0 |
| compute | `ComputerUse 90/90; X11 9/9` | exit 0 |
| dispatch | `Client 1/1; Desktop 80/80` | exit 0 |
| resil | `Resilience 62/62; Sessions 124/124` | exit 0 |
| heads | `Presentation 2646/2646; UI 882/882; Web 444/444; Native 2353/2353` | exit 0 |
| a11y | `node --test a11y 48/48; fixture PASS; size ok; StyleA11y 17/17` | exit 0 |
| audit | `CoworkAudit 16/16` | exit 0 |

*Per-criterion: Native 2353/2353 Cowork 67/67 UI 882/882 Presentation 2646/2646.*

*Gaps:*
- None beyond human_verification.

*Human (never automated):*
- Screen-reader pass Native+Web incl. hidden-surface announcement.
- Pointer/visual check 1280x800.
- Ctrl+Shift+1/2 vs browser reservation (OQ-320).

*Digest: v1:sha256 over concatenated bytes of covered_files in listed order excluding this VERIFICATION file (self-reference not computable). Prior content below retained as history.*

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

## Refresh 2026-11-22 (T8783-be, HEAD 52eb2c55, SDK 10.0.402)

*Evidence first, then verdict (ADR-0002/ADR-0003). No product code touched. Restore+build once (-warnaserror clean), then --no-build runs vs throwaway set ag67-8382 (see phase 67 refresh). No screen-reader/pointer/visual claimed.*

*Status: human_needed — 6/6 offline criteria re-proven; screen-reader/pointer/visual stay human.*

| Check | Command | Result |
|---|---|---|
| SC1 | ArchitectureTests `*CoworkSurfaceIsolationTests*` + `*ProjectGraphTests*` | 10/10 + 4/4; full ArchitectureTests 107/107 exit 0 |
| SC2 | Cowork.Tests 123 + Presentation.Tests 2646 | exit 0 |
| SC3 | Native `*Surface*` 6 pass + 3 Live-skipped (ArtifactSurfaceLiveTests), `*CoworkKanbanIsolationTests*` 7/7, `*Cowork*` 67/67; KanbanHead 4/4, SettingsEntry 7/7; literal `*HeadMotionTests*` matched 0 — motion suite is ReducedMotionTests 6/6 | exit 0 |
| SC4 | UI.Tests 882 + Web.Tests 444 | exit 0 |
| SC5 | Api.IntegrationTests vs ag67-8382 (CoworkSessionEndpointsTests, AccountBoardSurfaceTests inside full run) | 1580/1580 exit 0 |
| SC6 | `dotnet build -c Release --no-restore -warnaserror` + `generate-third-party-notices.py --check` | exit 0 + exit 0 |

## Refresh 2026-11-28 (current HEAD 994f02b0, Wave 2)

*Evidence first, then verdict (ADR-0002). No product code touched. Screen-reader + pointer/visual + shortcut-conflict items remain human; automated a11y machine gates green (StyleAccessibility 17/17; API-half HEAD bundle 0 errors from Wave 1), human checks unchanged. Verdict remains `human_needed`.*
