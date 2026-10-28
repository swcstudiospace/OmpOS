---
description: "Phase 75 brief (v0.16.0): Cowork parity part A, projects, global and project instructions, memory and scheduled tasks, with work units, success criteria and threat model."
---

# Phase 75: Cowork parity A (projects, instructions, memory, scheduled tasks)

Status: design brief, nothing built. Written 2026-10-28 for ADR-0037 ([decision record](../../../decisions/ADR-0037-cowork-parity-remainder.md)). Names are post-rename (Autonogrammer becomes Autonogrammer); paths use that spelling although the repo on disk still uses the old one. Facts cite the research reports of this folder (`06` Cowork parity, `04` data plane, `03` API, `02` UI, `09` recipe), the repo and the sibling records ADR-0030 to ADR-0036.

## Goal

A signed-in user keeps projects (name, instructions, context links, local folders), global and per-project instructions, and an account memory they control, and can schedule recurring tasks that the desktop runs while it is awake. Everything is listed, editable and deletable by the user, and nothing leaves the account store except by the user's request.

## Requirements

- AG-75-1: A user can create, list, rename, archive and hard-delete projects (at most 200, name at most 100 characters) and bind local folders to them without uploading a file.
- AG-75-2: The API stores projects, global instructions and memory per account (`sub`), answers 404 for another account's row, and is rate limited per user.
- AG-75-3: Global instructions (at most 8 KiB) and project instructions (at most 16 KiB) are built with the memory brief (at most 4 KiB) into one standing-context block delivered as an embedded resource at the start of the first prompt, with a fixed paragraph of at most 2 KiB in the system-prompt file, inside the 8 KiB total apportioned in `CoworkSystemPromptOptions` (base 4 KiB, standing context 2 KiB, browser 1 KiB, computer 1 KiB).
- AG-75-4: Memory entries are scoped to an account or a project, full-text searchable, capped (1,000 entries, 1,000 characters, 20 agent saves per session per hour) and hard-deleted on request.
- AG-75-5: `MemoryContentPolicy` refuses never-saved classes always and sensitive categories unless the user opted in, before every write, and says why to the agent and the user.
- AG-75-6: `MemoryMode` is the session's fixed memory-off flag; Off removes the provider and the brief; pause, reset, per-entry delete and JSON export exist.
- AG-75-7: The agent reaches memory through the `memory` tool provider (`memory_recall`, `memory_save`, `memory_forget`, all `ByPolicy`), so Manual and Auto ask for save and forget.
- AG-75-8: A scheduled task can target a Cowork or Code session with a preset schedule, an approval mode and optional project and folders; a missed run folds into one late run within 24 hours.
- AG-75-9: An unattended run that needs approval waits, notifies and is cancelled after `UnattendedApprovalTimeout` (30 minutes), and the run writes `schedule.run`, `schedule.late` and `schedule.waiting_for_approval` events (the closed `cowork_events.kind` list of phase 68 U3); Skip is refused without a FilesystemIsolated session.
- AG-75-10: Projects, instructions, memory and scheduled tasks have view models and desktop views (projects and memory also on the web) with accessible names, keyboard paths and specific degraded states.
- AG-75-11: `docs/parity/cowork-parity.md` exists with rows C1 to C31 and K01 to K20 and this phase's rows graded with evidence.

## Depends on

- Brief 67 (ADR-0030): account id from `IUserContext`, per-user rate-limit partitions, the token supplier, `SessionSurface`.
- Brief 68 (ADR-0031): schema `autonogrammer`, roles `autonogrammer_app` and `autonogrammer_migrator`, one `NpgsqlDataSource` per role, and unit U3, which writes migration M0009 `cowork_projects_memory` (the four tables of this phase, project FK `ON DELETE CASCADE`, `coalesce()` in the GIN expression) with its down step and adds them to the erasure batch. This phase authors no migration; the Postgres tests of 75-02 wait for U3 and apply M0009 through ADR-0031's runner. This phase must not add M0010 or later: M0010 `dispatch_device_state` is phase 74 U2's.
- Briefs 69 and 70 (ADR-0032): `CoworkProjectStore`, `CoworkSessionMetaStore` (project link, trusted folders, memory-off flag), `CoworkSessionHost`, `CoworkSessionProfile`, `CoworkToolHost` and `ICoworkToolProvider`. Units 75-01 and 75-06 edit or extend files those briefs create and follow them.
- ADR-0010 and ADR-0014: the tool host reaches omp through the gateway, feasible by source only; memory degrades to the brief if it does not.

## Projects and files owned

