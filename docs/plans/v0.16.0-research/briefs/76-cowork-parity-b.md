---
description: "Phase 76 brief (v0.16.0): Cowork parity part B, plugin bundles, skills, connectors, the Auto approval mode, local isolation, sub-agent view, usage and notifications, with units, success criteria and threat model."
---

# Phase 76: Cowork parity B (plugins, skills, connectors, permissions, isolation, sub-agents, notifications)

Status: design brief, nothing built. Written 2026-10-28 for ADR-0037 ([decision record](../../../decisions/ADR-0037-cowork-parity-remainder.md)). Names are post-rename (Autonogrammer becomes Autonogrammer); paths use that spelling although the repo on disk still uses the old one. Facts cite the research reports of this folder, the repo and the sibling records.

## Goal

Cowork and Code sessions share one extension and safety layer: content plugin bundles and skills, connector cards with a three-state tool permission matrix, an Auto (rules) approval mode with deletion protection and folder access modes for Cowork, an OS-level launch wrapper that confines writes, a sub-agent progress view, a local usage meter and desktop notifications. Nothing here claims to match Anthropic's classifiers or VM; each limit is stated on the screen that shows the feature.

## Requirements

- AG-76-1: A user can install a plugin bundle (Anthropic layout) from a local folder or `.zip` into user or project scope, enable or disable it, and see which of its components load; a project-scope bundle is off until enabled.
- AG-76-2: Install refuses path traversal, symbolic links, files over 2 MiB, more than 2,000 files or 50 MiB, and a malformed manifest, each with a named error.
- AG-76-3: Bundle skills reach omp through `skills.customDirectories`; commands join the palette as `/<plugin>:<command>`; agents are listed; `.mcp.json` servers join the MCP catalog off until confirmed one by one; hooks are listed and never run.
- AG-76-4: A per-skill enable switch excludes a disabled skill at launch; a built-in `/create-plugin` skill ships.
- AG-76-5: A connector card shows state; Check connection (user action only) lists tools; each tool and server is Always allow, Needs approval or Blocked, stored as permission rules.
- AG-76-6: `CoworkPermissionMode` gains `Auto`; Auto allows what the rules and the read-only list allow, asks otherwise, behaves like Manual for tool-provider and MCP calls and in a tainted session; the UI says "Auto (rules)"; the default stays Manual. "Mode" means only `CoworkPermissionMode` (Manual, Auto, Skip); other timeouts and policies use their own names (`CoworkToolHostOptions.ApprovalTimeout`, `Dispatch:CommandApprovalTimeout`, `UnattendedApprovalTimeout`, `BrowserUse:PolicyMode`).
- AG-76-7: Skip needs a FilesystemIsolated session or a per-session acknowledgement (ADR-0037 D10; an agreed amendment of ADR-0032 D4, written into ADR-0032 when it is accepted).
- AG-76-8: In every Cowork mode the ACP kind `delete` and shell commands recognised as deleting ask; the Cowork resolver also applies `Mcp(...)` allow and deny rules from the permission files.
- AG-76-9: Trusted folders gain an access mode (read-only or read-write) and a scope (session, project, global); `CoworkPathBoundary` also refuses `/`, the home directory itself and system directories.
- AG-76-10: The omp child starts through a process wrapper from a launch contribution: bubblewrap on Linux, `sandbox-exec` on macOS, a job object on Windows, with a probe, a reported level and named degraded states.
- AG-76-11: The sub-agent panel lists `task` tool calls from the ACP stream with status, elapsed time and the number running, accessibly.
- AG-76-12: A usage meter sums `usage_update` and per-turn usage into per-session and per-day views and a local ledger; the export history is no longer empty.
- AG-76-13: Notifications fan out to an in-app centre and OS sinks (Linux, macOS) with focus suppression, coalescing, Do-not-disturb and title-only privacy by default.
- AG-76-14: The phase's rows of `docs/parity/cowork-parity.md` are graded with evidence.

## Depends on

