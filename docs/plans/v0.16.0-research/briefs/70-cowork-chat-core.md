# Brief 70: Cowork chat core on omp over ACP and the Cowork tool host

Decision record: [ADR-0032](../../../decisions/ADR-0032-cowork-shell-sessions-chat-and-tool-host.md) (D4 chat core, D5 tool host contract, D6 policy). Research: reports 02, 06, 07 (section 4, section 5), 03, 09. Names are post-rename (Autonogrammer becomes Autonogrammer; `Autonogrammer.Harness.McpGateway` is `Autonogrammer.Harness.McpGateway`); the tree on disk still uses the old names. The bare token `omp`, `omp acp`, `OMP_*` and `models.yml` keep their names.

## Goal

A signed-in user holds a Cowork conversation with the local omp engine: streaming, stop, retry, steering, history after a restart, file and image attachments, a Cowork profile (system prompt, permissions, folder boundary) that differs from Code, and one governed seam, the tool host, through which Artifacts, Browser Use and Computer Use hand tools to omp, with approvals, caps, audit and images that reach both the model and the transcript.

## Requirements

- AG-70-1: `CoworkSessionProfile` produces the omp launch of a Cowork session: `--no-extensions`, an approval mode from the Cowork permission mode, `--add-dir` per trusted folder, an absolute `--append-system-prompt` file path, and a `--config` overlay with `browser.enabled: false` and `computer.enabled: false`.
- AG-70-2: `CoworkPermissionResolver` denies paths outside the trusted and Storage folders, always asks before a delete, allows `mcp__cowork_*` tools at the omp level, and follows Manual, Auto or Skip (`CoworkPermissionMode`; Auto is shown as "Auto (rules)") for the rest.
- AG-70-3: `CoworkSessionHost` implements `IAgentPanelHost` over the Cowork registry and composes engines with `EngineComposition.Compose`, never through the Code planner.
- AG-70-4: A Cowork conversation supports send, stop, retry, steer, resume from history (`session/load`), file attachments and image attachments, and refuses images with a specific message when the model reads none.
- AG-70-5: Tool-call updates carry images: `ToolUpdateContent.Images`, an image block in the transcript fold, the views, the export and the web projection, with alt text.
- AG-70-6: `Autonogrammer.Cowork.Abstractions` holds exactly the contract of ADR-0032 D5 and nothing else.
- AG-70-7: `CoworkToolHost` runs the eight-step call sequence of D5 for every call and enforces the descriptor rules and result caps.
- AG-70-8: `McpGatewayServer.Start(registry, invoker, coworkTools)` serves Cowork tools beside registry tools with disjoint names and maps results, images included, to `CallToolResult`.
- AG-70-9: `CoworkPolicyGate`, `CoworkApprovalBroker`, the grant store and the audit hook implement D6; `EveryCallOnExecutingDevice` is never satisfied by a grant, a mode or a remote decision.
- AG-70-10: `CoworkToolServer` starts one loopback gateway for Cowork, health-checks it before `session/new` and degrades with a visible notice, never by failing the session.
- AG-70-11: Cowork conversation models and views (Native and Web) show the transcript, composer, approval cards, provider status chips and the dock host.
- AG-70-12: `FakeCoworkToolProvider` and a contract test base let ADR-0033, 0034 and 0035 prove their providers against the same rules.
- AG-70-13: The host taints a session when a result carries untrusted content, raises `CoworkToolEvent`s (started, approval requested, completed, failed, denied), and in a tainted session Auto and Skip no longer apply to actuating calls or to `edit`, `delete`, `move` and `execute` (tests in units 1 and 3).
- AG-70-14: `ConfigOverlayWriter.WriteYaml(sessionStateDirectory, yaml)` (new, `Autonogrammer.Harness.AgentLoop/Launch`, owner-only mode, deleted on dispose) writes the Cowork overlay; the Cowork environment holds provider keys only and `CoworkPathBoundary` refuses the secrets, `~/.omp`, `~/.ssh` and account folders as trusted folders (tests in units 3 and 4).

## Depends on

- Brief 69 units 1 to 3 (registry, models, `SurfaceHost`, `CoworkSurfaceServices`); the unit order here follows; `ViewMapping.cs` and `CoworkSurfaceServices` are edited after brief 69 lands.
- `ICoworkAccountGate` (owned by `Autonogrammer.Cowork`: signed in, account folder key; brief 69 unit 1 defines it, the Native head implements it over ADR-0030 `IAccountSession`). `ICoworkToolAudit` is implemented here by the local `FileCoworkToolAudit` (owner-only hash-chained JSONL, the `ComputerAuditLog` pattern, unit 1); nothing from it goes to the ADR-0031 ledger.
- Briefs of ADR-0033, 0034 and 0035 implement providers against unit 1; none is required to finish this brief.

