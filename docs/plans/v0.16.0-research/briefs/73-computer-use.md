# Brief 73: Computer Use (Cowork)

Names are post-rename (`Autonogrammer` is `Autonogrammer`); the repo on disk still uses the old names until the rename tool is applied, and the line references below were read under the old names. Decision record: [ADR-0035](../../../decisions/ADR-0035-cowork-computer-use.md). Research: reports 07, 06, 08, 02, 09.

## Goal
Cowork can look at and operate the user's own desktop (X11 verified here; Windows and macOS compile-checked) through 15 `computer.*` tools behind one policy gate, with per-application grants, local approval, a kill switch, an always-visible indicator and a local audit log. Off by default, never in Code mode, never in the cloud, never remotely approvable.

## Requirements
- AG-73-1: `IComputerUseBackend` exposes availability, permissions, displays, windows (z-order), pointer, capture, input primitives, release-all and clipboard text, all async with `CancellationToken` last.
- AG-73-2: `IComputerActionGate` is the only route to injection; the executor needs a single-use ticket and an architecture test forbids any other reference to the backend.
- AG-73-3: The gate evaluates in the ADR-0035 D3 order and every refusal carries a stable `DenialCode` and a message id.
- AG-73-4: Grants are (session, `AppIdentity`, tier, origin, expiry); tiers are `ViewOnly`, `ClickOnly`, `Full`; the model cannot create a grant.
- AG-73-5: A data catalogue sets category defaults and a built-in denylist (terminals, password managers, system settings, trading and wallets, own process) that users may extend and never reduce.
- AG-73-6: `computer.list_windows` returns titles only for granted applications; hard-denied chords (launchers, run dialogs, kill chord) are refused.
- AG-73-7: Sensitivity classification (credential titles, deletion, purchase, mail and messaging raised above `ViewOnly`, clipboard, first action per application, Dispatch origin) forces local confirmation.
- AG-73-8: `IComputerApprovalPrompt` shows host-observed facts only, defaults focus to Deny, never auto-allows, and times out to deny after 120 s.
- AG-73-9: No primitive is injected while a prompt is open; own-process windows are denied as targets and masked in model frames; an optional security phrase shows in prompt and indicator.
- AG-73-10: `ComputerUseOptions` (`Enabled=false`) validate on start, name keys never values, and cover rate (60 per minute, burst 10), step limit (300, +100), failure pause (5), text cap (2000) and takeover threshold (24 px).
- AG-73-11: A global-hotkey kill switch on its own thread and OS connection is verified before arming; a trip revokes, releases all input, cancels the turn, updates the indicator and writes an audit record.
- AG-73-12: A top-most indicator is visible while armed or acting and the gate denies when it is not in the window list; pointer displacement over the threshold pauses with `UserTookOver`.
- AG-73-13: `ComputerAuditLog` writes owner-only JSON Lines with a `prev` SHA-256 chain, write-ahead before injection, denies on write failure, records typed text as length and hash only, keeps 30 days.
- AG-73-14: The frame pipeline maps coordinates (`CoordinateMap`), masks non-granted and own windows (`VisibleRegionMasker`), downscales to the model profile, encodes PNG under the byte cap and tracks the per-session image budget.
- AG-73-15: `ComputerToolProvider` (`ProviderId` `computer`) implements `ICoworkToolProvider` of ADR-0032 for the 15 `computer_*` tools with descriptor mapping, host-observed previews, untrusted blocks, and the Cowork `--config` overlay (owned by the Cowork profile) sets `computer.enabled=false` and `browser.enabled=false`.
- AG-73-16: The X11 backend captures with `XGetImage`, injects with XTest, lists displays (XRandR) and windows (EWMH, `XQueryTree` fallback) and types text including unmapped characters.
- AG-73-17: The X11 hotkey grabs the chord on a second connection and reports `KillSwitchUnavailable` on `BadAccess`.
- AG-73-18: Wayland sessions and platforms without a backend report typed unavailable states with remediation message ids and register no tools.
- AG-73-19: The Windows backend uses `SendInput`, GDI capture, UIPI and secure-desktop detection and `RegisterHotKey`, and builds warning-free on Linux.
- AG-73-20: The macOS backend uses CGEvent, the system `screencapture`, CoreFoundation window listing and Carbon hotkey, and builds warning-free on Linux.
- AG-73-21: `IComputerPermissionAdvisor` turns permission and availability states into guidance with Settings deep links and a bounded status poll.
- AG-73-22: No relay operation reaches a backend primitive; there is no `computer.*` relay operation (ADR-0036); a `cowork.task` whose `toolClasses` contains `computer_use`, on a desktop with `Cowork:ComputerUse:Enabled` and `Cowork:ComputerUse:AllowDispatchSessions`, registers `computer_*`, every other session registers none and `code.prompt` never does; approvals are local-only, `Cowork:ComputerUse:AllowDispatchSessions=false` by default, Dispatch grants end with the command and within 30 minutes.
- AG-73-23: A Computer Use settings page and opt-in dialog (provider disclosure, grants, denylist additions, kill chord, security phrase, audit) are accessible and tested headless.
- AG-73-24: The serialised scaffold step registers every project (slnx, slnf, `LayerMap`, architecture reference, locks, test projects); phase 73 adds code only and edits a registry file only for rows the scaffold manifest marks "per phase" (`docs/architecture.md` prose, parity audit rows); docs, changelog and `THIRD-PARTY-NOTICES.md` unchanged (no new package).

