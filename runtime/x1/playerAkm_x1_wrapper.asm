; ====================================================================================================
; ArkosTracker AKM Player (Minimalist) - X1 wrapper for FuzzyBASIC
;
; Builds with RASM: rasm playerAkm_x1_wrapper.asm -o PSGDRV_AKM -s
;
; Jump table layout (= AKG wrapper と同一、 PLY_AKM_ player を include する点だけ違う):
;   +$00  JP Init         (L=mode: 0=non-CTC, 1=CTC)
;   +$03  JP BGMPlay      (HL=music data address)
;   +$06  JP BGMStop
;   +$09  JP BGMPause
;   +$0C  JP BGMResume
;   +$0F  JP SFXInit      (HL=SFX table address)
;   +$12  JP SFXPlay      (L=SFX number, H=channel)
;   +$15  JP SFXStop      (L=channel 0-2)
;   +$18  JP PSG_PROC     (polling: RET or NOP->fallthrough to PlayPROC)
;   +$1B  JP PSG_END      (CTC teardown + stop)
;   +$1E  SND_CTC_PORT    (DW, caller writes)
;   +$20  SND_CTCVEC      (DW, caller writes)
;   +$22  SND_ISR_ENTRY      (DW, caller writes, 0=direct vector, !=0=trampoline)
;   +$24  SND_ISR_HANDLER_PTR (DW, caller writes, trampoline JP operand addr)
;   +$26  SND_ISR_RETURN_MODE (DB, caller writes, 0=RETI / 1=RET)
;
; licence: MIT License (ArkosTracker player by Targhan/Arkos)
; ====================================================================================================

        IFNDEF DRIVER_ORG
DRIVER_ORG = #c300
        ENDIF
        org DRIVER_ORG

; ----------------------------------------------------------------------------------------------------
; Jump table
; ----------------------------------------------------------------------------------------------------
        jp AKM_X1_Init          ; +$00
        jp AKM_X1_BGMPlay       ; +$03
        jp AKM_X1_BGMStop       ; +$06
        jp AKM_X1_BGMPause      ; +$09
        jp AKM_X1_BGMResume     ; +$0C
        jp AKM_X1_SFXInit       ; +$0F
        jp AKM_X1_SFXPlay       ; +$12
        jp AKM_X1_SFXStop       ; +$15
        jp AKM_X1_PSG_PROC      ; +$18
        jp AKM_X1_PSG_END       ; +$1B

; ----------------------------------------------------------------------------------------------------
; Parameter area (caller writes before Init)
; ----------------------------------------------------------------------------------------------------
AKM_X1_SND_CTC_PORT:        dw 0    ; +$1E
AKM_X1_SND_CTCVEC:          dw 0    ; +$20
AKM_X1_SND_ISR_ENTRY:       dw 0    ; +$22 (0=direct vector, !=0=trampoline)
AKM_X1_SND_ISR_HANDLER_PTR: dw 0    ; +$24 (trampoline JP operand addr)
AKM_X1_SND_ISR_RETURN_MODE: db 0    ; +$26 (0=RETI, 1=RET)


; ====================================================================================================
; Init (L=mode: 0=non-CTC, 1=CTC)
; ====================================================================================================
AKM_X1_Init:
        di
        push af
        push bc
        push de
        push hl
        push ix
        push iy

        ; L = mode: 0=non-CTC, 1=CTC
        ld a,l
        or a
        jp z,AKM_X1_Init_NoCTC

        ; CTC present?
        ld bc,(AKM_X1_SND_CTC_PORT)
        ld a,c
        or b
        jp z,AKM_X1_Init_NoCTC

        ; Program CTC channel 1 (~61Hz)
        ld bc,(AKM_X1_SND_CTC_PORT)
        dec c           ; Channel 1
        ld a,#a7        ; Reset, prescaler 256, interrupt enabled
        out (c),a
        ld a,0          ; Time constant 256
        out (c),a

        ; vector hook: SND_ISR_ENTRY = 0 -> direct (PlayISR_RETI)、 !=0 -> trampoline
        ld a,(AKM_X1_SND_ISR_ENTRY)
        ld b,a
        ld a,(AKM_X1_SND_ISR_ENTRY+1)
        or b
        jr nz,AKM_X1_Init_Tramp

        ; direct vector path
        ld hl,(AKM_X1_SND_CTCVEC)
        inc l
        inc l           ; CTC1 entry
        ld c,(hl)
        inc hl
        ld b,(hl)
        ld (AKM_X1_CTC_Backup),bc      ; Save original vector
        dec hl
        ld bc,AKM_X1_PlayISR_RETI
        ld (hl),c
        inc hl
        ld (hl),b
        jr AKM_X1_Init_VectorDone

