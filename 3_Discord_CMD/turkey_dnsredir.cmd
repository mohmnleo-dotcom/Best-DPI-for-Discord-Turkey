@echo off
setlocal
pushd "%~dp0"

REM Check for administrative privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ======================================================
    echo  Requesting Administrator privileges...
    echo ======================================================
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -WorkingDirectory '%~dp0' -Verb RunAs"
    exit /b
)

chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0turkey_dnsredir.ps1"

REM Clean up any proxy settings upon exit to ensure 100% clean state
powershell -NoProfile -ExecutionPolicy Bypass -Command "Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' -Name 'ProxyEnable' -Value 0 -Type DWord; Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' -Name 'AutoConfigURL' -ErrorAction SilentlyContinue" >nul 2>&1

popd
exit /b
