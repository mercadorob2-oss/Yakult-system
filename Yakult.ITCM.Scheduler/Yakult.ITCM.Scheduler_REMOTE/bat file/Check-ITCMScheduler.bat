@echo off
setlocal
title Yakult ITCM Scheduler Checker

set "NoPause="
if /I "%~1"=="--nopause" set "NoPause=1"
set "RelaunchArgs=%*"

 net session >nul 2>&1
 if errorlevel 1 (
     powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath $env:ComSpec -Verb RunAs -ArgumentList '/k','call ""%~f0"" %RelaunchArgs%'"
     exit /b
 )
 
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Check-ITCMScheduler.ps1"

if not defined NoPause echo.
if not defined NoPause echo Press any key to exit...
if not defined NoPause pause > nul
