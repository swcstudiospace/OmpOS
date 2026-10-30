# Phase 75: Cowork parity A: projects, memory, schedules - Research
Source: docs/plans/v0.16.0-research/briefs/75-cowork-parity-a.md

Summarises research reports 06 (Cowork parity), 04 (data plane), 03 (API), 09 (recipe), plus ADR-0037 and index R2, R11, R12, R15, R16, R17.

## Standard Stack / What to reuse
- Scheduler: `src/Autonogrammer.Harness.Automations/Hosted/AutomationLimits.cs:57` (`MisfireWindow` 5 min), `Hosted/AutomationScheduler.cs:117` (`now - due > MisfireWindow` skip rule), `Hosted/AutomationTargetKind.cs` (`Local`, `Cloud`), `Hosted/AutomationValidator.cs`, `Hosted/AtomicFile.cs:7` (`internal`). The brief names these without `Hosted/`; on disk they live there.
- Desktop run path: `apps/Autonogrammer.Native/Composition/Automations/HeadAutomationSessions.cs` stays for Code runs (ADR-0037 D6).
- Launch: `src/Autonogrammer.Harness.AgentLoop/Launch/OmpLaunchPlanner.cs:12` (`public static class`); `ILaunchContributor` does not exist yet (grep: no match) and is created by 75-06.
- API patterns: `services/Autonogrammer.Api/Endpoints/SessionEndpoints.cs:144` (idempotency), `Cloud/CloudDisabledMiddleware.cs:33` (disabled 404), `tests/Autonogrammer.Api.IntegrationTests/JwtTestHost.cs:17`.
- Postgres tests: `tests/Autonogrammer.Sessions.Postgres.Tests/PodmanPostgresContainer.cs`, `scripts/test-services.sh:86` (exports `AUTONOGRAMMER_TEST_POSTGRES_CONNECTION`).
- `Npgsql` 10.0.3 (existing central package, `Directory.Packages.props:45` per ADR-0037 Licence); no new package.

## Facts the planner needs
- Not on disk yet (created by earlier phases): `src/Autonogrammer.Cowork` and its project store, meta store, session host, profile, tool host (69, 70); `Cowork.Memory` and `Cowork.Memory.Postgres` projects (scaffold, manifest rows 14, 15); M0009 and `cowork_events` (68 U3); `docs/parity/cowork-parity.md` (75-06; the dir `docs/parity/` holds `session-chat-parity.md`).
- This phase authors no migration; M0009 `cowork_projects_memory` is phase 68 U3's, M0010 is phase 74's (R2; brief 75 line 30). 75-02 Postgres tests wait for 68 U3 and apply M0009 through the ADR-0031 runner.
- `omp` facts (read from docs, never run, OD-07): `promptCapabilities.embeddedContext` true (`docs/plans/v0.15.0-research/04-omp-cursor-map.md:52`); `memory.backend` off by default (`:15`); `AGENTS.md` read natively (`:14`). A 28 KiB first-prompt block is unverified (OQ-440).
- Scheduler semantics: never early, at most 5 min late still runs once, several missed fold into one (`docs/reference/automations.md:72`); DST rules `:74-78`; untrusted trigger data fencing `:172`; API refuses `local` (`:137`).
- System prompt budget is fixed (R12): base 4, standing context 2, browser 1, computer 1, total 8 KiB (`CoworkSystemPromptOptions`, brief 70).
- Option sections: `Cowork:Projects`, `Cowork:Memory` (R11); platform sections stay `Dispatch`, `Accounts`, `DataPlane`.
- `cowork_events.kind` is closed (R15); `schedule.*` kinds are in the list; browser and computer events never go to the ledger.
- Same-layer tuple `(Cowork.Memory.Postgres, Cowork.Memory)` is written by the scaffold step (manifest line 65); the stores take an `NpgsqlDataSource` from the host (R14).
- Anthropic reference facts (ADR-0037 line 26): memory leaves out sensitive topics by default and never saves government ID, criminal history, financial account numbers or immigration status.
- Cowork parity rows owned here: C7 PARTIAL, C8 MET, C9 PARTIAL, C10 PARTIAL, K17 PARTIAL (ADR-0037 D2 lines 50-53, 73).

## Architecture Patterns
- Extend, do not fork: `CoworkProjectStore` gains fields; account sync is a client (`HttpProjectApi`, `ProjectAccountSync`) with offline read-only fallback (ADR-0037 D3).
- Neutral launch seam: contributors return `LaunchContribution`; composers apply it generically; phase 76 supplies more contributors (ADR-0037 D4).
- One `IMemoryStore` contract with three implementations (in-memory, HTTP, Postgres) under a shared store contract base class (brief 75-02).
- Host policy over model output: `MemoryContentPolicy` runs before every write; `ByPolicy` tool effects route through the Cowork resolver (ADR-0037 D5).
- Standing context is an embedded resource in the first prompt, replayed with history, not an unverified CLI flag (ADR-0037 D4).

## Pitfalls
- Adding any migration file or an M0010+ breaks R2 and phase 74's ownership.
- Sending `folders` in a request body leaks local paths (assert in test).
- Letting a body name an account: refuse with 400 (`SessionEndpoints.cs:103-106` pattern per brief).
- Search expression drifting from the M0009 GIN index expression disables the index; test with `EXPLAIN` (brief 75-02).
- Memory poisoning: save must ask in Manual and Auto and in tainted Skip; brief fenced as untrusted; 20 saves per session per hour.
- Two memory systems: set `memory.backend: off` for every Cowork session (key unverified, OQ-441).
- Using `DateTime.UtcNow` in the scheduler; use the injected `TimeProvider`; DST cases per `automations.md:74-78`.
- Skip approval mode must be refused without FilesystemIsolated (D6) though isolation ships in phase 76: [INFERENCE] 75-03 must read the isolation level through an abstraction that reports "not isolated" until 76 lands, so Skip tasks are refused.
- "Raises a notification" (D6) depends on the notification sink built in 76-05: [INFERENCE] 75 emits the status and event only; the sink wiring follows in 76.
- Do not name the unattended timeout `ApprovalTimeout` (R16); never call it a "mode".
- Do not edit registry rows outside manifest "per phase" rows (R17).

## Verification commands (brief 75 lines 53-60)
- `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"`
- `dotnet restore Autonogrammer.Linux.slnf --locked-mode`; `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror`
- `TEST_SERVICES_ENV="$(bash scripts/test-services.sh up)"; source "$TEST_SERVICES_ENV"`; `dotnet test --project tests/Autonogrammer.Cowork.Memory.Postgres.Tests -c Release --no-build`
- `dotnet test --project tests/<name> -c Release --no-build --filter-not-trait "Category=Live"` for `Autonogrammer.Cowork.Tests`, `.Cowork.Memory.Tests`, `.Harness.Automations.Tests`, `.Harness.AgentLoop.Tests`, `.Presentation.Tests`, `.Native.Tests`, `.UI.Tests`, `.Web.Tests`, `.Api.IntegrationTests`
- `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release --no-build`; `bash scripts/ci/repo-checks.sh`; `bash scripts/ci/check-licences.sh`; `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore`; `./bootstrap.sh` is the full gate.

## Cannot be verified on this host (index row 75; brief 75 line 77)
Live Supabase tokens and live Railway Postgres (fakes and the throwaway TimescaleDB 2.13.1-pg16 container are used); Windows and macOS beyond compilation; omp keeping a 28 KiB first-prompt block, honouring `memory.backend: off`, and a model calling the memory tools (OD-07); the tool host reaching omp (gateway by source only); a task waking from real sleep; screen-reader speech.

## Validation Architecture
- Framework: xUnit v3 on Microsoft Testing Platform [INFERENCE from the `dotnet test --project` form, report 09:97]; headless Avalonia for Native, bUnit for UI.
- Quick run: `nice -n 15 dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release --filter-not-trait "Category=Live"` (report 09:97). Full run: the list above.
- Requirement to test map (brief 75 lines 44-49):
  - AG-75-1, 3: `ProjectValidatorTests`, `ProjectStoreMigrationTests`, `ProjectAccountSyncTests`, `StandingContextBuilderTests` (Cowork.Tests); `LaunchContributionTests`, `StandingContextContributorTests` (budget refusal above 2 KiB).
  - AG-75-2: `Api.IntegrationTests/Cowork/*` (401, cross-account 404, 400 when body names an account, 429 per user, size limits, `Idempotency-Key`).
  - AG-75-4, 5, 6, 7: `MemoryContentPolicyTests`, shared store contract base (three stores), `MemoryToolProviderTests` (name rule, state, preview), `Cowork.Memory.Postgres.Tests` (two accounts, GIN `EXPLAIN`, cascade, log canary).
  - AG-75-8, 9: `SchedulePresetTests`, `MissedRunTests` (manual `TimeProvider`: one late run, 24 h cut-off, disabled never catches up), `UnattendedApprovalTests` (30 min cancel, Skip refused), `ScheduledCoworkSessionStarterTests`; existing `Harness.Automations.Tests` stay green unchanged.
  - AG-75-10: `Presentation.Tests` (loading, empty, offline, error, ready), `Native.Tests` headless (names, focus order, no colour literals, reduced motion), `UI.Tests` (live regions).
  - AG-75-11: parity row status vocabulary check; ArchitectureTests; OpenAPI `GuardTests` equality.
- Wave 0 gaps (none exist yet): `tests/Autonogrammer.Cowork.Tests`, `Cowork.Memory.Tests`, `Cowork.Memory.Postgres.Tests` (scaffold step, manifest T15, T16); `tests/Autonogrammer.Api.IntegrationTests/Cowork/`; shared memory store contract base class; fixtures for old `projects.json`; M0009 availability (68 U3); `docs/parity/cowork-parity.md`; recorded-frame fixture is phase 76's, not needed here.
