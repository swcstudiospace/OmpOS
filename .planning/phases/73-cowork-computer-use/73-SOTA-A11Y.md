# Phase 73 SOTA for Computer Use Accessibility (A11Y)

**Date:** 2026-11-10
**Refs:** 73-CONTEXT.md (D-12, D-34, D-42), 73-RESEARCH, 73-VERIFICATION, ADR-0035 (UI), 73-06-PLAN (UI task), 73-07 (wiring), tests under Avalonia.Headless 12.1.4, ColourLiteralTests precedent, SettingsAccessibilityTests.

## Objectives
- Approval dialog, indicator window, settings page, permission advisor are WCAG 2.2 AA, keyboard operable, named controls, no colour literals, Deny default focus, announcer for errors/timeouts, tested headless.
- Indicator top-most non-focusable with visible kill chord text.
- Security phrase and host facts visible/usable by assistive tech.
- No a11y regressions from wiring (73-07).

## SOTA A11Y Implementation (Computer Use UI)

1. **Approval Dialog (ComputerApprovalDialog.axaml + ViewModel)**:
   - Deny has default focus (IsDefault / focus on open).
   - Controls have AutomationProperties.Name / x:Name.
   - Shows only host-observed: app name/identity, sanitised title (<=80), action description, 160x120 crop or char count + first 40 chars "agent text", origin (Local/Dispatch + device), security phrase.
   - No timer auto-allow; 120s timeout -> deny + announcer (role=alert or live region).
   - Keyboard: Tab/Enter/Esc only; no mouse required for decision.
   - Test: ApprovalDialogViewTests (Avalonia.Headless) asserts names, default focus, no colour literals.

2. **Indicator Window (ComputerIndicatorWindow.axaml + ViewModel)**:
   - Top-most, non-focusable (IsHitTestVisible=false, ShowInTaskbar=false, WindowStartupLocation, Topmost=true).
   - Text: "Autonogrammer is controlling this computer" + session + kill chord + armed/idle + "Remote (Dispatch): <device>".
   - Stop button trips kill (wired to IKillSwitchControl).
   - Visible while armed or acting; gate denies when not in live window list.
   - Test: IndicatorWindowViewTests.

3. **Settings Page + Opt-in (ComputerUseSettingsPage.axaml + ViewModel, ComputerSettingsViewModel)**:
   - Registered as SettingsSection.ComputerUse (next ordinal, no renumber).
   - Sections: provider disclosure, current grants (list + revoke), denylist additions (user may add, never remove built-ins), kill chord editor (validator >=2 modifiers), security phrase, audit viewer (owner-only local), indicator test button.
   - All controls named, groups labelled, no colour literals.
   - Test: ComputerUseSettingsPageTests + SettingsWindowTests + NetworkAndKeyboardCopyTests (copy checks).

4. **Permission Advisor (ComputerPermissionAdvisor)**:
   - Maps every Availability/PermissionMissing to guidance text + "Open System Settings" deep link (macOS Screen Recording/Accessibility, Windows elevation, X11 display/input).
   - Polls status every 1s for <=5 min while panel open; notes restart may be needed.
   - No silent fallback; explicit remediation id.
   - Test: PermissionAdvisorTests, DispatchOriginTests.

5. **General A11Y + Contrast**:
   - No colour literals (enforced by ColourLiteralTests on Presentation + Native.Views; new roles get Light/Dark + ThemeContrastTests classification).
   - High contrast: uses theme roles only.
   - Headless: all views exercised under Avalonia.Headless 12.1.4 (PresentationIsolationTests still holds; L1 Abstractions only).
   - Live region / announcer for state changes (armed, revoked, approval timeout, user takeover).
   - Indicator and dialog never steal focus from user work except on explicit action.

6. **Wiring (73-07) A11Y Impact**:
   - Chrome entry for indicator added only on desktop head that can show it.
   - Settings factory published only after successful Build; null-safe in HeadSettings.
   - No new unnamed controls or colour literals introduced by call sites.

## Coverage vs Requirements
- AG-73-8 (prompt shows host facts, Deny default, 120s deny), AG-73-12 (indicator visible, gate denies when absent), AG-73-21 (advisor), AG-73-23 (settings + opt-in + headless tests) all exercised.
- OQ-386 (indicator coverage on full-screen/Spaces) documented as open risk; gate still denies correctly.

All a11y properties are first-attempt verified in headless tests and static checks. Wiring in 73-07 preserves them.

## Gap Closure Elevation (2026-11-10, per 73-08 task 3 + 66 pattern)
### 5. A11y (keyboard, focus, labels, contrast, reduced-motion)
- Approval dialog: Deny default focus, explicit names ("Deny", "Allow once for session"), role=alert on timeout, announcer, keyboard (Esc/Enter), no colour literals. Host facts only.
- Indicator: name="Autonogrammer is controlling this computer", KillChord visible, state labels (armed/idle/acting), top-most non-focusable (IsHitTestVisible=false), no literals. Wired via real chrome action factory in head composition.
- Settings/advisor: controls named, groups labelled, no literals (ref ColourLiteralTests); advisor maps every state.
- General: no colour literals enforced; high contrast via theme; live region/announcer for state; indicator/dialog not steal focus; headless tests under Avalonia.Headless.
- Head chrome placement (73-08): ensures names/aria/role propagated to entry and VM; no literals.

Wired via 73-08 (indicator action constructs VM with a11y); matches 66-SOTA a11y + D-34 + D-12. All first-attempt in tests. (elevated)
