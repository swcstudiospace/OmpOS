---
phase: 72-cowork-browser-use
plan: 04
subsystem: browser
tags: [viewer, screencast, take-over, pane]
requires:
  - phase: 72-cowork-browser-use
    provides: IBrowserCallGate from 72-03
provides:
  - Pull-based screencast pump and desktop IBrowserPaneFeed
  - Desktop take-over, call gate and chrome indicator
  - Framework-free BrowserPaneViewModel
affects: [72-cowork-browser-use]
actuals:
  tokens: 40000
  tasks: 3
  commits: 1
tech-stack:
  added: []
  patterns: [ack after consume, drop never queue, chrome indicator outside the frame]
key-files:
  created:
    - src/Autonogrammer.Contracts/Cowork/Browser/IBrowserPaneFeed.cs
    - src/Autonogrammer.Contracts/Cowork/Browser/IBrowserPaneControl.cs
    - src/Autonogrammer.Harness.BrowserUse/Viewer/ScreencastPump.cs
    - src/Autonogrammer.Harness.BrowserUse/Viewer/LocalBrowserPaneFeed.cs
    - src/Autonogrammer.Harness.BrowserUse/Viewer/TakeOverController.cs
    - src/Autonogrammer.Harness.BrowserUse/Viewer/BrowserCallGate.cs
    - src/Autonogrammer.Presentation/Cowork/Browser/BrowserPaneViewModel.cs
  modified: []
key-decisions:
  - "No relay feed type. Web and Android stay on RemoteViewUnavailable."
  - "BrowserCallGate implements 72-03 IBrowserCallGate. Stop cancels that token and revokes without closing."
  - "F6 is the Release access key. The pane view in 72-05 calls ReleaseControlAsync."
patterns-established:
  - "ScreencastFrameAck runs only after the viewer consume task. A burst is dropped and acknowledged immediately."
  - "Pointer points become viewport CSS pixels from device size and page scale."
requirements-completed: [AG-72-6, AG-72-7, AG-72-13]
coverage:
  - id: D1
    description: Bounded desktop feed, desktop-only take-over, chrome Stop.
    requirement: AG-72-6
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.BrowserUse.Tests/Viewer/ScreencastPumpTests.cs
        status: pass
    gaps: []
---

# Phase 72 Plan 04: Viewer stream and pane model Summary

The desktop pane gets a pull-based JPEG screencast at 10 fps, quality 70 and long edge 1280. Take-over maps pointer and key input on this desktop only. Stop cancels the call token and revokes control without closing the browser.

## Verification

ScreencastPumpTests 5 passed. TakeOverControllerTests 3 passed. BrowserPaneViewModelTests 5 passed. PresentationIsolationTests 3 passed. ContractsUiFreeTests 6 passed. No Chromium was launched.

## Gaps

- The pane view that handles the F6 key is 72-05. This plan exposes ReleaseControlAsync and the Release command.
- Web and Android rendering of RemoteViewUnavailable is 72-05. The sentence is on BrowserPaneCopy.
- 72-08 still has to register TakeOverController as IBrowserPaneControl and pass the same BrowserCallGate into BrowserToolProvider.
