
; banjo sound driver
; Joe Kennedy 2024

.ifndef BANJO_DISABLE_ARPEGGIO

    music_update_arpeggio:

        ; decrement arpeggio position and check if it has gone negative
        dec (ix + channel.effect1)
        jp m, mpct_arp_reset

            ; if it wasn't zero after decrementing, use the first value
            jr z, mpct_arp_val2

                ld a, (ix + channel.effect2)
                ret

            ; if it was zero after decrementing, use the second value
            mpct_arp_val2:

                ld a, (ix + channel.effect3)
                ret

        mpct_arp_reset:

            ; arp position has gone negative, reset it to 2
            ld (ix + channel.effect1), 2

            ; return 0
            xor a, a

            ret

.endif

.ifdef BANJO_DISABLE_ARPEGGIO

    music_update_arpeggio:
        ret

.endif