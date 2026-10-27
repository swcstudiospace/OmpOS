# Settings parity: the 34 Cursor rows and what OmpOS does with each

Source of the rows: `docs/plans/vision-research/cursor-settings-window.md` (research of 2026-10-22, 34 rows). Owner decision of 2026-10-26 (ROADMAP DR-6): owner decisions 1 and 2 of that research were answered (a), a Cursor-shaped Settings window with per-provider fields in its Models section. Requirement: AL-20 (the table is one of the inventories that track Cursor parity) and VX-05. Phase records: `.planning/phases/40-settings-window-and-model-providers/`.

Written 2026-10-26 from the scout plan of the same day, checked row by row against the code at commit `1587dd5`. Where the code differs from the scout plan, the table follows the code (see "Where the scout plan and the code differ").

## What exists

The Settings window is a separate, non-modal window (OQ-89), opened from the rail footer button and with Ctrl+, (`SettingsWindowHost`). It has eleven pages, in this order (`apps/OmpOS.Native/Settings/SettingsWindowFactory.cs`):

| Page | What it holds | Class (page model) | Head backing |
|------|---------------|--------------------|--------------|
| Runtime | Idle stop (default 24 hours, 1 to 720) and completed-session retention (default 7 days, 1 to 90). Not a Cursor row: it is the VX-05 Runtime section. | `Presentation/Settings/RuntimePage.cs`, `RuntimeSettingsModel.cs` | `apps/OmpOS.Native/Settings/RuntimeSettingsBinding.cs`, `SessionLifecycleSweeper.cs`, `FileSettingsStore.cs` |
| General | Theme (System, Light, Dark; added by Phase 50), Motion (follow system, reduce, full) and a read-only privacy statement. | `GeneralPage.cs` | `GeneralSettingsSource.cs`, `FileSettingsStore.cs` |
| Agents | Terminal auto-run switch (off by default) and a note that every tool call follows omp's approval policy. | `AgentsPage.cs` | `AgentsSettingsSource.cs` (`ToTerminalSettings`), `Harness.Terminal/TerminalSettings.cs` |
| Models | Providers, keys, default model, custom provider form, templates, test connection. | `Settings/Models/ModelsSettingsPage.cs`, `ProviderCardModel.cs`, `CustomProviderDraft.cs` | `apps/OmpOS.Native/Settings/Models/ModelsSettingsSource.cs` over `Harness.OmpConfig`, `Harness.Providers`, `Harness.Secrets` |
| Tab | Read-only status of inline completion: on or off, the provider's host and port. | `TabPage.cs` | `TabSettingsSource.cs`, `Harness.Completion/CompletionSettingsLoader.cs` |
| Rules | Read-only list of the rule files in `.ompos/rules`. | `Customize/RulesSettingsPage.cs` | `RulesSettingsSource.cs`, `WorkspaceSnapshotReader.cs`, `Harness.Workspace` |
| Skills | Read-only list of the skill files in `.ompos/skills`. | `Customize/SkillsSettingsPage.cs` | `SkillsSettingsSource.cs`, `WorkspaceSnapshotReader.cs` |
| Commands | Read-only list of the slash commands. | `Customize/CommandsSettingsPage.cs` | `Customize/SlashCommandsSettingsSource.cs` over `Presentation/Commands/SlashCommandRegistry` |
| Plugins | Read-only list of the four plugins; there is deliberately no switch (OQ-80). Not a Cursor row on its own: Cursor's plugin page is row 13. | `PluginsPage.cs` | `PluginsSettingsSource.cs` |
| Network | Run diagnostics button and its result: one unauthenticated GET per known provider endpoint, run only on request, cancellable. Not a request OmpOS makes by itself. | `NetworkSettingsPage.cs`, `INetworkDiagnosticsSource.cs`, `EndpointCheck.cs` | `NetworkDiagnosticsSource.cs` over `Harness.OmpConfig` (`OmpModelsFile.ReadManagedAsync`) and `Harness.Providers` (`ProviderCatalogue`) |
| Keyboard | Read-only list of the shortcuts the shell handles, grouped by scope; remapping is omitted by design. | `KeyboardSettingsPage.cs`, `IKeyboardSettingsSource.cs`, `ShortcutRow.cs` | `KeyboardSettingsSource.cs` over `Native.Views/Views/Shortcuts/ShortcutCatalogue.cs` |

