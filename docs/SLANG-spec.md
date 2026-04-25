# SLANG 言語仕様書

原典: Oh!X 1988年3月号掲載 / 1990年1月号再掲載 / 1993年9月号再掲載
作: 大貫 信昭

本ドキュメントは原典の仕様を整理し、新コンパイラ実装に必要な情報をまとめたものです。
(※) は原典に記載されていなかった情報（後の記事等で追加）。

---

## 概要

SLANGはC言語風のスタイルを持つ、S-OSオリジナルのプログラミング言語（コンパイラ）。
1パスでオブジェクトを出力し、最適化にも力を入れている。
文法はAlgol系をベースに、BASICやWICSの雰囲気も持つ。

---

## 書式に関する規定

### フリーフォーマット
- 基本的にC言語同様のフリーフォーマット（行の概念なし）
- 例外:
  - `//` コメント: 行末まで
  - `"文字列"`: 2行にまたがれない
  - 配列: 配列名と `[` の間を空白で区切れない
  - 関数: 関数名と `(` の間を空白で区切れない

### コメント
```
// コメント        // から行末まで
/* コメント */     ネスティング不可
(* コメント *)     ネスティング不可
```

### 文括弧（すべて互換）
```
{ }
[ ]
( )
｢ ｣
BEGIN ... END;
```

---

## プリプロセッサ (#コマンド)

```
#INCLUDE ファイル名    別のソースを取り込む（ネスティング不可）
#CHAIN ファイル名      続きのソースを読み込む
#IF 式                 条件付きコンパイル
#ELSE
#ENDIF
```

---

## アドレス宣言

```
ORG 定数式      オブジェクトコードの先頭アドレス
WORK 定数式     変数・配列のワークエリア先頭アドレス
OFFSET 定数式   コード生成時のオフセット
```

---

## 型

| 型表記      | サイズ | 説明 |
|------------|--------|------|
| `BYTE`, `!` | 1バイト | 8ビット符号なし整数 |
| `WORD`, `%` | 2バイト | 16ビット符号なし整数（**デフォルト**） |
| `FLOAT`, `%%` | 3バイト | 24ビット浮動小数点 **(※ クロスコンパイラ版拡張)** |

型を省略した場合は **WORD** とみなされる。

### FLOAT型について (クロスコンパイラ版拡張)
- 24ビット浮動小数点（3バイト）
- `%%` プレフィックスで宣言: `VAR %% myFloat;`
- 専用ランタイム関数: `f24add`, `f24sub`, `f24mul`, `f24div`, `f24cmp`, `f24neg`
- 型変換: `i16tof24` (整数→float), `FTOI` (float→整数)
- ローカル変数として確保する場合は3バイト（BYTE/WORDは常に2バイト）
- 定数畳み込み対応（コンパイル時のfloat演算）
式はすべて16ビット長で演算を行う。

---

## データ形式

### 定数

| 形式 | 例 | 説明 |
|------|-----|------|
| 10進数 | `1234`, `-5` | 数字の文字列 |
| 16進数 | `$ABCD`, `12ABH`, `0FFFFH` | `$` 接頭辞 または `H` 接尾辞 |
| 2進数 | `111011001010B` | `B` 接尾辞 |
| 文字定数 | `'A'`, `'\N'` | シングルクォート。エスケープ文字使用可 |
| 文字列定数 | `"メッセージ\n"` | 自動的に末尾 `$00` 付加。格納アドレスが値 |
| 記号定数 | `PC` | CONST宣言で定義 |
| `$` | | 次に生成するオブジェクトコードのアドレス |

### 登録済み記号定数
```
FALSE = 0
TRUE  = 1
```

### エスケープ文字

| 表記 | 値 |
|------|------|
| `\\` | `\` |
| `\"` | `"` |
| `\'` | `'` |
| `\N` | `$0D` |
| `\/` | `$0D` |
| `\C` | `$0C` |
| `\R` | `$1C` |
| `\L` | `$1D` |
| `\U` | `$1E` |
| `\D` | `$1F` |
| `\0` | `$00` |

---

## 変数宣言 (VAR)

### 単純変数
```
VAR HENSUU, ABC;
```

### 間接変数（変数としても配列としても使える）
```
VAR BYTE POINT[], %KANSETU[], FLOAT FP[];
```
間接変数の値をアドレスとしてメモリをアクセスする。
型省略時はWORD。FLOAT 指定時はアクセス単位が 3 バイトになり、
整数値の代入では自動的に `i16tof24` 変換が挿入される (クロスコンパイラ版拡張)。

```
VAR FLOAT FP[];
ARRAY FLOAT BUF[5];
MAIN() BEGIN
  FP = &BUF[0];
  FP[0] = 1.5;
  FP[1] = 7;            (* 整数→FLOAT 自動変換 *)
  PRINT(FL$(FP[0]));
