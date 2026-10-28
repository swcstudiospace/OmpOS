# Phase 56 Verification: Slash Commands and Convention Discovery

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `56-SUMMARY.md`. Tests are hermetic: no omp, no network, no model. The omp command list was read from the installed omp 18.12.6 source and frozen as test data.

Recorded totals (orchestrator facts, recorded 2026-10-28, final tree before the last wiring): Presentation 2303, Native 2012 (2 skipped), Workspace 364, Remote 520 or more (2 skipped as root), Web 262 or more, UI 595, Architecture 46, all passing. The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`. The commands below were not re-run by the author of this record; "Inside the passing <Project> run" means the project passed as a whole, not that the class was run alone.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 | `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*SlashCommandCatalogueTests*" --filter-class "*SlashCommandReferenceTests*" --filter-class "*SlashCommandRegistryTests*" --filter-class "*SlashCommandResolverTests*" --filter-class "*SlashCommandDispatcherTests*" --filter-class "*SlashCommandArgumentsTests*" --filter-class "*SlashPromptComposerTests*"` | The catalogue holds 92 documented tokens plus `/update-settings`; the group and status counts match the documented tally; every token has a handler and every Unavailable row a reason; omp terminal-UI-only commands are not delegated; the documented table and the catalogue agree cell by cell; the resolver order catalogue, engine, command, skill, subagent; unknown names pass through | Inside the passing Presentation run (2303) |
| 1 | `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*ModelSlashHandlerTests*" --filter-class "*HostSeamSlashHandlerTests*" --filter-class "*SlashActionHandlerHostTests*"` and `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*DesktopSlashActionHostTests*" --filter-class "*DesktopSlashHandlerRoutingTests*" --filter-class "*ShellSlashHandlerTests*"` | The native actions the table names are run by a handler or the head, and a native command whose action the window cannot run is listed disabled | Inside the passing Presentation and Native runs |
| 2 (desktop) | `dotnet test tests/OmpOS.Harness.Workspace.Tests -c Release -- --filter-class "*ConventionCatalogTests*" --filter-class "*ConventionCatalogSourceTests*" --filter-class "*ConventionFrontmatterParserTests*" --filter-class "*LocalConventionFileSourceTests*" --filter-class "*BuiltinSkillDiscoveryTests*"` and `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*ConventionMappingTests*" --filter-class "*BuiltinSkillsDesktopTests*"` | Each of the six directories is read at both scopes; directory order, project over user and earlier root over later decide; malformed, oversize, non-UTF-8, duplicate and escaping files are diagnostics; built-in skills lose to a project or user skill | Inside the passing Workspace (364) and Native runs |
| 2 (remote) | `dotnet test tests/OmpOS.Harness.Remote.Tests -c Release -- --filter-class "*RemoteConventionFileSourceTests*" --filter-class "*RemoteSessionResourcesTests*" --filter-class "*ContainerSessionResourcesTests*"` | The remote file source lists and reads the six directories over the ssh file system (and over podman exec) with the same bounds; the session resources hand a `RemoteConventionSetup` | Inside the passing Remote run. `RemoteSlashDiscoverySource` (Native) has no test; UNVERIFIED: a headless test that a remote session's palette lists a command read from a fake remote file system |
| 2 (cloud) | none | Not built: no cloud-session discovery source exists | Not verified |
| 3 (desktop) | `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*SlashPaletteModelTests*" --filter-class "*SlashPaletteDiscoveryTests*" --filter-class "*SlashPaletteEngineCommandsTests*"` and `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*SlashPaletteBindingTests*" --filter-class "*SlashPaletteBindingDispatchTests*" --filter-class "*RuntimeSlashDispatchTests*" --filter-class "*RuntimeSlashPaletteTests*" --filter-class "*ComposerSlashPaletteViewTests*" --filter-class "*SlashPaletteEndToEndTests*"` | Rows carry badge and scope; a shared name is listed once per source; an unavailable row shows its reason; discovery refreshes at start and on open (throttled); sending a slash text runs the action, composes the prompt or passes through | Inside the passing Presentation and Native runs |
| 3 (web) | `dotnet test tests/OmpOS.UI.Tests -c Release -- --filter-class "*ComposerPanelTests*"` and `dotnet test tests/OmpOS.Web.Tests -c Release -- --filter-class "*WebSourcesTests*"` | The web composer lists the palette rows and the web action host runs reachable actions and refuses the rest | Inside the passing UI (595) and Web runs. The web palette has no discovered rows (`SlashDiscovery.Empty`) |
| Docs and architecture | `dotnet test tests/OmpOS.ArchitectureTests -c Release` | No forbidden project reference was added by the phase; no banned token in the shipped files | Inside the passing Architecture run (46) |
| Live | none | A palette command against a real omp session | UNVERIFIED (OD-07): `omp acp` session with a model key, send `/compact` and `/rename x` through the palette |

## Evidence files

| File | What it shows |
|------|---------------|
| `docs/reference/slash-commands.md` | The 93-row table with handler kind, handler, status and reason; the resolution order; the discovery rules. A test keeps it equal to the catalogue |
| `docs/reference/builtin-skills.md` | The ten built-in skills and the 11 Cursor skills that do not apply (its status banner is stale) |
| `assets/skills/*/SKILL.md` | The ten skill files |
| `tests/OmpOS.Presentation.Tests/Commands/OmpAcpBuiltinCommands.cs` | The 46 ACP commands of omp 18.12.6, frozen as test data |
| `docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md` | The live ssh run (session only; no discovery step) |

## Known limits of this verification

- No live omp turn (OD-07), no Windows, macOS or Android run.
- Remote discovery was tested over fake remote file systems and podman exec fixtures, not read from a live host.
- Cloud discovery and web discovery are not built, so no test exists for them.
- The 5 s refresh throttle uses `TimeProvider` in the tests; no real-time wait was measured.
