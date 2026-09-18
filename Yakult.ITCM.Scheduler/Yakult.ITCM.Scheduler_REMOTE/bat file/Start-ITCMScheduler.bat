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
set "TaskErrorFile=%temp%\YakultITCM_Start_Error.txt"
set "RunStateFile=%temp%\YakultITCM_Start_State.txt"

echo ================================================
echo  Yakult ITCM Scheduler - Start Background Process
echo ================================================
echo.

:: 1) Check if the task exists
echo [1/3] Checking if scheduled task exists...
if exist "%TaskErrorFile%" del "%TaskErrorFile%" >nul 2>&1
schtasks /Query /TN "%TaskName%" 1>nul 2>"%TaskErrorFile%"
if %errorlevel% neq 0 (
    echo       WARNING - Task NOT FOUND.
    if exist "%TaskErrorFile%" (
        for /f "usebackq delims=" %%E in ("%TaskErrorFile%") do echo         %%E
    )
    echo.
    echo       The scheduler has not been installed yet.
    echo       Run Install.bat first to deploy the scheduler.
    echo.
    if not defined NoPause pause
    exit /b 1
)
echo       OK - Task found.

echo.

:: 2) Enable the scheduled task
echo [2/3] Enabling scheduled task...
if exist "%TaskErrorFile%" del "%TaskErrorFile%" >nul 2>&1
schtasks /Change /TN "%TaskName%" /Enable 1>nul 2>"%TaskErrorFile%"
if %errorlevel% equ 0 (
    echo       OK - Task enabled.
) else (
    echo       ERROR - Could not enable task.
    if exist "%TaskErrorFile%" (
        for /f "usebackq delims=" %%E in ("%TaskErrorFile%") do echo         %%E
    )
    if not defined NoPause pause
    exit /b 1
)

echo.

:: 3) Optionally trigger a run now
echo [3/3] Enabling complete. Attempting an immediate run...
if exist "%TaskErrorFile%" del "%TaskErrorFile%" >nul 2>&1
schtasks /Run /TN "%TaskName%" 1>nul 2>"%TaskErrorFile%"
if %errorlevel% equ 0 (
    echo       OK - Immediate run request accepted.
) else (
    echo       NOTE - Immediate run request was not accepted.
    if exist "%TaskErrorFile%" (
        for /f "usebackq delims=" %%E in ("%TaskErrorFile%") do echo         %%E
    )
    echo              The scheduler is still enabled and will run on its schedule.
)

echo.
echo Verifying final task state...
set "TaskStatus="
if exist "%RunStateFile%" del "%RunStateFile%" >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { $t = Get-ScheduledTask -TaskPath '\Yakult\' -TaskName 'Yakult ITCM Background Processing' -ErrorAction Stop; [string]$t.State | Set-Content -Path '%RunStateFile%' -Encoding ASCII } catch { '' | Set-Content -Path '%RunStateFile%' -Encoding ASCII }"
if exist "%RunStateFile%" set /p TaskStatus=<"%RunStateFile%"

if /I "%TaskStatus%"=="Ready" (
    echo       VERIFIED - Scheduler is enabled. Task state is Ready.
) else (
    if /I "%TaskStatus%"=="Running" (
        echo       VERIFIED - Scheduler is enabled. Task state is Running.
    ) else (
        if /I "%TaskStatus%"=="Queued" (
            echo       VERIFIED - Scheduler is enabled. Task state is Queued.
        ) else (
            if defined TaskStatus (
                echo       WARNING - Scheduler state is %TaskStatus%.
            ) else (
                echo       WARNING - Could not verify final task state.
            )
        )
    )
)

if exist "%TaskErrorFile%" del "%TaskErrorFile%" >nul 2>&1
if exist "%RunStateFile%" del "%RunStateFile%" >nul 2>&1

echo.
echo ================================================
echo  Scheduler ENABLED.
echo ================================================
echo.
echo The scheduler is now enabled and will run automatically.
echo Use Check-ITCMScheduler.bat to verify it is healthy.
echo Use Stop-ITCMScheduler.bat to disable it again.
echo.
if not defined NoPause pause
