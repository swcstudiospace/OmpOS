# G-WEB re-measurement, 2026-11-12, Avalonia 12.1.6

Phase 23 criterion 5 evidence for the Avalonia release 12.1.6. Verdict: **Fail**.

## Command

Run from the repository root, with the pinned SDK on PATH and `CI=true` set in the shell:

    export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH
    GWEB_AVALONIA_VERSION=12.1.6 \
    GWEB_PUBLISH_ARGS="-p:TargetLatestRuntimePatch=false -p:RuntimeFrameworkVersion=10.0.13" \
    GWEB_EVIDENCE_DIR=docs/runbooks/evidence/gweb-remeasure-2026-11-12-avalonia-12.1.6 \
    bash tools/gweb/measure.sh

`GWEB_AVALONIA_VERSION` publishes the stock and named heads from a scratch copy in `$WORK/src` (under `/tmp`). The copy's `Directory.Packages.props` replaces every Avalonia family pin at 12.1.4 with 12.1.6. The repository pin is not modified. The scratch copy's lock files are regenerated, and locked restore is disabled for that copy (`RestoreLockedMode=false`), because `CI=true` would otherwise reject the changed pin.

## Environment on this host

- Ubuntu 24.04.4 LTS, Linux 6.8.0-147-generic x86_64
- .NET SDK 10.0.402 with the wasm-tools workload
- Node v24.18.0
- Google Chrome 153.0.8010.36 at /usr/bin/google-chrome (headless)
- Python 3.12.3

## Result

Resolved from the scratch lock files (`resolved_*` lines in `measure-output.txt`): Avalonia 12.1.6, Avalonia.Browser 12.1.6, Avalonia.Themes.Fluent 12.1.6.

| Build | Publish files | wwwroot bytes | roleNodes | textboxNodes | namedTextboxNodes | namedButtonNodes | namedRegionNodes |
|-------|---------------|---------------|-----------|--------------|-------------------|------------------|------------------|
| stock (`Autonogrammer.Gweb.Stock`) | 142 | 29790927 | 4 | 0 | 0 | 0 | 0 |
| named (`Autonogrammer.Gweb.Named`) | 142 | 29791541 | 4 | 0 | 0 | 0 | 0 |

Accessibility roles in both builds: RootWebArea 1, generic 2, Canvas 1. The probe reports `canvasElements: 1` and `domElementsWithRoleAttribute: 0`.

## Gate and verdict

Gate (ADR-0004 decision 2; `docs/runbooks/evidence/avalonia-spike-2026-10-21/README.md`): the Chromium accessibility tree must contain a textbox node named for the prompt input, a button node named for the send control, and a named transcript region.

Measured: no textbox, no named button and no named region in either build. Verdict **Fail**. The roleNodes count (4) matches 12.1.4 (`gweb-remeasure-2026-10-24`) and 12.1.5 (`gweb-remeasure-2026-11-12-avalonia-12.1.5`).

## Files

- `measure-output.txt`: full script output, including the pin override line and the resolved versions.
- `stock-ax-full.json`, `named-ax-full.json`: accessibility trees.
- `stock-ax-summary.json`, `named-ax-summary.json`: summaries.
- `stock-screen.png`, `named-screen.png`: screenshots.

## Limits

- Headless Chromium only. Browser accessibility is not the native head's AT-SPI tree.
- The pin override covers the Avalonia family lines in the working-tree `Directory.Packages.props` at run start. Those were 7 lines at 12.1.4 plus `Avalonia.Wayland` 12.1.4 (`pins_rewritten=8`), which another change added to the working tree. The browser projects do not reference `Avalonia.Wayland`, so it does not reach the measured builds.
- The 2026-10-24 run did not count button or region nodes, so its entry has no namedButtonNodes or namedRegionNodes values. Its textbox count (0) is enough for its Fail.
- Not committed. The repository `Directory.Packages.props` pin is unchanged by this run.
