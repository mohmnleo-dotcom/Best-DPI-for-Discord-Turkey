$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$wsh = New-Object -ComObject WScript.Shell
$desktop = [System.Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop 'Discord (Safe GUI Edition).lnk'
$shortcut = $wsh.CreateShortcut($shortcutPath)

$exePath = Join-Path $Root 'Discord_Safe_GUI.exe'
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $Root

$iconPath = Join-Path $Root 'discord_purple_black.ico'
$shortcut.IconLocation = "$iconPath,0"
$shortcut.Description = 'Discord Safe Bypass (Luxury GUI Edition)'
$shortcut.Save()

Write-Host '[✓] تم إنشاء اختصار الواجهة الرسومية الفاخرة على سطح المكتب بنجاح!' -ForegroundColor Green
