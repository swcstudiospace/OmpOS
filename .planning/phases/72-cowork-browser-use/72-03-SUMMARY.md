---
phase: 72-cowork-browser-use
plan: 03
subsystem: browser
tags: [tools, approvals, vision, taint]
requires:
  - phase: 72-cowork-browser-use
    provides: BrowserUseOptions, NavigationGate, OriginGrants
  - phase: 70-cowork-tool-host
    provides: CoworkToolHost, CoworkSessionContext.ModelReadsImages
provides:
  - Nine browser_* tools through BrowserToolProvider on the real CoworkToolHost
  - Stage-2 broker asks that Skip cannot skip
  - Tainted page envelope, screenshot ladder, and dispatch limits
affects: [72-cowork-browser-use]
actuals:
  tokens: 40000
  tasks: 3
  commits: 1
tech-stack:
  added: []
  patterns: [stage-2 broker ask, CDP screenshot ladder, untrusted page envelope]
key-files:
  created:
    - src/Autonogrammer.Harness.BrowserUse/Tools/BrowserToolProvider.cs
    - src/Autonogrammer.Harness.BrowserUse/Tools/BrowserApprovalStage2.cs
    - src/Autonogrammer.Harness.BrowserUse/Content/PageContentExtractor.cs
    - src/Autonogrammer.Harness.BrowserUse/Vision/ScreenshotPipeline.cs
    - src/Autonogrammer.Harness.BrowserUse/Vision/BrowserLimits.cs
  modified:
    - tests/Autonogrammer.Harness.BrowserUse.Tests/Autonogrammer.Harness.BrowserUse.Tests.csproj
    - tests/Autonogrammer.Harness.BrowserUse.Tests/packages.lock.json
key-decisions:
  - "72-04 BrowserCallGate is not on disk. The provider depends on IBrowserCallGate so Stop cancels the linked call token and take-over can wait. Viewer.BrowserCallGate should implement that interface."
  - "Local pages stay untrusted. LocalOriginsTrusted is not read by the extractor."
  - "AllowedAlways on a dispatch turn is treated as Allowed and is not stored."
patterns-established:
  - "Page text is wrapped with a fresh random id and angle brackets are neutralised."
  - "Screenshots walk PNG, JPEG 85, JPEG 70, then clip scale through CDP parameters only."
requirements-completed: [AG-72-8, AG-72-9, AG-72-10, AG-72-11, AG-72-12, AG-72-14, AG-72-15]
coverage:
  - id: D1
    description: Nine tools, stage 2, downloads, taint, ladder and dispatch limits.
    requirement: AG-72-8
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.BrowserUse.Tests/Tools/BrowserToolProviderTests.cs
        status: pass
    gaps: []
---

# Phase 72 Plan 03: Browser tools, approvals and vision Summary

Nine `browser_*` tools run through the real `CoworkToolHost`. Stage 2 asks the broker for a redirect even in Skip. Downloads deny by default. Page text is untrusted, marked and capped. Screenshots ladder under 3 MiB and stop at 40.

## Verification

Combined class filters passed 21 tests. Locked restore of the test project passed.

## Gaps

- `Viewer/BrowserCallGate` from 72-04 is absent. `IBrowserCallGate` is the seam.
- Dialog, popup and file-chooser CDP handlers are offered by `ApplyGuardsAsync`; the unit proof accepts beforeunload through `BrowserApprovalStage2.AcceptDialog`.
- No upload tool exists.