## Projects and files owned

New: `src/Autonogrammer.Cowork.Abstractions` (L1; `AssemblyMarker.cs`, the D5 types in `Tools/*.cs`) and its tests inside `tests/Autonogrammer.Cowork.Tests/Abstractions`; in `src/Autonogrammer.Cowork` (added by this brief) `Tools/CoworkToolHost.cs`, `CoworkToolHostOptions.cs` (+ validator), `CoworkDescriptorValidator.cs`, `CoworkResultLimiter.cs`, `CoworkPolicyGate.cs`, `FileCoworkGrantStore.cs`, `FileCoworkToolAudit.cs`, `FakeCoworkToolProvider.cs`, `Chat/CoworkSessionProfile.cs`, `CoworkSystemPrompt.cs` + `cowork-system-prompt.md` (embedded resource), `CoworkSystemPromptOptions.cs` (budgets: base 4 KiB, standing-context 2 KiB, browser guidance 1 KiB, computer guidance 1 KiB, validated total at most 8 KiB), `CoworkPermissionResolver.cs`, `CoworkPathBoundary.cs`, `CoworkPermissionMode.cs` (`Manual`, `Auto`, `Skip`); in `apps/Autonogrammer.Native/Cowork/` `CoworkSessionHost.cs`, `CoworkEngineComposer.cs`, `CoworkToolServer.cs`, `CoworkApprovalBroker.cs`, `CoworkApprovalBridge.cs`, `CoworkHead.cs`, `CoworkSurfaceServicesFactory.cs`; in `src/Autonogrammer.Presentation/Cowork/` `CoworkConversationModel.cs`, `CoworkApprovalQueue.cs`, `ProviderStatusChip.cs`; in `src/Autonogrammer.Native.Views/Views/Cowork/` `CoworkConversationView`, `CoworkComposerView`, `CoworkApprovalCard`; in `src/Autonogrammer.UI/Components/Cowork/` `CoworkConversation.razor`, `CoworkComposer.razor`, `CoworkApprovalCard.razor`; tests `tests/Autonogrammer.Harness.McpGateway.Tests`, `tests/Autonogrammer.Native.Tests/Cowork`, `tests/Autonogrammer.Presentation.Tests/Cowork`, `tests/Autonogrammer.UI.Tests/Cowork`.

Existing files touched: `src/Autonogrammer.Harness.McpGateway/McpGatewayServer.cs`, `McpGatewayDispatcher.cs`, `McpGatewayCoworkResults.cs` (new), `Autonogrammer.Harness.McpGateway.csproj` (reference to Cowork.Abstractions); `src/Autonogrammer.Contracts/Ui/ToolUpdateContent.cs` and the `ToolUpdateImage` record; every caller (`Autonogrammer.GenUi/UiFolder.cs`, `UiToolBlocks.cs`, `Autonogrammer.Sessions/HarnessEventProjection.cs`, `services/Autonogrammer.Api/Services/RunProjection.cs` and `ChangeProjection.cs` where they construct or deconstruct the record), a new `ImageBlock` in the GenUi block set and its renderers in Native.Views, UI and `Autonogrammer.Presentation/Export`; `apps/Autonogrammer.Native/Program.cs` or `App.axaml.cs` (compose `CoworkHead`); `docs/architecture.md` prose and `CHANGELOG.md`. No registry file is edited: the scaffold step (00-scaffold-manifest.md) created and registered every project (LayerMap, slnx, slnf, locks, csproj references) and the single `ContractVersion` bump; the `ToolUpdateContent` change is additive (`Contracts/Ui` is not a plugin contract, ADR-0008 section 7) and this brief does not touch `ContractVersion.Current`.

## Work units

