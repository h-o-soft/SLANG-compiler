#!/bin/sh
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
cd publish
zip -r SLANG-compiler.zip *
mv SLANG-compiler.zip ..
cd ..
