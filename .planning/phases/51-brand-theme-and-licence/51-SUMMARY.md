# Phase 51 Summary: Brand, Theme and Licence

Status (recorded 2026-10-28): criteria 1, 2 and 3 are Met; criterion 4 is Partial. The brand check, the licence check (`scripts/ci/check-licences.sh`, `THIRD-PARTY-NOTICES.md` regenerated in 035dcf81) and the documentation checks now run in the gate and CI workflow (17c8850a) and passed in the `./bootstrap.sh` gate on e24e6851. Criterion 4: the API answers `GET /source`, the desktop About links to the revision, the web footer shows the revision-bearing version and links the repository, but the web head still has no About page. The look and the licence reading have no owner sign-off: OQ-93 and OQ-94 are defaults. No gnu.org comparison of `LICENSE` was possible (UNVERIFIED). Nothing ran on Windows, macOS or Android, and no model turn was made (OD-07).

The record was written after the work. Statuses use: Met, Met (recorded), Partial, Blocked, Not verified.

Recorded test results (recorded 2026-10-28): per project before the gate: Presentation 2303, Native 2012 (2 skipped), UI 595, Api.IntegrationTests 1027/1027, Architecture 46, Contracts 137; the gate log lists every test project of `OmpOS.Linux.slnf` as passed. The full `./bootstrap.sh` gate passed on commit e24e6851 (recorded 2026-10-28, exit 0: `dotnet test OmpOS.Linux.slnf` 12215 total, 12213 succeeded, 0 failed, 2 skipped; warnings-as-errors Release build; `dotnet format --verify-no-changes` clean; repository checks, licence check, plugin tests and the web smoke OK); log `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`. No test class below is claimed to have passed on its own: each ran inside a passing project run, and the gate re-ran all of them on e24e6851.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | A new installation starts Light on the desktop and the web; Dark and System remain and persist | Met | Desktop: `DefaultThemeTests` (`TheDefaultsAreLight`: `GeneralSettingsValues.Defaults` and `StoredSettings.Defaults` are Light), `ThemePreferenceTests`, `FileSettingsStoreTests` (saved Dark kept, missing theme is Light), `GeneralPageTests`, `SettingsPageStateTests`; `apps/OmpOS.Native/App.axaml` and the Android application markup say `RequestedThemeVariant="Light"`. Web: `index.html` has `data-theme="light"`, `ThemeSwitchTests` (`MissingOrUnknownSavedThemeShowsLight`, order System, Light, Dark). `ThemeContrastTests` covers `Light` and `Dark`. Screenshots: `docs/assets/screens/desktop-agents-light.png` (Linux, no model turn), `docs/runbooks/evidence/web-app-2026-10-28/L1440-01-agents.png` and the other `L` and `D` captures. Each test ran inside the passing Native, Presentation or UI project run (recorded 2026-10-28) |
| 2 | Brand set under `assets/brand/` with its build script; sizes and dimensions checked; text outlined | Met | `assets/brand/` holds the mark, wordmark lockups, banner (1280 by 320, light and dark), social preview (1280 by 640, 51609 bytes, below 1 MB), 1024 icon; favicons in `apps/OmpOS.Web/wwwroot`; `.ico`, `.icns` and PNGs in `apps/OmpOS.Native/Assets/Brand`; launcher mipmaps in `apps/OmpOS.Android/Resources`. `scripts/brand/build-brand.sh` builds them (Chrome renders the bundled faces, glyphs re-placed as paths, `check-brand.sh` rejects `<text>` and `font-family` in published SVGs). I ran `bash scripts/ci/check-brand.sh` on the final tree on 2026-10-28: `ok (28 PNGs, 9 SVGs, 18 other files)`. The banner, social preview and icon dimensions were read from the PNG headers on 2026-10-28 (1280x320, 1280x640, 1024x1024). The script is a shell check, not a C# test; it runs in the gate through `scripts/ci/repo-checks.sh` (called by `bootstrap.sh` and `ci.yml`) and passed in the gate on e24e6851 (recorded 2026-10-28). The images were reviewed by the builder, not by an owner |
| 3 | `LICENSE`, `NOTICE`, `THIRD-PARTY-NOTICES.md`, SPDX in package metadata, README states the licence; a licence check script in the gate against the lock files | Met | `LICENSE`, `NOTICE`, `THIRD-PARTY-NOTICES.md`, `REUSE.toml`, `LICENSES/` exist; `Directory.Build.props` has `PackageLicenseExpression` and `VersionPrefix` 0.15.0; README header, badge and License section state AGPL-3.0-or-later. `scripts/ci/check-licences.sh` is called by `bootstrap.sh` (line 61) and `.github/workflows/ci.yml` (line 108) after the restore and passed in the gate on e24e6851 (recorded 2026-10-28); `THIRD-PARTY-NOTICES.md` was regenerated (035dcf81) and the check no longer reports it stale. `LICENSE` sha256 equals the constant `EXPECTED_SHA256=0d96a4ff68ad6d4b6f1f30f713b18d5184912ba8dd389f86aa7710db079abcb0` that the check script compares (re-run `sha256sum LICENSE` 2026-10-28: equal); it was copied from the ultrathink submodule and equals Debian's AGPL-3.0 text, but the comparison with gnu.org could not be made (UNVERIFIED: `curl -fsSL https://www.gnu.org/licenses/agpl-3.0.txt | sha256sum`; one attempt on 2026-10-28 returned empty output, no network). The criterion is read as met by the gate check against that constant |
| 4 | `GET /source` anonymous; web footer and About page and the desktop About link to the source of the running revision | Partial | API: `SourceEndpoints` maps `GET /source` (`MapSourceEndpoints` in `Program.cs`), `SourceEndpointTests`, `WebAppAuthenticationTests`. Desktop: `AboutModel` links `<repo>/blob/<revision>/<file>` notices and the source; `AboutViewTests`, `AboutModelTests`; reachable from the rail footer About button and `/about`. Web: `ShellFooter` shows `OmpOS <version+commit>` (from `GET /source` through `WorkbenchView.Source`), a Source code link to the repository root and the licence link (`ShellFooterTests`). Missing: the web head has no About page, and its footer link is the repository root, not the running revision. No headless view test of the web footer in a browser was recorded beyond the Demo smoke (68 axe runs, 0 violations; the gate ran the Demo half in six configurations with axe-core 4.14.0, 0 violations, recorded 2026-10-28) |

