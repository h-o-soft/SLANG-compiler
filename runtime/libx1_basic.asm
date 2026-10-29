; libx1_basic.asm
; SLANG x1native runtime — X1 BASIC 系ライブラリ (BAS_ 接頭辞)
;
; HuBASIC と同じように振る舞う画面・文字列・DATA・制御スタック等の処理。
; hubasic2sl (HuBASIC → SLANG トランスパイラ) の出力が使うが、手書きの SLANG
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
; @works BAS_CXY:2,BAS_WID:1,BAS_ATTR:1,BAS_CSZ:1,BAS_RAW:1,BAS_PAL:3,BAS_CSP:2,BAS_CSTK:640,BAS_TMPP:2,BAS_TMP:1024,BAS_NUMBUF:8,BAS_DBASE:2,BAS_DLTAB:2,BAS_DPOS:2,BAS_DEND:2,BAS_UFMT:256,BAS_PCGBUF:32,BAS_TEMPN:2,BAS_PLST:9,BAS_PLCH:15,BAS_LASTKEY:1,BAS_CLK:1,BAS_LNPTR:2,BAS_INBUF:256,BAS_INPOS:2,BAS_PUST:10,BAS_KREP:1,BAS_WIN:4
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
; BAS_INBUF: INPUT で読んだ 1 行 (長さ付き)、BAS_INPOS: 次の項目の始まり (1 始まりの位置)
; BAS_PUST: PRINT USING の数値の欄 (BAS_PUPARSE が読み、BAS_PULAY が使う。下の EQU)
; BAS_KREP: キーのリピート (REPEAT ON で 1、OFF で 0。起動したときは 1、INIT では戻さない)
; BAS_WIN: テキストの窓 (CONSOLE)。左・上・右・下 (右と下も窓に含む)。全画面は 0・0・桁数-1・24
; カーソルとパレットは連続した番地が要るので、まとめて確保して EQU で分ける
BAS_CX EQU BAS_CXY
BAS_CY EQU BAS_CXY + 1
BAS_WX0 EQU BAS_WIN             ; 窓の左の桁 (BAS_WY0 と続けて置く。LD HL,(BAS_WX0) で L = 左、H = 上)
BAS_WY0 EQU BAS_WIN + 1         ; 窓の上の行
BAS_WX1 EQU BAS_WIN + 2         ; 窓の右の桁
BAS_WY1 EQU BAS_WIN + 3         ; 窓の下の行
BAS_PALB EQU BAS_PAL
BAS_PALR EQU BAS_PAL + 1
BAS_PALG EQU BAS_PAL + 2
BAS_PUWID EQU BAS_PUST          ; 欄の桁 ('+' と '**' と ',' を含む。小数部は含まない)
BAS_PUPLUS EQU BAS_PUST + 1     ; 先頭の '+' があれば 1
BAS_PUSTAR EQU BAS_PUST + 2     ; '**' があれば 1
BAS_PUMINUS EQU BAS_PUST + 3    ; 末尾の '-' があれば 1
BAS_PUFRAC EQU BAS_PUST + 4     ; 小数部の桁
BAS_PUCOMMA EQU BAS_PUST + 5    ; 欄の中に ',' があれば 0 以外
BAS_PURESTN EQU BAS_PUST + 6    ; 書式の後ろの文字の数
BAS_PUNEG EQU BAS_PUST + 7      ; 値が負なら 1 (BAS_PULAY に渡す)
BAS_PUREST EQU BAS_PUST + 8     ; 書式の後ろの文字の先頭 (2 バイト)
RET


; ============================================================ 組の印

; @name BAS_ABI_1
; @resident shared
; @param_count 0
; hubasic2sl の出力とこのランタイムの組の印 (何もしない)。出力は MAIN の最初に BAS_ABI_<n>() を呼ぶので、
; 番号の違う組を混ぜると、アセンブルで未定義のラベルになって止まる。
; 出力とランタイムの約束 (関数の引数・戻り値の意味、作業領域・バッファの作り、呼ぶ順番の前提) を変えたら、
; ここの番号と hubasic2sl の SlangBackend.BasAbi を一緒に 1 つ上げる (迷ったら上げる)。
; ランタイムの中だけの直し (出力が前提にしない振る舞いを HuBASIC に合わせるなど) では上げない。
RET


; ============================================================ 初期化・終了・エラー

; @name BAS_INIT
; @resident shared
; @param_count 0
; @calls BAS_WORK,sWORK,INIT_CRTC,_C4025L,_C4025H,BAS_CLS,BAS_SETPAL,BAS_INITSTATE,BAS_SNDINIT
; HuBASIC 起動直後と同じ状態にする: WIDTH 40 (CRTC R0-R9 は _C4025L と同じ、
; R10/R11 は HuBASIC の値 $60/$07)、パレット AA CC F0、プライオリティ 0、
; COLOR 7,0、REPEAT ON、制御スタックと一時領域を空に、CLS 4。
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
LD A, 1
LD (BAS_KREP), A    ; REPEAT ON
CALL BAS_INITSTATE  ; プライオリティ 0 もここで書く
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
; @calls BAS_WORK,BAS_PALRST,BAS_WINRST
; INIT 文と BAS_INIT で共通の初期値: 色 7、装飾なし、プライオリティ 0、パレット既定 (INIT 文もプライオリティを戻す。HuBASIC の実測)、
; テキストの窓は全画面 (INIT 文も CONSOLE を戻す。カーソルは動かさない。HuBASIC の実測)
CALL BAS_WINRST
LD A, $07
LD (BAS_ATTR), A
XOR A
LD (BAS_CSZ), A
LD (BAS_RAW), A
LD BC, $1300
OUT (C), A          ; プライオリティ 0
JP BAS_PALRST


; @name BAS_PALRST
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_SETPAL
; PALET (引数なし): パレットの影を既定 (B $AA、R $CC、G $F0) に戻してポートに書く
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
; HL = 実行中の行番号を持つ変数のアドレス (hubasic2sl の出力が各行の頭で行番号を書く)
LD (BAS_LNPTR), HL
RET


; @name BAS_ERROR
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_PNL,BAS_PSTRZ,BAS_UTOA,BAS_END
; HL = エラー番号。HuBASIC と同じく、改行してから「<メッセージ> in <行番号>」、次の行に「Ok」を出して止まる
; (メッセージの綴りは HuBASIC の実測。表にない番号は「Error <番号>」、行番号が分からなければ in を出さない)
PUSH HL
XOR A
LD (BAS_RAW), A
LD (BAS_CSZ), A
LD A, (BAS_WX0)     ; カーソルが窓の左端でなければ改行
LD B, A
LD A, (BAS_CX)
CP B
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
; @calls BAS_WORK,BAS_WBLANK
; テキストの窓 (CONSOLE。ふだんは全画面) を空白で埋め、属性を「$40 + 現在の属性」で埋める (HuBASIC の実測)。カーソルを窓の左上へ
LD A, (BAS_WY0)
.bct_loop
PUSH AF
LD H, A
CALL BAS_WBLANK
POP AF
LD HL, BAS_WY1
CP (HL)
INC A               ; キャリーは変えない
JP C, .bct_loop     ; 窓の下の行まで
LD HL, (BAS_WX0)
LD (BAS_CXY), HL
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
; @calls BAS_WORK,BAS_WSCRL
; テキストの窓 (ふだんは全画面) を 1 行上にずらし、窓の下の行を空白 (属性 $40 + 現在の属性) で埋める
LD A, (BAS_WY0)
LD D, A
LD A, (BAS_WY1)
LD E, A
XOR A
JP BAS_WSCRL


; @name BAS_WSCRL
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_VOFF,BAS_WBLANK
; D = 上の行、E = 下の行 (D ≦ E)、A = 0 なら 1 行上へ・1 なら 1 行下へ。窓の桁の中だけで行 D〜E をずらし、空いた行を空白にする
; (ずらすのはテキストと属性。漢字の面は触らない。空白にする行は BAS_WBLANK)。スクロールと CHR$(14)・CHR$(15) が使う
OR A
JP NZ, .bws_down
.bws_up
LD A, D
CP E
JP NC, .bws_bl_e
LD H, D
INC H               ; 元 = D + 1
LD L, D             ; 先 = D
CALL .bws_copy
INC D
JP .bws_up
.bws_bl_e
LD H, E
JP BAS_WBLANK
.bws_down
LD A, E
CP D
JP Z, .bws_bl_d
JP C, .bws_bl_d
LD H, E
DEC H               ; 元 = E - 1
LD L, E             ; 先 = E
CALL .bws_copy
DEC E
JP .bws_down
.bws_bl_d
LD H, D
JP BAS_WBLANK
.bws_copy
; H = 元の行、L = 先の行 → 窓の桁の分だけ、テキストと属性を写す (DE は保つ)
PUSH DE
PUSH HL
LD A, (BAS_WX0)
LD L, A
CALL BAS_VOFF       ; HL = 元の位置
EX DE, HL           ; DE = 元の位置
POP HL
PUSH HL
LD H, L
LD A, (BAS_WX0)
LD L, A
CALL BAS_VOFF       ; HL = 先の位置
LD A, (BAS_WX0)
LD B, A
LD A, (BAS_WX1)
SUB B
INC A               ; A = 窓の桁の数
.bwc_loop
PUSH AF
LD A, D
OR $30
LD B, A
LD C, E
IN A, (C)           ; テキスト
EX AF, AF'
RES 4, B
IN A, (C)           ; 属性
PUSH AF
LD A, H
OR $30
LD B, A
LD C, L
EX AF, AF'          ; A = テキスト
OUT (C), A
RES 4, B
POP AF              ; A = 属性
OUT (C), A
INC DE
INC HL
POP AF
DEC A
JP NZ, .bwc_loop
POP HL
POP DE
RET


; @name BAS_WBLANK
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_VOFF,BAS_VPUT
; H = 行 → 窓の桁 (BAS_WX0〜BAS_WX1) のその行を空白 (文字 $20・属性 $40 + 現在の属性・漢字 0) にする。A・BC・DE・HL を壊す
LD A, (BAS_WX0)
LD L, A
CALL BAS_VOFF       ; HL = 位置
LD A, (BAS_WX0)
LD B, A
LD A, (BAS_WX1)
SUB B
INC A
LD B, A             ; B = 窓の桁の数
LD A, (BAS_ATTR)
OR $40
LD D, A
LD E, $20
.bwb_loop
PUSH BC
PUSH HL
CALL BAS_VPUT
POP HL
INC HL
POP BC
DJNZ .bwb_loop
RET


; @name BAS_NEWLINE
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_SCROLL
; カーソルを次の行の窓の左の端へ (窓の下の行ならスクロールして、下の行のまま)
LD A, (BAS_WX0)
LD (BAS_CX), A
LD A, (BAS_WY1)
LD B, A
LD A, (BAS_CY)
CP B
JP NC, BAS_SCROLL
INC A
LD (BAS_CY), A
RET


; @name BAS_PCHR
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_VOFF,BAS_VPUT,BAS_NEWLINE,BAS_CLSTEXT,BAS_SCROLL,BAS_WSCRL
; L = 文字コード。生出力 (PRINT#0) でなければ制御コード ($0D 改行、$0B HOME、$0C CLS、$0E 上へ詰める、$0F 下へ空ける、
; $1C-$1F カーソルの右・左・上・下) を解釈し、ほかの $00-$1F は出さない。生出力では CSIZE の拡大で書く。
; カーソルの動き・折り返し・スクロールはテキストの窓 (CONSOLE。ふだんは全画面) の中で行う
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
CP $0E
JP Z, .bp_rup
CP $0F
JP Z, .bp_rdn
CP $20
JP NC, .bp_char
CP $1C
RET C
CP $1D
JP Z, .bp_left
CP $1E
JP Z, .bp_up
CP $1F
JP Z, .bp_down
; 右: 窓の右端なら次の行の窓の左端 (窓の下の行ならスクロール。改行と同じ)
LD A, (BAS_CX)
LD HL, BAS_WX1
CP (HL)
JP NC, BAS_NEWLINE
INC A
LD (BAS_CX), A
RET
.bp_left
; 左: 窓の左端なら前の行の窓の右端 (窓のいちばん上の行なら同じ行の窓の右端。HuBASIC の実測)
LD A, (BAS_CX)
LD HL, BAS_WX0
CP (HL)
JP Z, .bp_lwrap
DEC A
LD (BAS_CX), A
RET
.bp_lwrap
LD A, (BAS_WX1)
LD (BAS_CX), A
.bp_up
; 上: 窓のいちばん上の行なら動かない
LD A, (BAS_CY)
LD HL, BAS_WY0
CP (HL)
RET Z
DEC A
LD (BAS_CY), A
RET
.bp_down
; 下: 窓の下の行ならスクロールして下の行のまま (桁はそのまま)
LD A, (BAS_CY)
LD HL, BAS_WY1
CP (HL)
JP NC, BAS_SCROLL
INC A
LD (BAS_CY), A
RET
.bp_rup
; $0E: 窓の上の行からカーソルの行までを 1 行上にずらし、カーソルの行を空白にする (カーソルは動かさない。HuBASIC の実測)
LD A, (BAS_WY0)
LD D, A
LD A, (BAS_CY)
LD E, A
XOR A
JP BAS_WSCRL
.bp_rdn
; $0F: カーソルの行から窓の下の行までを 1 行下にずらし、カーソルの行を空白にする
LD A, (BAS_CY)
LD D, A
LD A, (BAS_WY1)
LD E, A
LD A, 1
JP BAS_WSCRL
.bp_char
LD E, A             ; E = 文字
LD A, (BAS_RAW)
OR A
LD A, (BAS_CSZ)
JP NZ, .bp_sz
XOR A               ; 生出力でなければ拡大しない
.bp_sz
LD C, A             ; C = 拡大 (0 なし / 1 縦 / 2 横 / 3 縦横)
LD A, (BAS_ATTR)
BIT 0, C
JP Z, .bp_nv
OR $40
.bp_nv
BIT 1, C
JP Z, .bp_nh
OR $80
.bp_nh
LD D, A             ; D = 属性 (拡大なら HS/VS 付き)
CALL .bp_one
BIT 1, C
RET Z
; 横 2 倍は右半分をもう 1 マス (左半分で右端を越えていれば、次の行の左端に書く。HuBASIC と同じ)
.bp_one
PUSH BC
PUSH DE
LD HL, (BAS_CX)     ; L = X, H = Y
CALL .bp_cell
POP DE
POP BC
BIT 0, C
JP Z, .bp_adv
LD HL, (BAS_CX)     ; 縦 2 倍は 1 行下にも (窓の下の行なら書かない)
INC H
LD A, (BAS_WY1)
CP H
JP C, .bp_adv
PUSH BC
PUSH DE
CALL .bp_cell
POP DE
POP BC
.bp_adv
; カーソルを進める。窓の右端を越えたら、その場で次の行の窓の左端へ (縦 2 倍なら 2 行下)。
; 窓の下の行を越えたらスクロールする (右下の隅に書いた直後にもスクロールする。HuBASIC と同じ)
LD A, (BAS_CX)
LD HL, BAS_WX1
CP (HL)
JP NC, .bp_wrap
INC A
LD (BAS_CX), A
RET
.bp_wrap
LD A, (BAS_WX0)
LD (BAS_CX), A
LD A, (BAS_CY)
INC A
BIT 0, C
JP Z, .bp_wy
INC A
.bp_wy
LD HL, BAS_WY1
CP (HL)
JP C, .bp_wset
JP Z, .bp_wset
PUSH BC
PUSH DE
PUSH AF
CALL BAS_SCROLL
POP AF
POP DE
POP BC
DEC A
JP .bp_wy
.bp_wset
LD (BAS_CY), A
RET
.bp_cell
PUSH DE
CALL BAS_VOFF
POP DE
PUSH DE
CALL BAS_VPUT
POP DE
RET
.bp_home
LD HL, (BAS_WX0)    ; 窓の左上
LD (BAS_CXY), HL
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
; @calls BAS_WORK,BAS_PCHR
; 次の 10 桁区切り (画面の絶対の桁) の位置まで空白。次の区切りが窓の右端を越えるなら、窓の右端まで空白を出して折り返し、
; 折り返した所 (窓の左端) が区切りでなければ、そこから次の区切りまで空白 (HuBASIC の実測。全画面では折り返した所が 0 なので、そこで終わり)
CALL .bpz_next
LD A, (BAS_WX1)
CP C
JP NC, .bpz_pad     ; 次の区切りが窓の中
.bpz_eol
LD A, (BAS_CX)
LD B, A             ; B = 空白を出す前の桁
PUSH BC
LD L, $20
CALL BAS_PCHR
POP BC
LD A, B
LD HL, BAS_CX
CP (HL)
JP C, .bpz_eol      ; 進んだ (まだ折り返していない)
CALL .bpz_next
LD A, C
SUB 10
LD HL, BAS_CX
CP (HL)
RET Z               ; 改行した所が区切り
LD A, (BAS_WX1)
CP C
RET C               ; 次の区切りも窓の右端を越える
.bpz_pad
LD A, (BAS_CX)
CP C
RET NC
LD B, A             ; B = 空白を出す前の桁
PUSH BC
LD L, $20
CALL BAS_PCHR
POP BC
LD A, B
LD HL, BAS_CX
CP (HL)
RET NC              ; 進まなかった (出す前の桁 ≧ いまの桁。折り返した。CSIZE 2 の生出力で区切りを飛び越えて止まらなくなるのを防ぐ)
JP .bpz_pad
.bpz_next
; C = いまの桁より後の、次の区切りの桁
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
RET


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
; HL = X, DE = Y (画面の絶対の座標。テキストの窓 (CONSOLE。ふだんは全画面) の外はエラー 5。HuBASIC の実測)
LD A, H
OR D
JP NZ, .bl_err
LD B, L
LD C, E
LD HL, BAS_WX0
LD A, B
CP (HL)
JP C, .bl_err       ; 窓の左より左
INC HL              ; BAS_WY0
LD A, C
CP (HL)
JP C, .bl_err       ; 窓の上より上
INC HL              ; BAS_WX1
LD A, (HL)
CP B
JP C, .bl_err       ; 窓の右より右
INC HL              ; BAS_WY1
LD A, (HL)
CP C
JP C, .bl_err       ; 窓の下より下
LD L, B
LD H, C
LD (BAS_CXY), HL
RET
.bl_err
LD HL, 5
JP BAS_ERROR


; @name BAS_COLOR
; @resident shared
; @param_count 2
; @calls BAS_WORK,BAS_SETPAL
; HL = 前景色 (-1 = 変えない)、DE = 背景色 (-1 = 変えない)。
; 背景色はパレットの色番号 0 を置き換える (HuBASIC の実測)
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
; @calls BAS_WORK,sWORK,INIT_CRTC,_C4025L,_C4025H,_C8025L,_C8025H,BAS_CLS,BAS_WINRST
; HL = 桁数 (40 以下 → 40、それより大きい → 80)。テキストの窓を全画面に戻し (HuBASIC の実測)、CLS 4 と同じ消去をする
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
CALL BAS_WINRST
LD HL, 4
JP BAS_CLS


; @name BAS_WINRST
; @resident shared
; @param_count 0
; @calls BAS_WORK
; テキストの窓を全画面 (0, 0)-(桁数-1, 24) に戻す (起動・INIT・WIDTH・引数のない CONSOLE)。カーソルは動かさない
LD HL, 0
LD (BAS_WX0), HL
LD A, (BAS_WID)
DEC A
LD L, A
LD H, 24
LD (BAS_WX1), HL
RET


