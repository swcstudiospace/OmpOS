# Phase 69: Surface switch and Cowork sessions - Context
Source: docs/plans/v0.16.0-research/briefs/69-mode-shell-and-cowork-sessions.md
**Gathered:** 2026-10-28
**Status:** Ready for planning

<domain>
## Phase Boundary

Milestone v0.16.0. Requirement ids AG-69-1 to AG-69-13 (the index phase table says "AG-69-1 to AG-69-12"; the brief also defines AG-69-13, the dock host; plan for 13). Six work units from the brief: (1) Cowork contracts, registry and stores; (2) Presentation models; (3) Native shell; (4) Web shell; (5) API surface rules; (6) isolation gates. Depends on phase 67 (account state, `SessionSurface`, `GET /account/sessions`, `GET /account/board`) and phase 68 (`ListByOwnerAsync`, the `surface` column); the scaffold step (R17) already created `Autonogrammer.Cowork`, `tests/Autonogrammer.Cowork.Tests` and their registry rows. Phase 70 builds on units 1 to 3.

Delivers: a signed-in user switches Code | Cowork from the Native title bar and the Web top bar by keyboard or pointer, with accessible semantics and a remembered choice; both surfaces keep running when hidden; Cowork has its own layout, session store and list; a Cowork session can never create, update or appear as a Kanban card, in the desktop registry, the models, the API repository or the API routes.

Must NOT do: the Cowork chat core, `CoworkSessionHost`, the tool host, profile, permissions, conversation model and views (phase 70, ADR-0032 D4 to D6); artifact dock content (ADR-0033); Dispatch (ADR-0036); projects' memory, sharing, scheduling (ADR-0037); accounts and sign-in (phase 67); schema and migrations (phase 68); edit any registry file (LayerMap, slnx, slnf, locks, project files: R17; only `docs/architecture.md` prose, `docs/api/openapi.json` rows and CHANGELOG are per-phase edits); touch `ContractVersion.Current` (R9).

</domain>

<decisions>
## Implementation Decisions

### Surface switch, Native
- **D-01:** A window-level `SurfaceHost` (UserControl in `Autonogrammer.Native.Views`, L3) becomes `MainWindow.Content` and holds `MainView` (Code) and a new `CoworkView`. `MainView` gets no third top-level region (1705 lines, keyboard and focus code wired to Agents | Kanban). (ADR-0032 D1; AG-69-1)
- **D-02:** `MainWindow` keeps its constructor shape and gains an optional `CoworkSurfaceServices`; a window built without it shows no switch (Code only). `HeadSettings.Attach` resolves the `MainView` through `SurfaceHost.Code` when the content is a host; every `new MainView(` call site keeps its `MotionPreference` argument. (ADR-0032 D1; brief 69 U3)
- **D-03:** `MainView.axaml` gets exactly one slot `SurfaceSwitchSlot` (a `ContentControl`) in the unused column 2 of `TopBar`; `CoworkView` has the same bar; both bind one `SurfaceSwitchModel`. (ADR-0032 D1; brief 69 "Existing files touched")
- **D-04:** The switch is one two-item segmented control reusing the `segment-track` and `segment` classes: two `ToggleButton`s in one group, `AutomationProperties.Name` "Code, workspace" and "Cowork, workspace", selection announced by toggle state. (ADR-0032 D1; AG-69-2)
- **D-05:** Ctrl+Shift+1 shows Code and Ctrl+Shift+2 shows Cowork (Meta on macOS); both are added to `ShortcutIds`, `ShortcutCopy`, `ShortcutCatalogue`, the Keyboard settings source, `ShortcutHost.razor` and `autonogrammer-keys.js`, and each has a test that raises the gesture and, on Native, checks the selected toggle takes focus. (ADR-0032 D1; AG-69-2; OQ-320)
- **D-06:** A switch changes visibility only: both stacks stay alive, no session of either surface is stopped, paused or disposed. A session ends only by explicit Stop or Close, app exit (running turns found Lost next start) or sign-out (engines stop after confirmation, rows stay). (ADR-0032 D1; AG-69-4)
- **D-07:** `SurfaceActivityModel` (Presentation) counts per surface running, waiting-for-decision and failed sessions; the switch shows a chip on the hidden surface ("Cowork, 2 running", "Code, needs approval") announced politely: `role="status"` on Web, `AutomationProperties.LiveSetting="Polite"` on Native. An approval waiting in the hidden surface is reachable by the shortcut and times out by ADR-0032 D6. (ADR-0032 D1)
- **D-08:** Native persistence: member `shell.surface` (`"code"` or `"cowork"`, default `"code"`) in `config/settings.json` through a new `SettingsShellSection` and `FileSettingsStore.SetSurface` (pattern `SetMotion`, `FileSettingsStore.cs:77`); an unusable value is the existing error state, the surface stays Code, the settings error text shows and nothing is overwritten. (ADR-0032 D1; AG-69-3)