## Depends on
ADR-0030 and its brief (sign-in gate, `SessionSurface` {Code, Cowork}); ADR-0032 (`ICoworkToolProvider`, Cowork session service, shell, tool-result image contract `ToolUpdateContent`, `--config` overlay, Rule of Two: Computer Use sessions hold no workspace secrets); ADR-0036 (closed operation set with no `computer.*` operation, `computer_use` as a `cowork.task` tool class and capability flag only, `approval.decide` refusal for Computer Use, per-command local approval, 5 minute queue expiry, requirement that the approval window is on the always-denied list); ADR-0034 (optional shared approval service). The gateway route from omp is unverified live (ADR-0010, OD-07).

## Projects and files owned
New (layer per `tests/Autonogrammer.ArchitectureTests/LayerMap.cs`): `Autonogrammer.Harness.ComputerUse.Abstractions` (L1), `Autonogrammer.Harness.ComputerUse` (L2, references `Autonogrammer.Cowork.Abstractions`, L1, so no `SameLayerAllowed` entry), `.ComputerUse.X11`, `.ComputerUse.Windows`, `.ComputerUse.MacOS` (L2, plain `net10.0`, named `Harness.*` for the NoProcess exemption, `Directory.Build.props:52`); tests `Autonogrammer.Harness.ComputerUse.Tests`, `.X11.Tests`, `.Windows.Tests`, `.MacOS.Tests`. Tool names: `computer.<x>` in this brief is the logical name of descriptor `computer_<x>` (ADR-0035 D8). Presentation view models and Native.Views views under `Computer/` folders. Registration of the projects (`LayerMap.cs`, `Autonogrammer.ArchitectureTests.csproj`, `.slnx`, `.Linux.slnf`, locks, project files, test projects) is done by the scaffold step, not by this phase. Existing files touched by this phase: `docs/architecture.md` prose, parity audit rows, `CHANGELOG.md`, `docs/open-questions.md` and `docs/decisions/README.md` (orchestrator), the head composition (`apps/Autonogrammer.Native/...`, `SurfaceHost` of ADR-0032) for backend registration and the `HeaderEndSlot` indicator entry, `SettingsSection` for the new page.

## Work units (file-disjoint)
1. **U1 Contracts and options** (`...ComputerUse.Abstractions`, plus `ComputerUse.Tests/Abstractions`): AG-73-1, 3, 10, 18 (states), `CoordinateMap`, `DenialCode`, `ComputerAvailability`. Tests: `CoordinateMapTests` (round trip within 1 px, negative origins, two displays, macOS scale 2), `ComputerUseOptionsValidatorTests` (options section `Cowork:ComputerUse`; names the key not the value, chord needs two modifiers), `DenialCodeCatalogTests` (every code has a message id). Acceptance: builds with no process or I/O reference.
2. **U2 Gate, grants, catalogue, executor** (`...ComputerUse/Policy`, `/Executor`, `FakeComputerUseBackend`, DI): AG-2, 4 to 9, 11 (coordinator), 12 (takeover). Tests: `GateOrderTests`, `GrantTierTests`, `DenylistTests`, `ForbiddenChordTests`, `SensitivityClassifierTests`, `RateLimiterTests` (manual `TimeProvider`), `KillSwitchCoordinatorTests`, `ApprovalSerialisationTests` (no injection while a prompt is pending), `OwnWindowTests`, `ComputerUseBoundaryTests` in the architecture project. Acceptance: no backend call without a valid ticket.
3. **U3 Frames, audit, tools** (`...ComputerUse/Frames`, `/Audit`, `/Tools`): AG-13, 14, 15. Tests: `VisibleRegionMaskerTests` (z-order, occlusion), `FrameScalerTests`, `PngEncoderTests` (round trip with a test-only reader), `ImageBudgetTests`, `AuditLogChainTests` (truncation detected, write failure denies; computer events stay local: nothing goes to `cowork_events` or the ledger), `AuditRedactionTests`, `ComputerToolProviderTests` (15 tools, envelope text, overlay settings). Acceptance: a frame never exceeds the byte cap or one image per result.
4. **U4 X11 backend and hotkey** (`...ComputerUse.X11`): AG-16, 17, 18 (Wayland detection). Tests: pure `KeysymMapperTests`, `WaylandDetectionTests`; `Category=Live` `X11BackendLiveTests` over a private Xvfb (see Success criteria). Acceptance: xev oracle sees the same events the backend sent.
5. **U5 Windows and macOS backends** (`...ComputerUse.Windows`, `...MacOS`): AG-19, 20, 21 (status sources). Tests: `SendInputBuilderTests` (absolute normalisation, UNICODE text), `CoordinateScaleTests`, `ScreenCaptureArgumentsTests`. Acceptance: `dotnet build -warnaserror` clean on Linux; nothing executes the OS calls.
6. **U6 UI** (`Autonogrammer.Presentation/Computer`, `Autonogrammer.Native.Views/Computer`, head composition): AG-8, 12, 21, 22 (UI side), 23, 24. Tests: `ApprovalDialogViewTests`, `IndicatorWindowViewTests`, `ComputerUseSettingsPageTests` under `Avalonia.Headless`, `PermissionAdvisorTests`, `DispatchOriginTests`. Acceptance: Deny has default focus, controls have names, no colour literals in views.

