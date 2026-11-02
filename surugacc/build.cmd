@echo off
rem Build an SGDK project with SurugaCC (runs surugacc\build.ps1 with PowerShell 7.4 or later).
rem   surugacc\build.cmd [project folder] [-g] [-O 0..3] [-j N] [-LibOnly]
where pwsh >nul 2>nul
if errorlevel 1 goto nopwsh
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
exit /b %errorlevel%
:nopwsh
echo build.cmd: PowerShell 7.4 or later (pwsh) is required: https://aka.ms/powershell 1>&2
exit /b 1
