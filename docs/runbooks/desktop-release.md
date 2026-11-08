# Runbook: desktop release packages (Autonogrammer.Native)

Status: The tag job attaches the six desktop files (three archives and three installers) to the GitHub Release. The RID restore is unlocked on purpose (section 8, item 3). A tag has not yet been pushed from this revision.

This runbook covers the desktop app `apps/Autonogrammer.Native` (Avalonia) packaged for Linux x64, Windows x64 and macOS arm64, so that people can test it on each OS. It does not cover the API or worker containers.

## 1. Who this is for

- Maintainers who start a desktop packaging run or cut a release.
- Testers who download a package and run the app on one of the three platforms.

## 2. Which workflow to run

The workflow is `.github/workflows/desktop-release.yml` (name `desktop-release`).

| How it starts | What it does | Touches a release? |
| --- | --- | --- |
| Actions, `desktop-release`, Run workflow, pick a branch | Builds the six files and uploads them as workflow artifacts. The release job is skipped, so no `SHA256SUMS-desktop` is written. | No |
| Push a tag `vMAJOR.MINOR.PATCH` or `vMAJOR.MINOR.PATCH-prerelease` | Builds the six files, writes `SHA256SUMS-desktop`, and attaches them to the GitHub Release for that tag. If `release.yml` already published the release, this job uploads the files and leaves the notes alone. If no release exists yet, this job creates a published release. | Yes. Published. |

The tag rule is the one `release.yml` uses:

- The tag must match the `<VersionPrefix>` in `Directory.Build.props` once the leading `v` and any `-prerelease` suffix are removed. At the time of writing that prefix is `0.16.0`.
- The tagged commit must be reachable from `origin/main`.

A tag that fails either rule fails the build before anything is packaged.

`release.yml` also runs on every `v*` tag and creates the published release with the changelog notes. The desktop job uploads the six files onto that release whether or not it is still a draft. It does not turn a published release back into a draft, and it does not replace changelog notes that are already published.

## 3. The artifacts

Each build job uploads its archive and its installer as workflow artifacts. The file name carries the version and the runtime identifier.

| Artifact file | Platform | Contents | Runner |
| --- | --- | --- | --- |
| `Autonogrammer-<version>-linux-x64.tar.gz` | Linux x64 | One folder `Autonogrammer-<version>-linux-x64/` with the self-contained publish output. The executable is `Autonogrammer.Native`. | `ubuntu-24.04` |
| `Autonogrammer-<version>-linux-x64.deb` | Linux x64 | A Debian package made by `scripts/release/package-deb.sh`. No job installs it. Not verified on a Debian or Ubuntu system. | `ubuntu-24.04` |
| `Autonogrammer-<version>-win-x64.zip` | Windows x64 | One folder with the self-contained publish output. The executable is `Autonogrammer.Native.exe`. | `windows-2025` |
| `Autonogrammer-<version>-win-x64-Setup.exe` | Windows x64 | An Inno Setup 6 installer (`scripts/release/windows/Autonogrammer.iss`). It installs per user to `%LOCALAPPDATA%\Programs\Autonogrammer`, adds a Start Menu entry and an optional desktop shortcut, and is not signed. Its uninstaller asks whether to remove your settings and sessions in `%USERPROFILE%\.local\share\Autonogrammer`; the default is No. The job runs a silent install, a 15-second launch and an uninstall. Not verified on Windows: no workflow run has produced it yet. | `windows-2025` |
| `Autonogrammer-<version>-osx-arm64.zip` | macOS arm64 | `Autonogrammer.app`, made with `ditto -c -k --keepParent`. The app is unsigned and not notarized (section 5). | `macos-26` |
| `Autonogrammer-<version>-osx-arm64.dmg` | macOS arm64 | A compressed disk image of `Autonogrammer.app` with a link to `/Applications`, made with `hdiutil create` and checked with `hdiutil verify`. Unsigned and not notarized. No job opens it. Not verified on macOS: no workflow run has produced it yet. | `macos-26` |
| `SHA256SUMS-desktop` | all platforms | SHA-256 of the six files above. Tag runs only. `release.yml` writes `SHA256SUMS-images` for the container tars and SBOM, so the two jobs cannot replace each other's file. | `ubuntu-24.04` |

