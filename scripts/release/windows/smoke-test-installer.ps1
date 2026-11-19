<#
.SYNOPSIS
Smoke-tests the Autonogrammer Windows installer (Inno Setup, all-users install).

.DESCRIPTION
Runs on Windows with PowerShell 7 as an administrator (the windows-2025 runner user is one, with UAC effectively off).
It installs the setup silently for all users, checks the files, registry entry and shortcuts, launches the installed
app for 15 seconds, checks that the app wrote nothing beside its executable, upgrades in place, uninstalls, and checks
the legacy per-user to per-machine migration. Exit code 0 on success, 1 on any failure. On failure the last 80 lines of
every Inno log in -LogDirectory are printed.

.EXAMPLE
pwsh -NoProfile -File scripts/release/windows/smoke-test-installer.ps1 -SetupPath dist\Autonogrammer-0.20.0-win-x64-Setup.exe -Version 0.20.0 -LogDirectory $env:RUNNER_TEMP\installer-logs
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$SetupPath,
  [Parameter(Mandatory = $true)][string]$Version,
  [Parameter(Mandatory = $true)][string]$LogDirectory
)

$ErrorActionPreference = 'Stop'

# Must equal AppId in Autonogrammer.iss (Inno appends _is1 to the registry key name).
$UninstallKeyName = '{3C4EC86C-E483-4B14-99BC-3C096BF45A45}_is1'
$UninstallRoot = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
$ExeName = 'Autonogrammer.Native.exe'
$SilentArgs = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')

$script:installDir = $null
$script:logDir = $null

function Write-Step([string]$Text) { Write-Host "==> $Text" }

function Assert-True([bool]$Condition, [string]$Message) {
  if (-not $Condition) { throw $Message }
}

