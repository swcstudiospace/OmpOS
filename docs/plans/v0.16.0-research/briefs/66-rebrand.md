# Brief 66: Rebrand Autonogrammer to Autonogrammer (no ADR; tool and runbook)

Names are post-rename (Autonogrammer becomes Autonogrammer); the repo on disk still uses Autonogrammer, so `Autonogrammer.Api` is `Autonogrammer.Api` there. There is no ADR: the owner decision is recorded in [the runbook](../../../runbooks/rebrand-autonogrammer.md) (section 1) and research report [01](../01-rename-inventory.md). This brief orders the work, adds the three runtime migrations of runbook section 7, and fixes what must be true afterwards. "Runbook s.N" means a section of that runbook.

## Goal

Every name the project owns changes from Autonogrammer to Autonogrammer in one mechanical pass (paths, contents, identifiers, containers, schema, headers, Android id), existing users keep their data through three C# migrations, the generated files are regenerated, and the renamed tree is built and gated once. Not delivered: new artwork, the trademark decision, external renames (GitHub, Vercel, Railway, domain), `--urls`, a store identity for Android (runbook s.5 are owner actions).

## Requirements

- AG-66-1 `rebrand.py dry-run` runs on a clean tree with `obj/` and `bin/` deleted, writes its report outside the repository, and exits 0 (exit 2 means collisions and blocks apply).
- AG-66-2 `apply` runs with the default `--engine-adapters keep` and without `--urls`, moves paths with `git mv`, ends with `post-apply verification: clean`, and commits nothing itself.
- AG-66-3 `verify --baseline <dry-run json>` passes: nothing left to rename, rejected near-match counts and every `keepPatternCounts` entry (including bare `omp`) equal to the baseline; `git grep -c compose` is equal before and after.
- AG-66-4 The three runbook acceptance probes (s.2) hold: the `autonogrammer` grep lists only protected URLs, `git ls-files | grep -i autonogrammer` lists only evidence, milestone and legal paths, `git grep -c 'omp acp'` is unchanged outside excluded zones.
- AG-66-5 The rename is one commit `rename: Autonogrammer to Autonogrammer (mechanical, tools: scripts/rebrand)`; the regenerate output (s.4) is a second commit; neither mixes hand-written code.
- AG-66-6 After the regenerate commit `dotnet restore --locked-mode`, `dotnet build ... -warnaserror`, `dotnet format ... --verify-no-changes --no-restore` and the Linux test set are green, and `docs/api/openapi.json` equals the served document.
- AG-66-7 User data directory: one shared resolver returns the `Autonogrammer` directory; a legacy `Autonogrammer` directory is moved once, atomically, leaving a marker file; a new file is never overwritten; the legacy directory is never read afterwards (runbook s.7 item 1).
- AG-66-8 `.autonogrammer` discovery: order `.autonogrammer`, legacy `.autonogrammer` (read-only, deprecated), `.omp` (unchanged); a legacy-only directory is read with a one-time notice and a documented move command; nothing is ever written into a legacy directory; legacy-only `permissions.json` and `sandbox.json` are never silently ignored (s.7 item 2).
- AG-66-9 `models.yml` markers: read, update and removal recognise `# autonogrammer:begin/end <id>` and `# autonogrammer:begin/end <id>`; a legacy block is rewritten under the new markers in the same write, never both, no duplicated provider entries; unmanaged bytes and comments are preserved (s.7 item 3).
- AG-66-10 Old-name literals exist only in files named `LegacyBrandNames.cs`, one per project that needs them; the architecture or a unit test fails on any other `autonogrammer` literal under `src/`, `services/`, `apps/` (guards the freeze in runbook s.6).
- AG-66-11 `AGENTS.md` and `README.md` use the new name and match the tree; `CLAUDE.md` exists, is minimal, and imports `AGENTS.md`; `CHANGELOG.md` has the rename entry under `## [Unreleased]` and no released section changed.
- AG-66-12 `scripts/test-services.sh` creates only `autonogrammer-test-*` containers, so it cannot touch another session's `autonogrammer-test-*` containers; `down` removes only its own two.
- AG-66-13 The desktop head window title is `Autonogrammer` (`apps/Autonogrammer.Native/MainWindow.axaml`), checked by a launch under a private Xvfb display.
- AG-66-14 The owner-action list of runbook s.5 is delivered with the result, each item marked open; nothing in the tree claims an external rename happened.

