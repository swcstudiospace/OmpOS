---
description: "What OmpOS has built, what is partial and what is blocked as of v0.15.0, the roadmap of the release, and what needs the owner."
---

# Status and roadmap

Status: Built (this page summarises the parity audit and is as accurate as the audit)

Last verified: 2026-10-28

Applies to: v0.15.0

The row-by-row truth is the [Cursor parity audit of 2026-10-28](../quality/cursor-parity-audit-2026-10-28.md). This page is the short form. A feature is **Built** when it is implemented, reachable from a head and covered by tests that the project's gate runs. **Partial** means part of it is built or wired; the audit names the missing piece. **Blocked** means something outside the repository is needed.

## What works today

| Area | Status | Honest note |
|---|---|---|
| Desktop app on Linux: Agents window, thread, composer, Kanban, omp terminals, Changes bar, steering, Settings, slash palette | Built | Verified on Linux without a model turn. Windows and macOS not run. |
| The four plugins loop (Ultrathink, Swarm, Substrate, GSD-Core) | Built | Connected against the real omp 18.12.6 without a model turn. |
| White-and-Purple brand, Light default, logo set | Built | Owner sign-off of the look is open. |
| AGPL-3.0-or-later licence and the source offer | Built | Licence of two submodules is open (OQ-102). |
| Web app: shell, rail, thread, composer, Changes, Kanban, Settings, Demo | Partial | Components and the Demo are written with tests; not yet run in a browser against this tree; no public deployment. |
| Model provider catalogue: 103 rows (EU, US, CN, global, local) | Partial | Data and UI built; no row was called with a real credential; 49 rows are only Partial-verified. |
| Slash commands: all 92 Cursor tokens catalogued with a status | Partial | Each token has a handler or an honest reason; many need a live omp to prove. |
| Hooks (`hooks.json`, 21 events) | Partial | Library, bridge, omp extension, service and page built; not run against a live omp. |
| MCP servers from existing files | Partial | Loader, catalog and page built; not run against a live omp. |
| Run modes, `permissions.json`, `sandbox.json` | Partial | Engine and page built; Auto-review's hosted classifier is not available. |
| Checkpoints, worktrees, queue, custom modes, side chats, fork | Partial | Libraries and models with tests; wiring tracked in the audit. |
| Remote environments over ssh | Partial | Never run against a real host. |
| Multi-repo workspaces | Partial | Core built; no live run. |
| Cloud agents on Railway | Partial | Control plane and worker against fakes; no Railway deploy; no live run. |
| GitBook documentation site | Blocked | The repository carries the layout; the site is not published until the owner connects GitBook ([steps](../runbooks/gitbook-publish.md)). |
| Hosted API and web deployment | Blocked | Needs a sign-in authority and the owner's go-ahead for Railway ([Hosting](../runbooks/hosting.md)). |

## What is not built

Cursor's accounts, teams, billing, usage dashboards and hosted services are left out on purpose (OMITTED-BY-DECISION). The browser pane and design mode, automations in a head, the Pets view and mobile terminals are not built or not wired. Cursor features that Cursor itself has retired (inline edit as a separate flow, Memories) are not built.

## The v0.15.0 roadmap

Milestone v0.15.0 ("Cursor completeness, brand and public launch") covers phases 51 to 65: brand and licence (51), repository and community health (52), web head foundation and screens (53 to 55), slash commands and discovery (56), hooks (57), remote environments (58), multi-repo (59), cloud agents (60), settings parity (61), providers (62), feature completion (63), documentation (64) and integration and release (65). The planning records are in `.planning/` in the repository; the changelog is [CHANGELOG.md](https://github.com/swcstudiospace/OmpOS/blob/main/CHANGELOG.md).

## What needs the owner

- Connect GitBook and publish the site ([steps](../runbooks/gitbook-publish.md)).
- Approve or run: a live model turn, Windows and macOS runs, a Railway deploy, the sign-in authority for a public API.
- Decide: the licence of `agent-swarm` and `agent-substrate`, the logo sign-off, the contact address for the code of conduct.

Every unverified external fact and open decision is in the [open questions register](../open-questions.md).