### Surface switch, Web
- **D-09:** `SurfaceTabs.razor` (`Autonogrammer.UI`) goes into a new `Surface` render fragment of `TopBar.razor` before `Tabs`: `role="tablist"` labelled "Workspace", two `role="tab"` buttons "Code" and "Cowork", `aria-selected`, `aria-controls` the one panel, roving `tabindex`, ArrowLeft/ArrowRight/Home/End like `ViewTabs.razor`, `aria-keyshortcuts`. (ADR-0032 D1; AG-69-5)
- **D-10:** Code keeps `ViewTabs` (Agents | Kanban); Cowork replaces the `autonogrammer-view-panel` body and the `Rail` fragment of `WorkbenchView.razor`. Routes: `/` is Code or the last surface; `/cowork` selects Cowork (deep-link target of ADR-0036 notifications); a switch calls `NavigationManager.NavigateTo(..., replace: true)`. (ADR-0032 D1)
- **D-11:** Web persistence is `localStorage` key `autonogrammer.surface`, read before first paint like `theme-boot.js`. A stored surface is restored only when Cowork is available (signed in); otherwise the head opens Code without an error and Cowork shows its signed-out state ("Sign in to use Cowork" with the ADR-0030 action) when chosen. (ADR-0032 D1; brief 69 degraded states)

### Styling and chrome
- **D-12:** Class `surface-cowork` on the host root with selectors in `Views/Shell/SurfaceStyles.axaml`; `data-surface` on the Web shell root with `wwwroot/styles/surface.css`. No colour role is added (a role needs Light and Dark values, `ThemeContrastTests`, `tokens.css` mirror); one size token `Autonogrammer.Size.CoworkDockWidth` (420) and its CSS twin. (ADR-0032 D1; report 02 s5)
- **D-13:** Each top bar ends in a `HeaderEndSlot` bound to one `SurfaceChromeModel` (Presentation, Cowork folder); `SurfaceHost` owns the model and the placement; ADR-0030 (account menu) and ADR-0036 (Devices) fill it. Test: an entry added once appears in both surfaces in the same place and is keyboard reachable. (ADR-0032 D1; AG-69-12)

