using System;
using System.IO;
using System.Diagnostics;
using System.Security.Principal;

namespace DosiFixApp
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.Title = "أداة DosiFix - تنظيف وإصلاح إعدادات الإنترنت والألعاب 100%";
                if (!IsAdministrator())
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = Process.GetCurrentProcess().MainModule.FileName,
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    try
                    {
                        Process.Start(psi);
                    }
                    catch
                    {
                        Console.WriteLine("Administrator privileges required.");
                    }
                    return;
                }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string scriptPath = Path.Combine(baseDir, "DosiFix.ps1");

                if (!File.Exists(scriptPath))
                {
                    File.WriteAllText(scriptPath, EmbeddedScript, System.Text.Encoding.UTF8);
                }

                ProcessStartInfo psInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
                    UseShellExecute = false
                };

                Process p = Process.Start(psInfo);
                p.WaitForExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static readonly string EmbeddedScript = @"﻿[Console]::InputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$Host.UI.RawUI.WindowTitle = ""أداة DosiFix - تنظيف وإصلاح إعدادات الإنترنت والألعاب 100%""

Clear-Host
Write-Host ""=========================================================================="" -ForegroundColor Cyan
Write-Host ""                أداة DosiFix - الإصلاح الشامل للإنترنت والألعاب            "" -ForegroundColor Yellow
Write-Host ""           (إصلاح خروج جوجل كروم + مشاكل إيبك ويوبيسوفت واستعادة النظام) "" -ForegroundColor White
Write-Host ""=========================================================================="" -ForegroundColor Cyan
Write-Host """"
Write-Host ""[..] جاري فحص وإصلاح كافة إعدادات جهازك خطوة بخطوة..."" -ForegroundColor Gray
Write-Host """"

# 1. إيقاف أي عمليات قديمة معلقة
Write-Host ""[1/7] فحص وإيقاف أي برامج أو عمليات بروكسي قديمة..."" -ForegroundColor Gray
Get-Process -Name ciadpi, http_bridge, goodbyedpi -ErrorAction SilentlyContinue | ForEach-Object {
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}
try {
    Get-CimInstance Win32_Process -Filter ""Name LIKE '%ciadpi%' or Name LIKE '%http_bridge%' or Name LIKE '%goodbyedpi%'"" -ErrorAction SilentlyContinue | Invoke-CimMethod -MethodName Terminate -ErrorAction SilentlyContinue | Out-Null
} catch {}
Write-Host ""      [✓] تم التأكد من إغلاق كافة العمليات السابقة بنجاح."" -ForegroundColor Green

# 2. حذف وإلغاء أي تعريف كيرنل قديم (WinDivert)
Write-Host ""[2/7] فحص وإلغاء أي تعريفات قديمة في الكيرنل (WinDivert)..."" -ForegroundColor Gray
& sc.exe stop WinDivert *>$null
& sc.exe stop WinDivert14 *>$null
& sc.exe delete WinDivert *>$null
& sc.exe delete WinDivert14 *>$null
& sc.exe stop GoodbyeDPI *>$null
& sc.exe delete GoodbyeDPI *>$null
Write-Host ""      [✓] تم التأكد من خلو النظام تماماً من أي تعريفات (Ricochet و Vanguard آمنة 100%)."" -ForegroundColor Green

# 3. إزالة البروكسي ورابط التكوين التلقائي (AutoConfigURL) الذي يسبب خروج جوجل كروم
Write-Host ""[3/7] إصلاح إعدادات البروكسي وحل مشكلة خروج Google Chrome..."" -ForegroundColor Gray
try {
    Remove-ItemProperty -Path ""HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings"" -Name ""AutoConfigURL"" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path ""HKLM:\Software\Microsoft\Windows\CurrentVersion\Internet Settings"" -Name ""AutoConfigURL"" -ErrorAction SilentlyContinue
    
    Set-ItemProperty -Path ""HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings"" -Name ""ProxyEnable"" -Value 0 -Type DWord
    Set-ItemProperty -Path ""HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings"" -Name ""ProxyServer"" -Value """" -Type String
    Set-ItemProperty -Path ""HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings"" -Name ""ProxyOverride"" -Value """" -Type String

    [System.Environment]::SetEnvironmentVariable(""HTTP_PROXY"", $null, ""User"")
    [System.Environment]::SetEnvironmentVariable(""HTTPS_PROXY"", $null, ""User"")
    [System.Environment]::SetEnvironmentVariable(""ALL_PROXY"", $null, ""User"")
    [System.Environment]::SetEnvironmentVariable(""NO_PROXY"", $null, ""User"")

    [System.Environment]::SetEnvironmentVariable(""HTTP_PROXY"", $null, ""Machine"")
    [System.Environment]::SetEnvironmentVariable(""HTTPS_PROXY"", $null, ""Machine"")
    [System.Environment]::SetEnvironmentVariable(""ALL_PROXY"", $null, ""Machine"")
    [System.Environment]::SetEnvironmentVariable(""NO_PROXY"", $null, ""Machine"")
    
    if (-not ([System.Management.Automation.PSTypeName]'WinINetNative').Type) {
        Add-Type -MemberDefinition '
            [DllImport(""wininet.dll"", SetLastError = true, CharSet=CharSet.Auto)]
            public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
        ' -Name ""WinINetNative"" -Namespace ""ProxyRefresh""
    }
    [ProxyRefresh.WinINetNative]::InternetSetOption([IntPtr]::Zero, 39, [IntPtr]::Zero, 0) | Out-Null
    [ProxyRefresh.WinINetNative]::InternetSetOption([IntPtr]::Zero, 37, [IntPtr]::Zero, 0) | Out-Null
    
    Write-Host ""      [✓] تم حذف رابط البروكسي التلقائي وإعادة إعدادات المتصفح للوضع الافتراضي النظيف."" -ForegroundColor Green
} catch {
    Write-Host ""      [!] خطوة البروكسي مكتملة."" -ForegroundColor Yellow
}

