	ORG	$0100
ENV_TYPE EQU 0 
OS_TYPE EQU 0
LD HL,($0001)
LD (WBOOTBK),HL
; WORK ZERO CLEAR
XOR A
LD HL,__WORK__
LD DE,__WORK__+1
LD BC,__WORKEND__-__WORK__-1
LD (HL),A
LDIR
 CALL RUNTIME_INIT
LD HL,F0C0H
LD (_CTCVEC),HL
LD IY,__IYWORK
CALL MAIN
JP 0

MAIN:
; function MAIN
	PUSH	IY
	LD	HL,$0000
	LD	(_V_Y),HL
_L0:
	LD	HL,$0000
	LD	(_V_X),HL
_L3:
	LD	HL,(_V_X)
	LD	DE,$0027
	OR	A
	SBC	HL,DE
	CALL	i16tof24
	LD	(_V_FX),HL
	LD	(_V_FX+2),A
	LD	HL,(_V_Y)
	LD	DE,$000C
	OR	A
	SBC	HL,DE
	CALL	i16tof24
	LD	(_V_FY),HL
	LD	(_V_FY+2),A
	LD	HL,(_V_FX)
	LD	A,(_V_FX+2)
	PUSH	AF
	PUSH	HL
	LD	HL,(_FC0)
	LD	A,(_FC0+2)
	LD	C,A
	EX	DE,HL
	POP	HL
	POP	AF
	CALL	f24mul
	LD	(_V_CA),HL
	LD	(_V_CA+2),A
	LD	HL,(_V_FY)
	LD	A,(_V_FY+2)
	PUSH	AF
	PUSH	HL
	LD	HL,(_FC1)
	LD	A,(_FC1+2)
	LD	C,A
	EX	DE,HL
	POP	HL
	POP	AF
	CALL	f24mul
	LD	(_V_CB),HL
	LD	(_V_CB+2),A
	LD	HL,(_V_CA)
	LD	A,(_V_CA+2)
	LD	(_V_A),HL
	LD	(_V_A+2),A
	LD	HL,(_V_CB)
	LD	A,(_V_CB+2)
	LD	(_V_B),HL
	LD	(_V_B+2),A
	LD	HL,$0000
	LD	(_V_I),HL
_L6:
	LD	HL,(_V_A)
	LD	A,(_V_A+2)
	PUSH	AF
	PUSH	HL
	LD	HL,(_V_A)
	LD	A,(_V_A+2)
	LD	C,A
	EX	DE,HL
	POP	HL
	POP	AF
	CALL	f24mul
	PUSH	AF
	PUSH	HL
	LD	HL,(_V_B)
	LD	A,(_V_B+2)
	PUSH	AF
	PUSH	HL
	LD	HL,(_V_B)
	LD	A,(_V_B+2)
	LD	C,A
	EX	DE,HL
	POP	HL
	POP	AF
	CALL	f24mul
	LD	C,A
	EX	DE,HL
	POP	HL
	POP	AF
	CALL	f24sub
	PUSH	AF
	PUSH	HL
	LD	HL,(_V_CA)
	LD	A,(_V_CA+2)
	LD	C,A
	EX	DE,HL
	POP	HL
	POP	AF
	CALL	f24add
	LD	(_V_T),HL
	LD	(_V_T+2),A
	LD	HL,$0002
	CALL	i16tof24
	PUSH	AF
	PUSH	HL
	LD	HL,(_V_A)
	LD	A,(_V_A+2)
	LD	C,A
	EX	DE,HL
	POP	HL
	POP	AF
	CALL	f24mul
	PUSH	AF
	PUSH	HL
	LD	HL,(_V_B)
	LD	A,(_V_B+2)
	LD	C,A
	EX	DE,HL
	POP	HL
	POP	AF
	CALL	f24mul
	PUSH	AF
	PUSH	HL
	LD	HL,(_V_CB)
	LD	A,(_V_CB+2)
	LD	C,A
	EX	DE,HL
	POP	HL
	POP	AF
	CALL	f24add
	LD	(_V_B),HL
	LD	(_V_B+2),A
	LD	HL,(_V_T)
	LD	A,(_V_T+2)
	LD	(_V_A),HL
	LD	(_V_A+2),A
	LD	HL,(_V_A)
	LD	A,(_V_A+2)
	PUSH	AF
	PUSH	HL
	LD	HL,(_V_A)
	LD	A,(_V_A+2)
	LD	C,A
	EX	DE,HL
	POP	HL
	POP	AF
	CALL	f24mul
	PUSH	AF
	PUSH	HL
	LD	HL,(_V_B)
	LD	A,(_V_B+2)
	PUSH	AF
	PUSH	HL
	LD	HL,(_V_B)
	LD	A,(_V_B+2)
	LD	C,A
	EX	DE,HL
	POP	HL
	POP	AF
	CALL	f24mul
	LD	C,A
	EX	DE,HL
	POP	HL
	POP	AF
	CALL	f24add
	PUSH	AF
	PUSH	HL
	LD	HL,(_FC2)
	LD	A,(_FC2+2)
	LD	C,A
	EX	DE,HL
	POP	HL
	POP	AF
	EX	DE,HL
	LD	B,A
	LD	A,C
	LD	C,B
	CALL	f24cmp
	LD	HL,$0000
	JR	NC,$+3
	INC	HL
