# Phase 72: Cowork Browser Use - Context
Source: docs/plans/v0.16.0-research/briefs/72-browser-use.md
**Gathered:** 2026-10-28
**Status:** Ready for planning

<domain>
## Phase Boundary

Milestone v0.16.0. Requirements AG-72-1 to AG-72-16. The Cowork agent can browse: a host-launched, isolated Chromium driven over a CDP pipe by PuppeteerSharp, shown as a live pane on the desktop head, exposed to omp as `browser_*` tools with vision, under an egress guard, two-stage approvals, a visible Stop and a hostile-page test corpus. Record: `docs/decisions/ADR-0034-cowork-browser-use.md` (Proposed 2026-10-28, design only).

Work units (brief 72): WU-1 Chromium, launch, session (AG-72-1 to 3); WU-2 egress guard and policy (AG-72-4, 5); WU-3 tools, approvals, vision (AG-72-8 to 12, 14, 15); WU-4 viewer stream and pane model (AG-72-6, 7, 13); WU-5 pane views; WU-6 settings and acquisition; AG-72-16 (test site and 19-page corpus) spans WU-1 to WU-3. WU-4 to WU-6 may start once WU-1's contract names are agreed; WU-3 needs the WU-1 and WU-2 types.

Depends on (index phase table): 70 (and 69); ADR-0036 D13 (relay carries no frames) and D6 (capability `browser_use`); 68; 67. Shares no code with ADR-0035. Needs `Cowork.Abstractions` (`ICoworkToolProvider`, `CoworkToolHost`, `ICoworkApprovalBroker`, `ICoworkGrantStore`, `ICoworkToolAudit`, `IsUntrusted` taint, `ModelReadsImages`, `IDockPanel`, `CoworkDockHost`, overlay with `browser.enabled: false`, `ToolUpdateContent.Images`, prompt composer).

Must NOT do: no relay or remote frame feed (`RelayBrowserPaneFeed` is not built), no remote input, no file upload tool, no cookie import from the user's browsers, no bundled Chromium, no manifest rows without owner terms review, no cloud browser, no change to `ToolUpdateContent`, `McpGateway`, the launch overlay or the shell (ADR-0032's), no project registration beyond "per phase" rows (R17), no `ContractVersion.Current` edit (R9), no governance of omp's own browser in Code mode.

</domain>

<decisions>
## Implementation Decisions