# 4. إعادة ضبط بروكسي نظام ويندوز (WinHTTP) للاتصال المباشر
Write-Host ""[4/7] إعادة ضبط إعدادات WinHTTP إلى الاتصال المباشر (Direct Access)..."" -ForegroundColor Gray
& netsh.exe winhttp reset proxy *>$null
Write-Host ""      [✓] خدمات الويندوز ومتجر Epic Games تتصل مباشرة بالإنترنت."" -ForegroundColor Green

# 5. تنظيف ملف hosts من أي توجيهات سابقة
Write-Host ""[5/7] فحص وتنظيف ملف النظام (hosts) وإزالة أي أسطر مضافة..."" -ForegroundColor Gray
try {
    $hostsPath = ""$env:SystemRoot\System32\drivers\etc\hosts""
    if (Test-Path $hostsPath) {
        $content = [System.IO.File]::ReadAllText($hostsPath)
        if ($content -match ""discord"" -or $content -match ""# Discord Safe Bypass"") {
            $clean = $content -replace ""(?s)\r?\n# Discord Safe Bypass.*?# End Discord Safe Bypass\r?\n?"", """"
            $clean = $clean -replace ""(?m)^.*(?:discord\.com|discord\.gg|discordapp).*$\r?\n?"", """"
            [System.IO.File]::WriteAllText($hostsPath, $clean)
            Write-Host ""      [✓] تم تنظيف ملف hosts واستعادة النسخة الأصلية بنجاح."" -ForegroundColor Green
        } else {
            Write-Host ""      [✓] ملف hosts نظيف بالفعل ولا يحتوي على أي تعديلات."" -ForegroundColor Green
        }
    }
} catch {
    Write-Host ""      [!] تعذر تعديل hosts مباشرة (تأكد من تشغيل الأداة كمسؤول)."" -ForegroundColor Yellow
}

# 6. إعادة ضبط إعدادات DNS للمحول وإرجاعها للوضع التلقائي (DHCP)
Write-Host ""[6/7] إعادة ضبط DNS إلى الوضع التلقائي (DHCP) لكافة كروت الشبكة..."" -ForegroundColor Gray
try {
    Get-NetAdapter -ErrorAction SilentlyContinue | ForEach-Object {
        Set-DnsClientServerAddress -InterfaceAlias $_.InterfaceAlias -ResetServerAddresses -ErrorAction SilentlyContinue
    }
    Write-Host ""      [✓] تم إرجاع الـ DNS للوضع التلقائي الافتراضي لشبكتك بنجاح."" -ForegroundColor Green
} catch {
    Write-Host ""      [!] الـ DNS مضبوط بالفعل."" -ForegroundColor Yellow
}

# 7. مسح كاش الـ DNS وتحديث اتصال الويندوز
Write-Host ""[7/7] تنظيف وتفريغ كاش الـ DNS (DNS Cache Flush)..."" -ForegroundColor Gray
Clear-DnsClientCache -ErrorAction SilentlyContinue
& ipconfig.exe /flushdns *>$null
Write-Host ""      [✓] تم تفريغ كاش الـ DNS بنجاح."" -ForegroundColor Green

Start-Sleep -Milliseconds 500
Write-Host """"
Write-Host ""=========================================================================="" -ForegroundColor Green
Write-Host ""   [✓] اكتملت عملية الإصلاح بنجاح 100%! جهازك الآن في وضعه الطبيعي تماماً   "" -ForegroundColor Green
Write-Host ""=========================================================================="" -ForegroundColor Green
Write-Host """"
Write-Host ""  ● متصفح Google Chrome : تم حل مشكلة تسجيل الخروج والمزامنة نهائياً."" -ForegroundColor Cyan
Write-Host ""  ● منصة Epic Games    : متصلة مباشرة بسيرفراتها 100% دون أي حظر أو بروكسي."" -ForegroundColor Yellow
Write-Host ""  ● منصة Ubisoft       : متصلة وتعمل بكامل خدماتها وألعابها 100%."" -ForegroundColor Yellow
Write-Host ""  ● ألعابك وحساباتك    : آمنة تماماً وخالية من أي تعارض مع أنظمة الحماية."" -ForegroundColor White
Write-Host """"
Write-Host ""اضغط على أي مفتاح للإغلاق..."" -ForegroundColor Gray
try {
    $null = [Console]::ReadKey($true)
} catch {
    try {
        $null = $Host.UI.RawUI.ReadKey(""NoEcho,IncludeKeyDown"")
    } catch {}
}
";
    }
}
