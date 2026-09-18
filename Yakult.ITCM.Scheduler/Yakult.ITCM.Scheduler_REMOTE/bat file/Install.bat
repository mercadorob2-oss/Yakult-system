@echo off
:: One-click installer for Yakult ITCM Background Scheduler
:: Run this as Administrator on the target server
::
:: This batch file wraps the PowerShell deployment script so you can
:: double-click it instead of opening PowerShell manually.

title Yakult ITCM Scheduler Installation

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Deploy-ITCMScheduler.ps1" -ExePath "C:\Yakult\Yakult.ITCM.Scheduler\Yakult.ITCM.Scheduler.exe"

echo.
echo Press any key to exit...
pause > nul
