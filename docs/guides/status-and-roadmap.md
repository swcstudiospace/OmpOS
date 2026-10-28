---
description: "What OmpOS has built, what is partial and what is blocked as of v0.15.0, the roadmap of the release, and what needs the owner."
---

# Status and roadmap

Status: Built (this page summarises the parity audit and is as accurate as the audit; the final tree of v0.15.0 was re-graded on 2026-10-28)

Last verified: 2026-10-28

Applies to: v0.15.0

The row-by-row truth is the [Cursor parity audit of 2026-10-28](../quality/cursor-parity-audit-2026-10-28.md), re-graded against the final tree. This page is the short form. A feature is **Built** when it is implemented, reachable from a head and covered by tests that the project's gate runs. **Partial** means part of it is built or wired; the audit names the missing piece. **Blocked** means something outside the repository is needed.

## What was run, recorded 2026-10-28

| Proof | Result |
|---|---|
| Web Demo deployed (Vercel production alias of project `ompos-web`) | <https://ompos-web.vercel.app>: a static bundle, simulated engine, no backend, no secret. Captured with headless Chrome from the live URL: no console error, no failed request ([evidence](../runbooks/evidence/web-app-2026-10-28/README.md)). |
| Web smoke (`scripts/web-smoke/smoke-demo.mjs`) | 68 axe runs (Light and Dark at 1440, 1024 and 390 px, every screen), 0 violations in the Wave 5 tree. |
| Live ssh | A real `omp acp` 18.12.6 through a loopback `sshd` passed ([evidence](../runbooks/evidence/remote-ssh-live-2026-10-28/README.md)). No prompt turn. |
| Live podman | `OMPOS_LIVE_PODMAN=1` against an alpine image passed in the Wave 4 tree after two defects were fixed. |
| Services | Api.IntegrationTests 1027/1027, Coordination 32 and Sessions.Postgres 58 passed on the final tree against throwaway podman services. |
| Per-project tests on the final tree | Presentation 2303, Native 2012 (2 skipped), AgentLoop 1221, Acp 201, Mcp 197, Plugins 429, Remote 520+ (2 skipped as root), Workspace 364, Automations 386, Web 262+, UI 595, Architecture 46, AgentWorker 153, Contracts 137, Sandbox 329. |
| Desktop on Linux | Xvfb run of the real app: 21 Settings pages render; two layout defects found and fixed ([evidence](../runbooks/evidence/desktop-app-2026-10-28/README.md)). |
| GitHub settings | Applied with `scripts/github/apply-repo-settings.sh`: description, homepage (the Demo URL), 15 topics, discussions on, wiki off, delete branch on merge, private vulnerability reporting, Dependabot security updates, secret scanning with push protection, 22 labels. |

The full `./bootstrap.sh` gate on the final commit was still running when this page was written; its result is recorded in `docs/runbooks/evidence/` and is not claimed here.

## What works today

