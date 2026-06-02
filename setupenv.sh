#!/bin/sh

if [ $# -eq 0 ]; then
  echo SLANG-compiler setup batch v.1.0
  echo  setupenv.sh envname[mac / linux]
  exit 1
fi

# mac or linux
TARGETENV=$1

# POSIX sh 互換 (= Linux の /bin/sh = dash でも動く形)。
# 旧版の `function name() { ... }` は bash 拡張で dash で syntax error になる。
Error() {
  echo Error!
  echo
  cd $CURPATH
  exit 1
}

CmdError() {
  echo !
  echo             _________________
  echo --------------------------------------------
  echo Error! $CMDNAME がインストールされていません
  echo
  cd $CURPATH
  exit 1
}

CURPATH=$(cd $(dirname $0);pwd)/

# コマンドがあるかチェック
CMDNAME=curl
which $CMDNAME
if [ $? -ne 0 ]; then
  CmdError
fi

CMDNAME=unzip
which unzip
if [ $? -ne 0 ]; then
  CmdError
fi

CMDNAME=tar
which tar
if [ $? -ne 0 ]; then
  CmdError
fi

CMDNAME=mono
which mono
if [ $? -ne 0 ]; then
  CmdError
fi

# Linux/WSL では mono デフォルト install に CP932 (日本語) コードページが
# 含まれていない場合がある。HuDisk が CP932 を要求するため、
# `Encoding 932 data could not be found` で失敗するなら、
#   sudo apt install libmono-i18n4.0-all   (Debian/Ubuntu)
# を実行してから setupenv.sh を再実行してください。
# macOS の Homebrew mono にはデフォルトで含まれているため不要。

TOOLPATH=$(cd $(dirname $0);pwd)/tools/
mkdir images
mkdir -p images/templates
mkdir tools
mkdir temp
cd temp

# NDCをダウンロード

# Mac (POSIX sh では `==` ではなく `=`)
if [ "$TARGETENV" = "mac" ]; then
  DLPATH=https://euee.web.fc2.com/tool/ndcm0a08arm.tgz
elif [ "$TARGETENV" = "linux" ]; then
  DLPATH=https://euee.web.fc2.com/tool/ndcl0a08x64.tgz
else
  Error
fi
FILENAME=${DLPATH##*/}
curl $DLPATH -fsLO
tar zxvf $FILENAME
rm $FILENAME
mv ndc $TOOLPATH
mv ndcmsg.txt $TOOLPATH

# HuDISKをダウンロード
# curl https://github.com/BouKiCHi/HuDisk/raw/master/HuDisk.exe -OL
# (ASCII書き込み可能版)
curl https://github.com/ho-ogino/HuDisk/raw/feature/write-ascii-mode/HuDisk.exe -OL
if [ $? -ne 0 ]; then
  Error
fi
cp HuDisk.exe $TOOLPATH
rm HuDisk.exe

# AILZ80ASMをダウンロード
if [ "$TARGETENV" = "mac" ]; then
  DLPATH=https://github.com/AILight/AILZ80ASM/releases/download/v1.0.31/AILZ80ASM.osx-x64.v1.0.31.zip
elif [ "$TARGETENV" = "linux" ]; then
  DLPATH=https://github.com/AILight/AILZ80ASM/releases/download/v1.0.31/AILZ80ASM.linux-x64.v1.0.31.zip
else
  Error
fi
FILENAME=${DLPATH##*/}
curl $DLPATH -OL
if [ $? -ne 0 ]; then
  Error
fi
unzip -xo $FILENAME
chmod +x AILZ80ASM
cp AILZ80ASM $TOOLPATH
rm AILZ80ASM
rm $FILENAME

# S-OS(X1)をダウンロード
# 最終的に images/templates/SOSPROG.D88 に置く (= slangbuild --emit disk が
# 参照する pristine template、LSX の templates/ パターンと整合)。
curl http://www.retropc.net/ohishi/s-os/SWXCV110.zip -OL
unzip -xo SWXCV110.zip
# AUTOEXEC.BATを追加
mv SWXCV110.d88 SOSPROG.D88
mono $TOOLPATH/HuDisk.exe SOSPROG.D88 -a ../env/S-OS/AUTOEXEC.BAT --ascii
cp SOSPROG.D88 ../images/templates/
rm SOSPROG.D88
rm SWXCV110.zip

# LSX-Dodgersは特殊フォーマットのため取得して加工する事が出来ない(NDCでアクセス不可の)ため対応しない
# どうしたものか……
# curl https://github.com/tablacus/LSX-Dodgers/releases/download/1.55/ldsys155.zip -OL
# unzip ldsys155.zip

# 似非DOS for MSXをダウンロード
curl https://github.com/tablacus/dosformsx/releases/download/0.16/dosformsx_016.zip -OL
unzip -xo dosformsx_016.zip
# AUTOEXEC.BATを追加
$TOOLPATH/ndc P dosformsx.dsk 0 ../env/LSX-Dodgers/AUTOEXEC.BAT
cp dosformsx.dsk ../images/
rm dosformsx.dsk
rm dos2formsx.dsk
rm dosformsx_016.zip

# WLA-DX (wla-z80 / wlalink): banjo (Furnace) サウンドドライバ sample
# (examples/X1_BANJO, examples/X1_BANJO_MULTI) のビルドに必要。
# 他ツールと違い PATH の通った場所にあるシステムバイナリとして使う想定のため
# TOOLPATH には置かず、 自動インストールもしない (システムへ勝手に入れない方針)。
# 未導入なら導入コマンド例を示して非ゼロ終了する。
if which wla-z80 >/dev/null 2>&1 && which wlalink >/dev/null 2>&1; then
  echo "WLA-DX (wla-z80 / wlalink) は導入済みです。"
else
  echo
  echo "Error! WLA-DX (wla-z80 / wlalink) が PATH に見つかりません。"
  echo "  banjo (Furnace) サウンドドライバ sample のビルドに必要です。"
  echo "  PATH の通った場所に導入してください:"
  echo "    mac  : brew install wla-dx"
  echo "    linux: sudo apt install wla-dx   (または https://github.com/vhelin/wla-dx をビルド)"
  echo
  cd $CURPATH
  exit 1
fi

cd ..
rm -rf temp

exit 0
