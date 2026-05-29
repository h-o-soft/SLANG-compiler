; Converted from lib/libdef/libx1_base.yml
; SLANG Runtime Library (new format)

; @name VSYNC_CHECK
; @resident shared
; @works LASTVSYNCFLAG:1,VSYNCCOUNTER:1
LD A,1AH
IN A,(01H)

LD HL,LASTVSYNCFLAG
XOR (HL)
RET P
XOR (HL)
LD (HL),A
RET M
; LD HL,VSYNCCOUNTER
INC HL
INC (HL)

JP !VSYNC_PROC


; @name VSYNC
; @resident shared
; @calls VSYNC_CHECK
; HL = WAIT COUNT
VSYNC_LOOP:
LD A,(VSYNCCOUNTER)
CP L
JP NC,VSYNC_OVER
PUSH HL
CALL VSYNC_CHECK
POP HL
JP VSYNC_LOOP

VSYNC_OVER:
XOR A
LD (VSYNCCOUNTER),A

RET


; @name VSYNC1
; @resident shared
; VSYNC
LD BC,$1a01
.LP1
DB $ED,$70  ; IN F,(C)
JP P,.LP1
DI
.LP2
DB $ED,$70
JP M,.LP2
EI
RET


; @name SETUPCTC
; @resident shared
; @works CTCADR:2
PUSH	BC
LD	DE,04703H
INICTC1:
INC	C
OUT	(C),D
DB	0EDH,071H	;OUT (C),0	Z80未定義命令
DEC	E
JR	NZ,INICTC1
POP	BC

LD	DE,007FAH
OUT	(C),D
OUT	(C),E
IN	A,(C)
CP	E
RET	NZ
OUT	(C),D
OUT	(C),D
IN	A,(C)
CP	D
RET	NZ
; INC	C
; INC	C
LD	(CTCADR),BC
RET


; @name X1_CTC_PORT
; @param_count 0
; @calls SEARCHCTC
; CTC port (= _CTC convention、 SEARCHCTC が +2 した値) を return、 unavailable で 0
; - OS_TYPE == 0 (lsx/x1): LSX-Dodgers 1.62c の _CTC 固定 addr $EE8C
; - OS_TYPE == 1 (sosx1):  SLANGINIT で SEARCHCTC 済、 _CTC namespace
; - OS_TYPE == 4 (x1native): 本 helper 内で SEARCHCTC、 _CTC namespace
; 注: @calls SEARCHCTC は x1native branch 専用、 他 env では未参照 (= ghost dependency)
#IF NAME_SPACE_DEFAULT.OS_TYPE == 0
LD HL,($EE8C)
#ELIF NAME_SPACE_DEFAULT.OS_TYPE == 1
LD HL,(NAME_SPACE_DEFAULT._CTC)
#ELIF NAME_SPACE_DEFAULT.OS_TYPE == 4
CALL NAME_SPACE_DEFAULT.SEARCHCTC
LD HL,(NAME_SPACE_DEFAULT._CTC)
#ELSE
LD HL,0
#ENDIF
RET


; @name X1_CTC_VEC
; @param_count 0
; CTC vector base (= IM2 vector table 先頭 address) を return、 unavailable で 0。
; 全 OS_TYPE で _CTCVEC work 変数を参照 (= LSX は liblsx_base SLANGINIT、 sosx1 は
; libsosx1_base SLANGINIT、 x1native は SETUP_ISR_AREA がそれぞれ設定済)。
; driver Init は base + 2 (= CTC1 slot) に ISR addr を書く (= 既存 libx1_psg.asm と同じ)。
#IF NAME_SPACE_DEFAULT.OS_TYPE == 0
LD HL,(NAME_SPACE_DEFAULT._CTCVEC)
#ELIF NAME_SPACE_DEFAULT.OS_TYPE == 1
LD HL,(NAME_SPACE_DEFAULT._CTCVEC)
#ELIF NAME_SPACE_DEFAULT.OS_TYPE == 4
LD HL,(NAME_SPACE_DEFAULT._CTCVEC)
#ELSE
LD HL,0
#ENDIF
RET


