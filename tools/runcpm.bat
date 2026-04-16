@echo off
REM RunCPM wrapper for `make run ENV=cpm|lsx` on Windows.
REM
REM Usage: tools\runcpm.bat <path\to\program.com>
REM
REM Mirrors tools/runcpm.sh. Sets up a staging directory under
REM %TEMP%, places the target .COM under A\0\<NAME>.COM, writes
REM AUTOEXEC.TXT, launches tools\runcpm\RunCPM-win-x64.exe, pipes
REM EXIT into stdin so RunCPM shuts down after the program returns,
REM and filters RunCPM's boot banner from stdout via findstr.

setlocal enabledelayedexpansion

if "%~1"=="" (
    echo Usage: %~nx0 ^<path\to\program.com^> 1^>^&2
    exit /b 1
)

set COM_PATH=%~1
if not exist "%COM_PATH%" (
    echo Error: COM file not found: %COM_PATH% 1^>^&2
    exit /b 1
)

set SCRIPT_DIR=%~dp0
set RUNCPM_BIN=%SCRIPT_DIR%runcpm\RunCPM-win-x64.exe

if not exist "%RUNCPM_BIN%" (
    echo Error: RunCPM binary not found: %RUNCPM_BIN% 1^>^&2
    echo See tools\runcpm\README.md for build instructions. 1^>^&2
    exit /b 1
)

REM Staging directory (unique per invocation)
set STAGE=%TEMP%\slang-runcpm-%RANDOM%-%TIME:~6,2%%TIME:~9,2%
mkdir "%STAGE%\A\0" >nul 2>&1

REM Derive the stem (no extension) of the .COM. CP/M and NTFS both
REM treat file names case-insensitively, so we leave it as-is.
for %%F in ("%COM_PATH%") do set BASE=%%~nF

REM Copy as A\0\<STEM>.COM (CP/M requires .COM extension)
copy /Y "%COM_PATH%" "%STAGE%\A\0\%BASE%.COM" >nul

REM AUTOEXEC.TXT: stem only
echo %BASE%> "%STAGE%\AUTOEXEC.TXT"

REM Run from the staging dir. Pipe EXIT so CCP terminates after the
REM program returns, and filter boot banner with findstr.
pushd "%STAGE%"
echo EXIT | "%RUNCPM_BIN%" 2>&1 | findstr /V /C:"by Marcelo" /C:"Built " /C:"CPU is " /C:"T-states " /C:"clock speed" /C:"BIOS at " /C:"BIOS/BDOS" /C:"CCP CCP" /C:"FILEBASE " /C:"CP/M Emulator" /C:"Terminating" /C:"CPU Halted" /C:"RunCPM Version" /C:"----------" /C:"A0>EXIT"
popd

rmdir /S /Q "%STAGE%" >nul 2>&1

endlocal
exit /b 0
