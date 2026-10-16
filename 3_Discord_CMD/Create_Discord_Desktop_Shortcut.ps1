$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$wsh = New-Object -ComObject WScript.Shell
$desktop = [System.Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop 'Discord (Safe with Games).lnk'
$shortcut = $wsh.CreateShortcut($shortcutPath)

$exePath = Join-Path $Root 'Discord_Safe.exe'
if (Test-Path $exePath) {
    $shortcut.TargetPath = $exePath
} else {
    $shortcut.TargetPath = Join-Path $Root 'turkey_dnsredir.cmd'
}
$shortcut.WorkingDirectory = $Root

$iconPath = Join-Path $Root 'discord_purple_black.ico'
if (-not (Test-Path $iconPath)) {
    $iconPath = Join-Path $Root 'discord_red.ico'
}
$shortcut.IconLocation = "$iconPath,0"
$shortcut.Description = 'Discord Safe Bypass for Turkey (Black & Purple Luxury Edition)'
$shortcut.Save()

Write-Host '[✓] تم إنشاء اختصار سطح المكتب بالأيقونة الفاخرة (الأسود والبنفسجي) بنجاح!' -ForegroundColor Green
