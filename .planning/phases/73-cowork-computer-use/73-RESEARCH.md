# Phase 73: Cowork Computer Use - Research
Source: docs/plans/v0.16.0-research/briefs/73-computer-use.md
This file summarises research reports 02, 06, 07 and 09 (report 08 only as cited by ADR-0035) with the index reconciliation R1, R11, R12, R15, R16, R17 and the scaffold manifest. Nothing was built or run.

## Standard Stack / What to reuse
- No new package: P/Invoke into system libraries with `DllImport` plus `DefaultDllImportSearchPaths` per the exemplar `src/Autonogrammer.Sandbox/PosixProcessSignal.cs:15-16`; no project sets `AllowUnsafeBlocks` (`docs/decisions/ADR-0035-cowork-computer-use.md:18`).
- PNG and resize code does not exist in `src`, `apps` or `services` (grep recorded in `ADR-0035:18`); the pipeline uses `System.IO.Compression.ZLibStream` (`ADR-0035:131`).
- Clipboard: head-provided `IClipboardTextPort`, patterned on `src/Autonogrammer.Native.Views/Views/Attachments/IClipboardImageSource.cs` and `AvaloniaClipboardImageSource.cs` (`ADR-0035:60`).
- NoProcess ban keyed on the `Autonogrammer.Harness.` prefix (`Directory.Build.props:52`); layers `tests/Autonogrammer.ArchitectureTests/LayerMap.cs:13-56`; `ProcessSpawningAssemblies` at `tests/Autonogrammer.ArchitectureTests/ProjectGraphTests.cs:12` gains only `Harness.ComputerUse.MacOS` (it runs `screencapture`; manifest line 66); Web must not reference such assemblies (`ProjectGraphTests.cs:36-41`).
- Options validator precedent: `services/Autonogrammer.Api/Cloud/Options/CloudOptionsValidator.cs`; data-as-rows precedent: `docs/decisions/ADR-0019-provider-catalogue.md` (for `computer-use-apps.json`).
- Pins already present: `Avalonia.Headless` 12.1.4 (`Directory.Packages.props:26`), `ModelContextProtocol` 2.3.0 (`:74`).
- Approval defaults to reuse in tests: `src/Autonogrammer.Harness.Acp/AcpApprovalPolicy.cs:6-11` (deny by default); ceiling rule in `services/Autonogrammer.Api/Endpoints/ToolApprovalEndpoints.cs` is not a substitute for local approval (`ADR-0035:164`).
- Test oracles on this host (observed 2026-10-28, read-only check): `/usr/bin/Xvfb`, `xdotool`, `scrot`, `xterm`, `xev`; versions in `ADR-0035:188`.

## Facts the planner needs
- Interface, gate order, ticket rules and tiers are fixed text: `ADR-0035:41-57` (backend), `:62-83` (gate: ticket valid 2 s, eight steps), `:87-95` (tiers, denylist, chords), `:99-108` (sensitivity, prompt, 120 s), `:112-115` (limits, kill chord, indicator, 24 px takeover), `:119` (audit), `:123-125` (tools), `:129-134` (vision), `:138-152` (backends), `:156` (permissions), `:160-167` (Dispatch).
- Tool names are `computer_<x>` (15 tools, `^[a-z][a-z0-9]*(_[a-z0-9]+)*$`, provider id prefix, no `__`); dotted `computer.<x>` is only a label (`briefs/00-index.md:32`, `ADR-0035:123`). `ByProviderGate` is refused on `Effect=Destructive` by the descriptor validator (`ADR-0035:125`).
- R1: no `computer.*` Dispatch operation, `computer.task` is gone; `computer_use` is a tool class in `cowork.task.payload.toolClasses`; registration needs `Cowork:ComputerUse:Enabled` and `AllowDispatchSessions` (`briefs/00-index.md:85`; `ADR-0035:162-163`).
- R11: options section is `Cowork:ComputerUse` (`briefs/00-index.md:105`). R15: audit stays local, no `cowork_events` or ledger row (`briefs/00-index.md:113`; `ADR-0035:119`). R12 reserves 1 KiB of the 8 KiB prompt for computer guidance (`briefs/00-index.md:107`) but ADR-0035 and brief 73 define no such fragment [fact; gap].
- omp 18.12.5 already ships a `computer` prelude behind `computer.enabled` (default false), backed by a native module with X11 XTEST, Wayland portal, Win32, quartz backends; ADR-0035 does not use it and switches it off by overlay (`07-browser-computer-use-feasibility.md:105-113`; `ADR-0035:12,125`). Report 07 D3 recommended starting with it; ADR-0035 decided own code, which wins.
- Licence traps: `ScreenCapture.NET.X11` and `.DX11` are LGPL-2.1-only; SharpHook bundles `libuiohook` with an unresolved licence (`07:118-119,133`); Cua Driver is MIT but nightly-only and needs its own signed app on macOS (`ADR-0035:171`).
- Windows: `SendInput` is subject to UIPI and fails silently against higher integrity windows (`07:131`); `Windows.Graphics.Capture` draws a yellow border on Windows 10 (`07:132`). macOS: Sequoia re-prompts Screen Recording monthly (`07:139`); App Review may reject `CGEvent.post` (`07:138`, secondary).
- Wayland was never run on this host (`docs/runbooks/phase-20-avalonia-foundation.md:45`; `07:122-127`); X11 under Xvfb is the only measured display path.
- Image facts: tokens `ceil(w/28)*ceil(h/28)`; above 20 images a 2000 px dimension limit applies; the computer-use toolset rejects oversized screenshots (`07:160-162`); tool-host cap is 2 images of 3 MiB (`ADR-0035:125`), tighter than the 5 MiB Bedrock figure.
- `ToolUpdateContent` keeps text and edits only today (`src/Autonogrammer.Contracts/Ui/ToolUpdateContent.cs:5-24`); frames are invisible in the transcript until brief 70 U5 delivers image content (OQ-390, `ADR-0035:193`).
- Success criteria and build commands: `briefs/73-computer-use.md:49-54`.

