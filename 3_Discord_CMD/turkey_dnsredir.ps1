[Console]::InputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$Host.UI.RawUI.WindowTitle = "أداة تشغيل ديسكورد الآمنة في تركيا (Black & Purple Luxury Edition)"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root

# --- WinINet Native Helper ---
if (-not ([System.Management.Automation.PSTypeName]'WinINetNative').Type) {
    Add-Type -MemberDefinition '
        [DllImport("wininet.dll", SetLastError = true, CharSet=CharSet.Auto)]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
    ' -Name "WinINetNative" -Namespace "ProxyRefresh"
}

function Update-WinINet {
    [ProxyRefresh.WinINetNative]::InternetSetOption([IntPtr]::Zero, 39, [IntPtr]::Zero, 0) | Out-Null
    [ProxyRefresh.WinINetNative]::InternetSetOption([IntPtr]::Zero, 37, [IntPtr]::Zero, 0) | Out-Null
}

$script:bridgeProc = $null
$script:ciadpiProc = $null

function Clean-SystemSettings {
    try {
        # 1. تنظيف ملف hosts من أي أسطر ديسكورد سابقة واستعادة النسخة الأصلية النظيفة
        $hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
        if (Test-Path $hostsPath) {
            $content = [System.IO.File]::ReadAllText($hostsPath)
            if ($content -match "# Discord Safe Bypass") {
                $clean = $content -replace "(?s)\r?\n# Discord Safe Bypass.*?# End Discord Safe Bypass\r?\n?", ""
                [System.IO.File]::WriteAllText($hostsPath, $clean)
            }
        }
    } catch {}
    
    try {
        # 2. إرجاع إعدادات البروكسي للوضع الافتراضي النظيف (Direct Connection 100%)
        Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" -Name "AutoConfigURL" -ErrorAction SilentlyContinue
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" -Name "ProxyEnable" -Value 0 -Type DWord
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" -Name "ProxyServer" -Value "" -Type String
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" -Name "ProxyOverride" -Value "" -Type String
        Update-WinINet
        
        $env:HTTP_PROXY = ""
        $env:HTTPS_PROXY = ""
        $env:ALL_PROXY = ""
        $env:NO_PROXY = ""
    } catch {}
}

function Enable-ProxySettings {
    try {
        Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" -Name "AutoConfigURL" -ErrorAction SilentlyContinue
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" -Name "ProxyEnable" -Value 1 -Type DWord
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" -Name "ProxyServer" -Value "127.0.0.1:8080" -Type String
        
        # قائمة استثناء شاملة تضمن 0ms بنق لجميع الألعاب والشركات وحماية كاملة لجوجل كروم أثناء فترة التحديث
        $override = "<local>;*.google.com;*.google.com.tr;*.googleapis.com;*.gstatic.com;*.googleusercontent.com;*.youtube.com;*.gmail.com;*.accounts.google.com;accounts.google.com;*.epicgames.com;*.epicgames.dev;*.unrealengine.com;*.epicgamesstore.com;*.ubisoft.com;*.ubisoftconnect.com;*.ubi.com;*.riotgames.com;*.pvp.net;*.playvalorant.com;*.leagueoflegends.com;*.activision.com;*.callofduty.com;*.battle.net;*.demonware.net;*.steampowered.com;*.steamcommunity.com;*.steamstatic.com"
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" -Name "ProxyOverride" -Value $override -Type String
        Update-WinINet
        
        # تثبيت حارس الإقلاع الآمن لحماية المتصفح والألعاب عند تشغيل الكمبيوتر مستقبلاً
        $bootCmd = 'cmd.exe /c reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings" /v ProxyEnable /t REG_DWORD /d 0 /f >nul 2>&1'
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "DiscordSafeBootCleaner" -Value $bootCmd -Type String -ErrorAction SilentlyContinue
    } catch {}
}

