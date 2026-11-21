---
phase: 76-cowork-parity-plugins-auto-isolation
verified: 2026-11-21T22:40:00Z
status: gaps_found
score: 2/5 verified; 1 blocked (live isolation); 2 partial
covered_files:
  - .planning/phases/76-cowork-parity-plugins-auto-isolation/76-01-SUMMARY.md
  - .planning/phases/76-cowork-parity-plugins-auto-isolation/76-02-SUMMARY.md
  - .planning/phases/76-cowork-parity-plugins-auto-isolation/76-03-SUMMARY.md
  - .planning/phases/76-cowork-parity-plugins-auto-isolation/76-04-SUMMARY.md
  - .planning/phases/76-cowork-parity-plugins-auto-isolation/76-05-SUMMARY.md
  - .planning/phases/76-cowork-parity-plugins-auto-isolation/76-06-SUMMARY.md
  - .planning/phases/76-cowork-parity-plugins-auto-isolation/76-07-SUMMARY.md
  - .planning/phases/76-cowork-parity-plugins-auto-isolation/76-08-SUMMARY.md
  - .planning/phases/76-cowork-parity-plugins-auto-isolation/76-09-SUMMARY.md
  - src/Autonogrammer.Harness.Isolation/IsolationProbe.cs
  - tests/Autonogrammer.Harness.Isolation.Tests/IsolationLiveTests.cs
  - tests/Autonogrammer.Harness.Isolation.Tests/IsolationProbeTests.cs
  - docs/parity/cowork-parity.md
---

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
   - `Harness.AgentLoop.Tests`: 4 failures on a disk-full run (Hooks/RunModePolicy, phase-57/73
     surfaces). Not a 76 regression; not green. Re-run on a clean host required.
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

- SC1: clean-host runs of the remaining named projects (PluginBundles, Notifications, Mcp, Acp,
  Workspace, ContractTests, Abstractions, AgentLoop Hooks) — the 4 disk-full failures dispositioned.
- SC2: `AUTONOGRAMMER_LIVE_ISOLATION=1` positive control in a disposable-/etc outer Bubblewrap
  environment, after the wrapper design lands (76-09).
- SC4: `repo-checks.sh` on an idle disk; `./bootstrap.sh` owner run.
- SC5: parity MET audit against current test names; live-run records for omp-consumed rows.
