; ====================================================================================================
; ArkosTracker AKM Player (Minimalist) - ZX Spectrum Next (zxn) wrapper
;
; RASM で組む: rasm playerAkm_zxn_wrapper.asm -DDRIVER_ORG=0xE000 -o PSGAKM -s
; 再生部の本体は runtime/x1/PlayerAkm_x1.asm (= Arkos Tracker 3.7 の PlayerAkm.asm に X1 用の IFDEF を足したもの) を
; そのまま include する。zxn では X1 の IFDEF は使わず、Spectrum/Pentagon の道 (ポート #FFFD / #BFFD) を使う。
;
; 周期表は PLY_AKM_HARDWARE_PENTAGON (AY 1.75MHz)。Next の AY は 1.75MHz で、Spectrum 128 (1.7734MHz) ではない:
;   - 公式の FPGA のソース: gitlab.com/SpectrumNext/ZX_Spectrum_Next_FPGA の cores/zxnext/src/zxnext_top_issue4.vhd
;     `CLK_28_PSG_EN <= clkdiv_3_0; -- AY clock enable @ 1.75MHz` (28MHz を 16 で割る)
;   - 実機で測った (2026-11-09): AY の A に周期 249 の音を出してチューナーで A4 の約 -3 セント
;     (1.75MHz なら 439.3Hz = -3 セント、1.7734MHz なら 445.1Hz = 約 +20 セント)
;
; 飛び先の表 (並びは X1 の包みと同じ。SFXPlay の引数の受け方だけ X1 と違う):
;   +$00  Init       引数なし。TurboSound を有効にして AY 0 を選び、音を止める
;   +$03  BGMPlay    HL = 曲 (.akm) の番地
;   +$06  BGMStop
;   +$09  BGMPause
;   +$0C  BGMResume
;   +$0F  SFXInit    HL = 効果音 (.akx) の番地
;   +$12  SFXPlay    L = 効果音の番号 (1 から)、E = チャンネル (0〜2)  (X1 の包みは H がチャンネル)
;   +$15  SFXStop    L = チャンネル (0〜2)
;   +$18  PSG_PROC   1 フレームに 1 回呼ぶ
;   +$1B  PSG_END    BGMStop と同じ (この後 PSG_PROC を呼んでも鳴らない)
; SLANG からは MACHINE で番地を呼ぶ (examples/zxn/ARKOSZXN.SL)。引数は 1 つ目 HL、2 つ目 DE。
;
; すべての入口で: 呼んだときの AF・IX・IY・SP を保ち、割り込みの状態を控えて DI し、戻る前に元の状態に戻す
; (再生部は PLY_AKM_Play・PLY_AKM_Stop の中で SP を自分の表に切り替えるので、その間は割り込みを受けられない)。
; PSG_PROC は、そのほかのレジスタ (HL・DE・BC と裏の組) もすべて保つ。
;
; 呼ぶ側の決まり:
;   - PSG_PROC は 1 フレームに 1 回。曲の速さは呼ぶ回数で決まる (Arkos の曲の再生の周波数は、ふつう 50Hz)
;   - 効果音は BGM が動いている間だけ進む (PSG_PROC が PLY_AKM_Play の中で効果音も進める)
;   - 使っている間、ほかの処理が AY の選択 (#FFFD) を変えない
;
; AKG (Generic) を足すときは、この包みを写して include と PLY_AKM_ の名前を替える。表の並び・番地・
; MACHINE の宣言は共通 (AKM と AKG は同じ番地に置くので、同時には使えない)。
;
; licence: MIT License (ArkosTracker player by Targhan/Arkos、リポジトリの LICENSE の Arkos の節)
; ====================================================================================================

        IFNDEF DRIVER_ORG
DRIVER_ORG = #e000
        ENDIF

        org DRIVER_ORG

; ----------------------------------------------------------------------------------------------------
; 飛び先の表
; ----------------------------------------------------------------------------------------------------
        jp AKM_ZXN_Init         ; +$00
        jp AKM_ZXN_BGMPlay      ; +$03
        jp AKM_ZXN_BGMStop      ; +$06
        jp AKM_ZXN_BGMPause     ; +$09
        jp AKM_ZXN_BGMResume    ; +$0C
        jp AKM_ZXN_SFXInit      ; +$0F
        jp AKM_ZXN_SFXPlay      ; +$12
        jp AKM_ZXN_SFXStop      ; +$15
        jp AKM_ZXN_PSG_PROC     ; +$18
        jp AKM_ZXN_PSG_END      ; +$1B

; ----------------------------------------------------------------------------------------------------
; 入口: IX を積んでから本体の番地を IX に入れ、共通の処理へ (HL・DE・BC の引数には触らない)
; ----------------------------------------------------------------------------------------------------
AKM_ZXN_Init:      push ix : ld ix,AKM_ZXN_InitBody : jr AKM_ZXN_Guard
AKM_ZXN_BGMPlay:   push ix : ld ix,AKM_ZXN_BGMPlayBody : jr AKM_ZXN_Guard
AKM_ZXN_BGMStop:   push ix : ld ix,AKM_ZXN_BGMStopBody : jr AKM_ZXN_Guard
AKM_ZXN_BGMPause:  push ix : ld ix,AKM_ZXN_BGMPauseBody : jr AKM_ZXN_Guard
AKM_ZXN_BGMResume: push ix : ld ix,AKM_ZXN_BGMResumeBody : jr AKM_ZXN_Guard
AKM_ZXN_SFXInit:   push ix : ld ix,PLY_AKM_InitSoundEffects : jr AKM_ZXN_Guard
AKM_ZXN_SFXPlay:   push ix : ld ix,AKM_ZXN_SFXPlayBody : jr AKM_ZXN_Guard
AKM_ZXN_SFXStop:   push ix : ld ix,AKM_ZXN_SFXStopBody : jr AKM_ZXN_Guard
AKM_ZXN_PSG_PROC:  push ix : ld ix,AKM_ZXN_PSG_PROCBody : jr AKM_ZXN_Guard
AKM_ZXN_PSG_END:   push ix : ld ix,AKM_ZXN_BGMStopBody

; ----------------------------------------------------------------------------------------------------
; 共通の処理。LD A,I が A とフラグを変えるので、呼んだときの AF はその前に積む。
; 出口は割り込みの状態で先に分かれ、AF・IY・IX を戻し終えてから、許可だった方だけ EI する
; (EI が効くのは次の 1 命令の後なので、RET の直前に置く)。
; ----------------------------------------------------------------------------------------------------
AKM_ZXN_Guard:
        push iy
        push af                 ; 呼んだときの AF
        ld a,i                  ; P/V = IFF2 (割り込みが許されていたか)
        push af
        di
        call AKM_ZXN_CallIX
        pop af                  ; 割り込みの状態
        jp po,AKM_ZXN_ExitDI
        pop af
        pop iy
        pop ix
        ei
        ret
AKM_ZXN_ExitDI:
        pop af
        pop iy
        pop ix
        ret
AKM_ZXN_CallIX:
        jp (ix)

; ====================================================================================================
; 本体
; ====================================================================================================

; Init: NextReg $08 の 1 ビット目 (TurboSound) を立て (ほかのビットは残す)、#FFFD に #FF を書いて AY 0 を左右とも選ぶ。
; そのあと BGMStop と同じ (印を消して音を止める)。
AKM_ZXN_InitBody:
        ld bc,#243b
        ld a,#08
        out (c),a
        inc b                   ; #253b
        in a,(c)
        or %00000010
        out (c),a
        ld bc,#fffd
        ld a,#ff
        out (c),a
AKM_ZXN_BGMStopBody:
        xor a
        ld (AKM_ZXN_Active),a
        ld (AKM_ZXN_Paused),a
        jp PLY_AKM_Stop

AKM_ZXN_BGMPlayBody:
        xor a
        ld (AKM_ZXN_Active),a
        ld (AKM_ZXN_Paused),a
        call PLY_AKM_Init       ; HL = 曲、A = 0 (サブソング 0)
        ld a,1
        ld (AKM_ZXN_Active),a
        ret

AKM_ZXN_BGMPauseBody:
        ld a,(AKM_ZXN_Paused)
        or a
        ret nz
        inc a
        ld (AKM_ZXN_Paused),a
        jp PLY_AKM_Stop         ; 音を止める

AKM_ZXN_BGMResumeBody:
        xor a
        ld (AKM_ZXN_Paused),a
        ret

AKM_ZXN_SFXPlayBody:
        ld a,l                  ; 番号
        ld c,e                  ; チャンネル
        ld b,0                  ; 音量を下げない
        jp PLY_AKM_PlaySoundEffect

AKM_ZXN_SFXStopBody:
        ld a,l
        jp PLY_AKM_StopSoundEffectFromChannel

; PSG_PROC: 動いていて一時停止していなければ 1 フレーム進める。HL・DE・BC と裏の組も残す (AF・IX・IY は共通が残す)。
AKM_ZXN_PSG_PROCBody:
        ld a,(AKM_ZXN_Active)
        or a
        ret z
        ld a,(AKM_ZXN_Paused)
        or a
        ret nz
        push hl
        push de
        push bc
        ex af,af'
        push af
        exx
        push hl
        push de
        push bc
        call PLY_AKM_Play
        pop bc
        pop de
        pop hl
        exx
        pop af
        ex af,af'
        pop bc
        pop de
        pop hl
        ret

AKM_ZXN_Active: db 0            ; 1 = BGMPlay の後 (BGMStop・PSG_END・Init で 0)
AKM_ZXN_Paused: db 0            ; 1 = BGMPause の後

; ====================================================================================================
; 再生部
; ====================================================================================================
        PLY_AKM_HARDWARE_PENTAGON = 1
        PLY_AKM_MANAGE_SOUND_EFFECTS = 1
        PLY_AKM_REMOVE_HOOKS = 1
        include "../x1/PlayerAkm_x1.asm"

AKM_ZXN_End:
        ASSERT AKM_ZXN_End <= #fd00     ; FD00H〜FFFFH は zxslang の IDE の予約域
