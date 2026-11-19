; Inno Setup 6 script for the Autonogrammer Windows x64 installer. Minimum compiler: Inno Setup 6.3 (x64compatible).
;
; Build (from the repository root, as .github/workflows/desktop-release.yml does):
;   ISCC.exe /DAppVersion=<version> /DSourceDir=<publish-dir> /DOutputDir=<out-dir> /DOutputBaseFilename=Autonogrammer-<version>-win-x64-Setup scripts\release\windows\Autonogrammer.iss
;
; The installer is per machine, like a traditional Windows installer:
;   - Double-clicking it asks for administrator rights (UAC).
;   - It installs the 64-bit app under C:\Program Files\Autonogrammer, adds a Start Menu entry for all users,
;     an optional desktop shortcut, and an entry in Apps and Features.
;   - The wizard shows the licence (the repository LICENSE), the install folder, the desktop shortcut task
;     and, on the last page, a "Launch Autonogrammer" checkbox. The app is started without administrator rights.
;   - Installing the same or a newer version over an existing one keeps the install folder. Running copies
;     of the app are closed first and are not restarted. User data is never touched by an install.
;   - Install folder guard: uninstalling removes the whole install folder, so Setup refuses a folder that already holds
;     other files and is not an Autonogrammer install. NextButtonClick checks it on the folder page. A silent install with
;     /DIR=<folder with other files> is stopped by that same check (observed in CI: exit code 1, nothing installed).
;     PrepareToInstall is a backstop for installs that skip the folder page.
;   - Command line, for IT and for tests: /ALLUSERS (the default) and /CURRENTUSER (a per-user install under
;     %LOCALAPPDATA%\Programs\Autonogrammer, no administrator rights). Silent installs: /VERYSILENT
;     /SUPPRESSMSGBOXES /NORESTART /TASKS=desktopicon /LOG=<file>.
;   - When an all-users install finds the older per-user install of this app (the HKCU uninstall entry that earlier
;     versions of this installer created), it runs that uninstaller silently first. The user data stays.
;   - The uninstaller asks whether to remove the user data (settings, sessions and the managed Omp runtime).
;     The default answer is No.
; Not signed with an Authenticode certificate: Windows SmartScreen may warn when the installer is run.
; Windows 10 version 1607 (build 14393) is the oldest Windows this installer accepts, the oldest Windows 10 release
; that .NET 10 supports (https://learn.microsoft.com/dotnet/core/install/windows). Windows 8.1 and older are not supported by .NET 10.

#ifndef AppVersion
  #error AppVersion is required: pass /DAppVersion=<version>
#endif
#ifndef SourceDir
  #error SourceDir is required: pass /DSourceDir=<self-contained publish directory>
#endif
#ifndef OutputDir
  #error OutputDir is required: pass /DOutputDir=<directory for the installer>
#endif
#ifndef OutputBaseFilename
  #error OutputBaseFilename is required: pass /DOutputBaseFilename=Autonogrammer-<version>-win-x64-Setup
#endif

; VersionInfoVersion must be numeric: MAJOR.MINOR.PATCH[-prerelease] becomes MAJOR.MINOR.PATCH.0 (0.20.0-rc.1 -> 0.20.0.0).
; Evidence (ISPP help, jrsoftware.org/inno-setup/documentation):
;   - Expression Syntax (topic_expressions): ISPP expressions support the C-style conditional operator ?:, which is used below.
;   - Pos(SubStr, S) and Copy(S, Index, Count?) are 1-based; Pos returns 0 when SubStr is absent; Str(any) turns an integer
;     or a string into a string (topic_pos, topic_copy, topic_str).
;   - ISCC /D<name>=<value> "emulates #define public <name> <value>" (Extended Command-Line Compiler, topic_isppcc). The help does
;     not state the value's type. Str() accepts an integer or a string, so the code below works for either. A dotted value such as
;     0.20.0 or 0.20.0-rc.1 is not an integer, so it is a string; GitHub Actions run 44283136440 compiled the previous script with such a value.
#define AppVersionText Str(AppVersion)
#define PrereleasePos Pos("-", AppVersionText)
#define NumericVersion (PrereleasePos > 0 ? Copy(AppVersionText, 1, PrereleasePos - 1) : AppVersionText)
#define InstallerFileVersion NumericVersion + ".0"

[Setup]
AppId={{3C4EC86C-E483-4B14-99BC-3C096BF45A45}
AppName=Autonogrammer
AppVersion={#AppVersion}
AppPublisher=SWC Studio
AppPublisherURL=https://github.com/swcstudiospace/OmpOS
AppSupportURL=https://github.com/swcstudiospace/OmpOS
UninstallDisplayName=Autonogrammer
UninstallDisplayIcon={app}\Autonogrammer.Native.exe
VersionInfoVersion={#InstallerFileVersion}
VersionInfoCompany=SWC Studio
VersionInfoProductName=Autonogrammer
VersionInfoDescription=Autonogrammer Setup
VersionInfoCopyright=Copyright (C) 2026 SWC Studio
MinVersion=10.0.14393
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
DefaultDirName={autopf}\Autonogrammer
UsePreviousAppDir=yes
DisableProgramGroupPage=yes
WizardStyle=modern
LicenseFile=..\..\..\LICENSE
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
Compression=lzma2/max
SolidCompression=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile=..\..\..\apps\Autonogrammer.Native\Assets\Brand\autonogrammer.ico

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; WorkingDir is set on the shortcuts on purpose. When it is blank, Setup uses the folder of Filename, which would make the
; install folder the app's working directory (Inno Setup help, [Icons] WorkingDir). The stored string %USERPROFILE% is not
; expanded by Setup: Windows expands it each time a user starts the shortcut, in that user's own environment (the Windows
; Command Prompt shortcut uses %HOMEDRIVE%%HOMEPATH% the same way), so it is not the profile of the account that ran Setup.
[Icons]
Name: "{autoprograms}\Autonogrammer"; Filename: "{app}\Autonogrammer.Native.exe"; WorkingDir: "%USERPROFILE%"
Name: "{autodesktop}\Autonogrammer"; Filename: "{app}\Autonogrammer.Native.exe"; WorkingDir: "%USERPROFILE%"; Tasks: desktopicon

; The launch checkbox has no WorkingDir on purpose. A constant such as {%USERPROFILE} would be expanded inside the elevated
; Setup process: when a standard user approves UAC with another administrator's credentials that is the administrator's
; profile, which the original user cannot enter, and the runasoriginaluser launch would fail. The app falls back to the
; user's home folder by itself when its current directory is the install folder.
[Run]
Filename: "{app}\Autonogrammer.Native.exe"; Description: "Launch Autonogrammer"; Flags: nowait postinstall skipifsilent runasoriginaluser

; Files that a newer version no longer ships must not linger in the bundled plugin tree. [Files] installs the new tree afterwards.
[InstallDelete]
Type: filesandordirs; Name: "{app}\plugins"

; Leftovers the app or the .NET runtime created inside the install folder must not stop the folder from being removed.
; Install only into an empty folder or an existing Autonogrammer folder (see NextButtonClick), so this never removes foreign files.
[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
const
  // The uninstall entry of the per-user install that earlier versions of this installer created (same AppId).
  LegacyUninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{3C4EC86C-E483-4B14-99BC-3C096BF45A45}_is1';

// The folders the app writes, as the app computes them:
//   {userappdata}\Autonogrammer   settings (HeadServices.ResolveConfigDirectory: UserDataDirectory.PathFor(ApplicationData))
//   {localappdata}\Autonogrammer  the DPAPI secrets, and the managed Omp runtime in its omp folder (HeadServices.ResolveManagedOmpRoot)
//   the state root                sessions and loop state: XDG_DATA_HOME when it is an absolute path, else %USERPROFILE%\.local\share,
//                                 then Autonogrammer (AgentLoopEnvironment.ResolveStateRoot)
// None of them is under the install folder. An install or an upgrade never touches them.
function StateFolder(): String;
var
  DataHome: String;
  IsAbsolute: Boolean;
begin
  DataHome := GetEnv('XDG_DATA_HOME');
  // No string index is evaluated unless the string is long enough: Pascal Script does not promise short-circuit evaluation.
  IsAbsolute := False;
  if Length(DataHome) >= 2 then
    IsAbsolute := (DataHome[2] = ':') or (DataHome[1] = '\');
  if not IsAbsolute then
    DataHome := GetEnv('USERPROFILE') + '\.local\share';
  Result := DataHome + '\Autonogrammer';
end;

procedure DeleteDataFolder(const Folder: String);
begin
  if DirExists(Folder) then
    DelTree(Folder, True, True, True);
end;

// True when Dir holds files or folders and is not an Autonogrammer install. Uninstall removes the whole install folder,
// so such a folder must not be chosen.
function IsForeignFolder(const Dir: String): Boolean;
var
  Folder: String;
  FindRec: TFindRec;
begin
  Result := False;
  Folder := AddBackslash(Dir);
  if not DirExists(Folder) then
    Exit;
  if FileExists(Folder + 'Autonogrammer.Native.exe') or FileExists(Folder + 'unins000.exe') then
    Exit;
  if FindFirst(Folder + '*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
          Result := True;
      until Result or not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpSelectDir then
  begin
    if IsForeignFolder(WizardDirValue) then
    begin
      SuppressibleMsgBox('The folder ' + WizardDirValue + ' already contains other files. Choose an empty folder or a new one, ' +
        'because uninstalling Autonogrammer removes its whole install folder.', mbError, MB_OK, IDOK);
      Result := False;
    end;
  end;
end;

// Backstop for installs that skip the folder page (for example an upgrade, which reuses the previous folder).
// A silent install does not need it: Setup still runs NextButtonClick for the hidden folder page, which refuses the folder
// (observed in GitHub Actions run 44508808964: Inno Setup 6.7.1, exit code 1, nothing installed).
// Per the Inno Setup help (Event Functions), a non-empty result stops Setup at the Preparing to Install page. The installer
// smoke test does not exercise this function.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Folder: String;
begin
  Result := '';
  Folder := ExpandConstant('{app}');
  if IsForeignFolder(Folder) then
    Result := 'The folder ' + Folder + ' already contains other files. Choose an empty folder or a new one, ' +
      'because uninstalling Autonogrammer removes its whole install folder.';
end;

// Removes the per-user install that earlier versions of this installer made, so that one copy remains.
// It only runs for an all-users install. A failure is logged and the install goes on. The user data stays,
// because the uninstaller's default answer to the data question is No.
// Windows keeps a separate HKCU per account: when an administrator account other than the signed-in user
// approves the UAC prompt, the signed-in user's per-user install is not visible here and stays in place.
procedure RemoveLegacyPerUserInstall();
var
  UninstallString, Command, Parameters: String;
  ClosePos, ResultCode, Waited: Integer;
  StartsWithQuote, UninstallerExists: Boolean;
begin
  if not IsAdminInstallMode() then
    Exit;
  if not RegQueryStringValue(HKEY_CURRENT_USER, LegacyUninstallKey, 'UninstallString', UninstallString) then
    Exit;
  Log('Found the per-user install of Autonogrammer. UninstallString: ' + UninstallString);
  // Inno writes the quoted path of unins000.exe, possibly followed by parameters. Split the path from the parameters.
  Command := Trim(UninstallString);
  Parameters := '';
  // No string index is evaluated unless the string is long enough: Pascal Script does not promise short-circuit evaluation.
  StartsWithQuote := False;
  if Length(Command) > 0 then
    StartsWithQuote := (Command[1] = '"');
  if StartsWithQuote then
  begin
    ClosePos := Pos('"', Copy(Command, 2, Length(Command)));
    if ClosePos > 0 then
    begin
      Parameters := Trim(Copy(Command, ClosePos + 2, Length(Command)));
      Command := Copy(Command, 2, ClosePos - 1);
    end
    else
      Command := RemoveQuotes(Command);
  end;
  UninstallerExists := False;
  if Command <> '' then
    UninstallerExists := FileExists(Command);
  if not UninstallerExists then
  begin
    Log('WARNING: the per-user uninstaller was not found (' + Command + '). It was not removed. Continuing the install.');
    Exit;
  end;
  if Parameters <> '' then
    Parameters := Parameters + ' ';
  Parameters := Parameters + '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART';
  if Exec(Command, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Log('The per-user uninstaller returned exit code ' + IntToStr(ResultCode));
    // The uninstaller continues in a temporary copy of itself, so wait for its registry entry to disappear.
    Waited := 0;
    while RegKeyExists(HKEY_CURRENT_USER, LegacyUninstallKey) and (Waited < 60000) do
    begin
      Sleep(500);
      Waited := Waited + 500;
    end;
    if RegKeyExists(HKEY_CURRENT_USER, LegacyUninstallKey) then
      Log('WARNING: the per-user uninstall entry is still present after 60 seconds. Continuing the install.')
    else
      Log('The per-user install was removed.');
  end
  else
    Log('WARNING: the per-user uninstaller could not be started: ' + SysErrorMessage(ResultCode) + '. Continuing the install.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    RemoveLegacyPerUserInstall();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // The default answer is No: the data stays unless the user chooses Yes.
    if SuppressibleMsgBox('Also remove your Autonogrammer settings, sessions and managed Omp runtime?', mbConfirmation,
       MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES then
    begin
      DeleteDataFolder(ExpandConstant('{userappdata}\Autonogrammer'));
      DeleteDataFolder(ExpandConstant('{localappdata}\Autonogrammer'));
      DeleteDataFolder(StateFolder());
    end;
  end;
end;
