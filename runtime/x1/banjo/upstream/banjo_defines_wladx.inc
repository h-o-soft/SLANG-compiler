.ifndef BANJO_DEFINES
.define BANJO_DEFINES 1
.define NOTE_ON		0x0
.define NOTE_OFF		0x1
.define INSTRUMENT_CHANGE		0x2
.define VOLUME_CHANGE		0x3
.define FM_DRUM		0x4
.define SN_NOISE_MODE		0x5
.define SLIDE_UP		0x6
.define SLIDE_DOWN		0x7
.define SLIDE_PORTA		0x8
.define SLIDE_OFF		0x9
.define ARPEGGIO		0xa
.define ARPEGGIO_OFF		0xb
.define VIBRATO		0xc
.define VIBRATO_OFF		0xd
.define LEGATO_ON		0xe
.define LEGATO_OFF		0xf
.define GAME_GEAR_PAN		0x10
.define AY_ENV_ON		0x11
.define AY_ENV_OFF		0x12
.define AY_ENV_SHAPE		0x13
.define AY_ENV_PERIOD_HI		0x14
.define AY_ENV_PERIOD_LO		0x15
.define AY_CHANNEL_MIX		0x16
.define AY_NOISE_PITCH		0x17
.define AY_ENV_PERIOD_WORD		0x18
.define ORDER_JUMP		0x19
.define SET_SPEED_1		0x1a
.define SET_SPEED_2		0x1b
.define ORDER_NEXT		0x1c
.define NOTE_DELAY		0x1d
.define END_LINE		0x80

.define GAME_GEAR_PORT_0		0x0
.define GAME_GEAR_PAN_PORT		0x6
.define SMS_MEMORY_CONTROL_PORT		0x3e
.define SMS_MEMORY_CONTROL_CART_ENABLE		0x40
.define SMS_MEMORY_CONTROL_RAM_ENABLE		0x10
.define SMS_MEMORY_CONTROL_IO_ENABLE		0x2
.define SMS_AUDIO_CONTROL_PORT		0xf2
.define BANJO_RDSLT		0xc
.define BANJO_WRTSLT		0x14
.define MSX_FMPAC_IO_ENABLE		0x7ff6
.define MSX_FMPAC_STRING_LOC		0x4018
.define MSX_OPM_STRING_LOC		0x80

.define SLIDE_TYPE_NONE		0x0
.define SLIDE_TYPE_UP		0x1
.define SLIDE_TYPE_DOWN		0x2
.define SLIDE_TYPE_PORTA		0x4

.define CHAN_SN76489		0x0
.define CHAN_OPLL		0x1
.define CHAN_OPLL_DRUMS		0x2
.define CHAN_AY_3_8910		0x3

.define CHAN_COUNT_SN		0x4
.define CHAN_COUNT_OPLL		0x9
.define CHAN_COUNT_OPLL_DRUMS		0xb
.define CHAN_COUNT_AY		0x3

.define CHAN_FLAG_MUTED		0x1
.define CHAN_FLAG_NOTE_ON		0x2
.define CHAN_FLAG_ARP_MACRO		0x4
.define CHAN_FLAG_VOLUME_MACRO		0x8
.define CHAN_FLAG_VIBRATO		0x10
.define CHAN_FLAG_ARPEGGIO		0x20
.define CHAN_FLAG_SLIDE		0x40
.define CHAN_FLAG_EX_MACRO		0x80

.define CHAN_FLAG_BIT_MUTED		0x0
.define CHAN_FLAG_BIT_NOTE_ON		0x1
.define CHAN_FLAG_BIT_ARP_MACRO		0x2
.define CHAN_FLAG_BIT_VOLUME_MACRO		0x3
.define CHAN_FLAG_BIT_VIBRATO		0x4
.define CHAN_FLAG_BIT_ARPEGGIO		0x5
.define CHAN_FLAG_BIT_SLIDE		0x6
.define CHAN_FLAG_BIT_EX_MACRO		0x7

.define CHAN_EVENT_PITCH_CHANGED		0x1
.define CHAN_EVENT_PITCH_CHANGE_ALWAYS		0x2
.define CHAN_EVENT_VOLUME_CHANGE		0x4
.define CHAN_EVENT_LEGATO		0x8
.define CHAN_EVENT_WAVE_CHANGED		0x10
.define CHAN_EVENT_DUTY_CHANGED		0x20
.define CHAN_EVENT_TIC_WAIT		0x40
.define CHAN_EVENT_MACRO_RELEASE		0x80

.define CHAN_EVENT_BIT_PITCH_CHANGED		0x0
.define CHAN_EVENT_BIT_PITCH_CHANGE_ALWAYS		0x1
.define CHAN_EVENT_BIT_VOLUME_CHANGE		0x2
.define CHAN_EVENT_BIT_LEGATO		0x3
.define CHAN_EVENT_BIT_WAVE_CHANGED		0x4
.define CHAN_EVENT_BIT_DUTY_CHANGED		0x5
.define CHAN_EVENT_BIT_TIC_WAIT		0x6
.define CHAN_EVENT_BIT_MACRO_RELEASE		0x7

