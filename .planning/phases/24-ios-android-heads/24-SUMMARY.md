# Phase 24: iOS and Android Heads, Summary

**Status: blocked.** No head project has been created. Android is blocked on this host, and iOS needs a Mac.

The iOS and mobile scope replaces v0.9.2 Phases 15 and 16 under ADR-0004.

Mac channel: Yes for iOS. The iOS build, the UIAccessibility inspection, code signing and device runs are human-gated and listed under XP-11. Android needs no Mac step.

## Success criteria (ROADMAP Phase 24)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | iOS builds net10.0-ios on iOS/iPadOS 26; build log recorded | Blocked | Needs macOS with Xcode (Mac channel) |
| 2 | UIAccessibility returns name and role of prompt, send and transcript | Blocked | No iOS 26 run; Mac channel |
| 3 | Code signing and device runs listed under XP-11 | Blocked | Owner-run on the Mac channel |
| 4 | Android builds net10.0-android in CI on the Linux runner | Blocked | No Android SDK API 36 on this host (ANDROID_HOME unset). CI run log not recorded |
| 5 | Android 16 AccessibilityNodeInfo returns name and role of the three controls | Blocked | No emulator or device run recorded |

## Open items

- Install Android SDK API 36 on a host, or run the Android build on CI. Owner: owner (host setup) or engineering (CI). Route: ROADMAP Phase 24 criterion 4.
- iOS build, accessibility dump, signing and device run on a Mac with Xcode. Owner: owner, Mac channel. Route: XP-11 entry in the runbook.
- Create the iOS and Android head projects once the host blockers are resolved. Owner: engineering. Route: ROADMAP Phase 24.

## Commits

- No Phase 24 code commit.
- 1f4cb4f: ROADMAP reconciliation with the v0.9.2 audit (planning record)
- 9e5913d: gate-green HEAD
