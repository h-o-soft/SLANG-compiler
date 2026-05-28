#!/bin/sh
AILZ80ASM examples/X1LINE.ASM -f
cp examples/X1LINE.BIN examples/PROG.COM
ndc D images/LSXPROG.d88 0 PROG.COM
ndc P images/LSXPROG.d88 0 examples/PROG.COM
wine ~/Emus/X1/x1.exe images/LSXPROG.d88

