#!/bin/sh

createRelease() {
  cd publish/$1
  mkdir bin
  mv SLANGCompiler* bin
  cp ../../*.env .
  cp ../../*.yml .
  cp -r ../../extlib .
  cp -r ../../examples .
  cp -r ../../env .
  cp -r ../../images .
  cp ../../slbuild.$2 .
  cp ../../copyruntime.$2 .
  cp ../../setupenv.$2 .
  zip -r SLANG-compiler-$3-$1.zip * -x '*/.DS_Store'
  mv SLANG-compiler-$3-$1.zip ../../
  cd ../..
}

if [ $# -eq 0 ]; then
  echo SLANG Compiler publusher
  echo ./publish.sh version
  exit 1
fi

VERSION=$1
rm -rf publish
dotnet publish -c Release -r osx-x64 /p:publishSingleFile=true
dotnet publish -c Release -r osx-arm64 /p:publishSingleFile=true
dotnet publish -c Release -r win-x64 /p:publishSingleFile=true
dotnet publish -c Release -r linux-x64 /p:publishSingleFile=true
mkdir publish
mkdir publish/osx-x64
mkdir publish/osx-arm64
mkdir publish/win-x64
mkdir publish/linux-x64
cp bin/Release/net6.0/osx-x64/publish/SLANGCompiler publish/osx-x64
cp bin/Release/net6.0/osx-arm64/publish/SLANGCompiler publish/osx-arm64
cp bin/Release/net6.0/win-x64/publish/SLANGCompiler.exe publish/win-x64
cp bin/Release/net6.0/linux-x64/publish/SLANGCompiler publish/linux-x64
createRelease osx-x64 sh $VERSION
createRelease osx-arm64 sh $VERSION
createRelease win-x64 bat $VERSION
createRelease linux-x64 sh $VERSION
