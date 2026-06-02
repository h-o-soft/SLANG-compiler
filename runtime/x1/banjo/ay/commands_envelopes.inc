
; banjo sound driver
; Joe Kennedy 2024

; commands will be jumped to from music_process_new_line

; switch envelopes on and update envelope bit in channel volume
mpnl_ay_env_on:

    ; check if the channel is muted
    bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
    jp nz, mpnl_skip_1_byte_command

        ; X1 read-modify-write: volume reg を read → envelope bit(4) set → write。
        ; H に reg を保持したまま psg_read→psg_write (psg_read は HL 非破壊)。 DE は触らない。
        ld a, (ix + channel.subchannel)
        add a, 8
        ld h, a                 ; H = volume register (8/9/10)
        call psg_read           ; A = 現在 volume (H 保持)
        or a, 0x10              ; envelope bit set
        ld l, a                 ; L = new volume
        call psg_write          ; H=reg のまま書込

        ; update stored volume on channel with envelope bit set
        set 4, (ix + channel.volume)

        jp mpnl_command_done_one_byte_command


; switch envelopes off and update envelope bit in channel volume
mpnl_ay_env_off:

	; check if the channel is muted
	bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
	jp nz, mpnl_skip_1_byte_command

        ; X1 read-modify-write: volume reg を read → envelope bit(4) clear → write。
        ld a, (ix + channel.subchannel)
        add a, 8
        ld h, a                 ; H = volume register
        call psg_read           ; A = 現在 volume (H 保持)
        and a, 0xf              ; envelope bit clear (下位 4bit のみ残す)
        ld l, a                 ; L = new volume
        call psg_write

        ; update stored volume on channel with envelope bit cleared
        res 4, (ix + channel.volume)

        jp mpnl_command_done_one_byte_command


; change shape of envelope
mpnl_ay_env_shape:

	; check if the channel is muted
	bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
	jp nz, mpnl_skip_2_byte_command

        ; X1: envelope shape reg (13) に shape (曲データ 2 バイト目) を書く。 DE は触らない。
        ld a, (de)
        ld l, a                 ; L = shape
        ld h, 13                ; H = envelope shape register
        call psg_write

        jp mpnl_command_done


; set high bit of envelope period
mpnl_ay_env_period_hi:

	; check if the channel is muted
	bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
	jp nz, mpnl_skip_2_byte_command

        ; X1: envelope period high byte reg (12) に値 (曲データ 2 バイト目) を書く。
        ld a, (de)
        ld l, a                 ; L = period high
        ld h, 12                ; H = envelope period high register
        call psg_write

        jp mpnl_command_done


; set low bit of envelope period
mpnl_ay_env_period_lo:

	; check if the channel is muted
	bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
	jp nz, mpnl_skip_2_byte_command

        ; X1: envelope period low byte reg (11) に値 (曲データ 2 バイト目) を書く。
        ld a, (de)
        ld l, a                 ; L = period low
        ld h, 11                ; H = envelope period low register
        call psg_write

        jp mpnl_command_done


; set both high and low bytes of envelope period
mpnl_ay_env_period_word:

	; check if the channel is muted
	bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
	jp nz, mpnl_skip_3_byte_command

        ; X1: envelope period word = low (reg 11) + high (reg 12)。 曲データ 2 バイト目=low、
        ; 3 バイト目=high。 元コードと同じく中間で inc de して 3 バイト目へ進む (psg_write は DE 非破壊)。
        ld a, (de)              ; param1 = period low
        ld l, a
        ld h, 11                ; reg 11 = envelope period low
        call psg_write

        inc de                  ; advance to param2 (元コードと同じ DE 操作)
        ld a, (de)              ; param2 = period high
        ld l, a
        ld h, 12                ; reg 12 = envelope period high
        call psg_write

        jp mpnl_command_done