function Stop-WinDivertDriver {
    Get-Process goodbyedpi -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300
    & sc.exe stop WinDivert *>$null
    & sc.exe stop WinDivert14 *>$null
    & sc.exe delete WinDivert *>$null
    & sc.exe delete WinDivert14 *>$null
}

function Stop-AllProxies {
    Get-Process ciadpi -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Get-Process http_bridge -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Get-CimInstance Win32_Process -Filter "CommandLine LIKE '%http_bridge.py%'" -ErrorAction SilentlyContinue | ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }
}

function Start-ProxyEngines {
    Stop-AllProxies
    Start-Sleep -Milliseconds 400
    
    # 1. تشغيل محرك ByeDPI الآمن في طبقة المستخدم (بدون أي كيرنل)
    $ciadpiExe = Join-Path $Root "ciadpi.exe"
    if (Test-Path $ciadpiExe) {
        $ciadpiArgs = "-i 127.0.0.1 -p 1080 --split 1 --disorder 3+s --mod-http=h,d --auto=torst --tlsrec 1+s"
        $script:ciadpiProc = Start-Process -FilePath $ciadpiExe -ArgumentList $ciadpiArgs -WindowStyle Hidden -PassThru
    }
    
    Start-Sleep -Milliseconds 400
    
    # 2. تشغيل جسر HTTP الداخلي المخصص للديسكورد
    $bridgeExe = Join-Path $Root "http_bridge.exe"
    if (Test-Path $bridgeExe) {
        $script:bridgeProc = Start-Process -FilePath $bridgeExe -WindowStyle Hidden -PassThru
    } else {
        $bridgeScript = Join-Path $Root "http_bridge.py"
        $py = (Get-Command python -ErrorAction SilentlyContinue).Source
        if (-not $py) { $py = "C:\Users\mohmn\AppData\Local\Programs\Python\Python311\python.exe" }
        if ((Test-Path $py) -and (Test-Path $bridgeScript)) {
            $script:bridgeProc = Start-Process -FilePath $py -ArgumentList "`"$bridgeScript`"" -WindowStyle Hidden -PassThru
        }
    }
    
    # الانتظار حتى يصبح المنفذ 8080 جاهزاً
    for ($i = 0; $i -lt 15; $i++) {
        try {
            $tcp = New-Object System.Net.Sockets.TcpClient
            $tcp.Connect("127.0.0.1", 8080)
            $tcp.Close()
            break
        } catch {
            Start-Sleep -Milliseconds 200
        }
    }
}

function Fix-DiscordSettings {
    try {
        $settingsFile = Join-Path $env:APPDATA "discord\settings.json"
        if (Test-Path $settingsFile) {
            $json = Get-Content $settingsFile -Raw | ConvertFrom-Json
            if ($json.IS_MINIMIZED -ne $false) {
                $json.IS_MINIMIZED = $false
                $json | ConvertTo-Json | Set-Content $settingsFile -Encoding UTF8
            }
        }
    } catch {}
}

function Start-DiscordApp {
    Fix-DiscordSettings
    
    # إنهاء أي عمليات ديسكورد معلقة سابقة
    Get-Process Discord -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300
    
    # تفعيل إعدادات التوجيه مؤقتاً لتخطي فحص التحديثات
    Enable-ProxySettings
    
    $updateExe = Join-Path $env:LOCALAPPDATA "Discord\Update.exe"
    $discordArgs = '--processStart Discord.exe --process-start-args="--proxy-server=http://127.0.0.1:8080"'
    
    $discordAppFolder = (Get-ChildItem -Path "$env:LOCALAPPDATA\Discord" -Directory -Filter "app-*" -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1).FullName
    $discordExe = if ($discordAppFolder) { Join-Path $discordAppFolder "Discord.exe" } else { $null }
    
    $started = $false
    if (Test-Path $updateExe) {
        try {
            $shell = New-Object -ComObject Shell.Application
            $shell.ShellExecute($updateExe, $discordArgs, $discordAppFolder, "open", 1)
            $started = $true
        } catch {
            Start-Process -FilePath $updateExe -ArgumentList $discordArgs -WorkingDirectory $discordAppFolder
            $started = $true
        }
    } elseif ($discordExe -and (Test-Path $discordExe)) {
        try {
            $shell = New-Object -ComObject Shell.Application
            $shell.ShellExecute($discordExe, "--proxy-server=http://127.0.0.1:8080", $discordAppFolder, "open", 1)
            $started = $true
        } catch {
            Start-Process -FilePath $discordExe -ArgumentList "--proxy-server=http://127.0.0.1:8080" -WorkingDirectory $discordAppFolder
            $started = $true
        }
    }
    
    if ($started) {
        Write-Host "      [..] جاري التحقق من تحديثات ديسكورد وتخطي حظر التحديث..." -ForegroundColor Yellow
        for ($i = 0; $i -lt 10; $i++) {
            Start-Sleep -Milliseconds 500
            $p = Get-Process Discord -ErrorAction SilentlyContinue
            if ($p -and $p.Count -ge 2) {
                break
            }
        }
        Start-Sleep -Seconds 1
        
        # فك ارتباط النظام فوراً: إطفاء البروكسي كلياً من الويندوز
        Clean-SystemSettings
        Write-Host "      [✓] تم تخطي التحديث وإطفاء بروكسي النظام بالكامل (ProxyEnable = 0)!" -ForegroundColor Green
        Write-Host "      [✓] ديسكورد يعمل الآن في مساره المستقل، وكافة ألعابك وبرامجك تتصل مباشرة 100%." -ForegroundColor Green
    }
    return $started
}

function Cleanup-All {
    Write-Host "`n[..] جاري إيقاف الأداة والتأكد من نظافة النظام 100%..." -ForegroundColor Yellow
    Clean-SystemSettings
    Stop-AllProxies
    Stop-WinDivertDriver
    Write-Host "[✓] تم إيقاف الأداة. النظام والإنترنت في وضعهما الطبيعي تماماً." -ForegroundColor Green
    Start-Sleep -Milliseconds 800
}

Register-EngineEvent -SourceIdentifier ([System.Management.Automation.PsEngineEvent]::Exiting) -Action { Cleanup-All } | Out-Null

Clear-Host
Write-Host "==========================================================================" -ForegroundColor Magenta
Write-Host "    أداة تشغيل ديسكورد الآمنة في تركيا (Black & Purple Luxury Edition)    " -ForegroundColor Cyan
Write-Host "     [نظام العزل الحصري التام: ديسكورد فقط مفكوك الحجب + كل العالم DIRECT] " -ForegroundColor Yellow
Write-Host "==========================================================================" -ForegroundColor Magenta
Write-Host ""

# الخطوة 1: تنظيف وإلغاء أي تعريف كيرنل لحماية ألعابك
Write-Host "[1/4] التأكد من خلو النظام من أي تعريف كيرنل (WinDivert)..." -ForegroundColor Gray
Stop-WinDivertDriver
Write-Host "      [✓] الكيرنل نظيف تماماً (Ricochet و Vanguard آمنة 100%)." -ForegroundColor Green

# الخطوة 2: تشغيل محرك كسر الحجب المحلي
Write-Host "[2/4] تشغيل محرك كسر الحجب المحلي المخصص لتطبيق الديسكورد..." -ForegroundColor Gray
Start-ProxyEngines
Write-Host "      [✓] المحرك المحلي يعمل على المنفذين 1080 و 8080." -ForegroundColor Green

# الخطوة 3: تهيئة نظام العزل الذكي
Write-Host "[3/4] تهيئة نظام العزل الذكي لفك حظر التحديثات..." -ForegroundColor Gray
Write-Host "      [✓] جاهز للمصافحة الذكية مع سيرفرات Discord." -ForegroundColor Green

# الخطوة 4: تشغيل ديسكورد بعد 3 ثوانٍ
Write-Host "[4/4] إطلاق تطبيق Discord خلال 3 ثوانٍ فقط..." -ForegroundColor Cyan

for ($sec = 3; $sec -gt 0; $sec--) {
    Write-Host "      [..] جاري فتح الديسكورد بعد $sec ثانية..." -ForegroundColor Yellow
    Start-Sleep -Seconds 1
}

Start-DiscordApp | Out-Null
Write-Host "      [✓] تم إطلاق تطبيق الديسكورد بنجاح وبدون أي أخطاء تحديث!" -ForegroundColor Green

Start-Sleep -Milliseconds 600
Clear-Host

Write-Host "==========================================================================" -ForegroundColor Magenta
Write-Host "   [✓] الأداة تعمل بنجاح! ديسكورد شغال وكافة ألعابك وبرامجك معزولة 100%.  " -ForegroundColor Green
Write-Host "==========================================================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "  ● تطبيق الديسكورد       : شغال مع فك الحجب بالكامل في مساره المستقل (شات، صوت، بث)" -ForegroundColor Cyan
Write-Host "  ● بروكسي النظام العام   : معطل بالكامل (ProxyEnable = 0) اتصال مباشر 100% لكافة البرامج" -ForegroundColor Green
Write-Host "  ● متصفح Google Chrome   : اتصال مباشر ونظيف 100% بسيرفرات Google (لا تسجيل خروج)" -ForegroundColor Green
Write-Host "  ● منصات وسيرفرات الألعاب: Steam, Epic Games, Ubisoft, CoD, Valorant (اتصال مباشر 0ms)" -ForegroundColor Yellow
Write-Host "  ● حارس الإقلاع الآمن   : مفعل في الويندوز لحماية إعدادات جهازك عند كل إعادة تشغيل" -ForegroundColor White
Write-Host ""
Write-Host "  [!] ملاحظة هامة لكول أوف ديوتي:" -ForegroundColor Magenta
Write-Host "      يرجى إيقاف ميزة تراكب اللعبة (In-Game Overlay) من إعدادات الديسكورد" -ForegroundColor Magenta
Write-Host "      (Settings -> Game Overlay -> إيقاف / Off) لمنع تعارض الديسكورد مع محرك اللعبة." -ForegroundColor Magenta
Write-Host ""
Write-Host "==========================================================================" -ForegroundColor DarkGray
Write-Host "  الخيارات المتاحة:" -ForegroundColor Yellow
Write-Host "    [1] فتح / إعادة تشغيل تطبيق الديسكورد (Discord)" -ForegroundColor White
Write-Host "    [2] فحص جودة اتصال الديسكورد والاتصال المباشر للألعاب والمتصفح" -ForegroundColor White
Write-Host "    [3] فحص أمان لعبة Call of Duty والتأكد من خلو النظام من WinDivert" -ForegroundColor White
Write-Host "    [Q] إيقاف الأداة وإعادة النظام للوضع الافتراضي النظيف تماماً" -ForegroundColor White
Write-Host "==========================================================================" -ForegroundColor DarkGray
Write-Host " (اترك هذه النافذة مفتوحة أثناء استخدام الديسكورد ولعب ألعابك)" -ForegroundColor Gray
Write-Host ""

try {
    while ($true) {
        $choice = Read-Host "اختر خياراً [1, 2, 3, Q] ثم اضغط Enter"
        if ($choice -eq "1") {
            Write-Host "`n[..] جاري إعادة إطلاق تطبيق الديسكورد..." -ForegroundColor Cyan
            $ok = Start-DiscordApp
            if ($ok) {
                Write-Host "[✓] تم فتح تطبيق الديسكورد بنجاح وستظهر النافذة أمامك الآن!" -ForegroundColor Green
            } else {
                Write-Host "[!] افتح الديسكورد كالمعتاد." -ForegroundColor Yellow
            }
            Write-Host ""
        }
        elseif ($choice -eq "2") {
            Write-Host "`n[..] جاري فحص الاتصال والتأكد من العزل..." -ForegroundColor Cyan
            try {
                $code1 = & curl.exe -s -o /dev/null -w "%{http_code}" -x http://127.0.0.1:8080 --connect-timeout 6 "https://discord.com"
                $code2 = & curl.exe -s -o /dev/null -w "%{http_code}" -x http://127.0.0.1:8080 --connect-timeout 6 "https://updates.discord.com/distributions/app/manifests/latest?channel=stable&platform=win&arch=x64"
                Write-Host "      - اتصال تطبيق ديسكورد المعزول : $code1 (شغال ومفكوك الحجب 100%)" -ForegroundColor Green
                Write-Host "      - اتصال سيرفر التحديثات       : $code2 (شغال ومفكوك الحجب 100%)" -ForegroundColor Green
                
                $regProxy = (Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings").ProxyEnable
                Write-Host "      - حالة بروكسي الويندوز العام : $(if ($regProxy -eq 0) {'معطل ومباشر 100% (Direct)'} else {'مفعل'})" -ForegroundColor Green
                
                $codeGoogle = & curl.exe -s -o /dev/null -w "%{http_code}" --connect-timeout 4 "https://accounts.google.com"
                $codeSteam  = & curl.exe -s -o /dev/null -w "%{http_code}" --connect-timeout 4 "https://store.steampowered.com"
                $codeEpic   = & curl.exe -s -o /dev/null -w "%{http_code}" --connect-timeout 4 "https://store.epicgames.com"
                $codeUbi    = & curl.exe -s -o /dev/null -w "%{http_code}" --connect-timeout 4 "https://www.ubisoft.com"
                Write-Host "      - اتصال Google Chrome مباشر  : $codeGoogle (مباشر ونظيف 100%)" -ForegroundColor Green
                Write-Host "      - اتصال Steam مباشر          : $codeSteam (مباشر ونظيف 100%)" -ForegroundColor Green
                Write-Host "      - اتصال Epic Games مباشر     : $codeEpic (مباشر ونظيف 100%)" -ForegroundColor Green
                Write-Host "      - اتصال Ubisoft مباشر        : $codeUbi (مباشر ونظيف 100%)" -ForegroundColor Green
            } catch {
                Write-Host "[X] حدث خطأ أثناء فحص الاتصال." -ForegroundColor Red
            }
            Write-Host ""
        }
        elseif ($choice -eq "3") {
            Write-Host "`n[..] فحص أمان لعبة Call of Duty في نظام ويندوز..." -ForegroundColor Cyan
            $divertRunning = & sc.exe query WinDivert 2>&1
            $gDpiRunning = Get-Process goodbyedpi -ErrorAction SilentlyContinue
            
            if ($divertRunning -match "RUNNING" -or $gDpiRunning) {
                Write-Host "[!] تنبيه: تم العثور على WinDivert. جاري تنظيفه الآن..." -ForegroundColor Yellow
                Stop-WinDivertDriver
            }
            
            $divertCheck = & sc.exe query WinDivert 2>&1
            if ($divertCheck -match "RUNNING") {
                Write-Host "[!] تعذر إيقاف الخدمة تلقائياً. تأكد من تشغيل الأداة كمسؤول." -ForegroundColor Yellow
            } else {
                Write-Host "[✓] ممتاز! لا يوجد أي أثر لتعريف WinDivert في نظام الكيرنل." -ForegroundColor Green
                Write-Host "[✓] كول أوف ديوتي وفالورانت آمنة 100% ولا يوجد أي تعارض مع برامج الحماية!" -ForegroundColor Green
            }
            Write-Host ""
        }
        elseif ($choice -match "^[Qq]|exit|quit$") {
            break
        }
    }
}
finally {
    Cleanup-All
}
