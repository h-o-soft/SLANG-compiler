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
# 出力フォルダに書くのは 1 つの組み立てだけ。先に印 (.building) を作り (作れなければ、ほかの組み立てが書いている)、
# 印を持ったまま、出力フォルダが印のほかは空かを確かめる。印は、この組み立てが終わるときに 1 度だけ消す
if ! mkdir "$OUT/.building" 2> /dev/null; then
  echo "build-hubasic-wasm: $OUT には、ほかの組み立てが書いています (.building があります)" >&2
  exit 2
fi
trap 'rmdir "$OUT/.building" 2> /dev/null || true' EXIT
if [ -n "$(ls -A "$OUT" | grep -Fvx '.building' || true)" ]; then
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

# node は最後の確かめに要る。何かを作る前に確かめる (確かめられない束は作らない)
NODE="${NODE:-node}"
if ! command -v "$NODE" > /dev/null 2>&1 \
   || ! "$NODE" -e 'const [a,b]=process.versions.node.split(".").map(Number); process.exit(a>22||(a===22&&b>=7)?0:1)'; then
  echo "build-hubasic-wasm: node 22.7 以上が要ります (PATH に置くか、NODE で指してください)" >&2
  exit 1
fi
# 確かめ (wasm の中は / 区切りの道) は、Windows の node では動かない。Windows では WSL などの Linux で組み立てる
if [ "$("$NODE" -p 'process.platform')" = win32 ]; then
  echo "build-hubasic-wasm: Windows の node では束を確かめられません (WSL などの Linux で動かしてください)" >&2
  exit 1
fi

TMP="$(mktemp -d)"
# 束は出力フォルダに直に作る (許可は呼んだ人の umask と出力フォルダのまま)。manifest.json は束の外 ($TMP) に書いて
# 確かめ、manifest の検査と CLI との比べ合わせが全部通ってから、最後に名前の付け替えで束に入れる。
# 途中で止まった束には manifest.json が無いので、検査も取り込みも通らない
trap 'rm -rf "$TMP"; rmdir "$OUT/.building" 2> /dev/null || true' EXIT

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
  # slangbuild の引数 (空の配列を "${a[@]}" で広げると、古い bash の set -u で止まるので、いつも中身のある配列にする)
  args=(--emit disk -E "$env" "$base.SL" -o "$base" --slangc "$SLANGC" --asm "$ASM")
  if [ "$name" = FILEIO ]; then
    cp examples/hubasic/FILEIO.TXT "$base.GREET.TXT"
    args+=(--hufs-add "GREET.TXT=$base.GREET.TXT,text")
    HUFS_ADD="$HUFS_ADD${HUFS_ADD:+;}FILEIO:GREET.TXT=testdata/FILEIO.GREET.TXT,text"
  fi
  "$SLANGBUILD" "${args[@]}" > "$TMP/$name.log" 2>&1 || { cat "$TMP/$name.log" >&2; exit 1; }
  dotnet "$HUB" --check-bin "$base.bin" --lst "$base.LST"
  CASES="$CASES${CASES:+,}$name"
done

# X1Pen の SLANG タブ用 (x1 の env): 見本の .SL と、組んだ .ASM・.bin だけを置く
mkdir "$OUT/testdata/slang"
SLANG_CASES=""
# 見本は名前を書いて選ぶ (git に入っていない手元の .SL を束に入れない。publish.sh と同じ一覧)
for name in SCREEN SOUND INPUT; do
  sl="examples/x1pen/$name.SL"
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
python3 -I scripts/hubasic-wasm-manifest.py write "$REPO" "$OUT" "$SHA" "$DIRTY" "$VERSION" "$ASM" "$ASM_VERSION" "$CASES" "$HUFS_ADD" "$SLANG_CASES" "$TMP/manifest.json"
python3 -I scripts/hubasic-wasm-manifest.py verify "$OUT" "$TMP/manifest.json"

echo "[5/5] wasm の変換・コンパイル・ディスク作りを CLI と比べます"
"$NODE" scripts/hubasic-wasm-smoke.mjs "$OUT" "$REPO" "$HUB" "$SLANGC_DLL" "$TMP/manifest.json"

# 全部通ったので manifest.json を束に入れる (同じフォルダの中の rename で、一度に現れる)。
# 印 (.building) は最後の検査まで持ち、終わるときに消す
cp "$TMP/manifest.json" "$OUT/.manifest.json.tmp"
mv -f "$OUT/.manifest.json.tmp" "$OUT/manifest.json"
python3 -I scripts/hubasic-wasm-manifest.py verify "$OUT"
echo "build-hubasic-wasm: $OUT にできました (SHA $SHA、dirty $DIRTY)"