New projects, both L2 (created and registered by the scaffold step: `LayerMap` line, solution and filter entries, `ArchitectureTests` reference, lock file, test projects; this phase adds code only): `src/Autonogrammer.Cowork.Memory` (references the L1 `Cowork.Abstractions` and Contracts) and `src/Autonogrammer.Cowork.Memory.Postgres` (same-layer tuple to `Cowork.Memory` with an `// intentional:` comment), each with `tests/<name>.Tests`. No new NuGet package (Npgsql 10.0.3 exists). New folders `Projects/` and `Instructions/` in the existing `src/Autonogrammer.Cowork` with tests in `tests/Autonogrammer.Cowork.Tests`. New contract folders `src/Autonogrammer.Contracts/Cowork/{Projects,Memory,Scheduling}` (L0, additive; the scaffold step bumps `ContractVersion` to 1.6.0 and this phase does not edit `ContractVersion.Current`) with source-generated JSON contexts.

Existing files touched: `Cowork/CoworkProjectStore.cs` (75-01); `Harness.Automations/{AutomationTargetKind,AutomationTargetSpec,AutomationValidator,AutomationRunStatus,Hosted/AutomationScheduler,Hosted/AutomationRunner}.cs` (75-03); `Harness.Abstractions` (new `ILaunchContributor`, `LaunchRequest`, `LaunchContribution`), `Cowork/Chat/CoworkSessionProfile.cs`, `Harness.AgentLoop/Launch/OmpLaunchPlanner.cs`, `Program.cs`, `docs/api/openapi.json`, `apps/Autonogrammer.Native` composition (75-06). Registration is performed by the scaffold step; this phase edits a registry file only for rows `00-scaffold-manifest.md` marks "per phase" (OpenAPI, notices when a package lands, `docs/architecture.md` prose, parity audit rows), by 75-06 (ADR-0037 D14).

## Work units (file-disjoint; 75-06 last)

