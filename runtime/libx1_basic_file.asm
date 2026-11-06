; libx1_basic_file.asm
; SLANG x1native_hufs runtime — hubasic2sl のファイル命令 (BAS_F 接頭辞。docs/HUFS.md・docs/HUBASIC.md)
;
; x1native_hufs の env だけが持つ。ファイル API (libx1native_hufs.asm の FOPEN など) の上に、Hu-BASIC の
; OPEN・CLOSE・PRINT#・WRITE#・INPUT#・LINE INPUT#・EOF・KILL・NAME・MAXFILES の意味を乗せる。
;
; libx1_basic.asm の #if exists HUFS の中 (BAS_PCHR・BAS_PNL・BAS_PZONE・BAS_PRTAB・BAS_ERROR・BAS_END) が、
; 出力先 (BAS_FOUTN) と、ここの関数 (BAS_FPCHR など) を使う。それらの記号は、hubasic2sl の出力が始めに必ず呼ぶ
; BAS_FINIT の @calls からすべてたどれるようにしてある (libx1_basic.asm の関数の @calls・@works には足さない)。
;
; ファイル番号 n (1〜15) は、ファイル API の番号 n-1 に写す (1・2 はライブラリの緩衝、3 から後ろは hubasic2sl が
; 静的に取って FSETBUF で渡す)。番号ごとの状態 (BAS_FST、2 バイト):
;   +0 0 = 閉じている、1 = 書く ("O"・"A")、2 = 読む ("I")
;   +1 PRINT# の桁 (書いた字で 1 ずつ進み、CR で 0。, の区切りは 8 桁ごと。Hu-BASIC の実測)
; 順ファイルの終わりは $1A (CLOSE で書く。読むときは $1A か物理の終わりで終わり)。
; エラーの番号と文は Hu-BASIC の実測 (ファイル API のエラーは BAS_FCHK で写す)。


; ============================================================ ワーク

; @name BAS_FILEWK
; @resident shared
; @param_count 0
; @works BAS_FOUTN:1,BAS_FCUR:1,BAS_FMAX:1,BAS_FCAP:1,BAS_FDONE:1,BAS_FQUIET:1,BAS_FLAST:1,BAS_FFIRST:2,BAS_FST:30,BAS_FNZ1:32,BAS_FNZ2:32
; BAS_FOUTN: PRINT#・WRITE# の出力先のファイル番号 (0 = 画面)、BAS_FCUR: INPUT# で読むファイルの API の番号
; BAS_FMAX: いまの MAXFILES (始めは 2)、BAS_FCAP: 緩衝を取った本数 (hubasic2sl が BAS_FINIT に渡す)
; BAS_FDONE: 終わりでファイルを閉じたら 1、BAS_FQUIET: エラーで止まるところなら 1 (閉じるときのエラーを出さない)
; BAS_FLAST: 直前の BAS_FGETB がバイトを読んだら 1 (物理の終わりなら 0。BAS_FUNGET が戻す)
; BAS_FFIRST: 全部閉じるときの最初のエラー、BAS_FST: 番号ごとの状態、BAS_FNZ1・BAS_FNZ2: 0 で終わる名前
RET


; ============================================================ 組の印と始め

; @name BAS_FABI_1
; @resident shared
; @param_count 0
; hubasic2sl の出力 (x1native_hufs で組み立てるもの) と、このランタイムのファイルの命令の組の印 (何もしない)。
; 約束を変えたら、ここの番号と hubasic2sl の SlangBackend.BasFileAbi を一緒に 1 つ上げる
RET


; @name BAS_FINIT
; @resident shared
; @param_count 1
; @calls BAS_FILEWK,BAS_FPCHR,BAS_FNL,BAS_FZONE,BAS_FPTAB,BAS_FERRHOOK,BAS_FERRMSG,BAS_FENDHOOK
; HL = 緩衝を取った本数 (2〜15)。x1native_hufs の出力が始めに必ず呼ぶ。libx1_basic.asm の #if exists HUFS の中が
; 使う記号は、すべてこの @calls からたどれる (この関数を呼ばない出力を x1native_hufs で組み立てると、アセンブルで止まる)
LD A, L
LD (BAS_FCAP), A
LD A, 2
LD (BAS_FMAX), A
RET


