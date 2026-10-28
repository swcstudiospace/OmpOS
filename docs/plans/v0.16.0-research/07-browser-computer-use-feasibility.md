# R7: Browser Use and Computer Use feasibility (Autonogrammer Cowork)

Research date: 2026-10-28. Worktree: `/root/src/repos/ompos-autonogrammer` (branch `feat/autonogrammer`). Nothing was built, run, started or committed. All "verified" claims are file reads, NuGet/GitHub/vendor-page reads, or `strings`/`grep` on installed binaries. `[INFERENCE]` marks anything not read.

## 0. Verdict

| Question | Answer |
| --- | --- |
| Can `Avalonia.Controls.WebView` be the agent-driven browser? | No, as the agent surface. It is a good user-facing pane (ADR-0009), but it exposes JS eval and messages only: no screenshot, no CDP, no trusted input (section 2.1). |
| Primary browser option | **PuppeteerSharp 25.15.0 (MIT) driving a host-launched, isolated-profile Chromium over CDP**, with `Page.startScreencast` frames rendered in the app and CDP `Input.*` for user take-over (section 2.4). |
| Fallback browser option | **Microsoft.Playwright 1.64.1 (MIT .NET wrapper, Apache-2.0 driver)** over the same CDP endpoint. Same architecture, richer API, +about 130 MB Node driver per OS (section 2.3). A second, degraded fallback is NativeWebView + `InvokeScript` (read-only DOM, no vision). |
| Computer Use | **Do not write per-OS capture/input in C# first.** omp 18.12.5 already ships a native desktop session (X11 XTEST, Wayland portal + libei + AT-SPI, Win32, macOS quartz) behind `computer.enabled`. Expose it through the host gateway only if the product needs host-owned approval and Dispatch routing; otherwise enable it by config (section 3). |
| Tool route to omp | ACP `session/new.mcpServers` pointing at the host gateway is the only documented route (ADR-0010). The gateway code exists but is **not wired into any app**, serves tools only from upstream `McpClient`s, and passes image content blocks through untouched (section 4). |
| Biggest repo gap | Tool-result images are dropped by the UI contract: `ToolUpdateContent.Read` reads text and diffs only (section 4.3). Screenshots cannot appear in the transcript until that changes. |
| Biggest unverified item | The route `session/new.mcpServers` to gateway to model turn is "feasible, unverified" because it needs a live omp turn and a recorded owner approval (OD-07, ADR-0010 lines 244-257). |

## 1. Repo evidence (facts)

| Topic | Fact | Source |
| --- | --- | --- |
| Avalonia version | Avalonia, Desktop, Android, Themes.Fluent, Browser, Headless 12.1.4; Skia 12.1.4 | `Directory.Packages.props:20-26`, `:80` |
| WebView pin | `Avalonia.Controls.WebView` 12.1.1, group "Embedded browser pane spike (ADE-15, OQ-51, ADR-0009)" | `Directory.Packages.props:93-95` |
| MCP SDK pin | `ModelContextProtocol` 2.3.0 | `Directory.Packages.props:73-75` |
| Spike | Not in `OmpOS.slnx`, not a gate; uses `NativeWebView`, `AdapterPreference`, `InvokeScript` probe; passes on WebKitGTK 2.52.6 under `xvfb-run`, fails to start with no `DISPLAY` | `tools/browser-spike/OmpOS.BrowserSpike.csproj:3`, `SpikeWindow.cs:11-30,38-47`; `docs/decisions/ADR-0009-browser-pane.md:92-129` |
| ADR-0009 status | "Accepted for Linux only, on the WebKitGTK adapter. Windows: not measured. macOS: not measured." WPE adapter reports `IsSupported = False` here | `ADR-0009-browser-pane.md:3,18-20,102-108` |
| Not measured | Windows, macOS, Wayland, clicks, keyboard focus, text input, a11y tree, cookies, real sites, GPU | `ADR-0009-browser-pane.md:141-147` |
| Required OS lib | `libwebkit2gtk-4.1` on Linux | `ADR-0009-browser-pane.md:158` |
| OQ-51 | Open: "No verified embedded web view covers all six heads, and the Blazor web head cannot host a second browser in the page" | `docs/open-questions.md:88`; `docs/plans/ade-harness-plan.md:105` (R-3) |
| Phase 29 | Browser pane NOT STARTED; verification route planned: Linux gate "headless Chromium and fixture pages"; no Mac channel | `.planning/phases/29-browser-completion-export/29-SUMMARY.md:5-9`; `docs/quality/cursor-parity-audit-2026-10-28.md:110,141` (no WebView/browser code in `src/`, `apps/`, `services/`) |
| Heads | Avalonia native heads: Windows, macOS, Linux, iOS, Android. Web head stays Blazor WASM (Avalonia.Browser failed G-WEB) | `docs/decisions/ADR-0004-cross-platform-ui.md:34-35` |
| Native-control hosting | No `NativeControlHost`/`NativeWebView` use in `src/` or `apps/` (grep). The native head is `OmpOS.Native.Views.MainView` (`MainView.Panes.cs`), panes are Avalonia views | grep over `src`, `apps`; `src/OmpOS.Native.Views/MainView.Panes.cs:19-31` |
| Linux display | Wayland: XP-02 "BLOCKED (NOT RUN)"; only X11/Xvfb verified | `docs/runbooks/phase-20-avalonia-foundation.md:45` |
| Cloud desktop research | Railway docs describe no desktop/VNC/browser-streaming; Sandbox-per-session is the best primitive; Trust and Safety "remote desktop" wording is a conditional blocker (S1); sandbox egress has no documented allow-list (S6) | `docs/plans/vision-research/railway-cloud-desktop.md:5-8,72-92` |
| Licence policy | Repo is AGPL-3.0-or-later; every shipped dependency licence is MIT, BSD, Apache-2.0, PostgreSQL, OFL (all GPLv3-compatible) | `docs/decisions/ADR-0018-licence-agpl-and-source-offer.md:15`; `docs/plans/v0.15.0-research/11-agpl-audit.md:15` |

