# Changelog

All notable changes to Autonogrammer are documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Versions before 0.15.0 are milestones of the internal roadmap (`.planning/ROADMAP.md`). They overlapped in time, and only 0.9.1 was closed formally: the date of each other version is the day of its last recorded work or audit. Phase numbers refer to that roadmap.

## [Unreleased]

### Added

- Hosted web app (Phases 66.2 to 66.4; ADR-0021): the web head can be built as the real application with no Demo code (`scripts/vercel/build-web.sh`, asserted by `scripts/vercel/check-no-demo.sh`, which has an `--expect-demo` positive control for the Demo bundle) and used against a hosted API. Nothing is deployed by this change: the public URL still serves the Demo until the owner approves the switch (`docs/runbooks/deploy-and-rollback.md`).
- Sign-in for one operator: `Api:Auth:Mode=OwnerToken` accepts a bearer token whose SHA-256 digest is listed in `Api:Auth:OwnerToken:Sha256` (constant-time comparison, a failure throttle that answers 429 with `Retry-After`, rotation by listing two digests); `Api:Auth:Mode=Oidc` stays the default. `GET /auth/config` (anonymous, `no-store`) gives a client the mode, authority and audience. The web head has a connect screen, an auth gate and a sign-out button; the token is held in memory only and a 401 ends the session.
- Access control for hosted use: `Api:Auth:AllowedSubjects` and `Api:Auth:AllowAnyAuthenticatedUser`, a 403 `subject_not_allowed` problem, and a per-subject partition on all six rate-limit policies.
- API hosting hardening (`Hosting:*`): forwarded headers (`None`, `TrustAllSingleHop`, `KnownNetworks`), HSTS, security headers with a strict content-security policy for API responses, Kestrel limits, request timeouts (a 504 problem; event streams are exempt), a readiness flip on shutdown, `GET /health/startup`, SSE keep-alive comments and `retry: 2000` on shutdown, JSON logs and an OpenTelemetry resource.
- Engine isolation on the direct-run path: `Harness:Acp:Environment` (an allow-list outside Development), a 0700 workspace per session under `Harness:Acp:WorkspaceRoot`, a non-dumpable API process (`Hosting:RestrictProcessInspection`), and `Harness:Acp:Approval` (`Kinds`, `GrantableTools`, `DecisionTimeoutSeconds`; deny everything by default) so the tool-approval endpoints can grant a tool there.
- Web shell: routes (`/`, `/kanban`, `/sessions/{id}`, `/settings[/{page}]`, `/about`, `/connect`), a title per view, a route announcer and route focus, an error boundary, a not-found page and an About page; a transcript that renders the latest 50 turns with a "Show earlier messages" button and batched announcements; an offline app shell (a service worker that never caches cross-origin requests, `Authorization`, event streams or `/auth/*`); loading and `<noscript>` text.
- Accessibility (target: WCAG 2.2 AA, Phase 66.3): forced-colors and `prefers-contrast` styles, scroll padding, 24 px targets, reflow at 560 and 360 px, text spacing, persistent live regions, heading fixes, and scripted checks (`scripts/web-smoke/a11y/`: routes, keyboard walk, media emulation, reflow, text spacing, target size, states, connect) that the web smoke runs. `docs/accessibility/` holds the conformance statement (partially conformant), a 55-row WCAG table, a keyboard script, a screen-reader plan that has not been run, and the known issues.
- Hosted end-to-end harness (`scripts/hosted/`): a scripted OpenAI-compatible model server, an isolated omp home, `e2e-api.mjs`, `e2e-browser.mjs` and `run-local.sh`, which drive the real `omp acp`.
- Supply chain and CI: a generated SPDX SBOM (`docs/legal/sbom.spdx.json`, `check-sbom.sh`), base images pinned by tag and digest with a `HEALTHCHECK`, `scripts/ci/scan-images.sh`, `check-skip-markers.sh` (a skip marker needs a SUP row), an OpenAPI breaking-change check, a snapshot script for the omp dependencies, job timeouts and least permissions in `ci.yml`, an artifacts job in `release.yml`, and CodeQL and OpenSSF Scorecard workflows (SHA-pinned; never run).
- Operations and security documents: the hosted threat model, the data inventory and retention, SLOs and alerting, incident response, backup and restore, deploy and rollback, Railway Config as Code files in `deploy/railway/`, ADR-0021, and the record of the integration of the rename line (`docs/runbooks/integration-autonogrammer-2026-10-29.md`).
- Brand: the wordmark in two layouts with "Auto" in the text colour and "nogrammer" in the accent colour, the six SVG sources and the assets regenerated for the name Autonogrammer.

