#!/usr/bin/env bash
# hubasic2sl をブラウザ (X1Pen など) で動かすための一式 (束) を 1 つのフォルダに作る。
#
#   scripts/build-hubasic-wasm.sh <出力フォルダ>
#
# できるもの:
#   _framework/     src/SLANGCompiler.HuBasic.Wasm を browser-wasm で publish したもの
#   runtime/        x1native と x1native_slfs の env の libraries に挙がる .asm (LSX 用は入れない)
#   env/            x1native.env と x1native_slfs.env の写し
#   testdata/       試験用のプログラム 4 本の .BAS・.SL・.bin・.LST・.d88 (この木の hubasic2sl・slangbuild・slangc で作る)
#   manifest.json   組み立てた SHA、env ごとの値、アセンブラ、testdata、全部のファイルの sha256 (docs/HUBASIC.md)
#
# 要るもの: dotnet SDK、python3、AILZ80ASM (AILZ80ASM_PATH・PATH・この木の tools/ の順に探す)。
# node 22.7 以上 (NODE で指せる) があれば、最後に wasm の変換が CLI と同じかを確かめる。
# wasm とランタイムは同じ SHA の組で使う。manifest の dirty が true の束は取り込まない。
set -euo pipefail

if [ $# -ne 1 ]; then
  echo "使い方: $0 <出力フォルダ>" >&2
  exit 2
fi
REPO="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$1"
OUT="$(cd "$1" && pwd)"
if [ -n "$(ls -A "$OUT")" ]; then
  echo "build-hubasic-wasm: $OUT が空ではありません (消すか、別のフォルダを指定してください)" >&2
  exit 2
fi
# slangc は今の作業フォルダの runtime/ を先に探すので、この木の根から動かす
cd "$REPO"

# SHA と dirty は何かを作る前に読む (出力フォルダがリポジトリの中でも dirty にならないように)
SHA="$(git rev-parse HEAD)"
if [ -n "$(git status --porcelain)" ]; then DIRTY=true; else DIRTY=false; fi
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' src/SLANGCompiler.HuBasic/SLANGCompiler.HuBasic.csproj | head -1)"

# 組み立て直した slangc・slangbuild (と AILZ80ASM) は apphost なので、dotnet の場所を伝える
export DOTNET_ROOT="${DOTNET_ROOT:-$(dirname "$(readlink -f "$(command -v dotnet)")")}"

# アセンブラは 1 つに決めて、testdata のすべての組み立てに渡す
if [ -n "${AILZ80ASM_PATH:-}" ]; then
  ASM="$AILZ80ASM_PATH"
elif command -v AILZ80ASM > /dev/null 2>&1; then
  ASM="$(command -v AILZ80ASM)"
else
  ASM="$REPO/tools/AILZ80ASM"
fi
if [ ! -x "$ASM" ] || ! ASM_VERSION="$("$ASM" --version 2>/dev/null | head -1)"; then
  echo "build-hubasic-wasm: AILZ80ASM が見つからないか動きません ($ASM)。AILZ80ASM_PATH で指すか、PATH に置いてください" >&2
  exit 1
fi

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

echo "[1/5] CLI (hubasic2sl・slangc・slangbuild) を組み立てます"
for p in SLANGCompiler.HuBasic SLANGCompiler.CLI SLANGCompiler.Build; do
  dotnet build "src/$p/$p.csproj" -c Release --nologo -v q > "$TMP/build-$p.log" 2>&1 \
    || { cat "$TMP/build-$p.log" >&2; exit 1; }
done
HUB="$REPO/src/SLANGCompiler.HuBasic/bin/Release/net8.0/hubasic2sl.dll"
SLANGC="$REPO/src/SLANGCompiler.CLI/bin/Release/net8.0/slangc"
SLANGBUILD="$REPO/src/SLANGCompiler.Build/bin/Release/net8.0/slangbuild"

echo "[2/5] wasm を組み立てます"
dotnet publish src/SLANGCompiler.HuBasic.Wasm/SLANGCompiler.HuBasic.Wasm.csproj -c Release -o "$TMP/publish" --nologo -v q > "$TMP/publish.log" 2>&1 \
  || { cat "$TMP/publish.log" >&2; exit 1; }
cp -R "$TMP/publish/wwwroot/_framework" "$OUT/_framework"

echo "[3/5] 試験用のプログラムを作ります (AILZ80ASM $ASM_VERSION)"
mkdir "$OUT/testdata"
CASES=""
for bas in examples/hubasic/HELLO.BAS examples/hubasic/GRAPH.BAS examples/hubasic/MUSIC.BAS examples/hubasic/FLOAT.BAS; do
  name="$(basename "$bas" .BAS)"
  base="$OUT/testdata/$name"
  cp "$bas" "$base.BAS"
  dotnet "$HUB" "$base.BAS" -o "$base.SL"
  "$SLANGBUILD" --emit disk -E x1native_slfs "$base.SL" -o "$base" --slangc "$SLANGC" --asm "$ASM" > "$TMP/$name.log" 2>&1 \
    || { cat "$TMP/$name.log" >&2; exit 1; }
  dotnet "$HUB" --check-bin "$base.bin" --lst "$base.LST"
  CASES="$CASES${CASES:+,}$name"
done

echo "[4/5] ランタイムと manifest を作り、束を確かめます"
python3 -I scripts/hubasic-wasm-manifest.py copy "$REPO" "$OUT"
python3 -I scripts/hubasic-wasm-manifest.py write "$REPO" "$OUT" "$SHA" "$DIRTY" "$VERSION" "$ASM" "$ASM_VERSION" "$CASES"
python3 -I scripts/hubasic-wasm-manifest.py verify "$OUT"

echo "[5/5] wasm の変換を CLI と比べます"
NODE="${NODE:-node}"
if ! command -v "$NODE" > /dev/null 2>&1; then
  echo "build-hubasic-wasm: node が無いので、確かめませんでした (NODE で指せます)"
else
  "$NODE" scripts/hubasic-wasm-smoke.mjs "$OUT" "$REPO" "$HUB"
fi

echo "build-hubasic-wasm: $OUT にできました (SHA $SHA、dirty $DIRTY)"