- Phase 75 unit 75-06: `ILaunchContributor`, `LaunchRequest`, `LaunchContribution` (arguments, environment, overlay entries, prompt paragraph, first-prompt block, process wrapper) and the creation of `docs/parity/cowork-parity.md`. Phase 76 supplies contributors and edits no composer.
- Briefs 69 and 70 (ADR-0032): `CoworkPermissionMode`, `CoworkPermissionResolver`, `CoworkSessionProfile`, `CoworkPathBoundary`, `CoworkSessionMetaStore`, `CoworkToolHost` and its `CoworkToolEvent` stream. Unit 76-03 edits files those briefs create and follows them. The author of ADR-0032 agreed (2026-10-28) to Auto, the access modes, the Skip mapping (`--approval-mode always-ask`, already in ADR-0032 D4) and the connector rule; `RunModeLaunchMapper` stays Code-only.
- Existing code: `Harness.AgentLoop` (Safety, `ShellCommandAnalysis`), `Harness.Mcp`, `Harness.Workspace`, `Harness.Acp`, `Harness.Abstractions`.

## Projects and files owned

New projects (created and registered by the scaffold step; this phase adds code only), all L2 and named `Autonogrammer.Harness.*` so the process-spawn ban allows `Process`: `src/Autonogrammer.Harness.PluginBundles`, `src/Autonogrammer.Harness.Isolation`, `src/Autonogrammer.Harness.Notifications`, each with `tests/<name>.Tests`. No new NuGet package. Isolation and notifications start processes only by argument vector, never through a shell. Existing files touched: `Harness.Abstractions` (new `IProcessLaunchWrapper`), `Harness.Acp/AcpStdioTransport.cs` (optional wrapper; `FileName` and the argument list at `:219-232`), `Autonogrammer.Cowork/{CoworkPermissionMode,CoworkPermissionResolver,CoworkPathBoundary,CoworkSessionMetaStore}.cs` and `CoworkSessionProfile.cs` (Auto mapping only), `Harness.AgentLoop/Safety/ShellDeleteDetector.cs` (new) and `Harness.AgentLoop/Usage/*` (new), `Harness.Mcp/*` (checker, bundle source), `Harness.Workspace/{Conventions/ConventionScope.cs,BuiltinSkills/BuiltinSkillManifest.cs}` and `assets/skills/create-plugin/SKILL.md`, Presentation, Native.Views, apps composition. Registration of every project is done by the scaffold step; 76-06 edits a registry file only for rows `00-scaffold-manifest.md` marks "per phase" (OpenAPI, notices when a package lands, `docs/architecture.md` prose, parity audit rows), and no phase edits `ContractVersion.Current`.

## Work units (file-disjoint; 76-06 last)

