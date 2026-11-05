; Установщик MTD VPN (Inno Setup 6). Сборка: iscc installer\MtdVpn.iss  [/DMyAppVersion=1.2.0]
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppName "MTD VPN"
#define MyAppExe  "MtdVpn.exe"
#define Publish   "..\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{8B3C2E3A-5D54-4B9E-9C0B-6F3A1D7A2C11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=MTD
DefaultDirName={autopf}\MtdVpn
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExe}
OutputDir=Output
OutputBaseFilename=MtdVpn-Setup
Compression=lzma2/max
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
WizardStyle=modern

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Ярлык на рабочем столе"
Name: "autostart"; Description: "Запускать вместе с Windows"

[Files]
Source: "{#Publish}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
; автозапуск через планировщик: программа требует права администратора, так окно UAC при входе не появляется
Filename: "schtasks.exe"; Parameters: "/Create /TN MtdVpn /TR ""\""{app}\{#MyAppExe}\"""" /SC ONLOGON /RL HIGHEST /F"; Flags: runhidden; Tasks: autostart
Filename: "{app}\{#MyAppExe}"; Description: "Запустить {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/F /IM MtdVpn.exe /IM sing-box.exe"; Flags: runhidden; RunOnceId: "KillApp"
Filename: "schtasks.exe"; Parameters: "/Delete /TN MtdVpn /F"; Flags: runhidden; RunOnceId: "DelTask"

[Code]
// перед установкой/обновлением закрываем программу и ядро, иначе файлы заняты
function PrepareToInstall(var NeedsRestart: Boolean): String;
var R: Integer;
begin
  Exec('taskkill.exe', '/F /IM MtdVpn.exe /IM sing-box.exe', '', SW_HIDE, ewWaitUntilTerminated, R);
  Result := '';
end;
