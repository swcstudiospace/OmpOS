# Phase 76: Cowork parity B: plugins, Auto mode, isolation - Context
Source: docs/plans/v0.16.0-research/briefs/76-cowork-parity-b.md
**Gathered:** 2026-10-28
**Status:** Ready for planning

<domain>
## Phase Boundary

Cowork and Code sessions share one extension and safety layer: content plugin bundles and skills, connector cards with a three-state tool permission matrix, an Auto (rules) approval mode with deletion protection and folder access modes for Cowork, an OS-level launch wrapper that confines writes, a sub-agent progress view, a local usage meter and desktop notifications. Nothing claims to match Anthropic's classifiers or VM; each limit is stated on the screen that shows the feature. Requirement ids AG-76-1 to AG-76-14 (brief 76 lines 15-28). Work units 76-01 Plugins and skills, 76-02 Connectors, 76-03 Permissions, 76-04 Isolation, 76-05 Activity (sub-agents, usage, notifications), 76-06 Integration (last) (brief lines 44-49). ADR-0037 parts: D7 to D13 and the C3 to C6, C11 to C13, C23, C24, C26, C27, K04, K10, K16, K19, K20 rows of D2. ADR-0037 D3 to D6 are phase 75's and D14 (parity file) is created by 75-06 and owned by 77 for the audit.

Depends on (index phase table): 75-06 (`ILaunchContributor`, `LaunchRequest`, `LaunchContribution`, creation of `docs/parity/cowork-parity.md`), 69 and 70 (`CoworkPermissionMode`, `CoworkPermissionResolver`, `CoworkSessionProfile`, `CoworkPathBoundary`, `CoworkSessionMetaStore`, `CoworkToolHost`, `CoworkToolEvent`). Unit 76-03 edits files those briefs create and follows them. Phase 76 supplies contributors and edits no composer.

Must NOT do: run plugin hooks, map hooks onto ADR-0016, a marketplace or git install, ship any bundle, build a network fence, seccomp filter, Windows filesystem isolation, a Windows toast, phone push, the richer RPC sub-agent source, a host-owned OAuth vault, Accept-edits mode, or a classifier. No `ContractVersion.Current` edit, no registry rows beyond manifest "per phase" rows (R9, R17), no new NuGet package, no edit of `RunModeLaunchMapper` (stays Code-only).
</domain>

<decisions>
## Implementation Decisions

### Principles
- **D-01:** Capabilities are not tied to a surface: Cowork defaults to isolation on, Manual approval; Code defaults to isolation off and its run modes unchanged; connectors, skills, plugins and scheduled behaviour use the same code in both. (ADR-0037 D1.1; D2 K16, K20)
- **D-02:** No claim of equivalence with Anthropic's classifiers: Auto is a rule engine and screens, docs and audit say "Auto (rules)". Consent comes before code runs; with nobody present, wait, never approve. (ADR-0037 D1.3, D1.4)

