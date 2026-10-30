# Phase 70: Cowork chat core and tool host - Context
Source: docs/plans/v0.16.0-research/briefs/70-cowork-chat-core.md
**Gathered:** 2026-10-28
**Status:** Ready for planning

<domain>
## Phase Boundary

Milestone v0.16.0. Requirement ids AG-70-1 to AG-70-14; six work units from the brief: (1) tool host contract and host; (2) gateway extension; (3) profile and permissions; (4) Native host and composition; (5) images in the transcript; (6) conversation models and views. Depends on phase 69 units 1 to 3 (registry, models, `SurfaceHost`, `CoworkSurfaceServices`), phase 67 (account gate) and phase 68 (the index says a null audit is used until the ledger audit writer lands; R15 supersedes that: the audit is local, D-26, so nothing here waits for 68). The scaffold step already created and registered `Autonogrammer.Cowork.Abstractions`, the `Autonogrammer.Cowork` project and the `Harness.McpGateway` reference (R17); this phase adds code.

Delivers: a signed-in user holds a Cowork conversation with the local omp engine over ACP (streaming, stop, retry, steering, history after restart, file and image attachments) under a Cowork profile (system prompt, permissions, folder boundary) that differs from Code, and one governed seam, the tool host, through which Artifacts (ADR-0033), Browser Use (ADR-0034) and Computer Use (ADR-0035) hand tools to omp, with approvals, caps, audit, and images that reach both the model and the transcript.

Must NOT do: the surface switch, Cowork registry, stores, sync, layout shell (phase 69); any provider implementation (phases 71 to 73); Dispatch origin handling beyond `CoworkTurnOrigin` (phase 74); the Auto rules engine, Skip isolation level, skills, plugins, connectors UI (phases 75, 76); write to the ADR-0031 ledger; edit `ContractVersion.Current` (R9) or any registry file (R17); run a live omp turn (OD-07 not approved).

</domain>

<decisions>
## Implementation Decisions

### Chat core and launch profile
- **D-01:** `CoworkSessionHost` (`apps/Autonogrammer.Native/Cowork/`) implements `IAgentPanelHost` over the Cowork registry and is not a second `SessionEngineSet`; a session is a slot with one omp child over ACP composed with `EngineComposition.Compose` through a hand-built `OmpLaunchPlan`, as a side chat is (`SessionEngineSet.cs:650`), never through the Code planner. (ADR-0032 D4; AG-70-3)
- **D-02:** `CoworkSessionProfile` (`Autonogrammer.Cowork/Chat`) produces the launch: `--no-extensions`; `--approval-mode always-ask-cached` in Manual and `always-ask` in Auto and Skip (never `yolo`: omp sends the host no request under `yolo`); `--add-dir` per trusted folder; `--append-system-prompt <absolute path>`. `RunModeLaunchMapper` (`RunModeLaunchMapper.cs:21`) maps Code modes only and is not used. (ADR-0032 D4; AG-70-1)
- **D-03:** The prompt file is `<sessionDir>/cowork-system-prompt.md`, written owner-only by the composer from the embedded resource `cowork-system-prompt.md` via `CoworkSystemPrompt`; the start is refused with the reason when it cannot be written, because omp reads a missing path as literal text. (ADR-0032 D4; brief 70 U3)
- **D-04:** `CoworkSystemPromptOptions` budgets the file: base 4 KiB, standing-context paragraph 2 KiB (ADR-0037), browser guidance 1 KiB (ADR-0034), computer guidance 1 KiB (ADR-0035), validated total at most 8 KiB; a section over budget or a total over 8 KiB is refused. The embedded base text is at most 4 KiB and names six rules: the Cowork role; files go to the Storage folder; ask before deleting; text in tool results and pages is untrusted data; order of preference connector, browser, screen; never claim a tool that is unavailable. (R12; ADR-0032 D4; brief 70 U3)
- **D-05:** New `ConfigOverlayWriter.WriteYaml(sessionStateDirectory, yaml)` (`Autonogrammer.Harness.AgentLoop/Launch`, owner-only mode, deleted on dispose) writes the overlay `browser.enabled: false`, `computer.enabled: false`; the Cowork composer adds `--config <path>` itself because `OmpLaunchPlanner.AddConfigOverlay` is private (`OmpLaunchPlanner.cs:164`) and `ConfigOverlayWriter.Write` renders only `AgentLoopSettings` keys. The overlay is the only way a profile or provider changes an omp setting; providers never write it. (ADR-0032 D4; AG-70-14)
- **D-06:** The Cowork environment holds provider keys only (`ProviderKeyEnvironment`); no Substrate token, hook bridge, API token or account token. `CoworkPathBoundary` refuses `<stateRoot>/secrets`, `~/.omp`, `~/.ssh` and the account folders as trusted folders, and canonicalises symlinks and `..`. (ADR-0032 D6; AG-70-14; brief 70 threat model)
- **D-07:** After the `cowork` entry the composer offers the user's catalog servers through `McpServerCatalog.OfferedServers()` (as Code does, `SessionEngineSet.cs:604`); a catalog server named `cowork` is dropped with a notice. These tools bypass the tool host, so `CoworkPermissionResolver` is their only gate (`Mcp(...)` rules, Manual asks, tainted sessions count them as actuating even in Skip). Remote and cloud sessions get none (OQ-160). (ADR-0032 D4)
- **D-08:** Turn operations: send, stop (cancel the ACP turn), retry last prompt, steer (native steering channel of `AgentSessionRuntime`), history by `session/load` from `ResumeReference`, attachments (files and folders become `PromptResource`, images become `PromptImage`), refused with the `SupportsImages` message when the model reads no images. (ADR-0032 D4; AG-70-4)
- **D-09:** The conversation reuses `AgentSessionRuntime` (public, `Native.Views/Sessions`) with no-op stage, context and steering-record sources and a Cowork-built `AgentRow` whose `Card` is null; the Code timeline, task context and steering parts are not rendered. (ADR-0032 D3)

