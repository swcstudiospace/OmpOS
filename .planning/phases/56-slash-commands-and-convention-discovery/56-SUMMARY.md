# Phase 56 Summary: Slash Commands and Convention Discovery

Status (recorded 2026-10-28, re-checked against the tree): criterion 1 met; criteria 2 and 3 stay Partial: `apps/Autonogrammer.Web/Services/WebSessionControls.cs` still passes `SlashDiscovery.Empty` to the web palette (the web `WorkspaceSlashDiscoverySource` feeds only the Settings Commands page), and no cloud discovery source exists (the implementers of `ISlashDiscoverySource` are `ConventionSlashDiscoverySource`, `ConventionsCommandsSource`, `RemoteSlashDiscoverySource` and the web `WorkspaceSlashDiscoverySource`). The catalogue holds all 92 Cursor 3.27 tokens and aliases plus `/update-settings` (93 rows), each with a handler kind and a status, and the open-ended forms resolve. Discovery over the six convention directories at two scopes works in desktop sessions and is wired for sessions on a remote host (ssh, podman, WSL) through the remote file source; it is not wired for cloud sessions. The desktop palette lists engine, Autonogrammer, discovered and skill rows with their source; the web palette lists the catalogue and the engine commands but no discovered rows. Test results are the recorded per-project runs of 2026-10-28, not per-class results (Presentation 2303, Native 2012 with 2 skipped, Workspace 364, Remote 520 or more with 2 skipped as root, Web 262 or more, UI 595, all on the final tree before the last wiring). The full `./bootstrap.sh` gate on a clean worktree of commit e24e6851 exited 0 (recorded 2026-10-28: 12215 tests, 12213 succeeded, 0 failed, 2 skipped; format clean; repository checks and licence check OK); release head 9d4e0075 is tagged v0.15.0, later commits are documentation only. GitHub `ci` runs 40758215402 (main) and 40758275399 (tag) failed at 'Start test services' (rootless podman cannot create a user namespace in the runner container, OQ-168); the local gate is the gate of record. No live model turn ran (OD-07): no palette command was run against a real omp session.

## Criteria

| Criterion | Status | Evidence |
|-----------|--------|----------|
| 1. 92 tokens, handler kinds, open-ended forms | Met | `SlashCommandCatalogueTests` (all tokens present, no duplicate names, 65 CLI / 9 Chat / 19 built-in skill rows, the status tally 36 Built / 24 Partial / 9 Delegated / 24 Unavailable, every token has a handler and every Unavailable row has a reason, each handler kind is backed by what it names, every alias names an existing token, the documented table and the catalogue agree cell by cell, the open-ended forms are documented, every token is listed in the palette with its status), `SlashCommandResolverTests`, `SlashCommandDispatcherTests`, `SlashCommandArgumentsTests`, `SlashPromptComposerTests`; inside the passing Presentation run (2303, recorded 2026-10-28). The table of `docs/reference/slash-commands.md` has 93 rows: handler kinds 47 NativeAction, 11 OmpDelegated, 10 Skill, 1 CloudHandoff, 24 Unavailable (counted from the file) |
| 2. Discovery in desktop, remote and cloud sessions | Partial | Desktop: `ConventionCatalogTests` (each of the six directories at both scopes, directory order, project over user, earlier root over later, diagnostics for malformed, oversize, non-UTF-8, escaping and duplicate files), `ConventionCatalogSourceTests`, `ConventionFrontmatterParserTests`, `LocalConventionFileSourceTests`, `ConventionMappingTests`; inside the passing Workspace (364) and Native runs. Remote: `RemoteConventionFileSource` and `RemoteConventionSources` are covered by `RemoteConventionFileSourceTests`, `RemoteSessionResourcesTests` and `ContainerSessionResourcesTests` (inside the passing Remote run); `SessionEngineSet.SlashDiscoveryFor` picks `RemoteSlashDiscoverySource` for a remote session. Missing: no cloud-session discovery source exists in the tree; no test constructs `RemoteSlashDiscoverySource`; the live loopback `sshd` run of 2026-10-28 did not exercise discovery |
| 3. Palette lists engine, Autonogrammer, discovered and skill rows with source in both heads | Partial | Desktop: `SlashPaletteModelTests`, `SlashPaletteDiscoveryTests`, `SlashPaletteEngineCommandsTests`, `ComposerSlashPaletteViewTests`, `SlashPaletteBindingTests`, `SlashPaletteEndToEndTests`, `RuntimeSlashPaletteTests` (headless). Web: `WebSessionControls` builds the palette from the catalogue and the engine commands and passes `SlashDiscovery.Empty`, so no discovered command, skill or subagent is listed; `SlashPalette.razor` and `ComposerPanelTests` (UI) cover the list; `WebSourcesTests` covers the web action host. No bUnit test lists a discovered row in the web head |