## Architecture Patterns
- Single choke point: `ComputerActionExecutor` is the only holder of the backend; `IComputerActionGate` issues a single-use ticket bound to the action hash; `ComputerUseBoundaryTests` enforces it (`ADR-0035:72`).
- Thin per-OS adapters: primitives only; click, drag and chord semantics live in the executor; a backend never decides policy (`ADR-0035:60`).
- Pure cores unit-tested without a display: `CoordinateMap`, `VisibleRegionMasker`, `FrameScaler`, PNG encoder, `SendInputBuilder`, rate limiter on a manual `TimeProvider`, audit chain.
- X11 isolation: one `X11Connection` thread owns Xlib; the hotkey grabs on a second connection and thread; tests pass an explicit display name (`ADR-0035:139,143`).
- Fail closed: kill switch must register first, indicator must be in the window list, audit write failure denies, gate exception is `Denied(InternalError)` (`ADR-0035:75,81,83,113`).
- Provider asks through `ICoworkApprovalBroker` with `MustBeLocal = true`; host policy returns Allow for `ByProviderGate` (`ADR-0035:125`).

## Pitfalls
- Never read or set the session `DISPLAY` in tests; start `Xvfb -displayfd 3 -nolisten tcp -screen 0 1280x800x24` (`briefs/73-computer-use.md:50`).
- XTest and `SendInput` events are indistinguishable from hardware input, so an approval dialog cannot verify a trusted event; the defence is serialisation, own-window denial and masking (`ADR-0035:108,231`).
- Scratch-keycode typing changes the keyboard map briefly; a crash between remap and restore leaves it changed (`ADR-0035:230`).
- Compositor window bounds can disagree with the screen (shadows, fractional scaling), so masking is defence in depth (`ADR-0035:223`); XWayland capture of a Wayland desktop is black or partial, hence "unavailable" not "best effort" (`ADR-0035:146`).
- The top-most indicator may be covered by full-screen apps, macOS Spaces or window managers that ignore top-most; the gate then refuses (`ADR-0035:222`, OQ-386).
- Title patterns for credentials are heuristic and English-first; typing asks by default (`ADR-0035:221`, OQ-384).
- Windows and macOS catalogue identifiers are unverified (`ADR-0035:226`, OQ-388); screenshots go to the user's model provider, wording unconfirmed (`ADR-0035:228`).
- Analyzers under `AnalysisLevel latest-all` may force `LibraryImport` (OQ-391, `ADR-0035:229`).
- Do not add a Dispatch click, type or capture operation, and do not let `code.prompt` register `computer_*` (R1).
- Do not register projects or edit locks, slnx, slnf, LayerMap rows (R17); `THIRD-PARTY-NOTICES.md` stays unchanged because no package is added (`briefs/73-computer-use.md:32`).

