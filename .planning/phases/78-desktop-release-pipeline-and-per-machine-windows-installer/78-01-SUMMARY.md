---
phase: 78-desktop-release-pipeline-and-per-machine-windows-installer
plan: "01"
subsystem: windows-installer, ci-smoke
tags: [inno-setup, per-machine, uac, smoke-test]
dependency_graph:
  requires: []
  provides: [per-machine-installer, installer-smoke-test]
  affects: [78-03, 78-06]
tech_stack:
  added: []
  patterns: [admin/UAC install, Program Files, upgrade-in-place, legacy per-user silent migration, data-kept uninstall]
key_files:
  created:
    - scripts/release/windows/Autonogrammer.iss
    - scripts/release/windows/smoke-test-installer.ps1
decisions:
  - "Unsigned by owner decision; SmartScreen note (78-05) is the documented workaround, not signing"
  - "No x86 build, no auto-update, no store packaging (D-05)"
metrics:
  tasks: 2
  status: complete
---

# Phase 78 Plan 01: Per-Machine Windows Installer Summary

**One-liner:** `scripts/release/windows/Autonogrammer.iss` installs per-machine (UAC admin, `C:\Program Files\Autonogrammer`) and `smoke-test-installer.ps1` proves install/launch/upgrade/uninstall/legacy-migration on a Windows runner — exercised in the green rehearsal recorded by 78-VERIFICATION.

## Tasks Executed

**Task 1 (auto):** Inno script confirmed/created per D-04/D-05: `PrivilegesRequired=admin` with `PrivilegesRequiredOverridesAllowed=commandline`, `ArchitecturesInstallIn64BitMode=x64compatible`, `DefaultDirName={autopf}\Autonogrammer`, `LicenseFile=..\..\..\LICENSE`, `CloseApplications=yes`, `[Run]` with `postinstall skipifsilent runasoriginaluser`, uninstall prompt keeping user data by default (No), Pascal `[Code]` step uninstalling the legacy per-user entry (AppId `3C4EC86C-E483-4B14-99BC-3C096BF45A45`) silently.

**Task 2 (auto):** `smoke-test-installer.ps1` (parameters SetupPath/Version/LogDirectory) with `Write-Step` narration: preconditions (pwsh7, 64-bit, admin), silent install (`/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS=desktopicon`), HKLM entry + DisplayVersion + shortcuts, 15-second launch (still alive, nothing written beside exe), upgrade in place (data survives), uninstall (data stays), legacy per-user → all-users migration.

## Verification Performed

- `grep -nE "PrivilegesRequired=admin|DefaultDirName=.autopf|runasoriginaluser" Autonogrammer.iss` → lines 72, 74, 107 observed this batch (all three present)
- `grep -c "Write-Step" smoke-test-installer.ps1` → **13** (≥10 required)
- `git log --oneline -3 -- scripts/release/windows/Autonogrammer.iss` → tip `c58b580c` (silent-install folder-guard fix + crash diagnostics), `fed3d3f6` (per-machine install), `511077e8` (#6)
- Rehearsal run `44524125822` (78-VERIFICATION criterion 4): desktop-release package leg ran the smoke test green on windows-2025

## Deviations

None. Compile and smoke proof are the rehearsal/rc runs, recorded only in 78-06/78-VERIFICATION per the plan's own rule.

**Status:** 78-01 complete.
