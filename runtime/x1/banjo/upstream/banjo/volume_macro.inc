
; banjo sound driver
; Joe Kennedy 2024

; ix: channel
; iy: music state
music_update_volume_macro:
    
    ; check if we've reached the end of the macro
    ld a, (ix + channel.volume_macro_pos)
    or a, a
    ret z

        ; get pointer to volume macro data in hl
        ld l, (ix + channel.volume_macro_ptr)
        ld h, (ix + channel.volume_macro_ptr + 1)

        ; get byte with release mode flag
        ld e, (ix + channel.events)
        
        ; move position on
        inc a

        ; is macro release mode active?
        bit CHAN_EVENT_BIT_MACRO_RELEASE, e
        jr z, muvm_not_release

            ; move hl to release data
            inc hl
            inc hl

            ; hl is pointing at macro length
            ; compare our new pos with the length
            cp a, (hl)
            jr z, muvm_at_end

                ; store the new pos
                ld (ix + channel.volume_macro_pos), a

                ; move the ptr back
                dec a
                dec a

                jr muvm_continue

        muvm_not_release:

        ; hl is pointing at macro length
        ; compare our new pos with the length
        cp a, (hl)
        jr z, muvm_at_end

            ; store the new pos
            ld (ix + channel.volume_macro_pos), a
            
            muvm_continue:

            ; offset into macro data
            add a, l
            ld l, a
            adc a, h
            sub a, l
            ld h, a

            ; store new macro volume
            ld a, (hl)
            ld (ix + channel.volume_macro_vol), a
        
            ; flag that the volume has changed
            set CHAN_EVENT_BIT_VOLUME_CHANGE, (ix + channel.events)

            ret

    muvm_at_end:

        ; pos has reached the end of the macro data
        ; move hl to macro loop and get the value
        inc hl
        ld a, (hl)

        ; store the new pos
        ld (ix + channel.volume_macro_pos), a

        ; if the new pos == 0 then there's no looping
        or a, a
        ret z

        ;  move the ptr back
        dec a

        ; needs to move back more if release mode is on
        bit CHAN_EVENT_BIT_MACRO_RELEASE, e
        jr z, muvm_continue

            dec a 
            dec a

            jr muvm_continue
