# 66-07 Summary: owner-action list and phase-gate record (partial)

## Result
Partial. The owner rulings of 2026-11-03 are applied to the literal probe, the pin count and the lane files. No build, test, gate, service, display or commit was run in this lane; the lane brief forbids them, so they remain for the wave gate. Phase 66 is not closed by this summary.

## Owner rulings applied (2026-11-03)
1. Non-identity legacy literals become Autonogrammer; identity-bearing names stay (owner rule, commit b94232d1). No literal in this lane's files needed a rename; every remaining hit is a config-protected identity, a frozen pin, a frozen fixture, or a rename-tool input (see Residual list).
2. The probe exemption is data-driven from `scripts/rebrand/rebrand.config.json`. `LegacyBrandLiteralGuardTests` now reads `brandRegex`, `protectedUrlPatterns` (with `lineMustMatch`) and every `excludedGlobs` entry with `content: true`, and the hard-coded GitHub-slug regex is removed. The probe's file set is `*.cs` under `src`, `services` and `apps`.
3. The LegacyBrandNames.cs pin count is 14 (the files tracked today). The new test `LegacyBrandNamesPinCountMatchesTheTrackedFiles` asserts 14 over `git ls-files`. The plan's count of 12 is replaced.
4. `tests/Autonogrammer.Harness.Acp.Tests/AcpFixtures.cs` is NOT changed. Per the parent correction, `CapturedCwd` must equal the working directory recorded in `TestData/frames/acp-in.ndjson` byte for byte. That literal is config-protected (`protectedUrlPatterns`, `/root/src/repos/ompos`), so the probe exempts it. A runtime-derived path would fail replay in any other checkout, including CI.

## Changes
- `tests/Autonogrammer.ArchitectureTests/LegacyBrandLiteralGuardTests.cs`: rewritten. Config-driven probe (brand regex, protected spans, frozen-file globs loaded from the config; a small glob-to-regex converter); self-test and full-tree scan kept; pin-count test added.
- `scripts/rebrand/**`: unchanged. These are the rename tool, its config and its tests. Their legacy strings are tokenMap keys and fixtures, and the config freezes them (`rename-docs`).
- Unchanged: `AcpFixtures.cs`, all 14 pins, `DataPlaneGuardTests.cs`, the ACP frames.
- Behaviour note: the config's GitHub-slug pattern (`(?i)swcstudiospace/ompos(?![A-Za-z0-9_-])`) is wider than the removed hard-coded regex. The old regex allowed only `/OmpOS` and `/OmpOS/issues/new`; the config pattern exempts the slug followed by any non-slug character. The config is the owner-ruled source, so this follows it. Owner to confirm.

## 14 LegacyBrandNames.cs pins
Verified by an enumeration of the `**/LegacyBrandNames.cs` paths on disk, which matches the 14 files `git ls-files` shows per the earlier lane:
1. src/Autonogrammer.Harness.Abstractions/LegacyBrandNames.cs
2. src/Autonogrammer.Harness.Plugins/LegacyBrandNames.cs
3. src/Autonogrammer.Harness.OmpConfig/LegacyBrandNames.cs
4. services/Autonogrammer.Api/LegacyBrandNames.cs
5. services/Autonogrammer.AgentWorker/LegacyBrandNames.cs
6. tests/Autonogrammer.Harness.Abstractions.Tests/LegacyBrandNames.cs
7. tests/Autonogrammer.Harness.Workspace.Tests/LegacyBrandNames.cs
8. tests/Autonogrammer.Harness.Mcp.Tests/LegacyBrandNames.cs
9. tests/Autonogrammer.Harness.Remote.Tests/LegacyBrandNames.cs
10. tests/Autonogrammer.Native.Tests/LegacyBrandNames.cs
11. tests/Autonogrammer.Harness.AgentLoop.Tests/LegacyBrandNames.cs
12. tests/Autonogrammer.Harness.Plugins.Tests/LegacyBrandNames.cs
13. tests/Autonogrammer.Api.IntegrationTests/LegacyBrandNames.cs
14. tests/Autonogrammer.Harness.OmpConfig.Tests/LegacyBrandNames.cs

