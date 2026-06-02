
; banjo sound driver
; Joe Kennedy 2024

; out = ""; for (i = 0; i < 16; i++) { row = []; for (j = 0; j < 16; j++) { row.push(Math.floor(Math.sin((Math.PI/32) * j) * (i + 1) * 8));} out += ("\t.db " + row.join(",") + "\n"); } console.log(out);

.ifndef BANJO_DISABLE_VIBRATO

; quarter of sine table
; 16 rows, one for each vibrato amplitude
; 16 values per row, other quadrants of sine are calculated
music_vibrato_table:
	.db 0,0,1,2,3,3,4,5,5,6,6,7,7,7,7,7
	.db 0,1,3,4,6,7,8,10,11,12,13,14,14,15,15,15
	.db 0,2,4,6,9,11,13,15,16,18,19,21,22,22,23,23
	.db 0,3,6,9,12,15,17,20,22,24,26,28,29,30,31,31
	.db 0,3,7,11,15,18,22,25,28,30,33,35,36,38,39,39
	.db 0,4,9,13,18,22,26,30,33,37,39,42,44,45,47,47
	.db 0,5,10,16,21,26,31,35,39,43,46,49,51,53,54,55
	.db 0,6,12,18,24,30,35,40,45,49,53,56,59,61,62,63
	.db 0,7,14,20,27,33,40,45,50,55,59,63,66,68,70,71
	.db 0,7,15,23,30,37,44,50,56,61,66,70,73,76,78,79
	.db 0,8,17,25,33,41,48,55,62,68,73,77,81,84,86,87
	.db 0,9,18,27,36,45,53,60,67,74,79,84,88,91,94,95
	.db 0,10,20,30,39,49,57,65,73,80,86,91,96,99,102,103
	.db 0,10,21,32,42,52,62,71,79,86,93,98,103,107,109,111
	.db 0,11,23,34,45,56,66,76,84,92,99,105,110,114,117,119
	.db 0,12,24,37,48,60,71,81,90,98,106,112,118,122,125,127

; ix: current channel
; iy: music state
music_update_vibrato:

    ; add to counter, wraps around at 64
    ld a, (ix + channel.effect1)
    add a, (ix + channel.effect2)
    and a, 0x3f

    ; store new value and keep it in c
    ld (ix + channel.effect1), a
    ld c, a

    ; lower 4 bits value will be used as index 
    ; to look up within a row of music_vibrato_table
    and a, 0xf

    ; reverse index into table when counter is between 16-31 and 48-63
    ; if bit 4 is set (16)
    bit 4, c
    jr z, mvu_no_reverse

        neg
        add a, 0xf

    mvu_no_reverse:

    ; index into the table
    ; vibrato_depth used to select a row within music_vibrato_table
    or a, (ix + channel.effect3)
    add a, <music_vibrato_table
    ld e, a
    adc a, >music_vibrato_table
    sub a, e
    ld d, a

    ; get the value
    ld a, (de)

    ; vibrato amount == 0
    or a, a
    jr z, mvu_no_invert

    ; invert value from table when counter is between 32 and 63
    bit 5, c
    jr z, mvu_no_invert

		; upper byte of vibrato
        ld d, 0xff
		
		; lower byte of vibrato
		neg
        ld e, a
		
		ret

    mvu_no_invert:

		; upper byte of vibrato
		ld d, 0
		
        ; lower byte of vibrato
        ld e, a

		ret

.endif

.ifdef BANJO_DISABLE_VIBRATO

music_update_vibrato:
    ret

.endif