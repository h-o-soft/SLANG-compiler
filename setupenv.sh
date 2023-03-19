#!/bin/sh

if [ $# -eq 0 ]; then
  echo SLANG-compiler setup batch v.1.0
  echo  setupenv.sh envname[mac / linux]
  exit 1
fi

# mac or linux
TARGETENV=$1

function Error()
{
  echo Error!
  echo 
  cd $CURPATH
  exit 1
}

function CmdError() {
  echo !
  echo             _________________
  echo --------------------------------------------
  echo Error! $CMDNAME がインストールされていません
  echo 
  cd $CURPATH
  exit 1
}

CURPATH=$(cd $(dirname $0);pwd)/
PROGPATH=`readlink -f $1`

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

CMDNAME=lha
which lha
if [ $? -ne 0 ]; then
  CmdError
fi

CMDNAME=mono
which mono
if [ $? -ne 0 ]; then
  CmdError
fi

TOOLPATH=$(cd $(dirname $0);pwd)/tools/
mkdir images
mkdir tools
mkdir temp
cd temp

# NDCをダウンロード

# Mac
if [ $TARGETENV == "mac" ]; then
  DLPATH=https://euee.web.fc2.com/tool/ndcm0a06b.tgz
elif [ $TARGETENV == "linux" ]; then
  DLPATH=https://euee.web.fc2.com/tool/ndcl0a06b.tgz
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
if [ $TARGETENV == "mac" ]; then
  DLPATH=https://github.com/AILight/AILZ80ASM/releases/download/v1.0.1/AILZ80ASM.osx-x64.v1.0.1.zip
elif [ $TARGETENV == "linux" ]; then
  DLPATH=https://github.com/AILight/AILZ80ASM/releases/download/v1.0.1/AILZ80ASM.linux-x64.v1.0.1.zip
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
curl http://www.retropc.net/ohishi/s-os/SWXCV110.zip -OL
unzip -xo SWXCV110.zip
# AUTOEXEC.BATを追加
mv SWXCV110.d88 SOSPROG.d88
mono $TOOLPATH/HuDisk.exe SOSPROG.d88 -a ../env/S-OS/AUTOEXEC.BAT --ascii
cp SOSPROG.d88 ../images/
rm SOSPROG.d88
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

cd ..
rm -rf temp

exit 0