END;
```

### アドレス指定
```
VAR XY:$C000, BYTE Z[]:$D000;
```

### 初期化（大域・静的宣言のみ）
```
VAR A=0, B=3, C[]=$C000;
```
初期化はコンパイル時のみ行われ、実行時には行われない。
初期値を持つ変数はWORK宣言に関わらずオブジェクトコード中に埋め込まれる。

### 二次元間接変数 (※)
```
VAR F[][15];
```

---

## 配列宣言 (ARRAY)

### 基本
```
ARRAY BYTE ABUF[5], WORD C[3], FLOAT FA[3];
```
**定数式+1個分** の配列が確保される（`ARRAY BYTE BUFF[10]` → BUFF[0]～BUFF[10] の11個）。
添字省略時は0とみなされ、1個分確保。型省略時はWORD。
FLOAT 配列は 1 要素あたり 3 バイト (mantissa 2byte + exponent 1byte) を確保する。

### 二次元配列
```
ARRAY BYTE ABC[5][3];
```

### アドレス指定
```
ARRAY ABC[10]:$C000;
ARRAY ABC[]:$C000;     // 添字省略可
```

### 初期化 ({CODEリスト})
```
ARRAY BYTE DT[4]={0, 1, 2, 3, 4};
```
初期値が足りない場合は0で埋められる。多すぎる場合はエラー。

### FLOAT 配列の初期化 (クロスコンパイラ版拡張)
```
CONST PI = 3.14;
ARRAY FLOAT FA[3] = {1.5, 2.5, 3.5};
ARRAY FLOAT FB[3] = {1, 2, 3};           (* 整数→FLOAT 自動変換 *)
ARRAY FLOAT FC[2] = {PI, PI / 2.0};      (* CONST 参照 + FLOAT 定数式 *)
```
要素は全て FLOAT (3バイト) として展開される。足りない分は 0.0 で埋められる。
`%` (BYTE/WORD キャスト) を FLOAT 配列のトップレベル要素に書くのは禁止
(混在を防ぐため。式の内部に含まれる形 (`(%5) + 1.0` 等) は許容)。

---

## CONST宣言

```
CONST PC=$8001, MZ=2000;
```
静的宣言と局所宣言の差異はなく、どちらも局所的な記号定数の宣言となる。

### CONST ASM (大域宣言のみ)

```
CONST ASM _CHIP_VVRAMW_MAX = 42;
CONST ASM _CHIP_VVRAMH_MAX = 28;
```

通常の CONST に加えて ASM のキーワードを付けると、コンパイラが出力 ASM
ソース中に `EQU` 疑似命令として定数値を書き出す。ライブラリ内の `#ASM`
ブロックや MACHINE 実装から同名ラベルとして参照できるようになるため、
ライブラリが提供するパラメータ (例えばバッファ最大サイズ) を、ユーザー側
SLANG ソースの CONST ASM で `#include` より前に再定義することで上書き
できる。

```
// ユーザーコード
CONST _CHIP_VVRAM_OVERRIDE = 1;
CONST ASM _CHIP_VVRAMW_MAX = 50;
CONST ASM _CHIP_VVRAMH_MAX = 30;
#include CHIPLIB.LIB

// ライブラリ側 (概念図)
#IF (_CHIP_VVRAM_OVERRIDE != 1)
CONST ASM _CHIP_VVRAMW_MAX = 42;
CONST ASM _CHIP_VVRAMH_MAX = 28;
#ENDIF
```

値はコンパイル時定数でなければならない (リテラル、既定義 CONST の式など)。

---

## MACHINE宣言（大域宣言のみ）

