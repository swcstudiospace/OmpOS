---
phase: 72-cowork-browser-use
plan: 05
subsystem: browser
tags: [pane, avalonia, blazor, accessibility]
requires:
  - phase: 72-cowork-browser-use
    provides: BrowserPaneViewModel from 72-04
provides:
  - Desktop BrowserPaneView and BrowserPanePanel
  - Web BrowserPane mounted in the Cowork dock
  - browser-pane.css imported before a11y.css
affects: [72-cowork-browser-use]
key-files:
  created:
    - src/Autonogrammer.Native.Views/Views/Browser/BrowserPaneView.axaml
    - src/Autonogrammer.Native.Views/Views/Browser/BrowserPaneView.axaml.cs
    - src/Autonogrammer.Native.Views/Views/Browser/BrowserPaneStyles.axaml
    - src/Autonogrammer.Native.Views/Views/Browser/BrowserPanePanel.cs
    - src/Autonogrammer.UI/Components/Cowork/BrowserPane.razor
    - src/Autonogrammer.UI/wwwroot/styles/browser-pane.css
    - tests/Autonogrammer.Native.Tests/Browser/BrowserPaneViewTests.cs
    - tests/Autonogrammer.UI.Tests/Cowork/BrowserPaneTests.cs
  modified:
    - src/Autonogrammer.UI/wwwroot/styles/autonogrammer.css
    - src/Autonogrammer.UI/Components/Cowork/CoworkWorkbench.razor
key-decisions:
  - "The web head never draws a frame or img. It shows the view-model sentence, and the remote sentence when the feed is RemoteViewUnavailable."
  - "Frame alt text is title at host. A session id title|host supplies both; otherwise the title is Page."
  - "F6 releases keyboard capture only while the desktop pane holds it. CoworkDockHost registration stays in 72-08."
requirements-completed: [AG-72-6, AG-72-7, AG-72-9, AG-72-10, AG-72-13]
coverage:
  - id: D1
    description: Desktop and web panes bind the view model. Stop sits outside the frame.
    requirement: AG-72-13
    verification:
      - kind: unit
        ref: tests/Autonogrammer.UI.Tests/Cowork/BrowserPaneTests.cs
        status: pass
    gaps: []
---

# Phase 72 Plan 05: Pane views Summary

The desktop pane and the web Cowork pane bind BrowserPaneViewModel. Stop and the agent-controlling sentence stay in chrome, outside the page frame. The web head shows no frame. F6 on the desktop pane releases keyboard capture.

## Verification

BrowserPaneTests 5 passed. CssContractTests 32 passed. StyleAccessibilityTests 17 passed. AccessibilityTests was started. Native BrowserPaneViewTests, ColourLiteralTests and ThemeContrastTests were blocked when ComputerUse.Windows and ComputerUse.MacOS did not compile.

## Gaps

- CoworkDockHost registration is 72-08.
- Native headless tests could not run while sibling Computer Use projects failed to compile.
