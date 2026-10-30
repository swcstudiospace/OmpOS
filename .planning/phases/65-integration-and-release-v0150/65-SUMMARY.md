# Phase 65 Summary: Integration and Release v0.15.0

Status (recorded 2026-10-28): Complete with caveats. Criteria 1 and 2 are Met: the full `./bootstrap.sh` gate passed on a clean worktree of `e24e6851` (log `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`), `origin/main` moved from `04e80d1` to `9d4e0075` as a plain fast-forward, the annotated tag `v0.15.0` points at `9d4e0075`, and the `release` workflow published the GitHub Release https://github.com/swcstudiospace/OmpOS/releases/tag/v0.15.0. Criterion 3 is Met by the final report below. Caveats: the `ci` workflow on GitHub failed for both pushes at 'Start test services' (rootless podman cannot create a user namespace inside the self-hosted runner container, OQ-168), so the local gate is the gate of record; the live-model, Windows, macOS, Android, Railway, sign-in and GitBook items stay open (list below). The commits after `9d4e0075` are documentation only.

The record was written after the work. Statuses use: Met, Met (recorded), Partial, Blocked, Not verified.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | `./bootstrap.sh` passes on a clean worktree of the final commit; the log is saved under `docs/runbooks/evidence/` | Met | `AUTONOGRAMMER_TEST_PARALLEL_MODULES=3 AUTONOGRAMMER_SMOKE_PUBLISH_TIMEOUT=2400 AUTONOGRAMMER_SMOKE_DEMO_TIMEOUT=3600 ./bootstrap.sh` on a clean git worktree of `e24e6851554ae6ea710a9841920030269f96d2be` exited 0 (recorded 2026-10-28): pinned gitleaks scan OK, plugin node tests (69 and 76), `scripts/ci/repo-checks.sh` OK, locked restore, `scripts/ci/check-licences.sh` OK, warnings-as-errors Release build, `dotnet test Autonogrammer.Linux.slnf` 12215 total, 12213 succeeded, 0 failed, 2 skipped (two tests that need a non-root user) in 25 minutes with throwaway TimescaleDB and Dragonfly under podman, `dotnet format --verify-no-changes` clean, web smoke API half and Demo half with axe-core 4.14.0 (Demo half: six configurations, light and dark at 1440, 1024 and 390 px, 68 axe runs, 0 violations, no console error or failed request, no horizontal scroll, the drawer traps focus and closes on Escape, the theme persists). Log: `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`; its header records the two earlier runs of this branch that failed (`083ac50` failed 8 of 12215 tests; `75bc55b` cascaded in `Autonogrammer.Native.Tests` under a load average of 96) and that all causes were fixed. The gate ran on a shared host with `AUTONOGRAMMER_TEST_PARALLEL_MODULES=3`, which is why the knobs are set |
| 2 | `origin/main` is an ancestor of the final commit and the push is a fast-forward; tag `v0.15.0` and its GitHub Release exist | Met | `origin/main` went from `04e80d1` to `9d4e00758589aa8deab7a68ad60074b664b88e9a` as a plain fast-forward (no force, no merge commit) at about 19:52 UTC (`git merge-base --is-ancestor 04e80d1 9d4e0075` holds; `git ls-remote origin refs/heads/main` returns `9d4e0075`). The annotated tag `v0.15.0` (object `8d4f22d1`) points at `9d4e0075` (`git ls-remote --tags origin`; pushed 19:53 UTC). The `release` workflow run 40758275112 on the self-hosted `omega-ci` runner succeeded in 31 s and published the GitHub Release `Autonogrammer v0.15.0` (not a draft, published 2026-10-28T19:53:25Z; notes are the CHANGELOG `[0.15.0]` section): https://github.com/swcstudiospace/OmpOS/releases/tag/v0.15.0 |
| 3 | The final report separates verified items from items that need the owner | Met | The two lists below, written from the orchestrator facts and the builder reports |

CI on GitHub (recorded 2026-10-28, `gh run view`): the `ci` workflow ran for the main push (run 40758215402, 19:52 to 19:59 UTC) and for the tag push (run 40758275399). Both FAILED at the step 'Start test services (throwaway TimescaleDB and Dragonfly)': run 40758215402 with `newuidmap: write to uid_map failed: Operation not permitted`, run 40758275399 with `cannot clone: Permission denied` and `Error: cannot re-exec process`. Restore and the warnings-as-errors Release build passed; the tests, format check, web smoke, vulnerable-package list, secret scan, plugin tests, repository checks and licence checks were skipped in those runs. The previous main run (40453753279, 2026-10-27) failed the same way. Cause: rootless podman cannot create a user namespace inside the runner container (OQ-168). The local gate above is the gate of record; no GitHub-side run of the test suite has passed.