AKM_X1_Init_Tramp:
        ; trampoline path: vector slot = SND_ISR_ENTRY、 HANDLER_PTR に PlayISR_RETI/RET 書込
        ld hl,(AKM_X1_SND_CTCVEC)
        inc l
        inc l           ; CTC1 entry
        ld c,(hl)
        inc hl
        ld b,(hl)
        ld (AKM_X1_CTC_Backup),bc      ; Save original vector
        dec hl
        ld bc,(AKM_X1_SND_ISR_ENTRY)
        ld (hl),c
        inc hl
        ld (hl),b

        ; Backup old handler ptr + write new (RETI or RET entry)
        ld hl,(AKM_X1_SND_ISR_HANDLER_PTR)
        ld c,(hl)
        inc hl
        ld b,(hl)
        ld (AKM_X1_ISR_Handler_Backup),bc
        dec hl
        ld a,(AKM_X1_SND_ISR_RETURN_MODE)
        or a
        jr nz,AKM_X1_Init_TrampRET
        ld de,AKM_X1_PlayISR_RETI
        jr AKM_X1_Init_TrampWrite
AKM_X1_Init_TrampRET:
        ld de,AKM_X1_PlayISR_RET
AKM_X1_Init_TrampWrite:
        ld (hl),e
        inc hl
        ld (hl),d