; ============================================================ 下回り

; @name BAS_FCHK
; @resident shared
; @param_count 1
; @calls BAS_ERROR
; HL = ファイル API の戻り値。エラー ($FF01〜) なら Hu-BASIC のエラーにして止まる。でなければ何もしない
LD A, H
INC A
RET NZ
LD A, L
DEC A
CP 14
JP C, .bfc_map
LD A, 8                 ; 分からない番号は Device I/O error
.bfc_map
LD E, A
LD D, 0
LD HL, .bfc_tab
ADD HL, DE
LD L, (HL)
LD H, 0
JP BAS_ERROR
; $FF01 ない 53、$FF02 いっぱい 60、$FF03 番号 52、$FF04 開いている 54、$FF05 開いていない 71、$FF06 モード 30、
; $FF07 名前 65、$FF08 ディレクトリがいっぱい 60、$FF09 FDC 56、$FF0A 同じ名前 57、$FF0B 書き込み禁止 72、
; $FF0C 扱えない項目 30、$FF0D 壊れた FAT 64、$FF0E 範囲外 5
.bfc_tab
DB 53, 60, 52, 54, 71, 30, 65, 60, 56, 57, 72, 30, 64, 5


; @name BAS_FNUM
; @resident shared
; @param_count 1
; @calls BAS_FILEWK,BAS_ERROR
; HL = ファイル番号 → 1〜MAXFILES でなければエラー 52 (Bad file number)。A = 番号、HL = その状態 (BAS_FST)
LD A, H
OR A
JP NZ, .bfn_bad
LD A, L
OR A
JP Z, .bfn_bad
LD B, A
LD A, (BAS_FMAX)
CP B
JP C, .bfn_bad
LD A, B
DEC A
ADD A, A
LD E, A
LD D, 0
LD HL, BAS_FST
ADD HL, DE
LD A, B
RET
.bfn_bad
LD HL, 52
JP BAS_ERROR


; @name BAS_FSTP
; @resident shared
; @param_count 0
; @calls BAS_FILEWK
; A = ファイル番号 (1〜15) → HL = その状態 (BAS_FST)。A・DE を壊す
DEC A
ADD A, A
LD E, A
LD D, 0
LD HL, BAS_FST
ADD HL, DE
RET


; @name BAS_FNAMEZ
; @resident shared
; @param_count 2
; @calls BAS_ERROR
; HL = 名前 (BASIC の文字列)、DE = 置き場 (32 バイト) → 0 で終わる名前にする。30 文字を超えたらエラー 65
; (13 + 3 文字と "0:" と "." より長い名前は、ファイル API でもエラーになる)
LD A, (HL)
CP 31
JP NC, .bnz_bad
INC HL
OR A
JP Z, .bnz_end
LD C, A
LD B, 0
LDIR
.bnz_end
XOR A
LD (DE), A
RET
.bnz_bad
LD HL, 65
JP BAS_ERROR


; ============================================================ OPEN・CLOSE

