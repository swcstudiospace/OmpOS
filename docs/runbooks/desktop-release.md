# Runbook: desktop release packages (Autonogrammer.Native)

Status: A pushed `v*` tag makes `release.yml` create a draft GitHub Release and `desktop-release.yml` build, verify, attach and then publish the six desktop files. The Windows installer is per machine and not signed. The macOS app is ad-hoc signed, not Developer ID signed and not notarized. No tag build has run from this revision. One rehearsal has run (Actions run 44508808964, version `0.19.2-rc.1`): it found two faults that are fixed since, and the fixed paths have not run yet (section 8, item 4). No installer or disk image was run on a real Windows or macOS machine.

This runbook covers the desktop app `apps/Autonogrammer.Native` (Avalonia) packaged for Linux x64, Windows x64 and macOS arm64, so that people can install and test it on each OS. It does not cover the API or worker containers. The steps for an end user are in [Install Autonogrammer](../guides/install.md).

## 1. Who this is for

- Maintainers who start a desktop packaging run or cut a release.
- Testers who download a package and run the app on one of the three platforms.

## 2. Which workflow runs

Two workflows run on every `v*` tag. Neither one is enough alone:

- `.github/workflows/release.yml` (name `release`) creates the GitHub Release as a **draft** with the notes from the `CHANGELOG.md` section for the tag version. Its `artifacts` job builds the two container images as archives, the SBOM and `SHA256SUMS-images`, and attaches them to the draft. It never publishes.
- `.github/workflows/desktop-release.yml` (name `desktop-release`) builds the six desktop files and `SHA256SUMS-desktop`, attaches them to that draft, waits for `release.yml`'s files and then publishes the release. It never creates a release.

| How it starts | What it does | Touches a release? |
| --- | --- | --- |
| Actions, `desktop-release`, Run workflow, pick a branch, leave `version_suffix` empty (a plain dispatch) | Builds the six files and uploads them as workflow artifacts. The release job is skipped, so no `SHA256SUMS-desktop` is written. The gates are lenient: a missing private plugin credential only warns, and `bundle-plugins.sh` runs without a tag, so a missing plugin or `LICENSE` is skipped with a warning where the tag build would fail. | No |
| The same, with `version_suffix` set (characters `0-9 A-Z a-z . -`) | A rehearsal of a prerelease tag. The files and the installer are named `<VersionPrefix>-<suffix>`, and the strict tag gates apply as for a tag push: the missing-credential step fails, and `bundle-plugins.sh` runs in tag mode with the synthetic tag `v<VersionPrefix>-<suffix>`, so the required-plugin and `LICENSE` checks run. The files are uploaded as workflow artifacts. The release job is skipped, so no `SHA256SUMS-desktop` is written and no release is created. | No |
| Push a tag `vMAJOR.MINOR.PATCH` or `vMAJOR.MINOR.PATCH-prerelease` | `release.yml` creates the draft. `desktop-release.yml` builds the files, then (only after all three platform jobs passed) runs the steps below. | Yes. The release becomes public only at the last step. |

The tag job of `desktop-release.yml` runs these steps in this order. Each one stops the job when it fails:

1. **Verify.** `scripts/release/verify_desktop_assets.py` checks that exactly the six files for the version exist, are not empty, are of that version, start with the magic bytes of their format and that the installer and the disk image are larger than 1 MiB. It also reads the structure: the `.tar.gz` and the `.zip` are read to the end (a truncated archive is reported, and the `.zip` members are CRC-checked), the `.deb` is walked member by member to the end of the file, and the `.exe` has a PE header. The `.dmg` has the trailer check only. The checks are cheap integrity checks, not proof that a package installs or runs. The verifier has 58 tests (`scripts/release/test_verify_desktop_assets.py`, run by `scripts/ci/repo-checks.sh`). The step writes `SHA256SUMS-desktop` and reads it back.
2. **Wait for the draft** that `release.yml` creates, for up to 15 minutes. It fails at once when `release.yml`'s run for the tag failed or was cancelled, and it stops after 5 failed `gh` calls in a row instead of waiting for the deadline.
3. **Read the release state.** If the release is already published, the job ends here with success: nothing is uploaded, replaced or published (a re-upload with `--clobber` deletes an existing asset first, which must not happen on a public release). `release.yml` likewise leaves the title, notes and assets of a published release unchanged.
4. **Attach** the six files and `SHA256SUMS-desktop` to the draft (`gh release upload --clobber`) and check that each one is listed with its local size.
5. **Wait for `release.yml`'s files**: `autonogrammer-api-<tag>.tar`, `autonogrammer-agent-worker-<tag>.tar`, `autonogrammer-<tag>.spdx.json` and `SHA256SUMS-images`, for up to 150 minutes. It fails at once when `release.yml`'s run failed or was cancelled, and it stops after 5 failed `gh` calls in a row.
6. **Publish** the draft (`--latest`, or `--prerelease` for a tag with a `-suffix`). The notes are never changed.

