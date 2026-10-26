# ADR-0009: Embedded browser pane for ADE-15

- Status: Accepted for Linux only, on the WebKitGTK adapter. Windows: not measured. macOS: not measured.
- Date: 2026-10-22
- Deciders: ompos-maintainers
- Scope: the embedded browser pane for ADE-15 (`docs/plans/ade-harness-plan.md`, risk R-3). Covers the control choice, the Linux engine, and the spike that measured it in `tools/browser-spike`. The spike is not part of `OmpOS.slnx`, `OmpOS.Linux.slnf` or any gate, and it adds no product code. Does not cover navigation policy, cookies, the pane's place in the shell, or the Windows and macOS heads.

## Context

R-3 needs a browser pane on the native heads. `docs/open-questions.md` OQ-51 records the candidate as `Avalonia.Controls.WebView` 12.1.1 (MIT), with per-platform behaviour unverified. This ADR records a measurement of that candidate on Linux.

The package is MIT (`Avalonia.Controls.WebView.nuspec`, licence expression `MIT`). It ships a WebKitGTK adapter, a WPE adapter (`WpeWebViewAdapter`, which loads `WPEWebKit-2.0.so.1` and `WPEBackend-fdo-1.0.so.1`), and WebView2, WKWebView and Android adapters. The adapter list reported by the binary is `Unknown, WebView2, WebView1, WkWebView, WebKitGtk, AndroidWebView, BrowserIFrame, WpeWebKit, Headless`.

The spike asked three things: whether a page loads in the control, whether a selector resolves to a button, and whether its bounding box is reported. The fixture is a local page with `<button id="go">`, positioned at `left:40px; top:60px; width:120px; height:32px`. The probe script returns `document.querySelector('#go') === el` and `getBoundingClientRect()`.

## Decision

1. For Linux, the browser pane uses `Avalonia.Controls.WebView` 12.1.1. On this host the control selects the WebKitGTK adapter (`Type = WebKitGtk, Engine = WebKit, Version = 2.52.6`, `IsInstalled = True`).
2. The WPE engine named in the host check is not used. `WPEWebKit` is not installed, and the WPE adapter reports `IsSupported = False` on this platform (measured below). This ADR does not depend on WPE.
3. Windows and macOS are not decided here. The package has WebView2 and WKWebView adapters, but neither was run.

## Host state (read-only, measured 2026-10-22)

```
$ ldconfig -p | grep -Ei 'WPEWebKit|libwpe' ; echo "exit=$?"
exit=1
$ for c in WPEWebKitLauncher WPEWebKit-2.0 cog MiniBrowser WPEWebDriver xvfb-run dotnet; do printf '%s: ' "$c"; command -v "$c" || echo "(not found)"; done
WPEWebKitLauncher: (not found)
WPEWebKit-2.0: (not found)
cog: (not found)
MiniBrowser: (not found)
WPEWebDriver: (not found)
xvfb-run: /usr/bin/xvfb-run
dotnet: /usr/bin/dotnet
$ echo "DISPLAY=[${DISPLAY-}]"; echo "WAYLAND_DISPLAY=[${WAYLAND_DISPLAY-}]"
DISPLAY=[]
WAYLAND_DISPLAY=[]
$ ls /usr/lib/x86_64-linux-gnu/ | grep -Ei 'wpe'; echo "exit=$?"
exit=1
$ ldconfig -p | grep -Ei 'webkit2gtk|webkitgtk|javascriptcore'; echo "exit=$?"
	libwebkit2gtk-4.1.so.0 (libc6,x86-64) => /lib/x86_64-linux-gnu/libwebkit2gtk-4.1.so.0
	libwebkit2gtk-4.1.so (libc6,x86-64) => /lib/x86_64-linux-gnu/libwebkit2gtk-4.1.so
	libjavascriptcoregtk-4.1.so.0 (libc6,x86-64) => /lib/x86_64-linux-gnu/libjavascriptcoregtk-4.1.so.0
	libjavascriptcoregtk-4.1.so (libc6,x86-64) => /lib/x86_64-linux-gnu/libjavascriptcoregtk-4.1.so
exit=0
```

No WPE library, launcher or WebKit process binary is present. WebKitGTK 4.1 is present. Both display variables are unset, so the run uses `xvfb-run`.

## Build and restore

The shell's `PATH` cache held `/usr/bin/dotnet` (SDK 8.0.132). `hash -r` after the `PATH` export made `$HOME/.dotnet/dotnet` (SDK 10.0.402, pinned by `global.json`) resolve.

```
$ export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1; hash -r
$ cd tools/browser-spike
$ dotnet --version
10.0.402
$ dotnet restore OmpOS.BrowserSpike.csproj --force-evaluate
  Determining projects to restore...
  Restored /root/src/repos/ompos/tools/browser-spike/OmpOS.BrowserSpike.csproj (in 34.79 sec).
exit=0
$ dotnet build OmpOS.BrowserSpike.csproj -c Release --no-restore -warnaserror
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:44.78
exit=0
```

