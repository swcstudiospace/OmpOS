# Phase 69: Surface switch and Cowork sessions - Research
Source: docs/plans/v0.16.0-research/briefs/69-mode-shell-and-cowork-sessions.md

Summarises research reports 02 (UI architecture), 03 (API and backend, session storage) and 09 (project recipe and gate commands), plus the sources opened on disk on 2026-10-28.

## Standard Stack / What to reuse
- `AgentSessionRegistry` (`src/Autonogrammer.Harness.AgentLoop/Sessions/AgentSessionRegistry.cs:14`): cap `MaxSessions = 200` (`:17`, thrown at `:121-123`), `RecoverAfterRestart` (`:329`), `DetectLost` (`:336`); `AgentSessionRegistryOptions.StateRoot` (`AgentSessionRegistryOptions.cs:13`) moves the session folders. Wrap it in `CoworkSessionRegistry`; do not fork.
- `FileSettingsStore` (`apps/Autonogrammer.Native/Settings/FileSettingsStore.cs:29` `DirectoryName = "config"`, `:32` `settings.json`, `:36` `MotionMember`, `:77` `SetMotion`): pattern for `shell.surface` and for the meta and project stores (atomic write, 0600 file, unknown members kept).
- `ViewTabs.razor:2` (`role="tablist"`, roving `tabindex`, `aria-keyshortcuts`) is the template for `SurfaceTabs.razor`; `TopBar.razor:16-18,35` already has the `Tabs` fragment to place `Surface` beside.
- `MainView.axaml:63` (TopBar grid `Auto,Auto,*,Auto,*,Auto`) and `:90` (`TopTabs`) hold the unused column 2; `MainView.axaml.cs:924` `SetKanbanShown` is the visibility-only precedent.
- `ShortcutCatalogue.cs:19-20,26,34` already use Ctrl+1, Ctrl+2, Ctrl+Shift+A, Ctrl+Shift+K; Ctrl+Shift+1 and Ctrl+Shift+2 are free.
- `SessionEndpoints.cs:103-106` is the owner-in-body 400 to copy for `PUT /cowork/sessions/{id}`.

## Facts the planner needs
- Rename already applied on disk (commit 66-01): `src/`, `apps/`, `tests/` use `Autonogrammer.*`, so briefs' post-rename paths exist verbatim; no "on disk today" notes are needed.
- Not yet on disk (scaffold step or earlier phases): `src/Autonogrammer.Cowork`, `src/Autonogrammer.Contracts/Cowork`, `Presentation/Cowork`, `SessionSurface` (no hit in `src services apps`), the `surface` column, any `ISessionRepository.ListByOwnerAsync` (`ISessionRepository.cs:68` has only `ListSessionIdsByStatusAsync`; `ICloudAgentStore.ListByOwnerAsync` at `src/Autonogrammer.Contracts/Cloud/ICloudAgentStore.cs:42` is an unrelated cloud-agent method). `tests/Autonogrammer.Native.Tests/Shell` already exists; `Native.Tests/Cowork` does not.
- `HeadSettings.Attach` throws unless `owner.Content is MainView` (`HeadSettings.cs:173`); `MainWindow` builds `MainView` at `MainWindow.axaml.cs:24` and `ShellServicesOf` at `:30`; `ShellHarness.MainViewOf` casts `window.Content` to `MainView` (`ShellHarness.cs:97`) and `MainViewOf(` appears on 110 lines under `tests/`: change the helper once, not the callers.
- `KanbanBoardModel` is constructed at `AgentHead.cs:310` and `WebWorkbench.cs:74` only; it turns every `_catalog.Sessions` row into a card (`KanbanBoardModel.cs:328` `SyncEntries`); automations bind at `AgentHead.cs:323`. A card is a catalog row, so the bypass must be the data layer (report `02-ui-architecture.md:188`).
- Registry file name is `sessions.json` at the state root (`apps/Autonogrammer.Native/Agents/SessionEngineSet.cs:53`); Cowork gets `<stateRoot>/cowork/<account>/sessions.json`.
- Server: `GET /board` is registered at `CatalogEndpoints.cs:32`, `BoardAsync` at `:79`, loader `LoadOwnedAsync` at `:271`; ids come from the in-process `SessionRegistry` (lost on restart; `03-api-auth-backend.md:68`, finding 2 at `:167`). `CodeBoardSource` must read through `ListByOwnerAsync`, not `SessionRegistry`.
- Layer rules (`02-ui-architecture.md:97-103`): `Autonogrammer.Cowork` is L2 with the one tuple to `Harness.AgentLoop`; Presentation may not reference Avalonia or Razor (`PresentationIsolationTests`); `Autonogrammer.Web` may not reference `Harness.Acp`, `Harness.Rpc`, `Sandbox` or, by ADR-0032 D7, `Cowork`; `HeadMotionTests` requires `MotionPreference` on each `new MainView(` under `apps/` except Web.
- Colour and size: a new colour role needs Light and Dark values, a `ThemeContrastTests` classification and a `tokens.css` mirror (`02-ui-architecture.md:89,192`); `ColourLiteralTests` forbids literals in `Views/**`. Only `Autonogrammer.Size.CoworkDockWidth` (420) is new.
- Web contract chain: a new API call needs route, `docs/api/openapi.json`, Demo handler fixture and `ClientRouteSurface` entry (`02-ui-architecture.md:91`); CSP `connect-src` is one generated origin (`:128`).
- The 200-task limit appears three times: desktop file, server per-owner (409), and the UI message "You have 200 Cowork tasks. Delete or archive one to start another."
- Build flags (`02-ui-architecture.md:108`): `TreatWarningsAsErrors`, XML docs on public members, no reflection STJ (source-generated `CoworkJsonContext` required).

## Architecture Patterns
- Second stack, not a discriminator: Native Cowork rows never enter an `ISessionCatalog` (`02-ui-architecture.md:147,162`); server uses a typed query (`surface` required, non-nullable).
- Window-level host over in-view extension (`02-ui-architecture.md:121`); both children stay alive and visibility toggles.
- One shared view model per concern in Presentation, two renderers (`ViewMapping.Create` and Razor).
- Ports owned by the consumer (`ICoworkAccountGate` in `Autonogrammer.Cowork`, implemented in the Native head), so no accounts reference and no same-layer tuple (R8).
- Hand-written fakes shipped as product or test support, not NSubstitute (`09-project-recipe-and-conventions.md:121`): use for `ICoworkAccountGate`, `ICoworkSessionApi`, the in-memory repository fake.

## Pitfalls
- A UI-only filter is not isolation: the architecture tests (D-31) and the 3-vs-5 board test are the guard (report `02-ui-architecture.md:188`).
- `MainView` handles `Alt` by early return (ADR-0032 Context) and its skip-link and region-focus code is mode-aware for Agents | Kanban only (`02-ui-architecture.md:54`); F6 across rail, conversation and dock needs its own focus tests in `CoworkView`, not edits to `MainView`.
- `HeadSettings.Attach`, `HeadMotionTests` and `ShellHarness.MainViewOf` all assume `MainView` is the window content; miss one and 110 test call sites break.
- Surface must come from the route, owner from the token; a body field `surface` or `ownerId` must be rejected or ignored, never read.
- Account folder name must not contain the `sub`; hash is first 20 chars of lower-case base32 SHA-256 (D-15); a test must show two keys never share a file.
- Corrupt Cowork file: quarantine and banner, never overwrite; settings error state: stay Code, never overwrite.
- A browser may reserve Ctrl+Shift+1/2 (OQ-320): the visible switch is the sure path, so tests must exercise both.
- Brief 69 "Existing files touched" lists LayerMap, slnx and slnf; R17 and unit 6 say the scaffold step owns them. Do not edit them in this phase.