; @name BAS_FOPEN
; @resident shared
; @param_count 3
; @calls BAS_FILEWK,BAS_FNUM,BAS_FNAMEZ,BAS_FCHK,BAS_ERROR,FOPEN,FCLOSE,FGETC,FSEEK,FSEEKREC,FSIZEREC
; OPEN モード, #番号, 名前: HL = モード (文字列。頭の字が O・I・A、ほかはエラー 30)、DE = 番号、BC = 名前。
; "O" は作る (あれば中を捨てる)、"I" は読む、"A" は後ろに足す (ないとエラー 53。本物と同じ。
; 最後のレコードの最初の $1A の所から書く。$1A がなければ終わりから)
PUSH BC
PUSH DE
LD A, (HL)
OR A
JP Z, .bfo_bad
INC HL
LD A, (HL)
AND $DF                 ; 英字を大文字に
LD C, 3
CP 'O'
JP Z, .bfo_mode
LD C, 0
CP 'I'
JP Z, .bfo_mode
LD C, 2
CP 'A'
JP Z, .bfo_mode
.bfo_bad
LD HL, 30               ; Bad file mode
JP BAS_ERROR
.bfo_mode
LD A, C
LD (.bfo_m), A
POP HL                  ; 番号
CALL BAS_FNUM
LD (.bfo_st), HL
DEC A
LD (.bfo_fn), A
LD A, (HL)
OR A
LD HL, 54               ; Already open
JP NZ, BAS_ERROR
POP HL                  ; 名前
LD DE, BAS_FNZ1
CALL BAS_FNAMEZ
LD A, (.bfo_m)
CP 2
JP NZ, .bfo_open
; "A": ある物だけ (読むので開いて確かめてから閉じる)
LD BC, 0
CALL .bfo_call
CALL .bfo_fn_hl
CALL FCLOSE
LD A, (.bfo_m)
.bfo_open
LD C, A
LD B, 0
CALL .bfo_call
LD HL, (.bfo_st)
LD A, (.bfo_m)
OR A
LD A, 2                 ; 読む
JP Z, .bfo_st2
DEC A                   ; 書く
.bfo_st2
LD (HL), A
INC HL
LD (HL), 0              ; 桁
LD A, (.bfo_m)
CP 2
RET NZ
; "A": 最後のレコードの頭から、最初の $1A を探す
CALL .bfo_fn_hl
CALL FSIZEREC
DEC HL
EX DE, HL
CALL .bfo_fn_hl
CALL FSEEKREC
LD B, 0                 ; 256 バイトまで
.bfo_find
PUSH BC
CALL .bfo_fn_hl
CALL FGETC
POP BC
LD A, H
INC A
JP NZ, .bfo_byte
INC L
RET Z                   ; $FFFF: 物理の終わり (そこから書く)
DEC L
JP BAS_FCHK             ; エラー
.bfo_byte
LD A, L
CP $1A
JP Z, .bfo_back
DJNZ .bfo_find
RET
.bfo_back
CALL .bfo_fn_hl
LD DE, $FFFF
LD BC, 1
CALL FSEEK              ; $1A の所に戻す (そこから書いて上書きする)
JP BAS_FCHK
.bfo_call               ; FOPEN(番号, 名前, BC) → エラーなら止まる
CALL .bfo_fn_hl
LD DE, BAS_FNZ1
CALL FOPEN
JP BAS_FCHK
.bfo_fn_hl
LD A, (.bfo_fn)
LD L, A
LD H, 0
RET
.bfo_m DB 0
.bfo_fn DB 0
.bfo_st DW 0


; @name BAS_FCLOSE1
; @resident shared
; @param_count 0
; @calls BAS_FILEWK,BAS_FSTP,FPUTC,FCLOSE
; A = ファイル番号 (1〜15) を閉じる (書くものは $1A を書いてから)。開いていなければ何もしない。
; HL = ファイル API の最初のエラー (なければ 0)。エラーでも閉じた扱いにする
LD (.bfc1_fn), A
CALL BAS_FSTP
LD A, (HL)
LD (HL), 0
OR A
LD HL, 0
RET Z                   ; 閉じている
LD (.bfc1_err), HL
LD B, A                 ; モード
LD A, (.bfc1_fn)
DEC A
LD (.bfc1_fn), A        ; API の番号
LD A, B
CP 1
JP NZ, .bfc1_close
LD A, (.bfc1_fn)
LD L, A
LD H, 0
LD DE, $1A
CALL FPUTC              ; 順ファイルの終わりの印
LD (.bfc1_err), HL
.bfc1_close
LD A, (.bfc1_fn)
LD L, A
LD H, 0
CALL FCLOSE
EX DE, HL
LD HL, (.bfc1_err)      ; 先のエラー ($1A を書けなかった) を優先する
LD A, H
OR L
RET NZ
EX DE, HL
RET
.bfc1_fn DB 0
.bfc1_err DW 0