### Engine, client and launch
- **D-01:** Two browsers, two products: the agent browser is a host-launched Chromium; the Code-mode preview pane (ADR-0009, `Avalonia.Controls.WebView`) is unchanged; they never share a profile, cookie jar or process; Code mode keeps omp's own `browser` prelude as today. (ADR-0034 D1)
- **D-02:** Chromium is driven over the DevTools protocol on a pipe, never a listening port, using PuppeteerSharp 25.15.0 as CDP client only: `ChromiumProcessLauncher` (project `Autonogrammer.Harness.BrowserUse`) starts the process itself with an allow-listed environment and an argument list (never a shell string) and calls `Puppeteer.ConnectAsync` with a `TransportFactory` returning a `PipeTransport` over its own streams; Unix `/bin/sh -c 'exec 3<&0 4>&1 </dev/null >/dev/null; exec "$0" "$@"' <chrome> <args...>`; Windows `--remote-debugging-io-pipes=<read>,<write>`; the version is read with `Browser.getVersion`. This avoids PuppeteerSharp's env copy, its shell-joined pipe launch and the `<`/`>` in `--proxy-bypass-list=<-loopback>`. (ADR-0034 D2, Context)
- **D-03:** Raw CDP through `ICDPSession` for `Page.startScreencast`, `Fetch.enable`, `Accessibility.getFullAXTree`, `DOMSnapshot.captureSnapshot`, `Input.dispatch*`, `Browser.setDownloadBehavior`, `Browser.setPermission`, `Page.setInterceptFileChooserDialog`; the fallback if the pipe-connect spike fails is Microsoft.Playwright 1.64.1 over CDP, recorded in OQ-361 and not adopted. (ADR-0034 D2)
- **D-04:** Locate in order: `Cowork:BrowserUse:ExecutablePath`; a managed install under `<state>/browser/chromium/<version>/`; a system Chrome or Chromium at well-known per-OS paths (snap skipped, [INFERENCE]); else `BrowserNotInstalled`. Minimum major version 149 (`MinimumMajorVersion`), older gives `BrowserTooOld` and the browser is closed. (ADR-0034 D3)
- **D-05:** `ChromiumAcquirer` runs only on the user's action after a dialog naming version, source host, size and licence line; it downloads the row of the embedded `chromium-manifest.json` (`{version, platform, url, sha256, sizeBytes}`, written by `scripts/browser/pin-chromium.sh`) over TLS from `storage.googleapis.com` only, refuses redirects to other hosts, verifies size and SHA-256 before extraction, extracts without path traversal, sets the execute bit and publishes by atomic rename; no row for the platform is `ManagedBrowserNotPinned` and hides the action; no rows are committed (OQ-362); Chromium is never bundled. (ADR-0034 D3)
- **D-06:** Profile: a fresh 0700 directory per Cowork session under `Directory.CreateTempSubdirectory` (path of `[A-Za-z0-9._/\\:-]` only, else `ProfilePathUnsafe`), wiped at session end; no user cookies, logins or extensions; `PersistentProfile` (default false) keeps `<state>/browser/profiles/<id>` and the Settings text says cookies are protected only by file mode (`--password-store=basic --use-mock-keychain`). (ADR-0034 D4)
- **D-07:** Environment allow-list: `PATH`, `HOME`/`USERPROFILE`, `TMPDIR`/`TEMP`, `LANG`, `LC_*`, `DISPLAY`, `WAYLAND_DISPLAY`, `XDG_RUNTIME_DIR`, `SystemRoot`, `LOCALAPPDATA`, `APPDATA`; every `OMP_*`, `*_KEY`, `*_TOKEN`, `*_SECRET`, `AUTONOGRAMMER_*` and any other variable is dropped. (ADR-0034 D4)
- **D-08:** Chromium's sandbox stays on; if it cannot start the state is `SandboxUnavailable` with no automatic `--no-sandbox`; `SandboxMode=Disabled` exists for tests on this root host only and the validator refuses it unless `AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1`. (ADR-0034 D4)
- **D-09:** `ChromiumLaunchPlan` is the one unit-tested flag list: `--headless=new`, `--remote-debugging-pipe`, `--user-data-dir`, `--no-first-run`, `--no-default-browser-check`, `--disable-extensions`, `--disable-sync`, `--disable-background-networking`, `--disable-component-update`, `--mute-audio`, `--hide-scrollbars`, `--password-store=basic`, `--use-mock-keychain`, `--proxy-server=http://127.0.0.1:<port>`, `--proxy-bypass-list=<-loopback>`, `--force-webrtc-ip-handling-policy=disable_non_proxied_udp`, `--window-size=W,H`; each is launch-tested on Chrome 153, the WebRTC and component-update flags are [INFERENCE] until then. (ADR-0034 D4)
- **D-10:** One browser process per Cowork session; at most `MaxConcurrentBrowsers` (2); `MaxTabs` 5; memory cap 1536 MiB (`Process.WorkingSet64`); idle 10 min; wall 2 h; disposal closes via CDP then kills the process tree. (ADR-0034 D4; brief 72 options)