```
MACHINE MSUB(2):$C000;     // 外部サブルーチン、引数2個
MACHINE MON(0):$1F8E;      // 引数なし
MACHINE PRINTF();           // 引数数省略（スタック渡し、HLに引数数）
```

---

## 名前の有効範囲

- **局所的な名前**: 静的/局所宣言、仮引数、ラベル → 関数内のみ有効
- **大域的な名前**: 関数名、大域宣言 → プログラム全体で有効（宣言以後）
- 同名の場合、局所的な名前が優先

---

## 宣言の種類

| 種類 | メモリ割当 | 使用可能な機能 |
|------|-----------|--------------|
| 大域宣言 | 静的 | アドレス指定、初期化、MACHINE宣言 |
| 静的宣言 | 静的 | アドレス指定、初期化 |
| 局所宣言 | 動的 | **240バイト以内**。アドレス指定・初期化不可 |

---

## 関数

### 関数定義
```
SUB(X, Y)
  VAR I;            (* 静的宣言 *)
  BEGIN
    VAR L;          (* 局所宣言 *)
    I = X + Y;
    RETURN(I);
  END;
```

### 引数の型指定 (クロスコンパイラ版拡張)
仮引数に型を指定できる。指定しない場合は `WORD` として扱われる (既存互換)。

```
SUB(WORD A, FLOAT B, BYTE C)         (* 各引数の型を明示 *)
  BEGIN
    ...
  END;
```

- `WORD` (デフォルト): 2バイト、(IY+offset) に2バイト格納
- `FLOAT` (`%%`): 3バイト、(IY+offset) に3バイト格納 (mantissa 2byte + exponent 1byte)
- `BYTE` (`!`): 現状は WORD と同じ2バイト確保 (将来 1バイト化予定)

呼び出し側は仮引数の型に応じて値を渡す:
```
SUB(10, 1.5, 0)   (* 第1: WORD整数, 第2: FLOAT, 第3: BYTE/WORD *)
```

### 戻り値の型指定 (クロスコンパイラ版拡張)
関数名の直後に `:型` を書くと戻り値の型を指定できる。指定しない場合は `WORD` (既存互換)。

```
FX:FLOAT(FLOAT X)
  BEGIN
    RETURN X * X;
  END;

VAR FLOAT R;
MAIN()
  BEGIN
    R = FX(2.5);   (* 6.25 *)
  END;
```

- `FX:FLOAT(...)`: FLOAT を返す
- `FX:WORD(...)` または `FX(...)`: WORD を返す (デフォルト)
- 戻り値型が `FLOAT` の関数は **AHL レジスタ** で値を返す (A=exponent, HL=mantissa)
- 戻り値型が `WORD` の関数は従来通り **HL レジスタ** で値を返す

### 暗黙の型変換
関数引数および戻り値で、整数 (WORD/BYTE) → FLOAT への自動変換が入る (`i16tof24` 挿入)。
逆方向 (FLOAT → WORD) はコンパイルエラー。明示的に `FTOI` で変換する必要がある。

```
FX:FLOAT(FLOAT X) BEGIN RETURN X * 2.0; END;
MAIN() BEGIN
  R = FX(3);          (* 整数 3 が自動的に FLOAT 3.0 に変換される *)
  R = FX(FTOI(2.5));  (* 明示的な FLOAT→WORD は FTOI で *)
END;
```

### 引数個数チェック
関数の仮引数の個数と、呼び出し側の実引数の個数が一致しない場合はコンパイルエラー。
(型指定なし関数の場合は型情報がないため個数チェックも行わない = 既存互換)

### MACHINE 関数の戻り値型 (現状制限)
MACHINE 関数 (`FOO:アドレス(N);` 形式) には戻り値型を指定できない (常に WORD)。
`FOO:FLOAT(2);` のように書くとコンパイルエラーになる。将来対応予定。

### 関数の返値
- `RETURN(式);` で返す
- `END(式);` で関数定義の末尾で返す
- 関数から戻ったときの値 (戻り値型に応じて HL または AHL) が関数の値

### MAIN関数
プログラムには必ず `MAIN()` 関数が必要。実行 = MAIN()の実行。
MAIN()の定義位置は任意。

---

## 関数コールの実際（呼出規約）

### IYレジスタによるローカル変数管理