### Cowork session model, Native
- **D-14:** `CoworkSessionRegistry` (`Autonogrammer.Cowork`, L2) owns a private `AgentSessionRegistry` at `<stateRoot>/cowork/<account>/sessions.json` with `AgentSessionRegistryOptions.StateRoot = <stateRoot>/cowork/<account>`, so session folders are `<stateRoot>/cowork/<account>/sessions/<id>`. It reuses atomic write, quarantine, the 200-session cap and Lost recovery, and is not an `ISessionCatalog`. (ADR-0032 D2 Native 1; AG-69-6)
- **D-15:** `<account>` is the account folder key supplied by `ICoworkAccountGate`: first 20 characters of the lower-case base32 of SHA-256 of the `sub`, computed by the Native head from `IAccountSession`, never the `sub` itself. Two accounts never share a file; sign-out leaves the files in place. (ADR-0032 D2 Native 1; brief 69 U1)
- **D-16:** `Autonogrammer.Cowork` owns the port `ICoworkAccountGate` (signed-in state and account folder key only); `NativeCoworkAccountGate` (Native head) implements it over `IAccountSession`; `Autonogrammer.Cowork` references neither `Autonogrammer.Accounts` nor its Presentation types; no same-layer tuple; the old `IAccountState` name is gone. (R8; ADR-0032 D7)
- **D-17:** Own contract types in `Autonogrammer.Contracts.Cowork` (L0): `CoworkSessionSummary`, `ICoworkSessionCatalog`, `CoworkJsonContext`. No type implements both `ICoworkSessionCatalog` and `ISessionCatalog`; no Cowork row is a `SessionSummary`. (ADR-0032 D2 Native 2; AG-69-7)
- **D-18:** Seams stay as they are: `AgentSessionRegistry` is Code-only; `SessionCatalogModel` and `KanbanBoardModel` are unchanged and constructed only by `AgentHead.Build` and `WebWorkbench`; the rail uses `CoworkSessionListModel` over `ICoworkSessionCatalog`; the automations binding (`HeadAutomationSessions`, `AgentHead.cs:323`) stays bound to the Code host; `SessionForker` stays Code-only. (ADR-0032 D2 Native 3)
- **D-19:** The cap is 200 Cowork sessions in the Cowork file, independent of Code. `CoworkSessionHost` (phase 70) calls `RecoverAfterRestart` once when it first opens the registry and `DetectLost` on its own timer; phase 69 supplies the registry behaviour and tests that the pass touches only Cowork rows. A full registry shows "You have 200 Cowork tasks. Delete or archive one to start another." and "New task" is disabled with that reason. (ADR-0032 D2 Native 4; AG-69-6)
- **D-20:** Metadata the record has no field for (project, trusted folders, memory-off flag, pinned artifact ids) lives in `<stateRoot>/cowork/<account>/session-meta.json` (`CoworkSessionMetaStore`) and projects in `projects.json` (`CoworkProjectStore`: id, name, folder, instructions): owner-only mode, atomic write, unknown members kept, size cap, same rules as `FileSettingsStore`. Project memory, sharing, scheduling are ADR-0037. (ADR-0032 D2 Native 5; brief 69 U1)
- **D-21:** `CoworkSessionSync` (`Sync/`, over `ICoworkSessionApi`) debounces 5 s and pushes `PUT /cowork/sessions/{id}` summaries, keeps the latest summary per session while offline and retries with a capped backoff; a 409 or offline state shows a quiet "Not synced" mark with the reason on focus. (ADR-0032 D4 "Listing"; AG-69-11)

### Cowork session model, server
- **D-22:** `ISessionRepository` has no unfiltered list; the only list is `ListByOwnerAsync(string ownerId, SessionSurface surface, bool includeClosed, int limit, SessionCursor? cursor, CancellationToken cancellationToken)`, `surface` required, non-nullable, no default; column `surface text NOT NULL DEFAULT 'code'` (phase 68). Unit 5 uses an in-memory fake of the same signature until phase 68 lands. (ADR-0032 D2 Server 1; ADR-0031 D4; index s2 and s4.1)
- **D-23:** `CodeBoardSource` (`services/Autonogrammer.Api/Services/CodeBoardSource.cs`) is the one caller of the list with `SessionSurface.Code` for the board; `GET /account/board`, `/board` (`Endpoints/CatalogEndpoints.cs`) and any Kanban projection read only through it; `GET /account/sessions` makes two typed calls and labels each row; `POST /sessions` always writes Code. (ADR-0032 D2 Server 2; AG-69-9)
- **D-24:** Cowork rows are summary rows (empty `Messages`, no events) written by `PUT /cowork/sessions/{id}` and removed by `DELETE /cowork/sessions/{id}` in `services/Autonogrammer.Api/Cowork/` (`CoworkSessionEndpoints`, `CoworkSessionService`, `CoworkOptions` + validator, `CoworkServiceRegistration`); per-owner cap 200 answers 409; `surface` is fixed by the route, never read from a body; the owner comes from the token (a body naming an owner is 400, as `SessionEndpoints.cs:103-106`); another owner's row gives the same 404 as an absent one. (ADR-0032 D2 Server 3; brief 69 U5 and threat model)
- **D-25:** The server row holds title, status, times, project id and device id only, never messages, paths or trusted folders; titles are capped at 200 characters and rendered as plain text in both renderers. The transcript stays on the desktop and a web or mobile head reads it over the Dispatch relay. (brief 69 threat model; ADR-0032 D2 Server 3; OQ-324)
- **D-26:** This phase owns only the route prefix `/cowork/sessions`; `/cowork/artifacts/...` and `/cowork/artifact-render/{ticket}` are ADR-0033's (R10). Cowork feature options live under `Cowork:` (R11). (ADR-0032 header; R10; R11)