### Network policy
- **D-11:** `EgressGuardProxy` is a session-owned loopback HTTP proxy (forward and `CONNECT`, `System.Net.Sockets`, no dependency) started before Chromium and used by every request; it resolves names itself, classifies every returned address, connects to the validated address (no second lookup), and denies loopback, RFC1918, link-local (169.254.169.254), CGNAT 100.64/10, unspecified, multicast, reserved, IPv6 unique-local and link-local; IPv4-mapped and NAT64 addresses are unwrapped and re-checked; literals that parse in more than one reading are refused. Proxy credentials are per session (`Page.AuthenticateAsync` is [INFERENCE]). (ADR-0034 D5, Open risks)
- **D-12:** Local dev servers are reachable only through an explicit `LocalOriginGrant(host, port)` for `localhost`, `127.0.0.1` or `[::1]` created by a user decision (local-network wording, session scope, no "always"); only `http`, `https` and `about:blank` are allowed; `file:`, `chrome:`, `chrome-extension:`, `devtools:`, `view-source:`, `javascript:`, `data:` and `blob:` top-level navigations are refused by `NavigationGate` and repeated in PuppeteerSharp `BlockList`. (ADR-0034 D5)
- **D-13:** `BrowserUse:PolicyMode` is `PromptOnNewOrigin` (default) or `Strict` (only `AllowedHosts`, proxy refuses the rest, `Allowlist` second layer needs Chrome 149+); `Open` exists and is ignored for Dispatch turns; `DeniedHosts` applies in all modes; no category list is shipped. The name `PolicyMode` is kept by R16. (ADR-0034 D5; R16)
- **D-14:** `NavigationGate` uses raw `Fetch.enable` with `patterns:[{resourceType:"Document"}]` so every document request (each redirect hop, iframes) is paused: a new top-level origin raises the approval card, a redirect to a denied target aborts before a request leaves; the card shows the host-observed full URL and, when the query exceeds 64 characters, "this address carries N characters of data". Accepted residual: an approved page can load subresources from unapproved origins in `PromptOnNewOrigin`. (ADR-0034 D5)

### Pane and viewer stream
- **D-15:** The pane source is `Page.startScreencast` (`format: jpeg`, `maxFramesInFlight: 1`, acknowledged only after the viewer consumed the frame, `sendLastFrame: true`); frames exist only while a viewer is attached, else `stopScreencast`. (ADR-0034 D6)
- **D-16:** `IBrowserPaneFeed`, `BrowserFrame`, `BrowserPaneState`, `BrowserControlState` live in `Autonogrammer.Contracts` (L0, additive); `LocalBrowserPaneFeed` (desktop, in-process, up to 10 frames per second, JPEG quality 70, long edge 1280) is built; `RelayBrowserPaneFeed` is not built (ADR-0036 D13) and the Web and Android heads show `RemoteViewUnavailable` ("This browser runs on your desktop"). (ADR-0034 D6; brief 72 AG-72-6)
- **D-17:** `IDockPanel` is a content contract: the pane registers with `CoworkDockHost` (tab strip, one visible panel, beside Artifacts), each provider registers its own panel. (ADR-0034 D6; R7)
- **D-18:** Take-over exists only on the desktop that runs the browser: pointer and key events map to CSS pixels with frame metadata and go out as `Input.dispatchMouseEvent`, `dispatchKeyEvent`, `insertText`; agent calls wait (`UserHasControl`); the keyboard is captured only in take-over and F6 (existing region-cycle key, `src/Autonogrammer.Native.Views/MainView.axaml.cs:826`) leaves it; no remote viewer sends input (OQ-369). The pane shows an Agent view strip (last image the model received with size, format, bytes, time). (ADR-0034 D6)

