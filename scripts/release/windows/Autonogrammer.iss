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
// The folders the app writes, as the app computes them:
//   {userappdata}\Autonogrammer   settings (HeadServices.ResolveConfigDirectory: UserDataDirectory.PathFor(ApplicationData))
//   {localappdata}\Autonogrammer  the DPAPI secrets, and the managed Omp runtime in its omp folder (HeadServices.ResolveManagedOmpRoot)
//   the state root                sessions and loop state: XDG_DATA_HOME when it is an absolute path, else %USERPROFILE%\.local\share,
//                                 then Autonogrammer (AgentLoopEnvironment.ResolveStateRoot)
function StateFolder(): String;
var
  DataHome: String;
begin
  DataHome := GetEnv('XDG_DATA_HOME');
  if (Length(DataHome) < 2) or not ((DataHome[2] = ':') or (DataHome[1] = '\')) then
    DataHome := GetEnv('USERPROFILE') + '\.local\share';
  Result := DataHome + '\Autonogrammer';
end;

procedure DeleteDataFolder(const Folder: String);
begin
  if DirExists(Folder) then
    DelTree(Folder, True, True, True);
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