Until step 6 the release is a draft that the public cannot see. If a platform job, the verifier or `release.yml` fails, the draft stays hidden and nothing partial is published. To recover a failed tag run, re-run the failed jobs of the failed workflow run; the `attach desktop packages and publish` job of `desktop-release` then runs again (re-run it by hand if it already ran and failed). For a draft, `release.yml` updates the title and notes and `desktop-release` re-uploads the files with `--clobber`. The release is published only after the six desktop files verify and the two image archives, the SBOM and `SHA256SUMS-images` exist on it. If `release.yml` failed before it created the draft (for example the changelog check), fix that, re-run `release.yml`, then re-run the `desktop-release` release job. A re-run after publication changes nothing.

The tag rule is `scripts/ci/check-release-version.sh`, which both workflows run:

- The tag must match the `<VersionPrefix>` in `Directory.Build.props` once the leading `v` and any `-prerelease` suffix are removed. At the time of writing that prefix is `0.20.0`.
- The tagged commit must be reachable from `origin/main`.

A tag that fails either rule fails the build before anything is packaged.

### The private plugin credential

`plugins/agent-substrate` is a private repository (`swcstudiospace/substrate`), and every platform job checks it out. The credential, resolved 2026-11-19 ([OQ-63](../open-questions.md)):

- `SUBSTRATE_DEPLOY_KEY`, a repository secret that holds the private half of a read-only deploy key on `swcstudiospace/substrate`. The job reads the repository over SSH and pins the GitHub host key only when its fingerprint equals the one GitHub publishes.
- `SUBMODULE_TOKEN`, an HTTPS read token, is still accepted. `SUBSTRATE_DEPLOY_KEY` wins when both exist.
- With neither secret, a tag build and a rehearsal (a dispatch with `version_suffix`) fail at the checkout step. A plain dispatch build only warns, and its packages then lack the substrate plugin.
- To revoke access, delete the deploy key on `swcstudiospace/substrate` and delete the secret.

`bundle-plugins.sh` also refuses, in a tag build or a rehearsal, to bundle a required plugin that has no `LICENSE` file at its root. `agent-swarm` and `agent-substrate` carry the `AGPL-3.0-or-later` licence since 2026-11-19 ([OQ-102](../open-questions.md)). Because the packages include the plugins, the source of the private `agent-substrate` repository is distributed inside the public installers under that licence.

## 3. The artifacts

Each build job uploads its archive and its installer as workflow artifacts. The file name carries the version and the runtime identifier.

