@echo off
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0Build-DesktopRelease.ps1"
pause