## 2. Browser options

### 2.1 Avalonia WebView (`Avalonia.Controls.WebView` 12.1.1)

- Licence: MIT. Nuspec `<license type="expression">MIT`; repo LICENSE read from `raw.githubusercontent.com/AvaloniaUI/Avalonia.Controls.WebView/main/LICENSE` ("MIT License, Copyright (c) 2026 AvaloniaUI OÜ"). Was a paid Accelerate component; made MIT with 12.0.0 (Avalonia blog, 2026-03-11, via web search; release notes of the repo).
- Package: 1.49 MB nupkg, 7.2 MB unpacked in the NuGet cache. No engine is bundled (`README.md` in the package: "Unlike embedded WebView solutions that require bundling Chromium"). Targets net10.0, net8.0, net10.0-android36.0, net10.0-browser1.0 (nuspec).
- What it wraps: WebView2 (Windows), WKWebView (macOS/iOS), WebKitGTK (Linux baseline), WPE WebKit (Linux offscreen, opt-in if installed), Android WebView, browser iframe (`ADR-0009:12`; docs `webview-environment`). Adapter enum from the binary: `Unknown, WebView2, WebView1, WkWebView, WebKitGtk, AndroidWebView, BrowserIFrame, WpeWebKit, Headless` (`ADR-0009:76-90`).
- Automation surface (docs `controls/web/nativewebview`): `Source`, `Navigate`, `NavigateToString`, `InvokeScript(string) : Task<string?>`, `WebMessageReceived` (page-to-host via `invokeCSharpAction`), `WebResourceRequested` (request headers), `NavigationStarted/Completed`, `NewWindowRequested`, cookie manager, `PrintToPdfStreamAsync`, `EnvironmentRequested` (private mode, data dirs, WebView2 profile and browser args). A string search of `Avalonia.Controls.WebView.dll` (net10.0) for `CaptureScreenshot` and `Screenshot` returned 0 hits, and no method in the docs captures pixels. There is no CDP, no raw DevTools protocol handle, and no trusted-input API.
- Consequences for an agent: DOM read works through `InvokeScript` (spike proved selector + `getBoundingClientRect`, `ADR-0009:98`). Input would be synthetic DOM events (`isTrusted=false`, rejected by many sites). Screenshots would have to be taken by OS-level window capture, which the app does not have, or by in-page JS rasterisation (fragile). No vision loop is possible without extra machinery.
- Hosting: on X11 the control reparents a native child window; `GtkWebViewEnvironmentRequestedEventArgs.ExperimentalOffscreen` renders offscreen to avoid overlapping other Avalonia controls; `ForceX11GdkBackend` is on by default so Wayland sessions run through XWayland (docs `webview-environment`). Airspace overlap (native window above Avalonia content) is expected on Windows/macOS [INFERENCE: standard native-child-window behaviour; not measured in this repo].
- Platform status: Linux measured (WebKitGTK/Xvfb only); Windows and macOS not measured; Wayland not measured; mobile: Android adapter exists, not measured; web head cannot host it (R-3).
- Headless test on Linux: yes under `xvfb-run` (done, `ADR-0009:95-100`). Without a display it dies in `XOpenDisplay` (`:123-128`).
- Risks: depends on the OS engine version (WebKitGTK 2.52.6 here); iOS/Android/web are not agent-capable; no profile isolation on WKWebView beyond `NonPersistentDataStore`.
- Role: keep for the **user-facing** browser/preview pane and Design Mode (ADE-15) on Windows/macOS/Linux. Not the agent runtime.

### 2.2 CEF wrappers (CefGlue)