| Artifact file | Platform | Contents | Runner |
| --- | --- | --- | --- |
| `Autonogrammer-<version>-linux-x64.tar.gz` | Linux x64 | One folder `Autonogrammer-<version>-linux-x64/` with the self-contained publish output. The executable is `Autonogrammer.Native`. | `ubuntu-24.04` |
| `Autonogrammer-<version>-linux-x64.deb` | Linux x64 | A Debian package made by `scripts/release/package-deb.sh`. It installs `/opt/autonogrammer`, the command `/usr/bin/autonogrammer`, a desktop entry and an icon. No job installs it. Not verified on a Debian or Ubuntu system. | `ubuntu-24.04` |
| `Autonogrammer-<version>-win-x64.zip` | Windows x64 | One folder with the self-contained publish output. The executable is `Autonogrammer.Native.exe`. | `windows-2025` |
| `Autonogrammer-<version>-win-x64-Setup.exe` | Windows x64 | An Inno Setup 6 installer (`scripts/release/windows/Autonogrammer.iss`). It is per machine: a UAC prompt, files in `C:\Program Files\Autonogrammer` (64-bit only), a Start Menu shortcut for all users, an optional desktop shortcut, the licence page (AGPL-3.0), an entry in Apps and Features and a "Launch Autonogrammer" checkbox on the last page that starts the app as the normal user. The same installer upgrades in place. `/CURRENTUSER` installs per user under `%LOCALAPPDATA%\Programs\Autonogrammer` without administrator rights. An all-users install first removes the older per-user install of this app (user data stays). It is not signed with Authenticode, so SmartScreen may warn. Install and upgrade never touch user data. The uninstaller asks whether to remove the data (settings in `%APPDATA%\Autonogrammer`, sessions in `%USERPROFILE%\.local\share\Autonogrammer` or `XDG_DATA_HOME`, the managed Omp runtime and DPAPI secrets in `%LOCALAPPDATA%\Autonogrammer`); the default is No. Windows 10 version 1607 or newer, 64-bit. | `windows-2025` |
| `Autonogrammer-<version>-osx-arm64.zip` | macOS arm64 | `Autonogrammer.app`, made with `ditto -c -k --keepParent`. The bundle is ad-hoc signed by `scripts/release/macos-adhoc-sign.sh`: every Mach-O file under `Contents/MacOS` is signed with `codesign --force --sign - --timestamp=none`, then the bundle is signed the same way without `--deep`, and each Mach-O file and the bundle are verified. That is not a Developer ID signature, has no hardened runtime and is not notarization (section 5). | `macos-26` |
| `Autonogrammer-<version>-osx-arm64.dmg` | macOS arm64 | A compressed disk image of `Autonogrammer.app` with a link to `/Applications`, made with `hdiutil create` and checked with `hdiutil verify`. The script first verifies the ad-hoc signature of the copy it puts in the image. No job mounts or opens it. | `macos-26` |
| `SHA256SUMS-desktop` | all platforms | SHA-256 of the six files above. Tag runs only. `release.yml` writes `SHA256SUMS-images` for the container tars and SBOM, so the two workflows cannot replace each other's file. | `ubuntu-24.04` |