### Changed

<!-- rebrand:keep-start -->
- The product is renamed from OmpOS to Autonogrammer (milestone v0.16.0, Phase 66, mechanical pass). Renamed: the product name and display strings, the C# namespaces and assembly names, the projects and files (`Autonogrammer.slnx`, `Autonogrammer.Linux.slnf`, `src/Autonogrammer.*`, `services/Autonogrammer.*`, `apps/Autonogrammer.*`, `tests/Autonogrammer.*`), the environment variables (`OMPOS_*` to `AUTONOGRAMMER_*`), the HTTP header `X-Autonogrammer-Contract-Version`, the convention directory `.autonogrammer`, the PostgreSQL schema `autonogrammer`, the Dragonfly key prefix `autonogrammer:`, the plugin directory `plugins/autonogrammer-hooks-omp` and the Android `ApplicationId` (`com.swcstudiospace.autonogrammer`). The external omp engine names (the `omp` binary, `omp acp`, `~/.omp`, `OMP_*`, `models.yml`) and the first-party engine identifiers such as `OmpLauncher` are kept. The names of the existing GitHub, Vercel and Railway services, and the URLs that contain them, are unchanged until the owner renames those services.
<!-- rebrand:keep-end -->
- Migration: user data is to be migrated once from the old names (data directory, convention directories, state); that runtime migration is planned as a later unit of Phase 66 and is not part of this change, so the renamed tree does not read the old locations yet. The Android `ApplicationId` is a new identity, so an installed app does not upgrade to it.
<!-- rebrand:keep-start -->
- Owner actions after the rename: rename the GitHub repository, the Vercel project and the Railway services and then update the URLs; rename the `OMPOS_*` variables to `AUTONOGRAMMER_*` in Vercel, Railway, GitHub secrets and local `.env` files; recreate or migrate the podman containers, volumes, secrets and image, the PostgreSQL schema and its advisory-lock key and the Dragonfly keys, and deploy the API and the worker together; change the three plugin submodules that may read the old names in their own repositories; confirm the legal wording and the trademark.
<!-- rebrand:keep-end -->
- Breaking: an OIDC deployment outside Development must set `Api:Auth:AllowedSubjects` or `Api:Auth:AllowAnyAuthenticatedUser=true`; startup fails otherwise.
- Outside Development the engine process starts with an allow-listed environment instead of the API's whole environment; `Harness:Acp:Environment:Mode=Inherit` there needs `Harness:Acp:AllowInheritedEnvironment=true`.
- `scripts/web-smoke/size-budget.json` is now measured on a real publish of the web head (2026-10-29: 12.4 MB raw, 4.8 MB gzip, 3.8 MB Brotli) with 10 percent headroom, instead of a provisional ceiling taken from the Avalonia spike; the budget check no longer reads a comment in a stylesheet as an import.
- Breaking: outside Development the API stops at start when the non-dumpable setting (`Hosting:RestrictProcessInspection`, on by default) cannot be applied, instead of continuing; `Api:Cors:Origins` entries must be `scheme://host[:port]` and are checked at start.
- CI runs on GitHub-hosted runners (`ubuntu-24.04`, `windows-2022`, `macos-14`) instead of the Omega self-hosted pool. The Linux job installs rootful podman and Chrome; desktop jobs publish the Avalonia head on Linux, Windows and macOS. Docker is still not used.
- GitHub-hosted restore uses a private SDK 10.0.402. The runner image's runtime 10.0.14 otherwise changes `Microsoft.NET.Sdk.WebAssembly.Pack` from 10.0.13 to 10.0.14 and locked restore fails.

### Fixed

