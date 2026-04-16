# RunCPM bundled binary の作り方

このフォルダに置く RunCPM バイナリは、**上流 (https://github.com/MockbaTheBorg/RunCPM)
のソースを1箇所だけ修正した**ものです。

## 修正内容

`RunCPM/globals.h`:
```diff
-#define BOOTONLY FALSE
+#define BOOTONLY TRUE
```

`AUTOEXEC.TXT` を初回起動時にのみ消費させるための変更。これが無いと、SLANG プログラム
終了後に CCP が再起動するたびに AUTOEXEC を再実行して無限ループになる。

## macOS (arm64)

```sh
git clone --depth 1 https://github.com/MockbaTheBorg/RunCPM.git
cd RunCPM/RunCPM
sed -i.bak 's/#define BOOTONLY FALSE/#define BOOTONLY TRUE/' globals.h
make -f Makefile.macosx
cp RunCPM /path/to/SLANG-compiler/tools/runcpm/RunCPM-macos-arm64
```

## macOS (x64)

arm64 機から `-arch x86_64` でクロスビルド:

```sh
make -f Makefile.macosx clean
make -f Makefile.macosx CC="gcc -arch x86_64"
cp RunCPM /path/to/SLANG-compiler/tools/runcpm/RunCPM-macos-x64
```

Intel Mac で単純ビルドしても OK (結果は同じ `RunCPM-macos-x64`)。

## Linux (x64)

```sh
git clone --depth 1 https://github.com/MockbaTheBorg/RunCPM.git
cd RunCPM/RunCPM
sed -i 's/#define BOOTONLY FALSE/#define BOOTONLY TRUE/' globals.h
make -f Makefile.posix
strip RunCPM    # 任意: バイナリサイズ削減
cp RunCPM /path/to/SLANG-compiler/tools/runcpm/RunCPM-linux-x64
```

## Windows (x64)

### macOS/Linux から mingw-w64 でクロスビルド (推奨)

Homebrew の `x86_64-w64-mingw32-gcc` 等がある場合:

```sh
git clone --depth 1 https://github.com/MockbaTheBorg/RunCPM.git
cd RunCPM/RunCPM
sed -i.bak 's/#define BOOTONLY FALSE/#define BOOTONLY TRUE/' globals.h
# Makefile.mingw は clean で `del /Q` を使うので直接は呼ばない。
# main.o を手動で消してから CC 上書きでビルド:
rm -f main.o RunCPM.exe
make -f Makefile.mingw CC=x86_64-w64-mingw32-gcc
cp RunCPM.exe /path/to/SLANG-compiler/tools/runcpm/RunCPM-win-x64.exe
```

### Windows 上で直接ビルド

MinGW-w64 がインストール済みの前提:

```cmd
git clone --depth 1 https://github.com/MockbaTheBorg/RunCPM.git
cd RunCPM\RunCPM
REM globals.h の BOOTONLY を手動で FALSE → TRUE に修正
mingw32-make -f Makefile.mingw
copy RunCPM.exe C:\path\to\SLANG-compiler\tools\runcpm\RunCPM-win-x64.exe
```

MSYS2 環境なら `Makefile.msys2` を使用。

## ビルド確認

同梱バイナリは `tools/runcpm.sh` (Unix) / `tools/runcpm.bat` (Windows) 経由で
呼ばれる。`make TARGET=path/to/prog ENV=cpm run` で実行して、プログラムの標準出力
のみがフィルタされて表示されることを確認する。
