#!/bin/sh
# build_driver.sh - banjo X1 driver bin を固定 ORG でビルドする。
#
# banjo_driver_x1.asm は upstream/ への相対 include を持つので、 必ず本スクリプトのある
# runtime/x1/banjo/ を cwd にして wla-z80 を起動する (= どこから呼んでも自身の dir へ cd)。
# driver は曲を含まない、 chip 選択式・固定アドレスの共有 bin。 ABI 変換 + jump table 込み。
#
# 出力: <outdir>/driver.bin (trim 済) + <outdir>/driver.sym (EQU 抽出に使う)
#
# 使い方:
#   build_driver.sh --chip opm|ay|both [--org 0x8000] [--ram 0xC000] \
#                   [--max-channels N] --outdir <dir>
set -eu

CHIP=""
ORG=0x8000
RAM=0xC000
MAXCH=""
OUTDIR=""
CALLER_DIR="$(pwd)"

while [ $# -gt 0 ]; do
    case "$1" in
        --chip)          CHIP="$2"; shift 2 ;;
        --org)           ORG="$2"; shift 2 ;;
        --ram)           RAM="$2"; shift 2 ;;
        --max-channels)  MAXCH="$2"; shift 2 ;;
        --outdir)        OUTDIR="$2"; shift 2 ;;
        *) echo "build_driver.sh: unknown arg '$1'" >&2; exit 1 ;;
    esac
done

[ -n "$CHIP" ]   || { echo "build_driver.sh: --chip required (opm|ay|both)" >&2; exit 1; }
[ -n "$OUTDIR" ] || { echo "build_driver.sh: --outdir required" >&2; exit 1; }

# chip 選択 -> wla -D フラグ + 既定 channel 数
case "$CHIP" in
    opm)  CHIPDEF="-D BANJO_USE_OPM";                                  DEFCH=8  ;;
    ay)   CHIPDEF="-D BANJO_USE_AY -D BANJO_3_57MHZ";                  DEFCH=3  ;;
    both) CHIPDEF="-D BANJO_USE_OPM -D BANJO_USE_AY -D BANJO_3_57MHZ"; DEFCH=11 ;;
    *) echo "build_driver.sh: --chip must be opm|ay|both (got '$CHIP')" >&2; exit 1 ;;
esac
[ -n "$MAXCH" ] || MAXCH="$DEFCH"

# OUTDIR を呼び出し元 cwd 基準で絶対パス化してから、自身の dir へ移動する。
# 先に cd すると sample Makefile の `--outdir .` が runtime/x1/banjo/ を指してしまう。
case "$OUTDIR" in
    /*) : ;;
    *)  OUTDIR="$CALLER_DIR/$OUTDIR" ;;
esac
mkdir -p "$OUTDIR"
OUTDIR="$(cd "$OUTDIR" && pwd)"

# 自身の dir へ移動 (= upstream/ 相対 include を解決)
cd "$(dirname "$0")"
HERE="$(pwd)"
TOOLS="$HERE/../../../tools"

WLA="${WLA:-wla-z80}"
WLALINK="${WLALINK:-wlalink}"
PYTHON="${PYTHON:-python3}"

echo "build_driver: chip=$CHIP org=$ORG ram=$RAM max_channels=$MAXCH -> $OUTDIR/driver.bin"

# 1. assemble (cwd=banjo dir)
$WLA $CHIPDEF -D BANJO_ORG="$ORG" -D BANJO_RAM_BASE="$RAM" -D BANJO_MAX_CHANNELS="$MAXCH" \
    -o "$OUTDIR/driver.o" banjo_driver_x1.asm

# 2. link (RAMSECTION を SLOT 1 = BANJO_RAM_BASE に固定。 inline ORG だと SLOT0 自壊するため)
cat > "$OUTDIR/driver_link.txt" <<EOF
[objects]
$OUTDIR/driver.o

[ramsections]
bank 0 slot 1 "BANJO_X1_RAM"
bank 0 slot 1 "BANJO_RAM"
EOF
$WLALINK -s -S -r "$OUTDIR/driver_link.txt" "$OUTDIR/driver.rom"

# 3. trim (full-bank -> 実使用末尾)。 sym は EQU 抽出のため残す。
$PYTHON "$TOOLS/banjo_trim_to_end.py" "$OUTDIR/driver.rom" "$OUTDIR/driver.sym" \
    "$OUTDIR/driver.bin" --load "$ORG" --ram-base "$RAM"

echo "build_driver: done -> $OUTDIR/driver.bin (+ driver.sym)"
