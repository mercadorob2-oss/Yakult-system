@echo off
setlocal EnableDelayedExpansion

echo === Yakult API2 - IIS Setup (Admin Required) ===
echo.
echo Check if running as Administrator...
net session >nul 2>&1
if errorlevel 1 (
    echo ERROR: Must run as Administrator.
    echo Right-click this file ^& select "Run as administrator"
    pause
    exit /b 1
)

set SITE_NAME=Yakult.Inventory.Api2
set APP_POOL=YakultAPI2
set PORT=7014
set PHYSICAL_PATH=C:\inetpub\wwwroot\Yakult.Inventory.Api2_remote
set APPCMD=%windir%\system32\inetsrv\appcmd.exe

:: 1. App pool (v4.0 for .NET Framework 4.7.2)
echo [1/4] App pool...
%APPCMD% list apppool /name:"%APP_POOL%" >nul 2>nul
if errorlevel 1 (
    %APPCMD% add apppool /name:"%APP_POOL%" /managedRuntimeVersion:"v4.0" /managedPipelineMode:"Integrated"
    echo   Created: %APP_POOL%
) else (
    %APPCMD% set apppool "%APP_POOL%" /managedRuntimeVersion:"v4.0" /managedPipelineMode:"Integrated"
    echo   Updated: %APP_POOL%
)

:: 2. Site
echo [2/4] Site...
%APPCMD% list site /name:"%SITE_NAME%" >nul 2>nul
if errorlevel 1 (
    %APPCMD% add site /name:"%SITE_NAME%" /bindings:"http://*:%PORT%:" /physicalPath:"%PHYSICAL_PATH%"
    echo   Created: %SITE_NAME% on port %PORT%
) else (
    echo   Site already exists. Updating physical path...
    :: physicalPath is a vdir property (not app). Use double "//" for root vdir under root app.
    %APPCMD% set vdir "%SITE_NAME%//" /physicalPath:"%PHYSICAL_PATH%"
    if not errorlevel 1 ( echo   Physical path updated ) else ( echo   WARNING: vdir update failed )
)

:: 3. Assign app pool
echo [3/4] Assigning app pool...
%APPCMD% set app "%SITE_NAME%/" /applicationPool:"%APP_POOL%"

:: 4. Start
echo [4/4] Starting site...
%APPCMD% start apppool /apppool.name:"%APP_POOL%" >nul 2>nul
%APPCMD% start site /site.name:"%SITE_NAME%" >nul 2>nul
if errorlevel 1 (
    echo   WARNING: Site may already be running, or check port conflict.
    %APPCMD% list site /name:"%SITE_NAME%" | findstr /C:"state:Started" >nul && echo   Site is running.
) else (
    echo   Site started.
)

echo.
echo === Done ===
echo API base:  http://localhost:%PORT%/
echo Health:    http://localhost:%PORT%/dbinfo.ashx
echo.

pause