- `CefGlue.Avalonia` 120.6099.215: nuspec licence MIT; repo `OutSystems/CefGlue` LICENSE MIT (read); repo pushed 2026-09-25; the GitHub releases API returned no releases. Package is 55 KB; the CEF binaries come from dependencies of `CefGlue.Common` 120.6099.215: `cef.redist.linux64`, `cef.redist.osx64`, `chromiumembeddedframework.runtime`, `chromiumembeddedframework.runtime.win-x64`, all 120.1.8 (nuspec). `CefGlue.Avalonia` depends on `Avalonia.ReactiveUI` 11.0.9 (nuspec), so it targets Avalonia 11; compatibility with Avalonia 12.1.4 is unproven [INFERENCE].
- Chromium 120 is about 33 majors behind the 153 on this host (`google-chrome --version`: 153.0.8010.36). A browser that visits attacker-controlled pages on a stale Chromium is a security liability [INFERENCE from version gap]. The dependency list has no osx-arm64, win-arm64 or linux-arm64 redist.
- Automation: CEF exposes DevTools protocol through the host API, but there is no screenshot/automation library on top; you would write it yourself. Binary size: CEF runtime is a full Chromium (hundreds of MB per OS) [INFERENCE: not measured].
- Headless Linux: possible under Xvfb [INFERENCE]. Verdict: reject. Stale, Avalonia-11-bound, no arm64, nothing gained over driving system Chromium.

### 2.3 Microsoft.Playwright .NET (fallback)

- Version 1.64.1. Licence MIT (nuspec, repo `microsoft/playwright-dotnet` MIT); the bundled driver is `microsoft/playwright`, Apache-2.0 (GitHub API). Repo pushed 2026-10-23. Targets netstandard2.0 (nuspec).
- Size: nupkg 214 MB. Its zip directory shows the bundled Node driver per OS: darwin-arm64 122.5 MB, darwin-x64 125.7 MB, linux-arm64 123.4 MB, linux-x64 126.9 MB, win32_x64 93.9 MB (uncompressed), plus `package` 13.7 MB. Browsers are downloaded separately (`playwright install`, `PLAYWRIGHT_BROWSERS_PATH`, `PLAYWRIGHT_DOWNLOAD_HOST`; playwright.dev docs). On this host the cache holds `chromium-1248` 403 MB and `chromium_headless_shell-1248` 273 MB (`du`).
- Automation: `BrowserType.ConnectOverCDPAsync`, `NewCDPSessionAsync` ("Chromium-only" per docs), `CDPSession.SendAsync` (verified on playwright.dev/dotnet API pages). Rich locators, aria snapshot, screenshots, tracing.
- Linux/Windows/macOS: all three (driver per OS above). Headless on Linux: yes, `chromium_headless_shell` already cached here.
- Risks: runs a Node child process (extra process, 90 to 130 MB disk per OS); the "Do not start processes" rule in `BannedSymbols.NoProcess.txt` may need checking for the driver launch [INFERENCE: file exists at repo root, content not read]; each Playwright release pins a Chromium build.

### 2.4 PuppeteerSharp (primary)