| Unit | Owner project and files | Tests | Acceptance |
| --- | --- | --- | --- |
| 75-01 Projects and instructions | `Autonogrammer.Cowork/Projects`: `CoworkProject` fields (description, contextUrls, `folders`, useAccountMemory, archivedAt, updatedAt) in `CoworkProjectStore`, `ProjectValidator`, `IProjectApi`, `HttpProjectApi` (bearer from the token supplier delegate), `ProjectAccountSync` (offline: last copy, read-only); `Autonogrammer.Cowork/Instructions`: `StandingContextBuilder`, `InstructionLimits`; contracts `Cowork/Projects` | `ProjectValidatorTests`, `ProjectStoreMigrationTests` (an old `projects.json` loads, unknown members kept), `ProjectAccountSyncTests` (scripted `HttpMessageHandler`: sync, offline, 401, 429), `StandingContextBuilderTests` (order, truncation notice, brief fenced as untrusted, byte-stable) | Limits hold (name 100, 200 projects, 16 KiB, 8 KiB, 20 `https` URLs); `folders` never appear in a request body (assert); a missing folder gives `FolderMissing`, not an exception |
| 75-02 Memory | `Cowork.Memory`: `MemoryEntry`, `IMemoryStore`, `InMemoryMemoryStore`, `HttpMemoryStore` (desktop client), `MemoryContentPolicy` (Luhn, IBAN mod-97, national-ID patterns, English lexicons), `MemoryBriefBuilder`, `MemoryToolProvider` (ProviderId `memory`; tool names and effects per ADR-0032 D5; `GetStateAsync` Unavailable with a reason when off, paused, disabled, offline or signed out; preview shows the proposed text as agent-proposed); `Cowork.Memory.Postgres`: `PostgresMemoryStore` | `MemoryContentPolicyTests` (theory per class, near-miss numbers, limits named), shared store contract base run against all three stores, `MemoryToolProviderTests` (descriptors validate against the ADR-0032 name rule, state, preview), Postgres tests with two accounts (`AUTONOGRAMMER_TEST_POSTGRES_CONNECTION`, post-rename of `AUTONOGRAMMER_TEST_POSTGRES_CONNECTION`, `scripts/test-services.sh:9-16`) | Caps and scopes hold; account B never reads account A; the search expression equals the index expression of M0009 (a test reads `EXPLAIN` for a GIN scan); project delete cascades; no content in any log line (canary, as `mcp.md:199`) |
| 75-03 Scheduled tasks | `Harness.Automations`: `AutomationTargetKind.Session`, `SchedulePreset`, `MissedRunPolicy` {RunOnce, Skip}, `CatchUpWindow`, `UnattendedApprovalTimeout` (30 minutes; Automations options), `IScheduledSessionStarter`, statuses `late` and `waiting_for_approval`; the validator refuses `session` for the API (`automations.md:137`) | `SchedulePresetTests` (preset to cron, time zones, DST per `automations.md:74-78`), `MissedRunTests` with a manual `TimeProvider` (one late run, 24 h cut-off, disabled never catches up, the 5 minute rule unchanged for `Skip`), `UnattendedApprovalTests` (30 minute cancel, Skip refused without isolation) | Existing `Harness.Automations.Tests` stay green unchanged; no `DateTime.UtcNow`; cancellation reaches the starter |
| 75-04 API module | `services/Autonogrammer.Api/Cowork/{Projects,Memory}`: options with `IValidateOptions`, endpoints `/cowork/projects[/{id}[/archive]]`, `/cowork/instructions/global`, `/cowork/memory[/{id}]`, `/cowork/memory/{recall,reset,export,settings}`, disabled middleware, JSON contexts, `AddCowork*` and `Map*` extensions | `tests/Autonogrammer.Api.IntegrationTests/Cowork/*` with the `JwtTestHost` pattern: 401 without token, 404 across accounts, 400 when a body names an account, 429 per user, size limits, `Idempotency-Key` on create | No route accepts an owner in the body (as `SessionEndpoints.cs:103-106`); off by default (`Cowork:Projects:Enabled`, `Cowork:Memory:Enabled`); `/cowork/sessions` is ADR-0032's, not touched |
| 75-05 Presentation and views | `Autonogrammer.Presentation/Cowork/{Projects,Memory}` (Avalonia-free); `Native.Views/Views/Cowork/{Projects,Memory}`; extend the existing Automations settings page for session targets; `Autonogrammer.UI/Components/Cowork/{Projects,Memory}`; `apps/Autonogrammer.Web/Services/Cowork/*` and new `Demo/Cowork*Fixtures.cs` | `Presentation.Tests` (loading, empty, offline, error, ready), `Native.Tests` headless (names, focus order, no colour literals, reduced motion), `UI.Tests` bUnit (live regions) | Every control has an accessible name and keyboard path; a delete confirmation shows the counts; the project editor opens from the ADR-0032 rail Projects section; no new colour role |
| 75-06 Integration | `Harness.Abstractions`: `ILaunchContributor`, `LaunchRequest` (surface, session id, working directory, trusted folders), `LaunchContribution` (arguments, environment, overlay entries, system-prompt paragraph, first-prompt block, optional process wrapper); `CoworkSessionProfile` and `OmpLaunchPlanner` apply contributions generically; `StandingContextContributor` (paragraph at most 2 KiB, first-prompt resource block, `memory.backend: off`); `apps/Autonogrammer.Native/Cowork/{ScheduledCoworkSessionStarter,MemoryProviderRegistration}`; `Program.cs`; `openapi.json`; `docs/reference/cowork-projects-memory.md`; `docs/parity/cowork-parity.md`; `docs/SUMMARY.md`, `docs/architecture.md`, `CHANGELOG.md` | `LaunchContributionTests` (byte-exact argv and overlay, budget refusal above 2 KiB), `StandingContextContributorTests`, `ScheduledCoworkSessionStarterTests` (calls `CoworkSessionHost`, never a Code row), arch tests, OpenAPI freshness | No registry row outside the manifest's "per phase" rows is edited; `GuardTests` OpenAPI equality passes; every parity row has a status from the vocabulary |

## Success criteria

Run after the last unit, in order (`export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"`):

1. `dotnet restore Autonogrammer.Linux.slnf --locked-mode`, then `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror`.
2. `TEST_SERVICES_ENV="$(bash scripts/test-services.sh up)"; source "$TEST_SERVICES_ENV"`, then `dotnet test --project tests/Autonogrammer.Cowork.Memory.Postgres.Tests -c Release --no-build`.
3. `dotnet test --project tests/<name> -c Release --no-build --filter-not-trait "Category=Live"` for `Autonogrammer.Cowork.Tests`, `Autonogrammer.Cowork.Memory.Tests`, `Autonogrammer.Harness.Automations.Tests`, `Autonogrammer.Harness.AgentLoop.Tests`, `Autonogrammer.Presentation.Tests`, `Autonogrammer.Native.Tests`, `Autonogrammer.UI.Tests`, `Autonogrammer.Web.Tests`, `Autonogrammer.Api.IntegrationTests`.
4. `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release --no-build` (layers, same-layer tuple, process-ban naming, suppression ledger, unlinked-marker rule).
5. `bash scripts/ci/repo-checks.sh`, `bash scripts/ci/check-licences.sh`, `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore`.
6. `./bootstrap.sh` is the full gate. A parity row is MET only if a named test class above covers it.

## Threat model