; if t45 == 0 goto _L9
	LD	A,H
	OR	L
	JP	Z,_L9
	JP	_L8
_L9:
_L7:
	LD	HL,(_V_I)
	INC	HL
	LD	(_V_I),HL
	LD	DE,$000F
	CALL	OPSLEHLDE
; if t50 != 0 goto _L6
	LD	A,H
	OR	L
	JP	NZ,_L6
_L8:
	LD	HL,(_V_I)
	LD	DE,$000F
	EX	DE,HL
	OR	A
	SBC	HL,DE
	JP	C,_L11
	LD	HL,(_V_I)
	LD	DE,$0009
	EX	DE,HL
	OR	A
	SBC	HL,DE
	JP	NC,_L12
	LD	HL,(_V_I)
	LD	DE,$0007
	ADD	HL,DE
	LD	(_V_I),HL
_L12:
	LD	HL,$0030
	LD	DE,(_V_I)
	ADD	HL,DE
	CALL	PCHR
	JP	_L10
_L11:
	LD	HL,_S0
	CALL	PMSX
_L10:
_L4:
	LD	HL,(_V_X)
	INC	HL
	LD	(_V_X),HL
	LD	DE,$004E
	CALL	OPSLEHLDE
; if t68 != 0 goto _L3
	LD	A,H
	OR	L
	JP	NZ,_L3
_L5:
	CALL	PCRONE
_L1:
	LD	HL,(_V_Y)
	INC	HL
	LD	(_V_Y),HL
	LD	DE,$0018
	CALL	OPSLEHLDE
; if t73 != 0 goto _L0
	LD	A,H
	OR	L
	JP	NZ,_L0
_L2:
_MAIN_EXIT:
	POP	IY
	RET


; === String Table ===
_S0:
	DB	" ",0
_FC0:
	DB	$32,$77,$3A
_FC1:
	DB	$52,$55,$3B
_FC2:
	DB	$00,$00,$41

RUNTIME_INIT:
	RET

; === Runtime Functions ===
OPGEHLDE:
OR A
SBC HL, DE
LD HL, 0000
RET C
INC L
RET

OPSLEHLDE:
EX DE,HL

OPSGEHLDE:
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