; @name BAS_FCLOSEALL
; @resident shared
; @param_count 0
; @calls BAS_FILEWK,BAS_FCLOSE1
; 開いているファイルをすべて閉じる (エラーがあっても残りを閉じ続ける)。HL = 最初のエラー (なければ 0)
LD HL, 0
LD (BAS_FFIRST), HL
LD A, 1
.bfca_loop
PUSH AF
CALL BAS_FCLOSE1
LD A, H
OR L
JP Z, .bfca_next
LD DE, (BAS_FFIRST)
LD A, D
OR E
JP NZ, .bfca_next
LD (BAS_FFIRST), HL
.bfca_next
POP AF
INC A
CP 16
JP C, .bfca_loop
LD HL, (BAS_FFIRST)
RET


; @name BAS_FCLOSE
; @resident shared
; @param_count 1
; @calls BAS_FNUM,BAS_FCLOSE1,BAS_FCHK
; CLOSE #番号: HL = 番号 (1〜MAXFILES でなければエラー 52。開いていなければ何もしない。本物と同じ)
CALL BAS_FNUM
CALL BAS_FCLOSE1
JP BAS_FCHK


; @name BAS_FCLOSEA
; @resident shared
; @param_count 0
; @calls BAS_FCLOSEALL,BAS_FCHK
; CLOSE (番号なし): 開いているものをすべて閉じる
CALL BAS_FCLOSEALL
JP BAS_FCHK


