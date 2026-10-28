# Brief 69: Surface switch (Code | Cowork), Cowork layout and the Cowork session model

Decision record: [ADR-0032](../../../decisions/ADR-0032-cowork-shell-sessions-chat-and-tool-host.md). Research: reports 02, 03, 06, 09. Names are post-rename (OmpOS becomes Autonogrammer: `OmpOS.Native` is `Autonogrammer.Native`, `OmpOS.Native.Views` is `Autonogrammer.Native.Views`, `ompos-*` CSS is `autonogrammer-*`); the tree on disk still uses the old names and the rename tool is applied later. The word "surface" means Code or Cowork; the type is `SessionSurface` {Code, Cowork} (ADR-0030, `Autonogrammer.Contracts.Sessions`).

## Goal

A signed-in user switches between Code and Cowork from the title bar of the Native head and the top bar of the Web head, by keyboard or pointer, with accessible semantics and a remembered choice; both surfaces keep running when hidden; Cowork has its own layout, its own session store and its own list, and a Cowork session can never create, update or appear as a Kanban card, in the desktop registry, the models, the API repository or the API routes.

## Requirements

- AG-69-1: A window-level `SurfaceHost` is the content of `MainWindow` and holds `MainView` (Code) and `CoworkView`; a window built without Cowork services shows no switch.
- AG-69-2: The switch is a two-item segmented control with the semantics of ADR-0032 D1 (Web tablist, Native toggle group, polite activity chip) and the shortcuts Ctrl+Shift+1 (Code) and Ctrl+Shift+2 (Cowork), each in the shortcut catalogue with a test.
- AG-69-3: The last surface persists as `shell.surface` in `config/settings.json` (Native) and `autonogrammer.surface` in `localStorage` (Web) and is restored only when Cowork is available.
- AG-69-4: Switching changes visibility only; no session of either surface is stopped, and `SurfaceActivityModel` shows running, needs-approval and failed counts of the hidden surface.
- AG-69-5: The Web head gets `SurfaceTabs`, a `Surface` fragment in `TopBar`, the route `/cowork` and a Cowork workbench shell with rail and empty, signed-out and error states.
- AG-69-6: `CoworkSessionRegistry` keeps Cowork sessions in `<stateRoot>/cowork/<account>/sessions.json`, separate from `<stateRoot>/sessions.json`, with its own 200-session cap and `RecoverAfterRestart`.
- AG-69-7: Cowork has its own contract types (`CoworkSessionSummary`, `ICoworkSessionCatalog`) in `Autonogrammer.Contracts.Cowork`; none is an `ISessionCatalog` or `SessionSummary`.
- AG-69-8: The Cowork layout (rail, conversation host, dock host with a tab strip) has framework-free models in `Autonogrammer.Presentation.Cowork` and two renderers, with no Kanban tab.
- AG-69-9: `CodeBoardSource` is the only caller of the repository list for the board and passes `SessionSurface.Code`; `PUT` and `DELETE /cowork/sessions/{id}` store summary rows with `surface = cowork`.
- AG-69-10: Architecture tests and behavioural tests make the Kanban rule fail the build when broken (ADR-0032 D2).
- AG-69-11: `CoworkSessionSync` pushes Cowork summaries to the API and keeps the latest per session while offline.
- AG-69-12: Each surface's top bar ends in a `HeaderEndSlot` bound to one `SurfaceChromeModel` (Presentation, `Cowork` folder; Native `SurfaceHost` and the Web `TopBar` place it) that ADR-0030 and ADR-0036 fill with the account menu and the Devices entry; the slot has a test that an entry added once appears in both surfaces in the same place and is keyboard reachable.
- AG-69-13: `IDockPanel` is a content contract; `CoworkDockHost` (unit 3) hosts a registered list of panels with a tab strip and one visible panel, and each provider (Artifacts, Browser Use) registers its panel; there is no single fixed dock slot (ADR-0032 D3).

## Depends on

- Brief 67 (ADR-0030 accounts): `IAccountSession` (ADR-0030, in `Autonogrammer.Accounts`; this brief never references it: `Autonogrammer.Cowork` owns the port `ICoworkAccountGate` and the Native head implements it over `IAccountSession`), `SessionSurface`, `GET /account/sessions`, `GET /account/board`. Brief 67 names `CodeBoardSource` (unit 5 here) as its dependency for the board.
- Brief 68 (ADR-0031 repository): `ISessionRepository.ListByOwnerAsync(ownerId, SessionSurface surface, includeClosed, limit, cursor, ct)` (surface required, non-nullable; ADR-0031 D4) and the `surface` column. Unit 5 uses an in-memory fake with the same signature until it lands.
- The rename tool (applied later). Brief 70 builds on units 1 to 3 (it supplies `CoworkSessionHost` and the conversation).

## Projects and files owned

New: `src/Autonogrammer.Cowork` (L2; `AssemblyMarker.cs`, `Sessions/CoworkSessionRegistry.cs`, `CoworkAccountFolder.cs`, `CoworkSessionMetaStore.cs`, `CoworkProjectStore.cs`, `CoworkSessionOptions.cs`, `CoworkSessionLimitException.cs`, `Sync/CoworkSessionSync.cs`, `Sync/ICoworkSessionApi.cs`, `ICoworkAccountGate.cs`: signed in and account folder key) and `tests/Autonogrammer.Cowork.Tests`. The scaffold step (00-scaffold-manifest.md) created and registered both projects, the LayerMap entry `["Autonogrammer.Cowork"] = 2`, and the one `SameLayerAllowed` entry `("Autonogrammer.Cowork", "Autonogrammer.Harness.AgentLoop")` with an `// intentional:` comment.

New in existing projects: `src/Autonogrammer.Contracts/Cowork/*` (`CoworkSessionSummary`, `ICoworkSessionCatalog`, `CoworkJsonContext`); `src/Autonogrammer.Presentation/Cowork/*` (`SurfaceSwitchModel`, `SurfaceActivityModel`, `SurfaceChromeModel`, `CoworkShellModel`, `CoworkSessionListModel`, `CoworkSessionRow`, `CoworkCopy`, `IDockPanel`, a content contract, not a slot: ADR-0032 D3); `src/Autonogrammer.Native.Views/Shell/*` (`SurfaceHost`, `SurfaceSwitchView`, `CoworkSurfaceServices`) and `Views/Cowork/*` (`CoworkView`, `CoworkRailView`, `CoworkDockHost`) and `Views/Shell/SurfaceStyles.axaml`; `src/Autonogrammer.UI/Components/Shell/SurfaceTabs.razor`, `Components/Cowork/CoworkWorkbench.razor`, `CoworkRail.razor`, `wwwroot/styles/surface.css`; `apps/Autonogrammer.Native/Settings/SettingsShellSection.cs`; `services/Autonogrammer.Api/Cowork/*` (`CoworkSessionEndpoints`, `CoworkSessionService`, `CoworkOptions` + validator, `CoworkServiceRegistration`) and `Services/CodeBoardSource.cs`; `tests/Autonogrammer.ArchitectureTests/CoworkSurfaceIsolationTests.cs`; `tests/Autonogrammer.Native.Tests/Shell/*Surface*` and `Cowork/CoworkKanbanIsolationTests.cs`.

Existing files touched: `apps/Autonogrammer.Native/MainWindow.axaml.cs`, `App.axaml.cs`, `Settings/HeadSettings.cs` (`Attach` resolves the `MainView` from a `SurfaceHost`), `Settings/FileSettingsStore.cs`, `SettingsDocument.cs`, `StoredSettings.cs`, `SettingsJsonContext.cs`; `src/Autonogrammer.Native.Views/MainView.axaml` and `.axaml.cs` (one slot `SurfaceSwitchSlot` in `TopBar` column 2, nothing else), `ViewMapping.cs`, `Views/Shortcuts/ShortcutCatalogue.cs`, `ShortcutIds.cs`, `ShortcutCopy.cs`, the Keyboard settings source; `src/Autonogrammer.UI/Components/Shell/TopBar.razor`, `WorkbenchView.razor`, `ShortcutHost.razor`, `wwwroot/scripts/autonogrammer-keys.js`, `wwwroot/styles/autonogrammer.css`; `apps/Autonogrammer.Web/Pages/Index.razor`, `Services/WebWorkbench.cs`, `Services/WebServiceRegistration.cs`; `services/Autonogrammer.Api/Program.cs`, `Endpoints/CatalogEndpoints.cs` (the board reads `CodeBoardSource`), `docs/api/openapi.json`; `tests/Autonogrammer.ArchitectureTests/LayerMap.cs` and its csproj; `Autonogrammer.slnx`, `Autonogrammer.Linux.slnf`; `docs/architecture.md`, `docs/SUMMARY.md`, `CHANGELOG.md`.

## Work units

1. **Cowork contracts, registry and stores.** Owner: `Autonogrammer.Cowork` and `Autonogrammer.Contracts` (Cowork folder). Files: the contracts, `Sessions/*`, `CoworkAccountFolder`, both stores, `Autonogrammer.Cowork.csproj`, `tests/Autonogrammer.Cowork.Tests`. Tests: file separation from the Code file; `ICoworkAccountGate` carries only signed-in state and the account folder key (never the `sub`); per-account folder (two account keys never share a file; the folder name never contains the `sub`); 200-session cap and its message; `RecoverAfterRestart` touches only Cowork rows; quarantine of a corrupt file; meta and project stores (atomic write, owner-only mode, unknown members kept, size cap); no type implements `ISessionCatalog`. Acceptance: ADR-0032 D2 Native rules 1, 2, 4 and 5.
2. **Presentation models.** Owner: `Autonogrammer.Presentation` (Cowork folder only). Files: the models listed above. Tests: `SurfaceSwitchModel` (selection, Cowork unavailable, restore rule), `SurfaceActivityModel` (counts from two catalogs, polite announcement text), `CoworkSessionListModel` (grouping by project, filter, selection, 200-row message), `CoworkShellModel` (dock open and close); `PresentationIsolationTests` still pass (no Avalonia, no Razor). Acceptance: AG-69-4 and AG-69-8 for the models.
3. **Native shell.** Owner: `Autonogrammer.Native.Views` and `Autonogrammer.Native`. Files: `SurfaceHost`, `SurfaceSwitchView`, `CoworkSurfaceServices`, `CoworkView`, `CoworkRailView`, `CoworkDockHost`, `NativeCoworkAccountGate` (implements `ICoworkAccountGate` over `IAccountSession`), `SurfaceStyles.axaml`, the `MainView` slot, `MainWindow`, `HeadSettings.Attach`, shortcut files, settings files, `ShellHarness.MainViewOf` (test support). Tests (Avalonia.Headless): `CoworkDockHost` hosts a registered list of `IDockPanel` content with a tab strip and exactly one visible panel (two registered panels show two tabs and selecting a tab shows only that panel; one panel shows no strip; the strip is keyboard reachable and named); switch visible only with Cowork services; Ctrl+Shift+1 and Ctrl+Shift+2 switch and focus the selected toggle; both surfaces stay alive and a Code turn keeps streaming while Cowork is shown; persistence round trip and the bad-value error state; `AccessibilityNameTests` for the toggles and the chip; `HeadMotionTests` still pass; `KeyboardFocusTests` for F6 across the three Cowork regions; no colour literal. Acceptance: AG-69-1 to AG-69-4 and AG-69-13.
4. **Web shell.** Owner: `Autonogrammer.UI` and `Autonogrammer.Web`. Files: `SurfaceTabs.razor`, `TopBar.razor`, `WorkbenchView.razor`, `ShortcutHost.razor`, the JS and CSS files, `CoworkWorkbench`, `CoworkRail`, `Index.razor`, `WebWorkbench`. Tests (bUnit): tablist roles and `aria-*`, arrow, Home and End keys, shortcut ids, `/cowork` selects Cowork, `localStorage` round trip, signed-out and empty and error states, `CssContractTests` token parity, `ShellRegionTests`, `WebShellAccessibilityTests` (transitions off under reduced motion). The rail reads `GET /account/sessions` filtered to Cowork through the existing client pattern. Acceptance: AG-69-2, AG-69-3, AG-69-5.
5. **API surface rules.** Owner: `services/Autonogrammer.Api` (Cowork folder, `CodeBoardSource`, board endpoint). Files as listed. Tests (`Api.IntegrationTests`): `PUT` stores `surface = cowork` with the owner taken from the token (a body naming an owner is 400, like `SessionEndpoints.cs:103-106`); another owner's row is the same 404 as an absent one; the per-owner cap answers 409; `GET /account/board` and `/board` are empty with Cowork rows present; `GET /sessions` is unchanged; OpenAPI regenerated and `OpenApiCoverageTests` updated. `CoworkSessionSync` (unit 1) is tested against a scripted `ICoworkSessionApi`. Acceptance: AG-69-9 and AG-69-11.
6. **Isolation gates.** Owner: `tests/Autonogrammer.ArchitectureTests`, `Autonogrammer.Native.Tests/Cowork`, solution files. Files: `CoworkSurfaceIsolationTests`, `CoworkKanbanIsolationTests`. Tests: exactly those named in ADR-0032 D2 (type-level, reference-level, construction-site, repository-signature, API-module rules; head board holds exactly the Code sessions across create, update and close of Cowork sessions; automation runs stay in Code). Acceptance: AG-69-10. No registry file is edited here: LayerMap, slnx, slnf, locks and project files come from the scaffold step; only `docs/architecture.md` prose and OpenAPI rows are per-phase edits.

## Success criteria

- `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*CoworkSurfaceIsolationTests*"` and `-- --filter-class "*ProjectGraphTests*"` pass.
- `dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release` and `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release` pass.
- `dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*Surface*"` and `-- --filter-class "*CoworkKanbanIsolationTests*"` pass, and the existing `KanbanHeadTests`, `HeadMotionTests`, `SettingsEntryTests` still pass unchanged apart from the harness helper.
- `dotnet test --project tests/Autonogrammer.UI.Tests -c Release` and `dotnet test --project tests/Autonogrammer.Web.Tests -c Release` pass.
- `scripts/test-services.sh up` then `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release` passes (`CoworkSessionEndpointsTests`, `AccountBoardSurfaceTests`).
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` is clean; `python3 scripts/legal/generate-third-party-notices.py --check` is clean (no new package).

## Threat model

- Integrity of the Kanban rule: a future consumer lists Cowork rows into the board. Mitigated structurally (no shared interface, no unfiltered repository list, one `CodeBoardSource`) and by the architecture tests.
- Cross-account exposure on a shared desktop: Cowork files are per account folder; the folder name is a hash, so it does not leak the `sub`; signing out keeps files and hides them.
- Spoofed surface or owner over the API: `surface` is fixed by the route, never read from a body; the owner comes from the token only; every route is owner-scoped with the same 404 for absent and foreign rows.
- Information in summaries: the server row holds title, status, times, project id and device id only, never messages, paths or trusted folders; titles are capped at 200 characters and treated as plain text in both renderers.
- Resource exhaustion: 200 rows per registry and per owner; the sync debounce is 5 s and the retry backoff is capped.
- UI redress: the switch and the chip are host-drawn from host state; no page or tool text reaches them.

## Degraded and error states

- No Cowork services in the window: no switch, Code only.
- Signed out: Cowork shows "Sign in to use Cowork" with the sign-in action of ADR-0030; the restored surface falls back to Code without an error.
- Cowork registry file corrupt or newer: quarantined (existing rule), empty list and a banner "Cowork tasks could not be read; the file was kept as <name>".
- Registry full: the 200-task message of ADR-0032; "New task" is disabled with the reason.
- Settings file in the error state: the surface stays Code and the settings error text is shown; nothing is overwritten.
- Sync offline or 409: the rail shows a quiet "Not synced" mark with the reason on focus; local work is unaffected.
- Web rail load failure: an alert with Retry; an empty list shows the empty state with "New task" explained as desktop-started until ADR-0036 lands.

## Cannot be verified on this host

- Windows and macOS rendering and the macOS Meta shortcuts (compile-checked only); Wayland focus behaviour.
- Screen readers (NVDA, VoiceOver, Orca): only automation names, roles and live-region text are tested.
- Whether a browser reserves Ctrl+Shift+1 or Ctrl+Shift+2.
- Live Supabase sign-in (a fake `ICoworkAccountGate` is used) and any Railway deployment of the new routes.

## Open questions

- OQ-320: Do browsers reserve Ctrl+Shift+1 and Ctrl+Shift+2 for the Web head? Default: keep them, with the visible switch as the sure path; owner: autonogrammer-maintainers.
- OQ-324: Does the server keep Cowork transcripts? Default: summary rows only; transcripts stay on the desktop and travel over the Dispatch relay; owner: product owner.
- OQ-325: Are local Cowork files deleted on sign-out or account deletion? Default: kept and hidden on sign-out; deleted when the user deletes the account (ADR-0030 runbook); owner: product owner.
- OQ-326: Is the 200-task cap per account on the desktop and per owner on the server right? Default: yes, the same limit as Code; owner: autonogrammer-maintainers.

## Index rows

ADR index row for `docs/decisions/README.md`:

`| [ADR-0032](ADR-0032-cowork-shell-sessions-chat-and-tool-host.md) | Cowork shell, sessions, chat core and tool host | Proposed 2026-10-28 | Window-level SurfaceHost; second Cowork registry per account on the desktop, `surface` column with surface-typed queries on the server; one tool host (`ICoworkToolProvider`) served through the MCP gateway; live omp turn through the gateway not run (OD-07) |`

OQ rows for `docs/open-questions.md` (columns: id, question, why it matters, default taken, owner, status): OQ-320 to OQ-326 as listed here and in brief 70; the final block OQ-320 to OQ-339 is shared by briefs 69 and 70 (ADR-0032).
