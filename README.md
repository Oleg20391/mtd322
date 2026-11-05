# MTD VPN (WPF, .NET 8)

## Сборка
1. Установите .NET 8 SDK.
2. Скачайте sing-box для Windows (windows-amd64) с https://github.com/SagerNet/sing-box/releases
   — берите ветку 1.12.x (конфиг написан под неё). Положите `sing-box.exe` в папку `core\`.
   Если при подключении ядро ругается на wintun — положите туда же `wintun.dll`.
3. Сборка одним файлом:
   dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
4. В `bin\Release\net8.0-windows\win-x64\publish\` будут `MtdVpn.exe` и `sing-box.exe`. Их нужно раздавать вместе.

## Требования со стороны Remnawave
Подписка по User-Agent `sing-box` должна отдавать sing-box JSON (шаблон sing-box в настройках подписки Remnawave).
Проверка: откройте ссылку подписки через curl -A "sing-box/1.12.0" <ссылка> — должен прийти JSON с "outbounds".

## Как работает
- Приложение берёт из подписки только серверы (vless, hysteria2 и т.д.) и строит свой конфиг.
- Автовыбор — urltest: проверка каждые 30 секунд, при падении сервера трафик уходит на живой.
- Режим TUN (весь трафик), поэтому приложению нужны права администратора (манифест просит их сам).
- .ru / .рф и адреса bot.nu идут напрямую, как в вашем шаблоне.
