---
phase: "75"
slug: "cowork-parity-projects-memory-schedules"
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-10-28"
---

# Phase 75 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 on Microsoft Testing Platform (`dotnet test --project`); headless Avalonia (Native.Tests); bUnit (UI.Tests); `JwtTestHost` (Api.IntegrationTests) |
| **Config file** | `global.json` (MTP runner, SDK 10.0.402); each `tests/Autonogrammer.*.Tests/*.csproj` (projects created by phase 66.1) |
| **Quick run command** | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release --filter-not-trait "Category=Live"` (swap the project per unit) |
| **Full suite command** | the per-project list of the roadmap success criteria 3 and 4, each through `/tmp/ag-bin/ag-dotnet test --project tests/<name> -c Release --no-build --filter-not-trait "Category=Live"`; Postgres project after `source "$(bash scripts/test-services.sh up)"` |
| **Estimated runtime** | [INFERENCE] 60 to 180 s per project on the shared host (the wrapper serialises and waits for low load) |

---

## Sampling Rate

- **After every task commit:** the quick run command for the unit's own test project, filtered with `-- --filter-class "*<Class>*"` when only one class changed
- **After every plan wave:** the full suite command over the projects the wave touched
- **Before `/gsd:verify-work`:** every command of ROADMAP Phase 75 success criteria 1 to 5 green
- **Max feedback latency:** 300 s (one wrapper-serialised project test run)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 75-01-01 | 01 | 1 | AG-75-1 | T-75-01..03 | limits hold; old `projects.json` loads, unknown members kept; `FolderMissing` not an exception | unit | `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release -- --filter-class "*ProjectValidatorTests*"` and `"*ProjectStoreMigrationTests*"` | ❌ W0 (project from 66.1; class new) | ⬜ pending |
| 75-01-02 | 01 | 1 | AG-75-3 | T-75-01..03 | byte-stable standing context, brief fenced as untrusted, cut shown never silent | unit | `... --project tests/Autonogrammer.Cowork.Tests -c Release -- --filter-class "*StandingContextBuilderTests*"` | ❌ W0 | ⬜ pending |
| 75-01-03 | 01 | 1 | AG-75-1, AG-75-3 | T-75-01..03 | `folders` never in a request body; offline read-only; 401 and 429 handled; global instructions client | unit | `... --project tests/Autonogrammer.Cowork.Tests -c Release -- --filter-class "*ProjectAccountSyncTests*"` | ❌ W0 | ⬜ pending |
| 75-02-01 | 02 | 1 | AG-75-4, AG-75-5 | T-75-10..13 | never-saved and sensitive classes refused before every write with a reason | unit | `... --project tests/Autonogrammer.Cowork.Memory.Tests -c Release -- --filter-class "*MemoryContentPolicyTests*"` | ❌ W0 | ⬜ pending |
| 75-02-02 | 02 | 1 | AG-75-4, AG-75-6 | T-75-10..13 | two-account isolation, caps, GIN `EXPLAIN`, cascade, log canary across three stores | unit + Live (Postgres) | `... --project tests/Autonogrammer.Cowork.Memory.Tests -c Release --filter-not-trait "Category=Live"`; Postgres: `source "$(bash scripts/test-services.sh up)" && ... --project tests/Autonogrammer.Cowork.Memory.Postgres.Tests -c Release --no-build` | ❌ W0 | ⬜ pending |
| 75-02-03 | 02 | 1 | AG-75-7, AG-75-6 | T-75-10..13 | `memory_save` and `memory_forget` ask in Manual and Auto; unavailable states give a reason | unit | `... --project tests/Autonogrammer.Cowork.Memory.Tests -c Release -- --filter-class "*MemoryToolProviderTests*"` | ❌ W0 | ⬜ pending |
| 75-03-01 | 03 | 1 | AG-75-8 | T-75-20..23 | API validator refuses `session` target; Skip refused without isolation | unit | `... --project tests/Autonogrammer.Harness.Automations.Tests -c Release -- --filter-class "*SchedulePresetTests*"` and `"*SessionTargetTests*"` | ❌ W0 (classes new; project exists) | ⬜ pending |
| 75-03-02 | 03 | 1 | AG-75-8, AG-75-9 | T-75-20..23 | one late run within 24 h; disabled never catches up; unattended wait cancelled at 30 min; nothing auto-approves | unit | `... --project tests/Autonogrammer.Harness.Automations.Tests -c Release -- --filter-class "*MissedRunTests*"` then `--filter-not-trait "Category=Live"` | ❌ W0 | ⬜ pending |
| 75-03-03 | 03 | 1 | AG-75-8, AG-75-9 | T-75-20..23 | run history and `schedule.*` events; starter seam only | unit | `... --project tests/Autonogrammer.Harness.Automations.Tests -c Release --filter-not-trait "Category=Live"` | ❌ W0 | ⬜ pending |
| 75-04-01 | 04 | 2 | AG-75-2 | T-75-30..33 | 401 without token; 404 across accounts; 400 when a body names an account; `Idempotency-Key` on create | integration (`JwtTestHost`) | `... --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*ProjectsApiTests*"` | ❌ W0 | ⬜ pending |
| 75-04-02 | 04 | 2 | AG-75-2, AG-75-4 | T-75-30..33 | memory routes scoped to `sub`; size limits; query at most 200 chars | integration | `... --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*MemoryApiTests*"` | ❌ W0 | ⬜ pending |
| 75-04-03 | 04 | 2 | AG-75-2 | T-75-30..33 | off by default, 404 `cowork_disabled`; per-user 429; `/cowork/sessions` untouched | integration | `... --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*CoworkApiBoundaryTests*"` | ❌ W0 | ⬜ pending |
| 75-05-01 | 05 | 2 | AG-75-10 | T-75-40..43 | every D-29 state has a sentence; delete confirmation carries both counts | unit | `... --project tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*CoworkPresentationTests*"` | ❌ W0 | ⬜ pending |
| 75-05-02 | 05 | 2 | AG-75-10 | T-75-40..43 | accessible names, focus order, no colour literals, reduced motion, live regions | headless Avalonia + bUnit | `... --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*CoworkHeadTests*"` and `tests/Autonogrammer.UI.Tests ... "*CoworkComponentTests*"` | ❌ W0 | ⬜ pending |
| 75-05-03 | 05 | 2 | AG-75-10 | T-75-40..43 | web head stores nothing in `localStorage`; no reference to `Autonogrammer.Cowork` | unit | `... --project tests/Autonogrammer.Web.Tests -c Release -- --filter-class "*CoworkWebTests*"` | ❌ W0 | ⬜ pending |
| 75-06-01 | 06 | 3 | AG-75-3, AG-75-6 | T-75-50..53 | byte-exact argv and overlay; contribution above 2 KiB refused | unit | `... --project tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*LaunchContributionTests*"` | ❌ W0 | ⬜ pending |
| 75-06-02 | 06 | 3 | AG-75-1, AG-75-3, AG-75-6, AG-75-7, AG-75-8 | T-75-50..53 | `memory.backend: off` for Cowork, none for Code; Cowork run starts via `CoworkSessionHost`, never a Code row | unit | `... --project tests/Autonogrammer.Cowork.Tests -c Release -- --filter-class "*StandingContextContributorTests*"` and `tests/Autonogrammer.Native.Tests ... "*ScheduledCoworkSessionStarterTests*"` | ❌ W0 | ⬜ pending |
| 75-06-03 | 06 | 3 | AG-75-11 | T-75-50..53 | parity rows C7 PARTIAL, C8 MET, C9 PARTIAL, C10 PARTIAL, K17 PARTIAL each with a named passing class; OpenAPI equal | architecture + integration | `... --project tests/Autonogrammer.ArchitectureTests -c Release --filter-not-trait "Category=Live"` and `tests/Autonogrammer.Api.IntegrationTests ... "*GuardTests*"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky. `...` is `/tmp/ag-bin/ag-dotnet test`. Plans: 75-01..75-06 in `.planning/phases/75-cowork-parity-projects-memory-schedules/`.*

---

## Wave 0 Requirements

- [ ] `tests/Autonogrammer.Cowork.Tests`, `tests/Autonogrammer.Cowork.Memory.Tests`, `tests/Autonogrammer.Cowork.Memory.Postgres.Tests` exist (phase 66.1 scaffold, manifest T15, T16)
- [ ] M0009 `cowork_projects_memory` exists and is applied by the ADR-0031 runner (phase 68 U3); this phase authors no migration
- [ ] `tests/Autonogrammer.Api.IntegrationTests/Cowork/` folder (created by 75-04)
- [ ] shared memory store contract base class (created by 75-02)
- [ ] old-format `projects.json` fixture (created by 75-01)
- [ ] `docs/parity/cowork-parity.md` (created by 75-06)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| omp keeps a 28 KiB first-prompt block whole; honours `memory.backend: off`; a model calls the memory tools | AG-75-3, AG-75-6, AG-75-7 | needs a live omp turn (OD-07) | not observable on this host; recorded as unobserved in the parity file |
| A task wakes from real sleep | AG-75-8 | needs a sleeping machine | not observable; scheduler is tested with a manual `TimeProvider` |
| Live Supabase token against `/cowork/*` | AG-75-2 | no sign-in authority on this host | `JwtTestHost` signs test tokens; live call recorded as unobserved |
| Screen-reader speech | AG-75-10 | no screen reader on this host | headless tests check accessible names and roles only |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 300 s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
