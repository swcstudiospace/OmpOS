# G-WEB re-measurement, 2026-10-24

Phase 23 criterion 4 evidence. The measurement runs from the repository, not from a separate spike project.

## Command

Run from the repository root, with the pinned SDK on PATH:

    GWEB_EVIDENCE_DIR=docs/runbooks/evidence/gweb-remeasure-2026-10-24 bash tools/gweb/measure.sh

The script publishes `tools/gweb/stock` and `tools/gweb/named` (Avalonia.Browser builds), serves each `wwwroot` on localhost, drives headless Chromium over CDP through `tools/gweb/cdp-ax.mjs`, and prints the accessibility `roleNodes` count per build.

## Environment on this host

- Ubuntu 24.04.4 LTS, Linux 6.8.0-147-generic x86_64
- .NET SDK 10.0.402 with the wasm-tools workload
- Node v24.18.0 (the script requires 22 or later)
- Google Chrome 153.0.8010.36 at /usr/bin/google-chrome (CHROME_BIN unset, so the script selected it)
- Python 3.12.3

## Result

| Build | Publish files | roleNodes | textboxNodes | namedTextboxNodes |
|-------|---------------|-----------|--------------|-------------------|
| stock (`OmpOS.Gweb.Stock`) | 142 | 4 | 0 | 0 |
| named (`OmpOS.Gweb.Named`) | 142 | 4 | 0 | 0 |

The run took 99.85 seconds. Both wwwroot folders are about 29.8 MB.

## Comparison

The 2026-10-22 run in `docs/runbooks/evidence/gweb-remeasure-2026-10-22/` reports the same counts for both builds: roleNodes 4, textboxNodes 0, namedTextboxNodes 0. The measurement reproduces on this host.

## Files

- `measure-output.txt`: the full script output.
- `stock-ax-full.json` and `named-ax-full.json`: the accessibility trees.
- `stock-ax-summary.json` and `named-ax-summary.json`: the summaries.
- `stock-screen.png` and `named-screen.png`: screenshots.

## Limits

- Headless Chromium only. Browser accessibility is not the native head's AT-SPI tree.
- Criterion 5 requires one dated entry per Avalonia release. This entry covers the Avalonia version pinned in `tools/gweb/stock`.
