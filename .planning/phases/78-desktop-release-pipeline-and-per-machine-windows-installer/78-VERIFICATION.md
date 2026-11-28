---
phase: 78-desktop-release-pipeline-and-per-machine-windows-installer
verified: 2026-11-28T05:05:00Z
status: gaps_found
score: "SC1/SC3 re-proven on clean committed scope (A08 T8783-qa, clean HEAD worktree: repo-checks/check-docs/check-changelog exit 0); SC2/SC4/SC5 re-proven; SC6 human checklist open"
covered_files:
  - ".planning/phases/78-desktop-release-pipeline-and-per-machine-windows-installer/78-01-PLAN.md"
  - ".planning/phases/78-desktop-release-pipeline-and-per-machine-windows-installer/78-02-PLAN.md"
  - ".planning/phases/78-desktop-release-pipeline-and-per-machine-windows-installer/78-03-PLAN.md"
  - ".planning/phases/78-desktop-release-pipeline-and-per-machine-windows-installer/78-04-PLAN.md"
  - ".planning/phases/78-desktop-release-pipeline-and-per-machine-windows-installer/78-05-PLAN.md"
  - ".planning/phases/78-desktop-release-pipeline-and-per-machine-windows-installer/78-06-PLAN.md"
  - ".planning/phases/78-desktop-release-pipeline-and-per-machine-windows-installer/78-01-SUMMARY.md"
  - ".planning/phases/78-desktop-release-pipeline-and-per-machine-windows-installer/78-02-SUMMARY.md"
  - ".planning/phases/78-desktop-release-pipeline-and-per-machine-windows-installer/78-03-SUMMARY.md"
  - ".planning/phases/78-desktop-release-pipeline-and-per-machine-windows-installer/78-04-SUMMARY.md"
  - ".planning/phases/78-desktop-release-pipeline-and-per-machine-windows-installer/78-05-SUMMARY.md"
  - ".planning/phases/78-desktop-release-pipeline-and-per-machine-windows-installer/78-06-SUMMARY.md"
  - ".planning/phases/78-desktop-release-pipeline-and-per-machine-windows-installer/78-CONTEXT.md"
  - ".planning/phases/78-desktop-release-pipeline-and-per-machine-windows-installer/78-VERIFICATION.md"
covered_digest: "v1:sha256:18d1e6169c2a4ff6a6580285ff14c6ab583cef64040b25e0c6bdef58275f9d07"
behavior_unverified: 1
overrides_applied: 0
re_verification: true
previous_status: prose-pass (no frontmatter)
previous_score: "PASS prose at 08601167, 2026-11-21"
commits: ["994f02b0"]
gaps:
  - truth: "SC6 human checklist for Windows/macOS installs not recorded; v0.20.0 asset check not re-run (release v0.20.0-rc.1 only was downloaded+verified today)."
    status: unverified
    closing_step: "A person records docs/guides/install.md checklist result; then gh release download v0.20.0 and verify_desktop_assets.py + sha256sum -c pass."
human_verification:
  - "Windows + macOS manual install checklist (docs/guides/install.md) result recorded by a person."
---

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

## Refresh 2026-11-22 (T8783-be, HEAD 52eb2c55, SDK 10.0.402)

*Evidence first, then verdict (ADR-0002/ADR-0003). Parseable YAML frontmatter added above (prose preserved below). No product code touched. gh evidence is record-only. The 2026-11-21 prose PASS is superseded by the gaps_found frontmatter because SC6 is not closed. **A08 T8783-qa clean-scope recheck 2026-11-22:** on a clean HEAD (52eb2c55) worktree at /tmp/t8783-clean (no uncommitted orphan pages), `bash scripts/ci/repo-checks.sh` → ok exit 0, `check-docs.sh` → 230 files, 0 errors, exit 0, `check-changelog.sh` → valid (31 versions) exit 0. This proves the earlier SC1/SC3 failures were caused solely by other lanes' uncommitted orphan pages (docs/adr/0003*, docs/architecture/verification-refresh-blueprint-T8783.md, ADR-0002 Status line); those are listed as SC1/SC3 gaps no longer. covered_digest unchanged (VERIFICATION file excluded from digest; no covered file touched) — verified byte-exact.*

*Status: gaps_found — SC1/SC2/SC3/SC4/SC5 re-proven; SC6 human checklist open.*

| Check | Command | Result |
|---|---|---|
| SC1 | `python3 scripts/release/test_verify_desktop_assets.py` + `bash scripts/ci/repo-checks.sh` | 58/58 OK; on clean HEAD worktree repo-checks → ok exit 0 (230 files, 0 errors) — A08 re-proven 2026-11-22; in the dirty worktree it exits 1 only on other lanes' uncommitted orphan pages |
| SC2 | Native `*LaunchDirectoryTests*` 18/18 + `*WebView2UserDataFolderTests*` 3/3 | exit 0 |
| SC3 | `git ls-tree HEAD plugins/agent-swarm plugins/agent-substrate` | 59f12c8d3a28265652bb153b55157b1 / 70da6d80109eaea1dfdf3c55dff8847e090b46d4 — match ROADMAP; on clean HEAD worktree `check-docs.sh` → 0 errors exit 0 and `check-changelog.sh` → valid (31 versions) exit 0 — A08 re-proven 2026-11-22 |
| SC4 | `gh run list --workflow desktop-release.yml --branch fix/phase35-review-closure --limit 1` | run 44524125822, event workflow_dispatch, conclusion success (exists — recorded) |
| SC5 | `gh release download v0.20.0-rc.1` + `verify_desktop_assets.py --dir <desktop-only> --version 0.20.0-rc.1` + `sha256sum -c SHA256SUMS-desktop` | 6 files verified, exit 0; all 6 OK (54.6M–111.8M). Unfiltered dir errors on SHA256SUMS-images/image tars/SBOM as unexpected — verifier expects desktop-only dir |
| SC6 | Human checklist (docs/guides/install.md) | open — stays human_verification; v0.20.0 page not downloaded (rc.1 only) |
## Refresh 2026-11-28 (current HEAD 994f02b0, Wave 2)

*Evidence first, then verdict (ADR-0002). No product code touched. v0.20.0 asset re-check attempted via `gh release download v0.20.0` (the release exists, 2026-11-19): `sha256sum -c SHA256SUMS-desktop` passes for all six desktop assets (linux-x64 deb/tar.gz, osx-arm64 dmg/zip, win-x64 Setup.exe/zip). A default `--platform all` re-run of `verify_desktop_assets.py` flags 4 extra non-desktop files (SHA256SUMS-images + two image tars + spdx) as unexpected — the script's desktop expected-set does not know the image assets, so the all-run did not pass; per-platform checks name only the six desktop assets and sha256 passes. That second-half failure is recorded honestly here, not repaired.

*Remaining (honest):* the Windows/macOS human install checklist (docs/guides/install.md) result remains unrecorded and human. Verdict remains `gaps_found`.*
