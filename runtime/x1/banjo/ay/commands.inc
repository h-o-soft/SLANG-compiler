
; banjo sound driver
; Joe Kennedy 2024

; commands will be jumped to from music_process_new_line

music_ay_channel_mix_masks:
    .db 0xf6
    .db 0xed
    .db 0xdb


mpnl_ay_volume_change:

    ld a, (de)

    ; preserve envelope bit if it's present
    bit 4, (ix + channel.volume)
    jr z, mpnl_ay_vc_env_off

        or a, 0x10

    mpnl_ay_vc_env_off:
    ld (ix + channel.volume), a

    ; set bit to indicate a channel volume change event
    set CHAN_EVENT_BIT_VOLUME_CHANGE, (ix + channel.events)

    jp mpnl_command_done


mpnl_ay_channel_mix:

    ; check if the channel is muted
    bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
    jp nz, mpnl_skip_2_byte_command

        push bc

        ; X1 read-modify-write: reg7(mixer) を read → mask AND → 新値 OR → write。
        ; HL は psg I/O (H=reg/L=val) に使うので、 mask 値と新値を先に B/C へ取り出しておく。
        ; DE = instruction pointer なので絶対触らない。
        ld a, (ix + channel.subchannel)
        ld hl, music_ay_channel_mix_masks
        add a, l
        ld l, a
        adc a, h
        sub a, l
        ld h, a
        ld b, (hl)              ; B = mix mask (ch ごとの AND マスク)

        ; 新しい mix 値 (= 曲データの 2 バイト目) を C へ
        ld a, (de)
        ld c, a                 ; C = new mix bits

        ; read current mixer (reg 7)
        ld h, 7
        call psg_read           ; A = 現在の reg7
        and a, b                ; mask out 該当 ch の bits
        or a, c                 ; combine with new bits

        ; write back to reg 7
        ld l, a                 ; L = new mixer value
        ld h, 7
        call psg_write

        pop bc

        jp mpnl_command_done


mpnl_ay_noise_pitch:

    ; check if the channel is muted
    bit CHAN_FLAG_BIT_MUTED, (ix + channel.flags)
    jp nz, mpnl_skip_2_byte_command

        ; X1: noise reg (6) に noise amount (曲データ 2 バイト目) を書く。 DE は触らない。
        ld a, (de)
        ld l, a                 ; L = noise amount
        ld h, 6                 ; H = noise register
        call psg_write

        jp mpnl_command_done
