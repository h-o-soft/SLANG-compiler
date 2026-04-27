#!/bin/sh
# SLANG New Compiler (slangc) publisher

CSPROJ=src/SLANGCompiler.CLI/SLANGCompiler.CLI.csproj
CSPROJ_BUILD=src/SLANGCompiler.Build/SLANGCompiler.Build.csproj
TFM=net8.0

createRelease() {
  cd publish-new/$1
  mkdir bin
  mv slangc* bin
  mv slangbuild* bin
  cp -r ../../include .
  cp -r ../../runtime .
  # examples: SLANGソースのみ（ビルド成果物除外）
  # top-level の *.SL と、サブディレクトリ chip / spr / tile / tilespr / ui
  # から *.SL / *.sl / README.md / *.json のみ whitelist 方式で拾う。
  # *.ASM / *.LST / *.SYM / *.bin / PROG.com 等のビルド成果物は含めない。
  mkdir -p examples
  cp ../../examples/*.SL examples/ 2>/dev/null
  for sub in chip spr tile tilespr ui; do
    if [ -d "../../examples/$sub" ]; then
      mkdir -p "examples/$sub"
      cp ../../examples/$sub/*.SL        "examples/$sub/" 2>/dev/null
      cp ../../examples/$sub/*.sl        "examples/$sub/" 2>/dev/null
      cp ../../examples/$sub/README.md   "examples/$sub/" 2>/dev/null
      cp ../../examples/$sub/*.json      "examples/$sub/" 2>/dev/null
    fi
  done

  # assets: UILIB 用フォント / CHARMAP 再生成ソース (PNG, JSON)
  if [ -d "../../assets" ]; then
    cp -r ../../assets .
  fi

  # tools: ホストで動かす Python スクリプト (charmap-encode.py, png_to_asm.py,
  # disk-add-overlays.py)。disk-add-overlays.py は make ENV=lsx|x1 disk_image で
  # overlay バイナリ (M0.BIN..) を d88 へ書き込む際の helper。
  # RunCPM 系は後段で追加するので、ここでは Python ツールだけ拾う。
  mkdir -p tools
  cp ../../tools/charmap-encode.py    tools/ 2>/dev/null
  cp ../../tools/png_to_asm.py        tools/ 2>/dev/null
  cp ../../tools/disk-add-overlays.py tools/ 2>/dev/null

  cp -r ../../env .
  # images: LSXPROG.d88のみ
  mkdir -p images
  cp ../../images/LSXPROG.d88 images/
  cp -r ../../docs .
  cp -r ../../syntax .
  cp ../../Makefile.dist ./Makefile
  cp ../../README.md .
  cp ../../CHANGELOG.md .
  cp ../../LICENSE .
  cp ../../setupenv.bat .
  cp ../../setupenv.sh .

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

createRelease osx-x64 sh $VERSION
createRelease osx-arm64 sh $VERSION
createRelease win-x64 bat $VERSION
createRelease linux-x64 sh $VERSION

echo "Done. Packages created:"
ls -la SLANG-compiler-$VERSION-*.zip
