; Inno Setup script for keepawake — packages the self-contained Native AOT publish output
; (see ../keepawake/publish/, produced by: dotnet publish -c Release -r win-x64 -o publish;
; SelfContained/PublishAot are persisted directly in keepawake.csproj's Release-only PropertyGroup, so
; no ad-hoc publish flags are needed here) into a per-user installer.
;
; Unlike dnsw, there's no Windows Service to stop/reinstall and no admin requirement at all —
; keepawake's one Win32 call (SetThreadExecutionState) needs no elevation, ever (see CLAUDE.md), so
; this installer runs PrivilegesRequired=lowest and has no [Code] section beyond a taskkill so an
; upgrade isn't blocked by the running exe holding its own file open.

#define AppName "keepawake"
; Overridable from the command line (/DAppVersion=1.2.3) so the manually-triggered CI release
; workflow (.github/workflows/release.yml) can stamp the installer with whatever version was typed
; into (or auto-bumped for) that run — local manual builds with no override still default sensibly.
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "jehan593"
#define AppExeName "keepawake.exe"
#define AppId "{{62F82490-805E-4E55-87D3-00DDE2C03367}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=keepawakeSetup
SetupIconFile=..\keepawake\Assets\app-on.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "..\keepawake\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName} now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Registry]
; Cleans up the "Start with Windows" Run-key value StartupRegistration.SetEnabled() writes
; (Native/StartupRegistration.cs).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "keepawake"; Flags: deletevalue uninsdeletevalue

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    // Upgrading over an existing install: a running keepawake.exe holds its own file open, which
    // blocks the [Files] copy below with "DeleteFile failed; code 5. Access is denied." if not
    // closed first. No admin needed for this — same user, same privilege level as Setup itself.
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#AppExeName} /F', '', SW_HIDE,
      ewWaitUntilTerminated, ResultCode);
  end;
end;
