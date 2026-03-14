@echo off
REM Launches the Balsam Supervisor, which manages the API lifecycle.
REM The Supervisor runs on port 5001 and the API on port 5000.

set "SCRIPT_DIR=%~dp0"
set "SUPERVISOR=%SCRIPT_DIR%supervisor\Balsam.Supervisor.exe"

if not exist "%SUPERVISOR%" (
    echo Error: Supervisor executable not found at %SUPERVISOR%
    echo Make sure you extracted the complete Balsam Standalone bundle.
    pause
    exit /b 1
)

"%SUPERVISOR%"
