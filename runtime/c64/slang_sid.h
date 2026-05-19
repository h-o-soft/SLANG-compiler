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

#endif /* SLANG_SID_H */