.define MACRO_TYPE_PITCH		0x1
.define MACRO_TYPE_VOLUME		0x2
.define MACRO_TYPE_WAVE		0x10
.define MACRO_TYPE_DUTY		0x20
.define MACRO_TYPE_ARP		0x40

.define MACRO_TYPE_BIT_PITCH		0x0
.define MACRO_TYPE_BIT_VOLUME		0x1
.define MACRO_TYPE_BIT_WAVE		0x4
.define MACRO_TYPE_BIT_DUTY		0x5
.define MACRO_TYPE_BIT_ARP		0x6

.define STATE_FLAG_PROCESS_NEW_LINE		0x1
.define STATE_FLAG_LOOP		0x2
.define STATE_FLAG_MASTER_VOLUME_CHANGE		0x4
.define STATE_FLAG_ORDER_JUMP		0x8

.define STATE_FLAG_BIT_PROCESS_NEW_LINE		0x0
.define STATE_FLAG_BIT_LOOP		0x1
.define STATE_FLAG_BIT_MASTER_VOLUME_CHANGE		0x2
.define STATE_FLAG_BIT_ORDER_JUMP		0x3

.define BANJO_MAGIC_BYTE		0xba

.define SLOT_2_BANK_CHANGE		0xffff

.define BANJO_HAS_SN		0x1
.define BANJO_HAS_OPLL		0x2
.define BANJO_HAS_AY		0x4
.define BANJO_HAS_DUAL_SN		0x8
.define BANJO_HAS_OPL		0x10
.define BANJO_HAS_OPN		0x20
.define BANJO_HAS_OPNA		0x40
.define BANJO_HAS_OPM		0x80

.define BANJO_HAS_BIT_SN		0x0
.define BANJO_HAS_BIT_OPLL		0x1
.define BANJO_HAS_BIT_AY		0x2
.define BANJO_HAS_BIT_DUAL_SN		0x3
.define BANJO_HAS_BIT_OPL		0x4
.define BANJO_HAS_BIT_OPN		0x5
.define BANJO_HAS_BIT_OPNA		0x6
.define BANJO_HAS_BIT_OPM		0x7

.define BANJO_LOOP_OFF		0x0
.define BANJO_LOOP_ON		0x1

.define SYS_FLAG_GG		0x1
.define SYS_FLAG_MARK_III		0x2
.define SYS_FLAG_SYSTEM_E		0x4

.define SYS_FLAG_BIT_GG		0x0
.define SYS_FLAG_BIT_MARK_III		0x1
.define SYS_FLAG_BIT_SYSTEM_E		0x2

.STRUCT instrument
	macro_flags: db
	volume_macro_ptr: dw
	arp_macro_ptr: dw
	ex_macro_type: db
	ex_macro_ptr: dw
	fm_preset: db
	fm_patch: dw
	padding: db
.ENDST

.STRUCT channel
	flags: db
	subchannel: db		; channel number within the chip
	port: db		; output port, used for certain chips
	events: db		; 
	freq: dw		; current fnum/tone of the voice
	target_freq: db		; target fnum/tone used for portamento
	volume: db
	midi_note: db
	instrument_num: db
	effect1: db
	effect2: db
	effect3: db
	pattern_ptr: dw		; pointer to the current pattern
	wait: db		; wait for this many lines
	volume_macro_vol: db
	volume_macro_pos: db
	volume_macro_ptr: dw
	arp_macro_val: db
	arp_macro_pos: db
	arp_macro_ptr: dw
	ex_macro_val: db
	ex_macro_pos: db
	ex_macro_type: db
	ex_macro_ptr: dw
	gp1: db		; general purpose: opll patch
	gp2: db		; general purpose:
	gp3: db		; general purpose:
.ENDST

.STRUCT music_state
	magic_byte: db		; should be 0xba for a valid song
	bank: db
	channel_count: db
	flags: db
	master_volume: db
	master_volume_fade: db
	has_chips: db
	sfx_channel: db
	sfx_subchannel: db
	time_base: db
	speed_1: db
	speed_2: db
	pattern_length: db
	orders_length: db
	instrument_ptrs: dw
	order_ptrs: dw
	tic: db
	line: db
	order: db
	order_jump: db
	noise_mode: db
	panning: db
	channel_init_call: dw
	channel_update_call: dw
	channel_mute_calls: dw
	song_mute: dw
	song_stop: dw
.ENDST



.macro SFX_DEF(SFX_LABEL, SFX_PRIORITY)
    .db SFX_PRIORITY
    .db :SFX_LABEL
    .dw SFX_LABEL
.endm

.macro SONG_DEF(SONG_LABEL)
    .db 0
    .db :SONG_LABEL
    .dw SONG_LABEL
.endm

.macro SONG_CHANNELS(N)
    song_channels: INSTANCEOF channel N
.endm

.endif