- Spoofing and tampering: ids are guessable, so every query filters on the account id from the validated token and the two-account tests are the control (no row-level security, ADR-0031).
- Memory poisoning: a page the agent read can make it save text that steers later sessions. Controls: `memory_save` asks in Manual and Auto, asks in a tainted Skip session, shows the proposed text, the brief is fenced as untrusted notes, 20 saves per session per hour, every entry names its source session and is deletable. Residual risk accepted and stated on the Memory page.
- Information disclosure: memory and instruction text never appear in logs, exceptions or `ToString()` (canary tests); the standing-context block lives in the session history, the project file is mode 0600 in a 0700 folder; the web head stores nothing in `localStorage`; never-saved and sensitive classes are filtered before the network call.
- Denial of service: counts and sizes capped, search query at most 200 characters, per-user rate limits, bounded brief, the existing `maxConcurrentRuns` for tasks.
- Elevation: an unattended task never auto-approves and Skip needs isolation; trigger data from outside stays fenced as untrusted (`automations.md:172`).
- Repudiation: the run history records the trigger, the scheduled and actual start, each approval wait and its outcome.

## Degraded and error states (named, each with a sentence in the UI)

`SignedOut` (Cowork asks to sign in; Code unaffected), `TokenExpired` (cached read-only copy; sessions continue), `Offline` (projects read-only, memory unavailable), `ApiDisabled` (404 `cowork_disabled`), `RateLimited` (Retry-After shown), `MemoryPaused`, `MemoryOffForTask`, `MemoryRefusedNeverSave`, `MemoryRefusedSensitive`, `MemoryCapReached`, `FolderMissing`, `ProjectArchived`, `InstructionsTruncated`, `StandingContextOverBudget`, `ProjectFileCorrupt` (moved aside, empty set), `ScheduleLate`, `ScheduleSkippedTooOld`, `WaitingForApproval`, `ApprovalTimedOut`, `SkipRefusedNoIsolation`, `ToolHostUnavailable` (memory brief only).

## Cannot be verified on this host

Live Supabase tokens and a live Railway Postgres (owner credentials; fakes and the throwaway TimescaleDB 2.13.1-pg16 container are used); Windows and macOS beyond compilation; omp keeping the 28 KiB first-prompt block whole, honouring `memory.backend: off`, and a model calling the memory tools (no live turn, OD-07); the tool host reaching omp (gateway feasible by source only); a task waking from real sleep; screen-reader speech (headless tests check names and roles).

## Open questions (default, owner)

| Id | Question | Default | Owner |
| --- | --- | --- | --- |
| OQ-440 | Does omp keep a standing-context block of up to 28 KiB whole as the first embedded resource? | Yes by source; the profile shows `StandingContextOverBudget` and cuts the brief first if a check says no | autonogrammer-maintainers |
| OQ-441 | Does `memory.backend: off` in the profile overlay switch omp's memory off? | Set it; the first live run confirms | autonogrammer-maintainers |
| OQ-442 | How long is memory kept? | Until the user deletes it; Reset is a hard delete; owner and legal confirm before launch (report 04 D6) | product owner |
| OQ-443 | Missed scheduled runs: skip or run once late? | Run once within 24 hours, marked late | product owner |
| OQ-444 | Do task definitions sync to the account, and can a phone run one? | Device-local; a Dispatch run-now command is ADR-0036's | architect (ADR-0036 owner) |
| OQ-445 | Do global instructions also apply to Code sessions? | No in v0.16.0; AGENTS.md covers Code | product owner |
| OQ-446 | Memory detectors cover English only; add locales? | English only, limitation shown on the Memory page | product owner |

## Index rows (for the orchestrator to merge)

ADR index (`docs/decisions/README.md`):

`| [ADR-0037](ADR-0037-cowork-parity-remainder.md) | Cowork parity remainder: projects, instructions, memory, scheduled tasks, plugins, skills, connectors, permissions, local isolation, sub-agents, notifications | Proposed 2026-10-28 | Design only; extends the ADR-0032 project store, resolver and tool host; Windows and macOS compile-checked only; hooks in plugin bundles refused; no network fence |`

Open questions (`docs/open-questions.md`, columns ID, Question, Why it matters, Default taken, Owner, Status = `open (non-blocking default)`; question, default and owner as above):

`| OQ-442 | How long is Cowork memory kept? | Memory holds user content and ADR-0031 needs a retention rule | Kept until the user deletes it; Reset is a hard delete | product owner | open (non-blocking default) |` and likewise OQ-440, OQ-441, OQ-443 to OQ-446. OQ-440 to OQ-446 are phase 75's (shared block OQ-440 to OQ-479 with phase 76), assigned by the design integrator.
