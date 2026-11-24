---
description: "Cowork browser use on the desktop head: what it does, setup, options, verification, degraded states, the hosted exclusion and owner actions."
---

# Cowork browser use

Status: Incomplete; production page wiring, guarded live browser behavior and a live omp turn are not verified. Do not enable for production.

Last verified: 2026-11-24 (code and configuration read against this tree; no suite or live browser run for this page)

Applies to: [ADR-0034](../decisions/ADR-0034-cowork-browser-use.md), v0.16.0 (phase 72)

## What it does

Browser Use is the Cowork agent browser on the desktop head: a signed-in Cowork session drives a host-controlled Chromium over the DevTools protocol (PuppeteerSharp) through `browser_*` tools, with an origin policy, per-action approval through the Cowork approval broker, a grant store, a tool audit, screenshot limits for vision models, and a visible browser pane with a Stop chord. It is Cowork-only and desktop-only; the browser runs on the user's machine, never on a server. omp's own browser is disabled in the Cowork overlay (`browser.enabled: false`) so the session has exactly one, governed, browser.

Related runbooks: [Cowork](cowork.md), [Cowork isolation](cowork-isolation.md), [Dispatch](dispatch.md).

## Setup

`Cowork:BrowserUse:Enabled` defaults to false. On the desktop head the persisted Browser settings page values are applied to the options at composition (`AgentHead.WireBrowserUse` passes `Configure` into `BrowserUseComposition.Wire`, which binds `Cowork:BrowserUse`, locates Chromium, and composes the approval broker, the file-backed grant store and tool audit, the tool host and the pane). A located Chromium binary alone is not sufficient to make browser tools safe or available.

What production wiring still omits (phase 72 gap): `Wire` is called without a real page source, so the provider receives `ClosedPages`, whose `OpenAsync` throws "The browser is not available.", and a no-op local-port permit. The egress guard proxy (`EgressGuardProxy`) exists in `Autonogrammer.Harness.BrowserUse` but is not connected by this composition. Until a real page source, the broker/audit path and egress are wired and proven together, no page can open from the running head even when the tools are offered.

To configure (development):

1. Sign in and open a Cowork session on the desktop head (see [Cowork](cowork.md)).
2. In Settings > Browser, turn browser use on and set the origin policy, host lists, download and sandbox modes. The page shows the located Chromium state; an explicit executable path can be set there or as `Cowork:BrowserUse:ExecutablePath`.
3. Dispatch-origin turns additionally require `AllowDispatchSessions`; the production capability path remains unverified, and approval is still local.
4. The Stop chord is Ctrl+Shift+. (scope Application), bound on the browser pane and on the chrome indicator; its integrated browser behavior remains unverified. Close browser is a separate action.

## Options

Configuration section `Cowork:BrowserUse`, bound by the head (`BrowserUseOptions`). The Settings > Browser page edits the starred subset and persists it; the rest are configuration-only. Defaults are from `BrowserUseOptions`; validation failures name the `Cowork:BrowserUse` key and never a value.

| Key | Default | Meaning |
| --- | --- | --- |
| `Enabled`* | `false` | Whether the agent browser is enabled. Tools are offered only when true and a Chromium binary is located. |
| `ExecutablePath`* | empty | Explicit Chromium path. Empty means locate. |
| `Source` | `Auto` | Chromium locate source. |
| `MinimumMajorVersion` | `149` | Minimum Chromium major version; a found binary below it reports too old. |
| `ViewportWidth` / `ViewportHeight` | `1280` / `800` | Viewport in CSS pixels. |
| `PolicyMode`* | `PromptOnNewOrigin` | Origin policy for navigation. |
| `AllowedHosts`* | empty | Hosts allowed in strict mode. |
| `DeniedHosts`* | empty | Hosts that are always denied. |
| `DownloadMode`* | `Deny` | Download policy. |
| `SandboxMode`* | `Required` | Sandbox policy. `Disabled` is refused by validation unless `AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1` (a root test host escape; never set it in production). |
| `PersistentProfile`* | `false` | Whether a named profile is kept. |
| `AllowDispatchSessions`* | `false` | Whether dispatch-origin turns may use the browser. |
| `LocalOriginsTrusted`* | `false` | Whether granted local origins are treated as trusted. |
| `MaxConcurrentBrowsers` | `2` | Maximum browser processes per head. |
| `MaxTabs` | `5` | Maximum tabs in one browser. |
| `MaxActionsPerMinute` | `60` | Maximum actuating actions per minute. |
| `MaxCallsPerSession` | `500` | Maximum tool calls per session. |
| `MaxScreenshotsPerSession` | `40` | Maximum model screenshots per session. |
| `MaxScreenshotBytes` | 3 MiB | Maximum screenshot size; 3 MiB is also the host ceiling. |
| `ScreenshotLongEdge` | `1280` | Screenshot long edge in pixels. |
| `MaxReadChars` | `20000` | Maximum characters returned by a page read. |
| `MaxBrowserMemoryMiB` | `1536` | Browser working-set cap in mebibytes. |
| `IdleTimeout` | 10 minutes | Idle timeout. |
| `MaxSessionLifetime` | 2 hours | Wall-clock lifetime of a browser session. |
| `LocalFramesPerSecond` | `10` | Local pane frame rate. |

