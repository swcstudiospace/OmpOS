---
phase: 72-cowork-browser-use
plan: 08
subsystem: desktop
tags: [browser, composition, shortcuts, docs]
requires:
  - phase: 72-cowork-browser-use
    provides: 72-03 provider and capability, 72-04 gate and pane, 72-06 settings source
provides:
  - Desktop AddBrowserUse composition, shared BrowserCallGate, and HeadSettingsOptions.BrowserUse
  - Stop chord Ctrl+Shift+. and isolation checks
  - Runbook stating the live omp turn was not run
affects: []
actuals:
  tokens: 40000
  tasks: 3
  commits: 1
tech-stack:
  added: []
  patterns: [head composition, shared call gate]
key-files:
  created:
    - apps/Autonogrammer.Native/Cowork/BrowserUseComposition.cs
    - docs/runbooks/browser-use.md
    - tests/Autonogrammer.Native.Tests/Browser/BrowserUseCompositionTests.cs
    - tests/Autonogrammer.ArchitectureTests/BrowserUseIsolationTests.cs
  modified:
    - apps/Autonogrammer.Native/Agents/AgentHead.cs
    - src/Autonogrammer.Native.Views/Views/Shortcuts/ShortcutCatalogue.cs
key-decisions:
  - "The same BrowserCallGate is passed to TakeOverController and BrowserToolProvider."
  - "A disabled or unlocated browser is not added to CoworkToolHost."
  - "The live omp turn is not run because OD-07 was not approved."
patterns-established:
  - "AgentHead.Build attaches the composition whose settings source was assigned to HeadSettingsOptions."
requirements-completed: [AG-72-6, AG-72-8, AG-72-14, AG-72-15]
coverage:
  - id: D1
    description: Composition gates tools and the capability, hands settings to the head, and Stop cancels the shared gate.
    requirement: AG-72-8
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Native.Tests/Browser/BrowserUseCompositionTests.cs
        status: pass
    human_judgment: false
gaps:
  - "CoworkView.OnKeyDownTunnel was not edited. The Stop chord is bound on the pane view, so it runs while that view is in the tunnel, not while focus is only in the composer."
  - "Live Chromium and the owner-approved omp turn were not launched."
---

# 72-08 summary

Desktop composition wires `AddBrowserUse`, shares one `BrowserCallGate` between `TakeOverController` and `BrowserToolProvider`, and assigns `BrowserUseSettingsSource` to `HeadSettingsOptions.BrowserUse`. Tools are omitted when the browser is disabled or not located. Guidance is appended only when the provider is available. The overlay YAML is checked, not rewritten.

The Stop chord is catalogued as Ctrl+Shift+.. [INFERENCE] Phase 69's Cowork surface key tunnel exists (`CoworkView.OnKeyDownTunnel`) but only handles F6, and that file is outside this plan's file list, so the chord is bound on the pane host.

The live omp turn was not run. OD-07 was not approved.
