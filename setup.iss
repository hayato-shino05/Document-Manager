; Document Manager v4.0.0 用 Inno Setup スクリプト

#ifndef MyAppVersion
#define MyAppVersion "4.0.0"
#endif

#ifndef PublishDir
#define PublishDir "artifacts\publish\win-x64"
#endif

#ifndef OutputDir
#define OutputDir "artifacts\installer"
#endif

#define MyAppName "Document Manager"
#define MyAppPublisher "hayato-shino05"
#define MyAppURL "https://github.com/hayato-shino05/study-document-manager"
#define MyAppExeName "DocumentManager.exe"
#define MyAppSetupName "DocumentManager_v" + MyAppVersion + "_Setup"
#define DotnetDesktopRuntimeInstaller "redist\windowsdesktop-runtime-9.0.18-win-x64.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={localappdata}\Programs\DocumentManager
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE
OutputDir={#OutputDir}
OutputBaseFilename={#MyAppSetupName}
SetupIconFile=StudyDocumentManager\Assets\Brand\document-manager.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
LanguageDetectionMethod=uilanguage
ShowLanguageDialog=auto
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "vietnamese"; MessagesFile: "installer-languages\Vietnamese.isl"
Name: "chinese"; MessagesFile: "installer-languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
Source: "{#DotnetDesktopRuntimeInstaller}"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\windowsdesktop-runtime-9.0.18-win-x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing .NET 9 Desktop Runtime..."; Flags: waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
