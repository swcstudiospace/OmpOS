---
phase: 75-cowork-parity-projects-memory-schedules
verified: 2026-11-22T01:53:17Z
status: gaps_found
score: "suites pass; DB depth + SEC-77-02 remain"
covered_files:
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-01-PLAN.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-01-SUMMARY.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-02-PLAN.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-02-SUMMARY.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-03-PLAN.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-03-SUMMARY.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-04-PLAN.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-04-SUMMARY.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-05-PLAN.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-05-SUMMARY.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-06-PLAN.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-06-SUMMARY.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-07-PLAN.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-07-SUMMARY.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-08-PLAN.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-08-SUMMARY.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-09-PLAN.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-09-SUMMARY.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-10-PLAN.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-10-SUMMARY.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-11-PLAN.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-11-SUMMARY.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-12-PLAN.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-12-SUMMARY.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-13-PLAN.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-13-SUMMARY.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-CONTEXT.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-RESEARCH.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-SOTA-COWORK-PARITY.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-VALIDATION.md"
  - ".planning/phases/75-cowork-parity-projects-memory-schedules/75-VERIFICATION.md"
covered_digest: "v1:sha256:fc4135c993fd429b80fc71587fee314cda6d148ac9e097176f385f91e44885bf"
behavior_unverified: 2
overrides_applied: 0
re_verification: true
previous_status: gaps_found
previous_score: "gaps_found"
commits: ["b46fd81f"]
gaps:
  - truth: "Memory.Postgres depth thin (1/1); phase-gate depth not proven."
    status: unverified
    closing_step: "Run PG + Api Cowork/Project/Memory/Schedule filters vs services; record."
  - truth: "SEC-77-02 (75-12) open."
    status: unverified
    closing_step: "Close SEC-77-02 with owner; record live run."
human_verification:
  - "Live memory/schedule + owner runbook review."
---

---
## Refresh 2026-11-22 (T855b-be, HEAD b46fd81f, SDK 10.0.402)

*Evidence first, then verdict (ADR-0002). No product code touched. Fresh test-services up failed twice at Dragonfly readiness (90 s, loaded host); DB suites ran vs existing healthy throwaway set (ag67-8382). No live-model, live-CDP, Win/macOS install, Railway, sign-in or screen-reader run claimed.*

*Status: gaps_found - suites pass; DB depth + SEC-77-02 remain*

| Check | Command | Result |
|---|---|---|
| restore | `dotnet restore Autonogrammer.Linux.slnf --locked-mode` | exit 0 |
| build | `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` | 124 projects, 0 errors, 0 warnings, exit 0 |
| format | `dotnet format Autonogrammer.Linux.slnf --verify-no-changes` | exit 0 |
| rebrand | `python3 -m unittest scripts/rebrand/test_rebrand.py; rebrand.py verify` | 61 OK; verify OK |
| checks | `check-licences/brand/changelog/openapi-breaking/sbom/skip-markers/parity` | all exit 0 |
| arch | `ArchitectureTests -c Release --no-build` | 107/107 |
| contracts | `Contracts.Tests non-Live` | 179/179 |
| accounts | `Accounts 215/215; Secrets.Os 83/83; Desktop 47/47` | all exit 0 |
| coord | `Coordination.Tests with services` | 87/87 |
| postgres | `Sessions.Postgres.Tests with services` | 148/148 |
| dataplane | `Api *DataPlane* 70/70; *Dispatch* 127/127; *Cowork* 44/44; *Guard* 12/12` | all exit 0 |
| cowork | `Cowork 123/123; McpGateway 79/79; AgentLoop 1328/1328` | all exit 0 |
| artifacts | `Artifacts 32/32; Stores 17/17; Memory 35/35; Memory.PG 1/1` | all exit 0 |
| browser | `BrowserUse.Tests 124/124` | exit 0 |
| compute | `ComputerUse 90/90; X11 9/9` | exit 0 |
| dispatch | `Client 1/1; Desktop 80/80` | exit 0 |
| resil | `Resilience 62/62; Sessions 124/124` | exit 0 |
| heads | `Presentation 2646/2646; UI 882/882; Web 444/444; Native 2353/2353` | exit 0 |
| a11y | `node --test a11y 48/48; fixture PASS; size ok; StyleA11y 17/17` | exit 0 |
| audit | `CoworkAudit 16/16` | exit 0 |

*Per-criterion: AgentLoop 1328/1328 Presentation/Native/UI/Web/Arch pass.*

*Gaps:*
- Memory.Postgres depth thin (1/1); phase-gate depth not proven. -> Run PG + Api Cowork/Project/Memory/Schedule filters vs services; record.
- SEC-77-02 (75-12) open. -> Close SEC-77-02 with owner; record live run.

*Human (never automated):*
- Live memory/schedule + owner runbook review.

*Digest: v1:sha256 over concatenated bytes of covered_files in listed order excluding this VERIFICATION file (self-reference not computable). Prior content below retained as history.*

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
   - `Harness.AgentLoop.Tests`: 1328/1328 on re-run after disk space was freed (the 4 failures seen on the
     100%-full-disk run — Hooks/RunModePolicy tests — did not reproduce; they were IO artifacts, not 75
     code, and are not recorded as a regression).
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
- SC3: full `Api.IntegrationTests` non-live (Cowork persistence + project/memory suites) still requires a
  build-capable host; `Harness.AgentLoop.Tests` is 1328/1328 (re-run above).
- SC5: `bash scripts/ci/repo-checks.sh` on an idle, non-full disk; `./bootstrap.sh` owner run.
- SC7: live-model/host verification (omp `memory.backend: off` consumption; wake-from-sleep).
