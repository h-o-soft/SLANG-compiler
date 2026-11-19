#!/usr/bin/env bash
# hubasic2sl をブラウザ (X1Pen など) で動かすための一式 (束) を 1 つのフォルダに作る。
#
#   scripts/build-hubasic-wasm.sh <出力フォルダ>
#
# できるもの:
#   _framework/     src/SLANGCompiler.HuBasic.Wasm を browser-wasm で publish したもの
#   _framework/ には、hubasic2sl の変換 (HuBasicWeb) と、slangc のコンパイルと Hu-BASIC の形のディスク作り (SlangWeb) が入る
#   runtime/        x1native・x1native_slfs・x1native_hufs・x1 の env の libraries に挙がる .asm
#   env/            その 4 つの env の写し (ファイルの命令を使うプログラムは x1native_hufs。その env の disk.tool は hufs-pack
#                   (Hu-BASIC と同じ形のディスク。manifest の diskTool)。x1 は X1Pen の SLANG タブ用。ディスクの型は入れない)
#   include/        x1 の env の #INCLUDE で使う X1 向けの .LIB
#   LICENSE・LICENSE-APACHE-2.0.txt  ライセンスの表示 (runtime/libfloat.asm は Apache 2.0)
#   testdata/       HuBASIC の試験用のプログラム 5 本の .BAS・.SL・.bin・.LST・.d88 (この木の hubasic2sl・slangbuild・slangc で作る)。
#                   FILEIO は x1native_hufs で、FILEIO.TXT を GREET.TXT として --hufs-add で足したディスク (足したファイルは FILEIO.GREET.TXT)
#   testdata/slang/ X1Pen の SLANG タブ用 (x1 の env) の見本 (examples/x1pen) の .SL と、slangbuild・AILZ80ASM で組んだ .ASM・.bin
#   manifest.json   組み立てた SHA、env ごとの値、アセンブラ、testdata、全部のファイルの sha256 (docs/HUBASIC.md)
#
# 要るもの: dotnet SDK、python3、AILZ80ASM (AILZ80ASM_PATH・PATH・この木の tools/ の順に探す)。
# node 22.7 以上 (NODE で指せる): 最後に、wasm の変換・コンパイル・ディスク作りが CLI と同じかを確かめる (無ければ止まる)。
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
HUB="$REPO/src/SLANGCompiler.HuBasic/bin/Release/net10.0/hubasic2sl.dll"
SLANGC="$REPO/src/SLANGCompiler.CLI/bin/Release/net10.0/slangc"
SLANGBUILD="$REPO/src/SLANGCompiler.Build/bin/Release/net10.0/slangbuild"
SLANGC_DLL="$REPO/src/SLANGCompiler.CLI/bin/Release/net10.0/slangc.dll"

echo "[2/5] wasm を組み立てます"
dotnet publish src/SLANGCompiler.HuBasic.Wasm/SLANGCompiler.HuBasic.Wasm.csproj -c Release -o "$TMP/publish" --nologo -v q > "$TMP/publish.log" 2>&1 \
  || { cat "$TMP/publish.log" >&2; exit 1; }
cp -R "$TMP/publish/wwwroot/_framework" "$OUT/_framework"

echo "[3/5] 試験用のプログラムを作ります (AILZ80ASM $ASM_VERSION)"
mkdir "$OUT/testdata"
CASES=""
HUFS_ADD=""
for bas in examples/hubasic/HELLO.BAS examples/hubasic/GRAPH.BAS examples/hubasic/MUSIC.BAS examples/hubasic/FLOAT.BAS examples/hubasic/FILEIO.BAS; do
  name="$(basename "$bas" .BAS)"
  base="$OUT/testdata/$name"
  cp "$bas" "$base.BAS"
  dotnet "$HUB" "$base.BAS" -o "$base.SL" 2> "$TMP/$name.transpile.log" || { cat "$TMP/$name.transpile.log" >&2; exit 1; }
  # ファイルの命令を使う例は、変換が SL の頭に `// env: x1native_hufs` と書く
  env="$(sed -n 's|^// env: \([^ ]*\).*|\1|p' "$base.SL" | head -1)"
  env="${env:-x1native_slfs}"
  add=()
  if [ "$name" = FILEIO ]; then
    cp examples/hubasic/FILEIO.TXT "$base.GREET.TXT"
    add=(--hufs-add "GREET.TXT=$base.GREET.TXT,text")
    HUFS_ADD="$HUFS_ADD${HUFS_ADD:+;}FILEIO:GREET.TXT=testdata/FILEIO.GREET.TXT,text"
  fi
  "$SLANGBUILD" --emit disk -E "$env" "$base.SL" -o "$base" "${add[@]}" --slangc "$SLANGC" --asm "$ASM" > "$TMP/$name.log" 2>&1 \
    || { cat "$TMP/$name.log" >&2; exit 1; }
  dotnet "$HUB" --check-bin "$base.bin" --lst "$base.LST"
  CASES="$CASES${CASES:+,}$name"
done

# X1Pen の SLANG タブ用 (x1 の env): 見本の .SL と、組んだ .ASM・.bin だけを置く
mkdir "$OUT/testdata/slang"
SLANG_CASES=""
for sl in examples/x1pen/*.SL; do
  name="$(basename "$sl" .SL)"
  work="$TMP/slang-$name"
  mkdir "$work"
  cp "$sl" "$work/$name.SL"
  "$SLANGBUILD" -E x1 -I "$REPO/include" "$work/$name.SL" -o "$work/$name" --emit bin --keep-asm --slangc "$SLANGC" --asm "$ASM" \
    > "$TMP/slang-$name.log" 2>&1 || { cat "$TMP/slang-$name.log" >&2; exit 1; }
  for ext in SL ASM bin; do cp "$work/$name.$ext" "$OUT/testdata/slang/$name.$ext"; done
  SLANG_CASES="$SLANG_CASES${SLANG_CASES:+,}$name"
done

echo "[4/5] ランタイムと manifest を作り、束を確かめます"
python3 -I scripts/hubasic-wasm-manifest.py copy "$REPO" "$OUT"
python3 -I scripts/hubasic-wasm-manifest.py write "$REPO" "$OUT" "$SHA" "$DIRTY" "$VERSION" "$ASM" "$ASM_VERSION" "$CASES" "$HUFS_ADD" "$SLANG_CASES"
python3 -I scripts/hubasic-wasm-manifest.py verify "$OUT"

echo "[5/5] wasm の変換・コンパイル・ディスク作りを CLI と比べます"
NODE="${NODE:-node}"
if ! command -v "$NODE" > /dev/null 2>&1; then
  # 確かめずに束を渡さない (束が CLI と同じことを確かめるのは、この段だけ)
  echo "build-hubasic-wasm: node が無いので、束を確かめられません (node 22.7 以上を PATH に置くか、NODE で指してください)" >&2
  exit 1
fi
"$NODE" scripts/hubasic-wasm-smoke.mjs "$OUT" "$REPO" "$HUB" "$SLANGC_DLL"

echo "build-hubasic-wasm: $OUT にできました (SHA $SHA、dirty $DIRTY)"
