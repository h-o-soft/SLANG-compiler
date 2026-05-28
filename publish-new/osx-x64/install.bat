@echo off
REM SLANG Compiler installer (Windows, cmd.exe).
REM
REM Usage: install.bat [options]
REM   --prefix <path>      Install bin to <path>      (default: %USERPROFILE%\.local\bin)
REM   --config-dir <path>  Install lib to <path>      (default: %USERPROFILE%\.config\SLANG)
REM   --dry-run            Show actions without executing
REM   --verbose            Print each step to stderr
REM   --force              Skip overwrite confirmation (= for non-interactive use)
REM   --uninstall          Switch to uninstall mode
REM   --help               Show this usage
REM
REM `make install` calls this script with --force as a thin wrapper.
REM Make-based invocation also skips the uninstall confirmation prompt.

setlocal EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"

REM ---- arg parse ----
set "ACTION=install"
set "PREFIX="
set "CONFIG_DIR="
set "DRY_RUN=0"
set "VERBOSE=0"
set "FORCE=0"

:parse
if "%~1"=="" goto parsed
if /I "%~1"=="--prefix" (
  if "%~2"=="" ( echo Error: --prefix requires a non-empty value 1>&2 & exit /b 2 )
  set "PREFIX=%~2" & shift & shift & goto parse
)
if /I "%~1"=="--config-dir" (
  if "%~2"=="" ( echo Error: --config-dir requires a non-empty value 1>&2 & exit /b 2 )
  set "CONFIG_DIR=%~2" & shift & shift & goto parse
)
if /I "%~1"=="--dry-run"    ( set "DRY_RUN=1" & shift & goto parse )
if /I "%~1"=="--verbose"    ( set "VERBOSE=1" & shift & goto parse )
if /I "%~1"=="-v"           ( set "VERBOSE=1" & shift & goto parse )
if /I "%~1"=="--force"      ( set "FORCE=1" & shift & goto parse )
if /I "%~1"=="-f"           ( set "FORCE=1" & shift & goto parse )
if /I "%~1"=="--uninstall"  ( set "ACTION=uninstall" & shift & goto parse )
if /I "%~1"=="--help"       ( call :usage & exit /b 0 )
if /I "%~1"=="-h"           ( call :usage & exit /b 0 )
echo Unknown option: %~1 1>&2
call :usage 1>&2
exit /b 2
:parsed

if "%PREFIX%"==""     set "PREFIX=%USERPROFILE%\.local\bin"
if "%CONFIG_DIR%"=="" set "CONFIG_DIR=%USERPROFILE%\.config\SLANG"
set "BINDIR=%PREFIX%"

if /I "%ACTION%"=="uninstall" goto do_uninstall

REM ============================================================
REM   do_install
REM ============================================================
REM Run sanity check only on the install path. (uninstall does not need
REM to be run from an extracted distribution dir.)
for %%F in (bin\slangc.exe bin\slangbuild.exe include runtime images tools) do (
  if not exist "%%F" (
    echo Error: '%%F' not found in %SCRIPT_DIR%. 1>&2
    echo. 1>&2
    echo This installer must be run from an extracted distribution directory. 1>&2
    echo For repo development, run 'make publish-local' first to populate bin\. 1>&2
    exit /b 1
  )
)

echo Installing SLANG to:
echo   Binaries:  %BINDIR%
echo   Libraries: %CONFIG_DIR%

if "%FORCE%"=="0" (
  if exist "%BINDIR%\slangc.exe" (
    set /p ANS="Existing installation found. Overwrite? [y/N]: "
    if /I not "!ANS!"=="y" if /I not "!ANS!"=="yes" ( echo Aborted. & exit /b 1 )
  )
)

if "%DRY_RUN%"=="1" (
  echo DRY: mkdir %BINDIR%
  echo DRY: mkdir %CONFIG_DIR%
  echo DRY: copy bin\slangc.exe -^> %BINDIR%\
  echo DRY: copy bin\slangbuild.exe -^> %BINDIR%\
  for %%D in (include runtime images tools) do (
    echo DRY: rd /s /q %CONFIG_DIR%\%%D
    echo DRY: xcopy /E /Y /I %%D -^> %CONFIG_DIR%\%%D
  )
  exit /b 0
)

if not exist "%BINDIR%"     mkdir "%BINDIR%"
if not exist "%CONFIG_DIR%" mkdir "%CONFIG_DIR%"