## Success criteria
- `nice -n 15 dotnet test --project tests/Autonogrammer.Harness.ComputerUse.Tests -c Release --filter-not-trait "Category=Live"` passes.
- `AUTONOGRAMMER_LIVE_X11=1 nice -n 15 dotnet test --project tests/Autonogrammer.Harness.ComputerUse.X11.Tests -c Release --filter-trait "Category=Live"` passes, or each test reports `Assert.Skip("Xvfb not found")`. The fixture starts `Xvfb -displayfd 3 -nolisten tcp -screen 0 1280x800x24` on a free display, passes the display name to the backend options and never reads or sets the session `DISPLAY`.
- Live cases: `xdotool` moves the pointer and the backend reads it; the backend clicks a fixture and `xev` logs the button event; `type` of `Hello, ünï` reaches `xev` as the same keysyms; `scrot` and the backend frames of a fixed `xterm` agree within a per-channel tolerance; the hotkey fires on `xdotool key ctrl+alt+shift+Escape`; a second grab reports `KillSwitchUnavailable`; a bare Xvfb lists windows through `XQueryTree`.
- `nice -n 15 dotnet build src/Autonogrammer.Harness.ComputerUse.Windows/Autonogrammer.Harness.ComputerUse.Windows.csproj -c Release --no-restore -warnaserror -nodeReuse:false -m:1` and the same for `.MacOS` pass.
- `nice -n 15 dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*ProjectGraphTests*"` and `-- --filter-class "*ComputerUseBoundaryTests*"` pass; `-- --filter-class "*DecisionRecordTests*"` passes with ADR-0035 indexed.
- `bash scripts/ci/check-licences.sh` and `python3 scripts/legal/generate-third-party-notices.py --check` pass unchanged (no package added).

## Threat model
- Prompt injection from screen content: untrusted envelope, masking to granted windows, tiers, denylist, chords, confirmation, limits, kill switch, audit; residual misuse inside a `Full` grant.
- Approval spoofing: no injection while a prompt is open, own windows denied and masked, security phrase, host-observed prompt content.
- Kill switch failure: verified registration before arming, separate thread, three stop paths, token checks between primitives.
- Remote hijack: no relay primitive operation and no `computer.*` operation (Computer Use is a `cowork.task` tool class), local-only approvals, per-device opt-in, 30 minute Dispatch grants, indicator.
- TOCTOU on click: re-resolve the window after the move; abort on change.
- Secret leakage: credential-titled windows ask, no typed text or clipboard in logs, redacted audit thumbnails, provider disclosure.
- Application identity spoofing on X11: prefer `/proc/<pid>/exe`; hostile local programs are out of scope.

## Degraded and error states
`Disabled`, `UnsupportedPlatform`, `WaylandUnsupported` (message: sign out and pick the Xorg session), `NoDisplay`, `PermissionMissing(ScreenRecording|Accessibility)`, `InputInjectionUnavailable`, `KillSwitchUnavailable`, `IndicatorUnavailable`, `AuditUnavailable`, `ElevatedTarget`, `SecureDesktop`, `StaleFrame`, `FrameTooLarge`, `ImageBudget`, `RateLimited`, `StepLimit`, `UserTookOver`, `ApprovalTimeout`, `NoLocalApprover`, `Revoked`. Each has one remediation id, no raw window titles or typed text in messages, and no silent fallback to another mechanism.

