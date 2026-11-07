# 76-VALIDATION: phase 76 validation map

Scope: phase 76 (Cowork parity B, AG-76-1 to AG-76-14). Plans 76-01 to 76-05, 76-07, 76-08 run in wave 1; 76-06 runs last in wave 2 and owns the phase-end gate.

## Requirement to test-class map (Nyquist)

| Requirement | Covering test class | Plan |
|---|---|---|
| AG-76-1 install and enable | BundleInstallerTests, BundleSectionModelTests (new class in Presentation.Tests, D-37), BundlesPageViewTests (install, enable and per-skill switch commands; D-48) | 76-01 |
| AG-76-2 hostile install refused | BundleValidatorTests (traversal, symlink, size, count, manifest, zip-slip, zip-bomb archives built by BundleFixtureBuilder at test time) | 76-01 |
| AG-76-3 skills, commands, agents, MCP, hooks-listed | BundleLaunchContributorTests, BundleMcpSourceTests, BundlePrecedenceTests | 76-01 |
| AG-76-4 per-skill switch, create-plugin | BundleLaunchContributorTests (--skills filter), BuiltinSkillManifestTests (existing, one added assertion), ConventionCatalogPluginTests (D-45), BundlePrecedenceTests, BundlesPageViewTests | 76-01 |
| AG-76-5 connector card and check | McpConnectionCheckerTests, ConnectorMatrixModelTests, ConnectorsPageViewTests | 76-02 |
| AG-76-6 Auto mode | CoworkAutoModeTests, PermissionModesModelTests | 76-03 |
| AG-76-7 Skip gate | CoworkSkipAcknowledgementTests | 76-03 |
| AG-76-8 delete plus Mcp rules (write half) | ConnectorMatrixModelTests (rules written) | 76-02 |
| AG-76-8 delete plus Mcp rules (read half) | ShellDeleteDetectorTests, CoworkResolverMcpRuleTests | 76-03 |
| AG-76-9 folders and boundary | CoworkPathBoundaryTests (cases added to the phase-70 class), CoworkFolderGrantStoreTests (D-46), FolderGrantModel via PermissionsPageViewTests | 76-03 |
| AG-76-10 isolation | BubblewrapArgvBuilderTests, SeatbeltProfileBuilderTests, IsolationProbeTests, IsolationPlannerTests (probe, consent, level; D-47), IsolationPageViewTests, IsolationCompositionTests (76-06 wiring), AcpStdioTransportWrapperTests (wrapper after env policy, scrub in Inherit and Allowlist, default options byte-identical; existing AcpLaunchPlumbingTests and AcpEnvironmentIsolationTests stay green), IsolationLiveTests (gated; SUP row in docs/open-questions.md, D-35) | 76-04 |
| AG-76-11 sub-agent panel | SubagentPanelModelTests, AcpTaskToolActivitySourceTests, SubagentsPanelViewTests | 76-05 |
| AG-76-12 usage | FileUsageLedgerTests, UsageMeterModelTests, UsageMeterViewTests, cost cases in AcpUsageAndResourcesTests and the updated OmpLiveVariantContractTests.UsageUpdateBecomesAContextUsageEvent (D-31) | 76-07 |
| AG-76-13 notifications | NotificationPolicyTests, NotificationCommandTests, NotificationCentreModelTests, NotificationsViewTests | 76-08 |
| AG-76-14 parity graded | ProjectGraphTests, RepoHygieneTests, SuppressionLedgerTests, composition tests (including wrapper reaches AcpClientOptions for Cowork and not for Code, tee decorator forwards both IPromptChannel overloads), list locks of D-33 and D-34 (NetworkAndKeyboardCopyTests, SettingsAccessibilityTests, SettingsFontTests, WebSettingsPagesTests, SettingsPagesRenderTests), check-docs.sh and check-skip-markers.sh via repo-checks.sh | 76-06 |
| Secret canary (76-02 threat model) | ConnectorSecretCanaryTests (header, argument, URL path and environment values, D-30) | 76-02 |

Every AG-76 id is covered. No id is unplannable. Deferred items (hooks running, marketplace, network fence, seccomp, Windows filesystem isolation, Windows toast, phone push, RPC sub-agent source, OAuth vault, Accept-edits, classifier) appear in no test.

## Quick commands (per plan, cheapest first)

Run from /root/src/repos/ompos-autonogrammer through the serialising wrapper /tmp/ag-bin/ag-dotnet, never bare dotnet:

