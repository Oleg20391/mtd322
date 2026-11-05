@echo off
chcp 65001 >nul



net session >nul 2>&1 || (
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

set "URL=https://github.com/Oleg20391/mtd322.git/releases/latest/download/MtdVpn-Setup.exe"
set "F=%TEMP%\MtdVpn-Setup.exe"

echo Скачиваю MTD VPN, подождите...
powershell -NoProfile -Command "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; $ProgressPreference='SilentlyContinue'; Invoke-WebRequest '%URL%' -OutFile '%F%' -UseBasicParsing"
if not exist "%F%" (
  echo Не удалось скачать. Проверьте интернет и попробуйте ещё раз.
  pause
  exit /b 1
)

echo Устанавливаю...
"%F%" /SILENT /SUPPRESSMSGBOXES /NORESTART /TASKS="desktopicon,autostart"
del "%F%" >nul 2>&1
echo Готово.
timeout /t 3 >nul
