#!/bin/sh
# 旧コンパイラでビルドしてS-OSエミュレータで実行
# Usage: ./run_old.sh apps/BILLIARD

if [ $# -eq 0 ]; then
  echo "Usage: ./run_old.sh TARGET"
  echo "  e.g. ./run_old.sh apps/BILLIARD"
  exit 1
fi

TARGET=$1
SRC=${TARGET}.SL
ASM=${TARGET}_OLD.ASM
BIN=${TARGET}_OLD.bin
DISK=images/SOSPROG.D88
EMU="wine $HOME/Emus/X1/x1.exe"

echo "=== Compile (old) ==="
bin/Release/net6.0/SLANGCompiler "$SRC" -E sos -O "$ASM" || exit 1

echo "=== Assemble ==="
AILZ80ASM "$ASM" -f -o "$BIN" -bin || exit 1

echo "=== Write to disk ==="
mono tools/HuDisk.exe "$DISK" -d PROG.bin 2>/dev/null
mv "$BIN" PROG.bin
mono tools/HuDisk.exe "$DISK" -a PROG.bin -r 3000 -g 3000 || exit 1

echo "=== Run ==="
$EMU "$DISK"