mul16:
;This was made by Runer112
;Tested by jacobly
;BC*DE --> DEHL
; ~544.887cc as calculated in jacobly's test
;min: 214cc  (DE = 1)
;max: 667cc
;avg: 544.4507883cc   however, deferring to jacobly's result as mine may have math issues ?
;177 bytes
ld a,d
ld d,0
ld h,b
ld l,c
add a,a
jr c,Mul_BC_DE_DEHL_Bit14
add a,a
jr c,Mul_BC_DE_DEHL_Bit13
add a,a
jr c,Mul_BC_DE_DEHL_Bit12
add a,a
jr c,Mul_BC_DE_DEHL_Bit11
add a,a
jr c,Mul_BC_DE_DEHL_Bit10
add a,a
jr c,Mul_BC_DE_DEHL_Bit9
add a,a
jr c,Mul_BC_DE_DEHL_Bit8
add a,a
jr c,Mul_BC_DE_DEHL_Bit7
ld a,e
and %11111110
add a,a
jr c,Mul_BC_DE_DEHL_Bit6
add a,a
jr c,Mul_BC_DE_DEHL_Bit5
add a,a
jr c,Mul_BC_DE_DEHL_Bit4
add a,a
jr c,Mul_BC_DE_DEHL_Bit3
add a,a
jr c,Mul_BC_DE_DEHL_Bit2
add a,a
jr c,Mul_BC_DE_DEHL_Bit1
add a,a
jr c,Mul_BC_DE_DEHL_Bit0
rr e
ret c
ld h,d
ld l,e
ret
Mul_BC_DE_DEHL_Bit14:
add hl,hl
adc a,a
jr nc,Mul_BC_DE_DEHL_Bit13
add hl,bc
adc a,d
Mul_BC_DE_DEHL_Bit13:
add hl,hl
adc a,a
jr nc,Mul_BC_DE_DEHL_Bit12
add hl,bc
adc a,d
Mul_BC_DE_DEHL_Bit12:
add hl,hl
adc a,a
jr nc,Mul_BC_DE_DEHL_Bit11
add hl,bc
adc a,d
Mul_BC_DE_DEHL_Bit11:
add hl,hl
adc a,a
jr nc,Mul_BC_DE_DEHL_Bit10
add hl,bc
adc a,d
Mul_BC_DE_DEHL_Bit10:
add hl,hl
adc a,a
jr nc,Mul_BC_DE_DEHL_Bit9
add hl,bc
adc a,d
Mul_BC_DE_DEHL_Bit9:
add hl,hl
adc a,a
jr nc,Mul_BC_DE_DEHL_Bit8
add hl,bc
adc a,d
Mul_BC_DE_DEHL_Bit8:
add hl,hl
adc a,a
jr nc,Mul_BC_DE_DEHL_Bit7
add hl,bc
adc a,d
Mul_BC_DE_DEHL_Bit7:
ld d,a
ld a,e
and %11111110
add hl,hl
adc a,a
jr nc,Mul_BC_DE_DEHL_Bit6
add hl,bc
adc a,0
Mul_BC_DE_DEHL_Bit6:
add hl,hl
adc a,a
jr nc,Mul_BC_DE_DEHL_Bit5
add hl,bc
adc a,0
Mul_BC_DE_DEHL_Bit5:
add hl,hl
adc a,a
jr nc,Mul_BC_DE_DEHL_Bit4
add hl,bc
adc a,0
Mul_BC_DE_DEHL_Bit4:
add hl,hl
adc a,a
jr nc,Mul_BC_DE_DEHL_Bit3
add hl,bc
adc a,0
Mul_BC_DE_DEHL_Bit3:
add hl,hl
adc a,a
jr nc,Mul_BC_DE_DEHL_Bit2
add hl,bc
adc a,0
Mul_BC_DE_DEHL_Bit2:
add hl,hl
adc a,a
jr nc,Mul_BC_DE_DEHL_Bit1
add hl,bc
adc a,0
Mul_BC_DE_DEHL_Bit1:
add hl,hl
adc a,a
jr nc,Mul_BC_DE_DEHL_Bit0
add hl,bc
adc a,0
Mul_BC_DE_DEHL_Bit0:
add hl,hl
adc a,a
jr c,Mul_BC_DE_DEHL_FunkyCarry
rr e
ld e,a
ret nc
add hl,bc
ret nc
inc e
ret nz
inc d
ret
Mul_BC_DE_DEHL_FunkyCarry:
inc d
rr e
ld e,a
ret nc
add hl,bc
ret nc
inc e
ret

