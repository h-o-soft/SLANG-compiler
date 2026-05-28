@echo off
REM SLANG Compiler uninstaller (Windows). install.bat --uninstall to a thin shim.
REM call (instead of plain invocation) preserves quoted argument forwarding for
REM whitespace-containing paths (e.g. --prefix "C:\Temp\slang test").
call "%~dp0install.bat" --uninstall %*