; @name BAS_CONSOLE0
; @resident shared
; @param_count 0
; @calls BAS_WINRST
; CONSOLE (引数なし): テキストの窓を全画面に戻す。画面は消さず、カーソルも動かさない (HuBASIC の実測)
JP BAS_WINRST


; @name BAS_CONSOLEY
; @resident shared
; @param_count 2
; @calls BAS_WORK,BAS_CONSOLE
; HL = ys, DE = yl → CONSOLE ys,yl,0,桁数 と同じ
POP BC              ; 戻り先
PUSH HL             ; ys
PUSH DE             ; yl
LD HL, 0
PUSH HL             ; xs
LD A, (BAS_WID)
LD L, A
PUSH HL             ; xl
PUSH BC
JP BAS_CONSOLE


; @name BAS_CONSOLE
; @resident shared
; @calls BAS_WORK,BAS_ERROR
; @stack_cleanup callee
; スタック引数 (ys, yl, xs, xl)。テキストの窓を行 ys から yl 行・桁 xs から xl 桁にし、カーソルを (xs, ys) へ。
; ys 0〜24、yl 1〜25-ys、xs 0〜桁数-1、xl 1〜桁数-xs を外れたらエラー 5 (Illegal function call。HuBASIC の実測)
POP HL
LD (.bco_ret), HL
POP HL
LD (.bco_xl), HL
POP HL
LD (.bco_xs), HL
POP DE              ; DE = yl
POP HL              ; HL = ys
LD BC, (.bco_ret)
PUSH BC
LD A, H
OR D
JP NZ, .bco_err
LD A, L
CP 25
JP NC, .bco_err
LD B, L             ; B = ys
LD A, E
OR A
JP Z, .bco_err
LD A, 25
SUB B
CP E
JP C, .bco_err      ; yl > 25 - ys
LD A, B
ADD A, E
DEC A
LD C, A             ; C = 下の行
LD HL, (.bco_xs)
LD A, H
OR A
JP NZ, .bco_err
LD A, (BAS_WID)
DEC A
CP L
JP C, .bco_err      ; xs > 桁数 - 1
LD D, L             ; D = xs
LD HL, (.bco_xl)
LD A, H
OR A
JP NZ, .bco_err
LD A, L
OR A
JP Z, .bco_err
LD A, (BAS_WID)
SUB D
CP L
JP C, .bco_err      ; xl > 桁数 - xs
LD A, D
ADD A, L
DEC A
LD (BAS_WX1), A
LD A, C
LD (BAS_WY1), A
LD L, D
LD H, B
LD (BAS_WX0), HL    ; 左と上
LD (BAS_CXY), HL    ; カーソルを窓の左上へ
RET
.bco_err
LD HL, 5
JP BAS_ERROR
.bco_ret DW 0
.bco_xs DW 0
.bco_xl DW 0


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
; 和が 16 ビットの符号付きの範囲を出たらエラー 6 (Overflow)。HuBASIC は NEXT での
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
; 状態が 0 なら上限 10 で確定 (状態 1。ただし初めに入れてある上限 = 置き場の上限が 10 より小さければそのまま)。
; 添字が 0〜上限の外ならエラー 9
LD A, (DE)
OR A
JP NZ, .ba_check
LD A, 1
LD (DE), A
INC BC
LD A, (BC)
DEC BC
OR A
JP NZ, .ba_ten      ; 置き場の上限が 256 以上
LD A, (BC)
CP 10
JP C, .ba_check     ; 置き場の上限が 10 より小さい (DIM の上限) ならそのまま
.ba_ten
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


; @name BAS_VSYNCS
; @resident shared
; @param_count 1
; @calls BAS_WAITF
; VSYNC 文 (HuBASIC にない命令): HL フレーム待つ。0 と負は待たない
BIT 7, H
RET NZ
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
; HL = 代入先の変数のバッファ (256 バイト。--string-len N なら N+1 バイト), DE = 文字列 → 長さ+1 バイトを写す
EX DE, HL           ; HL = 元, DE = 先
LD A, (HL)
LD C, A
LD B, 0
INC BC
LDIR
RET


; @name BAS_SSETL
; @resident shared
; @param_count 3
; @calls BAS_SSET,BAS_ERROR
; HL = 代入先の変数のバッファ (N+1 バイト), DE = 文字列, BC = 長さの上限 N (--string-len、0〜254)
; → 長さが N を超えたらエラー 15 (String too long)、でなければ BAS_SSET と同じく写す
LD A, C
EX DE, HL
CP (HL)             ; N - 長さ
EX DE, HL
JP NC, BAS_SSET
LD HL, 15
JP BAS_ERROR


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
; HL = s → 先頭の文字コード (空文字列は 0。HuBASIC の実測)
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
; HL = s (16 進の文字列) → 2 桁を 1 バイトにした文字列。本物と同じく、スペースは区切りとして飛ばし、
; スペースか終わりの前の 1 桁は 1 バイトにする。空の文字列・16 進でもスペースでもない文字はエラー 5
LD A, (HL)
OR A
JP Z, .bsh_err
LD C, A             ; C = 入力の残りの文字数
SRL A
ADC A, 0            ; 結果は多くて (長さ + 1) / 2 バイト (255 → 128)
PUSH HL
CALL BAS_TALLOC     ; HL = 結果
POP DE              ; DE = s
PUSH HL
INC HL
INC DE
LD B, 0             ; B = 結果の長さ
.bsh_loop
LD A, (DE)
INC DE
DEC C
CP ' '
JP Z, .bsh_next
CALL BAS_HEXDIGIT
JP C, .bsh_err_pop
LD (HL), A          ; 1 桁目 (1 桁で終わればこのまま)
LD A, C
OR A
JP Z, .bsh_put      ; 残りがない
LD A, (DE)
CP ' '
JP Z, .bsh_put      ; 次がスペース
CALL BAS_HEXDIGIT
JP C, .bsh_err_pop
INC DE
DEC C
PUSH BC
LD B, A             ; B = 2 桁目
LD A, (HL)
ADD A, A
ADD A, A
ADD A, A
ADD A, A
OR B
POP BC
LD (HL), A
.bsh_put
INC HL
INC B
.bsh_next
LD A, C
OR A
JP NZ, .bsh_loop
POP HL
LD (HL), B
RET
.bsh_err_pop
POP HL
.bsh_err
LD HL, 5
JP BAS_ERROR


; @name BAS_SMIRROR
; @resident shared
; @param_count 1
; @calls BAS_TALLOC,BAS_SMIRRORV
; HL = s (定数・式の値) → 一時領域に写してから、各バイトのビットを左右逆にした文字列 (MIRROR$。s は変えない)
LD A, (HL)
PUSH HL
CALL BAS_TALLOC     ; HL = 結果 (長さは書き込み済み)
POP DE              ; DE = s
PUSH HL
EX DE, HL           ; HL = s, DE = 結果
LD C, (HL)
LD B, 0
INC BC
LDIR
POP HL
JP BAS_SMIRRORV


; @name BAS_SMIRRORV
; @resident shared
; @param_count 1
; HL = 変数・配列の要素の中身 → その場で各バイトのビットを左右逆にし、同じ HL を返す
; (本物の MIRROR$ は、引数が変数なら変数も書き換え、結果もその中身になる。&H83 → &HC1)
PUSH HL
LD B, (HL)          ; B = 残りのバイト数
INC HL
LD A, B
OR A
JP Z, .bmv_end
.bmv_loop
LD C, (HL)
PUSH BC
LD B, 8
.bmv_bit
RR C                ; 下の桁から順にキャリーへ
RLA                 ; キャリーを下から入れる (8 回で元の A は全部押し出される)
DJNZ .bmv_bit
POP BC
LD (HL), A
INC HL
DJNZ .bmv_loop
.bmv_end
POP HL
RET


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


; @name BAS_KEYIN
; @resident shared
; @param_count 0
; @calls BAS_WORK,sGETKY
; A = キーの文字 (なければ 0。一時文字列を作らない)。HuBASIC と同じく押しっぱなしでは自動でリピートする:
; 押した瞬間に 1 回、約 30 フレーム後から約 3.8 フレームごと (4,4,4,4,4,3 の繰り返し、実測に合わせた値)。
; REPEAT OFF (BAS_KREP が 0) なら押した瞬間の 1 回だけ。
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
; 押しっぱなし: REPEAT OFF なら返さない。ON なら次のリピートのフレームに達したか
LD A, (BAS_KREP)
OR A
JP Z, .bik_none
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
LD A, E
RET
.bik_none
XOR A
RET
.bik_vprev DB 0
.bik_frames DW 0
.bik_next DW 0
.bik_phase DB 0


; @name BAS_SINKEY
; @resident shared
; @param_count 0
; @calls BAS_KEYIN,BAS_TALLOC
; INKEY$: キーの文字 (なければ空文字列)。押した瞬間とリピートの扱いは BAS_KEYIN
CALL BAS_KEYIN
LD E, A
OR A
JP Z, BAS_TALLOC        ; 長さ 0 の文字列
LD A, 1
CALL BAS_TALLOC
INC HL
LD (HL), E
DEC HL
RET


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


;; PRINT USING: 書式を読む所 (BAS_PUPARSE) と数字を欄に並べる所 (BAS_PULAY) を、整数 (BAS_PUSING) と
;; 浮動小数 (BAS_PUSINGF) で共通にする

; @name BAS_PUSING
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_PUPARSE,BAS_PULAY,BAS_UTOA,BAS_PNUM
; HL = 整数の値。書式バッファの「前置きの文字 + 数値の欄 + 後ろの文字」で出す (小数部は 0 を並べる)。
; 数値の欄がない書式は、前置きの文字の後に値をふつうに出す
LD (.bpu_val), HL
CALL BAS_PUPARSE
OR A
JP NZ, .bpu_field
LD HL, (.bpu_val)
JP BAS_PNUM
.bpu_field
LD HL, (.bpu_val)
XOR A
BIT 7, H
JP Z, .bpu_pos
LD A, L
CPL
LD L, A
LD A, H
CPL
LD H, A
INC HL
LD A, 1
.bpu_pos
LD (BAS_PUNEG), A
CALL BAS_UTOA       ; HL = 絶対値の数字列 (0 終端)
LD DE, 0            ; 小数部は 0 を並べる
JP BAS_PULAY
.bpu_val DW 0


; @name BAS_PUPARSE
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_PCHR
; 書式バッファの前置きの文字 (数値の欄が始まるまで) を出し、数値の欄を読んで BAS_PU* に置く。A = 欄があれば 1、
; なければ 0 (前置きの文字はすべて出した)。数値の欄: 先頭の '+' (常に符号)、'**' (前を * で埋める、2 桁に数える)、
; '#' と欄の中の ',' (1 桁に数え、整数部を 3 桁ごとに区切る)、'.'+'#' (小数部の桁)、末尾の '-' (負なら後ろに -)。
; '#'・'+'・'**'・'.#' のどれかで欄が始まる (欄より前の ',' や、後ろが '#' でない '.' はただの文字)
LD HL, BAS_UFMT
LD B, (HL)          ; B = 書式の残り
INC HL
.bpp_pre
LD A, B
OR A
RET Z
LD A, (HL)
CP '#'
JP Z, .bpp_field
CP '+'
JP Z, .bpp_field
CP '*'
JP Z, .bpp_twoq
CP '.'
JP NZ, .bpp_prechar
LD C, '#'           ; '.' は後ろが '#' なら欄の始まり
JP .bpp_next
.bpp_twoq
LD C, '*'           ; '*' は後ろも '*' なら欄の始まり
.bpp_next
LD A, B
CP 2
JP C, .bpp_one
INC HL
LD A, (HL)
DEC HL
CP C
JP Z, .bpp_field
.bpp_one
LD A, (HL)
.bpp_prechar
PUSH HL
PUSH BC
LD L, A
CALL BAS_PCHR
POP BC
POP HL
INC HL
DEC B
JP .bpp_pre
.bpp_field
XOR A
LD (BAS_PUWID), A
LD (BAS_PUPLUS), A
LD (BAS_PUSTAR), A
LD (BAS_PUMINUS), A
LD (BAS_PUFRAC), A
LD (BAS_PUCOMMA), A
LD A, (HL)
CP '+'
JP NZ, .bpp_nolead
LD A, 1
LD (BAS_PUPLUS), A
LD (BAS_PUWID), A
INC HL
DEC B
.bpp_nolead
LD A, B
OR A
JP Z, .bpp_end
LD A, (HL)
CP '*'
JP NZ, .bpp_hash
INC HL
DEC B
LD A, B
OR A
JP Z, .bpp_end
LD A, (HL)
CP '*'
JP NZ, .bpp_hash
LD A, 1
LD (BAS_PUSTAR), A
LD A, (BAS_PUWID)
ADD A, 2
LD (BAS_PUWID), A
INC HL
DEC B
.bpp_hash
LD A, B
OR A
JP Z, .bpp_end
LD A, (HL)
CP '#'
JP Z, .bpp_h1
CP ','
JP NZ, .bpp_dot
LD (BAS_PUCOMMA), A
.bpp_h1
LD A, (BAS_PUWID)
INC A
LD (BAS_PUWID), A
INC HL
DEC B
JP .bpp_hash
.bpp_dot
CP '.'
JP NZ, .bpp_trail
INC HL
DEC B
.bpp_fr
LD A, B
OR A
JP Z, .bpp_end
LD A, (HL)
CP '#'
JP NZ, .bpp_trail
LD A, (BAS_PUFRAC)
INC A
LD (BAS_PUFRAC), A
INC HL
DEC B
JP .bpp_fr
.bpp_trail
LD A, B
OR A
JP Z, .bpp_end
LD A, (HL)
CP '-'
JP NZ, .bpp_end
LD A, 1
LD (BAS_PUMINUS), A
INC HL
DEC B
.bpp_end
LD (BAS_PUREST), HL
LD A, B
LD (BAS_PURESTN), A
LD A, 1
RET


