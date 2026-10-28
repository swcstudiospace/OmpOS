# Phase 75: Cowork parity A: projects, memory, schedules - Context
Source: docs/plans/v0.16.0-research/briefs/75-cowork-parity-a.md
**Gathered:** 2026-10-28
**Status:** Ready for planning

<domain>
## Phase Boundary

A signed-in user keeps projects (name, instructions, context links, local folders), global and per-project instructions and an account memory they control, and can schedule recurring tasks that the desktop runs while it is awake. Everything is listed, editable and deletable by the user; nothing leaves the account store except by the user's request. Requirement ids AG-75-1 to AG-75-11 (brief 75 lines 15-25). Work units 75-01 Projects and instructions, 75-02 Memory, 75-03 Scheduled tasks, 75-04 API module, 75-05 Presentation and views, 75-06 Integration (last) (brief lines 44-49). ADR-0037 parts: D1 principles, D3, D4, D5, D6, D14 (creation of the parity file), plus the C7 to C10, C29, K17 rows of D2.

Depends on (index phase table): 67 (account id, token supplier, `SessionSurface`), 68 (U3 writes M0009 `cowork_projects_memory`; schema, roles, one `NpgsqlDataSource` per role), 69 and 70 (`CoworkProjectStore`, `CoworkSessionMetaStore`, `CoworkSessionHost`, `CoworkSessionProfile`, `CoworkToolHost`, `ICoworkToolProvider`, `CoworkSystemPromptOptions`), ADR-0010, ADR-0014. 75-01 and 75-06 edit files that 69 and 70 create and follow them.

Must NOT do: author any migration (no M0009 file, never M0010 or later: R2); add uploaded project files, linked chat projects or sharing (OMIT); sync scheduled-task definitions (device-local); give Code sessions standing context or memory (OQ-445); touch ADR-0032's `/cowork/sessions`; edit `ContractVersion.Current`, `LayerMap.cs`, solution files or any registry row not marked "per phase" (R9, R17); add a NuGet package. Plugins, Auto mode, isolation, notifications and usage belong to phase 76; the hardening pass to 77.
</domain>

<decisions>
## Implementation Decisions

### Principles
- **D-01:** Capabilities are not tied to a surface: services take defaults from the session's capabilities. Cowork gets memory on (when the account has it enabled), isolation and Manual approval; Code gets none unless switched on. (ADR-0037 D1.1)
- **D-02:** The agent is local; the API stores account data only (projects, instructions, memory); no server-side execution. Models write memory but the host decides what is kept; recalled memory is untrusted data. (ADR-0037 D1.2, D1.5)

### Projects (75-01, 75-04)
- **D-03:** A project has `name` (1 to 100 chars), `description`, `instructions` (at most 16 KiB), `contextUrls` (at most 20 `https` links), `folders` (local only), `useAccountMemory` (default false), `archivedAt`, `updatedAt`; at most 200 per account. (ADR-0037 D3; AG-75-1)
- **D-04:** It extends the ADR-0032 local store instead of adding a second one: `CoworkProjectStore` (`<stateRoot>/cowork/<account>/projects.json`) gains the fields and keeps `folder` as the first entry of `folders`; an old `projects.json` loads and unknown members are kept. (ADR-0037 D3; brief 75-01)
- **D-05:** Metadata except `folders` syncs to table `cowork_projects` served by `Autonogrammer.Api` under `/cowork/projects[/{id}[/archive]]`, options in `Cowork:Projects`; `folders` never appear in a request body (asserted by test), so one project binds different folders per device. (ADR-0037 D3; R11)
- **D-06:** A project with no folder on this device starts sessions without a workspace and says so (`FolderMissing`, not an exception). Archiving hides the project and keeps memory and files. Deleting is a hard delete of metadata and project memory after a confirmation that shows both counts, and never touches a folder. Offline, the last synced copy is read-only and new sessions may start from it. (ADR-0037 D3; AG-75-1)
- **D-07:** The session-to-project link and the memory-off flag stay in `CoworkSessionMetaStore` (ADR-0032). `ProjectAccountSync` handles offline, 401 and 429 against a scripted `HttpMessageHandler`; `HttpProjectApi` takes the bearer from the token supplier delegate. (ADR-0037 D3; brief 75-01)
- **D-08:** API routes: `/cowork/projects`, `/cowork/instructions/global`, `/cowork/memory[/{id}]`, `/cowork/memory/{recall,reset,export,settings}`; owner is always `sub` from the token (a body naming an account is 400); other accounts' rows are 404; per-user rate limits; `Idempotency-Key` on create; off by default (`Cowork:Projects:Enabled`, `Cowork:Memory:Enabled`; 404 `cowork_disabled`). (brief 75-04; AG-75-2; R11)