### Permissions
- **D-10:** `CoworkPermissionResolver` (an `IPermissionResolver`) denies a path outside the trusted folders and the Storage folder, always asks before a delete, allows `mcp__cowork_*` at the omp level (the gateway gate is authoritative), and otherwise follows the mode. Because omp always runs in an asking mode, every request reaches this one policy seam. (ADR-0032 D4; AG-70-2; OQ-327)
- **D-11:** `CoworkPermissionMode` {Manual, Auto, Skip}, stored as `cowork.permissionMode`, default Manual; "mode" in this milestone means only this enum. Manual asks through the approval queue; Auto is shown as "Auto (rules)" and its rule engine is ADR-0037 (a rule engine, no classifier); Skip allows everything except deletes and, in a tainted session, `edit`, `delete`, `move` and `execute`. (R16; ADR-0032 D4)
- **D-12:** Skip is available only when the session's folder boundary is `FilesystemIsolated` or the user gave a per-session acknowledgement; otherwise the resolver treats the session as Manual and the picker says why. Names kept: `BrowserUse:PolicyMode`, `CoworkToolHostOptions.ApprovalTimeout`; renamed: `Dispatch:CommandApprovalTimeout`, Automations `UnattendedApprovalTimeout`. (R16; ADR-0032 D4)

### Tool host contract and host
- **D-13:** `Autonogrammer.Cowork.Abstractions` (L1, references `Contracts` only, no package) holds exactly the ADR-0032 D5 types and nothing else: `CoworkTurnOrigin`, `CoworkToolEffect`, `CoworkApprovalRequirement` {ByPolicy, EveryCall, EveryCallOnExecutingDevice, ByProviderGate}, `CoworkResultKinds`, `CoworkProviderAvailability`, `CoworkToolDescriptor`, `CoworkSessionContext`, `CoworkToolInvocation`, `CoworkGrantKey`, `CoworkProviderState`, `CoworkCallPreview`, content blocks, `CoworkToolResult`, `ICoworkToolProvider`, `ICoworkSessionLease`, `ICoworkToolHost`, `ICoworkPolicyGate` and its records, `CoworkToolEvent`, `ICoworkApprovalBroker` and its records, `ICoworkGrantStore`, `ICoworkToolAudit`, `CoworkToolAuditEntry`. (ADR-0032 D5, D7; AG-70-6)
- **D-14:** Descriptor rules enforced by `CoworkDescriptorValidator`: `Name` matches `^[a-z][a-z0-9]*(_[a-z0-9]+)*$`, 3 to 40 characters, starts with `ProviderId` + `_`, never contains `__`, never starts `cowork_`; `ProviderId` matches `^[a-z][a-z0-9]{1,19}$` (`artifact`, `browser`, `computer`, `memory`); `Description` at most 1024 characters; `InputSchema` of type object; `ByProviderGate` refused with `Effect = Destructive`. omp shows `mcp__cowork_<name>` (28 characters for `artifact_create`, 64-character cap). (ADR-0032 D5, D6; index s2)
- **D-15:** `CoworkToolHost.InvokeAsync` runs, in order and never skipping: (1) lease exists and the tool is offered (provider available, profile admits provider, image tools only when `ModelReadsImages`); (2) arguments a JSON object of at most 256 KiB; (3) `PreviewAsync`; (4) `ICoworkPolicyGate.DecideAsync`; (5) on Ask, `ICoworkApprovalBroker.RequestAsync` under `CoworkToolHostOptions.ApprovalTimeout` (120 s, a timeout is a denial read as "not approved"); (6) provider `InvokeAsync` under `CallTimeout` (120 s) and the call token; (7) result caps; (8) `ICoworkToolAudit.RecordAsync`, which never fails the call. A provider exception becomes `CoworkToolResult.Failure` with a fixed text and a log line without arguments; one call runs at a time per session. (ADR-0032 D5; AG-70-7)
- **D-16:** Result caps: text 65,536 characters, at most 2 images, each at most 3 MiB, 6 MiB in all, PNG, JPEG or WebP only; an over-cap result becomes a failure, nothing is truncated. (ADR-0032 D5)
- **D-17:** The turn origin (`CoworkTurnOrigin`) is set on the lease by the chat core per prompt; a model-supplied argument named origin is ignored. `IsTainted` is host state, never reset by a tool result. (ADR-0032 D5; brief 70 threat model)