| Unit | Owner project and files | Tests | Acceptance |
| --- | --- | --- | --- |
| 76-01 Plugins and skills | `Harness.PluginBundles`: `BundleManifestReader`, `BundleValidator`, `BundleInstaller` (folder and zip), `BundleStateStore` (`plugins-state.json`), `SkillStateStore` (`skills-state.json`), `BundleLaunchContributor`, `BundleCommandSource`, `BundleMcpSource`; `ConventionScope.Plugin`; `/create-plugin` skill; Presentation `Settings/Plugins` bundle section | `BundleValidatorTests` (traversal, symlink, size and count caps, bad names, zip-slip and zip-bomb fixtures), `BundleInstallerTests` (atomic, rollback), `BundleLaunchContributorTests` (overlay entry and `--skills` filter byte-exact), `BundleMcpSourceTests` (namespace, off until confirmed, `${CLAUDE_PLUGIN_ROOT}`), skill manifest test | Hooks listed, never run; a bundle never shadows a built-in skill or command (precedence tests); the fixture bundle is written for the test, no Anthropic file is committed |
| 76-02 Connectors | `Harness.Mcp`: `McpConnectionChecker` over `McpClientConnector` (explicit action, 10 s timeout, cancellation); Presentation `Settings/Connectors` (cards, matrix model, `IConnectorPermissionStore`); `apps/Autonogrammer.Native/Composition/Connectors/ConnectorPermissionStore` over `PermissionsFileEditor`; `Native.Views/Views/Settings/Connectors` | `McpConnectionCheckerTests` against the loopback HTTP fixture of `McpHttpTests`, `ConnectorMatrixModelTests` (three states to rules and back), a canary test that no header, argument or URL path reaches any text | Check connection never runs at start; Blocked writes a deny rule and Always allow an allow rule in the Autonogrammer permission file; the page says Auto does not trust `readOnlyHint` |
| 76-03 Permissions | `Autonogrammer.Cowork`: `CoworkPermissionMode.Auto`, resolver branches (Auto, `Mcp(...)` rules from `PermissionsLoader`, delete protection via `ShellDeleteDetector`, tainted-session demotion kept), `CoworkPathBoundary` additions, trusted-folder access mode and scope in `CoworkSessionMetaStore`, Auto mapped to `always-ask-cached` in the profile; `Harness.AgentLoop/Safety/ShellDeleteDetector`; Presentation `Settings/Permissions` (modes, folders, statements) and view | `CoworkAutoModeTests`, `ShellDeleteDetectorTests` (`rm`, `rmdir`, `unlink`, `shred`, `find -delete`, `git clean`, wrappers per `ShellCommandAnalysis`; a script that deletes is not recognised and the test says so), `CoworkResolverMcpRuleTests`, `CoworkPathBoundaryTests` (`/`, home, system dirs, symlink to a protected dir), existing Cowork and `RunMode*` tests unchanged and green | Default Manual; Skip warns and needs isolation or an acknowledgement; every decision recorded in `RunModeAuditLog` with its stage; `ByPolicy` provider calls ask in Auto |
| 76-04 Isolation | `Harness.Isolation`: `IsolationPolicy`, `IsolationLevel` {None, ProcessContained, FilesystemIsolated}, `BubblewrapArgvBuilder`, `BubblewrapIsolator`, `SeatbeltProfileBuilder`, `SeatbeltIsolator`, `JobObjectContainer` (`LibraryImport`), `IsolationProbe`, deny-list defaults, environment scrub, `IsolationContributor` (an `ILaunchContributor` returning the wrapper); `IProcessLaunchWrapper` in Harness.Abstractions and its use in `AcpStdioTransport`; Presentation chip model | `BubblewrapArgvBuilderTests` (golden argv: read-only root, one bind per read-write grant, tmpfs over denied directories and socket locations, `--die-with-parent`), `SeatbeltProfileBuilderTests` (golden text, path quoting), `IsolationProbeTests` with a fake process runner (`bwrap_missing`, `userns_disabled`, timeout), `AcpStdioTransportWrapperTests` (wrapper sees file name, arguments, directory, environment; none means unchanged), Live `AUTONOGRAMMER_LIVE_ISOLATION=1` (probe passes, a write outside a grant is refused) | Scrub removes `SSH_AUTH_SOCK`, `DBUS_SESSION_BUS_ADDRESS`, `DISPLAY`, `WAYLAND_DISPLAY`; no network namespace (the gateway keeps working); never silently unconfined; on by default for Cowork, off for Code |
| 76-05 Activity | Presentation `Subagents/*`, `Usage/*`, `Notifications/*`; `Native.Views/Sessions/AcpTaskToolActivitySource` and `Views/{Subagents,Usage,Notifications}`; `Harness.AgentLoop/Usage/FileUsageLedger`; `Harness.Notifications`: `INotificationSink`, `NotificationRequest`, `NotificationPolicy`, `NotificationCoalescer`, `OsNotificationSink`, `NotifySendCommand`, `OsascriptCommand`. Splits three ways by folder if staffed in parallel | `SubagentPanelModelTests` and `AcpTaskToolActivitySourceTests` over recorded-frame fixtures (names present and absent), `UsageMeterModelTests`, `FileUsageLedgerTests` (JSONL, 90 days, no content, mode 0600), `NotificationPolicyTests` (focus, 30 s coalesce, 10 per minute, Do-not-disturb, title only), command tests (argument vector only, control characters stripped, 200 character cap, parameterised script), headless view tests (live region, names, keys) | Agent text is plain text, never markup or an opening link; `MainView.CreateExport` receives the real history; no OS notification while the window is focused; sources include the `ApprovalRequested` tool event and the permission queue |
| 76-06 Integration | Composition of contributors in `SessionEngineSet`, `EngineComposition` and the Cowork host, `apps/Autonogrammer.Native/Composition/*`, `SettingsSection`, `SettingsWindowFactory`, `SettingsPageViews`, `ViewMapping`, web information pages with a desktop notice (`WebInfoPages.cs`), `docs/reference/cowork-extensions.md`, `docs/reference/cowork-safety.md`, `docs/runbooks/cowork-isolation.md`, `docs/parity/cowork-parity.md` rows, `docs/SUMMARY.md`, `docs/architecture.md`, `CHANGELOG.md` | Composition tests per wiring, `ProjectGraphTests`, `HeadMotionTests`, `ThemeContrastTests` and `ColourLiteralTests` unchanged, `RepoHygieneTests`, `SuppressionLedgerTests` | The runbook lists owner actions (Live isolation test on a host with user namespaces; macOS and Windows runs); parity rows cite named test classes |