## Verification commands
From the brief (success criteria); host Linux x64:
- `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*CoworkSurfaceIsolationTests*"` and `-- --filter-class "*ProjectGraphTests*"`
- `dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release`; `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release`
- `dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*Surface*"` and `-- --filter-class "*CoworkKanbanIsolationTests*"`; `KanbanHeadTests`, `HeadMotionTests`, `SettingsEntryTests` unchanged
- `dotnet test --project tests/Autonogrammer.UI.Tests -c Release`; `dotnet test --project tests/Autonogrammer.Web.Tests -c Release`
- `scripts/test-services.sh up` then `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release`
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror`; `python3 scripts/legal/generate-third-party-notices.py --check` (no new package)
- [INFERENCE from `09-project-recipe-and-conventions.md:97-99`] single project: `dotnet test --project tests/Autonogrammer.X.Tests -c Release --filter-not-trait "Category=Live"`.

## Cannot be verified on this host
Windows and macOS rendering and Meta shortcuts (compile only); Wayland focus; screen readers (only automation names, roles, live-region text tested); whether a browser reserves Ctrl+Shift+1 or 2; live Supabase (fake `ICoworkAccountGate`) and any Railway route (deploys paused; throwaway containers only).

## Validation Architecture
Framework: xunit.v3 4.0.2 on Microsoft.Testing.Platform, Shouldly (`09-project-recipe-and-conventions.md:66`); Native headless uses Avalonia.Headless with parallelism off (`02-ui-architecture.md:88`); Web uses bUnit; API uses the throwaway TimescaleDB.
- Quick run: `dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release` and `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release`.
- Full run: the success-criteria commands above (ArchitectureTests, Cowork, Presentation, Native `*Surface*` and `*CoworkKanbanIsolationTests*`, UI, Web, Api.IntegrationTests, solution build).

| Requirement | Test (project) |
| --- | --- |
| AG-69-1 | switch visible only with Cowork services; `SurfaceHost` holds both views (Native.Tests/Shell `*Surface*`) |
| AG-69-2 | Ctrl+Shift+1/2 switch and focus toggle; `AccessibilityNameTests`; bUnit tablist roles, arrows, Home, End, shortcut ids (Native.Tests, UI.Tests) |
| AG-69-3 | `shell.surface` round trip and bad-value error state (Native.Tests); `localStorage` round trip (UI.Tests) |
| AG-69-4 | both surfaces alive, Code turn keeps streaming; `SurfaceActivityModel` counts and announcement (Native.Tests, Presentation.Tests) |
| AG-69-5 | `/cowork` selects Cowork; signed-out, empty, error states; `CssContractTests`, `ShellRegionTests`, `WebShellAccessibilityTests` (UI.Tests, Web.Tests) |
| AG-69-6 | separate file, per-account folder, 200 cap, `RecoverAfterRestart` only Cowork rows, quarantine, meta and project stores (Cowork.Tests) |
| AG-69-7 | no type implements `ISessionCatalog` (Cowork.Tests, `CoworkSurfaceIsolationTests`) |
| AG-69-8 | `CoworkShellModel`, `CoworkSessionListModel`; `PresentationIsolationTests` (Presentation.Tests); `KeyboardFocusTests` F6 across three regions (Native.Tests) |
| AG-69-9 | `CoworkSessionEndpointsTests`, `AccountBoardSurfaceTests` (Api.IntegrationTests) |
| AG-69-10 | `CoworkSurfaceIsolationTests` (ArchitectureTests), `CoworkKanbanIsolationTests` (Native.Tests) |
| AG-69-11 | `CoworkSessionSync` against a scripted `ICoworkSessionApi` (Cowork.Tests) |
| AG-69-12 | chrome-slot entry appears in both surfaces and is keyboard reachable (Native.Tests, UI.Tests) |
| AG-69-13 | `CoworkDockHost` two panels two tabs, one panel no strip, strip keyboard reachable and named (Native.Tests, Avalonia.Headless) |

Wave 0 gaps (none of these exist): `tests/Autonogrammer.Cowork.Tests` content (project is scaffold-only), `tests/Autonogrammer.Native.Tests/Cowork/CoworkKanbanIsolationTests.cs`, `Native.Tests/Shell/*Surface*`, `tests/Autonogrammer.ArchitectureTests/CoworkSurfaceIsolationTests.cs`, `tests/Autonogrammer.Presentation.Tests/Cowork/*`, `tests/Autonogrammer.UI.Tests/Cowork/*`, Api tests `CoworkSessionEndpointsTests` and `AccountBoardSurfaceTests`, fakes (`ICoworkAccountGate`, `ICoworkSessionApi`, in-memory `ISessionRepository` with `ListByOwnerAsync`), and the `ShellHarness.MainViewOf` change through `SurfaceHost`.
