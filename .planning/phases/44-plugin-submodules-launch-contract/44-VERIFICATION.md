# Phase 44 Verification: Plugin Submodules and Launch Contract

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
git submodule update --init --recursive      # the private plugins/agent-substrate needs read access
```

Results recorded on 2026-10-26 are in `44-SUMMARY.md`. A row marked "no recorded run" has a command but no result.

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 | `dotnet test tests/OmpOS.ArchitectureTests -c Release -- --filter-class "*SubmoduleHygieneTests*"` | `.gitmodules` names exactly the three plugin submodules with https GitHub URLs and no credential or local path; each path is a gitlink in the index. | Part of the passing `OmpOS.ArchitectureTests` project (46 tests in the wave 3 run). |
| 1 | `git ls-remote https://github.com/swcstudiospace/ultrathink.git 71101ed069a66b3862fa5da5b01995a60c2160c2` (and the same for `swarm.git` with `c900a510db3e50de6e9f8b60453bdded6fdf72df`, `substrate.git` with `754c51a17a9e6ffbc222d04bdaf4e783b4eca720`) | Each pin exists on its remote. Empty output means the pin is not pushed. | No recorded run in this record set (D44-5 checked it at pin time). The substrate remote is private and needs read access. |
| 2 | `git clone --recurse-submodules https://github.com/swcstudiospace/OmpOS.git /tmp/fresh && cd /tmp/fresh && git submodule status && dotnet test tests/OmpOS.ArchitectureTests -c Release` | A fresh clone restores `plugins/claude-ultrathink`, `plugins/agent-swarm` and `plugins/agent-substrate`, and the hygiene tests pass with them present. | No recorded run. |
| 3 | `cd plugins/gsd-core-omp && npm test` (runs `node --test "test/*.test.mjs"`), then `node plugins/gsd-core-omp/bin/gsd-omp-probe.mjs; echo $?` with and without `GSD_CORE_HOME` pointing at an installed GSD-Core | The node tests cover health, linking, the steering inbox and the connection report. The probe prints one JSON object and exits 0 (healthy), 2 (degraded) or 3 (not installed). | No recorded run. |
| 4 | `dotnet test tests/OmpOS.Harness.AgentLoop.Tests -c Release -- --filter-class "*OmpPluginCatalogTests*" --filter-class "*SubmoduleNotInitializedExceptionTests*" --filter-class "*OmpPluginResolverTests*"` | Composition order Ultrathink, Swarm, Substrate, GSD-Core; the exception message names the plugin, the path and the exact fix; every resolver health state. | `OmpOS.Harness.AgentLoop.Tests` passed (596 tests) in the wave 3 run. |
| 5 | `dotnet test tests/OmpOS.Harness.AgentLoop.Tests -c Release -- --filter-class "*OmpLaunchPlannerTests*" --filter-class "*SessionStateLayoutTests*"` and `dotnet test tests/OmpOS.Harness.Acp.Tests -c Release -- --filter-class "*AcpLaunchPlumbingTests*" --filter-class "*AcpMcpServerSessionTests*"` | The exact argument list and environment of a plan; `SWARM_GSD_STATE_FILE` is `<session>/swarm/gsd-state.md`; `SWARM_AGENT` and `SWARM_TASK_ID` are never set; the token appears only in the MCP header; the transport applies extra arguments and environment to the child; `session/new` and `session/load` carry the MCP specs. | Passed inside the wave 3 run of both projects. |
| 6 | `OMPOS_LIVE_OMP=1 dotnet test tests/OmpOS.Harness.AgentLoop.Tests -c Release --no-build -- --filter-trait "Category=Live"` (needs `omp` on `PATH`, Linux, initialised submodules) | The real `omp acp`, started with the planned arguments and environment, connects all planned plugins, reports a fresh heartbeat in mode `rpc`, and no omp process of the run remains. No `session/prompt` is sent. | Passed on 2026-10-26 against omp 18.12.6 (`LiveOmpHandshakeTests`). |
| 7 | `./bootstrap.sh` (or, for the gate-equivalent run, `dotnet build OmpOS.Linux.slnf -c Release` and then `dotnet test OmpOS.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"`, the line `bootstrap.sh` runs) | Build with warnings as errors, the analyzers, and every non-live test. | Gate-equivalent run: 4237 passed, 0 failed (F5, 2026-10-26). After wave 3 every test project of `OmpOS.Linux.slnf` passed (F6b). `./bootstrap.sh` itself: no recorded run for this record. |
| 7 | `dotnet test tests/OmpOS.ArchitectureTests -c Release -- --filter-method "*TodoScan_SkipsPluginSubmodulesButScansTheFirstPartyPlugin*"` | The TODO scanner skips third-party content under `plugins/` and scans `plugins/gsd-core-omp`. | Inside the passing `OmpOS.ArchitectureTests` project. |

## Known limits of this verification

- Windows, macOS, Android and the hosted deployments were not run.
- A live model turn was not run (OD-07). Nothing here shows that a plugin does its work when a model answers; it shows that the plugins load and that the planner builds the exact launch.
- The CI step that checks out the private submodule needs the `SUBMODULE_TOKEN` secret (OQ-63). Without it the job warns and continues, so a green CI run does not show that the substrate plugin was present.