### Layout and view models
- **D-27:** Regions (Native `CoworkView`, Web `CoworkWorkbench`): left rail (tasks, then projects, filter box, "New task"), conversation, right dock (collapsible, `Size.CoworkDockWidth`, `GridSplitter` on Native, resizer on Web). No Kanban tab and no Agents | Kanban strip in Cowork. F6 cycles rail, conversation, dock; skip link "Skip to conversation". (ADR-0032 D3; AG-69-8)
- **D-28:** Phase 69 Presentation (framework-free, `Autonogrammer.Presentation.Cowork`): `SurfaceSwitchModel`, `SurfaceActivityModel`, `SurfaceChromeModel`, `CoworkShellModel` (three parts and dock state), `CoworkSessionListModel` (`CoworkSessionRow`, grouped by project, filter, selection), `CoworkCopy`, `IDockPanel`. `CoworkConversationModel`, `CoworkApprovalQueue`, `ProviderStatusChip` are phase 70. (ADR-0032 D3; brief 69 files; brief 70 files)
- **D-29:** `IDockPanel` is a content contract, not a slot: `CoworkDockHost` hosts a registered list of panels with a tab strip and one visible panel, each provider (Artifacts ADR-0033, Browser Use ADR-0034) registers its own; one registered panel shows no strip; the strip is keyboard reachable and named. (R7; ADR-0032 D3; AG-69-13)
- **D-30:** Native views are new `case`s in `ViewMapping.Create`; Web rail reads `GET /account/sessions` filtered to Cowork through the existing client pattern; the Web surface uses Contracts and Presentation only (`Autonogrammer.Web` references neither `Autonogrammer.Cowork` nor Harness projects). (ADR-0032 D3 and D7; brief 69 U4)

### Isolation gates
- **D-31:** `CoworkSurfaceIsolationTests` (ArchUnitNET and reflection): no type implements both catalog interfaces; no type in `Autonogrammer.Presentation.Kanban` or `Autonogrammer.Harness.AgentLoop.Sessions` references `Autonogrammer.Contracts.Cowork`, `Autonogrammer.Cowork` or `Autonogrammer.Presentation.Cowork`; `KanbanBoardModel` is constructed only in `AgentHead`, `WebWorkbench` and tests; `ISessionRepository` declares no method returning sessions without a `SessionSurface`; `Autonogrammer.Api.Cowork` uses neither `SessionRegistry` nor `CatalogProjector`. (ADR-0032 D2 tests; AG-69-10)
- **D-32:** `CoworkKanbanIsolationTests` (`Native.Tests/Cowork`): a head with 3 Code sessions and a Cowork registry with 5 rows has a board with exactly 3 cards before and after creating, updating and closing a Cowork session; the two files differ; an automation run never lands in the Cowork registry; the two caps and the two `RecoverAfterRestart` passes are independent. API: with a Cowork row in the throwaway TimescaleDB, `GET /account/board` is empty, `GET /account/sessions` lists both rows labelled, `GET /sessions` is unchanged. (ADR-0032 D2 tests; AG-69-10)

### Claude's Discretion
- Exact members of `CoworkSessionSummary` and `CoworkSessionRow` beyond title, status, times, project id, device id; `ICoworkSessionApi` method shapes; the sync debounce/backoff numbers other than the stated 5 s (backoff only "capped").
- Size caps of `session-meta.json` and `projects.json` ("size cap" is stated, no number); the config section path of `CoworkOptions` under `Cowork:` [INFERENCE: not named by the brief].
- Banner and empty-state copy not quoted in the brief (corrupt-file banner "Cowork tasks could not be read; the file was kept as <name>" and the registry-full message are quoted); the Web empty state explains "New task" as desktop-started until ADR-0036 lands.
- Visual form of the activity chip and the dock tab strip within existing tokens.

</decisions>

