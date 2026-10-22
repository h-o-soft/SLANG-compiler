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
; @works BAS_CXY:2,BAS_WID:1,BAS_ATTR:1,BAS_CSZ:1,BAS_RAW:1,BAS_PAL:3,BAS_CSP:2,BAS_CSTK:640,BAS_TMPP:2,BAS_TMP:1024,BAS_NUMBUF:8
; BAS_CX/CY: カーソル、BAS_WID: 桁数 (40/80)、BAS_ATTR: 現在の属性 (色 + CR/CF/CG)
; BAS_CSZ: CSIZE (0-3)、BAS_RAW: PRINT#0 中なら 1、BAS_PAL*: パレットの影 (書込専用ポートのため)
; BAS_CSP/BAS_CSTK: 制御スタック (10 バイト × 64 枠)
; BAS_TMPP/BAS_TMP: 文字列の一時領域 (使用位置 / 1024 バイト)
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
; @calls BAS_WORK,sWORK,INIT_CRTC,_C4025L,_C4025H,BAS_CLS,BAS_SETPAL,BAS_INITSTATE
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
JR C, .bi_crtc
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
JR .be_halt


; @name BAS_ERROR
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_PNL,BAS_PSTRZ,BAS_PNUM,BAS_END
; HL = エラー番号。改行して "Error <番号>" を出して止まる
PUSH HL
XOR A
LD (BAS_RAW), A
LD (BAS_CSZ), A
LD A, (BAS_CX)
OR A
CALL NZ, BAS_PNL
LD HL, .ber_msg
CALL BAS_PSTRZ
POP HL
CALL BAS_PNUM
JP BAS_END
.ber_msg
DB "Error", 0


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
JR NZ, .bv_40
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
JR Z, .bc_all
INC H               ; H = $FF (負) なら 0 に
JR Z, .bc_text
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
JR NZ, .bct_loop
XOR A
LD (BAS_CX), A
LD (BAS_CY), A
RET


; @name BAS_CLSGR
; @resident shared
; @param_count 0
; A = 0: グラフィック 3 面、1-3: その面だけを 0 で埋める
OR A
JR NZ, .bcg_one
LD A, $40
CALL .bcg_plane
LD A, $80
CALL .bcg_plane
LD A, $C0
JR .bcg_plane
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
JR NZ, .bcg_loop
INC B
DEC D
JR NZ, .bcg_loop
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
JR NZ, .bs_n
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
JR NZ, .bs_loop
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
JR C, .bn_set
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
JR NZ, .bp_char
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
JR Z, .bp_w1
INC A
.bp_w1
LD HL, BAS_WID
CP (HL)
JR C, .bp_put
PUSH DE
CALL BAS_NEWLINE
POP DE
.bp_put
LD A, (BAS_RAW)
OR A
LD A, (BAS_CSZ)
JR NZ, .bp_sz
XOR A               ; 生出力でなければ拡大しない
.bp_sz
OR A
JR Z, .bp_normal
; 拡大: 属性に HS/VS を付けて、横 2 マス・縦 2 行に同じ文字を書く
LD C, A             ; C = CSIZE (1 縦 / 2 横 / 3 縦横)
LD A, (BAS_ATTR)
BIT 0, C
JR Z, .bp_nv
OR $40
.bp_nv
BIT 1, C
JR Z, .bp_nh
OR $80
.bp_nh
LD D, A
PUSH BC
LD HL, (BAS_CX)     ; L = X, H = Y
CALL .bp_cell
POP BC
PUSH BC
BIT 1, C
JR Z, .bp_noright
LD HL, (BAS_CX)
INC L
CALL .bp_cell
.bp_noright
POP BC
PUSH BC
BIT 0, C
JR Z, .bp_nodown
LD HL, (BAS_CX)
INC H
LD A, H
CP 25
JR NC, .bp_nodown
CALL .bp_cell
POP BC
PUSH BC
BIT 1, C
JR Z, .bp_nodown
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
JR BAS_PSTRZ


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
JR Z, .bsp_n
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
JR C, .bpz_got
INC B
JR .bpz_div
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
JR .bpz_pad


; @name BAS_PNUM
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_PCHR,BAS_UTOA
; HL = 符号付きの値。符号の桁 (正は空白、負は '-') + 10 進 + 後ろに空白 1 つ
LD A, ' '
BIT 7, H
JR Z, .bpn_pos
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
JR Z, .bpn_end
PUSH HL
LD L, A
CALL BAS_PCHR
POP HL
INC HL
JR .bpn_loop
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
JR NZ, .bu_loop
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
JR NZ, .bl_err
LD A, (BAS_WID)
DEC A
CP L
JR C, .bl_err
LD A, E
CP 25
JR NC, .bl_err
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
JR NZ, .bco_bg
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
JR NZ, .bw_80
LD A, L
CP 41
JR NC, .bw_80
LD A, 40
LD HL, _C4025L
LD DE, _C4025H
JR .bw_set
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
JR C, .bw_crtc
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
JR NC, .bcs_full
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
JR Z, .bg_none
LD DE, 0 - 10
ADD HL, DE
LD A, (HL)
CP 1
JR NZ, .bg_loop
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
JR NZ, .bf_neg
; lim - v >= 0 か (符号付き)
CALL .bf_cmp        ; HL - DE の符号 → キャリー = 負
JR C, .bf_skip
JR .bf_run
.bf_neg
EX DE, HL           ; HL = v, DE = lim → v - lim >= 0 か
CALL .bf_cmp
JR C, .bf_skip
.bf_run
LD HL, (.bf_var)
CALL BAS_FORFIND    ; 同じ変数の枠を探す (HL = 枠 / 0)
LD A, H
OR L
JR Z, .bf_push
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
JR Z, .bff_none
PUSH DE
LD DE, 0 - 10
ADD HL, DE
POP DE
LD A, (HL)
CP 1
JR Z, .bff_none     ; GOSUB 枠で打ち切り
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
JR .bff_loop
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
JR NZ, .bn_found
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
JR Z, .bn_inrange
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
JR NZ, .bn_negstep
; step >= 0: lim - 和 >= 0 なら続行
BIT 7, B
JR NZ, .bn_exit
JR .bn_cont
.bn_negstep
; step < 0: lim - 和 <= 0 なら続行
BIT 7, B
JR NZ, .bn_cont
LD A, B
OR H
OR L
JR Z, .bn_cont
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
JR NZ, .bd_dup
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
JR NZ, .ba_check
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
JR NZ, .bdr_go
LD HL, 11
JP BAS_ERROR
.bdr_go
LD A, H
XOR D
LD (.bdr_sign), A   ; bit7 = 結果の符号
BIT 7, H
JR Z, .bdr_a
LD A, L
CPL
LD L, A
LD A, H
CPL
LD H, A
INC HL              ; |a|
.bdr_a
BIT 7, D
JR Z, .bdr_b
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
JR C, .bdr_nround
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
JR NZ, .bw_wait1
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
JR NZ, .bwl_loop
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
