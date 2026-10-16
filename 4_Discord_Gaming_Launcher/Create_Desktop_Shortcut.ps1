$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$wsh = New-Object -ComObject WScript.Shell
$desktop = [System.Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop 'Discord Gaming Hub.lnk'
$shortcut = $wsh.CreateShortcut($shortcutPath)

$exePath = Join-Path $Root 'Discord_Gaming_Launcher.exe'
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $Root

$iconPath = Join-Path $Root 'discord_purple_black.ico'
$shortcut.IconLocation = "$iconPath,0"
$shortcut.Description = 'Discord Safe Bypass - Gaming Hub (Duo-Launch & Notifications)'
$shortcut.Save()

Write-Host '[✓] تم إنشاء اختصار مشغل الألعاب الفاخر على سطح المكتب بنجاح!' -ForegroundColor Green
