---
phase: 75-cowork-parity-projects-memory-schedules
verified: 2026-11-21T22:35:00Z
status: gaps_found
score: 3/8 verified; 2 partial; 3 require DB/live execution
covered_files:
  - .planning/phases/75-cowork-parity-projects-memory-schedules/75-01-SUMMARY.md
  - .planning/phases/75-cowork-parity-projects-memory-schedules/75-02-SUMMARY.md
  - .planning/phases/75-cowork-parity-projects-memory-schedules/75-03-SUMMARY.md
  - .planning/phases/75-cowork-parity-projects-memory-schedules/75-04-SUMMARY.md
  - .planning/phases/75-cowork-parity-projects-memory-schedules/75-05-SUMMARY.md
  - .planning/phases/75-cowork-parity-projects-memory-schedules/75-06-SUMMARY.md
  - .planning/phases/75-cowork-parity-projects-memory-schedules/75-07-SUMMARY.md
  - .planning/phases/75-cowork-parity-projects-memory-schedules/75-08-SUMMARY.md
  - .planning/phases/75-cowork-parity-projects-memory-schedules/75-09-SUMMARY.md
  - .planning/phases/75-cowork-parity-projects-memory-schedules/75-10-SUMMARY.md
  - .planning/phases/75-cowork-parity-projects-memory-schedules/75-11-SUMMARY.md
  - .planning/phases/75-cowork-parity-projects-memory-schedules/75-12-SUMMARY.md
  - .planning/phases/75-cowork-parity-projects-memory-schedules/75-13-SUMMARY.md
  - src/Autonogrammer.Cowork/Projects/ProjectAccountSync.cs
  - src/Autonogrammer.Harness.AgentLoop/Launch/OmpLaunchPlan.cs
  - src/Autonogrammer.Cowork/Chat/CoworkSessionProfile.cs
  - src/Autonogrammer.Cowork.Memory/MemoryToolProvider.cs
  - services/Autonogrammer.Api/Cowork/Projects/PostgresProjectAccountStore.cs
  - src/Autonogrammer.Cowork.Memory.Postgres/PostgresMemoryStore.cs
  - src/Autonogrammer.Presentation/Cowork/Projects/ProjectListModel.cs
---

# 75-VERIFICATION

Re-checked 2026-11-21 against `origin/main` @ `c04095ba` (SDK 10.0.402). The existing 2026-11-10
`75-VERIFICATION.md` content is preserved in git history; this revision supersedes it for the current
tree and records the 75-07..13 SUMMARYs written this lane.

## Success criteria

1. Locked restore + warning-free build — VERIFIED. `dotnet restore --locked-mode` ok;
   `dotnet build -c Release --no-restore -warnaserror` 0/0.
2. Postgres memory store vs throwaway services — NOT EXECUTED. Requires
   `source "$(bash scripts/test-services.sh up)"` + `Cowork.Memory.Postgres.Tests`. Gap, never a pass.
3. Cowork/memory/automations/agent-loop/presentation/native/ui/web/api suites — PARTIAL.
     `Presentation.Tests` 2646/2646; `Native.Tests` 2353/2353 (--no-build batch; see Human Verification);
     `UI.Tests` 882/882; `Web.Tests` 444/444.
   - `Harness.AgentLoop.Tests`: 4 failures seen on a disk-full run
     (`HookBridgeIntegrationTests.SessionStartContextFromTheBridgeReachesTheNextPrompt`,
     `HooksServiceTests.TheBridgeStartsWithTheSessionStopsWithItAndRestartsWithANewToken`,
     `RunModePolicyTests.AShellCommandRunsOnlyWhenEverySimpleCommandMatchesAnAllowRule`,
     `AcpHookTapTests.ClosingTheSessionRunsSessionEndAfterTheQueuedObservations`).
     Hooks/RunModePolicy are phase-57/73 surfaces, not 75 code; recorded as pre-existing/unrelated,
     NOT as a 75 regression and NOT as green.
   - `Api.IntegrationTests` (Cowork persistence + project/memory suites) NOT RUN (disk-full blocked the
     build; `CoworkPersistenceTests.cs` is present on the tree).
4. Architecture (layers, same-layer tuple, process-ban naming, suppression ledger, unlinked-marker) —
   VERIFIED. `ArchitectureTests` 107/107.
5. Repo/licence/format + full gate — PARTIAL. `check-licences.sh` PASSED; `dotnet format
   --verify-no-changes` clean; `scripts/ci/repo-checks.sh` exited 1 (combined run timed out at 900 s
   under load; the docs half reports "224 markdown files, 121 listed in SUMMARY.md, 0 errors,
   0 warnings"). `./bootstrap.sh` NOT RUN (owner action per 76-VALIDATION gate notes).
6. Parity MET only with named passing test — PARTIAL. `docs/parity/cowork-parity.md` exists (10.4 KB).
   Rows for 75-07/75-09/75-10/75-11/75-13 lack the named tests their plans require (see those
   SUMMARYs); those rows must stay out of MET until their tests land.
7. omp honours `memory.backend: off`, task waking from real sleep — LIVE GAP. OD-07 not approved; never
   claimed passed.
8. Scheduled Cowork tick (75-11) — NOT SHIPPED. `ScheduledCoworkSessionStarter` + tests absent.

**Score:** 3/8 fully verified; 2 partial; 3 require DB/live execution.

### Human verification required

- SC2: throwaway-services run for `Cowork.Memory.Postgres.Tests` (Dragonfly/Postgres env as CI).
- SC3: clean-host re-run of `Harness.AgentLoop.Tests` (4 failures above) and full `Api.IntegrationTests`
  non-live; confirm Native 2353/2353 fresh.
- SC5: `bash scripts/ci/repo-checks.sh` on an idle, non-full disk; `./bootstrap.sh` owner run.
- SC7: live-model/host verification (omp `memory.backend: off` consumption; wake-from-sleep).
