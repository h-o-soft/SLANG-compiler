#!/bin/sh
# Add overlay binaries to a d88 disk image as M0.BIN, M1.BIN, ...
#
# Usage: disk-add-overlays.sh <ndc> <d88> <target-prefix-dir>
#
# Looks for <target-prefix-dir>/PROG._m*.bin (output from slangbuild) and
# stages each as M<N>.BIN under <target-prefix-dir>/.staging/, then writes
# them to the d88 via NDC P. Old M0..M9.BIN are deleted from the d88 first
# (sample limited: assumes overlay count <= 10).
#
# This is a *sample-only* helper for examples/MODTEST_RESIDENT.SL etc.
# Windows users have an equivalent at tools/disk-add-overlays.bat.

set -e

if [ $# -lt 3 ]; then
    echo "Usage: $0 <ndc> <d88> <target-prefix-dir>" >&2
    exit 1
fi

NDC="$1"
D88="$2"
PREFIX_DIR="$3"
STAGE_DIR="$PREFIX_DIR/.staging"

# Cleanup staging dir on exit (success or failure)
trap 'rm -rf "$STAGE_DIR"' EXIT INT TERM

# Delete any leftover M0..M9.BIN from previous builds (sample-limited range).
# `|| true` swallows "file not found" non-zero exits from NDC D.
n=0
while [ $n -lt 10 ]; do
    "$NDC" D "$D88" 0 "M${n}.BIN" >/dev/null 2>&1 || true
    n=$((n + 1))
done

# Stage and add overlay files (none → no-op, this is the common case for
# samples that don't use #MODULE).
mkdir -p "$STAGE_DIR"
for f in "$PREFIX_DIR"PROG._m*.bin; do
    [ -e "$f" ] || continue
    n=$(echo "$f" | sed 's/.*_m\([0-9][0-9]*\)\.bin/\1/')
    cp "$f" "$STAGE_DIR/M${n}.BIN"
    "$NDC" P "$D88" 0 "$STAGE_DIR/M${n}.BIN"
done
