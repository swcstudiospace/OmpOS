# Phase 45 Verification: Agent Loop Stage Sources

How to re-run each proof and what it proves. Run from the repository root with the .NET environment set as in `.planning/phases/44-plugin-submodules-launch-contract/44-VERIFICATION.md`. Every test below is hermetic: it uses fake processes, fake HTTP servers, temporary directories and recorded fixture files, never a Substrate server on `127.0.0.1:7410`, and never a model.

Results recorded on 2026-10-26 are in `45-SUMMARY.md`. All tests of this phase live in one project, `tests/Autonogrammer.Harness.AgentLoop.Tests`, which passed with 596 tests in the wave 3 run (F6b). The 2026-10-26 run recorded the project total, not a count per class.

| Criterion | Command | What it proves |
|-----------|---------|----------------|
| 1 | `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*UltrathinkPlanParserTests*" --filter-class "*UltrathinkStateObserverTests*" --filter-class "*SwarmLiveObserverTests*" --filter-class "*SwarmRunTableTests*" --filter-class "*SubstrateStageObserverTests*" --filter-class "*GsdToolsClientTests*" --filter-class "*GsdStateObserverTests*"` | Each observer maps its recorded fixture to the expected stage events, and unknown or retyped fields are skipped without an exception. |
| 2 | `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*CompositeStageSourceTests*" --filter-class "*GsdProcessRunnerTests*" --filter-class "*PluginAvailabilityStageSourceTests*" --filter-class "*SwarmEventTailTests*"` plus the degraded-path and cancellation methods of the observer classes above | A failing or missing source is one Degraded or Unavailable event with a plain reason; cancellation completes the sequence quietly and disposes every child enumerator; a timeout or a cancel kills the whole process tree of a GSD tool call. |
| 3 | `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*AgentSessionRegistryTests*" --filter-class "*AgentSessionKindTests*"` | Atomic write, owner-only file, crash leftovers ignored, quarantine of an unusable or oversized file, Running becomes Lost after a restart, resume references persisted and never printed. |
| 3 (head) | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*SessionEngineSetTests*" --filter-class "*AgentHeadPanelTests*"` | The native head recovers the registry after a restart and resumes a lost session with its stored native id (Phase 46 composition). |
| 4 | `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*GsdTaskContextSourceTests*" --filter-class "*SubstrateContextReaderTests*"` | The context merge, the Substrate title winning, scope discipline of every query, and one warning per failed part. |
| 5 | `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*SubstrateSteeringRecorderTests*" --filter-class "*SubstrateGatewayTests*"` | The exact `note` event body, no operator text, Skipped with a reason when the server is unreachable, and the gateway rules (no redirect, one origin, response cap, token only as a header). |
| 6 | `./bootstrap.sh`, or `dotnet build Autonogrammer.Linux.slnf -c Release` then `dotnet test Autonogrammer.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"` | Build with warnings as errors, analyzers and every non-live test. Recorded: 4237 passed, 0 failed (F5); every test project of the solution filter passed after wave 3 (F6b). |

## Known limits of this verification

- Nothing here talks to a real Substrate server, a real swarm run or a real planner run. The fixtures were recorded from the formats the plugins write (dossiers in `docs/plans/agent-loop/`); a plugin that changes its file format degrades its stage until the observer is updated.
- Windows, macOS, Android and the hosted deployments were not run.
- The observers' timing tests use fake clocks. Real poll timing on a loaded host was not measured.
