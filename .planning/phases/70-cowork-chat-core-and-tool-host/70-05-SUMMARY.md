---
phase: 70-cowork-chat-core-and-tool-host
plan: 05
subsystem: cowork-transcript
tags: [images, transcript, csp, genui]

requires: []
provides:
  - ToolUpdateImage and ToolUpdateContent.Images
  - ImageBlock kind image in both folds, views, export and JSON contexts
affects: []

actuals:
  tokens: 0
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - "Image entries are parsed in ToolUpdateContent.Read; both folds call ImageBlock.FromUpdate"
    - "A payload at or over 10 MiB is a placeholder with empty DataBase64; views never decode it"
    - "CSP stays img-src 'self' data: with no blob, https or wildcard"

key-files:
  created:
    - src/Autonogrammer.Contracts/Ui/ToolUpdateImage.cs
    - src/Autonogrammer.Contracts/Ui/ImageBlock.cs
    - src/Autonogrammer.Native.Views/Views/Blocks/ImageBlockView.axaml
    - src/Autonogrammer.UI/Components/Blocks/ImageBlockView.razor
    - tests/Autonogrammer.Web.Tests/ImageSourceCspTests.cs
  modified:
    - src/Autonogrammer.Contracts/Ui/ToolUpdateContent.cs
    - src/Autonogrammer.GenUi/UiToolBlocks.cs
    - src/Autonogrammer.Sessions/HarnessEventProjection.cs

key-decisions:
  - "ImageBlock members are CallId, ToolName, MimeType, DataBase64, Description, ByteLength, Omitted"
  - "Placeholder means DataBase64 empty and Omitted true; description is kept as alt text"
  - "Fallback alt text is Image returned by tool"
  - "No CSP edit: vercel.ts, build-web.sh, build-demo.sh and serve-demo.sh already allow data images"
  - "SseParser.MaxLineLength (1 MiB) and MaxFrameLength (4 MiB) still reject a frame carrying a 3 MiB image; open for phase 74 and the web-stream owner. Phase 70 does not change the parser or the API"

patterns-established:
  - "Block id is {callId}-image-{i}, replaced in place on a later update"

requirements-completed: [AG-70-5]

# Phase 70 Plan 05: Transcript images

Tool-call updates carry images into the same ImageBlock on both folds, with alt text, a 10 MiB placeholder, and no CSP widening.

## ImageBlock

Members: `CallId`, `ToolName`, `MimeType`, `DataBase64` (empty for a placeholder), `Description`, `ByteLength`, `Omitted`. Kind `image`. Registered in `UiBlockConverter` and both JSON contexts. No compatibility constructor. `ContractVersion.Current` was not edited. No package was added.

`ToolUpdateContent` is still constructed only in its own file. `UiFolder`, `RunProjection` and `ChangeProjection` only call `Read` and were not edited.

## Degraded states

A malformed image entry is ignored. A payload of 10 MiB or more becomes a placeholder and is not decoded. A non-allowlisted source (`image/svg+xml` and anything outside png, jpeg, gif, webp data URIs) never produces an `img`. Views cap width at 480 px. Export keeps the alt text and, when the payload was kept, the data URI reference.

## CSP and SSE

`img-src 'self' data:` was already present. `RenderCspScriptTests.cs` and the CSP files were not edited. `ImageSourceCspTests` locks the four sources.

A 3 MiB image still cannot cross SSE: `SseParser.MaxLineLength` is 1 MiB and `MaxFrameLength` is 4 MiB. That bound is open for phase 74 and the web-stream owner. No live model turn was run.

## Commits

- `feat(70-05):` transcript images through both folds

## Self-Check: PASSED