if "%VERBOSE%"=="1" echo   copy bin\slangc.exe -^> %BINDIR%\ 1>&2
copy /Y bin\slangc.exe     "%BINDIR%\" >nul
if errorlevel 1 ( echo Error: failed to copy slangc.exe 1>&2 & exit /b 1 )
if "%VERBOSE%"=="1" echo   copy bin\slangbuild.exe -^> %BINDIR%\ 1>&2
copy /Y bin\slangbuild.exe "%BINDIR%\" >nul
if errorlevel 1 ( echo Error: failed to copy slangbuild.exe 1>&2 & exit /b 1 )

REM Ghost-file safety: remove the existing subdir with rd /s /q before xcopy /E /Y /I.
for %%D in (include runtime images tools) do (
  if "%VERBOSE%"=="1" echo   replace %CONFIG_DIR%\%%D 1>&2
  if exist "%CONFIG_DIR%\%%D" rd /s /q "%CONFIG_DIR%\%%D"
  xcopy /E /Y /I "%%D" "%CONFIG_DIR%\%%D" >nul
  if errorlevel 1 ( echo Error: failed to copy %%D 1>&2 & exit /b 1 )
)

echo Installation complete!
echo.
echo Note: please ensure '%BINDIR%' is in your PATH.
exit /b 0

REM ============================================================
REM   do_uninstall
REM ============================================================
:do_uninstall
echo Uninstalling SLANG from:
echo   Binaries:  %BINDIR%\slangc.exe, slangbuild.exe
echo   Libraries: %CONFIG_DIR% (entire directory)

REM Dangerous-path guard:
REM (1) Resolve to a full path so D:\. or D:\foo\.. normalize first.
REM     for %%I in (...) do set "P=%%~fI" expands to an absolute path
REM     (analogous to cd "$p" && pwd on Unix).
REM (2) Strip trailing backslash (treat D:\ and D: as the same).
REM (3) Refuse any drive-root pattern (?:), then apply the existing guard.
for %%I in ("%CONFIG_DIR%") do set "P=%%~fI"
if "%P:~-1%"=="\" set "P=%P:~0,-1%"
if "%P%"==""                    goto :err_path
REM Refuse any drive root (X:) -- C:\ / D:\ / D:\. / D:\foo\.. all rejected
REM (the full-path step above already resolves D:\. to D:\ -> D:).
if "%P:~1,1%"==":" if "%P:~2%"=="" goto :err_path
REM Refuse parent / system dirs.
if /I "%P%"=="%SYSTEMDRIVE%"    goto :err_path
if /I "%P%"=="%USERPROFILE%"    goto :err_path
if /I "%P%"=="%LOCALAPPDATA%"   goto :err_path
if /I "%P%"=="%APPDATA%"        goto :err_path
if /I "%P%"=="%PROGRAMFILES%"   goto :err_path
if /I "%P%"=="%PROGRAMFILES(X86)%" goto :err_path
if /I "%P%"=="%SYSTEMROOT%"     goto :err_path
if /I "%P%"=="%WINDIR%"         goto :err_path
REM Refuse default-parent dirs (typo without trailing \SLANG would wipe other
REM apps' settings). Example: --config-dir "%USERPROFILE%\.config" without
REM \SLANG would remove all of ~/.config (git, vim, etc.).
if /I "%P%"=="%USERPROFILE%\.config" goto :err_path
if /I "%P%"=="%USERPROFILE%\.local"  goto :err_path

if "%FORCE%"=="0" (
  set /p ANS="Continue? [y/N]: "
  if /I not "!ANS!"=="y" if /I not "!ANS!"=="yes" ( echo Aborted. & exit /b 1 )
)

if "%DRY_RUN%"=="1" (
  echo DRY: del %BINDIR%\slangc.exe %BINDIR%\slangbuild.exe
  echo DRY: rd /s /q %P%
  exit /b 0
)

if exist "%BINDIR%\slangc.exe"     del /f /q "%BINDIR%\slangc.exe"     >nul 2>nul
if exist "%BINDIR%\slangbuild.exe" del /f /q "%BINDIR%\slangbuild.exe" >nul 2>nul
if exist "%P%"                     rd  /s /q "%P%"

echo Uninstallation complete!
exit /b 0

:err_path
echo Refusing to remove dangerous path: '%P%' (from '%CONFIG_DIR%') 1>&2
exit /b 1

:usage
echo SLANG Compiler installer (Windows, cmd.exe).
echo.
echo Usage: install.bat [options]
echo   --prefix ^<path^>      Install bin to ^<path^>      (default: %%USERPROFILE%%\.local\bin)
echo   --config-dir ^<path^>  Install lib to ^<path^>      (default: %%USERPROFILE%%\.config\SLANG)
echo   --dry-run            Show actions without executing
echo   --verbose            Print each step to stderr
echo   --force              Skip overwrite confirmation
echo   --uninstall          Switch to uninstall mode
echo   --help               Show this usage
goto :eof
