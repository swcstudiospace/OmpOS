# Runbook: desktop release packages (Autonogrammer.Native)

Status: Workflow and packaging script written. The workflow has not run, and no tag has been pushed. Section 8 lists what is still unverified.

This runbook covers the desktop app `apps/Autonogrammer.Native` (Avalonia) packaged for Linux x64, Windows x64 and macOS arm64, so that people can test it on each OS. It does not cover the API or worker containers.

## 1. Who this is for

- Maintainers who start a desktop packaging run or cut a draft pre-release.
- Testers who download a package and run the app on one of the three platforms.

## 2. Which workflow to run

The workflow is `.github/workflows/desktop-release.yml` (name `desktop-release`).

| How it starts | What it does | Touches a release? |
| --- | --- | --- |
| Actions, `desktop-release`, Run workflow, pick a branch | Builds the three packages and uploads them as workflow artifacts. The release job is skipped, so no `SHA256SUMS` is written. | No |
| Push a tag `vMAJOR.MINOR.PATCH` or `vMAJOR.MINOR.PATCH-prerelease` | Builds the three packages, writes `SHA256SUMS`, and creates or updates a **draft** pre-release named after the tag. | Draft only |

The tag rule is the one `release.yml` uses:

- The tag must match the `<VersionPrefix>` in `Directory.Build.props` once the leading `v` and any `-prerelease` suffix are removed. At the time of writing that prefix is `0.15.0`.
- The tagged commit must be reachable from `origin/main`.

A tag that fails either rule fails the build before anything is packaged.

**Known conflict (owner decision).** `release.yml` runs on every `v*` tag and creates a **published** (non-draft) release with the same name. If that release exists when the desktop release job runs, the desktop job refuses to modify it and fails, so the packages are not attached. Which run creates the release first is not verified. This workflow does not change `release.yml`.

## 3. The artifacts

Each build job uploads one archive as a workflow artifact. The archive name carries the version and the runtime identifier.

| Artifact file | Platform | Contents | Runner |
| --- | --- | --- | --- |
| `Autonogrammer-<version>-linux-x64.tar.gz` | Linux x64 | One folder `Autonogrammer-<version>-linux-x64/` with the self-contained publish output. The executable is `Autonogrammer.Native`. | `ubuntu-24.04` |
| `Autonogrammer-<version>-win-x64.zip` | Windows x64 | One folder with the self-contained publish output. The executable is `Autonogrammer.Native.exe`. | `windows-2022` |
| `Autonogrammer-<version>-osx-arm64.zip` | macOS arm64 | `Autonogrammer.app`, made with `ditto -c -k --keepParent`. The app is unsigned and not notarized (section 5). | `macos-15` |
| `SHA256SUMS` | all three | SHA-256 of the three archives. Tag runs only. | `ubuntu-24.04` |

The packages are self-contained. The workflow asks for runtime `10.0.13` (`-p:RuntimeFrameworkVersion=10.0.13`, the same value `release.yml` uses). The published output has not been inspected, so the exact runtime version in the archive is not verified.

The macOS job uses `macos-15`, not `macos-14`. GitHub retired all macOS 14 images on 2026-11-02 (actions/runner-images issue 15226). `macos-15` is the arm64 image that replaces it. Section 8 asks the owner to confirm this.

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

Tag runs attach `SHA256SUMS`. Check each archive against it before you run it.

- **Linux:** in the folder that holds the archives and `SHA256SUMS`, run `sha256sum -c SHA256SUMS`. Each line must print `OK`. This command was run locally on sample files, including a changed file that it reported as `FAILED`.
- **macOS:** run `shasum -a 256 <archive>` and compare the hash with the matching line in `SHA256SUMS`. Not run on macOS for this runbook.
- **Windows:** run `Get-FileHash -Algorithm SHA256 <archive>` in PowerShell and compare the hash with the matching line. Not run on Windows for this runbook.

Dispatch runs produce no `SHA256SUMS`. To check those archives, compute each hash as above and record it with the artifact.

## 7. Publishing

A tag run creates or updates a **draft** pre-release. The workflow never publishes it. A maintainer reviews the draft, then publishes it by hand in the GitHub UI.

If a published release named after the tag already exists, the desktop job fails and leaves that release unchanged. See the known conflict in section 2.

## 8. Open questions

1. **WebKitGTK on Linux.** `Avalonia.Controls.WebView.dll` (package 12.1.1) references `libwebkit2gtk` and `libsoup-3.0.so.0` in its GTK backend. The Artifacts panel creates a `NativeWebView` when it opens a document. Not verified: the WebKitGTK version the app needs (4.1 is the value the owner named), and what the app does when the library is missing.
2. **WebView2 on Windows.** The package README names WebView2 as the Windows engine. Not verified: whether the runtime must be installed separately.
3. **Locked restore for a runtime identifier.** The workflow restores with `--locked-mode`, as `release.yml` does. The `packages.lock.json` files contain no runtime-identifier targets. If restore reports NU1004 for a runtime, the lock file needs regenerating for that runtime. Not verified: no restore has run.
4. **Publish has not run.** `Directory.Build.props` sets `TreatWarningsAsErrors` to `true` and `AnalysisLevel` to `latest-all`. A warning in the desktop projects would fail the build. Not verified.
5. **Bundle identifier.** `com.swcstudiospace.autonogrammer` is the identifier the CHANGELOG names for the Android app. The Android project still uses `com.swcstudiospace.ompos`. The owner should confirm the macOS identifier.
6. **`release.yml` conflict.** Section 2. Owner decision.
7. **Docs index.** `scripts/docs/check_gitbook.py` reports any page under `docs/` that `docs/SUMMARY.md` does not list. This page needs a line in `docs/SUMMARY.md`, for example `* [Desktop release packages](runbooks/desktop-release.md)`. That file was out of scope for this change.
8. **7-Zip on Windows.** The runner-images Windows image tests run `7z` by name, so `7z` is expected on `PATH` on `windows-2022`. The script falls back to `zip` if `7z` is missing. Not verified: `7z` on the runner's `PATH` in a run, and how `7z` handles the Git Bash paths the script passes it.
9. **Artifact action versions.** The workflow uses `download-artifact` v8.0.2 to download artifacts from `upload-artifact` v7.0.2. The v8 release notes describe the change as transparent to callers. Not verified by a run.
10. **macOS image.** `macos-15` replaces `macos-14`, which GitHub retired. The owner should confirm that choice.
11. **macOS and Windows first-launch steps** (section 5) and **checksum commands for macOS and Windows** (section 6) were not run on those systems.
