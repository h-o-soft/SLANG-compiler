
; banjo sound driver
; Joe Kennedy 2025

music_volume_change_opm:

	push bc

	; hl = pointer to op slot count for this algorithm, first slot reg, and tls for this patch
	; different algoritms can require a different number of tls (output volumes) to be updated
	ld l, (ix + channel.gp2)
	ld h, (ix + channel.gp3)

	; get op slot count
	ld b, (hl)
	inc hl

	; first slot reg
	ld a, (ix + channel.subchannel)
	and a, 0x7
    add a, (hl)
    ld d, a
	inc hl

	; calculate volume w/ volume macros and master volume
	ld a, (iy + music_state.master_volume)
	ld c, 0x7f
	call music_calculate_volume_0x7f

	; keep calculated volume in e
	ld e, a

	; tl pointer into de
	; first slot reg into h
	; calculated volume now in l
	; c should still be 0x7f
	ex de, hl

	opm_slot_volume_update_loop:

        push hl

		; get tl
		ld a, (de)
		and a, c

		; apply calculated volume to the tl value
		; new_tl = tl + (0x7f - vol)
		add a, c
		sub a, l

		; check if vol > 0x7f (bit 7 set)
		jp p, muv_opm_not_neg

			; cap at 0x7f
			ld a, c

		muv_opm_not_neg:
	
		; register number in h
		; value to write in l
		ld l, a
		call banjo_opm_write

        pop hl

		; add to register number to move to next operator
		ld a, h
		add a, 0x8
		ld h, a

		djnz opm_slot_volume_update_loop

	pop bc

    jp music_volume_change_opm_done