; @name BAS_PULAY
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_PCHR,BAS_ERROR
; BAS_PUPARSE で読んだ数値の欄に数を並べて出し、書式の後ろの文字を出す。HL = 整数部の数字列 (0 終端、区切りなし)、
; DE = 小数部の数字列 (0 終端。小数部の桁より短ければ後ろを 0 で埋める。DE = 0 なら全部 0)、BAS_PUNEG = 負なら 1。
; 符号の文字 (先頭の '+'、または負で末尾の '-' がないときの '-') は数字の直前に置き、前を空白 ('**' なら *) で埋める。
; 整数部が 0 で小数部があるとき、符号の文字が欄の中にあるか、整数部の桁がなければ、頭の 0 を出さない
; (HuBASIC の実測: ###.## の -.5 は "  -.50"、+#.## の .5 は " +.50"、#.## の .5 は "0.50"、.## の .5 は ".50")。
; 欄に入らなければエラー 36 (Format over。HuBASIC の実測)
LD (.bpl_frac), DE
LD C, 0             ; C = 符号の文字が欄の中に要るか
LD A, (BAS_PUPLUS)
OR A
JP NZ, .bpl_signin
LD A, (BAS_PUNEG)
OR A
JP Z, .bpl_sgok
LD A, (BAS_PUMINUS)
OR A
JP NZ, .bpl_sgok
.bpl_signin
LD C, 1
.bpl_sgok
LD A, (BAS_PUPLUS)
LD B, A
LD A, (BAS_PUWID)
SUB B
LD E, A             ; E = 数字の欄の桁 ('+' の分を除く)
; 整数部が "0" で小数部があるときの頭の 0
LD A, (HL)
CP '0'
JP NZ, .bpl_grp
INC HL
LD A, (HL)
DEC HL
OR A
JP NZ, .bpl_grp
LD A, (BAS_PUFRAC)
OR A
JP Z, .bpl_grp
LD A, C
OR A
JP NZ, .bpl_nozero
LD A, E
OR A
JP NZ, .bpl_grp
.bpl_nozero
INC HL              ; 空の数字列 (0 終端を指す)
.bpl_grp
LD (.bpl_digits), HL
LD A, (BAS_PUCOMMA)
OR A
JP Z, .bpl_ngrp
; 3 桁ごとに ',' を入れた数字列を .bpl_gbuf に作る (最初の組は 長さ mod 3、0 なら 3 桁)
PUSH DE
PUSH BC
PUSH HL
LD B, 0
.bpl_glen
LD A, (HL)
OR A
JP Z, .bpl_glok
INC B
INC HL
JP .bpl_glen
.bpl_glok
POP HL
LD A, B
OR A
JP Z, .bpl_gnone    ; 数字がない
.bpl_gmod
CP 4
JP C, .bpl_gmok
SUB 3
JP .bpl_gmod
.bpl_gmok
LD C, A             ; C = いまの組の残りの桁
LD DE, .bpl_gbuf
.bpl_gl
LD A, (HL)
LD (DE), A
INC HL
INC DE
DEC B
JP Z, .bpl_gend
DEC C
JP NZ, .bpl_gl
LD A, ','
LD (DE), A
INC DE
LD C, 3
JP .bpl_gl
.bpl_gend
XOR A
LD (DE), A
LD HL, .bpl_gbuf
LD (.bpl_digits), HL
.bpl_gnone
POP BC
POP DE
.bpl_ngrp
LD HL, (.bpl_digits)
LD B, 0
.bpl_len
LD A, (HL)
OR A
JP Z, .bpl_lenok
INC B
INC HL
JP .bpl_len
.bpl_lenok
; 必要な桁 D = 数字 + 符号 ('+' の 1 桁は欄の桁から除いてある)
LD A, (BAS_PUPLUS)
OR A
LD A, B
JP NZ, .bpl_need
ADD A, C
.bpl_need
LD D, A
LD A, E
CP D
JP NC, .bpl_fit
LD HL, 36
JP BAS_ERROR
.bpl_fit
LD A, E             ; 詰め文字 (E - D 個)
SUB D
LD B, A
.bpl_pad
LD A, B
OR A
JP Z, .bpl_sign
PUSH BC
LD A, (BAS_PUSTAR)
OR A
LD L, ' '
JP Z, .bpl_padc
LD L, '*'
.bpl_padc
CALL BAS_PCHR
POP BC
DEC B
JP .bpl_pad
.bpl_sign
LD A, C
OR A
JP Z, .bpl_dig
LD A, (BAS_PUNEG)
OR A
LD L, '+'
JP Z, .bpl_sgn
LD L, '-'
.bpl_sgn
CALL BAS_PCHR
.bpl_dig
LD HL, (.bpl_digits)
.bpl_dl
LD A, (HL)
OR A
JP Z, .bpl_frac0
PUSH HL
LD L, A
CALL BAS_PCHR
POP HL
INC HL
JP .bpl_dl
.bpl_frac0
LD A, (BAS_PUFRAC)
OR A
JP Z, .bpl_tm
LD B, A
PUSH BC
LD L, '.'
CALL BAS_PCHR
POP BC
LD HL, (.bpl_frac)
.bpl_fl
LD A, H
OR L
JP Z, .bpl_f0       ; 小数部の数字列がない、または終わった: 0
LD A, (HL)
OR A
JP Z, .bpl_fz
INC HL
JP .bpl_fput
.bpl_fz
LD HL, 0
.bpl_f0
LD A, '0'
.bpl_fput
PUSH HL
PUSH BC
LD L, A
CALL BAS_PCHR
POP BC
POP HL
DJNZ .bpl_fl
.bpl_tm
LD A, (BAS_PUMINUS)
OR A
JP Z, .bpl_post
LD A, (BAS_PUNEG)
OR A
LD L, ' '
JP Z, .bpl_tmc
LD L, '-'
.bpl_tmc
CALL BAS_PCHR
.bpl_post
LD HL, (BAS_PUREST) ; 書式の後ろの文字
LD A, (BAS_PURESTN)
LD B, A
.bpl_pl
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
JP .bpl_pl
.bpl_frac DW 0
.bpl_digits DW 0
.bpl_gbuf DB 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0


; @name BAS_PUSINGF
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_PUPARSE,BAS_PULAY,BAS_PFLT,BAS_ERROR
; AHL = 浮動小数。PRINT USING の書式で出す (BAS_PUSING の浮動小数版。数値の欄がない書式は値をふつうに出す)。
; 数字は f24 の値を正確な 10 進にして、小数部の桁で四捨五入したもの: |x| × 10^d (d は小数部の桁、9 まで。その下は 0) を
; 64 ビットの整数で正確に計算して丸める (f24 の値 = 頭の 1 を付けた 17 ビットの仮数 × 2^(指数 - 79))。値が 2^17 以上
; (指数 - 79 が 0 以上) なら値は整数なので、10^d を掛けずに整数部だけを作る (小数部は BAS_PULAY が 0 で埋める。f24 の
; 値は 2^64 未満なので必ず収まる)。2^17 未満の値は × 10^9 でも 2^47 未満。符号は丸める前の値で決める (-.004 は "-.00")。
; 無限大・NaN はエラー 6
LD (.bpf_m), HL
LD (.bpf_a), A
CALL BAS_PUPARSE
OR A
JP NZ, .bpf_field
LD HL, (.bpf_m)
LD A, (.bpf_a)
JP BAS_PFLT
.bpf_field
LD A, (.bpf_a)
LD C, A
AND $7F
CP $7F
JP Z, .bpf_inf
LD B, A             ; B = 指数 (0 なら値は 0)
LD (.bpf_e), A
XOR A
BIT 7, C
JP Z, .bpf_sg
OR B                ; -0 は負でない
JP Z, .bpf_sg
LD A, 1
.bpf_sg
LD (BAS_PUNEG), A
LD HL, .bpf_q       ; q = 0
LD B, 8
XOR A
.bpf_clr
LD (HL), A
INC HL
DJNZ .bpf_clr
LD A, (BAS_PUFRAC)  ; d = 小数部の桁 (9 まで)
CP 10
JP C, .bpf_d
LD A, 9
.bpf_d
LD (.bpf_dd), A
LD A, (.bpf_e)
OR A
JP Z, .bpf_digits   ; 値が 0
LD HL, (.bpf_m)
LD (.bpf_q), HL
LD A, 1
LD (.bpf_q + 2), A  ; q = 仮数 (17 ビット)
LD A, (.bpf_e)
CP 79
JP C, .bpf_frac
XOR A               ; 2 の指数が 0 以上: 値は整数。小数部の数字は作らない (BAS_PULAY が 0 で埋める)
LD (.bpf_dd), A
LD A, (.bpf_e)
SUB 79
JP Z, .bpf_digits
LD C, A             ; 左へ C ビット (47 まで。2^64 未満に収まる)
.bpf_shl
CALL .bpf_sl1
DEC C
JP NZ, .bpf_shl
JP .bpf_digits
.bpf_frac
LD A, (.bpf_dd)
OR A
JP Z, .bpf_scale
LD C, A
.bpf_m10            ; q × 10^d (2^47 未満なのであふれない)
PUSH BC
CALL .bpf_x10
POP BC
DEC C
JP NZ, .bpf_m10
.bpf_scale
LD A, (.bpf_e)
SUB 79
NEG                 ; 2 の指数が負: 右へ A ビット (1〜78)。最後に落ちるビットで四捨五入する
LD C, A
DEC C
JP Z, .bpf_rnd
.bpf_shr
CALL .bpf_sr1
DEC C
JP NZ, .bpf_shr
.bpf_rnd
CALL .bpf_sr1
JP NC, .bpf_digits
CALL .bpf_inc
.bpf_digits
; 10 で割り続けて、下の桁から .bpf_rev に並べる (桁の数が d + 1 になるまでは 0 を足す)
LD DE, .bpf_rev
LD C, 0
.bpf_dl
PUSH DE
PUSH BC
CALL .bpf_div10
POP BC
POP DE
ADD A, '0'
LD (DE), A
INC DE
INC C
CALL .bpf_isz
JP NZ, .bpf_dl
LD A, (.bpf_dd)
CP C
JP NC, .bpf_dl
LD A, C
LD (.bpf_n), A
; 小数部 = 下の d 桁を上の桁から .bpf_fbuf へ
LD HL, .bpf_rev
LD A, (.bpf_dd)
LD E, A
LD D, 0
ADD HL, DE          ; HL = 小数部の上の桁の 1 つ上 (.bpf_rev + d)
PUSH HL
LD DE, .bpf_fbuf
LD A, (.bpf_dd)
OR A
JP Z, .bpf_fend
LD B, A
.bpf_fc
DEC HL
LD A, (HL)
LD (DE), A
INC DE
DJNZ .bpf_fc
.bpf_fend
XOR A
LD (DE), A
; 整数部 = 残りの上の桁 (.bpf_rev + n - 1 から .bpf_rev + d まで) を .bpf_ibuf へ
POP DE              ; DE = .bpf_rev + d
LD HL, .bpf_rev
LD A, (.bpf_n)
LD C, A
LD B, 0
ADD HL, BC          ; HL = .bpf_rev + n
LD A, (.bpf_dd)
LD B, A
LD A, C
SUB B
LD B, A             ; B = 整数部の桁 (1 以上)
LD DE, .bpf_ibuf
.bpf_ic2
DEC HL
LD A, (HL)
LD (DE), A
INC DE
DJNZ .bpf_ic2
XOR A
LD (DE), A
LD HL, .bpf_ibuf
LD DE, .bpf_fbuf
JP BAS_PULAY
.bpf_inf
LD HL, 6
JP BAS_ERROR
; q × 10 (q × 4 + q を 2 倍する)
.bpf_x10
LD HL, .bpf_q
LD DE, .bpf_t
LD BC, 8
LDIR
CALL .bpf_sl1
CALL .bpf_sl1
LD HL, .bpf_q
LD DE, .bpf_t
LD B, 8
OR A
.bpf_add
LD A, (DE)
ADC A, (HL)
LD (HL), A
INC HL
INC DE
DJNZ .bpf_add
JP .bpf_sl1
; q を左へ 1 ビット (キャリー = 落ちたビット。BC と A は保つ)
.bpf_sl1
PUSH BC
LD HL, .bpf_q
LD B, 8
OR A
.bpf_sl
RL (HL)
INC HL
DJNZ .bpf_sl
POP BC
RET
; q を右へ 1 ビット (キャリー = 落ちたビット。BC と A は保つ)
.bpf_sr1
PUSH BC
LD HL, .bpf_q + 7
LD B, 8
OR A
.bpf_sr
RR (HL)
DEC HL
DJNZ .bpf_sr
POP BC
RET
; q + 1
.bpf_inc
LD HL, .bpf_q
LD B, 8
.bpf_inl
INC (HL)
RET NZ
INC HL
DJNZ .bpf_inl
RET
; q が 0 なら Z
.bpf_isz
LD HL, .bpf_q
LD B, 8
XOR A
.bpf_izl
OR (HL)
INC HL
DJNZ .bpf_izl
OR A
RET
; q を 10 で割る → A = 余り (C・HL を壊す)
.bpf_div10
XOR A
LD C, 64
.bpf_dv
CALL .bpf_sl1
RLA
CP 10
JP C, .bpf_dvn
SUB 10
LD HL, .bpf_q
INC (HL)
.bpf_dvn
DEC C
JP NZ, .bpf_dv
RET
.bpf_m DW 0
.bpf_a DB 0
.bpf_e DB 0
.bpf_dd DB 0
.bpf_n DB 0
.bpf_q DB 0, 0, 0, 0, 0, 0, 0, 0
.bpf_t DB 0, 0, 0, 0, 0, 0, 0, 0
.bpf_rev DB 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
.bpf_ibuf DB 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
.bpf_fbuf DB 0, 0, 0, 0, 0, 0, 0, 0, 0, 0


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


; @name BAS_KEY
; @resident shared
; @param_count 2
; @calls BAS_ERROR
; KEY n, 文字列: 何もしない (ファンクションキーの文字列は持たない)。n が 0〜10 の外ならエラー 5 (HuBASIC の実測)
LD A, H
OR A
JP NZ, .bky_err
LD A, L
CP 11
RET C
.bky_err
LD HL, 5
JP BAS_ERROR


; @name BAS_KREPEAT
; @resident shared
; @param_count 1
; @calls BAS_WORK
; HL = 1 なら REPEAT ON (押しっぱなしのキーを自動でリピートする)、0 なら REPEAT OFF
LD A, L
LD (BAS_KREP), A
RET


; @name BAS_PALET
; @resident shared
; @param_count 2
; @calls BAS_WORK,BAS_SETPAL,BAS_ERROR
; HL = 色のコード c、DE = パレットの色 p (どちらも 0〜7、範囲外はエラー 5)。パレットの影の B・R・G の
; c ビット目に p のビット 0・1・2 を入れて、ポートに書く
LD A, H
OR D
JP NZ, .bpl_err
LD A, L
CP 8
JP NC, .bpl_err
LD A, E
CP 8
JP NC, .bpl_err
LD A, 1
LD B, L
INC B
JP .bpl_m2
.bpl_m
RLCA
.bpl_m2
DJNZ .bpl_m
LD C, A             ; C = c ビット目だけ 1
LD HL, BAS_PALB
LD B, 3
.bpl_lp
LD A, C
CPL
AND (HL)
RR E                ; キャリー = p の次のビット
JP NC, .bpl_n
OR C
.bpl_n
LD (HL), A
INC HL
DJNZ .bpl_lp
JP BAS_SETPAL
.bpl_err
LD HL, 5
JP BAS_ERROR


; @name BAS_PRW
; @resident shared
; @param_count 1
; @calls BAS_ERROR
; HL = プライオリティ (0〜255、範囲外はエラー 5) をポート $1300 に書く
LD A, H
OR A
JP NZ, .bpw_err
LD A, L
LD BC, $1300
OUT (C), A
RET
.bpw_err
LD HL, 5
JP BAS_ERROR


; @name BAS_SWAP
; @resident shared
; @param_count 3
; HL = a の番地、DE = b の番地、BC = 大きさ (0 なら文字列: 2 つの長さの大きい方 + 1 バイト) → 中身を入れ替える
LD A, B
OR C
JP NZ, .bsw_lp
LD A, (DE)
CP (HL)
JP NC, .bsw_s
LD A, (HL)
.bsw_s
LD C, A
LD B, 0
INC BC
.bsw_lp
PUSH BC
LD C, (HL)
LD A, (DE)
LD (HL), A
LD A, C
LD (DE), A
INC HL
INC DE
POP BC
DEC BC
LD A, B
OR C
JP NZ, .bsw_lp
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
; スタック引数 (x1, y1, x2, y2, 文字列, 0 = 線 / 1 = 箱 / 2 = 塗りつぶした箱)。指定した文字を現在の属性で、
; 線 (水平・垂直・斜め)、箱 (外周)、塗りつぶした箱 (BF) に置く。画面の外のセルは書かない。カーソルは動かさない
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
CP 2
JP Z, .btl_fill
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
; 線: 主軸を 1 つずつ進める Bresenham。誤差は符号なし 16 ビットの借りで判定するので、座標がどの値でも
; 主軸の長さ + 1 回で必ず終わる。誤差の初期値は主軸の長さの半分の切り捨て (HuBASIC と同じ点を置く。実測)
.btl_line
LD HL, (.btl_x1)
LD DE, (.btl_x2)
CALL .btl_delta
LD (.btl_dx), HL
LD (.btl_sx), DE
LD HL, (.btl_y1)
LD DE, (.btl_y2)
CALL .btl_delta
LD (.btl_dy), HL
LD (.btl_sy), DE
; |dx| >= |dy| なら x が主軸
LD DE, (.btl_dx)
OR A
SBC HL, DE          ; |dy| - |dx| (符号なし)
JP C, .btl_xmaj
JP Z, .btl_xmaj
LD HL, .btl_y1
LD (.btl_pmaj), HL
LD HL, .btl_x1
LD (.btl_pmin), HL
LD HL, (.btl_sy)
LD (.btl_smaj), HL
LD HL, (.btl_sx)
LD (.btl_smin), HL
LD HL, (.btl_dy)
LD DE, (.btl_dx)
JP .btl_run
.btl_xmaj
LD HL, .btl_x1
LD (.btl_pmaj), HL
LD HL, .btl_y1
LD (.btl_pmin), HL
LD HL, (.btl_sx)
LD (.btl_smaj), HL
LD HL, (.btl_sy)
LD (.btl_smin), HL
LD HL, (.btl_dx)
LD DE, (.btl_dy)
.btl_run
; HL = 主軸の長さ, DE = 副軸の長さ
LD (.btl_cnt), HL
LD (.btl_dmaj), HL
LD (.btl_dmin), DE
SRL H
RR L
LD (.btl_err), HL
.btl_loop
LD HL, (.btl_x1)
LD DE, (.btl_y1)
CALL .btl_plot
LD HL, (.btl_cnt)
LD A, H
OR L
RET Z
DEC HL
LD (.btl_cnt), HL
; 主軸を進める
LD HL, (.btl_pmaj)
LD DE, (.btl_smaj)
CALL .btl_add
; err -= 副軸の長さ。借りが出たら err += 主軸の長さ (結果は 0 以上・主軸の長さ未満に戻る) で、副軸も進める
LD HL, (.btl_err)
LD DE, (.btl_dmin)
OR A
SBC HL, DE
JP NC, .btl_nomin
LD DE, (.btl_dmaj)
ADD HL, DE
LD (.btl_err), HL
LD HL, (.btl_pmin)
LD DE, (.btl_smin)
CALL .btl_add
JP .btl_loop
.btl_nomin
LD (.btl_err), HL
JP .btl_loop
; (HL) の 16 ビットの値に DE を足す
.btl_add
LD A, (HL)
ADD A, E
LD (HL), A
INC HL
LD A, (HL)
ADC A, D
LD (HL), A
RET
; HL = 始点, DE = 終点 (符号付き) → HL = |終点 - 始点| (0-65535), DE = 進む向き (1 / -1)
.btl_delta
PUSH HL
PUSH DE
CALL .btl_sgt       ; キャリー = 始点 > 終点
POP DE
POP HL
JP C, .btl_dneg
EX DE, HL
OR A
SBC HL, DE          ; 終点 - 始点
LD DE, 1
RET
.btl_dneg
OR A
SBC HL, DE          ; 始点 - 終点
LD DE, $FFFF
RET
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
.btl_fill
; 塗りつぶし: 先に画面の中 (左右は 0〜桁数-1、上下は 0〜24) に切り詰めてから、上から 1 行ずつ水平線を引く
LD A, (BAS_WID)
DEC A
LD L, A
LD H, 0
LD (.btl_fxm), HL       ; 右端の桁
LD HL, (.btl_x1)
LD DE, (.btl_x2)
CALL .btl_order
BIT 7, D
RET NZ                  ; 右端が負: 何も書かない
BIT 7, H
JP Z, .btl_fa
LD HL, 0
.btl_fa
LD (.btl_x1), HL
LD (.btl_x2), DE
EX DE, HL               ; HL = x2
LD DE, (.btl_fxm)
CALL .btl_sgt           ; キャリー = x2 > 右端の桁
JP NC, .btl_fb
LD HL, (.btl_fxm)
LD (.btl_x2), HL
.btl_fb
LD HL, (.btl_x1)
LD DE, (.btl_x2)
CALL .btl_sgt           ; 左端が右端を越えた (左端が画面の右の外): 何も書かない
RET C
LD HL, (.btl_y1)
LD DE, (.btl_y2)
CALL .btl_order
BIT 7, D
RET NZ                  ; 下端が負
BIT 7, H
JP Z, .btl_fc
LD HL, 0
.btl_fc
LD (.btl_hy), HL
LD (.btl_fy2), DE
EX DE, HL               ; HL = y2
LD DE, 24
CALL .btl_sgt
JP NC, .btl_fd
LD HL, 24
LD (.btl_fy2), HL
.btl_fd
LD HL, (.btl_hy)
LD DE, (.btl_fy2)
CALL .btl_sgt           ; 上端が 24 を越えた: 何も書かない
RET C
.btl_fl
CALL .btl_hline
LD HL, (.btl_hy)
LD DE, (.btl_fy2)
OR A
SBC HL, DE
RET Z
LD HL, (.btl_hy)
INC HL
LD (.btl_hy), HL
JP .btl_fl
.btl_fxm DW 0
.btl_fy2 DW 0
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
.btl_cnt DW 0
.btl_dmaj DW 0
.btl_dmin DW 0
.btl_pmaj DW 0
.btl_pmin DW 0
.btl_smaj DW 0
.btl_smin DW 0


; ============================================================ グラフィックの LINE

; @name BAS_GLINE
; @resident shared
; @calls BAS_WORK,BAS_ERROR,LINE
; @stack_cleanup callee
; スタック引数 (x1, y1, x2, y2, 色, 形)。形: bit 0 = 箱 (B)、bit 1 = PRESET、bit 2 = 色なし (今の前景色)、bit 3 = 塗りつぶし (BF。bit 0 も立つ)。
; 色が 0〜7 の外ならエラー 5。箱と BF は、画面にかかる部分を VRAM に直接塗る (.bgl_rect。箱は 4 辺を幅 1 の長方形にする)。
; PSET は色で上書き、PRESET は本物と同じく色のビットを落とす。箱でない線は、4 つの座標がすべて画面の中なら libx1_grp の LINE
; (X1GLINE) に描かせ、PRESET なら色 0 で描く (割り切り)。1 つでも画面の外なら .bgl_clip で、libx1_grp の LINE と同じ手順で
; 端から端まで点を求め、画面の中の点だけを描く (本物と同じ。PRESET は色のビットを落とす)
POP HL
LD (.bgl_ret), HL
POP HL
LD A, L
LD (.bgl_mode), A
POP HL
LD (.bgl_c), HL
POP HL
LD (.bgl_y2), HL
POP HL
LD (.bgl_x2), HL
POP HL
LD (.bgl_y1), HL
POP HL
LD (.bgl_x1), HL
LD HL, (.bgl_ret)
PUSH HL
; 色
LD A, (.bgl_mode)
BIT 2, A
JP Z, .bgl_chk
LD A, (BAS_ATTR)
AND 7
JP .bgl_cput
.bgl_chk
LD HL, (.bgl_c)
LD A, H
OR A
JP NZ, .bgl_err
LD A, L
CP 8
JP NC, .bgl_err
.bgl_cput
LD (.bgl_col), A
; 画面の幅 (WIDTH を変えた後でも合うように毎回切り替える)
LD A, (BAS_WID)
CP 80
JP Z, .bgl_w80
LD HL, 319
LD (.bgl_xmax), HL
CALL X1GLINE.SET320
JP .bgl_wok
.bgl_w80
LD HL, 639
LD (.bgl_xmax), HL
CALL X1GLINE.SET640
.bgl_wok
LD A, (.bgl_mode)
RRCA
JP C, .bgl_box
; 線: 4 つの座標がすべて画面の中なら libx1_grp の LINE (PRESET なら色 0)。1 つでも外なら .bgl_clip (元の色のまま)
LD BC, (.bgl_xmax)
LD HL, (.bgl_x1)
CALL .bgl_in
JP C, .bgl_clip
LD HL, (.bgl_x2)
CALL .bgl_in
JP C, .bgl_clip
LD BC, 199
LD HL, (.bgl_y1)
CALL .bgl_in
JP C, .bgl_clip
LD HL, (.bgl_y2)
CALL .bgl_in
JP C, .bgl_clip
LD (.bgl_t), HL
LD A, (.bgl_mode)
AND 2
JP Z, .bgl_lin
XOR A
LD (.bgl_col), A
.bgl_lin
LD HL, (.bgl_x2)
LD B, H
LD C, L
LD HL, (.bgl_x1)
LD DE, (.bgl_y1)
JP .bgl_draw
.bgl_err
LD HL, 5
JP BAS_ERROR
; 箱と BF: x・y の範囲を画面に収める (どちらかがかからなければ何も描かない)
.bgl_box
LD HL, (.bgl_x1)
LD DE, (.bgl_x2)
LD BC, (.bgl_xmax)
CALL .bgl_span
RET C
LD HL, (.bgl_lo)
LD (.bgl_lx), HL
LD HL, (.bgl_hi)
LD (.bgl_hx), HL
LD HL, (.bgl_y1)
LD DE, (.bgl_y2)
LD BC, 199
CALL .bgl_span
RET C
LD HL, (.bgl_lo)
LD (.bgl_ly), HL
LD HL, (.bgl_hi)
LD (.bgl_hy), HL
LD A, (.bgl_mode)
AND 8
JP Z, .bgl_edges
LD HL, (.bgl_lx)
LD (.bgl_rx1), HL
LD HL, (.bgl_hx)
LD (.bgl_rx2), HL
LD HL, (.bgl_ly)
LD (.bgl_ry1), HL
LD HL, (.bgl_hy)
LD (.bgl_ry2), HL
JP .bgl_rect
; 箱: 上・下の辺 (y が画面の中なら x の範囲の 1 行)、左・右の辺 (x が画面の中なら y の範囲の 1 列)
.bgl_edges
LD HL, (.bgl_lx)
LD (.bgl_rx1), HL
LD HL, (.bgl_hx)
LD (.bgl_rx2), HL
LD HL, (.bgl_y1)
CALL .bgl_hedge
LD HL, (.bgl_y2)
CALL .bgl_hedge
LD HL, (.bgl_ly)
LD (.bgl_ry1), HL
LD HL, (.bgl_hy)
LD (.bgl_ry2), HL
LD HL, (.bgl_x1)
CALL .bgl_vedge
LD HL, (.bgl_x2)
.bgl_vedge
LD BC, (.bgl_xmax)
CALL .bgl_in
RET C
LD (.bgl_rx1), HL
LD (.bgl_rx2), HL
JP .bgl_rect
.bgl_hedge
LD BC, 199
CALL .bgl_in
RET C
LD (.bgl_ry1), HL
LD (.bgl_ry2), HL
JP .bgl_rect
; HL = x1, DE = y1, BC = x2, (.bgl_t) = y2 の線を、色 (.bgl_col) で libx1_grp の LINE に描かせる
.bgl_draw
PUSH HL
PUSH DE
PUSH BC
LD HL, (.bgl_t)
PUSH HL
LD A, (.bgl_col)
LD L, A
LD H, 0
PUSH HL
CALL LINE
RET
; はみ出す線: 本物の HuBASIC と同じ手順で端から端まで点を求め、画面の中の点だけを .bgl_rect (1 × 1) で描く (本物で 39 本を
; 測って点まで一致を確かめた手順)。書いた順の 1 つ目の端から始める。XX = |x2 - x1|、YY = |y2 - y1|、長い方を major・
; 短い方を minor として、増分 = minor × 65536 ÷ major (同じなら 65535、両方 0 なら 0)。小数は 32768 (y が減る向きの線は
; 32768 + 128) から始め、点ごとに増分を足して、繰り上がったら短い方の軸を 1 進める。点は major + 1 個 (65536 個まで)。
; y は一方の向きにしか動かないので、画面から離れる向きに出たら打ち切る。
; .bgl_rect はレジスタを壊すので、線の状態はすべてメモリに置く
.bgl_clip
LD HL, (.bgl_y2)
LD DE, (.bgl_y1)
CALL .bcl_lt
LD A, 1
JP NC, .bcl_yd
LD A, $FF           ; 上へ
EX DE, HL
.bcl_yd
LD (.bcl_sy), A
OR A
SBC HL, DE
LD (.bcl_yy), HL    ; YY (0〜65535)
LD HL, (.bgl_x2)
LD DE, (.bgl_x1)
CALL .bcl_lt
LD A, 1
JP NC, .bcl_xr
LD A, $FF           ; 左へ
EX DE, HL
.bcl_xr
LD (.bcl_sx), A
OR A
SBC HL, DE
LD (.bcl_xx), HL    ; XX (0〜65535)
LD HL, (.bcl_yy)
LD DE, (.bcl_xx)
OR A
SBC HL, DE
JP NC, .bcl_tate
LD HL, (.bcl_yy)    ; 横長 (YY < XX)
LD (.bcl_mn), HL
LD HL, (.bcl_xx)
LD (.bcl_mj), HL
XOR A
JP .bcl_kind
.bcl_tate
LD HL, (.bcl_xx)    ; 縦長 (YY >= XX)
LD (.bcl_mn), HL
LD HL, (.bcl_yy)
LD (.bcl_mj), HL
LD A, 1
.bcl_kind
LD (.bcl_k), A
; 増分 DE = minor × 65536 ÷ major
LD HL, (.bcl_mn)
LD BC, (.bcl_mj)
LD DE, 0
LD A, B
OR C
JP Z, .bcl_dset     ; 両方 0 (1 点)
LD A, H
CP B
JP NZ, .bcl_div
LD A, L
CP C
JP NZ, .bcl_div
LD DE, $FFFF        ; minor = major
JP .bcl_dset
.bcl_div
LD A, 16
.bdv_lp
SLA E
RL D
ADD HL, HL
JP C, .bdv_sub      ; 17 ビット目が立った: 必ず引ける
SBC HL, BC          ; (キャリーは 0)
JP NC, .bdv_one
ADD HL, BC
JP .bdv_nx
.bdv_sub
OR A
SBC HL, BC
.bdv_one
INC E
.bdv_nx
DEC A
JP NZ, .bdv_lp
.bcl_dset
LD (.bcl_de), DE
LD HL, (.bgl_x1)
LD (.bcl_x), HL
LD HL, (.bgl_y1)
LD (.bcl_y), HL
LD HL, $8000
LD A, (.bcl_sy)
CP 1
JP Z, .bcl_fset
LD L, $80           ; y が減る向き: 32768 + 128 (本物の実測)
.bcl_fset
LD (.bcl_f), HL
LD HL, (.bcl_mj)
LD (.bcl_n), HL
.bcl_lp
LD HL, (.bcl_y)
BIT 7, H
JP NZ, .bcl_above   ; 上にはみ出している
LD DE, 200
OR A
SBC HL, DE
JP C, .bcl_xin
LD A, (.bcl_sy)     ; 下にはみ出している: 下へ進む線ならこの先も下なので打ち切る
CP 1
RET Z
JP .bcl_skip
.bcl_above
LD A, (.bcl_sy)     ; 上へ進む線ならこの先も上なので打ち切る
CP 1
RET NZ
JP .bcl_skip
.bcl_xin
LD HL, (.bcl_x)
LD BC, (.bgl_xmax)
CALL .bgl_in
JP C, .bcl_skip
LD (.bgl_rx1), HL
LD (.bgl_rx2), HL
LD HL, (.bcl_y)
LD (.bgl_ry1), HL
LD (.bgl_ry2), HL
CALL .bgl_rect
.bcl_skip
LD HL, (.bcl_n)
LD A, H
OR L
RET Z
DEC HL
LD (.bcl_n), HL
LD HL, (.bcl_f)
LD DE, (.bcl_de)
ADD HL, DE
LD (.bcl_f), HL
LD A, 0
ADC A, 0
LD B, A             ; 短い方の軸を進めるか
LD A, (.bcl_k)
OR A
JP NZ, .bcl_tst
CALL .bcl_xstep     ; 横長: x を進め、繰り上がりで y
LD A, B
OR A
JP Z, .bcl_lp
CALL .bcl_ystep
JP .bcl_lp
.bcl_tst
CALL .bcl_ystep     ; 縦長: y を進め、繰り上がりで x
LD A, B
OR A
JP Z, .bcl_lp
CALL .bcl_xstep
JP .bcl_lp
.bcl_xstep
LD HL, (.bcl_x)
LD A, (.bcl_sx)
CP 1
JP NZ, .bxs_dec
INC HL
JP .bxs_put
.bxs_dec
DEC HL
.bxs_put
LD (.bcl_x), HL
RET
.bcl_ystep
LD HL, (.bcl_y)
LD A, (.bcl_sy)
CP 1
JP NZ, .bys_dec
INC HL
JP .bys_put
.bys_dec
DEC HL
.bys_put
LD (.bcl_y), HL
RET
; HL < DE (符号付き) ならキャリー。HL・DE は変えない
.bcl_lt
LD A, H
XOR D
JP P, .blt_same
LD A, H             ; 符号が違う: HL が負なら小さい
RLA
RET
.blt_same
PUSH HL
OR A
SBC HL, DE
POP HL
RET
; HL が 0〜BC (符号付き) の外ならキャリー
.bgl_in
BIT 7, H
SCF
RET NZ
LD A, C
SUB L
LD A, B
SBC A, H
RET
; HL = a, DE = b, BC = 上限 → 小さい方〜大きい方を 0〜上限に収めて .bgl_lo・.bgl_hi に (かからなければキャリー)
.bgl_span
LD A, H
XOR D
JP P, .bsp_same
LD A, H             ; 符号が違う: 負の方が小さい
RLA
JP C, .bsp_ord
EX DE, HL
JP .bsp_ord
.bsp_same
PUSH HL
OR A
SBC HL, DE
POP HL
JP C, .bsp_ord
EX DE, HL
.bsp_ord            ; HL = 小さい方, DE = 大きい方
BIT 7, D
SCF
RET NZ              ; 大きい方が負
BIT 7, H
JP Z, .bsp_lo
LD HL, 0
.bsp_lo
LD (.bgl_lo), HL
CALL .bgl_in        ; 小さい方が上限を超える
RET C
EX DE, HL
CALL .bgl_in
JP NC, .bsp_hi
LD H, B
LD L, C
.bsp_hi
LD (.bgl_hi), HL
OR A
RET
; 長方形を塗る: x .bgl_rx1〜.bgl_rx2、y .bgl_ry1〜.bgl_ry2 (画面の中に収めたもの。小さい方が先)。
; 番地 = (y mod 8) * $800 + (y / 8) * 幅 + x / 8、面は青 $4000・赤 $8000・緑 $C000 (色の bit 0・1・2)、バイトの上位ビットが左の点
.bgl_rect
LD HL, (.bgl_rx1)   ; 左端: バイトの位置と、$FF を x mod 8 だけ右へずらしたマスク
LD A, L
AND 7
LD B, A
LD A, $FF
CALL .brc_shr
LD (.brc_lm), A
CALL .brc_div8
LD A, L
LD (.brc_lb), A
LD HL, (.bgl_rx2)   ; 右端: $FF を 7 - x mod 8 だけ左へずらしたマスクと、左端からのバイト数
LD A, L
AND 7
XOR 7
LD B, A
LD A, $FF
CALL .brc_shl
LD (.brc_rm), A
CALL .brc_div8
LD A, (.brc_lb)
LD B, A
LD A, L
SUB B
LD (.brc_n), A
LD A, (BAS_WID)     ; 1 行のバイト数
CP 80
LD A, 80
JP Z, .brc_wset
LD A, 40
.brc_wset
LD (.brc_w), A
LD A, (.bgl_ry1)
LD (.brc_y), A
.brc_row
LD A, (.brc_y)
LD C, A
AND 7
ADD A, A
ADD A, A
ADD A, A
LD H, A
LD A, (.brc_lb)
LD L, A
LD A, C
RRCA
RRCA
RRCA
AND $1F
LD B, A
LD A, (.brc_w)
LD E, A
LD D, 0
INC B
JP .brc_m1
.brc_m0
ADD HL, DE
.brc_m1
DJNZ .brc_m0
LD (.brc_adr), HL
LD A, (.bgl_col)
LD (.brc_cb), A
LD A, $40
; 面ごと: D = 1 なら点を立てる、0 なら落とす。PSET はビットが 1 の面に立て 0 の面で落とす。PRESET はビットが 1 の面だけ落とす
.brc_plane
LD (.brc_p), A
LD A, (.brc_cb)
RRCA
LD (.brc_cb), A
LD D, 1
JP C, .brc_bit1
DEC D
LD A, (.bgl_mode)
AND 2
JP NZ, .brc_pnext
JP .brc_go
.brc_bit1
LD A, (.bgl_mode)
AND 2
JP Z, .brc_go
DEC D
.brc_go
LD HL, (.brc_adr)
LD A, (.brc_p)
ADD A, H
LD B, A
LD C, L
CALL .brc_line
.brc_pnext
LD A, (.brc_p)
ADD A, $40
JP NZ, .brc_plane
LD A, (.bgl_ry2)    ; 次の行
LD B, A
LD A, (.brc_y)
CP B
RET Z
INC A
LD (.brc_y), A
JP .brc_row
; 1 面の 1 行: BC = 左端の番地、D = 1 なら立てる・0 なら落とす
.brc_line
LD A, (.brc_n)
OR A
JP NZ, .brc_ln
LD A, (.brc_lm)
LD HL, .brc_rm
AND (HL)
JP .brc_put
.brc_ln
LD L, A
LD A, (.brc_lm)
CALL .brc_put
.brc_lmid
DEC L
JP Z, .brc_llast
LD A, $FF
CALL .brc_put
JP .brc_lmid
.brc_llast
LD A, (.brc_rm)
; A = マスク。BC のバイトのマスクの所を立てる (D = 1) か落とす (D = 0)。BC を 1 進める
.brc_put
LD E, A
IN A, (C)
DEC D
INC D
JP Z, .brc_res
OR E
OUT (C), A
INC BC
RET
.brc_res
LD H, A
LD A, E
CPL
AND H
OUT (C), A
INC BC
RET
; A を B ビット右へ・左へ (B は 0〜7)
.brc_shr
INC B
JR .brc_sr1
.brc_sr0
SRL A
.brc_sr1
DJNZ .brc_sr0
RET
.brc_shl
INC B
JR .brc_sl1
.brc_sl0
ADD A, A
.brc_sl1
DJNZ .brc_sl0
RET
; HL を 8 で割る
.brc_div8
SRL H
RR L
SRL H
RR L
SRL H
RR L
RET
.bcl_yy DW 0
.bcl_xx DW 0
.bcl_mn DW 0
.bcl_mj DW 0
.bcl_de DW 0
.bcl_x DW 0
.bcl_y DW 0
.bcl_f DW 0
.bcl_n DW 0
.bcl_sx DB 0
.bcl_sy DB 0
.bcl_k DB 0
.bgl_ret DW 0
.bgl_mode DB 0
.bgl_c DW 0
.bgl_col DB 0
.bgl_x1 DW 0
.bgl_y1 DW 0
.bgl_x2 DW 0
.bgl_y2 DW 0
.bgl_xmax DW 0
.bgl_t DW 0
.bgl_lo DW 0
.bgl_hi DW 0
.bgl_lx DW 0
.bgl_hx DW 0
.bgl_ly DW 0
.bgl_hy DW 0
.bgl_rx1 DW 0
.bgl_rx2 DW 0
.bgl_ry1 DW 0
.bgl_ry2 DW 0
.brc_lm DB 0
.brc_rm DB 0
.brc_lb DB 0
.brc_n DB 0
.brc_w DB 0
.brc_y DB 0
.brc_adr DW 0
.brc_cb DB 0
.brc_p DB 0


; ============================================================ PAINT

; @name BAS_PAINT
; @resident shared
; @calls BAS_WORK,BAS_ERROR,PAINT1
; @stack_cleanup callee
; @works BAS_PNTBUF:1056
; スタック引数 (x, y, 色, 境界色 1, …, 境界色 n, n)。n は 0〜7 (引数の数が呼び出しごとに違うので、n を見て取り除く)。
; 色が 0〜7 の外、境界色が 0〜255 の外はエラー 5。8〜255 の境界色はどの点とも合わないので捨てる。塗り始めの点が画面の外なら
; 何もしない (本物の実測)。塗りは libx1_grp (X1PAINT) に任せ、待ち行列は BAS_PNTBUF に置く (1056 バイト。libx1_grp が VRAM の
; 使っていない所と入れ替える大きさのうち大きい方)。待ち行列があふれたらエラー 7
POP HL
LD (.bpt_ret), HL
POP HL
LD B, L
XOR A
LD (.bpt_bad), A
LD C, A             ; 境界色の印 (bit k = 色 k)
INC B
JP .bpt_b1
.bpt_b0
POP HL
LD A, H
OR A
JP Z, .bpt_b2
LD A, 1             ; 0〜255 の外 (引数を取り除いてからエラーにする)
LD (.bpt_bad), A
JP .bpt_b1
.bpt_b2
LD A, L
CP 8
JP NC, .bpt_b1
LD E, A
LD A, 1
INC E
JP .bpt_s1
.bpt_s0
ADD A, A
.bpt_s1
DEC E
JP NZ, .bpt_s0
OR C
LD C, A
.bpt_b1
DJNZ .bpt_b0
POP HL
LD (.bpt_c), HL
POP HL
LD (.bpt_y), HL
POP HL
LD (.bpt_x), HL
LD HL, (.bpt_ret)
PUSH HL
; 検査
LD HL, (.bpt_c)
LD A, H
OR A
JP NZ, .bpt_err
LD A, L
CP 8
JP NC, .bpt_err
LD A, (.bpt_bad)
OR A
JP NZ, .bpt_err
; 塗り始めが画面の外なら何もしない
LD HL, (.bpt_x)
BIT 7, H
RET NZ
LD DE, 320
LD A, (BAS_WID)
CP 80
JP NZ, .bpt_w40
LD DE, 640
.bpt_w40
OR A
SBC HL, DE
RET NC
LD HL, (.bpt_y)
LD A, H
OR A
RET NZ
LD A, L
CP 200
RET NC
; libx1_grp の塗りのパラメータ: DW x, DW y, DB 色, DB 境界色… (数 = 1 + 境界色の数)
LD HL, (.bpt_x)
LD (.bpt_prm), HL
LD HL, (.bpt_y)
LD (.bpt_prm+2), HL
LD A, (.bpt_c)
LD (.bpt_prm+4), A
LD HL, .bpt_prm+5
LD DE, 1            ; E = 数、D = 色の番号
LD B, 8
.bpt_l0
SRL C
JP NC, .bpt_l1
LD (HL), D
INC HL
INC E
.bpt_l1
INC D
DJNZ .bpt_l0
LD A, E
LD (.bpt_n), A
LD HL, BAS_PNTBUF
LD (X1PAINT.BUFADR), HL
LD (.bpt_sp), SP
CALL .bpt_go
RET
; PAINT2 と同じ入り方 (3 つ積んで、DE = パラメータ、A = 数で GPAINT_TOP へ。ふつうに終われば 3 つ取り出して戻ってくる)。
; 待ち行列があふれると、libx1_grp は 3 段の戻り先を捨てた後、積んだ 3 つの一番上を戻り先にする。そこを .bpt_over にしておく
.bpt_go
PUSH BC
PUSH DE
LD HL, .bpt_over
PUSH HL
LD DE, .bpt_prm
LD A, (.bpt_n)
JP X1PAINT.GPAINT_TOP
.bpt_over
LD SP, (.bpt_sp)
LD HL, 7
JP BAS_ERROR
.bpt_err
LD HL, 5
JP BAS_ERROR
.bpt_ret DW 0
.bpt_bad DB 0
.bpt_c DW 0
.bpt_x DW 0
.bpt_y DW 0
.bpt_n DB 0
.bpt_sp DW 0
.bpt_prm DS 13


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
; 約 1ms (3983 T、HuBASIC の実測した刻み) 待つ。割り込みは止めている前提
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
; (HuBASIC の実測: PLAY はブロックする)。チャンネルごとの状態 (オクターブ・長さ・音量) は PLAY をまたいで残る
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
; エンベロープの形 (R13) を PSG から読んで同じ値を書き直す (V16 のエンベロープを音符ごとに最初から。
; 値は SOUND 13 や OUT で書いた今の値のまま。HuBASIC の実測)
PUSH BC
LD BC, $1C00
LD A, 13
OUT (C), A
LD B, $1B
IN A, (C)
OUT (C), A
POP BC
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
JP C, .bpn_parse    ; 数字がなければオクターブはそのまま (HuBASIC の実測)
OR A
JP Z, .bpn_err
CP 9
JP NC, .bpn_err
LD HL, (.bpn_st)
LD (HL), A
JP .bpn_parse
.bpn_vol
; 音量 0-16 (16 は PSG のエンベロープ)。先頭 2 桁が 16 以下なら 2 桁、そうでなければ 1 桁。
; 後に続く数字は捨てる (V17 は 1、V107 は 10、V255 は 2。HuBASIC の実測)
CALL .bpn_digit
JP C, .bpn_parse    ; 数字がなければ音量はそのまま (HuBASIC の実測)
LD C, A
CALL .bpn_peekdigit ; 2 桁目
JP C, .bpn_vskip
LD B, A
LD A, C
ADD A, A
LD D, A
ADD A, A
ADD A, A
ADD A, D            ; ×10
ADD A, B
CP 17
JP NC, .bpn_vskip   ; 17 以上なら 1 桁目だけ
LD C, A
.bpn_vskip
CALL .bpn_digit     ; 残りの数字を捨てる
JP NC, .bpn_vskip
LD HL, (.bpn_st)
INC HL
INC HL
LD (HL), C
; V を読んだ所で、すぐにそのチャンネルの音量のレジスタに書く (HuBASIC の実測)
LD E, C
LD A, (.bpn_ch)
ADD A, 8
CALL BAS_PSGW
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
; 長さ 0-9 → 1/32 音符の個数 (HuBASIC の実測)
.bpn_ltab DB 1, 2, 3, 4, 6, 8, 12, 16, 24, 32


; @name BAS_PTAB
; @resident shared
; @param_count 0
; 音程の値 (PSG 2MHz、125000 / 周波数 の切り捨て、HuBASIC の実測と一致)。O1-O8 × C C# D D# E F F# G G# A A# B
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
; チャンネル C を音程の値 190・音量 15 で約 80ms 鳴らし、音程を元に戻す (HuBASIC の実測)
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


; ============================================================ 浮動小数 (段階 F1a)
; 浮動小数は SLANG の FLOAT (f24): A = 符号 1 ビット + 指数 7 ビット (偏り 63。0 なら値 0、$7F なら無限大・NaN)、
; HL = 仮数 16 ビット (頭の 1 は省略)。記憶の上では 仮数の下位・上位・符号と指数 の 3 バイト。

; @name BAS_FTOIR
; @resident shared
; @param_count 1
; @calls BAS_ERROR
; AHL = 浮動小数 → HL = 整数。四捨五入 (0 から遠い側)。絶対値が 65535 以下なら 16 ビットに回り込ませ
; (整数の代入と同じ)、それを超える値・無限大・NaN はエラー 6 (Overflow)
LD C, A
AND $7F
JP Z, .bft_zero
CP $7F
JP Z, .bft_ovf
SUB 63
JP C, .bft_small
CP 16
JP NC, .bft_ovf
LD B, A             ; B = 2 の指数 (0〜15)
LD D, 0
LD A, 1             ; D:A = 整数部 (頭の 1 から)
INC B
DEC B
JP Z, .bft_int
.bft_sh
ADD HL, HL
RLA
RL D
DJNZ .bft_sh
.bft_int
LD E, A             ; DE = 整数部、HL = 小数部
BIT 7, H
JP Z, .bft_sign
INC DE              ; 小数部が 0.5 以上なら切り上げ
LD A, D
OR E
JP Z, .bft_ovf      ; 65535 を超えた
.bft_sign
EX DE, HL
BIT 7, C
RET Z
XOR A
SUB L
LD L, A
SBC A, A
SUB H
LD H, A
RET
.bft_small
CP $FF              ; 0.5 以上 1 未満なら ±1、それより小さければ 0
JP NZ, .bft_zero
LD HL, 1
BIT 7, C
RET Z
LD HL, $FFFF
RET
.bft_zero
LD HL, 0
RET
.bft_ovf
LD HL, 6
JP BAS_ERROR


; @name BAS_FDIV
; @resident shared
; @param_count 2
; @result_type float
; @calls BAS_ERROR,f24div
; HL = 割られる数の番地、DE = 割る数の番地 (どちらも浮動小数 3 バイト) → AHL = 商。割る数が 0 ならエラー 11
PUSH HL
EX DE, HL
LD E, (HL)
INC HL
LD D, (HL)
INC HL
LD A, (HL)
LD C, A             ; CDE = 割る数
AND $7F
JP NZ, .bfd_ok
POP HL
LD HL, 11
JP BAS_ERROR
.bfd_ok
POP HL
PUSH DE
LD E, (HL)
INC HL
LD D, (HL)
INC HL
LD A, (HL)
EX DE, HL           ; AHL = 割られる数
POP DE
JP f24div


; @name BAS_FCMP
; @resident shared
; @param_count 2
; @calls BAS_ERROR
; HL・DE = 比べる 2 つの浮動小数の番地 → HL = -1 (左 < 右) / 0 (等しい) / 1 (左 > 右)。
; 厳密に比べる (libfloat の f24cmp は仮数の下位のビットの差を許すので使わない)。0 は符号に関係なく等しい。
; 無限大・NaN はエラー 6
PUSH DE
LD DE, .bfc_a
CALL .bfc_key
POP HL
LD DE, .bfc_b
CALL .bfc_key
; 鍵を上の桁から符号なしで比べる
LD HL, .bfc_a + 2
LD DE, .bfc_b + 2
LD B, 3
.bfc_cmp
LD A, (DE)
CP (HL)             ; 右 - 左
JP NZ, .bfc_ne
DEC HL
DEC DE
DJNZ .bfc_cmp
LD HL, 0
RET
.bfc_ne
LD HL, 1            ; キャリー = 右 < 左
RET C
LD HL, $FFFF
RET
; (HL) の浮動小数を、値の大小の順に並ぶ 24 ビットの鍵にして (DE) に書く (下位から 3 バイト)。
; 鍵 = 指数:仮数 (負なら 2 の補数で反転)、最上位のビットを反転して符号なしの並びにする
.bfc_key
LD C, (HL)
INC HL
LD B, (HL)
INC HL
LD A, (HL)
LD L, A             ; L = 符号と指数
AND $7F
JP Z, .bfc_k0
CP $7F
JP Z, .bfc_ovf
LD H, A             ; H:B:C = 指数:仮数
BIT 7, L
JP Z, .bfc_kput
LD A, C
CPL
LD C, A
LD A, B
CPL
LD B, A
LD A, H
CPL
LD H, A
INC C
JP NZ, .bfc_kput
INC B
JP NZ, .bfc_kput
INC H
JP .bfc_kput
.bfc_k0
LD H, A
LD B, A
LD C, A
.bfc_kput
LD A, C
LD (DE), A
INC DE
LD A, B
LD (DE), A
INC DE
LD A, H
XOR $80
LD (DE), A
RET
.bfc_ovf
LD HL, 6
JP BAS_ERROR
.bfc_a DB 0, 0, 0
.bfc_b DB 0, 0, 0


; ============================================================ 浮動小数の FOR
; 浮動小数の FOR 枠は種別 3 (整数は 2)。枠には整数の FOR 枠と同じく出現番号と変数の番地を持ち、上限と増分 (3 バイトずつ) は
; 枠の深さごとの BAS_FSTK に置く (制御スタックの枠の大きさは変えない)。浮動小数の FOR があるプログラムの NEXT は、
; どちらの枠にも対応する BAS_FFORNEXT を呼ぶ (整数だけのプログラムは、いままでどおり BAS_FORNEXT だけを使う)。

; @name BAS_FWORK
; @resident shared
; @param_count 0
; @works BAS_FSTK:384
; BAS_FSTK: 浮動小数の FOR の上限と増分 (制御スタックの枠の深さごとに 6 バイト: 上限 3・増分 3。64 枠分)
RET


; @name BAS_FSLOT
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_FWORK
; HL = 制御スタックの枠の先頭 → HL = その深さの BAS_FSTK の領域 (BAS_FSTK + 深さ × 6)。A・DE を壊す
LD DE, BAS_CSTK
OR A
SBC HL, DE
EX DE, HL           ; DE = 深さ × 10
LD HL, BAS_FSTK
.bfs_loop
LD A, D
OR E
RET Z
LD A, E
SUB 10
LD E, A
LD A, D
SBC A, 0
LD D, A
LD A, L
ADD A, 6
LD L, A
JP NC, .bfs_loop
INC H
JP .bfs_loop


; @name BAS_FFORPUSH
; @resident shared
; @calls BAS_WORK,BAS_FWORK,BAS_CSPUSH,BAS_FORFIND,BAS_FCMP,BAS_FSLOT,BAS_FSTEPNEG
; @stack_cleanup callee
; スタック引数 (&変数, &上限, &増分, 出現番号)。浮動小数の FOR の入口の判定 (増分が負でなければ 変数 <= 上限、負なら
; 変数 >= 上限。BAS_FCMP で厳密に比べる。変数・上限・増分の無限大・NaN はエラー 6) をして、回るなら浮動小数の FOR 枠を
; 積み、その深さの BAS_FSTK に上限と増分を写して -1、回らないなら積まずに 0 を返す。積む前に、同じ変数の FOR 枠
; (いちばん上の GOSUB 枠より上) とその上の枠を捨てる (整数の BAS_FORPUSH と同じ)。
POP HL
LD (.bfp_ret), HL
POP HL
LD (.bfp_id), HL
POP HL
LD (.bfp_step), HL
POP HL
LD (.bfp_lim), HL
POP HL
LD (.bfp_var), HL
LD HL, (.bfp_ret)
PUSH HL
LD HL, (.bfp_step)
LD D, H
LD E, L
CALL BAS_FCMP       ; 増分の無限大・NaN を調べる (自分と比べる)
LD HL, (.bfp_var)
LD DE, (.bfp_lim)
CALL BAS_FCMP       ; HL = 変数と上限の大小 (-1 / 0 / 1)
LD DE, (.bfp_step)
CALL BAS_FSTEPNEG
JP Z, .bfp_pos
BIT 7, H            ; 増分が負: 変数 >= 上限 (大小が -1 でない) なら回る
JP NZ, .bfp_skip
JP .bfp_run
.bfp_pos
LD A, L             ; 増分が負でない: 変数 <= 上限 (大小が 1 でない) なら回る
DEC A
OR H
JP Z, .bfp_skip
.bfp_run
LD HL, (.bfp_var)
CALL BAS_FORFIND    ; 同じ変数の枠を探す (HL = 枠 / 0)
LD A, H
OR L
JP Z, .bfp_push
LD (BAS_CSP), HL    ; その枠とその上を捨てる
.bfp_push
LD DE, (.bfp_id)
LD A, 3
CALL BAS_CSPUSH     ; HL = 枠の先頭
PUSH HL
INC HL
INC HL
INC HL
LD DE, (.bfp_var)
LD (HL), E
INC HL
LD (HL), D
POP HL
CALL BAS_FSLOT
EX DE, HL           ; DE = この深さの上限と増分の置き場
LD HL, (.bfp_lim)
LD BC, 3
LDIR
LD HL, (.bfp_step)
LD BC, 3
LDIR
LD HL, $FFFF
RET
.bfp_skip
LD HL, 0
RET
.bfp_ret DW 0
.bfp_id DW 0
.bfp_step DW 0
.bfp_lim DW 0
.bfp_var DW 0


; @name BAS_FSTEPNEG
; @resident shared
; @param_count 1
; DE = 増分の番地 (浮動小数 3 バイト) → 負なら NZ、負でなければ Z (-0 は負でない)。A を壊す
INC DE
INC DE
LD A, (DE)
DEC DE
DEC DE
BIT 7, A
RET Z
AND $7F             ; 符号が立っていても、指数が 0 (値が 0) なら負でない
RET


; @name BAS_FFORNEXT
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_FWORK,BAS_FORFIND,BAS_FORNEXT,BAS_FSLOT,BAS_FCMP,BAS_FSTEPNEG,f24add
; HL = 変数のアドレス (0 = 指定なし)。浮動小数の FOR があるプログラムの NEXT。対応する枠が整数の FOR 枠なら (見つから
; なければも) BAS_FORNEXT と同じ。浮動小数の FOR 枠なら、その上の枠を捨て、変数 + 増分 (f24add) を変数に書いて上限と
; 厳密に比べる (無限大・NaN はエラー 6)。続行なら FOR の出現番号、終わりなら枠を外して -1 を返す。
PUSH HL
CALL BAS_FORFIND
LD A, H
OR L
JP Z, .bfn_int
LD A, (HL)
CP 3
JP Z, .bfn_float
.bfn_int
POP HL
JP BAS_FORNEXT
.bfn_float
POP DE
LD (.bfn_frame), HL
LD DE, 10
ADD HL, DE
LD (BAS_CSP), HL    ; 見つかった枠の上を捨てる
LD HL, (.bfn_frame)
INC HL
LD E, (HL)
INC HL
LD D, (HL)
LD (.bfn_id), DE
INC HL
LD E, (HL)
INC HL
LD D, (HL)
LD (.bfn_var), DE
LD HL, (.bfn_frame)
CALL BAS_FSLOT
LD (.bfn_lim), HL   ; 上限 (その後ろの 3 バイトが増分)
LD DE, 3
ADD HL, DE
LD (.bfn_step), HL
LD E, (HL)
INC HL
LD D, (HL)
INC HL
LD C, (HL)          ; CDE = 増分
PUSH DE
LD HL, (.bfn_var)
LD E, (HL)
INC HL
LD D, (HL)
INC HL
LD A, (HL)
EX DE, HL           ; AHL = 変数
POP DE
CALL f24add         ; AHL = 変数 + 増分
EX DE, HL
LD HL, (.bfn_var)
LD (HL), E
INC HL
LD (HL), D
INC HL
LD (HL), A
LD HL, (.bfn_var)
LD DE, (.bfn_lim)
CALL BAS_FCMP       ; HL = 変数と上限の大小 (無限大・NaN はエラー 6)
LD DE, (.bfn_step)
CALL BAS_FSTEPNEG
JP Z, .bfn_pos
BIT 7, H            ; 増分が負: 変数 >= 上限なら続行
JP NZ, .bfn_exit
JP .bfn_cont
.bfn_pos
LD A, L             ; 増分が負でない: 変数 <= 上限なら続行
DEC A
OR H
JP NZ, .bfn_cont
.bfn_exit
LD HL, (.bfn_frame)
LD (BAS_CSP), HL    ; 枠を外す
LD HL, $FFFF
RET
.bfn_cont
LD HL, (.bfn_id)
RET
.bfn_frame DW 0
.bfn_id DW 0
.bfn_var DW 0
.bfn_lim DW 0
.bfn_step DW 0


; @name BAS_FFLOOR
; @resident shared
; @param_count 1
; @result_type float
; @calls f24sub
; AHL → AHL = 床関数 (AHL を超えない最大の整数。INT 関数)。無限大・NaN はそのまま
LD C, A
AND $7F
RET Z               ; 0
CP $7F
JP Z, .bff_same
SUB 63
JP C, .bff_lt1
CP 16
JP NC, .bff_same    ; 小数部がない大きさ
; 2 の指数 E (0〜15): 仮数の下位 16-E ビットが小数部
LD B, A
LD DE, $FFFF
LD A, 16
SUB B
.bff_m
SLA E
RL D
DEC A
JP NZ, .bff_m       ; DE = 整数部を残すマスク
LD A, E
CPL
AND L
LD B, A
LD A, D
CPL
AND H
OR B
LD B, A             ; B = 0 なら小数部がない
LD A, L
AND E
LD L, A
LD A, H
AND D
LD H, A             ; HL = 小数部を捨てた仮数
LD A, C
BIT 7, C
RET Z               ; 正: 切り捨てたもの
INC B
DEC B
RET Z               ; 負で小数部なし: そのまま
LD C, $3F           ; 負で小数部あり: 切り捨てたもの - 1 (CDE = 1.0)
LD DE, 0
JP f24sub
.bff_same
LD A, C
RET
.bff_lt1
BIT 7, C            ; 絶対値が 1 未満: 正なら 0、負なら -1
JP NZ, .bff_m1
XOR A
LD H, A
LD L, A
RET
.bff_m1
LD A, $BF
LD HL, 0
RET


; @name BAS_IABS
; @resident shared
; @param_count 1
; HL = 整数 → HL = 絶対値 (ABS 関数)。-32768 はそのまま (16 ビットの回り込み)
BIT 7, H
RET Z
XOR A
SUB L
LD L, A
SBC A, A
SUB H
LD H, A
RET


; @name BAS_ISGN
; @resident shared
; @param_count 1
; HL = 整数 → HL = 符号 (SGN 関数。負は -1、0 は 0、正は 1)
LD A, H
OR L
RET Z
BIT 7, H
LD HL, 1
RET Z
LD HL, $FFFF
RET


; @name BAS_FSGN
; @resident shared
; @param_count 1
; AHL = 浮動小数 → HL = 符号 (SGN 関数。負は -1、0 (-0 も) は 0、正は 1)
LD C, A
AND $7F
LD HL, 0
RET Z
INC HL
BIT 7, C
RET Z
LD HL, $FFFF
RET


; @name BAS_FFIX
; @resident shared
; @param_count 1
; @result_type float
; @calls BAS_FFLOOR
; AHL → AHL = 0 の方向への切り捨て (FIX 関数)。負の数は絶対値の床に符号を付ける。結果が 0 なら符号を付けない
OR A
JP P, BAS_FFLOOR    ; 正: 床と同じ
AND $7F
CALL BAS_FFLOOR
LD C, A
AND $7F
RET Z               ; 0 (A = 0)
LD A, C
OR $80
RET


; @name BAS_FFRAC
; @resident shared
; @param_count 1
; @result_type float
; @calls BAS_FFIX,f24sub
; AHL → AHL = 小数部 (FRAC 関数。x - FIX(x))
PUSH HL
PUSH AF
CALL BAS_FFIX
LD C, A
EX DE, HL           ; CDE = FIX(x)
POP AF
POP HL              ; AHL = x
JP f24sub


; @name BAS_FSQR
; @resident shared
; @param_count 1
; @result_type float
; @calls BAS_ERROR,FSQRT
; AHL → AHL = 平方根 (SQR 関数)。負の数はエラー 5。0 (-0 も) は 0
LD C, A
AND $7F
RET Z
BIT 7, C
JP NZ, .bsq_err
LD A, C
JP f24sqrt
.bsq_err
LD HL, 5
JP BAS_ERROR


; @name BAS_FLOG
; @resident shared
; @param_count 1
; @result_type float
; @calls BAS_ERROR,FLOG
; AHL → AHL = 自然対数 (LOG 関数)。0 以下はエラー 5
LD C, A
AND $7F
JP Z, .blg_err
BIT 7, C
JP NZ, .blg_err
LD A, C
JP f24log
.blg_err
LD HL, 5
JP BAS_ERROR


; @name BAS_FPOW
; @resident shared
; @param_count 2
; @result_type float
; @calls BAS_ERROR,f24mul,f24inv,FPOW
; HL = 底 x の番地、DE = 指数 y の番地 (どちらも浮動小数 3 バイト) → AHL = x^y。
; x が 0: y が 0 なら 1、それ以外は 0 (本物は 0 の負の累乗も 0)。y が 0 なら 1。
; y が整数で絶対値が 65535 以下: 2 進の累乗で掛け算を繰り返す (x は負でもよい。本物も (-1)^32769 は -1)。y が負なら、先に底を 1/x にしてから
; |y| 乗する (後で割ると、.5^-63 の途中の 2^-63 が下にあふれて 0 になるため)。
; それ以外: x が正なら libfloat の FPOW (exp(y × log x))。x が負なら、y が整数でなければエラー 5、
; 絶対値が 65535 を超える整数ならエラー 6 (本物の実測: (-1)^65536 は Overflow)
PUSH DE
LD E, (HL)
INC HL
LD D, (HL)
INC HL
LD A, (HL)
LD (.bpw_x), DE
LD (.bpw_xa), A
POP HL
LD E, (HL)
INC HL
LD D, (HL)
INC HL
LD A, (HL)
LD (.bpw_y), DE
LD (.bpw_ya), A
LD A, (.bpw_xa)
AND $7F
JP NZ, .bpw_nz
LD A, (.bpw_ya)
AND $7F
JP Z, .bpw_one      ; 0^0 = 1
XOR A               ; 0^y = 0
LD H, A
LD L, A
RET
.bpw_nz
LD A, (.bpw_ya)
AND $7F
JP Z, .bpw_one      ; x^0 = 1
CP $7F
JP Z, .bpw_real     ; y が無限大・NaN
SUB 63
JP C, .bpw_real     ; |y| < 1 (整数でない)
CP 16
JP NC, .bpw_real    ; |y| >= 65536 (仮数が 16 ビットなので整数)
; |y| = 1.仮数 × 2^E (E = 0〜15): 仮数の上の E ビットを整数部に移し、残りが 0 なら整数
LD B, A
LD HL, (.bpw_y)
LD DE, 1
OR A
JP Z, .bpw_chk
.bpw_sh
ADD HL, HL
RL E
RL D
DJNZ .bpw_sh
.bpw_chk
LD A, H
OR L
JP NZ, .bpw_real    ; 小数部がある
LD (.bpw_n), DE
LD A, (.bpw_ya)
OR A
JP P, .bpw_pos
LD HL, (.bpw_x)
LD A, (.bpw_xa)
CALL f24inv         ; 底 = 1/x
LD (.bpw_x), HL
LD (.bpw_xa), A
.bpw_pos
LD HL, 0
LD (.bpw_r), HL
LD A, $3F
LD (.bpw_ra), A     ; 結果 = 1.0
.bpw_loop
LD HL, (.bpw_n)
SRL H
RR L
LD (.bpw_n), HL
JP NC, .bpw_sq
LD DE, (.bpw_x)
LD A, (.bpw_xa)
LD C, A
LD HL, (.bpw_r)
LD A, (.bpw_ra)
CALL f24mul         ; 結果 ×= 底
LD (.bpw_r), HL
LD (.bpw_ra), A
.bpw_sq
LD HL, (.bpw_n)
LD A, H
OR L
JP Z, .bpw_done
LD DE, (.bpw_x)
LD A, (.bpw_xa)
LD C, A
LD HL, (.bpw_x)
CALL f24mul         ; 底 ×= 底
LD (.bpw_x), HL
LD (.bpw_xa), A
JP .bpw_loop
.bpw_done
LD HL, (.bpw_r)
LD A, (.bpw_ra)
RET
.bpw_real
LD A, (.bpw_xa)
OR A
JP P, .bpw_pow
LD A, (.bpw_ya)     ; 負の数の累乗: |y| >= 65536 (無限大・NaN も) はエラー 6、それ以外 (整数でない) はエラー 5
AND $7F
CP 63 + 16
JP C, .bpw_err
LD HL, 6
JP BAS_ERROR
.bpw_pow
LD A, (.bpw_ya)
LD C, A
LD DE, (.bpw_y)
LD HL, (.bpw_x)
LD A, (.bpw_xa)
JP f24pow
.bpw_one
LD A, $3F
LD HL, 0
RET
.bpw_err
LD HL, 5
JP BAS_ERROR
.bpw_x DW 0
.bpw_xa DB 0
.bpw_y DW 0
.bpw_ya DB 0
.bpw_r DW 0
.bpw_ra DB 0
.bpw_n DW 0


; @name BAS_SLEN
; @resident shared
; @param_count 1
; HL = 文字列 → HL = 長さ (LEN 関数)
LD L, (HL)
LD H, 0
RET


; @name BAS_SINSTR
; @resident shared
; @param_count 3
; @calls BAS_ERROR
; HL = 探し始める位置 n (1〜255、それ以外はエラー 5)、DE = 文字列 a、BC = 探す文字列 b
; → HL = a の n 文字目から探して、b が見つかった位置 (1 始まり)。なければ 0 (INSTR 関数)。
; n の範囲を先に調べ、b が空なら 0 (本物の実測)
LD A, H
OR A
JP NZ, .bin_err
LD A, L
OR A
JP Z, .bin_err
LD A, (BC)
OR A
JP Z, .bin_none     ; b が空
LD (.bin_b), BC
LD A, (DE)          ; A = a の長さ
SUB L
JP C, .bin_none     ; n が a の長さより大きい
INC A               ; A = n 文字目から後ろの文字数
LD H, A
LD A, (BC)
LD C, A             ; C = b の長さ
LD A, H
SUB C
JP C, .bin_none     ; 残りが b より短い
INC A
LD B, A             ; B = 比べる位置の数
LD A, L
LD (.bin_pos), A
LD H, 0
ADD HL, DE          ; HL = a の n 文字目
.bin_loop
PUSH HL
PUSH BC
LD DE, (.bin_b)
INC DE
.bin_cmp
LD A, (DE)
CP (HL)
JP NZ, .bin_ne
INC HL
INC DE
DEC C
JP NZ, .bin_cmp
POP BC
POP HL
LD A, (.bin_pos)
LD L, A
LD H, 0
RET
.bin_ne
POP BC
POP HL
INC HL
LD A, (.bin_pos)
INC A
LD (.bin_pos), A
DJNZ .bin_loop
.bin_none
LD HL, 0
RET
.bin_err
LD HL, 5
JP BAS_ERROR
.bin_b DW 0
.bin_pos DB 0


; @name BAS_SHEXS
; @resident shared
; @param_count 1
; @calls BAS_TALLOC
; HL = 16 ビットの値 → その 16 進 (大文字、頭の 0 なし。0 は "0") の一時文字列 (HEX$ 関数)
LD DE, .bhs_buf
LD A, H
CALL .bhs_byte
LD A, L
CALL .bhs_byte
LD HL, .bhs_buf
LD B, 4
.bhs_lz
LD A, B
CP 1
JP Z, .bhs_go       ; 最後の 1 桁は残す
LD A, (HL)
CP '0'
JP NZ, .bhs_go
INC HL
DEC B
JP .bhs_lz
.bhs_go
PUSH HL
PUSH BC
LD A, B
CALL BAS_TALLOC
POP BC
POP DE              ; DE = 最初の桁
PUSH HL
INC HL
.bhs_cp
LD A, (DE)
LD (HL), A
INC HL
INC DE
DJNZ .bhs_cp
POP HL
RET
.bhs_byte           ; A の 2 桁を DE に書く
PUSH AF
RRCA
RRCA
RRCA
RRCA
CALL .bhs_nib
POP AF
.bhs_nib
AND $0F
ADD A, '0'
CP '9' + 1
JP C, .bhs_st
ADD A, 7
.bhs_st
LD (DE), A
INC DE
RET
.bhs_buf DB 0, 0, 0, 0


; @name BAS_PRTAB
; @resident shared
; @param_count 1
; @calls BAS_WORK,BAS_PSPC,BAS_ERROR
; HL = 桁 n (0〜255、それ以外はエラー 5) → いまの桁 (0 始まり) が n より小さければ、差の分だけ空白を出す
; (行の端では折り返す)。n 以上なら何もしない (PRINT の TAB)
LD A, H
OR A
JP NZ, .btb_err
LD A, (BAS_CX)
LD B, A
LD A, L
SUB B
RET Z
RET C
LD L, A
JP BAS_PSPC
.btb_err
LD HL, 5
JP BAS_ERROR


; @name BAS_RANDOMIZE
; @resident shared
; @param_count 0
; @calls z80rand
; 乱数の種を R レジスタから作り直す (RANDOMIZE。引数は見ない。FRAND の初期化と同じ)
LD HL, seed0
LD B, 2
.brz_0
LD A, R
LD (HL), A
INC HL
DJNZ .brz_0
LD HL, seed1
LD B, 4
.brz_1
LD A, R
LD (HL), A
INC HL
DJNZ .brz_1
RET


; @name BAS_FSTR
; @resident shared
; @param_count 1
; @calls BAS_ERROR,f24toa
; AHL = 浮動小数 → HL = HuBASIC の形の文字列 (0 終端)。頭に符号の桁 (正は空白、負は '-')、後ろの空白は付けない。
; 小数部のない値で 8 桁以下のものは整数として正確に出す。それ以外は f24toa の有効 5 桁を組み直す:
; 小数点の位置 X (数字の並びの頭から数えた位置) が 0〜8 なら 123.45 / .5 の形、X が負で桁が 8 以内なら .000123 の形、
; それ以外は 1.2345E+18 の形 (指数は 2 桁)。無限大・NaN はエラー 6
LD (.bfs_hl), HL
LD (.bfs_a), A
LD C, A
LD DE, .bfs_out
LD A, ' '
BIT 7, C
JP Z, .bfs_sg
LD A, '-'
.bfs_sg
LD (DE), A
INC DE
LD (.bfs_dst), DE
LD A, C
AND $7F
JP Z, .bfs_zero
CP $7F
JP Z, .bfs_ovf
SUB 63
JP C, .bfs_f24
CP 27
JP NC, .bfs_f24
; 小数部のない値 (2 の指数 0〜26) は、32 ビットの整数にして正確に出す (8 桁まで)
LD B, A
LD A, L
LD (.bfs_q), A
LD A, H
LD (.bfs_q + 1), A
LD A, 1
LD (.bfs_q + 2), A
XOR A
LD (.bfs_q + 3), A
LD A, B
CP 16
JP NC, .bfs_shl
LD A, 16            ; 指数が 16 未満: 16 - 指数 ビット右へ。1 が落ちたら小数部がある
SUB B
LD B, A
.bfs_shr
LD HL, .bfs_q + 2
SRL (HL)
DEC HL
RR (HL)
DEC HL
RR (HL)
JP C, .bfs_f24
DJNZ .bfs_shr
JP .bfs_dec
.bfs_shl
SUB 16
JP Z, .bfs_dec
LD B, A
.bfs_shl1
LD HL, .bfs_q
SLA (HL)
INC HL
RL (HL)
INC HL
RL (HL)
INC HL
RL (HL)
DJNZ .bfs_shl1
.bfs_dec
LD DE, .bfs_dig     ; 10 で割り続けて、下の桁から並べる
LD C, 0
.bfs_dl
CALL .bfs_div10
ADD A, '0'
LD (DE), A
INC DE
INC C
LD HL, (.bfs_q)
LD A, H
OR L
LD HL, (.bfs_q + 2)
OR H
OR L
JP NZ, .bfs_dl
LD A, C
CP 9
JP NC, .bfs_f24     ; 9 桁以上は指数の形にする
LD DE, (.bfs_dst)
LD HL, .bfs_dig - 1
LD B, 0
ADD HL, BC
.bfs_rv
LD A, (HL)
LD (DE), A
INC DE
DEC HL
DEC C
JP NZ, .bfs_rv
XOR A
LD (DE), A
LD HL, .bfs_out
RET
; .bfs_q (32 ビット) を 10 で割る → A = 余り (HL・B を壊す)
.bfs_div10
XOR A
LD B, 32
.bfs_dv
LD HL, .bfs_q
SLA (HL)
INC HL
RL (HL)
INC HL
RL (HL)
INC HL
RL (HL)
RLA
CP 10
JP C, .bfs_dvn
SUB 10
LD HL, .bfs_q
INC (HL)
.bfs_dvn
DJNZ .bfs_dv
RET
.bfs_f24
LD A, (.bfs_a)
AND $7F             ; 符号は書いたので、絶対値を文字にする
LD HL, (.bfs_hl)
LD DE, .bfs_raw
CALL f24toa         ; HL = 文字列 (例: 2.5 / .5 / 123460 / 1e18 / 1.23e-4)
; 数字を .bfs_dig に集める。B = 数字の数、C = 小数点の前の数字の数 ($FF = 小数点なし)
LD DE, .bfs_dig
LD BC, $00FF
XOR A
LD (.bfs_n), A
.bfs_p
LD A, (HL)
OR A
JP Z, .bfs_pend
INC HL
CP '.'
JP NZ, .bfs_p1
LD C, B
JP .bfs_p
.bfs_p1
CP 'e'
JP Z, .bfs_pe
CP '0'
JP C, .bfs_p
CP '9' + 1
JP NC, .bfs_p
LD (DE), A
INC DE
INC B
JP .bfs_p
.bfs_pe
PUSH BC             ; 指数: [-]数字
LD C, 0
LD A, (HL)
CP '-'
JP NZ, .bfs_pe1
INC C
INC HL
.bfs_pe1
LD B, 0
.bfs_pe2
LD A, (HL)
SUB '0'
JP C, .bfs_pe3
CP 10
JP NC, .bfs_pe3
LD E, A
LD A, B
ADD A, A
LD D, A
ADD A, A
ADD A, A
ADD A, D
ADD A, E
LD B, A
INC HL
JP .bfs_pe2
.bfs_pe3
LD A, B
DEC C
JP NZ, .bfs_pe4
NEG
.bfs_pe4
LD (.bfs_n), A
POP BC
.bfs_pend
LD A, C             ; X = 小数点の前の数字の数 + 指数
CP $FF
JP NZ, .bfs_x1
LD A, B
.bfs_x1
LD HL, .bfs_n
ADD A, (HL)
LD (.bfs_x), A
LD HL, .bfs_dig     ; 頭の 0 を除く (1 つごとに X を 1 減らす)
.bfs_lz
LD A, B
OR A
JP Z, .bfs_tz
LD A, (HL)
CP '0'
JP NZ, .bfs_tz
INC HL
DEC B
LD A, (.bfs_x)
DEC A
LD (.bfs_x), A
JP .bfs_lz
.bfs_tz
LD A, B             ; 後ろの 0 を除く
OR A
JP Z, .bfs_fmt
PUSH HL
LD E, B
LD D, 0
ADD HL, DE
DEC HL
LD A, (HL)
POP HL
CP '0'
JP NZ, .bfs_fmt
DEC B
JP .bfs_tz
.bfs_fmt
LD A, B
OR A
JP Z, .bfs_zero
LD DE, (.bfs_dst)
LD A, (.bfs_x)
BIT 7, A
JP NZ, .bfs_neg
CP 9
JP NC, .bfs_exp
LD C, A             ; 0 <= X <= 8: 小数点の前に X 桁 (数字が尽きたら 0)
OR A
JP Z, .bfs_pt
.bfs_ip
LD A, B
OR A
JP Z, .bfs_ipz
LD A, (HL)
INC HL
DEC B
JP .bfs_ipw
.bfs_ipz
LD A, '0'
.bfs_ipw
LD (DE), A
INC DE
DEC C
JP NZ, .bfs_ip
LD A, B
OR A
JP Z, .bfs_end
.bfs_pt
LD A, '.'
LD (DE), A
INC DE
.bfs_rest
LD A, B
OR A
JP Z, .bfs_end
LD A, (HL)
INC HL
LD (DE), A
INC DE
DEC B
JP .bfs_rest
.bfs_neg
NEG                 ; X < 0: 0 の数 (-X) と数字の数の合計が 8 以内なら .000ddd
LD C, A
ADD A, B
CP 9
JP NC, .bfs_exp
LD A, '.'
LD (DE), A
INC DE
.bfs_nz
LD A, '0'
LD (DE), A
INC DE
DEC C
JP NZ, .bfs_nz
JP .bfs_rest
.bfs_exp
LD A, (HL)          ; d.dddE±nn (nn = X - 1)
INC HL
LD (DE), A
INC DE
DEC B
LD A, B
OR A
JP Z, .bfs_e
LD A, '.'
LD (DE), A
INC DE
.bfs_e1
LD A, B
OR A
JP Z, .bfs_e
LD A, (HL)
INC HL
LD (DE), A
INC DE
DEC B
JP .bfs_e1
.bfs_e
LD A, 'E'
LD (DE), A
INC DE
LD A, (.bfs_x)
DEC A
LD C, '+'
BIT 7, A
JP Z, .bfs_e2
NEG
LD C, '-'
.bfs_e2
LD B, A
LD A, C
LD (DE), A
INC DE
LD A, B
LD C, '0' - 1
.bfs_e3
INC C
SUB 10
JP NC, .bfs_e3
ADD A, 10 + '0'
LD B, A
LD A, C
LD (DE), A
INC DE
LD A, B
LD (DE), A
INC DE
.bfs_end
XOR A
LD (DE), A
LD HL, .bfs_out
RET
.bfs_zero
LD HL, .bfs_out
LD (HL), ' '
INC HL
LD (HL), '0'
INC HL
LD (HL), 0
LD HL, .bfs_out
RET
.bfs_ovf
LD HL, 6
JP BAS_ERROR
.bfs_hl DW 0
.bfs_a DB 0
.bfs_dst DW 0
.bfs_n DB 0
.bfs_x DB 0
.bfs_q DB 0, 0, 0, 0
.bfs_raw DB 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
.bfs_dig DB 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
.bfs_out DB 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0


; @name BAS_SFSTR
; @resident shared
; @param_count 1
; @calls BAS_FSTR,BAS_TALLOC
; AHL = 浮動小数 → 一時領域の文字列 (STR$。PRINT の形から後ろの空白を除いたもの: 頭に符号の桁)
CALL BAS_FSTR       ; HL = 0 終端の文字列
PUSH HL
LD B, 0
.bsf_len
LD A, (HL)
OR A
JP Z, .bsf_lenok
INC B
INC HL
JP .bsf_len
.bsf_lenok
LD A, B
CALL BAS_TALLOC     ; HL = [長さ][本体]
POP DE              ; DE = 0 終端の文字列
PUSH HL
INC HL
EX DE, HL
LD C, B
LD B, 0
LDIR
POP HL
RET


; @name BAS_PFLT
; @resident shared
; @param_count 1
; @calls BAS_FSTR,BAS_PSTRZ,BAS_PCHR
; AHL = 浮動小数を PRINT の形で出す (符号の桁 + 数 + 後ろに空白 1 つ。整数の BAS_PNUM と同じ並び)
CALL BAS_FSTR
CALL BAS_PSTRZ
LD L, ' '
JP BAS_PCHR


; ============================================================ 文字列と画面 (段階 F1b)

; @name BAS_SSTR
; @resident shared
; @param_count 2
; @calls BAS_TALLOC,BAS_ERROR
; HL = 回数 n、DE = 文字列 s → s を n 回つなげたもの (STRING$。s は全体を繰り返す)。
; n が 0〜255 の外、s が空、結果が 255 バイトを超えるとエラー 5 (HuBASIC の実測)
LD A, H
OR A
JP NZ, .bss_err
LD A, (DE)
OR A
JP Z, .bss_err
LD (.bss_s), DE
LD (.bss_n), HL
LD C, A
LD B, L
LD HL, 0
LD D, 0
LD E, C
INC B
DEC B
JP Z, .bss_md
.bss_ml
ADD HL, DE
DJNZ .bss_ml
.bss_md
LD A, H
OR A
JP NZ, .bss_err
LD A, L
CALL BAS_TALLOC         ; HL = 結果 (長さは書き込み済み)
PUSH HL
INC HL
EX DE, HL               ; DE = 書き込み先
LD A, (.bss_n)
LD B, A
INC B
DEC B
JP Z, .bss_done
.bss_rep
PUSH BC
LD HL, (.bss_s)
LD C, (HL)
LD B, 0
INC HL
LDIR
POP BC
DJNZ .bss_rep
.bss_done
POP HL
RET
.bss_err
LD HL, 5
JP BAS_ERROR
.bss_s DW 0
.bss_n DW 0


; @name BAS_SCHARA
; @resident shared
; @param_count 2
; @calls BAS_WORK,BAS_VOFF,BAS_TALLOC,BAS_ERROR
; HL = x、DE = y → 画面の (x, y) の文字 1 文字 (CHARACTER$。テキスト VRAM を読む)。範囲外はエラー 5
LD A, H
OR D
JP NZ, .bsa_err
LD A, (BAS_WID)
DEC A
CP L
JP C, .bsa_err
LD A, E
CP 25
JP NC, .bsa_err
LD H, E                 ; H = y、L = x
CALL BAS_VOFF
LD A, H
AND $07
OR $30
LD B, A
LD C, L
IN E, (C)               ; テキスト
LD A, 1
CALL BAS_TALLOC
INC HL
LD (HL), E
DEC HL
RET
.bsa_err
LD HL, 5
JP BAS_ERROR


; @name BAS_SSCRN
; @resident shared
; @param_count 3
; @calls BAS_WORK,BAS_VOFF,BAS_TALLOC,BAS_ERROR
; HL = x、DE = y、BC = n → 画面の (x, y) から n 文字 (SCRN$。テキスト VRAM を画面の順に読む。右端の次は次の行の左端、
; 画面の最後の先は空白)。x が 0〜桁数-1、y が 0〜24、n が 0〜255 の外はエラー 5 (HuBASIC の実測)
LD A, H
OR D
OR B
JP NZ, .bsr_err
LD A, (BAS_WID)
DEC A
CP L
JP C, .bsr_err
LD A, E
CP 25
JP NC, .bsr_err
PUSH BC
LD H, E                 ; H = y、L = x
CALL BAS_VOFF           ; HL = 位置 (BC は壊れる)
EX DE, HL               ; DE = 位置
LD HL, 1000             ; 画面の文字の数 (40 桁)
LD A, (BAS_WID)
CP 80
JP NZ, .bsr_40
LD HL, 2000
.bsr_40
OR A
SBC HL, DE
LD (.bsr_rem), HL       ; 画面の最後までの文字の数
POP BC
LD A, C
CALL BAS_TALLOC         ; HL = 結果 (DE・BC は残る)
PUSH HL
INC HL
LD B, C
.bsr_lp
LD A, B
OR A
JP Z, .bsr_done
PUSH BC
LD BC, (.bsr_rem)
LD A, B
OR C
LD A, $20               ; 画面の最後の先は空白
JP Z, .bsr_put
DEC BC
LD (.bsr_rem), BC
LD A, D
AND $07
OR $30
LD B, A
LD C, E
IN A, (C)               ; テキスト
.bsr_put
LD (HL), A
INC HL
INC DE
POP BC
DEC B
JP .bsr_lp
.bsr_done
POP HL
RET
.bsr_err
LD HL, 5
JP BAS_ERROR
.bsr_rem DW 0


; @name BAS_SMIDSET
; @resident shared
; @calls BAS_ERROR
; @stack_cleanup callee
; スタック引数 (変数の文字列, 位置 p, 長さ m, 文字列 s)。MID$ の文: 変数の p 文字目から、m・s の長さ・変数の残りの
; 長さのうち いちばん短い数だけを s で置き換える (変数の長さは変えない)。p が 1〜255 の外、m が 0〜255 の外は
; エラー 5。p が変数の長さより大きければ何もしない (HuBASIC の実測)
POP HL
LD (.bms_ret), HL
POP HL
LD (.bms_s), HL
POP HL
LD (.bms_m), HL
POP HL
LD (.bms_p), HL
POP HL
LD (.bms_v), HL
LD HL, (.bms_ret)
PUSH HL
LD HL, (.bms_p)
LD A, H
OR A
JP NZ, .bms_err
LD A, L
OR A
JP Z, .bms_err
LD HL, (.bms_m)
LD A, H
OR A
JP NZ, .bms_err
LD HL, (.bms_v)
LD A, (HL)              ; 変数の長さ
LD HL, .bms_p
SUB (HL)
RET C                   ; p が長さより大きい: 何もしない
INC A                   ; 残りの長さ
LD B, A
LD A, (.bms_m)
CP B
JP NC, .bms_1
LD B, A
.bms_1
LD HL, (.bms_s)
LD A, (HL)
CP B
JP NC, .bms_2
LD B, A
.bms_2
LD A, B
OR A
RET Z
LD C, B
LD B, 0                 ; BC = 置き換える数
LD HL, (.bms_v)
LD A, (.bms_p)
LD E, A
LD D, 0
ADD HL, DE              ; HL = p 文字目
EX DE, HL               ; DE = 書き込み先
LD HL, (.bms_s)
INC HL                  ; HL = 写す元
; いつも前から写す。元と先が同じ変数で重なるとき (MID$(A$,2)=A$) は、書いた文字をまた読むことになるが、
; HuBASIC も同じ (実測: A$="ABCDEF":MID$(A$,2)=A$ は AAAAAA)
LDIR
RET
.bms_err
LD HL, 5
JP BAS_ERROR
.bms_ret DW 0
.bms_s DW 0
.bms_m DW 0
.bms_p DW 0
.bms_v DW 0


; ============================================================ 入力と音 (段階 F1c)

; @name BAS_SINKEY0
; @resident shared
; @param_count 0
; @calls sGETKY,BAS_TALLOC
; INKEY$(0): いま押されているキーの文字 (押している間ずっと返す。なければ空文字列)
CALL sGETKY
LD E, A
OR A
JP Z, BAS_TALLOC
LD A, 1
CALL BAS_TALLOC
INC HL
LD (HL), E
DEC HL
RET


; @name BAS_SINPUT
; @resident shared
; @param_count 1
; @calls BAS_KEYIN,BAS_TALLOC,BAS_ERROR
; HL = 文字数 n → キーを n 個待って読んだ文字列 (INPUT$。画面には出さない。RETURN なども 1 文字として読む)。
; n が 0〜255 の外はエラー 5
LD A, H
OR A
JP NZ, .bsi_err
LD A, L
LD (.bsi_n), A
CALL BAS_TALLOC
LD (.bsi_res), HL
LD A, (.bsi_n)
OR A
RET Z
LD B, A
INC HL
.bsi_loop
PUSH BC
PUSH HL
.bsi_wait
CALL BAS_KEYIN
OR A
JP Z, .bsi_wait
POP HL
LD (HL), A
INC HL
POP BC
DJNZ .bsi_loop
LD HL, (.bsi_res)
RET
.bsi_err
LD HL, 5
JP BAS_ERROR
.bsi_n DB 0
.bsi_res DW 0


; @name BAS_INPUT
; @resident shared
; @param_count 2
; @calls BAS_WORK,BAS_PSTR,BAS_PSTRZ,BAS_PCHR,BAS_KEYIN,BAS_NEWLINE,BAS_VOFF,BAS_VPUT
; HL = 案内の文字列 (0 なら出さない)、DE = 1 なら「? 」を出す。キーを 1 行分読んで (画面に出しながら。BS で 1 文字消す、
; RETURN で終わり) BAS_INBUF に入れ、次の項目の位置を先頭にする。項目は BAS_INFS / BAS_INFN で取り出す
LD A, H
OR L
JP Z, .bin_np
PUSH DE
CALL BAS_PSTR
POP DE
.bin_np
LD A, E
OR A
JP Z, .bin_rd
LD HL, .bin_q
CALL BAS_PSTRZ
.bin_rd
XOR A
LD (BAS_INBUF), A
.bin_key
CALL BAS_KEYIN
OR A
JP Z, .bin_key
CP $0D
JP Z, .bin_end
CP $08
JP Z, .bin_bs
CP $20
JP C, .bin_key          ; ほかの制御コードは無視
LD E, A
LD HL, BAS_INBUF
LD A, (HL)
CP 255
JP Z, .bin_key          ; いっぱい
INC (HL)
LD C, A
LD B, 0
INC HL
ADD HL, BC
LD (HL), E
LD L, E
CALL BAS_PCHR
JP .bin_key
.bin_bs
LD HL, BAS_INBUF
LD A, (HL)
OR A
JP Z, .bin_key
DEC (HL)
LD A, (BAS_CX)          ; カーソルを 1 つ戻して、そこを空白にする (窓の左端なら前の行の窓の右端へ)
LD HL, BAS_WX0
CP (HL)
JP NZ, .bin_bs1
LD A, (BAS_CY)
LD HL, BAS_WY0
CP (HL)
JP Z, .bin_key          ; 窓の左上
DEC A
LD (BAS_CY), A
LD A, (BAS_WX1)
INC A
.bin_bs1
DEC A
LD (BAS_CX), A
LD HL, (BAS_CX)
CALL BAS_VOFF
LD E, ' '
LD A, (BAS_ATTR)
LD D, A
CALL BAS_VPUT
JP .bin_key
.bin_end
CALL BAS_NEWLINE
LD HL, 1
LD (BAS_INPOS), HL
RET
.bin_q DB "? ", 0


; @name BAS_INFS
; @resident shared
; @param_count 0
; @calls BAS_WORK,BAS_TALLOC
; INPUT で読んだ行の次の項目 → 一時文字列。項目はカンマで区切り、頭と後ろの空白を除く。" で始まれば次の " までを
; そのまま (カンマを含めてよい)。項目が尽きていれば空文字列 (HuBASIC は聞き直さない。実測)
LD A, (BAS_INBUF)
LD E, A
LD D, 0
LD HL, BAS_INBUF
ADD HL, DE
LD (.bif_end), HL       ; 最後の文字の番地
LD DE, (BAS_INPOS)
LD HL, BAS_INBUF
ADD HL, DE              ; HL = 次の文字の番地
LD (.bif_s), HL
.bif_sp
CALL .bif_past
JP C, .bif_none
LD A, (HL)
CP ' '
JP NZ, .bif_st
INC HL
JP .bif_sp
.bif_st
CP '"'
JP Z, .bif_quote
LD (.bif_s), HL
.bif_sc
CALL .bif_past
JP C, .bif_cut
LD A, (HL)
CP ','
JP Z, .bif_cut
INC HL
JP .bif_sc
.bif_cut
PUSH HL                 ; 区切り (カンマか末尾の次)
DEC HL
.bif_tr
LD DE, (.bif_s)
PUSH HL
OR A
SBC HL, DE
POP HL
JP C, .bif_trd          ; 項目が空になった
LD A, (HL)
CP ' '
JP NZ, .bif_trd
DEC HL
JP .bif_tr
.bif_trd
INC HL
LD DE, (.bif_s)
OR A
SBC HL, DE
LD A, L                 ; 長さ
POP HL
PUSH AF
CALL .bif_next
POP AF
JP .bif_make
.bif_quote
INC HL
LD (.bif_s), HL
.bif_qs
CALL .bif_past
JP C, .bif_qe
LD A, (HL)
CP '"'
JP Z, .bif_qe
INC HL
JP .bif_qs
.bif_qe
PUSH HL
LD DE, (.bif_s)
OR A
SBC HL, DE
LD A, L
POP HL
PUSH AF
.bif_qk                 ; 閉じの " の後ろは、次のカンマまで捨てる
CALL .bif_past
JP C, .bif_qk2
LD A, (HL)
CP ','
JP Z, .bif_qk2
INC HL
JP .bif_qk
.bif_qk2
CALL .bif_next
POP AF
JP .bif_make
.bif_none
CALL .bif_next
XOR A
.bif_make
LD C, A                 ; A = 長さ、(.bif_s) = 始まり → 一時文字列に写す
CALL BAS_TALLOC
PUSH HL
LD A, C
OR A
JP Z, .bif_mk1
INC HL
EX DE, HL
LD HL, (.bif_s)
LD B, 0
LDIR
.bif_mk1
POP HL
RET
.bif_past               ; HL が最後の文字を越えたか → キャリー
PUSH DE
EX DE, HL
LD HL, (.bif_end)
OR A
SBC HL, DE
EX DE, HL
POP DE
RET
.bif_next               ; 区切りの番地 HL の次を、次の項目の始まりにする
LD DE, BAS_INBUF
OR A
SBC HL, DE
INC HL
LD (BAS_INPOS), HL
RET
.bif_end DW 0
.bif_s DW 0


; @name BAS_INFN
; @resident shared
; @param_count 0
; @result_type float
; @calls BAS_INFS,BAS_SVALF
; INPUT で読んだ行の次の項目を数として → AHL (VAL と同じ読み方。読めなければ 0。HuBASIC は聞き直さない。実測)
CALL BAS_INFS
JP BAS_SVALF


; @name BAS_READF
; @resident shared
; @param_count 0
; @result_type float
; @calls BAS_READS,BAS_SVALF
; 次の DATA の項目を数として読む → AHL (VAL と同じ読み方。読めなければ 0、空の項目も 0。HuBASIC の実測:
; DATA 12X は 12、DATA ABC は 0。読み尽くしたらエラー 4)
CALL BAS_READS
JP BAS_SVALF


; @name BAS_SVALF
; @resident shared
; @param_count 1
; @result_type float
; @calls BAS_SVAL,i16tof24,f24mul,f24div,f24pow10_LUT
; HL = 文字列 → AHL = 数として読めた所までの値 (VAL と同じ読み方で、小数点と指数 E・D も読む。読めなければ 0)。
; 有効数字は 9 桁まで (それより下の桁は捨てる)。&H・&O・&B は整数の VAL (BAS_SVAL) と同じ。
; 10 進の数の中の空白は、指数の E・D より前なら読み飛ばす (HuBASIC の実測: "1 2" は 12、" - 1" は -1、"1. 5" は 1.5、
; "1 E2" は 100)。指数の中の空白は読み飛ばさず、そこで止める ("1E 2" は 1、"1E -2" は 1、"1E2 3" は 100)
LD (.bsv_str), HL
LD B, (HL)              ; B = 残りの文字数
INC HL
XOR A
LD (.bsv_neg), A
LD (.bsv_nd), A
LD (.bsv_eneg), A
LD (.bsv_m), A
LD (.bsv_m + 1), A
LD (.bsv_m + 2), A
LD (.bsv_m + 3), A
LD DE, 0
LD (.bsv_x), DE
LD (.bsv_e), DE
.bsv_sp
LD A, B
OR A
JP Z, .bsv_conv
LD A, (HL)
CP ' '
JP NZ, .bsv_sg
INC HL
DEC B
JP .bsv_sp
.bsv_sg
CP '&'
JP Z, .bsv_radix
CP '+'
JP Z, .bsv_sg1
CP '-'
JP NZ, .bsv_int
LD A, 1
LD (.bsv_neg), A
.bsv_sg1
INC HL
DEC B
LD A, B                 ; 符号の後の &H・&O・&B も、整数の VAL に任せる (-&H10 は -16。HuBASIC の実測)
OR A
JP Z, .bsv_conv
LD A, (HL)
CP '&'
JP Z, .bsv_radix
.bsv_int                ; 整数部
LD A, B
OR A
JP Z, .bsv_conv
LD A, (HL)
CP ' '
JP Z, .bsv_isp
CP '.'
JP Z, .bsv_dot
CALL .bsv_isexp
JP Z, .bsv_exp
SUB '0'
JP C, .bsv_conv
CP 10
JP NC, .bsv_conv
CALL .bsv_add           ; キャリー = 桁を捨てた (その分 10 倍)
JP NC, .bsv_i1
PUSH HL
LD HL, (.bsv_x)
INC HL
LD (.bsv_x), HL
POP HL
.bsv_i1
.bsv_isp
INC HL
DEC B
JP .bsv_int
.bsv_dot
INC HL
DEC B
.bsv_frac               ; 小数部
LD A, B
OR A
JP Z, .bsv_conv
LD A, (HL)
CP ' '
JP Z, .bsv_f1
CALL .bsv_isexp
JP Z, .bsv_exp
SUB '0'
JP C, .bsv_conv
CP 10
JP NC, .bsv_conv
CALL .bsv_add
JP C, .bsv_f1
PUSH HL
LD HL, (.bsv_x)
DEC HL
LD (.bsv_x), HL
POP HL
.bsv_f1
INC HL
DEC B
JP .bsv_frac
.bsv_exp                ; 指数: [+-]数字
INC HL
DEC B
LD A, B
OR A
JP Z, .bsv_conv
LD A, (HL)
CP '+'
JP Z, .bsv_e0
CP '-'
JP NZ, .bsv_e1
LD A, 1
LD (.bsv_eneg), A
.bsv_e0
INC HL
DEC B
.bsv_e1
LD A, B
OR A
JP Z, .bsv_conv
LD A, (HL)
SUB '0'
JP C, .bsv_conv
CP 10
JP NC, .bsv_conv
PUSH HL
LD C, A
LD HL, (.bsv_e)
LD D, H
LD E, L
ADD HL, HL
ADD HL, HL
ADD HL, DE
ADD HL, HL              ; × 10
LD E, C
LD D, 0
ADD HL, DE
LD A, H
CP 4
JP C, .bsv_e2
LD HL, 999              ; 大きすぎる指数は 999 で止める
.bsv_e2
LD (.bsv_e), HL
POP HL
INC HL
DEC B
JP .bsv_e1
.bsv_radix
LD HL, (.bsv_str)
CALL BAS_SVAL
JP i16tof24
.bsv_conv               ; 仮数 M (32 ビット) → f24 (上位 × 65536 + 下位)
LD HL, (.bsv_m)
LD DE, (.bsv_m + 2)
LD A, H
OR L
OR D
OR E
JP NZ, .bsv_c1
XOR A
LD H, A
LD L, A
RET
.bsv_c1
; 後ろの 0 を除く (M が 10 で割り切れる間 M を 10 で割り、桁の補正を 1 増やす)。0.50000 を 5 × 10^-1 にして、
; 10 の累乗で割る回数と誤差を減らす
.bsv_tz
CALL .bsv_d10
OR A
JP NZ, .bsv_tzd
LD HL, (.bsv_t)
LD (.bsv_m), HL
LD HL, (.bsv_t + 2)
LD (.bsv_m + 2), HL
LD HL, (.bsv_x)
INC HL
LD (.bsv_x), HL
JP .bsv_tz
.bsv_tzd
LD HL, (.bsv_e)         ; 10 進の指数 X = 桁の補正 ± 指数
LD A, (.bsv_eneg)
OR A
JP Z, .bsv_x1
EX DE, HL
LD HL, 0
OR A
SBC HL, DE
.bsv_x1
LD DE, (.bsv_x)
ADD HL, DE
LD (.bsv_xx), HL
; ここから f24 にするまでを整数で正確に計算し、丸めは最後の 1 回だけにする (SLANG が定数を f24 に直すのと同じ値):
; X > 0 なら、M が 32 ビットに収まる間 M を 10 倍する。X < 0 なら D = 10^k (k = -X、9 まで) で割る。
; 商 Q = M / D に、余りから下のビットを足して 18 ビットにそろえ、最後のビットで四捨五入する
.bsv_pm
LD HL, (.bsv_xx)
BIT 7, H
JP NZ, .bsv_dset
LD A, H
OR L
JP Z, .bsv_dset
LD A, (.bsv_m + 3)
CP $19                  ; M × 10 が 32 ビットに収まるか (M < $19000000 なら収まる)
JP NC, .bsv_dset
CALL .bsv_m10
LD (.bsv_m), HL
LD (.bsv_m + 2), DE
LD HL, (.bsv_xx)
DEC HL
LD (.bsv_xx), HL
JP .bsv_pm
.bsv_dset
LD HL, (.bsv_xx)        ; k = X が負なら min(-X, 9)、そうでなければ 0
LD A, 0
BIT 7, H
JP Z, .bsv_dk
EX DE, HL
LD HL, 0
OR A
SBC HL, DE              ; HL = -X
LD A, H
OR A
LD A, 9
JP NZ, .bsv_dk
LD A, L
CP 10
JP C, .bsv_dk
LD A, 9
.bsv_dk
LD (.bsv_k), A
LD E, A                 ; X += k
LD D, 0
LD HL, (.bsv_xx)
ADD HL, DE
LD (.bsv_xx), HL
LD HL, .bsv_p10         ; D = 10^k (表から)
ADD HL, DE
ADD HL, DE
ADD HL, DE
ADD HL, DE
LD DE, .bsv_d
LD BC, 4
LDIR
CALL .bsv_div32         ; Q = M / D、R = M % D
LD HL, 0
LD (.bsv_be), HL        ; 2 の指数
.bsv_nbig               ; Q が 2^18 以上なら右へ送る (落ちるビットは四捨五入に使わない)
LD A, (.bsv_q + 3)
OR A
JP NZ, .bsv_nr
LD A, (.bsv_q + 2)
AND $FC
JP Z, .bsv_nsm
.bsv_nr
LD HL, .bsv_q + 3
SRL (HL)
DEC HL
RR (HL)
DEC HL
RR (HL)
DEC HL
RR (HL)
LD HL, (.bsv_be)
INC HL
LD (.bsv_be), HL
JP .bsv_nbig
.bsv_nsm                ; Q が 2^17 未満なら、余りから商の次のビットを足しながら左へ送る
LD A, (.bsv_q + 2)
AND $02
JP NZ, .bsv_nok
LD HL, .bsv_r
SLA (HL)
INC HL
RL (HL)
INC HL
RL (HL)
INC HL
RL (HL)
LD HL, .bsv_q
SLA (HL)
INC HL
RL (HL)
INC HL
RL (HL)
INC HL
RL (HL)
CALL .bsv_rsub          ; R >= D なら R -= D して Q の最下位を 1 に
LD HL, (.bsv_be)
DEC HL
LD (.bsv_be), HL
JP .bsv_nsm
.bsv_nok                ; Q = 18 ビット。仮数 = Q >> 1 の下位 16 ビット、Q の最下位が 1 なら切り上げ
LD A, (.bsv_q)
AND 1
LD C, A
LD HL, .bsv_q + 2
SRL (HL)
DEC HL
RR (HL)
DEC HL
RR (HL)
LD HL, (.bsv_be)
LD DE, 63 + 17
ADD HL, DE
EX DE, HL               ; DE = 指数 (偏り込み)
LD HL, (.bsv_q)
LD A, C
OR A
JP Z, .bsv_nr0
INC HL
LD A, H
OR L
JP NZ, .bsv_nr0
INC DE                  ; 仮数が繰り上がった
.bsv_nr0
BIT 7, D
JP NZ, .bsv_under
LD A, D
OR A
JP NZ, .bsv_over
LD A, E
OR A
JP Z, .bsv_under
CP 127
JP NC, .bsv_over
LD (.bsv_f), HL
LD (.bsv_f + 2), A
JP .bsv_sc
.bsv_over
LD HL, 0
LD (.bsv_f), HL
LD A, $7F
LD (.bsv_f + 2), A
JP .bsv_sc
.bsv_under
LD HL, 0
LD (.bsv_f), HL
XOR A
LD (.bsv_f + 2), A
.bsv_sc                 ; 10^X を、表 (1e19〜1e-18) の値を掛けて合わせる
LD HL, (.bsv_xx)
LD A, H
OR L
JP Z, .bsv_sgn
BIT 7, H
JP NZ, .bsv_scn
LD DE, 20
OR A
SBC HL, DE
JP C, .bsv_scp          ; X <= 19
LD HL, (.bsv_xx)
LD DE, -19
ADD HL, DE
LD (.bsv_xx), HL
LD DE, 19
CALL .bsv_mulp
JP .bsv_sc
.bsv_scp
LD DE, (.bsv_xx)
LD HL, 0
LD (.bsv_xx), HL
CALL .bsv_mulp
JP .bsv_sgn
.bsv_scn                ; X < 0: f24 で正確に表せる 10^k (k = 1〜6) で割る (10^-k を掛けると誤差が出る)
EX DE, HL
LD HL, 0
OR A
SBC HL, DE              ; HL = -X
PUSH HL
LD DE, 6
OR A
SBC HL, DE
POP HL
JP C, .bsv_sk           ; -X < 6 なら k = -X
LD HL, 6
.bsv_sk
PUSH HL
LD DE, (.bsv_xx)
ADD HL, DE
LD (.bsv_xx), HL        ; X += k
POP DE
CALL .bsv_divp
JP .bsv_sc
.bsv_sgn
LD HL, (.bsv_f)
LD A, (.bsv_f + 2)
LD C, A
LD A, (.bsv_neg)
OR A
LD A, C
RET Z
AND $7F
RET Z                   ; 0 には符号を付けない
OR $80
RET
.bsv_mulp               ; (.bsv_f) ×= 10^k (DE = k、-18〜19)
LD HL, 19
OR A
SBC HL, DE              ; 表の何番目か (0〜37)
LD D, H
LD E, L
ADD HL, HL
ADD HL, DE              ; × 3 バイト
LD DE, f24pow10_LUT
ADD HL, DE
LD E, (HL)
INC HL
LD D, (HL)
INC HL
LD C, (HL)
LD HL, (.bsv_f)
LD A, (.bsv_f + 2)
CALL f24mul
LD (.bsv_f), HL
LD (.bsv_f + 2), A
RET
.bsv_divp               ; (.bsv_f) /= 10^k (DE = k、1〜6。表の値は正確)
LD HL, 19
OR A
SBC HL, DE
LD D, H
LD E, L
ADD HL, HL
ADD HL, DE
LD DE, f24pow10_LUT
ADD HL, DE
LD E, (HL)
INC HL
LD D, (HL)
INC HL
LD C, (HL)
LD HL, (.bsv_f)
LD A, (.bsv_f + 2)
CALL f24div
LD (.bsv_f), HL
LD (.bsv_f + 2), A
RET
.bsv_d10                ; .bsv_t = M / 10、A = 余り (M は変えない)
LD HL, (.bsv_m)
LD (.bsv_t), HL
LD HL, (.bsv_m + 2)
LD (.bsv_t + 2), HL
XOR A
LD B, 32
.bsv_dv
LD HL, .bsv_t
SLA (HL)
INC HL
RL (HL)
INC HL
RL (HL)
INC HL
RL (HL)
RLA
CP 10
JP C, .bsv_dvn
SUB 10
LD HL, .bsv_t
INC (HL)
.bsv_dvn
DJNZ .bsv_dv
RET
.bsv_div32              ; Q = M / D、R = M % D (32 ビット)
LD HL, (.bsv_m)
LD (.bsv_q), HL
LD HL, (.bsv_m + 2)
LD (.bsv_q + 2), HL
LD HL, 0
LD (.bsv_r), HL
LD (.bsv_r + 2), HL
LD B, 32
.bsv_dl
LD HL, .bsv_q           ; Q:R を左へ (Q の最上位ビットが R の最下位へ)
SLA (HL)
INC HL
RL (HL)
INC HL
RL (HL)
INC HL
RL (HL)
LD HL, .bsv_r
RL (HL)
INC HL
RL (HL)
INC HL
RL (HL)
INC HL
RL (HL)
PUSH BC
CALL .bsv_rsub
POP BC
DJNZ .bsv_dl
RET
.bsv_rsub               ; R >= D なら R -= D、Q の最下位を 1 に
LD HL, (.bsv_r)
LD DE, (.bsv_d)
OR A
SBC HL, DE
PUSH HL
LD HL, (.bsv_r + 2)
LD DE, (.bsv_d + 2)
SBC HL, DE
JP C, .bsv_rno
LD (.bsv_r + 2), HL
POP HL
LD (.bsv_r), HL
LD HL, .bsv_q
INC (HL)
RET
.bsv_rno
POP HL
RET
.bsv_m10                ; DE:HL = M × 10 (M は変えない。.bsv_t を使う)
LD HL, (.bsv_m)
LD DE, (.bsv_m + 2)
ADD HL, HL
EX DE, HL
ADC HL, HL
EX DE, HL               ; × 2
LD (.bsv_t), HL
LD (.bsv_t + 2), DE
ADD HL, HL
EX DE, HL
ADC HL, HL
EX DE, HL               ; × 4
ADD HL, HL
EX DE, HL
ADC HL, HL
EX DE, HL               ; × 8
PUSH BC
LD BC, (.bsv_t)
ADD HL, BC
EX DE, HL
LD BC, (.bsv_t + 2)
ADC HL, BC
EX DE, HL               ; × 10
POP BC
RET
.bsv_isexp              ; A が指数の文字 (E e D d) なら Z
CP 'E'
RET Z
CP 'e'
RET Z
CP 'D'
RET Z
CP 'd'
RET
.bsv_add                ; M = M × 10 + A。有効数字が 9 桁あれば加えずにキャリー (B・HL は壊さない)
LD C, A
LD A, (.bsv_nd)
CP 9
JP NC, .bsv_drop
PUSH HL
PUSH BC
CALL .bsv_m10           ; DE:HL = M × 10
POP BC
PUSH BC
LD B, 0
ADD HL, BC              ; + 数字 (C)
EX DE, HL
LD BC, 0
ADC HL, BC
EX DE, HL
LD (.bsv_m), HL
LD (.bsv_m + 2), DE
LD A, H
OR L
OR D
OR E
JP Z, .bsv_a0           ; まだ 0 (頭の 0) なら有効数字に数えない
LD A, (.bsv_nd)
INC A
LD (.bsv_nd), A
.bsv_a0
POP BC
POP HL
OR A
RET
.bsv_drop
SCF
RET
.bsv_str DW 0
.bsv_neg DB 0
.bsv_nd DB 0
.bsv_eneg DB 0
.bsv_m DB 0, 0, 0, 0
.bsv_t DB 0, 0, 0, 0
.bsv_f DB 0, 0, 0
.bsv_x DW 0
.bsv_e DW 0
.bsv_xx DW 0
.bsv_k DB 0
.bsv_be DW 0
.bsv_d DB 0, 0, 0, 0
.bsv_q DB 0, 0, 0, 0
.bsv_r DB 0, 0, 0, 0
.bsv_p10                ; 10^0〜10^9 (32 ビット)
DB $01, $00, $00, $00
DB $0A, $00, $00, $00
DB $64, $00, $00, $00
DB $E8, $03, $00, $00
DB $10, $27, $00, $00
DB $A0, $86, $01, $00
DB $40, $42, $0F, $00
DB $80, $96, $98, $00
DB $00, $E1, $F5, $05
DB $00, $CA, $9A, $3B


; @name BAS_SOUND
; @resident shared
; @param_count 2
; @calls BAS_PSGW,BAS_ERROR
; HL = PSG のレジスタ (0〜13)、DE = 値 (0〜255) を書く (SOUND)。範囲外はエラー 5。PLAY の状態とは連動しない
LD A, H
OR D
JP NZ, .bsd_err
LD A, L
CP 14
JP NC, .bsd_err
JP BAS_PSGW
.bsd_err
LD HL, 5
JP BAS_ERROR


; @name BAS_BEEPN
; @resident shared
; @param_count 1
; @calls BAS_PSGW,BAS_ERROR
; HL = 1: チャンネル C で BEEP の音 (音程の値 190・音量 15) を鳴らし始める、0: チャンネル A・B・C の音量を 0 にして
; 止める (BEEP 1 / BEEP 0)。それ以外はエラー 5
LD A, H
OR A
JP NZ, .bbn_err
LD A, L
OR A
JP Z, .bbn_off
DEC A
JP NZ, .bbn_err
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
JP BAS_PSGW
.bbn_off
LD E, 0
LD A, 8
CALL BAS_PSGW
LD A, 9
CALL BAS_PSGW
LD A, 10
JP BAS_PSGW
.bbn_err
LD HL, 5
JP BAS_ERROR


; @name BAS_SALLOC
; @resident shared
; @param_count 1
; @calls BAS_TALLOC
; HL = 長さ (0〜255) → その長さの一時文字列 (中身は後から BAS_SPUTC で書く。CHR$(a, b, …) 用)
LD A, L
JP BAS_TALLOC


; @name BAS_SPUTC
; @resident shared
; @param_count 3
; @calls BAS_ERROR
; HL = 文字列、DE = 位置 (1 始まり)、BC = 文字コード (0〜255、それ以外はエラー 5) → その位置に書く
LD A, B
OR A
JP NZ, .bpc_err
ADD HL, DE
LD (HL), C
RET
.bpc_err
LD HL, 5
JP BAS_ERROR



; @name BAS_BYTE
; @resident shared
; @param_count 1
; @calls BAS_ERROR
; HL = POKE・POKE@ の値。0〜255 ならそのまま返す。それ以外はエラー 5 (HuBASIC と同じ Illegal function call)
LD A, H
OR A
RET Z
LD HL, 5
JP BAS_ERROR


; @name BAS_CALL
; @resident shared
; @param_count 1
; HL = 番地。IX・IY を保存して、その番地を CALL する (CALL 文。マシン語は RET で戻る)。割り込みの状態は変えない
PUSH IX
PUSH IY
LD DE, .bcl_ret
PUSH DE
JP (HL)
.bcl_ret
POP IY
POP IX
RET


; @name BAS_TSECS
; @resident shared
; @param_count 0
; サブ CPU の時刻 (コマンド $EF: 時・分・秒の BCD 3 バイト) を読み、その日の秒 (0〜86399) を A:HL (A が上位 8 ビット) に返す。
; やりとりはキーの読み取り (sGETKY) と同じ手順 (8255 の port B の bit 6 で書ける、bit 5 で読めるのを待つ)
LD BC, $1A01
.tsc_w1
IN A, (C)
AND $40
JP NZ, .tsc_w1
LD BC, $1900
LD A, $EF
OUT (C), A
LD BC, $1A01
.tsc_w2
IN A, (C)
AND $40
JP NZ, .tsc_w2
CALL .tsc_rd        ; 時
AND $3F
CALL .tsc_bin
LD (.tsc_h), A
CALL .tsc_rd        ; 分
AND $7F
CALL .tsc_bin
LD (.tsc_m), A
CALL .tsc_rd        ; 秒
AND $7F
CALL .tsc_bin
LD (.tsc_s), A
; A:HL = 時 × 3600 + 分 × 60 + 秒
XOR A
LD H, A
LD L, A
LD DE, 3600
LD A, (.tsc_h)
LD B, A
XOR A
INC B
JP .tsc_h2
.tsc_h1
ADD HL, DE
ADC A, 0
.tsc_h2
DJNZ .tsc_h1
LD C, A
LD DE, 60
LD A, (.tsc_m)
LD B, A
INC B
JP .tsc_m2
.tsc_m1
ADD HL, DE
JP NC, .tsc_m2
INC C               ; 16 ビットの繰り上がり
.tsc_m2
DJNZ .tsc_m1
LD A, (.tsc_s)
LD E, A
LD D, 0
ADD HL, DE
LD A, C
ADC A, 0
RET
; 8255 の port B の bit 5 で読めるのを待って、サブ CPU から 1 バイト読む
.tsc_rd
LD BC, $1A01
.tsc_r1
IN A, (C)
AND $20
JP NZ, .tsc_r1
LD BC, $1900
IN A, (C)
RET
; A (BCD) → 2 進
.tsc_bin
LD E, A
AND $F0
RRCA
LD D, A             ; 上の桁 × 8
RRCA
RRCA                ; 上の桁 × 2
ADD A, D
LD D, A             ; 上の桁 × 10
LD A, E
AND $0F
ADD A, D
RET
.tsc_h
DB 0
.tsc_m
DB 0
.tsc_s
DB 0


; @name BAS_TIMEH
; @resident shared
; @param_count 0
; @calls BAS_TSECS
; @works BAS_TBASE:3,BAS_TNOW:3
; TIME の読み出し (前半)。TIME = (その日の秒 − 基準) mod 86400 を BAS_TNOW (3 バイト) に入れ、TIME ÷ 4 (0〜21599) を返す。
; 下位 2 ビットは BAS_TIMEL で読む (どちらの戻り値も 32767 以下なので、符号付きの整数 → FLOAT の変換で正しい値になる)
CALL BAS_TSECS
LD DE, (BAS_TBASE)
OR A
SBC HL, DE
LD B, A
LD A, (BAS_TBASE+2)
LD E, A
LD A, B
SBC A, E
JP NC, .bth_pos
LD DE, $5180        ; 負なら 86400 ($015180) を足す
ADD HL, DE
ADC A, $01
.bth_pos
LD (BAS_TNOW), HL
LD (BAS_TNOW+2), A
SRL A
RR H
RR L
SRL A
RR H
RR L
RET


; @name BAS_TIMEL
; @resident shared
; @param_count 0
; @calls BAS_TIMEH
; TIME の読み出し (後半): 直前の BAS_TIMEH で作った TIME の下位 2 ビット (0〜3)
LD A, (BAS_TNOW)
AND 3
LD L, A
LD H, 0
RET


; @name BAS_TIMESET
; @resident shared
; @param_count 1
; @calls BAS_TSECS,BAS_TIMEH
; TIME = n (HL = n、0〜65535 の符号なし)。基準 = (その日の秒 − n) mod 86400 にする
PUSH HL
CALL BAS_TSECS
POP DE
OR A
SBC HL, DE
SBC A, 0
JP NC, .bts_pos
LD DE, $5180
ADD HL, DE
ADC A, $01
.bts_pos
LD (BAS_TBASE), HL
LD (BAS_TBASE+2), A
RET
