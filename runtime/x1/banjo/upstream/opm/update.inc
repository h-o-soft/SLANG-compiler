
; banjo sound driver
; Joe Kennedy 2025

; jumped to from music_update
; ix: channel
; iy: music state
banjo_update_channels_opm:

    call music_update_channel_opm
    call music_update_channel_opm
    call music_update_channel_opm
    call music_update_channel_opm
    call music_update_channel_opm
    call music_update_channel_opm
    call music_update_channel_opm
    call music_update_channel_opm

    ret


; ix: channel
; iy: music state
banjo_update_channel_opm:

    call music_update_channel_opm
    
    ret



; ix: channel
; iy: music state
music_update_channel_opm:

    ld bc, music_command_jump_table_opm
    call music_process_new_line

    ; get flags in b for later
    ld b, (ix + channel.flags)

    ; check that note on is set
    ; this should never be set if the channel is muted
    bit CHAN_FLAG_BIT_NOTE_ON, b
    jr z, muco2_done

        ; process volume macro
        bit CHAN_FLAG_BIT_VOLUME_MACRO, b
        call nz, music_update_volume_macro

        ; process arp macro
        bit CHAN_FLAG_BIT_ARP_MACRO, b
        call nz, music_update_arp_macro

        ; process ex macro
        bit CHAN_FLAG_BIT_EX_MACRO, b
        call nz, music_update_ex_macro

        ; b now has events
        ld b, (ix + channel.events)

        ; check if we need to update the pitch of this channel
        ld a, CHAN_EVENT_PITCH_CHANGED | CHAN_EVENT_PITCH_CHANGE_ALWAYS
        and a, b
        jp nz, music_update_pitch_registers_opm
        music_update_pitch_registers_opm_done:

        ; update volume if there's been a volume change event
        ; or the master volume changed
        ld a, b
        or a, (iy + music_state.flags)
        and a, CHAN_EVENT_VOLUME_CHANGE
        jp nz, music_volume_change_opm      
        music_volume_change_opm_done:

        ; clear events
		ld a, b
		and a, CHAN_EVENT_PITCH_CHANGE_ALWAYS | CHAN_EVENT_MACRO_RELEASE | CHAN_EVENT_LEGATO | CHAN_EVENT_TIC_WAIT
        ld (ix + channel.events), a

        muco2_done:

            ; move on to next channel
            ld de, _sizeof_channel
            add ix,de

            ret