## Final report

### Verified (with evidence)

| Item | Evidence |
|------|----------|
| The release gate | `./bootstrap.sh` exit 0 on a clean worktree of `e24e6851`: 12215 tests, 0 failed, 2 skipped; format clean; secret scan, repository checks, licence checks, web smoke with axe (recorded 2026-10-28; `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`) |
| The release | Fast-forward of `origin/main` to `9d4e0075`; annotated tag `v0.15.0`; the `release` workflow run 40758275112 succeeded in 31 s; GitHub Release https://github.com/swcstudiospace/OmpOS/releases/tag/v0.15.0 (recorded 2026-10-28) |
| GitHub repository settings | Applied with `scripts/github/apply-repo-settings.sh --apply` and read back after the push (`docs/runbooks/evidence/github-setup-2026-10-28/read-back.json`): description, homepage = the Demo URL, 15 topics, discussions on, wiki off, delete-branch-on-merge, private vulnerability reporting, Dependabot alerts and security updates, secret scanning and push protection, 22 labels of ours (26 on the repository with GitHub's defaults); GitHub detects the licence as AGPL-3.0 (key `agpl-3.0`); community profile health 100; no rulesets and no branch protection on `main`, on purpose |
| The web Demo is live | https://ompos-web.vercel.app (Vercel production alias of project ompos-web in team swcstudiospace; static Demo bundle, simulated engine, no backend, no secret; deployment record in `docs/runbooks/web-demo.md`), captured with headless Chrome from the live URL with no console errors and no failed requests; screenshots in `docs/runbooks/evidence/web-app-2026-10-28/` |
| Web head accessibility | The gate's Demo smoke: 68 axe runs (light and dark at 1440, 1024, 390), 0 violations (recorded 2026-10-28, gate log) |
| Remote over ssh | A live run against a loopback sshd with the real omp 18.12.6 opened a session (`docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md`); no model turn |
| Remote over podman | A live podman test passed in the Wave 4 tree (alpine image, RemoteScripts fix) |
| Cloud worker image | The worker image under podman, `scripts/cloud/worker-live-check.mjs`, 14 of 14 checks (`docs/runbooks/evidence/cloud-worker-container-2026-10-28/README.md`); after the release the same image was built by Railway and ran there as a staged private worker (health check, JWT gate, `initialize` and `session/new`; `docs/runbooks/evidence/railway-autonogrammer-worker-2026-10-28.md`) |
| Multi-repo launch contract | omp probe of `--add-dir` behaviour in `docs/runbooks/evidence/multi-repo-omp-probe-2026-10-27/` |
| Hosted API and stores | Api.IntegrationTests 1027 of 1027, Coordination 32, Sessions.Postgres 58 on the final tree with throwaway podman services; the gate ran them again inside the 12215 |
| Unit and headless tests | The gate's 12215 tests (Linux, headless) |
| Licence and community files | `LICENSE` (GNU Affero General Public License v3; `scripts/ci/check-licences.sh` in the gate pins its length and SHA-256 and compares it with `LICENSES/AGPL-3.0-or-later.txt`), `NOTICE`, `THIRD-PARTY-NOTICES.md` (fresh per the gate), `REUSE.toml`, `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md`, `SECURITY.md`, issue forms under `.github/ISSUE_TEMPLATE/`, `CHANGELOG.md` (8 sections, valid), `.github/workflows/release.yml` (ran once, for this tag) |
| Documentation layout | `.gitbook.yaml`, `docs/SUMMARY.md` (73 entries), `scripts/ci/check-docs.sh` (0 errors, in the gate through `repo-checks.sh`), `docs/runbooks/gitbook-publish.md` |
| Desktop wiring | Hooks, run modes, mentions, MCP import, checkpoints, queue, search, images, automations, container sessions composed into the desktop head (Phase 57 to 63 records); `Autonogrammer.Native.Tests` passed inside the gate |
| Provider catalogue | 103 rows with the plan rule (Phase 62 record) |
| Slash catalogue | 92 Cursor tokens with handlers and convention discovery (Phase 56 record) |
| Railway | Read-only check made before the release: nothing of Autonogrammer was deployed; the project 'Ultrathink' has TimescaleDB, Dragonfly, GreptimeDB and two tailscale services only. Update 2026-10-28, after the release: a staged private worker was deployed in a separate project, `ompos-staging`, and handshaken (`docs/runbooks/evidence/railway-autonogrammer-worker-2026-10-28.md`; the API is not deployed) |

### Unverified (built, not observed)

| Item | Why |
|------|-----|
| A live model turn (OD-07) | Streaming, real edits, hooks firing inside a real omp, MCP pickup, image turns, run-mode request field names (OQ-84), embedded resources and every cloud behaviour with a model are unobserved |
| Windows, macOS and Android | Nothing ran there (Windows PowerShell worktree setup, WSL, Seatbelt, Android build and terminal) |
| A GitHub-side run of the test suite | The `ci` workflow fails at 'Start test services' on the self-hosted runners (OQ-168); the tests ran locally only |
| Cloud agents on Railway | The Railway GraphQL operations marked UNVERIFIED in ADR-0020 D8, a live provisioning, a pull request from a cloud agent |
| Provider keys | No key was supplied; no provider was called live (OQ-71) |
| The C# `models.yml` writer through `omp models` | Only a Python replica of the layout was accepted by omp (ADR-0019); UNVERIFIED: C# writer output through `omp models --json` |

### Needs the owner

| Item | What is missing |
|------|-----------------|
| GitBook connection | The space is not created or connected (OQ-101); the steps are in `docs/runbooks/gitbook-publish.md` |
| Social preview upload and discussion categories | The upload of `assets/brand/autonogrammer-social-preview.png` (OQ-170) and the creation of categories (OQ-109) are not possible through the API (`docs/runbooks/github-setup.md`) |
| Notion and Linear tracker rows | The rows of the Ultrathink graph `ut-mvrb4grd-ff38e1ff` stay incomplete; Notion needs the owner's `ultrathink-mcp auth login notion` (OQ-169) |
| Older milestone tags | The tags `v0.9.1` to `v0.14.0` do not exist, so the CHANGELOG compare links of those milestones 404; the owner may tag them (OQ-108) |
| CI runner user namespaces | Rootless podman in the `omega-ci` runner container needs a runner or host change (OQ-168) |
| Live model turn and provider credentials | A provider credential for one live turn (OD-07, OQ-71) |
| Windows, macOS and Android runs | A machine for each |
| The sign-in authority (OD-03) | The hosted API cannot be public until an identity provider is chosen; the Demo has none |
| Railway deploy approval | At release time nothing was deployed; the cloud agent worker, API and web needed the owner's approval and credentials (OQ-100, OQ-73, `docs/decisions/ADR-0020-cloud-agents-on-railway.md`). Update 2026-10-28, after the release: a staged private worker was deployed and handshaken on Railway (`docs/runbooks/evidence/railway-autonogrammer-worker-2026-10-28.md`); the API is not deployed (OD-03); keep or remove the billed `ompos-staging` project: OQ-171 |
| AGPL `-only` or `-or-later` | The default taken is `AGPL-3.0-or-later` (OQ-93); the owner confirms or changes `LICENSE` notices, package metadata and README together; the licence of the submodules `agent-swarm` and `agent-substrate` is OQ-102 |
| Owner sign-off of the look | White and Purple and the Phase 50 look have no sign-off (OQ-87 to OQ-91, OQ-94); screenshots are Linux, headless |

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
- The ROADMAP criterion list of this phase has three criteria; the CI result is recorded above as a caveat of criterion 1, not as a criterion.

## Partial

None for this phase's three criteria. Phases 51, 54, 55, 56, 57, 59, 60, 61, 62 and 63 keep Partial criteria (see their records).

## Blocked (OQ/OD)

OD-03 (sign-in authority), OD-07 (live model turn), OQ-100, OQ-171 and OQ-73 (Railway deploy beyond the staged worker, and whether to keep it), OQ-101 (GitBook connection), OQ-93 (licence form), OQ-168 (CI runner namespaces), OQ-87 to OQ-91 and OQ-94 (the look).

## Known limits

- The gate ran on `e24e6851`; the commits after `9d4e0075` change documentation and evidence only.
- The CI workflow on GitHub has not passed; see the CI paragraph above.