Class paths are under `src/OmpOS.Presentation/Settings/` for the models and `apps/OmpOS.Native/Settings/` for the head. The views are in `src/OmpOS.Native.Views/Views/Settings/`.

There is no Hooks, MCP, Search, Subagents, Beta or Accounts page.

## The 34 rows

Decisions: **Kept as a page**, **Shown inside a page**, **Omitted**. Totals: 8 kept as a page (rows 1, 7, 8, 10, 13, 17, 18, 21), 4 shown inside a page (rows 3, 19, 22, 26), 22 omitted.

| # | Cursor surface | Decision | OmpOS page and backing | Reason |
|---|----------------|----------|------------------------|--------|
| 1 | Settings > Models: provider keys, default model | Kept as a page | Models. `ModelsSettingsSource` reads omp (`omp models --json`, `omp token`), imports a built-in provider's key with `omp auth import -`, sets the default with `omp config set modelRoles`, writes custom providers as OmpOS-owned blocks of omp's `models.yml`; a custom provider's key goes to the owner-only file of `OwnerOnlyFileSecretStore` and reaches omp as an environment variable (`ProviderKeyEnvironment`). | DR-6 asks for per-provider fields. omp is the engine, so providers live in omp (OQ-92). Cursor lists OpenAI, Anthropic, Google, Azure OpenAI and AWS Bedrock; the built-in table (`OmpBuiltInProviders`) has the providers verified against omp 18.12.6 and no Azure OpenAI or Bedrock row. A custom provider has its own base URL; a base-URL override of a built-in provider is not offered. |
| 2 | Team Settings > Models (web dashboard) | Omitted | None. | A web admin surface of Cursor accounts and teams. OmpOS has no accounts or teams. |
| 3 | Agents > Approvals & Execution: Run Mode | Shown inside a page | Agents: the terminal auto-run switch and the approval policy note (`AgentsPage`, `TerminalSettings.AutoRunEnabled`). | Tool approval is omp's policy and OmpOS passes it through. Only the terminal auto-run choice is OmpOS's. The Run Mode choice (Auto-review, Allowlist, Run Everything) and the Auto-review classifier have no backing and are not built. |
| 4 | Agents > Read Access, Read Allowlist, sandbox network mode | Omitted | None. | No read boundary or sandbox network policy exists in OmpOS to configure. |
| 5 | Agents > Context and Tools: Sync Skills for Cloud Agents | Omitted | None. | Cloud agents are gated by the owner's sign-in decision (OD-03). Project skills are listed on the Skills page (row 8). |
| 6 | Customize (umbrella page) | Omitted | The umbrella is not a page; its children are pages (rows 7, 8, 10, 13). | OmpOS has one left navigation. The scope filter, team scope and leaderboard need teams and a marketplace. |
| 7 | Customize > Rules | Kept as a page | Rules: read-only list of `.ompos/rules` files (`RulesSettingsSource`). | Lists what the workspace holds. Rule types, Cursor's `.cursor/rules` format, an editor and team enforcement are not built. |
| 8 | Customize > Skills | Kept as a page | Skills: read-only list of `.ompos/skills` files (`SkillsSettingsSource`). | Lists project skills. A built-in skill catalogue, personal skill sync and publishing to a team are not built. |
| 9 | Customize > Subagents | Omitted | None. | Subagent delegation is done by omp and the Swarm plugin. The Plugins page lists Swarm; no list of subagents or editor exists. |
| 10 | Customize > Commands | Kept as a page | Commands: read-only list of the slash commands (`SlashCommandsSettingsSource`). | The catalogue is the one the composer's slash palette offers. No storage path or editor is verified. |
| 11 | Customize > MCP | Omitted from Settings | The MCP tool registry has its own view in the Tools area of the main window (`Views/Mcp/McpToolsView`, `McpToolRegistrySource`). | MCP configuration is a tool surface, not a setting. Per-server network modes and an enterprise allowlist have no backing. The scout plan listed an MCP page; the code has none. |
| 12 | Customize > Hooks | Omitted | None. A hooks view model and view exist (`Presentation/Hooks/HooksViewModel`, `Views/Hooks/HooksView`), but no code outside them creates one: not the main view, not `ViewMapping`, not a head. | No product head wires the hook host (OQ-84). The scout plan listed a Hooks page; the code has none. |
| 13 | Customize > Plugins and Marketplace | Kept as a page | Plugins: read-only list of Ultrathink, Swarm, Substrate and GSD-Core with found or missing, version and pinned path (`PluginsPage`, `PluginsSettingsSource`). | The four plugins are always on (DR-5c, OQ-80), so there is nothing to install or switch. Marketplaces, scopes, installation modes and import need accounts and a store. |
| 14 | Settings > Indexing & Docs | Omitted | None. Workspace search is a tool of the main window (`WorkspaceSearchModel`), not a setting. | Only forum posts describe this page (row marked unverified). OmpOS ships text and regex search, no index to configure. |
| 15 | @Docs | Omitted | None. | Unverified in primary sources and reported as sunset. No backing. |
| 16 | Settings > Beta | Omitted | None. | No primary source lists its contents (row marked unverified). The research's owner decision 8 has no recorded answer; DR-6 records only decisions 1 and 2. |
| 17 | Settings > Tab | Kept as a page | Tab: read-only status (`TabPage`, `TabSettingsSource`, `CompletionSettingsLoader`; the endpoint comes from `OMPOS_COMPLETION_ENDPOINT` or the file named by `OMPOS_COMPLETION_CONFIG`; inline completion is off unless one is set). | The one real inline-completion setting is its endpoint, which is configured outside the window. Snooze, global disable, per-extension disable and key remapping are not built. |
| 18 | Settings > Network: Run Diagnostics, HTTP compatibility mode | Kept as a page | Network: a Run diagnostics button and the result of the latest run (`NetworkSettingsPage`, `NetworkDiagnosticsSource`). The endpoints are the base URLs of the providers OmpOS manages in omp's `models.yml` (read through `OmpModelsFile.ReadManagedAsync`) and the base endpoint of every `ProviderCatalogue` row that has one, one per host and port. Each gets ONE unauthenticated GET (no header, body, key, query or user information; 5 s limit; at most 3 redirects). Any HTTP status counts as Reachable and is shown; a refused or unresolved connection is Unreachable, a missed limit Timed out, a failed secure connection TLS failed. | Diagnostics only, run on the button and never at startup; the page says what is sent. The HTTP compatibility mode has no backing in omp or OmpOS and is not built. The Models page keeps its own Test connection for local endpoints. |
| 19 | General: Privacy Mode | Shown inside a page | General: a read-only privacy statement (`SettingsCopy.PrivacyStatement`: OmpOS sends nothing anywhere itself; requests go from omp with the providers and credentials the user configured). | There is no telemetry or training setting to toggle, so no toggle is offered. |
| 20 | General: workspace trust | Omitted | None. | No restricted mode or policy enforcement exists. |
| 21 | Settings > Keyboard Shortcuts | Kept as a page | Keyboard: a read-only list of the shortcuts the shell handles, grouped by scope (Application, Agents, Composer, Kanban, Settings) (`KeyboardSettingsPage`, `KeyboardSettingsSource`, `ShortcutCatalogue`). Every catalogued gesture is raised in a headless shell by `ShortcutCatalogueTests` and its effect observed; a gesture with no such test cannot be listed. | Remapping is omitted by design: a remap screen needs a binding store that does not exist, and the research's owner decision 10 has no recorded answer. The page lists the fixed map and says so. |
| 22 | Settings > Appearance: theme, fonts | Shown inside a page | General: Theme (System, Light, Dark, default Dark) and Motion (`GeneralPage`, `FileSettingsStore`; Phase 50 added the Theme list). | Fonts are fixed by the `OmpOS.Font.*` tokens (DM Sans, Anybody, Space Mono). Font size, family and ligature settings are not built. A separate Appearance page is not built. |
| 23 | Agents Window vs IDE layout | Omitted | None. The main window has an Agents tab and a Kanban tab (Ctrl+Shift+A, Ctrl+Shift+K). | A layout, not a setting. |
| 24 | Cloud Agents dashboard settings | Omitted | None. | Web dashboard; gated by the owner's sign-in decision (OD-03) and the paused hosted phases. |
| 25 | Worktrees settings | Omitted | None. A worktree library exists (`Harness.Worktrees`), no setting uses it. | No worktree setup file or cleanup setting is wired to the window. |
| 26 | Terminal (agent terminal tool, sandbox) | Shown inside a page | Agents: the terminal auto-run switch, off by default, with a warning while it is on (`AgentsPage`). | The only terminal policy OmpOS owns. Sandbox limits are omp's. |
| 27 | Browser tool and origin allowlist | Omitted | None. | Allowlist and per-workspace persistence have no backing. |
| 28 | Remote Control, keep awake | Omitted | None. | Gated by the owner's sign-in decision (OD-03) and a second-device decision (OQ-68). |
| 29 | Extensions: cooldown, allowed list, signature check | Omitted | None. | Editor extension compatibility is a deferred item (Phase 30); no stub ships and a test guards that no code names it (`DeferredScopeTests`). |
| 30 | Ignore files (`.cursorignore`) | Omitted | None. | No ignore rules exist in OmpOS and no note or editor was built. |
| 31 | Team and organization admin dashboard | Omitted | None. | Web admin; the admin, analytics and team controls item is deferred (Phase 30). |
| 32 | Accounts, usage and billing | Omitted | None. | OmpOS has no accounts or billing; gated by OD-03. |
| 33 | Desktop account and sign-in page | Omitted | None. | Blocked on the owner's sign-in decision (OD-03); unverified in primary sources. |
| 34 | CLI configuration files | Omitted | None. | Cursor's CLI configuration, not part of its Settings window. OmpOS opens omp in the terminal; omp's own configuration is edited only through the Models page blocks. |

