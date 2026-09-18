@echo off
echo RDP wrapper started at %DATE% %TIME% > %USERPROFILE%\Desktop\launcher_wrapper.log
echo Starting %USERPROFILE%\YakultLauncher\YakultLauncher.exe... >> %USERPROFILE%\Desktop\launcher_wrapper.log
%USERPROFILE%\YakultLauncher\YakultLauncher.exe >> %USERPROFILE%\Desktop\launcher_wrapper.log 2>&1
echo EXIT CODE: %ERRORLEVEL% >> %USERPROFILE%\Desktop\launcher_wrapper.log
