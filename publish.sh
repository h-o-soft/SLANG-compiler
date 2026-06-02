#!/bin/sh
# SLANG New Compiler (slangc) publisher

CSPROJ=src/SLANGCompiler.CLI/SLANGCompiler.CLI.csproj
CSPROJ_BUILD=src/SLANGCompiler.Build/SLANGCompiler.Build.csproj
CSPROJ_SLFS=src/SLANGCompiler.SlfsPack/SLANGCompiler.SlfsPack.csproj
TFM=net8.0

createRelease() {
  cd publish-new/$1
  mkdir bin
  mv slangc* bin
  mv slangbuild* bin
  mv slfs-pack* bin
  cp -r ../../include .
  cp -r ../../runtime .
  # examples: SLANGソースのみ（ビルド成果物除外）
  # top-level の *.SL と、サブディレクトリ chip / spr / tile / tilespr / ui /
  # pc80mk2 / c64 / X1NATIVE_MTREAD から *.SL / *.sl / README.md / *.json /
  # *.mml のみ whitelist 方式で拾う。*.ASM / *.LST / *.SYM / *.bin / PROG.com
  # 等のビルド成果物は含めない。
  mkdir -p examples
  cp ../../examples/*.SL examples/ 2>/dev/null
  for sub in chip spr tile tilespr ui pc80mk2 c64 X1NATIVE_MTREAD; do
    if [ -d "../../examples/$sub" ]; then
      mkdir -p "examples/$sub"
      cp ../../examples/$sub/*.SL        "examples/$sub/" 2>/dev/null
      cp ../../examples/$sub/*.sl        "examples/$sub/" 2>/dev/null
      cp ../../examples/$sub/README.md   "examples/$sub/" 2>/dev/null
      cp ../../examples/$sub/*.json      "examples/$sub/" 2>/dev/null
      cp ../../examples/$sub/*.mml       "examples/$sub/" 2>/dev/null
    fi
  done

  # examples/X1NATIVE_SLFS: SLFS demo の SL + asset (= 任意 binary、 *.TXT /
  # *.BIN 等)。 SLANG runtime 同梱の slfs-pack が pack するための材料。
  # asset は whitelist 拡張子に当てはまらないため assets/ subdir を丸ごと拾う
  # (= 現状 GREETING.TXT / NUMBERS.BIN のみ、 将来 asset 追加時も拡張不要)。
  if [ -d "../../examples/X1NATIVE_SLFS" ]; then
    mkdir -p examples/X1NATIVE_SLFS/assets
    cp ../../examples/X1NATIVE_SLFS/*.SL          examples/X1NATIVE_SLFS/         2>/dev/null
    cp ../../examples/X1NATIVE_SLFS/assets/*      examples/X1NATIVE_SLFS/assets/  2>/dev/null
  fi

  # examples/zxn は ZX Spectrum Next 用 (= asset binary も配布対象)。
  # NextDAW Runtime Player (= NextDAW_RuntimePlayer_E000.bin) は
  # シェアウェアにつき配布不可、.gitignore で除外済 (whitelist 拡張子に
  # 含まれないので自動的に対象外)。
  if [ -d "../../examples/zxn" ]; then
    mkdir -p examples/zxn
    for ext in SL sl cfg nxp nxi spr til ndr nfx; do
      cp ../../examples/zxn/*.$ext examples/zxn/ 2>/dev/null
    done
    cp ../../examples/zxn/Makefile examples/zxn/ 2>/dev/null
  fi

  # examples/X1_ARKOS は Arkos Tracker driver sample (AKG / AKM)。 SL + Makefile +
  # README + 楽曲/効果音データ (.AKG / .AKM / .AKX、 Arkos export 生データ) を配布。
  # driver bin (PSGAK*_8000.bin) / *.sizes.inc / *.assets.inc / *.d88 / *.tap 等の
  # ビルド成果物は Makefile が RASM / slangbuild で再生成するため含めない。
  if [ -d "../../examples/X1_ARKOS" ]; then
    mkdir -p examples/X1_ARKOS
    cp ../../examples/X1_ARKOS/*.SL       examples/X1_ARKOS/ 2>/dev/null
    cp ../../examples/X1_ARKOS/README.md  examples/X1_ARKOS/ 2>/dev/null
    cp ../../examples/X1_ARKOS/Makefile   examples/X1_ARKOS/ 2>/dev/null
    cp ../../examples/X1_ARKOS/*.AKG      examples/X1_ARKOS/ 2>/dev/null
    cp ../../examples/X1_ARKOS/*.AKM      examples/X1_ARKOS/ 2>/dev/null
    cp ../../examples/X1_ARKOS/*.AKX      examples/X1_ARKOS/ 2>/dev/null
  fi

  # examples/X1_BANJO は Furnace tracker 用 banjo driver sample。曲 .fur は
  # ユーザーが用意するため配布に含めない。driver/song の中間生成物も Makefile
  # が再生成する。
  if [ -d "../../examples/X1_BANJO" ]; then
    mkdir -p examples/X1_BANJO/assets
    cp ../../examples/X1_BANJO/*.SL       examples/X1_BANJO/ 2>/dev/null
    cp ../../examples/X1_BANJO/README.md  examples/X1_BANJO/ 2>/dev/null
    cp ../../examples/X1_BANJO/Makefile   examples/X1_BANJO/ 2>/dev/null
  fi

  # examples/X1_BANJO_MULTI は複数曲 / 複数 SFX を 1 bundle (banjodat.bin) に
  # まとめる sample。曲 .fur は X1_BANJO 同様ユーザーが用意するため含めない。
  # assets/banjo_assets.txt は manifest 雛形なので配布に含める (.gitkeep も)。
  # bgm.bin / sfx.bin / *.log 等の中間生成物・ログは含めない。
  if [ -d "../../examples/X1_BANJO_MULTI" ]; then
    mkdir -p examples/X1_BANJO_MULTI/assets
    cp ../../examples/X1_BANJO_MULTI/*.SL                  examples/X1_BANJO_MULTI/ 2>/dev/null
    cp ../../examples/X1_BANJO_MULTI/README.md             examples/X1_BANJO_MULTI/ 2>/dev/null
    cp ../../examples/X1_BANJO_MULTI/Makefile              examples/X1_BANJO_MULTI/ 2>/dev/null
    cp ../../examples/X1_BANJO_MULTI/assets/banjo_assets.txt examples/X1_BANJO_MULTI/assets/ 2>/dev/null
    cp ../../examples/X1_BANJO_MULTI/assets/.gitkeep        examples/X1_BANJO_MULTI/assets/ 2>/dev/null
  fi

  # assets: UILIB 用フォント / CHARMAP 再生成ソース (PNG, JSON)
  if [ -d "../../assets" ]; then
    cp -r ../../assets .
  fi

  # tools: ホストで動かす Python スクリプト (charmap-encode.py, png_to_asm.py,
  # disk-add-overlays.py, mml2sound.py)。disk-add-overlays.py は make ENV=lsx|x1
  # disk_image で overlay バイナリ (M0.BIN..) を d88 へ書き込む際の helper、
  # mml2sound.py は MML テキストから libpc80mk2_sound 用の byte data を生成。
  # RunCPM 系は後段で追加するので、ここでは Python ツールだけ拾う。
  mkdir -p tools
  cp ../../tools/charmap-encode.py    tools/ 2>/dev/null
  cp ../../tools/png_to_asm.py        tools/ 2>/dev/null
  cp ../../tools/disk-add-overlays.py tools/ 2>/dev/null
  cp ../../tools/mml2sound.py         tools/ 2>/dev/null
  cp ../../tools/banjo_extract_syms.py tools/ 2>/dev/null
  cp ../../tools/banjo_prep_song.py    tools/ 2>/dev/null
  cp ../../tools/banjo_trim_to_end.py  tools/ 2>/dev/null
  cp ../../tools/banjo_pack_assets.py  tools/ 2>/dev/null
  cp ../../tools/json2sms_x1.py        tools/ 2>/dev/null
  # udostool.exe: pc88mk2sr 用 (Bookworm's Library 由来、repo 同梱で
  # setup-tools 不要、license 詳細は THIRD_PARTY_NOTICES.md 参照)
  cp ../../tools/udostool.exe         tools/ 2>/dev/null

  cp -r ../../env .
  # images/templates/LSXPROG.D88: pristine template (slangbuild --emit disk が
  # 必ずコピーしてから書き込む元データ、env file の disk.template が指す)。
  # 出力先 images/LSXPROG.d88 はユーザーが `make ENV=lsx disk_image` を実行
  # した時点で生成されるので、配布 zip には含めない。SOSPROG.D88 等は
  # ライセンス都合で同梱せず、make setup-tools で取得する想定。
  mkdir -p images/templates
  cp ../../images/templates/LSXPROG.D88   images/templates/
  # pc88mk2sr template: Bookworm's Library 由来 (= filesys_20141128 系の boot
  # disk)、repo 同梱で setup-tools 不要、license 詳細は THIRD_PARTY_NOTICES.md
  cp ../../images/templates/PC88MK2SR.D88 images/templates/
  cp -r ../../docs .
  cp -r ../../syntax .
  cp ../../Makefile.dist ./Makefile
  cp ../../README.md .
  cp ../../CHANGELOG.md .
  cp ../../LICENSE .
  cp ../../THIRD_PARTY_NOTICES.md .
  cp ../../setupenv.bat .
  cp ../../setupenv.sh .
  # install scripts (Makefile に依存しない install 経路)
  cp ../../install.sh    .
  cp ../../install.bat   .
  cp ../../uninstall.sh  .
  cp ../../uninstall.bat .
  chmod +x install.sh uninstall.sh

  # RunCPM bundle (per-platform binary + CP/M utilities + wrapper)
  mkdir -p tools/runcpm/cpm
  cp ../../tools/runcpm/LICENSE tools/runcpm/
  cp ../../tools/runcpm/README.md tools/runcpm/
  cp ../../tools/runcpm/cpm/EXIT.COM   tools/runcpm/cpm/
  cp ../../tools/runcpm/cpm/SUBMIT.COM tools/runcpm/cpm/
  case "$1" in
    osx-arm64)   cp ../../tools/runcpm/RunCPM-macos-arm64 tools/runcpm/ ;;
    osx-x64)     cp ../../tools/runcpm/RunCPM-macos-x64   tools/runcpm/ ;;
    linux-x64)   cp ../../tools/runcpm/RunCPM-linux-x64   tools/runcpm/ ;;
    win-x64)     cp ../../tools/runcpm/RunCPM-win-x64.exe tools/runcpm/ ;;
  esac
  case "$1" in
    win-x64)     cp ../../tools/runcpm.bat tools/ ;;
    *)           cp ../../tools/runcpm.sh tools/ ;;
  esac

  # mzd88 (MZ-2500 D88 image 操作ツール、issaUt/mz2500-tools の C 実装、MIT)
  # repo には platform 別 file 名 (mzd88-{rid}) で commit、配布物では現在 OS 用
  # binary を `mzd88(.exe)` にリネームコピーする (= ToolResolver が両 file 名を
  # fallback で探す)。license / 出典は THIRD_PARTY_NOTICES.md に記載。
  case "$1" in
    osx-arm64)   cp ../../tools/mzd88-osx-arm64    tools/mzd88     && chmod +x tools/mzd88 ;;
    osx-x64)     cp ../../tools/mzd88-osx-x64      tools/mzd88     && chmod +x tools/mzd88 ;;
    linux-x64)   cp ../../tools/mzd88-linux-x64    tools/mzd88     && chmod +x tools/mzd88 ;;
    win-x64)     cp ../../tools/mzd88-win-x64.exe  tools/mzd88.exe ;;
  esac

  zip -r SLANG-compiler-$3-$1.zip * -x '*/.DS_Store'
  mv SLANG-compiler-$3-$1.zip ../../
  cd ../..
}

