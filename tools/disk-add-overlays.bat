@echo off
REM disk-add-overlays.bat — Windows companion to disk-add-overlays.sh
REM
REM Usage: disk-add-overlays.bat <ndc> <d88> <target-prefix-dir>
REM
REM Looks for <target-prefix-dir>PROG._m*.bin (output from slangbuild) and
REM stages each as M<N>.BIN under <target-prefix-dir>\.staging\, then writes
REM them to the d88 via NDC P. Old M0..M9.BIN are deleted from the d88 first
REM (sample limited: assumes overlay count <= 10).
REM
REM This is a *sample-only* helper for examples/MODTEST_RESIDENT.SL etc.

setlocal enabledelayedexpansion

if "%~3"=="" (
    echo Usage: %~nx0 ^<ndc^> ^<d88^> ^<target-prefix-dir^> 1>&2
    exit /b 1
)

set "NDC=%~1"
set "D88=%~2"
set "PREFIX_DIR=%~3"

REM Normalize forward slashes to backslashes (make passes "examples/")
set "PREFIX_DIR=%PREFIX_DIR:/=\%"
set "STAGE_DIR=%PREFIX_DIR%.staging"

REM Delete leftover M0..M9.BIN from previous builds (sample-limited range).
REM Errors (file not in d88) are suppressed.
for /L %%n in (0,1,9) do (
    "%NDC%" D "%D88%" 0 M%%n.BIN >nul 2>&1
)

REM Stage and add overlay files. No-op if no PROG._m*.bin exists (= sample
REM doesn't use #MODULE).
if not exist "%STAGE_DIR%" mkdir "%STAGE_DIR%"

for %%f in ("%PREFIX_DIR%PROG._m*.bin") do (
    if exist "%%f" (
        call :stage_one "%%f"
        if errorlevel 1 (
            rmdir /S /Q "%STAGE_DIR%" 2>nul
            exit /b 1
        )
    )
)

REM Cleanup staging dir
rmdir /S /Q "%STAGE_DIR%" 2>nul
exit /b 0

:stage_one
REM Stage and write a single overlay file. Args: %1 = source bin path.
REM Returns 0 on success, 1 if copy or NDC P fails.
set "BASE=%~n1"
REM Extract digits after "_m" — "PROG._m0" → "0"
set "N=!BASE:*_m=!"
copy /Y "%~1" "%STAGE_DIR%\M!N!.BIN" >nul
if errorlevel 1 (
    echo Error: copy failed for %~1 1>&2
    exit /b 1
)
"%NDC%" P "%D88%" 0 "%STAGE_DIR%\M!N!.BIN"
if errorlevel 1 (
    echo Error: NDC P failed for M!N!.BIN 1>&2
    exit /b 1
)
exit /b 0
