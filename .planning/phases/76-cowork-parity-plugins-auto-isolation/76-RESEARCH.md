# Phase 76: Cowork parity B: plugins, Auto mode, isolation - Research
Source: docs/plans/v0.16.0-research/briefs/76-cowork-parity-b.md

Summarises research reports 06 (Cowork parity and references), 09 (recipe and conventions), plus ADR-0037 and index R11, R14, R16, R17.

## Standard Stack / What to reuse
- Seam for the wrapper: `src/Autonogrammer.Harness.Acp/AcpStdioTransport.cs:219` (`FileName = _ompPath`) and `:232` (`startInfo.ArgumentList.Add(argument)`).
- Permission rules: `src/Autonogrammer.Harness.AgentLoop/Safety/PermissionsLoader.cs:4`, `PermissionsFileEditor.cs:6`, `RunModeAuditLog.cs:4`, `RunModePermissionResolver.cs`, `RunModeLaunchMapper.cs` (Code-only, untouched).
- Delete detection base: `Safety/ShellCommandAnalysis.cs:11` and `Safety/KnownSafeCommands.cs:8` are both `internal static`; `ShellDeleteDetector` is new and public in the same folder.
- MCP: `src/Autonogrammer.Harness.Mcp/Catalog/IMcpConnectionChecker.cs:9` (interface exists; no implementation registered, `docs/reference/mcp.md:18`), `McpClientConnector.cs:6` (public static); loopback fixture `tests/Autonogrammer.Harness.Mcp.Tests/McpHttpTests.cs:7`.
- Skills and conventions: `src/Autonogrammer.Harness.Workspace/Conventions/ConventionScope.cs:4` (add `Plugin`), `BuiltinSkills/BuiltinSkillManifest.cs:7`; shipped skills dir `assets/skills/` has `agent-review`, `create-hook`, `create-rule` (add `create-plugin`).
- Settings: `src/Autonogrammer.Presentation/Settings/SettingsSection.cs:22` (`Plugins = 5`); `src/Autonogrammer.Native.Views/ViewMapping.cs`.
- Usage: `src/Autonogrammer.Harness.Acp/AcpWireMapping.cs:29` maps `usage_update` (frame record `AcpProtocol/AcpFrames.cs:428`); export seam `src/Autonogrammer.Native.Views/MainView.axaml.cs:1231,1520` (`CreateExport`; audit row AC-175 says it passes empty history).
- Live-test pattern: class-level `[Trait("Category", "Live")]` plus env gate with `Assert.Skip` (report 09:69).

## Facts the planner needs
- Not on disk yet: `src/Autonogrammer.Cowork` (69, 70), `ILaunchContributor`/`LaunchContribution` (75-06; grep: no match today), `IProcessLaunchWrapper` and `ShellDeleteDetector` (grep: no match), `Harness.PluginBundles`, `.Isolation`, `.Notifications` (scaffold rows 16 to 18), `docs/parity/cowork-parity.md` (75-06).
- Process ban: `Directory.Build.props:52` exempts `Autonogrammer.Harness.*`, `Autonogrammer.Sandbox` and `*Tests`; the three new projects are `Harness.*`; `ProcessSpawningAssemblies` gets them in the scaffold step.
- `PluginBundles` must not reference `Harness.Workspace` or `Harness.Mcp` (R14, manifest line 32); composition in Native feeds the bundle sources.
- omp has no OS sandbox: no Landlock, Seatbelt or seccomp; `bash.patterns` is an approval policy, not containment (`docs/reference/run-modes.md:108`). Auto-review has no classifier (`:31`). `always-ask` lets omp approve file reads itself (`:115`).
- `bwrap` 0.9.0 is installed (ADR-0037 line 25, from `bwrap --version`); user namespaces were not tried; CI cannot create them for podman (OQ-168, `docs/open-questions.md:205`).
- Anthropic isolation reference is sandbox-runtime (Apache-2.0; design only, no code taken). Anthropic's own advice: computer use is not sandboxed; shipping it default-on would regress safety (report 06:144).
- Skill roots: `.autonogrammer`, `.omp`, `.cursor`, `.claude`, `.codex`, `.agents` (audit row AC-071); omp `skills.customDirectories`, `--skills`, `--no-skills` per `docs/plans/v0.15.0-research/04-omp-cursor-map.md:13`; RPC `get_subagents` only on RPC, not shared with ACP (`:86`, `:96`).
- MCP servers reach omp only via `session/new.mcpServers` (`docs/reference/mcp.md:23`); catalog servers bypass the Cowork tool host.
- Untrusted workspace flag covers permission files, not skills (`run-modes.md:80`); hence project-scope bundles are off until enabled.
- Contract: `CoworkPermissionMode.Auto` is additive; ContractVersion is bumped once by the scaffold step (R9).

## Architecture Patterns
- Launch contributors (`ILaunchContributor`) return arguments, environment, overlay entries, prompt paragraph and an optional process wrapper; composers apply them generically (ADR-0037 D4, D11).
- Argument-vector process start only, with golden argv and golden profile text tests (brief 76-04).
- Probe, report a level, name the degraded state, and ask before continuing unconfined (ADR-0037 D11).
- Rule engine shared with Code (`PermissionsLoader` files) read by the Cowork resolver; decisions go to `RunModeAuditLog` (ADR-0037 D10).
- Source interfaces (`ISubagentActivitySource`, `INotificationSink`) with one v0.16.0 implementation each and fakes for models (ADR-0037 D12, D13).

