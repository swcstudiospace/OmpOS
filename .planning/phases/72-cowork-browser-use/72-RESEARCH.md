# Phase 72: Cowork Browser Use - Research
Source: docs/plans/v0.16.0-research/briefs/72-browser-use.md
This file summarises research reports 02, 06, 07 and 09 as cited by ADR-0034, brief 72, the index reconciliation R7, R11, R12, R15, R16, R17 and the scaffold manifest. Nothing was built or run.

## Standard Stack / What to reuse
- Client: PuppeteerSharp 25.15.0 (MIT, 4.5 MB nupkg, targets net10.0) as CDP client only; `LaunchOptions.Pipe`, `Allowlist` (Chrome 149+), `BlockList`, `ICDPSession` exist (`07-browser-computer-use-feasibility.md:66-70`). It is a new pin with its own `Label` written by the scaffold (`briefs/00-scaffold-manifest.md:25,64`).
- `PipeTransport(Stream, Stream)` is public and `ConnectOptions.TransportFactory` accepts any `IConnectionTransport` (`docs/decisions/ADR-0034-cowork-browser-use.md:19`).
- Vision signal: `OmpModelInfo.AcceptsImages` (`src/Autonogrammer.Harness.OmpConfig/OmpModelInfo.cs:34`), consumed for composer attachments by `src/Autonogrammer.Native.Views/Sessions/ImageInputBinding.cs`; catalogue models write `AcceptsImages: false` (`src/Autonogrammer.Harness.OmpConfig/OmpCatalogueBlock.cs:54`).
- Overlay writer for `browser.enabled: false`: `src/Autonogrammer.Harness.AgentLoop/Launch/ConfigOverlayWriter.cs` (owned by ADR-0032; this phase only requires the setting).
- Prompt image limits: 8 images, 10 MiB each, 25 MiB total (`src/Autonogrammer.Harness.Acp/AcpImageLimits.cs:7`); the tool host caps results at 2 images of 3 MiB (`ADR-0034:9`).
- NoProcess ban keyed on the `Autonogrammer.Harness.` prefix (`Directory.Build.props:52`); `ProcessSpawningAssemblies` at `tests/Autonogrammer.ArchitectureTests/ProjectGraphTests.cs:12` (Acp, Rpc, Sandbox today; the scaffold adds `Harness.BrowserUse`, manifest line 66); layers `tests/Autonogrammer.ArchitectureTests/LayerMap.cs:13-56`.
- Options validator precedent naming keys not values: `services/Autonogrammer.Api/Cloud/Options/CloudOptionsValidator.cs`.
- Browser discovery and headless launch precedent: `scripts/web-smoke/run-smoke.sh:78-97` (`CHROME_BIN`, chromium, chromium-browser, google-chrome, chrome, Playwright cache), `:150-155` (`--headless --no-sandbox --remote-debugging-port=0`, empty profile).
- Live-test gate: `tests/Autonogrammer.Harness.OmpLauncher.Tests/Live/LiveOmpTerminalTests.cs:57` (`Assert.Skip` with the opt-in variable named).
- Pane host and F6: `src/Autonogrammer.Native.Views/MainView.axaml.cs:826`; settings registration points `src/Autonogrammer.Presentation/Settings/SettingsSection.cs`, `apps/Autonogrammer.Native/Settings/SettingsWindowFactory.cs`, `src/Autonogrammer.Native.Views/Views/Settings/SettingsPageViews.cs`.

