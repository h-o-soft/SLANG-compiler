; Converted manually for SLANG Runtime Library (new format)
; ====================================================================================================
; Arkos Tracker driver - SLANG MACHINE 中間層 (AKG / AKM 共通)
;
; wrapper bin (= playerAkg_x1_wrapper.asm / playerAkm_x1_wrapper.asm を RASM ビルドした
; PSGAKG_<ORG>.bin / PSGAKM_<ORG>.bin) を ARKOS_BASE にロードした前提で jump table 呼出。
; AKG (generic) / AKM (minimalist) は **同一の jump table layout + parameter area offset**
; を持つ (= 楽曲フォーマットの違いは driver bin 内で吸収、 中間層からは見えない)。 よって
; 本中間層は AKG/AKM 両対応、 どちらの driver を ARKOS_BASE に置くかで決まる。 wrapper の
; layout は runtime/x1/playerAk[gm]_x1_wrapper.asm 参照。
;
; ARKOS_BASE は SLANG プログラム側で `CONST ASM ARKOS_BASE = $8000;` のように定義する
; (= driver の load 先を決めるのはプログラム側、 RASM `-DDRIVER_ORG` と同値)。
; examples/X1_ARKOS/ sample では $8000 配置 (= x1native default $1000 / sosx1 $3000 から
; 十分離す)。 本 file は address 非依存 (= symbol 参照のみ)、 AILZ80ASM は forward EQU 解決。
;
; 注: AKG / AKM driver は同一 ARKOS_BASE に置くため排他 (= 1 プログラムで両 player は
; 同時に積めない)。 どちらか一方の BGM を鳴らすだけなので制約にならない。 SFX (.akx) は
; 両者共通フォーマット。
;
; MACHINE ABI (= 既存 libx1_psg.asm 等と同じ):
;  - 引数は HL (1 個目) / DE (2 個目) / BC (3 個目) で受ける
;  - prologue / epilogue は付かない。 関数は自前で RET、 または JP で他 routine へ
;    tail-call し、 飛び先が RET すれば SLANG 呼び出し元へ戻る
;  - よって wrapper jump table へは `JP ARKOS_BASE + offset` で tail-call する
;    (= wrapper handler が末尾 RET するので、 そのまま SLANG 側へ復帰)
;
; Arkos Tracker player 本体は Targhan/Arkos の MIT License (= runtime/x1/ の各 file header)。
; 本 file は SLANG ABI <-> wrapper ABI の薄い変換層、 SLANG プロジェクトの一部 (MIT)。
; ====================================================================================================


; @name ARKOS_INIT
; @param_count 1
; HL = mode (0=non-CTC、 1=CTC) -> wrapper は L=mode 参照
; 注: wrapper の SND_CTC_PORT / VEC / ISR_* は事前 ARKOS_SET_CTC_PORT で書込が必要
JP ARKOS_BASE + $00


; @name ARKOS_BGM_PLAY
; @param_count 1
; HL = music data addr
JP ARKOS_BASE + $03


; @name ARKOS_BGM_STOP
; @param_count 0
JP ARKOS_BASE + $06


; @name ARKOS_BGM_PAUSE
; @param_count 0
JP ARKOS_BASE + $09


; @name ARKOS_BGM_RESUME
; @param_count 0
JP ARKOS_BASE + $0C


; @name ARKOS_SFX_INIT
; @param_count 1
; HL = SFX table addr
JP ARKOS_BASE + $0F


; @name ARKOS_SFX_PLAY
; @param_count 2
; HL = sfx_num (L use), DE = channel (E use)
; wrapper: L = sfx_num, H = channel  -> SLANG 2 引数を L/H 詰め替えてから tail-call
LD A, E
LD H, A
JP ARKOS_BASE + $12


; @name ARKOS_SFX_STOP
; @param_count 1
; HL = channel (L use)
JP ARKOS_BASE + $15


; @name ARKOS_PSG_PROC
; @param_count 0
; polling mode で VSYNC ごとに caller (VSYNC_PROC) が call、 CTC mode 時は no-op
JP ARKOS_BASE + $18


; @name ARKOS_PSG_END
; @param_count 0
; CTC teardown + BGM stop + trampoline handler restore
JP ARKOS_BASE + $1B


; @name ARKOS_SET_CTC_PORT
; @param_count 2
; HL = CTC port (= _CTC convention)、 DE = vector base
; 本 routine は wrapper を呼ばず自前で完結 (= parameter area 書込 + CTC 設定)、
; 末尾は自前 RET (= 全 path が末尾 RET に合流する単一出口)。
; 0. HL == 0 (CTC unavailable) -> parameter area 全 clear して末尾へ (= 誤用 guard、
;    port - 2 への OUT を avoid)
; 1. parameter area (+$1E, +$20) 書込
; 2. port - 2 への vec low byte OUT (= CTC IM2 vector page 設定)
; 3. OS_TYPE 別 trampoline parameter (+$22, +$24, +$26) 設定
;    - x1native (4): trampoline 経由、 RETI
;    - x1/lsx (0) / sosx1 (1): direct vector、 RETI (= MVP は driver ORG $8000+ 前提)
;    - sosx1 turbo trampoline + RET は future
LD A, H
OR L
JR NZ, _ARKOS_SCP_OK
; CTC unavailable: $1E-$23 全 clear (= stale 値で PSG_END / Init 誤動作回避)
XOR A
LD (ARKOS_BASE + $1E), A
LD (ARKOS_BASE + $1F), A
LD (ARKOS_BASE + $20), A
LD (ARKOS_BASE + $21), A
LD (ARKOS_BASE + $22), A
LD (ARKOS_BASE + $23), A
RET
_ARKOS_SCP_OK:
; CTC/vector/trampoline 書換区間は DI + IFF2 guard (= 既存 PSG_INIT 流儀)
LD A, I
DI
PUSH AF                         ; IFF2 -> AF.PV に保存
LD (ARKOS_BASE + $1E), HL  ; SND_CTC_PORT
PUSH HL
EX DE, HL
LD (ARKOS_BASE + $20), HL  ; SND_CTCVEC
LD A, L                         ; A = vec low byte (= IM2 page)
POP BC                          ; BC = CTC port (= _CTC convention)
DEC C
DEC C                           ; BC = port - 2 (= ch0 vector register)
OUT (C), A                      ; CTC IM2 vector page 設定
#IF NAME_SPACE_DEFAULT.OS_TYPE == 4
; x1native: trampoline 経由、 RETI
; 注: _ISRADR / _ISRHANDLER は work 変数、 中身 (= 括弧付き indirect) を渡す
LD HL, (NAME_SPACE_DEFAULT._ISRADR)
LD (ARKOS_BASE + $22), HL  ; SND_ISR_ENTRY
LD HL, (NAME_SPACE_DEFAULT._ISRHANDLER)
LD (ARKOS_BASE + $24), HL  ; SND_ISR_HANDLER_PTR
XOR A
LD (ARKOS_BASE + $26), A   ; RETURN_MODE = RETI
#ELSE
; OS_TYPE == 0 (x1/lsx) / 1 (sosx1): direct vector、 RETI
XOR A
LD (ARKOS_BASE + $22), A
LD (ARKOS_BASE + $23), A
#ENDIF
POP AF                          ; IFF2 復元 (= AF.PV)
RET PO                          ; PV reset (= IFF2=0 だった) -> EI せず RET
EI
RET