The packages are self-contained. The workflow asks for runtime `10.0.14` (`-p:RuntimeFrameworkVersion=10.0.14`) so the installers do not bundle `Microsoft.NETCore.App.Runtime` 10.0.13, which has GHSA-p3jp-9v83-76w9 (CVE-2026-111497, HIGH, Brotli decoder out-of-bounds write). `release.yml` still pins 10.0.13 for the API and worker images.

The macOS job uses `macos-26` (arm64). GitHub retired all macOS 14 images on 2026-11-02 (actions/runner-images issue 15226). Section 8 asks the owner to confirm the runner choice.

## 4. Prerequisites on the machine that runs the app

The packages do not contain these. Install them separately:

- **omp** (the engine) and **Bun**. This is an owner requirement. The app's remote-host settings text names `~/.bun/bin`, `~/.local/bin` and `/usr/local/bin` as places it looks for omp, so put omp on `PATH` or in one of those folders.
- **Linux x64:** WebKitGTK is probably needed for the Artifacts panel. Section 8, item 1, explains what is and is not verified.
- **Windows x64:** the Artifacts panel uses WebView2 on Windows. Whether the WebView2 runtime must be installed is not verified (section 8, item 2).
- **macOS arm64:** macOS 14 or later. Microsoft's .NET 10 install page lists macOS 14 (Sonoma), 15 (Sequoia) and 26 (Tahoe) as supported. The bundle sets `LSMinimumSystemVersion` to `14.0` to match.

## 5. Unsigned status

The packages are **not signed with a publisher certificate and not notarized**:

- Windows: no Authenticode signature.
- macOS: no Developer ID signature and no notarization. The workflow has no `codesign` or notarization step. Whether the .NET toolchain adds its own signature to the executable, and how Gatekeeper treats the app, were not verified.
- Linux: no signature of any kind.

The workflow has no signing step. Expect operating-system warnings on first launch. The steps below are **not verified** on the target systems; a tester who runs them must confirm them and report what they see.

### macOS first launch (not verified on a Mac)

1. Unzip `Autonogrammer-<version>-osx-arm64.zip`. The result is `Autonogrammer.app`.
2. Control-click `Autonogrammer.app`, choose Open, and confirm.
3. If macOS still refuses to open it, open System Settings, Privacy & Security, and choose Open Anyway for Autonogrammer.
4. If the app is still blocked, remove the quarantine attribute that the browser added: `xattr -dr com.apple.quarantine Autonogrammer.app`.

### Windows SmartScreen first launch (not verified on Windows)

1. Extract `Autonogrammer-<version>-win-x64.zip`.
2. Run `Autonogrammer.Native.exe`. If SmartScreen shows "Windows protected your PC", choose More info, then Run anyway.

### Linux

```bash
tar -xzf Autonogrammer-<version>-linux-x64.tar.gz
./Autonogrammer-<version>-linux-x64/Autonogrammer.Native
```

`tar` keeps the execute bit. This was checked on a sample archive built by `scripts/release/package-desktop.sh`, not on the real publish output.

## 6. Verify the checksums

Tag runs attach `SHA256SUMS-desktop`, which lists the six files in section 3. Check each file against it before you run it. Do not use `SHA256SUMS` or `SHA256SUMS-images` for the desktop files: `release.yml` owns the image checksums.

- **Linux:** in the folder that holds the files and `SHA256SUMS-desktop`, run `sha256sum -c SHA256SUMS-desktop`. Each line must print `OK`. This command was run locally on sample files, including a changed file that it reported as `FAILED`.
- **macOS:** run `shasum -a 256 <file>` for the `.zip` and the `.dmg` and compare each hash with the matching line in `SHA256SUMS-desktop`. Not run on macOS for this runbook.
- **Windows:** run `Get-FileHash -Algorithm SHA256 <file>` in PowerShell for the `.zip` and the `-Setup.exe` and compare each hash with the matching line. Not run on Windows for this runbook.

