@echo off
echo ========================================
echo Cleaning Visual Studio Cache and Rebuilding
echo ========================================
echo.

cd /d "%~dp0"

echo Cleaning bin and obj directories...
if exist "Yakult.Inventory.App\bin" rmdir /s /q "Yakult.Inventory.App\bin"
if exist "Yakult.Inventory.App\obj" rmdir /s /q "Yakult.Inventory.App\obj"

echo.
echo Deleting Visual Studio cache files...
del /s /q /f *.suo 2>nul
del /s /q /f .vs\*.* 2>nul

echo.
echo ========================================
echo Cache cleared! Now rebuild the solution in Visual Studio.
echo ========================================
echo.
echo Instructions:
echo 1. Close Visual Studio if it's open
echo 2. Run this script
echo 3. Open Visual Studio
echo 4. Right-click solution -> Clean Solution
echo 5. Right-click solution -> Rebuild Solution
echo.

pause
