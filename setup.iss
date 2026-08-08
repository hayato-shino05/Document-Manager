; Document Manager v4.0.0 用 Inno Setup スクリプト

#define MyAppName "Document Manager"
#define MyAppVersion "4.0.0"
#define MyAppPublisher "hayato-shino05"
#define MyAppURL "https://github.com/hayato-shino05/study-document-manager"
#define MyAppExeName "study-document-manager.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE
OutputDir=installer
OutputBaseFilename=DocumentManager_v{#MyAppVersion}_Setup
SetupIconFile=study-document-manager\assets\logo\logo.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible x86compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "vietnamese"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "installnet48"; Description: "Cài đặt .NET Framework 4.8 (bắt buộc để chạy ứng dụng)"; GroupDescription: "Yêu cầu hệ thống:"; Check: not IsNet48Installed
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; .NET Framework 4.8 Web Installer (~1.5 MB) - đặt file ndp48-web.exe vào thư mục redist/
Source: "redist\ndp48-web.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Tasks: installnet48; Check: not IsNet48Installed
; Application files
Source: "study-document-manager\bin\Release\study-document-manager.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "study-document-manager\bin\Release\study-document-manager.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "study-document-manager\bin\Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "study-document-manager\bin\Release\x64\*"; DestDir: "{app}\x64"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "study-document-manager\bin\Release\x86\*"; DestDir: "{app}\x86"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "study-document-manager\bin\Release\assets\*"; DestDir: "{app}\assets"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
; Cài .NET Framework 4.8 trước (shellexec sẽ tự yêu cầu quyền Admin/UAC)
Filename: "{tmp}\ndp48-web.exe"; Parameters: "/passive /norestart"; StatusMsg: "Đang cài đặt .NET Framework 4.8... Vui lòng chờ."; Flags: shellexec waituntilterminated; Tasks: installnet48; Check: not IsNet48Installed
; Khởi chạy ứng dụng sau cài đặt
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsNet48Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  // Hiển thị thông báo nếu .NET 4.8 chưa cài và user ở trang chọn Tasks
  if (CurPageID = wpSelectTasks) and (not IsNet48Installed) then
  begin
    MsgBox('.NET Framework 4.8 chưa được cài đặt trên máy.' + #13#10 + #13#10 +
           'Hãy đánh dấu tùy chọn "Cài đặt .NET Framework 4.8" bên dưới.' + #13#10 +
           'Ứng dụng yêu cầu .NET 4.8 để hoạt động.', mbInformation, MB_OK);
  end;
end;