1. **Tool host contract and host.** Owner: `Autonogrammer.Cowork.Abstractions`, `Autonogrammer.Cowork/Tools`. Files: the D5 types, host, gate, limiter, validator, grant store, fake provider, `FileCoworkToolAudit` (tests: hash chain verifies and a tampered line is detected, owner-only mode, a write failure never fails the call). No registration: the scaffold step did it. Tests: descriptor rules (name regex, no `__`, no `cowork_` prefix, provider prefix, 40 characters, 1024-character description); the eight-step sequence in order with a scripted gate, broker and provider; an unoffered tool refused before preview; image tools hidden for a text-only model; every cap (text 65,536 characters, 2 images, 3 MiB each, 6 MiB total, mime allow-list) fails the result and never truncates; a provider exception gives a fixed failure text; the timeout and the caller token cancel; audit failure never fails the call; one call at a time per session; `EveryCallOnExecutingDevice` is never satisfied by `AllowedAlways`, a stored grant or Skip; `MustBeLocal` with a broker answer `Unavailable` denies; the origin comes from the lease, never from arguments. Acceptance: AG-70-6, 7, 9, 12.
2. **Gateway extension.** Owner: `Autonogrammer.Harness.McpGateway`. Files: server, dispatcher, result mapper, csproj. Tests: existing gateway tests unchanged and passing; `tools/list` shows registry names then Cowork names verbatim; `tools/call` resolves registry first then host; a name with `__` never reaches the host; an image result arrives at the real `ModelContextProtocol` client as `ImageContentBlock` with the same bytes and mime type; an ungranted Cowork tool is refused before the host; sessions cannot see each other's tools; bearer, Host and Origin checks unchanged. Acceptance: AG-70-8.
3. **Profile and permissions.** Owner: `Autonogrammer.Cowork/Chat`. Files as listed. Tests: exact argument list for Manual, Auto and Skip; prompt file written with owner-only mode and an absolute path, start refused when it cannot be written; overlay keys; `CoworkPathBoundary` (symlink and `..` escapes, case rules per platform, a path equal to a folder root, UNC and drive-root cases compiled but only Linux executed); resolver decisions for read, edit, delete, move, execute outside and inside folders, `mcp__cowork_*`, Manual, Auto and Skip; the embedded base prompt is at most 4 KiB and names the six required rules; `CoworkSystemPromptOptions` refuses a section over its budget (standing-context 2 KiB, browser guidance 1 KiB, computer guidance 1 KiB) and a total over 8 KiB. Acceptance: AG-70-1, 2.
4. **Native host and composition.** Owner: `apps/Autonogrammer.Native/Cowork`. Files as listed. Tests (headless, scripted ACP transport as in existing head tests): create, open, close and reload over the Cowork registry; `RecoverAfterRestart` once; engine composed with the profile, the `cowork` HTTP MCP entry (name, loopback URL, `Authorization` header) and the user's MCP servers; gateway health check failure starts the session without Cowork tools and raises the degraded notice; model without vision hides image tools and image attachments with the reason; approval broker raises a `PendingToolApproval`, completes on decision, times out after `CoworkToolHostOptions.ApprovalTimeout` (120 s), and `MustBeLocal` requests are never completed by a Dispatch decision; sign-out stops engines after confirmation and keeps rows; the `CoworkSurfaceServices` factory plugs into `SurfaceHost` of brief 69. Acceptance: AG-70-3, 4 (engine side), 10.
5. **Images in the transcript.** Owner: Contracts, GenUi, Sessions, Api projections, Presentation transcript and export, Native.Views and UI transcript renderers. Tests: `ToolUpdateContent.Read` reads `content` entries of type `image` with `data` and `mimeType` and ignores a malformed one without throwing; both folds (GenUi and Sessions) produce the same `ImageBlock` for the same recorded events (the existing replay test); the view shows the image with alt text and a size cap; export carries it; a 10 MB payload is refused with a placeholder; the CSP of the web head is not widened (`data:` image source is added only if the render needs it, and `RenderCspScriptTests` is updated in the same change). Acceptance: AG-70-5.
6. **Conversation models and views.** Owner: `Autonogrammer.Presentation/Cowork`, `Native.Views/Views/Cowork`, `UI/Components/Cowork`. Tests: `CoworkConversationModel` composes `SessionViewModel` and `ComposerViewModel` and the approval queue; provider status chips (Available, Needs setup, Unavailable with reason); attachment chips with remove; focus order (transcript, composer, approvals); `AccessibilityNameTests` and bUnit `AccessibilityTests` for cards and chips; reduced motion; no colour literals. Acceptance: AG-70-11, AG-70-4 (UI side).

## Success criteria

- `dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release` passes (tool host, gate, limiter, profile, permissions, fake provider contract tests).
- `dotnet test --project tests/Autonogrammer.Harness.McpGateway.Tests -c Release` passes, including the real MCP client image test.
- `dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*Cowork*"` passes.
- `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release`, `tests/Autonogrammer.UI.Tests` and `tests/Autonogrammer.Sessions.Tests` pass (image fold and replay parity).
- `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*ProjectGraphTests*"` passes; `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` is clean; `python3 scripts/legal/generate-third-party-notices.py --check` is clean (no new package).

## Threat model

