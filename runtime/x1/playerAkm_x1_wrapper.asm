; ====================================================================================================
; ArkosTracker AKM Player (Minimalist) - X1 wrapper for FuzzyBASIC
;
; Builds with RASM: rasm playerAkm_x1_wrapper.asm -o PSGDRV_AKM -s
;
; Same jump table layout as AKG wrapper.
;
; licence: MIT License (ArkosTracker player by Targhan/Arkos)
; ====================================================================================================

        IFNDEF DRIVER_ORG
DRIVER_ORG = #c300
        ENDIF
        org DRIVER_ORG

; ----------------------------------------------------------------------------------------------------
; Jump table (identical to AKG wrapper)
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
AKM_X1_SND_CTC_PORT:   dw 0    ; +$1E
AKM_X1_SND_CTCVEC:     dw 0    ; +$20


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

        ld a,l
        or a
        jp z,AKM_X1_Init_NoCTC

        ld bc,(AKM_X1_SND_CTC_PORT)
        ld a,c
        or b
        jp z,AKM_X1_Init_NoCTC

        ; Program CTC channel 1 (~61Hz)
        ld bc,(AKM_X1_SND_CTC_PORT)
        dec c
        ld a,#a7
        out (c),a
        ld a,0
        out (c),a

        ; Hook CTC1 interrupt vector
        ld hl,(AKM_X1_SND_CTCVEC)
        inc l
        inc l
        ld c,(hl)
        inc hl
        ld b,(hl)
        ld (AKM_X1_CTC_Backup),bc
        dec hl
        ld bc,AKM_X1_PlayISR
        ld (hl),c
        inc hl
        ld (hl),b

        ld a,#c9
        ld (AKM_X1_PSG_PROC),a

        jr AKM_X1_Init_Done

AKM_X1_Init_NoCTC:
        ld hl,AKM_X1_PSG_PROC
        xor a
        ld (hl),a       ; NOP

AKM_X1_Init_Done:
        xor a
        ld (AKM_X1_Paused),a
        ld (AKM_X1_Active),a

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
        ld (AKM_X1_Paused),a
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
        ld (AKM_X1_Active),a
        ld (AKM_X1_Paused),a
        jp PLY_AKM_Stop


; ====================================================================================================
; BGMPause
; ====================================================================================================
AKM_X1_BGMPause:
        ld a,(AKM_X1_Paused)
        or a
        ret nz
        ld a,1
        ld (AKM_X1_Paused),a
        jp PLY_AKM_Stop


; ====================================================================================================
; BGMResume
; ====================================================================================================
AKM_X1_BGMResume:
        ld a,(AKM_X1_Paused)
        or a
        ret z
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
        ld b,0          ; Full volume
        jp PLY_AKM_PlaySoundEffect


; ====================================================================================================
; SFXStop (L=channel 0-2)
; ====================================================================================================
AKM_X1_SFXStop:
        ld a,l
        jp PLY_AKM_StopSoundEffectFromChannel


; ====================================================================================================
; PSG_PROC
; ====================================================================================================
AKM_X1_PSG_PROC:
        ret
; ====================================================================================================
; PlayISR
; ====================================================================================================
AKM_X1_PlayISR:
        di
        push af
        ld a,(AKM_X1_Active)
        or a
        jr z,AKM_X1_PlayISR_Skip
        ld a,(AKM_X1_Paused)
        or a
        jr nz,AKM_X1_PlayISR_Skip
        push hl
        push de
        push bc
        push ix
        push iy
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
        pop iy
        pop ix
        pop bc
        pop de
        pop hl
AKM_X1_PlayISR_Skip:
        pop af
        ei
        reti


; ====================================================================================================
; PSG_END - CTC teardown + stop
; ====================================================================================================
AKM_X1_PSG_END:
        call PLY_AKM_Stop

        ld hl,(AKM_X1_SND_CTC_PORT)
        ld a,l
        or h
        ret z

        ld hl,(AKM_X1_SND_CTC_PORT)
        dec l
        ld c,l
        ld b,h
        ld a,3
        out (c),a

        ld hl,(AKM_X1_SND_CTCVEC)
        inc l
        inc l
        ld de,(AKM_X1_CTC_Backup)
        ld (hl),e
        inc hl
        ld (hl),d
        ret


; ====================================================================================================
; Work area
; ====================================================================================================
AKM_X1_CTC_Backup:     dw 0
AKM_X1_Paused:         db 0
AKM_X1_Active:         db 0


; ====================================================================================================
; AKM Player configuration
; ====================================================================================================
        PLY_AKM_HARDWARE_X1 = 1
        PLY_AKM_MANAGE_SOUND_EFFECTS = 1

        include "PlayerAkm_x1.asm"

; ====================================================================================================
AKM_X1_End:
