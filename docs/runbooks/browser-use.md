---
description: "Cowork browser use on the desktop head: setup, degraded states, and the hosted exclusion."
---

# Cowork browser use

Status: Built on the desktop head. The live omp turn is not run.

Last verified: 2026-10-30

Applies to: v0.16.0

## Setup

The desktop head binds `Cowork:BrowserUse` through `AddBrowserUse`. `Enabled` defaults to false. A located Chromium binary is required before tools are registered. The user confirms any managed download from Settings > Browser. The embedded manifest ships with no rows, so a missing binary stays degraded until a system or configured binary is found.

The capability name is `browser_use`. It is offered only when `Enabled` and `AllowDispatchSessions` both hold. Nothing in this phase calls a Dispatch hub.

Stop is the catalogued chord Ctrl+Shift+. (scope Application). It cancels the in-flight call token, sets control to revoked, and leaves the browser process open. Close browser is a separate action.

## Degraded states

| State | What the operator does |
| --- | --- |
| Browser use is turned off | Turn on browser use in Settings > Browser |
| Chromium is not installed | Install Chromium, or set an explicit executable path |
| The managed browser is not pinned | Keep using a system browser until a manifest row is pinned |
| Chromium is too old | Update Chromium to at least the configured major (default 149) |
| The sandbox is unavailable | Enable the sandbox. A root host may set `AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX` only for a live test the owner approved |
| This model cannot see page images | Choose a vision model the catalogue already marks as accepting images |
| This browser runs on your desktop | Open the desktop app. Web and Android have no relay |

## Hosted exclusion

The hosted API and the Web head never launch a browser. Web and Android show RemoteViewUnavailable. The ADR-0021 deployment has no browser process. There is no `RelayBrowserPaneFeed`.

## Live omp turn

Not run. Reason: OD-07 not approved. The required record is an owner-approved turn that shows the Cowork gateway, an image result, and `browser.enabled: false` in the Cowork overlay. The overlay YAML written by the Cowork profile is checked in unit tests only. No approver and no date are recorded because the turn did not happen.
