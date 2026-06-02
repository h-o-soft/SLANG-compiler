
; banjo sound driver
; Joe Kennedy 2024

; ix: channel
; iy: music state
music_update_arp_macro:
    
    ; check if we've reached the end of the macro
    ld a, (ix + channel.arp_macro_pos)
    or a, a
    ret z

        ; get pointer to ex macro data in hl
        ld l, (ix + channel.arp_macro_ptr)
        ld h, (ix + channel.arp_macro_ptr + 1) 
        
        ; move position on
        inc a

        ; hl is pointing at macro length
        ; compare our new pos with the length
        cp a, (hl)
        jr z, muam_at_end

            ; store the new pos
            ld (ix + channel.arp_macro_pos), a
            
            muam_continue:

            ; offset into macro data
            add a, l
            ld l, a
            adc a, h
            sub a, l
            ld h, a

            ; store new macro value
            ld a, (hl)
            ld (ix + channel.arp_macro_val), a
        
            ; set pitch change flag
            set CHAN_EVENT_BIT_PITCH_CHANGED, (ix + channel.events)

            ret

    muam_at_end:

        ; pos has reached the end of the macro data
        ; move hl to macro loop and get the value
        inc hl
        ld a, (hl)

        ; store the new pos
        ld (ix + channel.arp_macro_pos), a

        ; if the new pos == 0 then there's no looping
        or a, a
        ret z

        ;  move the ptr back
        dec a

        jr muam_continue

