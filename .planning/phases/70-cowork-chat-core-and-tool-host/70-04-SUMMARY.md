# 70-04 Summary

Cowork Native host: session host, engine composer, one loopback tool server, approval broker and bridge, and CoworkHead composed from AgentHead.

## What landed

- `EngineCompositionOptions` gained `EnvironmentMode` (default Inherit), `EnvironmentAllowList` (default empty) and `PermissionResolverFactory`. `EngineComposition.Compose` copies the first two and installs the factory. Combining the factory with `RunModePolicy` throws `ArgumentException`. Defaults leave Code composition unchanged.
- `CoworkEngineComposer` builds an `OmpLaunchPlan` from `CoworkSessionProfile` and composes with `EngineEnvironmentMode.Allowlist`, provider keys only, all ten ACP kinds, and `CoworkPermissionResolver` over the pending resolver. Windows extras are named; Linux and macOS pass an empty extra list. A catalog server named `cowork` raises a notice.
- `CoworkToolServer` starts one `McpGatewayServer` per head. A failed or closed health check returns `Cowork tools are unavailable:` plus the reason. The bearer is only on the session entry, not `ToString`.
- `CoworkApprovalBroker` waits on `CoworkToolHostOptions.ApprovalTimeout`, records host-observed target and details, and refuses a remote decision for `MustBeLocal`.
- `CoworkSessionHost` implements create, open, close and reload. `RecoverAfterRestart` runs once. `CreateTerminalSessionAsync` throws `NotSupportedException`. Send, retry and steer set `CoworkTurnOrigin.Local` and ignore a model `origin` argument.
- `CoworkHead` is created in `AgentHead.Build` and disposed before `Host.DisposeAsync`. `App.CreateWindow` uses `CoworkSurfaceServicesFactory`. `CoworkView.BindConversation` shows `ViewMapping.Create` for the selected row.
- Native gained a project reference to `Autonogrammer.Harness.McpGateway`.

## Tests

Targeted Native filter (session host, composer, tool server, broker, EngineCompositionOptions) was run with analyzers off because sibling Presentation files (`CoworkApprovalQueue`, `CoworkConversationModel`, `ProviderStatusChip`) fail CA1711 and MA0048, and two unrelated test files warn CS8604. Passed 17/17: CoworkSessionHostTests, CoworkEngineComposerTests, CoworkToolServerTests, CoworkApprovalBrokerTests, EngineCompositionOptionsTests. Command used -p:RunAnalyzersDuringBuild=false -p:TreatWarningsAsErrors=false because sibling Presentation analyzer errors and unrelated CS8604 warnings otherwise fail the build.

Broad phase commands (Cowork.Tests, McpGateway.Tests, Presentation, ArchitectureTests, slnf, notices) were not all run. ProjectGraphTests was not run in this turn.

## Gaps

- `CoworkConversationModel` arrived from 70-06 while this plan was in progress and does not yet compile under analyzers. The host conversation delegate returns the broker card list (`object?`) until that model is the bound type.
- Steering is recorded as a local-origin turn; the runtime's private native steering channel is not invoked.
- Screen-reader and Windows path behaviour are source-verified only. Windows allow-list extras are named, not executed.
- A live omp turn is not started.