### Tool family and approvals
- **D-19:** `BrowserToolProvider : ICoworkToolProvider`, `ProviderId` `browser`, nine tools `browser_navigate`, `browser_click`, `browser_type`, `browser_scroll`, `browser_read`, `browser_screenshot`, `browser_wait`, `browser_back`, `browser_close` (omp shows `mcp__cowork_browser_*`); all `ReadOnly, ByPolicy` except `browser_click` and `browser_type` (`Actuating, ByPolicy`); `browser_navigate` previews `Grant = ("browser.origin", target origin)`; `browser_screenshot` has `Results` Text and Image. Arguments and results as the ADR-0034 D7 table (`browser_wait` at most 10 s; `browser_read` `view` is `tree`, `text` or `console`). (ADR-0034 D7)
- **D-20:** `GetStateAsync` reports `Available`, `NeedsSetup` (`BrowserDisabled`, `BrowserNotInstalled`, `ManagedBrowserNotPinned`) or `Unavailable` (`BrowserTooOld`, `SandboxUnavailable`, `ProfilePathUnsafe`, `LaunchFailed`); `PreviewAsync` reads host state only (URL without query, element role and name from the accessibility tree) and never touches the page; `EndSessionAsync` closes the browser, wipes the profile and forgets session approvals; the provider holds no per-call lock (host serialises calls). (ADR-0034 D7)
- **D-21:** Two approval stages: stage 1 is ADR-0032's gate over the descriptors (in Manual a ReadOnly call is allowed only with a stored `("browser.origin", origin)` grant, an Actuating call is asked, Skip allows); stage 2 is the provider's own through `ICoworkApprovalBroker` for facts known only mid-call (an origin other than the requested one, a form submit, a sensitive field, a download release) and is not skipped by Skip. In Manual every `browser_click` and `browser_type` asks (OQ-376). (ADR-0034 D7)
- **D-22:** Grounding is hybrid: `browser_read` assigns refs `e<n>` from `Accessibility.getFullAXTree` (`backendDOMNodeId`) per snapshot generation; a stale ref fails `StaleRef` and asks for a new `read`; coordinates are the fallback scaled back by the host. Page text passes a `DOMSnapshot.captureSnapshot` visibility filter (no area, `display:none`, `visibility:hidden`, opacity under 0.1, font under 4 px, far off page), invisible Unicode (zero width, bidi, tag block U+E0000 to U+E007F) is stripped, length capped at 20,000 characters. (ADR-0034 D7; AG-72-11)
- **D-23:** Tool calls carry a `CancellationToken` the Stop control cancels; limits: 60 actions per minute, 500 calls per session (warning at 400), per-tool timeout 60 s, page load timeout 30 s; every limit is a validated option and an exhausted one gives `RateLimited` or `SessionLimitReached`. (ADR-0034 D7; AG-72-15)

### Vision
- **D-24:** `browser_screenshot` calls `Page.captureScreenshot` on demand at a fixed 1280x800 CSS-pixel viewport, device scale 1; a ladder (PNG, JPEG 85, JPEG 70, smaller `clip.scale`) keeps each image under `MaxScreenshotBytes` (3 MiB, never above the host cap); `ScreenshotLongEdge` 640 to 2576; at most one image per call, minimum 500 ms between shots, `MaxScreenshotsPerSession` 40 then `ImageBudgetExhausted` with a pane "Allow 20 more"; `zoom` returns a region at full resolution within the byte cap. (ADR-0034 D8)
- **D-25:** The provider returns a `CoworkImageBlock` (ADR-0032 maps it to an MCP `ImageContentBlock`); the transcript renders it through `ToolUpdateContent.Images`; a stored thumbnail of at most 320 px is requested of ADR-0032 (OQ-370); the audit holds no image bytes. (ADR-0034 D8)
- **D-26:** Vision authority is omp's model listing: `CoworkSessionContext.ModelReadsImages` from `OmpModelInfo.AcceptsImages`; when false the host withholds `browser_screenshot`, state stays `Available` for text tools, the pane shows `ModelCannotSeeImages` with "Choose a vision model", and a late screenshot fails with that reason; no image flag on `CatalogueModel` (OQ-363). (ADR-0034 D8)
- **D-27:** `BrowserLoopGuidance` is a fragment of at most 1 KiB appended by the Cowork system-prompt composer when this provider is `Available` (build, navigate to the granted local origin, screenshot three viewports, read the console, compare, fix, at most five rounds); the prompt budget is `CoworkSystemPromptOptions`: base 4 KiB, standing context 2 KiB, browser guidance 1 KiB, computer guidance 1 KiB, total at most 8 KiB. (ADR-0034 D8; R12)

