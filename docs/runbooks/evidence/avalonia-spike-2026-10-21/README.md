# Avalonia spike, 2026-10-21

Evidence for ADR-0004 (`docs/decisions/ADR-0004-cross-platform-ui.md`). The spike projects were throwaway and outside the repository. Nothing from the spike is product code. The scripts in `spike-tools/` reproduce the measurements.

## Environment

- Host: Ubuntu 24.04.4 LTS, kernel 6.8.0-147-generic, x64. Ubuntu 24.x is an Avalonia Tier 2 Linux distribution.
- .NET SDK 10.0.402. Workload `wasm-tools` at 10.0.13 packs.
- Avalonia 12.1.4 from `Avalonia.Templates` 12.1.4, template `avalonia.xplat`.
- Chromium 156, headless, started with remote debugging on port 9333.
- Xvfb, dbus-run-session, at-spi2-core 2.52.0, python3-pyatspi 2.46.1 with Atspi GI 2.52.
- Bun 1.x for the CDP script.

## Procedure

1. `dotnet new install Avalonia.Templates`, then `dotnet new avalonia.xplat -n OmpOSSpike -o <dir>`.
2. Desktop head: `dotnet build OmpOSSpike.Desktop/OmpOSSpike.Desktop.csproj -c Release -warnaserror`. Result: 2 projects, 0 errors, 0 warnings.
3. Browser head: `dotnet workload install wasm-tools`, then `dotnet publish OmpOSSpike.Browser/OmpOSSpike.Browser.csproj -c Release -warnaserror -o <dir>`. The publish fails under warnings-as-errors without the workload, because native file references are not linked and trim analysis IL2026 is raised for the reflection-based default `ViewLocator`.
4. Linux accessibility: `spike-tools/run-atspi.sh` starts Xvfb on display :99, a session bus with the AT-SPI bus launcher, and the desktop head. It then dumps the accessibility tree with `spike-tools/atspi_dump.py`.
5. Browser accessibility: serve the publish `wwwroot` (for example `python3 -m http.server 5311 --bind 127.0.0.1`), then run `bun spike-tools/cdp-ax.ts http://127.0.0.1:5311/ <outPrefix> 25000`. The script opens the page in the Chromium on port 9333, waits for the WebAssembly runtime, and records the full accessibility tree, a summary of named and visible nodes, and a screenshot.
6. Named-control variant: `MainView.axaml` adds `AutomationProperties.Name="Greeting"` to the TextBox. Steps 2 to 5 are repeated.

## Results

| Measurement | Stock template | With `AutomationProperties.Name` |
|---|---|---|
| Desktop build, warnings as errors | Pass, 0 warnings | Pass, 0 warnings |
| AT-SPI2 name of the text box (desktop) | `TextBox` (default) | `Greeting` |
| Browser publish, warnings as errors | Pass with `wasm-tools` | Pass with `wasm-tools` |
| Chromium accessibility tree: textbox role and name | None. Nodes are RootWebArea `OmpOSSpike.Browser`, generic, Canvas, generic | None. Same four nodes |
| DOM elements with a `role` attribute | 0 | 0 |
| Canvas elements | 1 | 1 |

Browser payload, stock publish (`publish-size.txt`): 33,781,416 bytes on disk across 153 files, including precompressed `.gz` and `.br` copies. The largest runtime file is `dotnet.native.wasm`, at 8,894,847 bytes raw and 3,240,973 bytes gzip-9.

Avalonia's browser glue (`_framework/avalonia.js`) has one match for the substring `aria`. It sits inside the CSS property name `fontVariant`. The file contains no ARIA attributes and no role assignments.

## Conclusions

- Gate G-WEB failed on Avalonia 12.1.4. Neither build exposes a textbox role or a name to the Chromium accessibility tree, and the page has no DOM role elements at all. The web head therefore stays Blazor WebAssembly (ADR-0004, decision 2).
- On Linux, AT-SPI2 exposes Avalonia controls under the names they are given. The default name is the control type name, so every control needs an explicit `AutomationProperties.Name`.
- A browser publish with warnings as errors needs the `wasm-tools` workload. The reflection-based default `ViewLocator` produces trim analysis warnings, so product heads should use explicit view mapping.

## Limitations

- Only one control, a TextBox, was measured in each head. Buttons, lists and the transcript were not measured.
- The Linux host is Ubuntu 24.04, which is Tier 2 for Avalonia. Tier 1 verification (Ubuntu 25.x, Fedora 43, Debian 13) is pending in CI.
- Windows, macOS, iOS and Android were not measured. This host has no Android SDK, no Windows host and no macOS host.
- The G-WEB result was measured in Chromium 156 with CDP accessibility enabled. It must be re-measured on each Avalonia release.

## Re-measuring G-WEB

Repeat steps 3 and 5 with the new Avalonia version. G-WEB passes only when the accessibility tree contains a textbox node named for the prompt input, a button node named for the send control, and a named transcript region. Only then is Avalonia.Browser considered for the web head (ADR-0004, decision 2).

## Files

- `desktop-atspi-baseline.txt`, `desktop-atspi-named.txt`: AT-SPI2 trees for the stock and named builds.
- `browser-baseline-ax-full.json`, `browser-baseline-ax-summary.json`, `browser-baseline-screen.png`: Chromium accessibility tree, summary and screenshot for the stock browser build.
- `browser-named-ax-full.json`, `browser-named-ax-summary.json`, `browser-named-screen.png`: the same for the named build.
- `publish-size.txt`: payload measurement for the stock browser publish.
- `spike-tools/`: `cdp-ax.ts`, `atspi_dump.py`, `run-atspi.sh`.