AKM_X1_Init_VectorDone:

        ; Ensure PSG_PROC is RET (in case previously NOP'd by non-CTC init)
        ld a,#c9        ; RET opcode
        ld (AKM_X1_PSG_PROC),a

        jr AKM_X1_Init_Done

AKM_X1_Init_NoCTC:
        ; NOP PSG_PROC -> falls through to AKM_X1_PlayPROC
        ld hl,AKM_X1_PSG_PROC
        xor a
        ld (hl),a       ; NOP

AKM_X1_Init_Done:
        xor a
        ld (AKM_X1_Paused),a
        ld (AKM_X1_Active),a   ; Not yet active until BGMPlay

        pop iy
        pop ix
        pop hl
        pop de
        pop bc
        pop af
        ei
        ret


; ====================================================================================================
; BGMPlay (HL=music data address)
; ====================================================================================================
AKM_X1_BGMPlay:
        xor a
        ld (AKM_X1_Active),a    ; Inactive during init (prevent ISR from calling Play)
        ld (AKM_X1_Paused),a    ; Clear pause flag
        push ix
        push iy
        xor a                   ; Subsong 0
        call PLY_AKM_Init
        pop iy
        pop ix
        ld a,1
        ld (AKM_X1_Active),a    ; Now safe to play
        ret


; ====================================================================================================
; BGMStop
; ====================================================================================================
AKM_X1_BGMStop:
        xor a
        ld (AKM_X1_Active),a    ; Mark music as inactive
        ld (AKM_X1_Paused),a    ; Clear pause flag
        jp PLY_AKM_Stop


; ====================================================================================================
; BGMPause
; ====================================================================================================
AKM_X1_BGMPause:
        ld a,(AKM_X1_Paused)
        or a
        ret nz          ; Already paused
        ld a,1
        ld (AKM_X1_Paused),a
        jp PLY_AKM_Stop ; Mute all channels


; ====================================================================================================
; BGMResume
; ====================================================================================================
AKM_X1_BGMResume:
        ld a,(AKM_X1_Paused)
        or a
        ret z           ; Not paused
        xor a
        ld (AKM_X1_Paused),a
        ret


; ====================================================================================================
; SFXInit (HL=SFX table address)
; ====================================================================================================
AKM_X1_SFXInit:
        jp PLY_AKM_InitSoundEffects


; ====================================================================================================
; SFXPlay (L=SFX number 1-based, H=channel 0-2)
; ====================================================================================================
AKM_X1_SFXPlay:
        ld a,l          ; SFX number
        ld c,h          ; Channel
        ld b,0          ; Full volume (inverted=0)
        jp PLY_AKM_PlaySoundEffect


; ====================================================================================================
; SFXStop (L=channel 0-2)
; ====================================================================================================
AKM_X1_SFXStop:
        ld a,l          ; Channel number
        jp PLY_AKM_StopSoundEffectFromChannel


; ====================================================================================================
; PSG_PROC - Polling entry point
;   CTC mode:     RET keeps polling no-op
;   non-CTC mode: Init_NoCTC writes NOP -> fallthrough to PlayPROC
; ====================================================================================================
AKM_X1_PSG_PROC:
        ret
; ====================================================================================================
; PlayPROC - polling 用 entry (= 通常 RET ending、 PSG_PROC NOP fallthrough 先)
; ====================================================================================================
AKM_X1_PlayPROC:
        call AKM_X1_PlayBody
        ei
        ret
; ====================================================================================================
; PlayISR_RETI - CTC direct vector or trampoline RETI entry
; ====================================================================================================
AKM_X1_PlayISR_RETI:
        call AKM_X1_PlayBody
        ei
        reti
; ====================================================================================================
; PlayISR_RET - trampoline RET entry (= future sosx1 turbo 用)
; ====================================================================================================
AKM_X1_PlayISR_RET:
        call AKM_X1_PlayBody
        ei
        ret
; ====================================================================================================
; PlayBody - 共通 body (= active/paused check + PLY_AKM_Play call、 通常 RET で終わる)
; ====================================================================================================
AKM_X1_PlayBody:
        di
        push af
        ld a,(AKM_X1_Active)
        or a
        jr z,AKM_X1_PlayBody_Skip
        ld a,(AKM_X1_Paused)
        or a
        jr nz,AKM_X1_PlayBody_Skip
        push hl
        push de
        push bc
        push ix
        push iy
        ex af,af'
        push af                 ; Save AF'
        exx
        push hl                 ; Save HL'
        push de                 ; Save DE'
        push bc                 ; Save BC'
        call PLY_AKM_Play
        pop bc                  ; Restore BC'
        pop de                  ; Restore DE'
        pop hl                  ; Restore HL'
        exx
        pop af                  ; Restore AF'
        ex af,af'
        pop iy
        pop ix
        pop bc
        pop de
        pop hl
AKM_X1_PlayBody_Skip:
        pop af
        ret


; ====================================================================================================
; PSG_END - CTC teardown + stop
; ====================================================================================================
AKM_X1_PSG_END:
        call PLY_AKM_Stop

        ; CTC present?
        ld hl,(AKM_X1_SND_CTC_PORT)
        ld a,l
        or h
        ret z           ; No CTC -> done

        ; Stop CTC1
        ld hl,(AKM_X1_SND_CTC_PORT)
        dec l
        ld c,l
        ld b,h
        ld a,3
        out (c),a

        ; Restore original CTC1 vector
        ld hl,(AKM_X1_SND_CTCVEC)
        inc l
        inc l
        ld de,(AKM_X1_CTC_Backup)
        ld (hl),e
        inc hl
        ld (hl),d

        ; trampoline handler ptr restore (SND_ISR_ENTRY != 0 のみ)
        ld a,(AKM_X1_SND_ISR_ENTRY)
        ld b,a
        ld a,(AKM_X1_SND_ISR_ENTRY+1)
        or b
        ret z           ; direct vector path -> done

        ld hl,(AKM_X1_SND_ISR_HANDLER_PTR)
        ld de,(AKM_X1_ISR_Handler_Backup)
        ld (hl),e
        inc hl
        ld (hl),d
        ret


; ====================================================================================================
; Work area
; ====================================================================================================
AKM_X1_CTC_Backup:         dw 0    ; Original CTC1 vector
AKM_X1_ISR_Handler_Backup: dw 0    ; trampoline 旧 handler ptr (= SND_ISR_ENTRY != 0 時)
AKM_X1_Paused:             db 0    ; Pause flag
AKM_X1_Active:             db 0    ; Music active flag (0=not started, 1=playing)


; ====================================================================================================
; AKM Player configuration
; ====================================================================================================
        PLY_AKM_HARDWARE_X1 = 1
        PLY_AKM_MANAGE_SOUND_EFFECTS = 1

        include "PlayerAkm_x1.asm"

; ====================================================================================================
AKM_X1_End:
