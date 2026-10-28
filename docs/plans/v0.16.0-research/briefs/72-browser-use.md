# Brief 72: Cowork Browser Use

Names are post-rename (Autonogrammer.*); the repository on disk still uses OmpOS.*; paths are written in the post-rename spelling at the same relative path (line numbers cite the pre-rename files). Decision record: [ADR-0034](../../../decisions/ADR-0034-cowork-browser-use.md). Research: reports 06 (C14 to C17), 07 (sections 2, 4, 5), 02 (sections 8 to 10), 09 (sections 2 and 7). Built on ADR-0032 (briefs 69 and 70) as written; design only, nothing was built or run.

## Goal

The Cowork agent can browse: a host-launched, isolated Chromium driven over a CDP pipe by PuppeteerSharp, shown as a live pane on the desktop head (the Web and Android panes render the same models and wait for a relay feed, which ADR-0036 holds back), exposed to omp as `browser_*` tools with vision, under an egress guard, two-stage approvals, a visible Stop and a hostile-page test corpus.

## Requirements

- AG-72-1: The agent browser is a host-owned Chromium process connected over a pipe (no debugging port) and used through PuppeteerSharp 25.15.0 as CDP client only.
- AG-72-2: Chromium starts with an allow-listed environment, argument-list arguments, the sandbox on, and a fresh 0700 profile per Cowork session wiped at its end.
- AG-72-3: Chromium is found (configured, managed, system) or acquired only through a user-confirmed, checksum-verified step; nothing downloads silently and nothing is bundled.
- AG-72-4: Every browser request passes a per-session loopback egress guard that resolves names itself, denies private, loopback, link-local and metadata addresses, and pins the connection to the validated address.
- AG-72-5: Top-level navigations, including redirects, pass a navigation gate; the requested origin is covered by the host gate (stage 1) and any other origin by a provider approval card (stage 2); Strict mode allows only listed hosts.
- AG-72-6: Frames from `Page.startScreencast` reach a pane through `IBrowserPaneFeed` with bounded rate and size; the desktop feed is built, the Web and Android feed is not (ADR-0036 D13) and those heads show `RemoteViewUnavailable`.
- AG-72-7: The user can take over the pane with pointer and keyboard on the desktop that runs the browser, and leaves keyboard capture with F6.
- AG-72-8: `BrowserToolProvider : ICoworkToolProvider` offers `browser_navigate`, `browser_click`, `browser_type`, `browser_scroll`, `browser_read`, `browser_screenshot`, `browser_wait`, `browser_back` and `browser_close` with hybrid element-ref and coordinate grounding and the Effect classes of ADR-0034 D7.
- AG-72-9: `browser_screenshot` returns one bounded image (default 1280x800, at most 3 MiB, at most 40 per session) the model receives and the pane shows in an Agent view strip.
- AG-72-10: A model that cannot read images gets a specific degraded state and a vision-model chooser fed by omp's model listing (`ModelReadsImages`).
- AG-72-11: Every page-derived block sets `IsUntrusted`, carries a per-result random-id marker, passes a visibility and invisible-Unicode filter, and is capped at 20,000 characters.
- AG-72-12: Sensitive fields, form submits, downloads, popups, dialogs, permissions and file choosers follow the ADR-0034 matrix; downloads are denied by default and uploads do not exist.
- AG-72-13: An indicator outside the page frame states that the agent controls the browser, with a Stop control that cancels in-flight calls and revokes control.
- AG-72-14: Dispatch-origin turns use the browser only when `AllowDispatchSessions` is on, with `Open` mode ignored and sensitive actions `MustBeLocal`.
- AG-72-15: Every limit (actions per minute, calls, screenshots, tabs, memory, idle, wall time) is a validated option, and logs and audit targets carry no page text, typed text or query string.
- AG-72-16: A local test site and a 19-page hostile corpus prove the host invariants on this Linux host, headless and under a private Xvfb.

## Depends on

