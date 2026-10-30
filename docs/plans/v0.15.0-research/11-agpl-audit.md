# R-LEGAL: AGPL-3.0 licensing of Autonogrammer (research date 2026-10-27)

Engineering analysis, not legal advice. Method: read-only. Repo facts come from files under /root/src/repos/autonogrammer; licence facts come from the NuGet flat-container nuspec (`https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.nuspec`, "nuspec" below) or the vendor repository; compatibility reasoning cites gnu.org. "[INFERENCE]" marks anything not read directly.

Compatibility scale: **C** = compatible (permissive); **C+N** = compatible, notice/attribution text must ship; **I** = incompatible; **U** = unknown.

## 0. Decisions and sources

| Topic | Finding | Evidence |
|---|---|---|
| Licence name | "AGL 3.0" is read as GNU Affero General Public License v3.0 (SPDX `AGPL-3.0-only` / `AGPL-3.0-or-later`). | owner direction in task |
| Recommendation | **`AGPL-3.0-or-later`**. | below |
| Reason 1 | The submodule `plugins/claude-ultrathink` is `AGPL-3.0-or-later` (package.json `"license": "AGPL-3.0-or-later"`; README line 229 "either version 3 of the License, or (at your option) any later version"; `docs/open-questions.md` OQ-62). Using the same identifier gives one licence family for Autonogrammer and its closest plugin and avoids a combined-work question if code ever moves between the repositories. | plugins/claude-ultrathink/{package.json,README.md,LICENSE} |
| Reason 2 | `-or-later` lets the FSF successor licence apply without a relicence round; `-only` removes that option permanently. Cost of `-or-later`: a future FSF text you may dislike applies at users' option (AGPL section 14 lets the maintainer designate a proxy). | https://www.gnu.org/licenses/agpl-3.0.txt (sections 13, 14) |
| Reason 3 | Every third-party licence in the shipped graph (MIT, BSD-2/3, Apache-2.0, PostgreSQL, OFL-1.1 fonts) is GPLv3-compatible and therefore AGPLv3-compatible, so no dependency constrains the choice between the two identifiers. Apache-2.0 is compatible with GPL version 3 only (not v2), so a v3 licence is required. | https://www.gnu.org/licenses/license-list.html (Expat, Modified BSD, FreeBSD, Apache2.0 "compatible with version 3 of the GNU GPL") |
| When `-only` would be better | If the owner wants to freeze terms against future FSF revisions, or if a dependency were GPL-3.0-only. None in this audit is. | n/a |
| Copyright holder | Plugin headers use `Copyright (C) 2026 SWC Studio`. Use the same for Autonogrammer. Whether SWC Studio owns all existing Autonogrammer code is **not verified**. | docs/plugins.md (ultrathink dossier) |

## 1. Shipped NuGet packages

