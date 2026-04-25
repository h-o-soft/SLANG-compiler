#!/bin/sh
# RunCPM wrapper for `make run ENV=cpm|lsx`.
#
# Usage: tools/runcpm.sh <path/to/program.com>
#
# Sets up a staging directory under $TMPDIR (or /tmp) with the CP/M
# drive-A user-0 layout that RunCPM requires. AUTOEXEC.TXT runs
# `SUBMIT BOOT`, and BOOT.SUB contains two lines: the program name and
# `EXIT`. SUBMIT.COM and EXIT.COM are bundled under tools/runcpm/cpm/.
# This way RunCPM auto-terminates after the program returns, without
# needing stdin redirection (which is unreliable on Windows).
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
CPM_UTILS_DIR="$RUNCPM_DIR/cpm"

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

if [ ! -f "$CPM_UTILS_DIR/EXIT.COM" ] || [ ! -f "$CPM_UTILS_DIR/SUBMIT.COM" ]; then
    echo "Error: EXIT.COM / SUBMIT.COM not found under $CPM_UTILS_DIR" >&2
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

# Stage overlay binaries (sample-only support, see examples/MODTEST_RESIDENT.SL).
# Looks for <com_dir>/<original-stem>._m*.bin (slangbuild output naming) and
# copies each as M0.BIN, M1.BIN, ... under A/0/. The stem here is the
# slangbuild -o stem (= basename of COM_PATH without extension), NOT the
# upper-cased COM stem. No-op when sample doesn't use #MODULE.
COM_DIR="$(cd "$(dirname "$COM_PATH")" && pwd)"
SRC_STEM="${BASE%.*}"
for f in "$COM_DIR/${SRC_STEM}._m"*.bin; do
    [ -e "$f" ] || continue
    n="$(echo "$f" | sed 's/.*_m\([0-9][0-9]*\)\.bin/\1/')"
    cp "$f" "$STAGE/A/0/M${n}.BIN"
done

# Bundle SUBMIT.COM and EXIT.COM so the CCP can chain commands.
cp "$CPM_UTILS_DIR/SUBMIT.COM" "$STAGE/A/0/SUBMIT.COM"
cp "$CPM_UTILS_DIR/EXIT.COM"   "$STAGE/A/0/EXIT.COM"

# BOOT.SUB drives the CCP: run the program, then EXIT. CP/M text files
# use CR LF line endings.
printf '%s\r\nEXIT\r\n' "$STEM" > "$STAGE/A/0/BOOT.SUB"

# AUTOEXEC.TXT kicks off SUBMIT, which expands BOOT.SUB into $$$.SUB
# for the CCP to read on subsequent loop iterations.
printf 'SUBMIT BOOT\n' > "$STAGE/AUTOEXEC.TXT"

# Filter RunCPM's boot banner so only the program output is shown.
cd "$STAGE"
"$RUNCPM_BIN" 2>&1 | tr -d '\r' | awk '
    /by Marcelo|Built |CPU is |T-states |clock speed|BIOS at |BIOS\/BDOS |CCP CCP|FILEBASE |CP\/M Emulator|Terminating|CPU Halted|RunCPM Version/ { next }
    /^-+$/ { next }
    /^A0[>$](SUBMIT BOOT|EXIT|[A-Z][A-Z0-9]*)$/ { next }
    { sub(/\x1b\[[0-9]*[JH]/, ""); sub(/\x1b\[[0-9]*[JH]/, ""); print }
'
