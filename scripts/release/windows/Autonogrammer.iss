; Inno Setup 6 script for the Autonogrammer Windows x64 installer.
;
; Build (from the repository root, as .github/workflows/desktop-release.yml does):
;   ISCC.exe /DAppVersion=<version> /DSourceDir=<publish-dir> /DOutputDir=<out-dir> /DOutputBaseFilename=Autonogrammer-<version>-win-x64-Setup scripts\release\windows\Autonogrammer.iss
;
; The installer is per-user: it installs under %LOCALAPPDATA%\Programs\Autonogrammer and needs no administrator rights.
; It is not signed with an Authenticode certificate.
; The uninstaller asks whether to remove the user data (settings and sessions). The default answer is No.

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

[Setup]
AppId={{3C4EC86C-E483-4B14-99BC-3C096BF45A45}
AppName=Autonogrammer
AppVersion={#AppVersion}
AppPublisher=SWC Studio
DefaultDirName={localappdata}\Programs\Autonogrammer
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile=..\..\..\apps\Autonogrammer.Native\Assets\Brand\autonogrammer.ico
UninstallDisplayIcon={app}\Autonogrammer.Native.exe
Compression=lzma2

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\Autonogrammer\Autonogrammer"; Filename: "{app}\Autonogrammer.Native.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\Autonogrammer"; Filename: "{app}\Autonogrammer.Native.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Code]
const
  // Win32 message box flags. Inno Setup's Pascal does not define every MB_ name, so the value is written out:
  // MB_YESNO (4) or MB_ICONQUESTION (32) or MB_DEFBUTTON2 (256) = 292. IDYES and IDNO are built in.
  MbYesNoQuestionDefaultNo = 292;

// The per-user data folder that the app keeps its settings (config\settings.json) and sessions (sessions\) in. It is the state root
// that AgentLoopEnvironment.ResolveStateRoot computes when AUTONOGRAMMER_STATE_DIR is not set:
// UserDataDirectory.PathFor(ResolveDataHome), where ResolveDataHome is XDG_DATA_HOME when that is an absolute path, else
// %USERPROFILE%\.local\share, and PathFor appends "Autonogrammer".
function UserDataFolder(): String;
var
  DataHome: String;
begin
  DataHome := GetEnv('XDG_DATA_HOME');
  if (Length(DataHome) < 2) or not ((DataHome[2] = ':') or (DataHome[1] = '\')) then
    DataHome := GetEnv('USERPROFILE') + '\.local\share';
  Result := DataHome + '\Autonogrammer';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataFolder: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if SuppressibleMsgBox('Also remove your Autonogrammer data (settings and sessions)?', 'Autonogrammer',
       MbYesNoQuestionDefaultNo, IDNO) = IDYES then
    begin
      DataFolder := UserDataFolder();
      if DirExists(DataFolder) then
        DelTree(DataFolder, True, True, True);
    end;
  end;
end;