- Version 25.15.0, licence MIT (nuspec; repo `hardkoded/puppeteer-sharp`, pushed 2026-10-28). Nupkg 4.5 MB, pure .NET CDP client plus WebDriver BiDi (`WebDriverBiDi` 0.0.61 dependency), targets net10.0, net8.0, netstandard2.0.
- API read from source on GitHub (`lib/PuppeteerSharp`): `LaunchOptions` has `ExecutablePath`, `Channel`, `UserDataDir`, `Args`, `Env`, `HeadlessMode`, `Pipe` (CDP over pipes, no TCP port), `BlockList`, `Allowlist` (URL patterns; "Requires Chrome 149+" for the allowlist, failing any non-matching request incl. subresources; CDP only). `ConnectOptions` has `BrowserWSEndpoint`, `BrowserURL`. `ICDPSession` exists for raw `Page.startScreencast`, `Input.dispatchMouseEvent`, `Accessibility.getFullAXTree`, `DOMSnapshot.*`. `BrowserFetcher` downloads a pinned Chrome build (this host's puppeteer cache already holds `chrome` 152.0.7977.75, 182.9 MB zip, and `chrome-headless-shell` 114 MB zip / 120 MB unpacked).
- Linux/Windows/macOS: all (Chrome for Testing/Chromium builds for each); iOS/Android: not applicable (use the cloud browser, section 2.6).
- Headless test on a Linux box with its own Xvfb: yes. Headless mode needs no display at all; headful runs under `xvfb-run`. Host has Chrome 153 and the cached builds above [facts, `command -v`/`du`].
- Risks:
  1. Chrome for Testing / Google Chrome redistribution terms were **not read**; bundling Chrome inside an AGPL installer may be disallowed. Prefer download-on-first-run into the app data dir, or the user's installed Chrome/Edge (`Channel`) [INFERENCE; owner/legal check needed].
  2. Frame rate and latency of screencast over the network head path is unmeasured.
  3. Site bot-detection: omp's built-in browser tool ships `puppeteer/00_stealth_*` scripts, which shows the issue is real for headless Chromium (omp `src/tools/puppeteer/`, `browser/launch.ts:19-31`).

### 2.5 Side-by-side

| | Avalonia WebView | CefGlue | Playwright .NET | PuppeteerSharp |
| --- | --- | --- | --- | --- |
| Licence | MIT | MIT (CEF BSD, not read) | MIT wrapper + Apache-2.0 driver | MIT |
| AGPL fit | yes | yes | yes | yes |
| Win / mac / Linux | WebView2 / WKWebView / WebKitGTK | win-x64, osx-x64, linux-x64 only | yes, per-OS Node | yes |
| Mobile / web head | Android adapter, not hostable on web head | no | no | no (stream frames instead) |
| DOM read | `InvokeScript` only | CEF API | full | full + AX tree |
| Trusted input | no | yes | yes | yes |
| Screenshots | no API | roll own | yes | yes |
| Own profile dir | engine-specific | yes | yes | yes (`UserDataDir`) |
| Footprint | 1.5 MB + OS engine | CEF (large) | +214 MB nupkg, browser 273 to 403 MB | 4.5 MB, browser 120 to 183 MB zip |
| Headless Linux test | yes (Xvfb, proven) | likely | yes | yes |
| Main risk | no agent surface | stale Chromium, Avalonia 11 | weight | Chrome redistribution terms |

### 2.6 Recommended architecture

1. The host (desktop head, API sandbox, or cloud environment) launches Chromium with an isolated `--user-data-dir` per workspace/session, `Pipe=true` (no listening port), and the `Allowlist`/`BlockList` policy; one CDP owner (PuppeteerSharp) in the host process.
2. The `browser` MCP tool (section 4) reads DOM/AX tree, performs actions, returns screenshots as MCP image content.
3. The UI shows `Page.startScreencast` frames (JPEG/PNG + ack) as an Avalonia `Image` on native heads and as an `<img>`/canvas fed by the API stream on the Blazor head; user take-over sends `Input.dispatchMouseEvent/KeyEvent` back. This is the only design that works on all six heads, including Dispatch from mobile/web, because it never needs a native web control on the viewer.
4. NativeWebView stays as the user-facing pane for ADE-15 on desktop heads; the two are different products (agent browser vs user browser) and must not share a profile.
5. Cloud: browser runs inside the session sandbox next to omp (ADR-0020); Railway policy risk S1 applies (`railway-cloud-desktop.md:85`).

Alternative worth deciding on: omp already has a `browser` eval prelude (puppeteer-core), settings `browser.enabled`, `browser.cdpUrl`, `browser.headless`, `browser.relay` (omp `src/tools/browser/settings.ts`). Pointing `browser.cdpUrl` at the host-owned Chromium would reuse omp's tool with no host code, but it is an eval prelude (no per-call host approval, no URL policy hook, version-coupled to omp), so the host cannot enforce section 5 on it.

## 3. Computer Use per OS

### 3.0 omp already has a native desktop backend (new fact)

- omp 18.12.5 contains a `computer` eval prelude, disabled by default: `computer.enabled=false`, `computer.display=active`, `computer.maxWidth=3840`, `computer.maxHeight=2400`, `computer.backgroundFallback=takeover|refuse` (omp `src/tools/settings.ts:603-690`). Backed by `DesktopSession` from `@oh-my-pi/pi-natives` (`index.d.ts:31-80,1423-1460`), which exposes capture, input, accessibility, menus, held input, global Escape cancel, lock/sleep state, and per-OS permission strings (`capturePermission`, `inputPermission`, `axPermission`).
- Backends, from the installed Linux x64 native module (`pi_natives.linux-x64-modern.node`, 111 MB): strings show `x11rb` and `XTEST` (X11), `org.freedesktop.portal.RemoteDesktop`, `libei`, `uinput`, `pipewire` (Wayland), `org.a11y.atspi.*` (AT-SPI). `computer/worker.ts:398,460,722,817` names a `quartz` backend (macOS). `DesktopWindow.id` doc: "Numeric on X11/Win32/macOS; a composite AT-SPI string on Wayland" (`index.d.ts:1581-1583`). Only the linux-x64 package is installed here, so Win32 and macOS behaviour is read from code comments, not run.
- The prelude's own prompt already states the injection policy: screen text is untrusted data, only direct user messages authorize consequential actions, confirm before external side effects, task-scoped control needs live human confirmation (omp `src/prompts/system/computer-use.md`).
- It sizes screenshots for Anthropic models to 1280x896 ("coordinate-safe") and otherwise up to 3840x2400 (`computer.ts:35-50`).
- Open: whether the `computer` prelude is reachable in an ACP session (it is an Eval prelude, not a standalone tool) was not checked.

Decision: for **local** Cowork on the user's own machine, enabling this is the cheapest path. A host-owned `computer` MCP tool (own implementation or a thin proxy over omp's native module) is only needed for (a) host approval UI per action, (b) Dispatch (omp runs elsewhere, desktop executes), (c) audit/ledger logging.

### 3.1 Linux X11

