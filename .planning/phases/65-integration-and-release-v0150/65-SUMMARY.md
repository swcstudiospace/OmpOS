# Phase 65 Summary: Integration and Release v0.15.0

Status (recorded 2026-10-28): criterion 3 is met by this record; criteria 1 and 2 are Partial and pending the orchestrator. The tree at `7535859` is the integrated final tree. The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`, and no such file existed when this record was written, so no pass is claimed. The push, the tag and the GitHub Release had not been done.

The record was written after the work. Statuses use: Met, Met (recorded), Partial, Blocked, Not verified.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | `./bootstrap.sh` passes on a clean worktree of the final commit; the log is saved under `docs/runbooks/evidence/` | Partial | The gate was running on the final commit; the orchestrator records its result in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log` (no 2026-10-28 log in the tree when written; the latest are `bootstrap-gate-2026-10-27-d75fd88.log` and `-d93dd11.log`). Per-project runs on the final tree passed (recorded 2026-10-28): Presentation 2303, Native 2012 (2 skipped), AgentLoop 1221, Acp 201, Mcp 197, Plugins 429, Remote 520+ (2 skipped as root), Workspace 364, Automations 386, Web 262+, UI 595, Architecture 46, AgentWorker 153, Contracts 137, Sandbox 329; Api.IntegrationTests 1027/1027, Coordination 32, Sessions.Postgres 58 with throwaway podman services. Those counts were taken before the last wiring commits |
| 2 | `origin/main` is an ancestor of the final commit and the push is a fast-forward; tag `v0.15.0` and its GitHub Release exist | Partial (pending the orchestrator) | `git merge-base --is-ancestor origin/main 7535859` holds against the locally known `origin/main` (`04e80d1`) in this worktree; nothing was pushed, `git tag -l 'v0.15*'` lists no tag, and `gh release view v0.15.0` was not run (no release). `Directory.Build.props` holds `VersionPrefix` 0.15.0 and `.github/workflows/release.yml` exists |
| 3 | The final report separates verified items from items that need the owner | Met | The two lists below, written from the orchestrator facts and the builder reports |

## Final report

### Verified (with evidence)

| Item | Evidence |
|------|----------|
| The web Demo is live | https://ompos-web.vercel.app (Vercel production alias of project ompos-web in team swcstudiospace; static Demo bundle, simulated engine, no backend, no secret), captured with headless Chrome from the live URL with no console errors and no failed requests (recorded 2026-10-28); screenshots in `docs/runbooks/evidence/web-app-2026-10-28/` |
| Web head accessibility | `scripts/web-smoke/smoke-demo.mjs`: 68 axe runs (light and dark at 1440, 1024, 390 across every screen), 0 violations, in the Wave 5 tree |
| Remote over ssh | A live run against a loopback sshd with the real omp 18.12.6 passed (`docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md`); no model turn |
| Remote over podman | A live podman test passed in the Wave 4 tree (alpine image, RemoteScripts fix) |
| Multi-repo launch contract | omp probe of `--add-dir` behaviour in `docs/runbooks/evidence/multi-repo-omp-probe-2026-10-27/` |
| Hosted API and stores | Api.IntegrationTests 1027/1027, Coordination 32, Sessions.Postgres 58 on the final tree with throwaway podman services |
| Unit and headless tests | The per-project counts of criterion 1 (recorded 2026-10-28); Linux, headless |
| Licence and community files | `LICENSE` (GNU Affero General Public License v3), `NOTICE`, `THIRD-PARTY-NOTICES.md`, `REUSE.toml`, `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md`, `SECURITY.md`, issue forms under `.github/ISSUE_TEMPLATE/`, `CHANGELOG.md`, `.github/workflows/release.yml` (existence only; the gate runs the checks) |
| GitHub repository settings | Applied 2026-10-28 with `scripts/github/apply-repo-settings.sh`: description, homepage = the Demo URL, 15 topics, discussions on, wiki off, delete-branch-on-merge, private vulnerability reporting, Dependabot security updates, secret scanning and push protection, 22 labels |
| Documentation layout | `.gitbook.yaml`, `docs/SUMMARY.md`, `scripts/docs/check_gitbook.py` (run by the gate), `docs/runbooks/gitbook-publish.md` |
| Desktop wiring | Hooks, run modes, mentions, MCP import, checkpoints, queue, search, images, automations, container sessions composed into the desktop head (Phase 57 to 63 records); Native 2012 tests inside the passing run |
| Provider catalogue | 103 rows with the plan rule (Phase 62 record) |
| Slash catalogue | 92 Cursor tokens with handlers and convention discovery (Phase 56 record) |
| Railway | Read-only check: nothing of OmpOS is deployed; the project 'Ultrathink' has TimescaleDB, Dragonfly, GreptimeDB and two tailscale services only |

### Needs the owner (not verified)

