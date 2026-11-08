@echo off
rem SurugaCC: set up cmd.exe to use this package (surugacc, surugacc-sgdk, run68k).
rem
rem   setup.bat                         set up this cmd window
rem   setup.bat C:\IsuSDK               also set GDK / ISDK / SGDK (the IsuSDK folder used by surugacc-sgdk)
rem   setup.bat /persist [C:\IsuSDK]    also write to the user environment variables (new windows)
rem   setup.bat /remove                 undo (this window and the user environment variables)
rem
rem Sets PATH (this folder and bin\), SURUGACC_HOME, SURUGACC (surugacc.exe) and, with an IsuSDK folder, GDK, ISDK and SGDK (SGDK is kept for compatibility).
rem The persistent part and the tool check are done by setup.ps1 (Windows PowerShell).
rem This file is kept ASCII only so that cmd.exe reads it the same way in any code page.

set "SURUGACC_HOME=C:\Users\hiro_\working\tools\SurugaCC"
if "%SURUGACC_HOME:~-1%"=="\" set "SURUGACC_HOME=%SURUGACC_HOME:~0,-1%"
set "_SURUGA_SGDK="
set "_SURUGA_OPT="

:args
if "%~1"=="" goto argsdone
if /i "%~1"=="/persist" goto opt_persist
if /i "%~1"=="-persist" goto opt_persist
if /i "%~1"=="/remove" goto opt_remove
if /i "%~1"=="-remove" goto opt_remove
if /i "%~1"=="/?" goto usage
if /i "%~1"=="-h" goto usage
if /i "%~1"=="--help" goto usage
set "_SURUGA_SGDK=%~f1"
shift
goto args
:opt_persist
set "_SURUGA_OPT=-Persist"
shift
goto args
:opt_remove
set "_SURUGA_OPT=-Remove"
shift
goto args

:argsdone
if not defined _SURUGA_SGDK goto nosgdk
if "%_SURUGA_SGDK:~-1%"=="\" set "_SURUGA_SGDK=%_SURUGA_SGDK:~0,-1%"
if exist "%_SURUGA_SGDK%\" goto nosgdk
echo setup.bat: IsuSDK folder not found: %_SURUGA_SGDK%
goto cleanup_error
:nosgdk

rem Take out what an earlier run added, so that running this twice does not grow PATH
call set "PATH=%%PATH:%SURUGACC_HOME%;%SURUGACC_HOME%\bin;=%%"
if /i "%_SURUGA_OPT%"=="-Remove" goto remove

set "PATH=%SURUGACC_HOME%;%SURUGACC_HOME%\bin;%PATH%"
set "SURUGACC=%SURUGACC_HOME%\surugacc.exe"
if not defined _SURUGA_SGDK goto runps
set "GDK=%_SURUGA_SGDK%"
set "ISDK=%_SURUGA_SGDK%"
set "SGDK=%_SURUGA_SGDK%"

:runps
if defined _SURUGA_SGDK goto runps_sgdk
powershell -NoProfile -ExecutionPolicy Bypass -File "%SURUGACC_HOME%\setup.ps1" -FromBatch %_SURUGA_OPT%
goto cleanup
:runps_sgdk
powershell -NoProfile -ExecutionPolicy Bypass -File "%SURUGACC_HOME%\setup.ps1" -FromBatch %_SURUGA_OPT% -Sgdk "%_SURUGA_SGDK%"
goto cleanup

:remove
set "SURUGACC="
powershell -NoProfile -ExecutionPolicy Bypass -File "%SURUGACC_HOME%\setup.ps1" -FromBatch -Remove
set "SURUGACC_HOME="
goto cleanup

:usage
echo usage: setup.bat [IsuSDK folder] [/persist]
echo        setup.bat /remove
goto cleanup

:cleanup_error
set "_SURUGA_SGDK="
set "_SURUGA_OPT="
exit /b 1

:cleanup
set "_SURUGA_SGDK="
set "_SURUGA_OPT="
:exit /b 0
cmd /k