- Capture: XGetImage/XShm/XComposite via libX11/libxcb; CLI tools on this host: `scrot`, `import`, `xwd`, `ffmpeg`. Input: XTEST (`libXtst.so.6` present) or `xdotool` (`/usr/bin/xdotool`, `libxdo.so.3`). Window list/focus: `xwininfo`, `xprop`, `xfwm4` present.
- .NET libraries: `ScreenCapture.NET.X11` 3.0.0 is LGPL-2.1-only (nuspec), "Currently the only way to use ScreenCapture.NET on linux. Quite slow and can easily break depending on the X-Server configuration" (README). **Reject**: LGPL-2.1-only is a new licence class for this repo (ADR-0018 lists MIT/BSD/Apache only), no macOS support, quality warning from the author.
- SharpHook 8.0.1 (MIT wrapper): global hook + input simulation, no capture. X11 full, Wayland lacks features and "requires elevated privileges", text-entry simulation not supported on Wayland; macOS needs Accessibility permission (README). The package bundles native `libuiohook` for linux-x64/arm/arm64, osx-arm64/x64, maccatalyst, win-x64/x86/arm64 (nupkg listing, 43 KB to 950 KB each). Upstream `kwhat/libuiohook` ships `COPYING.LESSER.md` (LGPL-3.0) at tag 1.3; SharpHook's fork `TolikPylypchuk/libuiohook` reports licence NOASSERTION in the GitHub API. The nuspec says MIT. **The native binary licence is unresolved**, so do not adopt without an owner decision. We only need injection, not hooks, so it is avoidable by direct P/Invoke (XTestFakeKeyEvent, SendInput, CGEventPost).
- Verifiable here: yes, fully (section 3.5).

### 3.2 Linux Wayland

- Mechanism: `xdg-desktop-portal` `RemoteDesktop` + `ScreenCast` sessions; the user sees a dialog on `Start()`; input via EIS (libei, recommended) or `Notify*` D-Bus methods; capture through a PipeWire node; persistence via `persist_mode` and a single-use `restore_token` (portal docs, RemoteDesktop version 2, read from flatpak.github.io). `ydotool` needs uinput privileges and is not installed here.
- .NET: `Tmds.DBus.Protocol` 0.95.2 (MIT) for the portal calls; libei and PipeWire have no maintained .NET binding known to this research [INFERENCE: not searched exhaustively]. omp's native module already links libei/portal/pipewire (3.0).
- Repo status: no Wayland session ever verified (`phase-20...:45`, `ADR-0009:144`). Avalonia 12.1.4 here runs X11 by default (`ADR-0004:15`).
- Verifiable here: no. `xdg-desktop-portal` binaries exist under `/usr/libexec` and `weston` is installed, but `pipewire`, `wireplumber`, and a portal backend with an interactive dialog are absent. At best a `weston --backend=headless` smoke of the compositor; the portal consent path is not testable.

### 3.3 Windows

- Input: `SendInput` in `User32.dll`. It "is subject to UIPI" and only injects into equal or lower integrity level, and "neither GetLastError nor the return value will indicate the failure was caused by UIPI blocking" (Microsoft Learn, read). Elevated windows and the secure desktop (UAC, lock screen) are unreachable unless the app is elevated.
- Capture: `Windows.Graphics.Capture` (WinRT) or DXGI Desktop Duplication. `GraphicsCaptureSession.IsBorderRequired` exists from 10.0.20348 and in practice means Windows 11; borderless capture needs `GraphicsCaptureAccess.RequestAccessAsync(Borderless)` and a manifest capability for packaged apps; on Windows 10 the setter can throw `E_NOINTERFACE` (Microsoft Learn via web search; the Windows 10 failure is from a third-party issue report, `openai/codex#38271`). So expect a yellow capture border by default.
- .NET: `ScreenCapture.NET.DX11` 3.0.0 is LGPL-2.1-only (reject for the licence reason above). Direct P/Invoke or CsWin32 for `SendInput`; Vortice/TerraFX for DXGI/D3D11 were not licence-checked in this run. omp's native module covers Win32 [not run here].
- Verifiable here: no (Linux host only). Mark Windows as unmeasured, exactly as ADR-0009 does.

### 3.4 macOS

- Input: `CGEventPost`. TCC has separate buckets for Accessibility, PostEvent, ListenEvent; `CGPreflightPostEventAccess` / `CGRequestPostEventAccess` exist; the Mac App Store has rejected apps for `CGEvent.post` (App Review guideline 2.4.5, Apple Developer Forums thread 820594, via web search; secondary).
- Capture: ScreenCaptureKit (macOS 12.3+ [INFERENCE from API history, not re-read]). Sequoia re-prompts for Screen Recording monthly unless the app uses `SCContentSharingPicker` or holds the restricted `com.apple.developer.persistent-content-capture` entitlement; MDM key `forceBypassScreenCaptureAlert` from 15.1 (web search of Apple/9to5Mac sources, secondary).
- .NET: no maintained ScreenCaptureKit binding was found in this research. Options: omp native module (`quartz` backend), a small Swift/ObjC helper, or ObjC-runtime P/Invoke. Avalonia desktop on macOS is plain net10.0, so the net-macos bindings are not available without a workload [INFERENCE].
- Verifiable here: no. Phase 29 already says "Mac channel: No" (`29-SUMMARY.md:5`).

### 3.5 What can be verified on this host under a private Xvfb display

Tools present (all `command -v`/`ldconfig`): `Xvfb`, `xvfb-run`, `xdotool`, `scrot`, `import`, `xwd`, `xprop`, `xwininfo`, `xfwm4`, `ffmpeg`, `dbus-launch`, `libXtst`, `libxdo`, `libatspi`, `libpipewire`, `google-chrome` 153, `weston`, Playwright Chromium 1248 and Puppeteer Chrome 152 caches, `node`, `bun`.