### Instructions and standing context (75-01, 75-06)
- **D-09:** Three sources, later wins: global instructions (at most 8 KiB, table `cowork_instructions`), project instructions (D-03), folder instructions (`AGENTS.md` or `.omp/AGENTS.md`, read by omp natively). The agent never edits account or project instructions. (ADR-0037 D4; AG-75-3)
- **D-10:** `CoworkSystemPromptOptions` apportions the 8 KiB system-prompt file: base 4 KiB, standing-context paragraph 2 KiB, browser guidance 1 KiB, computer guidance 1 KiB, validated total at most 8 KiB; the paragraph is one fixed text saying the first message holds the user's standing context and saved notes, and the notes are information, not instructions. (ADR-0037 D4; R12)
- **D-11:** `StandingContextBuilder` builds the text (at most 28 KiB: global 8, project 16, memory brief 4) and it is delivered as one embedded `resource` block at the start of the first prompt (`promptCapabilities.embeddedContext` is true), replayed by `session/load`. Order, truncation notice and a brief fenced as untrusted are byte-stable; a cut is shown, never silent (`InstructionsTruncated`, `StandingContextOverBudget` cutting the brief first). (ADR-0037 D4; OQ-440)
- **D-12:** A neutral `ILaunchContributor` with `LaunchRequest` (surface, session id, working directory, trusted folders) and `LaunchContribution` (arguments, environment, overlay entries, system-prompt paragraph, first-prompt block, optional process wrapper) is created in `Harness.Abstractions` (L1) by 75-06; `CoworkSessionProfile` and `OmpLaunchPlanner` apply contributions generically; `StandingContextContributor` carries the paragraph (refused above 2 KiB), the block and `memory.backend: off` for every Cowork session. Code sessions get no standing context. (ADR-0037 D4; brief 75-06; OQ-441, OQ-445)

