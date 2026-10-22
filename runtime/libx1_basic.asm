; libx1_basic.asm
; SLANG x1native runtime — X1 BASIC 系ライブラリ (BAS_ 接頭辞)
;
; Hu-BASIC と同じように振る舞う画面・文字列・DATA・制御スタック等の処理。
; hub2sl (Hu-BASIC → SLANG トランスパイラ) の出力が使うが、手書きの SLANG
; からも呼べる汎用ライブラリ。x1native / x1native_slfs の env から選択リンクされる。
;
; 呼び出し規約: SLANG の MACHINE 規約 (引数 1-3 個は HL/DE/BC、4 個以上はスタックで
; 関数側が後始末、戻り値は HL)。IY は SLANG が使うので壊さない。
; 文字列は「先頭 1 バイトが長さ、続いて本体」のバッファへのポインタで受け渡す。
;
; X1 の VRAM は I/O 空間: テキスト $3000+off、属性 $2000+off、漢字 $3800+off
; (OUT (C),0 = DB $ED,$71 で 0 を書く)。グラフィック B=$4000 R=$8000 G=$C000。
; 属性ビット: bit7 HS(横2倍) bit6 VS(縦2倍) bit5 CG(PCG) bit4 CF(点滅) bit3 CR(反転) bit2-0 色。


; ============================================================ ワーク

; @name BAS_WORK
; @resident shared
; @param_count 0
; @works BAS_CXY:2,BAS_WID:1,BAS_ATTR:1,BAS_CSZ:1,BAS_RAW:1,BAS_PAL:3,BAS_CSP:2,BAS_CSTK:640,BAS_TMPP:2,BAS_TMP:1024,BAS_NUMBUF:8,BAS_DBASE:2,BAS_DLTAB:2,BAS_DPOS:2,BAS_DEND:2,BAS_UFMT:256,BAS_PCGBUF:32,BAS_TEMPN:2,BAS_PLST:9,BAS_PLCH:15,BAS_LASTKEY:1,BAS_CLK:1,BAS_LNPTR:2
; BAS_CX/CY: カーソル、BAS_WID: 桁数 (40/80)、BAS_ATTR: 現在の属性 (色 + CR/CF/CG)
; BAS_CSZ: CSIZE (0-3)、BAS_RAW: PRINT#0 中なら 1、BAS_PAL*: パレットの影 (書込専用ポートのため)
; BAS_CSP/BAS_CSTK: 制御スタック (10 バイト × 64 枠)
; BAS_TMPP/BAS_TMP: 文字列の一時領域 (使用位置 / 1024 バイト)
; BAS_DBASE/DLTAB/DPOS/DEND: DATA の本体・行の表・読み出し位置・終わり
; BAS_UFMT: PRINT USING の書式 (長さ付き)、BAS_PCGBUF: PCG の受け渡し用
; BAS_TEMPN: 1/32 音符の長さ (約 1ms の刻みの数 = 7500/テンポ)、BAS_PLST: チャンネルごとの MML の状態
; (オクターブ・長さ (1/32 単位)・音量 × 3)、BAS_PLCH: PLAY 中のチャンネル (位置 2・残り 1・数え 1・動作中 1 × 3)
; BAS_LASTKEY: INKEY$ の押した瞬間の判定用、BAS_CLK: CLICK の状態
; BAS_LNPTR: 実行中の行番号を持つ変数のアドレス (0 なら行番号を出さない)
; カーソルとパレットは連続した番地が要るので、まとめて確保して EQU で分ける
BAS_CX EQU BAS_CXY
BAS_CY EQU BAS_CXY + 1
BAS_PALB EQU BAS_PAL
BAS_PALR EQU BAS_PAL + 1
BAS_PALG EQU BAS_PAL + 2
RET


; ============================================================ 初期化・終了・エラー

; @name BAS_INIT
; @resident shared
; @param_count 0
; @calls BAS_WORK,sWORK,INIT_CRTC,_C4025L,_C4025H,BAS_CLS,BAS_SETPAL,BAS_INITSTATE,BAS_SNDINIT
; Hu-BASIC 起動直後と同じ状態にする: WIDTH 40 (CRTC R0-R9 は _C4025L と同じ、
; R10/R11 は Hu-BASIC の値 $60/$07)、パレット AA CC F0、プライオリティ 0、
; COLOR 7,0、制御スタックと一時領域を空に、CLS 4。
LD A, 40
LD (AT_WIDTH), A
LD (BAS_WID), A
LD BC, $1FF0
IN A, (C)
RRCA
LD HL, _C4025L
JP C, .bi_crtc
LD HL, _C4025H
.bi_crtc
CALL INIT_CRTC
LD BC, $1800
LD A, 10
OUT (C), A
LD BC, $1801
LD A, $60
OUT (C), A
LD BC, $1800
LD A, 11
OUT (C), A
LD BC, $1801
LD A, $07
OUT (C), A
LD BC, $1300
XOR A
OUT (C), A          ; プライオリティ 0
CALL BAS_INITSTATE
CALL BAS_SNDINIT    ; 音: テンポ 120、各チャンネル O4・長さ 5・音量 15、PSG のミキサー $3F・音量 0
LD HL, BAS_CSTK
LD (BAS_CSP), HL
LD HL, BAS_TMP
LD (BAS_TMPP), HL
LD HL, 4
JP BAS_CLS


; @name BAS_INITSTATE
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_SETPAL
; INIT 文と BAS_INIT で共通の初期値: 色 7、装飾なし、パレット既定
LD A, $07
LD (BAS_ATTR), A
XOR A
LD (BAS_CSZ), A
LD (BAS_RAW), A
LD A, $AA
LD (BAS_PALB), A
LD A, $CC
LD (BAS_PALR), A
LD A, $F0
LD (BAS_PALG), A
JP BAS_SETPAL


; @name BAS_INIT2
; @resident shared
; @param_count 0
; @calls BAS_INITSTATE
; INIT 文: 色・表示モード・装飾を戻す (WIDTH は戻さない)
JP BAS_INITSTATE


; @name BAS_SETPAL
; @resident shared
; @param_count 0
; @calls BAS_WORK
; パレットの影をポートに書く
LD BC, $1000
LD A, (BAS_PALB)
OUT (C), A
LD BC, $1100
LD A, (BAS_PALR)
OUT (C), A
LD BC, $1200
LD A, (BAS_PALG)
OUT (C), A
RET


; @name BAS_END
; @resident shared
; @param_count 0
; END 文: 画面はそのまま、割り込みを止めて止まる
DI
.be_halt
HALT
JP .be_halt


; @name BAS_LNINIT
; @resident shared
; @param_count 1
; @calls BAS_WORK
; HL = 実行中の行番号を持つ変数のアドレス (hub2sl の出力が各行の頭で行番号を書く)
LD (BAS_LNPTR), HL
RET


; @name BAS_ERROR
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_PNL,BAS_PSTRZ,BAS_UTOA,BAS_END
; HL = エラー番号。Hu-BASIC と同じく、改行してから「<メッセージ> in <行番号>」、次の行に「Ok」を出して止まる
; (メッセージの綴りは Hu-BASIC の実測。表にない番号は「Error <番号>」、行番号が分からなければ in を出さない)
PUSH HL
XOR A
LD (BAS_RAW), A
LD (BAS_CSZ), A
LD A, (BAS_CX)
OR A
CALL NZ, BAS_PNL
POP DE              ; E = エラー番号
LD HL, .ber_tab
.ber_find
LD A, (HL)
OR A
JP Z, .ber_unknown
CP E
JP Z, .ber_found
.ber_skip
INC HL
LD A, (HL)
OR A
JP NZ, .ber_skip
INC HL
JP .ber_find
.ber_found
INC HL
CALL BAS_PSTRZ
JP .ber_line
.ber_unknown
PUSH DE
LD HL, .ber_msg
CALL BAS_PSTRZ
POP DE
LD H, 0
LD L, E
CALL BAS_UTOA
CALL BAS_PSTRZ
.ber_line
LD HL, (BAS_LNPTR)
LD A, H
OR L
JP Z, .ber_ok
PUSH HL
LD HL, .ber_in
CALL BAS_PSTRZ
POP HL
LD A, (HL)
INC HL
LD H, (HL)
LD L, A
CALL BAS_UTOA
CALL BAS_PSTRZ
.ber_ok
CALL BAS_PNL
LD HL, .ber_okmsg
CALL BAS_PSTRZ
CALL BAS_PNL
JP BAS_END
.ber_msg DB "Error ", 0
.ber_in DB " in ", 0
.ber_okmsg DB "Ok", 0
; [番号][メッセージ 0 終端] の並び、0 で終わり
.ber_tab
DB 1, "NEXT without FOR", 0
DB 3, "RETURN without GOSUB", 0
DB 4, "Out of data", 0
DB 5, "Illegal function call", 0
DB 6, "Overflow", 0
DB 7, "Out of memory", 0
DB 9, "Subscript out of range", 0
DB 10, "Duplicate definition", 0
DB 11, "Division by zero", 0
DB 13, "Type mismatch", 0
DB 15, "String too long", 0
DB 16, "String formula too complex", 0
DB 36, "Format over", 0
DB 0


; ============================================================ 画面の出力

; @name BAS_VOFF
; @resident shared
; @param_count 0
; @calls BAS_WORK
; H = Y、L = X → HL = Y * BAS_WID + X (テキスト VRAM 内の位置)
PUSH DE
LD E, L
LD D, 0
LD A, H
LD L, A
LD H, 0
ADD HL, HL          ; ×2
ADD HL, HL          ; ×4
ADD HL, HL          ; ×8
PUSH HL
ADD HL, HL          ; ×16
ADD HL, HL          ; ×32
POP BC
ADD HL, BC          ; ×40
LD A, (BAS_WID)
CP 80
JP NZ, .bv_40
ADD HL, HL          ; ×80
.bv_40
ADD HL, DE
POP DE
RET


; @name BAS_VPUT
; @resident shared
; @param_count 0
; HL = VRAM 内の位置、E = 文字コード、D = 属性 → 漢字 0・テキスト・属性を書く
LD A, H
AND $07
OR $38
LD B, A
LD C, L
DB $ED, $71         ; OUT (C),0 = 漢字 0 (ANK)
RES 3, B
OUT (C), E          ; テキスト
RES 4, B
OUT (C), D          ; 属性
RET


; @name BAS_CLS
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_CLSTEXT,BAS_CLSGR
; HL = n: -1 (引数なし) = テキストだけ、0 = グラフィック全部、1-3 = グラフィックの 1 面、4 = 全部
LD A, L
CP 4
JP Z, .bc_all
INC H               ; H = $FF (負) なら 0 に
JP Z, .bc_text
DEC H
LD A, H
OR A
RET NZ              ; 範囲外は何もしない
LD A, L
CP 4
RET NC
JP BAS_CLSGR        ; A = 0-3
.bc_all
XOR A
CALL BAS_CLSGR
.bc_text
JP BAS_CLSTEXT


; @name BAS_CLSTEXT
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_VPUT
; テキストを空白で埋め、属性を「$40 + 現在の属性」で埋める (Hu-BASIC の実測)。カーソルを左上へ
LD A, (BAS_ATTR)
OR $40
LD D, A
LD E, $20
LD HL, 0
.bct_loop
PUSH HL
CALL BAS_VPUT
POP HL
INC HL
LD A, H
CP $08              ; $800 セル (2048 = 80×25 + 余り)
JP NZ, .bct_loop
XOR A
LD (BAS_CX), A
LD (BAS_CY), A
RET