| Verifiable on a private Xvfb (`Xvfb :N` with its own `DISPLAY`, never the main one) | Not verifiable here |
| --- | --- |
| Launch Chromium isolated-profile, CDP over pipe, `startScreencast` frames, `Input.*` events, allow/block-list enforcement, AX tree read, headless and headful | Windows (SendInput/WGC/UIPI/UAC), macOS (TCC, ScreenCaptureKit, CGEvent), real GPU paths |
| X11 capture + XTEST input against a fixture app/page in a private Xvfb; click-hit-test with `xdotool`/`scrot` as an independent oracle | Wayland portal consent and PipeWire (no `pipewire`, no interactive portal backend, XP-02 BLOCKED) |
| NativeWebView agent-surface negative test (confirm no screenshot/CDP) under `xvfb-run`, extends the existing spike | AT-SPI semantic tree on Linux (`at-spi2-registryd` not installed) |
| Screenshot downscale/size pipeline, image block size limits, gateway image pass-through (unit level) | Live model vision accuracy/cost (needs approved live turn, OD-07) |
| Gateway + in-proc MCP loopback over the real `ModelContextProtocol` 2.3.0 client (existing test pattern) | omp `session/new.mcpServers` end-to-end (needs live omp turn and owner approval) |

## 4. Vision pipeline and tool route

### 4.1 Image limits (provider facts)

From the Claude vision docs (platform.claude.com, read 2026-10-28):
- Tokens = `ceil(w/28) * ceil(h/28)`. High-resolution tier (Claude 4.7 and later): max long edge 2,576 px, max 4,784 tokens. Standard tier: 1,568 px and 1,568 tokens. 1920x1080 costs 1,560 (standard, resized to 1456x819) or 2,691 (high-res, native). 3840x2160 costs 1,560 or 4,784.
- Per-request: 100 images (models with 200k context), 600 otherwise; max 8000x8000 px per image; **above 20 images in one request a stricter per-image dimension applies (2000 px)** and it counts images inside `tool_result`, including resent history; max 10 MB base64 per image on the Claude API, 5 MB on Bedrock; 32 MB request cap. Lossy compression hurts text legibility.
- Computer-use toolset docs: coordinates are in screenshot pixels; the toolset **rejects** oversized screenshots instead of downscaling, so the host must resize and scale coordinates back; `zoom` returns a region at full resolution; the toolset definition costs about 4,500 input tokens; Anthropic recommends a dedicated VM/container, no sensitive data, domain allowlist, and human confirmation (`computer-use-tool`, security section, lines 63-75).
- Other providers (OpenAI, Gemini, Bedrock variants) limits: not read in this run. ADR-0019 provider catalogue is provider-neutral, so sizing must be a per-model policy value, not a constant.

Recommended host pipeline [INFERENCE from the above]: capture native pixels, keep an immutable full-res frame in memory for `zoom`, downscale to a per-model target (default long edge 1280 for coordinate-safe models, up to 2576 for high-res models), PNG for UI/text-heavy frames, JPEG q>=85 only for photographic pages, cap each frame under 5 MB, keep at most N recent screenshots in context (drop older ones from the tool output, keep a text caption). Return coordinates in the *scaled* frame plus the scale factor in the text block.

### 4.2 How ACP carries images in this repo

- Prompt to omp: `AcpImageContent.Build` makes `{"type":"image","data":<base64>,"mimeType":...}` blocks, allowed types png/jpeg/gif/webp, defaults 8 images, 10 MB each, 25 MB total (`src/OmpOS.Harness.Acp/AcpImageLimits.cs:7`, `AcpImageContent.cs:11-50`). `AcpAgentSession.SupportsImages` reads `promptCapabilities.image` from `initialize`; an image prompt is refused when it is false (`AcpAgentSession.cs:249-256,1065,2273-2277`). omp advertises `image: true` and `mcpCapabilities: {http, sse}` (omp `acp-agent.ts:836-846`).
- Tool results from MCP: omp's MCP bridge keeps image blocks: `formatMCPContent` pushes `case "image"` items through as image content (omp `src/mcp/tool-bridge.ts:222-224`), and the ACP layer resolves image data for session updates (`acp-agent.ts:1804-1825`).

### 4.3 Gaps in the app (repo facts)

- `ToolUpdateContent` documents "the output text and the file edits" and ignores everything else; `AcpHookSignals.OutputText` also reads only text (`src/OmpOS.Contracts/Ui/ToolUpdateContent.cs:5-24`, `src/OmpOS.Harness.Acp/AcpHookSignals.cs:122-140`). A screenshot returned by a tool never reaches the transcript model. Adding an image field to the tool-update contract and the transcript view, plus export (`OmpOS.Presentation/Export`), is required before Cowork can show what the agent saw.
- The gateway is not wired into any head: `McpGateway` appears only in its own project, tests, and the architecture test (`grep src apps services tests`). Apps use `AcpMcpServerMapper` only if wired; `AcpAgentSession` passes `mcpServers` in `session/new`, `load`, and `fork` (`AcpAgentSession.cs:127,607,1125,1142`).

### 4.4 The route to expose `computer` and `browser` to omp

