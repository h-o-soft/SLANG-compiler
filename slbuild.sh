#!/bin/sh

# emulator path
EMUPATH=`readlink -f ~/emulator/x1/x1.exe`
MSXEMUPATH=`readlink -f /Applications/openMSX.app/Contents/MacOS/openmsx`

function Launch() {
  cd $CURPATH

  $EMULATOR $IMAGEPATH/$PROGIMAGE

  echo DONE!
  exit 0
}

function LaunchMSX() {
  cd $CURPATH

  $MSXEMULATOR -cart $PROG.BIN

  echo DONE!
  exit 0
}

function Error() {
  echo ERROR! $1
  cd $CURPATH
  exit 1
}

if [ $# -eq 0 ]; then
  echo SLANG Compile batch v.1.0
  echo  slbuild.bat SLANGSource.SL
  exit 1
fi

if [ -z $EMUPATH ]; then
  Error "emulator not found"
fi

# where $MSXEMUPATH
# if [ $? -ne 0 ]; then
#   EmuError
# fi

# program info
CURPATH=$(cd $(dirname $0);pwd)/
PROGPATH=`readlink -f $1`

PROGDIR=${PROGPATH%/*}
FILENAME=${PROGPATH##*/}
PROG=${FILENAME%.*}
PROGEXT=${FILENAME##*.}

echo SOURCE : $PROGDIR$PROG.$PROGEXT

# Target = lsx / x1 / sos / msx2
if [ $# -eq 1 ]; then
TARGETENV=lsx
CHECKCPM=0
elif [ $2 == "cpm" ]; then
TARGETENV=lsx
CHECKCPM=1
else
TARGETENV=$2
CHECKCPM=0
fi

# Additional library
# ADDLIB=-L soroban

# runtime copy to ~/.config/SLANG
COPYRUNTIME=0
RUNTIMEPATH=.

# S-OS addresses
LOADADR=3000
RUNADR=3000

TOOLPATH=${CURPATH}tools
IMAGEPATH=${CURPATH}images

SLANGCOMPILER="${CURPATH}bin/SLANGCompiler"
ASM=$TOOLPATH/AILZ80ASM


EMULATOR="wine $EMUPATH"
MSXEMULATOR="$MSXEMUPATH"

NDCPATH=$TOOLPATH/ndc
HUDISKPATH="mono $TOOLPATH/HuDisk.exe"
CPMEMUPATH="wine $TOOLPATH/cpm.exe"

if [ $COPYRUNTIME -gt 0 ]; then
  mkdir ~/.config/SLANG/
  cp $RUNTIMEPATH/*.env ~/.config/SLANG/
  cp $RUNTIMEPATH/*.yml ~/.config/SLANG/
  cp -R extlib ~/.config/SLANG/
fi

cd $PROGDIR

$SLANGCOMPILER ${PROG}.${PROGEXT} -E $TARGETENV $ADDLIB
if [ $? -ne 0 ]; then
  Error ""
fi

$ASM $PROG.ASM -sym -lst -bin -f
if [ $? -ne 0 ]; then
  Error ""
fi

if [ $CHECKCPM -gt 0 ]; then
  $CPMEMUPATH $PROG.BIN
  cd $CURPATH
  exit 0
elif [ $TARGETENV == "lsx" ] || [ $TARGETENV == "x1" ]; then
  echo LSX-Dodgers
  PROGIMAGE=LSXPROG.D88
  rm PROG.COM
  mv $PROG.BIN PROG.COM
  $NDCPATH D $IMAGEPATH/$PROGIMAGE 0 PROG.COM
  $NDCPATH P $IMAGEPATH/$PROGIMAGE 0 PROG.COM

  Launch
elif [ $TARGETENV == "sos" ]; then
  echo S-OS
  PROGIMAGE=SOSPROG.D88
  rm PROG.BIN
  mv $PROG.BIN PROG.BIN
  $HUDISKPATH -d $IMAGEPATH/$PROGIMAGE PROG.BIN
  $HUDISKPATH -a $IMAGEPATH/$PROGIMAGE PROG.BIN -r $LOADADR -g $RUNADR

  Launch
elif [ $TARGETENV == "msxrom" ]; then
  echo MSX ROM
  LaunchMSX
else
  echo NOT SUPPORTED $TARGETENV
fi

exit 0
