# 72-07 Summary

## Outcome

Offline site lifecycle and corpus inventory are in place. Live Chromium proofs were not executed.

## What landed

- Test project copies `Site/pages/*.html` and `Corpus/pages/*.html` to the output directory.
- Granted and ungranted site pages, plus `SiteTests` against `LocalTestSite` request logs.
- Nineteen hostile corpus pages and `CorpusPages` / `CorpusPagesInventoryTests`.
- Live classes exist and skip unless `AUTONOGRAMMER_LIVE_BROWSER=1`: `SiteLiveTests`, `HostileCorpusLiveTests`, `HeadfulSmokeLiveTests`, `ScreenshotLadderLiveTests`.

## Tests

- Targeted command: `/tmp/ag-bin/ag-dotnet test tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter-not-trait "Category=Live" -- --filter-class "*SiteTests*" --filter-class "*CorpusPagesInventoryTests*"`.
- Result: not green. The command reached the compiler and failed in sibling projects this plan does not own (`Autonogrammer.Cowork` `RunModeStage` members, `Autonogrammer.Contracts` image types). No `SiteTests` or `CorpusPagesInventoryTests` result was produced.
- Skipped, not launched: every `Category=Live` class in this plan. This execution must not start Chromium or Xvfb.

## Gaps

- Live bodies do not drive Chromium even when the env gate is set. A second `Assert.Skip` records that the browser drive was withheld. They do not yet assert `BlockedByPolicy`, marker unforgeability, hidden-text drop, `IsUntrusted`, caps, or taint on a real browser.
- `LocalTestSite` still answers `ok` for every non-redirect path. Offline tests prove both ports log requests and that the HTML fixtures ship beside the assembly. They do not serve the fixture HTML through the listener.
- Headful smoke and the screenshot ladder were not run under `xvfb-run`.
- Offline tests did not run to completion because sibling projects failed to compile.