; @name BAS_CLSGR
; @resident shared
; @param_count 0
; A = 0: グラフィック 3 面、1-3: その面だけを 0 で埋める
OR A
JP NZ, .bcg_one
LD A, $40
CALL .bcg_plane
LD A, $80
CALL .bcg_plane
LD A, $C0
JP .bcg_plane
.bcg_one
RRCA
RRCA                ; 1 → $40, 2 → $80, 3 → $C0
.bcg_plane
LD B, A
LD C, 0
LD D, $40           ; 16KB = $40 × 256
XOR A
.bcg_loop
OUT (C), A
INC C
JP NZ, .bcg_loop
INC B
DEC D
JP NZ, .bcg_loop
RET


; @name BAS_SCROLL
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_VPUT
; 1 行上にずらし、最下行を空白 (属性 $40 + 現在の属性) で埋める
LD A, (BAS_WID)
LD E, A
LD D, 0             ; DE = 桁数 (読み出し側のずれ)
LD HL, 0            ; HL = 書き込み先の位置
LD A, (BAS_WID)
CP 80
LD BC, 40 * 24
JP NZ, .bs_n
LD BC, 80 * 24
.bs_n
.bs_loop
PUSH BC
PUSH HL
ADD HL, DE          ; 読み出し元 = 書き込み先 + 桁数
LD A, H
OR $30
LD B, A
LD C, L
IN A, (C)           ; テキスト
EX AF, AF'
RES 4, B
IN A, (C)           ; 属性
POP HL
PUSH HL
PUSH AF
LD A, H
OR $30
LD B, A
LD C, L
EX AF, AF'
OUT (C), A
RES 4, B
POP AF
OUT (C), A
POP HL
INC HL
POP BC
DEC BC
LD A, B
OR C
JP NZ, .bs_loop
; 最下行
LD A, (BAS_ATTR)
OR $40
LD D, A
LD A, (BAS_WID)
LD B, A
.bs_fill
PUSH BC
PUSH HL
PUSH DE
LD E, $20
CALL BAS_VPUT
POP DE
POP HL
INC HL
POP BC
DJNZ .bs_fill
RET


; @name BAS_NEWLINE
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_SCROLL
; カーソルを次の行の先頭へ (最下行ならスクロール)
XOR A
LD (BAS_CX), A
LD A, (BAS_CY)
INC A
CP 25
JP C, .bn_set
CALL BAS_SCROLL
LD A, 24
.bn_set
LD (BAS_CY), A
RET