| Item | What is missing |
|------|-----------------|
| GitBook connection | The space is not created or connected (OQ-101); the steps are in `docs/runbooks/gitbook-publish.md` |
| Social preview upload and discussion categories | The image upload and the category creation are not scriptable (`docs/runbooks/github-setup.md`) |
| Notion authentication | Not authenticated; no Notion write was made |
| Live model turn (OD-07) | No turn was run: streaming, real edits, hooks firing, MCP pickup, image turns, run-mode request field names (OQ-84), embedded resources and every cloud behaviour with a model are unobserved |
| Provider credentials | No key was supplied; no provider was called live |
| Windows, macOS and Android | Nothing ran there (Windows PowerShell worktree setup, WSL, Seatbelt, Android build and terminal) |
| The sign-in authority (OD-03) | The hosted API cannot be public until a sign-in authority is chosen; the Demo has none |
| Railway deploy approval | Nothing is deployed; the cloud agent worker, API and web need the owner's approval and credentials (OQ-100, `docs/decisions/ADR-0020-cloud-agents-on-railway.md`) |
| AGPL `-only` or `-or-later` | The default taken is `AGPL-3.0-or-later` (OQ-93); the owner confirms or changes `LICENSE` notices, package metadata and README together |
| Owner sign-off of the look | White and Purple and the Phase 50 look have no sign-off (OQ-87 to OQ-91, OQ-94); screenshots are Linux, headless |
| The release itself | Gate log, push, tag and GitHub Release (criteria 1 and 2), pending the orchestrator |

## Commit map

Range `87b2cf3..7535859`, 31 commits (`git log --oneline`), grouped by the phase in the subject.

| Phase | Commits |
|-------|---------|
| 51 Brand, theme and licence | `857b146` feat(51-01) brand set; `9632a69` feat(51-02) AGPL licence, NOTICE, third-party notices, REUSE; `24eb651` docs(51-00) ADR index rows ADR-0015 to ADR-0020, hooks plugin node tests, OQ rows |
| 52 Repository, release and community health | `3d0d57c` feat(52-01) community health files, issue forms, release workflow, CHANGELOG, GitHub setup scripts |
| 53 Web head foundation and API surface | `75bee92` feat(53-01) web head foundation; `36734ab` feat(53-02) API surface for the web screens; `c750cd7` chore(53-03) ArchitectureTests relock |
| 54 Web agent panel and workspace screens | `d9ed851` feat(54-02) web Settings pages, full provider directory in the Demo, visual polish, smoke |
| 55 Showing the web application | `e7b894b` feat(55-02) headless screenshot capture script |
| 56 Slash commands and convention discovery | `f993650` feat(56-01) 92 slash tokens, resolver, convention discovery; `e7c451e` feat(56-02) built-in skills in the palette and Skills page (also carries MCP presets and image attach of Phase 63) |
| 57 Hooks | `ca7b53d` feat(57-01) 21-event catalogue, loaders, dispatcher, shell runner v2 |
| 58 Remote environments over SSH | `fd8f335` feat(58-01) core; `d3f8b6d` docs(58-03) live ssh run record |
| 59 Multi-repo workspaces | `99ca412` feat(59-01) workspace sets, `omp --add-dir` launch, worktree run sets, `.code-workspace` |
| 60 Cloud agents on Railway | `1c02516` feat(60-01) worker bridge, `AcpWebSocketTransport`, Railway GraphQL client, static pool |
| 62 Providers | `7d6d827` feat(62-01) catalogue of 103 rows |
| 63 Cursor feature completion | `942d813` feat(63-05) automations and container and WSL environments wired into the desktop head |
| 64 Documentation and GitBook | `7535859` docs(64-03) desktop screenshots for the README and guides |
| Wave integration (no phase in the subject) | `3171ee8` fix(wave1); `491cf37` wip(f2c); `46f6f03` wip(f2a); `753f938` merge f2c-done; `23944a7` wip(f2b); `38e7cc8` merge f2b-done; `2e7d1f8` wip(wave3-4); `e7f1b88` wip(f3a) base plus Wave 3 patches; `785e79d` wip(f3b) base plus Wave 4 patches; `5275a23` wip(f3a) Wave 3 wiring; `f919596` wip(f3b) Wave 4 extras and automations API; `f06927a` merge f3b-done |

Phase 61 (settings parity) has no commit of its own: its pages and `docs/settings-parity.md` landed inside `2e7d1f8`. Phase 65 has no commit before this record.

## Deviations

- The ROADMAP evidence folder `web-app-2026-10-27` is `web-app-2026-10-28` in the tree.
- Criteria 1 and 2 are the orchestrator's last steps and are recorded Partial here.

## Partial

Criteria 1 and 2 (gate log, push, tag, release).

## Blocked (OQ/OD)

OD-03 (sign-in authority), OD-07 (live model turn), OQ-100 (Railway deploy), OQ-101 (GitBook connection), OQ-93 (licence form), OQ-87 to OQ-91 and OQ-94 (the look).

## Known limits

- The per-project counts predate the last wiring commits; only the gate log on the final commit covers them.
- `origin/main` in the ancestor check is the ref known to this worktree, not a fresh fetch.