### Safety, approvals matrix, Dispatch, audit
- **D-28:** Page-derived text is `CoworkTextBlock(text, IsUntrusted: true)` with `[page content begins, id <random>]` and `[page content ends, id <random>]` markers (fresh random id per result), angle-bracket look-alikes of any delimiter neutralised (OQ-360), and each image `Description` names the origin. `IsUntrusted` is set on every block that carries page content or a page image, local dev pages included; `LocalOriginsTrusted` (default false) can mark granted local origins trusted at the price of the Rule of Two; the provider cannot clear taint; the browser has no workspace access and the provider holds no credentials. (ADR-0034 D9 layers 1, 2)
- **D-29:** Approval cards show host-observed facts only (URL without query, element role and name, field type). Matrix: navigate to requested origin is stage 1 (`Target` URL without query and fragment, query length in `Details`, `AllowedAlways` stores `("browser.origin", origin)`, `Allowed` approves the origin for the session); other top-level origin is a stage 2 card in `PromptOnNewOrigin`; local dev server uses `("browser.local", "host:port")` and opens that port after the answer; form submit asks each time for a sensitive form, else once per origin per session; password, one-time-code and payment fields (`type=password`, `autocomplete` `cc-*`, `one-time-code`, names matching card, cvv, ssn, otp) ask every call with `MustBeLocal` and `browser_read` masks the value; downloads are `deny` (`Browser.setDownloadBehavior`; opt-in `Quarantine` per session: 0700 directory, 50 MiB cap, executable extensions refused, released only by a `MustBeLocal` card); file chooser cancelled and reported, no upload tool; `alert` and `confirm` dismissed, `beforeunload` accepted; popups closed; camera, microphone, geolocation, notifications, clipboard denied via `Browser.setPermission`; autofill and password manager off. (ADR-0034 D9; AG-72-12)
- **D-30:** Dispatch-origin turns (`CoworkTurnOrigin.Dispatch`): `Cowork:BrowserUse:AllowDispatchSessions` false by default and the desktop reports capability `browser_use` to the hub only when `Enabled` and `AllowDispatchSessions` hold; `Open` is treated as `PromptOnNewOrigin`; origin cards may be decided by the controller; sensitive typing, sensitive submits and download release are `MustBeLocal` and denied with no local user; `AllowedAlways` is treated as `Allowed` and never stored. (ADR-0034 D9.4; AG-72-14; OQ-375)
- **D-31:** While any actuating call is possible the pane and the Cowork header show "The agent is controlling the browser" with a **Stop** button and a keyboard chord (proposal `Ctrl+Shift+.`, set in the shortcut catalogue); the indicator is application chrome outside the frame; Stop cancels in-flight calls, revokes control (`ControlRevoked` until the user resumes) and leaves the browser open; "Close browser" ends the process. (ADR-0034 D9.5; AG-72-13)
- **D-32:** Every call is recorded through `ICoworkToolAudit` (no arguments, no result bytes) whose desktop sink is the local owner-only hash-chained JSONL `FileCoworkToolAudit`; nothing goes to the ledger or `cowork_events`; the provider's `Preview.Target` is the URL without query and fragment; guard denials, stage 2 decisions and taint changes go to `ILogger` via `LoggerMessage.Define` with event ids and the origin only, never page text, typed text or a full URL. (ADR-0034 D9.6; R15)
- **D-33:** omp's own browser stays off in Cowork: ADR-0032 D4 writes `browser.enabled: false` into the Cowork overlay; this phase requires it and verifies it only in the live run (OQ-365, ADR-0032 OQ-322). (ADR-0034 D9.7)

