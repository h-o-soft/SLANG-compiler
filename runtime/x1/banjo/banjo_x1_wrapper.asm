; ====================================================================================================
; banjo_x1_wrapper.asm — banjo Core への X1 用 jump table + ABI 変換 + system init
;
; banjo Core は固定 offset jump table を持たず entry が label 直 call なので、 SLANG 中間層
; (libx1_banjo.asm) が `JP BANJO_BASE + offset` で叩けるよう、 driver bin 先頭に固定 jump table を
; 被せる (Arkos wrapper と同思想)。 本 file は banjo_driver_x1.asm の `.ORG 0` 直後に include され、
; jump table が driver bin の先頭 (= BANJO_BASE) に来る前提。
;
; SLANG MACHINE ABI は HL(1st)/DE(2nd) だが banjo native は banjo_init(A=ch数,L=chip) /
; banjo_play_song(HL=曲,D=loop) なので、 ここで詰め替える。
; ====================================================================================================

; --- driver build パラメータ (Makefile -D で上書き可) ---
.ifndef BANJO_MAX_CHANNELS
.define BANJO_MAX_CHANNELS 8        ; OPM=8 / AY=3 / AY+OPM=11。 song の channel_count はこれ以下
.endif

; --- 固定 jump table (BANJO_BASE + offset)。 各 jp = 3 byte ---
; +$00 INIT / +$03 PLAY / +$06 UPDATE / +$09 STOP / +$0C 予約 / +$0F END
    jp banjo_x1_init        ; +$00
    jp banjo_x1_play        ; +$03
    jp banjo_x1_update      ; +$06
    jp banjo_x1_stop        ; +$09
    jp banjo_x1_update      ; +$0C 予約
    jp banjo_x1_end         ; +$0F

; --- parameter area (libx1_banjo.asm が BANJO_SET_CTC_PORT で書く) ---
banjo_x1_ctc_port:        .dw 0    ; +$12 (_CTC convention = CTC ch2 port)
banjo_x1_ctcvec:          .dw 0    ; +$14 (IM2 vector table base)
banjo_x1_isr_entry:       .dw 0    ; +$16 (0=direct vector, !=0=trampoline entry)
banjo_x1_isr_handler_ptr: .dw 0    ; +$18 (trampoline JP operand addr)
banjo_x1_isr_return_mode: .db 0    ; +$1A (0=RETI, 1=RET)

; --- SFX jump table (param area 直後に後置。 CTC param offset +$12..+$1A を動かさないため) ---
; +$1B SFX_INIT / +$1E SFX_PLAY / +$21 SFX_STOP。 stub は AY build のみ本体、 OPM-only は no-op(ret)。
    jp banjo_x1_sfx_init    ; +$1B
    jp banjo_x1_sfx_play    ; +$1E
    jp banjo_x1_sfx_stop    ; +$21

; --- ABI 変換 stub ---

; BANJO_INIT(chipflag): SLANG HL=chipflag (L=chipflag)。
;   banjo_has_chips に chipflag を保存 (banjo_init_system_call が PSG mixer 要否判定に使う)、
;   A=BANJO_MAX_CHANNELS (driver build 固定) で banjo_init を呼ぶ (banjo_init は L 未使用)。
banjo_x1_init:
    ld a, l
    ld (banjo_has_chips), a
    ld a, BANJO_MAX_CHANNELS
    call banjo_init
.ifdef BANJO_USE_AY
    call banjo_sfx_init     ; SFX 状態 RAM (sfx_playing 等) を 0 化。 banjo_init は触らないので、
                            ; CTC 有効化 (ctc_setup) 前にここで初期化 (未初期化 RAM の誤読/暴走を防ぐ)。
.endif
    call banjo_x1_ctc_setup
    ret

; BANJO_PLAY(songaddr, loop): SLANG HL=song, DE=loop (E=loop)。 banjo_play_song は HL=song, D=loop。
banjo_x1_play:
    ld d, e
    ld a, (banjo_x1_ctc_port)
    ld e, a
    ld a, (banjo_x1_ctc_port + 1)
    or e
    jr z, banjo_x1_play_noctc
    di
    call banjo_play_song
    ei
    ret
banjo_x1_play_noctc:
    jp banjo_play_song

; BANJO_UPDATE(): 毎フレーム tick (polling mode 専用)。
;   CTC mode (ctc_port != 0) では ISR が banjo_update_song を回すので、 poll 呼出は no-op に
;   する (= 誤って毎フレーム呼んでも二重 update / ISR 再入で暴走しないための self-guard)。
;   stop/play/end と同じ ctc_port 判定。
banjo_x1_update:
    ld a, (banjo_x1_ctc_port)
    ld e, a
    ld a, (banjo_x1_ctc_port + 1)
    or e
    ret nz                  ; CTC mode -> poll update は無視 (ISR 側で song+sfx 両方回す)