<specifics>
## Specific Ideas

- Precedent to copy: the Agents | Kanban switch hides regions and keeps state (`MainView.axaml.cs:924` `SetKanbanShown`); the rail segmented pair style (`MainView.axaml:129-145`); `ViewTabs.razor` for roving tabindex and arrow keys.
- Degraded states from the brief: no Cowork services, no switch; signed out, "Sign in to use Cowork"; corrupt or newer Cowork file, quarantined, empty list plus banner; registry full, "New task" disabled with reason; settings in error state, Code stays; Web rail load failure, an alert with Retry.
- UI redress rule: the switch and chip are host-drawn from host state; no page or tool text reaches them.

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Decisions and briefs
- `docs/decisions/ADR-0032-cowork-shell-sessions-chat-and-tool-host.md` D1, D2, D3, D7 and the D4 "Listing" bullet: this phase's share (D4 chat core, D5, D6 are phase 70).
- `docs/plans/v0.16.0-research/briefs/69-mode-shell-and-cowork-sessions.md` — requirements, six units, threat model, degraded states, OQ rows.
- `docs/plans/v0.16.0-research/briefs/00-index.md` — phase table, naming rules (s2), R7, R8, R9, R10, R11, R17, and s4.1 (`ListByOwnerAsync` full signature).
- `docs/plans/v0.16.0-research/briefs/00-scaffold-manifest.md` — row 6 (`Autonogrammer.Cowork`) and T5 (`Autonogrammer.Cowork.Tests`): created and registered before this phase.
- `docs/plans/v0.16.0-research/briefs/70-cowork-chat-core.md` — consumes units 1 to 3; owns `CoworkSessionHost`, the conversation model and views.
- ADR-0030 (`IAccountSession`, `SessionSurface`, `/account/*`), ADR-0031 D4 (`ListByOwnerAsync`, `surface`), ADR-0033 (dock content, `/cowork/artifacts`), ADR-0036 (`/cowork` deep link, Devices entry), ADR-0037 (project memory, scheduled tasks) in `docs/decisions/`.

### Research
- `docs/plans/v0.16.0-research/02-ui-architecture.md` s3 (window and navigation), s4 (registration), s6 (tests), s7 (arch rules), s8 (switch placement), s9 and s10 (isolation, Kanban seam), s11 (running sessions), s12 (settings).
- `docs/plans/v0.16.0-research/03-api-auth-backend.md` s4 (session and Kanban storage), s12 finding 2 (process-local `SessionRegistry`).
- `docs/plans/v0.16.0-research/09-project-recipe-and-conventions.md` s3 to s6 — conventions and gate commands.
- `docs/open-questions.md` rows OQ-320, OQ-324, OQ-325, OQ-326 (already appended).

### Existing source and tests touched (verified on disk)
- `apps/Autonogrammer.Native/MainWindow.axaml.cs`, `App.axaml.cs`, `Settings/HeadSettings.cs` (`Attach`, line 173), `Settings/FileSettingsStore.cs`, `SettingsDocument.cs`, `StoredSettings.cs`, `SettingsJsonContext.cs`.
- `src/Autonogrammer.Native.Views/MainView.axaml` (`TopBar` grid line 63, `TopTabs` line 90), `MainView.axaml.cs`, `ViewMapping.cs`, `Views/Shortcuts/{ShortcutCatalogue,ShortcutIds,ShortcutCopy}.cs`, `Shell/ShellServices.cs`; `src/Autonogrammer.Presentation/Settings/IKeyboardSettingsSource.cs`.
- `src/Autonogrammer.UI/Components/Shell/{TopBar,ViewTabs,WorkbenchView,ShortcutHost,AppShell}.razor`, `wwwroot/scripts/{autonogrammer-keys,theme-boot}.js`; `apps/Autonogrammer.Web/Pages/Index.razor`, `Services/WebWorkbench.cs`.
- `src/Autonogrammer.Harness.AgentLoop/Sessions/AgentSessionRegistry.cs`, `AgentSessionRegistryOptions.cs`; `src/Autonogrammer.Presentation/Kanban/KanbanBoardModel.cs`; `services/Autonogrammer.Api/Endpoints/{CatalogEndpoints,SessionEndpoints}.cs`, `Services/{SessionRegistry,CatalogProjector}.cs`; `docs/api/openapi.json`.
- Tests that must still pass: `tests/Autonogrammer.Native.Tests/AgentLoop/KanbanHeadTests.cs`, `ShellHarness.cs` (`MainViewOf`, line 97, changes to go through `SurfaceHost`), `AccessibilityNameTests.cs`, `KeyboardFocusTests.cs`, `Settings/Head/SettingsEntryTests.cs`; `tests/Autonogrammer.ArchitectureTests/{HeadMotionTests,ProjectGraphTests,WebShellAccessibilityTests}.cs`; `tests/Autonogrammer.Presentation.Tests/{PresentationIsolationTests.cs,Kanban/KanbanBoardModelTests.cs}`; `tests/Autonogrammer.UI.Tests/{CssContractTests,ShellRegionTests,WorkbenchViewTests}.cs`; `tests/Autonogrammer.Web.Tests/OpenApiCoverageTests.cs`.

