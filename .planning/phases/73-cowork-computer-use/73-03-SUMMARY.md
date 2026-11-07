---
phase: 73-cowork-computer-use
plan: 73-03
subsystem: harness
tags: [computer-use, frames, audit, tools]
requires:
  - 73-02
provides:
  - VisionPipeline
  - ComputerAuditLog
  - ComputerToolProvider
  - ComputerUseToolsComposition
affects: [73-04, 73-06]
tech-stack:
  added: []
  patterns:
    - PNG via BCL ZLibStream; no JPEG and no new package
    - Tools, frames and audit append only through ComputerUseToolsComposition
    - Masking is defence in depth over granted windows
key-files:
  created:
    - src/Autonogrammer.Harness.ComputerUse/Frames/VisionPipeline.cs
    - src/Autonogrammer.Harness.ComputerUse/Audit/ComputerAuditLog.cs
    - src/Autonogrammer.Harness.ComputerUse/Tools/ComputerToolProvider.cs
    - src/Autonogrammer.Harness.ComputerUse/ComputerUseToolsComposition.cs
  modified: []
decisions:
  - SHA-256 hex in the audit log is uppercase because CA1308 rejects ToLowerInvariant
  - CoworkSessionProfile does not expose EnvironmentMode; the Allowlist check stays open
  - Extra type files exist so MA0048 holds
metrics:
  duration: null
  completed: null
---

# Phase 73 Plan 03: Frames, audit, tools Summary

The Computer Use project now masks and budgets frames, writes a local hash-chained audit log, and exposes the 15 `computer_*` tools through a separate composition file.

## What landed

- `VisibleRegionMasker` blacks the desktop, ungranted windows and this process, and stamps a label. Granted windows keep source pixels. A higher window covers a lower one. This is defence in depth and does not open a display.
- `FrameScaler` area-filters to the long edge. `CoordinateMap` remains the only coordinate scale. `PngEncoder` uses `ZLibStream` and re-encodes at 0.85 up to four times, then returns `FrameTooLarge`.
- `VisionPipeline` captures one display, settles from options, tracks `ceil(w/28)*ceil(h/28)` tokens, denies the 81st image with `ImageBudget`, and drops the zoom frame after 60 seconds idle. A layout change returns `StaleFrame`.
- `ComputerAuditLog` writes owner-only JSON Lines with a genesis prev and a per-line SHA-256 chain. A failed write is `AuditUnavailable`. Typed text is length plus hash (`Audit:TypedText` Off). Clipboard bytes are not a field. It implements `IComputerAuditReader`.
- `CoworkToolAuditSinkAdapter` records `ICoworkToolAudit` entries with no arguments and no bytes, and has no ledger. Field names should be re-read when phase 70 settles, without changing redaction.
- `ComputerToolProvider` (`ProviderId` `computer`) exposes the 15 descriptors. Screenshots and window lists are untrusted. Actuating calls ask the broker with `MustBeLocal` and then the 73-02 executor. `ShouldRegisterFor` is true only for a Cowork session whose tool classes contain `computer_use` with both flags on. `code.prompt` never registers.
- The overlay stays on `CoworkSessionProfile`. This project does not write it.

## Gaps

- `CoworkSessionProfile` does not expose an engine `EnvironmentMode`. The Allowlist assertion is open until phase 70 adds that property.
- The live desktop was not driven.
- Extra files beyond the plan list exist only so MA0048 holds (`BgraImage`, `FrameRecord`, `VisionCapture`, `PngEncodeResult`, `ComputerAuditWrite`, `ComputerRegistrationQuery`, `ComputerUseTools`, `ComputerUseToolsOptions`).