.ifdef BANJO_USE_AY
    call banjo_update_song
    jp banjo_update_sfx     ; SFX 未再生時は内部で即 ret (sfx_playing チェック)
.else
    jp banjo_update_song
.endif

; BANJO_STOP(): 再生停止。
banjo_x1_stop:
    ld a, (banjo_x1_ctc_port)
    ld e, a
    ld a, (banjo_x1_ctc_port + 1)
    or e
    jr z, banjo_x1_stop_noctc
    di
    call banjo_song_stop
    ei
    ret
banjo_x1_stop_noctc:
    jp banjo_song_stop

; BANJO_END(): CTC teardown + stop。polling mode では単なる stop。
;   SFX も止める (AY SFX 音の残留防止)。 BANJO_STOP は BGM のみ停止 (SFX 継続可)、 END は両方停止。
banjo_x1_end:
    ld hl, (banjo_x1_ctc_port)
    ld a, l
    or h
    jr nz, banjo_x1_end_ctc
    ; non-CTC: SFX 停止 → song stop
.ifdef BANJO_USE_AY
    call banjo_sfx_stop
.endif
    jp banjo_song_stop
banjo_x1_end_ctc:
    di
.ifdef BANJO_USE_AY
    call banjo_sfx_stop
.endif
    call banjo_x1_ctc_teardown
    call banjo_song_stop
    ei
    ret

; --- SFX ABI 変換 stub (AY 専用。 OPM-only build では全て no-op ret) ----------------------------------
; いずれも CTC mode では ISR (banjo_x1_update_body) と PSG/mute 状態が競合しないよう di/ei で囲う
; (banjo_x1_play / _stop と同じ ctc_port 判定パターン)。

; BANJO_SFX_INIT(): SFX 状態を安全に再初期化。 banjo_sfx_init 単体は sfx_playing を消すだけで
;   再生中の AY 発音停止 / BGM unmute をしないため、 再生中に呼ばれても orphan 化しないよう
;   stop してから init する。 ※通常は banjo_x1_init が init 済なので明示呼出は不要。
banjo_x1_sfx_init:
.ifdef BANJO_USE_AY
    ld a, (banjo_x1_ctc_port)
    ld e, a
    ld a, (banjo_x1_ctc_port + 1)
    or e
    jr z, banjo_x1_sfx_init_noctc
    di
    call banjo_sfx_stop
    call banjo_sfx_init
    ei
    ret
banjo_x1_sfx_init_noctc:
    call banjo_sfx_stop
    jp banjo_sfx_init
.else
    ret
.endif

; BANJO_SFX_PLAY(sfxaddr): SLANG HL=sfxaddr。 banjo_play_sfx も HL=addr なので素通し
;   (ctc 判定は A/E のみ使い HL 非破壊)。 内部で music_play + 対応 BGM ch mute を行う。
banjo_x1_sfx_play:
.ifdef BANJO_USE_AY
    ld a, (banjo_x1_ctc_port)
    ld e, a
    ld a, (banjo_x1_ctc_port + 1)
    or e
    jr z, banjo_x1_sfx_play_noctc
    di
    call banjo_play_sfx
    ei
    ret
banjo_x1_sfx_play_noctc:
    jp banjo_play_sfx
.else
    ret
.endif

; BANJO_SFX_STOP(): 再生中 SFX を停止し、 mute していた BGM ch を unmute。
banjo_x1_sfx_stop:
.ifdef BANJO_USE_AY
    ld a, (banjo_x1_ctc_port)
    ld e, a
    ld a, (banjo_x1_ctc_port + 1)
    or e
    jr z, banjo_x1_sfx_stop_noctc
    di
    call banjo_sfx_stop
    ei
    ret
banjo_x1_sfx_stop_noctc:
    jp banjo_sfx_stop
.else
    ret
.endif


; --- CTC setup/teardown -------------------------------------------------------------------------------

banjo_x1_ctc_setup:
    ld bc, (banjo_x1_ctc_port)
    ld a, c
    or b
    ret z

    di

    ; Program CTC channel 1 (~61Hz, X1 CTC standard setup)
    ld bc, (banjo_x1_ctc_port)
    dec c
    ld a, $a7
    out (c), a
    xor a
    out (c), a

    ; vector hook: isr_entry = 0 -> direct RETI, !=0 -> x1native trampoline
    ld a, (banjo_x1_isr_entry)
    ld b, a
    ld a, (banjo_x1_isr_entry + 1)
    or b
    jr nz, banjo_x1_ctc_setup_tramp

    ld hl, (banjo_x1_ctcvec)
    inc l
    inc l
    ld c, (hl)
    inc hl
    ld b, (hl)
    ld (banjo_x1_ctc_backup), bc
    dec hl
    ld bc, banjo_x1_isr_reti
    ld (hl), c
    inc hl
    ld (hl), b
    ei
    ret