### Gateway
- **D-18:** `McpGatewayServer.Start(McpToolRegistry registry, McpToolInvoker invoker, ICoworkToolHost? coworkTools = null)`: `tools/list` returns registry-granted `server__tool` names then `ListOfferedAsync` names verbatim; `tools/call` resolves a registry grant first, the host second; a name with `__` never reaches the host; an ungranted Cowork tool is refused before the host; sessions cannot see each other's tools. The Cowork head runs its own gateway with an empty registry. (ADR-0032 D5; AG-70-8)
- **D-19:** `CoworkTextBlock` maps to `TextContentBlock`, `CoworkImageBlock` to `ImageContentBlock.FromBytes(data, mimeType)` (`McpGatewayCoworkResults.cs`, new); results with untrusted content are wrapped in a labelled envelope; bearer, Host and Origin checks and the 1 MiB request cap are unchanged. (ADR-0032 D5, D6; brief 70 U2)
- **D-20:** `CoworkToolServer` starts one loopback gateway for Cowork and health-checks it with `initialize` before `session/new`; on failure the session starts without Cowork tools and the conversation shows "Cowork tools are unavailable: <reason>", never a failed session. The `cowork` HTTP MCP entry carries the loopback URL and an `Authorization` bearer; the bearer is only in the `session/new` header list and never logged or in `ToString`. (ADR-0032 Open risks; AG-70-10)

### Policy, approval, audit
- **D-21:** Default `CoworkPolicyGate`: `EveryCallOnExecutingDevice` gives Ask with `MustBeLocal = true` in every mode and from every origin, and a broker `Unavailable` is a denial; `EveryCall` asks in every mode; `ByPolicy`: Skip allows, Auto follows its rules, Manual allows a non-`Destructive` call whose `Preview.Grant` names a stored grant and asks otherwise. A provider reporting `Unavailable` is never allowed. (ADR-0032 D6; R6; AG-70-9)
- **D-22:** A stored grant satisfies `ByPolicy` only, never `EveryCall` or `EveryCallOnExecutingDevice`, never a `Destructive` call, and is created only by an `AllowedAlways` answer. The artifact provider returns `Preview.Grant = ("artifact.write", "cowork")`, so the first write asks once. A tainted session still asks for every `Actuating` call whatever grants are stored. (R6; ADR-0032 D6)
- **D-23:** Taint: the first tool result carrying an `IsUntrusted` text or image block taints the session, permanently; Auto and Skip then no longer apply: the gate asks for every `Actuating` or `Destructive` call and the resolver treats `edit`, `delete`, `move`, `execute` as Manual. The host raises `CoworkToolEvent` (Started, ApprovalRequested, Completed, Failed, Denied). (ADR-0032 D6; AG-70-13)
- **D-24:** `ByProviderGate`: the host gate returns Allow once the provider is Available, never Ask; the provider gets an `ICoworkApprovalBroker` at construction and must ask through it with `MustBeLocal = true` for its own grants and sensitive actions in every mode, including Skip and tainted sessions; the host cannot tell, so the shared provider contract tests of unit 1 assert it per provider. Taint adds no per-call ask for these tools. (ADR-0032 D6; AG-70-12)
- **D-25:** `CoworkApprovalBroker` (Native) raises a `PendingToolApproval` in the session approval queue (existing card; host-observed `Target` and `Details`, never model text), completed by `CoworkApprovalBridge` (an `IToolApprovalBridge`); a decision from another device (Dispatch) never completes a `MustBeLocal` request. Timeout or no window: "The tool call was not approved.", card shows Timed out or Unavailable. (ADR-0032 D6; brief 70 U4)
- **D-26:** Audit is the local owner-only hash-chained JSONL `FileCoworkToolAudit` (the `ComputerAuditLog` pattern); entries hold no arguments or result bytes; a write failure never fails the call; nothing goes to the ledger; browser and computer events stay local. (R15; ADR-0032 D5)

