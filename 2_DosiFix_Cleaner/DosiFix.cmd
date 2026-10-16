@echo off
setlocal
pushd "%~dp0"

REM Check for administrative privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ======================================================
    echo  Requesting Administrator privileges for DosiFix...
    echo ======================================================
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -WorkingDirectory '%~dp0' -Verb RunAs"
    exit /b
)

chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0DosiFix.ps1"

popd
exit /b