Test-only variables: `AUTONOGRAMMER_LIVE_BROWSER=1` plus `CHROME_BIN=<chromium binary>` opt the `Category=Live` browser tests in. Never set them in production.

## How to verify

Offline (no browser launch, no live turn):

```sh
dotnet test --project tests/Autonogrammer.Harness.BrowserUse.Tests -c Release -- --filter-not-trait "Category=Live"
dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-not-trait "Category=Live"
```

Live (real Chromium, network host): set `AUTONOGRAMMER_LIVE_BROWSER=1` and `CHROME_BIN` and run the `Category=Live` tests of `Autonogrammer.Harness.BrowserUse.Tests` (headful smoke and pipe-connect). These have no recorded pass; phase 72 verification records the live CDP tests failing on the then-current host and the headful run executing zero tests without `CHROME_BIN`. Record any run with date, host and Chromium version before claiming more than this page does.

## Degraded states

| State | What the operator does |
| --- | --- |
| Browser use is turned off | Turn on browser use in Settings > Browser |
| Chromium is not installed | Install Chromium, or set an explicit executable path |
| The managed browser is not pinned | Keep using a system browser until a manifest row is pinned |
| Chromium is too old | Update Chromium to at least the configured major (default 149) |
| The sandbox is unavailable | Keep browser use disabled. `SandboxMode: Disabled` is refused unless `AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1`, which is a test-host escape only. |
| This model cannot see page images | Choose a vision model the catalogue already marks as accepting images |
| This browser runs on your desktop | Open the desktop app. Web and Android have no relay |
| Tools offered but opening a page fails with "The browser is not available." | Expected on the production head: no real page source is wired yet (see Setup). Do not work around it. |
| A host is denied by policy | Check `PolicyMode`, `AllowedHosts` and `DeniedHosts` in Settings > Browser; denied hosts stay denied |

## Hosted exclusion

The hosted API and the Web head never launch a browser. Web and Android show RemoteViewUnavailable. The ADR-0021 deployment has no browser process. There is no `RelayBrowserPaneFeed`.

## Live omp turn

Not run. Reason: OD-07 not approved. The required record is an owner-approved turn that shows the Cowork gateway, an image result, and `browser.enabled: false` in the Cowork overlay. The overlay YAML written by the Cowork profile is checked in unit tests only. No approver and no date are recorded because the turn did not happen.

## Owner actions

See [v0.16.0 owner actions](v0.16.0-owner-actions.md). Browser-specific: resolve the phase 72 D-12/D-13 decision — BlockList and the Strict Allowlist cannot be combined in the pinned PuppeteerSharp, so an owner must choose how both boundaries hold (separate connections, a fork, own enforcement, or relax one requirement) before `browser_*` tools can be offered in production; approve the live omp turn (OD-07); record a live/headful run with `CHROME_BIN` on a network host. This runbook performs none of them.

## What cannot be verified

- Any real page navigation from the running head (production page source is closed; see Setup).
- Guarded live behavior end to end: egress proxy enforcement, approval and audit on a real page, the Stop chord stopping a live browser.
- The live omp turn with an image result (OD-07 not approved).
- Windows and macOS Chromium location and launch behaviour beyond compile checks.
