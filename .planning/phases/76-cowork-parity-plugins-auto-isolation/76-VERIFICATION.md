---
phase: 76-cowork-parity-plugins-auto-isolation
verified: 2026-11-22T01:53:17Z
status: gaps_found
score: "unit pass; live isolation blocked"
covered_files:
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-01-PLAN.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-01-SUMMARY.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-02-PLAN.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-02-SUMMARY.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-03-PLAN.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-03-SUMMARY.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-04-PLAN.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-04-SUMMARY.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-05-PLAN.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-05-SUMMARY.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-06-PLAN.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-06-SUMMARY.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-07-PLAN.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-07-SUMMARY.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-08-PLAN.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-08-SUMMARY.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-09-PLAN.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-09-SUMMARY.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-CONTEXT.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-PLAN-CHECK.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-RESEARCH.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-SOTA-COWORK-PARITY-PLUGINS-AUTO-ISOLATION.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-SOTA-COWORK-PARITY.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-VALIDATION.md"
  - ".planning/phases/76-cowork-parity-plugins-auto-isolation/76-VERIFICATION.md"
covered_digest: "v1:sha256:ad295f55011713f222a18133d365eb0bd81a56d917f006d6b69967cb4279dd34"
behavior_unverified: 2
overrides_applied: 0
re_verification: true
previous_status: gaps_found
previous_score: "gaps_found"
commits: ["b46fd81f"]
gaps:
  - truth: "Live isolation not executed."
    status: unverified
    closing_step: "Run live isolation suites on provisioned host; record."
  - truth: "Two partial criteria need host proof."
    status: unverified
    closing_step: "Complete composed run; record."
human_verification:
  - "Live isolation + on-host auto runs."
---

---
## Refresh 2026-11-22 (T855b-be, HEAD b46fd81f, SDK 10.0.402)

*Evidence first, then verdict (ADR-0002). No product code touched. Fresh test-services up failed twice at Dragonfly readiness (90 s, loaded host); DB suites ran vs existing healthy throwaway set (ag67-8382). No live-model, live-CDP, Win/macOS install, Railway, sign-in or screen-reader run claimed.*

*Status: gaps_found - unit pass; live isolation blocked*

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

*Per-criterion: AgentLoop/EngineHost/Harness slices pass.*

*Gaps:*
- Live isolation not executed. -> Run live isolation suites on provisioned host; record.
- Two partial criteria need host proof. -> Complete composed run; record.

*Human (never automated):*
- Live isolation + on-host auto runs.

*Digest: v1:sha256 over concatenated bytes of covered_files in listed order excluding this VERIFICATION file (self-reference not computable). Prior content below retained as history.*

# 76-VERIFICATION

Re-checked 2026-11-21 against `origin/main` @ `c04095ba` (SDK 10.0.402). Supersedes the 2026-11-10
`76-VERIFICATION.md` (which blamed a missing SDK 10.0.402 on the verifier host — the gate was never run
there; the SDK is installed and used here). The old "docs/parity/cowork-parity.md is absent" note is stale:
the file exists (10.4 KB).

## Success criteria

1. Plugin bundle/isolation/notification/Cowork/agent-loop/MCP/ACP/workspace/presentation/native/
   architecture test projects pass — PARTIAL.
   - `Harness.Isolation.Tests` (non-live) 30/30 VERIFIED.
   - `Cowork.Tests` 123/123; `Presentation.Tests` 2646/2646; `Native.Tests` 2353/2353 (--no-build batch);
     `Web.Tests` 444/444; `ArchitectureTests` 107/107 VERIFIED.
   - `Harness.AgentLoop.Tests`: 1328/1328 on re-run after disk space was freed (the disk-full run's 4
     Hooks/RunModePolicy failures did not reproduce; IO artifacts, not a 76 regression).
   - PluginBundles, Notifications, Mcp, Acp, Workspace, ContractTests, Abstractions suites NOT RUN by
     this lane.
2. Live isolation (Bubblewrap positive control) — BLOCKED. Probe fix landed (IsolationProbe.cs line 10:
   `--ro-bind / / /bin/true`); `IsolationLiveTests` gated by `AUTONOGRAMMER_LIVE_ISOLATION=1` and NOT
   executed (needs Bubblewrap + userns + disposable /etc). Wrapper design defects (deny-mask precedence,
   missing-target/socket handling, nested read-only grants, /tmp ordering) remain; see 76-09-SUMMARY.
3. Auto-mode rules behaviour (allows what rules + read-only list allow; asks otherwise; Manual parity for
   provider/MCP/tainted sessions) — SOURCE-VERIFIED only. Live model behaviour requires a live run
   (OD-07); never claimed passed.
4. Repo/licence/format + full gate — PARTIAL. `check-licences.sh` PASSED; format clean; `repo-checks.sh`
   exited 1 on a combined timed-out run (docs half: 224 files, 121 in SUMMARY.md, 0 errors, 0 warnings).
   `./bootstrap.sh` NOT RUN (owner action).
5. Parity rows MET only with named passing test; omp-consumed rows stay PARTIAL — HELD. `docs/parity/
   cowork-parity.md` exists, but rows depending on omp consuming flags and on unshipped 75-07..13/76-09
   work must remain PARTIAL until their named tests land and pass.

**Score:** 2/5 verified; 1 blocked; 2 partial.

### Human verification required

- SC1: runs of the remaining named projects (PluginBundles, Notifications, Mcp, Acp, Workspace,
  ContractTests, Abstractions) on a build-capable host.
- SC2: `AUTONOGRAMMER_LIVE_ISOLATION=1` positive control in a disposable-/etc outer Bubblewrap
  environment, after the wrapper design lands (76-09).
- SC4: `repo-checks.sh` on an idle disk; `./bootstrap.sh` owner run.
- SC5: parity MET audit against current test names; live-run records for omp-consumed rows.