### Plugin bundles and skills (76-01)
- **D-03:** A bundle is Anthropic layout: optional `.claude-plugin/plugin.json`, `skills/<name>/SKILL.md`, `commands/*.md`, `agents/*.md`, `.mcp.json`, `hooks/hooks.json`; types are named `PluginBundle` because the existing Plugins page is the four fixed omp plugins. Install from a local folder or `.zip` into `~/.autonogrammer/plugins/<id>/` (user) or `<folder>/.autonogrammer/plugins/<id>/` (project). (ADR-0037 D7; AG-76-1)
- **D-04:** Install validates names and refuses path traversal, symbolic links, files over 2 MiB, more than 2,000 files or 50 MiB, and a malformed manifest, each with a named error (`BundleInvalid(code)`); zip-slip and zip-bomb fixtures included; install is atomic with rollback. (ADR-0037 D7; AG-76-2)
- **D-05:** Skills reach omp through `skills.customDirectories` as an overlay entry from `BundleLaunchContributor`; commands join the slash palette as `/<plugin>:<command>`; agents are listed; `.mcp.json` servers join the MCP catalog as `<plugin>.<server>`, off until the user confirms each one (showing the command line), `${CLAUDE_PLUGIN_ROOT}` expands to the bundle directory. (ADR-0037 D7; AG-76-3)
- **D-06:** Hooks in a bundle are listed and never run (`BundleComponentNotLoaded(hooks)`). A project-scope bundle is off until the user enables it (`BundleProjectScopeOff`); a bundle never shadows a built-in skill or command. No bundle ships with the product; the Apache-2.0 `anthropics/knowledge-work-plugins` content is user-installable and not copied; the test fixture bundle is written for the test. (ADR-0037 D7, Open risks 7; AG-76-1, AG-76-3)
- **D-07:** No new skill loader. Added: `skills-state.json` per-skill enable switch (a disabled skill excluded at launch through `--skills`, glob semantics unverified, OQ-454), `ConventionScope.Plugin` in `Harness.Workspace`, the same skill set in Cowork and Code, and a built-in `/create-plugin` skill (`assets/skills/create-plugin/SKILL.md`, `BuiltinSkillManifest`). State files: `plugins-state.json`, `skills-state.json`. (ADR-0037 D8; brief 76-01; AG-76-4)
- **D-08:** `Autonogrammer.Harness.PluginBundles` (L2, `Harness.*` so `Process` is allowed) references `Harness.Abstractions` only: no edge to `Harness.Workspace` or `Harness.Mcp`; the Native composition feeds bundle sources into the workspace and the MCP catalog. (R14; manifest row 16)

