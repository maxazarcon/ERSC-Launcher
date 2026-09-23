; Per-user installer for the Seamless Co-Op Launcher. Build with:
;   iscc /DAppVersion=1.6.0 /DSourceExe=..\dist\ERSCLauncher.exe /O..\dist installer\ERSCLauncher.iss

#ifndef AppVersion
  #error Pass /DAppVersion=X.Y.Z
#endif
#ifndef SourceExe
  #define SourceExe "..\dist\ERSCLauncher.exe"
#endif

[Setup]
; Keep in sync with App.UninstallKey in src/ERSC.Launcher/App.cs.
AppId={{6F1C2D84-3B7A-4E59-9C0D-8A2E5B7F4C13}
AppName=Seamless Co-Op Launcher
AppVersion={#AppVersion}
AppVerName=Seamless Co-Op Launcher {#AppVersion}
AppPublisher=maxazarcon
AppPublisherURL=https://github.com/maxazarcon/ERSC-Launcher
AppSupportURL=https://github.com/maxazarcon/ERSC-Launcher/issues
; A per-user folder the launcher can write to, so its built-in updater keeps working without admin rights.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\ERSC Launcher
DefaultGroupName=Seamless Co-Op Launcher
DisableProgramGroupPage=yes
UninstallDisplayName=Seamless Co-Op Launcher
UninstallDisplayIcon={app}\ERSCLauncher.exe
SetupIconFile=..\src\ERSC.Launcher\app.ico
OutputBaseFilename=ERSCLauncher-{#AppVersion}-setup-win-x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Tasks]
Name: "steam"; Description: "Add to Steam (for Big Picture mode and controllers)"; GroupDescription: "Shortcuts:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "ERSCLauncher.exe"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\Seamless Co-Op Launcher"; Filename: "{app}\ERSCLauncher.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\Seamless Co-Op Launcher"; Filename: "{app}\ERSCLauncher.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\ERSCLauncher.exe"; Parameters: "--add-to-steam"; StatusMsg: "Adding the launcher to Steam..."; Tasks: steam; Flags: waituntilterminated
Filename: "{app}\ERSCLauncher.exe"; Description: "Open Seamless Co-Op Launcher"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\ERSCLauncher.exe"; Parameters: "--remove-from-steam"; RunOnceId: "RemoveSteamShortcut"; Flags: waituntilterminated

[UninstallDelete]
; Leftovers from an interrupted in-place update.
Type: files; Name: "{app}\ERSCLauncher.exe.update-*"
Type: files; Name: "{app}\ERSCLauncher.exe.writecheck-*"