1. Host implements two MCP servers in-process (ModelContextProtocol 2.3.0 ships `StreamServerTransport`, `StreamClientTransport`, `ImageContentBlock`, `McpServerTool`; string search of `ModelContextProtocol.Core.dll` in the NuGet cache) and connects an `McpClient` to them over a stream pair. `McpClientConnector` today opens stdio and Streamable HTTP only (`src/OmpOS.Harness.Mcp/McpClientConnector.cs:5-60`), so an in-proc connector method is a small addition [INFERENCE].
2. `McpToolRegistry.RegisterServerAsync(serverName, client, ct)` registers their tools (`McpToolRegistry.cs:47`). `Enable(sessionId, tool)` grants per session (`:93`).
3. `McpGatewayServer.Start(registry, invoker)` listens on `127.0.0.1` ephemeral port with a per-start bearer token; hand `LocalEndPoint` + `BearerToken` to omp as an `AcpHttpMcpServer` entry in `session/new.mcpServers` (`McpGatewayServer.cs:34,61,67,74`; entry types in `AcpHttpMcpServer.cs`). Tool names reach omp as `<server>__<tool>` (`McpGatewayToolNames.cs`).
4. `tools/call` goes through `McpToolInvoker.InvokeAsync`, which first checks the tool is offered to that session, then calls the host `McpToolApproval` delegate (boolean), then the upstream client (`McpToolInvoker.cs:33-50`). The returned `CallToolResult` is serialised whole, so image blocks pass unchanged (`McpGatewayDispatcher.cs:101-106,292`).
5. Constraints found: the gateway's 1 MiB cap is on request bodies only (`McpGatewayServer.cs:34,171-181`), so large screenshot responses are not capped there; any single response larger than omp or the model accepts must be downscaled before it enters the result. `tools/list` shows only `name`, `description`, `inputSchema` (`WriteTool`, `McpGatewayDispatcher.cs:108-128`), so no MCP tool annotations (`readOnlyHint` etc.) reach omp today.
6. A gateway outage fails `session/new` in omp (`#configureMcpServers` throws, ADR-0010 Consequences); health-check first.
7. Disabling omp's own `browser` and `computer` preludes in Cowork sessions (settings `browser.enabled=false`, `computer.enabled=false` via a `--config` overlay, which is settings-only, ADR-0010 Verdict) keeps one governed path. Not yet verified at runtime.

### 4.5 Dispatch (control of the user's desktops from mobile/web)

[INFERENCE, design only] When omp runs in the cloud (ADR-0020), the desktop cannot be reached by a loopback gateway. The desktop app must dial out to the API (WebSocket), register as a capability provider, and the cloud gateway forwards `computer` calls to it; frames travel back as JPEG/PNG. The tool executes where the screen is, so the approval UI must be on that desktop (or a paired mobile device), never in the cloud. ADR-0015 (remote environments) and ADR-0020 are the places to extend; not read for this report.

## 5. Prompt-injection containment

