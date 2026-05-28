
VAR I;

CONST SOUNDDATA = SNDADR;

MACHINE AKG_INIT(1):$C300;
MACHINE AKG_PLAY(1):$C303;
MACHINE AKG_PROC(0):$C318;

VSYNC_PROC()
{
	//AKG_PROC();
}


MAIN()
{
	MEM[$C300] = $A5;
	PRINT("SGL_INIT",/);
	SGL_INIT();
	PRINT("Done.",/);

	PRINT("FOPEN",/);
	FOPEN(0,"PSGAKG.BIN",0);
	PRINT("FREAD",/);
	FREAD(0,$C300,4000);
	PRINT("FCLOSE",/);
	FCLOSE(0);
	PRINT("Done.",/);
	FOR I=0 TO 15
	{
		PRINT(HEX2$(MEM[$C300+I]),",");
	}
#ASM
	LD BC,($EE8C)
	LD ($C31E),BC
	LD HL,$EEC0
	LD ($C320),HL
#END
	AKG_INIT(1);	// USE CTC
	AKG_PLAY(SOUNDDATA);
	loop{
		VSYNC(1);
	}
}

#ASM

SNDADR:
StartDisarkGenerateExternalLabel:

DisarkByteRegionStart0:
    db "AT30"
DisarkPointerRegionStart1:
    dw ArpeggioTable    ; The address of the Arpeggio table.
    dw PitchTable    ; The address of the Pitch table.
    dw InstrumentTable    ; The address of the Instrument table.
    dw EffectBlockTable    ; The address of the Effect Block table.
DisarkPointerRegionEnd1:


; The addresses of each Subsong.
DisarkPointerRegionStart2:
    dw Subsong0_Start
DisarkPointerRegionEnd2:

; Declares all the Arpeggios.
ArpeggioTable:
DisarkPointerRegionStart3:
DisarkPointerRegionEnd3:

; Declares all the Pitches.
PitchTable:
DisarkPointerRegionStart4:
DisarkPointerRegionEnd4:

; Declares all the Instruments.
InstrumentTable:
DisarkPointerRegionStart5:
    dw EmptyInstrument
    dw Instrument1
DisarkPointerRegionEnd5:

EmptyInstrument:
    db 0    ; The speed (>0, 0 for 256).

    db 0    ; No Soft no Hard. Volume: 0. Noise? false.

    db 6    ; Loop to silence.

Instrument1:
    db 1    ; The speed (>0, 0 for 256).

    db 249    ; Soft only. Volume: 15. Volume only.

    db 241    ; Soft only. Volume: 14. Volume only.

    db 233    ; Soft only. Volume: 13. Volume only.

    db 225    ; Soft only. Volume: 12. Volume only.

    db 217    ; Soft only. Volume: 11. Volume only.

    db 209    ; Soft only. Volume: 10. Volume only.

    db 201    ; Soft only. Volume: 9. Volume only.

    db 193    ; Soft only. Volume: 8. Volume only.

    db 185    ; Soft only. Volume: 7. Volume only.

    db 177    ; Soft only. Volume: 6. Volume only.

    db 169    ; Soft only. Volume: 5. Volume only.

    db 161    ; Soft only. Volume: 4. Volume only.

    db 153    ; Soft only. Volume: 3. Volume only.

    db 145    ; Soft only. Volume: 2. Volume only.

    db 137    ; Soft only. Volume: 1. Volume only.

    db 6    ; Loop to silence.


; The indexes of the effect blocks used by this song.
EffectBlockTable:
DisarkPointerRegionStart6:
    dw EffectBlock_P4P0    ; Index 0
    dw EffectBlock_P4P2    ; Index 1
DisarkPointerRegionEnd6:
EffectBlock_P4P0:
    db 4
    db 0

EffectBlock_P4P2:
    db 4
    db 2


DisarkByteRegionEnd0:

; Subsong 0
; ----------------------
Subsong0_DisarkByteRegionStart0:
Subsong0_Start:
    db 2    ; ReplayFrequency (0=12.5hz, 1=25hz, 2=50hz, 3=100hz, 4=150hz, 5=300hz).
    db 1    ; Digichannel (>=0).
    db 1    ; PSG count (>0).
    db 0    ; Loop start index (>=0).
    db 0    ; End index (>=0).
    db 8    ; Initial speed (>=0).
    db 29    ; Base note index (>=0).

Subsong0_Linker:
Subsong0_DisarkPointerRegionStart1:
; Position 0
Subsong0_Linker_Loop:
    dw Subsong0_Track0
    dw Subsong0_Track1
    dw Subsong0_Track2
    dw Subsong0_LinkerBlock0
Subsong0_DisarkPointerRegionEnd1:
    dw 0    ; Loop.