## Pitfalls
- A symlink or `..` entry inside a zip or folder bundle; a zip bomb; files over 2 MiB; name collisions with built-in skills or commands.
- `.mcp.json` stdio servers are processes the machine starts: confirm each one individually; never auto-enable from a cloned repo.
- Auto must ignore `readOnlyHint`; tainted sessions demote Auto and Skip to Manual for actuating calls.
- `ShellCommandAnalysis` and `KnownSafeCommands` are `internal`: Cowork (L2, tuple to `Harness.AgentLoop`) cannot call them directly; expose public types (e.g. `ShellDeleteDetector`) or an `InternalsVisibleTo` [INFERENCE; planner decides].
- Read-only root does not stop connecting to existing Unix sockets: tmpfs over `/run/user` and Docker socket paths, scrub `SSH_AUTH_SOCK`, `DBUS_SESSION_BUS_ADDRESS`, `DISPLAY`, `WAYLAND_DISPLAY` (ADR-0037 D11).
- `~/.omp` stays readable (credentials omp keeps there); no network fence: say so on screen.
- Passing notification text through a shell or splicing it into AppleScript is an injection: argument vector and `on run argv` only.
- A lock screen is a display surface: title only by default.
- Do not name another timeout `ApprovalTimeout` or call a policy a "mode" (R16).
- Skip gating depends on `--approval-mode always-ask` behaviour of omp, read from docs and never run (ADR-0037 Open risk 9); parity rows that rely on omp flags stay PARTIAL.
- Editing registry rows outside manifest "per phase" rows violates R17.

## Verification commands (brief 76 lines 53-59)
- `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"`
- `dotnet restore Autonogrammer.Linux.slnf --locked-mode`; `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror`
- `dotnet test --project tests/<name> -c Release --no-build --filter-not-trait "Category=Live"` for `Autonogrammer.Harness.PluginBundles.Tests`, `.Harness.Isolation.Tests`, `.Harness.Notifications.Tests`, `.Cowork.Tests`, `.Harness.AgentLoop.Tests`, `.Harness.Mcp.Tests`, `.Harness.Acp.Tests`, `.Harness.Workspace.Tests`, `.Presentation.Tests`, `.Native.Tests`, `.ArchitectureTests`
- Live, only where `bwrap --version` works and user namespaces are allowed: `AUTONOGRAMMER_LIVE_ISOLATION=1 dotnet test --project tests/Autonogrammer.Harness.Isolation.Tests -c Release --no-build --filter-trait "Category=Live"`
- `bash scripts/ci/repo-checks.sh`; `bash scripts/ci/check-licences.sh`; `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore`; `./bootstrap.sh` is the full gate.

## Cannot be verified on this host (index row 76; brief 76 line 76)
A real `bwrap` run (user namespaces untried; CI cannot create them, OQ-168); macOS `sandbox-exec` and `osascript`; Windows job objects; OS notification display on any platform; omp consuming `skills.customDirectories`, `--skills` and `--approval-mode always-ask`, and whether a `task` call names its agent in `rawInput` (no live turn, OD-07); MCP OAuth sign-in (omp-owned); screen-reader output.

## Validation Architecture
- Framework: xUnit v3 on Microsoft Testing Platform [INFERENCE from the `dotnet test --project` form, report 09:97]; headless Avalonia for views; Live tests gated by `AUTONOGRAMMER_LIVE_ISOLATION`.
- Quick run: `nice -n 15 dotnet test --project tests/Autonogrammer.Harness.PluginBundles.Tests -c Release --filter-not-trait "Category=Live"` (report 09:97). Full run: the list above.
- Requirement to test map (brief 76 lines 44-49):
  - AG-76-1, 2, 3, 4: `BundleValidatorTests`, `BundleInstallerTests`, `BundleLaunchContributorTests`, `BundleMcpSourceTests`, skill manifest test, precedence tests.
  - AG-76-5: `McpConnectionCheckerTests`, `ConnectorMatrixModelTests`, canary test (no header, argument or URL path in text).
  - AG-76-6, 7, 8, 9: `CoworkAutoModeTests`, `ShellDeleteDetectorTests`, `CoworkResolverMcpRuleTests`, `CoworkPathBoundaryTests` (`/`, home, system dirs), Skip-needs-isolation-or-acknowledgement test, tool-provider calls ask in Auto.
  - AG-76-10: `BubblewrapArgvBuilderTests`, `SeatbeltProfileBuilderTests`, `IsolationProbeTests` (fake runner: `bwrap_missing`, `userns_disabled`, timeout), `AcpStdioTransportWrapperTests` (in `Harness.Acp.Tests`), Live isolation test.
  - AG-76-11, 12, 13: `SubagentPanelModelTests`, `AcpTaskToolActivitySourceTests` (recorded frames, names present and absent), `UsageMeterModelTests`, `FileUsageLedgerTests`, `NotificationPolicyTests`, command tests, headless view tests.
  - AG-76-14 and integration: composition tests per wiring, `ProjectGraphTests`, `HeadMotionTests`, `ThemeContrastTests`, `ColourLiteralTests`, `RepoHygieneTests`, `SuppressionLedgerTests`.
- Wave 0 gaps (none exist yet): the three new test projects (scaffold step); `Cowork.Tests` (69, 70); recorded-frame fixtures of `task` tool calls with and without agent names; zip-slip, zip-bomb and symlink bundle fixtures; golden `bwrap` argv and Seatbelt profile files; fake process runner; `assets/skills/create-plugin/SKILL.md`; `AcpStdioTransportWrapperTests` in `Harness.Acp.Tests`.