if [ $# -eq 0 ]; then
  echo "SLANG New Compiler publisher"
  echo "./publish-new.sh version"
  exit 1
fi

VERSION=$1
rm -rf publish-new

for RID in osx-x64 osx-arm64 win-x64 linux-x64; do
  dotnet publish $CSPROJ       -c Release -r $RID --self-contained true /p:PublishSingleFile=true
  dotnet publish $CSPROJ_BUILD -c Release -r $RID --self-contained true /p:PublishSingleFile=true
  dotnet publish $CSPROJ_SLFS  -c Release -r $RID --self-contained true /p:PublishSingleFile=true
done

mkdir -p publish-new/osx-x64
mkdir -p publish-new/osx-arm64
mkdir -p publish-new/win-x64
mkdir -p publish-new/linux-x64

cp src/SLANGCompiler.CLI/bin/Release/$TFM/osx-x64/publish/slangc          publish-new/osx-x64
cp src/SLANGCompiler.CLI/bin/Release/$TFM/osx-arm64/publish/slangc        publish-new/osx-arm64
cp src/SLANGCompiler.CLI/bin/Release/$TFM/win-x64/publish/slangc.exe      publish-new/win-x64
cp src/SLANGCompiler.CLI/bin/Release/$TFM/linux-x64/publish/slangc        publish-new/linux-x64

cp src/SLANGCompiler.Build/bin/Release/$TFM/osx-x64/publish/slangbuild        publish-new/osx-x64
cp src/SLANGCompiler.Build/bin/Release/$TFM/osx-arm64/publish/slangbuild      publish-new/osx-arm64
cp src/SLANGCompiler.Build/bin/Release/$TFM/win-x64/publish/slangbuild.exe    publish-new/win-x64
cp src/SLANGCompiler.Build/bin/Release/$TFM/linux-x64/publish/slangbuild      publish-new/linux-x64

cp src/SLANGCompiler.SlfsPack/bin/Release/$TFM/osx-x64/publish/slfs-pack       publish-new/osx-x64
cp src/SLANGCompiler.SlfsPack/bin/Release/$TFM/osx-arm64/publish/slfs-pack     publish-new/osx-arm64
cp src/SLANGCompiler.SlfsPack/bin/Release/$TFM/win-x64/publish/slfs-pack.exe   publish-new/win-x64
cp src/SLANGCompiler.SlfsPack/bin/Release/$TFM/linux-x64/publish/slfs-pack     publish-new/linux-x64

createRelease osx-x64 sh $VERSION
createRelease osx-arm64 sh $VERSION
createRelease win-x64 bat $VERSION
createRelease linux-x64 sh $VERSION

echo "Done. Packages created:"
ls -la SLANG-compiler-$VERSION-*.zip