f24sub:
;AHL - CDE ==> AHL
;Destroys BC,DE
;
ld b,a
ld a,c
xor $80
ld c,a
ld a,b
jp f24add
f24rsub:
;-AHL + CDE ==> AHL
;Destroys BC,DE
;
xor $80

f24add:
;AHL + CDE ==> AHL
;Destroys BC,DE
;
;save A
ld b,a
;check for special values
and $7F
#if exists return_CDE
jp z,return_CDE
#else
jr nz,f24add1
return_CDE:
ld a,c
ex de,hl
ret
f24add1:
#endif
inc a
jp m,f24add_op1_inf_nan
ld a,c
;check for special values
and $7F
jp z,return_exp_b
inc a
jp m,return_CDE
ld a,b
xor c
jp m,f24add_subtract
;we need to add
call f24add_reorder
jr z,f24add_add_same_exp
ret nc
push bc
call rshift_1DE
sla b
adc hl,de
;if carry is reset, then we are all good :)
pop de
ld a,d
ret nc
;otherwise, we need to increment the sign and see if it overflows to inf
and $7F
cp $7E
ld a,d
jr z,f24_return_inf
inc a
;we also need to shift a 0 down into the HL
srl h
rr l
ret nc
inc hl
ret
f24add_add_same_exp:
ld a,b
and $7F
cp $7E
ld a,b
jr z,f24_return_inf
inc a
add hl,de
rr h
rr l
ret nc
inc l
ret nz
inc h
ret nz
inc a
ret
f24_return_inf:
or %01111111
ld hl,0
ret
f24add_subtract:
call f24add_reorder
jr z,f24add_subtract_same_exp
ret nc
push bc
call rshift_1DE
sub c
ld c,a
ld a,0
sbc a,b
ld b,a
sbc hl,de
;if carry is not set, then we are all good :)
pop de
ld a,d
ret nc
;otherwise, the implicit bit is set to 0, so we need to renormalize
normalize_D_HLBC:
;D is the sign+exponent
;HLBC is the significand
;returns AHLBC
;make sure HLBC is not 0
ld a,h
or l
or b
or c
ret z
ld a,d
normalize_D_HLBC_nonzero:
;save the sign
add a,a
push af
rrca
.f24add3
dec a
jr z,.f24add2
sla c
rl b
adc hl,hl
jp nc,.f24add3
;now round
sla c
ld bc,0
adc hl,bc
;if carry is set, then the implicit bit is 2, and the rest of the exponent is 0
;so we can just increment A and keep HL as 0
adc a,b
add a,a
.f24add2
ld d,a
pop af
ld a,d
rra
ret
f24add_subtract_same_exp:
;subtract the significands
ld a,b
;  or a
sbc hl,de
;if zero, then the result is zero, but we'll keep the same sign
jr nz,$+5
and %10000000
ret
;if the carry flag is set, then we need to change the sign of the output
;and negate the significand. if reset, then we still need to normalize and whatnot
ld bc,0
jr nc,normalize_D_HLBC_nonzero
xor $80
ld d,a
xor a
sub l
ld l,a
sbc a,a
sub h
ld h,a
ld a,d
jr normalize_D_HLBC_nonzero
f24add_reorder:
xor c
rlc c
rla
;Want to rearrange so that A-C>=0
sub c
ret z
jr nc,.f24add4
neg
;A is the difference in exponents
rrc c
ld b,c
ex de,hl
.f24add4
;A is how many bits to shift DE right
;B is the sign+exponent of the result
or a
rra
cp 18
ret c
return_exp_b:
ld a,b
ret
f24add_op1_inf_nan:
ld a,h
or l
jr nz,return_exp_b
;so op1 is +inf or -inf
;If op2 is finite, then just return op1
or c
jr z,return_exp_b
inc a
add a,a
jr nz,return_exp_b
;if op2 is NaN, return NaN
ld a,d
or e
ld a,c
jr nz,.f24add5
;so |op1| and |op2| are inf
;if they have the same sign, fine, else return NaN
cp b
ret z
.f24add5
dec hl
ret
rshift_1DE:
ld bc,0
scf
.f24add6
rr d
rr e
rr b
rr c
dec a
jr nz,.f24add6
ret

