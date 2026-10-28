# Phase 70: Cowork chat core and tool host - Validation

Source: 70-RESEARCH.md Validation Architecture table, ROADMAP Phase 70 success criteria 1 to 5.

## Requirement to test class to command to failure signal

| Requirement | Test class (project) | Command | Failure signal |
|---|---|---|---|
| AG-70-1 | CoworkSessionProfileTests, CoworkSystemPromptTests (Cowork.Tests/Chat) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release -- --filter-class "*CoworkSessionProfileTests*" | non-zero exit, or any mode argument list differs by one flag, or the prompt file is not owner-only |
| AG-70-2 | CoworkPermissionResolverTests, CoworkPathBoundaryTests (Cowork.Tests/Chat) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release -- --filter-class "*CoworkPermissionResolverTests*" | non-zero exit, or an outside-folder read allowed, or a delete allowed without asking |
| AG-70-3 | CoworkSessionHostTests (Native.Tests/Cowork) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*CoworkSessionHostTests*" | non-zero exit, or RecoverAfterRestart invoked more than once, or a real omp process starts |
| AG-70-4 engine | CoworkSessionHostTests turn sections (Native.Tests/Cowork) | same as AG-70-3 | non-zero exit, or stop/retry/steer/resume not wired to the scripted transport |
| D-17 origin | CoworkSessionHostTests.TurnOriginSetPerPromptAndModelOriginIgnored (Native.Tests/Cowork) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*CoworkSessionHostTests*" | non-zero exit, or the lease origin differs from Local, or a model-supplied origin argument reaches the lease |
| AG-70-4 UI | CoworkConversationTests bUnit (UI.Tests/Cowork) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.UI.Tests -c Release -- --filter-class "*CoworkConversationTests*" | non-zero exit, or any accessibility assertion fails |
| AG-70-5 | ToolBlockContractTests (Contracts.Tests), ToolBlocksFoldTests (UI.Tests), ToolBlockExportTests (Presentation.Tests), fold parity in Sessions.Tests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Contracts.Tests -c Release -- --filter-class "*ToolBlockContractTests*" | non-zero exit, or folds disagree on any image event, or malformed entry throws |
| AG-70-6 | CoworkContractShapeTests (Cowork.Tests/Abstractions) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release -- --filter-class "*CoworkContractShapeTests*" | non-zero exit, or an extra type exists in the Abstractions assembly |
| AG-70-7 | CoworkToolHostTests (Cowork.Tests/Tools) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release -- --filter-class "*CoworkToolHostTests*" | non-zero exit, or provider invoked before gate decision, or over-cap result truncated instead of failed |
| AG-70-8 | CoworkGatewayTests, CoworkGatewayImageTests (McpGateway.Tests) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.McpGateway.Tests -c Release -- --filter-class "*CoworkGatewayImageTests*" | non-zero exit, or image bytes differ by one byte, or a __ name reaches the host |
| AG-70-9 | CoworkPolicyGateTests (Cowork.Tests), CoworkApprovalBrokerTests (Native.Tests/Cowork) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release -- --filter-class "*CoworkPolicyGateTests*" | non-zero exit, or a grant satisfies EveryCallOnExecutingDevice, or a remote decision completes MustBeLocal |
| AG-70-10 | CoworkToolServerTests (Native.Tests/Cowork) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*CoworkToolServerTests*" | non-zero exit, or health-check failure fails the session instead of degrading with the notice |
| AG-70-11 | CoworkConversationModelTests (Presentation.Tests/Cowork), CoworkConversationAccessibilityTests (Native.Tests/Cowork), CoworkConversationTests (UI.Tests/Cowork) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*CoworkConversationModelTests*" | non-zero exit, or focus order wrong, or a colour literal present |
| AG-70-12 | FakeCoworkToolProviderTests (Cowork.Tests/Tools) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release -- --filter-class "*FakeCoworkToolProviderTests*" | non-zero exit, or the base passes for a non-asking ByProviderGate double |
| AG-70-13 host | CoworkToolHostTests taint sections (Cowork.Tests/Tools) | same as AG-70-7 | non-zero exit, or tainted Skip allows an actuating call |
| AG-70-13 resolver | CoworkPermissionResolverTests taint sections (Cowork.Tests/Chat) | same as AG-70-2 | non-zero exit, or tainted edit/delete/move/execute not treated as Manual |
| AG-70-14 overlay | ConfigOverlayWriterTests WriteYaml (AgentLoop.Tests/Launch) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*ConfigOverlayWriterTests*" | non-zero exit, or overlay file not owner-only, or not deleted on dispose |
| AG-70-14 env/boundary | CoworkSessionProfileTests env, CoworkPathBoundaryTests (Cowork.Tests/Chat) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release -- --filter-class "*CoworkPathBoundaryTests*" | non-zero exit, or a non-provider key in the environment, or a symlink escape allowed |
| Broad 5 | ProjectGraphTests (ArchitectureTests) | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*ProjectGraphTests*" | non-zero exit, or a layering violation naming Cowork |
| Broad 5 | warning-free build | /tmp/ag-bin/ag-dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror | non-zero exit or any warning text |
| Broad 5 | notices check | python3 scripts/legal/generate-third-party-notices.py --check | non-zero exit or a new package named |

## Wave 0 gaps

None exist as scaffolds: the red tasks create CoworkContractShapeTests, CoworkToolHostTests, CoworkPolicyGateTests, CoworkAuditGrantStoreTests, FakeCoworkToolProviderTests, CoworkGatewayTests, CoworkGatewayImageTests, CoworkSessionProfileTests, CoworkSystemPromptTests, CoworkPermissionResolverTests, CoworkPathBoundaryTests, WriteYaml cases in ConfigOverlayWriterTests, CoworkSessionHostTests, CoworkEngineComposerTests, CoworkToolServerTests, CoworkApprovalBrokerTests, ToolBlockContractTests image cases, fold parity extensions, CoworkConversationModelTests, CoworkConversationAccessibilityTests and bUnit CoworkConversationTests. No wave-0 scaffold plan is needed; each plan's Task 1 is the failing-first test.

## Live and unverifiable list

No live omp turn runs in any plan (OD-07 not approved): the gateway connection through session/new, --append-system-prompt application, overlay browser.enabled effects, the 120 s approval wait against omp's MCP client timeout (OQ-321), and the mcp__cowork_ name as seen by the model stay source-verified only. No live Supabase sign-in: ICoworkAccountGate is faked in tests. Windows and macOS path rules plus UNC and drive-root cases compile but only Linux executes. Screen-reader behaviour is proved by automated names, focus order and reduced-motion tests only, not by a live reader run.
