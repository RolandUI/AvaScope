@echo off
setlocal EnableExtensions DisableDelayedExpansion
title AvaScope Installation Verification
mode con cols=72 lines=24 >nul 2>&1
color 07
for /F "delims=#" %%E in ('"prompt #$E# & for %%E in (1) do rem"') do set "ESC=%%E"

set "NO_PAUSE="
if /I "%~1"=="--no-pause" set "NO_PAUSE=1"

set "RESULT_FILE=%TEMP%\avascope-verify-%RANDOM%-%RANDOM%.txt"
call "%~dp0avascope.cmd" --version >"%RESULT_FILE%" 2>&1
set "VERIFY_EXIT=%ERRORLEVEL%"
set "VERSION="
set /p "VERSION="<"%RESULT_FILE%"

if not "%VERIFY_EXIT%"=="0" goto verification_failed
if not defined VERSION goto verification_failed

:verification_succeeded
cls
echo.
echo ========================================================================
echo                         AVASCOPE SETUP CHECK
echo ========================================================================
echo.
echo.
echo                    %ESC%[92m        [ SUCCESS ]%ESC%[0m
echo.
echo                     AvaScope is ready to use.
echo.
echo   Installed version : %VERSION%
echo   Command           : avascope
echo   MCP server        : avascope mcp
echo.
echo ------------------------------------------------------------------------
echo        Open a new terminal before using the PATH command.
echo ------------------------------------------------------------------------
set "VERIFY_EXIT=0"
goto verification_finished

:verification_failed
cls
echo.
echo ========================================================================
echo                         AVASCOPE SETUP CHECK
echo ========================================================================
echo.
echo.
echo                    %ESC%[91m         [ FAILED ]%ESC%[0m
echo.
echo                  AvaScope could not be started.
echo.
echo ------------------------------------------------------------------------
type "%RESULT_FILE%"
echo ------------------------------------------------------------------------
echo.
echo   Install the Microsoft .NET 10 Runtime, then run Setup again.
set "VERIFY_EXIT=1"

:verification_finished
del /q "%RESULT_FILE%" >nul 2>&1
echo.
if defined NO_PAUSE goto verification_exit
echo                       Press any key to close...
pause >nul

:verification_exit
exit /b %VERIFY_EXIT%