f24mul:
;AHL * CDE ==> AHL
;Destroys BC,DE
;
;put the output sign in the top bit of A
ld b,a
ld a,c
and $80
xor b
;check for special values
;NaN*x ==> NaN
;0*fin ==> 0
;0*inf ==> NaN
;inf*fin ==> inf
;inf*inf ==> inf
;save A
ld b,a
and $7F
jr z,f24mul_op1_0
inc a
jp m,f24mul_op1_inf_nan
;so the first value is finite
ld a,c
and $7F
ld c,a
ret z
inc a
#if exists return_CDE
jp m,return_CDE
#else
jp p,f24mul_1
return_CDE:
ld a,c
ex de,hl
ret
f24mul_1:
#endif
;upper bit of B is the output sign
;first approximation of the exponent is
; (B&7F) + (C&7F) - 63
ld a,b
and $7F
add a,c
sub 63
jr nc,$+4
xor a     ;underflowed, so return 0
ret
cp $7F
jr c,f24mul_2
f24mul_return_inf:
ld a,b
or %01111111
ld hl,0   ;overflow so return inf
ret
f24mul_2:
xor b
and $7F
xor b
f24mul_significand:
;save the exponent
push af
;now compute (1.HL * 1.DE)
; = (1+.HL)(1+.DE)
; = 1+.HL+.DE+.HL*.DE
ld b,h
ld c,l
push hl
push de
call mul16
;result is .DEHL
;we can discard HL, but first round
xor a
sla h
ex de,hl
pop bc
adc hl,bc
adc a,0
pop bc
add hl,bc
adc a,0
rra
;now 1+A.HL is the significand
pop bc    ;B is the exponent
ld a,b
ret z
ccf
rr h
rr l
inc a
inc a
add a,a
jr z,f24mul_return_inf
rra
dec a
ret
f24mul_op1_0:
ld a,c
and $7F
ret z
inc a
jp m,f24mul_return_NaN
xor a
ret
f24mul_op1_inf_nan:
ld a,h
or l
ld a,b
ret nz    ;NaN
;inf*0 is NaN
ld a,c
and $7F
jr nz,f24mul_3
f24mul_return_NaN:
dec a   ;inf*0
ld h,a  ;=
ld l,a  ;NaN
ret
f24mul_3:
inc a
jp m,f24mul_4
ld a,b    ;returning inf
ret
f24mul_4:
;op1 is inf
;op2 is is either NaN or inf
; inf*NaN ==> NaN
; inf*inf ==> inf
;so just return op2's significand
ld a,c
ex de,hl
ret

i16tof24:
;Inputs:
;   HL holds a 16-bit signed integer, (-32768 to 32767)
;Outputs:
;   Converts to an f24 float in AHL
;   returns z flag set if zero, nz otherwise :)
bit 7,h
jr z,i16tof24_pos
xor a
sub l
ld l,a
sbc a,a
sub h
ld h,a
ld b,$BF+16
db $11     ;start of `ld de,**`, eats the next two bytes

u16tof24:
i16tof24_pos:
ld b,$3F+16    ;Initial exponent and sign
;Check if HL is 0, if so return AHL == 0x000000
ld a,h
or l
ret z
; HL is non-zero
; shift HL left until there is an overflow (the implicit bit)
; meanwhile, decrement B, the exponent each iteration
dec b
add hl,hl
jr nc,$-2
ld a,b
ret