## Where the scout plan and the code differ

The scout plan (2026-10-26, before the build) proposed more pages than were built. The table above follows the code.

| Scout plan | Code |
|------------|------|
| Pages for MCP, Hooks, Search and Network, and a Keyboard page. | MCP and Search are tools of the main window and the Hooks view is not created by any head, so those three are not pages. Network and Keyboard were built afterwards (rows 18 and 21). |
| Subagents as a read-only list inside Agents, and a worktree note in Agents. | Neither exists; Agents holds the auto-run switch and the approval note. |
| Appearance as a page with theme and font scale. | Theme and Motion are on General; there is no font scale. |
| Rules need a `.mdc` loader. | The Rules and Skills pages list `.ompos/rules` and `.ompos/skills` through `Harness.Workspace`. |
| Provider keys in `ISecretStore`, with only an in-memory store available, so a durable OS-backed store was needed. | A built-in provider's key goes to omp (`omp auth import -`) and OmpOS keeps none. A custom provider's key is kept in `OwnerOnlyFileSecretStore`, a plain-text owner-only file, not an OS-backed store. |
| The default model persisted by `ModelSelectionModel`. | The default model is `modelRoles.default` of omp (`OmpDefaultModel`); the composer chip is `ModelPickerModel` over omp's ACP `model` option. |
| Row 16 (Beta): "decision 8 answered (b) per DR-6". | DR-6 records only decisions 1 and 2. Beta is omitted for lack of a verified source. |
| Row 30: a read-only ignore note on a Search page. | No such page or note. |

## Open points

- Remapping of shortcuts and an HTTP compatibility switch are not built; the Keyboard page lists a fixed map and the Network page only diagnoses. The reason is missing backing, not an owner decision.
- No test enumerates the 34 rows. The page-level tests are listed in `.planning/phases/40-settings-window-and-model-providers/40-VERIFICATION.md`.
