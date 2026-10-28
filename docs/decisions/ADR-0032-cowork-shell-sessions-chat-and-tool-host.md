# ADR-0032: Cowork shell, sessions, chat core and tool host

- Status: Proposed 2026-10-28
- Date: 2026-10-28
- Owner: autonogrammer-maintainers
- Deciders: autonogrammer-maintainers
- Scope: milestone v0.16.0 (Autonogrammer). The Code | Cowork surface switch in the Native and Web heads, the Cowork session model and the rule that a Cowork session never creates, updates or appears on the Kanban board, the Cowork layout and view models, the Cowork chat core on omp over ACP, and the Cowork tool host: the one seam through which Artifacts (ADR-0033), Browser Use (ADR-0034) and Computer Use (ADR-0035) give tools to omp. Not in scope: accounts and sign-in (ADR-0030), the data services (ADR-0031), the Dispatch relay (ADR-0036), the Cowork features not listed here (ADR-0037). Research: `docs/plans/v0.16.0-research/` reports 02, 03, 06, 07 and 09. Briefs: `briefs/69-mode-shell-and-cowork-sessions.md` and `briefs/70-cowork-chat-core.md`.
- Naming: every name is written in its post-rename form (OmpOS becomes Autonogrammer, `OmpOS.Native` becomes `Autonogrammer.Native`) although the tree on disk still uses the old names; repo paths are written the same way. The bare token `omp`, `omp acp`, `~/.omp`, `OMP_*` and `models.yml` keep their names. "Surface" is the word for Code or Cowork, because "mode" is already used for run mode, custom mode, approval mode and motion (report 02, section 11). The type is `SessionSurface` {Code, Cowork} in `Autonogrammer.Contracts.Sessions` (named by ADR-0030; `SessionKind` is Chat or Terminal and stays).
- Vocabulary for the other ADRs of the milestone: the window-level mode host asked for by the task text is `SurfaceHost`; the Cowork session service is `CoworkSessionHost`; the shell mount point for a right-hand pane (artifact dock, browser pane) is `IDockPanel` (a content contract, D3); `/cowork/*` is the API route prefix, of which this ADR owns only `/cowork/sessions` (ADR-0033 owns `/cowork/artifacts/...` and `/cowork/artifact-render/{ticket}`); the Cowork features of ADR-0033 to ADR-0037 are configured under the `Cowork:` option section (`Artifacts`, `BrowserUse`, `ComputerUse`, `Projects`, `Memory`), and platform modules keep `Dispatch`, `Accounts`, `DataPlane`.
- Owner decisions taken as given: Cowork needs sign-in and Code does not; the Cowork agent is local (omp over ACP) plus the Dispatch relay; Computer Use commands always need local approval on the desktop; Windows and macOS code is compile-checked only.

## Context

- The Native head has no mode concept. Its window is `MainWindow` holding one `MainView` (`apps/Autonogrammer.Native/MainWindow.axaml.cs:24-25`); `HeadSettings.Attach` throws unless `owner.Content is MainView` (`apps/Autonogrammer.Native/Settings/HeadSettings.cs:173-176`); the Agents | Kanban tab switch hides regions and keeps state (`src/Autonogrammer.Native.Views/MainView.axaml.cs` `SetKanbanShown`, `MainView.axaml:90-103`); shortcuts Ctrl+Shift+A, Ctrl+Shift+K, Ctrl+1, Ctrl+2 are taken (`MainView.axaml.cs:791-810`, `Views/Shortcuts/ShortcutCatalogue.cs:16-44`). `MainView` handles `Alt` by returning early (`MainView.axaml.cs:776-779`).
- A Kanban card is a catalog row: `AgentSessionRegistry` is the only writer of rows (`src/Autonogrammer.Harness.AgentLoop/Sessions/AgentSessionRegistry.cs:111`), its `Snapshot()` feeds `SessionCatalogModel`, and `KanbanBoardModel.SyncEntries` turns every `_catalog.Sessions` row into a card (`src/Autonogrammer.Presentation/Kanban/KanbanBoardModel.cs:328-355`). The registry holds at most 200 sessions (`AgentSessionRegistry.cs:17`, `:121`) and `RecoverAfterRestart` marks every Running row Lost (`:329`). `SessionEngineSet` opens exactly one registry at `<stateRoot>/sessions.json` (`apps/Autonogrammer.Native/Agents/SessionEngineSet.cs:1079-1088`) and is Code-specific: planner, plugins, Substrate, GSD, stage sources, Kanban details (`:429-487`, `:530-572`).
- On the server the same projection exists: `GET /sessions` and `GET /board` read ids from the in-process `SessionRegistry` (`services/Autonogrammer.Api/Services/SessionRegistry.cs:9-23`) and hydrate them (`Endpoints/CatalogEndpoints.cs:60-113`); `Session` has no kind or surface (`src/Autonogrammer.Contracts/Sessions/Session.cs:8-33`). ADR-0030 adds the column `surface` and the routes `GET /account/sessions` and `GET /account/board`.
- The MCP gateway exists but is wired into no head (report 07, section 4.3). `McpGatewayServer.Start(McpToolRegistry, McpToolInvoker)` listens on `127.0.0.1` with a per-start bearer secret and serves `/mcp/{sessionId}` (`src/Autonogrammer.Harness.McpGateway/McpGatewayServer.cs:74-96`); the dispatcher answers `initialize`, `ping`, `tools/list`, `tools/call` and refuses a call to a tool the session is not granted before any upstream is reached (`McpGatewayDispatcher.cs:184-190`, `:281-287`); it lists only `name`, `description`, `inputSchema` (`:108-130`); the approval hook is a boolean delegate (`src/Autonogrammer.Harness.Mcp/McpToolApproval.cs:10`). Tools can only come from upstream `McpClient`s today (`McpToolRegistry.RegisterServerAsync`, `McpToolRegistry.cs:47`).
- omp 18.12.5 (source, nothing run, per ADR-0010): `omp acp` is the root launch flow with `mode=acp`, so launch flags parse (`src/commands/acp.ts:18-34`); `--append-system-prompt`, `--system-prompt` and `--tools` exist (`src/cli/flag-tables.ts:179-186`, `src/main.ts:1700-1703`); a prompt argument that is an existing file path is read, anything else is literal text (`src/system-prompt.ts:285-299`); ACP sessions connect only the MCP servers of `session/new.mcpServers` (ADR-0010 table, `acp-agent.ts:2668-2730`) and a failed server fails `session/new`; an MCP tool is named `mcp__<server>_<tool>` after both parts are lower-cased, every other character run becomes one `_`, and a tool part that starts with `<server>_` loses that prefix (`src/mcp/tool-bridge.ts:464-494`); omp's own `browser.enabled` defaults to true and `computer.enabled` to false (`src/tools/browser/settings.ts:7-10`, `src/tools/settings.ts:602-606`); a tool-call update carries an image as `{type: "content", content: {type: "image", data, mimeType}}` (`src/modes/acp/acp-event-mapper.ts:895-913`).
- The transcript drops images: `ToolUpdateContent` reads text and diffs only (`src/Autonogrammer.Contracts/Ui/ToolUpdateContent.cs:11`, `:69-94`).

