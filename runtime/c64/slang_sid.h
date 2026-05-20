/*
 * SLANG SID bridge API for C64 (oscar64).
 *
 * oscar64 <c64/sid.h> の薄い wrapper。SID register direct access + 単発 SFX
 * helper を SLANG WORD 経由で SLANG コードから扱えるよう型整合を吸収する。
 *
 * v3b-A スコープ: register direct (FREQ / ADSR / CTRL / VOLUME / GATE) +
 * 単発 SFX wrapper (= ADSR + waveform + GATE on 1 行)。HVSC .sid 取り込みは
 * v3b-B、priority SFX overlay (oscar64 audio/sidfx bridge) は v3b-C。
 *
 * 使い方 (SLANG 側、典型):
 *   #INCLUDE "C64_SID.LIB"
 *   SID_VOLUME(15);                     ; master volume max
 *   SID_SFX(SID_VOICE_1, NOTE_C4,       ; voice 1 で C4 を C major triangle
 *           SID_ATK_8 OR SID_DKY_240,   ; attack 8ms + decay 240ms
 *           ($F * 16) OR SID_DKY_300,   ; sustain 15/15 + release 300ms
 *           SID_WF_TRI);
 *   ; ... 一定 frame 後
 *   SID_GATE_OFF(SID_VOICE_1);          ; release 開始
 *
 * voice 番号: 0..2 (= SID_VOICE_1/2/3 で別名提供)。範囲外は無視 (= bridge 内 if)。
 * volume: 0..15 (= 下位 4bit)。
 * frequency: SID register 値 (= PAL clock 基準の 16-bit WORD、NOTE_C4 等の
 *            事前計算済 register 値を C64_SID.LIB から取得して渡す)。
 * ADSR pack: attdec = (attack << 4) | decay、susrel = (sustain << 4) | release。
 *            SID_ATK_* / SID_DKY_* 定数を OR で組み合わせ (C64_SID.LIB 参照)。
 * waveform: SID_WF_TRI / SAW / PULSE / NOISE のいずれか (= ctrl の上位 bit)。
 */
#ifndef SLANG_SID_H
#define SLANG_SID_H

/* === 初期化 === */

/* SID register を全 0 で初期化 (= 既存音停止 + 安全状態)。
 * 起動直後やゲーム reset で 1 回呼んでから個別 voice 設定する。 */
void slang_sid_init_quiet(void);

/* === Master === */

/* master volume 設定 (0..15、下位 4bit のみ有効、上位 bit (filter mode) は保持)。 */
void slang_sid_volume(unsigned char vol);

/* === Voice direct (= register 直接アクセス) === */

/* voice N (0..2) の frequency register (16-bit)。 NOTE_C4 等の事前計算値を渡す。 */
void slang_sid_freq(unsigned char voice, unsigned int freq);

/* voice N の pulse width register (= 12-bit、PULSE wave 時のみ意味あり)。
 * PULSE 波形を使うときは sound 出力前に必須 (= 0 のまま PULSE を gate on すると
 * duty 0% で無音、$0800 = 50% duty が標準的な矩形波)。範囲は 0..$0FFF (上位
 * 4 bit は無視)、$0001 (極狭) ~ $0FFF (極広) で音色が変わる。 */
void slang_sid_pwm(unsigned char voice, unsigned int pwm);

/* voice N の ADSR pack 設定:
 *   ad = (attack << 4) | decay   (attdec register)
 *   sr = (sustain << 4) | release (susrel register)
 * 各 4bit 値は SID_ATK_* / SID_DKY_* 定数で時定数指定可能。 */
void slang_sid_adsr(unsigned char voice, unsigned char ad, unsigned char sr);

/* voice N の control register (waveform + GATE + sync 等の bit 集合)。 */
void slang_sid_ctrl(unsigned char voice, unsigned char ctrl);

/* voice N の GATE bit を立てる/降ろす (= 既存 ctrl register 維持で gate のみ操作)。
 * gate_on で attack 開始、gate_off で release 開始。 */
