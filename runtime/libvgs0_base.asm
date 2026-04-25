; Converted from lib/libdef/libvgs0_base.yml
; SLANG Runtime Library (new format)

; @name VGSCALLS
; @resident shared
DUMMY   EQU $0000
ADDR_OAM EQU $9000
ADDR_OAM16 EQU $9A00


; @name SLANGINIT
; @resident local
; @calls VGSWORK,VGSCALLS
im 1
di
ld hl, 09F07h
wait_vdp_standby:
ld a, (hl)
and 080h
jp z, wait_vdp_standby

; WORK ZERO CLEAR
XOR A
LD HL,__WORK__
LD DE,__WORK__+1
LD BC,__WORKEND__-__WORK__-1
LD (HL),A
LDIR

; initial BG/FG attribute
LD A,$80
LD (TXTATR),A
; BG($80) or FG($88)
; LD A,$80
LD (TXTPLANE),A

<<CALLINITIALIZER>>

LD IY,__IYWORK

call MAIN

di
halt

INFLOOP:
JP INFLOOP


; @name STOP
; @resident shared
; @param_count 0
JP INFLOOP


; @name vgs0_bank0_switch
; @resident shared
; L = bank num
ld a, l
out ($B0), a
ret


; @name vgs0_bank1_switch
; @resident shared
; L = bank num
ld a, l
out ($B1), a
ret


; @name vgs0_bank2_switch
; @resident shared
; L = bank num
ld a, l
out ($B2), a
ret


; @name vgs0_bank3_switch
; @resident shared
ld a, l
out ($B3), a
ret


; @name vgs0_rambank_switch
; @resident shared
ld a,l
out ($B4), a
ret


; @name vgs0_bank0_get
; @resident shared
in a, ($B0)
ld l, a
ld h,0
ret


; @name vgs0_bank1_get
; @resident shared
in a, ($B1)
ld l, a
ld h,0
ret


; @name vgs0_bank2_get
; @resident shared
in a, ($B2)
ld l, a
ld h,0
ret


; @name vgs0_bank3_get
; @resident shared
in a, ($B3)
ld l, a
ld h,0
ret


; @name vgs0_rambank_get
; @resident shared
in a, ($B4)
ld l, a
ld h,0
ret


; @name vgs0_wait_vsync
; @resident shared
ld hl, $9F07
wait_vblank_loop:
ld a, (hl)
and $80
jp z, wait_vblank_loop
ret


; @name vgs0_dma
; @resident shared
ld a, l
out ($C0), a
ret


; @name vgs0_memset
; @resident shared
; HL = dst
; DE = value
; BC = cnt
ld a,e

; HLとBCを交換(うーん)
ld e,l
ld d,h
ld l,c
ld h,b
ld c,e
ld b,d

; execute DMA
out ($C2), a
ret


; @name vgs0_memcpy
; @resident shared
; HL = dst
; DE = src
; BC = cnt

; HLとBCを交換(ううーん)
push hl
push bc
pop hl
pop bc
; execute DMA
out ($C3), a
ret


; @name vgs0_collision_check
; @resident shared
; HL = addr
in a, ($C4)
ld l, a
ld h,0
ret


; @name vgs0_mul
; @resident shared
; HL = val1
; DE = val2
ld h,e
; hl = h * l
ld a, $00
out ($C5), a
ret


; @name vgs0_smul
; @resident shared
; HL = val1
; DE = val2
ld h,e
; hl = h * l (signed)
ld a, $40
out ($C5), a
ret


; @name vgs0_div
; @resident shared
; HL = val1
; DE = val2
; val1 / val2
ld d,l
ld l,e
ld h,d
ld a, $01
out ($C5), a
ret


; @name vgs0_sdiv
; @resident shared
; HL = val1
; DE = val2
; val1 / val2 (signed)
ld d,l
ld l,e
ld h,d
ld a, $41
out ($C5), a
ret


; @name vgs0_mod
; @resident shared
; HL = val1
; DE = val2
; val1 % val2
ld d,l
ld l,e
ld h,d
ld a, $02
out ($C5), a
ret


; @name vgs0_mul16
; @resident shared
; HL = HL * E
; push bc
ld c,e
ld a,$80
out ($c5),a
; pop bc
RET


