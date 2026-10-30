# Phase 69 Validation Record

Source: 69-RESEARCH.md Validation Architecture table, made consistent with the six plans.
No test is run here; executors run the commands. Every command uses the serialising wrapper /tmp/ag-bin/ag-dotnet (never bare dotnet). Service-gated rows need bash scripts/test-services.sh up first (throwaway TimescaleDB via podman).

| Requirement | Test class | Project | Command |
| --- | --- | --- | --- |
| AG-69-1 | SurfaceHostTests (switch visible only with Cowork services; SurfaceHost holds both views) | tests/Autonogrammer.Native.Tests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*SurfaceHostTests*" |
| AG-69-2 | SurfaceSwitchTests + ShortcutCatalogueTests (Ctrl+Shift+1/2 switch and focus toggle); SurfaceTabsTests (tablist roles, arrows, Home, End, shortcut ids); AccessibilityNameTests | tests/Autonogrammer.Native.Tests, tests/Autonogrammer.UI.Tests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*SurfaceSwitchTests*"; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.UI.Tests -c Release -- --filter-class "*SurfaceTabsTests*" |
| AG-69-3 | SurfaceSettingsTests (shell.surface round trip, bad-value error state); SurfacePersistenceTests (localStorage round trip, restore-only-when-available) | tests/Autonogrammer.Native.Tests, tests/Autonogrammer.UI.Tests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*SurfaceSettingsTests*"; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.UI.Tests -c Release -- --filter-class "*SurfacePersistenceTests*" |
| AG-69-4 | SurfaceSwitchTests (visibility-only, Code turn keeps streaming); SurfaceActivityModelTests (counts + polite announcement) | tests/Autonogrammer.Native.Tests, tests/Autonogrammer.Presentation.Tests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*Surface*" ; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*SurfaceActivityModelTests*" |
| AG-69-5 | CoworkWorkbenchTests (/cowork route, signed-out/empty/error states); CssContractTests, ShellRegionTests, WebShellAccessibilityTests | tests/Autonogrammer.UI.Tests, tests/Autonogrammer.Web.Tests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.UI.Tests -c Release; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Web.Tests -c Release |
| AG-69-6 | CoworkSessionRegistryTests (separate file, per-account folder, 200 cap + message, RecoverAfterRestart only Cowork rows, quarantine); CoworkAccountFolderTests; CoworkSessionMetaStoreTests; CoworkProjectStoreTests | tests/Autonogrammer.Cowork.Tests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release |
| AG-69-7 | CatalogSeparationTests (no type implements ISessionCatalog); CoworkSurfaceIsolationTests (no dual-catalog type) | tests/Autonogrammer.Cowork.Tests, tests/Autonogrammer.ArchitectureTests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release -- --filter-class "*CatalogSeparationTests*"; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*CoworkSurfaceIsolationTests*" |
| AG-69-8 | CoworkShellModelTests; CoworkSessionListModelTests; PresentationIsolationTests; KeyboardFocusTests (F6 across three regions) | tests/Autonogrammer.Presentation.Tests, tests/Autonogrammer.Native.Tests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*Surface*" |
| AG-69-9 | CoworkSessionEndpointsTests; AccountBoardSurfaceTests | tests/Autonogrammer.Api.IntegrationTests | bash scripts/test-services.sh up, then /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release |
| AG-69-10 | CoworkSurfaceIsolationTests; CoworkKanbanIsolationTests | tests/Autonogrammer.ArchitectureTests, tests/Autonogrammer.Native.Tests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*CoworkSurfaceIsolationTests*" and -- --filter-class "*ProjectGraphTests*"; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*CoworkKanbanIsolationTests*" |
| AG-69-11 | CoworkSessionSyncTests (scripted ICoworkSessionApi: 5 s debounce, latest-per-session, capped backoff, Not-synced mark) | tests/Autonogrammer.Cowork.Tests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release -- --filter-class "*CoworkSessionSyncTests*" |
| AG-69-12 | CoworkChromeSlotTests (entry once, both surfaces, keyboard reachable); Surface fragment chrome cases in CoworkWorkbenchTests | tests/Autonogrammer.Native.Tests, tests/Autonogrammer.UI.Tests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*CoworkChromeSlotTests*"; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.UI.Tests -c Release |
| AG-69-13 | CoworkDockHostTests (two panels two tabs, one panel no strip, strip keyboard reachable and named); IDockPanel contract via 69-02 | tests/Autonogrammer.Native.Tests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*CoworkDockHostTests*" |

## Quick run (no services)

- /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release
- /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release

## Full run (ROADMAP success criteria order)

1. /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*CoworkSurfaceIsolationTests*" and -- --filter-class "*ProjectGraphTests*"
2. Steps for AG-69-6/7/11 (Cowork.Tests) and AG-69-4/8 (Presentation.Tests) above
3. /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*Surface*" and -- --filter-class "*CoworkKanbanIsolationTests*"; existing KanbanHeadTests, HeadMotionTests, SettingsEntryTests unchanged apart from the harness helper
4. /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.UI.Tests -c Release; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Web.Tests -c Release
5. bash scripts/test-services.sh up, then /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release
6. Build and licence gates (main agent): /tmp/ag-bin/ag-dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror; python3 scripts/legal/generate-third-party-notices.py --check (no new package)

## Gates that cannot run on this host

Windows/macOS rendering and Meta shortcuts (compile only); Wayland focus; screen readers (automation names/roles/live-region text only); whether a browser reserves Ctrl+Shift+1/2; live Supabase (fake ICoworkAccountGate); Railway deploys (throwaway containers only).