function Get-NormalizedPath([string]$Path) {
  return [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Test-PathUnder([string]$Path, [string]$Root) {
  return (Get-NormalizedPath $Path).StartsWith((Get-NormalizedPath $Root) + '\', [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-SamePath([string]$Left, [string]$Right) {
  return (Get-NormalizedPath $Left) -ieq (Get-NormalizedPath $Right)
}

function Open-RegistrySubKey([string]$Hive, [string]$View, [string]$SubKey) {
  $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]$Hive, [Microsoft.Win32.RegistryView]$View)
  try { return $base.OpenSubKey($SubKey) } finally { $base.Dispose() }
}

# Returns the uninstall values of the Autonogrammer entry in one hive and registry view, or $null when it is absent.
function Get-UninstallEntry([string]$Hive, [string]$View) {
  $key = Open-RegistrySubKey $Hive $View "$UninstallRoot\$UninstallKeyName"
  if ($null -eq $key) { return $null }
  try {
    return [pscustomobject]@{
      DisplayName     = [string]$key.GetValue('DisplayName')
      DisplayVersion  = [string]$key.GetValue('DisplayVersion')
      Publisher       = [string]$key.GetValue('Publisher')
      UninstallString = [string]$key.GetValue('UninstallString')
      InstallLocation = [string]$key.GetValue('InstallLocation')
    }
  } finally { $key.Dispose() }
}

# Lists every uninstall entry whose DisplayName is Autonogrammer, whatever its key name, in all hives and views Setup could use.
function Get-AutonogrammerEntries {
  $found = @()
  foreach ($combo in @(@('LocalMachine', 'Registry64'), @('LocalMachine', 'Registry32'), @('CurrentUser', 'Registry64'))) {
    $root = Open-RegistrySubKey $combo[0] $combo[1] $UninstallRoot
    if ($null -eq $root) { continue }
    try {
      foreach ($name in $root.GetSubKeyNames()) {
        $sub = $root.OpenSubKey($name)
        if ($null -eq $sub) { continue }
        try {
          if ([string]$sub.GetValue('DisplayName') -eq 'Autonogrammer') { $found += "$($combo[0])/$($combo[1])/$name" }
        } finally { $sub.Dispose() }
      }
    } finally { $root.Dispose() }
  }
  return $found
}

function Wait-Until([scriptblock]$Condition, [int]$TimeoutSeconds, [string]$Description) {
  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  while (-not (& $Condition)) {
    if ((Get-Date) -ge $deadline) { throw "timed out after $TimeoutSeconds seconds waiting for: $Description" }
    Start-Sleep -Seconds 2
  }
}

function Invoke-Elevated([string]$FilePath, [string[]]$Arguments, [string]$Description) {
  Write-Host "    $FilePath $($Arguments -join ' ')"
  $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -Wait -PassThru
  Assert-True ($process.ExitCode -eq 0) "$Description exited with code $($process.ExitCode)"
}

# Every process that descends from $RootId (children, grandchildren, ...), by Win32_Process.ParentProcessId.
function Get-DescendantProcessIds([int]$RootId) {
  $all = @(Get-CimInstance -ClassName Win32_Process)
  $result = New-Object System.Collections.Generic.List[int]
  $queue = New-Object System.Collections.Generic.Queue[int]
  $queue.Enqueue($RootId)
  while ($queue.Count -gt 0) {
    $parent = $queue.Dequeue()
    foreach ($candidate in $all) {
      $id = [int]$candidate.ProcessId
      if (([int]$candidate.ParentProcessId -eq $parent) -and ($id -ne $parent) -and -not $result.Contains($id)) {
        $result.Add($id)
        $queue.Enqueue($id)
      }
    }
  }
  return $result.ToArray()
}

# Stops the given process ids, then every process whose image is under $Directory, so nothing holds files in the install folder.
function Stop-InstalledProcesses([string]$Directory, [int[]]$ExtraIds = @()) {
  foreach ($id in $ExtraIds) {
    $extra = Get-Process -Id $id -ErrorAction SilentlyContinue
    if ($null -ne $extra) {
      Write-Host "    stopping child process $($extra.ProcessName) ($id)"
      Stop-Process -Id $id -Force -ErrorAction SilentlyContinue
      $extra.WaitForExit(30000) | Out-Null
    }
  }
  foreach ($process in @(Get-Process -ErrorAction SilentlyContinue)) {
    $path = $null
    try { $path = $process.Path } catch { continue }
    if ($path -and (Test-PathUnder $path $Directory)) {
      Write-Host "    stopping $($process.ProcessName) ($($process.Id))"
      Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
      $process.WaitForExit(30000) | Out-Null
    }
  }
}

# Always call this as @(Get-FolderSnapshot ...): the function emits the lines unwrapped.
function Get-FolderSnapshot([string]$Root) {
  $lines = @(Get-ChildItem -LiteralPath $Root -Recurse -Force | Sort-Object FullName | ForEach-Object {
      if ($_.PSIsContainer) { "D|$($_.FullName)" } else { "F|$($_.FullName)|$($_.Length)" }
    })
  return $lines
}

# Splits an Inno UninstallString (a quoted path, possibly followed by parameters) into the path and the parameters.
function Get-UninstallCommand([string]$UninstallString) {
  $text = $UninstallString.Trim()
  if ($text -match '^"([^"]+)"\s*(.*)$') {
    return [pscustomobject]@{ Path = $Matches[1]; Arguments = $Matches[2] }
  }
  return [pscustomobject]@{ Path = $text; Arguments = '' }
}

# Runs the uninstaller exactly as the Apps and Features Uninstall button would, plus the silent switches and a log file.
function Invoke-RegisteredUninstall([string]$UninstallString, [string]$LogPath, [string]$Description) {
  $command = Get-UninstallCommand $UninstallString
  $arguments = @()
  if ($command.Arguments) { $arguments += $command.Arguments }
  $arguments += $SilentArgs
  $arguments += "/LOG=`"$LogPath`""
  Invoke-Elevated $command.Path $arguments $Description
}

function Get-Shortcut([string]$Path) {
  $shell = New-Object -ComObject WScript.Shell
  try { return $shell.CreateShortcut($Path) } finally { [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) }
}

function Assert-Shortcut([string]$Path, [string]$ExpectedTarget, [string]$InstallDir) {
  Assert-True (Test-Path -LiteralPath $Path -PathType Leaf) "shortcut missing: $Path"
  $link = Get-Shortcut $Path
  Assert-True (Test-SamePath $link.TargetPath $ExpectedTarget) "shortcut $Path targets '$($link.TargetPath)', expected '$ExpectedTarget'"
  $workingDirectory = [string]$link.WorkingDirectory
  Assert-True ($workingDirectory.Trim() -ne '') "shortcut $Path has an empty working directory, so Windows would use the install folder"
  # The value is stored unexpanded (%USERPROFILE%): Windows expands it for whoever starts the shortcut.
  $expanded = [Environment]::ExpandEnvironmentVariables($workingDirectory)
  Assert-True (-not (Test-SamePath $expanded $InstallDir)) "shortcut $Path starts the app in the install folder: $workingDirectory"
  Assert-True (-not (Test-PathUnder $expanded $InstallDir)) "shortcut $Path starts the app below the install folder: $workingDirectory"
  Write-Host "    $Path -> $($link.TargetPath) (start in: $workingDirectory)"
}

function Assert-MachineInstall([string]$Version, [string]$InstallDir) {
  $exe = Join-Path $InstallDir $ExeName
  Assert-True (Test-Path -LiteralPath $exe -PathType Leaf) "installed executable missing: $exe"
  Assert-True (Test-Path -LiteralPath (Join-Path $InstallDir 'unins000.exe') -PathType Leaf) 'unins000.exe missing from the install folder'
  $entry = Get-UninstallEntry 'LocalMachine' 'Registry64'
  Assert-True ($null -ne $entry) "HKLM (64-bit view) uninstall key missing: $UninstallRoot\$UninstallKeyName"
  Assert-True ($entry.DisplayName -eq 'Autonogrammer') "DisplayName is '$($entry.DisplayName)', expected 'Autonogrammer'"
  Assert-True ($entry.DisplayVersion -eq $Version) "DisplayVersion is '$($entry.DisplayVersion)', expected '$Version'"
  Assert-True ($entry.Publisher -eq 'SWC Studio') "Publisher is '$($entry.Publisher)', expected 'SWC Studio'"
  Assert-True ($entry.UninstallString.Trim() -ne '') 'UninstallString is empty'
  Assert-True ($entry.UninstallString.Trim().StartsWith('"')) "UninstallString is not quoted, so Windows cannot run it from a path with spaces: $($entry.UninstallString)"
  $uninstallCommand = Get-UninstallCommand $entry.UninstallString
  Assert-True (Test-SamePath $uninstallCommand.Path (Join-Path $InstallDir 'unins000.exe')) "UninstallString runs '$($uninstallCommand.Path)', expected '$(Join-Path $InstallDir 'unins000.exe')'"
  Assert-True ($entry.InstallLocation.Trim() -ne '') 'InstallLocation is empty'
  Assert-True (Test-SamePath $entry.InstallLocation $InstallDir) "InstallLocation is '$($entry.InstallLocation)', expected '$InstallDir'"
  return $entry
}

function Write-LogTails([string]$Directory) {
  if (-not $Directory -or -not (Test-Path -LiteralPath $Directory -PathType Container)) { return }
  $logs = @(Get-ChildItem -LiteralPath $Directory -File -Filter '*.log' -ErrorAction SilentlyContinue | Sort-Object Name)
  if ($logs.Count -eq 0) { Write-Host "No Inno logs found in $Directory" }
  foreach ($log in $logs) {
    Write-Host "---- $($log.FullName) (last 80 lines) ----"
    Get-Content -LiteralPath $log.FullName -Tail 80 -ErrorAction Continue | ForEach-Object { Write-Host $_ }
    Write-Host "---- end of $($log.Name) ----"
  }
}

# Printed when the app exits before the liveness window ends. It never throws: a diagnostic must not hide the real failure.
function Write-AppExitDiagnostics($App, [string]$StdoutPath, [string]$StderrPath) {
  try {
    $code = $null
    try { $App.WaitForExit(5000) | Out-Null; $code = $App.ExitCode } catch { Write-Host "    could not read the app exit code: $_" }
    Write-Host "    app exit code: $code"
    foreach ($file in @($StdoutPath, $StderrPath)) {
      if (Test-Path -LiteralPath $file -PathType Leaf) {
        Write-Host "---- $file (last 40 lines) ----"
        Get-Content -LiteralPath $file -Tail 40 -ErrorAction Continue | ForEach-Object { Write-Host $_ }
        Write-Host "---- end of $(Split-Path -Leaf $file) ----"
      } else {
        Write-Host "    no output file: $file"
      }
    }
    # Error (2) and Warning (3) events, plus Critical (1). Windows Error Reporting writes its crash report as an Information event, so
    # events of that provider are listed whatever their level.
    $providers = @('.NET Runtime', 'Application Error', 'Windows Error Reporting')
    Write-Host '    Application event log, last 3 minutes (.NET Runtime, Application Error, Windows Error Reporting):'
    try {
      $events = @(Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = (Get-Date).AddMinutes(-3) } -ErrorAction Stop |
          Where-Object { ($providers -contains $_.ProviderName) -and (($_.Level -in 1, 2, 3) -or ($_.ProviderName -eq 'Windows Error Reporting')) } |
          Select-Object -First 10)
      if ($events.Count -eq 0) { Write-Host '    (no matching events)' }
      foreach ($logEvent in $events) {
        $text = ([string]$logEvent.Message) -replace '\s+', ' '
        if ($text.Length -gt 600) { $text = $text.Substring(0, 600) }
        Write-Host "    [$($logEvent.TimeCreated.ToString('s'))] $($logEvent.ProviderName) id $($logEvent.Id) $($logEvent.LevelDisplayName): $text"
      }
    } catch {
      Write-Host "    could not read the Application event log: $_"
    }
  } catch {
    Write-Host "    the diagnostics failed: $_"
  }
}

function Test-Installer {
  Write-Step 'Preconditions: PowerShell 7 on 64-bit Windows as an administrator'
  Assert-True ($IsWindows) 'this smoke test runs on Windows only'
  Assert-True ([Environment]::Is64BitProcess) 'run this script with 64-bit PowerShell: $env:ProgramFiles must be C:\Program Files'
  $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
  Assert-True ((New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) 'the smoke test must run as an administrator, otherwise the installer waits for a UAC prompt'

  New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null
  $script:logDir = (Resolve-Path -LiteralPath $LogDirectory).Path
  $installDir = Join-Path $env:ProgramFiles 'Autonogrammer'
  $script:installDir = $installDir
  $exe = Join-Path $installDir $ExeName
  $uninstaller = Join-Path $installDir 'unins000.exe'
  $legacyDir = Join-Path $env:LOCALAPPDATA 'Programs\Autonogrammer'
  $legacyExe = Join-Path $legacyDir $ExeName
  $x86Dir = Join-Path ${env:ProgramFiles(x86)} 'Autonogrammer'
  $startLink = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Autonogrammer.lnk'
  $desktopLink = Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'Autonogrammer.lnk'
  $userStartLink = Join-Path ([Environment]::GetFolderPath('Programs')) 'Autonogrammer.lnk'
  $userDesktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Autonogrammer.lnk'
  $markerDir = Join-Path $env:APPDATA 'Autonogrammer'
  $marker = Join-Path $markerDir 'smoke-marker.txt'

  Write-Step "1. The setup file: $SetupPath"
  Assert-True (Test-Path -LiteralPath $SetupPath -PathType Leaf) "setup not found: $SetupPath"
  $setup = (Resolve-Path -LiteralPath $SetupPath).Path
  $item = Get-Item -LiteralPath $setup
  Assert-True ($item.Length -gt 0) "setup is empty: $setup"
  $stream = [System.IO.File]::OpenRead($setup)
  try {
    $head = New-Object byte[] 2
    $read = $stream.Read($head, 0, 2)
  } finally { $stream.Dispose() }
  Assert-True (($read -eq 2) -and ($head[0] -eq 0x4D) -and ($head[1] -eq 0x5A)) 'setup does not start with MZ, so it is not a Windows executable'
  Write-Host "    size: $($item.Length) bytes"
  Write-Host "    sha256: $((Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash)"

  Write-Step '2. No previous install on this machine'
  $existing = @(Get-AutonogrammerEntries)
  Assert-True ($existing.Count -eq 0) "an Autonogrammer uninstall entry already exists: $($existing -join ', ')"
  Assert-True ($null -eq (Get-UninstallEntry 'LocalMachine' 'Registry64')) 'HKLM (64-bit view) uninstall key already exists'
  Assert-True ($null -eq (Get-UninstallEntry 'CurrentUser' 'Registry64')) 'HKCU uninstall key already exists'
  foreach ($folder in @($installDir, $legacyDir, $x86Dir)) {
    Assert-True (-not (Test-Path -LiteralPath $folder)) "install folder already exists: $folder"
  }

  Write-Step '2b. Setup refuses a folder that holds other files (silent install with /DIR)'
  $foreignDir = Join-Path ([System.IO.Path]::GetTempPath()) ('autonogrammer-smoke-foreign-' + [guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory -Force -Path $foreignDir | Out-Null
  $keepFile = Join-Path $foreignDir 'keep.txt'
  Set-Content -LiteralPath $keepFile -Value 'this file must survive'
  try {
    $refusalArgs = $SilentArgs + @('/ALLUSERS', "/DIR=`"$foreignDir`"", "/LOG=`"$(Join-Path $script:logDir 'foreign-folder.log')`"")
    Write-Host "    $setup $($refusalArgs -join ' ')"
    $refused = Start-Process -FilePath $setup -ArgumentList $refusalArgs -Wait -PassThru
    Write-Host "    exit code: $($refused.ExitCode)"
    Assert-True ($refused.ExitCode -ne 0) 'the installer accepted a folder that already holds other files'
    # Observed in GitHub Actions run 44508808964 (Inno Setup 6.7.1): a silent install still runs NextButtonClick for the hidden
    # folder page. Its message box is suppressed, it returns False, and the Inno log says 'Failed to proceed to next wizard page;
    # aborting.' The exit code is 1 (not 7: PrepareToInstall is not reached). The log text proves the refusal came from our guard.
    $foreignLog = Join-Path $script:logDir 'foreign-folder.log'
    Assert-True (Test-Path -LiteralPath $foreignLog -PathType Leaf) "the Inno log was not written: $foreignLog"
    Assert-True (Select-String -LiteralPath $foreignLog -SimpleMatch 'already contains other files' -Quiet) "the refusal did not come from the folder guard: $foreignLog does not contain 'already contains other files'"
    Assert-True (Test-Path -LiteralPath $keepFile -PathType Leaf) "the installer removed $keepFile"
    Assert-True (@(Get-ChildItem -LiteralPath $foreignDir -Force).Count -eq 1) "the installer wrote into the refused folder $foreignDir"
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $foreignDir $ExeName))) "the installer put $ExeName into the refused folder"
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $foreignDir 'unins000.exe'))) 'the installer put unins000.exe into the refused folder'
    Assert-True (@(Get-AutonogrammerEntries).Count -eq 0) 'the refused install created an Autonogrammer uninstall entry'
  } finally {
    Remove-Item -LiteralPath $foreignDir -Recurse -Force -ErrorAction SilentlyContinue
  }

  Write-Step '3. Silent all-users install'
  Invoke-Elevated $setup ($SilentArgs + @('/ALLUSERS', '/TASKS=desktopicon', "/LOG=`"$(Join-Path $script:logDir 'install.log')`"")) 'the installer'

  Write-Step '4. Installed files, Apps and Features entry and shortcuts'
  Assert-True (Test-PathUnder $installDir $env:ProgramFiles) "install folder is not under $env:ProgramFiles"
  Assert-True (-not (Test-PathUnder $installDir ${env:ProgramFiles(x86)})) 'install folder is under Program Files (x86)'
  Assert-True (-not (Test-PathUnder $installDir $env:LOCALAPPDATA)) 'install folder is under LOCALAPPDATA'
  Assert-True (Test-Path -LiteralPath $exe -PathType Leaf) "installed executable missing: $exe"
  Assert-True (Test-Path -LiteralPath $uninstaller -PathType Leaf) "uninstaller missing: $uninstaller"
  $entry = Assert-MachineInstall $Version $installDir
  Write-Host "    $UninstallRoot\${UninstallKeyName}: $($entry.DisplayName) $($entry.DisplayVersion), $($entry.Publisher), $($entry.InstallLocation)"
  Assert-True ($null -eq (Get-UninstallEntry 'CurrentUser' 'Registry64')) 'an all-users install also wrote a per-user uninstall key'
  Assert-Shortcut $startLink $exe $installDir
  Assert-Shortcut $desktopLink $exe $installDir

  Write-Step '5. Launch the installed app for 15 seconds; it must not write beside its executable'
  $before = @(Get-FolderSnapshot $installDir)
  Write-Host "    $($before.Count) entries under $installDir"
  $appOut = Join-Path $script:logDir 'app-stdout.log'
  $appErr = Join-Path $script:logDir 'app-stderr.log'
  $app = Start-Process -FilePath $exe -WorkingDirectory $env:USERPROFILE -RedirectStandardOutput $appOut -RedirectStandardError $appErr -PassThru
  # Reading the handle keeps it open so that ExitCode stays readable after the process exits.
  $null = $app.Handle
  try {
    Start-Sleep -Seconds 15
    if ($app.HasExited) { Write-AppExitDiagnostics $app $appOut $appErr }
    Assert-True (-not $app.HasExited) "$ExeName exited within 15 seconds with code $($app.ExitCode)"
  } finally {
    $childIds = @()
    try { $childIds = @(Get-DescendantProcessIds $app.Id) } catch { Write-Host "could not list child processes: $_" }
    if (-not $app.HasExited) { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue }
    $app.WaitForExit(30000) | Out-Null
    Stop-InstalledProcesses $installDir $childIds
  }
  $after = @(Get-FolderSnapshot $installDir)
  $difference = @(Compare-Object -ReferenceObject $before -DifferenceObject $after)
  if ($difference.Count -gt 0) {
    $listing = ($difference | Select-Object -First 20 | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }) -join [Environment]::NewLine
    throw "the app changed the install folder while it ran ($($difference.Count) differences; Program Files is read-only for normal users):$([Environment]::NewLine)$listing"
  }
  Write-Host '    install folder unchanged'

  Write-Step '6. Upgrade in place: install the same setup again, user data must survive'
  New-Item -ItemType Directory -Force -Path $markerDir | Out-Null
  Set-Content -LiteralPath $marker -Value "smoke marker $Version"
  Invoke-Elevated $setup ($SilentArgs + @('/ALLUSERS', '/TASKS=desktopicon', "/LOG=`"$(Join-Path $script:logDir 'upgrade.log')`"")) 'the upgrade install'
  $entries = @(Get-AutonogrammerEntries)
  Assert-True ($entries.Count -eq 1) "expected exactly one Autonogrammer uninstall entry after the upgrade, found $($entries.Count): $($entries -join ', ')"
  $upgraded = Assert-MachineInstall $Version $installDir
  Assert-True (Test-SamePath $upgraded.InstallLocation $entry.InstallLocation) "the upgrade moved the install: '$($entry.InstallLocation)' -> '$($upgraded.InstallLocation)'"
  Assert-True (Test-Path -LiteralPath $exe -PathType Leaf) "installed executable missing after the upgrade: $exe"
  Assert-True (Test-Path -LiteralPath $marker -PathType Leaf) "user data was touched by the upgrade: $marker is gone"
  Assert-True ($null -eq (Get-UninstallEntry 'CurrentUser' 'Registry64')) 'the upgrade created a per-user uninstall key'

  Write-Step '7. Silent uninstall: everything goes except the user data'
  Invoke-RegisteredUninstall $upgraded.UninstallString (Join-Path $script:logDir 'uninstall.log') 'the registered uninstaller'
  Wait-Until { -not (Test-Path -LiteralPath $installDir) } 120 "the install folder $installDir to disappear"
  Wait-Until { $null -eq (Get-UninstallEntry 'LocalMachine' 'Registry64') } 60 'the HKLM uninstall key to disappear'
  Wait-Until { -not (Test-Path -LiteralPath $startLink) } 60 "the Start Menu shortcut $startLink to disappear"
  Wait-Until { -not (Test-Path -LiteralPath $desktopLink) } 60 "the desktop shortcut $desktopLink to disappear"
  Assert-True (@(Get-AutonogrammerEntries).Count -eq 0) 'an Autonogrammer uninstall entry remains after the uninstall'
  Assert-True (Test-Path -LiteralPath $marker -PathType Leaf) "the uninstaller removed user data: $marker is gone (the default answer must be No)"
  Remove-Item -LiteralPath $marker -Force
  if ((Test-Path -LiteralPath $markerDir) -and -not (Get-ChildItem -LiteralPath $markerDir -Force)) { Remove-Item -LiteralPath $markerDir -Force }

  Write-Step '8. Legacy per-user install is replaced by an all-users install'
  Invoke-Elevated $setup ($SilentArgs + @('/CURRENTUSER', '/TASKS=desktopicon', "/LOG=`"$(Join-Path $script:logDir 'legacy-install.log')`"")) 'the per-user install'
  Assert-True (Test-Path -LiteralPath $legacyExe -PathType Leaf) "the per-user install did not create $legacyExe"
  $legacyEntry = Get-UninstallEntry 'CurrentUser' 'Registry64'
  Assert-True ($null -ne $legacyEntry) 'the per-user install did not create the HKCU uninstall key'
  Assert-True (Test-SamePath $legacyEntry.InstallLocation $legacyDir) "the per-user InstallLocation is '$($legacyEntry.InstallLocation)', expected '$legacyDir'"
  Assert-True ($null -eq (Get-UninstallEntry 'LocalMachine' 'Registry64')) 'the per-user install created an HKLM uninstall key'
  Assert-True (-not (Test-Path -LiteralPath $installDir)) "the per-user install created $installDir"
  Assert-True (Test-Path -LiteralPath $userStartLink -PathType Leaf) "per-user Start Menu shortcut missing: $userStartLink"
  Assert-True (Test-Path -LiteralPath $userDesktopLink -PathType Leaf) "per-user desktop shortcut missing: $userDesktopLink"
  New-Item -ItemType Directory -Force -Path $markerDir | Out-Null
  Set-Content -LiteralPath $marker -Value "smoke marker $Version"

  Invoke-Elevated $setup ($SilentArgs + @('/ALLUSERS', '/TASKS=desktopicon', "/LOG=`"$(Join-Path $script:logDir 'migrate.log')`"")) 'the all-users install over the per-user install'
  Wait-Until { $null -eq (Get-UninstallEntry 'CurrentUser' 'Registry64') } 60 'the per-user (HKCU) uninstall key to disappear'
  Wait-Until { -not (Test-Path -LiteralPath $legacyDir) } 120 "the per-user install folder $legacyDir to disappear"
  Wait-Until { -not (Test-Path -LiteralPath $userStartLink) } 60 "the per-user Start Menu shortcut $userStartLink to disappear"
  Wait-Until { -not (Test-Path -LiteralPath $userDesktopLink) } 60 "the per-user desktop shortcut $userDesktopLink to disappear"
  $migrated = @(Get-AutonogrammerEntries)
  Assert-True ($migrated.Count -eq 1) "expected exactly one Autonogrammer uninstall entry after the migration, found $($migrated.Count): $($migrated -join ', ')"
  $migratedEntry = Assert-MachineInstall $Version $installDir
  Assert-Shortcut $startLink $exe $installDir
  Assert-True (Test-Path -LiteralPath $marker -PathType Leaf) "the migration removed user data: $marker is gone"

  Write-Step '8b. Uninstall the migrated install'
  Invoke-RegisteredUninstall $migratedEntry.UninstallString (Join-Path $script:logDir 'uninstall-migrated.log') 'the registered uninstaller after the migration'
  Wait-Until { -not (Test-Path -LiteralPath $installDir) } 120 "the install folder $installDir to disappear"
  Wait-Until { $null -eq (Get-UninstallEntry 'LocalMachine' 'Registry64') } 60 'the HKLM uninstall key to disappear'
  Assert-True (@(Get-AutonogrammerEntries).Count -eq 0) 'an Autonogrammer uninstall entry remains after the final uninstall'
  Assert-True (Test-Path -LiteralPath $marker -PathType Leaf) "the final uninstall removed user data: $marker is gone"
  Remove-Item -LiteralPath $marker -Force
  if ((Test-Path -LiteralPath $markerDir) -and -not (Get-ChildItem -LiteralPath $markerDir -Force)) { Remove-Item -LiteralPath $markerDir -Force }

  Write-Step "Installer smoke test passed for version $Version"
}

try {
  Test-Installer
  exit 0
} catch {
  $failure = $_
  $message = [string]$failure.Exception.Message
  # Diagnostics first, then cleanup, so a cleanup error cannot hide them.
  try { Write-LogTails $(if ($script:logDir) { $script:logDir } else { $LogDirectory }) } catch { Write-Host "could not print the Inno logs: $_" }
  try { if ($script:installDir) { Stop-InstalledProcesses $script:installDir } } catch { Write-Host "cleanup failed: $_" }
  Write-Host "::error::installer smoke test failed: $($message -replace '[\r\n]+', ' ')"
  Write-Host 'installer smoke test failed, full message:'
  Write-Host ($failure | Out-String)
  exit 1
}