What the jobs test of the installer: on `windows-2025`, `scripts/release/windows/smoke-test-installer.ps1` checks that Setup refuses a folder that holds other files (a silent install with `/DIR=` into such a folder, step 2b), runs a silent all-users install, checks the files, the Apps and Features entry and the shortcuts, launches the app for 15 seconds and checks that it wrote nothing beside its executable (the app's output and the Application event log are printed when it exits early), installs the same setup again (upgrade in place; a marker file in the user data must survive), uninstalls silently (the user data must stay), and installs per user and then all-users to check the migration. A failed run uploads the Inno logs as the workflow artifact `installer-logs-win-x64`. **The smoke test has started on a CI runner once and has not passed yet.** In the first rehearsal (run 44508808964) it failed at step 2b: the script expected exit code 7 from a refused folder, and Setup exits with code 1. The script is fixed since, and the steps after 2b have not run. The interactive wizard, the UAC prompt, SmartScreen, the Yes answer of the uninstaller and any real Windows machine are not covered by any job.

A silent install into a folder that holds other files is refused by the folder-page check: Setup runs the page checks even with `/VERYSILENT`, so it exits with code 1, installs nothing and leaves the folder untouched (observed in run 44508808964 with Inno Setup 6.7.1).

The packages are self-contained. The workflow asks for runtime `10.0.15` (`-p:RuntimeFrameworkVersion=10.0.15`) so the installers do not bundle `Microsoft.NETCore.App.Runtime` 10.0.13, which has GHSA-p3jp-9v83-76w9 (CVE-2026-111497, HIGH, Brotli decoder out-of-bounds write). `release.yml` still pins 10.0.13 for the API and worker images.

The macOS job uses `macos-26` (arm64). GitHub retired all macOS 14 images on 2026-11-02 (actions/runner-images issue 15226). Section 8 asks the owner to confirm the runner choice.

## 4. Prerequisites on the machine that runs the app

The packages do not contain these. Install them separately:

- **omp** (the engine) and **Bun**. This is an owner requirement. The app's remote-host settings text names `~/.bun/bin`, `~/.local/bin` and `/usr/local/bin` as places it looks for omp, so put omp on `PATH` or in one of those folders.
- **Linux x64:** WebKitGTK is probably needed for the Artifacts panel. Section 8, item 1, explains what is and is not verified.
- **Windows x64:** the Artifacts panel uses WebView2 on Windows. The app keeps the WebView2 data in `%LOCALAPPDATA%\Autonogrammer\webview2`, not beside the executable, because `C:\Program Files` is read-only for a normal user. Whether the WebView2 runtime must be installed on a clean machine is not verified (section 8, item 2).
- **macOS arm64:** macOS 14 or later. Microsoft's .NET 10 install page lists macOS 14 (Sonoma), 15 (Sequoia) and 26 (Tahoe) as supported. The bundle sets `LSMinimumSystemVersion` to `14.0` to match.

Where the app starts: its workspace is the folder it is launched from. It falls back to the user's home folder when it is started from its own install folder, a system folder, a filesystem root or a folder that does not exist (`apps/Autonogrammer.Native/Agents/LaunchDirectory.cs`). A shortcut, Finder or a double-click therefore opens the home folder, and a launch from a terminal inside a project keeps that project.

## 5. Signing status

The packages are **not signed with a publisher certificate and not notarized**:

- Windows: no Authenticode signature. SmartScreen may show "Windows protected your PC".
- macOS: `scripts/release/macos-adhoc-sign.sh`, sourced by `package-desktop.sh`, signs every Mach-O file under `Contents/MacOS` ad hoc (`codesign --force --sign - --timestamp=none`), then signs the bundle the same way **without `--deep`**, and verifies each Mach-O file and the bundle. The strict verification of the whole bundle is a non-fatal diagnostic. `--deep` is not used because the plugin trees sit flat in `Contents/MacOS` (for example `plugins/agent-swarm/.cursor`); `--deep` treats such directories as nested bundles and failed on the runner with "bundle format unrecognized, invalid, or unsuitable" (run 44508808964). An ad-hoc signature has no Developer ID and no notarization, so Gatekeeper still shows the unidentified-developer prompt. The first CI run of this signing path failed and the script was fixed; **the fixed path has not run yet** (it runs in the next rehearsal). Gatekeeper's behaviour on the `.zip` and the `.dmg` has not been observed (section 8, item 6).
- Linux: no signature of any kind.

Expect operating-system warnings on first launch. The steps below are **not verified** on the target systems; a tester who runs them must confirm them and report what they see.

### Windows first run (not verified on Windows)

1. Download `Autonogrammer-<version>-win-x64-Setup.exe` and double-click it.
2. If SmartScreen shows "Windows protected your PC", choose More info, then Run anyway.
3. Approve the UAC prompt, follow the wizard and, on the last page, leave "Launch Autonogrammer" checked or start the app from the Start Menu.

The zip is the alternative: extract `Autonogrammer-<version>-win-x64.zip` and run `Autonogrammer.Native.exe`; the SmartScreen step is the same.

### macOS first launch (not verified on a Mac)

1. Open `Autonogrammer-<version>-osx-arm64.dmg` and drag `Autonogrammer.app` onto the `Applications` link. (The zip holds the same app: unzip it and move `Autonogrammer.app` to `/Applications`.)
2. Control-click `Autonogrammer.app`, choose Open, and confirm.
3. If macOS still refuses to open it, open System Settings, Privacy & Security, and choose Open Anyway for Autonogrammer.
4. If the app is still blocked, remove the quarantine attribute that the browser added: `xattr -dr com.apple.quarantine /Applications/Autonogrammer.app`.

### Linux

```bash
tar -xzf Autonogrammer-<version>-linux-x64.tar.gz
./Autonogrammer-<version>-linux-x64/Autonogrammer.Native
```

`tar` keeps the execute bit. This was checked on a sample archive built by `scripts/release/package-desktop.sh`, not on the real publish output. The `.deb` is installed with `sudo apt install ./Autonogrammer-<version>-linux-x64.deb`; no job has done that.

## 6. Verify the checksums

Tag runs attach `SHA256SUMS-desktop`, which lists the six files in section 3. Check each file against it before you run it. Do not use `SHA256SUMS-images` for the desktop files: it belongs to `release.yml` and lists the images, the SBOM and nothing else.

- **Linux:** in the folder that holds the files and `SHA256SUMS-desktop`, run `sha256sum -c SHA256SUMS-desktop`. Each line must print `OK`. This command was run locally on sample files, including a changed file that it reported as `FAILED`.
- **macOS:** run `shasum -a 256 <file>` for the `.zip` and the `.dmg` and compare each hash with the matching line in `SHA256SUMS-desktop`. Not run on macOS for this runbook.
- **Windows:** run `Get-FileHash -Algorithm SHA256 <file>` in PowerShell for the `.zip` and the `-Setup.exe` and compare each hash with the matching line. Not run on Windows for this runbook.

Dispatch runs, rehearsals included, produce no `SHA256SUMS-desktop`. To check those files, compute each hash as above and record it with the artifact.

## 7. Publishing

Cut a release in this order:

1. Make sure `VersionPrefix` in `Directory.Build.props` and the `CHANGELOG.md` section for it agree, and that the commit is on `main`.
2. Push the tag `vMAJOR.MINOR.PATCH[-prerelease]`. Both workflows start.
3. Watch `release` and `desktop-release`. The release job of `desktop-release` ends by publishing. Its job summary states the order it followed and the signing status.
4. Check the release page: it holds the six desktop files, `SHA256SUMS-desktop`, the two image archives, the SBOM and `SHA256SUMS-images`, and the notes are the changelog notes.

Download the files for your platform and `SHA256SUMS-desktop` from the release page. The installers are the `-win-x64-Setup.exe` for Windows and the `.dmg` for macOS. On Linux, use the `.tar.gz` or the `.deb`.

If something fails, nothing partial is public: the release stays a draft (section 2). Re-run the failed jobs of the failed workflow run; the release job re-runs too. Re-running the release job on an already published release ends with success and uploads, replaces and publishes nothing.

Not verified: the first rehearsal compiled the Windows installer but did not get past the smoke test, and the macOS leg failed before it built the disk image, so no run has produced a complete set of six files from this revision. No tag run of the new ordering (verify, wait, read state, attach, wait, publish) has happened. Until a run does and someone installs the files on that OS, the installer and the disk image stay unverified (section 8).

## 8. Open questions

1. **WebKitGTK on Linux.** `Avalonia.Controls.WebView.dll` (package 12.1.1) references `libwebkit2gtk` and `libsoup-3.0.so.0` in its GTK backend. The Artifacts panel creates a `NativeWebView` when it opens a document. Not verified: the WebKitGTK version the app needs (4.1 is the value the owner named), and what the app does when the library is missing.
2. **WebView2 on a clean Windows machine.** The package README names WebView2 as the Windows engine, and the app puts its data folder in `%LOCALAPPDATA%\Autonogrammer\webview2`. Not verified: whether the WebView2 runtime must be installed separately, and what the Artifacts panel shows when it is missing. No run was made on Windows.
3. **Locked restore for a runtime identifier cannot match one lock file (verified 2026-11-07).** `dotnet restore -r linux-x64 --locked-mode` sets the project's runtime identifiers to `linux-x64` only. A lock file generated with `linux-x64;win-x64;osx-arm64` then fails NU1004 because NuGet compares the sets for equality, and a lock file written by one `-r` restore drops the other two. The desktop workflow therefore restores with `-p:RestoreLockedMode=false --force-evaluate`. Package versions still come from the committed lock file. A normal `dotnet restore --locked-mode` without `-r` is unchanged.
4. **Rehearsal and first tag build of this pipeline.** The tag builds of `desktop-release` failed at the substrate checkout on v0.17.0 and v0.19.1 because neither secret existed (recorded in OQ-63). A rehearsal (a dispatch with `version_suffix`, section 2) applies the same credential and `bundle-plugins.sh` tag gates and creates no release. Dated evidence, recorded 2026-11-19, from the first rehearsal (Actions run 44508808964, branch `fix/phase35-review-closure`, version `0.19.2-rc.1`): all three legs checked out the private plugin with the deploy key; the tag gates passed on the Linux leg; the Windows leg compiled the installer with Inno Setup 6.7.1 and started the smoke test, which failed on an assertion that expected exit code 7 (fixed: the real exit code is 1); the macOS leg failed at `codesign --deep` (fixed: section 5). The fixes have not run yet. Not yet run: the tag gates on the Windows and macOS legs after the fixes, the whole Windows smoke test, the `.dmg` build, and the release job (the 15-minute and 150-minute waits, the `gh run list` lookup of `release.yml`'s conclusion, the already-published branch and the publish step), because a rehearsal skips it. The attestation steps of `release.yml` are untested too (its header says it was not executed). `Directory.Build.props` sets `TreatWarningsAsErrors` to `true` and `AnalysisLevel` to `latest-all`, so a warning in a desktop project would fail a leg.
5. **The Windows installer has not passed its smoke test and was not run on a real machine.** The smoke test failed once at step 2b (item 4); the install, upgrade, uninstall and migration steps have not run. Not run by any job: the interactive wizard pages, the UAC prompt as a standard user, the launch checkbox, the Yes answer of the uninstaller, `/CURRENTUSER` from the command line by a person, the migration when another administrator approves UAC (the signed-in user's per-user install is not visible then and stays), and Windows 10 1607 as the oldest accepted version.
6. **macOS ad-hoc signature on this bundle.** The signing path described in section 5 (per-file signing, bundle signed without `--deep`) has not run on the runner since the fix; the `--deep` variant failed there. Not verified: that `codesign --verify` passes on the bundle, Gatekeeper's prompt on the `.zip` and the `.dmg`, the Open Anyway path, whether the app starts on Apple silicon after an ad-hoc signature, and that `hdiutil` produces a mountable image on `macos-26`.
7. **Windows signing and macOS notarization.** The owner has not decided whether to buy an Authenticode certificate or join the Apple Developer Program. Until then both warnings stay.
<!-- rebrand:keep-start -->
8. **Bundle identifier.** `com.swcstudiospace.autonogrammer` is the identifier the CHANGELOG names for the Android app. The Android project still uses `com.swcstudiospace.ompos`. The owner should confirm the macOS identifier.
<!-- rebrand:keep-end -->
9. **`release.yml` checksum name.** Resolved 2026-11-07: on `v0.16.1`, `release.yml` uploaded `SHA256SUMS` with `--clobber` and replaced the desktop checksums with sums for image tars that were then cancelled. Desktop checksums are `SHA256SUMS-desktop`. Image and SBOM checksums are `SHA256SUMS-images`.
10. **Docs index.** Resolved: this page and [Install Autonogrammer](../guides/install.md) are listed in `docs/SUMMARY.md`. `scripts/docs/check_gitbook.py` reports any page under `docs/` that the summary does not list.
11. **7-Zip on Windows.** The runner-images Windows image tests run `7z` by name, so `7z` is expected on `PATH` on the Windows runner. The script falls back to `zip` if `7z` is missing. Not verified: `7z` on the runner's `PATH` in a run, and how `7z` handles the Git Bash paths the script passes it.
12. **Artifact action versions.** The workflow uses `download-artifact` v8.0.2 to download artifacts from `upload-artifact` v7.0.2. The v8 release notes describe the change as transparent to callers. Not verified by a run.
13. **macOS image.** The workflow uses `macos-26`, as section 3 records. The owner should confirm that choice.
14. **Private plugin credential (OQ-63).** Resolved 2026-11-19: a read-only deploy key on `swcstudiospace/substrate` and the secret `SUBSTRATE_DEPLOY_KEY` (section 2). Not verified by a tag run (item 4).
15. **Licence of `agent-swarm` and `agent-substrate` (OQ-102).** Resolved 2026-11-19: both carry `AGPL-3.0-or-later` (swarm#49, substrate#221), and the pins moved by that one file. The source of the private `agent-substrate` repository is distributed inside the public installers under that licence.
16. **First-launch and checksum steps** (sections 5 and 6) and the manual checklists in [Install Autonogrammer](../guides/install.md) were not run on Windows or macOS.