## What the phase delivered

- The brand set, its build and its check; the web favicons and manifest; desktop and Android icons wired into the projects and the Android manifest.
- The licence files, the generator for the third-party notices, REUSE data, ADR-0018 and `docs/licensing.md`.
- Light as the default on desktop, Android and web; the theme switch on the web head.
- The About window on the desktop with the AGPL section 13 offer; the anonymous `GET /source`; the web footer offer.

## Deviations

- The licence reading "AGL 3.0" is `AGPL-3.0-or-later` (OQ-93, default taken).
- Light supersedes the Dark default of Phase 50 (OQ-94, default taken).
- `check-brand.sh` is a shell script, not a C# size test as the criterion suggests.
- The web footer link is the repository root rather than the revision.

## Partial

- Criterion 4: a web About page and a revision link in the footer (the footer links the repository root).

## Blocked (OQ/OD)

- OQ-93: owner may choose `-only` instead of `-or-later`. OQ-102: licence of `agent-swarm` and `agent-substrate` (no LICENSE file in either). Both have a default.

## Known limits

- `LICENSE` was not compared with gnu.org (UNVERIFIED, no network on 2026-10-28); the gate compares it with a constant in `check-licences.sh`.
- CI on GitHub did not reach the checks: main-push run 40758215402 and tag-push run 40758275399 failed at 'Start test services' (rootless podman, OQ-168); the local gate is the gate of record.

- Ownership of existing OmpOS code is not verified (git history not reviewed); the copyright holder "SWC Studio" follows the ultrathink submodule.
- `TextMateSharp.Grammars` carries no licence file for its grammars (UNVERIFIED, noted in `docs/licensing.md`); `favicon.png` and `icon-192.png` have no recorded provenance.
- `ompos.icns` was not opened in Finder, the Android adaptive icon not seen on a launcher, the `.ico` not in Explorer; `reuse lint` not run.
- The container licence files for Bun, omp and .NET are written by `deploy/api.Containerfile` but nothing was built with podman for this record.