Facts from sources read: Anthropic computer-use docs say Claude "follows commands found in content even when they conflict with your instructions", recommend VM/container, no sensitive data, domain allowlist and human confirmation, and note classifiers scan tool-returned screenshots (`computer-use-tool`, lines 63-75). Simon Willison's lethal trifecta (private data + untrusted content + external communication, 2025-06-16) and Meta's Agents Rule of Two (2025-10-31, https://ai.meta.com/blog/practical-ai-agent-security/, read): an agent session should hold at most two of untrusted input, sensitive access, and state change/external communication; all three requires human-in-the-loop. Also Google DeepMind CaMeL (cited by Willison, not read directly).

Patterns applicable here (design recommendations, not implemented):

| Pattern | Applied to Autonogrammer |
| --- | --- |
| Rule of Two per session | Browser sessions: untrusted input (A) is always on. Default them to A+C (isolated profile, no credentials, no workspace secrets, no access to the user's tokens); never allow B in the same session. Reading private files or secrets in the same omp session after browsing needs a fresh session or human approval. |
| Isolated profile and environment | Dedicated `--user-data-dir` per session, no copy of the user's cookies/logins, env scrubbed, no host filesystem mount in the cloud sandbox (Railway S4/S5: env and checkpoints persist secrets, `railway-cloud-desktop.md:88-90`). |
| Network containment | `Allowlist`/`BlockList` in PuppeteerSharp (Chrome 149+ for allowlist); deny `file://`, `chrome://`, `localhost`/RFC1918 and the cloud metadata address by default (SSRF). Egress allow-list at the sandbox (Railway documents none, S6). |
| Exfiltration channels | Block image/link/CSS fetches to non-allowed hosts (Willison: any HTTP request or image load leaks). Strip `target=_blank` pop-outs (`NewWindowRequested` in the WebView; `Target.setAutoAttach` in CDP). Do not let the agent paste into the address bar from page content. |
| Treat page content as data | Wrap every tool result in a delimited, labelled envelope ("untrusted page content, source URL, retrieved at") in the text block, strip zero-width/hidden text and `display:none`/off-screen nodes before returning DOM/AX text, cap length. omp's computer prelude already tells the model to treat screen text as untrusted (`computer-use.md`); add the same preamble to the browser tool description. Delimiters reduce but do not remove risk. |
| Human gates on consequential actions | Host-owned approval (`McpToolApproval` delegate, `AcpApprovalPolicy` allowlist/narrow hook) on: form submit, navigation to a new origin, file download, file upload, clipboard, credential fields, purchases, any `computer` action outside the approved app/window. The approval dialog must show the host-observed target (URL, window title, element text), not model-supplied text. `AcpApprovalPolicy` is deny-by-default (`AcpApprovalPolicy.cs:20-35`). |
| Capability scoping | Split tools: read-only (`screenshot`, `read_page`, `list_windows`) vs actuating (`click`, `type`, `key`, `navigate`), approvals and grants per tool via `Enable(sessionId, tool)`. The gateway already refuses ungranted tools before approval (`McpGatewayDispatcher.CallToolAsync`). |
| No credential injection | Never let a tool fill passwords from the secret store from a model-chosen origin; autofill only after the host verifies the origin matches the stored entry. |
| Output hygiene and logging | Redact secrets in screenshots before they enter transcripts/ledger where known (clipboard, token fields); log each tool call (URL, action, approval) to the append-only ledger the plugins already use [INFERENCE: ledger API not read in this run]. |
| Kill switch and rate limits | Escape/hotkey and a host "Stop" that aborts the CDP session or revokes the `computer` grant (omp native session has `globalEscape` and `holdDisplayUntilReleased` as precedent, `index.d.ts:46-58,1440-1445`); cap actions per minute and steps per task. |
| Classifier layers | Provider classifiers on screenshots exist for Anthropic computer use; they are "not ideal for use cases without a human in the loop" and "precautions ... remain important" (docs). Treat as defence in depth only. |

## 6. Risks (ranked)

1. Prompt injection with real actions (browser + desktop) in one session. Mitigation needs the Rule of Two split and host approval to ship with the first release, not later.
2. Licensing traps: ScreenCapture.NET (LGPL-2.1-only), SharpHook's bundled libuiohook (LGPL upstream, fork NOASSERTION), Chrome for Testing redistribution terms (unread). All are new classes versus ADR-0018's current dependency set.
3. Gateway never exercised against a real omp turn (OD-07) and not wired to any head; the whole tool route is feasible-by-source only.
4. UI contract drops tool-result images; screenshots invisible in transcripts and exports.
5. Windows/macOS/Wayland unmeasured for every option here; Mac App Store policy may reject synthetic input.
6. Railway Trust and Safety wording on agent-driven desktops (S1) can take a workspace offline.
7. omp version coupling: `computer`/`browser` preludes and the native module are omp-version specific; pinned omp is 18.12.5 here.

## 7. Decisions needed and gating

| # | Decision | Options | Recommendation | Gate / who |
| --- | --- | --- | --- | --- |
| D1 | Agent browser engine | PuppeteerSharp primary; Playwright fallback; reuse omp `browser` prelude; CEF | PuppeteerSharp over host-launched Chromium (2.4), Playwright as fallback | Owner; then record as new ADR (extends ADR-0009, closes OQ-51 for the agent browser) |
| D2 | Chromium source | System Chrome/Edge via `Channel`; download Chrome for Testing at first run; bundle Chromium (BSD) | Download at first run + use system browser when present; no bundling | Owner/legal must read Chrome for Testing terms; **gates any installer work** |
| D3 | Computer Use implementation | Enable omp native `computer` only; host MCP `computer` tool over omp native module; own per-OS C# | Start with D3a for local, add host MCP tool when Dispatch/approval needs it | Owner; verify `computer` reachable in ACP |
| D4 | Licence class admission | Admit LGPL libs (ScreenCapture.NET, libuiohook) or forbid | Forbid; use P/Invoke or omp native | Owner/legal; amend ADR-0018 only if admitted |
| D5 | Run the live ACP verification | Approve one live omp `session/new` with gateway stub (ADR-0010 steps), no prompt | Approve it; it unblocks the whole route | Owner records approval under OD-07 in `docs/open-questions.md` |
| D6 | Session safety model | Rule of Two enforced by host vs advisory | Enforced: browser sessions never share a session with secret/workspace-write access | Product + security review (A10) |
| D7 | Transcript images | Extend `ToolUpdateContent` with images and render/export | Yes; prerequisite for Cowork Browser Use UI | Engineering (Presentation + Contracts), no owner gate |
| D8 | Windows/macOS/Wayland measurements | Windows 11 24H2 runner, Mac channel, Wayland session | Schedule spikes; until then mark unavailable (as R-3 requires) | Owner provides runners (same blocker as OQ-51, OQ-69) |
| D9 | Dispatch topology | Desktop dials out to API relay; per-device pairing and approval on desktop | Outbound relay, approvals on the executing device | Architecture (A03) + ADR-0015/0020 follow-up |
| D10 | Cloud browser/desktop host | Railway Sandbox per session vs headless-only browser in the Cloud agent container | Headless-only first; defer desktop streaming until Railway answers the policy question | Owner asks Railway before building (`railway-cloud-desktop.md` Owner decision 2) |
