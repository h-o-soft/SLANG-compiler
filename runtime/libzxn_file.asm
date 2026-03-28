; Converted from /home/user/SLANG-compiler/lib/libdef/libzxn_file.yml
; SLANG Runtime Library (new format)

; @name FOPEN
; @calls ZXNCALLS,ZXNWORK
; HL = fname addr, DE = mode
PUSH IX

LD A,'*'
PUSH HL
POP IX
LD B,E

LD DE,FHEADER

EXX
RST $08
DB $9A  ; F_OPEN

LD L,A
LD H,0
POP IX

RET


; ; HL=fnum DE=fname addr BC=mode/open action/create action

; ; modeは3つの情報をマージした値となっている
; ; 0-2bit
; ; CONST DOS_MODE_EX_READ = 1;
; ; CONST DOS_MODE_EX_WRITE = 2;
; ; CONST DOS_MODE_EX_READWRITE = 3;
; ; CONST DOS_MODE_SHARED_READ = 5;
; ; CONST DOS_MODE_SHARED_WRITE = 6;
; ; CONST DOS_MODE_SHARED_READWRITE = 7;
; ;
; ; 3-5bit
; ; CONST DOS_OPENACT_ERROR = 0;
; ; CONST DOS_OPENACT_HEADER = (1 << 3);
; ; CONST DOS_OPENACT_NOHEADER = (2 << 3);
; ; CONST DOS_OPENACT_BACKUP = (3 << 3);
; ; CONST DOS_OPENACT_CREATE = (4 << 3);
; ;
; ; 6-7bit
; ; CONST DOS_CREATEACT_ERROR = 0;
; ; CONST DOS_CREATEACT_HEADER = (1 << 6);
; ; CONST DOS_CREATEACT_NOHEADER = (2 << 6);

; EX DE,HL
; LD B,L
; ; B = File number 0...15
; ; HL = fname address
; ; C  = mode
; LD D,C  ; backup mode

; ; mode
; LD A,C
; AND 7
; LD C,A

; ; open action
; LD A,D
; SRA A
; SRA A
; SRA A
; AND 7
; LD E,A

; ; create action
; LD A,D
; RLCA
; RLCA
; AND 3
; LD D,A

; PUSH IX

; EXX
; LD DE,DOS_OPEN
; LD C,0
; RST $08
; DB $94
; ; CALL DOS_OPEN


; POP IX
; JR NC,.error
; LD A,0
; JR .end
; .error
; .end
; LD L,A
; RET


; @name FCLOSE
; @calls ZXNCALLS,ZXNWORK
; HL = File Handle
LD A,L

EXX
RST $08
DB $9B  ; F_CLOSE

LD L,A
LD H,0
RET


; PUSH IX

; EXX
; LD DE,DOS_CLOSE
; LD C,0
; RST $08
; DB $94
; ;CALL DOS_CLOSE

; POP IX

; JR NC,.error
; LD A,0
; JR .end
; .error
; .end
; LD L,A

; RET


; @name FSEEK
; @calls ZXNCALLS,ZXNWORK
; HL = file handle, DE = bytes to seek address, BC = mode
PUSH IX

EX DE,HL
PUSH BC
POP IX

LD A,E
; HL = bytes to seek address
LD E,(HL)
INC HL
LD D,(HL)
INC HL
LD C,(HL)
INC HL
LD B,(HL)

EXX
RST $08
DB $9F  ; F_SEEK

EX DE,HL
LD E,C
LD D,B

POP IX
RET


; @name FSTAT
; @calls ZXNCALLS,ZXNWORK
; HL = file handle, DE = stat address
PUSH IX

LD A,L
PUSH DE
POP IX

EXX
RST $08
DB $A1  ; F_STAT

POP IX
RET


; @name FREAD
; @calls ZXNCALLS,ZXNWORK
; HL = file handle, DE = address, BC = size
PUSH IX
LD A,L
PUSH DE
POP IX

EXX
RST $08
DB $9D    ; F_READ

POP IX;
RET


; @name FWRITE
; @calls ZXNCALLS,ZXNWORK
; HL = file handle, DE = address, BC = size
PUSH IX
LD A,L
PUSH DE
POP IX

EXX
RST $08
DB $9E    ; F_WRITE

POP IX;
RET


