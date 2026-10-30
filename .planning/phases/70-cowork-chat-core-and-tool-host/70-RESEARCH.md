# Phase 70: Cowork chat core and tool host - Research
Source: docs/plans/v0.16.0-research/briefs/70-cowork-chat-core.md

Summarises research reports 02 (UI architecture), 06 (Cowork parity inventory), 07 (browser and computer use feasibility, s4 and s5) and 09 (conventions and commands), plus the sources opened on disk on 2026-10-28. omp facts below are the ADR-0032 Context citations of omp 18.12.5 source (nothing was run; OD-07 not approved).

## Standard Stack / What to reuse
- Gateway: `McpGatewayServer.Start(McpToolRegistry, McpToolInvoker)` (`src/Autonogrammer.Harness.McpGateway/McpGatewayServer.cs:74`) binds `127.0.0.1` with a bearer (`:61-67` doc); `McpGatewayDispatcher.cs:187-188` routes `tools/list` and `tools/call`, `ListTools` at `:235`, `CallToolAsync` at `:252` refuses an ungranted tool at `:281-287`, `WriteTool` at `:108` writes name, description, inputSchema only; `McpGatewayToolNames.Separator = "__"` (`McpGatewayToolNames.cs:14`). Existing gateway tests: `tests/Autonogrammer.Harness.McpGateway.Tests/{McpGatewayGrantTests,McpGatewaySdkClientTests,McpGatewayToolNamesTests,GatewayHarness}.cs`.
- Overlay: `ConfigOverlayWriter.Write(AgentLoopSettings, sessionStateDirectory, ...)` (`ConfigOverlayWriter.cs:32`) is file mode 0600 / dir 0700 (`:13-14`) but renders only `AgentLoopSettings` keys; `OmpLaunchPlanner.AddConfigOverlay` is `private static` (`OmpLaunchPlanner.cs:164`). `WriteYaml` is new.
- Engine composition: `EngineComposition.Compose` (`apps/Autonogrammer.Native/Engine/EngineComposition.cs:29,57`); side-chat precedent `SessionEngineSet.ComposeSideChatEngine` (`SessionEngineSet.cs:650`, doc at `:645` "asks before every tool call"); user MCP servers `services.Mcp.OfferedServers()` (`SessionEngineSet.cs:604`, `McpServerCatalog.cs:78`).
- Panel host seam: `IAgentPanelHost` (`src/Autonogrammer.Presentation/AgentPanel/IAgentPanelHost.cs:9`: `CreateSessionAsync` `:14`, `CreateTerminalSessionAsync` `:22`, `OpenSession` `:30`, `CloseSessionAsync` `:39`, `ReloadAsync` `:44`); runtime `AgentSessionRuntime` (`src/Autonogrammer.Native.Views/Sessions/AgentSessionRuntime.cs:49`).
- Approvals: `IPermissionResolver.ResolveAsync(PermissionRequestEvent, ct)` (`src/Autonogrammer.Harness.Abstractions/IPermissionResolver.cs:8`); `IToolApprovalBridge.DecideAsync` (`src/Autonogrammer.Presentation/Approvals/IToolApprovalBridge.cs:7`); card record `PendingToolApproval` (`src/Autonogrammer.Contracts/Controls/PendingToolApproval.cs:4`); host delegate `McpToolApproval` is a boolean (`src/Autonogrammer.Harness.Mcp/McpToolApproval.cs:10`).
- Images: `PromptImage(Name, MimeType, Data)` (`PromptImage.cs:7`), `AcpImageLimits(8, 10 MiB, 25 MiB)` (`AcpImageLimits.cs:7`), refusal when `SupportsImages == false` (`AcpAgentSession.cs:2274`).
- Transcript: `ToolUpdateContent(string Output, IReadOnlyList<ToolFileEdit> Edits)` (`ToolUpdateContent.cs:11`, `Read` `:25`, `ReadEntry` `:69` handles `diff` and text-only `content`); users: `UiFolder.cs:136`, `HarnessEventProjection.cs:204`, `UiToolBlocks.cs`, `RunProjection.cs`, `ChangeProjection.cs`.
- Test fixtures to extend: `tests/Autonogrammer.Harness.Agent.Tests/ScriptedAcpTransport.cs`, `tests/Autonogrammer.Native.Tests/AgentLoop/ScriptedOmp.cs`, `AgentLoopFixtures.cs`, `tests/Autonogrammer.UI.Tests/{ToolBlocksFoldTests,ReplayTreeTests}.cs` (GenUi fold), `tests/Autonogrammer.UI.Tests/AccessibilityTests.cs`.

