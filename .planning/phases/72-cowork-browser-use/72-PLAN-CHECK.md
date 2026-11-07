# Phase 72 plan check, round 2 (after the D-47..D-51 amendments)

Scope: B1-B6 of round 1, new defects from the replan (waves, ownership, XML structure, stale strings, AG coverage). Every claim below was checked by reading the plan files or by a structure script over them. Nothing was built or run.

**Verdict: BLOCKERS (1 new blocker, 6 warnings). B1-B6 of round 1 are resolved.**

## Round-1 blockers

| # | Status | Evidence |
| --- | --- | --- |
| B1 Settings page unreachable | resolved | 72-06 frontmatter lines 24-26 own `HeadSettings.cs`, `HeadSettingsOptions.cs`, `HeadSettingsBrowserUseTests.cs`; verify line 110 runs the class; 72-08 line 75/88 hands `BrowserUseSettingsSource` to the options `AgentHead` builds (D-47). |
| B2 composition site | resolved | 72-08 frontmatter line 9 is `Agents/AgentHead.cs`; `Program.cs`/`App.axaml.cs` appear only in "NOT edited" sentences (72-08:67, CONTEXT D-42). |
| B3 capability seam | resolved | D-44: 72 defines the `BrowserUseCapability.IsOffered` predicate (72-03:89, :145 truth table), 72-08:79/107 only registers it; no hub call. |
| B4 web mount | resolved | 72-05 owns one render line in `src/Autonogrammer.UI/Components/Cowork/CoworkWorkbench.razor` (69-04-PLAN.md:16 creates that path) with a bUnit assertion (72-05 Task 3, verify lists `*BrowserPaneTests*`); the Avalonia panel is registered in 72-08; Android edit is explicitly out (D-43). |
| B5 Quarantine | resolved | 72-03 files list `BrowserDownloadQuarantine.cs`, `BrowserDownloadTests.cs`; acceptance and verify (line 117) cover Deny/Quarantine, 0700, 50 MiB, executable refusal, `MustBeLocal` release, the `browser.local` card and `beforeunload`. |
| B6 pane seam | resolved | 72-04 adds `IBrowserPaneControl.cs`, `BrowserInput.cs`, `BrowserCallGate.cs`; `TakeOverController` implements the control; the view model binds feed + control; 72-05 key_link forbids Harness.BrowserUse in views; 72-03 depends on 72-04 and wraps every call in the gate; 72-08 registers the controller and tests Stop through the real controller and host. |

## New BLOCKER

- **72-08-PLAN.md:6 (`depends_on: [72-03, 72-05, 72-06]`) vs Task 3 verify lines 131-145.** 72-08 and 72-07 share wave 4, but 72-08 Task 3 runs the whole offline BrowserUse suite (line 131), the whole `Category=Live` suite (line 133), the `*HeadfulSmokeLiveTests*` class (line 135) and the full ArchitectureTests run (line 141). Those classes and the corpus fixtures (`SiteTests`, `CorpusPagesInventoryTests`, `HostileCorpusLiveTests`, `HeadfulSmokeLiveTests`, `ScreenshotLadderLiveTests`) are created by 72-07. With no dependency the headful verify finds no class if 72-08 runs first, and the full-suite results depend on an undeclared order. Fix: add `72-07` to 72-08 `depends_on` (wave 5, and update D-50 / VALIDATION wave text), or move those four gates into 72-07's last task.

## Warnings (6)

1. **72-07-PLAN.md:79** still says 72-03 owned the csproj edit "earlier in wave 2"; 72-03 is now wave 3.
2. **72-04-PLAN.md:71** says "four additive L0 types"; the plan now creates six (frontmatter lines 8-13).
3. **72-04-PLAN.md:71 and :106** still cite `MainView.axaml.cs:826` as the F6 key; D-51 says the Cowork surface has its own focus code and F6-release lives in `BrowserPaneView` (72-05:80). Task 2 acceptance line 107 also gives "F6 releases it" to `TakeOverControllerTests`; align it to the release method that the pane calls.
4. **72-01-PLAN.md:100** lists `ALL_PROXY` among names "the engine policy keeps"; `EngineEnvironmentPolicy.BuiltInNames` has no `ALL_PROXY`. Harmless extra plant, wording only.
5. **Raw `<` in prose** (`Action<BrowserUseOptions>` in 72-01:79, `e<n>` in 72-03:101, `<img>` in 72-05:57) while the rest of the files use `&lt;`.
6. **72-RESEARCH.md lines 11 and 13** keep the pre-amendment text (ProcessSpawning "Acp, Rpc, Sandbox today", `run-smoke.sh:78-97`); the new "Integration amendments" section at line 81 overrides them. Cosmetic.

## Checks that passed

- Waves versus `depends_on` (script over frontmatter): 72-01 and 72-04 wave 1 (`[]`); 72-02 wave 2 (`[72-01]`), 72-05 wave 2 (`[72-04]`), 72-06 wave 2 (`[72-01]`); 72-03 wave 3 (`[72-01, 72-02, 72-04]`); 72-07 wave 4 (`[72-03]`), 72-08 wave 4 (`[72-03, 72-05, 72-06]`). Each wave number equals max(deps)+1; no cycles.
- Same-wave `files_modified` overlap: none. The only shared file is the BrowserUse test csproj (72-03 wave 3, 72-07 wave 4), ordered by `depends_on`.
- Frontmatter `files_modified` equals the union of task `<files>` in all eight plans (no differences; globs removed from 72-03). `estimate.tasks` equals the actual task count in all eight.
- 49-plus automated blocks: every `<automated>` is directly followed by `<fails_when>` (01: 6/6, 02: 4/4, 03: 8/8, 04: 5/5, 05: 9/9, 06: 7/7, 07: 7/7, 08: 17/17); every `<verify>` has its `</verify>`.
- Stale strings: no plan names `Program.cs`/`App.axaml.cs` as a site to edit, `src/.../BrowserUse/Support/` or `Settings/Pages/` (only in "does not exist" sentences); "four new files" gone from the ownership text; "MUST NOT depend on 72-01" gone from 72-02 (now `depends_on: [72-01]`, CONTEXT D-50); 72-02 Task 2 now owns `LocalTestSite.cs`; 72-01 commit covers all three tasks; 72-07 filter is `*CorpusPagesInventoryTests*`; D-35 now says `Settings/BrowserUse/`; 72-04 Task 1 verify runs `ContractsUiFreeTests`; 72-05 Task 3 verify runs `ColourLiteralTests`, `ThemeContrastTests`, `AccessibilityTests`; 72-08 adds `KeyboardSettingsSource.cs` (`Key.OemPeriod => "."`) and its test row.
- AG-72-1..16 each appear in a plan `requirements`: 01 {1,2,3,15}; 02 {4,5}; 03 {8,9,10,11,12,14,15}; 04 and 05 {6,7,9,10,13}; 06 {3}; 07 {16}; 08 {6,8,14,15}.
- Must-not-do list still respected: no relay feed, upload tool, cookie import, bundled Chromium, manifest rows, `ContractVersion` edit or registry row; `IBrowserPaneControl` is the minimum seam D-48 needs and is in-process only.