Package set source: `packages.lock.json` of `apps/Autonogrammer.Native`, `apps/Autonogrammer.Web`, `apps/Autonogrammer.Android` (incl. RID sections `android-arm64`, `android-x64`), `services/Autonogrammer.Api`; src/* locks read for Harness.Mcp, GenUi, Coordination.Dragonfly, Sessions.Postgres, UI add no package absent from the head locks. Versions are lock `resolved`. `Verified` = `nuspec` (read) | `vendor repo` (read) | `family [INFERENCE]` (same publisher/version family as a read sibling, not read itself).

### 1.1 Avalonia, SkiaSharp, HarfBuzz

| Package | Version | Licence (SPDX) | Compat | Verified | Note |
|---|---|---|---|---|---|
| Avalonia | 12.1.4 | MIT | C+N | nuspec | |
| Avalonia.Desktop | 12.1.4 | MIT | C+N | nuspec | |
| Avalonia.Themes.Fluent | 12.1.4 | MIT | C+N | nuspec | |
| Avalonia.Android | 12.1.4 | MIT | C+N | nuspec | Android workload package; only AGPL-relevant issue is store policy (6.5) |
| Avalonia.Skia | 12.1.4 | MIT | C+N | nuspec | |
| Avalonia.HarfBuzz | 12.1.4 | MIT | C+N | nuspec | |
| Avalonia.Native | 12.1.4 | MIT | C+N | nuspec | macOS glue |
| Avalonia.Win32 | 12.1.4 | MIT | C+N | nuspec | |
| Avalonia.X11 | 12.1.4 | MIT | C+N | nuspec | |
| Avalonia.FreeDesktop | 12.1.4 | MIT | C+N | nuspec | |
| Avalonia.FreeDesktop.AtSpi | 12.1.4 | MIT | C+N | nuspec | |
| Avalonia.Remote.Protocol | 12.1.4 | MIT | C+N | nuspec | |
| Avalonia.BuildServices | 11.3.2 | MIT | C+N | nuspec | build-time, `developmentDependency`; tags include "telemetry" (privacy note, not a licence issue) |
| Avalonia.Angle.Windows.Natives | 2.1.27548.20260419 | BSD-3-Clause | C+N | nuspec says `license type="file"`; file read from vendor repo https://raw.githubusercontent.com/AvaloniaUI/angle/1c89805903c1482166356d3b950d474973180e61/LICENSE (the package's repository commit): "Copyright 2018 The ANGLE Project Authors", redistribution clauses | file inside the .nupkg was not opened (nupkg member read returned HTTP 404); same content is [INFERENCE] |
| Avalonia.AvaloniaEdit | 12.0.0 | MIT | C+N | nuspec | |
| AvaloniaEdit.TextMate | 12.0.0 | MIT | C+N | nuspec | |
| MicroCom.Runtime | 0.11.6 | MIT | C+N | nuspec | |
| Tmds.DBus.Protocol | 0.94.1 | MIT | C+N | nuspec | Linux only |
| SkiaSharp | 3.119.4 | MIT | C+N | nuspec | wraps Google Skia (BSD-3-Clause) [INFERENCE, not re-read] |
| SkiaSharp.HarfBuzz | 3.119.4 | MIT | C+N | nuspec | |
| SkiaSharp.NativeAssets.Linux / .macOS / .Win32 / .Android / .WebAssembly | 3.119.4 | MIT | C+N | nuspec (all five read) | native binaries embed Skia, FreeType, libpng, zlib; their upstream notices are not in the nuspec (section 8) |
| HarfBuzzSharp | 8.3.1.5 | MIT | C+N | nuspec | |
| HarfBuzzSharp.NativeAssets.macOS / .Win32 / .Android | 8.3.1.5 | MIT | C+N | nuspec (all three read) | |
| HarfBuzzSharp.NativeAssets.Linux / .WebAssembly | 8.3.1.3 | MIT | C+N | nuspec (both read) | |
| Avalonia.Browser | 12.1.4 | MIT | C+N | nuspec | only in tools/gweb spike, not shipped |
| Avalonia.Controls.WebView | 12.1.1 | MIT | C+N | nuspec | only in tools/browser-spike (not shipped); authors "AvaloniaUI OÜ"; listed MIT; re-confirm before adopting in a head |

### 1.2 Terminal, editor, text, MCP

| Package | Version | Licence (SPDX) | Compat | Verified | Note |
|---|---|---|---|---|---|
| Iciclecreek.Avalonia.Terminal | 4.1.0 | MIT | C+N | nuspec | |
| Porta.Pty | 2.2.3 | MIT | C+N | nuspec | |
| XTerm.NET | 3.0.0 | MIT | C+N | nuspec | |
| Microsoft.Windows.Console.ConPTY | 1.24.260710001 | MIT | C+N | nuspec | ships Microsoft ConPTY binaries |
| Wcwidth | 4.0.1 | MIT | C+N | nuspec | |
| Unicode.net | 2.0.0 | MIT | C+N | nuspec says `license type="file"`; vendor repo https://raw.githubusercontent.com/neosmart/unicode.net/master/LICENSE read: "MIT License, Copyright (c) 2017 NeoSmart Technologies" | `master` read, not the 2.0.0 tag; same content [INFERENCE] |
| TextMateSharp | 2.0.3 | MIT | C+N | nuspec | |
| TextMateSharp.Grammars | 2.0.3 | MIT | C+N | nuspec | **bundles many third-party TextMate grammars/themes** each with its own licence; nuspec does not list them. Follow-up: inspect package grammar folders for per-grammar LICENSE files. Not done |
| Onigwrap | 1.0.10 | MIT | C+N | nuspec | wraps Oniguruma (BSD-2-Clause upstream) [INFERENCE, not read] |
| Markdig | 1.4.2 | BSD-2-Clause | C+N | nuspec | |
| ModelContextProtocol | 2.3.0 | Apache-2.0 | C+N | nuspec | Apache-2.0 compatible with GPLv3/AGPLv3 |
| ModelContextProtocol.Core | 2.3.0 | Apache-2.0 | C+N | nuspec | carry any Apache NOTICE file [INFERENCE: package contents not inspected] |
| Microsoft.Extensions.AI.Abstractions | 10.11.0 | MIT | C+N | nuspec | |
| YamlDotNet | 16.3.0 | MIT | C+N | nuspec | |
| DiffPlex | 1.9.0 | Apache-2.0 | C+N | not in any lock yet; pinned in Directory.Packages.props; licence per AGENTS.md line 53; nuspec **not read** | add to notices when it lands |

### 1.3 API, data, telemetry

| Package | Version | Licence (SPDX) | Compat | Verified | Note |
|---|---|---|---|---|---|
| Npgsql | 10.0.3 | PostgreSQL | C+N | nuspec | permissive BSD/MIT-like; GNU list entry not located this session [INFERENCE] |
| StackExchange.Redis | 3.4.5 | MIT | C+N | nuspec | |
| RESPite | 3.4.5 | MIT | C+N | nuspec | |
| System.IO.Hashing | 10.0.12 | MIT | C+N | nuspec | |
| Scalar.AspNetCore | 2.18.0 | MIT | C+N | nuspec | bundled JS UI; Development-only today (Program.cs line 245) |
| Microsoft.OpenApi | 2.12.2 | MIT | C+N | nuspec | |
| Microsoft.AspNetCore.OpenApi | 10.0.13 | MIT | C+N | family [INFERENCE] | |
| Microsoft.Extensions.ApiDescription.Server | 10.0.13 | MIT | C+N | nuspec | build-time |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.13 | MIT | C+N | nuspec | |
| Microsoft.IdentityModel.Tokens | 8.19.2 | MIT | C+N | nuspec | |
| Microsoft.IdentityModel.Abstractions, .JsonWebTokens, .Logging, .Protocols, .Protocols.OpenIdConnect | 8.19.2 | MIT | C+N | family [INFERENCE] | |
| System.IdentityModel.Tokens.Jwt | 8.19.2 | MIT | C+N | nuspec | |
| Microsoft.Bcl.Cryptography | 10.0.2 | MIT | C+N | nuspec | |
| OpenTelemetry | 1.20.0 | Apache-2.0 | C+N | nuspec | |
| OpenTelemetry.Api | 1.20.0 | Apache-2.0 | C+N | nuspec | |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.20.0 | Apache-2.0 | C+N | nuspec | |
| OpenTelemetry.Instrumentation.AspNetCore | 1.20.0 | Apache-2.0 | C+N | nuspec | |
| OpenTelemetry.Exporter.Console, .Extensions.Hosting, .Api.ProviderBuilderExtensions | 1.20.0 | Apache-2.0 | C+N | family [INFERENCE] | |

### 1.4 Microsoft runtime, Blazor, MSBuild, analyzers

| Package | Version | Licence (SPDX) | Compat | Verified | Note |
|---|---|---|---|---|---|
| Microsoft.AspNetCore.Components.WebAssembly | 10.0.13 | MIT | C+N | nuspec | the WASM payload also conveys the .NET runtime and ICU data to browsers (6.2) |
| Microsoft.AspNetCore.Components.WebAssembly.DevServer | 10.0.13 | MIT | C+N | nuspec | dev-time |
| Microsoft.AspNetCore.Components.Web; .Components; .Components.Forms; .Authorization; .Metadata; .Components.Analyzers | 10.0.13 | MIT | C+N | family [INFERENCE] | |
| Microsoft.JSInterop | 10.0.13 | MIT | C+N | nuspec | |
| Microsoft.JSInterop.WebAssembly | 10.0.13 | MIT | C+N | family [INFERENCE] | |
| Microsoft.AspNetCore.App.Internal.Assets | 10.0.13 | MIT | C+N | nuspec | |
| Microsoft.NET.Sdk.WebAssembly.Pack | 10.0.13 | MIT | C | nuspec | build-time |
| Microsoft.NET.ILLink.Tasks | 10.0.13 | MIT | C | nuspec | build-time |
| Microsoft.Extensions.Logging | 10.0.13 | MIT | C+N | nuspec | |
| Microsoft.Extensions.{Configuration, .Abstractions, .Binder, .FileExtensions, .Json}, Caching.Abstractions, DependencyInjection(+.Abstractions), Diagnostics(+.Abstractions), FileProviders.Abstractions/.Physical, FileSystemGlobbing, Hosting.Abstractions, Logging.Abstractions, Options(+.ConfigurationExtensions), Primitives, Validation | 10.0.13 | MIT | C+N | family [INFERENCE] (sibling Logging read) | |
| Meziantou.Analyzer | 3.0.315 | MIT | C (build-only) | nuspec | `developmentDependency` |
| Microsoft.VisualStudio.Threading.Analyzers | 18.12.1 | MIT | C (build-only) | nuspec | |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | 5.6.0 | MIT | C (build-only) | nuspec | |

### 1.5 Android binding packages (apps/Autonogrammer.Android only)

| Package | Version | Licence (SPDX) | Compat | Verified | Note |
|---|---|---|---|---|---|
| Xamarin.AndroidX.Core | 1.17.0.2 | MIT AND Apache-2.0 | C+N | nuspec | binding MIT; wrapped Google/JetBrains Java libraries Apache-2.0 |
| Xamarin.AndroidX.AppCompat | 1.7.1.3 | MIT AND Apache-2.0 | C+N | nuspec | |
| Xamarin.Kotlin.StdLib | 2.3.10.1 | MIT AND Apache-2.0 | C+N | nuspec | |
| Xamarin.KotlinX.Coroutines.Core | 1.10.2.3 | MIT AND Apache-2.0 | C+N | nuspec | |
| Xamarin.Google.Guava.ListenableFuture | 1.0.0.31 | MIT AND Apache-2.0 | C+N | nuspec | |
| Xamarin.Jetbrains.Annotations | 26.1.0.1 | MIT AND Apache-2.0 | C+N | nuspec | |
| Xamarin.JSpecify | 1.0.0.6 | MIT AND Apache-2.0 | C+N | nuspec | |
| Remaining ~50 Xamarin.* packages in the Android lock: AndroidX Activity 1.12.4.1, Annotation(.Experimental/.Jvm), AppCompat.AppCompatResources, Arch.Core.Common/Runtime, Collection(.Jvm), Compose.Runtime.Annotation(+.Android), Concurrent.Futures, Core.Core.Ktx, Core.ViewTree, CursorAdapter, CustomView, DocumentFile, DrawerLayout, Emoji2(+.ViewsHelper), Fragment, Interpolator, Lifecycle.* (10 ids, 2.10.0.2), Loader, NavigationEvent(+.Android), ProfileInstaller.ProfileInstaller, ResourceInspection.Annotation, SavedState(+.SavedState.Android), Startup.StartupRuntime, Tracing.Tracing(+.Android), VectorDrawable(+.Animated), VersionedParcelable, ViewPager, Window(+.WindowCore, .WindowCore.Jvm); KotlinX.Coroutines.Android/Core.Jvm 1.10.2.3; KotlinX.Serialization.Core(+.Jvm) 1.10.0.1 | see lock | MIT AND Apache-2.0 | C+N | family [INFERENCE] (seven siblings read, identical) | confirm with a scripted nuspec sweep before release |
| Android SDK / `Microsoft.Android.Sdk` workload (build-time) | n/a | Android SDK licence agreement (not OSS) / workload MIT | U | **not read** | workload packs are installed by `dotnet workload`, not NuGet-locked; build tools, not redistributed. Verify APK contents after the first build |

### 1.6 Non-NuGet components that ship with the products

| Component | Where | Licence | Compat | Verified | Note |
|---|---|---|---|---|---|
| .NET runtime, ASP.NET Core shared framework (not in locks) | desktop publish, API image, WASM payload | MIT + third-party notices (ICU etc.) | C+N | not read [INFERENCE] | copy Microsoft's THIRD-PARTY-NOTICES.TXT from the runtime |
| Bun 1.4.3 in `deploy/api.Containerfile` | API image | MIT + statically linked JavaScriptCore (LGPL-2) + libs listed at https://bun.sh/docs/project/licensing (tinycc LGPL-2.1, zlib-ng, ICU, sqlite public domain, etc.) | C+N | vendor page read | the image conveys Bun; carry its notices and link its sources |
| omp `@oh-my-pi/pi-coding-agent` 18.12.5 tarball in the API image | API image | MIT ("Copyright (c) 2025 Mario Zechner, 2025-2026 Can Bölük") | C+N | LICENSE read; ADR-0010/0013 | npm dependency tree inside the tarball not audited |
| Base images (SDK, runtime, Debian) | api.Containerfile | mixed (Debian GPL/LGPL packages) | n/a | not read | standard image distribution duties |

## 2. Test-only packages (not shipped)

| Package | Version | Licence | Compat | Verified | Note |
|---|---|---|---|---|---|
| xunit.v3 (and xunit.v3.* family) | 4.0.2 | Apache-2.0 | C | nuspec for xunit.v3; family [INFERENCE] | |
| xunit.analyzers | 2.2.0 | Apache-2.0 | C | nuspec | |
| Shouldly | 4.3.0 | BSD-3-Clause | C | nuspec | chosen over FluentAssertions 8.x (Xceed Community Licence, non-commercial; AGENTS.md lines 70, 188). **No Xceed package is in the set** |
| NSubstitute | 6.2.0 | BSD-3-Clause | C | nuspec | |
| TngTech.ArchUnitNET.xUnitV3 | 0.13.4 | Apache-2.0 | C | nuspec | |
| coverlet.MTP | 10.1.0 | MIT | C | nuspec | |
| Microsoft.Testing.Platform | 2.5.1 | MIT | C | nuspec | |
| Microsoft.Testing.Extensions.TrxReport | 2.5.1 | MIT | C | nuspec | |
| Microsoft.Testing.Extensions.Telemetry | 2.5.1 | MIT | C | nuspec | |
| Microsoft.Testing.Platform.MSBuild | 2.5.1 | MIT | C | nuspec | |
| Microsoft.Testing.Extensions.CodeCoverage | - | Microsoft proprietary terms | n/a | **not referenced** in Directory.Packages.props or any lock read | the flagged package is absent; coverlet.MTP is used. Re-check if added |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.13 | MIT | C | nuspec | |
| bunit | 2.12.5 | MIT | C | nuspec | |
| Verify.XunitV3 | 33.3.4 | MIT | C | nuspec | |
| AngleSharp / AngleSharp.Css / AngleSharp.Diffing | 1.8.5 / 1.1.3 / 1.1.1 | MIT | C | nuspec (first two); Diffing family [INFERENCE] | |
| DiffEngine | 11.3.0 | MIT | C | nuspec | |
| EmptyFiles | 4.4.0 | MIT | C | nuspec | |
| Microsoft.ApplicationInsights | 2.23.0 | MIT | C | nuspec | |
| System.Management | 6.0.1 | MIT | C | nuspec | System.CodeDom, System.Security.AccessControl, Microsoft.Win32.Registry, Microsoft.Bcl.AsyncInterfaces family [INFERENCE] |
| Avalonia.Headless | 12.1.4 | MIT | C | nuspec | UI tests |

Only `tests/Autonogrammer.Web.Tests` lock was parsed in full; the ~40 other test locks were not. The `Tests` item group in Directory.Packages.props is the declared superset [INFERENCE].

## 3. npm and JavaScript assets

| Item | Version | Licence | Compat | Verified | Note |
|---|---|---|---|---|---|
| plugins/gsd-core-omp/package.json | 0.1.0, `"private": true` | no `license` field; no `dependencies`/`devDependencies` | n/a | file read | no npm dependency to audit. First-party code: add `"license": "AGPL-3.0-or-later"` and SPDX headers |
| bootstrap (apps/Autonogrammer.Web/wwwroot/lib/bootstrap/dist/**) | 5.3.3 | MIT (banner in bootstrap.css and bootstrap.bundle.js: "Copyright 2011-2024 The Bootstrap Authors", "Licensed under MIT") | C+N | banner read | **no LICENSE file ships next to the library**; `.min.*` banners not read. Add `wwwroot/lib/bootstrap/LICENSE` or list in THIRD-PARTY-NOTICES, or delete the directory if the restyle drops Bootstrap (index.html links bootstrap.min.css) |
| axe-core (scripts/web-smoke) | 4.14.0 (sha256 pinned in axe-core-4.14.0.sha256) | MPL-2.0 (LICENSE read from https://raw.githubusercontent.com/dequelabs/axe-core/develop/LICENSE); GNU list: MPL-2.0 is compatible with GPL/AGPL via section 3.3 unless marked "Incompatible With Secondary Licenses" | C (not shipped) | LICENSE read | downloaded at smoke time by `npm pack axe-core@4.14.0` (run-smoke.sh line 128), never committed nor shipped. Whether the "Incompatible" notice is absent was not checked beyond the matched lines |
| xterm.js (web terminal) | not in repo | MIT upstream (not read) | C+N | not verified; no xterm.js/Monaco mention found in docs/ or ROADMAP.md | if planned: add MIT notices for xterm.js and each addon |
| tools/browser-spike, tools/gweb | n/a | n/a | n/a | locks read | spikes, not shipped |

## 4. Fonts and images

| Asset | Path | Licence | Compat | Verified | Note |
|---|---|---|---|---|---|
| DM Sans (Regular, Medium, SemiBold) | src/Autonogrammer.Native.Views/Assets/Fonts/DMSans/ | SIL OFL 1.1, "Copyright 2014 The DM Sans Project Authors (https://github.com/googlefonts/dm-fonts)" | C | OFL.txt present, read | embedded as AvaloniaResource including OFL.txt (Native.Views csproj lines 38-39) |
| Anybody (SemiBold only) | .../Fonts/Anybody/ | SIL OFL 1.1, "Copyright 2020 The Anybody Project Authors (https://github.com/Etcetera-Type-Co/Anybody)" | C | OFL.txt present | |
| Space Mono (Regular, Bold) | .../Fonts/SpaceMono/ | SIL OFL 1.1, "Copyright 2016 The Space Mono Project Authors (https://github.com/googlefonts/spacemono)" | C | OFL.txt present | |
| OFL bundling rule | - | OFL text may be omitted only "when a font is embedded in a document or bundled within a program"; the font stays under OFL and keeps authorship info. https://openfontlicense.org/ofl-faq/ | - | FAQ read | |
| Reserved Font Names | - | none seen in the three copyright lines | - | read | WOFF2 conversion for the web head: whether conversion is a "Modified Version" was not settled by the FAQ section read. **Not verified.** Keep OFL.txt and name the source next to any WOFF2 |
| Web fonts | apps/Autonogrammer.Web/wwwroot/css/app.css | system stack only | - | read | no font served on the web yet |
| favicon.png, icon-192.png | apps/Autonogrammer.Web/wwwroot/ | provenance unknown; names match the stock Blazor WASM template [INFERENCE] | U | binaries not opened | replace with Autonogrammer-authored artwork before claiming REUSE-clean |
| Android icons | apps/Autonogrammer.Android | none present (only Resources/values/styles.xml and Properties/AndroidManifest.xml found) | - | glob | add authored icon when created |
| Brand logo/banner (to be added) | - | must be Autonogrammer-authored | - | - | the typefaces come from open repos; using them implies no right to Greptile or Cursor marks |

## 5. Submodules, omp and the licence relationship

| Item | Licence in repo | Evidence | Compatibility with distributing Autonogrammer under AGPL-3.0 | Action |
|---|---|---|---|---|
| plugins/claude-ultrathink (`https://github.com/swcstudiospace/ultrathink.git`, pin 71101ed0...) | AGPL-3.0-or-later: LICENSE present (661 lines, "GNU AFFERO GENERAL PUBLIC LICENSE Version 3"), package.json `"license"`, README grant "either version 3 ... or (at your option) any later version" | LICENSE head, package.json, README read | Compatible (same family). Autonogrammer pins it as a submodule and runs it as an omp extension; its source is never copied into Autonogrammer (ADR-0013 rule 6). Since omp (MIT) loads it in-process at the user's machine, an AGPL obligation falls on whoever conveys omp together with the plugin; Autonogrammer conveys only a submodule pointer | record in THIRD-PARTY-NOTICES |
| plugins/agent-swarm (`.../swarm.git`, pin c900a510...) | **no LICENSE; no `license` in package.json (`"private": true`) or pyproject.toml; no LICENSE/COPYING/NOTICE in the tree** (glob empty) | read | no licence = all rights reserved by default. Same owner org (`swcstudiospace`), so the owner can add one. A public Autonogrammer with only a submodule pointer does not convey its files; the API image excludes `plugins/` (.containerignore last line) | add LICENSE (AGPL-3.0-or-later suggested) and `license` fields in the swarm repo, then bump the pin |
| plugins/agent-substrate (`.../substrate.git`, pin 754c51a1..., private) | **no LICENSE; no license field** (package.json private, 0.1.0) | read | as swarm; private, so nothing is conveyed publicly. Public AGPL users cannot obtain it: a product problem, not a licence conflict | decide publish-with-AGPL or keep private; document |
| plugins/gsd-core-omp | Autonogrammer-owned, in-repo, no licence field | package.json read | becomes AGPL-3.0-or-later with the root LICENSE | add SPDX headers + `license` |
| omp 18.12.5 | MIT | https://raw.githubusercontent.com/can1357/oh-my-pi/main/LICENSE read | Compatible. Invoked as a separate program over ACP/RPC stdio (`omp acp`, `omp --mode rpc`), not linked. Whether pipe communication counts as aggregation is a judgment call from the GNU FAQ (not read this session) | none; keep MIT notice in the published API image |
| Third-party SaaS (Linear, Notion, Railway, vendor model APIs) | provider terms | not read | not distributed by Autonogrammer | none |

## 6. Obligations

| # | Obligation | What it means here | Action |
|---|---|---|---|
| 6.1 | **Section 13**: if you modify the Program, the modified version "must prominently offer all users interacting with it remotely through a computer network ... an opportunity to receive the Corresponding Source ... from a network server at no charge, through some standard or customary means" (https://www.gnu.org/licenses/agpl-3.0.txt, section 13, line 540 ff.) | applies to `services/Autonogrammer.Api` (Railway) and the Blazor web head for any deployed modified build. Treat every deployment as modified | design in 6.A |
| 6.2 | Conveying object code (desktop installers, APK, the Blazor WASM payload sent to browsers, container images) | Section 6 requires Corresponding Source to accompany them or be offered, including "access to copy the Corresponding Source from a network server at no charge" (lines 254-255). One tagged source URL per release serves both | tag each release; point links at the exact commit |
| 6.3 | Corresponding Source scope | includes build scripts, Directory.Packages.props and lock files (in repo already); not system libraries or unmodified separate programs (omp, Bun) | submodules are separate works |
| 6.4 | Keep notices | LICENSE, copyright lines, THIRD-PARTY-NOTICES and OFL texts ship in every installer, APK, image and the web About page | file list below |
| 6.5 | App stores | AGPL section 10 forbids further restrictions; store terms that add restrictions conflict. Google Play: **not verified**. Apple: no iOS head exists; known friction with GPL-family licences is [INFERENCE] from general knowledge | ship Android via GitHub Releases/F-Droid first; decide before any store publish |
| 6.6 | Trademark | AGPL section 7(e) lets the licensor decline trademark rights; the licence grants no right to the Autonogrammer name or logo | `TRADEMARKS.md`; clearance of "Autonogrammer" not searched |
| 6.7 | CLA vs DCO | with `AGPL-3.0-or-later` + DCO, SWC Studio cannot later sell a proprietary licence of contributed code. A CLA is only needed for dual licensing or later non-copyleft relicensing | **DCO, no CLA** unless a commercial dual licence is planned; owner decision |
| 6.8 | Provenance | existing history written by humans and agents; ownership by SWC Studio unverified | review `git shortlog` and any third-party snippets before the first AGPL tag |
| 6.9 | REUSE / SPDX | REUSE spec 3.3: licence texts in `LICENSES/<SPDX-id>.txt`; per-file `SPDX-FileCopyrightText` + `SPDX-License-Identifier` headers, or `REUSE.toml` `[[annotations]]` (`path`, `precedence`, `SPDX-FileCopyrightText`, `SPDX-License-Identifier`); REUSE.toml and DEP5 are mutually exclusive; no licence file for an unused licence; each submodule is a separate Project (https://reuse.software/spec-3.3/) | root REUSE.toml |
| 6.10 | Containers | `deploy/api.Containerfile` conveys Bun and omp | carry their notices; add OCI labels `org.opencontainers.image.licenses` and `.source` [convention, not fetched] |

### 6.A Source-offer design

| Surface | Design | Notes |
|---|---|---|
| Shared build info | a `BuildInfo` record in a contracts/branding project: `{ licence: "AGPL-3.0-or-later", sourceUrl, revision, tag, modified }`; fill `revision` from `SourceRevisionId`/`InformationalVersion` (+sha, SDK default) and `sourceUrl` from `RepositoryUrl` in Directory.Build.props | the link must point at the **deployed** revision (`.../tree/<sha>` or `/archive/<sha>.tar.gz`), not the default branch |
| API | anonymous `GET /source` returning the BuildInfo JSON (map with `.AllowAnonymous()` exactly like `/health/live`, because the fallback policy requires auth: Program.cs lines 85 and 254); optional 302 to the archive for non-JSON `Accept`; add `Link: <.../tree/<sha>>; rel="source"` on responses via the middleware pipeline near `CorrelationIdMiddleware` (Program.cs line 231); document in OpenAPI | no auth needed |
| Web head (Blazor WASM) | footer link "Source code (AGPL-3.0-or-later)" on every screen incl. unauthenticated, to an `/about` page rendering BuildInfo, the source tree link for that revision, LICENSE and THIRD-PARTY-NOTICES. Load BuildInfo from `/source` or bake into `appsettings.json` at publish (scripts/render-web-config.sh already renders config) | visible without login (Section 13 "prominently") |
| Static host | publish `/LICENSE`, `/THIRD-PARTY-NOTICES.md`, `/source.json` in the web root | vercel.ts |
| Desktop / Android | About dialog showing BuildInfo; installers bundle LICENSE and THIRD-PARTY-NOTICES | |
| Container | OCI labels (6.10) | |
| Wording | "This service runs Autonogrammer <ver> (<sha>), licensed AGPL-3.0-or-later. Source: <url>" | |

## 7. Exact file and edit set

Paths relative to /root/src/repos/autonogrammer. Nothing was created in the repo by this research.

| # | Path | Content / edit | Basis |
|---|---|---|---|
| 1 | `LICENSE` | canonical AGPL-3.0 text, verbatim from https://www.gnu.org/licenses/agpl-3.0.txt (identical to plugins/claude-ultrathink/LICENSE) | GNU |
| 2 | `LICENSES/AGPL-3.0-or-later.txt`, `LICENSES/MIT.txt`, `LICENSES/OFL-1.1.txt` | REUSE licence texts for licences actually used | REUSE spec |
| 3 | `REUSE.toml` | `version = 1`; catch-all `path="**"`, `precedence="aggregate"`, copyright `2026 SWC Studio`, `AGPL-3.0-or-later`; override tables for bootstrap (MIT, "The Bootstrap Authors") and the three font folders (OFL-1.1 + each copyright line from section 4) | REUSE spec |
| 4 | `NOTICE` | project name, `Copyright (C) 2026 SWC Studio`, licence line, pointer to THIRD-PARTY-NOTICES.md, trademark note, source URL | |
| 5 | `THIRD-PARTY-NOTICES.md` | generated from sections 1, 3, 4: id, version, SPDX, copyright, URL; full texts once for MIT, BSD-2/3, Apache-2.0 (+NOTICE files), PostgreSQL, OFL-1.1; pointers for Bun/JavaScriptCore LGPL-2 and omp MIT; generate by script over shipped locks plus a nuspec fetch and regenerate in CI | audit tables |
| 6 | `Directory.Build.props` | add `<Authors>SWC Studio</Authors>`, `<Copyright>Copyright (C) 2026 SWC Studio</Copyright>`, `<PackageLicenseExpression>AGPL-3.0-or-later</PackageLicenseExpression>`, `<RepositoryUrl>https://github.com/swcstudiospace/OmpOS</RepositoryUrl>`, `<RepositoryType>git</RepositoryType>`, `<PublishRepositoryUrl>true</PublishRepositoryUrl>`. Tests are `IsPackable=false` (props line 29); apps set `IsPackable=false` in their csprojs. `PackageLicenseExpression` and `PackageLicenseFile` are mutually exclusive | files read |
| 7 | `README.md` | licence badge (shields.io URL pattern, not fetched), "Licence" section after "Docs": AGPL-3.0-or-later, section 13 source offer, links to LICENSE, THIRD-PARTY-NOTICES, TRADEMARKS; one line that omp (MIT) and the plugins are separate programs | README read |
| 8 | `CONTRIBUTING.md` | licence of contributions, DCO `Signed-off-by` rule (`git commit -s`), SPDX header rule, where notices are generated | file read |
| 9 | `SECURITY.md` | absent at root; add supported versions, private reporting channel, response targets (pattern: plugins/claude-ultrathink/SECURITY.md) | glob |
| 10 | `CODE_OF_CONDUCT.md`, `CHANGELOG.md` | absent at root (ultrathink has both as patterns) | glob |
| 11 | `TRADEMARKS.md` | Autonogrammer name/logo/banner reserved by SWC Studio [owner decision]; no AGPL trademark grant; forks rename; no endorsement | |
| 12 | SPDX headers in `src/**`, `apps/**`, `services/**`, `tests/**`, `plugins/gsd-core-omp/**`, `scripts/**` | either `// SPDX-License-Identifier: AGPL-3.0-or-later` + `// SPDX-FileCopyrightText: 2026 SWC Studio` per file, or rely on the REUSE.toml catch-all. If headers are enforced by an `.editorconfig` file-header rule under TreatWarningsAsErrors, apply to all files first | .editorconfig, props |
| 13 | `apps/Autonogrammer.Web/wwwroot/lib/bootstrap/LICENSE` (or remove the library) | MIT text, Bootstrap copyright | section 3 |
| 14 | `plugins/gsd-core-omp/package.json` | add `"license": "AGPL-3.0-or-later"` | |
| 15 | `deploy/api.Containerfile` | OCI labels; copy LICENSE, NOTICE, THIRD-PARTY-NOTICES.md and Bun/omp licence files into the final image | 6.10 |
| 16 | Source-offer code | `/source` endpoint, `BuildInfo`, About page + footer link, desktop/Android About entry | 6.A |
| 17 | `docs/open-questions.md` OQ-62; `docs/plugins.md` "Licence facts"; `docs/decisions/ADR-0013-plugin-isolation.md` Licence table (Autonogrammer row is NOASSERTION); `docs/runbooks/deferred-and-blocked-2026-10-26.md` line 35 | replace "no LICENSE file" with AGPL-3.0-or-later and close OQ-62; keep swarm/substrate NOASSERTION until their repos get a LICENSE | files read |
| 18 | New ADR | records `-or-later`, DCO/no CLA, source-offer design, plugin boundary | AGENTS.md ADR practice |
| 19 | sibling repos swarm, substrate | add LICENSE (owner decision) | section 5 |
| 20 | GitHub repo settings | root LICENSE makes GitHub show AGPL-3.0 | |

## 8. What could not be verified

| Item | Reason |
|---|---|
| Legal conclusions (aggregate vs derivative for omp + plugin; app stores; relicensing existing code) | engineering analysis only; GNU FAQ aggregation/plugin entries not read this session |
| Ownership of all existing Autonogrammer code; absence of third-party snippets | git history not examined |
| favicon.png and icon-192.png provenance | binaries not opened |
| TextMateSharp.Grammars bundled licences; SkiaSharp/HarfBuzz native upstream notices; Onigwrap/Oniguruma notice | .nupkg members could not be opened (HTTP 404 on member read); only nuspec metadata read |
| ~65 packages marked "family [INFERENCE]" | not individually fetched; run a scripted nuspec sweep before release |
| Test lock files other than Autonogrammer.Web.Tests | not parsed |
| Android SDK/workload licences; Google Play policy | not read |
| omp npm dependency tree inside the API image | tarball not inspected |
| xterm.js plans | no mention found |
| OFL WOFF2 conversion rule | FAQ section not read to a conclusion |
| Trademark availability of "Autonogrammer" | no search |
| DiffPlex nuspec | not in any lock yet |
| OCI label keys and shields.io badge URL | stated from convention, not fetched |

## 9. Sources

| URL | Used for |
|---|---|
| https://www.gnu.org/licenses/agpl-3.0.txt | canonical text; section 13 and section 6 wording |
| https://www.gnu.org/licenses/license-list.html | Expat/Modified BSD/FreeBSD/Apache-2.0/MPL-2.0 compatibility |
| https://reuse.software/spec-3.3/ | REUSE layout and REUSE.toml |
| https://openfontlicense.org/ofl-faq/ | OFL bundling/embedding |
| https://bun.sh/docs/project/licensing | Bun/JavaScriptCore licences |
| https://raw.githubusercontent.com/can1357/oh-my-pi/main/LICENSE | omp MIT |
| https://raw.githubusercontent.com/dequelabs/axe-core/develop/LICENSE | axe-core MPL-2.0 |
| https://raw.githubusercontent.com/AvaloniaUI/angle/1c89805903c1482166356d3b950d474973180e61/LICENSE | ANGLE BSD-3-Clause |
| https://raw.githubusercontent.com/neosmart/unicode.net/master/LICENSE | Unicode.net MIT |
| https://api.nuget.org/v3-flatcontainer/ (per-package nuspec) | all NuGet licence expressions |