## What the phase delivered

- The 92-token catalogue with five handler kinds and four statuses, published as `docs/reference/slash-commands.md` and kept equal to the code by a test. Nine tokens are delegated to omp after reading omp 18.12.6 itself; five commands that omp runs only in its terminal UI are not delegated.
- The resolver, dispatcher, argument expansion and prompt composition; the `&` cloud prefix; native action handlers for model, mode, settings, new chat and terminal, checkpoints, fork and side chat, worktrees, copy, about, quit, feedback, open file, changes, workspace save and load, and display toggles.
- Convention discovery over `.autonogrammer`, `.omp`, `.cursor`, `.claude`, `.codex`, `.agents` at project and user scope with precedence and diagnostics, a remote file source for ssh, podman and WSL, and desktop wiring in the palette and the Rules, Skills and Commands Settings pages.
- Ten built-in skills with their own wording (`assets/skills/`), turned on in the desktop discovery (`ConventionSlashDiscoverySource` and `ConventionSettingsLoader` call `WithBuiltin`); nine slash rows moved to Built.

## Deviations

- The catalogue has 93 rows, not 92: `/update-settings` is an Autonogrammer addition that replaces the two Cursor-only configuration skills in practice. The 92 Cursor tokens are all present.
- The status tally at the end of the phase is 36 Built, 24 Partial, 9 Delegated, 24 Unavailable (counted from the table and asserted by `TheStatusCountsAreTheDocumentedTally`). The first report said 8 Built, 9 Delegated, 25 Partial, 50 Unavailable; the built-in skills (`e7c451e`) and the Wave 2 handlers moved rows to Built and Partial.
- `docs/reference/builtin-skills.md` still carries a status banner that says the desktop does not turn the built-in source on; the tree does (`ConventionSlashDiscoverySource`, `ConventionSettingsLoader`). The banner is stale.
- `SlashPaletteSource.Omp` was renamed `Engine`; the badge label "omp" became "Engine".
- Cloud and remote conventions are not read by omp running there unless omp itself reads them: omp does not read `.cursor/agents`, so a subagent definition is sent in the prompt (documented in `docs/reference/slash-commands.md`).

## Partial

- Criterion 2: cloud sessions have no discovery. Remote discovery is wired but not exercised by a test of the Native source or by the live ssh run.
- Criterion 3: the web palette has no discovered rows.
- Multi-root: the desktop discovery reads the session's primary root only (`SessionEngineSet.SlashDiscoveryFor`, `ConventionSettingsLoader`); the library merges several roots (`AnEarlierWorkspaceRootWinsASharedName`) but no head passes more than one.
- 24 tokens are Partial and 24 are Unavailable with a stated reason; this is the honest tally, not a gap in the catalogue.

## Blocked (OQ/OD)

- OD-07: no palette command ran against a real omp session; delegation is verified against omp 18.12.6 source and the frozen ACP list only.
- OQ-103 (are Cursor's own file locations read by default): the default taken is that all six directories are read.
- Proposed OQ: should user-home convention directories follow symlinks that leave the home folder (dotfile managers)? Default today: refused with a diagnostic.
- Proposed OQ: built-in skills in remote and API-hosted sessions live on the machine that runs the head, not on the remote omp host. Options: inline the SKILL.md text into the prompt, or sync to the workspace. Default: desktop only.

## Known limits

- No file watcher: discovery re-runs at session start and at most every 5 s when the palette opens; Settings pages read again on open.
- Remote discovery connects to the host on the first palette open if the session has not started (`RemoteSlashDiscoverySource`).
- A native slash action handled with images attached drops the images (WireSkillsImages report).
- The 5 s throttle, the 256 KiB file cap and the frontmatter bounds are defaults, not owner decisions.
