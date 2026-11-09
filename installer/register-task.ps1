# Автозапуск MTD VPN при входе любого пользователя, с повышенными правами (нужны для TUN)
param([Parameter(Mandatory = $true)][string]$Exe)

$action    = New-ScheduledTaskAction -Execute $Exe
$trigger   = New-ScheduledTaskTrigger -AtLogOn
$principal = New-ScheduledTaskPrincipal -GroupId 'BUILTIN\Users' -RunLevel Highest
$settings  = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)

Register-ScheduledTask -TaskName 'MtdVpn' -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
