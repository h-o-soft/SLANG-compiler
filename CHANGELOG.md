# 更新履歴

## Unreleased

### Commodore 64 (oscar64) C transpile backend (experimental)

Z80 専用だった SLANG コンパイラに **Commodore 64 (6502)** 対応を追加。**SLANG → C ソース → oscar64 → .prg** の二段変換で 6502 バイナリを生成。oscar64 ([https://github.com/drmortalwombat/oscar64](https://github.com/drmortalwombat/oscar64)) は別途インストール必要 (= 配布物に同梱なし)。

**ビルドフロー**:
- `slangc -E c64 input.SL -o output.c` → SLANG → C ソース変換のみ
- `slangbuild -E c64 input.SL -o prefix` → 内部 slangc → oscar64 一括ビルド → `<prefix>.prg`
- oscar64 binary の解決順: `--oscar-path` → env file `oscar_path:` → 環境変数 `$OSCAR64` → PATH

**env file** `runtime/env/c64.env`:
- `backend: oscar_c` / `output: c_source` / `oscar_machine: c64` / `oscar_format: prg` / `oscar_petscii: true`
- `c_runtime_files:` で `runtime/c64/slang_runtime.c` + `slang_sprite.c` を oscar64 source list に並べる
- `c_bindings:` で env が提供する C 関数 binding 表を一括公開 (= SLANG 側 CFUNC 宣言不要、sprite API 等の標準 API として呼べる)

**SLANG 言語拡張**:
- **`CFUNC` 宣言**: SLANG → C 関数の直接マッピング (略式 `CFUNC FOO(2):foo;` + 型あり `CFUNC FOO(BYTE x, WORD y) VOID :foo;` 両対応)。配列ポインタ引数 `BYTE buf[]`、戻り値型 `BYTE/WORD/FLOAT/VOID` 指定可、`c_name` は case preserve。Z80 backend では診断 error (= `#IF BACKEND==1` で gate)
- **`VOID` キーワード**: CFUNC の戻り値型表記用に追加 (既存 SLANG コードへの影響なし)
- **`BACKEND` preprocessor const**: env が自動定義 (0=Z80、1=OscarC)、`#IF BACKEND==1` で C backend 専用コード gate

**slangbuild CLI 拡張**:
- `--oscar-path <p>`: oscar64 binary 上書き指定
- `--c-source <p>`: ユーザー C ファイルを oscar64 build に追加 (repeatable、CFUNC 実体を置く)。Z80 env で指定すると early reject

**実装 (oscar64 backend)**:
- `runtime/c64/slang_runtime.{h,c}`: PRINT 13 構文 (`!` / `%` / `FORM$` / `DECI$` / `HEX2$` / `HEX4$` / `MSG$` / `STR$` / `CHR$` / `SPC$` / `CR$` / `TAB$` / `FL$` / `MSX$` / `PN$` / `/`) + INPUT 系 (`INPUT()` 数値入力、`GETL(buf)` / `GETLIN(buf, x)` / `LINPUT(buf, x)` 文字列入力、ESC で `$FFFF` 返却、`_CARRY` 機構は v1 未対応) + RND + BIT + LOCATE + INKEY (= KERNAL $C5 直読みで押下中=値・離した瞬間=0 の即時状態取得) + SCREEN ($0400 screen RAM 直読み) + WIDTH (C64 は 40 桁固定なので no-op)
- `runtime/c64/slang_sprite.{h,c}`: VIC sprite bridge 9 関数 (`SPR_INIT` / `SPR_SET` / `SPR_MOVE` / `SPR_SHOW` / `SPR_POSX` / `SPR_POSY` / `SPR_COLOR` / `SPR_IMAGE` / `VIC_WAIT`)。oscar64 sprites.h を直接 binding せず bridge 経由で型整合と pointer table 管理を吸収。slang_runtime.h ← slang_sprite.h chain include で生成 C 側 extern と bridge 実装の signature drift を防ぐ
- `include/C64_VIC.LIB`: VIC-II 色定数 16 個 (`VCOL_BLACK..VCOL_LT_GREY`)、`#INCLUDE` で取り込み
- 生成 C は `unsigned int` (16-bit) で WORD wrap を保持、整数 → FLOAT 変換は signed cast 経由 (= Z80 backend の `i16tof24` と同じセマンティクス)
- FOR ループは body 前比較 + body 後 step + wrap-sentinel break 形式で SLANG 仕様 (自然終了で `I=end+step`) と body 内 manual `I++` の両方をサポート
- `ARRAY A[N]` は SLANG 仕様通り要素数 N+1 (index 0..N) として確保
- `MEM[addr]` / `MEMW[addr]` (= memory-mapped 配列) は `SLANG_MEM(addr)` / `SLANG_MEMW(addr)` マクロに展開

**動作確認済サンプル** (実機 VICE):
- `examples/FMANDEL.SL` (テキストマンデルブロ、FLOAT 演算、oscar64 最適化で Z80 backend より高速動作)
- `examples/FURUI.SL` (エラトステネス素数判定 1〜10000)
- `examples/STARS.SL` (リアルタイム key 入力でアニメーション、A/D で星数増減)
- `examples/c64/SPRITE.SL` (VIC sprite 1 個 + VSYNC 同期で画面端バウンス、c64 専用 sample 用ディレクトリ)
- `examples/c64/FMANDEL.SL` (`examples/FMANDEL.SL` 80 桁版を C64 40 桁画面用に縮めた版、X 軸解像度半減を倍率 2 倍で補償)
- `examples/c64/JOYSPR.SL` (= v3a) joystick port 2 で sprite 移動 + fire で色変更 (= ロードマップ v3a に対応、`JOY_DIR` bitmask + `JOY_B` 主、`JOY_X/Y` の signed `-1=$FFFF` パターン解説含む)
- `examples/c64/HISCORE.SL` (= v3c) D64 disk image に `HISCORE,S,W` で save → `HISCORE,S,R` で read → 内容比較する KERNAL file I/O 往復 sample
- `examples/c64/SIDSFX.SL` (= v3b-A) joystick で sprite 移動 + fire ボタンで voice 0 SFX 発射 + 1/2/3 キーで音色 preset 切替 (= laser/boom/noise の 3 種類)

### v3b-A — SID 基盤 + 単発 SFX binding (#180 配下)

ロードマップ #178 配下の v3b の 3 分割初回 (= SID 基盤 + 単発 SFX) を実装:

- 新規 `runtime/c64/slang_sid.{h,c}`: oscar64 `c64/sid.h` の register direct 8 関数
  + SFX wrapper 1 関数 (`slang_sid_init_quiet` / `_volume` / `_freq` / `_pwm`
  / `_adsr` / `_ctrl` / `_gate_on` / `_gate_off` / `_sfx`)。oscar64 `c64/sid.h`
  は hardware register memory map (= API レベル) のため SLANG MIT runtime に
  取り込んでも GPL 影響なし。**PULSE 波形は事前に `SID_PWM` で pulse width を
  設定する必要あり** (= pwm=0 だと duty 0% で無音、$0800 が標準矩形波)
- 新規 `include/C64_SID.LIB`: voice 番号定数 3 個 + waveform 4 個 + CTRL bit 4 個
  + ATK/DKY/SUS 各 16 段階 + pulse width preset 4 個 (`SID_PWM_NARROW/QUARTER/SQR/WIDE`)
  + NOTE_C2..B6 5 オクターブ 60 個 (= PAL clock 基準の事前計算済 SID register
  値、`SID_FREQ_PAL(NOTE_X(o))` 二段変換結果)
- `runtime/env/c64.env` `c_bindings:` に 9 entry 追加、`c_runtime_files:` に
  `slang_sid.c` 追加
- `runtime/c64/slang_runtime.h` に `#include "slang_sid.h"` chain
- 新規 `tests/SLANGCompiler.Tests/SidBindingTests.cs`: 8 関数の extern
  signature と呼出展開、実 `runtime/env/c64.env` 全 8 binding 列挙の golden
- 新規 `examples/c64/SIDSFX.SL`: JOYSPR.SL 拡張、joystick 移動 + fire で SFX
  発射 + 1/2/3 キーで preset 切替 (laser/boom/noise) + sprite 色連動

ライセンス整理:
- oscar64 `c64/sid.h` は hardware register API なので borrow OK (= bridge 関数
  はすべて SLANG 側オリジナル実装、`slang_*` 命名で明示)
- oscar64 `audio/sidfx.c` (= GPL-3.0 の priority SFX player) は v3b-A では使わない
  (= v3b-C で `#include <audio/sidfx.h>` の bridge 経由参照に限定、SLANG MIT
  runtime に GPL コードを borrow しない方針継続)

v3b-A 設計判断:
- voice 番号 0..2、volume 0..15、frequency は PAL clock 基準の register 値 (=
  NOTE_C4 等の事前計算済定数を C64_SID.LIB から取得)
- ADSR pack は `SID_ATK_* OR SID_DKY_*` (attdec)、`SID_SUS_* OR SID_DKY_*` (susrel)
  形式で SLANG コード側で組み立て
- SFX wrapper は「gate on までの helper」と再定義 = release は user 明示
  `SID_GATE_OFF(voice)` で開始、自動 release scheduling は v3b-C / v4f BGM player
  の責務に分離
- NOTE_B7 は 16-bit overflow (= $10249 = 66121) のため C7..AS7 のみ提供、B7 と
  それ以上のオクターブは user 側で計算

v3b roadmap (= 後続 PR):
- v3b-B: HVSC .sid 取り込み + BGM 再生 (= slangbuild `--sid` option、`SID_LOAD` /
  `SID_PLAYER_INIT` / `SID_PLAYER_PLAY` bridge、sample SIDBGM.SL)
- v3b-C: oscar64 `audio/sidfx` bridge で priority SFX overlay (= `#include
  <audio/sidfx.h>` 経由参照、SLANG MIT runtime に GPL コードを含めない方針継続、
  sample SIDOVERLAY.SL)

### v3c — KERNAL file I/O binding (#181)

ロードマップ #178 配下の v3c (= disk file 入出力) を実装:

- 新規 `runtime/c64/slang_kio.{h,c}`: oscar64 `c64/kernalio.h` の bridge
  関数 16 個 + raw address 用 4 個。通常 API (`KIO_SETNAM` / `KIO_OPEN` /
  `KIO_OPEN_NAMED` / `KIO_CLOSE` / `KIO_CHKIN` / `KIO_CHKOUT` / `KIO_CLRCHN` /
  `KIO_CHRIN` / `KIO_CHROUT` / `KIO_GETCH` / `KIO_PUTCH` / `KIO_READ` /
  `KIO_WRITE` / `KIO_PUTS` / `KIO_GETS` / `KIO_STATUS`) は文字列・buffer
  を `byte_ptr` で受ける。raw address 用 (`KIO_SETNAM_ADDR` /
  `KIO_OPEN_NAMED_ADDR` / `KIO_READ_ADDR` / `KIO_WRITE_ADDR`) は SLANG WORD
  で絶対アドレスを直接渡せる
- 新規 `include/C64_KIO.LIB`: krnioerr 定数 9 個 (`KRNIO_OK / DIR / TIMEOUT /
  SHORT / LONG / VERIFY / CHKSUM / EOF / NODEVICE`) + device 定数 6 個
  (`DEV_TAPE / DEV_PRINTER / DEV_DISK / DEV_DISK9 / 10 / 11`) + channel
  定数 3 個 (`CH_READ / CH_WRITE / CH_COMMAND`)
- `runtime/env/c64.env` `c_bindings:` に 20 entry 追加、
  `c_runtime_files:` に `slang_kio.c` 追加、`oscar_optimize: Os` 永続化
  (= 既定 `-O1` で bitwise AND を含む特定パターンの最適化バグを踏むため、
  `-Os` に切替で回避 + size 最小化、副作用なし。詳細は env file 内コメント参照)
- `runtime/c64/slang_runtime.h` に `#include "slang_kio.h"` chain
- 新規 `tests/SLANGCompiler.Tests/KioBindingTests.cs`: extern signature
  drift と呼出展開、`$FFFF` エラー判定パターン、実 `runtime/env/c64.env`
  全 20 binding 列挙の golden (= byte_ptr / word / void return + 多引数を網羅)
- 文字列は NUL terminated PETSCII 固定 (= oscar64 `-psci` で SLANG リテラル
  `"score,s,r"` がそのまま PETSCII 化されて KERNAL に渡る)

PETSCII / 1541 disk 上の流儀 (= HISCORE.SL sample の通り):
- 文字列リテラルは「主部 + type/mode token とも全小文字」で書く: oscar64
  `-psci` は ASCII 小文字 (0x61-0x7A) を PETSCII unshifted 大文字
  (0x41-0x5A) に変換するため、1541 が `,S,W` 等の token を unshifted 大文字
  (0x53) で expect する仕様と整合する。SLANG リテラル大文字
  (`"HISCORE,S,W"`) は PETSCII shifted 領域 (0xC1-0xDA) に化けて
  type/mode token が parse されず PRG として保存されてしまうので注意
- SEQ ファイルの主部は PRG (= 起動用 SLANG プログラム) と別名にする
  (sample では `score`)。1541 emu は同主部 PRG と SEQ の共存を拒否する
  ケースがあるため、衝突回避のため別主部を選ぶのが安全

戻り値の慣行:
- bool 系 (`open` / `chkin` / `chkout` / `chrout`) は 0 = fail / 1 = success、
  詳細は `KIO_STATUS()` (krnioerr enum) で確認
- byte 読み系 (`chrin`) は 0..255
- `getch` / `read` / `write` / `gets` / `puts` は oscar64 で signed int を
  返しエラーは負値。SLANG WORD では sign extension で `$FFFx` として届くので、
  エラー判定は `IF (result & $8000) THEN ...` または `IF result == $FFFF`

付随する CTranspiler 修正:
- `CastTo`: PointerType target は src と同型でも明示 cast を残す。理由:
  SLANG StringLiteral を `byte_ptr` binding に渡す際、C 側の `const u8[]`
  から `unsigned char *` への暗黙変換が oscar64 で reject されるため
- `VisitReturnStmt`: SLANG `RETURN;` (引数なし) を関数の戻り型 size に応じた
  default 値 (`return 0;` / `return 0.0;`) で emit (= CTranspiler は全関数を
  非 void で emit するため整合が必要)

既知の oscar64 issue (= 本 PR 範囲外、最小再現と issue 投稿は別途):
- oscar64 `-O1` (= 既定) で `if (((unsigned int)((V_X) & 0x8000u)))` の
  ような多重 cast + 周辺の bridge call chain がある場合、定数式 `5 & 0x8000`
  の partial constant fold が誤動作して常時 true 判定されるケースを観測。
  `-O0` / `-O2` / `-O3` / `-Os` では未再現のため、c64.env で `oscar_optimize: Os`
  を採用して回避。最小再現を別途切り出して oscar64 へ報告予定

### v3a — Joystick binding (#179)

ロードマップ #178 配下の v3a (= ゲーム入力中核) を実装:

- 新規 `runtime/c64/slang_joystick.{h,c}`: oscar64 `joystick.h` の bridge
  関数 5 個 (`slang_joy_poll` / `slang_joy_x` / `slang_joy_y` / `slang_joy_b` /
  `slang_joy_dir`)。oscar64 signed byte (-1/0/1) を SLANG WORD では `-1=$FFFF` に
  sign extension して返す。`slang_joy_dir` は 5 bit bitmask (UP=1 / DOWN=2 /
  LEFT=4 / RIGHT=8 / FIRE=16) を組み立てる
- 新規 `include/C64_JOY.LIB`: `JOY_UP/DOWN/LEFT/RIGHT/FIRE` の bitmask 定数 +
  `JOY_PORT1/PORT2` port 番号、計 7 CONST
- `runtime/env/c64.env` `c_bindings:` に 5 entry 追加 (`JOY_POLL` / `JOY_X` /
  `JOY_Y` / `JOY_B` / `JOY_DIR`)、`c_runtime_files:` に `slang_joystick.c` 追加
- `runtime/c64/slang_runtime.h` に `#include "slang_joystick.h"` chain
- 新規 `tests/SLANGCompiler.Tests/JoystickBindingTests.cs`: 5 関数の extern
  signature と呼出展開、`$FFFF` 比較パターンの golden
- 新規 `examples/c64/JOYSPR.SL`: SPRITE.SL 改造、port 2 joystick で 8 方向移動 +
  fire 色変更 (VSYNC 同期、debouncing 含む)

Codex レビュー反映:
- 主 API は `JOY_DIR` bitmask (= ゲーム実装で短く書ける + 符号問題なし)
- `JOY_X` / `JOY_Y` は補助 API として位置付け、`-1=$FFFF` 判定パターンを sample で示す
- `JOY_POLL` は明示呼出 (= 自動 poll は副作用 / 重複呼出を避ける)

全 370 テスト pass (= 368 + JoystickBindingTests 2)。

**v1 制約**:
- 文字列は ASCII printable (0x20-0x7E) のみサポート。日本語・カナ・SJIS は未対応 (今後 oscar64 `p"..."` 拡張等で検討)
- `MACHINE` 宣言 / inline `#ASM` / `PORT IN/OUT` / `#MODULE` (overlay) / 多段 `EXIT(n>=2)` は診断エラー
- 未宣言関数呼び出しは error (= Z80 backend の MACHINE 暗黙呼出を許さない、明示的に CFUNC または env c_bindings: で declare)
- sprite multiplex / VIC bitmap mode / SID sound / KERNAL file I/O / CRT 等は未対応 (bridge 追加で順次対応予定)

**Z80 backend 互換性**:
- 既存 16 env の挙動は変わらず、`IrGenerator` / `CodeGenerator` / `RuntimeManager` / `Z80Emitter` / `PeepholeOptimizer` 無変更
- `--c-source` を Z80 env で指定すると early reject (= 既存 oscar_*/c_runtime_* 排他検証と同じ規律)
- 既存 + 新規 全 361 単体テスト pass

## Version 0.24.1

- MZ-2500 環境の MAGIC ライブラリ対応と IOCS 入力ランタイム拡充 (#175)
  - `sosmz2500` 環境に MZ-2500 用 MAGIC ライブラリ (`runtime/libmz2500_magic.asm`) をリンクするよう追加。これにより S-OS / MZ-2500 上で MAGIC を使う SLANG コードがビルド可能 (`examples/MAGICSMPL.SL` がそのまま動作)
  - `mz25iocs` 環境の入力ランタイム (`runtime/libmz25iocs_input.asm`) に `GETL` / `GETLIN` / `LINPUT` / `INPUT` を実装。IOCS `SVC_GETL` (call 0Ch) をベースに、`GETL` / `GETLIN` はカラム 0 から、`LINPUT` / `INPUT` は IOCS ワーク `$05E2` の現在カーソル X を読み飛ばして S-OS 版と同じ "プロンプト除去" 挙動に揃えた
  - `INPUT` は `$` プレフィックスで 16 進数を、それ以外で 10 進数をパース。ESC / SHIFT+BREAK で `_CARRY=1` をセットして 0 を返す
  - `mz25iocs` 環境に `LOCATE(x, y)` を実装 (`libmz25iocs_print.asm`) — IOCS `SVC_CMOV` (call 6Fh) を使用、`L=X` / `H=Y` で呼び出す
  - 新サンプル: `examples/MZ25IOCS.SL` に `INPUT` / `GETL` / `GETLIN` / `LINPUT` の動作確認コードを追加
  - `docs/MZ2500.md` に入力ランタイムの挙動 (`SVC_GETL` 仕様、`length` の下位 8bit 制約 / `0` = 256 文字扱い、推奨バッファサイズ、`LOCATE` の引数渡し) を追記

## Version 0.24.0

- コンパイラ: BYTE 配列引数の型情報伝播 + 関数スコープ重複名検出 (#171)
  - `F(BYTE T[]) ... T[i]` のような BYTE 配列引数が WORD として扱われ、1 byte step / byte load の代わりに 2 byte step / 2 byte load の wrong code を生成していたバグを修正 (`ParamDecl.IsArray` + `Size` を IR の `LocalVarInfo` と semantic の `PointerType` 両方に伝播)
  - 関数引数とローカル/静的宣言が同名でも警告無しに silent overwrite され、IR と semantic で同一識別子の解決先が逆転する dangerous な状態を解消。`SemanticAnalyzer.VisitFuncDef` で param 同士 / param vs static decl / param vs local decl / static vs local decl の 4 ケースを明示エラー化 (case-insensitive default、`_caseSensitive` 尊重)
  - **既存 SLANG コードへの behavior change**: BYTE 配列引数を使ってる既存 SL は wrong code が消えて意図通りに動くようになる。重複名を意図的に書いていた既存 SL は build エラーになる (= 修正対象、silent drop で動いていた挙動は本来未定義)

- PCG ボード (PCG-8100 後期 / PCG-8200 / PCG-8800 + PSA3.0 等の互換) 搭載 8253 3 ch サウンド向けドライバ (`runtime/libpc80mk2_sound.asm`) の品質改善
  - `SND_ISPLAYING()` の 3 ch 判定バグ修正 (CH1/CH2 の判定結果が捨てられて実質 CH3 のみで判定されていた)
  - 休符サポート追加 — `TONE.REST = 0x7F` を MML データに置けば該当 length 分 silence
  - `SND_PROC` 出力段の動的 KEYON マスク (= 休符 / 未使用 ch を 8253 の出力 gate で物理 mute)
  - `SND_PROC` 出力段の shadow 最適化 (= 同 frequency を毎 VSYNC 書き直すと 8253 mode 3 が再ロードでガタつくため、変化時のみ書き込み)
  - 音長カウンタ修正 — 旧版は length=N で内部 N+1 tick 占有していた (= silent end 1 tick が length 値に組込まれていなかった) ため ch ごとに音符数が違うと位相ズレが発生していた
  - `examples/PC80mk2.SL` の SOUNDDATA を新 8 小節 verse (melody + bass + harmony 3 ch、octave doubling 廃止) に更新

- MML → ASM コンバータ `tools/mml2sound.py` を追加
  - 簡易 MML テキスト (note + sharp/flat、八度、長さ + dotted、休符) から `libpc80mk2_sound` 用の byte data (length / note / 0x7F rest / 0x80 end) を生成
  - 出力モード: ASM `.db` 列 (既定、`#ASM` block 互換、length 圧縮済) / `--binary <prefix>` で per-channel raw bin
  - 1..3 ch 制約 + 不足分は `.__empty: db 0x80` で自動 padding
  - MML channel 順を物理 CH に対応 (= 1 番目 ch が物理 CH1、SE 多重化は CH3 = MML 3 番目)
  - サンプル `examples/pc80mk2/chouchou.mml` (8 小節「ちょうちょ」、`PC80mk2.SL` の SOUNDDATA 出元) と `examples/pc80mk2/README.md` を同梱
  - 配布 zip に `tools/mml2sound.py` も同梱

- 配布 zip に `install.sh` / `install.bat` / `uninstall.sh` / `uninstall.bat` を同梱、Makefile に依存せず install / uninstall できるよう導線を切り出し
  - オプション: `--prefix <path>` / `--config-dir <path>` / `--dry-run` / `--verbose` / `--force` / `--uninstall` / `--help`
  - **危険 path guard**: uninstall 時に空 / `/` / `$HOME` / `/tmp` 単体 / `C:\` / `%USERPROFILE%` 等を refuse (絶対パス正規化してから完全一致判定、`/tmp/sub` 等は許可)
  - **ghost file 対策**: install 時、サブディレクトリ (include / runtime / images / tools) は staging copy → 既存削除 → rename でサブディレクトリ単位置換 (= 古い env file 等が残らない)
  - `make install` / `make uninstall` は scripts への薄い wrapper として残置 (`--force` 既定 ON で後方互換、Make 経由は uninstall も非対話)
  - **Windows install default を `%LOCALAPPDATA%\Programs\SLANG` → `%USERPROFILE%\.local\bin` に変更** (= install.sh の `~/.local/bin` と対称、uv / pipx 等の CLI ツール慣習)

- pc88mk2sr 環境を `slangbuild --emit disk` 経路に統合
  - 新ツール `udostool` (Bookworm's Library 公開の汎用ディスクルーチン用、`tools/udostool.exe` 同梱、Linux/macOS は mono 経由起動)
  - `slangbuild --emit disk -E pc88mk2sr` でテンプレート D88 から `PC88MK2SR.D88` を生成、IPL/SUB/SYS の書き込み + main (`$1A00.$$$`) + overlay (`M{index}.BIN`) の格納を 1 コマンドで実行
  - overlay は `M{index}.BIN` として disk に格納される。`#MODULE` を使った SL では `libp88_file` の `Disk_Load` / `Disk_Load3` 系で読み込む (disk 内名は `Disk_Load("M0 BIN", addr)` の space 区切り形式)
  - **ORG = $1A00 固定運用**。SL 側で `#ORG $XXXX` 上書きすると `main_name` (= `"$1A00.$$$"`) と loader 期待が不整合になり、build は通るが boot しない
  - VRTC 割り込みでの `GAMEVSYNC` 呼び出しを SL 側 `CONST ASM USE_GAMEVSYNC = 1;` で有効化する仕組みに変更。`USE_GAMEVSYNC` 未定義の SL では `GAMEVSYNC` 関数を書かずに build 可能
  - AILZ80ASM のアセンブル失敗時の詳細 (未定義 label 等) を `--verbose` 無しでも stderr に出力するよう修正
  - 同梱物の出典を新規 `THIRD_PARTY_NOTICES.md` に記録

- zxn 環境 (ZX Spectrum Next) を `slangbuild` に対応
  - `make ENV=zxn build TARGET=examples/zxn/game` で `.bin` を生成可能。`Makefile.dist` に zxn ENV ブロック追加 (`SRC_EXT = .sl` で `examples/zxn/*.sl` 小文字に対応)
  - `examples/zxn/Makefile` を slangbuild 経由に書き換え。`.nex` 形式 (CSpect 等で実行可能) への変換は外部ツール `nexcreator` を引き続き使用
  - `examples/zxn` のサンプル (`game.sl` / `game_nomusic.sl` + asset) を配布 zip に同梱。`examples/zxn/Makefile` の default target を NextDAW 不要の `game_nomusic.nex` に変更 (`make build` で動作)。NextDAW を含む完全版 (`game.nex`) は `make music` で build 可能 (= 別途 NextDAW Runtime Player の配置が必要、`https://nextdaw.biasillo.com/`、2026-04-30 時点で公式サイト入手不可)

- vgs0 環境 (VGS-Zero) を `slangbuild` に対応 — 8KB bank switching に合わせた bin padding
  - env file 新フィールド `bin_pad_size:` (main 用、固定サイズで末尾 0 padding) と `overlay_pad_align:` (overlay 用、指定値の倍数に切り上げ末尾 0 padding) を追加
  - `runtime/env/vgs0.env` で `bin_pad_size: 16384` + `overlay_pad_align: 8192` を設定 (= main を 16KB 固定 ROM、各 overlay を 8KB bank 単位に揃える)
  - bin が `bin_pad_size` を超えた場合は明示エラーで build 失敗 (= 意図しない切り詰めを防ぐ)
  - `make ENV=vgs0 build` 経路に対応

- pc80mk2xsd 環境 (PC-8001mkII XBIOS 直接環境、SD カード経路) を `slangbuild` に対応
  - env file 新フィールド `cmt_assets:` (output dir にコピーする static asset 群)、`overlay_name:` (overlay 出力ファイル名のテンプレート、`{index}` 展開)、`overlay_output_format:` (overlay 専用の出力フォーマット、`bin` / `cmt` 切替)
  - pc80mk2xsd では main を CMT 形式 + overlay を raw binary で出し、`M{index}.BIN` 命名で output dir に配置。`XBIOS.CMT` も output dir にコピーされるので、output dir 全体を SD カードに移すだけで動作する

- env file `defines:` フィールドを追加 — env 別の整数定数を SL / ASM の両側に自動定義
  - env file `defines: { NAME: int_value }` 形式で定義した整数定数は、SL の `#IF NAME==VAL` と ASM の `#if exists NAME` の両方で参照できる
  - pc80mk2xsd で `defines: { PC8001_SD: 1 }` を定義しているので、SL 側に `CONST ASM PC8001_SD = 1;` を書かなくても env 切替だけで SD 経路が有効化される
  - 名前は識別子規則 (英数字 + アンダースコア、先頭は文字または `_`)、値は整数限定

- pc80mk2x 環境 (PC-8001mkII XBIOS 直接環境) を `slangbuild` に対応 — XBIOS.CMT 結合 build
  - `XBIOS.CMT` を `runtime/templates/` に同梱。slangbuild が pc80mk2x build 時に main.cmt + XBIOS.CMT + 各 overlay を 1 本に結合 (= 旧 `COPY /B` / `cat` の手動結合が不要)
  - env file 新フィールド `cmt_concat:` (build 後の main bin 直後に concat する追加 .cmt ファイルの相対 path リスト)
  - 結合元ファイルが見つからない場合は明示エラー
  - 結合に消費された overlay は通常時クリーンアップ (`--keep-asm` 指定時は残る)
  - `THIRD_PARTY_NOTICES.md` に XBIOS.CMT の出典を記載

- pc80mk2 環境 (PC-8001mkII ROM 環境) を `slangbuild` に統合 — CMT (cassette tape) 出力対応
  - env file 新フィールド `output: cmt` で AILZ80ASM の CMT 形式出力 (`-cmt -gap 0`) に切替、出力拡張子は `.cmt`。`bin` / `cmt` 以外を指定するとエラー
  - `make ENV=pc80mk2 build / run / disk_image` のいずれでも `examples/PROG.cmt` が生成される。M88 等のエミュレータで `.cmt` をそのまま CLOAD 可能 (`EMU` 変数はユーザー側設定)
  - `--emit disk` を `disk:` セクション無し env で指定すると compile 前にエラー終了
  - overlay は `_m0.cmt` 別ファイルで出るのみで、main への結合 / loader 組み立てはユーザー側

- MZ-2500 環境を追加 (#172)
  - `sosmz2500`: S-OS 上で動作させる環境 (HuDisk 経由 D88 作成、既存 sos / sosx1 と同じフロー、MAGIC 系ライブラリは含めない)
  - `mz25iocs`: MZ-2500 BASIC システム / IOCS 上で動作させる最小環境。`PRINT` / `INKEY` のみ IOCS 経由で実装、その他入力系 (`LINPUT` / `GETL` / `GETLIN` / `INPUT`) は ESC キャンセル相当の stub return
  - 新ライブラリ: `runtime/libmz25iocs_print.asm` / `runtime/libmz25iocs_input.asm` (`libsos_print.asm` / `libsos_input.asm` を下敷きに、S-OS 呼び出しを IOCS 直叩きに置換)
  - 新サンプル: `examples/MZ25IOCS.SL`
  - `make run ENV=mz25iocs` で外部ツール `mzd88` 経由 D88 作成に対応 (= `mzd88` は外部依存、`make setup-tools` 対象外、`MZD88=/path/to/mzd88` で指定可)
  - 起動用 BASIC ローダ `runtime/mz2500/J8000.bas.bsd` を同梱 (= `&H8000` にバイナリをロードして `CALL` する最小コード)
  - `mz25iocs.env` に新 `env_type: 7` (MZ-2500 IOCS 系) 割り当て (`sosmz2500.env` は S-OS 系として `env_type: 2`)
  - 新 `Makefile` 変数 `MZD88` (default `mzd88`)、`BIN_EXT = .obj` for mz25iocs
  - `Makefile` の `OUTPROG = ...PROG.bin` ハードコードを `OUTPROG = ...PROG$(BIN_EXT)` に変更 (default `.bin` 維持、`BIN_EXT` を変える env で別拡張子に切替可能に)
  - 配布版 (`Makefile.dist`) の MZ-2500 対応は別途対応予定 (= dev ビルド `Makefile` のみ対応)
  - 詳細メモは `docs/MZ2500.md`

- slangbuild の MZ-2500 系環境 (sosmz2500 / mz25iocs) 対応 — `--emit disk` で D88 image 自動生成
  - sosmz2500: 既存 HuDisk driver でそのまま動作 (env file の `disk:` セクションは sos / sosx1 と同形式)
  - mz25iocs: 新規 mzd88 driver を追加。`mzd88 -blank` で空 D88 を生成し `mzd88 -add` で main + extra_files を格納
  - env file `disk:` セクション拡張: `title` (mzd88 `--title` 用、optional) / `extra_files` (= mzd88 で main 後に追加格納するファイル群、起動用 BASIC ローダ等)
  - 新 CLI option `--mzd88` + 環境変数 `MZD88_PATH` で path override 可能 (= ndc / hudisk と同パターンの解決順)
  - mz25iocs では overlay (`#MODULE`) は当面 scope 外 (= overlay bin が渡されると明示エラーで終了)
  - 配布版 (`Makefile.dist`) の `make ENV=mz25iocs disk_image` 統合は別途対応予定

- mzd88 (MZ-2500 D88 image 操作ツール、issaUt/mz2500-tools の C 実装、MIT) を 4 platform binary として同梱
  - `tools/mzd88-{osx-arm64,osx-x64,linux-x64,win-x64.exe}` を repo に commit
  - publish.sh が現在 OS 用の binary を `tools/mzd88(.exe)` にリネームコピー (= 配布 zip では 1 file)
  - ToolResolver.ResolveMzd88 を platform suffix 付き file 名でも探すよう拡張 (dev 環境で repo の `tools/mzd88-{rid}` を発見可能)
  - cross-build 手順: macOS は `cc -Os` + `clang -arch x86_64 -Os`、Linux/Windows は `zig cc -target x86_64-linux-musl|x86_64-windows-gnu -Os -Wl,-s`
  - source 改変なし。license 文 + 出典は `THIRD_PARTY_NOTICES.md` に記載

- 配布版 `Makefile.dist` の MZ-2500 系対応 (= `make -f Makefile.dist ENV=mz25iocs|sosmz2500 disk_image` で D88 自動生成)
  - sosmz2500: ENV ブロック追加 (`DISK_IMAGE = images/SOSPROG.D88`、sos / sosx1 と template 共用)、`disk_image` target で slangbuild + HuDisk 経路
  - mz25iocs: ENV ブロック追加 (`DISK_IMAGE = $(dir $(TARGET))M25PROG.d88`、`BIN_EXT = .obj`)、`disk_image` target で slangbuild + mzd88 経路
  - mzd88 path は `--mzd88` 明示せず `ResolveMzd88` の auto fallback に任せる (= 配布物では `tools/mzd88(.exe)`、dev 環境では `tools/mzd88-{rid}(.exe)` を発見、他 tool との非対称な唯一の例外)
  - help target の ENV 一覧に sosmz2500 / mz25iocs 追加

- README / setupenv.sh に Linux/WSL 環境での mono CP932 対応 (`libmono-i18n4.0-all`) 注意書きを追加 (= HuDisk 経路の sos / sosx1 / sosmz2500 で `Encoding 932 data could not be found` を踏むため、Debian/Ubuntu では `sudo apt install libmono-i18n4.0-all` で別途インストール必要、macOS Homebrew mono にはデフォルトで含まれるため不要)

## Version 0.23.0


- `#MODULE` (オーバーレイ) のモジュール専用ワーク対応 (#142)
  - モジュール直下の `VAR` / `ARRAY` を **モジュール私有ワークエリア `__WORK_M<N>__`** に配置。main と同名の変数を宣言しても物理メモリ上同居せず、各 overlay の swap 先でメモリを再利用できる
  - `#MODULE` 内に `WORK <定数式>` でモジュール専用ワークの ORG を明示可能 (未指定時は overlay コード末尾に連続配置)
  - `WORK` / `ORG` / `OFFSET` の各ディレクティブが定数式 (`CONST WA = $9000; WORK WA` など) を受けるように拡張
  - モジュール直下の初期値付き変数 / 固定アドレス指定 / トップレベル `#ASM` はコンパイルエラー化

- `#MODULE $addr RESIDENT` を追加 — overlay 間で共有するランタイム関数を main に集約してメモリ節約
  - 既存の `#MODULE $addr` (省略時) は従来通り Local モード (overlay 内に runtime 複製)。`RESIDENT` 指定で共有化が起動
  - 全 13 環境 (lsx / x1 / msxlsx / msx2 / msxrom / sos / sosx1 / pc80mk2 / pc80mk2x / pc88mk2sr / vgs0 / zxn / cpm) の runtime ライブラリに `; @resident shared|local` 属性を付与済 (= 共有 773 関数 / overlay-local 14 関数)
  - 実測 (`examples/MODTEST_RESIDENT.SL`): overlay バイナリが Local 248B → RESIDENT 57B (-77%)。overlay を増やすほど節約効果が大きい

- 二段アセンブル driver `slangbuild` を新ドライバとして追加
  - `slangc` は ASM 生成までを担当、`slangbuild` が main / overlay の AILZ80ASM 実行を orchestrate (GCC 的責務分離)
  - `Makefile.dist` を `slangbuild` 経由に切替。overlay 不要 (`#MODULE` 未使用) の SL は単段フローで動く
  - **prelink モード**: main / overlay 間で **任意の SLANG 関数の相互呼び出し** をサポート (cross-ref 検出時に自動有効化)。解決するのはアドレスのみで、swap 制御・呼び先 overlay のロード状態確認はユーザー責任

- overlay バイナリのディスクイメージ取り込みサンプルを追加
  - `tools/disk-add-overlays.py` を新設 (Mac / Win / Linux 共通、stdlib のみ)、`make ENV=lsx|x1 TARGET=examples/MODTEST_RESIDENT disk_image` が `PROG.com` + `M0.BIN` を d88 に書き込む
  - `examples/MODTEST_RESIDENT.SL` で LSX-Dodgers のファイル API (`FOPEN` / `FREAD`) 経由 overlay ロード → 実機エミュレータ (X Millennium / Cocoa1 等) で動作確認
  - **サンプル限定の最小実装**: 命名は `M<N>.BIN` 固定、overlay 0 が 128 byte 以内であることを前提

- `slangbuild --emit disk` で D88 ディスクイメージまで一気通貫ビルド (#157)
  - 新オプション `--emit disk` / `--disk-image <path>` / `--disk-template <path>` / `--ndc <path>` / `--hudisk <path>` を追加。`slangbuild input.SL -E lsx --emit disk --disk-image out.d88` 1 コマンドで slangc → AILZ80ASM → ディスクイメージ書き込みまで完結 (z88dk + appmake 相当)
  - env file に新規 `disk:` セクション (`format: d88` / `template` / `tool: ndc | hudisk` / `main_name` / `overlay_name` / `main_load` / `main_exec` / `overlay_load`)。アドレス値は `$3000` / `0x3000` / 10進すべて受理
  - pristine template は **`images/templates/`** に分離 (`images/templates/LSXPROG.D88` / `SOSPROG.D88`)。ビルドごとに `$(DISK_IMAGE)` 出力先にコピーしてから書き込み、template 自体は不変 (CI で SHA-256 比較により検証)
  - **対応済 env (`Makefile.dist disk_image`)**: lsx / x1 (ndc 経路)、sos / sosx1 (HuDisk 経路)
  - **従来経路維持 env**: msx2 / msxlsx / pc80mk2 / pc88mk2sr 等は従来の `tools/disk-add-overlays.py` 経路
  - `tools/disk-add-overlays.py` は legacy helper として残置 (旧経路ユーザー保護、新規利用は非推奨)
  - **動作環境**: `make setup-tools && make install` 後の installed 環境 (`~/.config/SLANG/`)、配布 zip 解凍直後、開発時の repo 直下、いずれでも動作。`make install` で `images/` + `tools/` も `~/.config/SLANG/` に配置、`ToolResolver` が install dir 配下を検索する。Linux/macOS の sos 系では `mono` (HuDisk.exe 起動用) と setupenv での S-OS template 取得が前提
  - **配布物の HuDisk**: `make setup-tools` が ho-ogino/HuDisk fork の `feature/write-ascii-mode` ブランチ (= ASCII 書き込み可能版) を curl で取得。Windows は `.exe` 直接実行、Linux/macOS は mono 経由起動
  - **配布同梱**: ライセンス都合で `ndc` / `HuDisk.exe` 本体は配布 zip に含めない (= `make setup-tools` でユーザー側ダウンロード)

- `make install` の default を `~/.local/bin` (= XDG Base Dir Spec) に変更 (Linux/macOS、Windows は元々 `%LOCALAPPDATA%\Programs\SLANG` でユーザーローカル)
  - 旧 default `/usr/local/bin` は sudo 必須で、かつ sudo 起動だと `$(HOME)/.config/SLANG` の `$HOME` が `/root` に取られて lib が user dir に入らない問題があった
  - 新 default はユーザーローカル完結 (sudo 不要)。install 完了メッセージで `~/.local/bin` を PATH に通す案内を表示
  - 従来通りシステムワイドにしたい場合は `sudo make install PREFIX=/usr/local CONFIG_DIR=/usr/local/share/slang` のように `CONFIG_DIR` も明示

- `cpm` 環境を独立 env として明示化 + 専用 file ライブラリ追加 (#145)
  - `runtime/env/cpm.env` を新設。これまで `-E cpm` は env file 不在で全 `runtime/*.asm` を fallback ロードしており、他環境のラベル衝突 (例: `libpc80mk2_print` の `WORK10`) を起こしていた
  - cpm 専用 `runtime/libcpm_file.asm` を新設。CP/M 2.2 互換 BDOS 関数 (`_RDRND` / `_WRRND`) ベースで RunCPM 上で動作 (lsx の `liblsx_file.asm` は CP/M 3+ 専用関数を使うため非互換だった)
  - `FREAD` / `FWRITE` は record-aligned (128 byte 単位)、`FGETC` / `FPUTC` は単一 active fnum + 128 byte 内部バッファ (lsx 完全互換ではない、制約は `runtime/libcpm_file.asm` ヘッダコメント参照)
  - `examples/CPMIOTEST.SL` + `examples/MODTEST_RESIDENT.SL` (cpm 経由) で RunCPM 実機動作を確認

- env 解決を厳格化 — 不明 env は即エラー化 (**breaking change**)
  - 従来は `slangc -E xxx` で env file 不在時に「全 `runtime/*.asm` を fallback ロード」して進行 → 後段で謎エラー
  - 新動作: 起動直後に env 解決失敗で `Error: Unknown environment 'xxx'` で即終了 (`exit 1`)
  - 既存の有効 env (`lsx` / `x1` / `sos` / `msxrom` / `cpm` 等) を指定するワークフローへの影響なし。`-E` typo や独自 env 名で env file 未配置のケースのみ挙動が変わる

- `slangbuild` の overlay 検出 glob を厳密化 — `--keep-asm` 残骸付きで次回 build が無限ループする問題を修正 (#152)
  - case-insensitive な FS (macOS APFS / Windows) で `_m*.ASM` パターンが旧 `--keep-asm` 残骸の `.dummy.imports.asm` 等まで拾い、prelink Pass 1 で再帰的に dummy/imports suffix が積まれる現象を `_m<digits>.ASM` 厳密一致 regex で post-filter

- `make publish-local` を dev `Makefile` に追加 — 現在 OS 向けに `dotnet publish` して `bin/` に slangc / slangbuild を配置する簡易 publish (Windows clone 直後でも `make -f Makefile.dist install / disk_image / run` のテストフローが回せるようにするための導線)
  - `RID` は default で current OS を自動検出 (`win-x64` / `osx-arm64` / `linux-x64` 等)。`RID=win-arm64 make publish-local` で上書き可
  - **release zip 作成ではない** (= リリース用は引き続き `make publish VERSION=x.x.x` → `publish.sh` で 4 platform 一括)

## Version 0.22.0

- X1 グラフィックライブラリ群を include/ に新設 (#139)
  - **TILELIB.LIB**: PCG タイル背景 (スーパーチップ 2×2 + マップ + スクロール + アニメ + ダブルバッファ)
  - **SPRLIB.LIB**: GVRAM 前景スプライト 最大 8 枚 / 16×16 (ダブルバッファ + old1 erase, 4 dot 単位座標)
  - **CHIPLIB.LIB**: GVRAM 直描画の全部入り (VVRAM 差分転送 + チップスプライト合成 + マスク + アニメ + スクロール)
  - **TILESPR.LIB**: TILELIB + SPRLIB 併用時のページ同期 shim
  - **UILIB.LIB**: GVRAM 静的 HUD (両ページ同時書き込み + OR 描画 + 9-slice 枠 + 256 glyph フォント内蔵)
  - 各ライブラリのサンプル + API リファレンスを examples/{chip,spr,tile,tilespr,ui}/ に配置
  - assets/ui/ に UILIB 用フォント資産 (font_charset1.png ASCII / font_charset2.png 日本語拡張 / window.png 9-slice / uicharset*.json CHARMAP) を同梱。美咲フォント由来、改変・商用・再配布自由の free software permit
  - tools/ にホスト側 Python ツール (png_to_asm.py / charmap-encode.py) を追加
- ランタイムに `PCGDEFS(startidx, ptr, count)` を追加 — 連続 PCG 定義の一括登録
- コンパイラに `CONST ASM` のコード生成を実装 — ライブラリ内パラメータをユーザー側 CONST ASM で上書き可能 (例: `_CHIP_VVRAMW_MAX`)
- x1 環境の `WIDTH()` を LSX-Dodgers と整合するよう修正 (#138)
  - LSX-Dodgers の CRTCD 領域に 16 byte LDIR で同期。プログラム終了後に LSX がプロンプト再描画しても画面が崩れない
  - X1 CRTC の 25 行表示を使い切るよう LSX `_HEIGHT` を 25 に書き込み
  - 従来の誤った `_PAGE_MINUS = -WIDTH*24` 固定計算も修正
- lsx 環境の PCHR が NUL バイトを出力していた問題を修正 (#138) — `make ENV=cpm run` で画面に何も出ない問題の原因
- sos 環境を機種非依存化、従来の X1 依存版は `sosx1` 環境として分離 (#137)
- TILELIB の TILE_ATTR アドレスと TileInit テキスト初期値を修正 (#140)
  - TILE_ATTR を X1 仕様通りの `$2000` に (従来 `$2800` はミラー経由で結果的に動作)
  - TileInit のテキスト VRAM 初期値を `$20` (空白) に (PCG glyph 0 = 定義済みタイル 0 が画面下端に露出する問題, X1 turbo / turboZ で顕著)

## Version 0.21.0
- ユーザー定義関数の FLOAT 引数・戻り値に対応
  - 宣言構文: `FX:FLOAT(FLOAT X) BEGIN RETURN X * X; END;`
  - 整数引数→FLOAT 引数の自動変換 (`i16tof24` 挿入)
  - WORD 戻り値との混在を型エラーで検出 (`Cannot pass FLOAT`/`Cannot return FLOAT`)
  - MACHINE 関数は戻り値型指定を拒否 (現状サポート外のため)
- ARRAY FLOAT に対応
  - ロード/ストアで 3 バイト単位の mantissa+exponent を正しく扱う
  - グローバル/ローカル/static の全経路、定数/動的インデックス両対応
  - `ARRAY FLOAT FA[3] = {1.5, 2.5, 3.5};` の初期値付き宣言をサポート
    - 整数リテラルは FLOAT に自動変換 (`{1, 2, 3}` → 1.0, 2.0, 3.0)
    - CONST 参照と FLOAT 定数式 (`{PI, PI/2.0}`) も評価可能
  - 配列要素への代入で整数→FLOAT の自動変換が動作
- FLOAT を指す間接変数 (PointerType) に対応
  - `VAR FLOAT FP[]; FP = &BUF[0]; FP[i] = 1.5;` の形で外部メモリを FLOAT 配列として扱える
  - ×3 スケーリング計算の共通ヘルパーで間接変数 3 経路 (load/AddressOf/store) を統一
- CP/M 実行環境を RunCPM (MIT) に切り替え
  - `make run ENV=cpm|lsx` の CP/M エミュレータを従来の `cpm` 実行環境から RunCPM に変更
  - macOS (arm64/x64) / Linux (x64) / Windows (x64) 4 プラットフォーム分のプリビルド RunCPM バイナリを同梱
  - SUBMIT/EXIT を使った自動終了方式で stdin リダイレクトに依存せず全 OS で動作
  - 配布 zip (Makefile.dist + publish.sh) でも RunCPM 一式を同梱して即実行可能に
- Makefile のクロスプラットフォーム対応強化
  - ツールパスを OS 別に `tools/AILZ80ASM` / `tools\AILZ80ASM.exe` で直接参照 (PATH 依存を排除)
  - `make clean` / 進捗表示 (`ls -la` vs `dir`) / ファイル移動 (`mv` vs `move`) を OS 別に分岐
  - Windows で bat に渡す引数のパス区切りを `\` に自動変換
  - `runcpm.bat` を ASCII + CRLF に統一

## Version 0.20.2
- CASE文の冗長なexprVal初回評価を削除（生成コードのサイズ縮小、JS版コンパイラとの整合性向上）
- x1環境で SGL ライブラリ（libx1_sgl_lsx）が使用可能に
  - LSX-Dodgers のシステム領域と衝突する固定アドレス依存を排除
  - VRAM_ADRS_TBL/BITLINE_BUFFER を @works に統合
  - BitLine の OR-trick を AND+ADD 方式に置換（64箇所）
- @works_align を絶対アドレスでアライン保証
  - EQU 仮想ベース方式 (`__WORK_ALIGNED_<N>__`) で実現
  - __WORK__ 自体は動かさず、WORK 指定の意味を壊さない
  - @works_align は 2 の冪のみ受け付け（不正値はエラー）
- FLOAT 周辺のバグ修正
  - `f24add` の指数差ちょうど18ビット時の誤分岐を修正（FCOS(3.1447)=0.31201 等の異常値を解消）
  - FLOAT 単項マイナスが整数演算として展開されていたバグを修正（`-T` で f24 値が破壊されていた）
  - `@alias` と `@name` 両方使用時に関数本体が重複出力される問題を修正
- ITOF/UTOF エイリアス追加（i16tof24/u16tof24 を呼びやすく）

## Version 0.20.1
- CASE文のカンマ区切り値（`6,7,8: body`）でbodyコードが重複生成される問題を修正
- FLOAT対応の強化
  - CONST定数式でFLOAT演算に対応（`CONST DEG2RAD = 3.14 / 180.0`）
  - ランタイム関数（FCOS, FSIN等）の戻り値型をFLOATとして正しく追跡
  - FL$()関数のPRINT出力に対応
  - FLOAT定数の即値ロード最適化（constant pool廃止、LD DE,imm / LD C,imm 方式）
  - FLOAT二項演算の直接レジスタロード最適化（halfDirectOps/reverseHalfDirectOps対応）
  - FLOAT比較演算の融合ジャンプ対応（fusedCompareJumps）
- x1環境の改善
  - _TXADRをLSX-Dodgers 1.62cの$EE8Eと共有化（GETLIN後のカーソル位置同期）
  - 改行時のスクロール処理をLSX-DodgersのBDOSに委譲（24行スクロール対応）
  - WIDTH(40/80)切替時にLSX-Dodgersのワーク変数を同期

## Version 0.20.0
- 新コンパイラ(slangc)への移行
  - IR(中間表現)ベースのコンパイラに全面書き換え（Parser → SemanticAnalyzer → IrGenerator → CodeGenerator）
  - ランタイムライブラリを .yml から .asm 形式に変更
  - #MODULE 指定時のモジュール分割ASMを直接出力（ModuleSplitter不要）
  - プリプロセッサ定数 ENV_TYPE / OS_TYPE を #IF 条件式で参照可能に
  - #IF プリプロセッサに定数式評価器を実装
  - 文字列データのShift-JIS変換対応
  - コード生成最適化: fusedCompareJumps、MACHINE直接レジスタロード、定数畳み込み等
- 旧コンパイラのソースは obsolete/ フォルダに移動

## Version 0.12.0
- ZX Spectrum Next環境（zxn）を追加
- PC-8801mkIISR環境（pc88mk2sr）を追加
- VGS-Zeroライブラリを1.24.0ベースに更新
  - vgs0_oam_set16()関数を追加
  - vgs0_debug()関数を追加
- Makefileの整理
  - ソースからのビルド・インストールを`make`、`make install`で行えるよう対応
  - `make publish`でリリースパッケージを作成可能に
- runtime.ymlにMIN()、MAX()関数を追加
- MEMSETのバグを修正（bcのデクリメント漏れ）

## Version 0.11.0
- VGS-Zero環境を追加
- 間接変数が正常に動作していなかったのを修正
- MSX0の開発環境を追加
- 最適化が効いていなかった問題を修正
- 引数スタック渡しの関数についてスタックの補正が漏れていたのを修正
- 二進数の解釈に失敗する事がある問題を修正
- BYTE変数でFORループした時に正しくループしない問題を修正
- 2のn乗での符号なしMODをANDに変換するよう対応
- シフト式の左右の値がWORDにキャストされない問題を修正

## Version 0.10.0
- pc80mk2x環境の追加
- X1のグラフィック関数の追加
- X1にマウス関数を追加
- 環境構築時に取得するAILZ80ASMのバージョンをv1.0.7に更新
- X1のS-OS環境でグラフィック関連関数、マウス関連関数を呼べるよう対応

## Version 0.9.0
- msxlsx環境の追加
- ビルド用Makefileの追加
- M8Aライブラリを20230325版に更新
- MSXのBDOSコールでIYを保存するよう対応
- output-debug-symbolをつけると_(関数名/変数名)_というシンボルを定義するよう対応
- PC-8001mkII環境の追加
- ModuleSplitterの追加

## Version 0.8.3
- インクルード／ライブラリフォルダを整理
- syntaxフォルダにVisual Studio Code用のSLANGシンタックスハイライト拡張機能を追加

## Version 0.8.2
- X1 SGLライブラリを追加
- ライブラリとして複数ファイルを読み込めるよう対応

## Version 0.8.1
- MSX ROM用関数をいくつか追加
- 標準関数にMEMCPYとMEMSETを追加
- 文字列0x80から0xffまでをバイナリ値として扱うよう変更
- MSX ROM環境でSOROBANが使えるよう対応

## Version 0.8.0
- MAGICライブラリ追加
- SOROBANライブラリ追加
- GRAPH.LIB、GRAPHF.LIB追加
- 圧縮データの展開ライブラリを追加
- RND()の乱数ロジックを変更

## Version 0.7.3
- FLOAT→WORDの自動キャストが正常に動かない場合があったのを修正
- CONSTでFLOAT値を定義出来るよう対応

## Version 0.7.2
- コンパイラの更新ミスがあったのを急遽修正

## Version 0.7.1
- X1turbo版S-OSにおいてPSG再生が出来ない問題を修正
- PSG再生のテンポを割り込みを持たないX1においても一定に保てるよう対応

## Version 0.7.0
- PSG再生ライブラリを追加(X1/MSX ROM)
- 外部シンボルを格納アドレス指定及びCONSTにて指定可能に

## Version 0.6.0
- Mac用のビルドスクリプト追加
- MAG画像読み込みライブラリを追加
- M8A画像読み込みライブラリを追加
- MSXのROM環境をお試しで追加
- 変数「WORKEND」がワークの末尾を指す変数として自動定義されるよう対応

## Version 0.5.0
- FLOAT型を試験的に追加(SLANG非互換)
- ランタイムを個々に読むのではなく環境ファイルを読むように変更(-Eオプションの追加)

## Version 0.3.0
- S-OS環境で文字入力によりワークが壊れる問題を修正
- ファイル入出力ライブラリの追加

## Version 0.2.0
- CONSTにCODEリストを与える事が出来るよう対応
- MACHINE関数について定義のみで実装出来なかった不具合を修正
- ELSEIFを追加
- 間接変数の二次元配列に対応
- EXIT(num)でnum個ぶんループを抜けられるよう対応

## Version 0.1.0
- --use-symbolオプションを追加
- プリプロセッサのIFの定数評価の仕組みを調整
- ELSEIFを追加

## Version 0.0.2
- 符号反転(NEGHL)が正常に機能しない問題を修正
- コマンドラインオプションをザッと実装

## Version 0.0.1
- 初版