### Connectors (76-02)
- **D-09:** A connector is an MCP server entry shown as a card; the 13 presets and consent stay. `McpConnectionChecker` over `McpClientConnector` runs only on the user's Check connection (10 s timeout, cancellation), never at start, and lists tools. OAuth stays omp's (`/mcp reauth`); a host-owned token vault is deferred (OQ-456). (ADR-0037 D9; AG-76-5)
- **D-10:** Tool permission matrix, three states per tool and per server: Always allow (`Mcp(server:tool)` allow rule), Needs approval (no rule), Blocked (deny rule), stored in the permission files via `PermissionsFileEditor`/`PermissionsLoader` and read by the Cowork resolver; Blocked is refused in every mode, Always allow runs in Manual and Auto. (ADR-0037 D9, D10; AG-76-5, AG-76-8)
- **D-11:** Auto never trusts `readOnlyHint`: MCP annotations are untrusted hints (omp's permission request does not carry them), so Auto treats MCP calls like Manual unless a rule allows the tool; the page says so. Catalog-server calls bypass the Cowork tool host, so the resolver and these rules are their only gate; taint demotes them to Manual. No header, argument or URL path reaches any text (canary test). (ADR-0037 D9; brief 76-02)

### Permissions (76-03)
- **D-12:** `CoworkPermissionMode` gains `Auto` (additive): an allow rule or a short read-only command from `KnownSafeCommands` inside the trusted folders runs, a deny rule refuses, everything else asks; tool-provider and MCP calls behave like Manual; a tainted session behaves like Manual for every call. UI label "Auto (rules)"; default stays Manual (OQ-450). Auto maps to `always-ask-cached` in the profile. (ADR-0037 D10; brief 76-03; AG-76-6)
- **D-13:** "Mode" means only `CoworkPermissionMode` (Manual, Auto, Skip); other policies use their own names (`CoworkToolHostOptions.ApprovalTimeout`, `Dispatch:CommandApprovalTimeout`, `UnattendedApprovalTimeout`, `BrowserUse:PolicyMode`). (R16; AG-76-6)
- **D-14:** Skip maps to `--approval-mode always-ask`; the resolver allows every call except deletes and, in a tainted session, `edit`, `delete`, `move`, `execute`. Skip shows a one-time warning and needs the FilesystemIsolated level or a per-session acknowledgement (`SkipNeedsAcknowledgement`): an agreed amendment of ADR-0032 D4, written into ADR-0032 when accepted. (ADR-0037 D10; R16; AG-76-7)
- **D-15:** Deletion protection: in every Cowork mode the ACP kind `delete` and shell commands recognised by a new public `ShellDeleteDetector` (in `Harness.AgentLoop/Safety`, over `ShellCommandAnalysis`) ask: `rm`, `rmdir`, `unlink`, `shred`, `find -delete`, `git clean`, plus wrappers. A deletion built in a script is not recognised and the tests and docs say so. (ADR-0037 D10; AG-76-8)
- **D-16:** Folder grants: trusted folders gain an access mode (read-only or read-write) and a scope (session, project, global) in `CoworkSessionMetaStore`; `CoworkPathBoundary` additionally refuses `/`, the home directory itself and system directories, on top of secrets, `~/.omp`, `~/.ssh` and account-folder refusals; sessions write only inside read-write grants and the session scratch directory; a revoked grant applies to the next session (`FolderChangeAppliesNextSession`). (ADR-0037 D10; AG-76-9)
- **D-17:** Every decision goes to the existing `RunModeAuditLog` with Cowork stage names. (ADR-0037 D10)

### Isolation (76-04)
- **D-18:** `Autonogrammer.Harness.Isolation` wraps the omp child (so its shell tools) through `IProcessLaunchWrapper` in `Harness.Abstractions`, consumed by `AcpStdioTransport` (where `FileName` and the argument list are set); no wrapper means unchanged behaviour. `IsolationContributor` is an `ILaunchContributor` returning the wrapper. Levels `IsolationLevel` {None, ProcessContained, FilesystemIsolated}. (ADR-0037 D11; AG-76-10)
- **D-19:** Linux `bwrap`: `--ro-bind / /`, `--bind` per read-write grant and for session scratch and omp state dirs, `--tmpfs` over denied directories (`~/.ssh`, `~/.aws`, `~/.gnupg`, `~/.kube`, cloud and browser profile dirs, Autonogrammer secrets) and over `/run/user` and Docker socket locations, `--dev /dev`, `--proc /proc`, private `/tmp`, `--unshare-pid --unshare-ipc --unshare-uts --die-with-parent --new-session`; environment scrub removes `SSH_AUTH_SOCK`, `DBUS_SESSION_BUS_ADDRESS`, `DISPLAY`, `WAYLAND_DISPLAY`. macOS: `sandbox-exec -p <profile>` (allow default, deny writes, allow under grants, deny reads of the deny list). Windows: job object (kill on close, no breakaway, memory ceiling), `ProcessContained` only. (ADR-0037 D11)
- **D-20:** Network is not isolated (level shows "network: unrestricted" always, `NetworkUnrestricted`); no `--unshare-net`, no egress proxy, no seccomp (deferred, OQ-452). `IsolationProbe` runs `bwrap --version` and one `bwrap ... true` self-test at session start; failures map to `bwrap_missing`, `userns_disabled`, `probe_timeout`, `platform_unsupported`; a session never silently runs unconfined: it asks to continue (`ContinueWithoutIsolation`). On by default for Cowork, off for Code. Processes start by argument vector only. (ADR-0037 D11; AG-76-10)
- **D-21:** `~/.omp` must be writable and so is readable by the agent's shell: a recorded risk, shown, not hidden. (ADR-0037 D11, Open risk 1)

### Activity (76-05)
- **D-22:** `SubagentPanelModel` shows one row per omp `task` tool call from ACP (agent name, status running/done/failed/cancelled, elapsed time, last output line, number running) via `ISubagentActivitySource`; the only source is `AcpTaskToolActivitySource` over `tool_call` and `tool_call_update`; unnamed rows read "Sub-agent 1, 2" if `rawInput` has no name (OQ-459, `SubagentNamesUnavailable`); output is plain text, never markup or an opening link; the RPC `get_subagents` source is deferred. (ADR-0037 D12; AG-76-11)
- **D-23:** `UsageMeterModel` sums ACP `usage_update{size, used, cost}` and per-turn usage per session and per day into a local `usage-ledger.jsonl` (`FileUsageLedger`, no prompt content, 90 days, mode 0600); `MainView.CreateExport` receives the real history instead of an empty one; plan limits and billing omitted (`UsageUnavailable` when the engine reports no cost). (ADR-0037 D13; AG-76-12)
- **D-24:** `INotificationSink` fans out `NotificationRequest` {kind, title, summary, sessionId, privacy} to the in-app centre (toast plus list with live region) and `OsNotificationSink` (Linux `notify-send`, macOS `osascript` with text as `on run argv` parameters, never spliced into script source); no Windows OS notification (OQ-455). Kinds: run finished, approval needed (`ApprovalRequested` tool event and permission queue), scheduled task finished, failed, missed or waiting. (ADR-0037 D13; AG-76-13)
- **D-25:** `NotificationPolicy`: suppress OS notification while the window is focused, coalesce repeats within 30 s, at most 10 per minute, honour Do-not-disturb, title only on the OS surface unless "Show content" is on; commands strip control characters and cap at 200 characters. (ADR-0037 D13; brief 76-05)

### Integration (76-06)
- **D-26:** 76-06 composes contributors in `SessionEngineSet`, `EngineComposition` and the Cowork host, `apps/Autonogrammer.Native/Composition/*`, `SettingsSection`, `SettingsWindowFactory`, `SettingsPageViews`, `ViewMapping`, web information pages with a desktop notice (`WebInfoPages.cs`); writes `docs/reference/cowork-extensions.md`, `docs/reference/cowork-safety.md`, `docs/runbooks/cowork-isolation.md` (lists owner actions, including the Live isolation run), grades this phase's parity rows (C3 to C6, C11 to C13, C23, C24, C26, K04, K10, K16, K19, K20: PARTIAL, each MET only with a named passing test class; rows needing omp stay PARTIAL until a live run). (brief 76-06; ADR-0037 D2; AG-76-14; R17)

### Claude's Discretion
- Internal class layout of the three new `Harness.*` projects; deny-list defaults beyond those named; the exact `bwrap` binds for omp state; Seatbelt profile wording; matrix page layout; unnamed sub-agent labels; notification copy; usage chart form; which of the three 76-05 folders is staffed in parallel.
</decisions>

<specifics>
## Specific Ideas

- Isolation pattern is Anthropic's sandbox-runtime (read everything except a deny list, write only to grants), reimplemented in C#; no code or Node sidecar is taken (ADR-0037 D11).
- Each limit is stated where the feature is shown: "Auto (rules)", "network: unrestricted", "hooks listed, not run", sub-agent view limited to ACP.
- Live isolation test: `AUTONOGRAMMER_LIVE_ISOLATION=1`; without the variable the tests skip, they do not fail (brief 76 line 57).
</specifics>

<canonical_refs>
## Canonical References

- `docs/decisions/ADR-0037-cowork-parity-remainder.md` - D7 to D13, D2 rows, Open risks 1 to 10.
- `docs/plans/v0.16.0-research/briefs/76-cowork-parity-b.md` - requirements, units, success criteria, OQ-450 to OQ-459.
- `docs/plans/v0.16.0-research/briefs/00-index.md` - phase table row 76, R11, R14, R16, R17.
- `docs/plans/v0.16.0-research/briefs/00-scaffold-manifest.md` - rows 16, 17, 18 (new `Harness.PluginBundles`, `.Isolation`, `.Notifications`), `ProcessSpawningAssemblies`.
- `docs/plans/v0.16.0-research/06-cowork-parity-and-references.md` - rows C3 to C6 (lines 64-67), C11 to C13 (72-74), C23, C24, C26 (84, 85, 87), section 6 references (line 113), risks (line 141).
- `docs/reference/run-modes.md` - lines 31 (no classifier), 52 (order of refusal), 80 (untrusted workspace), 108 (omp has no OS sandbox), 130 (annotation names unverified); `docs/reference/mcp.md:18` (no connection checker registered), `:23`; `docs/reference/builtin-skills.md`.
- `docs/plans/v0.15.0-research/04-omp-cursor-map.md` - lines 13 (`skills.customDirectories`, `--skills`), 86, 96 (RPC `get_subagents`).
- `docs/quality/cursor-parity-audit-2026-10-28.md` - rows AC-071, AC-081, AC-083, AC-116, AC-175. `docs/open-questions.md` - OQ-168, OQ-450 to OQ-459.

### Existing code (verified)
- `src/Autonogrammer.Harness.Acp/AcpStdioTransport.cs` (`FileName` at 219, `ArgumentList.Add` at 232); `src/Autonogrammer.Harness.AgentLoop/Safety/{ShellCommandAnalysis,KnownSafeCommands}.cs` (both `internal`), `PermissionsLoader.cs`, `PermissionsFileEditor.cs`, `RunModeAuditLog.cs`, `RunModePermissionResolver.cs`, `RunModeLaunchMapper.cs`.
- `src/Autonogrammer.Harness.Mcp/Catalog/IMcpConnectionChecker.cs:9` (interface exists, no implementation registered), `McpClientConnector.cs`; `src/Autonogrammer.Harness.Workspace/Conventions/ConventionScope.cs` (`Project`, `User`, built-in), `BuiltinSkills/BuiltinSkillManifest.cs`; `assets/skills/{agent-review,create-hook,create-rule}/`; `src/Autonogrammer.Presentation/Settings/SettingsSection.cs:22` (`Plugins = 5`); `src/Autonogrammer.Harness.Acp/AcpWireMapping.cs:29` (`usage_update`); `src/Autonogrammer.Native.Views/MainView.axaml.cs:1520` (`CreateExport`).
- Tests: `tests/Autonogrammer.Harness.Mcp.Tests/McpHttpTests.cs` (loopback HTTP fixture), `tests/Autonogrammer.ArchitectureTests/{RepoHygieneTests,HeadMotionTests,SuppressionLedgerTests}.cs`, `tests/Autonogrammer.Native.Tests/{ThemeContrastTests,Theme/ColourLiteralTests}.cs`.
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `PermissionsLoader` and `PermissionsFileEditor` for the matrix rules; `ShellCommandAnalysis` as base for the delete detector; `McpClientConnector` for the checker; convention and skill discovery (roots `.autonogrammer`, `.omp`, `.cursor`, `.claude`, `.codex`, `.agents`; ten skills ship); `RunModeAuditLog`; `AcpWireMapping` usage frames.
- Created by earlier phases: `ILaunchContributor`, `LaunchContribution`, parity file (75-06); `Autonogrammer.Cowork` types (69, 70); the three new L2 projects (scaffold step).

### Established Patterns
- `Autonogrammer.Harness.*` is the only family allowed to spawn processes (`Directory.Build.props:52`); processes start by argument vector, never a shell; golden-file tests for argv and profile text; Live tests are class-level `[Trait("Category","Live")]` plus a runtime env gate with `Assert.Skip` (report 09:69).
- Internal helpers (`ShellCommandAnalysis`, `KnownSafeCommands`) are `internal` in `Harness.AgentLoop`; `Cowork` has a same-layer tuple to `Harness.AgentLoop` (manifest line 65).

### Integration Points
- `AcpStdioTransport` wrapper seam, `CoworkSessionProfile`, `OmpLaunchPlanner` (Code isolation default off), `SessionEngineSet`, `EngineComposition`, Settings pages (Plugins, Connectors, Permissions), `ViewMapping`, `WebInfoPages.cs`.
</code_context>

<deferred>
## Deferred Ideas and Omitted-by-Decision

- OMITTED (reason, source): plugin directory, organisation states, developer portal (need a store that does not exist; D7); organisation-provisioned skills (D8); Anthropic connector directory and MCPB extensions (no verified bundle spec; D2 C13); Claude in Chrome (C15, classifiers and extension not reusable); Admin controls, Managed settings, Compliance and Analytics APIs (no organisation tenancy; C27, C28, K18); plan limits and billing (C26); Claude Duet (C30); iOS Simulator pane (K15).
- DEFERRED: cloud execution C2; git or marketplace plugin install; hooks mapping (OQ-453); network fence and seccomp (OQ-452); Windows filesystem isolation (OQ-457); Windows OS notification (OQ-455); phone push (ADR-0036 excludes it); RPC sub-agent source (AC-083); background shells and cross-session messaging (K10); Accept edits mode (OQ-458); host-owned OAuth vault (OQ-456); keep-awake for scheduled runs (OQ-451).
- Open questions and defaults: OQ-450 default mode Manual; OQ-451 no keep-awake; OQ-452 no network fence; OQ-453 hooks listed, never run; OQ-454 probe `--skills` and overlay key at launch, show a disabled skill that cannot be excluded as such; OQ-455 in-app only on Windows; OQ-456 deferred; OQ-457 process containment only; OQ-458 Manual, Auto, Skip and Plan only; OQ-459 first test of 76-05 on recorded frames.
</deferred>