Earlier build attempts failed on analyzer findings (MA0048, MA0006, IDE0005, VSTHRD100, CA2007 and MA0004). Each was fixed in source. No suppression was added. Resolved versions are in `tools/browser-spike/packages.lock.json`: `Avalonia.Controls.WebView` 12.1.1 requests `Avalonia` 12.0.0, which central transitive pinning resolves to 12.1.4.

## Runs (final binary, `xvfb-run` with `timeout 120`)

Adapter list:

```
$ dotnet bin/Release/net10.0/OmpOS.BrowserSpike.dll --list-adapters
Unknown
WebView2
WebView1
WkWebView
WebKitGtk
AndroidWebView
BrowserIFrame
WpeWebKit
Headless
exit=0
```

Run A, default adapter selection:

```
$ timeout 120 xvfb-run -a dotnet bin/Release/net10.0/OmpOS.BrowserSpike.dll
libEGL warning: DRI3 error: Could not get DRI3 device
libEGL warning: Ensure your X server supports DRI3 to get accelerated rendering
SPIKE PASS load+selector+box: adapter=DetailedWebViewAdapterInfo { Type = WebKitGtk, Engine = WebKit, Version = 2.52.6, IsSupported = True, IsInstalled = True, UnavailableReason = , SupportedScenarios = NativeDialog } selector=#go resolves=True box=(40,60,120,32)
exit=0
```

Run B, forced WPE adapter:

```
$ OMPOS_SPIKE_ADAPTER=WpeWebKit timeout 120 xvfb-run -a dotnet bin/Release/net10.0/OmpOS.BrowserSpike.dll
SPIKE FAIL deadline: no result within 60 s; adapter=DetailedWebViewAdapterInfo { Type = Unknown, Engine = Unknown, Version = , IsSupported = False, IsInstalled = False, UnavailableReason = The adapter is not supported on the current platform., SupportedScenarios = None }
exit=1
```

Run C, forced WebKitGTK adapter:

```
$ OMPOS_SPIKE_ADAPTER=WebKitGtk timeout 120 xvfb-run -a dotnet bin/Release/net10.0/OmpOS.BrowserSpike.dll
libEGL warning: DRI3 error: Could not get DRI3 device
libEGL warning: Ensure your X server supports DRI3 to get accelerated rendering
SPIKE PASS load+selector+box: adapter=DetailedWebViewAdapterInfo { Type = WebKitGtk, Engine = WebKit, Version = 2.52.6, IsSupported = True, IsInstalled = True, UnavailableReason = , SupportedScenarios = NativeDialog } selector=#go resolves=True box=(40,60,120,32)
exit=0
```

No display, no `xvfb-run`:

```
$ env -u DISPLAY -u WAYLAND_DISPLAY dotnet bin/Release/net10.0/OmpOS.BrowserSpike.dll
Unhandled exception. System.Exception: XOpenDisplay failed
   at Avalonia.X11.AvaloniaX11Platform.Initialize(X11PlatformOptions options)
   at Avalonia.AvaloniaX11PlatformExtensions.<>c.<UseX11>b__0_0()
   at Avalonia.AppBuilder.SetupUnsafe()
exit=134
```

What the runs show:

- Load, selector and bounding box all pass on the WebKitGTK adapter, in both the default run and the forced run. The box matches the fixture CSS exactly: `(40,60,120,32)`.
- The forced WPE run never completes navigation. Its adapter reports `IsSupported = False` and `UnavailableReason = The adapter is not supported on the current platform.`. Its stderr is empty, so no other exception text exists.
- The `libEGL` lines come from the display stack under Xvfb (no DRI3 device). They are printed with the verdict and are not treated as a failure.

## Status rule applied

The rule was: Accepted for Linux only if load, selector and bounding box all work in the run. Run A and Run C passed all three. Status is therefore Accepted for Linux, scoped to the WebKitGTK adapter on this host. WPE is not part of the acceptance.

## Not measured

- Windows (WebView2) and macOS (WKWebView): not measured. No run was made on either.
- Wayland: not measured. Runs used Xvfb (X11). `WAYLAND_DISPLAY` was unset.
- Interaction: no clicks, keyboard focus, text input, accessibility tree, navigation policy, cookies or printing were measured.
- Repeatability beyond the runs listed above, real sites, and GPU-accelerated rendering were not measured.
- WebKitGTK licence: the package does not ship WebKitGTK, which is an operating-system library. Its licence was not checked in this run.

## Licences

| Package | Version | Licence | Source |
| --- | --- | --- | --- |
| Avalonia.Controls.WebView | 12.1.1 | MIT (nuspec expression; https://licenses.nuget.org/MIT) | https://github.com/AvaloniaUI/Avalonia.Controls.WebView |

## Consequences

- `Directory.Packages.props` gains one labelled group, "Embedded browser pane spike (ADE-15, OQ-51, ADR-0009)", with the single pin `Avalonia.Controls.WebView` 12.1.1.
- A Linux runner or desktop install needs `libwebkit2gtk-4.1` (WebKitGTK 4.1) installed. The WPE adapter is not a dependency.
- The spike is not referenced by any solution, filter or gate. Product integration of the pane is a separate ADE-15 change, which this ADR does not authorise.
- OQ-51 in `docs/open-questions.md` stays open for Windows and macOS. The orchestrator owns that file and should update the OQ-51 row.
