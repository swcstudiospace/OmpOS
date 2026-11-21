# Plan 72-09 SUMMARY: Live Chromium drive for site, corpus, headful and ladder

Status: blocked — not executed. The plan requires driving the real Harness.BrowserUse library (`BrowserSessionHost`, provider through `CoworkToolHost`) in five live test classes with the three environment gates (72-09-PLAN.md:66,78,90). That work was never done: the live test bodies it was meant to replace do not exist in the tree, so no task was run and no product code was changed. This is the same gap 72-VERIFICATION.md records as gap 1 / criterion 2-3 failure.

## What was checked (tree at c04095ba, 2026-11-21)

- `tests/Autonogrammer.Harness.BrowserUse.Tests/Live/` contains only `HeadfulSmokeLiveTests.cs` and `PipeConnectLiveTests.cs` (the 72-01 spike + 72-07 headful smoke). The three files the plan's Tasks 1-2 name as edit targets — `Site/SiteLiveTests.cs`, `Corpus/HostileCorpusLiveTests.cs`, `Live/ScreenshotLadderLiveTests.cs` — do not exist. Only the offline `Site/SiteTests.cs`, `Corpus/CorpusPagesInventoryTests.cs` and `Vision/ScreenshotLadderTests.cs` exist. There is no second unconditional `Assert.Skip` to remove because the live bodies were never written; the vacuous placeholders 72-VERIFICATION.md:44-49 describes were removed without replacement (72-VERIFICATION.md:40).
- `HeadfulSmokeLiveTests.cs` retains both env-gate skips ("Set AUTONOGRAMMER_LIVE_BROWSER=1…", "Set CHROME_BIN…") and no drive body beyond the 72-07 launch/frame assertion; it has never been run green under the ROADMAP `xvfb-run` command (72-VERIFICATION.md:33).
- No SOTA state matrix (`BrowserPaneState.*`, `CoworkProviderState.*`) exists in live assertions, and no full offline run was reported by this plan. Task 3's "full offline green for first time" claim is unmade.

## Blockers

- The live test bodies themselves (this plan's deliverable) — never implemented. The library path they would drive (`BrowserSessionHost`, `ChromiumProcessLauncher`, provider) exists from 72-01..72-03, but no test exercises it against real Chromium in these classes.
- Live execution requires `AUTONOGRAMMER_LIVE_BROWSER=1` + `CHROME_BIN=/usr/bin/google-chrome` + `AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1` on a host with a real Chrome; headful additionally requires a private `xvfb-run`. Neither was run for this plan.

## Not run

- The four plan `<verify>` commands (SiteLive, HostileCorpusLive, HeadfulSmokeLive under xvfb, ScreenshotLadderLive, plus full offline) — not run; the classes do not exist. The Phase 72 VERIFICATION re-run covers what the current live classes do.