- Direct-run tool approvals: the hosted adapter denied every request and no tool was ever grantable, so every `allow` answered 403; the new `Harness:Acp:Approval` options set the kinds that reach the endpoints and the tools an allow may grant.
- First load moved focus to the heading and skipped the skip link (WCAG 2.4.1); focus now moves to the heading only when the page changes after the first render (`RouteFocus`).
- Markdown from an agent could carry attributes and media links through the generic-attributes and media-link extensions; the renderer no longer enables them and judges every link it renders, with no limit on their number.
- A refused sign-in for rate limiting looked like a wrong token; the connect screen now says to wait. The connect and auth-config requests end after 15 seconds instead of waiting for ever, and a late refusal of an old token no longer ends a newer session.
- Request timeouts ran before the exception handler, so a timed-out request could be reported as a client cancellation; they now run after it and still answer a 504 problem.
- The route announcer said nothing, or the previous title, after a tab, an agent in the rail or Settings: the browser reports the new address after the render that shows the new title. The workbench now arms the announcer before it changes the view.
- Scrollable code, tool output and file-edit text took keyboard focus without a name or a role; they are now named groups. Kanban card titles, the conversation title and the slash palette wrap instead of being clamped or cut with an ellipsis, so wider text spacing hides none of their words (WCAG 1.4.12). The composer card draws the same two-pixel focus ring as every control.
- After every turn of a real omp session the transcript showed a disclosure "Unsupported content (ContextUsageEvent)": omp reports the context window use at the end of each turn, the API stored it under the name of its CLR type and the web head rendered every unknown kind. The event is now stored as `context_usage` with its two numbers and the transcript does not render it. Found by the local run against the real omp, not by a test.
- Choosing an agent in the rail called the focus before the render that shows its thread, so the focus landed on the thread about to be replaced and fell to the page; it now follows the render. The "Jump to latest" control was removed while it had the keyboard focus (focusing it scrolled the view to the end), which dropped the focus to the page and ended the Tab walk before the composer and the footer; it now stays while it has the focus and hands the focus to the transcript when it is used.
- On a short screen the agents drawer was as tall as its content (a fixed box with `height: auto` under `align-self: start`), so the last agents and the Settings button lay below the screen where neither a pointer nor the keyboard could reach them; it now has the height between the top bar and the footer and scrolls inside it.
- axe reported "needs review" for `aria-controls` next to `aria-haspopup`; the model and option chips no longer set `aria-controls`, and the composer textarea no longer sets `aria-haspopup`.
- Under forced colours a primary button was drawn with a highlight fill, and a palette whose highlight is not opaque made the browser draw a canvas-coloured plate behind its label; it now takes a thicker highlight border and no fill. Agent titles in the rail wrap instead of being cut with an ellipsis.
- The scripted accessibility checks had false findings of their own, found by their first runs and removed: visually hidden live regions read as clipped text, a card around a field was not accepted as its focus ring and its boundary, a focus scroll under the sticky bar read as an undersized target, a control that exists in one walk direction only read as a different Shift+Tab path, Back and Forward assumed an agent row, and the size budget read a comment as an import.
- A theme chosen in the theme switch before the saved theme had finished loading was overwritten by the saved one, so the switch showed the old theme as pressed while the new one had been saved; the person's choice now wins. Found by the full gate on a loaded host, as a failing test.
- The close control of the selected agent row covered the end of its relative time ("ago") when the title was short, and a click on that part of the time reached the control; the title line of the selected row now leaves the room of the control. Found by looking at a screenshot of the real bundle under forced colours.
- The scripted accessibility checks give a state one more attempt from the start page before they call it unreachable, and report a second-attempt success as a warning; a navigation lost on a loaded host stopped a whole group once.
- Two gates or test runs on one host removed each other's throwaway TimescaleDB and Dragonfly containers: `scripts/test-services.sh up` began with `down` on fixed container names, so the second run deleted the first run's containers in the middle of its tests and every test that needs a server failed with a refused connection (67 of 69 failures of a full gate run). `AUTONOGRAMMER_TEST_SERVICES_NAME` now gives a run its own pair (`autonogrammer-test-postgres-<name>`, `autonogrammer-test-dragonfly-<name>`), and `./bootstrap.sh` sets it to `gate-<pid>` unless the caller names one; unset, the script keeps the plain names.
- The remote shell test helper no longer fails when a script that refuses its input has exited before the helper closes the input stream, and the UI tests wait up to ten seconds, not one, for a render that is on its way on a loaded host.
- A Vercel build from the repository of the real web head (`vercel.ts`, no `AUTONOGRAMMER_DEMO`) published the loopback `Api:BaseUrl` of the checked-in `appsettings.json` while the content-security policy allowed only the real origin, so the app could not reach its API. It now runs `scripts/vercel/finish-web-bundle.sh` after the publish, as `scripts/vercel/build-web.sh` does: it writes the origin, refreshes the service-worker asset manifest, verifies every asset against it and refuses a bundle with Demo code.
- The service worker of the real bundle could not install after the API origin was written: it pre-caches `appsettings.json` with the hash in `service-worker-assets.js`, which the rewrite had left stale, so `cache.addAll` rejected and the offline shell and the update notice were lost. `scripts/vercel/refresh-web-manifest.mjs` re-hashes the file, changes the manifest version (the name of the worker's cache) and the first line of `service-worker.js`, and has a `--verify` mode. It replaces only those literals, so the SDK's own spelling of the manifest (`\u002B` for `+` in some SDK versions) stays as published.
- The SBOM freshness check failed on a runner that restores only `Autonogrammer.Linux.slnf`, because the NuGet cache then lacks Android-only packages and their licences became `NOASSERTION`; when the cache has no nuspec for a package version, its licence is now taken from the committed SBOM for the same package name and version, never carried to another version, and a nuspec that is present always wins (even one that declares no licence), so a licence a package stops declaring is still reported.
- The OpenAPI breaking-change check raised `RecursionError` on a schema that refers to itself, directly or through another, and passed a response whose body had been removed; it now ends a descent at a schema pair it is already comparing and reports the removed body.
- The API and worker image builds ignored a failed cleanup after the omp snapshot (the commands were joined with `;`); a failure now stops the build.
- The web smoke's in-app route check failed on a loaded host: the address changed but the render had not finished, so the title was still the boot title and the shell controls were absent, and every step failed. A step that lands on the boot shell now restarts the whole sequence once after a reload, so Back and Forward are still checked against a real history, and a second landing is an error rather than a pass with nothing checked.

### Security

- Owner tokens are never stored or logged: the API holds a digest, compares in constant time and throttles failures; the browser keeps the token in memory only.
- The engine environment allow-list, a per-session workspace, a non-dumpable API process, security headers, request timeouts and Kestrel limits narrow what a hosted run exposes; the threat model and its open questions are in `docs/security/hosted-threat-model.md`.
- The API image no longer makes `/app` writable by the engine's user (the application files are read-only for it) and starts with `DOTNET_EnableDiagnostics=0`, which closes the runtime's diagnostics socket.
- A warning at start when `Hosting:ForwardedHeaders:Mode` is `None` outside Development: behind a proxy the API would see the proxy's address for every client, which makes the per-client rate limits and sign-in throttle useless.

## [0.15.0] - 2026-10-28

Cursor completeness, brand and public launch (Phases 51 to 65). No live model turn, no Windows, macOS or Android run and no Railway deploy of the API or of a cloud agent had happened when the release was cut (recorded 2026-10-28); after the release a staged private worker was deployed to Railway and exercised (see the Cloud agents entry below); items marked partial are built and tested but not exercised live. What each feature has behind it (built, wired into a head, proven by a named test or evidence folder) is recorded row by row in `docs/quality/cursor-parity-audit-2026-10-28.md`.

### Added

- White-and-Purple brand: a logo set, wordmark lockups, banner (1280 by 320), social preview (1280 by 640), favicons, desktop and Android icons under `assets/brand/` with a reproducible build script and a check, in the bundled DM Sans, Anybody and Space Mono faces (Phase 51).
- `AGPL-3.0-or-later` licence: `LICENSE`, `NOTICE`, generated `THIRD-PARTY-NOTICES.md`, REUSE data, an SPDX expression in package metadata and a licence check script; an anonymous `GET /source` that names the licence, the source URL and the running revision, with a source link in the web footer and the About dialogs (Phase 51).
- Community health and release files: `SECURITY.md`, `SUPPORT.md`, `CODE_OF_CONDUCT.md`, issue forms, a pull request template, `CODEOWNERS`, Dependabot, labels, release notes categories, a release workflow that reads this changelog, and a changelog check script (Phase 52).
- Web head foundation: tokens, self-hosted woff2 fonts, theme boot, a responsive shell with top bar, agent rail and footer, no Bootstrap (Phase 53).
- API surface for the web screens: catalogue, board, user event stream, stages, context, steering, config options, commands, change review, session lifecycle and workspace routes (contract version 1.3.0) (Phase 53).
- Web screens: agent rail, thread, composer with model, mode and thinking chips and a slash palette, tool approvals, Changes bar and review, Kanban board, Settings dialog and a terminal view (Phases 54 and 55). A polished White-and-Purple pass fixed the Kanban scroll, the duplicate workspace label, the approval card, the slash palette and the phone top bar (Wave 5). A smoke of 68 axe runs (Light and Dark at 1440, 1024 and 390 px across every screen) found 0 violations in the Wave 5 tree (`scripts/web-smoke/smoke-demo.mjs`).
- Web Settings with 18 pages (General, Runtime, Agents, Run mode, Models, Tab, Rules, Skills, Commands, Hooks, MCP, Plugins, Workspaces, Remote environments, Cloud agents, Privacy, Network, Keyboard): Models is a real page over `GET /providers` with the provider directory by region and kind, plan cards with the rule tick box and a write-only key field; Runtime, Rules, Skills, Commands, Hooks, MCP and Plugins are read-only lists over API routes; Agents, Run mode, Workspaces, Remote environments and Cloud agents are information pages that point to the desktop app (partial: Display, Worktrees and Ignore are desktop pages only).
- The web Demo is deployed at https://ompos-web.vercel.app (recorded 2026-10-28): a static build with a simulated engine, no backend and no secret, labelled on screen. `scripts/web-demo/capture.mjs` captured it with headless Chrome from the live URL with no console error and no failed request; screenshots and the method are in `docs/runbooks/evidence/web-app-2026-10-28/`. The Demo answers the provider directory (103 rows), the settings routes and the custom-endpoint rules in the browser (`Demo/DemoProviders*`, `DemoWorkspaceSettings`).
- Desktop screenshots of the real app on Linux (Light theme, Xvfb, no model turn) in `docs/runbooks/evidence/desktop-app-2026-10-28/`, with the two defects the smoke found and fixed: a clipped environment picker header and a Run mode page that opened scrolled down.
- All 92 Cursor slash tokens plus OmpOS's `/update-settings` (93 rows: Built 36, Partial 24, Delegated 9, Unavailable 24), with handler kinds, statuses and reasons, a resolver that merges engine commands, custom command files, skills and subagents, discovery over six directories at two scopes, and a generated `docs/reference/slash-commands.md` (Phase 56). Rewind, fork, side chat, worktree commands, copy, about, workspace sets, `/rules`, `/skills`, `/commands`, `/plugins` and `/run-everything` run as native actions in the desktop window (Wave 3).
- Built-in skills: ten skills (`create-hook`, `create-rule`, `create-skill`, `create-subagent`, `migrate-to-skills`, `review`, `review-security`, `split-to-prs`, `agent-review`, `update-settings`) under `assets/skills/`, discovered by the desktop slash palette and Skills page with scope Built-in; a project skill of the same name takes over (`BuiltinSkillsDesktopTests`). API-hosted sessions carry none.
- Cursor-compatible hooks: a 21-event catalogue, `hooks.json` loaders, a dispatcher, a shell hook runner, per-version consent, a loopback bridge, an omp extension that enforces blocking events (76 node tests pass), an ACP tap, a hooks service and a Hooks page (Phase 57). The desktop head starts one hooks session per omp session and passes the gate to the engine: a hook deny rejects a tool call before the approval policy, the run mode and the user (`EnginePermissionOrderTests`). Cloud workers run consented project hooks and send `_ompos/hook` notifications (`CloudHooksWorkerTests`; the API does not yet mint the consent claim, so cloud sessions report `untrusted`). Partial: no live omp session ran a hook.
- Remote environments over `ssh`: profiles, `ssh -G` resolution, preflight diagnosis, launch rewrite, reconnect, remote file system, remote convention discovery, remote terminals, a profile store, a Remote environments page and a New session target picker (Phase 58), wired into the desktop session path (`RemoteTransportSelectionTests`). A live run of a real `omp acp` through a loopback `sshd` passed on Linux (2026-10-28, `docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md`). Partial: no prompt turn, no host over a real network; the remote file explorer, terminal pane, hook files and mention files are not wired for ssh sessions.
- Container and WSL environments: `podman exec` into a running container and `wsl.exe` profiles, listing of running containers, a container and WSL form in the Remote environments page and sessions started in them (Wave 6; `ContainerSessionSelectionTests`, `RuntimeEnvironmentsViewTests`). A live podman test against an alpine image passed on Linux and found two defects that are fixed: the dev-container search filter lacked `label=`, and BusyBox `realpath` made every file creation in the container fail (`RemoteScriptsBusyBoxTests`). WSL never ran (Windows only); the terminal pane and the dev-container attach button are not wired.
- Multi-repository workspaces: validated workspace sets, omp launched with every root (`--add-dir`), one git worktree per git root, a multi-root explorer and `.code-workspace` import and export (Phase 59); a workspace sets dialog and `/save-workspace`, `/load-workspace` in the desktop head (partial; no live run).
- Cloud agents on Railway: an agent worker with a JWT-authenticated WebSocket bridge to `omp acp`, a Railway GraphQL client, a static pool client, the API control plane (create, prompt, stop, wake, delete, secrets, limits, pull requests) with a PostgreSQL store, and a cloud agents list and form in the heads (Phase 60), and the desktop `ApiCloudAgentsHost` behind them (`ApiCloudAgentsHostTests`, fakes only; it exists only when `OMPOS_API_BASE_URL` is set). The worker image builds and runs under podman: `scripts/cloud/worker-live-check.mjs` passed 14 of 14 checks (probes, the JWT gate, a real omp 18.12.5 answering `initialize` and `session/new` through the bridge; 2026-10-28, `docs/runbooks/evidence/cloud-worker-container-2026-10-28/README.md`). Partial: the control plane is tested against fakes, `Cloud:Enabled` is false by default, the worker clone bootstrap is missing, and the API is not deployed (no identity provider, OD-03). After the release, on 2026-10-28, a staged private worker (`ompos-worker` in the Railway project `ompos-staging`, no domain, no TCP proxy) was deployed from the released tree, passed Railway's `/healthz` check, and a real `omp acp` answered `initialize` and `session/new` through its JWT-gated bridge from inside the container; no model turn, no clone, no sleep and wake test (`docs/runbooks/evidence/railway-ompos-worker-2026-10-28.md`). The refusal of 2026-10-21 (`Deploys have been paused temporarily`) did not recur that day.
- Provider catalogue of 103 EU, US, CN, global and local providers, aggregators, coding plans and local runtimes as JSON data, a writer for every omp API style, region and kind filters, coding-plan rule cards, `GET /providers` and credential routes, and a generated `docs/reference/providers.md` (Phase 62). A read-only experiment with omp 18.12.6 showed omp accepts a `models.yml` with a block for every route of the catalogue (2026-10-27). Partial: no row was called with a real credential (OQ-71).
- Run modes (Auto-review, Allowlist, Run Everything), `permissions.json`, `sandbox.json` readers and writers, a decision engine and a Run mode page (Phase 63); the desktop head applies them to every session (`--approval-mode`, a `bash:` overlay and the permission order above). Partial; Auto-review's hosted classifier is not available; a changed run mode reaches omp only when the engine is composed again.
- `@` mentions with a fuzzy picker, Cursor-style ignore files (gitignore syntax) for the agent, Tab and mentions, a conversation search index, a context ring and ACP prompt content blocks (Phase 63); a mention reaches the prompt as a resource and `usage_update` feeds the context ring (`AgentSessionRuntimeWiringTests`). Partial: no live turn; remote files are not mentionable.
- MCP configuration import from OmpOS, Cursor, Claude Code and VS Code files, a server catalog and an MCP settings page (Phase 63), plus 13 vendor presets added from the page (written disabled, no credential; `McpPresetSourceTests`) (Wave 6). Partial: no live omp session called an MCP tool.
- Image attachments in the desktop composer: paste, pick or drop, sent as ACP image blocks when omp's `initialize` reply declares `promptCapabilities.image` (`AcpImagePromptTests`, `AgentSessionRuntimeImageTests`). Partial: no live turn; model vision stays Unknown; no web or Android attach.
- File checkpoints with rewind and restore, a worktree command model, a message queue, custom modes, side chats and fork (Phase 63), mounted in the desktop window (`ShellPaneToolsTests`; fork over `session/fork`, `AcpForkTests`). Partial: rewind restores files only; no live omp session.
- Automations: a cron scheduler, local and cloud targets, a first-run consent card and an Automations page in the desktop Settings (the 22nd page), and API routes with `Automations:Enabled` false by default (contract version 1.5.0; `AutomationsHostTests`, `LocalSessionAutomationTargetTests`, 73 API tests). Webhook triggers are refused on the desktop. Partial: the real UI session path is not tested; no live run.
- Documentation in GitBook's Git Sync layout: `.gitbook.yaml`, `docs/SUMMARY.md`, guides for getting started, the desktop and web apps, concepts, providers, settings and troubleshooting, an architecture diagram, an HTTP API reference, a publish runbook, a documentation check script (`scripts/ci/check-docs.sh`) and a refreshed Cursor parity audit (`docs/quality/cursor-parity-audit-2026-10-28.md`, re-graded against the final tree) (Phase 64). The README shows eight screenshots and links the Demo.
- GitHub settings applied with `scripts/github/apply-repo-settings.sh` (2026-10-28): description, homepage (the Demo URL), 15 topics, discussions on, wiki off, delete branch on merge, private vulnerability reporting, Dependabot security updates, secret scanning with push protection and 22 labels. The social preview upload, the discussion categories and the GitBook space need the owner.

### Changed

- The Light theme is the default in the desktop and web heads; Dark and System remain choices (Phase 51).
- The README is rewritten as the GitHub landing with a feature status table and the licence block (Phase 64).
- `docs/architecture.md` now covers the web shell, remote environments, cloud agents and hooks; `docs/handoff.md` is marked as the historical v0.9.1 handoff (Phase 64).
- ADR-0013 decision 4 (no shell, closed stdin for plugin hooks) is amended by ADR-0016 so that Cursor hook commands run as written (Phase 57).
- Licence statements in `docs/plugins.md`, ADR-0013, the GSD-Core plugin README and the deferred-and-blocked record now say OmpOS is `AGPL-3.0-or-later`; the licences of `agent-swarm` and `agent-substrate` stay open (Phase 64).
- The README shows eight screenshots (six of the Demo build with a simulated engine, two of the desktop app on Linux without a model turn) and links the deployed Demo; the homepage of the repository is the Demo URL until the documentation site exists (Phase 64).
- The Settings window has 21 pages plus Automations when the head supplies it; `docs/settings-parity.md` tracks the 50 further rows S01 to S50 by id (Phase 61).

### Fixed

- A hook run log is created with owner-only permissions because it holds prompts and output excerpts (Phase 57).
- The web head no longer ships Bootstrap; the unused library and its annotation are removed (Phase 53).
- The desktop environment picker's header ("Where should the agent run?") was clipped in the rail and hid the current choice; it wraps now (2026-10-28 smoke).
- The desktop Run mode page opened scrolled down because the selected item scrolled the page (`AutoScrollToSelectedItem="False"`).
- Container sessions: the dev container search filter lacked `label=`, and BusyBox `realpath` (alpine) refused `--` and non-existent paths, so every file creation in the container failed; found by the live podman test, fixed with a fallback (`RemoteScriptsBusyBoxTests`).
- The web Kanban board showed one card at the right edge after a tab switch (the page kept the thread's scroll offset); the board resets the scroll and each lane scrolls inside itself.
- A whitespace-only ignore pattern is rejected as a problem instead of being stored (`IgnoreSettingsLimits`).
- Automations API responses no longer carry null members.

### Security

- AGPL section 13 source offer for network use, anonymous and without sign-in (Phase 51).
- Cloud agent worker tokens are short-lived and verified by the worker; secrets are write-only through the API (Phase 60).
- Provider key writes, custom endpoints and live tests are off by default in the API and need explicit operator switches; the live test connects to public addresses only (Phase 62).
- The web provider key field is write-only (`type=password`, never rendered with a value). In the Demo it is disabled and the Demo handler answers every credential write with 403 `credential_writes_disabled`, so nothing typed there is stored or sent.

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