## Depends on

Runbook s.4 order and s.3 (replay, do not merge). Applied after the units of briefs 67 to 76 are written post-rename: they use new names and the tool leaves them alone (s.3, last point); they must not run the tool (00-index.md section 1, row 66). Phase 66 does not run before the scaffold decision of 00-index.md (R17) and `00-scaffold-manifest.md`, which either runs after the rename tool or writes post-rename names directly; if the scaffold already wrote post-rename names, `dry-run` simply finds fewer rows. Brief 77 needs 66 applied. Tools: Python 3.12, git, .NET SDK 10.0.402 with `DOTNET_ROOT` as in `bootstrap.sh`, `CI` unset, a warm NuGet cache (needed by the notices step).

## Projects and files owned

| Area | New or existing | Files |
| --- | --- | --- |
| Whole tree | existing | the tool's output (paths and contents), excluding the frozen zones of runbook s.1 |
| `scripts/rebrand/` | existing | read-only here; exceptions go into `rebrand.config.json`, never the tool (s.6) |
| Generated files | existing | 85 `packages.lock.json`, `docs/api/openapi.json`, `THIRD-PARTY-NOTICES.md`, `site.webmanifest` and the brand asset manifest (s.4) |
| `Autonogrammer.Harness.Workspace` (L2), `.Mcp`, `.Remote`, `.AgentLoop`, `.Plugins`, `.OmpConfig` (L2), `apps/Autonogrammer.Native`, `services/Autonogrammer.Api` | existing | `LegacyBrandNames.cs` in each project that holds a legacy literal; the sites listed in runbook s.7 items 1 to 3 |
| Tests | existing | `*.Tests` next to each migrated site (fake home directory, temp repos, `models.yml` fixtures) |
| Agent and prose docs | existing, one new | `AGENTS.md`, `CLAUDE.md` (new), `README.md`, `docs/` pages that name the product, `CHANGELOG.md` |
| `scripts/test-services.sh` | existing | renamed by the tool (see U5) |

No new project, no new package, no contract change.

## Work units (sequential; U3 may be written before U1 but is applied after it)

