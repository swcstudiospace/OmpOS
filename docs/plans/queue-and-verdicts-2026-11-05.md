# Queue and verdicts, 2026-11-05

Scope: the 22 repo-buildable open-question rows in `docs/open-questions.md`; the 39 unchecked phases in v0.9.2 to v0.15.0; the remaining items of phases 66.1 to 66.4. Verdicts come from repository reads; the Task 1 build and restore logs (`/tmp/ag-recon/v66-07-task1.log`); headless runs of `KeyboardFocusTests` (9 of 9) and `FontFaceResolutionTests` (7 of 7) on this host (Ubuntu 24.04.4, Avalonia 12.1.4); two throwaway headless probes, deleted after their runs; and one primary-source read of Avalonia issue #22021. The register rows are not edited here; their status text stays as it was until an owner confirms each verdict.

## 1. Open-question verdicts

| ID | Category | Verdict | Evidence |
| --- | --- | --- | --- |
| OQ-30 | Answered from a primary source | Not fixed. Avalonia issue #22021 is open (fetched 2026-11-05; last updated 2026-10-14), and the 12.1.4 release notes list no fix. Keep the mitigation. | https://github.com/AvaloniaUI/Avalonia/issues/22021; tests/Autonogrammer.Native.Tests/TestAppBuilder.cs:10 |
| OQ-31 | Answered by a passing test | ShiftTabMovesFocusBack passes: KeyboardFocusTests 9 of 9 on Avalonia 12.1.4, Ubuntu 24.04.4, this host. The class remark at lines 17-19 is not the evidence. | tests/Autonogrammer.Native.Tests/KeyboardFocusTests.cs:54 |
| OQ-32 | Answered by a passing test | KeyboardFocusedSharedControlsShowTheAccentRing (lines 73-97) calls AssertAccentRing, whose focus-adorner-null assertion at line 250 passes in the 9 of 9 run. | tests/Autonogrammer.Native.Tests/KeyboardFocusTests.cs:250 |
| OQ-33 | Answered by the build | The call compiles in the Linux solution, which includes the Native head: warnings as errors, exit 0, no warning lines. | Autonogrammer.Linux.slnf:44; Task 1 log |
| OQ-35 | Answered by the build, net of suppressions | No warning-level analyzer diagnostic reaches the Linux build (warnings as errors, 0 warnings). The recorded suppressions and .editorconfig overrides can hide a diagnostic, so this does not prove that none would fire. | Task 1 log; 25.1-FINDINGS.md section 10 |
| OQ-36 | Answered by the build, net of suppressions | No warning-level AVLN diagnostic reaches the same build. The same suppression caveat applies. | Task 1 log; 25.1-FINDINGS.md section 10 |
| OQ-40 | Answered by a headless probe | ScrollViewer defaults to Focusable=False (IsTabStop=True) in the headless app, measured on a detached control. SessionView sets Focusable="True" explicitly on TranscriptScroll, so the explicit setting is needed. The probe was deleted after the run. | src/Autonogrammer.Native.Views/Views/SessionView.axaml:5-8 |
| OQ-41 | Answered by a passing test | FontFaceResolutionTests passes 7 of 7 on this host: each Autonogrammer.Font.Family weight (400, 500, 600) resolves to a bundled face whose family is DM Sans, with no fallback and no synthesised bold. The embedded TTFs are used; fontconfig is not involved. | tests/Autonogrammer.Native.Tests/Theme/FontFaceResolutionTests.cs:39-59 |
| OQ-42 | Answered; owner decided 2026-11-05: IsTrimmable=false on the Native head and views | A trimmed publish of the head fails under TreatWarningsAsErrors: 11 IL2026 from ReflectionBinding in XAML (src/Autonogrammer.Native.Views/MainView.axaml:377 and :402, among others), 2 IL2104 from third-party assemblies (Avalonia.Controls.DataGrid 12.1.2, Iciclecreek.Avalonia.Terminal 4.1.0), then NETSDK1144. No shipped publish trims (desktop-release.yml:109, ci.yml:297). Applied: both csproj set IsTrimmable=false with a comment, following services/Autonogrammer.Api. Both packages.lock.json files lose only Microsoft.NET.ILLink.Tasks (6 lines each), which the SDK adds for IsTrimmable=true. Warnings-as-errors build of the head: 0 warnings, 0 errors. | apps/Autonogrammer.Native/Autonogrammer.Native.csproj:8; src/Autonogrammer.Native.Views/Autonogrammer.Native.Views.csproj:7; /tmp/ag-recon/v-trim-build.log |
| OQ-43 | Answered by the committed lock files | The transitive pins are in the committed packages.lock.json files, and the locked restore passes (Task 1: exit 0, no NU1004). | packages.lock.json files; Task 1 log |
| OQ-45 | Answered by the build | The container usage compiles on 12.1.4 under the Linux build gate. The container type is confirmed by compilation, not by reading code. | src/Autonogrammer.Native.Views/Views/Automations/AutomationsPageView.axaml.cs:142 |
| OQ-46 | Answered by a passing test | The Thickness assertion is at KeyboardFocusTests.cs:249; the row's :219 citation is stale. It sits in AssertAccentRing, called by KeyboardFocusedSharedControlsShowTheAccentRing, which passes in the 9 of 9 run. | tests/Autonogrammer.Native.Tests/KeyboardFocusTests.cs:249 |
| OQ-48 | Answered by a headless probe; defect found | The 12.1.4 TextBox template has no PART_Watermark. Its placeholder parts are PART_Placeholder and PART_FloatingPlaceholder, so the style at AutonogrammerControls.axaml:358-360 matches nothing. The TextSecondary token resolves (#55555a, light). The fix and a regression test are a separate item. | src/Autonogrammer.Native.Views/Themes/AutonogrammerControls.axaml:358 |
| OQ-56 | Answered in the repo | The MCP revision is pinned to 2025-11-25 and used by PinnedOptions. Re-verifying on each SDK upgrade stays a process step. | src/Autonogrammer.Harness.Mcp/McpClientConnector.cs:14,109 |
| KL-01 to KL-07 | Known limit | Retained as documented limits, with no code change in this milestone. Owner acceptance is not recorded. | Each row's file and line |
| Q-HOST-DFLY-PIN | Owner decision | The owner chooses the Railway tag and digest. The local stack pins dragonfly v1.27.1 by digest, while Railway runs the unpinned ghcr.io `:latest` image. | deploy/ local stack file, line 29; deploy/railway/README.md |

Counts: 21 of the 22 items have a verdict (OQ-30, OQ-31, OQ-32, OQ-33, OQ-35, OQ-36, OQ-40, OQ-41, OQ-42, OQ-43, OQ-45, OQ-46, OQ-48, OQ-56 and KL-01 to KL-07). One owner question remains: Q-HOST-DFLY-PIN (the pin). OQ-42's policy question is answered by the owner (IsTrimmable=false on the Native head and views).

## 2. Unchecked phases v0.9.2 to v0.15.0: classification and queue

Artifacts are PLAN, SUMMARY and VERIFICATION counts (P/S/V) in the phase directory. Classes: REPO-BUILDABLE (no external dependency), EXTERNAL (the dependency is named), SUPERSEDED (evidence named), UNKNOWN (status line not read). Queue: EARLY means repo-local with no dependency; MID means repo work that depends on other phases or on a repo-local step; LATE means it waits on an external dependency or on later phases.

| Phase | Title | Artifacts | Class | Queue | External blocker or next step |
| --- | --- | --- | --- | --- | --- |
| 6 | Per-Sandbox Credential Issuance | none | EXTERNAL | LATE | A verified vendor revoke call (owner action) |
| 12 | Hosted Deploy Config and Exposure Gate | none | EXTERNAL | LATE | OD-03 sign-in authority; Railway deploys paused |
| 13 | Hosted Routed harness (ROADMAP title names the engine) | none (commit 3f18b99 only) | EXTERNAL | LATE | OD-03; Railway deploy for criterion 2 |
| 14 | Vercel Web Against Production Origin | none | EXTERNAL | LATE | Production origin unset (HO-07); OD-03 |
| 22 | Desktop Heads | P0/S1/V0 | EXTERNAL | LATE | Windows runner (criterion 3); Mac channel signing (criteria 4 and 5) |
| 23 | Web Head on Shared Presentation Layer | P1/S1/V0 | REPO-BUILDABLE | MID | Vercel static hosting step is owner-side |
| 24 | iOS and Android Heads | P1/S1/V0 | EXTERNAL | LATE | Mac channel with Xcode for iOS (HO-06) |
| 25.4 | Build, Deploy and Image Integrity | P1/S1/V0 | EXTERNAL (pin) and REPO-BUILDABLE (rest) | MID | GHCR access for the `:latest` pin (Q-SB-IMAGE-PIN); remaining repo items proceed |
| 26 | Agents, Worktrees, Cloud and Remote | P0/S1/V0 | EXTERNAL | LATE | OD-03; hosted API for live cloud runs (OQ-70) |
| 27 | Editor and Review | P0/S1/V0 | REPO-BUILDABLE | EARLY | Open-folder action and inline-edit binding missing |
| 28 | Terminal, MCP, Rules and Search | P0/S1/V0 | REPO-BUILDABLE | MID | Terminal approval bridge and MCP registry wiring; feeds phases 31 and 33 |
| 29 | Browser Pane, Completion and Export | P0/S1/V0 | REPO-BUILDABLE | MID | No browser pane code (OQ-51) |
| 30 | Later Compatibility and Admin | P0/S1/V0 | EXTERNAL | LATE | OD-03 via phase 26; XP-09 gate |
| 31 | Plugin Manifests and Hooks | none | REPO-BUILDABLE | EARLY | No head loads manifests or runs hooks (OQ-84); foundation for 32 and 33 |
| 32 | Automations | none | REPO-BUILDABLE | MID | Partial: the engine, guards and presentation model pass; nothing wires the engine together or starts a run, and there is no view. Depends on phase 31 (ROADMAP status at :734). |
| 33 | Omp CLI Terminal and Shared Registry | none | REPO-BUILDABLE | MID | Partial: criteria 1 and 4 met live; criteria 2 and 3 partial. The status line (:748) says no head references Harness.McpGateway, but apps/Autonogrammer.Native references it (Autonogrammer.Native.csproj:32; Cowork/CoworkToolServer.cs:4), so re-audit before queueing. ADR-0010 is Proposed: owner acceptance needed. |
| 34 | Remote Development | none | SUPERSEDED in part | LATE | Phase 58 (ADR-0015) supersedes most of it; the live remainder waits on OD-06 |
| 35 | Code Quality Bar | P1/S1/V1 | REPO-BUILDABLE | LATE | Criteria 1 and 4 partly met; best done after feature phases |
| 37 | Session Runtime, Streaming and Local Terminal | P1/S1/V1 | EXTERNAL | MID | Owner durability decision; real herdr endpoint (VX-06) |
| 38 | Chat Window and Slash Commands | none | REPO-BUILDABLE | MID | Criteria 3 and 4 partial; builds on phase 37 streams |
| 39 | Kanban Board | P1/S1/V1 | REPO-BUILDABLE | EARLY | No lease holder reported; screenshots missing |
| 40 | Settings Window and Model Providers | P1/S1/V1 | EXTERNAL | LATE | Owner provider credentials for criterion 5 (OQ-71) |
| 41 | Pets Companion | none | EXTERNAL | LATE | Owner sign-off and asset licence (OQ-72) |
| 42 | Cloud Terminals on Railway | no directory | EXTERNAL | LATE | Railway's written policy on persistent containers; prototype only until then |
| 43 | Mobile and Web Terminals | no directory | EXTERNAL | LATE | iOS and Android hardware; Mac channel for the iOS build |
| 44 | Plugin Submodules and Launch Contract | P1/S1/V1 | REPO-BUILDABLE | EARLY | Fresh-clone run unrecorded (CI token is owner action OQ-63) |
| 46 | Default Agent Panel Parity | P1/S1/V1 | REPO-BUILDABLE | MID | Criterion 6 eight-screenshot set missing; phase 50 follow-up exists |
| 47 | Agent Panel on the Web Head | P0/S0/V0 (deferred note only) | EXTERNAL | LATE | Hosted API runs only the InMemory harness; Railway deploys paused (OD-03) |
| 49 | Live Steering | P1/S1/V1 | EXTERNAL | LATE | Approved model turn for the recorded live run (OD-07) |
| 51 | Brand, Theme and Licence | P1/S1/V1 | REPO-BUILDABLE | EARLY | The web head has no About page (criterion 4) |
| 54 | Web Agent Panel and Workspace Screens | P1/S1/V1 | REPO-BUILDABLE | MID | No Cloud or Remote view; depends on the phase 53 API surface |
| 55 | Showing the Web Application | P1/S1/V1 | REPO-BUILDABLE | MID | Dark 1024 and Dark 390 screenshots uncommitted; depends on phase 54 |
| 56 | Slash Commands and Convention Discovery | P1/S1/V1 | REPO-BUILDABLE | MID | No cloud-session discovery; web palette passes Empty |
| 57 | Hooks | P1/S1/V1 | EXTERNAL | LATE | Live enforcement in the engine unverified (OD-07) |
| 59 | Multi-repo Workspaces | P1/S1/V1 | REPO-BUILDABLE | MID | MultiRootWorkspaceTree and convention merge unwired |
| 60 | Cloud Agents on Railway | P1/S1/V1 | EXTERNAL | LATE | OD-03; API not deployed (OQ-71, OQ-73, OQ-100) |
| 61 | Settings Parity | P1/S1/V1 | REPO-BUILDABLE | LATE | Labs page missing; Worktrees and Ignore pages absent; depends on phases 56 to 60 |
| 62 | Providers, EU US CN and Coding Plans | P1/S1/V1 | EXTERNAL | LATE | Live provider key check needs owner keys (OQ-71) |
| 63 | Cursor Feature Completion | P1/S1/V1 | REPO-BUILDABLE | LATE | PR, browser, terminal-output and side-chat features not wired; depends on phases 56 to 62 |

Totals: 39 phases. REPO-BUILDABLE 19, EXTERNAL 18, plus phase 25.4 (mixed), SUPERSEDED in part 1 (phase 34), UNKNOWN 0. Queue: EARLY 5 (27, 31, 39, 44, 51), MID 13, LATE the rest.

## 3. Phases 66.1 to 66.4: remaining items

Recorded status: 66.1 `human_needed` (VERIFICATION line 3); 66.2 "Complete, not deployed"; 66.3 "Complete with caveats, no screen reader or device"; 66.4 "Complete with caveats". No UAT files exist.

- 66.1, repo-buildable: the `./bootstrap.sh` gate has not been run at HEAD (local; needs the container services); OQ-405 text at `docs/open-questions.md:274`; residuals L4 to L8 and M1 to M2; REQUIREMENTS.md still says "Planned" for AG-SC-1 to AG-SC-6. External: the Android lock was hand-patched and not restored (needs the Android workload or the GitHub Actions android job); CI has never run on the four commits.
- 66.2, repo-buildable: `api.Containerfile` has never been built (local podman build). External: pushing the branch and opening the PR, the Railway API project, the Vercel production URL, the model credential (OQ-172), the sandbox provider (OQ-173), `PR_SET_DUMPABLE` in the Railway container (OQ-174), the first engine dependency snapshot (OQ-176), SLO targets (OQ-180), multi-user sign-in (OQ-182), the approval recipe (OQ-183), Railway Config as Code (OQ-181), and the staged worker (OQ-171).
- 66.3, repo-buildable: desktop head (OQ-179); skip-link Tab behaviour (K2); not-found landmarks (K3); box-overlap check (K14); first-load stalls (OQ-191); test counts predate the final stylesheet edits, so rerun. External: NVDA, JAWS, VoiceOver, TalkBack and Orca passes (OQ-178); phones, tablets and switch devices; the real Windows High Contrast theme (K11); browsers other than Chrome; the legal accessibility statement.
- 66.4, repo-buildable: the full bootstrap gate on the shipped commit (about 40 minutes locally, load-sensitive); a local restore drill; the gate-row log storage note. External: the image build, trivy scan, attestation and HEALTHCHECK (OQ-175); CodeQL, Scorecard and the release workflow, which have never run; the CI "Start test services" failure (OQ-168); restore from a real backup; logo sign-off and trademark.

## 4. Owner actions (external blockers, consolidated)

- Sign-in authority OD-03 (phases 12, 13, 14, 26, 30, 47, 60).
- Railway: written policy on persistent containers (42), paused deploys (12, 13), and the staged worker (OQ-171).
- Windows runner and Mac channel, including iOS signing (22, 24, 43).
- Production origin HO-07 (14).
- GHCR access for the image pin (25.4; Q-SB-IMAGE-PIN).
- Remote transport decision OD-06 (34); owner durability decision and herdr endpoint VX-06 (37).
- Owner provider credentials OQ-71 (40, 62); asset licence OQ-72 (41); hosted API deployment OQ-70, OQ-73, OQ-100 (26, 60).
- Approval for live engine turns OD-07 (49, 57).
- CI token OQ-63 (44); XP-09 gate (30).
- Dragonfly pin tag and digest (Q-HOST-DFLY-PIN).
- Model credential OQ-172 and sandbox provider OQ-173 (66.2).
- Assistive-technology passes and devices OQ-178; legal accessibility statement (66.3).
- SLO targets OQ-180; logo sign-off and trademark (66.4).