## Success criteria

After the last unit (`export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"`):

1. `dotnet restore Autonogrammer.Linux.slnf --locked-mode`, then `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror`.
2. `dotnet test --project tests/<name> -c Release --no-build --filter-not-trait "Category=Live"` for `Autonogrammer.Harness.PluginBundles.Tests`, `Autonogrammer.Harness.Isolation.Tests`, `Autonogrammer.Harness.Notifications.Tests`, `Autonogrammer.Cowork.Tests`, `Autonogrammer.Harness.AgentLoop.Tests`, `Autonogrammer.Harness.Mcp.Tests`, `Autonogrammer.Harness.Acp.Tests`, `Autonogrammer.Harness.Workspace.Tests`, `Autonogrammer.Presentation.Tests`, `Autonogrammer.Native.Tests`, `Autonogrammer.ArchitectureTests`.
3. On a host where `bwrap --version` works and user namespaces are allowed: `AUTONOGRAMMER_LIVE_ISOLATION=1 dotnet test --project tests/Autonogrammer.Harness.Isolation.Tests -c Release --no-build --filter-trait "Category=Live"`. Without the variable the tests skip, they do not fail.
4. `bash scripts/ci/repo-checks.sh`, `bash scripts/ci/check-licences.sh`, `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore`; `./bootstrap.sh` is the full gate.
5. A row of `docs/parity/cowork-parity.md` is MET only with a named passing test class; rows that depend on omp consuming a flag stay PARTIAL until a live run is recorded.

## Threat model