| Area | Status | Honest note |
|---|---|---|
| Desktop app on Linux: Agents window, thread, composer, Kanban, omp terminals, Changes bar, steering, Settings (21 pages), slash palette | Built | Verified on Linux without a model turn. Windows and macOS not run. |
| The four plugins loop (Ultrathink, Swarm, Substrate, GSD-Core) | Built | Connected against the real omp 18.12.6 without a model turn. |
| White-and-Purple brand, Light default, logo set | Built | Owner sign-off of the look is open. |
| AGPL-3.0-or-later licence and the source offer | Built | Licence of two submodules is open (OQ-102). |
| Web Demo | Built | Deployed; simulated engine; checked in a browser (see above). |
| Web app: shell, rail, thread, composer, Changes, Kanban, Settings (18 pages) | Partial | Tested components over the API; real Models page; Hooks, MCP and Plugins pages read-only; no hosted API (OD-03); no image attach. |
| Model provider catalogue: 103 rows (EU, US, CN, global, local) | Partial | Data, writer and both Models pages built; omp accepts every route in a read-only experiment; no row was called with a real credential; 49 rows are only Partial-verified. |
| Slash commands: 93 rows (all 92 Cursor tokens and `/update-settings`) | Partial | Built 36, Partial 24, Delegated 9, Unavailable 24. Many need a live omp to prove. |
| Built-in skills (10) | Partial | In the desktop palette and Skills page; ssh, container, API-hosted and web sessions carry none; no live turn. |
| Hooks (`hooks.json`, 21 events) | Partial | Wired into desktop sessions, ordering proven with fakes (`EnginePermissionOrderTests`); cloud workers run consented project hooks but the API does not mint the consent claim yet; no live omp ran a hook. |
| MCP (import from four tools, 13 presets) | Partial | Wired; presets are written disabled; no live omp called an MCP tool. |
| Run modes, `permissions.json`, `sandbox.json` | Partial | Applied to every desktop session; Auto-review's hosted classifier is not available; a changed mode reaches omp when the engine is composed again. |
| `@` mentions, context ring, ignore files | Partial | Wired; remote files are not mentionable; no live turn. |
| Image attachments | Partial | Desktop only; model vision unknown; no live turn. |
| Checkpoints and rewind, worktree commands, queue, custom modes, side chats, fork | Partial | Mounted in the desktop window; rewind restores files only. |
| Automations | Partial | Desktop cron scheduler and page; API routes off by default; webhooks refused on the desktop. |
| Remote environments over ssh | Partial | Live loopback run passed; no real network host; remote file explorer, terminal pane, hook files and mentions not wired. |
| Containers (podman) and WSL | Partial | Live podman test passed on Linux; WSL never ran; terminal pane and dev-container attach not wired. |
| Multi-repo workspaces | Partial | Workspace sets and `--add-dir` wired; no live run. |
| Cloud agents on Railway | Partial | Control plane, worker and desktop client against fakes; no Railway deploy; the worker clone bootstrap is missing. |
| GitBook documentation site | Blocked | The repository carries the layout; the site is not published until the owner connects GitBook ([steps](../runbooks/gitbook-publish.md)). |
| Hosted API deployment | Blocked | Needs a sign-in authority and the owner's go-ahead for Railway ([Hosting](../runbooks/hosting.md)). Nothing of OmpOS is deployed there: the Railway project "Ultrathink" holds TimescaleDB, Dragonfly, GreptimeDB and two tailscale services only. |

## What is not built

Cursor's accounts, teams, billing, usage dashboards and hosted services are left out on purpose (OMITTED-BY-DECISION). The browser pane and design mode, the Pets view and mobile terminals are not built or not wired; image attach on the web and Android is not built; automation webhooks on the desktop are refused. Cursor features that Cursor itself has retired (inline edit as a separate flow, Memories) are not built.

## What has never been run

No live model turn since the bounded Phase 11 capture (OD-07); nothing on Windows, macOS or Android; no Railway deploy; no remote host over a real network; no real provider credential.

## The v0.15.0 roadmap

Milestone v0.15.0 ("Cursor completeness, brand and public launch") covers phases 51 to 65: brand and licence (51), repository and community health (52), web head foundation and screens (53 to 55), slash commands and discovery (56), hooks (57), remote environments (58), multi-repo (59), cloud agents (60), settings parity (61), providers (62), feature completion (63), documentation (64) and integration and release (65). The planning records are in `.planning/` in the repository; the changelog is [CHANGELOG.md](https://github.com/swcstudiospace/OmpOS/blob/main/CHANGELOG.md).

## What needs the owner

- **GitBook:** connect the space and publish the site ([steps](../runbooks/gitbook-publish.md), OQ-101).
- **GitHub:** upload the social preview image (`assets/brand/ompos-social-preview.png`) and create the discussion categories; discussions are switched on, categories need the web UI.
- **Notion:** authorise the Notion connection used for project tracking.
- **Live model turn:** approve one (OD-07); every "no live turn" note on these pages waits on it.
- **Provider credentials:** supply test credentials so live validation of providers can run (OQ-71).
- **Windows, macOS and Android runs:** the desktop on Windows and macOS, the Android head on a device or emulator (OQ-74).
- **Sign-in authority for a public API** (OD-03), and the Railway deploy decisions: what may be deployed (OQ-100) and whether Railway permits an agent-driven desktop (OQ-73).
- **Licence of `agent-swarm` and `agent-substrate`** (OQ-102).
- **Trademark clearance** of the name "OmpOS" (none has been made, see [Licensing](../licensing.md)) and sign-off of the logo.
- **Code of conduct:** a contact for conduct reports beyond GitHub's private reporting.

Every unverified external fact and open decision is in the [open questions register](../open-questions.md).
