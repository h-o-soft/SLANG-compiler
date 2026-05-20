/*
 * SLANG SFX overlay bridge API for C64 (oscar64 audio/sidfx)
 *
 * oscar64 <audio/sidfx.h> の薄い wrapper。priority-based 多 voice SFX 再生を
 * SLANG コードから扱えるようにする (= v3b-C スコープ)。
 *
 * 重要 (= license 運用方針):
 *   oscar64 audio/sidfx.c は GPL-3.0、 SLANG runtime は MIT のため source 借用
 *   不可。本 bridge は `#include <audio/sidfx.h>` で declaration を参照するのみ、
 *   sidfx.c 実体は user の oscar64 install からリンク時に解決する形を取る
 *   (= SLANG 配布物に GPL コード含めず、 sprite/joystick/kio と同じ規律)。
 *
 * SIDFX struct layout (= oscar64 audio/sidfx.h 定義、 14 byte fixed):
 *   offset 0-1  : freq      (WORD, SID frequency 初期値)
 *   offset 2-3  : pwm       (WORD, pulse width 初期値、 PULSE 時のみ意味あり)
 *   offset 4    : ctrl      (BYTE, waveform + SID_CTRL_GATE 等の bit 集合)
 *   offset 5    : attdec    (BYTE, attack << 4 | decay の ADSR 上位)
 *   offset 6    : susrel    (BYTE, sustain << 4 | release の ADSR 下位)
 *   offset 7-8  : dfreq     (WORD, 毎 tick の freq 加算量、 sweep 用)
 *   offset 9-10 : dpwm      (WORD, 毎 tick の pwm 加算量、 PWM modulation)
 *   offset 11   : time1     (BYTE, phase 1 duration in ticks)
 *   offset 12   : time0     (BYTE, phase 0 duration in ticks)
 *   offset 13   : priority  (BYTE, 同 voice 上書き判定値、 高い方が勝つ)
 *
 * SLANG コードで SIDFX struct 1 個を ARRAY BYTE で書く例 (v3b-D 機能):
 *   ARRAY BYTE FX_LASER[] = {
 *       %$1500, %$0000,                         // freq=$1500, pwm=0
 *       SID_CTRL_GATE OR SID_WF_SAW,            // ctrl (= gate on + sawtooth)
 *       $10, $A8,                               // attdec, susrel
 *       %$FFC0, %$0000,                         // dfreq (= -64 sweep down), dpwm=0
 *       $20, $00,                               // time1=32, time0=0
 *       1                                       // priority=1
 *   };
 *   SIDFX_PLAY(SIDFX_VOICE_3, FX_LASER, 1);  // 1 個の phase で再生
 *
 * VSYNC 連動 (= 必須、 毎フレーム呼ばないと進行しない):
 *   LOOP { VIC_WAIT(); SIDFX_LOOP_2(); ... }
 *
 * BGM 同居の方針:
 *   - SIDFX_LOOP() = voice 0/1/2 全部 tick (= BGM player 不使用、 全 voice SFX)
 *   - SIDFX_LOOP_2() = voice 2 のみ tick (= voice 0/1 を BGM 用に空けて voice 2
 *     を SFX 専用に予約する一般的な構成)
 */

#ifndef SLANG_SIDFX_H
#define SLANG_SIDFX_H

/* sidfx system 初期化 (= 全 voice idle 状態に reset)。起動時 1 回呼ぶ。 */
void slang_sidfx_init(void);

/* SIDFX struct array (= byte_ptr で渡す、 1 struct = 14 byte) を voice chn
 * (= 0/1/2) で再生開始。cnt は配列内 SIDFX struct 数 (= 1 phase なら 1)。
 * 既に再生中の SFX がいる場合は priority 比較で勝った方だけ残る (= 内部で
 * sidfx_play が判定)。 */
void slang_sidfx_play(unsigned char chn, unsigned char *fx, unsigned char cnt);

/* voice chn の再生を即座に停止 (= ADSR release 経由ではなく強制 silence)。 */
void slang_sidfx_stop(unsigned char chn);

/* voice chn が idle (= 再生してない) か判定。1 = idle / 0 = 再生中。
 * 次の SFX 投入タイミング判定や effect 連結に使う。 */
unsigned int slang_sidfx_idle(unsigned char chn);

/* voice chn の現在 SFX phase index (= 多 phase 構成の場合に進行状況確認)。
 * 1-phase SFX なら常に 0、 終了時は cnt 以上。 */
unsigned int slang_sidfx_cnt(unsigned char chn);

/* 3 voice すべてを 1 tick 進める (= VSYNC 後に呼ぶ、 BGM player 不使用構成)。 */
void slang_sidfx_loop(void);

/* voice 2 のみを 1 tick 進める (= voice 0/1 を BGM 用に温存して voice 2 だけ
 * SFX 専用、 BGM player と並列稼働する一般的な構成)。 */
void slang_sidfx_loop_2(void);

#endif /* SLANG_SIDFX_H */
