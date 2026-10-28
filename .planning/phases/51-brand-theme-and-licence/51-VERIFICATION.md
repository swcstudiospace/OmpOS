# Phase 51 Verification: Brand, Theme and Licence

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `51-SUMMARY.md`. Tests are hermetic (headless Avalonia, bUnit, the API test host); no omp, no network.

Recorded totals (recorded 2026-10-28, per project): Presentation 2303, Native 2012 (2 skipped), UI 595, Api.IntegrationTests 1027/1027, Architecture 46, Contracts 137 passed on the final tree. The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`. The commands below were not re-run by the author of this record, except the three shell scripts marked "run on 2026-10-28".

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 desktop default | `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*DefaultThemeTests*" --filter-class "*ThemePreferenceTests*" --filter-class "*FileSettingsStoreTests*" --filter-class "*SettingsPageStateTests*" --filter-class "*ThemeContrastTests*"` and `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*GeneralPageTests*"` | Presentation and stored defaults are Light; a saved Dark or System is kept; the window variant follows the setting; contrast roles hold in Light and Dark | Inside the passing Native and Presentation runs (recorded 2026-10-28); no class-level result recorded |
| 1 web default | `dotnet test tests/OmpOS.UI.Tests -c Release -- --filter-class "*ThemeSwitchTests*" --filter-class "*CssContractTests*" --filter-class "*AccessibilityTests*"` | The switch lists System, Light, Dark; a missing or unknown saved value shows Light; tokens and tint contrast | Inside the passing UI run (recorded 2026-10-28) |
| 1 screenshots | `ls docs/assets/screens/desktop-agents-light.png docs/runbooks/evidence/web-app-2026-10-28/L1440-01-agents.png` | Light desktop (Linux, no model turn) and Light web Demo captures exist | Files exist; the web Demo was captured from https://ompos-web.vercel.app with no console error and no failed request (recorded 2026-10-28) |
| 2 brand set | `bash scripts/ci/check-brand.sh` | 28 PNGs, 9 SVGs, 18 other files; ICO sizes, ICNS chunks, manifest keys; social preview below 1048576 bytes; no `<text>` or `font-family` in published SVGs | Run on 2026-10-28 on the final tree: `check-brand: ok (28 PNGs, 9 SVGs, 18 other files)` |
| 2 build | `bash scripts/brand/build-brand.sh` | Rebuilds every file from `assets/brand/src` with the bundled faces (needs Chrome, cairosvg, Pillow, optipng, ImageMagick) | Not re-run for this record (UNVERIFIED: run it and `git status --short` shows no change) |
| 3 licence files | `bash scripts/ci/check-licences.sh` (after `python3 scripts/legal/generate-third-party-notices.py`) | `THIRD-PARTY-NOTICES.md` matches the locks, fonts, images and image pins; every licence determined | Run on 2026-10-28 on the final tree: FAILED, `THIRD-PARTY-NOTICES.md is stale`. Regenerate and re-run to close |
| 3 licence text | `curl -fsSL https://www.gnu.org/licenses/agpl-3.0.txt \| sha256sum` | `LICENSE` equals the canonical text (expected `0d96a4ff68ad6d4b6f1f30f713b18d5184912ba8dd389f86aa7710db079abcb0`) | Not run (offline when written); Debian's copy was byte-equal |
| 4 API | `dotnet test tests/OmpOS.Api.IntegrationTests -c Release -- --filter-class "*SourceEndpointTests*" --filter-class "*WebAppAuthenticationTests*"` | `GET /source` answers without a token with exactly `licence, licenceUrl, sourceUrl, revision, version`; revision rule | Api.IntegrationTests 1027/1027 (recorded 2026-10-28) |
| 4 desktop | `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*AboutModelTests*"` and `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*AboutViewTests*" --filter-class "*AppIconTests*" --filter-class "*AndroidManifestTests*"` | The About model and window show version, revision, licence, source and notice links; icons resolve; the Android manifest names the icons | Inside the passing Presentation and Native runs (recorded 2026-10-28) |
| 4 web | `dotnet test tests/OmpOS.UI.Tests -c Release -- --filter-class "*ShellFooterTests*" --filter-class "*ShellInfoTests*"` | The footer is the page's contentinfo with the version, the Source code link and the licence link | Inside the passing UI run (recorded 2026-10-28) |

## Evidence files

| File | What it shows |
|------|---------------|
| `assets/brand/` | The shipped brand images and SVGs |
| `docs/brand.md` | Brand rules and the measured contrast table |
| `docs/licensing.md`, `docs/decisions/ADR-0018-licence-agpl-and-source-offer.md` | Licence reading, conclusions, owner decisions, the source offer contract |
| `docs/assets/screens/desktop-agents-light.png`, `docs/assets/screens/desktop-settings-light.png` | Desktop Light (Linux) |
| `docs/runbooks/evidence/web-app-2026-10-28/` | Web Demo captures, Light (`L...`) and Dark (`D...`) |

## Known limits of this verification

- No owner sign-off of the look (OQ-94) or the licence reading (OQ-93).
- `check-brand.sh` and `check-licences.sh` are not in the gate; the second fails on the final tree until the notices are regenerated.
- Nothing ran on Windows, macOS or Android; no model turn (OD-07).
- `reuse lint` is not installed; the `.icns`, `.ico` and the Android adaptive icon were not seen in their native shells.
