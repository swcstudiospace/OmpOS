# Phase 78 Verification — Desktop release pipeline and per-machine Windows installer

**Verified:** 2026-11-21 (auto-78 worktree at `08601167`, SDK 10.0.402)
**Verdict: PASS** — all six success criteria verified from observed evidence. Two installs remain a human-verification item (owner action, not a code gap).

## Success criteria (ROADMAP)

1. **Verifier + repo checks pass (AG-78-7)** — PASS: `python3 scripts/release/test_verify_desktop_assets.py` 58/58 OK; `bash scripts/ci/repo-checks.sh` → `ok`; `check-docs.sh` 224 files 0 errors; `check-changelog.sh` valid (28 versions).
2. **App starts from read-only install dir (AG-78-4)** — PASS: `LaunchDirectoryTests` 18/18; `WebView2UserDataFolderTests` 3/3 (both `--filter-class` matched ≥1).
3. **Plugin pins/licences + docs (AG-78-9/10)** — PASS: `git ls-tree HEAD plugins/agent-swarm plugins/agent-substrate` → `59f12c8d3a28265652bb153b55157b1` / `70da6d80109eaea1dfdf3c55dff8847e090b46d4` (owner-directed LICENSE-only pins); docs/changelog checks green.
4. **Dispatch rehearsal green (AG-78-1/2/3/5/6/8)** — PASS: run `44524125822` (`workflow_dispatch`, branch `fix/phase35-review-closure`, `desktop-release`) completed **success**; jobs: package (osx-arm64/linux-x64/win-x64) all success; secret `SUBSTRATE_DEPLOY_KEY` present since 2026-11-19.
5. **v0.20.0-rc.1 release assets (AG-78-5/11)** — PASS: release page lists 6 desktop files + `SHA256SUMS-desktop` + image archives + `SHA256SUMS-images` + SBOM; `verify_desktop_assets.py --dir <desktop-only> --version 0.20.0-rc.1` → `6 files verified` exit 0 (all non-empty: 54.6M–111.8M).
6. **Human checklist + v0.20.0** — v0.20.0 tagged and released (Latest, 2026-11-19T15:31Z) with same 6 files + sums + SBOM (`gh release view v0.20.0`). The Windows/macOS manual checklist in `docs/guides/install.md` is handed over and stays a **human-verification item** until a person records the result — no claim is made from CI alone, per plan 78-06 prohibition.

## Prior gaps closed

- Root-cause fix proven: private-plugin checkout via deploy key (D-03) — tag runs `44551891120` (rc.1) and `44554031690` (v0.20.0) both **success** (vs v0.17.0/v0.19.1 failures).
- Windows installer is per-machine (UAC, `C:\Program Files`, Inno `Autonogrammer.iss` + `smoke-test-installer.ps1` on disk and exercised in rehearsal legs).
- No code-signing, no auto-update, x64-only — out of scope per owner decisions, unchanged.

## What this phase does NOT claim

- No live model turn, no Windows/macOS human install, no new tag/release in this pass. Next: owner records the manual checklist (AG-78-11), then any follow-on release.
- Phases 66–77 remain verification-debt (stale/missing VERIFICATION in `init.manager`); this pass closes only Phase 78. Linear: umbrella SPE-34831; integration SPE-34834; verification SPE-34835.
