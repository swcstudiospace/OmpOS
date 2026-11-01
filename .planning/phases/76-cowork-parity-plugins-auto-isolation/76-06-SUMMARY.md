# 76-06 summary

## What landed

Desktop composition registers bundle, isolation and activity contributors and copies `EngineCompositionOptions.ProcessWrapper` into `AcpClientOptions`. Bundle MCP servers enter `McpServerCatalog` through `IMcpExtraServerSource` switched off. Convention loaders append `PluginRoots` when `BundleComposition.PluginRootsFor` is set. Settings sections Bundles, Connectors, Cowork permissions, Isolation, Usage and Notifications are registered, with web information pages that carry the desktop notice.

## Tests

- `IsolationCompositionTests`, `BundleCompositionTests`, `ActivityCompositionTests`, `NotificationBridgeTests`
- `McpServerCatalogTests.ExtraServersArriveAfterFileServersAndStayOffUntilEnabled`

## Gaps

- `docs/parity/cowork-parity.md` is not in the tree. 75-06 has not landed that file. Rows C3-C6, C11-C13, C23, C24, C26, K04, K10, K16, K19 and K20 were not graded and the file was not created.
- `76-04-SUMMARY.md` and `76-08-SUMMARY.md` were absent. Isolation types were already on disk. `INotificationSink` was not. `NotificationBridge` is the in-app centre in Native and does not reference Harness.Notifications. Scheduled-task kinds have no producer.
- Live isolation, macOS, Windows and `./bootstrap.sh` remain owner actions in `docs/runbooks/cowork-isolation.md`.
- `SettingsWindowTests` was updated so the fixture navigation list still matches. It is outside the plan file list.