### Images in the transcript
- **D-27:** `ToolUpdateContent` gains `Images` (`IReadOnlyList<ToolUpdateImage>`; `ToolUpdateImage(string MimeType, string DataBase64, string? Description)`, `Autonogrammer.Contracts.Ui`) read from `content` entries of type `image`; a malformed entry is ignored without throwing. The GenUi and Sessions folds produce the same `ImageBlock`; transcript views, export and web projection render it with alt text and a size cap; a 10 MB payload becomes a placeholder; every caller is updated (no compatibility constructor). The change is additive and this phase edits no contract version. The web CSP is not widened unless the render needs a `data:` image source, then `RenderCspScriptTests` changes in the same change. This phase owns the change for ADR-0034 and ADR-0035. (ADR-0032 D4; R9; AG-70-5)

### Conversation models and views, composition
- **D-28:** `CoworkConversationModel` composes `SessionViewModel`, `ComposerViewModel`, `CoworkApprovalQueue` of `PendingToolApproval` and `ProviderStatusChip`s (Available, Needs setup, Unavailable with reason); views `CoworkConversationView`, `CoworkComposerView`, `CoworkApprovalCard` (Native, `ViewMapping.Create` cases) and `CoworkConversation.razor`, `CoworkComposer.razor`, `CoworkApprovalCard.razor`; focus order transcript, composer, approvals; attachment chips with remove; no colour literals; reduced motion honoured. (ADR-0032 D3; AG-70-11)
- **D-29:** A model that reads no images turns image tools and image attachments off with "This model cannot read images" and can be changed in place; an attachment over `AcpImageLimits` (8 images, 10 MB each, 25 MB total) or a missing file shows the reason on its chip and the prompt is not sent. (brief 70 degraded states; `AcpImageLimits.cs:7`)
- **D-30:** `FakeCoworkToolProvider` and a shared contract test base let ADR-0033, 0034, 0035 providers prove themselves against the same rules. `CoworkHead` is composed in `apps/Autonogrammer.Native/Program.cs` or `App.axaml.cs`; `CoworkSurfaceServicesFactory` plugs into `SurfaceHost` of phase 69; sign-out stops engines after confirmation and keeps rows. The account gate is `ICoworkAccountGate` (R8). (brief 70 U1, U4; AG-70-12)

### Claude's Discretion
- Exact file name and folder of `FileCoworkToolAudit` and of `FileCoworkGrantStore` [INFERENCE: not named by brief or ADR]; other fields of `CoworkToolHostOptions`; wording of the labelled untrusted envelope and the six-rule base prompt text.
- Behaviour of `IAgentPanelHost.CreateTerminalSessionAsync` (`IAgentPanelHost.cs:22`) in the Cowork host (the ADR calls the host "create, open, close, reload"); the in-product form of the Skip acknowledgement and how a phase-70 session learns `FilesystemIsolated` before phase 76 supplies it; what Auto does before ADR-0037's engine lands (ADR-0037 says it behaves like Manual for provider calls and in tainted sessions).
- Shape of `FakeCoworkToolProvider`, the contract-test base API and the visual form of status chips and the approval card.

</decisions>

<specifics>
## Specific Ideas