- ADR-0032 (briefs 69, 70): `ICoworkToolProvider`, `CoworkToolHost`, `ICoworkApprovalBroker`, `ICoworkGrantStore`, `ICoworkToolAudit` (the desktop sink is the local owner-only hash-chained JSONL `FileCoworkToolAudit`, brief 70 unit 1; nothing goes to the ledger or `cowork_events`), `IsUntrusted` taint, `ModelReadsImages`, `IDockPanel` (a content contract) and `CoworkDockHost` (brief 69 unit 3: tab strip, one visible panel, each provider registers its panel), the Cowork overlay with `browser.enabled: false`, `ToolUpdateContent.Images`, the system-prompt composer (`BrowserLoopGuidance` fragment), the Settings page frame.
- ADR-0036 D13 and D6 (relay carries no frames; capability `browser_use`); ADR-0031 (audit store); ADR-0030 (sign-in gates Cowork); ADR-0035 (shares no code).
- Existing: `ModelContextProtocol` 2.3.0, `OmpModelInfo.AcceptsImages`, `ImageInputBinding`, `ConfigOverlayWriter`.

## Projects and files owned

Layers per `tests/Autonogrammer.ArchitectureTests/LayerMap.cs:13-56`.

- NEW `src/Autonogrammer.Harness.BrowserUse` (L2; the `Harness.` name keeps it outside the NoProcess ban, `Directory.Build.props:52`). Folders `Chromium/`, `Egress/`, `Policy/`, `Tools/`, `Content/`, `Vision/`, `Viewer/`, `Options/`. References Contracts (L0), `Autonogrammer.Cowork.Abstractions` (L1) and no other L2 project.
- NEW `tests/Autonogrammer.Harness.BrowserUse.Tests` with `Support/` (fake launcher, fake resolver, fake broker, `LocalTestSite`), `Site/` and `Corpus/` HTML; it may reference `Autonogrammer.Cowork` to run the real `CoworkToolHost`.
- NEW in `src/Autonogrammer.Contracts` (L0): `Cowork/Browser/BrowserFrame.cs`, `BrowserPaneState.cs`, `BrowserControlState.cs`, `IBrowserPaneFeed.cs`.
- NEW in `src/Autonogrammer.Presentation` (L2): `Cowork/Browser/BrowserPaneViewModel.cs` and state types; `Settings/Pages/BrowserUse*`.
- NEW in `src/Autonogrammer.Native.Views` (L3): `Views/Browser/BrowserPaneView.axaml(.cs)`, `BrowserPaneStyles.axaml`, `Views/Settings/BrowserUsePageView`. NEW in `src/Autonogrammer.UI` (L2): `Components/Cowork/BrowserPane.razor`, `wwwroot/styles/browser-pane.css`.
- NEW `scripts/browser/pin-chromium.sh` (maintainer script that writes manifest rows), `docs/runbooks/browser-use.md`.
- TOUCHED (the slnx, slnf, `LayerMap.cs`, csproj, lock and `ProjectGraphTests.cs` rows are written by the scaffold step): `Directory.Packages.props` (PuppeteerSharp 25.15.0, new `Label`), `Autonogrammer.slnx`, `Autonogrammer.Linux.slnf`, `tests/Autonogrammer.ArchitectureTests/LayerMap.cs` and its csproj, `ProjectGraphTests.cs:12` (`ProcessSpawningAssemblies` gains the project), `THIRD-PARTY-NOTICES.md`, `docs/architecture.md`, `docs/SUMMARY.md`, `CHANGELOG.md`, `docs/open-questions.md`, `SettingsSection`, `SettingsWindowFactory`, `SettingsPageViews`, `autonogrammer.css` imports, `apps/Autonogrammer.Native` composition (`AddBrowserUse`). The scaffold step (`00-scaffold-manifest.md`) creates and registers the projects, the `ProcessSpawningAssemblies` entry, the slnx, slnf, `LayerMap.cs` and lock rows, and bumps `ContractVersion` to 1.6.0 once for all v0.16.0 additions; this phase adds code only, edits no `ContractVersion.Current`, and edits a registry file only for rows the manifest marks 'per phase' (notices when a package lands, `docs/architecture.md` prose). No change to `ToolUpdateContent`, `McpGateway`, the launch overlay or the shell: they are ADR-0032's.
- Options `Cowork:BrowserUse` (`BrowserUseOptions`, `IValidateOptions<T>` + `ValidateOnStart`, messages name the key, never the value): `Enabled` false, `ExecutablePath`, `Source` Auto, `MinimumMajorVersion` 149, `Viewport` 1280x800, `PolicyMode` PromptOnNewOrigin, `AllowedHosts`, `DeniedHosts`, `DownloadMode` Deny, `SandboxMode` Required, `PersistentProfile` false, `AllowDispatchSessions` false, `LocalOriginsTrusted` false, `MaxConcurrentBrowsers` 2, `MaxTabs` 5, `MaxActionsPerMinute` 60, `MaxCallsPerSession` 500, `MaxScreenshotsPerSession` 40, `MaxScreenshotBytes` 3 MiB (never above the host cap), `ScreenshotLongEdge` 1280, `MaxReadChars` 20000, `MaxBrowserMemoryMiB` 1536, `IdleTimeout` 10 min, `MaxSessionLifetime` 2 h, `LocalFramesPerSecond` 10.