### Options, layers, tests
- **D-34:** Options section `Cowork:BrowserUse` (`BrowserUseOptions`, `IValidateOptions<T>`, `ValidateOnStart`, messages name the key never the value): `Enabled` false, `ExecutablePath`, `Source` Auto, `MinimumMajorVersion` 149, `Viewport` 1280x800, `PolicyMode` PromptOnNewOrigin, `AllowedHosts`, `DeniedHosts`, `DownloadMode` Deny, `SandboxMode` Required, `PersistentProfile` false, `AllowDispatchSessions` false, `LocalOriginsTrusted` false, `MaxConcurrentBrowsers` 2, `MaxTabs` 5, `MaxActionsPerMinute` 60, `MaxCallsPerSession` 500, `MaxScreenshotsPerSession` 40, `MaxScreenshotBytes` 3 MiB, `ScreenshotLongEdge` 1280, `MaxReadChars` 20000, `MaxBrowserMemoryMiB` 1536, `IdleTimeout` 10 min, `MaxSessionLifetime` 2 h, `LocalFramesPerSecond` 10. (brief 72; R11)
- **D-35:** `Autonogrammer.Harness.BrowserUse` (L2) references `Contracts` and `Cowork.Abstractions` only and no other L2 project; the `Harness.` name keeps it outside the NoProcess ban; it joins `ProcessSpawningAssemblies`; `Web`, `UI` and `Presentation` must not reference it; the scaffold step creates it, the test project, the PuppeteerSharp pin (own `Label`) and the embedded manifest; Presentation hosts `Cowork/Browser/BrowserPaneViewModel` and `Settings/Pages/BrowserUse*`, Native.Views the pane and settings views, `UI` `BrowserPane.razor`; the Web head omits the Settings page. (brief 72; manifest row 9; R17)
- **D-36:** The test site is an `HttpListener` server on two loopback ports (one granted, one not granted with a request log; a redirect from the first to the second must produce zero hits); the hostile corpus has 19 pages (list in the brief and ADR-0034 D10); live tests carry `[Trait("Category", "Live")]` with a first-statement `Assert.Skip` on `AUTONOGRAMMER_LIVE_BROWSER=1`; provider tests run through the real `CoworkToolHost` with a fake broker, grant store and audit; headful smoke only under a private `xvfb-run`. (ADR-0034 D10; AG-72-16)

### Claude's Discretion
- Internal class split below the brief's file list, view-model state record shapes, copy text of each degraded state sentence and fix action (one sentence and one action each).
- Final Stop chord (`Ctrl+Shift+.` is a proposal) and its entry in the shortcut catalogue.
- Which Chrome for Testing build is pinned (chosen only when the manifest is produced and OQ-362 is answered).
- `FontsMissing` host check (proposed, unverified) and the exact `Chromium` per-OS well-known path list.
- Exact `PageContentExtractor` tree format, `Preview.Details` wording, the `Quarantine` UI.

</decisions>

<specifics>
## Specific Ideas

