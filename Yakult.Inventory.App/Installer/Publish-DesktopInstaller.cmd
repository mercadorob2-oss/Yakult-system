@echo off
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0Publish-DesktopRelease.ps1"
pause
