@echo off
echo ========================================
echo Deploying call-field-visits.ashx to Development Server (_local)
echo ========================================
echo.

echo Source: %~dp0Yakult.Inventory.Api2_remote\call-field-visits.ashx
echo Destination: C:\inetpub\wwwroot\Yakult.Inventory.Api2_local\call-field-visits.ashx
echo.

copy "%~dp0Yakult.Inventory.Api2_remote\call-field-visits.ashx" "C:\inetpub\wwwroot\Yakult.Inventory.Api2_local\call-field-visits.ashx"

if %errorlevel% equ 0 (
    echo.
    echo [SUCCESS] File deployed successfully to _local!
    echo.
) else (
    echo.
    echo [ERROR] Deployment failed. Make sure you ran this as Administrator.
    echo.
)

pause