```
引数ワーク:   (IY+$70) ～ (IY+$7F)   最大8個
動的変数:     (IY+$00) ～ (IY+$6F)   最大240バイト
```

#### 引数渡し（ユーザー関数）
WORD 引数の場合 (2バイト):
```asm
LD   HL,(VARA)
LD   (IY+$70),L
LD   (IY+$71),H     ; 第1引数
LD   HL,(VARB)
LD   (IY+$72),L
LD   (IY+$73),H     ; 第2引数
CALL SUB
```

FLOAT 引数の場合 (3バイト)、`PUSH AF + PUSH HL` で AHL 値をスタックに退避し、
呼び出し側で IY+offset に書き込む:
```asm
; FX(2.5) — 第1引数 FLOAT
LD   HL,$4000      ; mantissa
LD   A,$40         ; exponent
PUSH AF
PUSH HL
; CALL 直前で POP HL → IY+$70..$71 (mantissa)、POP AF → IY+$72 (exponent)
CALL FX
```

BYTE/WORD/FLOAT 引数を混在させた場合、各引数のサイズに応じて IY+offset の格納位置と
バイト数が決まる (FLOAT は 3 バイト、それ以外は 2 バイト)。

#### 関数プロローグ/エピローグ
動的変数がある場合:
```asm
; プロローグ
PUSH IY
LD   BC, n      ; n = 動的変数の合計バイト数
ADD  IY, BC
; エピローグ
POP  IY
```

#### 動的変数のアドレス例
```
SUB(I, J)         ; 仮引数
  VAR K;          ; 静的宣言（静的メモリ）
  BEGIN
    VAR L;        ; 局所宣言（動的）
```
動的変数は I, J, L の3個:
```
I: (IY+$6A), (IY+$6B)   ← 実引数の値で初期化
J: (IY+$6C), (IY+$6D)   ← 実引数の値で初期化
L: (IY+$6E), (IY+$6F)   ← 不定
```

### MACHINE関数の引数渡し

| 引数数 | 渡し方 |
|--------|--------|
| 0個 | CALLのみ |
| 1個 | HL |
| 2個 | HL, DE |
| 3個 | HL, DE, BC |
| 4個以上 | スタックに積んでCALL |
| 省略 | スタックに積み、HLに引数の数を代入してCALL |

---

## 演算子

### 算術演算子
```
+  -  *  /  MOD        （符号なし）
+(単項)  -(単項)
```

### ピリオド演算子（符号付き）
```
.*.  ./.  .MOD.  .<<.  .>>.  .<=.  .>=.  .<.  .>.
```

### ビット演算子
```
AND  OR  XOR  CPL
<<  >>
HIGH  LOW
```

### 論理演算子
```
NOT    （論理否定、真=1/偽=0）
```

### 関係演算子（真=1、偽=0）
```
==  <>  !=  >  >=  <  <=
```

### 代入演算子
```
=
```

### その他
```
++  --    インクリメント/デクリメント（前置・後置）
&         アドレス演算子
?:        三項演算子
,         カンマ演算子
```

### 演算の優先順位（上が高い）
```
1.  ( ) [ ]
2.  ++ -- &
3.  + - HIGH LOW NOT CPL    （すべて単項）
4.  * / MOD << >> .*. ./. .MOD. .<<. .>>.
5.  + -
6.  == <> != <= >= < > .<=. .>=. .<. .>.
7.  AND OR XOR
8.  ? :
9.  =
10. ,
```

**注意**: 原典の優先順位では `AND OR XOR` が関係演算子より低い。
`&&` `||` は原典の仕様書には記載されていない（後の拡張と思われる）。

---

## 文

### IF文
```
IF 式 [THEN] 文1 [ELSE 文2] [ENDIF;]
```
- `ELSE IF` は `ELSEIF` または `EF` とも書ける
- THEN, ENDIF は省略可

### FOR文
```
FOR 変数名 = 式1 TO|DOWNTO 式2 [DO] 文 [NEXT;]
```
- **まず文を実行してから、終値の判定を行う**（do-while型）
- 式1と式2の間に0をはさむ場合、1回で終了

### WHILE文
```
WHILE 式 [DO] 文 [WEND;]
```

### REPEAT文
```
REPEAT 文 UNTIL 式;
```

### LOOP文 (※)
```
LOOP 文;
```
永久ループ。

