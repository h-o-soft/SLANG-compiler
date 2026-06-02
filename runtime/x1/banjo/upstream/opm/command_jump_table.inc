
; banjo sound driver
; Joe Kennedy 2025

; jumps to code to handle commands
music_command_jump_table_opm:

	jp mpnl_note_on_opm
	jp mpnl_note_off_opm
	jp mpnl_opm_instrument_change
	jp mpnl_opm_volume_change
	jp mpnl_skip_2_byte_command		; mpnl_opll_drum
	jp mpnl_skip_2_byte_command     ; mpnl_sn_noise_mode
	jp mpnl_pitch_slide_up
	jp mpnl_pitch_slide_down
	jp mpnl_portamento
	jp mpnl_slide_off
	jp mpnl_arpeggio
	jp mpnl_arpeggio_off
	jp mpnl_vibrato
	jp mpnl_vibrato_off
	jp mpnl_legato_on
	jp mpnl_legato_off
	jp mpnl_opm_pan
    jp mpnl_skip_1_byte_command     ; mpnl_ay_env_on
	jp mpnl_skip_1_byte_command     ; mpnl_ay_env_off
    jp mpnl_skip_2_byte_command     ; mpnl_ay_env_shape
    jp mpnl_skip_2_byte_command     ; mpnl_ay_env_period_hi
	jp mpnl_skip_2_byte_command     ; mpnl_ay_env_period_lo
	jp mpnl_opm_noise
	jp mpnl_opm_lfo_rate
	jp mpnl_opm_lfo_wave
	jp mpnl_order_jump
	jp mpnl_set_speed_1
	jp mpnl_set_speed_2
    jp mpnl_order_next
    jp mpnl_note_delay
	jp mpnl_note_release
