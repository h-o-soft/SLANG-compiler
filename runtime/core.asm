; Converted from lib/libdef/runtime.yml
; SLANG Runtime Library (new format)

; @name MULHLDE
; @param_count 2
; @function_type Machine
LD A, L
SUB E
LD A, H
SBC A, D
JR NC, .mul1
EX DE, HL
.mul1
LD B, H
LD C, L
LD HL, 0000
LD A, D
OR A
JR Z, .mul2
.mul4
RRA
RR E
JR NC, .mul3
ADD HL, BC
.mul3
SLA C
RL B
OR A
JR NZ, .mul4
.mul2
LD A, E
.mul6
RRA
JR NC, .mul5
ADD HL, BC
.mul5
SLA C
RL B
OR A
JR NZ, .mul6
RET


; @name DIVHLDE
; @calls DIVHLDE8
LD A, L
SUB E
LD A, H
SBC A, D
JR NC, .div1
EX DE, HL
LD HL, 0000
RET
.div1
INC D
DEC D
JP Z, DIVHLDE8
LD C, L
LD L, H
XOR A
LD H, A
LD B, 08
.div3
ADD A, A
SLA C
ADC HL, HL
SBC HL, DE
JR NC, .div2
ADD HL, DE
DEC A
.div2
INC A
DJNZ .div3
EX DE, HL
LD H, B
LD L, A
RET


; @name DIVHLDE8
INC E
DEC E
JR NZ, .div81
EX DE, HL
LD HL, 0000
RET
.div81
XOR A
LD B, $10
.div84
ADD HL, HL
ADC A, A
JR C, .div82
CP E
JR C, .div83
.div82
SUB E
INC L
.div83
DJNZ .div84
LD E, A
RET


; @name SDIVHLDE
; @calls NEGHL,DIVHLDE
LD A, H
XOR D
LD B, A
BIT 7, H
CALL NZ, NEGHL
EX DE, HL
BIT 7, H
CALL NZ, NEGHL
EX DE, HL
BIT 7, B
JR Z, DIVHLDE
CALL DIVHLDE
JR NEGHL


; @name MODHLDE
; @calls DIVHLDE
CALL DIVHLDE
EX DE,HL
RET


; @name SMODHLDE
; @calls NEGHL,MODHLDE
EX DE, HL
BIT 7, H
CALL NZ, NEGHL
EX DE, HL
BIT 7, H
JR Z, MODHLDE
CALL NEGHL
CALL MODHLDE


; @name NEGHL
; @calls CPLHL
DEC HL


; @name CPLHL
LD A, H
CPL
LD H, A
LD A, L
CPL
LD L, A
RET


; @name NOTHL
; @calls OPEQHLDE

; @name OPEQHL
LD A, H
OR L
LD HL, 0000
RET NZ
INC L
RET


; @name OPNEQHL
LD A, H
OR L
RET Z
LD HL, 0001
RET


; @name OPGTHLDE
; @calls OPLTHLDE
EX DE,HL


; @name OPLTHLDE
OR A
SBC HL, DE
LD HL, 0000
RET NC
INC L
RET


; @name OPLEHLDE
; @calls OPGEHLDE
EX DE,HL


; @name OPGEHLDE
OR A
SBC HL, DE
LD HL, 0000
RET C
INC L
RET


; @name OPSGTHLDE
; @calls OPSLTHLDE
EX DE,HL


; @name OPSLTHLDE
; @calls OPLTHLDE
BIT 7, H
JR NZ, .opslt1
BIT 7, D
JR Z, OPLTHLDE
LD HL, 0000
RET
.opslt1
BIT 7, D
JR NZ, OPLTHLDE
LD HL, 0001
RET


; @name OPSLEHLDE
; @calls OPSGEHLDE
EX DE,HL


; @name OPSGEHLDE
; @calls OPGEHLDE
BIT 7, H
JR NZ, .opsge1
BIT 7, D
JR Z, OPGEHLDE
LD HL, 0001
RET
.opsge1
BIT 7, D
JR NZ, OPGEHLDE
LD HL, 0000
RET


; @name SLSHIFTHLDE
; @calls LSHIFTHLDE

; @name LSHIFTHLDE
LD A, E
AND $0F
RET Z
.lshift1
ADD HL, HL
DEC A
JR NZ, .lshift1
RET


; @name RSHIFTHLDE
LD A, E
AND $0F
RET Z
.rshift1
SRL H
RR L
DEC A
JR NZ, .rshift1
RET


; @name SRSHIFTHLDE
LD A, E
AND $0F
RET Z
.srshift1
SRA H
RR L
DEC A
JR NZ, .srshift1
RET


; @name ORHLDE
LD A, L
OR E
LD L, A
LD A, H
OR D
LD H, A
RET


; @name ANDHLDE
LD A, L
AND E
LD L, A
LD A, H
AND D
LD H, A
RET


; @name XORHLDE
LD A, L
XOR E
LD L, A
LD A, H
XOR D
LD H, A
RET


; @name RBIT
; @alias BIT
; @param_count 2
; @calls RSHIFTHLDE
CALL RSHIFTHLDE
BIT 0,L
LD HL,0
RET Z
INC HL
RET


; @name RSET
; @alias SET
; @param_count 2
; @calls ORHLDE
EX DE,HL
LD A,L
AND $0F
LD HL,1
JR Z,.set1
.set2
ADD HL,HL
DEC A
JR NZ,.set2
.set1
JP ORHLDE


