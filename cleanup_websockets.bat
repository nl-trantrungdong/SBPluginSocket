@echo off
echo WebSocket Cleanup Utility for SilverBullet
echo ========================================

echo.
echo Checking for SilverBullet processes...

REM Tìm và kill tất cả SilverBullet processes
tasklist /FI "IMAGENAME eq SilverBullet.exe" 2>NUL | find /I /N "SilverBullet.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo Found SilverBullet processes. Terminating...
    taskkill /F /IM "SilverBullet.exe" /T
    if "%ERRORLEVEL%"=="0" (
        echo Successfully terminated SilverBullet processes.
    ) else (
        echo Failed to terminate some processes.
    )
) else (
    echo No SilverBullet processes found.
)

REM Tìm và kill các processes có thể liên quan đến WebSocket
echo.
echo Checking for related processes...

REM Kill các processes có thể giữ WebSocket connections
tasklist /FI "IMAGENAME eq dotnet.exe" 2>NUL | find /I /N "dotnet.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo Found dotnet processes. Checking if they're related to SilverBullet...
    for /f "tokens=2" %%i in ('tasklist /FI "IMAGENAME eq dotnet.exe" /FO CSV ^| find "dotnet.exe"') do (
        echo Checking process %%i...
        wmic process where "ProcessId=%%i" get CommandLine /format:list | find "SilverBullet" >NUL
        if "%ERRORLEVEL%"=="0" (
            echo Terminating SilverBullet-related dotnet process %%i...
            taskkill /F /PID %%i
        )
    )
)

REM Kill các processes có thể giữ file handles
echo.
echo Checking for processes that might hold file handles...

REM Kill các processes có thể giữ DLL files
tasklist /FI "IMAGENAME eq rundll32.exe" 2>NUL | find /I /N "rundll32.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo Found rundll32 processes. These might be holding DLL files...
    taskkill /F /IM "rundll32.exe" /T
)

echo.
echo Cleanup completed.
echo.
echo If you still cannot delete the SilverBullet folder:
echo 1. Restart your computer
echo 2. Try deleting the folder in Safe Mode
echo 3. Use Process Explorer to find which process is holding the files
echo.
pause 