- Degraded states, each one user sentence plus one fix action, shown in the pane, on the provider chip and as a short model failure: `BrowserDisabled`, `BrowserNotInstalled`, `ManagedBrowserNotPinned`, `BrowserTooOld`, `SandboxUnavailable`, `ProfilePathUnsafe`, `FontsMissing`, `LaunchFailed`, `Crashed`, `RemoteViewUnavailable`, `UserHasControl`, `ControlRevoked`, `ModelCannotSeeImages`, `ImageBudgetExhausted`, `StaleRef`, `OriginDenied`, `BlockedByPolicy` (names the rule), `DownloadDenied`, `PopupClosed`, `DialogDismissed`, `PageTooLarge`, `NavigationTimeout`, `RateLimited`, `SessionLimitReached`, `EgressGuardUnavailable` (all requests fail closed), `NoLocalApprover`.
- Corpus (19 pages): visible and hidden instructions, attribute text, comments and noscript, forged close tag and system message, invisible and bidi Unicode, text only in an image, redirects to loopback, RFC1918 and metadata in decimal, hex, octal, IPv6 and IPv4-mapped forms, simulated DNS rebinding, popup storms, automatic downloads, file chooser, password and card forms, dialog loops, oversized DOM and redirect loops, `javascript:`/`data:`/`blob:`/`file:`/`chrome:` links, permission and clipboard requests, cross-origin iframe with `postMessage`, a fake "agent is controlling" banner. Assertions are host invariants; model compliance is not claimed.
- Success checks on this host: `google-chrome` 153.0.8010.36 with `CHROME_BIN=/usr/bin/google-chrome AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1` (host is uid 0); `/proc/<pid>/environ` holds none of the planted secrets; version 148 gives `BrowserTooOld`; a 6 MiB PNG ladders under 3 MiB; the 41st screenshot gives `ImageBudgetExhausted`; stage 2 asks for a redirect origin even in Skip.

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Decision and briefs
- `docs/decisions/ADR-0034-cowork-browser-use.md` - D1 to D10, blocked list, open risks, verification route.
- `docs/plans/v0.16.0-research/briefs/72-browser-use.md` - AG-72-1 to 16, units WU-1 to WU-6, options, degraded states, OQ-360 to OQ-377.
- `docs/plans/v0.16.0-research/briefs/00-index.md` - phase 72 row; R7 (dock slots), R11 (`Cowork:` sections), R12 (prompt budget), R15 (local audit), R16 (`PolicyMode` kept), R17 (registries), R9.
- `docs/plans/v0.16.0-research/briefs/00-scaffold-manifest.md` - row 9 (`Harness.BrowserUse`), T8 (test project), `ProcessSpawningAssemblies` row.
- `docs/decisions/ADR-0032-cowork-shell-sessions-chat-and-tool-host.md` - D3 (`IDockPanel`), D4 (overlay, `ToolUpdateContent.Images`, prompt budget), D5 (`ICoworkToolProvider`), D6 (taint, grants); `docs/decisions/ADR-0036-dispatch-relay.md` D6, D13; `docs/decisions/ADR-0009-browser-pane.md` (user preview pane, lines 92 to 129 measurements); `docs/decisions/ADR-0010-omp-mcp-gateway-feasibility.md`.
- `docs/open-questions.md` - OQ-360 to OQ-377 (rows at lines 236 to 253), OQ-322, OQ-427, OQ-73.