## Cannot be verified on this host
Windows (SendInput, UIPI, GDI, secure desktop, hotkey), macOS (TCC, CGEvent, `screencapture` attribution, Carbon hotkey, Spaces), Wayland portals, AT-SPI, real multi-DPI multi-monitor, non-US layouts, top-most over full-screen applications, live model vision accuracy and cost, omp reaching the tools through `session/new.mcpServers` and honouring `computer.enabled=false` (OD-07).

## Open questions (each with default and owner; rows below)
OQ-380 to OQ-394 are used by this brief (final block OQ-380 to OQ-399, assigned by the design integrator).

## Index rows
ADR index (`docs/decisions/README.md`):
`| [ADR-0035](ADR-0035-cowork-computer-use.md) | Cowork Computer Use: backend abstraction, policy gate and per-OS backends | Proposed 2026-10-28 | X11 verified under Xvfb; Windows and macOS compile-checked; Wayland unavailable; Cua Driver not adopted; 15 \`computer.*\` tools behind one gate; no new package |`

Open questions (`docs/open-questions.md`, owner autonogrammer-maintainers, status open):
| ID | Question | Why it matters | Default taken |
| --- | --- | --- | --- |
| OQ-380 | Build a Wayland portal backend (RemoteDesktop, ScreenCast, EIS, PipeWire)? | Wayland is unavailable in v0.16.0 | Unavailable with a clear message; revisit when a .NET libei and PipeWire route and a compositor runner exist |
| OQ-381 | Windows capture: GDI now or `Windows.Graphics.Capture` (border, TFM)? Is there a Windows 11 runner? | GDI misses protected video; WGC needs WinRT interop | GDI; measure on a runner before any change |
| OQ-382 | macOS capture: `screencapture` child or a ScreenCaptureKit helper; is its Screen Recording grant attributed to the app? | Decides the TCC guidance and the monthly re-prompt behaviour | `screencapture`; first Mac run must verify attribution |
| OQ-383 | Adopt MIT Cua Driver as an optional backend later? | Avoids three native stacks but needs a second signed app on macOS and nightly pinning | Own code; no sidecar in v0.16.0 (owner and legal) |
| OQ-384 | Add focused-element inspection (AT-SPI, UI Automation, AX) to detect credential fields? | Title patterns are heuristic | Off; typing asks unless the user opts in per application |
| OQ-385 | Where do per-model image sizes come from: provider catalogue field or options table? | Limits differ per provider (only Claude read) | Options table `Vision:ModelOverrides`, default 1280 long edge |
| OQ-386 | Default kill chord and indicator coverage (full-screen apps, macOS Spaces, window managers without top-most)? | A hidden indicator or taken chord blocks arming | Ctrl+Alt+Shift+Escape, configurable; gate denies when the indicator is absent |
| OQ-387 | Does omp honour `computer.enabled=false` and `browser.enabled=false` in an ACP session, and is the host gateway reachable from a live turn? | Otherwise omp's native prelude bypasses the gate | Overlay set and tested at unit level; live check under OD-07 |
| OQ-388 | Verify the catalogue identifiers of Windows and macOS products (terminals, password managers, settings)? | A missed denylist row is a hole | Ship rows marked unverified; runner check before release |
| OQ-389 | Send masked progress thumbnails to the user's other devices over Dispatch? | Privacy and Railway acceptable use | Off by default; stills only, never video |
| OQ-390 | Who delivers image rendering and export of tool results (`ToolUpdateContent`)? | Frames are invisible in the transcript without it | The Cowork shell brief (ADR-0032) |
| OQ-391 | Do the analyzers require `LibraryImport` with `AllowUnsafeBlocks` instead of `DllImport`? | Decides the P/Invoke style | `DllImport` per `PosixProcessSignal.cs:15-16`; confirm at first build |
| OQ-392 | Enforce no workspace secrets in sessions that hold `computer.*` (Rule of Two)? | Browser and screen content is untrusted input | ADR-0032 enforces; this brief only refuses to register tools otherwise |
| OQ-393 | May users override built-in denylist entries (for example a terminal at `ClickOnly`)? | Power users want it; it reopens the shell bypass | No override in v0.16.0 (product owner) |
| OQ-394 | `CoworkApprovalRequirement.ByProviderGate` for actuating `computer_*` tools (ADR-0032 D5/D6) | Per-call prompts would make the vision loop unusable | Accepted by the ADR-0032 author 2026-10-28; the provider asks through `ICoworkApprovalBroker` with `MustBeLocal=true`; row kept so the orchestrator can close it (ADR-0032 owner) |
