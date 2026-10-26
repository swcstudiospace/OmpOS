# G-WEB re-measurement, 2026-10-22

Re-measures the Chromium accessibility tree of Avalonia.Browser 12.1.4 with and without a named TextBox. It repeats steps 3 and 5 of `../avalonia-spike-2026-10-21/README.md` ("Re-measuring G-WEB"). The spike folder was read only and is unchanged. The measurement tooling is in `tools/gweb/`.

Run: 2026-10-22, 08:19:55Z to 08:45:07Z, `tools/gweb/measure.sh`, exit 0.

## Result as observed

| Measurement | Stock head (no names) | Named head (`AutomationProperties.Name="Greeting"`) |
|---|---|---|
| `dotnet publish -c Release` | Pass, 0 warnings | Pass, 0 warnings |
| Page probe (`Runtime.evaluate`) | title `OmpOS.Gweb.Stock`, 1 canvas, 0 `[role]` DOM elements, `readyState` complete | title `OmpOS.Gweb.Named`, 1 canvas, 0 `[role]` DOM elements, `readyState` complete |
| AX nodes total (`Accessibility.getFullAXTree`) | 7 | 7 |
| **roleNodes** | **4** | **4** |
| textboxNodes (role `textbox`, not ignored) | 0 | 0 |
| namedTextboxNodes | 0 | 0 |
| roleCounts | RootWebArea 1, generic 2, Canvas 1 | RootWebArea 1, generic 2, Canvas 1 |
| Named AX nodes (not ignored) | RootWebArea `OmpOS.Gweb.Stock`; generic; Canvas; generic | RootWebArea `OmpOS.Gweb.Named`; generic; Canvas; generic |
| Files under publish output dir | 142 | 142 |
| Bytes under `wwwroot` | 29,769,183 | 29,769,269 |

Definitions used by `cdp-ax.mjs`:
- **roleNodes**: AX nodes with `ignored` false and a non-empty `role.value`.
- **textboxNodes**: non-ignored nodes with role `textbox`. **namedTextboxNodes**: those with a non-empty `name`.

Checks on the raw tree (`*-ax-full.json`): neither build contains the string `textbox` or `Greeting` anywhere in the full tree, including ignored nodes. Each has 3 ignored nodes and 4 non-ignored nodes.

Screenshot `named-screen.png` shows one TextBox rendered at the centre of the canvas, so the control renders but is absent from the accessibility tree. The stock screenshot was saved but not viewed.

**Conclusion as observed:** the named-control change does not reach the Chromium accessibility tree. The textbox role and name are both missing in both builds. The G-WEB textbox criterion is not met. The spike's pass rule also requires a named button and a named transcript region. This head has neither control, so those criteria were not measured. This matches the 2026-10-21 spike: roleNodes 4, textbox 0, DOM role attributes 0, canvas 1 in both builds.

## Host

- OS: Ubuntu 24.04.4 LTS, kernel `6.8.0-147-generic`, `x86_64`. The spike recorded the same OS and kernel.
- .NET SDK 10.0.402. Workload `wasm-tools` (manifest 10.0.113/10.0.100, installed from SDK 10.0.400). Emscripten pack `Microsoft.NET.Runtime.Emscripten.3.1.56.Sdk` 10.0.13.
- Avalonia.Browser 12.1.4, Avalonia 12.1.4, Avalonia.Themes.Fluent 12.1.4.
- Chrome: `/usr/bin/google-chrome`, Google Chrome 153.0.8010.36, headless (`--headless=new`). The spike used Chromium 156.
- node v24.18.0 (global `WebSocket`, `fetch`), python 3.12.3 (`http.server`), bash 5.2.21, curl 8.5.0.

### Host checks (read only, before the run)

```
$ dotnet workload list
Installed Workload Id      Manifest Version       Installation Source
wasm-tools                 10.0.113/10.0.100      SDK 10.0.400

$ command -v chromium chromium-browser google-chrome chrome
/usr/bin/google-chrome

$ ls ~/.cache/ms-playwright
755  .links/
755  chromium-1248/
755  chromium_headless_shell-1248/
755  ffmpeg-1013/
755  firefox-1555/
```

Both blockers were absent, so the measurement ran. The Playwright Chromium was not used. The run used `/usr/bin/google-chrome`, the only Chromium-family binary on `PATH`.

## Commands

```
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
cd tools/gweb/named && dotnet restore OmpOS.Gweb.Named.csproj --force-evaluate
cd tools/gweb/stock && dotnet restore OmpOS.Gweb.Stock.csproj --force-evaluate
bash tools/gweb/measure.sh
```

`measure.sh` runs, for each head (`stock`, then `named`):

```
dotnet publish -c Release -o <work>/<name>                # in tools/gweb/<name>
python3 -m http.server <port> --bind 127.0.0.1 --directory <work>/<name>/wwwroot
node tools/gweb/cdp-ax.mjs 9444 http://127.0.0.1:<port>/ docs/runbooks/evidence/gweb-remeasure-2026-10-22/<name> 25000
```

Chrome was started once with:

```
/usr/bin/google-chrome --headless=new --no-sandbox --disable-gpu --remote-debugging-port=9444 \
  --user-data-dir=<work>/chrome-profile --window-size=1024,768 about:blank
```

`<work>` was `/tmp/gweb-remeasure.Z2VaLb` for this run. The stock and named ports were 5411 and 5412. The wait before sampling was 25000 ms.

## Procedure notes

1. The first restore failed with NU1010 (`Avalonia.Browser` had no central version). A single line was added to `Directory.Packages.props`, in the "Avalonia native head" group: `<PackageVersion Include="Avalonia.Browser" Version="12.1.4" />`. This is the only central pin in this change.
2. Under `CI=true` the repo uses locked restore. The lock file was stale after the failed restore (NU1004), so both heads were restored with `--force-evaluate`. The resulting `packages.lock.json` files are in `tools/gweb/stock` and `tools/gweb/named`.
3. The first publish of `stock` failed with CA1852 (classes must be sealed). The four internal classes were sealed in both heads, and the publish was rerun.
4. A publish run was killed by the tool's 300 s command timeout during native linking (`emcc`/`wasm-opt`). The compile had already succeeded. The `measure.sh` run was made with no deadline. Native linking took most of the 25 minutes.
5. `measure.sh` has no `wait` in its cleanup trap, so the `tee` process substitution cannot block exit.

## Evidence files

- `measure-output.txt`: complete raw output of `measure.sh`, including the publish and Chrome versions.
- `stock-ax-full.json`, `named-ax-full.json`: raw `Accessibility.getFullAXTree` results.
- `stock-ax-summary.json`, `named-ax-summary.json`: non-ignored nodes with a role or name.
- `stock-screen.png`, `named-screen.png`: `Page.captureScreenshot` results.

## Limitations

- Only the TextBox was measured. The spike's G-WEB rule also needs a button and a transcript region; neither is in these heads.
- The named label is `Greeting`, the same as the spike. The spike's rule asks for a prompt input. This head is a generic TextBox, so its name does not map to a prompt input.
- Chrome 153 was used, not the spike's Chromium 156.
- Publish size is measured differently from the spike. The spike's 33,781,416 bytes counts the full publish with `.gz`/`.br` copies over 153 files. The figures above cover the publish output and `wwwroot` only. They are not comparable to the spike's number, and this run did not measure compressed copies.
- Linux, Ubuntu 24.04 (Tier 2 for Avalonia), only. No other platform was measured.
