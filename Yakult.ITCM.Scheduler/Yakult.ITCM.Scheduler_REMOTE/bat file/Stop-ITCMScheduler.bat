@echo off
setlocal

set "NoPause="
if /I "%~1"=="--nopause" set "NoPause=1"
set "RelaunchArgs=%*"

:: ── Auto-elevate to Administrator (required for task management) ──
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting Administrator privileges...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath $env:ComSpec -Verb RunAs -ArgumentList '/k','call ""%~f0"" %RelaunchArgs%'"
    exit /b
)

set "TaskName=\Yakult\Yakult ITCM Background Processing"
set "TaskShort=Yakult ITCM Background Processing"
set "EndErrorFile=%temp%\YakultITCM_Stop_End_Error.txt"
set "DisableErrorFile=%temp%\YakultITCM_Stop_Disable_Error.txt"
set "StateFile=%temp%\YakultITCM_Stop_State.txt"

echo ================================================
echo  Yakult ITCM Scheduler - Disable Scheduler
echo ================================================
echo.

:: 1) End currently running instance (if any)
echo [1/2] Stopping any currently running instance...
if exist "%EndErrorFile%" del "%EndErrorFile%" >nul 2>&1
schtasks /End /TN "%TaskName%" 1>nul 2>"%EndErrorFile%"
if %errorlevel% equ 0 (
    echo       OK - Running instance stopped.
) else (
    echo       NOTE - No running instance found or it was already stopped.
    if exist "%EndErrorFile%" (
        for /f "usebackq delims=" %%E in ("%EndErrorFile%") do echo         %%E
    )
)

echo.

:: 2) Disable the scheduled task
echo [2/2] Disabling the scheduler task...
if exist "%DisableErrorFile%" del "%DisableErrorFile%" >nul 2>&1
schtasks /Change /TN "%TaskName%" /Disable 1>nul 2>"%DisableErrorFile%"
if %errorlevel% equ 0 (
    echo       OK - Task disabled via schtasks.
) else (
    echo       WARNING - schtasks could not disable the task.
    if exist "%DisableErrorFile%" (
        for /f "usebackq delims=" %%E in ("%DisableErrorFile%") do echo         %%E
    )
    echo       Trying PowerShell Disable-ScheduledTask fallback...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Disable-ScheduledTask -TaskPath '\Yakult\' -TaskName 'Yakult ITCM Background Processing' -ErrorAction Stop | Out-Null; exit 0 } catch { Write-Error $_.Exception.Message; exit 1 }"
    if %errorlevel% equ 0 (
        echo       OK - Task disabled via PowerShell.
    ) else (
        echo       ERROR - PowerShell fallback also failed.
    )
)

echo.
echo Verifying final scheduler state...
set "TaskStatus="
if exist "%StateFile%" del "%StateFile%" >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { $t = Get-ScheduledTask -TaskPath '\Yakult\' -TaskName 'Yakult ITCM Background Processing' -ErrorAction Stop; [string]$t.State | Set-Content -Path '%StateFile%' -Encoding ASCII } catch { '' | Set-Content -Path '%StateFile%' -Encoding ASCII }"
if exist "%StateFile%" set /p TaskStatus=<"%StateFile%"

if /I "%TaskStatus%"=="Disabled" (
    echo       VERIFIED - Scheduler is disabled.
) else (
    if defined TaskStatus (
        echo       WARNING - Scheduler state is still %TaskStatus%.
    ) else (
        echo       WARNING - Could not verify final scheduler state.
    )
)

if exist "%DisableErrorFile%" del "%DisableErrorFile%" >nul 2>&1
if exist "%EndErrorFile%" del "%EndErrorFile%" >nul 2>&1
if exist "%StateFile%" del "%StateFile%" >nul 2>&1

echo.
echo ================================================
echo  Scheduler DISABLED.
echo ================================================
echo.
echo To re-enable the scheduler later, run Start-ITCMScheduler.bat
echo.
if not defined NoPause pause