## Verification commands
- `nice -n 15 dotnet test --project tests/Autonogrammer.Harness.ComputerUse.Tests -c Release --filter-not-trait "Category=Live"` (`briefs/73-computer-use.md:49`).
- `AUTONOGRAMMER_LIVE_X11=1 nice -n 15 dotnet test --project tests/Autonogrammer.Harness.ComputerUse.X11.Tests -c Release --filter-trait "Category=Live"` (line 50; each test skips with `Assert.Skip("Xvfb not found")` when Xvfb is missing).
- `nice -n 15 dotnet build src/Autonogrammer.Harness.ComputerUse.Windows/Autonogrammer.Harness.ComputerUse.Windows.csproj -c Release --no-restore -warnaserror -nodeReuse:false -m:1` and the same for `.MacOS` (line 52).
- `nice -n 15 dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*ProjectGraphTests*"`, `"*ComputerUseBoundaryTests*"`, `"*DecisionRecordTests*"` (line 53).
- `bash scripts/ci/check-licences.sh` and `python3 scripts/legal/generate-third-party-notices.py --check`, unchanged (line 54).
- Presentation and Native views: `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release` and `tests/Autonogrammer.Native.Tests` with `-- --filter-class` for the computer view classes [INFERENCE: brief 73 names the test classes (U6) but no command].

## Cannot be verified on this host
Windows (SendInput, UIPI, GDI, secure desktop, hotkey); macOS (TCC, CGEvent, `screencapture` attribution, Carbon hotkey, Spaces); Wayland portals; AT-SPI; real multi-DPI multi-monitor; non-US layouts; top-most over full-screen applications; live model vision accuracy and cost; omp reaching the tools and honouring `computer.enabled=false` (OD-07). X11 is verifiable under a private Xvfb. Source: `briefs/00-index.md:18`, `briefs/73-computer-use.md:69`, `ADR-0035:188`.

## Validation Architecture
Framework: xunit.v3 with Shouldly under Microsoft.Testing.Platform (`briefs/00-scaffold-manifest.md:9`); views under `Avalonia.Headless` 12.1.4. Projects: `tests/Autonogrammer.Harness.ComputerUse.Tests` (with `Abstractions/`), `.X11.Tests`, `.Windows.Tests`, `.MacOS.Tests` (manifest T9 to T12), `Presentation.Tests`, `Native.Tests`, `ArchitectureTests`. Quick run: the first command above. Full run: every command above plus the repo gate (report 09 section 5).

| Requirement | Tests (brief units) |
| --- | --- |
| AG-73-1, 3, 10, 18 (states) | U1: `CoordinateMapTests` (round trip within 1 px, negative origins, two displays, macOS scale 2), `ComputerUseOptionsValidatorTests` (names key not value, chord needs two modifiers), `DenialCodeCatalogTests` (every code has a message id) |
| AG-73-2, 4 to 7, 9, 11, 12 | U2: `GateOrderTests`, `GrantTierTests`, `DenylistTests`, `ForbiddenChordTests`, `SensitivityClassifierTests`, `RateLimiterTests`, `KillSwitchCoordinatorTests`, `ApprovalSerialisationTests`, `OwnWindowTests`, `ComputerUseBoundaryTests` |
| AG-73-13, 14, 15 | U3: `VisibleRegionMaskerTests`, `FrameScalerTests`, `PngEncoderTests`, `ImageBudgetTests`, `AuditLogChainTests` (truncation detected, write failure denies), `AuditRedactionTests`, `ComputerToolProviderTests` (15 tools, envelope, overlay) |
| AG-73-16, 17, 18 (Wayland) | U4: `KeysymMapperTests`, `WaylandDetectionTests`, Live `X11BackendLiveTests` (xev, xdotool, scrot oracles; hotkey; second grab `KillSwitchUnavailable`; `XQueryTree` fallback) |
| AG-73-19, 20, 21 (sources) | U5: `SendInputBuilderTests`, `CoordinateScaleTests`, `ScreenCaptureArgumentsTests`; `-warnaserror` builds on Linux |
| AG-73-8, 12, 21, 22, 23 | U6: `ApprovalDialogViewTests` (Deny default focus), `IndicatorWindowViewTests`, `ComputerUseSettingsPageTests`, `PermissionAdvisorTests`, `DispatchOriginTests` |
| AG-73-24 | scaffold registration plus licence and notices checks unchanged |
Brief 73's U2 line reads "AG-2, 4 to 9, 11, 12"; AG-2 is read as AG-73-2.

Wave 0 gaps (do not exist yet): all five source and four test projects (scaffold); `FakeComputerUseBackend` and a fake `IComputerApprovalPrompt` (U2); `computer-use-apps.json` catalogue; test-only PNG reader; private-Xvfb fixture with `-displayfd`; `Fixtures` for window lists and z-order; headless view fixtures for dialog and indicator; `ComputerUseBoundaryTests` in the architecture project; message-id resources for every denial code.