### CASE文
```
CASE 式0 [OF] {
  定数式1 [:] 文1
  定数式1 TO 定数式2 [:] 文1    ← 範囲指定
  定数式1, 定数式2, 定数式3 [:] 文1  ← 複数値
  OTHERS [:] 文
}
```
上から順に比較。一致したら実行してCASE文を脱出。

### EXIT文
```
EXIT;                    ループから脱出（C言語のbreak）
EXIT TO ラベル名;        ラベルにジャンプ（あと戻り不可）
EXIT(式);               （※）式の値の数だけループを抜ける
```

### CONTINUE文
仕様書に明示的記載なし（後の拡張と思われる）。

### RETURN文
```
RETURN;
RETURN(式);
```

### GOTO文
```
GOTO ラベル名;
```
EXIT TOと違い、ジャンプ先に制限なし。

---

## システム配列

| 名前 | 説明 |
|------|------|
| `MEM[式]` | メモリを1バイト単位でアクセス |
| `MEMW[式]` | メモリを2バイト単位でアクセス |
| `PORT[式]` | I/Oポートを1バイト単位でアクセス |
| `PORTW[式]` | I/Oポートを2バイト単位でアクセス |
| `SOS[式]` | S-OS特殊ワークを1バイト単位でアクセス |
| `SOSW[式]` | S-OS特殊ワークを2バイト単位でアクセス |

---

## 登録済み変数

| 名前 | 説明 |
|------|------|
| `^A` | Aレジスタ。CALL/GETREG関数で使用 |
| `^BC`, `^DE`, `^HL` | 各レジスタペア |
| `^IX`, `^IY` | インデックスレジスタ |
| `^AF` | AFレジスタ |
| `^SP` | スタックポインタ |
| `^CARRY`, `^CY` | CYフラグ（1 or 0） |
| `^ZERO` | Zフラグ（1 or 0） |
| `@KBUFF` | キー入力用バッファのアドレス |

---

## システム関数（CODE / PRINT）

### CODE関数
直接データをオブジェクトに落とす。式中で使用可。

#### CODEリスト項目
| 形式 | 説明 |
|------|------|
| `"文字列"` | 文字列をそのまま出力（末尾$00なし） |
| `[式]` | 式の値をHLに代入するコード |
| `<ラベル名>` | ラベルのアドレスを2バイトで出力 |
| `型, 定数式` | 型に応じて1or2バイトで出力。型省略時は1バイト |

### PRINT関数

| 書式項 | 説明 |
|--------|------|
| `"文字列"` | 文字列をそのまま出力 |
| `/` | 改行 |
| `値` | 10進左詰め出力 |
| `FORM$(値, n)` | 10進n桁右詰め |
| `DECI$(値)` | 10進5桁右詰め |
| `%(値)` / `PN$(値)` | 符号付き10進左詰め |
| `HEX2$(値)` | 16進2桁 |
| `HEX4$(値)` | 16進4桁 |
| `MSG$(値)` | アドレスから$0Dの直前までASCII出力 |
| `MSX$(値)` / `!(値)` | アドレスから$00の直前までASCII出力 |
| `STR$(値, n)` | 値のキャラクタをn個出力 |
| `CHR$(n)` | 上位・下位バイトの順にASCII出力 |
| `SPC$(n)` | 空白をn個出力 |
| `CR$(n)` | 改行をn個出力 |
| `TAB$(n)` | カーソルをn回右移動 |

---

## 登録済み基本関数

| 関数 | 説明 |
|------|------|
| `BEEP()` | BEEP音 |
| `STOP()` | 実行終了 |
| `LOCATE(X, Y)` | カーソル移動 |
| `INKEY(n)` | キー入力（n=0: GETKY, n=1: FLGET, 他: INKEY） |
| `INPUT()` | 数値入力 |
| `GETL(addr)` | 1行入力 |
| `GETLIN(addr, len)` | 長さ指定1行入力 |
| `LINPUT(addr, len)` | カーソル以降読み込み |
| `WIDTH(n)` | 画面モード切替 |
| `SCREEN(X, Y)` | 画面キャラクタ読み出し |
| `PRMODE(n)` | PRINT出力先切替 |
| `BIT(val, n)` | 第nビット取得 |
| `SET(val, n)` | 第nビットを1に |
| `RESET(val, n)` | 第nビットを0に |
| `ABS(n)` | 絶対値（符号付きとみなす） |
| `SEX(n)` | 符号拡張（1バイト→2バイト） |
| `SGN(n)` | 符号判定（正:1, 零:0, 負:-1） |
| `RND(n)` | 0～n-1の乱数 |
| `VTOS(val, buff)` | 数値→文字列変換（buff 6バイト必要） |
| `GETREG()` | 全レジスタ値を登録済み変数に代入 |
| `CALL(addr)` | レジスタ設定してコール |

