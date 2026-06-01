; ====================================================================================================
; banjo (Furnace tracker) music driver - SLANG MACHINE 中間層 (AY / OPM 共通)
;
; banjo (Joe Kennedy, MIT) の Furnace 曲を X1 で鳴らす driver bin を BANJO_BASE にロードした前提で
; jump table 呼出。 driver bin 先頭の固定 jump table (banjo_x1_wrapper.asm) へ `JP BANJO_BASE + offset`
; で tail-call する (= banjo Core は固定 offset entry を持たず label 直 call なので wrapper を被せた)。
; ABI 変換 (chip flag / loop / channel 数) は driver wrapper 側で行うため、 本中間層は SLANG 呼出
; 規約 (HL=1 個目 / DE=2 個目) のまま JP するだけの薄い層。
;
; BANJO_BASE は SLANG プログラム側で `CONST ASM BANJO_BASE = $8000;` のように定義する
; (= driver の load 先を決めるのはプログラム側、 driver build の --org と同値)。 AY / OPM は同一
; jump table layout なので本中間層は両対応 (= driver bin に積んだ chip で決まる)。
;
; MACHINE ABI (= 既存 libx1_psg.asm / libx1_arkos.asm 等と同じ):
;  - 引数は HL (1 個目) / DE (2 個目) で受ける
;  - prologue / epilogue は付かない。 JP tail-call し、 飛び先が RET すれば SLANG 呼び出し元へ戻る
;  - よって wrapper jump table へは `JP BANJO_BASE + offset` で tail-call する
;
; banjo driver 本体は Joe Kennedy の MIT License。 本 file は SLANG ABI <-> wrapper ABI の薄い
; 変換層、 SLANG プロジェクトの一部 (MIT)。
; ====================================================================================================


; @name BANJO_INIT
; @param_count 1
; HL = chip flag (BANJO_CHIP_AY=4 / _OPM=128 / _AY_OPM=132)。
; channel 数は driver build 時の BANJO_MAX_CHANNELS を wrapper が固定セットするので渡さない。
JP BANJO_BASE + $00


; @name BANJO_PLAY
; @param_count 2
; HL = song data addr、 DE = loop (BANJO_LOOP_ON=1 / _OFF=0)。 wrapper が D=E に詰替えて
; banjo_play_song (HL=song, D=loop) を呼ぶ。
JP BANJO_BASE + $03


; @name BANJO_UPDATE
; @param_count 0
; 1 フレームぶん再生を進める (= 毎 VSYNC に 1 回呼ぶ)。
JP BANJO_BASE + $06


; @name BANJO_STOP
; @param_count 0
; 再生を停止する。
JP BANJO_BASE + $09


; @name BANJO_END
; @param_count 0
; CTC teardown + 再生停止 (SFX も停止)。polling mode では SFX 停止 + BANJO_STOP 相当。
JP BANJO_BASE + $0F


; @name BANJO_SFX_INIT
; @param_count 0
; SFX エンジンの状態を初期化する (再生中に呼んでも安全な stop+init)。
; ※ BANJO_INIT が自動で初期化するため通常は呼ぶ必要なし。 SFX は AY (PSG) 専用。
JP BANJO_BASE + $1B


; @name BANJO_SFX_PLAY
; @param_count 1
; HL = SFX データ addr。 再生 ch は SFX データ変換時に固定。 再生で対応 BGM ch を mute、
; 停止で unmute。 同時に鳴らせる SFX は 1 つ。 SFX は AY (PSG) 専用。
JP BANJO_BASE + $1E


; @name BANJO_SFX_STOP
; @param_count 0
; 再生中の SFX を停止し、 mute していた BGM ch を unmute する。 SFX は AY (PSG) 専用。
JP BANJO_BASE + $21


; @name BANJO_SET_CTC_PORT
; @param_count 2
; HL = CTC port (= _CTC convention)、 DE = vector base
; wrapper parameter area (+$12..+$1A) に CTC/trampoline 情報を書き込む。
; 実際の CTC ch1 設定と vector hook は BANJO_INIT 側で行う。
LD A, H
OR L
JR NZ, _BANJO_SCP_OK
; CTC unavailable: parameter area clear (= stale 値で init/end が誤動作しないようにする)
XOR A
LD (BANJO_BASE + $12), A
LD (BANJO_BASE + $13), A
LD (BANJO_BASE + $14), A
LD (BANJO_BASE + $15), A
LD (BANJO_BASE + $16), A
LD (BANJO_BASE + $17), A
LD (BANJO_BASE + $18), A
LD (BANJO_BASE + $19), A
LD (BANJO_BASE + $1A), A
RET

_BANJO_SCP_OK:
; CTC/vector/trampoline 書換区間は DI + IFF2 guard (= Arkos / PSG_INIT と同じ)
LD A, I
DI
PUSH AF                         ; IFF2 -> AF.PV
LD (BANJO_BASE + $12), HL       ; CTC port
PUSH HL
EX DE, HL
LD (BANJO_BASE + $14), HL       ; CTC vector base
LD A, L                         ; A = vec low byte (= IM2 page)
POP BC                          ; BC = CTC port (= _CTC convention)
DEC C
DEC C                           ; BC = port - 2 (= ch0 vector register)
OUT (C), A
#IF NAME_SPACE_DEFAULT.OS_TYPE == 4
; x1native: ISR trampoline 経由
LD HL, (NAME_SPACE_DEFAULT._ISRADR)
LD (BANJO_BASE + $16), HL       ; ISR_ENTRY
LD HL, (NAME_SPACE_DEFAULT._ISRHANDLER)
LD (BANJO_BASE + $18), HL       ; JP operand addr
XOR A
LD (BANJO_BASE + $1A), A        ; RETURN_MODE = RETI
#ELSE
; x1/lsx, sosx1: direct vector。sosx1 turbo trampoline + RET は future。
XOR A
LD (BANJO_BASE + $16), A
LD (BANJO_BASE + $17), A
LD (BANJO_BASE + $18), A
LD (BANJO_BASE + $19), A
LD (BANJO_BASE + $1A), A
#ENDIF
POP AF
RET PO
EI
RET