## Facts the planner needs
- Not on disk yet: `src/Autonogrammer.Cowork`, `src/Autonogrammer.Cowork.Abstractions` (scaffold), `CoworkSurfaceServices`, `CoworkSessionRegistry`, `ICoworkAccountGate` (phase 69), `ComputerAuditLog` (ADR-0035:119 creates it in phase 73; brief 70 only borrows its pattern).
- omp (ADR-0032:18): `omp acp` parses launch flags (`acp.ts:18-34`); `--append-system-prompt` exists (`flag-tables.ts:179-186`) and a prompt argument that is an existing file is read, anything else is literal (`system-prompt.ts:285-299`); ACP sessions connect only `session/new.mcpServers` and a failed server fails `session/new` (`acp-agent.ts:2668-2730`); tool name `mcp__<server>_<tool>` lower-cased with runs collapsed to `_` and a repeated `<server>_` prefix dropped (`tool-bridge.ts:464-494`); `browser.enabled` defaults true, `computer.enabled` false (`browser/settings.ts:7-10`, `tools/settings.ts:602-606`); an image in a tool update is `{type:"content", content:{type:"image", data, mimeType}}` (`acp-event-mapper.ts:895-913`). Under `yolo` omp sends the host no request (ADR-0032 D4); omp has no OS sandbox (brief 70 threat model, `docs/reference/run-modes.md`).
- omp's MCP bridge keeps image blocks (`07-browser-computer-use-feasibility.md:170`); the gateway serialises `CallToolResult` whole, so images pass (`:182`); the 1 MiB cap is on request bodies only, so responses are capped by the host (`:183`); no MCP tool annotations reach omp (`:183`); a gateway outage fails `session/new` (`:184`).
- ADR-0032 D5 limits: arguments at most 256 KiB; text 65,536 characters; 2 images, 3 MiB each, 6 MiB total; `ApprovalTimeout` and `CallTimeout` 120 s; name 3 to 40 characters; description 1024; ProviderId `^[a-z][a-z0-9]{1,19}$`.
- Contract version: `Contracts/Ui` is not a plugin contract (ADR-0008 s7); the single bump 1.5.0 to 1.6.0 is the scaffold's (`00-scaffold-manifest.md:85`); no phase edits `ContractVersion.Current`. `Harness.McpGateway` (L2) adds a reference to the L1 abstractions (manifest row 2, s3 table); `Autonogrammer.Cowork` tuple `Harness.AgentLoop` already registered.
- Name sets are disjoint because registry names always contain `__` and Cowork names never do (`McpGatewayToolNames.cs:14`, ADR-0032 D5); two names differing only in underscore runs would collide in omp, hence the `__` ban.
- Report 06 rows C4 (trusted folders, Storage folder), C5 (delete always asks), C6 (Manual, Auto, Skip; Skip has no checks) at `06-cowork-parity-and-references.md:65-67`; Auto's classifier is not copyable (risk 1 at `06-cowork-parity-and-references.md:143`).
- Build flags (`02-ui-architecture.md:108`): warnings as errors, XML docs on public members, no reflection STJ, `ILogger<T>? logger = null` (`09-project-recipe-and-conventions.md:61`).

## Architecture Patterns
- One seam to omp: providers implement `ICoworkToolProvider`; only `CoworkToolHost` talks to the gateway; no provider writes the overlay or a second MCP entry.
- Defence in depth: gateway gate (host, authoritative for `mcp__cowork_*`) plus resolver (omp permission requests); omp always asks, so the resolver sees every request.
- Failures are values: `CoworkToolResult.Failure` with fixed text; audit and event failures never fail a call; over-cap is failure, never truncation.
- Taint is host state on the lease; no tool result can clear it.
- Fake in product code plus contract-test base (`09:68`): `FakeCoworkToolProvider` and the base asserting the `ByProviderGate` broker rule per provider.
- Consumer-owned ports (`ICoworkAccountGate`, R8) keep `Autonogrammer.Cowork` free of `Autonogrammer.Accounts`.

## Pitfalls
- Skipping a step of the eight-step sequence (for example calling the provider before the gate) defeats the shared contract tests; test the order with a scripted gate, broker and provider.
- A grant must never satisfy `EveryCall`, `EveryCallOnExecutingDevice` or a `Destructive` call (R6); Skip and `AllowedAlways` must not satisfy `EveryCallOnExecutingDevice`.
- A model-supplied `origin` argument must be ignored; origin comes only from the lease.
- A prompt file path that is missing is read as literal prompt text by omp: fail the start, never fall back to the Code prompt.
- A gateway that fails its health check must not be passed to `session/new` (it would fail the session).
- `ToolUpdateContent` is a positional record: adding `Images` breaks every construct and deconstruct site; update all (no compat constructor), including the `UiFolder`/`HarnessEventProjection` parity.
- `data:` image sources can widen the web CSP; keep `RenderCspScriptTests` in the same change.
- Brief 70 U3 tests "the six required rules"; ADR-0032 D4 lists them (D-04), keep wording testable.
- Approval wait (120 s) may outlive omp's MCP client timeout (not read; OQ-321).