Dispatch runs produce no `SHA256SUMS-desktop`. To check those files, compute each hash as above and record it with the artifact.

## 7. Publishing

A tag run attaches the six files in section 3 and `SHA256SUMS-desktop` to the GitHub Release for that tag. The release is published, either by `release.yml` or by this workflow if it creates the release. Download the files for your platform and `SHA256SUMS-desktop` from the release page. The installers are the `-win-x64-Setup.exe` for Windows and the `.dmg` for macOS. On Linux, use the `.tar.gz` or the `.deb`.

If the release was still a draft when this job ran, the job publishes it so the installers can be downloaded. It does not modify the notes of a release that is already published.

The release job fails when any of the six files is missing, or when `dist` holds any other file, so a release never gets a partial or extra set.

Not verified: no workflow run has produced the Windows installer or the macOS disk image yet. Until a run on that OS produces them and someone installs them on that OS, both stay unverified (section 5).

## 8. Open questions

1. **WebKitGTK on Linux.** `Avalonia.Controls.WebView.dll` (package 12.1.1) references `libwebkit2gtk` and `libsoup-3.0.so.0` in its GTK backend. The Artifacts panel creates a `NativeWebView` when it opens a document. Not verified: the WebKitGTK version the app needs (4.1 is the value the owner named), and what the app does when the library is missing.
2. **WebView2 on Windows.** The package README names WebView2 as the Windows engine. Not verified: whether the runtime must be installed separately.
3. **Locked restore for a runtime identifier cannot match one lock file (verified 2026-11-07).** `dotnet restore -r linux-x64 --locked-mode` sets the project's runtime identifiers to `linux-x64` only. A lock file generated with `linux-x64;win-x64;osx-arm64` then fails NU1004 because NuGet compares the sets for equality, and a lock file written by one `-r` restore drops the other two. The desktop workflow therefore restores with `-p:RestoreLockedMode=false --force-evaluate`. Package versions still come from the committed lock file. A normal `dotnet restore --locked-mode` without `-r` is unchanged.
4. **Publish has not run.** `Directory.Build.props` sets `TreatWarningsAsErrors` to `true` and `AnalysisLevel` to `latest-all`. A warning in the desktop projects would fail the build. Not verified.
<!-- rebrand:keep-start -->
5. **Bundle identifier.** `com.swcstudiospace.autonogrammer` is the identifier the CHANGELOG names for the Android app. The Android project still uses `com.swcstudiospace.ompos`. The owner should confirm the macOS identifier.
<!-- rebrand:keep-end -->
6. **`release.yml` checksum name.** Resolved 2026-11-07: on `v0.16.1`, `release.yml` uploaded `SHA256SUMS` with `--clobber` and replaced the desktop checksums with sums for image tars that were then cancelled. Desktop checksums are `SHA256SUMS-desktop`. Image and SBOM checksums are `SHA256SUMS-images`.
7. **Docs index.** `scripts/docs/check_gitbook.py` reports any page under `docs/` that `docs/SUMMARY.md` does not list. This page needs a line in `docs/SUMMARY.md`, for example `* [Desktop release packages](runbooks/desktop-release.md)`. That file was out of scope for this change.
8. **7-Zip on Windows.** The runner-images Windows image tests run `7z` by name, so `7z` is expected on `PATH` on `windows-2022`. The script falls back to `zip` if `7z` is missing. Not verified: `7z` on the runner's `PATH` in a run, and how `7z` handles the Git Bash paths the script passes it.
9. **Artifact action versions.** The workflow uses `download-artifact` v8.0.2 to download artifacts from `upload-artifact` v7.0.2. The v8 release notes describe the change as transparent to callers. Not verified by a run.
10. **macOS image.** The workflow uses `macos-26`, as section 3 records. The owner should confirm that choice.
11. **macOS and Windows first-launch steps** (section 5) and **checksum commands for macOS and Windows** (section 6) were not run on those systems.
