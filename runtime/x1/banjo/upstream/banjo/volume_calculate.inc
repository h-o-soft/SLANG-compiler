
; banjo sound driver
; Joe Kennedy 2024

; used for chips with volume range 0-f
; a : master volume
; c : volume mask
music_calculate_volume_0x0f:

	rra

; used for chips with volume range 0-1f
; a : master volume
; c : volume mask
music_calculate_volume_0x1f:

	rra
	
; used for chips with volume range 0-3f
; a : master volume
; c : volume mask
music_calculate_volume_0x3f:

	rra

; used for chips with volume range 0-7f
; a : master volume
; c : volume mask
music_calculate_volume_0x7f:
	
	; mask off unused upper bits
	and a, c

	; subtract master volume 
	; master volume should be an attenuation
	; a = channel volume - master_volume
	neg
	add a, (ix + channel.volume)

	; subtract volume macro
    sub a, (ix + channel.volume_macro_vol)

	; we're done if the volume is positive
    ret p

		; zero it if the volume is negative
		xor a, a
		ret