---

## 制限事項

| 項目 | 制限 |
|------|------|
| 動的局所域 | 1関数240バイト |
| 引数ワーク | 最大8個 |
| ループネスト | 16レベル |
| 行長 | 255文字 |
| 名前長 | 32文字 |
| #INCLUDEネスト | 8レベル |

---

## 新コンパイラでの拡張点（原典との差異）

以下は新コンパイラ (クロスコンパイラ版) で追加された機能:

- `FLOAT` (`%%`) 型: 浮動小数点演算
- `&&`, `||`: 論理AND/OR演算子
- `CONTINUE` 文
- `LOOP` 文
- `EXIT(式)`: 多重ループ脱出
- `+=`, `-=`, `*=`, `/=`: 複合代入演算子
- マルチプラットフォーム対応（MSX, X1, PC-8001, PC-8801, ZX Next, VGS0等）
- ランタイムライブラリシステム
- モジュールシステム (`#MODULE`)
- プリプロセッサ定数 `ENV_TYPE` / `OS_TYPE`（`#IF` 条件式で環境判定可能）

---

## MODULE/オーバーレイ機能

`#MODULE` 指定により、同一アドレス空間で動的に切り替えて使用するモジュールを分割出力できる。

新コンパイラでは、コンパイラが直接モジュール単位のASMファイルを出力する（ModuleSplitter不要）:

```
slangc source.SL
  → source.ASM       (メイン部)
  → source._m0.ASM   (モジュール0)
  → source._m1.ASM   (モジュール1)
  → source._inc.ASM  (共有シンボル定義)
```

- 全モジュールを1パスでコンパイル（シンボルテーブル共有）
- 共通シンボルファイル(.inc)を生成して各ASMからINCLUDE

### モジュール専用ワークエリア (v0.22)

`#MODULE` 直下で宣言された `VAR` / `ARRAY` は **モジュール私有のワークエリア
`__WORK_M<N>__`** に配置され、メイン側 `__WORK__` とは物理的に独立した
領域になる。これにより複数モジュールを同じメモリ範囲に swap しても各
モジュールの変数が干渉しない、本来のオーバーレイ動作が得られる。

- モジュール私有変数の ASM ラベルは **`_V_M<N>_<NAME>`** (例: module 0 の
  `X` → `_V_M0_X`)。メイン側 `.inc` には出力されない
- 関数定義 / `MACHINE` / `CONST` は従来どおり **global** に登録される
  (main から module の関数を呼び出す既存運用を維持)
- モジュール内関数から、メイン側 global 変数と自モジュール私有変数の
  両方を参照できる。同名の場合は自モジュール私有側が優先
- モジュール内関数本体の **インライン `#ASM`** は従来どおり使用可能

#### `WORK` ディレクティブ (module 内)

`#MODULE` 内で `WORK <定数式>` を書くと、そのモジュール専用ワークの
ORG を明示できる:

```slang
#MODULE $8000
    WORK $9000          /* __WORK_M0__ を $9000 に配置 */
    VAR X;
    ARRAY BYTE BUF[16];
    SUB()
    BEGIN
        X = 1;
        BUF[0] = $42;
    END;
#END
```

- `WORK` を省略した場合、モジュール私有ワークは overlay コードの末尾に
  連続配置される
- アドレスには定数式が使える (`CONST WA = $9000; WORK WA` など)。
  メイン側のトップレベル `WORK` も同様に定数式対応

#### モジュール直下での制約

以下の構文はモジュール直下 (`#MODULE ... #END` のトップレベル) では
**コンパイルエラー** となる:

