@echo off
rem build with Wine: wine cmd /c build.bat
setlocal enabledelayedexpansion enableextensions
mkdir out
set MDSDRVDIR=C:\Users\hiro_\working\SGDK_work\MDSDRV\
set MDSDATA=
set OUTDIR=out
for %%x in (data\bgm\*.mml,data\bgm\*.mds,data\se\*.mml,data\se\*.mds) do set MDSDATA=!MDSDATA! "%%x"
echo Found files: %MDSDATA%
echo Make mdsseq.bin and mdspcm.bin ...
%MDSDRVDIR%tools\mdslink.exe -o "%OUTDIR%/mdsseq.bin" "%OUTDIR%/mdspcm.bin" -i "%OUTDIR%/mdsseq.inc" -h "%OUTDIR%/mdsseq.h" %MDSDATA% > %OUTDIR%\error.txt
if %ERRORLEVEL% neq 0 goto error
echo Make mdssub.bin ...
%MDSDRVDIR%tools\sjasmplus.exe %MDSDRVDIR%src\mdssub.z80 --raw=%OUTDIR%\mdssub.bin --lst=%OUTDIR%\mdssub.lst >%OUTDIR%\error.txt
if %ERRORLEVEL% neq 0 goto error
echo Make mdssub.zx0 ...
%MDSDRVDIR%tools\salvador.exe %OUTDIR%\mdssub.bin %OUTDIR%\mdssub.zx0
if %ERRORLEVEL% neq 0 goto error
echo Make main.bin ...
%MDSDRVDIR%tools\asm68k.exe /k /p /o ae- src\main.68k, main.bin >%OUTDIR%\error.txt, , %OUTDIR%\main.lst
if %ERRORLEVEL% neq 0 goto error
echo Make mdsdrv.bin ...
%MDSDRVDIR%tools\asm68k.exe /k /p /o ae- %MDSDRVDIR%src\blob.68k, %OUTDIR%\mdsdrv.bin >%OUTDIR%\error.txt, , %OUTDIR%\mdsdrv.lst
if %ERRORLEVEL% neq 0 goto error
if "%GDK_WIN%"=="" goto nosgdk
:error
pause