## Work units

| Unit | Owner project and files | Tests | Acceptance |
| --- | --- | --- | --- |
| WU-1 Chromium, launch, session | `BrowserUse/Chromium/**`, `Options/**`, registration, `pin-chromium.sh`: `ChromiumLocator`, `ChromiumManifest`, `ChromiumAcquirer`, `ChromiumLaunchPlan`, `BrowserEnvironment`, `ChromiumProcessLauncher`, `BrowserSessionHost`, `AddBrowserUse` | `Chromium/*Tests` (plan lists every flag; allow-list drops 10 secret-named variables; locator over a fake file system; acquirer against a local server: good archive, wrong hash, wrong size, redirect to another host, traversal entry); `PipeConnectLiveTests` (Live) | Host launch, pipe `ConnectAsync`, `Browser.getVersion` >= 149 and one screencast frame on `google-chrome` 153; the browser's `/proc/<pid>/environ` holds none of the planted secrets; version 148 gives `BrowserTooOld`; no manifest row gives `ManagedBrowserNotPinned` |
| WU-2 Egress guard and policy | `BrowserUse/Egress/**`, `Policy/**`: `EgressGuardProxy`, `AddressClassifier`, `IHostResolver`, `OriginGrants`, `BrowserUrlPolicy`, `NavigationGate` | `AddressClassifierTests` (each denied range plus decimal, hex, octal, IPv6, IPv4-mapped, NAT64), `RebindingTests` (fake resolver flips), `NavigationGateTests`, `EgressProxyTests` (CONNECT and HTTP, granted versus ungranted port, wrong proxy credentials) | A redirect from the granted test origin to the ungranted loopback port, to 169.254.169.254 and to an RFC1918 literal gives zero hits in the target's request log; Strict refuses every unlisted host |
| WU-3 Tools, approvals, vision | `BrowserUse/Tools/**`, `Content/**`, `Vision/**`: `BrowserToolProvider`, one class per tool, `BrowserApprovalStage2`, `ElementRefTable`, `PageContentExtractor`, `PageMarker`, `SensitiveFieldClassifier`, `ScreenshotPipeline`, `BrowserLimits`, `BrowserLoopGuidance` | `Tools/*Tests` through the real `CoworkToolHost` with fake broker, grants and audit; `HostileCorpusLiveTests`, `SiteLiveTests` (Live); `ScreenshotLadderTests` | Each corpus page ends with its asserted invariants (no unapproved navigation, marker id unforgeable, hidden text dropped, every page block `IsUntrusted`, caps held, tainted session after the first read); a 6 MiB PNG ladders under 3 MiB; the 41st screenshot gives `ImageBudgetExhausted`; stage 2 asks for a redirect origin even in Skip |
| WU-4 Viewer stream and pane model | `BrowserUse/Viewer/**` (`ScreencastPump`, `LocalBrowserPaneFeed`, `TakeOverController`, `BrowserIndicatorState`), Contracts `Cowork/Browser/**`, Presentation `Cowork/Browser/**` | `ScreencastPumpTests` (ack-driven rate, drop never queue, stop with no viewer), `BrowserPaneViewModelTests` (every state with its copy; announcements coalesced to one per 2 s); `PresentationIsolationTests` unchanged | At most one frame in flight per viewer; the view model exposes Idle, Starting, Live, UserControl, Stopped, `RemoteViewUnavailable` and each degraded state with fix text; no Avalonia or Razor type in Presentation |
| WU-5 Pane views | Native.Views `Views/Browser/**`; UI `Components/Cowork/BrowserPane.razor`, `browser-pane.css`; both mounted as `IDockPanel` content and registered with `CoworkDockHost` | Native.Tests headless Avalonia (`BrowserPaneViewTests`; contrast and `ColourLiteralTests` green), UI.Tests bUnit (`BrowserPaneTests`, `AccessibilityTests`, `CssContractTests`) with scripted frames | Image has alt text (page title and host); Take over, Stop and Close are keyboard operable; Stop is outside the frame; no new colour role; reduced motion disables the only transition; F6 leaves capture |
| WU-6 Settings and acquisition | Presentation `Settings/Pages/BrowserUse*` (state of Chromium, policy lists, `PolicyMode`, acquisition confirmation model), Native.Views `Views/Settings/BrowserUsePageView`, `SettingsSection` and factory entries; Web omits the page | Presentation.Tests (page states, dialog text names version, host, size, licence line), Native.Tests (page headless) | The acquisition action is hidden unless a manifest row exists and is confirmed in a dialog; `PolicyMode`, `Open` and `LocalOriginsTrusted` show a one-line risk text |

