# 76-VALIDATION: phase 76 validation map

Scope: phase 76 (Cowork parity B, AG-76-1 to AG-76-14). Plans 76-01 to 76-05, 76-07, 76-08 run in wave 1; 76-06 runs last in wave 2 and owns the phase-end gate.

## Requirement to test-class map (Nyquist)

| Requirement | Covering test class | Plan |
|---|---|---|
| AG-76-1 install and enable | BundleInstallerTests, BundleSectionModel via PluginsPageTests | 76-01 |
| AG-76-2 hostile install refused | BundleValidatorTests (traversal, symlink, size, count, manifest, zip-slip, zip-bomb fixtures) | 76-01 |
| AG-76-3 skills, commands, agents, MCP, hooks-listed | BundleLaunchContributorTests, BundleMcpSourceTests, BundlePrecedenceTests | 76-01 |
| AG-76-4 per-skill switch, create-plugin | BundleLaunchContributorTests (--skills filter), skill manifest test, BundlePrecedenceTests | 76-01 |
| AG-76-5 connector card and check | McpConnectionCheckerTests, ConnectorMatrixModelTests, ConnectorsPageViewTests | 76-02 |
| AG-76-6 Auto mode | CoworkAutoModeTests, PermissionModesModelTests | 76-03 |
| AG-76-7 Skip gate | CoworkSkipAcknowledgementTests | 76-03 |
| AG-76-8 delete plus Mcp rules (write half) | ConnectorMatrixModelTests (rules written) | 76-02 |
| AG-76-8 delete plus Mcp rules (read half) | ShellDeleteDetectorTests, CoworkResolverMcpRuleTests | 76-03 |
| AG-76-9 folders and boundary | CoworkPathBoundaryTests, FolderGrantModel via PermissionsPageViewTests | 76-03 |
| AG-76-10 isolation | BubblewrapArgvBuilderTests, SeatbeltProfileBuilderTests, IsolationProbeTests, AcpStdioTransportWrapperTests, IsolationLiveTests (gated) | 76-04 |
| AG-76-11 sub-agent panel | SubagentPanelModelTests, AcpTaskToolActivitySourceTests, SubagentsPanelViewTests | 76-05 |
| AG-76-12 usage | FileUsageLedgerTests, UsageMeterModelTests, UsageMeterViewTests | 76-07 |
| AG-76-13 notifications | NotificationPolicyTests, NotificationCommandTests, NotificationCentreModelTests, NotificationsViewTests | 76-08 |
| AG-76-14 parity graded | ProjectGraphTests, RepoHygieneTests, SuppressionLedgerTests, composition tests | 76-06 |
| Secret canary (76-02 threat model) | ConnectorSecretCanaryTests | 76-02 |

Every AG-76 id is covered. No id is unplannable. Deferred items (hooks running, marketplace, network fence, seccomp, Windows filesystem isolation, Windows toast, phone push, RPC sub-agent source, OAuth vault, Accept-edits, classifier) appear in no test.

## Quick commands (per plan, cheapest first)

Run from /root/src/repos/ompos-autonogrammer through the serialising wrapper /tmp/ag-bin/ag-dotnet, never bare dotnet:

- 76-01: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.PluginBundles.Tests -c Release -- --filter-class "*BundleValidatorTests*"
- 76-02: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.Mcp.Tests -c Release -- --filter-class "*McpConnectionCheckerTests*"
- 76-03: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release -- --filter-class "*CoworkAutoModeTests*"
- 76-04: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.Isolation.Tests -c Release -- --filter-class "*BubblewrapArgvBuilderTests*"
- 76-05: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*SubagentPanelModelTests*"
- 76-07: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*FileUsageLedgerTests*"
- 76-08: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.Notifications.Tests -c Release -- --filter-class "*NotificationPolicyTests*"
- 76-06: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*ProjectGraphTests*"

Each runnable has a paired fails_when in its plan: non-zero exit, 0 passed or No tests ran in the output, or the plan-named observable (golden drift, canary marker, empty history, missing limit sentence).

## Full commands (phase-end, 76-06)

- /tmp/ag-bin/ag-dotnet restore Autonogrammer.Linux.slnf --locked-mode
- /tmp/ag-bin/ag-dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror -nodeReuse:false -m:1
- /tmp/ag-bin/ag-dotnet test --project tests/NAME -c Release --no-build --filter-not-trait "Category=Live" for Autonogrammer.Harness.PluginBundles.Tests, Autonogrammer.Harness.Isolation.Tests, Autonogrammer.Harness.Notifications.Tests, Autonogrammer.Cowork.Tests, Autonogrammer.Harness.AgentLoop.Tests, Autonogrammer.Harness.Mcp.Tests, Autonogrammer.Harness.Acp.Tests, Autonogrammer.Harness.Workspace.Tests, Autonogrammer.Presentation.Tests, Autonogrammer.Native.Tests, Autonogrammer.ArchitectureTests
- Live, only where bwrap --version works and user namespaces are allowed: AUTONOGRAMMER_LIVE_ISOLATION=1 /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.Isolation.Tests -c Release --no-build --filter-trait "Category=Live" (without the variable the tests skip, never fail)
- bash scripts/ci/repo-checks.sh; bash scripts/ci/check-licences.sh; /tmp/ag-bin/ag-dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore
- ./bootstrap.sh is the full gate and is an owner action recorded in docs/runbooks/cowork-isolation.md, never run by the planner or executor

## Wave 0 gaps (fixtures and seams that must exist before behaviour work)

- Scaffold 66.1: Harness.PluginBundles, Harness.Isolation, Harness.Notifications projects plus T17-T19 test projects with AssemblyMarker.cs only, LayerMap rows, ProcessSpawningAssemblies entries, slnx and slnf entries, locks, ContractVersion 1.6.0. Every plan gates on git ls-files first and STOPS if absent.
- Phases 69 and 70: CoworkPermissionMode, CoworkPermissionResolver, CoworkSessionProfile, CoworkPathBoundary, CoworkSessionMetaStore, CoworkToolHost, CoworkToolEvent and the phase-70 profile test asserting the old Auto mapping. 76-03 gates on their symbols and STOPS if absent.
- Phase 75-06: ILaunchContributor, LaunchRequest, LaunchContribution and docs/parity/cowork-parity.md. 76-01, 76-04 and 76-06 gate on them and STOP if absent.
- This phase creates: zip-slip, zip-bomb and symlink fixtures; golden bwrap argv and Seatbelt profile files; FakeProcessRunner; recorded task frames with and without agent names; assets/skills/create-plugin/SKILL.md; AcpStdioTransportWrapperTests.
