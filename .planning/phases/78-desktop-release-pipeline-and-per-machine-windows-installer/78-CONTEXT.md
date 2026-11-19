# Phase 78: Desktop release pipeline and per-machine Windows installer - Context
Source: owner decisions of 2026-11-19 (Linear umbrella SPE-34831), the failed `desktop-release` tag runs of v0.17.0 and v0.19.1, and the worktree changes on `fix/phase35-review-closure`
**Gathered:** 2026-11-19
**Status:** Code written and reviewed in the worktree; no CI run has proven it. Ready for integration (plan 78-06).

<domain>
## Phase Boundary

Release engineering on the shipped 0.19.x line. Make a tagged release carry working desktop installers (Windows, macOS, Linux) next to the container image archives, SBOM and checksums that `release.yml` already published, and make the Windows installer behave like traditional software: per machine, with a UAC prompt, in `C:\Program Files`.

Root cause (observed, not inferred): the tag runs of `desktop-release` for v0.17.0 and v0.19.1 failed at the checkout of the private plugin `agent-substrate`, because no `SUBMODULE_TOKEN` secret existed. `release.yml` had already published the GitHub release, so every release had images, SBOM and checksums but no installers. The earlier Windows installer was per user (`%LOCALAPPDATA%\Programs`, `PrivilegesRequired=lowest`), which is not what Windows users expect.

Phase 77 is NOT a dependency. This phase is independent of the Cowork hardening line: it touches no product feature, only packaging, workflows, the installer, two app start-up paths, documentation and the plugin pins.

Not delivered (out of scope, owner decision): Authenticode or other code-signing certificates, Apple notarization, auto-update, winget, the Microsoft Store or any other store, Android and iOS packages, a 32-bit (x86) build.
</domain>

<decisions>
## Implementation Decisions (locked by the owner on 2026-11-19)

### Plugins and licences
- **D-01:** All five required plugins are bundled into the desktop packages (no optional-plugin split).
- **D-02:** `agent-swarm` and `agent-substrate` are licensed `AGPL-3.0-or-later`. Each change is an upstream commit that adds only the `LICENSE` file, and the plugin pin in this repository moves by that commit only: agent-swarm `59f12c8d3a28265652ab0e64e52bb153b55157b1`, agent-substrate `70da6d80109eaea1dfdf3c55dff8847e090b46d4`. Both commits are also merged to each default branch (agent-swarm PR #49, agent-substrate PR #221). (OQ-102)
- **D-03:** Credential for the private plugin: a read-only deploy key on `swcstudiospace/substrate`, stored as the repository secret `SUBSTRATE_DEPLOY_KEY`. `SUBMODULE_TOKEN` is still accepted, and the deploy key wins when both exist. A tag build fails when neither exists; a branch run warns. Neither value is echoed. (OQ-63)

### Installer and platforms
- **D-04:** The Windows build is x64 only, installed to `C:\Program Files\Autonogrammer` (the 64-bit Program Files, not x86).
- **D-05:** Inno Setup stays the installer technology (`scripts/release/windows/Autonogrammer.iss`). The installer is per machine: UAC, Program Files, Start Menu and desktop shortcuts, a licence page, launch as the original user, upgrade in place, migration of the legacy per-user install, an uninstaller that keeps user data by default. `/CURRENTUSER` stays available on the command line for IT and for tests.
- **D-06:** Unsigned. SmartScreen (Windows) and Gatekeeper (macOS) warnings are documented in `docs/guides/install.md`, not hidden. The macOS app gets an ad-hoc `codesign` (not notarized).
- **D-07:** An early pre-release `v0.20.0-rc.1` is tagged before `v0.20.0`. Between them nothing but fixes for what the pre-release shows may merge.

### Pipeline
- **D-08:** `release.yml` creates the tag release as a DRAFT. `desktop-release.yml` attaches the files, waits for the `release.yml` assets (image archives, SBOM, checksums) and publishes only when all six desktop files verify.
- **D-09:** A rehearsal is a `workflow_dispatch` run with `version_suffix` set. It uses the prerelease file names and enforces the same tag gates (private-plugin credential, version and changelog checks), runs the verifier and the installer smoke test, and uploads workflow artifacts; it publishes nothing.
- **D-10:** `scripts/release/verify_desktop_assets.py` (stdlib only) checks names, sizes and file signatures of the six files and writes `SHA256SUMS-desktop`. Its 42 tests run in `scripts/ci/repo-checks.sh`.

### Claude's Discretion
- Wording of the install guide and runbook beyond the required sections; the order of workflow steps that do not depend on each other.
</decisions>

<deliverables>
## Deliverables on disk (verified in the worktree on 2026-11-19, not yet proven by CI)

| Area | Files |
|------|-------|
| Installer | `scripts/release/windows/Autonogrammer.iss`, `scripts/release/windows/smoke-test-installer.ps1` |
| App readiness | `apps/Autonogrammer.Native/Agents/LaunchDirectory.cs`, WebView2 user data folder under LocalAppData (Native head start-up) |
| Workflows | `.github/workflows/release.yml` (draft), `.github/workflows/desktop-release.yml` (deploy-key checkout, rehearsal, verifier, installer smoke test, macOS ad-hoc codesign, attach, wait for `release.yml` assets, publish only when six files verify) |
| Verifier | `scripts/release/verify_desktop_assets.py`, `scripts/release/test_verify_desktop_assets.py` (42 tests), `scripts/ci/repo-checks.sh` |
| Plugins | two LICENSE-only pin moves (D-02) |
| Docs | `docs/guides/install.md`, `docs/runbooks/desktop-release.md`, CHANGELOG `## [0.20.0]` |

Linear: umbrella SPE-34831 (https://linear.app/swcstudio/issue/SPE-34831). Steps: SPE-34768 and SPE-34769 (installer), SPE-34800 and SPE-34803 (workflows), SPE-34809 (verifier), SPE-34834 (integration), SPE-34835 (verification).
</deliverables>

<verification_posture>
## What "done" means here

The code exists and has been reviewed. Nothing is proven until CI runs it. Evidence this phase must produce, in order: the verifier tests pass in `repo-checks`; a `workflow_dispatch` rehearsal on the branch is green including the Windows installer smoke test (the run id is recorded in the evidence only when the run exists); the `v0.20.0-rc.1` release page lists the six desktop files, `SHA256SUMS-desktop`, the image archives and the SBOM, and every asset downloads non-empty; the checksums verify. macOS and Windows installs are verified by CI build and artifact inspection only. A human runs the manual checklist in `docs/guides/install.md` on a real Windows and a real macOS machine; that stays a `human-verification` item until a person records the result.

No SUMMARY or VERIFICATION file exists for this phase. Do not mark a plan or the phase complete before its own VERIFICATION says passed.
</verification_posture>

<canonical_refs>
## Canonical References

- `.github/workflows/desktop-release.yml`, `.github/workflows/release.yml`
- `scripts/release/windows/Autonogrammer.iss`, `scripts/release/windows/smoke-test-installer.ps1`
- `scripts/release/verify_desktop_assets.py`, `scripts/release/test_verify_desktop_assets.py`
- `scripts/release/package-desktop.sh`, `package-deb.sh`, `package-dmg.sh`, `bundle-plugins.sh`
- `docs/guides/install.md`, `docs/runbooks/desktop-release.md`, `CHANGELOG.md` (0.20.0)
- `docs/open-questions.md` (OQ-63, OQ-102)
</canonical_refs>