## Residual list (after the config-driven exemption)
Probe file set (`*.cs` under `src`, `services`, `apps`):
- `src/Autonogrammer.Presentation/About/AboutModel.cs:22`: protected (GitHub slug).
- `src/Autonogrammer.Presentation/Commands/Handlers/FeedbackSlashHandler.cs:10`: protected (GitHub slug, `/issues/new`).
- `src/Autonogrammer.UI/Components/Shell/ShellInfo.cs:16`: protected (GitHub slug).
- `services/Autonogrammer.Api/Services/SourceInfoProvider.cs:17`: protected (GitHub slug).
- The 14 pins above: frozen by `**/LegacyBrandNames.cs`.

Outside the probe file set (not `.cs`):
- `apps/Autonogrammer.Android/Autonogrammer.Android.csproj:7` and `apps/Autonogrammer.Android/Properties/AndroidManifest.xml:2` (`com.swcstudiospace.ompos`): owner-kept globs (ApplicationId kept by owner decision 2026-11-03).

Lane files:
- `tests/Autonogrammer.Harness.Acp.Tests/AcpFixtures.cs:14` (`CapturedCwd`): protected pattern `/root/src/repos/ompos`, kept per the parent correction.
- `tests/Autonogrammer.Harness.Acp.Tests/TestData/frames/acp-in.ndjson:3`: frozen content (`tests/*/TestData/frames/**`).
- `tests/Autonogrammer.ArchitectureTests/DataPlaneGuardTests.cs:28-29`: owner-kept glob (guard's forbidden legacy-schema literal).
- `scripts/rebrand/**`: rename tool, config and tests (`rename-docs`).

## Owner or orchestrator decisions outstanding
- `.planning/phases/66-rebrand-to-autonogrammer/66-07-PLAN.md` (not in this lane, not edited) still pins 12 in its Task 2 verify snippet (`assert len(expected) == 12`) and in its prose. It should read 14 per the owner ruling. The orchestrator owns that edit.
- Confirm the widened GitHub-slug exemption noted under Changes.

## Task status
| Task | Status | Evidence or stop condition |
| --- | --- | --- |
| 1 Build, format, pure tests, repo gates | Not run in lane | Lane brief forbids builds, tests, formatters and gates. Left for the wave gate. |
| 2 Rebrand tool, probes, migration inventory | Owner rulings applied; gate not run | Pin count 14 recorded; probe exemption config-driven. `scripts/rebrand/test_rebrand.py` and fresh dry-run/verify not run. |
| 3 Test-services isolation (AG-66-12) | Blocked (earlier lane, unchanged) | `autonogrammer-test-postgres` and `autonogrammer-test-dragonfly` already present with unknown ownership; `scripts/test-services.sh` declares a third `GREPTIME_NAME`. Not touched. |
| 4 Web smoke on private ports (AG-66-6) | Not run in lane | Ports 5387, 5380, 5391 were free at the earlier check; the smoke is a gate step. |
| 5 Desktop window title (AG-66-13) | Route A: existing test (not run); route B not run | `tests/Autonogrammer.Native.Tests/Branding/MainWindowTitleTests.cs` is tracked and asserts `MainWindow.Title == "Autonogrammer"`. `/tmp/.X11-unix/X131` absent at the earlier check. |
| 6 Owner-action list (AG-66-14) | Implemented (earlier lane) | `66-OWNER-ACTIONS.md`: 8 open items with proving commands. |
| 7 Final record and gate commit | Not performed | Lane brief forbids `git add` and `git commit`. |

## Verification (exercised in this lane)
- Read-only enumeration of `**/LegacyBrandNames.cs` (14 paths).
- Read-only scan of the probe file set and lane files with the brand regex. Hits are as listed under Residual list.
- Not run: the new guard tests, the build, the format check, `test_rebrand.py`, and the service, smoke and display tasks. The main agent should build and run `LegacyBrandLiteralGuardTests` (`SelfTestIgnoresComposeAndCatchesAPlantedLiteral`, `FullTreeScanPasses`, `LegacyBrandNamesPinCountMatchesTheTrackedFiles`) on the final tree.

## Blockers carried forward
1. Task 3 (owner or A01): ownership of the existing `autonogrammer-test-postgres` and `autonogrammer-test-dragonfly` containers, and the third `GREPTIME_NAME` in `scripts/test-services.sh`. Not resolved here.
2. Held gate (human): the plan's `## Status` section holds gate execution pending parent authorization.
3. Tasks 1, 4, 5 (external): build, test, smoke and display steps need the wave gate and a private display.
4. Task 7 (integrator): gate commit with explicit paths.