| # | Unit | Owner | Tests | Acceptance |
| --- | --- | --- | --- | --- |
| U1 | Apply and commit: delete `obj/`, `bin/`; clean tree; `dry-run --report-dir /tmp/ag-recon/rebrand-report` (outside the repo); read `suspect` rows, flags and collisions, fix by config (s.2 before-apply 1 to 3); `apply`; commit 1 (content and paths, message of AG-66-5) | tool | `python3 -m unittest scripts/rebrand/test_rebrand.py`; `verify --baseline` | AG-66-1 to AG-66-5 (commit 1); second `apply` changes nothing |
| U2 | Regenerate, in this order (s.4): 1 `dotnet restore Autonogrammer.Linux.slnf --force-evaluate -p:RestoreLockedMode=false` (Android restored on its own); 2 `dotnet format Autonogrammer.Linux.slnf --no-restore` (re-sorts about 500 files' usings); 3 build `services/Autonogrammer.Api -c Release --no-restore`, review that only brand strings differ in `openapi.json`; 4 `python3 scripts/legal/generate-third-party-notices.py` then `bash scripts/ci/check-licences.sh`; 5 `python3 scripts/brand/build_brand.py` then `bash scripts/ci/check-brand.sh`; commit 2 | tool, dotnet | `GuardTests`, `OpenApiCoverageTests`, `check-licences.sh`, `check-brand.sh` | AG-66-6 (commit 2); lock files never text-rewritten |
| U3 | Three runtime migrations of s.7, each a shared helper plus `LegacyBrandNames.cs`: (a) data-directory resolver used by the six sites of s.7 item 1; (b) convention-directory discovery for the sites of item 2, notice plus move command, system hook paths; (c) `OmpModelsLayout.cs` and `OmpManagedBlockSpan.cs` dual markers | Workspace, Mcp, Remote, AgentLoop, Plugins, OmpConfig, Native, Api | (a) fake home: move, already migrated, both present (new wins, legacy untouched, warning), unreadable legacy, two processes first start (one migration). (b) per file kind and per safety file: new only, legacy only, both, neither; no write into legacy. (c) only legacy, only new, one of each for different ids, legacy plus new for one id (new wins, legacy removed); removal of both families; byte preservation | AG-66-7 to AG-66-10 |
| U4 | Agent files and docs: update `AGENTS.md` (project name, project section, any `Autonogrammer` or `.autonogrammer` text; keep the GSD marker blocks as generated); add a short section "Code/Cowork and accounts" to `AGENTS.md` (the Code and Cowork surfaces and `SessionSurface` of ADR-0030, Cowork rows stay out of the Code catalog, accounts through `Autonogrammer.Accounts`, no secret or token in files or logs, the briefs 67 to 70 and ADR-0030 and ADR-0032 as the source); create `CLAUDE.md` (does not exist) containing only the import line `@AGENTS.md` and one sentence; `README.md` (banner and badge names follow the renamed asset, repo URLs stay until `--urls`); `docs/` pages only where a page named the product or a moved path; `CHANGELOG.md` `## [Unreleased]` entry (rename, the three migrations, env var and schema hard cutover, Android `ApplicationId`) | docs | `DecisionRecordTests`, link check if present | AG-66-11; no section padded; no ADR text invented |
| U5 | Test-services isolation. Result of checking: the tool already does it, no config change needed. `scripts/test-services.sh` rows 9, 10, 13, 14, 84, 86, 88 carry `autonogrammer`; none matches a protected URL pattern (those are `ompos-web`, `ompos-api`, `ompos.dev`, vercel and railway hosts), so the tool rewrites them: `POSTGRES_NAME=autonogrammer-test-postgres`, `DRAGONFLY_NAME=autonogrammer-test-dragonfly`, `DB=autonogrammer_test`, `DB_USER=autonogrammer`, `mktemp .../autonogrammer-test-services.XXXXXX`, exports `AUTONOGRAMMER_TEST_POSTGRES_CONNECTION` and `AUTONOGRAMMER_TEST_DRAGONFLY_CONNECTION`. Test code that reads the old names (`AUTONOGRAMMER_TEST_*`, found in `tests/Autonogrammer.*`) changes in the same pass. Ports are random host ports already. Task: after apply, assert by `grep -n autonogrammer scripts/test-services.sh` (no hit) and `grep -rn AUTONOGRAMMER_TEST` (none outside frozen zones); run `up`/`down` only when no other session uses `autonogrammer-test-*` names | tool | a throwaway-container run of `up`, `podman ps --format '{{.Names}}'` shows only `autonogrammer-test-*`, `down` twice (idempotent) | AG-66-12 |
| U6 | Verification: items below; owner-action list | main agent | see Success criteria | AG-66-3, 4, 6, 13, 14 |

## Success criteria

- `python3 -m unittest scripts/rebrand/test_rebrand.py` passes; `python3 scripts/rebrand/rebrand.py verify --baseline /tmp/ag-recon/rebrand-report/rebrand-report.json` reports clean; the three probes of runbook s.2 give the stated results (the first lists protected URLs only).
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` and `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore` clean; `dotnet restore Autonogrammer.Linux.slnf --locked-mode` clean; full Linux test run (`bootstrap.sh` gate) green, with `bash scripts/test-services.sh up` for the service-backed tests.
- Web smoke with non-colliding ports: `API_PORT=5287 WEB_PORT=5280 DEMO_PORT=5291 bash scripts/web-smoke/run-smoke.sh` (defaults are 5187, 5180, 5191; pick free ports, check with `ss -ltn` first).
- Window title: build `apps/Autonogrammer.Native`, start `Xvfb :97 -screen 0 1280x800x24 &` (pick a free display; never `:99`, another session uses it; check `ls /tmp/.X11-unix`), run the head with `DISPLAY=:97` and a sandbox profile as in the desktop evidence page, read the title with `xdotool getwindowname` or `xprop -root _NET_ACTIVE_WINDOW`, expect `Autonogrammer`; stop the head and Xvfb. Alternative that needs no display: an Avalonia.Headless test asserting `MainWindow.Title == "Autonogrammer"`.
- `bash scripts/ci/check-licences.sh`, `bash scripts/ci/check-brand.sh` pass; `.gitleaksignore` fingerprints for the relocated recorded frames added after the rename commit exists (runbook s.5 item 6).

## Owner actions (not done by this brief; runbook s.5)

1. Rename GitHub repo `swcstudiospace/OmpOS`, the Vercel project, the Railway services; register the replacement for `ompos.dev`; then `apply --urls` (follows: `NOTICE:20`, `Directory.Build.props` `RepositoryUrl`, README, CHANGELOG compare links, `.github/ISSUE_TEMPLATE/config.yml`, `scripts/ci/check-licences.sh`).
2. Rename `AUTONOGRAMMER_*` to `AUTONOGRAMMER_*` in Vercel, Railway, GitHub secrets and developer `.env` files.
3. Recreate or migrate podman containers, volumes, secrets, image `autonogrammer-agent-worker`, label `io.autonogrammer.cloud-agent`, PostgreSQL schema `autonogrammer` and its advisory-lock key, Dragonfly prefixes, GreptimeDB queries, branches `autonogrammer/cloud/*`; deploy API and worker together (JWT issuer and audience).
4. Android `ApplicationId` is a new store identity; installed apps will not upgrade.
5. Confirm the legal wording (`NOTICE:41`, `docs/licensing.md`); trademark decision and new artwork.
6. The three submodules (`agent-substrate`, `agent-swarm`, `claude-ultrathink`) that may read `AUTONOGRAMMER_*` or `.autonogrammer` change in their own repositories before cutover (inventory gate G9).

## Degraded and error states

Dry-run exit 2 (collision): stop, read the collision section, fix by config or by deleting stale `obj/` trees. Dirty tree: apply refuses. A `suspect` near-match row: a human decides; add a config line, never an edit of the tool. Legacy data directory unreadable or both directories present: new wins, legacy untouched, a warning is shown (AG-66-7). Legacy-only safety file: shown as a conflict, never ignored (AG-66-8).

## Cannot be verified on this host

Windows and macOS (data-directory move, `%ProgramData%\Autonogrammer` and `/Library/Application Support/Autonogrammer` hook paths, compile only); Android build and the new `ApplicationId`; every deployment (Vercel, Railway, domain, GitHub rename, secrets); the three submodules; live omp reading renamed `models.yml` blocks (no live model turn, README maturity note); the real result of `apply --urls`.

## Open questions (default; owner)

Block OQ-490 to OQ-499 for this phase (OQ-480 to OQ-489 belong to phase 77); the rows are in `docs/open-questions.md`.

- OQ-490 How long is the legacy `.autonogrammer` read path kept? Default: one minor release after 0.16.0, then removed; owner.
- OQ-491 Is the old-name literal guard (AG-66-10) a unit test or an architecture test? Default: architecture test over source files; architect.
- OQ-492 Rename `OmpLauncher` and the other first-party engine-adapter identifiers later? Default: no, they name the omp engine integration (runbook s.1, `--engine-adapters keep`); owner.
- OQ-493 Does the notice move command run from a button, a command, or both? Default: a documented command and a settings button; owner.

## Index rows

Row 66 of 00-index.md section 1 points to this brief (AG-66-1 to AG-66-14); OQ-490 to OQ-493 are in `docs/open-questions.md`.
