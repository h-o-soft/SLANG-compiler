
; banjo sound driver
; Joe Kennedy 2025

; ix: channel
; iy: music state
banjo_mute_channel_opm:

	ret

; ix: channel
; iy: music state
banjo_unmute_channel_opm:
        
    ; clear muted bit
    res CHAN_FLAG_BIT_MUTED, (ix + channel.flags)

    ret

; mute all FM channels by sending key-offs
banjo_mute_all_opm:
	

	ret