- 初期値付き `VAR X = 10;` / `ARRAY BYTE A[] = {1,2,3};`
  — swap 時の初期化タイミングが曖昧なため。必要なら main の共有ワーク
  として宣言する
- 固定アドレス指定 `VAR X:$9000;` / `ARRAY BYTE A[]:$ADDR;`
  — モジュール私有の namespace と固定アドレスの組み合わせは意味論が
  複雑なため。固定アドレスが必要なら main トップレベルで宣言する
- トップレベル `#ASM ... #END` ブロック
  — overlay GlobalData 未サポート。必要なら関数本体にインライン展開する

（関数内ローカル `VAR` / `ARRAY` やインライン `#ASM` はこの制限の対象外）

### モジュールのランタイム集約ポリシー (v0.23 PR-A: 内部設計)

`#MODULE` ヘッダ位置に optional のポリシー識別子を書ける:

```slang
#MODULE $8000             /* = Local (省略時、現状互換)         */
#MODULE $8000 RESIDENT    /* = Resident (main 集約モード)        */
#MODULE $8000 SELFCONTAIN /* = SelfContain (将来予約、現時点エラー) */
#MODULE $8000 AUTO        /* = Auto (将来予約、現時点エラー)        */
```

組み合わせは **ランタイム関数側 `@resident` 属性** との 2 軸で決まる:

```asm
; runtime/foo.asm 内で関数ヘッダに付与
; @name MPRNT
; @resident shared        ← main 常駐に向く (PRINT 系の大物等)
; ...
; @name FRAGILE
; @resident local         ← 各 ASM に local 強制 (override)
; ...
```

#### コンフリクト解決マトリクス

| Module ↓ \ Function → | Local (default = 未指定) | Shared |
|---|---|---|
| Local (module default)     | local | local (module 側 Local が勝つ) |
| Resident                   | local (override 効く) | **shared** ← メモリ節約効果 |

メモリ節約効果は `RESIDENT` × `@resident shared` の交点のみ。安全な
関数だけ opt-in で集約され、`@resident local` を明示した関数は #MODULE
RESIDENT でも overlay 内に local 展開される (self-modifying / overlay-
specific WORK を持つ関数を守る安全策)。

#### 現時点 (PR-A) の制約

- 本機能は **ランタイム共有の内部設計まで** が PR-A スコープ。実バイナリ
  でリンクされるには別 PR (PR-B = 二段アセンブル toolchain) が必要
- 既存 runtime ライブラリには `@resident shared` がまだ付いていないので、
  `#MODULE $addr RESIDENT` を書いても現時点では **何も共有されない**
  (動作は完全に現状互換)。共有効果は別 PR (PR-C) で `MPRNT` / `P10` /
  `VTOS` 等に段階的に `@resident shared` を付与してから出る
- 未実装ポリシー (`SELFCONTAIN` / `AUTO`) はコンパイルエラーになる
- overlay ASM 末尾に `; === Shared Runtime References ===` という EXTERN
  コメントが出力される (= PR-B での EQU 注入の入力リスト)

### 二段アセンブル toolchain `slangbuild` (v0.23 PR-B)

PR-B で新ドライバ `slangbuild` を追加。`slangc` を呼んで ASM 群を生成し、
AILZ80ASM で main → overlay の順に二段アセンブルする。`#MODULE RESIDENT`
で main 集約された runtime シンボルを overlay 側で実バイナリレベルで解決する:

```
slangbuild source.SL -E lsx
  → source.bin              (main bin)
  → source._m0.bin          (overlay 0 bin、main の shared label を参照済み)
  → source._mN.bin          (...)
```

`#MODULE` を使わない通常の SL でも `slangbuild` は使える (単段フローで
従来の `slangc + AILZ80ASM` と同じ結果)。

#### 二段アセンブル機構

1. `slangc source.SL` → `source.ASM` + `source._mN.ASM` + `source.inc`
2. `AILZ80ASM source.ASM -sm minimal-equ` → `source.bin` + `source.sym`
3. 各 overlay について: overlay ASM 内の `; EXTERN` リストと `source.sym`
   の交集合だけを抽出した `source._mN.imports.asm` を生成 (raw `source.sym`
   を渡すと compiler 内部ラベル等の衝突リスクがあるため filter する)
4. `AILZ80ASM source._mN.imports.asm source._mN.ASM -sm minimal-equ` →
   `source._mN.bin`

