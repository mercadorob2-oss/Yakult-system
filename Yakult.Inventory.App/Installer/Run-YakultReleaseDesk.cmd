@echo off
setlocal
set "PSModulePath="
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -STA -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0YakultReleaseDesk.ps1" %*
if errorlevel 1 (
    echo.
    echo Yakult Release Desk could not start.
    pause
)
endlocal
