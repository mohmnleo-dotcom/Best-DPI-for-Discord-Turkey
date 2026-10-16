@echo off
pushd "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Create_Discord_Desktop_Shortcut.ps1"
popd
exit /b 0