# Changelog

All notable changes to OmpOS are documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Versions before 0.15.0 are milestones of the internal roadmap (`.planning/ROADMAP.md`). They overlapped in time, and only 0.9.1 was closed formally: the date of each other version is the day of its last recorded work or audit. Phase numbers refer to that roadmap.

## [Unreleased]

## [0.15.0] - 2026-10-28

Cursor completeness, brand and public launch (Phases 51 to 65). Items marked partial are built and unit-tested but not exercised live: no live model turn, Windows or macOS run, or Railway deploy has happened, and the head wiring of several Wave 2 features is recorded in `docs/quality/cursor-parity-audit-2026-10-28.md`.

### Added

- White-and-Purple brand: a logo set, wordmark lockups, banner (1280 by 320), social preview (1280 by 640), favicons, desktop and Android icons under `assets/brand/` with a reproducible build script and a check, in the bundled DM Sans, Anybody and Space Mono faces (Phase 51).
- `AGPL-3.0-or-later` licence: `LICENSE`, `NOTICE`, generated `THIRD-PARTY-NOTICES.md`, REUSE data, an SPDX expression in package metadata and a licence check script; an anonymous `GET /source` that names the licence, the source URL and the running revision, with a source link in the web footer and the About dialogs (Phase 51).
- Community health and release files: `SECURITY.md`, `SUPPORT.md`, `CODE_OF_CONDUCT.md`, issue forms, a pull request template, `CODEOWNERS`, Dependabot, labels, release notes categories, a release workflow that reads this changelog, and a changelog check script (Phase 52).
- Web head foundation: tokens, self-hosted woff2 fonts, theme boot, a responsive shell with top bar, agent rail and footer, no Bootstrap (Phase 53).
- API surface for the web screens: catalogue, board, user event stream, stages, context, steering, config options, commands, change review, session lifecycle and workspace routes (contract version 1.3.0) (Phase 53).
- Web screens: agent rail, thread, composer with model, mode and thinking chips and a slash palette, tool approvals, Changes bar and review, Kanban board, Settings dialog and a terminal view; and a Demo build with a simulated engine, labelled on screen, that runs without a backend (Phases 54 and 55; partial: not run in a browser against this tree, no public deployment).
- All 92 Cursor slash tokens with handler kinds, statuses and reasons, a resolver that merges engine commands, custom command files, skills and subagents, discovery over six directories at two scopes, and a generated `docs/reference/slash-commands.md` (Phase 56).
- Cursor-compatible hooks: a 21-event catalogue, `hooks.json` loaders, a dispatcher, a shell hook runner, per-version consent, a loopback bridge, an omp extension that enforces blocking events (76 node tests pass), an ACP tap, a hooks service and a Hooks page (Phase 57; partial: not run against a live omp session).
- Remote environments over `ssh`: profiles, `ssh -G` resolution, preflight diagnosis, launch rewrite, reconnect, remote file system, remote convention discovery, remote terminals, a profile store, a Remote environments page and a New session target picker (Phase 58; partial: never run against a real host).
- Multi-repository workspaces: validated workspace sets, omp launched with every root, one git worktree per git root, a multi-root explorer and `.code-workspace` import and export (Phase 59; partial).
- Cloud agents on Railway: an agent worker with a JWT-authenticated WebSocket bridge to `omp acp`, a Railway GraphQL client, a static pool client, the API control plane (create, prompt, stop, wake, delete, secrets, limits, pull requests) with a PostgreSQL store, and a cloud agents list and form in the heads (Phase 60; partial: against fakes, `Cloud:Enabled` is false by default, no deploy).
- Provider catalogue of 103 EU, US, CN, global and local providers, aggregators, coding plans and local runtimes as JSON data, a writer for every omp API style, region and kind filters, coding-plan rule cards, `GET /providers` and credential routes, and a generated `docs/reference/providers.md` (Phase 62; partial: no row was called with a real credential).
- Run modes (Auto-review, Allowlist, Run Everything), `permissions.json`, `sandbox.json` readers and writers, a decision engine and a Run mode page (Phase 63; partial; Auto-review's hosted classifier is not available).
- `@` mentions with a fuzzy picker, Cursor-style ignore files (gitignore syntax) for the agent, Tab and mentions, a conversation search index, a context ring and ACP prompt content blocks (Phase 63; partial).
- MCP configuration import from OmpOS, Cursor, Claude Code and VS Code files, a server catalog and an MCP settings page (Phase 63; partial).
- File checkpoints with rewind and restore, a worktree command model, a message queue, custom modes, side chats and fork, as libraries and presentation models (Phase 63; partial).
- Documentation in GitBook's Git Sync layout: `.gitbook.yaml`, `docs/SUMMARY.md`, guides for getting started, the desktop and web apps, concepts, providers, settings and troubleshooting, an architecture diagram, an HTTP API reference, a publish runbook, a documentation check script (`scripts/ci/check-docs.sh`) and a refreshed Cursor parity audit (`docs/quality/cursor-parity-audit-2026-10-28.md`) (Phase 64).

### Changed

- The Light theme is the default in the desktop and web heads; Dark and System remain choices (Phase 51).
- The README is rewritten as the GitHub landing with a feature status table and the licence block (Phase 64).
- `docs/architecture.md` now covers the web shell, remote environments, cloud agents and hooks; `docs/handoff.md` is marked as the historical v0.9.1 handoff (Phase 64).
- ADR-0013 decision 4 (no shell, closed stdin for plugin hooks) is amended by ADR-0016 so that Cursor hook commands run as written (Phase 57).
- Licence statements in `docs/plugins.md`, ADR-0013, the GSD-Core plugin README and the deferred-and-blocked record now say OmpOS is `AGPL-3.0-or-later`; the licences of `agent-swarm` and `agent-substrate` stay open (Phase 64).

### Fixed

- A hook run log is created with owner-only permissions because it holds prompts and output excerpts (Phase 57).
- The web head no longer ships Bootstrap; the unused library and its annotation are removed (Phase 53).

### Security

- AGPL section 13 source offer for network use, anonymous and without sign-in (Phase 51).
- Cloud agent worker tokens are short-lived and verified by the worker; secrets are write-only through the API (Phase 60).
- Provider key writes, custom endpoints and live tests are off by default in the API and need explicit operator switches; the live test connects to public addresses only (Phase 62).

## [0.14.0] - 2026-10-27

Agent loop on the omp engine and the default Cursor-style Agent Panel (Phases 44 to 50).

### Added

- `claude-ultrathink`, `agent-swarm` and `agent-substrate` as pinned plugin submodules, and `plugins/gsd-core-omp`, an omp plugin whose node test suite is part of `bootstrap.sh` and CI (Phase 44).
- A launch contract that starts every omp process the application runs (the engine session and each omp terminal) with the four plugins loaded once, with a connection report and fix text when a plugin is missing (Phases 44 and 48).
- Agent loop stage sources: stage events and task context in a session catalogue that survives a restart, and visible degradation when a plugin is missing or failing (Phase 45).
- The default Agent Panel: a session rail, thread, composer with model, mode and thinking chips, several concurrent sessions and a pipeline strip (Phase 46).
- The omp slash command palette in the composer, listing the commands omp advertises (Phase 49).
- A Changes bar with Keep, Undo, Review, Keep all and Undo all, feeding the Diff review tool (Phase 49).
- Live steering: a running session takes a steering message from the composer or its Kanban card through omp's `_session/steering`, with truthful delivery reporting (Phase 49).
- Cursor-style visual parity: static Greptile typefaces (DM Sans, Anybody, Space Mono), a token set and control library, 32 px targets, compact density, and screenshots in Dark and Light (Phase 50).

### Changed

- The Agent Panel is the single default surface: the idea of a second agent-loop surface was superseded on 2026-10-26.
- The Agent Panel on the web head (Phase 47) stays deferred.

### Fixed

- An editor tab-switch deadlock in the TextMate integration (Phase 50).
- The GSD-Core plugin's `bin` scripts, which `.gitignore` hid, are now tracked (Phase 44).

## [0.13.0] - 2026-10-27

Cursor 1:1 workspace and provider platform (Phases 36 to 43).

### Added

- The shared contracts every surface consumes: one token set, command record, status model, provider record, secret type, and versioned herdr and omp adapter contracts with recorded fakes (Phase 36).
- Session replay without duplicates and a real Linux local PTY terminal backend (Phase 37).
- A slash command registry and chat window states, with the catalogue checked against a recorded Cursor reference (Phase 38).
- A Kanban board with one card per chat, live status and event-driven changes, and an Agents tab (Phase 39).
- A Cursor-shaped Settings window with eleven pages, including Models, Keyboard Shortcuts and Network, over omp's `models.yml` (Phase 40).
- An omp configuration library and an owner-only provider key store (Phase 40).
- A model provider catalogue in which a provider is a registry entry, with error mapping for quota exhaustion and plan expiry (Phase 40).
- A composer model picker over omp's ACP model option (Phase 40).
- A Pets companion state model, preferences and text equivalents (Phase 41).
- A cloud session host over a fake Railway environment client, as a library (Phase 42).

### Changed

- The static Greptile faces of Phase 50 (0.14.0) replaced the variable font files bundled in Phase 36.
- No product project references the herdr adapter yet; sessions re-attach through the session registry. A Pets view, live cloud runs and mobile and web terminals (Phase 43) are partial or deferred in the parity audit.

## [0.12.0] - 2026-10-27

Cursor parity and quality bar (Phases 31 to 35).

### Added

- Plugin manifests with API version, capabilities and permissions, a host that loads, quarantines or rejects plugins without crashing, and a hook runner that needs consent for the exact command (Phase 31).
- A hooks view with enable, disable, reorder and explicit consent (Phase 31).
- Automations guards and engine (chain depth, fail-closed store, rate and runtime limits) and a presentation model (Phase 32).
- An omp terminal launcher, a shared plugin and MCP registry, and a host-owned MCP gateway with grant enforcement (Phase 33).
- A documented suppression audit and an automated web smoke with axe-core (Phase 35).
- A Cursor parity audit of 221 rows (98 met, 16 met live, 70 partial, 20 deferred, 3 not started, 14 omitted by decision), recorded at commit `d75fd88` (Phase 35).

### Changed

- Phase 34 (remote development) stayed blocked: no verified remote omp endpoint exists (OD-06), and no stub ships.
- No head loads a plugin manifest, runs a hook or composes the automations engine yet (OQ-84).
- Decision records exist for plugin isolation and the MCP client (Phase 35).

### Security

- A plugin quarantine fails closed and lifts only with a recorded reason (Phase 31).
- One pinned, digest-checked secret scan runs in `bootstrap.sh` and CI, with a fingerprint allow-list for fake test tokens (Phase 35).

## [0.11.0] - 2026-10-22

ADE harness (Phases 26 to 30).

### Added

- Side chats with transcript search, and an Agents window with a status filter (Phase 26).
- A per-run git worktree manager and opt-in run worktrees (Phase 26).
- A hosted run contract with run endpoints (Phase 26).
- An engine-side permission resolver for in-process ACP approvals (Phase 26).
- A code editor on AvaloniaEdit, a workspace tree, per-hunk diff review and inline edit logic (Phase 27).
- An integrated terminal panel over a PTY runner (Phase 28).
- Project rules, skills and modes, codebase search and export logic (Phases 28 and 29).

### Changed

- The native shell wires the diff, inline edit, explorer, rules, MCP tools, search, completion, export, editor and terminal views.
- Phase 30 (VS Code extension compatibility, admin and team controls) is deferred in writing until its criteria hold.

### Fixed

- The sandbox runner freezes the process tree before killing it.

### Security

- Session ownership is enforced on every session endpoint, with remote control authentication tests (Phase 26).

## [0.10.0] - 2026-10-26

Cross-platform UI on Avalonia (Phases 20 to 25.9).

### Added

- A shared Avalonia native shell with a framework-neutral presentation layer, an accessibility baseline and plugin host contracts (Phase 20).
- Session chat, tool approvals and steering on the shared presentation layer, with approvals for edit, delete, move, execute, read and search (Phase 21).
- The Blazor WebAssembly web head on the shared presentation layer, with an axe-core smoke in `bootstrap.sh` and CI (Phase 23).
- An Android head, `apps/OmpOS.Android`, built for `net10.0-android36.0` with warnings as errors, and a CI job that builds it (Phase 24).
- 13 Avalonia parity behaviours that were unrendered, each with a native test (Phase 25).
- A codebase quality audit register of 130 findings and eight fix phases (Phases 25.1 to 25.9).
- A vulnerable-package audit that also covers the Android head (Phase 24).

### Changed

- Every CI job runs on the Omega CI runner pool, MSBuild is sized to the runner's 2 vCPU budget, and two test modules run at a time.
- Phase 22 (Linux, Windows and macOS desktop heads) was skipped by owner decision on 2026-10-24: the X11 and XWayland starts are recorded; the Windows and Mac runners are external.
- iOS and macOS builds stay blocked on a Mac channel.

### Removed

- The MAUI head (`apps/OmpOS.App`), after every one of its parity rows had a passing Avalonia test (Phase 25).

### Fixed

- The web head sends prompts and streams only to the configured API origin, and its SSE client stays bounded under abuse (Phase 25.2).
- Every read of external bytes has a named cap and fails with a bounded error when oversized (Phase 25.3).
- Failures that were swallowed are logged or surfaced to the caller (Phase 25.5).
- The UI heads honour reduced motion, label images and keep the error dismiss keyboard-operable (Phase 25.7).
- Tests assert what their names claim, and settlement tests wait deterministically instead of sleeping (Phase 25.8).
- Every suppression and skip marker has a recorded decision (Phase 25.9).

### Security

- Build and deploy inputs are verified or pinned: installers checked before execution, CI actions and runners pinned, images pinned by digest (Phase 25.4).
- Bearer tokens reach only https or loopback origins, compose files in shared temporary directories are owner-only, and repository setup steps need per-repository consent (Phase 25.6).

## [0.9.2] - 2026-10-21

Hardening and hosted API (Phases 1 to 19 of the 0.9.2 roadmap; audit status `tech_debt`).

### Added

- A PostgreSQL session log shared by the API and the worker, read back gapless across processes (Phase 7).
- Permission requests, parked ACP approvals and a session approval service, with decisions routed to the pending request (Phase 10).
- An OIDC bearer boundary for the API.
- Verified agentOS network policy with fail-closed vendor operations (Phase 4).
- A per-sandbox podman secret credential, revoked at teardown (Phase 6; other providers stay blocked).
- Podman build files for the API image and a pinned omp runtime inside it.
- Frozen contract fixtures of omp 18.12.5 `session/update` variants, from a bounded live capture (Phase 11).
- A build-time content-security-policy `connect-src` from one validated API origin, an icon-only send control and the skip link first in tab order.
- Readiness, exporter, prompt logging and rate-limit seams.
- A throwaway PostgreSQL service for the CI test runs.

### Changed

- Session work runs in the API on Railway, replacing Temporal; the Temporal worker and its replay phase were superseded (Phases 8 and 9).
- The MAUI-specific phases were superseded by the Avalonia program of 0.10.0 (Phases 15 and 16).
- Phases 6, 12, 13 and 14 stay blocked on external criteria (paused Railway deploys, an unset production API origin and no verified vendor secret-store operation).

### Fixed

- Correlation ids, resume positions, owner ids, container ids and session ids are validated.
- SSE ends on id-less or duplicate terminal frames.
- Per-sandbox credential and host-secret flags are enforced.
- The SDK archive digest is verified before install, and the CI workflows profile boots.
- Unregistered permission requests settle as denied at the registration bound.

### Security

- Production refuses the in-memory adapter and the in-memory session store, and the API verifies bearer tokens at its boundary.

### Removed

- The Temporal worker and its Temporal Cloud options (superseded 2026-10-21).

## [0.9.1] - 2026-10-21

Foundation through verified handoff (Phases 1 to 5).

### Added

- A governed 15-project .NET 10 monorepo with analyzers, warnings as errors, deterministic builds, central package management with committed lock files, and `bootstrap.sh` as the single gate command (Phase 1).
- `OmpOS.Contracts`, the shared contract surface, with kind converters and a source-generated JSON context (Phase 1).
- Layer-direction architecture tests with live-gate proofs, and a CI pipeline that mirrors `bootstrap.sh` (Phase 1).
- A validated podman compose stack and an eight-file documentation skeleton with the first architecture decision records (Phase 1).
- The catch-all prompt parser, ACP v1 and omp RPC clients, an in-memory adapter, session orchestration and a Server-Sent Events API (Phase 2).
- A sandbox provider abstraction with fake and local podman-compose providers, and durable workflows through a direct runner (Phase 3).
- A shared Razor class library chat surface with a Generative UI renderer, a Blazor WebAssembly web head and a MAUI Blazor Hybrid head (Phase 4).
- A clean-clone bootstrap record, the handoff document and the open-questions register (Phase 5).

[Unreleased]: https://github.com/swcstudiospace/OmpOS/compare/v0.15.0...HEAD
[0.15.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.14.0...v0.15.0
[0.14.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.13.0...v0.14.0
[0.13.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.12.0...v0.13.0
[0.12.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.11.0...v0.12.0
[0.11.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.10.0...v0.11.0
[0.10.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.9.2...v0.10.0
[0.9.2]: https://github.com/swcstudiospace/OmpOS/compare/v0.9.1...v0.9.2
[0.9.1]: https://github.com/swcstudiospace/OmpOS/releases/tag/v0.9.1
