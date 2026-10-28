# Phase 63 Verification: Cursor Feature Completion

```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `63-SUMMARY.md`. A row marked "no recorded run" has a command but no result. The tests are hermetic: headless Avalonia, fake clocks and temp directories, no omp and no network (git and podman fakes where a row says so).

Recorded totals: per-project runs on the final tree before the last wiring passed (recorded 2026-10-28, orchestrator): Presentation 2303, Native 2012 (2 skipped), AgentLoop 1221, Acp 201, Mcp 197, Workspace 364, Automations 386, Architecture 46; Api.IntegrationTests 1027/1027 with throwaway podman services. No individual test class is claimed here; each row says "inside the passing run". The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 (parse, sources, caps) | `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*MentionParserTests*" --filter-class "*MentionSourceTests*" --filter-class "*MentionComposerModelTests*" --filter-class "*MentionSecretFilesTests*" --filter-class "*ComposerMentionsTests*"` | Trigger rules, canonical text and chips, one source per kind, secret names never listed, caps and failing-source handling | Inside the passing Presentation run |
| 1 (wire) | `dotnet test tests/OmpOS.Harness.Acp.Tests -c Release -- --filter-class "*AcpPromptContentTests*"` | Mentions become `resource` blocks with the size caps and truncation notes | Inside the passing Acp run |
| 1 (view) | `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*ComposerMentionViewTests*"` | The picker and chips in the desktop composer | Inside the passing Native run |
| 1 (embedded resource accepted by omp) | `omp acp` in a sandbox HOME, send a prompt with a `resource` block | omp accepts `embeddedContext` content | UNVERIFIED (needs a model turn, OD-07). omp's `promptCapabilities.embeddedContext:true` was read in its source |
| 2 (run mode) | `dotnet test tests/OmpOS.Harness.AgentLoop.Tests -c Release -- --filter-class "*RunModePolicyTests*" --filter-class "*RunModePermissionResolverTests*" --filter-class "*PermissionsFileTests*" --filter-class "*SandboxFileTests*" --filter-class "*SandboxBoundaryTests*" --filter-class "*RunModeLaunchTests*"` | Decision stages, token matching, file formats and precedence, read boundary, launch mapping | Inside the passing AgentLoop run |
| 2 (run mode, head) | `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*RunModePolicyHolderTests*" --filter-class "*RunModePageViewTests*"` | The head holds one policy and the page edits it | Inside the passing Native run |
| 2 (omp request fields) | `HOME=/tmp/x PI_CODING_AGENT_DIR=/tmp/x/.omp/agent omp acp`, one MCP server, log one `session/request_permission` frame | omp's field names match `RunModeRequestParser` | UNVERIFIED (no live omp frame was captured) |
| 2 (ignore) | `dotnet test tests/OmpOS.Harness.Workspace.Tests -c Release -- --filter-class "*IgnoreSyntaxTests*" --filter-class "*IgnoreHierarchyTests*" --filter-class "*IgnoreDefaultsTests*" --filter-class "*IgnoreLimitsTests*"` | Gitignore syntax, hierarchy, default list, limits | Inside the passing Workspace run |
| 2 (MCP import) | `dotnet test tests/OmpOS.Harness.Mcp.Tests -c Release -- --filter-class "*McpConfigFormatTests*" --filter-class "*McpConfigPrecedenceTests*" --filter-class "*McpServerCatalogTests*" --filter-class "*AcpMcpServerMapperTests*" --filter-class "*McpSecretRedactionTests*"` | Six-file loader, precedence, variable expansion, catalog state, ACP mapping, no secret in output | Inside the passing Mcp run |
| 2 (checkpoints) | `dotnet test tests/OmpOS.Harness.Workspace.Tests -c Release -- --filter-class "*CheckpointStoreTests*"` and `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*CheckpointsModelTests*"` and `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*CheckpointBackendTests*" --filter-class "*CheckpointsTimelineViewTests*"` | Snapshot, plan, rewind and the head backend and view | Inside the passing Workspace, Presentation and Native runs. Mid-rewind rollback after an IO failure: no test (running as root bypasses permissions) |
| 2 (worktrees) | `dotnet test tests/OmpOS.Harness.Worktrees.Tests -c Release -- --filter-class "*CursorWorktreesConfigTests*" --filter-class "*WorktreeApplierTests*" --filter-class "*WorktreeSetupRunnerTests*"` and `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*WorktreeCommandsModelTests*"` | `.cursor/worktrees.json` parsing, apply, consent-gated setup, commands model | Inside the passing runs of those projects (Worktrees.Tests count not in the orchestrator list: no recorded result for that project) |
| 2 (queue, search, ring) | `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*QueuedMessagesModelTests*" --filter-class "*ConversationSearchModelTests*" --filter-class "*ConversationSearchIndexTests*" --filter-class "*ContextRingModelTests*"` | Queue order and send now, index-backed search, ring state | Inside the passing Presentation run |
| 2 (images) | `dotnet test tests/OmpOS.Harness.Acp.Tests -c Release -- --filter-class "*AcpImagePromptTests*"` and `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*AgentSessionRuntimeImageTests*"` | Image blocks gated on `promptCapabilities.image` | Inside the passing Acp and Native runs; no live turn |
| 2 (automations) | `dotnet test tests/OmpOS.Harness.Automations.Tests -c Release -- --filter-class "*AutomationRunnerTests*" --filter-class "*AutomationSchedulerTests*"` and `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*AutomationsHostTests*" --filter-class "*LocalSessionAutomationTargetTests*"` and `dotnet test tests/OmpOS.Api.IntegrationTests -c Release -- --filter-class "*AutomationWebhookEndpointTests*" --filter-class "*AutomationManagementTests*"` | Cron firing, consent once, no-omp behaviour, signed webhooks, management API | Inside the passing Automations, Native and Api.IntegrationTests (1027/1027) runs |
| 2 (settings and gate) | `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*SettingsParityTableTests*"` and `./bootstrap.sh` | Parity table matches the pages; the whole gate | `SettingsParityTableTests` inside the passing Native run; `./bootstrap.sh`: result recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log` (not claimed here) |

## Evidence files

| File | What it holds |
|------|---------------|
| `docs/quality/cursor-parity-audit-2026-10-28.md` | Feature statuses of the agent, chat, context and safety rows (pre-wiring tree; sections 5, 6, 10, 11) |
| `docs/reference/run-modes.md`, `checkpoints-and-worktrees.md`, `automations.md`, `mcp.md`, `builtin-skills.md` | User reference with status banners |
| `docs/settings-parity.md` | The 21-page order and the S01 to S50 table |

## Known limits of this verification

- No live model turn (OD-07): omp's permission field names, MCP server pickup and image turns are not observed.
- `HeadAutomationSession` and the slot consent bridge have no test.
- No run on Windows, macOS or Android; the Windows PowerShell worktree setup path and Windows symlink tests are unverified.
- The audit and some reference banners predate the wiring.