- Model for the gateway extension: existing `McpGatewayDispatcher` refuses a not-granted tool before any upstream is reached (`McpGatewayDispatcher.cs:281-287`); `ListTools` at `:235` writes name, description, inputSchema only.
- Model for tests: a scripted ACP transport as in existing head tests; a real `ModelContextProtocol` client reads the image result as `ImageContentBlock` with the same bytes and mime type.
- Degraded states: gateway down; provider unavailable or needs setup (its reason on the chip, for example "Chromium is not installed"); result over a cap, fixed failure message; omp not found or `session/new` failure use the existing `AgentSessionRuntime` error states with Retry, a lost engine offers Resume (`session/load`); prompt file unwritable, start refused (never run with the Code prompt); sign-out while running, confirmation then engines stop, rows stay.

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Decisions and briefs
- `docs/decisions/ADR-0032-cowork-shell-sessions-chat-and-tool-host.md` D3 (conversation model and views), D4 (chat core; not the Listing bullet), D5, D6, D7, Open risks: this phase's share (D1, D2 and the Listing bullet are phase 69).
- `docs/plans/v0.16.0-research/briefs/70-cowork-chat-core.md` — requirements, six units, threat model, degraded states, OQ rows.
- `docs/plans/v0.16.0-research/briefs/00-index.md` — naming rules (s2), R6, R8, R9, R12, R15, R16, R17.
- `docs/plans/v0.16.0-research/briefs/00-scaffold-manifest.md` — rows 2 (`Cowork.Abstractions`) and 6 (`Cowork`), T5, edit of `Harness.McpGateway`.
- `docs/plans/v0.16.0-research/briefs/69-mode-shell-and-cowork-sessions.md` — registry, `CoworkSurfaceServices`, `ICoworkAccountGate`, dock host.
- ADR-0033 D4 (artifact grant), ADR-0034 D8/D9 (browser prompt, overlay), ADR-0035 (`ByProviderGate`, `ComputerAuditLog`), ADR-0036 (Dispatch origin), ADR-0037 D4 and Auto/Skip text, ADR-0010 (omp over ACP facts) in `docs/decisions/`.

### Research
- `docs/plans/v0.16.0-research/02-ui-architecture.md` s2 to s4, s6, s7 (composition, views, layer rules, tests).
- `docs/plans/v0.16.0-research/03-api-auth-backend.md` s11 (account seams the gate depends on).
- `docs/plans/v0.16.0-research/06-cowork-parity-and-references.md` s3 rows C4 to C6 (trusted folders, deletion protection, approval modes) and s7 risk 1 (Auto needs no classifier).
- `docs/plans/v0.16.0-research/07-browser-computer-use-feasibility.md` s4.2 to s4.4 (ACP image path, gateway gaps, route to omp) and s5 (prompt-injection containment).
- `docs/plans/v0.16.0-research/09-project-recipe-and-conventions.md` s4 (fakes, options validators, STJ contexts), s5, s6 (commands).
- `docs/open-questions.md` rows OQ-321, 322, 323, 327, 328, 329 (already appended).

### Existing source and tests touched (verified on disk)
- `src/Autonogrammer.Harness.McpGateway/{McpGatewayServer.cs (Start line 74),McpGatewayDispatcher.cs,McpGatewayToolNames.cs (Separator line 14)}`; tests `tests/Autonogrammer.Harness.McpGateway.Tests/` (`McpGatewayGrantTests`, `McpGatewaySdkClientTests`, `McpGatewayToolNamesTests`, `GatewayHarness`).
- `src/Autonogrammer.Harness.AgentLoop/Launch/{ConfigOverlayWriter.cs (Write line 32),OmpLaunchPlan.cs,OmpLaunchPlanner.cs}`; `Safety/RunModeLaunchMapper.cs`; test `tests/Autonogrammer.Harness.AgentLoop.Tests/Launch/ConfigOverlayWriterTests.cs`.
- `src/Autonogrammer.Contracts/Ui/ToolUpdateContent.cs` (record line 11, `Read` line 25, `ReadEntry` line 69); callers `src/Autonogrammer.GenUi/{UiFolder.cs,UiToolBlocks.cs}`, `src/Autonogrammer.Sessions/HarnessEventProjection.cs`, `services/Autonogrammer.Api/Services/{RunProjection.cs,ChangeProjection.cs}`; export `src/Autonogrammer.Presentation/Export/TranscriptMarkdownExporter.cs`; test `tests/Autonogrammer.Contracts.Tests/Ui/ToolBlockContractTests.cs`, `tests/Autonogrammer.Web.Tests/RenderCspScriptTests.cs`.
- `apps/Autonogrammer.Native/Agents/SessionEngineSet.cs` (side chat `:650`, offered servers `:604`), `Engine/EngineComposition.cs` (`Compose` lines 29, 57), `Settings/Models/ProviderKeyEnvironment.cs`; `src/Autonogrammer.Native.Views/Sessions/AgentSessionRuntime.cs`; `src/Autonogrammer.Presentation/AgentPanel/IAgentPanelHost.cs`, `Approvals/IToolApprovalBridge.cs`; `src/Autonogrammer.Contracts/Controls/PendingToolApproval.cs`; `src/Autonogrammer.Harness.Abstractions/{IPermissionResolver,PromptImage,PromptResource}.cs`; `src/Autonogrammer.Harness.Acp/AcpImageLimits.cs`, `AcpAgentSession.cs` (`SupportsImages` guard line 2274); `src/Autonogrammer.Harness.Mcp/{McpToolApproval.cs,Catalog/McpServerCatalog.cs}`.