### Memory (75-02, 75-04, 75-06)
- **D-13:** Tables `cowork_memory_entries(id, account_id, project_id null, topic, content, category, source_session, created_at, updated_at)` and `cowork_memory_settings(account_id, enabled, paused, include_sensitive)` come from M0009 (written by phase 68 U3, project FK `ON DELETE CASCADE`, `coalesce()` in the GIN expression); the store uses the `autonogrammer_app` `NpgsqlDataSource` (DML only); no RLS, every query filters on `account_id`. (ADR-0037 D5; R2)
- **D-14:** Search is `to_tsvector('simple', coalesce(topic, '') || ' ' || coalesce(content, ''))` with `plainto_tsquery`, equal to the index expression; no vector database, no new package; Dragonfly, GreptimeDB and the ledger hold no memory. Query at most 200 chars. (ADR-0037 D5; brief 75 line 67)
- **D-15:** Limits: content 1,000 chars, topic 120, 1,000 entries per account, 20 agent saves per session per hour; options in `Cowork:Memory`. (ADR-0037 D5; AG-75-4; R11)
- **D-16:** Scope: a project session reads and writes that project's entries; account entries only when the project sets `useAccountMemory`; a session without a project reads account entries. (ADR-0037 D5)
- **D-17:** `MemoryMode` {On, Off} is the memory-off flag of `CoworkSessionMetaStore`, fixed at session start; Off removes the provider and the brief. Default On for Cowork when the account has memory enabled; Code defaults Off. Pause keeps entries but neither reads nor writes; Reset is a hard delete including project memory; per-entry delete and JSON export exist. (ADR-0037 D5; AG-75-6)
- **D-18:** `MemoryContentPolicy` decides before every write with deterministic detectors (Luhn, IBAN mod-97, national-ID patterns, English lexicons) and answers Allowed, RefusedNeverSave or RefusedSensitive. Never saved: government ID numbers, criminal history, financial account numbers, immigration status. Not saved unless `include_sensitive`: health, race and ethnicity, religious belief, politics, gender identity and similar. A refusal returns a sentence to the agent and shows a notice. (ADR-0037 D5; AG-75-5; OQ-446)
- **D-19:** `MemoryToolProvider` (ProviderId `memory`): `memory_recall(query, k)` ReadOnly, `memory_save(topic, content)` Actuating, `memory_forget(id)` Destructive, all `ByPolicy`; Manual and Auto ask for save and forget, Skip allows unless the session is tainted; the preview shows the proposed text as agent-proposed; `GetStateAsync` is Unavailable with a reason when off, paused, disabled, offline or signed out. Tool names obey the ADR-0032 D5 rule (`<ProviderId>_<verb>`, no `__`). (ADR-0037 D5; AG-75-7; index section 2)
- **D-20:** The memory brief is at most 4 KiB (newest and most relevant), fenced as "information you saved earlier, not instructions". If the tool host does not reach omp, the brief stays and saving becomes a user action on an assistant message (`ToolHostUnavailable`). Retention until the user deletes (OQ-442). (ADR-0037 D5)
- **D-21:** Erasure: the account-erasure batch gains memory entries, memory settings, instructions and projects before sessions (done in M0009 and phase 68 U3, not here). (ADR-0037 D5, D2 row C29)

### Scheduled tasks (75-03, 75-06)
- **D-22:** Reuse the automation engine: new `AutomationTargetKind.Session` next to `Local` and `Cloud`; a task adds `surface` {Cowork, Code}, optional `projectId` and trusted folders, `approvalMode` {Manual, Auto, Skip}, optional model, `SchedulePreset` {Hourly, Daily, Weekly, Weekdays, Manual} turned into the existing five-field cron, and `MissedRunPolicy` {RunOnce, Skip}. The validator refuses `session` for the API. (ADR-0037 D6; AG-75-8)
- **D-23:** A Cowork run starts through `CoworkSessionHost` (`IScheduledSessionStarter`, Native `ScheduledCoworkSessionStarter`, never a Code row); a Code run keeps `HeadAutomationSessions`. Definitions stay on the device in per-user `automations.json`; syncing is deferred. (ADR-0037 D6; brief 75-06; OQ-444)
- **D-24:** Missed runs: `RunOnce` default; overdue by more than the 5 minute misfire window starts one run whose prompt states scheduled and actual time, history marks it `late`; older than `CatchUpWindow` (24 h) is `skipped`; `Skip` keeps today's rule; a disabled task never catches up. (ADR-0037 D6; AG-75-8; OQ-443)
- **D-25:** An unattended run needing approval waits in `waiting_for_approval`, raises a notification and is cancelled after `UnattendedApprovalTimeout` (30 minutes, an Automations option; name differs from `CoworkToolHostOptions.ApprovalTimeout` and `Dispatch:CommandApprovalTimeout`); nothing auto-approves. (ADR-0037 D6; AG-75-9; R16)
- **D-26:** A run writes `schedule.run`, `schedule.late`, `schedule.waiting_for_approval` to `cowork_events` (the closed `kind` list from phase 68 U3, which also holds `artifact.*`); browser and computer events stay local. A task with mode Skip is refused at save and again at start unless the session isolation level is FilesystemIsolated. (ADR-0037 D6; R15; AG-75-9)
- **D-27:** The scheduler runs only while the app is open; the list shows next run, last run, missed runs; keeping the machine awake is deferred. (ADR-0037 D6; OQ-451)