; @name RESET
; @param_count 2
; @calls ANDHLDE
EX DE,HL
LD A,L
AND $0F
LD HL,$FFFE
JR Z,.reset1
.reset2
SCF
ADC HL, HL
DEC A
JR NZ,.reset2
.reset1
JP ANDHLDE


; @name ABS
; @param_count 1
; @calls NEGHL
BIT 7,H
ABSNEGCALL:
CALL NZ,NEGHL
RET


; @name SGN
; @param_count 1
; @calls ABS
LD A,H
OR L
RET Z
BIT 7,H
LD HL,1
JR ABSNEGCALL
BIT 7,L
LD H,0
RET Z
DEC H
RET


; @name SEX
; @param_count 1
BIT 7,L
LD H,0
RET Z
DEC H
RET


; @name SRAND
; @param_count 1
; @calls RND
LD (RND_SEED1),HL
RET


; @name RND
; @param_count 1
; @calls MULHLDE,MODHLDE
; @works RND_SEED1:2,RND_SEED2:2
; @init_code
LD HL,$E933
LD (RND_SEED2),HL
LD A,R
LD L,A
LD (RND_SEED1),A
XOR H
LD (RND_SEED1+1),A
RET
; @end_init
PUSH HL
LD HL,(RND_SEED1)
LD B,H
LD C,L
ADD HL,HL
ADD HL,HL
INC L
ADD HL,BC
LD (RND_SEED1),HL
LD HL,(RND_SEED2)
ADD HL,HL
SBC A,A
AND %00101101
XOR L
LD L,A
LD (RND_SEED2),HL
ADD HL,BC
POP DE
LD A,D
OR E
JR NZ,.RND1
EX DE,HL
RET
.RND1
JP MODHLDE
RET


; @name VTOS
; @param_count 2
; @function_type Machine
; @calls DIVHLDE8
PUSH HL
EXX
POP HL
EXX
LD HL, $0005
ADD HL, DE
LD (HL), $00
LD B, $05
.vtos1
EXX
LD E, $0A
CALL DIVHLDE8
LD A, E
ADD A, $30
EXX
DEC HL
LD (HL), A
DJNZ .vtos1
LD B, $04
.vtos3
LD A, (HL)
CP $30
JR NZ, .vtos2
LD (HL), $20
INC HL
DJNZ .vtos3
.vtos2
RET


; @name RCALL
; @alias CALL
; @calls GETREG
PUSH IY
LD DE,.call1
PUSH DE
PUSH HL
LD A,(_AF+1)
LD BC,(_BC)
LD DE,(_DE)
LD HL,(_HL)
LD IX,(_IX)
LD IY,(_IY)
RET
.call1
PUSH HL
CALL GETREG
LD HL,0006
ADD HL,SP
POP HL
POP IY
RET


; @name GETREG
PUSH HL
LD (_IY), IY
LD (_IX), IX
LD (_HL), HL
LD (_DE), DE
LD (_BC), BC
PUSH AF
POP HL
LD (_AF), HL
LD HL, 0000
JR NC, .getreg1
INC HL
.getreg1
LD (_CARRY), HL
LD HL, 0000
JR NZ, .getreg2
INC HL
.getreg2
LD (_ZERO), HL
LD HL, 0004
ADD HL, SP
LD (_SP), HL
POP HL
RET


; @name SASC
AND	$0F
ADD	A,'0'
CP	3AH
CCF
RET	NC
ADD	A,'A'-$3A
RET


; @name MEMCPY
; hl = dst, de = source, bc = size
ex de,hl
ldir
ret


; @name MEMSET
; hl = addr, de = value, bc = count
push bc
push de

ld e,l
ld d,h
inc de

; value to (addr)
pop bc
ld (hl),c

; pop count
pop bc
dec bc

ldir
ret


; @name STRLEN
PUSH BC
LD B, 0
.COUNT_LOOP
LD A, (HL)
OR A
JR Z,.END_OF_STRING
INC HL
INC B
JR .COUNT_LOOP
.END_OF_STRING
LD L,B
LD H,0
POP BC
RET


; @name MIN
; HL = value 1
; DE = value 2
ld a,h
cp d
jr c,.HL_is_Smaller
jr nz,.DE_is_Smaller
; 上位バイトが等しい場合、下位バイトを比較
ld a,l        ; HL の下位バイトを A にロード
cp e          ; DE の下位バイトと比較
jr c,.HL_is_Smaller ; キャリーがセットされていれば HL < DE
jr .DE_is_Smaller   ; HL > DE（キャリーなし）
.HL_is_Smaller
; HL はそのまま小さい
ret           ; そのまま戻る
.DE_is_Smaller:
; 入れ替える
ex de,hl
ret


; @name MAX
ld a,h        ; HL の上位バイトを A にロード
cp d          ; DE の上位バイトと比較
jr c,.DE_is_Greater ; キャリーがセットされていれば HL < DE
jr nz,.HL_is_Greater ; キャリーがなく、上位バイトが等しくない場合 HL > DE
; 上位バイトが等しい場合、下位バイトを比較
ld a,l        ; HL の下位バイトを A にロード
cp e          ; DE の下位バイトと比較
jr c,.DE_is_Greater ; キャリーがセットされていれば HL < DE
jr .HL_is_Greater   ; HL > DE（キャリーなし）
.HL_is_Greater
; HL はそのまま大きい
ret           ; そのまま戻る
.DE_is_Greater
; 入れ替える
ex de,hl
ret


