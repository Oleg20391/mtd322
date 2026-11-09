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
Source: "register-task.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
; автозапуск через планировщик для группы Users: окно UAC при входе не появляется, а установка от SYSTEM (MeshCentral) работает
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\register-task.ps1"" -Exe ""{app}\{#MyAppExe}"""; Flags: runhidden; Tasks: autostart
Filename: "{app}\{#MyAppExe}"; Description: "Запустить {#MyAppName}"; Flags: nowait postinstall skipifsilent
; после автообновления из самой программы запускаем её снова
Filename: "{app}\{#MyAppExe}"; Flags: nowait; Check: IsUpdateRun

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/F /IM MtdVpn.exe /IM sing-box.exe"; Flags: runhidden; RunOnceId: "KillApp"
Filename: "schtasks.exe"; Parameters: "/Delete /TN MtdVpn /F"; Flags: runhidden; RunOnceId: "DelTask"

[Code]
function IsUpdateRun: Boolean;
begin
  Result := ExpandConstant('{param:UPDATED|0}') = '1';
end;

// перед установкой/обновлением закрываем программу и ядро, иначе файлы заняты
function PrepareToInstall(var NeedsRestart: Boolean): String;
var R: Integer;
begin
  Exec('taskkill.exe', '/F /IM MtdVpn.exe /IM sing-box.exe', '', SW_HIDE, ewWaitUntilTerminated, R);
  Result := '';
end;
