; Converted from lib/libdef/libzxn_nextdaw.yml
; SLANG Runtime Library (new format)

; @name ZXNDAWCALLS
; @resident shared
_NextDAW_PlayerAddr            EQU $E000                      ; Driver code address
_NextDAW_InitSong              EQU _NextDAW_PlayerAddr+(3*0)    ; Initialize/set song to play.
_NextDAW_UpdateSong            EQU _NextDAW_PlayerAddr+(3*1)    ; Call once per frame (NextDAW will automatically update at either 50Hz or 60Hz, depending on the Next's configuration).
_NextDAW_PlaySong              EQU _NextDAW_PlayerAddr+(3*2)    ; Start song.
_NextDAW_StopSong              EQU _NextDAW_PlayerAddr+(3*3)    ; Stop song - update must still be called each frame as the notes will not release otherwise.
_NextDAW_StopSongHard          EQU _NextDAW_PlayerAddr+(3*4)    ; Stop song and cut off voices immediately.
_NextDAW_UpdateSongNoAY        EQU _NextDAW_PlayerAddr+(3*5)
_NextDAW_UpdateAY              EQU _NextDAW_PlayerAddr+(3*6)
_NextDAW_InitSystem            EQU _NextDAW_PlayerAddr+(3*7)
_NextDAW_InitSFXBank           EQU _NextDAW_PlayerAddr+(3*8)
_NextDAW_PlaySFX               EQU _NextDAW_PlayerAddr+(3*9)
_NextDAW_UpdateSFX             EQU _NextDAW_PlayerAddr+(3*10)
_NextDAW_GetPSGDataPtr         EQU _NextDAW_PlayerAddr+(3*11)
_NextDAW_EnablePSGWrite        EQU _NextDAW_PlayerAddr+(3*12)   ; a: 0 = disable, 1 = enable


; @name NextDAW_InitSystem
; @resident shared
; @calls ZXNDAWCALLS
; L = mmu1
; E = mmu2
; C = mmu3
          ; l = mmu1
LD H,E    ; h = mmu2
          ; c = mmu3
PUSH IX
PUSH IY
CALL _NextDAW_InitSystem
POP IY
POP IX
RET


; @name NextDAW_InitSong
; @resident shared
; @calls ZXNDAWCALLS
; hl = data mapping table
; e  = force mono

LD A,E
EX DE,HL  ; DE = data mapping table

PUSH IX
PUSH IY
; de        song data pages
; a         force AY mono (bits 0,1,2 control AY 1,2,3.  Set to force to mono, otherwise use song default)
call _NextDAW_InitSong

POP IY
POP IX
RET


; @name NextDAW_UpdateSong
; @resident shared
; @calls ZXNDAWCALLS
PUSH IX
PUSH IY

call _NextDAW_UpdateSong

POP IY
POP IX
RET


; @name NextDAW_PlaySong
; @resident shared
; @calls ZXNDAWCALLS
PUSH IX
PUSH IY

call _NextDAW_PlaySong

POP IY
POP IX
RET


; @name NextDAW_StopSong
; @resident shared
; @calls ZXNDAWCALLS
PUSH IX
PUSH IY

call _NextDAW_StopSong

POP IY
POP IX
RET


; @name NextDAW_StopSongHard
; @resident shared
; @calls ZXNDAWCALLS
PUSH IX
PUSH IY

call _NextDAW_StopSongHard

POP IY
POP IX
RET


; @name NextDAW_UpdateSongNoAY
; @resident shared
; @calls ZXNDAWCALLS
PUSH IX
PUSH IY

call _NextDAW_UpdateSongNoAY

POP IY
POP IX
RET


; @name NextDAW_UpdateAY
; @resident shared
; @calls ZXNDAWCALLS
PUSH IX
PUSH IY

call _NextDAW_UpdateAY

POP IY
POP IX
RET


; @name NextDAW_InitSFXBank
; @resident shared
; @calls ZXNDAWCALLS
; L = bank index [0..3]
; E = sfx bank data page
; BC = sfx bank data ptr

LD D,B
LD B,E
LD E,C
LD C,L

; c         bank index
; b         sfx bank data page
; de        sfx bank data ptr
PUSH IX
PUSH IY

call _NextDAW_InitSFXBank

POP IY
POP IX
RET


; @name NextDAW_PlaySFX
; @resident shared
; @calls ZXNDAWCALLS
; L = bank index [0..3]
; E = sfx index [0..63]

LD H,L
LD L,E
; h = bank index [0..3], l = sfx index [0...63]
PUSH IX
PUSH IY

call _NextDAW_PlaySFX

POP IY
POP IX
RET


; @name NextDAW_UpdateSFX
; @resident shared
; @calls ZXNDAWCALLS
PUSH IX
PUSH IY

call _NextDAW_UpdateSFX

POP IY
POP IX
RET


; @name NextDAW_GetPSGDataPtr
; @resident shared
; @calls ZXNDAWCALLS
PUSH IX
PUSH IY

call _NextDAW_GetPSGDataPtr

POP IY
POP IX
RET


; @name NextDAW_EnablePSGWrite
; @resident shared
; @calls ZXNDAWCALLS
PUSH IX
PUSH IY

call _NextDAW_EnablePSGWrite

POP IY
POP IX
RET


