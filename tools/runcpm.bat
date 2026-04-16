@echo off
REM RunCPM wrapper for `make run ENV=cpm|lsx` on Windows.
REM
REM Usage: tools\runcpm.bat <path\to\program.com>
REM
REM Mirrors tools/runcpm.sh. AUTOEXEC.TXT runs `SUBMIT BOOT` and
REM BOOT.SUB drives the CCP through `<PROG>` then `EXIT`. SUBMIT.COM
REM and EXIT.COM are bundled under tools\runcpm\cpm\. This avoids
REM relying on stdin redirection, which is unreliable on Windows.

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
set CPM_UTILS_DIR=%SCRIPT_DIR%runcpm\cpm

if not exist "%RUNCPM_BIN%" (
    echo Error: RunCPM binary not found: %RUNCPM_BIN% 1^>^&2
    echo See tools\runcpm\README.md for build instructions. 1^>^&2
    exit /b 1
)

if not exist "%CPM_UTILS_DIR%\EXIT.COM" (
    echo Error: EXIT.COM not found under %CPM_UTILS_DIR% 1^>^&2
    exit /b 1
)
if not exist "%CPM_UTILS_DIR%\SUBMIT.COM" (
    echo Error: SUBMIT.COM not found under %CPM_UTILS_DIR% 1^>^&2
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

REM Bundle SUBMIT.COM and EXIT.COM so the CCP can chain commands.
copy /Y "%CPM_UTILS_DIR%\SUBMIT.COM" "%STAGE%\A\0\SUBMIT.COM" >nul
copy /Y "%CPM_UTILS_DIR%\EXIT.COM"   "%STAGE%\A\0\EXIT.COM"   >nul

REM BOOT.SUB drives the CCP: run the program, then EXIT. CP/M text
REM files use CR LF line endings, which is the Windows default for echo.
(
echo %BASE%
echo EXIT
)> "%STAGE%\A\0\BOOT.SUB"

REM AUTOEXEC.TXT kicks off SUBMIT, which expands BOOT.SUB into $$$.SUB
REM for the CCP to read on subsequent loop iterations.
echo SUBMIT BOOT> "%STAGE%\AUTOEXEC.TXT"

REM Filter RunCPM's boot banner with findstr /V.
pushd "%STAGE%"
"%RUNCPM_BIN%" 2>&1 | findstr /V /C:"by Marcelo" /C:"Built " /C:"CPU is " /C:"T-states " /C:"clock speed" /C:"BIOS at " /C:"BIOS/BDOS" /C:"CCP CCP" /C:"FILEBASE " /C:"CP/M Emulator" /C:"Terminating" /C:"CPU Halted" /C:"RunCPM Version" /C:"----------" /C:"A0>SUBMIT BOOT" /C:"A0$SUBMIT BOOT" /C:"A0$%BASE%" /C:"A0$EXIT" /C:"A0>EXIT"
popd

rmdir /S /Q "%STAGE%" >nul 2>&1

endlocal
exit /b 0