- 76-01: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.PluginBundles.Tests -c Release -- --filter-class "*BundleValidatorTests*"
- 76-02: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.Mcp.Tests -c Release -- --filter-class "*McpConnectionCheckerTests*"
- 76-03: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release -- --filter-class "*CoworkAutoModeTests*"
- 76-04: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.Isolation.Tests -c Release -- --filter-class "*BubblewrapArgvBuilderTests*"; then /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.Acp.Tests -c Release -- --filter-class "*AcpStdioTransportWrapperTests*"
- 76-05: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*SubagentPanelModelTests*"
- 76-07: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*FileUsageLedgerTests*"
- 76-08: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.Notifications.Tests -c Release -- --filter-class "*NotificationPolicyTests*"
- 76-06: /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*ProjectGraphTests*"

Each runnable has a paired fails_when in its plan: non-zero exit, 0 passed or No tests ran in the output, or the plan-named observable (golden drift, canary marker, empty history, missing limit sentence).

## Full commands (phase-end, 76-06)

- /tmp/ag-bin/ag-dotnet restore Autonogrammer.Linux.slnf --locked-mode
- /tmp/ag-bin/ag-dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror -nodeReuse:false -m:1
- /tmp/ag-bin/ag-dotnet test --project tests/NAME -c Release --no-build --filter-not-trait "Category=Live" for Autonogrammer.Harness.PluginBundles.Tests, Autonogrammer.Harness.Isolation.Tests, Autonogrammer.Harness.Notifications.Tests, Autonogrammer.Cowork.Tests, Autonogrammer.Harness.AgentLoop.Tests, Autonogrammer.Harness.Mcp.Tests, Autonogrammer.Harness.Acp.Tests, Autonogrammer.Harness.Workspace.Tests, Autonogrammer.Presentation.Tests, Autonogrammer.Native.Tests, Autonogrammer.ArchitectureTests, Autonogrammer.Web.Tests (added: 76-06 edits web settings pages and their list locks, D-34, D-38), Autonogrammer.Harness.ContractTests and Autonogrammer.Harness.Abstractions.Tests (added: 76-07 changes ContextUsageEvent and one live-variant expectation, D-31)
- Live, only where bwrap --version works and user namespaces are allowed: AUTONOGRAMMER_LIVE_ISOLATION=1 /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.Isolation.Tests -c Release --no-build --filter-trait "Category=Live" (without the variable the tests skip, never fail)
- bash scripts/ci/repo-checks.sh (runs check-docs.sh, check-skip-markers.sh, check-changelog.sh and the settings parity check since 66.4); bash scripts/ci/check-licences.sh; /tmp/ag-bin/ag-dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore
- ./bootstrap.sh is the full gate and is an owner action recorded in docs/runbooks/cowork-isolation.md, never run by the planner or executor

## Wave 0 gaps (fixtures and seams that must exist before behaviour work)

- Scaffold 66.1 (on disk, verified 2026-10-29): Harness.PluginBundles, Harness.Isolation, Harness.Notifications projects plus T17-T19 test projects with AssemblyMarker.cs and AssemblyMarkerTests.cs only, LayerMap rows (LayerMap.cs:51-53), ProcessSpawningAssemblies entries, slnx and slnf entries, locks, ContractVersion 1.6.0, and the four Harness.Abstractions seam files. Every plan still gates on git ls-files first and STOPS if absent.
- Phases 69 and 70: CoworkPermissionMode, CoworkPermissionResolver, CoworkSessionProfile, CoworkPathBoundary, CoworkSessionMetaStore, CoworkToolHost, CoworkToolEvent and the phase-70 profile test asserting the old Auto mapping. 76-03 gates on their symbols and STOPS if absent.
- Phase 75-06: the composer logic that applies contributions (including copying LaunchContribution.ProcessWrapper into AcpClientOptions.ProcessWrapper, D-28) and docs/parity/cowork-parity.md; the seam files themselves exist since 66.1 and 76-04 replaces the shape of IProcessLaunchWrapper. 76-01, 76-04 and 76-06 gate on the seams and STOP if absent; 76-06 STOPS if docs/parity/cowork-parity.md is absent.
- This phase creates: BundleFixtureBuilder (zip-slip, zip-bomb and symlink archives built at test time, none committed); golden bwrap argv and Seatbelt profile files; FakeProcessRunner; recorded task tool-call payloads with and without agent names; assets/skills/create-plugin/SKILL.md; AcpStdioTransportWrapperTests; ProcessLaunch.cs and UsageCost.cs in Harness.Abstractions; AcpClientOptions.ProcessWrapper; a SUP row for IsolationLiveTests.
