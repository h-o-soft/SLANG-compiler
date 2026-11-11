; SLANG Runtime Library (new format)
; 素の ZX Spectrum (zxs) の部品。zxs.env の variants: [__NOROM, __ZXS] で、zxn の部品の X の中身を
; X__NOROM の後で X__ZXS のものに差し替える (名前と出力の順は X のまま)。ほかの env では、どこからも呼ばれず出力に入らない。


; @name PRT__ZXS
; @resident shared
; @param_count 1
; @calls ZXNWORK
; PRT と同じ動き。画面の番地は素の Z80 で、字の形は 48 BASIC の ROM の 3D00H から (BASIC の読み込み口から USR で入ったときは、48K・128K・+2A とも 0000H に 48 BASIC の ROM がある)
PUSH HL
; PSTR は回数を DE に持って呼ぶので、DE・BC も残す
PUSH DE
PUSH BC
CP $0D
JR Z,.NEXTLINE

; 字の形の番地 DE = 3D00H + (字 - 32) * 8
SUB 32
LD L,A
LD H,0
ADD HL,HL
ADD HL,HL
ADD HL,HL
LD DE,$3D00
ADD HL,DE
EX DE,HL
; 画面の番地 HL: H = 40H | (行 AND 18H)、L = ((行 AND 7) << 5) | 桁
LD A,(LOCY)
LD C,A
AND $18
OR $40
LD H,A
LD A,C
AND $07
RRCA
RRCA
RRCA
LD C,A
LD A,(LOCX)
OR C
LD L,A
LD B,8
.drawfont
LD A,(DE)
LD (HL),A
INC DE
INC H
DJNZ .drawfont

LD A,(LOCX)
INC A
LD (LOCX),A
CP $20
JR NZ,.NONEXT
LD A,0
LD (LOCX),A
LD A,(LOCY)
INC A
LD (LOCY),A
.NONEXT
POP BC
POP DE
POP HL
RET

.NEXTLINE
LD A,(LOCY)
INC A
LD (LOCY),A
XOR A
LD (LOCX),A
POP BC
POP DE
POP HL
RET


; @name SLANGINIT__ZXS
; @resident local
; @calls ZXNWORK
; SLANGINIT と同じこと (DI、SP、WORK を 0、初期化、IY、EI、MAIN) に加えて、素の Z80 の IM 2 にする。
; ROM の IM 1 (0038H) は使えない: ROM の割り込みの処理は IY を ROM の変数 (5C3AH) と思って (IY+1) を書き替えるが、slangc の IY はローカル変数の場所。
; 割り込みのときのデータの線の値は決まらないので、FE00H〜FF00H (257 バイト) を FDH にし、FDFDH〜FDFFH に EI・RETI を置く (FD00H〜FFFFH は env の置き場の外)
di

; SP の初め: __STACKTOP (env の stack_top、ソースの CONST __STACKTOP で変えられる。zxs の既定 $FD00)
LD SP,__STACKTOP

; WORK ZERO CLEAR
XOR A
LD HL,__WORK__
LD DE,__WORK__+1
LD BC,__WORKEND__-__WORK__-1
LD (HL),A
LDIR

<<CALLINITIALIZER>>

LD IY,__IYWORK

LD HL,$FE00
LD DE,$FE01
LD BC,256
LD (HL),$FD
LDIR
LD A,$FB            ; FDFDH: EI
LD ($FDFD),A
LD HL,$4DED         ; FDFEH: RETI (ED 4D)
LD ($FDFE),HL
LD A,$FE
LD I,A
IM 2

ei
call MAIN

di
halt

INFLOOP:
JP INFLOOP