; ============================================================ 書く (PRINT#・WRITE#)

; @name BAS_FOUT
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_FILEWK,BAS_FNUM,BAS_ERROR
; PRINT#・WRITE# の始め: HL = 番号。0 なら PRINT#0 と同じ (画面への生の出力)。でなければ、書くもので開いた番号を
; 出力先にする (開いていなければエラー 71、読むものならエラー 30)。BAS_PCHR などがファイルに書くようになる
LD A, H
OR L
JP NZ, .bfo_file
INC A
LD (BAS_RAW), A
RET
.bfo_file
CALL BAS_FNUM
LD B, A
LD A, (HL)
CP 1
JP Z, .bfo_ok
OR A
LD HL, 71               ; File not open
JP Z, BAS_ERROR
LD HL, 30               ; Bad file mode
JP BAS_ERROR
.bfo_ok
LD A, B
LD (BAS_FOUTN), A
RET


; @name BAS_FOUT0
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_FILEWK
; PRINT#・WRITE# の終わり: 出力を画面に戻す
XOR A
LD (BAS_FOUTN), A
LD (BAS_RAW), A
RET


; @name BAS_FPCHR
; @resident shared
; @param_count 1
; @calls BAS_FILEWK,BAS_FSTP,BAS_FCHK,FPUTC
; L = 文字を、出力先のファイル (BAS_FOUTN) にそのまま書く (BAS_PCHR から。制御コードもそのまま)。桁を進める (CR で 0)
LD E, L
PUSH DE
LD A, (BAS_FOUTN)
DEC A
LD L, A
LD H, 0
LD D, H
CALL FPUTC
CALL BAS_FCHK
LD A, (BAS_FOUTN)
CALL BAS_FSTP           ; DE を壊すので、書いた字はスタックから取り直す
POP DE
INC HL
LD A, E
CP $0D
JP Z, .bfp_cr
INC (HL)
RET
.bfp_cr
LD (HL), 0
RET


; @name BAS_FNL
; @resident shared
; @param_count 0
; @calls BAS_FPCHR
; 改行 (ファイルには CR だけ。BAS_PNL から)
LD L, $0D
JP BAS_FPCHR


; @name BAS_FZONE
; @resident shared
; @param_count 0
; @calls BAS_FILEWK,BAS_FSTP,BAS_FPCHR
; PRINT# の , : 次の 8 桁の区切りまで空白 (画面は 10 桁。Hu-BASIC の実測。BAS_PZONE から)
LD A, (BAS_FOUTN)
CALL BAS_FSTP
INC HL
LD A, (HL)
AND $F8
ADD A, 8
LD B, A                 ; 次の区切り (248 から後ろは 0 になり、何も出さない)
.bfz_loop
LD A, (BAS_FOUTN)
CALL BAS_FSTP
INC HL
LD A, (HL)
CP B
RET NC
PUSH BC
LD L, ' '
CALL BAS_FPCHR
POP BC
JP .bfz_loop


; @name BAS_FPTAB
; @resident shared
; @param_count 1
; @calls BAS_FILEWK,BAS_FSTP,BAS_PSPC,BAS_ERROR
; PRINT# の TAB(n): HL = n (0〜255、それ以外はエラー 5)。ファイルの桁が n より小さければ、差の分だけ空白 (BAS_PRTAB から)
LD A, H
OR A
JP NZ, .bft_err
PUSH HL
LD A, (BAS_FOUTN)
CALL BAS_FSTP
INC HL
LD B, (HL)
POP HL
LD A, L
SUB B
RET Z
RET C
LD L, A
JP BAS_PSPC
.bft_err
LD HL, 5
JP BAS_ERROR


; @name BAS_FWSEP
; @resident shared
; @param_count 0
; @calls BAS_PCHR
; WRITE# の項目の区切り
LD L, ','
JP BAS_PCHR


; @name BAS_FWSTR
; @resident shared
; @param_count 1
; @calls BAS_PCHR,BAS_PSTR
; WRITE# の文字列: HL = 文字列を " で囲んで書く
PUSH HL
LD L, $22
CALL BAS_PCHR
POP HL
CALL BAS_PSTR
LD L, $22
JP BAS_PCHR


; @name BAS_FWINT
; @resident shared
; @param_count 1
; @calls BAS_PCHR,BAS_UTOA,BAS_PSTRZ
; WRITE# の整数: HL = 符号付きの値を、前後の空白なしで書く (負なら '-')
BIT 7, H
JP Z, .bfwi_pos
LD A, L
CPL
LD L, A
LD A, H
CPL
LD H, A
INC HL
PUSH HL
LD L, '-'
CALL BAS_PCHR
POP HL
.bfwi_pos
CALL BAS_UTOA
JP BAS_PSTRZ


; @name BAS_FWFLT
; @resident shared
; @param_count 1
; @calls BAS_FSTR,BAS_PSTRZ
; WRITE# の浮動小数: AHL = 値を、前後の空白なしで書く (PRINT の形から頭の空白を除いたもの。.001 は .001)
CALL BAS_FSTR
LD A, (HL)
CP ' '
JP NZ, BAS_PSTRZ
INC HL
JP BAS_PSTRZ


; ============================================================ 読む (INPUT#・LINE INPUT#・EOF)

; @name BAS_FIN
; @resident shared
; @param_count 1
; @calls BAS_FILEWK,BAS_FNUM,BAS_ERROR
; INPUT#・LINE INPUT# の始め: HL = 番号。読むもので開いた番号を、読むファイルにする
; (開いていなければエラー 71、書くものならエラー 30)
CALL BAS_FNUM
LD B, A
LD A, (HL)
CP 2
JP Z, .bfi_ok
OR A
LD HL, 71
JP Z, BAS_ERROR
LD HL, 30
JP BAS_ERROR
.bfi_ok
LD A, B
DEC A
LD (BAS_FCUR), A
RET


; @name BAS_FGETB
; @resident shared
; @param_count 0
; @calls BAS_FILEWK,BAS_FCHK,FGETC
; 読むファイル (BAS_FCUR) から 1 バイト → A。物理の終わりは $1A として返す (BAS_FLAST = 0)
LD A, (BAS_FCUR)
LD L, A
LD H, 0
CALL FGETC
LD A, H
OR A
JP NZ, .bfg_sp
INC A
LD (BAS_FLAST), A
LD A, L
RET
.bfg_sp
INC HL
LD A, H
OR L
JP NZ, .bfg_err         ; $FFFF でない: エラー
LD (BAS_FLAST), A
LD A, $1A
RET
.bfg_err
DEC HL
JP BAS_FCHK


; @name BAS_FUNGET
; @resident shared
; @param_count 0
; @calls BAS_FILEWK,BAS_FCHK,FSEEK
; 直前に BAS_FGETB で読んだ 1 バイトを戻す (物理の終わりだったら何もしない)
LD A, (BAS_FLAST)
OR A
RET Z
XOR A
LD (BAS_FLAST), A
LD A, (BAS_FCUR)
LD L, A
LD H, 0
LD DE, $FFFF
LD BC, 1
CALL FSEEK
JP BAS_FCHK


; @name BAS_FITEM
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_FGETB,BAS_FUNGET,BAS_ERROR
; 読むファイルから項目を 1 つ読んで BAS_INBUF ([長さ][本体]) に入れる → HL = BAS_INBUF。A = 0 文字列 (INPUT#)、
; 1 数 (INPUT#)、2 1 行 (LINE INPUT#)。読み方は Hu-BASIC の実測:
;   - 始めに $1A か物理の終わりならエラー 61 (Input past end)
;   - INPUT# は頭の空白を飛ばす。" で始まれば (文字列でも数でも) 次の " まで、その後ろの空白と , か CR を 1 つ捨てる
;     (ほかの字なら、そこから次の項目)。そうでなければ、文字列は , か CR まで (後ろの空白は残す)、数は空白か , か CR まで
;     (空白の後ろの空白と , か CR を 1 つ捨てる)
;   - LINE INPUT# は CR まで (CR は捨てる)
;   - INPUT# の項目は 254 字まで (255 字目は読んで捨て、その後ろは次の項目になる)。LINE INPUT# は 255 字を読んだら、
;     その後ろ (CR も) を読まずに終わる (次に読む)
;   - $1A の手前で終わる (次の項目で 61)
LD (.bfit_kind), A
CP 2
LD A, 254
JP NZ, .bfit_lim
INC A
.bfit_lim
LD (.bfit_max), A
XOR A
LD (BAS_INBUF), A
CALL BAS_FGETB
LD C, A
LD A, (.bfit_kind)
CP 2
JP Z, .bfit_chk
.bfit_skip
LD A, C
CP ' '
JP NZ, .bfit_chk
CALL BAS_FGETB
LD C, A
JP .bfit_skip
.bfit_chk
LD A, C
CP $1A
JP NZ, .bfit_go
LD HL, 61               ; Input past end
JP BAS_ERROR
.bfit_go
LD A, (.bfit_kind)
CP 2
JP Z, .bfit_line
LD A, C
CP $22
JP Z, .bfit_quote       ; 数の項目も " で始まれば次の " まで (中の , も含めて 1 つ。本物と同じ)
.bfit_plain
LD A, C
CP ','
JP Z, .bfit_done
CP $0D
JP Z, .bfit_done
CP $1A
JP Z, .bfit_unget
CP ' '
JP NZ, .bfit_padd
LD A, (.bfit_kind)
OR A
JP NZ, .bfit_numsp      ; 数は空白で終わる
.bfit_padd
CALL .bfit_add
JP C, .bfit_done        ; 255 字目は捨てる
CALL BAS_FGETB
LD C, A
JP .bfit_plain
.bfit_numsp             ; 数の後ろの空白を飛ばし、, か CR を 1 つ捨てる
CALL BAS_FGETB
CP ' '
JP Z, .bfit_numsp
JP .bfit_tail
.bfit_quote
CALL BAS_FGETB
LD C, A
CP $22
JP Z, .bfit_qend
CP $0D
JP Z, .bfit_done
CP $1A
JP Z, .bfit_unget
CALL .bfit_add
JP C, .bfit_done
JP .bfit_quote
.bfit_qend              ; 閉じの " の後ろ: 空白を飛ばし、, か CR を 1 つ捨てる
CALL BAS_FGETB
CP ' '
JP Z, .bfit_qend
.bfit_tail
CP ','
JP Z, .bfit_done
CP $0D
JP Z, .bfit_done
JP .bfit_unget
.bfit_line
LD A, C
CP $0D
JP Z, .bfit_done
CP $1A
JP Z, .bfit_unget
CALL .bfit_add
LD A, (BAS_INBUF)
CP 255
JP Z, .bfit_done        ; 255 字で終わり (後ろの CR も読まない)
CALL BAS_FGETB
LD C, A
JP .bfit_line
.bfit_unget
CALL BAS_FUNGET
.bfit_done
LD HL, BAS_INBUF
RET
.bfit_add               ; C を BAS_INBUF に足す。いっぱいなら足さずに Cy = 1
LD HL, BAS_INBUF
LD A, (.bfit_max)
CP (HL)
SCF
RET Z
LD E, (HL)
INC (HL)
LD D, 0
INC HL
ADD HL, DE
LD (HL), C
OR A
RET
.bfit_kind DB 0
.bfit_max DB 0


; @name BAS_FINS
; @resident shared
; @param_count 0
; @calls BAS_FITEM
; INPUT# の次の項目を文字列として → HL (BAS_INBUF。すぐ変数に写すこと)
XOR A
JP BAS_FITEM


; @name BAS_FLINE
; @resident shared
; @param_count 0
; @calls BAS_FITEM
; LINE INPUT# の 1 行 → HL (BAS_INBUF。すぐ変数に写すこと)
LD A, 2
JP BAS_FITEM


; @name BAS_FINN
; @resident shared
; @param_count 0
; @result_type float
; @calls BAS_FITEM,BAS_SVALF
; INPUT# の次の項目を数として → AHL (VAL と同じ読み方。12AB は 12、読めなければ 0。Hu-BASIC の実測)
LD A, 1
CALL BAS_FITEM
JP BAS_SVALF


; @name BAS_FEOF
; @resident shared
; @param_count 1
; @calls BAS_FILEWK,BAS_FNUM,BAS_FGETB,BAS_FUNGET,BAS_ERROR
; EOF(番号): HL = 番号 → 次が $1A か物理の終わりなら -1、でなければ 0 (読む位置も、INPUT# で選んだ読むファイルも変えない)。
; 開いていなければエラー 71、書くものならエラー 30 (本物と同じ)
CALL BAS_FNUM
LD B, A
LD A, (HL)
CP 2
JP Z, .bfe_ok
OR A
LD HL, 71
JP Z, BAS_ERROR
LD HL, 30
JP BAS_ERROR
.bfe_ok
LD A, (BAS_FCUR)        ; 読むファイルの選びを戻す (INPUT #1,A(EOF(2)) の添字の中でも、読むのは 1 番のまま)
PUSH AF
LD A, B
DEC A
LD (BAS_FCUR), A
CALL BAS_FGETB
PUSH AF
CALL BAS_FUNGET
POP AF
POP BC
LD HL, 0
CP $1A
JP NZ, .bfe_ret
DEC HL
.bfe_ret
LD A, B
LD (BAS_FCUR), A
RET


; ============================================================ KILL・NAME・MAXFILES

; @name BAS_FKILL
; @resident shared
; @param_count 1
; @calls BAS_FILEWK,BAS_FNAMEZ,BAS_FCHK,FDELETE
; KILL 名前: HL = 名前 (開いているものはエラー 54、書き込み禁止はエラー 72。本物と同じ)
LD DE, BAS_FNZ1
CALL BAS_FNAMEZ
LD HL, BAS_FNZ1
CALL FDELETE
JP BAS_FCHK


; @name BAS_FNAME
; @resident shared
; @param_count 2
; @calls BAS_FILEWK,BAS_FNAMEZ,BAS_FCHK,FRENAME
; NAME 古い名前 AS 新しい名前: HL = 古い名前、DE = 新しい名前 (新しい名前があればエラー 57)
PUSH DE
LD DE, BAS_FNZ1
CALL BAS_FNAMEZ
POP HL
LD DE, BAS_FNZ2
CALL BAS_FNAMEZ
LD HL, BAS_FNZ1
LD DE, BAS_FNZ2
CALL FRENAME
JP BAS_FCHK


; @name BAS_FMAXF
; @resident shared
; @param_count 1
; @calls BAS_FILEWK,BAS_FCLOSEALL,BAS_FCHK,BAS_ERROR
; MAXFILES n: HL = 本数 (0〜緩衝を取った本数。それ以外はエラー 5)。開いているファイルをすべて閉じてから、使える番号を
; 1〜n にする (変数を消すのは hubasic2sl の出力。本物は開いていたファイルを書き出さずに捨てるが、こちらは閉じる)
LD A, H
OR A
JP NZ, .bfm_bad
LD A, (BAS_FCAP)
CP L
JP C, .bfm_bad
PUSH HL
CALL BAS_FCLOSEALL
CALL BAS_FCHK
POP HL
LD A, L
LD (BAS_FMAX), A
RET
.bfm_bad
LD HL, 5
JP BAS_ERROR


; @name BAS_FCLR
; @resident shared
; @param_count 2
; HL = 番地、DE = バイト数 → 0 で埋める (MAXFILES で変数を消す)
LD A, D
OR E
RET Z
LD (HL), 0
DEC DE
LD A, D
OR E
RET Z
LD B, D
LD C, E
LD D, H
LD E, L
INC DE
LDIR
RET


; ============================================================ エラーと終わり (libx1_basic.asm の #if exists HUFS から)

; @name BAS_FERRHOOK
; @resident shared
; @param_count 0
; @calls BAS_FILEWK
; BAS_ERROR の入口: 出力を画面に戻し、このあと終わりでファイルを閉じるときのエラーは出さないことにする
; (最初のエラーの文を出して止まる)。HL・DE は壊さない
XOR A
LD (BAS_FOUTN), A
INC A
LD (BAS_FQUIET), A
RET


; @name BAS_FERRMSG
; @resident shared
; @param_count 0
; E = エラーの番号 → ファイルのエラーの文の表にあれば NZ で HL = その番号のバイト (続いて 0 で終わる文)。なければ Z。
; DE は壊さない (BAS_ERROR の表と同じ形。文は Hu-BASIC の実測。Bad file descripter の綴りも本物のまま)
LD HL, .bfm_tab
.bfm_find
LD A, (HL)
OR A
RET Z
CP E
JP Z, .bfm_found
.bfm_skip
INC HL
LD A, (HL)
OR A
JP NZ, .bfm_skip
INC HL
JP .bfm_find
.bfm_found
OR A
RET
.bfm_tab
DB 30, "Bad file mode", 0
DB 52, "Bad file number", 0
DB 53, "File not found", 0
DB 54, "Already open", 0
DB 56, "Device I/O error", 0
DB 57, "File already exists", 0
DB 60, "Device full", 0
DB 61, "Input past end", 0
DB 64, "Bad allocation table", 0
DB 65, "Bad file descripter", 0
DB 71, "File not open", 0
DB 72, "Write protected", 0
DB 0


; @name BAS_FENDHOOK
; @resident shared
; @param_count 0
; @calls BAS_FILEWK,BAS_FCLOSEALL,BAS_FCHK
; BAS_END の入口: 開いているファイルをすべて閉じる (1 回だけ)。閉じるときにエラーがあれば、エラーで止まる途中でなければ
; そのエラーの文を出して止まる (本物は END だけが閉じ、エラーで止まったときとプログラムの終わりを越えたときは
; 書き出さない。こちらはどの終わり方でも閉じる)
LD A, (BAS_FDONE)
OR A
RET NZ
INC A
LD (BAS_FDONE), A
XOR A
LD (BAS_FOUTN), A
CALL BAS_FCLOSEALL
LD A, (BAS_FQUIET)
OR A
RET NZ
JP BAS_FCHK