; @name BAS_PCHR
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_VOFF,BAS_VPUT,BAS_NEWLINE,BAS_CLSTEXT
; L = 文字コード。生出力 (PRINT#0) でなければ制御コード ($0D 改行、$0B HOME、$0C CLS) を解釈し、
; ほかの $00-$1F は出さない。生出力では CSIZE の拡大で書く。
LD A, (BAS_RAW)
OR A
LD A, L
JP NZ, .bp_char
CP $0D
JP Z, BAS_NEWLINE
CP $0B
JP Z, .bp_home
CP $0C
JP Z, BAS_CLSTEXT
CP $20
RET C
.bp_char
LD E, A             ; E = 文字
; 折り返し: X >= 桁数 (拡大中は X+1 >= 桁数) なら次の行へ
LD A, (BAS_CSZ)
AND $02             ; 横 2 倍か
LD A, (BAS_CX)
JP Z, .bp_w1
INC A
.bp_w1
LD HL, BAS_WID
CP (HL)
JP C, .bp_put
PUSH DE
CALL BAS_NEWLINE
POP DE
.bp_put
LD A, (BAS_RAW)
OR A
LD A, (BAS_CSZ)
JP NZ, .bp_sz
XOR A               ; 生出力でなければ拡大しない
.bp_sz
OR A
JP Z, .bp_normal
; 拡大: 属性に HS/VS を付けて、横 2 マス・縦 2 行に同じ文字を書く
LD C, A             ; C = CSIZE (1 縦 / 2 横 / 3 縦横)
LD A, (BAS_ATTR)
BIT 0, C
JP Z, .bp_nv
OR $40
.bp_nv
BIT 1, C
JP Z, .bp_nh
OR $80
.bp_nh
LD D, A
PUSH BC
LD HL, (BAS_CX)     ; L = X, H = Y
CALL .bp_cell
POP BC
PUSH BC
BIT 1, C
JP Z, .bp_noright
LD HL, (BAS_CX)
INC L
CALL .bp_cell
.bp_noright
POP BC
PUSH BC
BIT 0, C
JP Z, .bp_nodown
LD HL, (BAS_CX)
INC H
LD A, H
CP 25
JP NC, .bp_nodown
CALL .bp_cell
POP BC
PUSH BC
BIT 1, C
JP Z, .bp_nodown
LD HL, (BAS_CX)
INC H
INC L
CALL .bp_cell
.bp_nodown
POP BC
LD HL, BAS_CX
INC (HL)
BIT 1, C
RET Z
INC (HL)
RET
.bp_cell
PUSH DE
CALL BAS_VOFF
POP DE
PUSH DE
CALL BAS_VPUT
POP DE
RET
.bp_normal
LD A, (BAS_ATTR)
LD D, A
LD HL, (BAS_CX)
PUSH DE
CALL BAS_VOFF
POP DE
CALL BAS_VPUT
LD HL, BAS_CX
INC (HL)
RET
.bp_home
XOR A
LD (BAS_CX), A
LD (BAS_CY), A
RET


; @name BAS_PSTR
; @resident shared
; @param_count 1
; @calls BAS_PCHR
; HL = 文字列 (長さ付き) を出す
LD B, (HL)
INC HL
LD A, B
OR A
RET Z
.bps_loop
PUSH BC
PUSH HL
LD L, (HL)
CALL BAS_PCHR
POP HL
POP BC
INC HL
DJNZ .bps_loop
RET


; @name BAS_PSTRZ
; @resident shared
; @param_count 1
; @calls BAS_PCHR
; HL = 0 終端の文字列を出す (ランタイム内部用)
LD A, (HL)
OR A
RET Z
PUSH HL
LD L, A
CALL BAS_PCHR
POP HL
INC HL
JP BAS_PSTRZ


; @name BAS_PNL
; @resident shared
; @param_count 0
; @calls BAS_NEWLINE
JP BAS_NEWLINE


; @name BAS_PSPC
; @resident shared
; @param_count 1
; @calls BAS_PCHR
; HL = 空白の個数 (0-255、負は 0)
BIT 7, H
RET NZ
LD A, H
OR A
JP Z, .bsp_n
LD L, $FF
.bsp_n
LD B, L
LD A, B
OR A
RET Z
.bsp_loop
PUSH BC
LD L, $20
CALL BAS_PCHR
POP BC
DJNZ .bsp_loop
RET


; @name BAS_PZONE
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_PCHR,BAS_NEWLINE
; 次の 10 桁区切りの位置まで空白。次の区切りが行末を越えるなら改行
LD A, (BAS_CX)
LD B, 0
.bpz_div
SUB 10
JP C, .bpz_got
INC B
JP .bpz_div
.bpz_got
INC B               ; B = 次の区切りの番号
LD A, B
ADD A, A
LD C, A
ADD A, A
ADD A, A
ADD A, C            ; A = B * 10 (次の区切りの桁)
LD C, A
LD A, (BAS_WID)
DEC A
CP C
JP C, BAS_NEWLINE   ; 次の区切りが行内にない
.bpz_pad
LD A, (BAS_CX)
CP C
RET NC
PUSH BC
LD L, $20
CALL BAS_PCHR
POP BC
JP .bpz_pad


; @name BAS_PNUM
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_PCHR,BAS_UTOA
; HL = 符号付きの値。符号の桁 (正は空白、負は '-') + 10 進 + 後ろに空白 1 つ
LD A, ' '
BIT 7, H
JP Z, .bpn_pos
LD A, L
CPL
LD L, A
LD A, H
CPL
LD H, A
INC HL              ; HL = |値| (-32768 は 32768)
LD A, '-'
.bpn_pos
PUSH HL
LD L, A
CALL BAS_PCHR
POP HL
CALL BAS_UTOA       ; HL = 数字列 (0 終端)
.bpn_loop
LD A, (HL)
OR A
JP Z, .bpn_end
PUSH HL
LD L, A
CALL BAS_PCHR
POP HL
INC HL
JP .bpn_loop
.bpn_end
LD L, ' '
JP BAS_PCHR


; @name BAS_UTOA
; @resident shared
; @param_count 1
; @calls BAS_WORK,DIVHLDE
; HL = 符号なしの値 → HL = BAS_NUMBUF 内の 10 進の数字列 (0 終端)
LD BC, BAS_NUMBUF + 7
XOR A
LD (BC), A
.bu_loop
DEC BC
PUSH BC
LD DE, 10
CALL DIVHLDE        ; HL = 商、DE = 余り
POP BC
LD A, E
ADD A, '0'
LD (BC), A
LD A, H
OR L
JP NZ, .bu_loop
LD H, B
LD L, C
RET


; @name BAS_LOCATE
; @resident shared
; @param_count 2
; @calls BAS_WORK,BAS_ERROR
; HL = X, DE = Y (範囲外はエラー 5)
LD A, H
OR D
JP NZ, .bl_err
LD A, (BAS_WID)
DEC A
CP L
JP C, .bl_err
LD A, E
CP 25
JP NC, .bl_err
LD A, L
LD (BAS_CX), A
LD A, E
LD (BAS_CY), A
RET
.bl_err
LD HL, 5
JP BAS_ERROR


; @name BAS_COLOR
; @resident shared
; @param_count 2
; @calls BAS_WORK,BAS_SETPAL
; HL = 前景色 (-1 = 変えない)、DE = 背景色 (-1 = 変えない)。
; 背景色はパレットの色番号 0 を置き換える (Hu-BASIC の実測)
BIT 7, H
JP NZ, .bco_bg
LD A, (BAS_ATTR)
AND $F8
LD B, A
LD A, L
AND $07
OR B
LD (BAS_ATTR), A
.bco_bg
BIT 7, D
RET NZ
LD HL, BAS_PALB
LD B, 3
.bco_pal
LD A, (HL)
AND $FE
SRL E
ADC A, 0            ; bit0 = 背景色の該当ビット
LD (HL), A
INC HL
DJNZ .bco_pal
JP BAS_SETPAL


; @name BAS_WIDTH
; @resident shared
; @param_count 1
; @calls BAS_WORK,sWORK,INIT_CRTC,_C4025L,_C4025H,_C8025L,_C8025H,BAS_CLS
; HL = 桁数 (40 以下 → 40、それより大きい → 80)。CLS 4 と同じ消去をする
LD A, H
OR A
JP NZ, .bw_80
LD A, L
CP 41
JP NC, .bw_80
LD A, 40
LD HL, _C4025L
LD DE, _C4025H
JP .bw_set
.bw_80
LD A, 80
LD HL, _C8025L
LD DE, _C8025H
.bw_set
LD (AT_WIDTH), A
LD (BAS_WID), A
LD BC, $1FF0
IN A, (C)
RRCA
JP C, .bw_crtc
EX DE, HL
.bw_crtc
CALL INIT_CRTC
LD HL, 4
JP BAS_CLS


; ============================================================ 制御スタック (GOSUB と FOR)
; 枠 = 10 バイト: [種別 1][番号 2][変数 2][上限 2][増分 2][予備 1]
;   種別 1 = GOSUB (番号 = 戻り先 ID)、2 = FOR (番号 = FOR の出現番号)

; @name BAS_GPUSH
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_CSPUSH
; HL = 戻り先 ID。GOSUB 枠を積む (満杯ならエラー 7)
EX DE, HL
LD A, 1
JP BAS_CSPUSH


; @name BAS_CSPUSH
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_ERROR
; A = 種別、DE = 番号 → 枠を 1 つ積み、HL = 枠の先頭を返す (満杯ならエラー 7)
LD HL, (BAS_CSP)
PUSH DE
LD DE, BAS_CSTK + 640
OR A                ; キャリーを消す (A は保つ)
PUSH HL
SBC HL, DE
POP HL
POP DE
JP NC, .bcs_full
PUSH HL
LD (HL), A
INC HL
LD (HL), E
INC HL
LD (HL), D
POP HL
PUSH HL
LD DE, 10
ADD HL, DE
LD (BAS_CSP), HL
POP HL
RET
.bcs_full
LD HL, 7
JP BAS_ERROR


; @name BAS_GPOP
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_ERROR
; いちばん上の GOSUB 枠までの FOR 枠を捨て、GOSUB 枠を外して戻り先 ID を返す (なければエラー 3)
LD HL, (BAS_CSP)
.bg_loop
LD DE, BAS_CSTK
OR A
PUSH HL
SBC HL, DE
POP HL
JP Z, .bg_none
LD DE, 0 - 10
ADD HL, DE
LD A, (HL)
CP 1
JP NZ, .bg_loop
LD (BAS_CSP), HL
INC HL
LD E, (HL)
INC HL
LD D, (HL)
EX DE, HL
RET
.bg_none
LD HL, 3
JP BAS_ERROR


; @name BAS_FORPUSH
; @resident shared
; @calls BAS_WORK,BAS_CSPUSH,BAS_FORFIND
; @stack_cleanup callee
; スタック引数 (&変数, 上限, 増分, 出現番号)。入口の判定をして、回るなら FOR 枠を積んで -1、
; 回らないなら積まずに 0 を返す。積む前に、同じ変数の FOR 枠 (いちばん上の GOSUB 枠より上) と
; その上の枠を捨てる。
POP HL
LD (.bf_ret), HL
POP HL
LD (.bf_id), HL
POP HL
LD (.bf_step), HL
POP HL
LD (.bf_lim), HL
POP HL
LD (.bf_var), HL
LD HL, (.bf_ret)
PUSH HL
; 入口の判定: step >= 0 なら v <= lim、step < 0 なら v >= lim (符号付き)
LD HL, (.bf_var)
LD E, (HL)
INC HL
LD D, (HL)          ; DE = v
LD HL, (.bf_lim)    ; HL = lim
LD A, (.bf_step + 1)
BIT 7, A
JP NZ, .bf_neg
; lim - v >= 0 か (符号付き)
CALL .bf_cmp        ; HL - DE の符号 → キャリー = 負
JP C, .bf_skip
JP .bf_run
.bf_neg
EX DE, HL           ; HL = v, DE = lim → v - lim >= 0 か
CALL .bf_cmp
JP C, .bf_skip
.bf_run
LD HL, (.bf_var)
CALL BAS_FORFIND    ; 同じ変数の枠を探す (HL = 枠 / 0)
LD A, H
OR L
JP Z, .bf_push
LD (BAS_CSP), HL    ; その枠とその上を捨てる
.bf_push
LD DE, (.bf_id)
LD A, 2
CALL BAS_CSPUSH     ; HL = 枠の先頭
INC HL
INC HL
INC HL
LD DE, (.bf_var)
LD (HL), E
INC HL
LD (HL), D
INC HL
LD DE, (.bf_lim)
LD (HL), E
INC HL
LD (HL), D
INC HL
LD DE, (.bf_step)
LD (HL), E
INC HL
LD (HL), D
LD HL, $FFFF
RET
.bf_skip
LD HL, 0
RET
; HL - DE を符号付きで比べ、HL < DE ならキャリーを立てる
.bf_cmp
LD A, H
XOR D
JP M, .bf_diffsign
OR A
SBC HL, DE          ; 同符号: 引き算で判定
RET
.bf_diffsign
LD A, H             ; 異符号: HL が負なら HL < DE
RLA
RET
.bf_ret DW 0
.bf_id DW 0
.bf_step DW 0
.bf_lim DW 0
.bf_var DW 0


; @name BAS_FORFIND
; @resident shared
; @param_count 1
; @calls BAS_WORK
; HL = 変数のアドレス (0 = 指定なし) → いちばん上の GOSUB 枠より上で一致する FOR 枠の先頭 (なければ 0)
EX DE, HL           ; DE = 変数
LD HL, (BAS_CSP)
.bff_loop
PUSH DE
LD DE, BAS_CSTK
OR A
PUSH HL
SBC HL, DE
POP HL
POP DE
JP Z, .bff_none
PUSH DE
LD DE, 0 - 10
ADD HL, DE
POP DE
LD A, (HL)
CP 1
JP Z, .bff_none     ; GOSUB 枠で打ち切り
LD A, D
OR E
RET Z               ; 指定なし: いちばん上の FOR 枠
PUSH HL
INC HL
INC HL
INC HL
LD A, (HL)
INC HL
LD H, (HL)
LD L, A
OR A
SBC HL, DE
POP HL
RET Z
JP .bff_loop
.bff_none
LD HL, 0
RET


; @name BAS_FORNEXT
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_FORFIND,BAS_ERROR
; HL = 変数のアドレス (0 = 指定なし)。対応する FOR 枠を探し (なければエラー 1)、その上の枠を捨てる。
; v + step を 24 ビットで計算して上限と比べ、変数には下位 16 ビットを書く。
; 続行なら FOR の出現番号、終わりなら枠を外して -1 を返す。
CALL BAS_FORFIND
LD A, H
OR L
JP NZ, .bn_found
LD HL, 1
JP BAS_ERROR
.bn_found
LD (.bn_frame), HL
LD DE, 10
ADD HL, DE
LD (BAS_CSP), HL    ; 見つかった枠の上を捨てる
LD HL, (.bn_frame)
INC HL
LD E, (HL)
INC HL
LD D, (HL)
LD (.bn_id), DE
INC HL
LD E, (HL)
INC HL
LD D, (HL)
LD (.bn_var), DE
INC HL
LD E, (HL)
INC HL
LD D, (HL)
LD (.bn_lim), DE
INC HL
LD E, (HL)
INC HL
LD D, (HL)          ; DE = step
LD HL, (.bn_var)
LD A, (HL)
INC HL
LD H, (HL)
LD L, A             ; HL = v
; 24 ビットの和: C:HL = sext(v) + sext(step)
LD A, H
RLA
SBC A, A
LD C, A             ; C = v の符号の拡張
LD A, D
RLA
SBC A, A            ; A = step の符号の拡張
ADD HL, DE
ADC A, C
LD C, A             ; C:HL = 和
; 和が 16 ビットの符号付きの範囲を出たらエラー 6 (Overflow)。Hu-BASIC は NEXT での
; 増分だけはオーバーフローをエラーにする (A=A+1 は回り込む)
LD A, H
RLA                 ; キャリー = 和の bit 15
LD A, C
ADC A, 0            ; C が符号の拡張と一致していれば 0
JP Z, .bn_inrange
LD HL, 6
JP BAS_ERROR
.bn_inrange
; 変数に下位 16 ビットを書く
PUSH HL
EX DE, HL
LD HL, (.bn_var)
LD (HL), E
INC HL
LD (HL), D
POP HL              ; C:HL = 和
; diff = lim - 和 (24 ビット) の符号で判定
LD DE, (.bn_lim)
LD A, D
RLA
SBC A, A
LD B, A             ; B:DE = sext(lim)
EX DE, HL           ; HL = lim, DE = 和の下位
OR A
SBC HL, DE
LD A, B
SBC A, C            ; A:HL = lim - 和
LD B, A
; 増分の符号を見る
PUSH HL
LD HL, (.bn_frame)
LD DE, 8
ADD HL, DE
LD A, (HL)          ; 増分の上位バイト
POP HL
BIT 7, A
JP NZ, .bn_negstep
; step >= 0: lim - 和 >= 0 なら続行
BIT 7, B
JP NZ, .bn_exit
JP .bn_cont
.bn_negstep
; step < 0: lim - 和 <= 0 なら続行
BIT 7, B
JP NZ, .bn_cont
LD A, B
OR H
OR L
JP Z, .bn_cont
.bn_exit
LD HL, (.bn_frame)
LD (BAS_CSP), HL    ; 枠を外す
LD HL, $FFFF
RET
.bn_cont
LD HL, (.bn_id)
RET
.bn_frame DW 0
.bn_id DW 0
.bn_var DW 0
.bn_lim DW 0


; ============================================================ 配列

; @name BAS_DIM
; @resident shared
; @param_count 3
; @calls BAS_ERROR
; HL = &状態, DE = &上限, BC = n。状態が 0 なら上限 n で確定 (状態 2)、それ以外はエラー 10
LD A, (HL)
INC HL
OR (HL)
DEC HL
JP NZ, .bd_dup
LD (HL), 2
INC HL
LD (HL), 0
EX DE, HL
LD (HL), C
INC HL
LD (HL), B
RET
.bd_dup
LD HL, 10
JP BAS_ERROR


; @name BAS_AIDX
; @resident shared
; @param_count 3
; @calls BAS_ERROR
; HL = 添字, DE = &状態, BC = &上限 → HL = 添字。
; 状態が 0 なら上限 10 で確定 (状態 1)。添字が 0〜上限の外ならエラー 9
LD A, (DE)
OR A
JP NZ, .ba_check
LD A, 1
LD (DE), A
LD A, 10
LD (BC), A
INC BC
XOR A
LD (BC), A
DEC BC
.ba_check
LD A, (BC)
LD E, A
INC BC
LD A, (BC)
LD D, A             ; DE = 上限
PUSH HL
EX DE, HL           ; HL = 上限, DE = 添字
OR A
SBC HL, DE          ; 上限 - 添字 (符号なし: 負の添字は大きな数になる)
POP HL
RET NC
LD HL, 9
JP BAS_ERROR


; ============================================================ 整数の除算

; @name BAS_IDIV
; @resident shared
; @param_count 2
; @calls SDIVHLDE,BAS_ERROR
; HL \ DE (0 方向への切り捨て)。DE = 0 ならエラー 11
LD A, D
OR E
JP NZ, SDIVHLDE
LD HL, 11
JP BAS_ERROR


; @name BAS_IMOD
; @resident shared
; @param_count 2
; @calls SMODHLDE,BAS_ERROR
; HL MOD DE (符号は被除数と同じ)。DE = 0 ならエラー 11
LD A, D
OR E
JP NZ, SMODHLDE
LD HL, 11
JP BAS_ERROR


; @name BAS_IDIVR
; @resident shared
; @param_count 2
; @calls DIVHLDE,BAS_ERROR
; HL / DE を四捨五入した整数 (0 から遠い側に丸める)。DE = 0 ならエラー 11
LD A, D
OR E
JP NZ, .bdr_go
LD HL, 11
JP BAS_ERROR
.bdr_go
LD A, H
XOR D
LD (.bdr_sign), A   ; bit7 = 結果の符号
BIT 7, H
JP Z, .bdr_a
LD A, L
CPL
LD L, A
LD A, H
CPL
LD H, A
INC HL              ; |a|
.bdr_a
BIT 7, D
JP Z, .bdr_b
LD A, E
CPL
LD E, A
LD A, D
CPL
LD D, A
INC DE              ; |b|
.bdr_b
LD (.bdr_div), DE
CALL DIVHLDE        ; HL = 商, DE = 余り
PUSH HL
EX DE, HL
ADD HL, HL          ; 2 × 余り
LD DE, (.bdr_div)
OR A
SBC HL, DE          ; 2r - |b| >= 0 なら切り上げ
POP HL
JP C, .bdr_nround
INC HL
.bdr_nround
LD A, (.bdr_sign)
BIT 7, A
RET Z
LD A, L
CPL
LD L, A
LD A, H
CPL
LD H, A
INC HL
RET
.bdr_sign DB 0
.bdr_div DW 0


; ============================================================ 文字列 (基本)

; @name BAS_STMP
; @resident shared
; @param_count 0
; @calls BAS_WORK
; 文字列の一時領域を空に戻す
LD HL, BAS_TMP
LD (BAS_TMPP), HL
RET


; ============================================================ 待ち

; @name BAS_WAITF
; @resident shared
; @param_count 1
; HL = フレーム数。垂直帰線の始まりを HL 回待つ (8255 port B ($1A01) の bit 7)
LD A, H
OR L
RET Z
LD BC, $1A01
.bw_wait1
IN A, (C)
JP P, .bw_wait1     ; 表示期間になるまで
.bw_wait2
IN A, (C)
JP M, .bw_wait2     ; 帰線期間の始まりまで
DEC HL
LD A, H
OR L
JP NZ, .bw_wait1
RET


; @name BAS_VSYNC
; @resident shared
; @param_count 1
; @calls BAS_WAITF
; 速度のモード (frame): HL フレーム待つ
JP BAS_WAITF


; @name BAS_WLINE
; @resident shared
; @param_count 1
; 速度のモード (line): HL × 約 0.1 ms の空ループ (4 MHz で 1 単位 ≒ 400 T)
LD A, H
OR L
RET Z
.bwl_loop
LD B, 30
.bwl_in
DJNZ .bwl_in
DEC HL
LD A, H
OR L
JP NZ, .bwl_loop
RET


; @name BAS_PAUSE
; @resident shared
; @param_count 1
; @calls BAS_WAITF
; HL × 0.1 秒 (= 6 フレーム) 待つ。負は 0
BIT 7, H
RET NZ
LD D, H
LD E, L
ADD HL, HL
ADD HL, DE          ; ×3
ADD HL, HL          ; ×6
JP BAS_WAITF


; ============================================================ 文字列 (段階 2)
; 文字列 = [長さ 0-255][本体]。作る関数は一時領域に結果を置き、そのポインタを返す。
; 結果が 255 を超えたらエラー 15、一時領域があふれたらエラー 16。

; @name BAS_TALLOC
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_ERROR
; A = 長さ → HL = 一時領域の [長さ][本体] (長さは書き込み済み)。あふれたらエラー 16
PUSH DE
PUSH BC
LD C, A
LD HL, (BAS_TMPP)
PUSH HL
LD B, 0
ADD HL, BC
INC HL              ; HL = 次の使用位置
PUSH HL
LD DE, BAS_TMP + 1024 + 1
OR A
SBC HL, DE
POP DE              ; DE = 次の使用位置
POP HL              ; HL = 確保した先頭
JP NC, .bt_full
LD (BAS_TMPP), DE
LD (HL), C
POP BC
POP DE
RET
.bt_full
LD HL, 16
JP BAS_ERROR


; @name BAS_SSET
; @resident shared
; @param_count 2
; HL = 代入先の変数のバッファ (256 バイト), DE = 文字列 → 長さ+1 バイトを写す
EX DE, HL           ; HL = 元, DE = 先
LD A, (HL)
LD C, A
LD B, 0
INC BC
LDIR
RET


; @name BAS_SCAT
; @resident shared
; @param_count 2
; @calls BAS_TALLOC,BAS_ERROR
; HL = a, DE = b → a + b (長さが 255 を超えたらエラー 15)
LD A, (DE)
ADD A, (HL)
JP C, .bsc_long
PUSH HL
PUSH DE
CALL BAS_TALLOC     ; HL = 結果
POP DE              ; DE = b
EX (SP), HL         ; (SP) = 結果, HL = a
PUSH DE
EX DE, HL           ; DE = a
POP HL              ; HL = b
PUSH HL
; 結果の本体へ a の本体を写す
LD HL, 2
ADD HL, SP
LD A, (HL)
INC HL
LD H, (HL)
LD L, A             ; HL = 結果
INC HL              ; 結果の本体
EX DE, HL           ; HL = a, DE = 結果の本体
LD C, (HL)
LD B, 0
INC HL
LD A, C
OR A
JP Z, .bsc_a0
LDIR
.bsc_a0
POP HL              ; HL = b
LD C, (HL)
LD B, 0
INC HL
LD A, C
OR A
JP Z, .bsc_b0
LDIR
.bsc_b0
POP HL              ; HL = 結果
RET
.bsc_long
LD HL, 15
JP BAS_ERROR


; @name BAS_SCOPY
; @resident shared
; @param_count 0
; @calls BAS_TALLOC
; HL = 元の本体の先頭, A = 長さ → 一時領域に [長さ][本体] を作って HL に返す (内部用)
PUSH HL
CALL BAS_TALLOC
POP DE              ; DE = 元
PUSH HL
LD C, (HL)
LD B, 0
INC HL
EX DE, HL           ; HL = 元, DE = 先の本体
LD A, C
OR A
JP Z, .bsy_end
LDIR
.bsy_end
POP HL
RET


; @name BAS_SMID
; @resident shared
; @param_count 3
; @calls BAS_SCOPY,BAS_ERROR
; HL = s, DE = p (1 始まり), BC = n。p<1・n<0・p や n が 255 超えはエラー 5。
; p が長さより大きい、または n=0 なら空文字列。末尾を越える n は末尾まで
LD A, D
OR A
JP NZ, .bsm_err
LD A, B
OR A
JP NZ, .bsm_err
LD A, E
OR A
JP Z, .bsm_err      ; p = 0
LD A, (HL)          ; A = 長さ
CP E
JP C, .bsm_empty    ; p > 長さ
; 残り = 長さ - p + 1
SUB E
INC A
CP C
JP C, .bsm_len      ; 残り < n なら残りまで
LD A, C
.bsm_len
LD D, 0
ADD HL, DE          ; HL = s + p = p 文字目
JP BAS_SCOPY
.bsm_empty
XOR A
JP BAS_SCOPY
.bsm_err
LD HL, 5
JP BAS_ERROR


; @name BAS_SRIGHT
; @resident shared
; @param_count 2
; @calls BAS_SCOPY,BAS_ERROR
; HL = s, DE = n。n<0・n が 255 超えはエラー 5。n が長さ以上なら全体
LD A, D
OR A
JP NZ, .bsr_err
LD A, (HL)
CP E
JP C, .bsr_all      ; 長さ < n
LD A, E
.bsr_all
; A = 取り出す長さ、開始 = s + 1 + (長さ - A)
LD C, A
LD A, (HL)
SUB C
LD E, A
LD D, 0
INC HL
ADD HL, DE
LD A, C
JP BAS_SCOPY
.bsr_err
LD HL, 5
JP BAS_ERROR


; @name BAS_SCHR
; @resident shared
; @param_count 1
; @calls BAS_TALLOC,BAS_ERROR
; HL = 文字コード (0-255、それ以外はエラー 5) → 1 文字の文字列
LD A, H
OR A
JP NZ, .bch_err
LD E, L
LD A, 1
CALL BAS_TALLOC
INC HL
LD (HL), E
DEC HL
RET
.bch_err
LD HL, 5
JP BAS_ERROR


; @name BAS_SASC
; @resident shared
; @param_count 1
; HL = s → 先頭の文字コード (空文字列は 0。Hu-BASIC の実測)
LD A, (HL)
OR A
JP Z, .bas_empty
INC HL
LD L, (HL)
LD H, 0
RET
.bas_empty
LD HL, 0
RET


; @name BAS_HEXDIGIT
; @resident shared
; @param_count 0
; A = 文字 → A = 値 (0-15)、16 進の数字でなければキャリーを立てる
CP '0'
RET C
CP '9' + 1
JP C, .bhd_num
AND $DF             ; 小文字 → 大文字
CP 'A'
RET C
CP 'F' + 1
CCF
RET C
SUB 'A' - 10
OR A
RET
.bhd_num
SUB '0'
OR A
RET


; @name BAS_SVAL
; @resident shared
; @param_count 1
; @calls BAS_HEXDIGIT,BAS_ERROR
; HL = s → 整数。前の空白を読み飛ばし、符号・&H/&O/&B・10 進を読み、数として読めない所で止める。
; 16 ビットを超える値はエラー 6。&H8000 以上は負 (16 ビットのまま)
LD B, (HL)          ; B = 残りの文字数
INC HL
LD DE, 0            ; DE = 値
LD C, 0             ; C = 負なら 1
.bv_sp
LD A, B
OR A
JP Z, .bv_done
LD A, (HL)
CP ' '
JP NZ, .bv_sign
INC HL
DEC B
JP .bv_sp
.bv_sign
CP '-'
JP NZ, .bv_plus
INC C
JP .bv_skip1
.bv_plus
CP '+'
JP NZ, .bv_body
.bv_skip1
INC HL
DEC B
.bv_body
LD A, B
OR A
JP Z, .bv_done
LD A, (HL)
CP '&'
JP NZ, .bv_dec
; &H / &O / &B
INC HL
DEC B
LD A, B
OR A
JP Z, .bv_done
LD A, (HL)
AND $DF
INC HL
DEC B
CP 'H'
LD A, 4
JP Z, .bv_radix
LD A, (HL)
DEC HL
LD A, (HL)
INC HL
AND $DF
CP 'O'
LD A, 3
JP Z, .bv_radix
LD A, 1             ; &B
.bv_radix
LD (.bv_shift), A
.bv_rloop
LD A, B
OR A
JP Z, .bv_done
LD A, (HL)
CALL BAS_HEXDIGIT
JP C, .bv_done
PUSH AF
LD A, (.bv_shift)
; 進数の範囲外の数字なら止める (8 進は 0-7、2 進は 0-1)
CP 4
JP Z, .bv_rok
CP 3
JP NZ, .bv_rbin
POP AF
CP 8
JP NC, .bv_done
PUSH AF
JP .bv_rok
.bv_rbin
POP AF
CP 2
JP NC, .bv_done
PUSH AF
.bv_rok
LD A, (.bv_shift)
EX DE, HL
.bv_sh
ADD HL, HL
JP C, .bv_ovf_pop
DEC A
JP NZ, .bv_sh
POP AF
OR L
LD L, A
EX DE, HL
INC HL
DEC B
JP .bv_rloop
.bv_ovf_pop
POP AF
JP .bv_ovf
.bv_dec
LD A, B
OR A
JP Z, .bv_done
LD A, (HL)
SUB '0'
JP C, .bv_done
CP 10
JP NC, .bv_done
; DE = DE * 10 + A (16 ビットを超えたらエラー 6)
PUSH HL
LD H, D
LD L, E
ADD HL, HL
JP C, .bv_ovf_hl
ADD HL, HL
JP C, .bv_ovf_hl
ADD HL, DE
JP C, .bv_ovf_hl
ADD HL, HL
JP C, .bv_ovf_hl
LD E, A
LD D, 0
ADD HL, DE
JP C, .bv_ovf_hl
EX DE, HL
POP HL
INC HL
DEC B
JP .bv_dec
.bv_ovf_hl
POP HL
.bv_ovf
LD HL, 6
JP BAS_ERROR
.bv_done
EX DE, HL
LD A, C
OR A
RET Z
LD A, L
CPL
LD L, A
LD A, H
CPL
LD H, A
INC HL
RET
.bv_shift DB 0


; @name BAS_SHEX
; @resident shared
; @param_count 1
; @calls BAS_TALLOC,BAS_HEXDIGIT,BAS_ERROR
; HL = s (16 進の文字列) → 2 文字を 1 バイトにした文字列。奇数の長さ・16 進以外の文字はエラー 5
LD A, (HL)
BIT 0, A
JP NZ, .bsh_err
SRL A
PUSH HL
CALL BAS_TALLOC     ; HL = 結果
POP DE              ; DE = s
PUSH HL
LD B, (HL)          ; B = 結果の長さ
INC HL
INC DE
LD A, B
OR A
JP Z, .bsh_end
.bsh_loop
LD A, (DE)
CALL BAS_HEXDIGIT
JP C, .bsh_err_pop
ADD A, A
ADD A, A
ADD A, A
ADD A, A
LD C, A
INC DE
LD A, (DE)
CALL BAS_HEXDIGIT
JP C, .bsh_err_pop
OR C
LD (HL), A
INC HL
INC DE
DJNZ .bsh_loop
.bsh_end
POP HL
RET
.bsh_err_pop
POP HL
.bsh_err
LD HL, 5
JP BAS_ERROR


; @name BAS_SCMP
; @resident shared
; @param_count 2
; HL = a, DE = b → -1 (a < b) / 0 / 1 (a > b)。バイトごとに比べ、短い方が先に尽きたら小さい
LD B, (HL)          ; B = a の長さ
LD A, (DE)
LD C, A             ; C = b の長さ
INC HL
INC DE
.bcm_loop
LD A, B
OR A
JP Z, .bcm_aend
LD A, C
OR A
JP Z, .bcm_gt       ; b が先に尽きた
LD A, (DE)
CP (HL)             ; b - a
JP C, .bcm_gt       ; a > b
JP NZ, .bcm_lt
INC HL
INC DE
DEC B
DEC C
JP .bcm_loop
.bcm_aend
LD A, C
OR A
JP NZ, .bcm_lt      ; a が先に尽きた
LD HL, 0
RET
.bcm_lt
LD HL, $FFFF
RET
.bcm_gt
LD HL, 1
RET


; @name BAS_SINKEY
; @resident shared
; @param_count 0
; @calls BAS_WORK,sGETKY,BAS_TALLOC
; キーの文字を返す (なければ空文字列)。Hu-BASIC と同じく押しっぱなしでは自動でリピートする:
; 押した瞬間に 1 回、約 30 フレーム後から約 3.8 フレームごと (4,4,4,4,4,3 の繰り返し、実測に合わせた値)。
; フレームは呼ばれるたびに垂直帰線の切り替わり (8255 port B の bit 7) を見て数えるので、
; ループで頻繁に呼ばれる使い方を前提とする。
; フレームを数える
LD BC, $1A01
IN A, (C)
AND $80
LD HL, .bik_vprev
LD B, (HL)
LD (HL), A
CP B
JP Z, .bik_nof
OR A
JP NZ, .bik_nof     ; 0 → 1 は数えない (1 → 0 で 1 フレーム)
LD HL, (.bik_frames)
INC HL
LD (.bik_frames), HL
.bik_nof
CALL sGETKY
LD E, A
OR A
JP NZ, .bik_key
LD (BAS_LASTKEY), A
JP .bik_none
.bik_key
LD HL, BAS_LASTKEY
CP (HL)
JP Z, .bik_held
; 押した瞬間
LD (HL), A
LD HL, 0
LD (.bik_frames), HL
LD HL, 30
LD (.bik_next), HL
XOR A
LD (.bik_phase), A
JP .bik_ret
.bik_held
; 押しっぱなし: 次のリピートのフレームに達したか
LD HL, (.bik_frames)
LD BC, (.bik_next)
OR A
SBC HL, BC
JP C, .bik_none
LD A, (.bik_phase)
INC A
CP 6
JP C, .bik_ph
XOR A
.bik_ph
LD (.bik_phase), A
LD HL, (.bik_next)
LD BC, 4
OR A
JP NZ, .bik_add
DEC BC              ; 6 回に 1 回は 3
.bik_add
ADD HL, BC
LD (.bik_next), HL
.bik_ret
LD A, 1
CALL BAS_TALLOC
INC HL
LD (HL), E
DEC HL
RET
.bik_none
XOR A
JP BAS_TALLOC
.bik_vprev DB 0
.bik_frames DW 0
.bik_next DW 0
.bik_phase DB 0


; ============================================================ DATA

; @name BAS_DATAINIT
; @resident shared
; @param_count 2
; @calls BAS_WORK
; HL = DATA の本体, DE = 行の表 ((行番号, 位置) の並び、番兵 ($FFFF, 全長))
LD (BAS_DBASE), HL
LD (BAS_DPOS), HL
LD (BAS_DLTAB), DE
EX DE, HL
.bdi_loop
LD A, (HL)
INC HL
AND (HL)
INC HL
CP $FF
JP Z, .bdi_end
INC HL
INC HL
JP .bdi_loop
.bdi_end
LD E, (HL)
INC HL
LD D, (HL)          ; DE = 全長
LD HL, (BAS_DBASE)
ADD HL, DE
LD (BAS_DEND), HL
RET


; @name BAS_RESTORE
; @resident shared
; @param_count 1
; @calls BAS_WORK
; HL = 行番号 (0 = 先頭)。行番号が HL 以上の最初の DATA 行の先頭へ (なければ末尾)。
; DATA がない (BAS_DATAINIT が呼ばれていない) ときは末尾 (= 0) のままにする
EX DE, HL
LD HL, (BAS_DLTAB)
LD A, H
OR L
JP Z, .brs_end
EX DE, HL
LD A, H
OR L
JP NZ, .brs_find
LD HL, (BAS_DBASE)
LD (BAS_DPOS), HL
RET
.brs_find
EX DE, HL           ; DE = 行番号
LD HL, (BAS_DLTAB)
.brs_loop
LD C, (HL)
INC HL
LD B, (HL)          ; BC = 表の行番号 (番兵は $FFFF)
INC HL
LD A, B
AND C
CP $FF
JP Z, .brs_end
PUSH HL
LD H, B
LD L, C
OR A
SBC HL, DE          ; 表の行番号 - 行番号 (符号なし)
POP HL
JP NC, .brs_hit
INC HL
INC HL
JP .brs_loop
.brs_hit
LD E, (HL)
INC HL
LD D, (HL)
LD HL, (BAS_DBASE)
ADD HL, DE
LD (BAS_DPOS), HL
RET
.brs_end
LD HL, (BAS_DEND)
LD (BAS_DPOS), HL
RET


; @name BAS_READS
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_SCOPY,BAS_ERROR
; 次の DATA の項目を文字列で読む (読み尽くしたらエラー 4)
LD HL, (BAS_DPOS)
LD DE, (BAS_DEND)
PUSH HL
OR A
SBC HL, DE
POP HL
JP NC, .brd_out
LD A, (HL)
PUSH HL
LD C, A
LD B, 0
ADD HL, BC
INC HL
LD (BAS_DPOS), HL
POP HL
INC HL
JP BAS_SCOPY
.brd_out
LD HL, 4
JP BAS_ERROR


; @name BAS_READN
; @resident shared
; @param_count 0
; @calls BAS_READS,BAS_SVAL
; 次の DATA の項目を整数で読む (BAS_SVAL と同じ解釈、空の項目は 0)
CALL BAS_READS
JP BAS_SVAL


; ============================================================ PRINT の拡張

; @name BAS_PRAW
; @resident shared
; @param_count 1
; @calls BAS_WORK
; HL = 1 なら PRINT#0 の生出力 (制御コードも文字として出す、CSIZE が効く)、0 なら通常
LD A, L
AND 1
LD (BAS_RAW), A
RET


; @name BAS_PUFMT
; @resident shared
; @param_count 1
; @calls BAS_WORK
; HL = PRINT USING の書式 → 書式バッファに写す
LD DE, BAS_UFMT
LD C, (HL)
LD B, 0
INC BC
LDIR
RET


; @name BAS_PUSING
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_PCHR,BAS_UTOA,BAS_ERROR,BAS_PNUM
; HL = 値。書式バッファの「前置きの文字 + 数値の欄 + 後ろの文字」で出す。
; 数値の欄: 先頭の '+' (常に符号)、'**' (前を * で埋める、2 桁に数える)、'#'、'.'+'#' (小数部は 0)、
; 末尾の '-' (負なら後ろに -)。桁あふれはエラー 36 (Format over)。
LD (.bpu_val), HL
LD HL, BAS_UFMT
LD B, (HL)          ; B = 書式の残り
INC HL
; 前置きの文字 (数値の欄が始まるまで)
.bpu_pre
LD A, B
OR A
JP Z, .bpu_noform
LD A, (HL)
CP '#'
JP Z, .bpu_field
CP '+'
JP Z, .bpu_field
CP '*'
JP NZ, .bpu_prechar
INC HL
LD A, (HL)
DEC HL
CP '*'
JP Z, .bpu_field
LD A, '*'
.bpu_prechar
PUSH HL
PUSH BC
LD L, A
CALL BAS_PCHR
POP BC
POP HL
INC HL
DEC B
JP .bpu_pre
.bpu_field
XOR A
LD (.bpu_width), A
LD (.bpu_plus), A
LD (.bpu_star), A
LD (.bpu_minus), A
LD (.bpu_frac), A
LD A, (HL)
CP '+'
JP NZ, .bpu_nolead
LD A, 1
LD (.bpu_plus), A
LD A, (.bpu_width)
INC A
LD (.bpu_width), A
INC HL
DEC B
.bpu_nolead
LD A, B
OR A
JP Z, .bpu_fmtend
LD A, (HL)
CP '*'
JP NZ, .bpu_hash
INC HL
DEC B
LD A, B
OR A
JP Z, .bpu_fmtend
LD A, (HL)
CP '*'
JP NZ, .bpu_hash
LD A, 1
LD (.bpu_star), A
LD A, (.bpu_width)
ADD A, 2
LD (.bpu_width), A
INC HL
DEC B
.bpu_hash
LD A, B
OR A
JP Z, .bpu_fmtend
LD A, (HL)
CP '#'
JP Z, .bpu_h1
CP ','
JP NZ, .bpu_dot
.bpu_h1
LD A, (.bpu_width)
INC A
LD (.bpu_width), A
INC HL
DEC B
JP .bpu_hash
.bpu_dot
CP '.'
JP NZ, .bpu_trail
INC HL
DEC B
.bpu_fr
LD A, B
OR A
JP Z, .bpu_fmtend
LD A, (HL)
CP '#'
JP NZ, .bpu_trail
LD A, (.bpu_frac)
INC A
LD (.bpu_frac), A
INC HL
DEC B
JP .bpu_fr
.bpu_trail
LD A, B
OR A
JP Z, .bpu_fmtend
LD A, (HL)
CP '-'
JP NZ, .bpu_fmtend
LD A, 1
LD (.bpu_minus), A
INC HL
DEC B
.bpu_fmtend
LD (.bpu_rest), HL
LD A, B
LD (.bpu_restn), A
; 数字列を作る
LD HL, (.bpu_val)
XOR A
LD (.bpu_neg), A
BIT 7, H
JP Z, .bpu_abs
LD A, 1
LD (.bpu_neg), A
LD A, L
CPL
LD L, A
LD A, H
CPL
LD H, A
INC HL
.bpu_abs
CALL BAS_UTOA       ; HL = 数字列 (0 終端)
LD (.bpu_digits), HL
LD B, 0
.bpu_len
LD A, (HL)
OR A
JP Z, .bpu_lenok
INC B
INC HL
JP .bpu_len
.bpu_lenok
; 符号の文字が欄の中に要るか (先頭の + あり、または負で末尾の - なし)
LD C, 0
LD A, (.bpu_plus)
OR A
JP NZ, .bpu_signin
LD A, (.bpu_neg)
OR A
JP Z, .bpu_cnt
LD A, (.bpu_minus)
OR A
JP NZ, .bpu_cnt
.bpu_signin
LD C, 1
.bpu_cnt
; 必要な桁 = 数字 + 符号 (+ の分は width に含めて数えている)
LD A, (.bpu_plus)
OR A
LD A, B
JP NZ, .bpu_need    ; + の 1 桁は width に含まれている
ADD A, C
.bpu_need
LD D, A             ; D = 必要な桁 (+ の場合は数字の桁)
LD A, (.bpu_plus)
OR A
LD A, (.bpu_width)
JP Z, .bpu_w
DEC A               ; + の分を除いた数字の欄
.bpu_w
LD E, A             ; E = 数字の欄の桁
LD A, E
CP D
JP NC, .bpu_fit
; 桁あふれはエラー 36 (Format over。Hu-BASIC の実測)
LD HL, 36
JP BAS_ERROR
.bpu_fit
; 詰め文字 (E - D 個)
LD A, E
SUB D
LD B, A
.bpu_pad
LD A, B
OR A
JP Z, .bpu_sign
PUSH BC
PUSH DE
LD A, (.bpu_star)
OR A
LD L, ' '
JP Z, .bpu_padc
LD L, '*'
.bpu_padc
CALL BAS_PCHR
POP DE
POP BC
DEC B
JP .bpu_pad
.bpu_sign
LD A, C
OR A
JP Z, .bpu_dig
LD A, (.bpu_neg)
OR A
LD L, '+'
JP Z, .bpu_sgn
LD L, '-'
.bpu_sgn
CALL BAS_PCHR
.bpu_dig
LD HL, (.bpu_digits)
.bpu_dl
LD A, (HL)
OR A
JP Z, .bpu_frac0
PUSH HL
LD L, A
CALL BAS_PCHR
POP HL
INC HL
JP .bpu_dl
.bpu_frac0
LD A, (.bpu_frac)
OR A
JP Z, .bpu_tm
LD B, A
LD L, '.'
PUSH BC
CALL BAS_PCHR
POP BC
.bpu_fz
PUSH BC
LD L, '0'
CALL BAS_PCHR
POP BC
DJNZ .bpu_fz
.bpu_tm
LD A, (.bpu_minus)
OR A
JP Z, .bpu_post
LD A, (.bpu_neg)
OR A
LD L, ' '
JP Z, .bpu_tmc
LD L, '-'
.bpu_tmc
CALL BAS_PCHR
.bpu_post
; 後ろの文字
LD HL, (.bpu_rest)
LD A, (.bpu_restn)
LD B, A
.bpu_pl
LD A, B
OR A
RET Z
PUSH HL
PUSH BC
LD L, (HL)
CALL BAS_PCHR
POP BC
POP HL
INC HL
DEC B
JP .bpu_pl
.bpu_noform
; 数値の欄がない書式: 値をふつうに出す
LD HL, (.bpu_val)
JP BAS_PNUM
.bpu_val DW 0
.bpu_digits DW 0
.bpu_rest DW 0
.bpu_restn DB 0
.bpu_width DB 0
.bpu_plus DB 0
.bpu_star DB 0
.bpu_minus DB 0
.bpu_frac DB 0
.bpu_neg DB 0


; ============================================================ 文字の装飾

; @name BAS_CFLASH
; @resident shared
; @param_count 1
; @calls BAS_WORK
; HL = 1 なら点滅 (属性 $10)、0 なら解除
LD A, (BAS_ATTR)
AND $EF
BIT 0, L
JP Z, .bcf_set
OR $10
.bcf_set
LD (BAS_ATTR), A
RET


; @name BAS_CGEN
; @resident shared
; @param_count 1
; @calls BAS_WORK
; HL = 1 なら PCG (属性 $20)、0 なら ROM の文字
LD A, (BAS_ATTR)
AND $DF
BIT 0, L
JP Z, .bcg_set
OR $20
.bcg_set
LD (BAS_ATTR), A
RET


; @name BAS_CSIZE
; @resident shared
; @param_count 1
; @calls BAS_WORK
; HL = 0-3 (1 縦 2 倍、2 横 2 倍、3 縦横 2 倍)。拡大は PRINT#0 のときに効く
LD A, L
AND 3
LD (BAS_CSZ), A
RET


; ============================================================ テキスト版 LINE

; @name BAS_TLINE
; @resident shared
; @calls BAS_WORK,BAS_VOFF,BAS_VPUT,BAS_ERROR
; @stack_cleanup callee
; スタック引数 (x1, y1, x2, y2, 文字列, 箱なら 1)。指定した文字を現在の属性で、線 (水平・垂直・斜め)
; または箱 (外周) に置く。画面の外のセルは書かない。カーソルは動かさない
POP HL
LD (.btl_ret), HL
POP HL
LD (.btl_box), HL
POP HL
LD A, (HL)
OR A
JP NZ, .btl_ch
LD HL, 5
JP BAS_ERROR
.btl_ch
INC HL
LD A, (HL)
LD (.btl_char), A
POP HL
LD (.btl_y2), HL
POP HL
LD (.btl_x2), HL
POP HL
LD (.btl_y1), HL
POP HL
LD (.btl_x1), HL
LD HL, (.btl_ret)
PUSH HL
LD A, (.btl_box)
OR A
JP Z, .btl_line
; 箱: 上下の辺と左右の辺
LD HL, (.btl_y1)
LD (.btl_hy), HL
CALL .btl_hline
LD HL, (.btl_y2)
LD (.btl_hy), HL
CALL .btl_hline
LD HL, (.btl_x1)
LD (.btl_vx), HL
CALL .btl_vline
LD HL, (.btl_x2)
LD (.btl_vx), HL
JP .btl_vline
; 水平線 (x1..x2, y=.btl_hy)
.btl_hline
LD HL, (.btl_x1)
LD DE, (.btl_x2)
CALL .btl_order     ; HL <= DE
.btl_hl
PUSH HL
PUSH DE
LD DE, (.btl_hy)
CALL .btl_plot
POP DE
POP HL
PUSH HL
OR A
SBC HL, DE
POP HL
RET Z
INC HL
JP .btl_hl
; 垂直線 (y1..y2, x=.btl_vx)
.btl_vline
LD HL, (.btl_y1)
LD DE, (.btl_y2)
CALL .btl_order
.btl_vl
PUSH HL
PUSH DE
EX DE, HL           ; DE = y
LD HL, (.btl_vx)
CALL .btl_plot
POP DE
POP HL
PUSH HL
OR A
SBC HL, DE
POP HL
RET Z
INC HL
JP .btl_vl
; HL, DE を符号付きで小さい順に
.btl_order
PUSH HL
LD A, H
XOR D
JP M, .btl_od
OR A
SBC HL, DE
POP HL
RET C
RET Z
EX DE, HL
RET
.btl_od
POP HL
BIT 7, H
RET NZ              ; HL が負 = 小さい
EX DE, HL
RET
; (HL = x, DE = y) に文字を置く。範囲外は何もしない
.btl_plot
LD A, H
OR D
RET NZ              ; 負または 256 以上
LD A, (BAS_WID)
DEC A
CP L
RET C
LD A, E
CP 25
RET NC
LD H, E             ; H = y, L = x
CALL BAS_VOFF
LD A, (.btl_char)
LD E, A
LD A, (BAS_ATTR)
LD D, A
JP BAS_VPUT
; 線: Bresenham (dx, dy、符号付き)
.btl_line
LD HL, (.btl_x2)
LD DE, (.btl_x1)
OR A
SBC HL, DE
LD (.btl_dx), HL
LD HL, (.btl_y2)
LD DE, (.btl_y1)
OR A
SBC HL, DE
LD (.btl_dy), HL
; sx, sy と |dx|, |dy|
LD HL, (.btl_dx)
LD DE, 1
BIT 7, H
JP Z, .btl_sxok
LD DE, $FFFF
CALL .btl_neg
.btl_sxok
LD (.btl_dx), HL
LD (.btl_sx), DE
LD HL, (.btl_dy)
LD DE, 1
BIT 7, H
JP Z, .btl_syok
LD DE, $FFFF
CALL .btl_neg
.btl_syok
LD (.btl_dy), HL
LD (.btl_sy), DE
; err = dx - dy
LD HL, (.btl_dx)
LD DE, (.btl_dy)
OR A
SBC HL, DE
LD (.btl_err), HL
.btl_loop
LD HL, (.btl_x1)
LD DE, (.btl_y1)
CALL .btl_plot
; 終点に着いたら終わり
LD HL, (.btl_x1)
LD DE, (.btl_x2)
OR A
SBC HL, DE
JP NZ, .btl_step
LD HL, (.btl_y1)
LD DE, (.btl_y2)
OR A
SBC HL, DE
RET Z
.btl_step
; e2 = 2 * err
LD HL, (.btl_err)
ADD HL, HL
LD (.btl_e2), HL
; e2 > -dy なら err -= dy, x += sx
LD DE, (.btl_dy)
LD HL, 0
OR A
SBC HL, DE          ; HL = -dy
EX DE, HL           ; DE = -dy
LD HL, (.btl_e2)
CALL .btl_sgt       ; キャリー = HL > DE
JP NC, .btl_ny
LD HL, (.btl_err)
LD DE, (.btl_dy)
OR A
SBC HL, DE
LD (.btl_err), HL
LD HL, (.btl_x1)
LD DE, (.btl_sx)
ADD HL, DE
LD (.btl_x1), HL
.btl_ny
; e2 < dx なら err += dx, y += sy
LD HL, (.btl_dx)
LD DE, (.btl_e2)
CALL .btl_sgt       ; キャリー = dx > e2
JP NC, .btl_loop
LD HL, (.btl_err)
LD DE, (.btl_dx)
ADD HL, DE
LD (.btl_err), HL
LD HL, (.btl_y1)
LD DE, (.btl_sy)
ADD HL, DE
LD (.btl_y1), HL
JP .btl_loop
; HL > DE (符号付き) ならキャリーを立てる
.btl_sgt
LD A, H
XOR D
JP M, .btl_sgd
EX DE, HL
OR A
SBC HL, DE          ; DE - HL < 0 ⇔ HL > DE
RET
.btl_sgd
LD A, D             ; 異符号: DE が負なら HL > DE
RLA
RET
.btl_neg
LD A, L
CPL
LD L, A
LD A, H
CPL
LD H, A
INC HL
RET
.btl_ret DW 0
.btl_box DW 0
.btl_char DB 0
.btl_x1 DW 0
.btl_y1 DW 0
.btl_x2 DW 0
.btl_y2 DW 0
.btl_hy DW 0
.btl_vx DW 0
.btl_dx DW 0
.btl_dy DW 0
.btl_sx DW 0
.btl_sy DW 0
.btl_err DW 0
.btl_e2 DW 0


; ============================================================ PCG

; @name BAS_DEFCHR
; @resident shared
; @param_count 2
; @calls BAS_WORK,PCGDEF,BAS_ERROR
; HL = 文字コード (0-255、それ以外はエラー 5), DE = 文字列 (青 8・赤 8・緑 8 の面ごとの並び)。
; 24 バイトに満たない分は 0。PCGDEF が受け取る「行ごとに青・赤・緑」の並びに組み替えて書く
LD A, H
OR A
JP Z, .bdc_ok
LD HL, 5
JP BAS_ERROR
.bdc_ok
PUSH HL
; BAS_PCGBUF を 0 で埋める
LD HL, BAS_PCGBUF
LD B, 24
XOR A
.bdc_clr
LD (HL), A
INC HL
DJNZ .bdc_clr
; 面 p (0-2)・行 r (0-7) のバイト (元の位置 p*8+r) を、先 r*3+p へ
LD A, (DE)          ; A = 長さ
CP 25
JP C, .bdc_len
LD A, 24
.bdc_len
LD B, A             ; B = 写すバイト数
INC DE
LD C, 0             ; C = 元の位置
.bdc_loop
LD A, B
OR A
JP Z, .bdc_put
PUSH BC
LD A, C
AND 7               ; r
LD B, A
ADD A, A
ADD A, B            ; r*3
LD B, A
LD A, C
SRL A
SRL A
SRL A               ; p
ADD A, B            ; r*3 + p
LD HL, BAS_PCGBUF
ADD A, L
LD L, A
LD A, 0
ADC A, H
LD H, A
LD A, (DE)
LD (HL), A
INC DE
POP BC
INC C
DEC B
JP .bdc_loop
.bdc_put
POP HL              ; L = 文字コード
LD DE, BAS_PCGBUF
JP PCGDEF


; @name BAS_SCGPAT
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_TALLOC,GETCGROM,BAS_PCGREAD,BAS_ERROR
; HL = 文字コード (0-255、それ以外はエラー 5) → 32 バイトの文字列 (ROM の字形 8 + PCG の青・赤・緑 各 8)
LD A, H
OR A
JP Z, .bcp_ok
LD HL, 5
JP BAS_ERROR
.bcp_ok
LD A, L
LD (.bcp_code), A
LD A, 32
CALL BAS_TALLOC
LD (.bcp_res), HL
INC HL
EX DE, HL           ; DE = 本体の先頭
LD A, (.bcp_code)
LD L, A
LD H, 0
PUSH DE
CALL GETCGROM       ; ROM の字形 8 バイト
POP DE
LD HL, 8
ADD HL, DE
LD (.bcp_dst), HL
LD B, $15
.bcp_plane
PUSH BC
LD A, (.bcp_code)
LD C, A
LD HL, (.bcp_dst)
CALL BAS_PCGREAD    ; B = ポートの上位、C = コード、HL = 先
LD HL, (.bcp_dst)
LD DE, 8
ADD HL, DE
LD (.bcp_dst), HL
POP BC
INC B
LD A, B
CP $18
JP NZ, .bcp_plane
LD HL, (.bcp_res)
RET
.bcp_code DB 0
.bcp_res DW 0
.bcp_dst DW 0


; @name BAS_PCGREAD
; @resident shared
; @param_count 0
; @calls sWORK,X1WORK,PCGCOMMON
; B = PCG の読み出しポートの上位 ($15 青 / $16 赤 / $17 緑)、C = 文字コード、HL = 先 (8 バイト)。
; GETCGROM と同じ方式: 表示されないセルに対象の文字を PCG (属性 $20) で敷き、ラスタに同期して
; 1 ライン 250T で 8 ライン読む
LD (.bpr_dest), HL
LD A, B
LD (.bpr_port), A
LD A, C
LD (SETPCGCELLS.GCG_CODE), A
LD A, (AT_WIDTH)
CP 40
JP Z, .bpr_w40
LD HL, $07D0
LD A, 48
JP .bpr_span
.bpr_w40
LD HL, $03E8
LD A, 24
.bpr_span
LD (SETPCGCELLS.GCG_NODISPADR), HL
LD (SETPCGCELLS.GCG_SPAN), A
LD A, $20
LD (SETPCGCELLS.GCG_ATTR), A
CALL SETPCGCELLS
LD HL, (.bpr_dest)
LD A, (.bpr_port)
LD B, A
LD C, 0
LD E, 8
EXX
DI
LD BC, $1A01
.bpr_vdsp0
IN A, (C)
JP P, .bpr_vdsp0
.bpr_vdsp1
IN A, (C)
JP M, .bpr_vdsp1
EXX
.bpr_loop
IN A, (C)           ; 12
LD (HL), A          ; 7
INC HL              ; 6
NOP                 ; 4
NOP                 ; 4
LD A, $0E           ; 7
.bpr_dly
DEC A               ; 4
JP NZ, .bpr_dly     ; 10
DEC E               ; 4
JP NZ, .bpr_loop    ; 10
EI
RET
.bpr_dest DW 0
.bpr_port DB 0


; ============================================================ 入力

; @name BAS_PSGW
; @resident shared
; @param_count 0
; A = PSG のレジスタ、E = 値を書く
PUSH BC
LD BC, $1C00
OUT (C), A
LD BC, $1B00
OUT (C), E
POP BC
RET


; @name BAS_PSGR
; @resident shared
; @param_count 0
; A = PSG のレジスタ → A = 値
PUSH BC
LD BC, $1C00
OUT (C), A
LD BC, $1B00
IN A, (C)
POP BC
RET


; @name BAS_STICK
; @resident shared
; @param_count 1
; @calls sGETKY,BAS_PSGR,BAS_ERROR
; HL = 0: キーボード (押されている '1'-'9' の値。テンキーとメインの数字キー)、1・2: ジョイスティック
; (PSG のレジスタ 14・15、負論理: b0 上 b1 下 b2 左 b3 右)。戻り値はテンキーの配置 (0 = 入力なし)
LD A, H
OR A
JP NZ, .bst_err
LD A, L
OR A
JP NZ, .bst_joy
CALL sGETKY
SUB '1'
JP C, .bst_zero
CP 9
JP NC, .bst_zero
INC A
LD L, A
LD H, 0
RET
.bst_zero
LD HL, 0
RET
.bst_joy
CP 3
JP NC, .bst_err
ADD A, 13           ; 1 → 14, 2 → 15
CALL BAS_PSGR
CPL
AND $0F             ; 押されている方向のビット (b0 上 b1 下 b2 左 b3 右)
LD HL, .bst_map
LD E, A
LD D, 0
ADD HL, DE
LD L, (HL)
LD H, 0
RET
.bst_err
LD HL, 5
JP BAS_ERROR
; 上下左右のビットの組 (0-15) → テンキーの配置
.bst_map
DB 0, 8, 2, 0, 4, 7, 1, 4, 6, 9, 3, 6, 0, 8, 2, 0


; @name BAS_STRIG
; @resident shared
; @param_count 1
; @calls sGETKY,BAS_PSGR,BAS_ERROR
; HL = 0: スペースキー、1・2: ジョイスティックのトリガー (b5・b6、負論理)。押されていれば -1
LD A, H
OR A
JP NZ, .bsg_err
LD A, L
OR A
JP NZ, .bsg_joy
CALL sGETKY
CP ' '
JP Z, .bsg_on
LD HL, 0
RET
.bsg_joy
CP 3
JP NC, .bsg_err
ADD A, 13
CALL BAS_PSGR
AND $60
CP $60
JP NZ, .bsg_on
LD HL, 0
RET
.bsg_on
LD HL, $FFFF
RET
.bsg_err
LD HL, 5
JP BAS_ERROR


; ============================================================ 音

; @name BAS_SNDINIT
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_PSGW
; テンポ 120、各チャンネル O4・長さ 5 (1/32 単位で 8)・音量 15。PSG のミキサー $3F、音量 0
LD HL, 62           ; 7500 / 120
LD (BAS_TEMPN), HL
LD HL, BAS_PLST
LD B, 3
.bsi_ch
LD (HL), 4
INC HL
LD (HL), 8
INC HL
LD (HL), 15
INC HL
DJNZ .bsi_ch
LD A, 7
LD E, $3F
CALL BAS_PSGW
LD A, 8
LD E, 0
CALL BAS_PSGW
LD A, 9
CALL BAS_PSGW
LD A, 10
JP BAS_PSGW


; @name BAS_TEMPO
; @resident shared
; @param_count 1
; @calls BAS_WORK,DIVHLDE,BAS_ERROR
; HL = テンポ (30-7500、範囲外はエラー 5)。1/32 音符を 7500/テンポ 個の約 1ms の刻みにする
LD DE, 30
PUSH HL
OR A
SBC HL, DE
POP HL
JP C, .btp_err
LD DE, 7501
PUSH HL
OR A
SBC HL, DE
POP HL
JP NC, .btp_err
EX DE, HL
LD HL, 7500
CALL DIVHLDE        ; HL = 7500 / テンポ
LD (BAS_TEMPN), HL
RET
.btp_err
LD HL, 5
JP BAS_ERROR


; @name BAS_TICK
; @resident shared
; @param_count 0
; 約 1ms (3983 T、Hu-BASIC の実測した刻み) 待つ。割り込みは止めている前提
PUSH BC
LD B, 0
.btk_a
DJNZ .btk_a         ; 256 回 × 13T
LD B, 43
.btk_b
DJNZ .btk_b         ; 43 回 × 13T (呼び出しと BAS_UNITWAIT の 1 周を含めて約 3976T)
NOP
NOP
POP BC
RET


; @name BAS_UNITWAIT
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_TICK
; 1/32 音符の長さ (BAS_TEMPN 個の刻み) だけ待つ
LD HL, (BAS_TEMPN)
.buw_loop
LD A, H
OR L
RET Z
CALL BAS_TICK
DEC HL
JP .buw_loop


; @name BAS_PLAY
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_PSGW,BAS_UNITWAIT,BAS_PLNOTE,BAS_ERROR
; HL = MML の文字列。':' で区切った最大 3 チャンネルを同時に進め、鳴らし終わるまで戻らない
; (Hu-BASIC の実測: PLAY はブロックする)。チャンネルごとの状態 (オクターブ・長さ・音量) は PLAY をまたいで残る
LD B, (HL)          ; B = 残りの文字数
INC HL
; チャンネルを空にする
PUSH HL
LD HL, BAS_PLCH
LD C, 15
XOR A
.bpl_clr
LD (HL), A
INC HL
DEC C
JP NZ, .bpl_clr
POP HL
; ':' で分ける。DE = 今のチャンネルの枠、C = その長さ
LD DE, BAS_PLCH
LD A, L
LD (DE), A
INC DE
LD A, H
LD (DE), A
DEC DE
LD C, 0
XOR A
LD (.bpl_nch), A
.bpl_split
LD A, B
OR A
JP Z, .bpl_last
LD A, (HL)
CP ':'
JP NZ, .bpl_more
; 区切り: 今のチャンネルの長さを書き、次のチャンネルへ
PUSH HL
LD H, D
LD L, E
INC HL
INC HL
LD (HL), C          ; 残り
INC HL
INC HL
LD (HL), 1          ; 動作中
POP HL
LD A, (.bpl_nch)
INC A
LD (.bpl_nch), A
CP 3
JP Z, .bpl_err      ; 4 つ目のチャンネル
PUSH HL
LD HL, 5
ADD HL, DE
EX DE, HL           ; DE = 次の枠
POP HL
INC HL
DEC B
LD A, L
LD (DE), A
INC DE
LD A, H
LD (DE), A
DEC DE
LD C, 0
JP .bpl_split
.bpl_more
INC C
INC HL
DEC B
JP .bpl_split
.bpl_last
LD H, D
LD L, E
INC HL
INC HL
LD (HL), C
INC HL
INC HL
LD (HL), 1
; ミキサー: 3 チャンネルとも音を出す、ノイズは止める
LD A, 7
LD E, $38
CALL BAS_PSGW
; 主ループ: 数えが 0 のチャンネルは次の音を読む。全チャンネルが終わったら抜ける
.bpl_main
XOR A
LD (.bpl_any), A
LD (.bpl_ch), A
.bpl_chloop
LD A, (.bpl_ch)
CALL .bpl_frame     ; HL = 枠
INC HL
INC HL
INC HL
LD A, (HL)          ; 数え
INC HL
LD C, (HL)          ; 動作中
DEC HL
DEC HL
DEC HL
DEC HL
LD B, A
LD A, C
OR A
JP Z, .bpl_next
LD A, B
OR A
JP NZ, .bpl_active
LD A, (.bpl_ch)
CALL BAS_PLNOTE     ; 次の音 (終わりなら動作中を 0 に)
LD A, (.bpl_ch)
CALL .bpl_frame
LD DE, 4
ADD HL, DE
LD A, (HL)
OR A
JP Z, .bpl_next
.bpl_active
LD A, 1
LD (.bpl_any), A
.bpl_next
LD A, (.bpl_ch)
INC A
LD (.bpl_ch), A
CP 3
JP NZ, .bpl_chloop
LD A, (.bpl_any)
OR A
RET Z
CALL BAS_UNITWAIT
; 動作中のチャンネルの数えを 1 減らす
LD HL, BAS_PLCH + 3
LD B, 3
.bpl_dec
INC HL
LD A, (HL)          ; 動作中
DEC HL
OR A
JP Z, .bpl_dn
DEC (HL)
.bpl_dn
LD DE, 5
ADD HL, DE
DJNZ .bpl_dec
JP .bpl_main
; A = チャンネル → HL = BAS_PLCH + A*5
.bpl_frame
LD L, A
ADD A, A
ADD A, A
ADD A, L
LD L, A
LD H, 0
LD DE, BAS_PLCH
ADD HL, DE
RET
.bpl_err
LD HL, 5
JP BAS_ERROR
.bpl_nch DB 0
.bpl_any DB 0
.bpl_ch DB 0


; @name BAS_PLNOTE
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_PSGW,BAS_ERROR,BAS_PTAB
; A = チャンネル。そのチャンネルの MML から次の音 (または休符) を読み、PSG に書いて数えを設定する。
; 文字列が尽きたら音量 0 にして動作中を 0 にする
LD (.bpn_ch), A
LD L, A
ADD A, A
ADD A, A
ADD A, L
LD L, A
LD H, 0
LD DE, BAS_PLCH
ADD HL, DE
LD (.bpn_frame), HL
LD A, (.bpn_ch)
LD L, A
ADD A, A
ADD A, L
LD L, A
LD H, 0
LD DE, BAS_PLST
ADD HL, DE
LD (.bpn_st), HL
XOR A
LD (.bpn_sharp), A
LD (.bpn_delta), A
.bpn_parse
CALL .bpn_get       ; A = 次の文字、キャリー = 尽きた
JP C, .bpn_end
CP ' '
JP Z, .bpn_parse
AND $DF             ; 小文字 → 大文字 (記号は下で比べる前に元の値を見る)
LD C, A
LD A, (.bpn_last)
CP '#'
JP Z, .bpn_issharp
CP '+'
JP Z, .bpn_up
CP '-'
JP Z, .bpn_down
LD A, C
CP 'O'
JP Z, .bpn_oct
CP 'V'
JP Z, .bpn_vol
CP 'R'
JP Z, .bpn_rest
SUB 'A'
JP C, .bpn_err
CP 7
JP NC, .bpn_err
; 音名 → 半音 (A9 B11 C0 D2 E4 F5 G7)
LD E, A
LD D, 0
LD HL, .bpn_semi
ADD HL, DE
LD A, (.bpn_sharp)
ADD A, (HL)
LD (.bpn_semit), A
CALL .bpn_len
; オクターブ = 状態 + 一時的な上下、半音が 12 ならオクターブを 1 つ上げる
LD HL, (.bpn_st)
LD A, (.bpn_delta)
ADD A, (HL)
LD C, A
LD A, (.bpn_semit)
CP 12
JP C, .bpn_o
SUB 12
LD (.bpn_semit), A
INC C
.bpn_o
LD A, C
DEC A
CP 8
JP NC, .bpn_err     ; オクターブが 1-8 の外
; 表の位置 = (オクターブ - 1) * 12 + 半音
LD L, A
ADD A, A
ADD A, L            ; ×3
ADD A, A
ADD A, A            ; ×12
LD HL, .bpn_semit
ADD A, (HL)
LD E, A
LD D, 0
LD HL, BAS_PTAB
ADD HL, DE
ADD HL, DE
LD C, (HL)
INC HL
LD B, (HL)          ; BC = 音程の値
LD A, (.bpn_ch)
ADD A, A
LD E, C
CALL BAS_PSGW       ; 細
INC A
LD E, B
CALL BAS_PSGW       ; 粗
LD HL, (.bpn_st)
INC HL
INC HL
LD E, (HL)          ; 音量
LD A, (.bpn_ch)
ADD A, 8
CALL BAS_PSGW
JP .bpn_count
.bpn_rest
CALL .bpn_len
LD A, (.bpn_ch)
ADD A, 8
LD E, 0
CALL BAS_PSGW
.bpn_count
; 数え = 状態の長さ
LD HL, (.bpn_st)
INC HL
LD A, (HL)
LD HL, (.bpn_frame)
INC HL
INC HL
INC HL
LD (HL), A
RET
.bpn_issharp
LD A, 1
LD (.bpn_sharp), A
JP .bpn_parse
.bpn_up
LD A, 1
LD (.bpn_delta), A
JP .bpn_parse
.bpn_down
LD A, $FF
LD (.bpn_delta), A
JP .bpn_parse
.bpn_oct
CALL .bpn_digit
JP C, .bpn_err
OR A
JP Z, .bpn_err
CP 9
JP NC, .bpn_err
LD HL, (.bpn_st)
LD (HL), A
JP .bpn_parse
.bpn_vol
CALL .bpn_digit
JP C, .bpn_err
LD C, A
CALL .bpn_peekdigit ; 2 桁目
JP C, .bpn_v1
LD A, C
ADD A, A
LD B, A
ADD A, A
ADD A, A
ADD A, B            ; ×10
LD B, A
CALL .bpn_digit
ADD A, B
LD C, A
.bpn_v1
LD A, C
CP 16
JP NC, .bpn_err
LD HL, (.bpn_st)
INC HL
INC HL
LD (HL), A
JP .bpn_parse
.bpn_end
; 文字列が尽きた: 音量 0、動作中を 0 に
LD A, (.bpn_ch)
ADD A, 8
LD E, 0
CALL BAS_PSGW
LD HL, (.bpn_frame)
LD DE, 4
ADD HL, DE
LD (HL), 0
RET
.bpn_err
LD HL, 5
JP BAS_ERROR
; 長さの数字 (0-9) があれば状態の長さにする
.bpn_len
CALL .bpn_peekdigit
RET C
CALL .bpn_digit
LD E, A
LD D, 0
LD HL, .bpn_ltab
ADD HL, DE
LD A, (HL)
LD HL, (.bpn_st)
INC HL
LD (HL), A
RET
; 次の文字を取る (A、キャリー = 尽きた)。元の文字は .bpn_last に
.bpn_get
LD HL, (.bpn_frame)
INC HL
INC HL
LD A, (HL)          ; 残り
OR A
SCF
RET Z
DEC (HL)
DEC HL
DEC HL
LD E, (HL)
INC HL
LD D, (HL)          ; DE = 位置
LD A, (DE)
LD (.bpn_last), A
INC DE
LD (HL), D
DEC HL
LD (HL), E
OR A
RET
; 次の文字が数字なら キャリーなし (読まない)
.bpn_peekdigit
LD HL, (.bpn_frame)
INC HL
INC HL
LD A, (HL)
OR A
SCF
RET Z
DEC HL
DEC HL
LD E, (HL)
INC HL
LD D, (HL)
LD A, (DE)
SUB '0'
RET C
CP 10
CCF
RET
; 次の文字を数字として読む (A = 値、キャリー = 数字でない・尽きた)
.bpn_digit
CALL .bpn_peekdigit
RET C
CALL .bpn_get
SUB '0'
OR A
RET
.bpn_ch DB 0
.bpn_frame DW 0
.bpn_st DW 0
.bpn_sharp DB 0
.bpn_delta DB 0
.bpn_semit DB 0
.bpn_last DB 0
.bpn_semi DB 9, 11, 0, 2, 4, 5, 7
; 長さ 0-9 → 1/32 音符の個数 (Hu-BASIC の実測)
.bpn_ltab DB 1, 2, 3, 4, 6, 8, 12, 16, 24, 32


; @name BAS_PTAB
; @resident shared
; @param_count 0
; 音程の値 (PSG 2MHz、125000 / 周波数 の切り捨て、Hu-BASIC の実測と一致)。O1-O8 × C C# D D# E F F# G G# A A# B
DW 3822, 3607, 3405, 3214, 3033, 2863, 2702, 2551, 2407, 2272, 2145, 2024
DW 1911, 1803, 1702, 1607, 1516, 1431, 1351, 1275, 1203, 1136, 1072, 1012
DW 955, 901, 851, 803, 758, 715, 675, 637, 601, 568, 536, 506
DW 477, 450, 425, 401, 379, 357, 337, 318, 300, 284, 268, 253
DW 238, 225, 212, 200, 189, 178, 168, 159, 150, 142, 134, 126
DW 119, 112, 106, 100, 94, 89, 84, 79, 75, 71, 67, 63
DW 59, 56, 53, 50, 47, 44, 42, 39, 37, 35, 33, 31
DW 29, 28, 26, 25, 23, 22, 21, 19, 18, 17, 16, 15


; @name BAS_BEEP
; @resident shared
; @param_count 0
; @calls BAS_PSGW,BAS_PSGR,BAS_TICK
; チャンネル C を音程の値 190・音量 15 で約 80ms 鳴らし、音程を元に戻す (Hu-BASIC の実測)
LD A, 4
CALL BAS_PSGR
LD (.bbp_save), A
LD A, 5
CALL BAS_PSGR
LD (.bbp_save + 1), A
LD A, 7
LD E, $38
CALL BAS_PSGW
LD A, 4
LD E, 190
CALL BAS_PSGW
LD A, 5
LD E, 0
CALL BAS_PSGW
LD A, 10
LD E, 15
CALL BAS_PSGW
LD B, 80
.bbp_wait
CALL BAS_TICK
DJNZ .bbp_wait
LD A, 10
LD E, 0
CALL BAS_PSGW
LD A, (.bbp_save)
LD E, A
LD A, 4
CALL BAS_PSGW
LD A, (.bbp_save + 1)
LD E, A
LD A, 5
JP BAS_PSGW
.bbp_save DW 0


; @name BAS_CLICK
; @resident shared
; @param_count 1
; @calls BAS_WORK
; HL = 1 (ON) / 0 (OFF)。キーのクリック音はランタイムが出さないので、状態を持つだけ
LD A, L
LD (BAS_CLK), A
RET
