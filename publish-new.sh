#!/bin/sh
# SLANG New Compiler (slangc) publisher

CSPROJ=src/SLANGCompiler.CLI/SLANGCompiler.CLI.csproj
TFM=net8.0

createRelease() {
  cd publish-new/$1
  mkdir bin
  mv slangc* bin
  cp -r ../../include .
  cp -r ../../lib .
  cp -r ../../runtime .
  cp -r ../../examples .
  cp -r ../../env .
  cp -r ../../images .
  cp -r ../../syntax .
  cp ../../Makefile.dist ./Makefile
  cp ../../README.md .
  cp ../../LICENSE .
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

dotnet publish $CSPROJ -c Release -r osx-x64 --self-contained true /p:PublishSingleFile=true
dotnet publish $CSPROJ -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true
dotnet publish $CSPROJ -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
dotnet publish $CSPROJ -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true

mkdir -p publish-new/osx-x64
mkdir -p publish-new/osx-arm64
mkdir -p publish-new/win-x64
mkdir -p publish-new/linux-x64

cp src/SLANGCompiler.CLI/bin/Release/$TFM/osx-x64/publish/slangc publish-new/osx-x64
cp src/SLANGCompiler.CLI/bin/Release/$TFM/osx-arm64/publish/slangc publish-new/osx-arm64
cp src/SLANGCompiler.CLI/bin/Release/$TFM/win-x64/publish/slangc.exe publish-new/win-x64
cp src/SLANGCompiler.CLI/bin/Release/$TFM/linux-x64/publish/slangc publish-new/linux-x64

createRelease osx-x64 sh $VERSION
createRelease osx-arm64 sh $VERSION
createRelease win-x64 bat $VERSION
createRelease linux-x64 sh $VERSION

echo "Done. Packages created:"
ls -la SLANG-compiler-$VERSION-*.zip
