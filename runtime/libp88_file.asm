; Converted from lib/libdef/libp88_file.yml
; SLANG Runtime Library (new format)

; @name Disk_Load
; @resident shared
; @calls Disk_FileName
; HL = path Address, DE = load address
push de
call Disk_FileName
pop hl

; Disk_Load
jp $0000 + $3


; @name Disk_FileName
; @resident shared
; HL = path Address
; Disk_FileName (deにFileNameのaddressを返す)
call $0000 + $1e
xor a
.loop
ldi
cp  (hl)
jr  nz,.loop
ret


; @name Disk_Load3
; @resident shared
; HL = load address, DE = offset, BC = size

; limit 64KB
ld a,0

; Disk_Load3
jp $0000 + $9
ret


; @name Disk_Save
; @resident shared
; HL = save data address, b=cnt c=drv d=Trk e=Sec
jp $0000 + 12


; @name Disk_SecLoad
; @resident shared
; HL = load data address, b=cnt c=drv d=Trk e=Sec
jp $0000 + 33