## Decision

### D1. The surface switch

- Native: a window-level `SurfaceHost` (UserControl in `Autonogrammer.Native.Views`, layer L3) becomes `MainWindow.Content` and holds two children: the existing `MainView` (Code) and a new `CoworkView`. `MainView` is not extended with a third top-level region: its keyboard and focus code is wired to Agents | Kanban and it is 1705 lines. `MainWindow` keeps its constructor shape and gains an optional `CoworkSurfaceServices`; a window built without it shows no switch, exactly as `MainView` shows no Kanban tab until a board is supplied (`MainView.axaml.cs:284-290`) and as `ShellServicesOf(null)` omits features that need services (`MainWindow.axaml.cs:30`). `HeadSettings.Attach` resolves the `MainView` through `SurfaceHost.Code` when the content is a host; `new MainView(` call sites keep their `MotionPreference` argument (HeadMotionTests).
- The switch control is one two-item segmented control (existing `segment-track` and `segment` classes). Each surface owns a slot in its own top bar (`MainView` gets one `ContentControl` in the unused column 2 of `TopBar`; `CoworkView` has the same bar), both bound to one `SurfaceSwitchModel`, so the switch sits in the title bar and no second bar costs vertical space.
- Web: a new `SurfaceTabs.razor` in `Autonogrammer.UI` goes into a new `Surface` render fragment of `TopBar.razor` (before `Tabs`). Code keeps `ViewTabs` (Agents | Kanban); Cowork replaces the `ompos-view-panel` body and the `Rail` fragment (`WorkbenchView.razor:23-32`). Routes: `/` is Code or the last surface; `/cowork` selects Cowork and is the deep-link target of notifications (ADR-0036). A switch calls `NavigationManager.NavigateTo(..., replace: true)`.
- Accessible semantics: Web `role="tablist"` labelled "Workspace" with two `role="tab"` buttons "Code" and "Cowork", `aria-selected`, `aria-controls` the one panel, roving `tabindex`, ArrowLeft/ArrowRight/Home/End like `ViewTabs.razor:2-23` and `:66-82`, `aria-keyshortcuts`. Native: two `ToggleButton`s in one group with `AutomationProperties.Name` "Code, workspace" and "Cowork, workspace", the selected one announced by the toggle state. The activity chip (D1 below) is a `role="status"` live region on Web and an `AutomationProperties.LiveSetting="Polite"` text on Native.
- Shortcuts: Ctrl+Shift+1 shows Code and Ctrl+Shift+2 shows Cowork (Meta on macOS, like Ctrl+K at `MainView.axaml.cs:834`). They are added to `ShortcutIds`, `ShortcutCatalogue`, the Keyboard settings page and `ShortcutHost.razor` / `ompos-keys.js`; every entry gets a test that raises the gesture (the catalogue's own rule, `ShortcutCatalogue.cs:7-10`). Whether a browser reserves Ctrl+Shift+1 is not verified (OQ-320).
- Persistence: Native writes the member `shell.surface` (`"code"` or `"cowork"`, default `"code"`) to `config/settings.json` through a new `SettingsShellSection` and `FileSettingsStore.SetSurface` (pattern: `FileSettingsStore.cs:36-40`, `:186-216`); an unusable value is the existing error state. Web writes `localStorage` key `autonogrammer.surface`, read before first paint like `theme-boot.js`. The stored surface is restored only when Cowork is available (signed in, ADR-0030); otherwise the head opens Code without an error and Cowork shows its signed-out state when chosen.
- A switch only changes visibility. Nothing running in the other surface is stopped, paused or disposed: both stacks stay alive (the Agents | Kanban precedent, `MainView.axaml.cs` class remark on kept panes). `SurfaceActivityModel` (Presentation) counts, per surface, running sessions, sessions waiting for a decision and failed sessions, and the switch shows a chip on the hidden surface ("Cowork, 2 running" or "Code, needs approval") announced politely; an approval that waits in the hidden surface is reachable by the shortcut and times out by D6. A session is cancelled only by the user's explicit Stop or Close, by app exit (existing behaviour: running turns are found Lost at the next start), or by sign-out, which stops Cowork engines after a confirmation (the sessions stay in the registry).
- Styling reuses the existing tokens: a class `surface-cowork` on the host root with selectors in `Views/Shell/SurfaceStyles.axaml`, and `data-surface` on the Web shell root with `wwwroot/styles/surface.css`. No colour role is added (a new role needs Light and Dark values, a `ThemeContrastTests` class and a `tokens.css` mirror; report 02, section 5). One size token, `Autonogrammer.Size.CoworkDockWidth` (420) and its CSS twin, is added.
- Chrome slot: each surface's top bar ends in a `HeaderEndSlot` bound to one `SurfaceChromeModel` that other ADRs fill with entries (the account menu of ADR-0030, the Devices entry of ADR-0036); `SurfaceHost` owns the model and the placement, so the entries sit in the same place in Code and Cowork.

### D2. The Cowork session model and the Kanban rule

Compared options (report 02, sections 9 and 10):

| Option | Strength | Weakness | Verdict |
| --- | --- | --- | --- |
| A. A second registry and a second catalog type for Cowork | Cowork rows are not in the `ISessionCatalog` the board reads, so no filter can be forgotten; own file, own 200-session cap, own `RecoverAfterRestart`; Code files and `KanbanHeadTests` stay as they are | A second host to compose | Native head: chosen |
| B. A discriminator on `AgentSessionRecord` with a filtered `Snapshot` | One registry | Every consumer of `Snapshot()` and `SnapshotRecords()` must filter, including a future one; one shared cap and one recovery pass; automations, forks and cloud writers must each state the surface (report 02, section 10) | Rejected for Native |
| C. A `surface` column on the server `sessions` table with typed queries | One table and one events hypertable, no second schema; ADR-0030 already names the column | A list without the filter would leak | Server: chosen, with the rules below |

Native rules:

1. `CoworkSessionRegistry` (project `Autonogrammer.Cowork`, L2) owns a private `AgentSessionRegistry` at `<stateRoot>/cowork/<account>/sessions.json` with `AgentSessionRegistryOptions.StateRoot = <stateRoot>/cowork/<account>`, so session folders are `<stateRoot>/cowork/<account>/sessions/<id>`. `<account>` is the account folder key supplied by `ICoworkAccountGate` (the first 20 characters of the lower-case base32 of SHA-256 of the account's `sub` claim, computed by the Native head from `IAccountSession`; never the `sub` itself): two accounts on one desktop never see each other's tasks, and signing out leaves the files in place. It reuses the atomic write, quarantine, 200-session cap and Lost recovery of the existing class and is not an `ISessionCatalog`.
2. Cowork has its own contract types in `Autonogrammer.Contracts.Cowork` (L0): `CoworkSessionSummary`, `ICoworkSessionCatalog`. No type implements both `ICoworkSessionCatalog` and `ISessionCatalog`; no Cowork row is a `SessionSummary` that any catalog can hold.
3. The seams and what each does: `AgentSessionRegistry` stays Code-only and is never given a Cowork row; `SessionCatalogModel` and `KanbanBoardModel` are unchanged and are constructed only by `AgentHead.Build` and `WebWorkbench`; the Cowork rail uses `CoworkSessionListModel` (Presentation) over `ICoworkSessionCatalog`; the automations binding (`HeadAutomationSessions`, `AgentHead.cs:323`) stays bound to the Code host and creates Code sessions only (Cowork scheduled tasks belong to ADR-0037 and call `CoworkSessionHost` themselves); `SessionForker` stays Code-only.
4. The cap is 200 Cowork sessions in the Cowork file, independent of Code. `CoworkSessionHost` calls `RecoverAfterRestart` once when it first opens its registry and `DetectLost` on its own maintenance timer, so a crash in one surface never rewrites the other's rows. A full registry shows the specific message "You have 200 Cowork tasks. Delete or archive one to start another."
5. Cowork metadata that the registry record has no field for (project, trusted folders, memory-off flag, pinned artifact ids) lives in `<stateRoot>/cowork/<account>/session-meta.json` (`CoworkSessionMetaStore`, owner-only mode, atomic write, same rules as `FileSettingsStore`) and projects in `<stateRoot>/cowork/<account>/projects.json` (`CoworkProjectStore`: id, name, folder, instructions). Memory, sharing and scheduling of projects are ADR-0037.

Server rules (with ADR-0030 and the repository brief 68):

1. `ISessionRepository` has no unfiltered list. The only list is `ListByOwnerAsync(string ownerId, SessionSurface surface, bool includeClosed, int limit, SessionCursor? cursor, CancellationToken cancellationToken)` (ADR-0031 D4) with `surface` required, non-nullable and no default. The column is `surface text NOT NULL DEFAULT 'code'`, so existing rows are Code.
2. `CodeBoardSource` (new, `services/Autonogrammer.Api/Services/CodeBoardSource.cs`) is the one place that calls the list with `SessionSurface.Code` for the board, and `GET /account/board` and any Kanban projection read only through it. `GET /account/sessions` makes two typed calls and labels each row with its surface. `POST /sessions` always writes Code.
3. Cowork rows on the server are summary rows (empty `Messages`, no events) written by the desktop with `PUT /cowork/sessions/{id}` and removed with `DELETE /cowork/sessions/{id}` in `services/Autonogrammer.Api/Cowork/`; the transcript stays on the desktop and a web or mobile head reads it over the Dispatch relay (ADR-0036). A per-owner cap of 200 Cowork rows answers 409.

Tests that make the rule unbreakable (architecture tests in `Autonogrammer.ArchitectureTests`, behavioural tests beside them):

- `CoworkSurfaceIsolationTests` (ArchUnitNET and reflection): no type implements both catalog interfaces; no type in `Autonogrammer.Presentation.Kanban` or `Autonogrammer.Harness.AgentLoop.Sessions` references `Autonogrammer.Contracts.Cowork`, `Autonogrammer.Cowork` or `Autonogrammer.Presentation.Cowork`; `KanbanBoardModel` is constructed only in `AgentHead`, `WebWorkbench` and tests; `ISessionRepository` declares no method that returns sessions without a `SessionSurface` parameter; `Autonogrammer.Api.Cowork` does not use `SessionRegistry` or `CatalogProjector`.
- `CoworkKanbanIsolationTests` (Native.Tests): a head with 3 Code sessions and a Cowork registry with 5 rows has a board with exactly 3 cards before and after creating, updating and closing a Cowork session; the Cowork file and the Code file are different files; an automation run never lands in the Cowork registry; the two caps and the two `RecoverAfterRestart` passes are independent.
- API: with a Cowork row in the throwaway TimescaleDB, `GET /account/board` is empty, `GET /account/sessions` lists both rows labelled, and `GET /sessions` is unchanged.

### D3. Cowork layout and view models

- Regions (Native `CoworkView`, Web `CoworkWorkbench`): a left rail (tasks, then projects; filter box; "New task"), the conversation (header with title and status, transcript with streaming, composer with attachments), and a right artifact dock (collapsible, `Size.CoworkDockWidth`, a `GridSplitter` on Native and a resizer on Web). There is no Kanban tab and no Agents | Kanban strip in Cowork. F6 cycles rail, conversation, dock; skip link "Skip to conversation".
- Presentation (framework-free, `Autonogrammer.Presentation.Cowork`): `CoworkShellModel` (owns the three parts and the dock state), `CoworkSessionListModel` (rows of `CoworkSessionRow`, grouped by project, filter, selection), `CoworkConversationModel` (composes the existing leaf models `SessionViewModel` and `ComposerViewModel`, an approval queue of `PendingToolApproval` and the provider status chips), `SurfaceSwitchModel`, `SurfaceActivityModel`, `CoworkCopy`. `IDockPanel` is a content contract, not a slot: `CoworkDockHost` (brief 69 unit 3) hosts a registered list of panels with a tab strip and one visible panel, and each provider (Artifacts, ADR-0033; Browser Use, ADR-0034) registers its own panel. The artifact dock model is ADR-0033's.
- `Autonogrammer.Native.Views`: `Shell/SurfaceHost`, `Shell/SurfaceSwitchView`, `Views/Cowork/CoworkView`, `CoworkRailView`, `CoworkConversationView`, `CoworkComposerView`, registered as new `case`s in `ViewMapping.Create` (`ViewMapping.cs:39-67`). `Autonogrammer.UI`: `Components/Shell/SurfaceTabs.razor`, `Components/Cowork/*`. The two renderers share the models, as for Code.
- The conversation reuses `AgentSessionRuntime` (public, `Native.Views/Sessions`) with no-op stage, context and steering-record sources and a Cowork-built `AgentRow` whose `Card` is null; the Code timeline, task context and steering parts are simply not rendered. The runtime owns the streaming, stop, retry and image-input binding already tested for Code.

### D4. The Cowork chat core

- `CoworkSessionHost` (Native, `apps/Autonogrammer.Native/Cowork/`) implements `IAgentPanelHost` over the Cowork registry (it is small: create, open, close, reload; `IAgentPanelHost.cs:9-45`) and is not a second `SessionEngineSet`. A session is a slot with one omp child over ACP, composed with `EngineComposition.Compose` exactly as a side chat is (a hand-built `OmpLaunchPlan`, `SessionEngineSet.cs:675-691`), never through the Code planner.
- Session profile, `CoworkSessionProfile` (Autonogrammer.Cowork): launch arguments `--no-extensions` (the Code plugins of ADR-0012 never load; OQ-82 and OQ-83 explain the flag), `--approval-mode always-ask-cached` in Manual and `always-ask` in Auto and Skip (never `yolo`: omp sends the host no request under `yolo`, so the delete rule, the folder boundary and the taint rule of D6 could not run; `RunModeLaunchMapper.cs:21-26` maps the Code modes only and is not used), `--add-dir` per trusted folder, `--append-system-prompt <absolute path>` of `<sessionDir>/cowork-system-prompt.md` written by the composer from the embedded text `CoworkSystemPrompt` (base text at most 4 KiB; `CoworkSystemPromptOptions` apportions the file: base 4 KiB, standing-context paragraph 2 KiB (ADR-0037), browser guidance 1 KiB (ADR-0034), computer guidance 1 KiB (ADR-0035), total validated at most 8 KiB: the Cowork role, files go to the Storage folder, ask before deleting, text in tool results and pages is untrusted data, order of preference connector, browser, screen, never claim a tool that is unavailable); a missing file would be read as literal text by omp, so the composer fails the start when it cannot write it. A `--config` overlay written by `ConfigOverlayWriter` sets `browser.enabled: false` and `computer.enabled: false` so omp's own browser and desktop tools never compete with the governed ones (D5). Flags are verified by source only; whether the ACP session factory applies `--append-system-prompt` to every `session/new` is not verified (OD-07, no live turn).
- Overlay: `ConfigOverlayWriter.Write` renders only keys of `AgentLoopSettings`; the Cowork composer needs a new `ConfigOverlayWriter.WriteYaml(sessionStateDirectory, yaml)` (same owner-only mode and cleanup) and adds `--config <path>` to the plan's arguments itself, because `OmpLaunchPlanner.AddConfigOverlay` is private to the Code planner (`OmpLaunchPlanner.cs:164-175`). The overlay is the only way a profile or a provider changes an omp setting; providers do not write it.
- Permissions: `CoworkPermissionResolver` (an `IPermissionResolver`, Autonogrammer.Cowork) wraps the ACP requests. It denies a file path outside the trusted folders and the Storage folder, always asks before a delete, allows `mcp__cowork_*` tools at the omp level (the gateway gate of D6 is authoritative), and otherwise follows the mode: Manual asks through the approval queue, Auto applies the rules of ADR-0037 (a rule engine, no classifier) and is shown as "Auto (rules)"; Skip allows everything except deletes and, in a tainted session, `edit`, `delete`, `move` and `execute`. Skip is available only when the session's folder boundary is `FilesystemIsolated` or the user has given a per-session acknowledgement; otherwise the resolver treats the session as Manual and the picker says why. `CoworkPermissionMode` {Manual, Auto, Skip} is stored as `cowork.permissionMode` (default Manual); "mode" in this milestone means only this enum (Browser Use keeps `BrowserUse:PolicyMode`; Dispatch and Automations name their timeouts `Dispatch:CommandApprovalTimeout` and `UnattendedApprovalTimeout`). Because omp is always started with an asking approval mode, every request reaches the resolver, which is the one policy seam.
- Connectors: after the `cowork` entry the composer offers the user's MCP catalog servers through `McpServerCatalog.OfferedServers()`, as Code does (`SessionEngineSet.cs:604`); a catalog server named `cowork` is dropped with a notice. These tools bypass the tool host, so `CoworkPermissionResolver` is their only gate (permissions-file `Mcp(...)` rules, Manual asks, and in a tainted session they count as actuating and are asked about even in Skip). Remote and cloud sessions are not offered catalog servers (OQ-160 default).
- Turn operations: send, stop (cancel the ACP turn), retry the last prompt, steer (native steering channel of `AgentSessionRuntime`), history (the omp session is resumed with `session/load` from `ResumeReference`, `AgentSessionRegistry.cs:193`), attachments: files and folders become `PromptResource`s and images become `PromptImage`s (`PromptOptions.cs:9-13`), refused with the message of `SupportsImages` when the model reads none.
- Listing: the registry change feeds `ICoworkSessionCatalog`; a `CoworkSessionSync` debounces (5 s) and pushes `PUT /cowork/sessions/{id}` summaries; offline it keeps the latest summary per session and retries. The account surface lists them as Cowork sessions (D2 server rules).
- Images in the transcript: `ToolUpdateContent` gains `Images` (`IReadOnlyList<ToolUpdateImage>`; `ToolUpdateImage(string MimeType, string DataBase64, string? Description)`, in `Autonogrammer.Contracts.Ui`) read from the `content` entries of type `image`; `GenUi` and `Sessions` folds produce an `ImageBlock`; the transcript views, the export and the web projection render it with alt text. This ADR (brief 70, unit 5) owns this change for ADR-0034 and ADR-0035. Every caller of the record is updated (no compatibility constructor).

### D5. The tool host contract

One seam: `ICoworkToolProvider` implementations register with `CoworkToolHost`, which `McpGatewayServer` serves as a loopback MCP server handed to omp in `session/new.mcpServers`.

```csharp
namespace Autonogrammer.Cowork.Abstractions; // project Autonogrammer.Cowork.Abstractions, L1, references Autonogrammer.Contracts only

public enum CoworkTurnOrigin { Local = 0, Dispatch = 1 }
public enum CoworkToolEffect { ReadOnly = 0, Actuating = 1, Destructive = 2 }
public enum CoworkApprovalRequirement
{
    ByPolicy = 0,                    // the permission mode and stored grants decide
    EveryCall = 1,                   // ask on every call in every mode; no grant satisfies it
    EveryCallOnExecutingDevice = 2,  // ask on the machine that runs the tool, every call, every origin, every mode (Computer Use access grant)
    ByProviderGate = 3,              // the host neither asks nor skips: the provider's own gate decides through the broker it is given (per-app grants, sensitive-action asks); the host still applies Unavailable = deny, MustBeLocal for every answer the provider requests, audit and events
}
[Flags] public enum CoworkResultKinds { Text = 1, Image = 2 }
public enum CoworkProviderAvailability { Available = 0, NeedsSetup = 1, Unavailable = 2 }

public sealed record CoworkToolDescriptor
{
    public required string Name { get; init; }        // ^[a-z][a-z0-9]*(_[a-z0-9]+)*$, 3 to 40 chars, starts with ProviderId + "_", never "__", never starts "cowork_"
    public required string ProviderId { get; init; }  // ^[a-z][a-z0-9]{1,19}$: "artifact", "browser", "computer"
    public required string Description { get; init; } // model-facing, at most 1024 chars
    public required JsonElement InputSchema { get; init; }   // JSON Schema, type object
    public required CoworkToolEffect Effect { get; init; }
    public required CoworkApprovalRequirement Approval { get; init; }
    public CoworkResultKinds Results { get; init; } = CoworkResultKinds.Text;
}

public sealed record CoworkSessionContext
{
    public required string SessionId { get; init; }
    public required string WorkingDirectory { get; init; }                  // absolute
    public required IReadOnlyList<string> TrustedFolders { get; init; }     // absolute
    public required bool ModelReadsImages { get; init; }
}
public sealed record CoworkToolInvocation(string SessionId, string CallId, string ToolName, JsonElement Arguments, CoworkTurnOrigin Origin);
public sealed record CoworkGrantKey(string Scope, string Value);            // ("browser.origin", "https://example.com")
public sealed record CoworkProviderState(CoworkProviderAvailability Availability, string? Reason);
public sealed record CoworkCallPreview(string Headline, string? Target, IReadOnlyList<string> Details, CoworkGrantKey? Grant); // Target is what the HOST observed (URL, window title), never model text

public abstract record CoworkContentBlock;
public sealed record CoworkTextBlock(string Text, bool IsUntrusted) : CoworkContentBlock;
public sealed record CoworkImageBlock(ReadOnlyMemory<byte> Data, string MimeType, string Description, bool IsUntrusted) : CoworkContentBlock; // png, jpeg or webp; a page or screen capture is untrusted
public sealed record CoworkToolResult(IReadOnlyList<CoworkContentBlock> Content, bool IsError)
{
    public static CoworkToolResult Text(string text, bool isUntrusted = false);
    public static CoworkToolResult Failure(string message);                 // fixed text, never an exception message
}

public interface ICoworkToolProvider
{
    string ProviderId { get; }
    IReadOnlyList<CoworkToolDescriptor> Describe();
    ValueTask<CoworkProviderState> GetStateAsync(CoworkSessionContext session, CancellationToken cancellationToken);
    ValueTask<CoworkCallPreview> PreviewAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken);
    ValueTask<CoworkToolResult> InvokeAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken);
    ValueTask EndSessionAsync(string sessionId, CancellationToken cancellationToken);   // release the profile, pages, grants of the session
}

public interface ICoworkSessionLease : IAsyncDisposable
{
    void SetTurnOrigin(CoworkTurnOrigin origin);   // set by the chat core per prompt; never by the model
    bool IsTainted { get; }                         // true once any result of the session carried untrusted content; never reset
}

public interface ICoworkToolHost   // what the gateway serves
{
    ICoworkSessionLease BeginSession(CoworkSessionContext session);
    event EventHandler<CoworkToolEvent>? ToolEvent;   // raised on the caller's context: started, approval requested, completed, failed
    ValueTask<IReadOnlyList<CoworkToolDescriptor>> ListOfferedAsync(string sessionId, CancellationToken cancellationToken);
    ValueTask<CoworkToolResult> InvokeAsync(string sessionId, string toolName, JsonElement arguments, CancellationToken cancellationToken);
}

public interface ICoworkPolicyGate { ValueTask<CoworkPolicyDecision> DecideAsync(CoworkPolicyRequest request, CancellationToken cancellationToken); }
public enum CoworkPolicyOutcome { Allow = 0, Ask = 1, Deny = 2 }
public sealed record CoworkPolicyRequest(CoworkSessionContext Session, CoworkToolDescriptor Tool, CoworkToolInvocation Invocation, CoworkCallPreview Preview, bool SessionTainted);
public sealed record CoworkPolicyDecision(CoworkPolicyOutcome Outcome, bool MustBeLocal, string? Reason);
public enum CoworkToolEventKind { Started = 0, ApprovalRequested = 1, Completed = 2, Failed = 3, Denied = 4 }
public sealed record CoworkToolEvent(string SessionId, string CallId, string ProviderId, string ToolName, CoworkToolEventKind Kind, string? Target);

public interface ICoworkApprovalBroker { ValueTask<CoworkApprovalDecision> RequestAsync(CoworkApprovalRequest request, CancellationToken cancellationToken); }
public sealed record CoworkApprovalRequest(string SessionId, string CallId, string ToolName, CoworkCallPreview Preview, CoworkTurnOrigin Origin, bool MustBeLocal);
public enum CoworkApprovalOutcome { Allowed = 0, AllowedAlways = 1, Denied = 2, TimedOut = 3, Unavailable = 4 }
public sealed record CoworkApprovalDecision(CoworkApprovalOutcome Outcome);

public interface ICoworkGrantStore
{
    ValueTask<bool> IsGrantedAsync(CoworkGrantKey key, CancellationToken cancellationToken);
    ValueTask GrantAsync(CoworkGrantKey key, CancellationToken cancellationToken);
    ValueTask RevokeAsync(CoworkGrantKey key, CancellationToken cancellationToken);
}

public interface ICoworkToolAudit { ValueTask RecordAsync(CoworkToolAuditEntry entry, CancellationToken cancellationToken); } // implemented by the local owner-only hash-chained JSONL `FileCoworkToolAudit` (the `ComputerAuditLog` pattern, brief 70 unit 1); nothing from it goes to the ledger
public sealed record CoworkToolAuditEntry(string SessionId, string CallId, string ProviderId, string ToolName, CoworkTurnOrigin Origin,
    CoworkPolicyOutcome Policy, CoworkApprovalOutcome? Approval, bool IsError, CoworkResultKinds ResultKinds, TimeSpan Duration, string? Target); // no arguments, no result bytes
```

Gateway change (`Autonogrammer.Harness.McpGateway`, L2, now also referencing the L1 abstractions): `public static McpGatewayServer Start(McpToolRegistry registry, McpToolInvoker invoker, ICoworkToolHost? coworkTools = null)`. `tools/list` returns the registry's granted tools (names `server__tool`) followed by `ListOfferedAsync` (names verbatim); `tools/call` resolves a name against the registry grants first and the host second. The two name sets cannot meet: a registry name always contains `McpGatewayToolNames.Separator` (`__`, `McpGatewayToolNames.cs:14`), a Cowork name never does (descriptor validation). A Cowork result maps to `CallToolResult`: `CoworkTextBlock` to `TextContentBlock`, `CoworkImageBlock` to `ImageContentBlock.FromBytes(data, mimeType)` (verified in ModelContextProtocol.Core 2.3.0 documentation). The Cowork head runs its own gateway instance with an empty registry.

Naming that omp shows: the ACP entry is named `cowork`, so `artifact_create` reaches the model as `mcp__cowork_artifact_create` (28 characters; the 64-character cap leaves 52 for a tool name, the limit is 40). Underscores collapse, so two names that differ only in underscore runs would collide; descriptor validation forbids `__` for that reason.

`CoworkToolHost` (Autonogrammer.Cowork) `InvokeAsync` runs this sequence for every call and never skips a step: (1) the session lease exists and the tool is offered (provider available, profile admits the provider, image tools only when `ModelReadsImages`); (2) arguments are a JSON object of at most 256 KiB; (3) `PreviewAsync` reads host state; (4) `ICoworkPolicyGate.DecideAsync`; (5) on Ask, `ICoworkApprovalBroker.RequestAsync` (timeout `CoworkToolHostOptions.ApprovalTimeout`, default 120 s, which maps to a denial the model reads as "not approved"); (6) `InvokeAsync` of the provider under `CallTimeout` (default 120 s) and the call token; (7) result caps: text 65,536 characters, at most 2 images, each at most 3 MiB, 6 MiB in all, PNG, JPEG or WebP only; an over-cap result becomes a failure result, nothing is truncated silently; (8) `ICoworkToolAudit.RecordAsync`, which never fails the call. A provider exception becomes `CoworkToolResult.Failure` with a fixed text and a log line without arguments. One call runs at a time per session.

### D6. Policy, approval and audit

- Default `CoworkPolicyGate`: `EveryCallOnExecutingDevice` gives Ask with `MustBeLocal = true` in every mode and from every origin; a broker answer `Unavailable` (no desktop window) is a denial. `EveryCall` gives Ask in every mode. `ByPolicy`: Skip allows and Auto follows its rules; Manual allows any non-`Destructive` call whose `Preview.Grant` names a grant that is stored (`AllowedAlways`) and asks otherwise. The artifact provider returns `Preview.Grant = ("artifact.write", "cowork")`, so the first artifact write asks once and "Allow always" persists it (ADR-0033 D4). A tainted session still asks for every `Actuating` call whatever grants are stored. Never allowed by any rule: a call whose provider reports `Unavailable`. A stored grant satisfies `ByPolicy` only, never `EveryCall` or `EveryCallOnExecutingDevice`, is never used for a `Destructive` call, and is created only by an `AllowedAlways` answer.
- Taint (the host side of the rule of two): a session is tainted the first time any tool result in it carries a text or image block with `IsUntrusted`. In a tainted session Auto and Skip no longer apply: the gate asks for every `Actuating` or `Destructive` call, and `CoworkPermissionResolver` treats `edit`, `delete`, `move` and `execute` as Manual. Taint is permanent for the session; a new task starts clean. A session whose providers read untrusted input has no secrets in reach: the composer builds its environment from `ProviderKeyEnvironment` only (no Substrate token, hook bridge, API token or account token), and `CoworkPathBoundary` refuses `<stateRoot>/secrets`, `~/.omp`, `~/.ssh` and the account folders as trusted folders.
- `ByProviderGate` (accepted at the request of ADR-0035, so a click and type vision loop is usable): the host gate returns Allow once the provider reports Available, never Ask; the provider receives an `ICoworkApprovalBroker` at construction and must ask through it, with `MustBeLocal = true`, for its own grants and sensitive actions in every mode including Skip and in a tainted session. The descriptor validator refuses `ByProviderGate` on a tool with `Effect = Destructive`. The host cannot tell whether the provider asked, so the shared provider contract tests of brief 70, unit 1 assert it for each provider. Taint adds no per-call ask for `ByProviderGate` tools; it still ends Skip for `EveryCall` and `ByPolicy` actuating tools and for omp's own `edit`, `delete`, `move` and `execute`.
- Approval UI: `CoworkApprovalBroker` (Native) raises a `PendingToolApproval` in the session's approval queue (the existing card, with the host-observed `Target` and the `Details`, not model text), decided through an `IToolApprovalBridge` implementation that completes the broker's wait (`IToolApprovalBridge.cs:7-16`). A decision made on another device (Dispatch) never satisfies `MustBeLocal` (ADR-0036 and ADR-0035).
- Prompt-injection stance for the host: tool results from pages and screens carry `IsUntrusted`; the gateway wraps them in a labelled envelope; providers still filter and neutralise their own content (ADR-0034, ADR-0035); the taint rule above is the host's enforcement and a provider cannot switch it off.

### D7. Projects, layers and registration

| New project | Layer | References | Notes |
| --- | --- | --- | --- |
| `Autonogrammer.Cowork.Abstractions` | L1 | Contracts | D5 contract only; no packages |
| `Autonogrammer.Cowork` | L2 | Contracts, Cowork.Abstractions, Harness.Abstractions, Harness.AgentLoop | `SameLayerAllowed` entry `("Autonogrammer.Cowork", "Autonogrammer.Harness.AgentLoop")` with an `// intentional:` comment (the registry wraps `AgentSessionRegistry`); no process type used, so the NoProcess ban holds |
| existing `Autonogrammer.Harness.McpGateway` | L2 | + Cowork.Abstractions | lower layer, no allow-list entry |
| existing Contracts, Presentation, UI, Native.Views, Native, Web, Api | unchanged | | new namespaces `Contracts.Cowork`, `Presentation.Cowork`, `Api.Cowork` |

The scaffold step (`briefs/00-scaffold-manifest.md`) creates and registers every new project (13-step checklist of report 09, section 2: LayerMap, slnx, slnf, locks, csproj references) before any phase starts; the briefs of this ADR add code only. The Cowork port `ICoworkAccountGate` (signed in, account folder key) is owned by `Autonogrammer.Cowork`; the Native head implements it over ADR-0030 `IAccountSession`, so `Autonogrammer.Cowork` references neither `Autonogrammer.Accounts` nor Presentation types of it. `Autonogrammer.Web` references neither `Autonogrammer.Cowork` nor Harness projects (the ProjectGraph rule); the Web Cowork surface uses Contracts and Presentation only.

## Blocked, and not verified

- A live omp turn through the gateway (OD-07) is not approved: that `session/new` connects to the gateway, that `--append-system-prompt` applies, that the overlay disables omp's browser and that an approval wait survives omp's MCP request timeout (the default is not read) are all feasible by source only.
- The Windows and macOS heads, the macOS Meta shortcuts and the WebKit or Wayland behaviour of the new views are compile-checked or headless-checked only.
- Live Supabase and the account state (ADR-0030) are not reachable on this host; the Cowork gate uses a fake `ICoworkAccountGate` in tests.
- The API cannot be deployed (Railway deploys paused); the server rules are verified against throwaway local containers.

## Consequences

- `MainWindow`, `HeadSettings.Attach`, `ShellHarness` and the tests that call `MainViewOf(window)` change to go through `SurfaceHost`; `MainView` itself gains one slot and nothing else.
- Code behaviour, `KanbanHeadTests`, `KanbanBoardModelTests` and the Code files on disk are untouched.
- `ToolUpdateContent` changes shape (additive for the contract version: `Contracts/Ui` is not a plugin contract under ADR-0008 section 7, and the scaffold step bumps `ContractVersion` to 1.6.0 once; this ADR edits no version); `GenUi`, `Sessions`, the Api projections and the export are updated in the same change.
- ADR-0033, ADR-0034 and ADR-0035 implement `ICoworkToolProvider` and must not add a second route to omp.

## Candidate chosen

A window-level `SurfaceHost` over the existing `MainView` and a new `CoworkView`; a physically separate Cowork registry (`CoworkSessionRegistry`) with its own contract types for the Native head and a `surface` column with surface-typed queries (`ListByOwnerAsync(ownerId, surface, includeClosed, limit, cursor, ct)`, `CodeBoardSource`) on the server; a `CoworkSessionHost` over `IAgentPanelHost` composing omp over ACP with a Cowork profile; and `ICoworkToolProvider` plus `CoworkToolHost` served by an extended `McpGatewayServer`. No new package.

## Licence

| Component | SPDX | Source | Verified |
| --- | --- | --- | --- |
| `Autonogrammer.Cowork.Abstractions`, `Autonogrammer.Cowork`, the Cowork views and models | AGPL-3.0-or-later | https://www.gnu.org/licenses/agpl-3.0.html | Project code under ADR-0018 |
| `ModelContextProtocol` 2.3.0 (existing pin, `Directory.Packages.props:74`) | Apache-2.0 | https://github.com/modelcontextprotocol/csharp-sdk | `<license type="expression">Apache-2.0</license>` in the local NuGet cache nuspec, 2026-10-28; first use of `ImageContentBlock.FromBytes` checked in the package documentation |
| `Avalonia` 12.1.4 (existing pin, `Directory.Packages.props:21`) | MIT | https://github.com/AvaloniaUI/Avalonia | `<license type="expression">MIT</license>` in the local NuGet cache nuspec, 2026-10-28 |
| omp (`@oh-my-pi/pi-coding-agent` 18.12.5, read as source, not redistributed) | MIT | https://github.com/can1357/oh-my-pi/blob/main/LICENSE | Recorded in ADR-0010 |

## Verification route

`dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*CoworkSurfaceIsolationTests*"`, `dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release`, `dotnet test --project tests/Autonogrammer.Harness.McpGateway.Tests -c Release`, `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release`, `dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*Surface*"`, `dotnet test --project tests/Autonogrammer.UI.Tests -c Release` and `dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release` (the last needs `scripts/test-services.sh up`). A live gateway turn follows ADR-0010 "Next verification step" after the owner records approval under OD-07 (`docs/open-questions.md`).

## Open risks

- The gateway is feasible by source only; a gateway outage fails `session/new` (ADR-0010 Consequences), so the composer health-checks it with an `initialize` over loopback first and starts the session without Cowork tools, with a visible degraded notice, when the check fails.
- Approval waits can outlive omp's client-side MCP request timeout (not read); the 120 s default may need to be lower (OQ-321).
- `--append-system-prompt` and the overlay keys are verified by source only; if omp ignores them in ACP, the Cowork profile silently equals Code's prompt and omp's own browser stays on (OQ-322).
- Ctrl+Shift+1 and Ctrl+Shift+2 may be reserved by some browsers (OQ-320).
- Two engine sets (Code and Cowork) double the omp children a power user can hold; no cap on running Cowork sessions is set here (OQ-323).
- The server `surface` column and the typed list belong to briefs 67 and 68; this ADR's rule fails if either ships an unfiltered list. The architecture test is the guard.
