---
description: "Cowork on the desktop and web heads: what it does, how to enable it, options, verification, degraded states and owner actions."
---

# Cowork

Status: Built and tested offline (unit, integration and architecture suites). No live model turn, live sign-in or hosted deployment is claimed; see "What cannot be verified".

Last verified: 2026-11-23 (offline suites only)

Applies to: [ADR-0032](../decisions/ADR-0032-cowork-shell-sessions-chat-and-tool-host.md), v0.16.0 (phases 69 to 76)

## What it does

Cowork is the chat-style surface beside Code (the Cursor-style workspace). A Cowork session is an omp session whose streamed events render as chat with tool cards, Artifacts, and (when separately enabled) Browser Use and Computer Use. Cowork sessions never create a Kanban card and never appear on the Kanban; `SessionSurface` enforces this in the data layer (ADR-0032). Code works signed out; Cowork requires sign-in (ADR-0030).

Related runbooks: [browser use](browser-use.md), [computer use](computer-use.md), [Cowork isolation](cowork-isolation.md), [Dispatch pairing secret storage](dispatch.md), [Accounts](accounts.md).

## How to enable

1. Sign in (see [Accounts](accounts.md)). Signed out, Cowork shows the sign-in gate and starts no session.
2. Switch surface with the Code/Cowork tabs (Ctrl+Shift+1 / Ctrl+Shift+2 where the browser or OS has not reserved them; OQ-320).
3. Choose or create a project folder when prompted. Project paths are device-specific and never sent to the API.
4. Browser Use and Computer Use stay off until enabled in Settings; see their runbooks. Nothing in Cowork enables them implicitly.

There is no server-side Cowork switch. The API serves Cowork chat, projects, memory, schedules and artifacts to authenticated owners only; every read is owner-scoped and a foreign identifier answers 404.

## Options

| Variable | Default | Meaning |
| --- | --- | --- |
| `Accounts__Enabled` | `false` | Account and board endpoints. Existing installations must set `true` before upgrade or those endpoints answer 404 `account_disabled` (see [Accounts](accounts.md)). |
| `Dispatch__Enabled` | `false` | Dispatch relay. Do not enable outside Development (see [Dispatch](dispatch.md)). |
| `Cowork:BrowserUse:Enabled` | `false` | Browser Use on the desktop head (see [browser use](browser-use.md)). |
| `AUTONOGRAMMER_LIVE_BROWSER` | unset | Test-only: opts the Browser Use live tests in. Never set in production. |
| `AUTONOGRAMMER_LIVE_ISOLATION` | unset | Test-only: opts the bubblewrap isolation live tests in. Never set in production. |
| `AUTONOGRAMMER_LIVE_GREPTIME` | unset | Test-only: opts the GreptimeDB telemetry live test in. Never set in production. |

Cowork approval modes are Manual (default), Auto (rules) and Skip. Manual asks before every actuating tool call. An approval that cannot reach a window is refused, never auto-approved.

## How to verify

Offline (no credential, no live turn):

```sh
dotnet test Autonogrammer.Linux.slnf -c Release -- --filter-not-trait "Category=Live"
bash scripts/ci/check-docs.sh
python3 scripts/docs/parity_counts.py --check
```

The parity grades for every Cowork row are in [the Cowork parity audit](../parity/cowork-parity.md); a row is MET only with a named passing test class.

## Degraded states

| State | What the user sees | Operator action |
| --- | --- | --- |
| Signed out | Sign-in gate; no session starts | Sign in per [Accounts](accounts.md) |
| API unreachable | Offline notice; local Code keeps working | Check the API origin and network; Cowork resumes on reconnect |
| Approval window unavailable | Actuating tool refused and audited | Focus the desktop head; retry |
| Browser Use off or Chromium missing | Browser tools not offered | See [browser use](browser-use.md) |
| Isolation unavailable | Session runs unconfined only with the recorded consent chip, or refuses | See [Cowork isolation](cowork-isolation.md) |

## Owner actions

See [v0.16.0 owner actions](v0.16.0-owner-actions.md). Cowork-specific: OQ-400 (Mermaid vendoring licence), OQ-401 (interactive artifact render on the web), OQ-403 (scripts on open vs per-click), OD-07 (live model turn approval). This runbook performs none of them.

## What cannot be verified

- A live omp turn through the gateway (OD-07 not approved; no credential in this environment).
- Live sign-in against Supabase (no publishable key; OQ-301).
- Screen-reader and real-device passes (OQ-178); Windows, macOS and Android runtime behaviour beyond compile checks.
- Hosted deployment (Railway API, Vercel web): built and tested, not deployed by this work.