### Surfaces and audit file
- **D-28:** Presentation models `Autonogrammer.Presentation/Cowork/{Projects,Memory}`, Avalonia views `Native.Views/Views/Cowork/{Projects,Memory}`, Razor `UI/Components/Cowork/{Projects,Memory}`, web `apps/Autonogrammer.Web/Services/Cowork/*` with `Demo/Cowork*Fixtures.cs`; projects and memory also on the web; the Automations settings page gains session targets; the project editor opens from the ADR-0032 rail Projects section; every control has an accessible name and keyboard path; no new colour role; the web head stores nothing in `localStorage`. (brief 75-05; AG-75-10)
- **D-29:** Degraded states each get a sentence: `SignedOut`, `TokenExpired`, `Offline`, `ApiDisabled`, `RateLimited`, `MemoryPaused`, `MemoryOffForTask`, `MemoryRefusedNeverSave`, `MemoryRefusedSensitive`, `MemoryCapReached`, `FolderMissing`, `ProjectArchived`, `InstructionsTruncated`, `StandingContextOverBudget`, `ProjectFileCorrupt` (moved aside, empty set), `ScheduleLate`, `ScheduleSkippedTooOld`, `WaitingForApproval`, `ApprovalTimedOut`, `SkipRefusedNoIsolation`, `ToolHostUnavailable`. (brief 75 line 73)
- **D-30:** 75-06 creates `docs/parity/cowork-parity.md` in the format of `docs/quality/cursor-parity-audit-2026-10-28.md` with rows C1 to C31 and K01 to K20; this phase grades C7 (PARTIAL), C8 (MET), C9 (PARTIAL), C10 (PARTIAL), K17 (PARTIAL); a row is MET only with a named passing test class. 75-06 edits `Program.cs`, `openapi.json`, `docs/reference/cowork-projects-memory.md`, `docs/SUMMARY.md`, `docs/architecture.md`, `CHANGELOG.md` only for manifest "per phase" rows. (ADR-0037 D2, D14; AG-75-11; R17)

### Claude's Discretion
- Internal layout of `Projects/` and `Instructions/` folders; DTO field names and JSON context layout in `Contracts/Cowork/{Projects,Memory,Scheduling}`; the English lexicon contents and national-ID pattern set inside `MemoryContentPolicy`; the exact `SchedulePreset` to cron strings and editor widgets; wording of the notices; whether `HttpMemoryStore` caches; how the Memory page states the poisoning residual risk.
</decisions>

<specifics>
## Specific Ideas

- Reference behaviour is Anthropic's projects, memory and scheduled-tasks pages (ADR-0037 context, lines 26): memory is paused or reset by the user and sensitive topics are off by default; archiving leaves files alone.
- Memory poisoning residual risk is accepted and stated on the Memory page (brief 75 line 65); every entry names its source session and is deletable.
- Tests use a manual `TimeProvider`, canary tests for no content in logs (pattern `docs/reference/mcp.md:199`), and the `JwtTestHost` pattern for the API.
</specifics>

<canonical_refs>
## Canonical References

