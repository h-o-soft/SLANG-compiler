#!/usr/bin/env bash
# hubasic2sl をブラウザ (X1Pen など) で動かすための一式を 1 つのフォルダに作る。
#
#   scripts/build-hubasic-wasm.sh <出力フォルダ>
#
# できるもの:
#   _framework/     src/SLANGCompiler.HuBasic.Wasm を browser-wasm で publish したもの
#   runtime/        x1native と x1native_slfs の env の libraries に挙がる .asm (LSX 用は入れない)
#   env/            x1native.env と x1native_slfs.env の写し
#   manifest.json   組み立てた SHA と、env ごとのコンパイラ・アセンブラに渡す値 (docs/HUBASIC.md)
#
# 最後に node (NODE で指せる) があれば scripts/hubasic-wasm-smoke.mjs で、wasm の変換が CLI と同じかを確かめる。
# wasm とランタイムは同じ SHA の組で使う。manifest の dirty が true の一式は取り込まない。
set -euo pipefail

if [ $# -ne 1 ]; then
  echo "使い方: $0 <出力フォルダ>" >&2
  exit 2
fi
REPO="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$1"
if [ -e "$OUT" ] && [ -n "$(ls -A "$OUT")" ]; then
  echo "build-hubasic-wasm: $OUT が空ではありません (消すか、別のフォルダを指定してください)" >&2
  exit 2
fi

# SHA と dirty は何かを作る前に読む (出力フォルダがリポジトリの中でも dirty にならないように)
SHA="$(git -C "$REPO" rev-parse HEAD)"
if [ -n "$(git -C "$REPO" status --porcelain)" ]; then DIRTY=true; else DIRTY=false; fi
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$REPO/src/SLANGCompiler.HuBasic/SLANGCompiler.HuBasic.csproj" | head -1)"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

echo "[1/3] wasm を組み立てます"
dotnet publish "$REPO/src/SLANGCompiler.HuBasic.Wasm/SLANGCompiler.HuBasic.Wasm.csproj" -c Release -o "$TMP/publish" --nologo -v q
mkdir -p "$OUT"
cp -R "$TMP/publish/wwwroot/_framework" "$OUT/_framework"

echo "[2/3] ランタイムと manifest を写します"
python3 -I "$REPO/scripts/hubasic-wasm-manifest.py" "$REPO" "$SHA" "$DIRTY" "$VERSION" "$OUT" > "$OUT/manifest.json"

echo "[3/3] wasm の変換を CLI と比べます"
NODE="${NODE:-node}"
if ! command -v "$NODE" > /dev/null 2>&1; then
  echo "build-hubasic-wasm: node が無いので、確かめませんでした (NODE で指せます)"
else
  dotnet build "$REPO/src/SLANGCompiler.HuBasic/SLANGCompiler.HuBasic.csproj" -c Release --nologo -v q
  "$NODE" "$REPO/scripts/hubasic-wasm-smoke.mjs" "$OUT" "$REPO" \
    "$REPO/src/SLANGCompiler.HuBasic/bin/Release/net8.0/hubasic2sl.dll"
fi

echo "build-hubasic-wasm: $OUT にできました (SHA $SHA、dirty $DIRTY)"
