# Phase 70 plan check after the 2026-10-29 integration amendments

Checker: replan-wave-4.Plan70Checker (general task agent in the gsd-plan-checker role). Date: 2026-10-29.
Scope checked: 70-01..70-06-PLAN.md, 70-CONTEXT.md (D-01..D-40, U-1..U-3), 70-RESEARCH.md, 70-VALIDATION.md. The older 70-PLAN-CHECK.md is untouched.
Method: every claim below was read from the tree (git ls-files, grep, file reads) in /root/src/repos/ompos-autonogrammer; nothing was built or run. Anything not read is marked [INFERENCE].

## Verdict: ISSUES FOUND (2 BLOCKER, 16 WARNING, 5 INFO)

The amendments are largely accurate and correctly scoped. Two defects would make an executor fail a gate or deliver unwired product code; both have small fixes.

---

## BLOCKERs

### B1. The relock of Native.csproj misses a third lock file (tests/Autonogrammer.ArchitectureTests/packages.lock.json)
- Where: 70-CONTEXT.md D-34(c) (line 165) and U-2 (line 175); 70-04-PLAN.md frontmatter lines 22-24, Task 2 action line 100 (relock sentence), Task 2 acceptance line 102 ("both lock files were relocked"), Artifacts line 152, Files owned line 158. Task 2 `<files>` (line 98) repeats the two-file list.
- Evidence: the lock files embed the direct dependencies of every referenced project. `tests/Autonogrammer.ArchitectureTests/packages.lock.json` has an `autonogrammer.native` project entry whose dependency list names Cowork, Harness.Acp and others but not `Autonogrammer.Harness.McpGateway` (read with a JSON script: `native entry has McpGateway: False`; the gateway project entry itself already exists there). `tests/Autonogrammer.ArchitectureTests/Autonogrammer.ArchitectureTests.csproj` line 57 references `apps/Autonogrammer.Native/Autonogrammer.Native.csproj`. Only two test projects reference Native.csproj (`git grep`: ArchitectureTests and Native.Tests, plus slnx and slnf). `Directory.Build.props:29` sets `RestorePackagesWithLockFile=true`, and line 38 sets `RestoreLockedMode=true` when `CI=true`.
- Failure mode: after the Native.csproj edit, ArchitectureTests (which holds `ProjectGraphTests`, the plan's own broad gate) restores with a stale lock. In CI that is NU1004; locally the restore silently rewrites a tracked file that is outside every plan's owned and committed paths (commits use explicit paths), leaving the tree dirty and CI red.
- Exact fix: in 70-04 add `tests/Autonogrammer.ArchitectureTests/packages.lock.json` to frontmatter `files_modified`, to Task 2 `<files>`, to Artifacts (line 152 "the two relocked" becomes "the three relocked"), to Files owned (line 158, limited to the `autonogrammer.native` entry), to the acceptance sentence (line 102), and to the relock command in line 100: add `/tmp/ag-bin/ag-dotnet restore tests/Autonogrammer.ArchitectureTests/Autonogrammer.ArchitectureTests.csproj --force-evaluate -p:RestoreLockedMode=false`. Update CONTEXT D-34(c) and U-2 ("two lock files" becomes "three"). Alternative that matches CONTRIBUTING.md:91: relock once with `restore Autonogrammer.Linux.slnf --force-evaluate -p:RestoreLockedMode=false` and list the three changed lock files.
- U-2 assessment otherwise: the Native.csproj edit is conditional (`git grep McpGateway apps/Autonogrammer.Native/Autonogrammer.Native.csproj` is empty today: confirmed, 0 matches), minimal (one L4 to L2 ProjectReference, tuple not needed, `LayerMap.SameLayerAllowed.Count` pinned at 29 by `ScaffoldRegistrationTests` is not touched), logged (CONTEXT U-2, commit naming, plan text), and adds no package (ModelContextProtocol already flows through Harness.Mcp, which Native references). It is the only registry-type edit in the phase besides the Cowork.csproj `EmbeddedResource` item (70-03, not a reference or package edit; see W9).

### B2. Nothing in 70-04 builds the CoworkConversationModel or feeds its approval queue and provider chips, and the plug point into phase 69 is declared read-only
- Where: 70-CONTEXT.md D-34(e) (line 165: "the Native host maps CoworkApprovalRequest and CoworkProviderState to them"), Code Context line 123 ("`ViewMapping.cs` and `CoworkSurfaceServices` are edited after phase 69 lands"); 70-04-PLAN.md frontmatter `depends_on` (lines 6-9: 70-01, 70-02, 70-03 only), Task 2 action (f) in line 100, Files owned line 158 ("Phase-69 CoworkSurfaceServices and SurfaceHost are read-only as the factory plug point"); 70-06-PLAN.md line 81 (the models "that the Native host builds over AgentSessionRuntime (Native.Views, 70-04)").
- Evidence: `grep` of 70-04 for `CoworkApprovalQueue`, `ProviderStatusChip`, `CoworkConversationModel` and `70-06` returns nothing. 70-06 creates those three Presentation types and the three views plus `ViewMapping.Create` cases, and states that 70-04 supplies the SessionViewModel, ComposerViewModel, approvals and chip states. 70-04 builds `CoworkApprovalBroker` that raises `PendingToolApproval` "in the session approval queue (existing card)" and a `CoworkSurfaceServicesFactory`, but no task constructs `CoworkConversationModel`, maps `CoworkProviderState` to `ProviderStatusChip`, or states which approval queue (the runtime's existing one or 06's `CoworkApprovalQueue`) the broker feeds. Phase 69 builds `CoworkSurfaceServices` itself in a head-side file (`69-03-PLAN.md` line 90: `apps/Autonogrammer.Native/Cowork/CoworkSessionCatalogAdapter.cs` "builds the CoworkSurfaceServices that App.axaml.cs hands to MainWindow"; 69-CONTEXT D-40: services carry the catalog, Presentation models and delegates only). Its final shape has no conversation slot that I can verify (69 is not executed); 70-04's `CoworkSurfaceServicesFactory` and its `App.CreateWindow` edit therefore either duplicate 69's builder or have nowhere to hand a conversation.
- Failure mode: AG-70-3 truth 1 and AG-70-11 end up as tested widgets with no producer in the running Native head: the approval card and status chips never appear for a real session, or the executor invents an unplanned edit of phase 69's `CoworkSurfaceServices`/`CoworkView` while the plan says read-only.
- Exact fix (70-04 Task 2 action (f), plus metadata): (1) add `70-06` to `depends_on` (the Native host maps onto 06's Presentation types; wave stays 3). (2) State the build step: `CoworkSessionHost` (or `CoworkHead`) constructs one `CoworkConversationModel` per open session from the runtime's `SessionViewModel` and `ComposerViewModel`, feeds `CoworkApprovalQueue` from `CoworkApprovalBroker`/`CoworkApprovalBridge` (name the single queue; the broker must not also write a second queue), and feeds `ProviderStatusChip` from `ICoworkToolHost` provider state. (3) Replace the `CoworkSurfaceServicesFactory` paragraph with "extend the phase 69 builder (`CoworkSessionCatalogAdapter` and its CoworkSurfaceServices construction in App.CreateWindow); do not add a second one", and state the exact member of phase 69's `CoworkSurfaceServices` the conversation is handed through. If 69-03 provides no such delegate, add `src/Autonogrammer.Native.Views/Shell/CoworkSurfaceServices.cs` to 70-04's Files owned as an additive edit (one delegate) and align it with CONTEXT line 123, and add a coordination note to 69-03. (4) Add a test row for the mapping (a provider state change moves a chip; a broker request appears in the queue) to `CoworkSessionHostTests` and VALIDATION.

---

## WARNINGs

### W1. Residual contradiction: 70-02 Task 2 action still says to add the csproj reference conditionally
70-02-PLAN.md line 74 ("FIRST verify the csproj for a ProjectReference to Autonogrammer.Cowork.Abstractions and add it only if absent") against the amendment on line 76 and acceptance on line 77 (`Autonogrammer.Harness.McpGateway.csproj` is not edited). Verified fact: the csproj already has `ProjectReference ../Autonogrammer.Cowork.Abstractions/...` (line 5). Fix: delete the clause from the action.

### W2. Residual contradiction: 70-03 Task 1 action still gives the profile catalog servers
70-03-PLAN.md line 77 ("catalog servers from McpServerCatalog.OfferedServers except a server named cowork dropped with a notice") against amendment line 79 (clause MOVED to 70-04) and D-34(b) (Cowork cannot use Harness.Mcp). An executor reading the action first would reference `McpServerCatalog` (Harness.Mcp, L2 to L2, no tuple) and fail `ProjectGraphTests`. Fix: strike that clause from the action so only the amendment's cowork-entry-only profile remains. The Task 2 `read_first` mention of `McpServerCatalog.cs` (line 87) is read-only and acceptable.

### W3. Residual contradiction: 70-04 objective still says Program.cs
70-04-PLAN.md line 63 ("composed in Program.cs"). D-33 and Task 2 say Program.cs is not edited (verified: 36 lines, `Main` and `BuildAvaloniaApp` only). Fix: "composed in AgentHead.Build and App.CreateWindow".

### W4. Residual contradiction: 70-06 Task 2 action says the Presentation models reuse AgentSessionRuntime
70-06-PLAN.md line 90 versus its own amendment (line 81: Presentation cannot reference Native.Views; the models receive SessionViewModel and ComposerViewModel). The amendment says "read: they receive those view models", but the action line is what the executor follows. Fix: rewrite the clause.

### W5. 70-04 starts one gateway per session; D-18/D-20/D-33 describe one Cowork gateway per head
70-04-PLAN.md line 100 (c) ("starting one loopback gateway per session") against D-20 ("starts one loopback gateway for Cowork"), D-18 ("The Cowork head runs its own gateway with an empty registry") and the existing `McpGatewayServer.OpenSession(sessionId)` (McpGatewayServer.cs lines 84-96) that gives per-session isolation on one listener. Pick one: recommended one `McpGatewayServer` per head with `OpenSession` per Cowork session (also matches D-33 disposal and the health check). Note the gateway has no production caller today (only tests); `Start` needs an `McpToolRegistry` and a `McpToolInvoker(registry, McpToolApproval)` (Harness.Mcp, which Native references): state the deny-all approval delegate for the empty registry.

### W6. 70-04 CoworkHead composition omits the catalog source and nullable inputs
D-33 lists the `SessionEngineSetOptions` pieces (TransportFactory, LoggerFactory, OmpPath, TimeProvider, ProviderKeys) but D-07's catalog servers come from `HeadServices.Mcp.OfferedServers()` (`SessionEngineSet.cs:604` area: `_services.Mcp.OfferedServers()`), reachable through `AgentHead.Services`, which can be null (`CreateProduction()` without services), and `SessionEngineSetOptions.ProviderKeys` is `ProviderKeyEnvironment?` (nullable). Fix: add both to Task 2 (f) with the null behaviour (no catalog servers; empty provider-key dictionary).

### W7. 70-04 cites ScriptedAcpTransport, which Native.Tests does not have
70-04-PLAN.md line 90 ("using ScriptedOmp and ScriptedAcpTransport"). `git grep ScriptedAcpTransport -- tests/Autonogrammer.Native.Tests` is empty; the class lives in Harness.Agent.Tests and Harness.Acp.Tests and is not linked into `Native.Tests.csproj` (not in any files list). Native tests use `ScriptedOmp` (records the `AcpClientOptions` each launch, which D-31 needs) and the `Rig`/`ScriptedAgent` pattern of `EnginePermissionOrderTests` (`session/request_permission` at `Send/ScriptedAgent.cs:201`). Fix: say "ScriptedOmp and the EnginePermissionOrderTests Rig".

### W8. Files owned globs overlap across plans
70-01 line 149: `tests/Autonogrammer.Cowork.Tests/**` also covers 70-03's `tests/Autonogrammer.Cowork.Tests/Chat/*`. 70-04 line 158: `tests/Autonogrammer.Native.Tests/Cowork/*` also covers 70-06's `CoworkConversationAccessibilityTests.cs`. Different waves, so no same-wave collision, but narrow the globs (`Abstractions/*` and `Tools/*` for 70-01; the four named files for 70-04).

### W9. Frontmatter versus task `<files>` and ownership text
- 70-01: `src/Autonogrammer.Cowork.Abstractions/Tools/CoworkToolHostContract.cs` is in frontmatter (line 9) but in no task `<files>`; Task 1 lists only `CoworkToolContract.cs`. Add it to Task 1.
- 70-05: RED `UiBlockJsonTests` image row is added in Task 1 (amendment line 86) but the file is listed only in Task 2 `<files>`. Add it to Task 1.
- 70-06: Files owned says "sixteen files_modified entries" (line 148); frontmatter has 18 entries (lines 10-27). Fix the count.
- 70-03: the Cowork.csproj `EmbeddedResource` edit is the only csproj edit in the plan and 70-01 says "no csproj edit" (amendment line 81): consistent per plan, but CONTEXT line 13 still says the phase edits no registry file; the EmbeddedResource item is neither a reference nor a package, so keep it, and add it to the CONTEXT amendment's list of conscious csproj edits next to D-34(c).

### W10. Task-level verify commands do not exercise what the task's fails_when and acceptance name
- 70-01 Task 1 (line 80): verify filters `*CoworkContractShapeTests*`; the descriptor-boundary and cap tests live in `CoworkToolHostTests.cs` (Task 1 `<files>`), so the fails_when "descriptor boundary case passes validation" cannot fire. Add `--filter-class "*CoworkToolHostTests*"`, or move the validator and limiter tests into a named class and filter on it.
- 70-03 Task 1 (line 78): the WriteYaml tests live in `ConfigOverlayWriterTests` (AgentLoop.Tests) but the only automated command runs `CoworkSessionProfileTests`; they first run in Task 3 (line 100). Add the AgentLoop.Tests command to Task 1.
- 70-06 Task 2 (line 91): GREEN of the Task 1 Presentation tests is verified only by the Native accessibility filter; add `--project tests/Autonogrammer.Presentation.Tests ... "*CoworkConversationModelTests*"`.
- 70-05 Task 3 (line 104) and VALIDATION row 33: `tests/Autonogrammer.Native.Tests/Blocks/ImageBlockViewTests.cs` and the Native half of `SharedScenarioParityTests` are named in acceptance (line 105) but no command runs `Native.Tests` for them (plan verification block, lines 128-130, lists Contracts, UI, Sessions, Presentation). Add a Native.Tests command with `--filter-class "*ImageBlockViewTests*" --filter-class "*SharedScenarioParityTests*"` (and a VALIDATION row).
- 70-04 Task 3 `<files>` lists `CoworkSessionHostTests.cs` for a docs/verification task, and VALIDATION row 29 names an "AgentHead disposal case" with no named test class or file. Name the test class and file in Task 1 (the existing `tests/Autonogrammer.Native.Tests/AgentLoop/AgentHeadPanelTests.cs` is the nearest pattern) and add it to frontmatter.

### W11. D-37 and 70-06 Task 3 claim motion gates that do not scan cowork-chat.css
D-37 (CONTEXT line 168) and 70-06 Task 3 third `<verify>` (line 102) and VALIDATION row 36 rely on `WebShellAccessibilityTests` and `HeadMotionTests`. Read: `WebShellAccessibilityTests` reads `apps/Autonogrammer.Web/wwwroot/css/app.css`; `HeadMotionTests` scans head `*.cs` and the heads' `*.axaml` application styles. Neither reads `src/Autonogrammer.UI/wwwroot/styles/*.css`. Reduced motion for UI styles is the global rule in `base.css` (`transition-duration: 0.01ms !important`, asserted by `CssContractTests.ReducedMotionIsHonouredByQueryAndBySetting`). So the fails_when "cowork-chat.css declares a transition ... that reduced motion does not switch off" can never fire and "no CSS transition or animation is added" is untested. Fix: add one assertion to `tests/Autonogrammer.UI.Tests/Cowork/CoworkConversationTests.cs` (via `CssSources.ReadStyle("cowork-chat.css")`) that the stripped sheet contains no `transition`, `animation` or `@keyframes`; keep or drop the ArchitectureTests command, but fix the fails_when text and the D-37 and VALIDATION wording. `CssContractTests` (import, tokens, colour literals) and `StyleAccessibilityTests` do auto-discover the new sheet (`CssSources.AllCssFiles` enumerates the styles directory): those two checks are real.

### W12. D-39 says ProjectGraphTests is "new or extended by this phase"; no plan edits it
D-39 (line 170) lists `ProjectGraphTests (no Cowork edge from Presentation, UI, Web, Native.Views)`. No plan lists `ProjectGraphTests.cs` or any new architecture test, and the layer rules do not forbid Web (L4) or Native.Views (L3) from referencing Cowork (L2); only Presentation/UI (L2 to L2 with no tuple) are forbidden. The only enforcement is 70-06's bUnit namespace check for UI components. Phase 69-06 plans `CoworkSurfaceIsolationTests` (ArchitectureTests, five D-31 rules) which may cover this [INFERENCE: not read in full]. Fix: reword D-39 to "ProjectGraphTests runs unchanged; the no-Cowork-reference rule for Native.Views and Web is held by D-34(e) discipline plus 69-06's isolation tests", or add a named test to 70-06.

### W13. Suppression ledger and broad catches are not planned
The plans require "a provider exception becomes a fixed Failure", "an audit write failure never fails the call", a gateway health check that degrades, and a grant-store read that tolerates errors. With `AnalysisLevel latest-all` and `TreatWarningsAsErrors` (Directory.Build.props), a general catch needs either a narrow catch or a `SuppressMessage` with a kept `SUP` row in `docs/open-questions.md` (`SuppressionLedgerTests`, 240 existing rows; `69-06-PLAN.md` line 56 already warns about this scan). No 70 plan mentions it and `docs/open-questions.md` is in no files list. Fix: add a line to 70-01, 70-02 and 70-04 (Files owned, "docs/open-questions.md limited to SUP rows") or instruct narrow catches; also note `TodoMarkerScanner` (no TODO without a link).

### W14. 70-03 Task 3 profile environment filter relies on a name shape; D-31 allowlist behaviour change is not surfaced to the catalog stdio servers beyond a degraded-state sentence
Not a defect in correctness (the `ProviderKeyEnvironment.IsKeyName` seam is verified, `AUTONOGRAMMER_PROVIDER_*_KEY`), but D-31 changes what a user's catalog stdio MCP server and every omp child tool inherit (no `SSH_AUTH_SOCK`, `GIT_*`, `XDG_*`, `DISPLAY`, ...). The only user-facing text is "stated in the degraded-state copy only if a start fails". Consider one line in `docs/architecture.md` (70-04 already edits it) naming the dropped inheritance so it is discoverable. [INFERENCE: omp's own need for XDG variables was not read.]

### W15. D-29 message versus existing composer copy
D-29 and 70-06 require "This model cannot read images"; `Presentation/Composer/Attachments/AttachmentCopy.cs` already has `ModelRefuses` ("The selected model does not read images. Choose a model with image input.") and `EngineRefuses`. 70-06 composes `ComposerViewModel`, so two texts could surface. Fix: state which owns the Cowork text (a Cowork-scoped constant that overrides the composer's for Cowork sessions) in 70-06 Task 1.

### W16. `fails_when` text claims versus single `<automated>` in 70-01 Task 3 and 70-04 Task 2
Minor: 70-01 Task 3 (line 101) asserts "the base passes for a double that never calls the broker", which needs the negative-control test to be in the filtered class `FakeCoworkToolProviderTests`; the amendment (line 102) puts it there. OK once W10 is applied; no separate action.

---

## INFO

- I1. Line anchors off by a few lines (harmless): `AgentHead.Build` signature is line 297 (`new SessionEngineSet` at 299); `AgentHead.CreateProduction` public overload is line 82, the private one that composes is 84; `A2uiSurfaceCatalog.IsAllowedImageSource` starts at about line 158 (the cited :161 is inside the body).
- I2. D-31/D-32/D-36 mandatory-correction test: confirmed necessary and minimal (see Scope). `EngineEnvironmentPolicy` (public), `AcpClientOptions.EnvironmentMode/EnvironmentAllowList` (lines 77, 83) exist as claimed.
- I3. `namespace Autonogrammer.Native.Cowork` (apps/Autonogrammer.Native/Cowork/) shadows `Autonogrammer.Cowork` for partially qualified names inside `Autonogrammer.Native.*`; executors must use full `using Autonogrammer.Cowork.Chat;` forms. Not a plan defect.
- I4. Grant store and audit serialise JSON with `JsonSerializerIsReflectionEnabledByDefault=false`; no source-generated context file appears in 70-01 files. Hand-written `Utf8JsonWriter`/`JsonDocument` inside the owned files is fine; a context file in `src/Autonogrammer.Cowork/Tools/` would also be inside the owned glob.
- I5. D-30 text (still says "Program.cs or App.axaml.cs") and CONTEXT line 13 ("or any registry file (R17)") are stale next to D-33/D-34(c); the section header says the amendments win, so no action beyond W9.

---

## 1. Facts verified (spot checks, all by reading the tree)

| Claim | Result |
|---|---|
| D-31: `EngineComposition.Compose` builds `AcpClientOptions` at lines 82-93 without `EnvironmentMode`/`EnvironmentAllowList`; options default `Inherit`; `EngineEnvironmentPolicy` is public with the built-in names listed | Confirmed (`EngineComposition.cs:82-93`; `AcpClientOptions.cs:77,83`; `EngineEnvironmentPolicy.cs:14`, built-ins include PATH, HOME, USER, LOGNAME, LANG, TERM, TZ, TMPDIR, SHELL, PI_CODING_AGENT_DIR, NODE_EXTRA_CA_CERTS, SSL_CERT_FILE, SSL_CERT_DIR, proxies; prefixes `LC_`, `OMP_`) |
| D-32: `PendingPermissionResolver` created at :72, `RunModePermissionResolver` wraps it (:73-80); `AcpApprovalPolicy.Allows` denies a kind outside the allowlist before any resolver | Confirmed (`EngineComposition.cs:72-80`; `AcpAgentSession.cs:1440-1460, 1473`; `AcpApprovalPolicy.cs:37-45`) |
| D-32: `Program.ProductionAllowedKinds` = edit, delete, move, execute, read, search; `RunModeLaunchMapper.AcpToolKinds` has all ten kinds; `RunModeRequestParser.Parse :25`/`AcpKind :66` are public | Confirmed (`Program.cs:18`; `RunModeLaunchMapper.cs:11`; `RunModeRequestParser.cs:13,25,66`) |
| D-33: Program.cs is a 36-line entry; `AgentHead.CreateProduction`, `Build`, `DisposeAsync :252`; `App.CreateWindow :126` creating `new MainWindow(head.Panel, head.Settings.Motion, ..., head.Services)`; `StopHeadThenShutDownAsync`; `ProviderKeyEnvironment.Current` instance property | Confirmed (minor anchor offsets, I1) |
| D-34(a): Harness.McpGateway.csproj references Cowork.Abstractions (line 5) and Harness.Mcp (line 4) | Confirmed |
| D-34(b): Cowork.csproj references Contracts, Cowork.Abstractions, Harness.Abstractions, Harness.AgentLoop only; `LayerMap` has the tuple `Cowork -> Harness.AgentLoop`; Cowork L2, Abstractions L1, McpGateway L2, Native L4, Native.Views L3; `BannedSymbols.NoProcess.txt` bans `Process` | Confirmed |
| D-34(c): Native.csproj has no McpGateway reference | Confirmed (0 matches); but see B1 for the lock files |
| D-34(d): `AssemblyMarker` exists in both Cowork and Cowork.Abstractions with `AssemblyMarkerTests` (two files) | Confirmed |
| D-34(e): Presentation.csproj references Contracts, GenUi, Parser only | Confirmed |
| D-36: `ToolUpdateContent` constructed only inside its file (`None`, `ReadObject`); other callers (`UiFolder.cs:136`, `HarnessEventProjection.cs:204`, `RunProjection.cs:251`, `ChangeProjection.cs:66`, tests) only call `Read`; `UiBlockConverter.KindToType` lacks `image`; both JSON contexts list every block type; `ParityNormalization.KindOf` and `UiBlockJsonTests.AllBlocks` exist; `A2uiSurfaceCatalog.IsAllowedImageSource` and `DataImage.TryCreatePicture` exist; `img-src 'self' data:` is in `vercel.ts:21`, `build-web.sh:36`, `build-demo.sh:23`, `serve-demo.sh:23`; `RenderCspScriptTests` covers `render-csp.sh`; `ImageBlock` name is free | Confirmed |
| D-37: `autonogrammer.css` import list ends with `about.css` then `a11y.css`; `CssContractTests.AutonogrammerCssImportsEveryOtherStylesheet`, colour-literal and reduced-motion tests exist; `StyleAccessibilityTests` auto-discovers sheets; `IndexRoutesTests.ThePageDeclaresEveryWorkbenchAddress` exists | Confirmed; the motion-gate claim is wrong (W11) |
| D-38: `SseParser.MaxLineLength` 1 MiB (line 14), `MaxFrameLength` 4 MiB (line 17) | Confirmed |
| D-35: `HarnessToolApprovalSettings`, `HarnessApprovalOptions`, `ToolApprovalEndpoints`, `DecisionTimeoutSeconds`, `GrantableTools` exist in `services/Autonogrammer.Api` | Confirmed |
| ADR anchors: ADR-0032 D5 block (`SetTurnOrigin` at line 146, `ListOfferedAsync` at 154), ADR-0035 `ComputerAuditLog` line 119, ADR-0037 file name; OQ-321..329 rows | Confirmed |
| 70-01 type list equals the ADR-0032 D5 type set (31 names compared by script) | Confirmed, no missing or extra type |
| All frontmatter paths exist (modify) or are new (create) as the plans say; `scripts/legal/generate-third-party-notices.py` exists; `/tmp/ag-bin/ag-dotnet` exists (serialises dotnet, sets nice, disables node reuse) | Confirmed |

## 2. Layering and registry legality

- No plan adds a project, a LayerMap tuple (`SameLayerAllowed.Count` is pinned at 29 by `ScaffoldRegistrationTests`), a slnx/slnf line, a package, `Directory.Packages.props` or `ContractVersion.Current` edit. Edges used: Cowork (L2) to Contracts, Cowork.Abstractions (L1), Harness.Abstractions (L1), Harness.AgentLoop (L2, tuple exists): legal. McpGateway (L2) to Cowork.Abstractions (L1): legal, already referenced. Native (L4) to Cowork and McpGateway: legal. Presentation, UI, Native.Views, Web get no Cowork reference: consistent.
- Registry-type edits in the phase: Native.csproj plus lock files (deliberate, U-2: see B1 for the missing third lock file) and the Cowork.csproj `EmbeddedResource` item (W9). Contracts JSON contexts and `ParityKinds`/`ParityNormalization` are phase-owned additive edits (manifest line 71 lists "JSON contexts" as per phase). `docs/architecture.md` rows exist already (lines 108, 112); 70-04 edits prose only; `CHANGELOG.md` per phase.
- Phase 69 edits the same Native files (`App.axaml.cs`, `ViewMapping.cs`, `autonogrammer.css`): sequencing statements in the plans are present (69 lands first). See B2 for the overlap on the CoworkSurfaceServices builder.

## 3. Plan structure

- Waves: 01 and 05 are wave 1 with no dependencies and disjoint files; 02, 03, 06 are wave 2 (depends on 01, or 01 and 05) and file-disjoint; 04 is wave 3 (01, 02, 03). Each wave equals max(depends_on)+1 (see B2 for the missing 70-06 dependency of 04). The only shared files are `ViewMapping.cs` (05 wave 1, 06 wave 2) and are sequenced.
- Every task has `read_first`, `action`, `acceptance_criteria` and `verify`; every `<automated>` is directly followed by a `<fails_when>` (checked by script: all pairs match; 70-03 T3, 70-05 T2/T3 and 70-06 T3 carry several pairs). Every command uses `/tmp/ag-bin/ag-dotnet`; the one other command is `python3 scripts/legal/generate-third-party-notices.py --check` in 70-04. No bare `dotnet`. Every plan carries Artifacts, Files owned and the explicit-path commit protocol.
- Live tests: none. VALIDATION's Live list names the deferred items (OD-07 live omp turn, size-budget and a11y smoke with a private headless Chromium, Windows allow-list extras, SSE multi-megabyte image U-1) and no plan starts a container, Xvfb, browser or service.
- Residual contradictions an executor would trip over: W1, W2, W3, W4, W5, W7.

## 4. Requirement coverage

AG-70-1, 2 (70-03); 3, 4, 9, 10, 14 (70-04); 4, 11 (70-06); 5 (70-05); 6, 7, 9, 12, 13 (70-01); 8 (70-02); 13, 14 (70-03). All fourteen appear in some plan's frontmatter `requirements`. 70-VALIDATION.md has a row with command and failure signal for every id (rows 9-28, plus D-17, D-31..D-37, broad rows). Gaps in what the rows execute: W10 (Native image tests, validator tests).

## 5. Scope (re-plan must not silently shrink or widen)

- Unchanged scope: AG-70-1..14 remain, six units remain.
- Additions, each assessed:
  - D-31 (explicit `EnvironmentMode.Allowlist`, three new fields on `EngineCompositionOptions`): mandatory correction, otherwise D-06 "provider keys only" is false. Minimal: defaults leave Code, side chat and unattended engines byte-identical, with a default-options test.
  - D-32 (`PermissionResolverFactory` plus ten ACP kinds): mandatory, otherwise D-10 "every request reaches the resolver" is impossible (verified: the policy denies kinds outside the allowlist before any resolver). Minimal: one optional member and an `ArgumentException` for the factory plus `RunModePolicy`. Note: it also allows `think` and `switch_mode` in Cowork (D-32) which Code denies; accepted by D-32 and recorded.
  - D-36 (ImageBlock registration in both JSON contexts, UiBlockJsonTests row, ParityKinds/KindOf, `ImageSourceCspTests`): mandatory (STJ source generation only and the parity switch throws otherwise); removing `RunProjection`/`ChangeProjection`/`UiFolder`/RenderCsp edits is a shrink of unnecessary files, not of scope.
  - D-34(c) (Native.csproj reference): required by `CoworkToolServer`; see B1.
  - D-35, D-38, D-39: tests and an open-item record; no product scope. D-40 (framework-free contract base): forced by the no-test-package rule; no scope change, but phases 71-73 must call `AssertContractAsync` from one Fact (no downstream plan mentions the base yet: `grep` found none).
  - D-37 (new `cowork-chat.css`, no route, no `@page`): consistent with original D-28; see W11.
- No decision silently narrows scope. B2 is the one place where a locked statement (D-34e mapping) lost its executing task.

## 6. Locked-decision consistency (D-01..D-30 versus D-31..D-40)

- D-31 amends D-02/D-06 (environment): flags of D-02 unchanged. D-32 amends D-01/D-10: D-10's allow of `mcp__cowork_*` by name and path/delete rules unchanged; D-11 matrix extended to `fetch`/`other`/`think`/`switch_mode` without changing existing rows. D-33 amends D-30 (composition site) and the 70-04 composition text; D-36 amends D-27; D-37 amends D-28; D-40 amends D-30 (contract base). All say they amend; no unflagged contradiction found in the CONTEXT decisions themselves.
- Contradictions that remain only in unamended action text: W1-W5. Stale prose in CONTEXT outside the amendments: I5.
- ADR-0021 (hosted API, web head, accessibility): no collision; D-35 keeps Cowork approvals independent of `Harness:Acp:Approval`.

---

## Round 2 (re-verification of the orchestrator's fixes)

Checker: replan-wave-4.Plan70Checker. Scope: only the fixes listed by replan-wave-4; read from the files, nothing built or run.

### Verdict: PASSED (0 BLOCKER, 5 new WARNING, 2 INFO)

### Fix verification
| Item | Result | Evidence |
|---|---|---|
| B1 three lock files | Fixed | 70-04 frontmatter lines 26-28 list `apps/Autonogrammer.Native/packages.lock.json`, `tests/Autonogrammer.Native.Tests/packages.lock.json`, `tests/Autonogrammer.ArchitectureTests/packages.lock.json`; Task 2 `<files>`, action (c) relocks all three (Native, Native.Tests, ArchitectureTests restore commands), Files owned names the `autonogrammer.native` entry; CONTEXT U-2 (line 175) says THREE |
| B2 conversation wiring | Fixed | 70-04 `depends_on` now 70-01, 70-02, 70-03, 70-06 (wave 3 = max+1); Task 2 action (f) builds one `CoworkConversationModel` per session, feeds the single `CoworkApprovalQueue` from the broker and chips from tool-host state, adds one `OpenConversation` member to `CoworkSurfaceServices` plus one `CoworkView.axaml.cs` hunk "only if 69-03 has none"; `CoworkSurfaceServicesFactory` only supplies the delegate to the 69 builder. 69-03 line 95 carries the matching hand-off ("does not carry an equivalent member"), and both files are in 70-04 frontmatter (lines 19-20) and Files owned. `CoworkHeadTests.cs` added (frontmatter line 34, Task 1 `<files>`, VALIDATION row 25); VALIDATION row 26 covers D-41 mapping |
| W1-W4, W7 | Fixed | 70-02 clause gone (0 matches); 70-03 action no longer lists catalog servers (only the quoting amendment remains); 70-04 objective now "composed in AgentHead.Build"; 70-06 action now "taking the SessionViewModel and ComposerViewModel the Native host builds"; `ScriptedAcpTransport` no longer in 70-04 (0 matches), the Rig/ScriptedOmp are named |
| W5 gateway per head | Fixed | action (c): ONE `McpGatewayServer` per head, empty registry, deny-all `McpToolApproval`, `OpenSession`/`CloseSession` per session (`McpGatewayServer.cs:86`, `:101` confirmed) |
| W8 globs | Fixed | 70-01 owns `Abstractions/CoworkContractShapeTests.cs` and `Tools/*` only; 70-04 names five test files, 70-06's accessibility test is outside the glob |
| W9 frontmatter vs `<files>` | Fixed | script check: for all six plans frontmatter equals the union of task `<files>` (no differences); 70-06 Files owned no longer says "sixteen" |
| W10 verify widening | Fixed except as noted in N1/N5 | 70-01 T1 runs ContractShape + ToolHost; 70-03 T1 runs Profile + SystemPrompt + ConfigOverlayWriter; 70-05 T3 runs a Native.Tests command; 70-06 T2 runs Presentation + Native commands; every `<automated>` is followed by a `<fails_when>` (script: all pairs ok, all tasks have read_first/action/acceptance, no bare dotnet) |
| W11 motion | Fixed | 70-06 T3 fails_when and acceptance assert `transition`/`animation`/`@keyframes` absence in `CoworkConversationTests` via `CssSources.ReadStyle`; the ArchitectureTests command was replaced by `IndexRoutesTests`; VALIDATION row 34; CONTEXT "D-37 corrected" |
| W12 D-39 | Fixed | CONTEXT "D-39 corrected" (line 185): ProjectGraphTests unchanged, Web/Native.Views held by D-34e, 70-06 namespace test and 69-06 |
| W13 SUP | Fixed | 70-01, 70-02, 70-04 each prefer narrow filters and allow `docs/open-questions.md` SUP rows (named in Files owned, conditional so not in frontmatter) |
| W14, W15 | Fixed | 70-04 amendment adds the Allowlist note to the architecture paragraph; 70-06 line 82 and CONTEXT "D-29 corrected" give the Cowork-scoped copy constant |
| Waves | OK | 01/05 wave 1 (disjoint), 02/03/06 wave 2 (disjoint), 04 wave 3 |
| Requirements | OK | AG-70-1..14 still each in some frontmatter; VALIDATION rows retained |

### New WARNINGs introduced by the edits
- N1. 70-04 Task 1 (line 96) verifies only `*CoworkSessionHostTests*` although Task 1 also creates `CoworkEngineComposerTests`, `CoworkHeadTests` and appends `EngineCompositionOptionsTests`; Task 2's verify (`*Cowork*`) does not match `EngineCompositionOptionsTests`. The composer, head and options cases are first run by the plan-level verification block. Fix: widen the Task 1 and Task 2 commands with `--filter-class "*CoworkEngineComposerTests*" --filter-class "*CoworkHeadTests*" --filter-class "*EngineCompositionOptions*"`.
- N2. D-41's view half (`OpenConversation` member and the `CoworkView.axaml.cs` hunk showing `ViewMapping.Create(model)`) has no named test; VALIDATION row 26 covers only the Native mapping in `CoworkSessionHostTests`. Fix: add one headless Native test (a `CoworkView` built with services whose `OpenConversation` returns a model shows the conversation view for the selected row) to `CoworkSessionHostTests` or a named file in the 70-04 file list, and a VALIDATION row.
- N3. 70-04 Files owned says "the four named test files" but names five; the Artifacts Tests line omits `CoworkHeadTests`. Cosmetic.
- N4. 70-05 Task 1 verify (line 83) runs only `*ToolBlockContractTests*` although the amendment places the RED `UiBlockJsonTests` image row in Task 1; add `--filter-class "*UiBlockJsonTests*"` (Task 2 already runs both).
- N5. CONTEXT D-34(c) (line 165) and D-39 (line 170) bodies still carry the pre-correction wording (two lock files; "new or extended"); the corrections section overrides them and says so, but a reader of D-34c alone sees two. Optional: edit the bodies or add "(corrected below)".

### INFO
- Conditional edits (Native.csproj, `CoworkSurfaceServices.cs`, `CoworkView.axaml.cs`, `docs/open-questions.md`) are in Files owned with "only if" conditions; the first three are in frontmatter, `docs/open-questions.md` is not (acceptable: conditional).
- `McpToolInvoker` takes `McpToolApproval`; the plan now names the deny-all delegate. No further action.