### Research
- `docs/plans/v0.16.0-research/07-browser-computer-use-feasibility.md` - lines 38 to 101 (browser options, recommended architecture), 155 to 190 (vision limits, tool route), 191 to 209 (injection containment), 211 to 234 (risks, decisions D1, D2, D6, D7).
- `docs/plans/v0.16.0-research/06-cowork-parity-and-references.md` - lines 75 to 77 (C14, C15, C16); `02-ui-architecture.md` lines 186 to 197 (findings 5, 6); report 09 sections 2 and 7 (registration, conventions).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/Autonogrammer.Harness.OmpConfig/OmpModelInfo.cs:34` (`AcceptsImages`); `src/Autonogrammer.Native.Views/Sessions/ImageInputBinding.cs` (same signal for composer attachments); `src/Autonogrammer.Harness.AgentLoop/Launch/ConfigOverlayWriter.cs` (overlay writer, tested by `tests/Autonogrammer.Harness.AgentLoop.Tests/Launch/ConfigOverlayWriterTests.cs`).
- `src/Autonogrammer.Harness.Acp/AcpImageLimits.cs:7` (prompt images 8, 10 MiB, 25 MiB); `src/Autonogrammer.Harness.Mcp/McpToolInvoker.cs:33-50` (registry-tool path, not used by this provider).
- `services/Autonogrammer.Api/Cloud/Options/CloudOptionsValidator.cs` (validator naming keys, never values); `scripts/web-smoke/run-smoke.sh:78-97,150-155` (Chrome discovery and headless launch precedent).

### Established Patterns
- `Directory.Build.props:52` NoProcess ban keyed on the `Autonogrammer.Harness.` prefix; `tests/Autonogrammer.ArchitectureTests/ProjectGraphTests.cs:12` `ProcessSpawningAssemblies`; `tests/Autonogrammer.ArchitectureTests/LayerMap.cs:13-56` layers.
- Currently `src/Autonogrammer.Contracts/Ui/ToolUpdateContent.cs:5-24` reads text and edits only (images are added by brief 70 U5); `src/Autonogrammer.Harness.OmpConfig/OmpCatalogueBlock.cs:54` writes `AcceptsImages: false` for catalogue models.
- Live-test gate convention: `tests/Autonogrammer.Harness.OmpLauncher.Tests/Live/LiveOmpTerminalTests.cs:57`; Avalonia headless view tests, `tests/Autonogrammer.Native.Tests/Theme/ColourLiteralTests.cs`, `ThemeContrastTests.cs`, `tests/Autonogrammer.UI.Tests/CssContractTests.cs`, `tests/Autonogrammer.Presentation.Tests/PresentationIsolationTests.cs` (no Avalonia or Razor type in Presentation).

### Integration Points
- Settings: `src/Autonogrammer.Presentation/Settings/SettingsSection.cs`, `apps/Autonogrammer.Native/Settings/SettingsWindowFactory.cs`, `src/Autonogrammer.Native.Views/Views/Settings/SettingsPageViews.cs`; composition `apps/Autonogrammer.Native` (`AddBrowserUse`); `autonogrammer.css` imports for `browser-pane.css`.
- Created by earlier phases or the scaffold, not on disk yet: `Autonogrammer.Cowork.Abstractions`, `CoworkToolHost`, `FileCoworkToolAudit` (scaffold, 70 U1); `CoworkDockHost`, `IDockPanel` (69 U3); `ToolUpdateContent.Images`, `ImageBlock`, prompt composer, overlay (70); `Contracts/Cowork/Browser/*` (this phase adds the files; folder by scaffold); `Autonogrammer.Harness.BrowserUse` and `.Tests` (scaffold); `docs/runbooks/browser-use.md`, `scripts/browser/pin-chromium.sh` (this phase).

</code_context>

<deferred>
## Deferred Ideas

OMITTED BY DECISION: `RelayBrowserPaneFeed` and any frame stream to Web or Android (ADR-0036 D13, OQ-73; reserved budgets 2 fps, quality 55, 960 px, 2 Mbit/s are unmeasured, OQ-367); remote viewer input (OQ-369); file upload tool (ADR-0034 D9); cookie import from the user's browsers (OQ-364); Claude in Chrome (report 06 C15, OMIT); cloud-side browser for Web-only users (OQ-366, Railway terms); bundling Chromium; Chrome for Testing manifest rows (OQ-362); a financial or crypto category list (no classifier exists); site-detection evasion; governing omp's own browser in Code mode (OQ-371); `blob:` instead of `data:` pane URLs (OQ-368); an image flag on `CatalogueModel` (OQ-363).

Open questions that stay open, each with its default: OQ-360 delimiter (provider neutralises angle-bracket look-alikes and adds its own markers); OQ-361 Playwright fallback (yes if the spike fails); OQ-362 Chrome terms (system and configured browsers only); OQ-365 overlay disables omp browser and images reach the model (assumed, live run); OQ-370 stored screenshot (thumbnail 320 px); OQ-372 loop measurement (fragment yes, no measurement); OQ-373 Windows and macOS measurement (compile only); OQ-374 dock slot (R7: tab strip); OQ-375 Dispatch rules (as D-30); OQ-376 Manual friction (accepted); OQ-377 `LocalOriginsTrusted` (no). OQ-322 belongs to brief 70, OQ-427 to brief 74.

</deferred>

---

*Phase: 72-cowork-browser-use*
*Context gathered: 2026-10-28*