void slang_sid_gate_on(unsigned char voice);
void slang_sid_gate_off(unsigned char voice);

/* === SFX wrapper === */

/* 単発 SFX 発射: freq + ADSR + waveform を 1 関数で設定し GATE on まで実行。
 * release は user 明示の slang_sid_gate_off で開始する (= 自動 release scheduling
 * は v4f BGM player の責務、v3b-A では「鳴らしっぱなしになるので user が
 * gate off を呼ぶ」と明示)。
 * waveform は SID_WF_TRI / SAW / PULSE / NOISE のいずれか (= ctrl の上位 bit)。
 * 内部で GATE off → GATE on の 2 段書き込みを行うため、既に GATE が立っている
 * voice に再呼出した場合も attack が確実に re-trigger される (= 連射 SFX OK)。 */
void slang_sid_sfx(unsigned char voice, unsigned int freq,
                   unsigned char ad, unsigned char sr, unsigned char waveform);

/* === HVSC .sid BGM 再生 (v3b-B) === */

/* PSID v2 .sid file (buf, len) を parse して payload を loadAddress に配置、
 * init/play address を bridge 内 static に保存する。
 *
 * PSID header layout (BE WORD):
 *   0x00-0x03: magic ("PSID")  -- "RSID" は v3b-B 非対応 (= playAddr=0 の通常 program 形式)
 *   0x06-0x07: dataOffset       -- 通常 0x007C
 *   0x08-0x09: loadAddress      -- 0 なら payload 先頭 2 byte (LE WORD) が実 loadAddress
 *   0x0A-0x0B: initAddress
 *   0x0C-0x0D: playAddress      -- 0 なら IRQ-driven (RSID 様、v3b-B 非対応)
 *
 * 戻り値: 1 = 成功 / 0 = 失敗 (= magic 不一致、version > 2、 playAddr=0 等)。
 * 失敗時は init/play addr が 0 のまま、後続 PLAYER_INIT/PLAY は no-op 化。
 * 検証は slang_sid_player_ready() で個別取得可能。 */
unsigned int slang_sid_load_from_buf(unsigned char *buf, unsigned int len);

/* SID_LOAD_FROM_BUF 成功時に bridge 内に保存された init address を呼び出す
 * (= A レジスタに song 番号を入れて JSR initAddress 相当)。.sid file 1 つで
 * 複数 song を含む場合は song = 0..(songs-1) で切替。 */
void slang_sid_player_init(unsigned char song);

/* SID_LOAD_FROM_BUF 成功時に bridge 内に保存された play address を呼び出す
 * (= JSR playAddress 相当)。毎フレーム (VSYNC 後) 呼ぶことで music が進行する。
 * 通常は PAL 50Hz / NTSC 60Hz クロック前提、v3b-B は VIC_WAIT() 連動で 50Hz 想定。 */
void slang_sid_player_play(void);

/* SID_LOAD_FROM_BUF が成功して player ready 状態かを返す (1 = ready / 0 = not loaded
 * or load 失敗)。SLANG コード側で再生開始前の状態判定に使う。 */
unsigned int slang_sid_player_ready(void);

/* SID_LOAD_FROM_BUF の raw address 版。SLANG コードで `ARRAY BYTE BUF[]` を
 * declare すると oscar64 の BSS が $0F78-$1F91 等の領域を占有してしまい、
 * Hoopsidasies (= loadAddress $1000 系) の payload memcpy で SLANG runtime の
 * global 変数を破壊する。回避策として KIO_READ_ADDR で raw addr ($C000 等の
 * SLANG runtime と衝突しない free RAM area) に disk read してから、本関数に
 * その raw addr を渡して bridge 側で PSID parse + memcpy する。 */
unsigned int slang_sid_load_from_buf_addr(unsigned int buf_addr, unsigned int len);

#endif /* SLANG_SID_H */
