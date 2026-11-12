@echo off
setlocal
rem Let Windows PowerShell initialize its own modules when launched from PowerShell 7.
set "PSModulePath="
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0Publish-DesktopRelease.ps1" %*
set "publishExitCode=%errorlevel%"
pause
exit /b %publishExitCode%