banjo_x1_ctc_setup_tramp:
    ld hl, (banjo_x1_ctcvec)
    inc l
    inc l
    ld c, (hl)
    inc hl
    ld b, (hl)
    ld (banjo_x1_ctc_backup), bc
    dec hl
    ld bc, (banjo_x1_isr_entry)
    ld (hl), c
    inc hl
    ld (hl), b

    ld hl, (banjo_x1_isr_handler_ptr)
    ld c, (hl)
    inc hl
    ld b, (hl)
    ld (banjo_x1_isr_handler_backup), bc
    dec hl
    ld a, (banjo_x1_isr_return_mode)
    or a
    jr nz, banjo_x1_ctc_setup_tramp_ret
    ld de, banjo_x1_isr_reti
    jr banjo_x1_ctc_setup_tramp_write
banjo_x1_ctc_setup_tramp_ret:
    ld de, banjo_x1_isr_ret
banjo_x1_ctc_setup_tramp_write:
    ld (hl), e
    inc hl
    ld (hl), d
    ei
    ret

banjo_x1_ctc_teardown:
    ld hl, (banjo_x1_ctc_port)
    ld a, l
    or h
    ret z

    ; Stop CTC1
    ld hl, (banjo_x1_ctc_port)
    dec l
    ld c, l
    ld b, h
    ld a, 3
    out (c), a

    ; Restore original CTC1 vector
    ld hl, (banjo_x1_ctcvec)
    inc l
    inc l
    ld de, (banjo_x1_ctc_backup)
    ld (hl), e
    inc hl
    ld (hl), d

    ; Restore trampoline handler ptr if trampoline path was used
    ld a, (banjo_x1_isr_entry)
    ld b, a
    ld a, (banjo_x1_isr_entry + 1)
    or b
    ret z
    ld hl, (banjo_x1_isr_handler_ptr)
    ld de, (banjo_x1_isr_handler_backup)
    ld (hl), e
    inc hl
    ld (hl), d
    ret


; --- CTC ISR entries ----------------------------------------------------------------------------------

banjo_x1_isr_reti:
    call banjo_x1_update_body
    ei
    reti

banjo_x1_isr_ret:
    call banjo_x1_update_body
    ei
    ret

banjo_x1_update_body:
    di
    push af
    push hl
    push de
    push bc
    push ix
    push iy
    ex af, af'
    push af
    exx
    push hl
    push de
    push bc
    call banjo_update_song
.ifdef BANJO_USE_AY
    call banjo_update_sfx      ; CTC ISR 文脈でも SFX を tick (未再生なら内部で即 ret)
.endif
    pop bc
    pop de
    pop hl
    exx
    pop af
    ex af, af'
    pop iy
    pop ix
    pop bc
    pop de
    pop hl
    pop af
    ret

; --- banjo_init_system_call (banjo_init が最初に call、 SYS=0 では upstream init_*.inc が無いので自前) ---
; AY 使用時のみ PSG mixer (reg7=$f8) を初期化 (= MSX BIOS GICINI 相当)。 OPM は初期化不要。
; banjo_has_chips は banjo_x1_init で chipflag に set 済。 BANJO_USE_AY define 時のみ AY 経路を出す
; (= OPM-only build では psg_write が未定義なので参照しない)。
banjo_init_system_call:
.ifdef BANJO_USE_AY
    ld a, (banjo_has_chips)
    and BANJO_HAS_AY
    jr z, bisc_ay_done      ; AY 不使用 → skip
    ld h, 7
    ld l, $f8               ; mixer reg7 = all tone on / noise off
    call psg_write
bisc_ay_done:
.endif
.ifdef BANJO_USE_OPM
    ld a, (banjo_has_chips)
    and BANJO_HAS_OPM
    jr z, bisc_opm_done     ; OPM 不使用 → skip
    ; 全 8 FM ch を key-off (reg $08 = ch番号、 slot bit 3-6 = 0 で全 operator off)。
    ; warm reset 後にエミュレータが残す key-on 状態を消し、 cold boot と同じ「全 operator
    ; key-off」初期状態に揃える。 これが無いと 1 音目の note-on が既に key-on の operator に
    ; 当たり、 YM2151 envelope が 0->1 エッジで再トリガせずアタックが出ない (= 2 音目から
    ; 鳴り始めたように聞こえる) 不具合になる。 upstream の banjo_mute_all_opm は中身が空の
    ; stub で、 banjo_init_opm も key-off しないため、 ここで X1 system init として補う。
    ld b, 8
    ld c, 0                 ; C = channel number 0..7
bisc_opm_keyoff:
    ld h, $08
    ld l, c                 ; L = ch番号 (slot bit 無し = key off)
    call banjo_opm_write
    inc c
    djnz bisc_opm_keyoff
bisc_opm_done:
.endif
    ret


; --- work area ----------------------------------------------------------------------------------------
banjo_x1_ctc_backup:         .dw 0
banjo_x1_isr_handler_backup: .dw 0