; @name vgs0_smul16
; @resident shared
; HL = HL * E
; push bc
ld c,e
ld a,$c0
out ($c5),a
; pop bc
RET


; @name vgs0_div16
; @resident shared
; HL = val1
; DE = val2
; val1 / val2
ld  c,e
ld a,$81
out ($C5), a
ret


; @name vgs0_sdiv16
; @resident shared
; HL = val1
; DE = val2
; val1 / val2 (signed)
ld  c,e
ld a,$82
out ($C5), a
ret


; @name vgs0_sin
; @resident shared
ld a, l
out ($C6), a
ld l, a
bit 7,a
ld h,0
jr z,.notminus
dec h
.notminus
ret


; @name vgs0_cos
; @resident shared
ld a, l
out ($C7), a
ld l, a
bit 7,a
ld h,0
jr z,.notminus
dec h
.notminus
ret


; @name vgs0_atan2
; @resident shared
; HL = yx
in a, ($C8)
ld l, a
ld h,0
ret


; @name vgs0_atan2b
; @resident shared
; vgs0_atan2b(y, x) と、書ける
; HL = y
; DE = x
ld d,l
ld l,e
ld h,d
in a, ($C8)
ld l, a
ld h,0
ret


; @name vgs0_srand8
; @resident shared
ld a, l
out ($C9), a
ret


; @name vgs0_rand8
; @resident shared
in a, ($C9)
ret


; @name vgs0_srand16
; @resident shared
ld a, l
out ($CA), a
ret


; @name vgs0_rand16
; @resident shared
in a, ($CA)
ret


; @name vgs0_noise_seed
; @resident shared
in a, ($CB)
ret


; @name vgs0_noise_limitX
; @resident shared
out ($CC), a
ret


; @name vgs0_noise_limitY
; @resident shared
out ($CD), a
ret


; @name vgs0_noise
; @resident shared
; HL = x
; DE = y
in a,($CE)
ld l,a
ret


; @name vgs0_noise_oct
; @resident shared
; HL = oct
; DE = x
; BC = y

; oct -> A
ld a,l
ld l,c
ld h,b
ex de,hl
; HL = x
; DE = y
in a, ($CF)
ld l, a
ret


; @name vgs0_joypad_get
; @resident shared
in a, ($A0)
xor $FF
ld l, a
ld h, 0
ret


; @name vgs0_bgm_play
; @resident shared
ld a, l
out ($E0), a
ret


; @name vgs0_bgm_pause
; @resident shared
ld a, 0
out ($E1), a
ret


; @name vgs0_bgm_resume
; @resident shared
ld a, 1
out ($E1), a
ret


; @name vgs0_bgm_fadeout
; @resident shared
ld a, 2
out ($E1), a
ret


; @name vgs0_se_play
; @resident shared
ld a, l
out ($F0), a
ret


; @name vgs0_se_stop
; @resident shared
ld a, l
out ($F1), a
ret


; @name vgs0_se_playing
; @resident shared
ld a, l
out ($F2), a
ld l, a
ret


; @name vgs0_save
; @resident shared
; HL = addr
; DE = size
LD c,l
ld b,h
ld l,e
ld h,d
; execute SAVE
out ($DA), a
ld l, a
ld h, 0
ret


; @name vgs0_load
; @resident shared
; HL = addr
; DE = size
LD c,l
ld b,h
ld l,e
ld h,d
; execute SAVE
in a,($DA)
ld l, a
ld h, 0
ret


; @name vgs0_oam_set16
; @resident shared
; (ret), h, w, ptn, attr, y, x, num の順で入っている
LD HL,12+2
ADD HL,SP

; DE = num
LD E,(HL)
INC HL
LD D,(HL)

EX DE,HL
ADD HL,HL
ADD HL,HL   ; HL = HL * 4 (OAM16のオフセットにする)
PUSH HL
LD DE,ADDR_OAM16
ADD HL,DE
; OAM16アドレスをDEに退避
EX DE,HL

; y位置アドレスをDEに得る
LD HL,8+2+2   ; PUSH HLのぶんを足す
ADD HL,SP
; OAM16アドレスをHLに戻す
EX DE,HL

