#!/bin/sh
# SLANG Compiler uninstaller (Unix). install.sh --uninstall への薄い shim。
exec "$(dirname "$0")/install.sh" --uninstall "$@"