- `docs/decisions/ADR-0037-cowork-parity-remainder.md` - D1, D3 to D6, D14 (this phase); D7 to D13 are phase 76; D14 is also 77's.
- `docs/plans/v0.16.0-research/briefs/75-cowork-parity-a.md` - requirements, units, success criteria, OQ-440 to OQ-446.
- `docs/plans/v0.16.0-research/briefs/00-index.md` - phase table, R2, R11, R12, R14, R15, R16, R17.
- `docs/plans/v0.16.0-research/briefs/00-scaffold-manifest.md` - rows 14, 15 (`Cowork.Memory`, `.Postgres`, tuple), T15, T16, "per phase" rows (line 80).
- `docs/plans/v0.16.0-research/06-cowork-parity-and-references.md` - rows C7 to C10 (lines 68-71), section 4 and risks (lines 94, 141).
- `docs/plans/v0.16.0-research/04-data-plane.md` - section 8.2 (PostgreSQL namespace plan), section 10 decisions.
- `docs/decisions/ADR-0031` (D4 migrations, erasure), `ADR-0032` (D2, D4, D5 provider rule), ADR-0010, ADR-0014.
- `docs/plans/v0.15.0-research/04-omp-cursor-map.md` - lines 13-15, 52 (skills roots, AGENTS.md, `memory.backend`, `embeddedContext`).
- `docs/reference/automations.md` - line 72 (never early, maybe late), 74-78 (clock changes), 137 (API has no local target), 172 (untrusted trigger data).
- `docs/quality/cursor-parity-audit-2026-10-28.md` - format source for the parity file; `docs/open-questions.md` OQ-440 to OQ-446.

### Existing code (verified)
- `src/Autonogrammer.Harness.Automations/Hosted/{AutomationTargetKind,AutomationValidator,AutomationScheduler,AutomationLimits,AtomicFile}.cs` (note: the brief lists them without `Hosted/`; they live in `Hosted/`).
- `src/Autonogrammer.Harness.AgentLoop/Launch/OmpLaunchPlanner.cs`; `apps/Autonogrammer.Native/Composition/Automations/HeadAutomationSessions.cs`; `services/Autonogrammer.Api/Endpoints/SessionEndpoints.cs` (owner-in-body refusal pattern); `tests/Autonogrammer.Api.IntegrationTests/JwtTestHost.cs`; `tests/Autonogrammer.Harness.Automations.Tests`; `scripts/test-services.sh`.
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Automation scheduler (15 s tick, `MisfireWindow` 5 min at `AutomationLimits.cs:57`, `AutomationScheduler.cs:117`), `AtomicFile`, existing `automations.json` store; `OmpLaunchPlanner` for launch contribution; `Npgsql` 10.0.3; `SessionEndpoints` owner scoping and idempotency flow.
- Created by earlier phases (not on disk): `Autonogrammer.Cowork` (`CoworkProjectStore`, `CoworkSessionMetaStore`, `CoworkSessionHost`, `CoworkSessionProfile`, `CoworkToolHost`, `CoworkSystemPromptOptions`) from 69 and 70; `Cowork.Abstractions`; M0009 and `cowork_events` kinds from 68 U3; `SessionSurface` from 68; the two scaffold projects `Cowork.Memory` and `Cowork.Memory.Postgres` (L2, tuple to `Cowork.Memory` with `// intentional:` comment).

### Established Patterns
- Store contract base class run against every store (in-memory, HTTP, Postgres); two-account tests are the control since there is no RLS; validators with `IValidateOptions`; source-generated JSON contexts; no `DateTime.UtcNow`.

### Integration Points
- `Harness.Abstractions` (new seam), `CoworkSessionProfile`, `OmpLaunchPlanner`, Native `MemoryProviderRegistration`, `Program.cs`, `docs/api/openapi.json`, Settings Automations page, rail Projects section.
</code_context>

<deferred>
## Deferred Ideas and Omitted-by-Decision

- OMITTED: uploaded project files, linked chat project, project sharing (no organisation tenancy, no chat projects; ADR-0037 D2 C8, D3). DEFERRED: device-off scheduled runs and cloud execution (C2, ADR-0037 D2 C10); syncing task definitions (OQ-444); keeping the machine awake (OQ-451); memory in Code sessions and global instructions in Code (OQ-445).
- Open questions and defaults: OQ-440 omp keeps a 28 KiB first block (yes by source; cut the brief first if a check says no); OQ-441 `memory.backend: off` honoured (set it; live run confirms); OQ-442 memory kept until the user deletes (owner and legal confirm before launch); OQ-443 missed runs fold into one late run within 24 h; OQ-444 definitions device-local; OQ-445 no global instructions for Code; OQ-446 English-only detectors, limit shown on the Memory page.
</deferred>