- Hostile bundle: zip-slip, symlinks, zip bombs, name collisions and oversized files are refused at install; a `.mcp.json` stdio server is code the machine starts, so each server needs its own confirmation showing the command line; hooks never run; a bundle from a cloned repository is off until enabled (its skills are prompt text from outside the user's trust).
- Connector abuse: MCP annotations are untrusted hints and Auto ignores them; Check connection contacts or starts a server only on a user action with a timeout; secrets (headers, env, URL paths) never reach a log, a diagnostic or `ToString()`. Catalog servers bypass the tool host, so the resolver rules are their only gate.
- Policy bypass: a deletion built in a script is not recognised (documented); in Skip, `always-ask` makes the host the decider; a tainted session demotes Auto and Skip to Manual for actuating calls (ADR-0032).
- Isolation escape and limits: reachable Unix sockets are the main hole of a read-only root, so the wrapper puts tmpfs over `/run/user` and Docker socket locations and scrubs the environment; a seccomp filter is DEFER. No network fence (the agent can send any readable file anywhere); `~/.omp` is readable because omp needs it writable; Windows has process containment only.
- Notifications: a lock screen is a display surface, so the OS text is the title only by default; notification text is data, never a command (argument vector, parameterised macOS script); floods are coalesced and capped.
- Usage ledger and sub-agent panel: the ledger holds no prompt content; agent output shown in the panel is untrusted text.

## Degraded and error states (named, with a sentence in the UI)

`BundleInvalid(code)`, `BundleComponentNotLoaded(hooks)`, `BundleServerNotConfirmed`, `BundleProjectScopeOff`, `ConnectorSignInNeeded`, `ConnectorCheckFailed`, `ConnectorCheckTimedOut`, `ToolBlocked`, `SkipNeedsAcknowledgement`, `FolderRefused(protected)`, `FolderChangeAppliesNextSession`, `IsolationUnavailable(bwrap_missing | userns_disabled | probe_timeout | platform_unsupported)`, `IsolationProcessOnly` (Windows), `NetworkUnrestricted` (always shown), `ContinueWithoutIsolation` (asks), `SubagentNamesUnavailable`, `UsageUnavailable` (engine reports no cost), `NotificationOsUnavailable(notify_send_missing | osascript_failed | windows_not_supported)`, `NotificationSuppressedFocused`, `NotificationDnd`.

## Cannot be verified on this host

A real `bwrap` run (the binary is 0.9.0 but user namespaces were not tried and the CI runner cannot create them, OQ-168); everything on macOS (`sandbox-exec`, `osascript`) and Windows (job objects); OS notification display on any platform; omp consuming `skills.customDirectories`, `--skills` and `--approval-mode always-ask` with a model, and whether a `task` call names its agent in `rawInput` (no live turn, OD-07); MCP OAuth sign-in (omp-owned); screen-reader output.

## Open questions (default, owner)

| Id | Question | Default | Owner |
| --- | --- | --- | --- |
| OQ-450 | Default Cowork approval mode | Manual (our Auto has no classifier) | product owner |
| OQ-451 | Keep the machine awake during a scheduled run | No; documented, revisit | product owner |
| OQ-452 | Network fence for the isolated agent (egress proxy, `--unshare-net`) | Not in v0.16.0; level shows "network: unrestricted" | architect |
| OQ-453 | Map Anthropic plugin hooks onto the ADR-0016 hook model | Listed, never run | autonogrammer-maintainers |
| OQ-454 | Semantics of `--skills <globs>` and the overlay key for `skills.customDirectories` | Probe at launch; a disabled skill that cannot be excluded is shown as such | autonogrammer-maintainers |
| OQ-455 | Application identity for Windows toast notifications | No Windows OS notification; in-app only | platform |
| OQ-456 | Host-owned OAuth token vault behind the gateway | Deferred until the gateway is verified live | architect |
| OQ-457 | Windows filesystem isolation route | Process containment only; a dedicated-account route needs an installer decision | architect |
| OQ-458 | Accept edits mode of the Code tab | Deferred; Manual, Auto, Skip and Plan only | product owner |
| OQ-459 | Does omp's `task` tool call carry the agent name in `rawInput`? | First test of 76-05 on recorded frames; unnamed rows if not | autonogrammer-maintainers |

## Index rows (for the orchestrator to merge)

ADR index (`docs/decisions/README.md`): the same row as in brief 75, added once.

Open questions (`docs/open-questions.md`, columns ID, Question, Why it matters, Default taken, Owner, Status = `open (non-blocking default)`): OQ-450 to OQ-459 with the questions, defaults and owners of the table above, for example `| OQ-452 | Network fence for the isolated agent? | Isolation confines writes and hides secret directories but not exfiltration of a readable file | Not in v0.16.0; level shows "network: unrestricted" | architect | open (non-blocking default) |`. OQ-450 to OQ-459 are phase 76's (shared block OQ-440 to OQ-479 with phase 75), assigned by the design integrator.