f24cmp:
;returns the flags for float AHL minus float CDE
;   AHL >= CDE, nc
;   AHL < CDE,  c
;   AHL == CDE, z (and nc)
;
;Note:
;   This allows some wiggle room in the bottom two bits. For example, if the two
;   exponents are the same and the two significands differ by at most 3, they are
;   considered equal.
;
;Note:
;   NaN is a special case. This routines returns that NaN<x for all x.
;   This gives the weird property NaN<NaN, and when sorting, NaN will be the
;   smallest element.
;
;check for inf and NaN
ld b,a
and $7F
inc a
jp m,f24cmp_special
ld a,b
;save the old exponent
push af
call f24sub
;restore the old exponent
pop bc
; if 0, return equal
xor 80h
ret z
xor 80h
ret z
;if the difference was only in the bottom two bits, we'll call it good
;check if (B&7F)-(A&7F) >= 15
;check if (B&7F) > 14 + (A&7F)
ld c,a    ;new exponent, need to save the sign for later comparison
res 7,b
and $7F
add a,14
sub b
jr nc,$+4
xor a
ret
;otherwise, not equal, so let's return the sign in c and nz
ld a,c
return_nz_sign_a:
or $7F
add a,a
ret
f24cmp_special:
ld a,h
or l
ccf
ret nz
;so the first op is inf
;if second of is finite, return the sign of B in carry and nz
ld a,c
and $7F
inc a
ld a,b
jp p,return_nz_sign_a
;second op is either NaN or inf
ld a,d
or e
ret nz
; op1 op2 result
; 7F  7F  z, nc
; 7F  FF  nz,nc
; FF  7F  nz,c
; FF  FF  z, nc
ld a,c
cp b
ret

LSXCALLS:
BDOS EQU $0005
PRNOUT EQU $05
INPOUT EQU $06
DIRIO EQU $06
DIRIN	EQU	$07
GETS EQU $0A
CONST	EQU	$0B
lSCRN EQU $EFD0
lLOC EQU $EFD6
FCB1	EQU	005CH
FCB2	EQU	006CH
DTA1	EQU	0080H

sWORK:

PCRONE:
LD HL,1

PCR:
EX DE,HL
LD HL,$0D0A
JR PSTR2

PSTR2:
.pstr1
LD A,D
OR E
RET Z
CALL PCHR
DEC DE
JR .pstr1

PCHR:
LD A, H
CALL PRT
LD A, L
JR PRT

PRT:
PUSH HL
PUSH DE
PUSH BC
PUSH IY
LD E,A
LD C,$06
CALL $0005
POP IY
POP BC
POP DE
POP HL
RET
WORK10:
DB  "12345",0
DS  4

PMSX:
LD B, 00

PMSX1:
LD A, (HL)
CP B
RET Z
CALL PRT
INC HL
JR PMSX1

PMSG:
LD B, $0D
JR PMSX1

SLANG_PROG_END:

; ; Variables (works)
__WORK__:
_V_X EQU (__WORK__ + 0)
_V_Y EQU (__WORK__ + 2)
_V_I EQU (__WORK__ + 4)
_V_VAL EQU (__WORK__ + 6)
_V_CA EQU (__WORK__ + 8)
_V_CB EQU (__WORK__ + 11)
_V_FX EQU (__WORK__ + 14)
_V_FY EQU (__WORK__ + 17)
_V_A EQU (__WORK__ + 20)
_V_B EQU (__WORK__ + 23)
_V_T EQU (__WORK__ + 26)
_BC EQU (__WORK__ + 29)
_DE EQU (__WORK__ + 31)
_HL EQU (__WORK__ + 33)
_IX EQU (__WORK__ + 35)
_IY EQU (__WORK__ + 37)
_AF EQU (__WORK__ + 39)
_CARRY EQU (__WORK__ + 41)
_ZERO EQU (__WORK__ + 43)
_SP EQU (__WORK__ + 45)
_A EQU (_AF + 1)
sCRTCD EQU (__WORK__ + 47)
sXYADR EQU (__WORK__ + 48)
sKBFAD EQU (__WORK__ + 50)
sKBFAD0 EQU (__WORK__ + 178)
sKBFAD1 EQU (__WORK__ + 179)
sKBFADX EQU (__WORK__ + 180)
sPRBF EQU (__WORK__ + 261)
sSUBPS EQU (__WORK__ + 341)
sSUBBF EQU (__WORK__ + 343)
sSPBK EQU (__WORK__ + 599)
WBOOTBK EQU (__WORK__ + 601)
_CTCVEC EQU (__WORK__ + 603)
__IYWORK EQU (__WORK__ + 605)
WORKEND EQU (__WORK__ + 861)

__WORKEND__ EQU (__WORK__ + 861)