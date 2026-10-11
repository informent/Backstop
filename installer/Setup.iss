#ifndef AppVersion
  #error AppVersion must be supplied
#endif
#ifndef PayloadDir
  #error PayloadDir must be supplied
#endif
#ifndef OutputDir
  #error OutputDir must be supplied
#endif
[Setup]
AppId={{052E264C-4955-5A84-A3BB-D06C9D7006E4}
AppName=Backstop
AppVersion={#AppVersion}
AppPublisher=informent
AppPublisherURL=https://github.com/informent/Backstop
DefaultDirName={localappdata}\Programs\Backstop
DefaultGroupName=Backstop
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=Backstop-v{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Backstop.exe
CloseApplications=yes
CloseApplicationsFilter=Backstop.exe
RestartApplications=no
SetupLogging=yes
[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\Backstop"; Filename: "{app}\Backstop.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Backstop"; Filename: "{app}\Backstop.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Run]
Filename: "{app}\Backstop.exe"; Description: "Open Backstop"; Flags: nowait postinstall skipifsilent unchecked
