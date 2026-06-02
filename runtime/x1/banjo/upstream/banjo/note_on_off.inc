
; banjo sound driver
; Joe Kennedy 2024

; handle note-on stuff common between all channel types
; a: midi number
; bc : jump table address (preserve this!)
; de : instruction pointer (preserve this!)
; ix: channel
; iy: music_state
music_note_on:

	; restrict to 0-127 and save in channel
	and a, 0x7f
	ld (ix + channel.midi_note), a

	; get flags in h
	ld h, (ix + channel.flags)

	; decide where to put fnum depending on portamento mode
	bit CHAN_FLAG_BIT_SLIDE, h
	jr z, mnon_no_portamento

		; is portamento on?
		bit 2, (ix + channel.effect1)
		jr z, mnon_no_portamento

			; portamento on, put fnum in target_freq
			ld (ix + channel.target_freq), a

			; check high byte of freq
			; if it's >= 0x80 that implies no notes have played so far
			; so we write to freq too, by falling through below
			bit 7, (ix + channel.freq + 1)
			jr z, mnon_done

	mnon_no_portamento:

		; portamento off, put fnum in freq
		ld (ix + channel.freq), 0
		ld (ix + channel.freq + 1), a

	mnon_done:

	; set note-on bit when the channel is not muted
	; clear note-on bit when the channel is muted
	bit CHAN_FLAG_BIT_MUTED, h
	jr nz, mno_muted

		; set note-on bit
		set CHAN_FLAG_BIT_NOTE_ON, h

		; update flags
		ld (ix + channel.flags), h

		; mark pitch and volume as requiring update
		; clear release flag
		ld a, (ix + channel.events)
		or a, CHAN_EVENT_PITCH_CHANGED | CHAN_EVENT_VOLUME_CHANGE
		and a, ~CHAN_EVENT_MACRO_RELEASE
		ld (ix + channel.events), a

		; if legato is on, we don't need to reset the macro
		bit CHAN_EVENT_BIT_LEGATO, a
		ret nz

			xor a, a

			; reset vibrato position			
			ld (ix + channel.effect1), a

			; reset macro positions
			inc a
			ld (ix + channel.ex_macro_pos), a
			ld (ix + channel.arp_macro_pos), a

			add a, 2
			ld (ix + channel.volume_macro_pos), a

			ret

	mno_muted:

		; clear note-on bit
		res CHAN_FLAG_BIT_NOTE_ON, h
		ld (ix + channel.flags), h

		; don't bother doing anything else
		ret