- Untrusted tool output (pages, screens, files) steering the agent: results carry `IsUntrusted`, the gateway wraps them in a labelled envelope, the system prompt tells the model they are data; consequential actions still need host-observed approvals (a delete, a send, Computer Use).
- Gateway abuse: loopback IPv4 only, per-start random bearer, Host and Origin checks, 1 MiB request cap, session-scoped tool list; the bearer is passed only in the `session/new` header list and is never logged or put in `ToString`.
- Origin spoofing: the turn origin is set on the lease by the chat core; a model-supplied argument named origin is ignored.
- Injection chaining: after untrusted content has entered a session (taint), a later instruction hidden in it cannot reach an actuating tool or a write without a human decision, even in Skip; the taint flag is host state and no tool result can clear it.
- Approval spoofing and fatigue: the card shows the host-observed target and details, never model text; `EveryCallOnExecutingDevice` needs a local decision each time; the wait ends after 120 s as a denial.
- Double governance: omp runs with an asking approval mode in both Manual and Skip (`always-ask-cached` and `always-ask`, never `yolo`), so every request reaches the resolver; the gateway gate is independent of omp's approval mode, so Skip cannot reach a Computer Use access grant without a local approval; the omp-level allow of `mcp__cowork_*` is safe only because of that, and the name prefix is verified by omp's minting rule (`tool-bridge.ts:464-494`). `ByProviderGate` tools rely on the provider asking through its broker, which the shared provider contract tests assert.
- Folder boundary: omp has no OS sandbox (`docs/reference/run-modes.md`); the boundary is the resolver, so canonicalisation covers symlinks and `..`; shell commands (`execute`) outside the folders are asked about, not trusted.
- Resource exhaustion: the result caps, one call per session, the call timeout and the per-session image count bound memory and model cost.
- Secrets: the audit entry holds no arguments or result bytes; provider keys reach omp only through the existing `ProviderKeyEnvironment`; the resume reference is never logged.
- Prompt file: written owner-only inside the session folder, read by omp as a file path only when it exists (verified rule, `system-prompt.ts:285-299`).

## Degraded and error states

- Gateway cannot start or fails its health check: the session starts without Cowork tools and the conversation shows "Cowork tools are unavailable: <reason>".
- Provider unavailable or needs setup: its tools are not offered; the chip shows the provider's reason (for example "Chromium is not installed").
- Model reads no images: image tools and image attachments are off with "This model cannot read images"; the model can be changed in place.
- Approval timed out or no window to ask: the call returns "The tool call was not approved." and the card shows Timed out or Unavailable.
- Result over a cap or of a refused type: a failure result with a fixed message; nothing is truncated.
- omp not found, handshake timeout, `session/new` failure: the existing engine error states of `AgentSessionRuntime`, with Retry; a lost engine offers Resume (`session/load`).
- Prompt file cannot be written: the start is refused with the reason; no session runs with the Code prompt by accident.
- Attachment over `AcpImageLimits` (8 images, 10 MB each, 25 MB total) or a missing file: the chip shows the reason and the prompt is not sent.
- Sign-out while running: confirmation, then engines stop and rows stay.

## Cannot be verified on this host

- A live omp turn through the gateway, `--append-system-prompt` and overlay effects in ACP, omp's MCP request timeout against a long approval, and the `mcp__cowork_` name seen by the model (all feasible by source only; OD-07 needs an owner approval).
- Real providers (ADR-0033, 0034, 0035) and any model's use of images.
- Windows and macOS path rules and the composer on those heads (compile-checked only); screen readers.

## Open questions

- OQ-321: Is an approval wait of 120 s shorter than omp's MCP client timeout for ACP-declared servers? Default: keep 120 s; measure when OD-07 allows a live turn; owner: autonogrammer-maintainers.
- OQ-322: Does omp apply `--append-system-prompt` and the `browser.enabled` overlay in `omp acp` sessions? Default: assume yes by source, verify with the live turn, fail closed by also hiding omp's browser tool if the check fails; owner: autonogrammer-maintainers.
- OQ-323: Is a limit on running Cowork engines needed? Default: none; one omp child per open task, stopped by the same idle-stop setting as Code (`runtime.idleStopHours`); owner: product owner.
- OQ-327: Is the omp-level allow of `mcp__cowork_*` acceptable? Default: yes, the gateway gate decides; owner: Acp owner.
- OQ-328: Does deleting a Cowork task delete omp's own session file under `~/.omp`? Default: only the Autonogrammer row and folder; omp's file stays and is listed in the delete dialog; owner: product owner.
- OQ-329: May Cowork load the user's omp extensions and skills? Default: no (`--no-extensions`); ADR-0037 decides the skills and plugins path; owner: product owner.

## Index rows

ADR index row: see brief 69 (ADR-0032 is one record for both briefs). OQ rows for `docs/open-questions.md`: OQ-321, OQ-322, OQ-323, OQ-327, OQ-328 and OQ-329 above (columns id, question, why it matters, default taken, owner, status "open (non-blocking default)"); OQ-320 and OQ-324 to OQ-326 are in brief 69. The final block OQ-320 to OQ-339 is shared by briefs 69 and 70 (ADR-0032).