#### CLI

```
slangbuild <input.SL> [options]
  -o <prefix>     Output file prefix (default: derived from input)
  -E <env>        Environment name (default: lsx)
  -I <path>       Include search path passed to slangc (repeatable)
  -L <path>       Library search path passed to slangc (repeatable)
  --asm <path>    AILZ80ASM executable path (override resolution)
  --slangc <path> slangc executable path (override resolution)
  --keep-asm      Keep intermediate ASM / sym files
  --verbose       Show subprocess (slangc / AILZ80ASM) output
```

#### ツール解決順 (実行ファイル基準)

`AppContext.BaseDirectory` を起点に決定論的に探す。配布スクリプトでは
`--slangc` / `--asm` を明示指定する運用 (PATH 優先は再現性が低いため):

- **slangc**: `--slangc` → bundled bin (slangbuild と同 publish 物) → PATH → `dotnet run --project ...` (dev fallback、SDK 必要)
- **AILZ80ASM**: `--asm` → `AILZ80ASM_PATH` 環境変数 → PATH → bundled `tools/AILZ80ASM` → repo root (dev fallback)

#### 現時点 (PR-B) の制約

- `slangbuild` は二段アセンブル機構を提供するが、**実際にメモリ節約効果が
  出るには runtime ライブラリへの `@resident shared` 付与 (PR-C) が必要**。
  本 PR-B 単体では現状互換 (overlay に runtime が複製展開される) のまま動く
- AILZ80ASM の制約により、imports ファイルの拡張子は `.asm` を使う
  (`.sym` 拡張子は出力扱いされてエラーになる)
- 未解決ラベル (overlay の `; EXTERN` リストにあるが main.sym にない) は
  `--verbose` 時に warning として表示。AILZ80ASM 側で「未定義シンボル」
  エラーで止まる

### 全ファイル間関数 cross-reference 解決 (v0.23 PR-B2)

PR-B2 で `slangbuild` を拡張し、`main / overlay 0 / overlay 1 / ...` の
任意の組み合わせ間で **SLANG 関数を相互呼び出し** できるようにする
(prelink 二段アセンブル方式)。

#### サポート範囲 (関数シンボルのみ)

- main → overlay 関数
- overlay → overlay 関数
- overlay → main 関数

#### 仕様 (= ユーザー責任)

- 解決するのは **アドレスだけ**。呼び先 overlay が実行時にメモリにロード
  されている保証は driver / runtime には無い
- 同一 ORG を共有する overlay 間 call は未定義動作になりうる
- swap タイミング / 呼び出し可否はすべて **ユーザー責任**

つまり SLANG は呼び出しコードを `CALL <address>` として吐くだけで、その
address が今そこに居るかは関知しない (低レベル言語の責務分担)。

#### 仕組み (内部、参考)

`slangc` が各 ASM に **Exports** (= 自分が定義する関数: `; FUNC <name>`) と
**User Function References** (= 自分が呼ぶ他ファイル関数: `; EXTERN <name>`)
の 2 セクションをコメントとして出力する。`slangbuild` がこれらを検出した
場合、自動的に prelink モードに入る:

```
Pass 1: 各 target に dummy imports (全 extern を $0000 EQU) を渡してアセンブル
        → 各 target.pass1.sym 取得 (= 自身が定義する label のアドレス確定)
Pass 2: 全 target の Exports セクションに列挙された関数だけを拾って
        ExportedFunctionTable を構築
        (注: runtime 関数等 export 宣言されていない label は除外、衝突なし)
Pass 3: combined imports.asm を生成 (= user function は ExportedFunctionTable
        から、shared runtime / globals は main.sym から解決) → 各 target を
        本番アセンブル
```

`-nsa` (no super-assemble) は **prelink モード時のみ** 付与され、Pass 1
と Pass 3 で同じ target 内のラベルアドレスが一致することを保証する。
PR-B 既存の単段フロー (cross-ref 無しの場合) では `-nsa` を付けない。

#### 制約 (本 PR-B2)

- 関数シンボルのみ対応。overlay private 変数 / overlay work / data の
  cross-reference は未対応
- `#MODULE` のネストは禁止 (`#END` 不足の検出を兼ねて compile エラー)