WU-4 to WU-6 may start once WU-1's contract names are agreed; WU-3 needs the WU-1 and WU-2 types.

## Success criteria

- `dotnet test --project tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter-not-trait "Category=Live"` passes.
- `AUTONOGRAMMER_LIVE_BROWSER=1 CHROME_BIN=/usr/bin/google-chrome AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1 dotnet test --project tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter-trait "Category=Live"` passes (headless; this host is uid 0, hence the sandbox switch).
- `AUTONOGRAMMER_LIVE_BROWSER=1 AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1 xvfb-run -a dotnet test --project tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter-trait "Category=Live" -- --filter-class "*HeadfulSmokeLiveTests*"` passes.
- `dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release`, `tests/Autonogrammer.Native.Tests`, `tests/Autonogrammer.UI.Tests` and `tests/Autonogrammer.ArchitectureTests -- --filter-class "*ProjectGraphTests*"` pass; `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` is clean; `python3 scripts/legal/generate-third-party-notices.py --check` passes; `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*DecisionRecordTests*"` passes.
- A live omp turn with the gateway, an image result and `browser.enabled: false` is a separate owner-approved run (ADR-0010, OD-07), recorded in `docs/runbooks/browser-use.md`.

## Threat model

- Assets: the user's files, credentials and accounts; the internal networks of the machine and its cloud; the session's secrets; the model budget.
- Page content to model (injection): `IsUntrusted` on every block (host taint, Skip off for actuating calls and `edit`), per-result random markers, hidden-text and invisible-Unicode filtering, host-observed approvals, caps. Not prevented: influence of the model by visible text or image text.
- Model to network (SSRF, rebinding, exfiltration): egress guard with address pinning, denied ranges, a document-request gate that shows the query length, http and https only, `PolicyMode` `Strict`.
- Page to host: sandbox on, no extensions, fresh profile, scrubbed environment, no debugging port, downloads denied, no uploads, permissions denied, popups closed, proxy credentials per session.
- Local abuse: other local processes cannot reach CDP (pipe) or use the guard (credentials, [INFERENCE] until the spike); profile directory 0700.
- UI spoofing: indicator and approval cards are application chrome outside the frame; card text is host-observed, never model or page text.
- Remote abuse (Dispatch): off by default; sensitive actions `MustBeLocal`; `AllowedAlways` never stored for a dispatched turn.
- Resource abuse: tab, memory, idle, wall time, action rate, call count; process tree killed on dispose.
- Privacy: screenshots go to the model provider the user chose; the audit holds no page or typed text.

## Degraded and error states

Each is a named state with one user-facing sentence and one fix action, shown in the pane, on the provider chip (`GetStateAsync`) and returned to the model as a short failure: `BrowserDisabled`, `BrowserNotInstalled`, `ManagedBrowserNotPinned`, `BrowserTooOld`, `SandboxUnavailable`, `ProfilePathUnsafe`, `FontsMissing`, `LaunchFailed`, `Crashed`, `RemoteViewUnavailable`, `UserHasControl`, `ControlRevoked`, `ModelCannotSeeImages`, `ImageBudgetExhausted`, `StaleRef`, `OriginDenied`, `BlockedByPolicy` (names the rule), `DownloadDenied`, `PopupClosed`, `DialogDismissed`, `PageTooLarge`, `NavigationTimeout`, `RateLimited`, `SessionLimitReached`, `EgressGuardUnavailable` (all requests fail closed), `NoLocalApprover`.

## Cannot be verified on this host

Windows and macOS launch, pipe handles, the proxy and a downloaded browser's signature behaviour (compile-checked only); a pinned Chrome for Testing download (no manifest rows, terms unread); live omp with the gateway, images and the overlay; vision accuracy, cost and the frontend loop; any relay frame rate or mobile latency (no feed exists); fonts on a minimal Linux; sites that block headless Chromium. Verifiable here: the success criteria on `google-chrome` 153, with a private Xvfb for the headful smoke.