## Facts the planner needs
- `Avalonia.Controls.WebView` cannot be the agent surface: no screenshot, no CDP, no trusted input (`07:43-44`); ADR-0009 measured WebKitGTK under Xvfb only (`ADR-0034:13`).
- PuppeteerSharp defects to design around: `SetEnvVariables` copies the whole host env and `Env` cannot remove variables; pipe mode on Unix joins arguments into one `/bin/sh -c` string; `--proxy-bypass-list=<-loopback>` contains `<` `>` (`ADR-0034:17-18`). Therefore the host owns the launch (`ADR-0034:35`).
- `LaunchOptions.Allowlist` needs Chrome 149+, is CDP-only, matches URL patterns, cannot combine with `BlockList` (`ADR-0034:20`); hence `MinimumMajorVersion` 149.
- Chrome for Testing publishes URLs and no checksums; `BrowserFetcher` has no checksum check, so the checksum-pinned manifest is mandatory; stable at writing 156.0.8078.73; terms unread (`ADR-0034:21,42`). Owner decision OQ-362 gates any download.
- omp 18.12.6 enables its own `browser` prelude by default (`browser.enabled` default true); the Cowork overlay must turn it off (`ADR-0034:15,126`); effect is unverified (OQ-365, OQ-322).
- Host: `/usr/bin/google-chrome` 153.0.8010.36, Xvfb and xvfb-run present, root uid 0 (needs `AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1`), cached Chrome 152.0.7977.75 under `~/.cache/puppeteer` (`ADR-0034:24`; `briefs/72-browser-use.md:63`).
- Tool route: `CoworkToolHost` through `McpGatewayServer` and `session/new.mcpServers`; names `mcp__cowork_browser_*`; image result becomes an MCP `ImageContentBlock` (`ADR-0034:22`); omp's MCP bridge keeps image blocks (`07:170`); route unverified live (ADR-0010, OD-07).
- Model image thresholds: tokens `ceil(w/28)*ceil(h/28)`; above 20 images in a request a 2000 px dimension limit applies and images inside `tool_result` count (`07:160-161`); Anthropic's toolset rejects oversize screenshots, so the host must size them (`07:162`). Hence 1280x800 default and 40 screenshots.
- `ToolUpdateContent` reads text and edits only today (`src/Autonogrammer.Contracts/Ui/ToolUpdateContent.cs:5-24`); images come from ADR-0032 D4 / brief 70 U5 (`ADR-0034:23`).
- Prompt budget (R12): base 4 KiB, standing context 2 KiB, browser guidance 1 KiB, computer guidance 1 KiB, total at most 8 KiB (`briefs/00-index.md:107`).
- `IDockPanel` is content only; `CoworkDockHost` shows a tab strip with one visible panel and each provider registers its panel (R7, `briefs/00-index.md:97`).
- The audit sink is the local owner-only hash-chained JSONL `FileCoworkToolAudit`; browser events stay local (R15, `briefs/00-index.md:113`).
- The Web head and Android show `RemoteViewUnavailable`; no relay feed exists in v0.16.0 (`ADR-0034:66,141`).

## Architecture Patterns
- One project `Autonogrammer.Harness.BrowserUse` (L2) with folders `Chromium/`, `Egress/`, `Policy/`, `Tools/`, `Content/`, `Vision/`, `Viewer/`, `Options/` (`briefs/72-browser-use.md:38`); contracts in `Contracts/Cowork/Browser/*` so view models and both renderers share one shape without referencing the Harness layer.
- Host-built environment, argument list, per-session loopback proxy with per-session credentials, pipe CDP; fakes for launcher, resolver, broker, grants, audit (`ADR-0034:131`).
- Two-stage approval: stage 1 descriptor policy, stage 2 provider asks through the broker for mid-call facts; Skip does not skip stage 2 (`ADR-0034:87`).
- Pull-based screencast pump: one frame in flight per viewer, drop never queue, ack after consumption, stop when no viewer (`ADR-0034:65`; WU-4 tests).
- Screenshots are separate from the viewer stream; a CDP-only compression ladder adds no imaging dependency (`ADR-0034:95`).
- Pane views mount as `IDockPanel` content; Presentation view model has no Avalonia or Razor type (`PresentationIsolationTests`).

## Pitfalls
- Never `--no-sandbox` automatically; `SandboxMode=Disabled` only with the env switch (`ADR-0034:48`).
- The egress guard is new SSRF-critical code: normalise every address form (decimal, hex, octal, IPv6, IPv4-mapped, NAT64), resolve once and pin (`ADR-0034:56,180`).
- Default `PromptOnNewOrigin` lets an approved page load subresources from unapproved origins; `Strict` closes it (`ADR-0034:61,182`).
- Proxy authentication through `Page.AuthenticateAsync` is [INFERENCE]; without it any local process could relay public traffic (`ADR-0034:180`).
- Headless Chromium without fonts renders blank text; `FontsMissing` is proposed and unverified (`ADR-0034:185`); sites may block headless Chromium (`ADR-0034:186`).
- Manual mode asks per `browser_click` and `browser_type`, and per omp `edit` in a tainted session (`ADR-0034:187`, OQ-376).
- Provider must set `IsUntrusted` on every page-derived block including local dev pages (`ADR-0034:106`); it cannot clear taint.
- Typed text must never be echoed, logged or audited; `Preview.Target` is the URL without query and fragment (`ADR-0034:79,125`).
- A downloaded Chrome for Testing carries unmeasured signature and redistribution questions on Windows and macOS (`ADR-0034:183`).
- The Playwright fallback adds a 214 MB nupkg with a Node driver per OS (`07:61`); do not adopt it unless the spike fails.
- Do not register projects or edit locks, slnx, slnf or LayerMap rows; the scaffold does (R17).