Subsong0_DisarkWordForceReference2:
    dw Subsong0_Linker_Loop

Subsong0_LinkerBlock0:
    db 64    ; Height.
    db 0    ; Transposition 0.
    db 0    ; Transposition 1.
    db 0    ; Transposition 2.
Subsong0_DisarkWordForceReference3:
    dw Subsong0_SpeedTrack0    ; SpeedTrack address.
Subsong0_DisarkWordForceReference4:
    dw Subsong0_EventTrack0    ; EventTrack address.


Subsong0_Track0:
    db 223
    db 1    ; New Instrument (1).
    db 0    ; Index to an effect block.
    db 60    ; Waits for 1 line.

    db 31
    db 60    ; Waits for 1 line.

    db 35
    db 60    ; Waits for 1 line.

    db 38
    db 61
    db 8    ; Waits for 9 lines.

    db 36
    db 60    ; Waits for 1 line.

    db 35
    db 60    ; Waits for 1 line.

    db 36
    db 60    ; Waits for 1 line.

    db 35
    db 61
    db 8    ; Waits for 9 lines.

    db 31
    db 60    ; Waits for 1 line.

    db 31
    db 60    ; Waits for 1 line.

    db 35
    db 60    ; Waits for 1 line.

    db 38
    db 61
    db 8    ; Waits for 9 lines.

    db 36
    db 60    ; Waits for 1 line.

    db 35
    db 60    ; Waits for 1 line.

    db 33
    db 60    ; Waits for 1 line.

    db 31
    db 61
    db 127    ; Waits for 128 lines.


Subsong0_Track1:
    db 218
    db 1    ; New Instrument (1).
    db 1    ; Index to an effect block.
    db 62 + 1 * 64    ; Optimized wait for 3 lines.

    db 26
    db 62 + 3 * 64    ; Optimized wait for 5 lines.

    db 47
    db 60    ; Waits for 1 line.

    db 55
    db 62 + 1 * 64    ; Optimized wait for 3 lines.

    db 24
    db 62 + 1 * 64    ; Optimized wait for 3 lines.

    db 19
    db 62 + 3 * 64    ; Optimized wait for 5 lines.

    db 55
    db 50
    db 43
    db 60    ; Waits for 1 line.

    db 38
    db 60    ; Waits for 1 line.

    db 26
    db 62 + 1 * 64    ; Optimized wait for 3 lines.

    db 26
    db 62 + 3 * 64    ; Optimized wait for 5 lines.

    db 47
    db 60    ; Waits for 1 line.

    db 55
    db 62 + 1 * 64    ; Optimized wait for 3 lines.

    db 31
    db 60    ; Waits for 1 line.

    db 30
    db 60    ; Waits for 1 line.

    db 28
    db 60    ; Waits for 1 line.

    db 23
    db 61
    db 127    ; Waits for 128 lines.


Subsong0_Track2:
    db 199
    db 1    ; New Instrument (1).
    db 0    ; Index to an effect block.
    db 62 + 1 * 64    ; Optimized wait for 3 lines.

    db 19
    db 62 + 1 * 64    ; Optimized wait for 3 lines.

    db 7
    db 62 + 1 * 64    ; Optimized wait for 3 lines.

    db 19
    db 60    ; Waits for 1 line.

    db 26
    db 60    ; Waits for 1 line.

    db 12
    db 60    ; Waits for 1 line.

    db 12
    db 60    ; Waits for 1 line.

    db 12
    db 60    ; Waits for 1 line.

    db 12
    db 60    ; Waits for 1 line.

    db 14
    db 62 + 1 * 64    ; Optimized wait for 3 lines.

    db 14
    db 60    ; Waits for 1 line.

    db 16
    db 18
    db 7
    db 62 + 1 * 64    ; Optimized wait for 3 lines.

    db 19
    db 62 + 1 * 64    ; Optimized wait for 3 lines.

    db 7
    db 62 + 1 * 64    ; Optimized wait for 3 lines.

    db 19
    db 60    ; Waits for 1 line.

    db 26
    db 60    ; Waits for 1 line.

    db 12
    db 60    ; Waits for 1 line.

    db 11
    db 60    ; Waits for 1 line.

    db 9
    db 60    ; Waits for 1 line.

    db 7
    db 62 + 1 * 64    ; Optimized wait for 3 lines.

    db 7
    db 60    ; Waits for 1 line.

    db 7
    db 61
    db 127    ; Waits for 128 lines.


; The speed tracks
Subsong0_SpeedTrack0:
    db 255    ; Wait for 128 lines.

; The event tracks
Subsong0_EventTrack0:
    db 255    ; Wait for 128 lines.

Subsong0_DisarkByteRegionEnd0:



#END

