; Build with publish/build-inno.ps1 so the version and absolute paths are supplied.
#ifndef MyAppVersion
  #error MyAppVersion is required
#endif
#ifndef PublishDir
  #error PublishDir is required
#endif
#ifndef OutDir
  #error OutDir is required
#endif
#ifndef IconPath
  #error IconPath is required
#endif

#define MyAppName "LawMatic Calendar"
#define MyAppPublisher "LawLabs"
#define MyAppExeName "LawMatic.Calendar.exe"

[Setup]
AppId={{3D86A995-8BC6-43DA-9E52-1481022B0A2B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\LawMaticCalendar
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#OutDir}
OutputBaseFilename=LawMaticCalendar-Setup-{#MyAppVersion}-x64
SetupIconFile={#IconPath}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
