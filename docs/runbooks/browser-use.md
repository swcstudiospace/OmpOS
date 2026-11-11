---
description: "Cowork browser use on the desktop head: setup, degraded states, and the hosted exclusion."
---

# Cowork browser use

Status: Incomplete; desktop composition, required audit and guarded live browser behavior are not verified. Do not enable for production.

Last verified: 2026-10-30

Applies to: v0.16.0

## Setup

`Cowork:BrowserUse:Enabled` defaults to false. The Native head does not yet wire the complete per-session page, approval, audit and egress path. A located Chromium binary alone is not sufficient to make browser tools safe or available.

Dispatch additionally requires `AllowDispatchSessions`; the production capability path remains unverified.

The intended Stop chord is Ctrl+Shift+. (scope Application); its integrated browser behavior remains unverified. Close browser is a separate action.

## Degraded states

| State | What the operator does |
| --- | --- |
| Browser use is turned off | Turn on browser use in Settings > Browser |
| Chromium is not installed | Install Chromium, or set an explicit executable path |
| The managed browser is not pinned | Keep using a system browser until a manifest row is pinned |
| Chromium is too old | Update Chromium to at least the configured major (default 149) |
| The sandbox is unavailable | Keep browser use disabled. The documented `AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX` variable is not consumed by the current launcher. |
| This model cannot see page images | Choose a vision model the catalogue already marks as accepting images |
| This browser runs on your desktop | Open the desktop app. Web and Android have no relay |

## Hosted exclusion

The hosted API and the Web head never launch a browser. Web and Android show RemoteViewUnavailable. The ADR-0021 deployment has no browser process. There is no `RelayBrowserPaneFeed`.

## Live omp turn

Not run. Reason: OD-07 not approved. The required record is an owner-approved turn that shows the Cowork gateway, an image result, and `browser.enabled: false` in the Cowork overlay. The overlay YAML written by the Cowork profile is checked in unit tests only. No approver and no date are recorded because the turn did not happen.