## Verification commands
- `dotnet test --project tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter-not-trait "Category=Live"` (`briefs/72-browser-use.md:62`).
- `AUTONOGRAMMER_LIVE_BROWSER=1 CHROME_BIN=/usr/bin/google-chrome AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1 dotnet test --project tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter-trait "Category=Live"` (line 63).
- `AUTONOGRAMMER_LIVE_BROWSER=1 AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1 xvfb-run -a dotnet test --project tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter-trait "Category=Live" -- --filter-class "*HeadfulSmokeLiveTests*"` (line 64; private display only).
- Spike first: `... -- --filter-class "*PipeConnectLiveTests*"` (`ADR-0034:171`).
- `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release`, `tests/Autonogrammer.Native.Tests`, `tests/Autonogrammer.UI.Tests`; `tests/Autonogrammer.ArchitectureTests -- --filter-class "*ProjectGraphTests*"` and `"*DecisionRecordTests*"` (line 65).
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` and `python3 scripts/legal/generate-third-party-notices.py --check` (line 65).
- The live omp turn with gateway, image result and `browser.enabled: false` is a separate owner-approved run recorded in `docs/runbooks/browser-use.md` (line 66).

## Cannot be verified on this host
Windows and macOS launch, pipe handles, proxy and a downloaded browser's signature behaviour (compile only); a pinned Chrome for Testing download (no manifest rows, terms unread); a live omp turn with gateway, images and overlay; vision accuracy and cost; relay frame rate (no feed exists); fonts on a minimal Linux; sites that block headless Chromium. Verifiable here: the success criteria on `google-chrome` 153 with a private Xvfb (`briefs/00-index.md:17`, `briefs/72-browser-use.md:86`).

## Validation Architecture
Framework: xunit.v3 with Shouldly under Microsoft.Testing.Platform (`briefs/00-scaffold-manifest.md:9`). Project: `tests/Autonogrammer.Harness.BrowserUse.Tests` (folders `Support/`, `Site/`, `Corpus/`; may reference `Autonogrammer.Cowork` to run the real `CoworkToolHost`), plus `Presentation.Tests`, `Native.Tests`, `UI.Tests`, `ArchitectureTests`. Quick run: `dotnet test --project tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter-not-trait "Category=Live"`. Full run: the three BrowserUse commands above plus the four head and architecture projects.

| Requirement | Tests (brief acceptance column) |
| --- | --- |
| AG-72-1, 2, 3 | `Chromium/*Tests` (plan lists every flag, allow-list drops 10 secret-named variables, locator over a fake file system, acquirer: good archive, wrong hash, wrong size, redirect to another host, traversal entry); `PipeConnectLiveTests` |
| AG-72-4, 5 | `AddressClassifierTests`, `RebindingTests`, `NavigationGateTests`, `EgressProxyTests`: zero hits in the ungranted target's log; Strict refuses unlisted hosts |
| AG-72-8, 9, 10, 11, 12, 14, 15 | `Tools/*Tests` through the real `CoworkToolHost`; `ScreenshotLadderTests` (6 MiB PNG under 3 MiB, 41st screenshot `ImageBudgetExhausted`); stage 2 asks for a redirect origin even in Skip; tainted after first read |
| AG-72-6, 7 | `ScreencastPumpTests` (ack-driven rate, drop never queue, stop with no viewer), `BrowserPaneViewModelTests` (every state and copy; announcements coalesced to one per 2 s) |
| AG-72-7, 13 | `Native.Tests/BrowserPaneViewTests`, `UI.Tests/BrowserPaneTests`, `AccessibilityTests`, `CssContractTests`: Stop outside the frame, F6 leaves capture, no new colour role, reduced motion |
| settings (WU-6) | `Presentation.Tests` page states and dialog text, `Native.Tests` page headless; acquisition hidden without a manifest row |
| AG-72-16 | `HostileCorpusLiveTests`, `SiteLiveTests`, `HeadfulSmokeLiveTests` (Live) |
Brief 72 gives no requirement-to-class table beyond the acceptance column; the mapping above is read from it [INFERENCE for pairing].

Wave 0 gaps (do not exist yet): the source and test projects (scaffold); `Support/` fakes (launcher, resolver, broker, grants, audit) and `LocalTestSite` on two loopback ports; `Site/` and 19 `Corpus/` HTML pages; embedded `chromium-manifest.json` with no rows; `scripts/browser/pin-chromium.sh`; `docs/runbooks/browser-use.md`; scripted-frame fixtures for pane tests; the `ProcessSpawningAssemblies` entry; `Contracts/Cowork/Browser/*` types; a fake `IBrowserPaneFeed`.