## Open questions

Owner is `autonogrammer-maintainers` unless stated. The final block OQ-360 to OQ-379 belongs to this brief (assigned by the design integrator); OQ-322 is brief 70's and OQ-427 is brief 74's.

- OQ-360: Does ADR-0032's labelled envelope name its delimiter so the provider can neutralise exactly it? Default: the provider neutralises angle-bracket tag look-alikes and adds its own random-id markers inside. Owner: architecture.
- OQ-361: If the pipe-connect or proxy-credential spike fails, is the fallback Playwright .NET over CDP? Default: yes, same architecture, a 214 MB larger footprint. Owner: architecture.
- OQ-362: Do the Chrome for Testing and Chrome terms allow the user-triggered download and a pinned manifest? Default: only system and configured browsers until the owner reads the terms. Owner: owner and legal.
- OQ-363: Add an image flag to `CatalogueModel` (ADR-0019 amendment)? Default: no; omp's model listing is the authority. Owner: providers.
- OQ-364: Import cookies from the user's browsers? Default: not built; the user signs in inside the pane. Owner: product and security.
- OQ-365: Does the overlay disable omp's browser and do images reach the model in a live turn? Default: assumed; verified in the live run of ADR-0010 together with ADR-0032 OQ-322. Owner: owner approval of the run.
- OQ-366: A cloud-side browser for Web-only users? Default: deferred until Railway answers OQ-73. Owner: owner.
- OQ-367: Remote viewer budgets (2 frames per second, quality 55, 960 pixels, 2 Mbit/s) for a future relay feed. Default: reserved, unmeasured. Owner: engineering.
- OQ-368: Switch Web panes from `data:` to `blob:` URLs, which needs a CSP change in `vercel.ts` and `render-csp.sh`? Default: stay on `data:`. Owner: web head.
- OQ-369: May a remote viewer ever send input? Default: no. Owner: security.
- OQ-370: Does a stored transcript keep a screenshot in full or as a thumbnail of at most 320 pixels? Default: thumbnail; the audit holds none. Request to ADR-0032 (brief 70). Owner: product.
- OQ-371: Govern omp's own browser prelude in Code mode? Default: unchanged. Owner: product.
- OQ-372: How is the frontend loop measured, and may `BrowserLoopGuidance` (at most 1 KiB) join the composer's 8 KiB prompt? Default: yes to the fragment, no measurement until a live vision turn. Owner: product.
- OQ-373: Windows and macOS measurement of launch, pipes, proxy and a downloaded browser. Default: compile-only, marked unmeasured. Owner: owner provides runners (OQ-69).
- OQ-374: `IDockPanel` is a content contract; `CoworkDockHost` (brief 69 unit 3) hosts the registered panels (Artifacts, Browser) with a tab strip and one visible panel, and each provider registers its own panel. Default: yes. Owner: brief 69.
- OQ-375: Dispatch-origin browser rules (`AllowDispatchSessions` false, `MustBeLocal` split, `AllowedAlways` not stored). Default: as ADR-0034 D9. Owner: security (with ADR-0036 OQ-427).
- OQ-376: Manual mode asks per `browser_click`, `browser_type` and per `edit` in a tainted session; add a session-scoped grant to the gate? Default: accept the friction. Owner: brief 70.
- OQ-377: Should granted local dev-server content count as trusted (`LocalOriginsTrusted`)? Default: no. Owner: security.

## Index rows

ADR index (`docs/decisions/README.md`):

`| [ADR-0034](ADR-0034-cowork-browser-use.md) | Cowork Browser Use: a host-owned Chromium driven over CDP, shown as a screencast, governed by an egress guard | Proposed 2026-10-28 | Design only. PuppeteerSharp 25.15.0 (MIT) as CDP client over a pipe; host-owned launch with a scrubbed environment; loopback egress guard; two-stage approvals on ADR-0032's tool host; no relay feed (ADR-0036 D13); no Chrome for Testing download until terms are read (OQ-362); Windows and macOS unmeasured |`

Open questions (`docs/open-questions.md`; columns ID, Question, Why it matters, Default taken, Owner, Status), all `open (non-blocking default)`: OQ-360 to OQ-377 as the bullets above (question, default and owner as written; why it matters is the ADR-0034 section named in the bullet's topic).