; y
LD A,(DE)
LD (HL),A
INC HL
INC DE
LD A,(DE)
LD (HL),A
INC HL
INC DE

; x
LD A,(DE)
LD (HL),A
INC HL
INC DE
LD A,(DE)
LD (HL),A
DEC DE
DEC DE
DEC DE
DEC DE
DEC DE
DEC DE
DEC DE

; HLをOAM16からOAMに切り替える
POP HL
PUSH DE
ADD HL,HL
LD DE,ADDR_OAM+2  ; ptnのオフセット
ADD HL,DE
POP DE

; ptn
LD A,(DE)
LD (HL),A
INC HL
INC DE
INC DE

; attr
LD A,(DE)
LD (HL),A
INC HL
DEC DE
DEC DE
DEC DE
DEC DE
DEC DE
DEC DE

; h
LD A,(DE)
LD (HL),A
INC HL
INC DE
INC DE

; w
LD A,(DE)
LD (HL),A
INC HL

; bank
XOR A
LD (HL),A

RET


; @name vgs0_oam_set
; @resident shared
; (ret), h, w, ptn, attr, y, x, num の順で入っている
LD HL,12+2
ADD HL,SP

; DE = num
LD E,(HL)
INC HL
LD D,(HL)

EX DE,HL
; NUM << 3
LD DE,3
CALL LSHIFTHLDE
LD DE,ADDR_OAM
ADD HL,DE
; OAMアドレスをDEに退避
EX DE,HL

; y位置アドレスをDEに得る
LD HL,8+2
ADD HL,SP
; OAMアドレスをHLに戻す
EX DE,HL

; なぜ関数の引数とOAMの並びがバラバラなんだろう?(INC/DECが見苦しい事に……)
; y
LD A,(DE)
LD (HL),A
INC HL
INC DE
INC DE

; x
LD A,(DE)
LD (HL),A
INC HL
DEC DE
DEC DE
DEC DE
DEC DE
DEC DE
DEC DE

; ptn
LD A,(DE)
LD (HL),A
INC HL
INC DE
INC DE

; attr
LD A,(DE)
LD (HL),A
INC HL
DEC DE
DEC DE
DEC DE
DEC DE
DEC DE
DEC DE

; h
LD A,(DE)
LD (HL),A
INC HL
INC DE
INC DE

; w
LD A,(DE)
LD (HL),A
INC HL

; bank
XOR A
LD (HL),A

RET

; ; IY使う版(見やすいが若干遅い)
; PUSH IY
; ; (iy),(ret), h, w, ptn, attr, y, x, num の順で入っている

; ; IYにHのアドレスを得ておく
; LD HL,4
; ADD HL,SP
; PUSH HL
; POP IY

; LD HL,12+2
; ADD HL,SP

; ; DE = num
; LD E,(HL)
; INC HL
; LD D,(HL)

; EX DE,HL
; ; NUM << 3
; LD DE,3
; CALL LSHIFTHLDE
; LD DE,ADDR_OAM
; ADD HL,DE

; ; y
; LD A,(IY+8)
; LD (HL),A
; INC HL

; ; x
; LD A,(IY+10)
; LD (HL),A
; INC HL

; ; ptn
; LD A,(IY+4)
; LD (HL),A
; INC HL

; ; attr
; LD A,(IY+6)
; LD (HL),A
; INC HL

; ; h
; LD A,(IY+0)
; LD (HL),A
; INC HL

; ; w
; LD A,(IY+2)
; LD (HL),A
; INC HL

; ; bank
; LD A,0
; LD (HL),A

; POP IY
; RET


; @name vgs0_oam_move
; @resident shared
; HL = num
; DE = X
; BC = Y

; NUM << 3
ADD HL,HL
ADD HL,HL
ADD HL,HL
LD A,$90  ; OAM/H
OR H
LD H,A

; y
LD (HL),C
INC HL

; x
LD (HL),E

RET


; @name vgs0_debug
; @resident shared
.loop
ld a, (hl)
out ($00), a
and a
ret z
inc hl
jr .loop
ret


; @name VGSWORK
; @resident shared
; @param_count 0
; @works LOCX:1,LOCY:1,TXTATR:1,TXTPLANE:1,WORK10:12
;


