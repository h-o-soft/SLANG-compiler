#!/bin/sh
# RunCPM wrapper for `make run ENV=cpm|lsx`.
#
# Usage: tools/runcpm.sh <path/to/program.com>
#
# Sets up a staging directory under $TMPDIR (or /tmp) with the CP/M
# drive-A user-0 layout that RunCPM requires, writes an AUTOEXEC.TXT
# that runs the program, launches the platform's pre-built RunCPM
# binary from tools/runcpm/, pipes EXIT into stdin so RunCPM shuts
# down after the program returns, and filters RunCPM's boot banner
# from stdout.
#
# Requires: tools/runcpm/RunCPM-<platform> (placed by the repo; see
# tools/runcpm/README.md).

set -e

if [ $# -lt 1 ]; then
    echo "Usage: $0 <path/to/program.com>" >&2
    exit 1
fi

COM_PATH="$1"
if [ ! -f "$COM_PATH" ]; then
    echo "Error: COM file not found: $COM_PATH" >&2
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
RUNCPM_DIR="$SCRIPT_DIR/runcpm"

# Detect platform and pick the right binary
UNAME_S="$(uname -s)"
UNAME_M="$(uname -m)"
case "$UNAME_S" in
    Darwin)
        case "$UNAME_M" in
            arm64)   RUNCPM_BIN="$RUNCPM_DIR/RunCPM-macos-arm64" ;;
            x86_64)  RUNCPM_BIN="$RUNCPM_DIR/RunCPM-macos-x64" ;;
            *)       echo "Unsupported macOS arch: $UNAME_M" >&2; exit 1 ;;
        esac
        ;;
    Linux)
        case "$UNAME_M" in
            x86_64)  RUNCPM_BIN="$RUNCPM_DIR/RunCPM-linux-x64" ;;
            *)       echo "Unsupported Linux arch: $UNAME_M" >&2; exit 1 ;;
        esac
        ;;
    *)
        echo "Unsupported platform: $UNAME_S" >&2
        exit 1
        ;;
esac

if [ ! -x "$RUNCPM_BIN" ]; then
    echo "Error: RunCPM binary not found or not executable: $RUNCPM_BIN" >&2
    echo "See tools/runcpm/README.md for build instructions." >&2
    exit 1
fi

# Staging directory (unique per invocation to avoid collisions)
STAGE="${TMPDIR:-/tmp}/slang-runcpm-$$"
mkdir -p "$STAGE/A/0"
trap 'rm -rf "$STAGE"' EXIT INT TERM

# Copy the binary under A/0/<STEM>.COM (CP/M requires .COM extension).
# Input may be .bin (raw AILZ80ASM output) or .com; we always normalize to .COM.
BASE="$(basename "$COM_PATH")"
STEM="$(echo "${BASE%.*}" | tr '[:lower:]' '[:upper:]')"
cp "$COM_PATH" "$STAGE/A/0/$STEM.COM"

# AUTOEXEC.TXT contains just the program's stem (no extension).
printf '%s\n' "$STEM" > "$STAGE/AUTOEXEC.TXT"

# Launch. Pipe `EXIT` so RunCPM's CCP terminates after the program
# returns control and drops back to the A0> prompt.
# Filter RunCPM's boot banner so only the program output is shown.
cd "$STAGE"
echo "EXIT" | "$RUNCPM_BIN" 2>&1 | tr -d '\r' | awk '
    /by Marcelo|Built |CPU is |T-states |clock speed|BIOS at |BIOS\/BDOS |CCP CCP|FILEBASE |CP\/M Emulator|Terminating|CPU Halted|RunCPM Version/ { next }
    /^-+$/ { next }
    /^A0>EXIT/ { next }
    { sub(/\x1b\[[0-9]*[JH]/, ""); sub(/\x1b\[[0-9]*[JH]/, ""); print }
'
