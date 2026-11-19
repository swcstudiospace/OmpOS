---
description: "Install the Autonogrammer desktop app from a GitHub release on Windows, macOS or Linux: download, checksum, install steps, first run, upgrade, uninstall and a manual test checklist."
---

# Install Autonogrammer

Status: The six desktop files are built by `desktop-release.yml`. The Windows installer is per machine and not signed; the macOS app is ad-hoc signed and not notarized. The Windows installer smoke test ran end to end on a Windows runner and passed (Actions run 44514593485, 2026-11-19); the macOS leg has not yet produced a signed bundle on a runner. No step on this page was run on a real Windows or macOS machine.

Last verified: 2026-11-19 (read against the installer script, the workflows and the app code; nothing was installed)

Applies to: v0.20.0

This page is for people who install the desktop app from a release. To run it from a source checkout, see [Getting started](getting-started.md). How the files are built and published is in [Desktop release packages](../runbooks/desktop-release.md).

## Download

Open the release page of the repository: <https://github.com/swcstudiospace/OmpOS/releases>. A release appears there only after all desktop files and the container files are attached, so a tag that failed to build has no public release.

Download the file for your system and `SHA256SUMS-desktop` from the same release:

| System | File |
| --- | --- |
| Windows x64 | `Autonogrammer-<version>-win-x64-Setup.exe` (installer), or `Autonogrammer-<version>-win-x64.zip` (no installer) |
| macOS arm64 (Apple silicon) | `Autonogrammer-<version>-osx-arm64.dmg` (disk image), or `Autonogrammer-<version>-osx-arm64.zip` |
| Linux x64 | `Autonogrammer-<version>-linux-x64.deb` (Debian and Ubuntu), or `Autonogrammer-<version>-linux-x64.tar.gz` |

`<version>` is the release version without the leading `v`. The other files on the release page (image archives, SBOM, `SHA256SUMS-images`) are for the server components, not for this app.

## Verify the download

`SHA256SUMS-desktop` lists the SHA-256 hash of each of the six desktop files. Compare the hash of your file with its line before you run it.

- **Windows (PowerShell):** `Get-FileHash -Algorithm SHA256 .\Autonogrammer-<version>-win-x64-Setup.exe`, then compare the `Hash` value with the line for that file, ignoring case.
- **macOS (Terminal):** `shasum -a 256 Autonogrammer-<version>-osx-arm64.dmg`, then compare the value with the line for that file.
- **Linux:** in the folder that holds the file and `SHA256SUMS-desktop`, run `sha256sum -c SHA256SUMS-desktop --ignore-missing`. The line for your file must end in `OK`.

A hash that differs means the download is damaged or not the published file. Download it again and do not run it.

The Windows and macOS commands were not run for this page. The Linux command was run on sample files.

## Prerequisites

The packages do not contain **omp** (the engine) or **Bun**. Install both separately on the machine that runs the app. Put `omp` on `PATH`, or in `~/.bun/bin`, `~/.local/bin` or `/usr/local/bin`, where the app looks for it. Without omp the app opens, but it cannot run a turn.

Other requirements, from [Desktop release packages](../runbooks/desktop-release.md):

- Windows: 64-bit Windows 10 version 1607 or newer. The Artifacts panel uses WebView2; whether its runtime must be installed on a clean machine is not verified.
- macOS: Apple silicon and macOS 14 or newer.
- Linux: the `.deb` declares the system libraries it needs. The Artifacts panel probably needs WebKitGTK; this is not verified.

## Windows

The installer is per machine, like a traditional Windows installer.

1. Double-click `Autonogrammer-<version>-win-x64-Setup.exe`.
2. The installer is not signed with a publisher certificate, so SmartScreen may show "Windows protected your PC". Choose **More info**, then **Run anyway**.
3. Windows asks for administrator rights (UAC). Approve it.
4. The wizard shows the licence (AGPL-3.0), the install folder (default `C:\Program Files\Autonogrammer`) and a checkbox for a desktop shortcut. The wizard refuses a folder that already holds other files, because uninstalling removes the whole install folder.
5. On the last page, "Launch Autonogrammer" starts the app as your normal user, not as administrator.

What you get:

- The app in `C:\Program Files\Autonogrammer`. The installer is 64-bit only, so it does not use `Program Files (x86)`.
- A Start Menu shortcut for all users, and the desktop shortcut if you chose it.
- An entry named Autonogrammer in Apps and Features (Settings, Apps, Installed apps).
- Your data is not in the install folder (see [Where your data lives](#where-your-data-lives)).

**Launching.** Use the Start Menu or desktop shortcut. The shortcuts start the app in your user profile folder. The app uses the folder it is launched from as its workspace, and falls back to your home folder when it is launched from its own install folder, a system folder, a drive root or a missing folder.

**Upgrading.** Run the newer `-Setup.exe`. It installs over the existing copy and keeps the install folder, because both versions use the same application ID. A running copy of the app is closed first and is not restarted. Your data is not touched. The plugin files under the install folder are replaced by the ones in the new version.

**Uninstalling.** Open Apps and Features, choose Autonogrammer, then Uninstall. The uninstaller asks whether to also remove your settings, sessions and the managed Omp runtime. The default answer is **No**, which keeps your data. Choose Yes to remove it.

**Per-user install (no administrator rights).** Run the installer from a command line with `/CURRENTUSER`. It installs under `%LOCALAPPDATA%\Programs\Autonogrammer` and adds shortcuts for your account only. The wizard has no page to choose between the two modes: the default is all users, and `/ALLUSERS` states it. If you later run an all-users install, it first removes the per-user install it finds for the signed-in account and keeps your data.

**Silent install (for IT).**

```powershell
.\Autonogrammer-<version>-win-x64-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS=desktopicon /LOG=C:\Temp\autonogrammer-install.log
```

- `/ALLUSERS` (the default) or `/CURRENTUSER` selects the mode. An all-users install must be run from an elevated prompt, or it waits for UAC.
- `/TASKS=desktopicon` creates the desktop shortcut; leave it out for none.
- `/DIR="D:\Apps\Autonogrammer"` changes the install folder. A folder that already holds other files, and is not an Autonogrammer install, is refused even in a silent install: Setup runs the folder-page check with `/VERYSILENT`, exits with code 1, installs nothing and leaves the folder untouched (observed once on a CI runner with Inno Setup 6.7.1).
- `/LOG=<file>` writes the Inno Setup log. The "Launch Autonogrammer" step is skipped in a silent install.
- To uninstall silently, run `unins000.exe /VERYSILENT /SUPPRESSMSGBOXES` from the install folder. The data question then takes its default answer, No.

**The zip.** `Autonogrammer-<version>-win-x64.zip` needs no installer: extract it and run `Autonogrammer.Native.exe`. It creates no shortcut and no uninstall entry.

## macOS

The app is ad-hoc signed: each Mach-O file in the bundle and then the bundle itself are signed with identity `-`. That is not a Developer ID signature and not notarization, so Gatekeeper shows its unidentified-developer prompt on the first launch. The signing step failed on its first CI run and was fixed; the fixed step has not run yet.

1. Open `Autonogrammer-<version>-osx-arm64.dmg`.
2. Drag `Autonogrammer.app` onto the `Applications` link in the window. Eject the disk image.
3. The first launch: in Finder, Control-click `Autonogrammer.app` in Applications, choose **Open**, and confirm.
4. If macOS still refuses, open System Settings, Privacy & Security, and choose **Open Anyway** for Autonogrammer.
5. If the app is still blocked, remove the quarantine attribute the browser added: `xattr -dr com.apple.quarantine /Applications/Autonogrammer.app`.

The `.zip` holds the same `Autonogrammer.app`. Extract it with Finder (double-click) or with `ditto -x -k <zip> <folder>`, then move the app to `/Applications` and follow steps 3 to 5. Do not use the `unzip` command: it drops the extended attributes that hold the signatures of the bundled `.dll` files, and the app then fails the verification below, although it still starts.

To uninstall, drag `Autonogrammer.app` to the Trash. Your data stays (see below) until you delete it.

These steps are the documented fallbacks. They were not run on a Mac.

## Linux

**Debian and Ubuntu (`.deb`).**

```bash
sudo apt install ./Autonogrammer-<version>-linux-x64.deb
autonogrammer
```

The package installs the app in `/opt/autonogrammer`, the command `/usr/bin/autonogrammer`, a desktop entry and an icon. Remove it with `sudo apt remove autonogrammer`. No job has installed the `.deb` on a Debian or Ubuntu system.

**Any x64 distribution (`.tar.gz`).**

```bash
tar -xzf Autonogrammer-<version>-linux-x64.tar.gz
./Autonogrammer-<version>-linux-x64/Autonogrammer.Native
```

## First run

- Start the app. It opens in your **home folder** unless you launch it from a terminal inside a project folder: the app uses its launch folder as the workspace, and replaces its own install folder, system folders and filesystem roots with your home folder. To work on a project, start the app from a terminal in that folder (`cd` into it, then run the command or the executable).
- It starts no omp process until you send a prompt or open an omp terminal.
- Press Ctrl+, to open Settings and add a provider key on the Models page ([Settings guide](settings-guide.md), [Choose a model provider](providers-guide.md)).
- The packages bundle the plugins under `plugins/` beside the executable: Ultrathink, Swarm, Substrate, GSD-Core and the hooks plugin, and a source-only copy of Realm that is not loaded. A plugin that cannot start shows Unavailable with the fix text, and sessions then run as plain omp.

## Where your data lives

The app is built to write nothing into its install folder (the installer smoke test is written to check this for a 15-second run; it has not run yet). Install, upgrade and uninstall leave your data alone, except when you answer Yes to the uninstaller's data question on Windows.

| Data | Windows | Linux and macOS |
| --- | --- | --- |
| Settings | `%APPDATA%\Autonogrammer` | The per-user application data folder of .NET, then `Autonogrammer` (on Linux `~/.config/Autonogrammer`) |
| Sessions and loop state | `%USERPROFILE%\.local\share\Autonogrammer`, or `XDG_DATA_HOME` when it is an absolute path | `~/.local/share/Autonogrammer`, or `XDG_DATA_HOME` when it is an absolute path |
| Managed Omp runtime and encrypted secrets | `%LOCALAPPDATA%\Autonogrammer` | The per-user local application data folder, then `Autonogrammer` |
| WebView2 data (Artifacts panel) | `%LOCALAPPDATA%\Autonogrammer\webview2` | not applicable |

The `AUTONOGRAMMER_STATE_DIR` variable moves the state root. The Linux and macOS settings paths come from the .NET folder rules and the app code; they were not checked on a Mac.

## Manual test checklist

Run these on a machine that never had Autonogrammer, and write down what you see. "Expected" is what the installer script and the app code are written to do. A different result is a finding: report it with the screen text.

**No job has run any of these steps.** The CI smoke test (`scripts/release/windows/smoke-test-installer.ps1`) covers only a silent install, upgrade, uninstall and migration on a Windows runner, and it has not run on a runner yet. Nothing on this page was run on a real Windows or macOS machine.

### Windows

| Step | Expected |
| --- | --- |
| 1. Compute the SHA-256 of the setup file and compare it with `SHA256SUMS-desktop`. | The hash equals the listed line. |
| 2. Double-click the setup file. | SmartScreen may warn. More info, then Run anyway, continues. Then a UAC prompt appears. Record the publisher text it shows. |
| 3. Go through the wizard. | A licence page (AGPL-3.0), a folder page that defaults to `C:\Program Files\Autonogrammer`, a desktop shortcut option, and a last page with "Launch Autonogrammer" checked. |
| 4. Choose a non-empty folder that is not an Autonogrammer install, then Next. | The wizard refuses it with a message and stays on the folder page. |
| 5. Finish with "Launch Autonogrammer" checked. | The app window opens. Task Manager shows it running without administrator rights. |
| 6. Check the Start Menu, the desktop (if chosen) and Apps and Features. | A Start Menu entry, a desktop shortcut, and an Autonogrammer entry with the installed version. |
| 7. Check the workspace the app shows, started from the Start Menu. | Your user profile folder, not `C:\Program Files\Autonogrammer`. |
| 8. Open Settings (Ctrl+,), then a document in the Artifacts panel. | Settings opens. Record whether the Artifacts panel renders, and whether it asks for WebView2. |
| 9. Create a setting or a session, then run the same setup again. | It installs over the existing copy in the same folder. One Autonogrammer entry remains. The setting or session is still there. |
| 10. Uninstall from Apps and Features and answer No. | The install folder and the shortcuts are gone. `%APPDATA%\Autonogrammer` is still there. |
| 11. Install again, uninstall, and answer Yes. | The settings, sessions and managed runtime folders from the table above are removed. |
| 12. Optional: from an elevated prompt, run the setup with `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=<file>`. | No window appears, the app is installed, and the log file exists. |

### macOS

| Step | Expected |
| --- | --- |
| 1. Compute `shasum -a 256` of the `.dmg` and compare it with `SHA256SUMS-desktop`. | The hash equals the listed line. |
| 2. Open the `.dmg`. | A window shows `Autonogrammer.app` and an `Applications` link. |
| 3. Drag the app onto `Applications` and eject the disk image. | The app is in `/Applications`. |
| 4. Run `codesign --verify --deep --strict --verbose=2 /Applications/Autonogrammer.app`, `codesign -dv --verbose=2 /Applications/Autonogrammer.app`, and list `Contents/Resources/plugins`. | The first command reports no error (it prints `satisfies its Designated Requirement`) when the app was extracted with Finder or `ditto`. The second shows `Signature=adhoc`. The folder lists the bundled plugins. |
| 5. Start the app by double-click. | Gatekeeper blocks it or shows an unidentified-developer prompt. Record the exact text. |
| 6. Control-click the app, choose Open, confirm. | The app window opens. If not, try Privacy & Security, Open Anyway, and then the `xattr` command, and record which step worked. |
| 7. Check the workspace the app shows. | Your home folder. A launch from Finder or the Dock may start the process in `/` or in the bundle, and the app replaces those with the home folder. |
| 8. Open Settings (Ctrl+, or the equivalent shortcut), then a document in the Artifacts panel. | Settings opens. Record whether the Artifacts panel renders. |
| 9. Drag the app to the Trash. | The app is gone. Your data folders are still there. |
