---
description: "Cowork Computer Use: what it does, how to enable it, the kill switch, approvals, degraded states and owner actions."
---

# Cowork Computer Use

Status: Off by default. The core, backends and tools are built and unit-tested; production-head registration and a real-display run are not verified. Do not enable for production use.

Last verified: 2026-11-23 (unit suites only)

Applies to: [ADR-0035](../decisions/ADR-0035-cowork-computer-use.md) (Proposed), v0.16.0 (phase 73)

## What it does

Computer Use lets a signed-in Cowork session on the desktop head observe the screen and inject pointer and keyboard input through a platform backend (X11 on Linux; Windows and macOS backends are compile-checked only). Every actuating action needs local approval at the machine; Dispatch (remote) commands cannot approve Computer Use. A visible indicator and a kill switch are part of the design; the feature refuses to act when either cannot be shown.

Computer Use is Cowork-only. Code sessions, the API, the web head and cloud agents never register its tools.

## How to enable

1. Sign in and open a Cowork session on the desktop head (see [Cowork](cowork.md)).
2. Turn on the local opt-in control in Settings. The default is off and stays off across restarts until changed.
3. Grant the OS permissions the backend needs (Linux X11 session; macOS Accessibility and Screen Recording; Windows interactive desktop). A denied grant keeps the tools unoffered.
4. Approvals are local-only (`MustBeLocal` through the Cowork approval broker). The kill chord and the session Stop both stop injection immediately.

If the provider is not registered by the head, no `computer_*` tool is offered; that is the current production state and the safe default.

## Options

| Setting | Default | Meaning |
| --- | --- | --- |
| Local opt-in (Settings) | off | Master switch. Off means no capture, no injection, no tools offered. |
| `AllowDispatchSessions` | `false` | Whether Dispatch-originated sessions may use Computer Use at all. Keep `false`; remote approval is never accepted. |
| Approval mode | Manual | Every actuating action asks locally. Auto/Skip never apply to Computer Use. |

## How to verify

Offline:

```sh
dotnet test --project tests/Autonogrammer.Harness.ComputerUse.Tests -c Release -- --filter-not-trait "Category=Live"
dotnet test --project tests/Autonogrammer.Harness.ComputerUse.X11.Tests -c Release -- --filter-not-trait "Category=Live"
```

Live (real display, human present): start a session, confirm the indicator is visible and topmost, approve one harmless action, then stop it with the kill chord and confirm injection stops. Record the result with date and OS version. This check has not been recorded.

## Degraded states

| State | Behaviour |
| --- | --- |
| Opt-in off | Tools not offered; no capture |
| Backend unavailable (Wayland, headless, locked screen) | Refused with a named denial; no fallback backend |
| Indicator cannot be shown | Refused; Computer Use never runs invisibly |
| Approval timed out or window unavailable | Refused and audited; never auto-approved |
| Dispatch session | Refused unless `AllowDispatchSessions` is on, and approval is still local-only |

## Owner actions

Accept ADR-0035 (currently Proposed); run the Windows (SendInput/UIPI) and macOS (TCC, CGEvent) device smokes on real machines; decide whether Wayland stays unsupported or a portal backend is funded. See [v0.16.0 owner actions](v0.16.0-owner-actions.md). This runbook performs none of them.

## What cannot be verified

- Real-display indicator, kill-switch and approval behaviour (no recorded run).
- Windows and macOS backends at runtime (compile-checked only; no Mac channel, no Windows host here).
- Wayland and AT-SPI paths (unsupported by design today).