</canonical_refs>

<code_context>
## Code Context

### Reusable Assets
- `AgentSessionRuntime` (streaming, stop, retry, image-input binding already tested for Code); `EngineComposition.Compose`; `ConfigOverlayWriter` (owner-only mode `OwnerFileMode`, `ConfigOverlayWriter.cs:14`); `McpServerCatalog.OfferedServers()`; `PendingToolApproval` and `IToolApprovalBridge`; `AcpImageContent.Build` and `AcpImageLimits`.
- Hand-written fakes shipped as product code with a contract base class (`09-project-recipe-and-conventions.md:68`): the model for `FakeCoworkToolProvider`.

### Established Patterns
- Options: plain class with `SectionName`, `AddOptions<T>().BindConfiguration().ValidateOnStart()` and a hand-written `IValidateOptions<T>` (`09:59`); STJ source generation only (`09:62`); `ILogger<T>? logger = null` (`09:61`); async methods end in `Async` with a last `CancellationToken` (`09:60`).
- Failure results are values, not exceptions-as-flow; failure text is fixed and secret-free (`09:63`).
- The gateway listens on `127.0.0.1` with a per-start bearer and 1 MiB request cap (`McpGatewayServer.cs:74-96`).

### Integration Points
- Created by earlier phases/scaffold, not on disk yet: `Autonogrammer.Cowork`, `Autonogrammer.Cowork.Abstractions`, `Cowork/` folders in Contracts, Presentation, Native.Views, UI (scaffold, phase 69); `CoworkSurfaceServices`, `CoworkSessionRegistry`, `ICoworkAccountGate` (phase 69); `ComputerAuditLog` (phase 73, ADR-0035).
- `ViewMapping.cs` and `CoworkSurfaceServices` are edited after phase 69 lands.
- Provider phases register `ICoworkToolProvider` instances with `CoworkToolHost`; no second route to omp.

</code_context>

<deferred>
## Deferred Ideas and omissions

- **OMITTED BY DECISION:** `--approval-mode yolo` in Cowork (the resolver, boundary and taint rules could not run); an Auto classifier (rule engine only, ADR-0037; hosted-inference models cannot be copied, report 06 s7); loading the user's omp extensions and skills (`--no-extensions`, OQ-329; ADR-0037 decides skills); writing tool audit to the ledger (R15); MCP tool annotations (`readOnlyHint`) reaching omp (gateway lists only name, description, inputSchema; report 07 s4.4); catalog MCP servers for remote and cloud sessions (OQ-160).
- Phase 76: Auto rules engine, `FilesystemIsolated` level, Skip warning and acknowledgement UI (AG-76-6, AG-76-7), `ShellDeleteDetector`. Phase 75: standing-context paragraph and memory tools. Phases 71 to 73: providers and the panel contents. Phase 74: Dispatch origin and remote decisions.
- Open, default in force (non-blocking): OQ-321 120 s approval vs omp MCP timeout (keep 120 s, measure under OD-07); OQ-322 does omp apply `--append-system-prompt` and `browser.enabled` in `omp acp` (assume yes by source, fail closed by also hiding omp's browser tool if the check fails); OQ-323 no cap on running Cowork engines (idle-stop setting `runtime.idleStopHours`); OQ-327 omp-level allow of `mcp__cowork_*` accepted; OQ-328 deleting a task leaves omp's session file under `~/.omp` and the delete dialog lists it; OQ-329 no user extensions.
- A live omp turn (OD-07 not approved): gateway connection, `--append-system-prompt`, overlay, MCP timeout and the `mcp__cowork_` name seen by the model stay source-verified only.

</deferred>

---

*Phase: 70-cowork-chat-core-and-tool-host*
*Context gathered: 2026-10-28*