## Verification commands
From the brief (success criteria):
- `dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release`
- `dotnet test --project tests/Autonogrammer.Harness.McpGateway.Tests -c Release`
- `dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*Cowork*"`
- `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release`, `tests/Autonogrammer.UI.Tests`, `tests/Autonogrammer.Sessions.Tests`
- `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*ProjectGraphTests*"`
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror`; `python3 scripts/legal/generate-third-party-notices.py --check` (no new package)
- [INFERENCE from `09-project-recipe-and-conventions.md:97-99`] add `tests/Autonogrammer.Harness.AgentLoop.Tests` and `tests/Autonogrammer.Contracts.Tests` for `WriteYaml` and `ToolUpdateContent`.

## Cannot be verified on this host
A live omp turn through the gateway (`--append-system-prompt`, overlay effects, omp's MCP request timeout against a long approval, the `mcp__cowork_` name seen by the model; OD-07); real providers (ADR-0033 to 0035) and any model's use of images; Windows and macOS path rules and composers (compile only; UNC and drive-root cases compiled, Linux executed); screen readers.

## Validation Architecture
Framework: xunit.v3 on Microsoft.Testing.Platform with Shouldly; hand-written fakes; scripted ACP transport for head tests; bUnit for UI; no real omp (`09:66`).
- Quick run: `dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release` and `dotnet test --project tests/Autonogrammer.Harness.McpGateway.Tests -c Release`.
- Full run: the success-criteria commands above.

| Requirement | Test (project) |
| --- | --- |
| AG-70-1 | exact argument lists for Manual, Auto, Skip; prompt file owner-only, absolute; overlay keys (Cowork.Tests/Chat) |
| AG-70-2 | resolver decisions for read, edit, delete, move, execute inside and outside folders, `mcp__cowork_*`, three modes (Cowork.Tests) |
| AG-70-3 | create, open, close, reload over Cowork registry; `RecoverAfterRestart` once (Native.Tests/Cowork) |
| AG-70-4 | send, stop, retry, steer, `session/load`, image refusal message (Native.Tests/Cowork, scripted transport) |
| AG-70-5 | `ToolUpdateContent.Read` image entries, both folds equal `ImageBlock`, 10 MB placeholder, export (Contracts.Tests, UI.Tests, Sessions.Tests, Presentation.Tests) |
| AG-70-6 | exactly the D5 types and nothing else (Cowork.Tests/Abstractions) |
| AG-70-7 | descriptor rules, eight-step order, caps, timeouts, one call per session (Cowork.Tests/Tools) |
| AG-70-8 | list order, call resolution, `__` never reaches host, `ImageContentBlock` bytes through a real `ModelContextProtocol` client, bearer, Host and Origin (McpGateway.Tests) |
| AG-70-9 | `EveryCallOnExecutingDevice` never satisfied by grant, mode or remote decision; `MustBeLocal` with `Unavailable` denies (Cowork.Tests, Native.Tests) |
| AG-70-10 | health-check failure starts without Cowork tools and raises the notice (Native.Tests/Cowork) |
| AG-70-11 | models, chips, attachment chips, focus order, `AccessibilityNameTests`, bUnit `AccessibilityTests`, reduced motion, no colour literals (Presentation.Tests, Native.Tests, UI.Tests) |
| AG-70-12 | `FakeCoworkToolProvider` and the contract base pass for the fake (Cowork.Tests) |
| AG-70-13 | taint after untrusted result; Auto and Skip stop applying; events raised (Cowork.Tests unit 1 and 3) |
| AG-70-14 | `WriteYaml` owner-only and deleted on dispose (Harness.AgentLoop.Tests); `CoworkPathBoundary` refusals, symlink and `..` escapes (Cowork.Tests) |

Wave 0 gaps (do not exist): `tests/Autonogrammer.Cowork.Tests/{Abstractions,Tools,Chat}` content, `tests/Autonogrammer.Native.Tests/Cowork/*`, `tests/Autonogrammer.Presentation.Tests/Cowork/*`, `tests/Autonogrammer.UI.Tests/Cowork/*`, new gateway tests for Cowork tools and the image client test, `ConfigOverlayWriter.WriteYaml` tests, image cases for `ToolBlockContractTests`, `FakeCoworkToolProvider` with its contract base, and a scripted gateway client for the real-client image test.