</canonical_refs>

<code_context>
## Code Context

### Reusable Assets
- `AgentSessionRegistry` (`AgentSessionRegistry.cs:14`, `MaxSessions = 200` line 17, `RecoverAfterRestart` line 329, `DetectLost` line 336) wrapped, not forked.
- `FileSettingsStore` member pattern (`MotionMember` line 36, `SetMotion` line 77) for `shell.surface`; `theme-boot.js` pattern for the `localStorage` read.
- `ViewTabs.razor` (tablist, roving tabindex) and the `segment-track`/`segment` classes for the two switches.

### Established Patterns
- No DI container on Native: hand-wired objects; views are explicit `case`s in `ViewMapping.Create` (`ViewMapping.cs:39-67`).
- Presentation is framework-free (`PresentationIsolationTests`); a model needed by both heads lives there.
- Missing optional services remove a feature (no board, no Kanban tab; `ShellServicesOf(null)`), the model for "no Cowork services, no switch".
- Scaffolded projects ship `AssemblyMarker.cs` only; later units are file-disjoint (manifest s4 step 8).

### Integration Points
- `MainWindow.Content` becomes `SurfaceHost`; `HeadSettings.Attach` and `ShellHarness.MainViewOf` follow.
- `CatalogEndpoints` board reads `CodeBoardSource`; new `services/Autonogrammer.Api/Cowork/` module registered in `Program.cs`.
- Created by earlier phases, not on disk yet: `SessionSurface` and the `surface` column and `ListByOwnerAsync` (68/scaffold), `IAccountSession` (67), `Autonogrammer.Cowork` project and `Contracts/Cowork` folder (scaffold step). Created here: everything under `Cowork/`, `Shell/Surface*`, `Presentation/Cowork`.

</code_context>

<deferred>
## Deferred Ideas and omissions

- **OMITTED BY DECISION:** a Kanban tab or card for Cowork; a discriminator on `AgentSessionRecord` with a filtered `Snapshot` (Option B rejected for Native: every consumer must filter); a single `SessionKind`-style discriminator (`SessionKind` stays Chat/Terminal); a server-stored Cowork transcript (summary rows only, OQ-324); a third region inside `MainView`; a new colour role. (ADR-0032 D1 and D2; index s2)
- Cowork scheduled tasks and project memory/sharing: ADR-0037 (call `CoworkSessionHost` themselves). Starting a Cowork task from web or mobile: ADR-0036; until then the Web empty state says "New task" is desktop-started.
- Chat core, tool host, profile: phase 70. Artifact and browser dock panels: phases 71 and 72 register their own `IDockPanel`.
- Open, default in force (non-blocking): OQ-320 browsers may reserve Ctrl+Shift+1/2 (keep them; the visible switch is the sure path); OQ-324 server keeps summary rows only; OQ-325 local files kept and hidden on sign-out, deleted on account deletion (ADR-0030 runbook); OQ-326 200 per account on desktop and per owner on server. OQ-323 (cap on running engines) and OQ-327 to OQ-329 belong to phase 70.

</deferred>

---

*Phase: 69-surface-switch-and-cowork-sessions*
*Context gathered: 2026-10-28*
