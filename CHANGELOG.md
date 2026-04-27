# 更新履歴

## Unreleased (v0.23.0 候補)

- `runtime/libcpm_file.asm` の scope B 拡張: 汎用 file API として動作可能に
  - `FREAD` / `FWRITE` を **record-aligned multi-record loop** 化 (size パラメータを honor、返り値 = 実際に読み/書きできた records × 128)
  - `FGETC` / `FPUTC` を **single-active-buffer 方式** で実装 (128 byte 内部バッファ + 1 active fnum + read/write mode 排他)
  - 新規 `examples/CPMIOTEST.SL` を追加し RunCPM 上で end-to-end 検証 (FGETC/FPUTC 200 byte round-trip + FREAD multi-record + FREAD partial 動作確認)
  - **制約 (重要)**:
    - `FREAD` / `FWRITE` は record-aligned (128 byte 単位)、size の 128 未満端数は切り捨て (sub-record 精度なし)。任意 byte 単位が必要なら `FGETC` / `FPUTC` を使う
    - 同一 fnum で `FGETC` / `FPUTC` の **read/write mode 切替は未サポート** — `FCLOSE` → `FOPEN` で reopen 必須 (= サイレント誤動作を防ぐため、未サポート組合せは $FF を返す)
    - 同一 open file で `FGETC` / `FPUTC` と `FREAD` / `FWRITE` / `FSEEK` を混在させるのは未サポート — 後者の呼び出し時に active buffer は invalidate される
    - lsx 完全互換ではない (lsx の FREAD は CP/M 3+ の variable record size を使い、任意 byte 数を正確に R/W できる)
    - 128 byte 境界整数倍ファイルなら `FREAD` / `FWRITE` で正確にコピー可能。slcopy.sl を cpm で動かす場合は対象ファイルが 128B 境界であることを caller が保証するか、`FGETC` を loop する必要がある
  - `FPUTC` は write mode 入口時 / flush 後に ACTBUF を 0 で初期化 (= partial record の tail が stale data 漏れせず確定 0 になる)
  - work 領域増加: 132 byte (ACTBUF + state)。cpm 全体で ~858 → ~990 byte
  - bug fix (実装中に発見): `FPUTC` 初期化時の `LDIR` (ACTBUF 0 クリア) が DE レジスタを破壊し、初回 byte が garbage 値で書かれる問題を修正 (`PUSH DE` / `POP DE` で chr 引数を保護)

- cpm 環境向け file ライブラリ `runtime/libcpm_file.asm` を新設
  - `liblsx_file.asm` のコピー + CP/M 2.2 互換書き換え (random access `_RDRND` $21 / `_WRRND` $22 ベース、`liblsx_file` の CP/M 3+ `_RDBLK` $27 / `_WRBLK` $26 が RunCPM で動作しない問題を解消)
  - `runtime/env/cpm.env` の参照を `liblsx_file.yml` → `libcpm_file.yml` に切り替え。lsx / x1 等他 env は引き続き `liblsx_file.asm` を使用、影響なし
  - **scope A (本 PR 範囲)**: `FREAD` / `FWRITE` は 1 record (128 byte) 固定実装、`size` パラメータは無視。FCB+33..36 (random record) は成功時に内部 helper `FCBRECINC` で +1 され、sequential semantics (連続 FREAD で次 record を読む) を維持。`FSEEK` も整合 (FCB+33..36 を直接更新)。`FGETC` / `FPUTC` はスタブ ($FF return)
  - **scope B (follow-up)**: `FREAD` / `FWRITE` の multi-record loop 化、`FGETC` / `FPUTC` の 128 byte 内部バッファ化
  - 動作確認: `make ENV=cpm TARGET=examples/MODTEST_RESIDENT run` で RunCPM 上 "Module value: 100 / Main value: 10" の end-to-end 動作 (PR #151 で lsx/x1 がカバーされていなかった cpm を追加でカバー)

- `examples/MODTEST_RESIDENT.SL` に overlay loader (FOPEN/FREAD/FCLOSE) を実装、`Makefile.dist` を拡張して `make ENV=lsx|x1 TARGET=examples/MODTEST_RESIDENT disk_image` が D88 イメージに `PROG.com` + `M0.BIN` を書き込むようにした
  - `tools/disk-add-overlays.sh` (POSIX) を新設、`PROG._m*.bin` を staging 経由で `M0.BIN`/`M1.BIN`/... として d88 へ書き込む。古い `M*.BIN` (前回ビルド残骸) は事前削除。一時 staging dir (`examples/.staging/`) は trap で都度削除
  - エミュレータ (X Millennium / Cocoa1 等) で d88 起動 → "Module value: 100 / Main value: 10" の end-to-end 動作を確認 (LSX-Dodgers の FILES API 経由)
  - `tools/runcpm.sh` も同じ命名規則 (`M<N>.BIN`) で staging するように拡張。当初 RunCPM (CP/M 2.2 互換) では FREAD が CP/M 3+ の BDOS `_RDBLK` ($27) を使うため動作しなかったが、後続で `runtime/libcpm_file.asm` (CP/M 2.2 互換実装) を新設して解消、cpm 環境でも end-to-end 動作する
  - 汎用 loader 機構ではなく **サンプル限定の最小実装** で、命名規則は `M<N>.BIN` (= overlay インデックスと一致)、loader は overlay 0 が 128 byte 以内であることを前提。他 env (sos/msx/pc88/vgs0/zxn) は別 loader API のため本変更の対象外
  - Windows 環境の disk_image 拡張は本 PR では対応せず (Makefile.dist の `ifneq ($(OS),Windows_NT)` で skip)。POSIX shell スクリプトのため、Windows ユーザーは手動で M0.BIN を d88 に追加する必要あり

- `cpm` 環境を独立した env として明示化 (#145)
  - `runtime/env/cpm.env` を新設 (env_type/os_type は lsx と同じ 0/0、libraries は lsx 互換)。条件コンパイル `#IF (ENV_TYPE<=1)` 等の意味は変わらない
  - `Makefile.dist` の `ENV=cpm` で `SLANGENV=cpm` を参照
  - これまで `-E cpm` 指定時は env file が見つからず「全 runtime/*.asm を fallback ロード」していたため、`libpc80mk2_print` の `@works WORK10:10` と `liblsx_print` 等の local `WORK10:` ラベルが AILZ80ASM 段階で衝突していた (Issue #145)。cpm.env 追加により lsx 互換セットのみがロードされ衝突解消

- env 解決を一本化、未定義 env を即エラー化 (**breaking change**)
  - 従来: `slangc -E xxx` で `xxx.env` が見つからなかった場合、Preprocessor 用と Runtime ロード用に env 解決が **二重に走り**、後段が見つからない場合は `runtime/*.asm` を **全部 fallback ロード** していた (これが Issue #145 の根本原因)
  - 新動作: 起動直後に **1 回だけ** env を解決。見つからない env は `Error: Unknown environment 'xxx'` で **即終了 (exit 1)**。fallback 経路は廃止
  - ファイル不在 (`Unknown environment '<name>'`) と YAML 破損 (`Failed to load env file for '<name>'`) を別エラーで報告
  - 既存の有効 env (`-E lsx` / `-E x1` / `-E sos` / `-E msxrom` / `-E cpm` 等) を指定するワークフローへの影響無し。`-E` を typo した場合や、独自に env 名を作っていて env file を用意していなかった場合のみ挙動が変わる

- 残り 10 環境 (msxlsx / msx2 / msxrom / sos / sosx1 / pc80mk2 / pc80mk2x / pc88mk2sr / vgs0 / zxn) の runtime にも `; @resident shared|local` を付与 (PR-C2) — PR-C1 の手順を機械的に横展開
  - 対象 30 ファイル / 527 関数の追加付与 (env ごとに 1 commit)
  - 真の self-mod として `local` 化した関数 (PR-C2 で新規):
    - `libsos_print.PRMODE` / `libpc80mk2xbios_print.PRMODE` (PRT+1 の operand patch)
    - `libp88_base.PSET` (PSETADR / PSETCOLOR の operand patch)
    - 各 base ファイルの `SLANGINIT` (8 件、main inline 専用)
  - **全 env 累計**: shared 773 関数 / local 14 関数 (10 base SLANGINIT + M8ALOAD + 3 PRMODE/PSET)
  - smoke 検証 (env ごと): `slangc -E <env> examples/<sample>.SL` で SLANG 段の compile 成功確認。pc80mk2 / pc80mk2x は `AILZ80ASM` までのフルアセンブルも `0 error/warn` (#145 系統のため厚めに)
  - 全テスト 190 / 190 合格 (PR-C1 と同じ test base、回帰なし)
- lsx / x1 環境の runtime ライブラリに `; @resident shared|local` を全関数付与 (PR-C1) — `#MODULE $addr RESIDENT` で実バイナリでメモリ節約効果
  - 対象 17 ファイル (lsx.env / x1.env が参照する .asm を `tools/resident-audit.py --env` で機械的に列挙) / 260 関数
  - 内訳: **shared 258 関数 / local 2 関数** (`SLANGINIT` = main inline 専用, `M8ALOAD` = 命令オペランド書き換えあり)
  - `tools/resident-audit.py` (関数別 self-mod ヒューリスティック判定 + env-aware 走査) と `tools/resident-apply.py` (override map + 一括付与 / dry-run / idempotent) を追加。手書き列挙の漏れを排除
  - 効果実測: `examples/MODTEST.SL` (Local) と `examples/MODTEST_RESIDENT.SL` (新設、`#MODULE $3000 RESIDENT`) を比較 → overlay バイナリが **248B → 57B (-77%)** に縮小 (MPRNT/P10/PCRONE が main 集約され、overlay 内 EXTERN 参照に変換)。複数 overlay を並べる用途では効果がさらに大きい
  - サンプル分割の意図: `MODTEST.SL` は引き続き「overlay 基本例 (Local モード)」、`MODTEST_RESIDENT.SL` は「resident runtime デモ」と役割を分離 (Codex レビュー指摘反映)
  - 既存 IntegrationTest `Overlay_RuntimePolicy_Resident_DefaultRuntimes_StillLocal` を `..._SharedRuntimes_PromotedToMain` に書き換え (PR-A 時点で「runtime 側未対応」として placeholder 化していたものを、本 PR で正の挙動アサートに転換)
  - 既存 examples (MANDEL / FMANDEL / STARS など overlay 未使用) はバイナリ変化なし。`#MODULE` を使わない SL は影響を受けない
- `slangbuild` に prelink 二段アセンブル機構を追加 (PR-B2) — main / overlay 間で **任意の SLANG 関数の相互呼び出し** をサポート
  - cross-reference 検出時に自動的に prelink モードへ (Pass 1: 各 target を dummy imports でアセンブル → Pass 2: 全 target の Exports セクションから ExportedFunctionTable 構築 → Pass 3: combined imports で本番アセンブル)
  - サポート範囲: main → overlay 関数 / overlay → main 関数 / overlay → overlay 関数 (関数シンボルのみ)
  - **仕様**: 解決するのはアドレスだけ。swap 制御・呼び先 overlay のロード状態確認は ユーザー責任 (低レベル言語の責務分担)
  - compiler 側 (`CodeGenerator`) で各 ASM に `; === Exported User Functions ===` (= 自分が定義する関数) + `; === User Function References ===` (= 自分が呼ぶ他ファイル関数) の 2 セクションを出力
  - driver 側に `AsmSectionParser` / `ExportedFunctionTable` / `PrelinkPlan` 新設。`-nsa` は prelink Pass 1/3 のみ付与 (既存単段フローには影響なし)
  - PR-A バグ修正: `Preprocessor` が `#END` を「stray」として skip してしまい、`#MODULE` 内 `#END` が Parser に届いていなかった問題を解消 (これにより 2 つ以上の `#MODULE` が正しく個別 overlay として認識される)
  - PR-A バグ修正: `#MODULE` ネスト禁止 (= `#END` 不足検出も兼ねた parse エラー)
  - 失敗時は `--keep-asm` 指定なしでも中間 ASM を残す (AILZ80ASM のエラー行追跡用、PR-B 既存)
  - 新規テスト: `AsmSectionParserTests` (4) + `ExportedFunctionTableTests` (4) + `PrelinkPlanTests` (5) + `BuildIntegrationTests` 拡張 (4 件 prelink E2E + -o 相対パス回帰 2 件、合計 17 件追加)
- 二段アセンブル toolchain `slangbuild` を新ドライバとして追加 (PR-B)
  - `src/SLANGCompiler.Build/` を新規 .NET 8 プロジェクトとして追加。`slangc` は改修せず、`slangbuild` が orchestration (slangc → AILZ80ASM main → AILZ80ASM overlay) を担う GCC 的責務分離
  - **二段フロー**: main を `-sm minimal-equ` でアセンブルして `.sym` を生成 → 各 overlay について overlay ASM 内の `; EXTERN` リストと main.sym の交集合だけを抽出した `<overlay>.imports.asm` (filtered EQU) を作成 → `imports.asm + overlay.ASM` を AILZ80ASM に投入。raw main.sym をそのまま渡すと compiler 内部ラベル衝突が起きるため必ず filter する
  - `OverlayImportsBuilder` が PR-A で出力された 3 つの固定セクション (`Shared Runtime References` / `Shared Symbols` / `String references`) 内の `; EXTERN` 行のみを regex で抽出 (誤検出防止)
  - `ToolResolver` で slangc / AILZ80ASM のパス解決順を仕様化 (`--slangc` / `--asm` → 同梱 bin → PATH → dev fallback)。配布スクリプト (Makefile.dist / publish.sh) では明示指定で再現性確保
  - `Makefile.dist` を `slangbuild` 経由に切替。overlay 不要 (`#MODULE` 未使用) の SL でも単段フローで動く
  - `publish.sh` に slangbuild の 4 platform publish + zip 同梱を追加
  - 新規テスト: `SymFileReaderTests` (5 件) + `OverlayImportsBuilderTests` (4 件) + `BuildIntegrationTests` (6 件 E2E) = 計 15 件
  - **注意**: 本 PR は **toolchain 機構** のみ。実バイナリでメモリ節約効果が出るには別 PR (PR-C) で `runtime/lsx*.asm` 等への `@resident shared` 付与が必要。本 PR マージ直後は全 overlay が default Local で従来挙動互換
- `#MODULE` (オーバーレイ) をモジュール専用ワーク対応に拡張
  - モジュール直下の `VAR` / `ARRAY` を **モジュール私有ワークエリア `__WORK_M<N>__`** に配置。ASM ラベルは `_V_M<N>_<NAME>` で名前空間分離されるため、main と同名の変数を宣言しても物理メモリ上同居せず、各 overlay の swap 先でメモリを再利用できる
  - `#MODULE` 内に `WORK <定数式>` を書くことでモジュール専用ワークの ORG を明示可能 (未指定時は overlay コード末尾に連続配置)
  - `WORK` / `ORG` / `OFFSET` の各ディレクティブが定数式 (`CONST WA = $9000; WORK WA` など) を受けるように拡張
  - モジュール直下の初期値付き変数 / 固定アドレス指定 / トップレベル `#ASM` はコンパイルエラー化 (関数本体内の `#ASM` / ローカル変数は従来どおり)
  - 関数定義 / `MACHINE` / `CONST` は従来互換で global に登録 — main から overlay 関数を呼ぶ既存運用は変わらず
- `#MODULE` ランタイム集約ポリシーの **内部設計** を導入 (PR-A)
  - ヘッダ位置に optional のポリシー識別子を追加: `#MODULE $8000 RESIDENT` (省略時は Local = 現状互換)
  - ランタイム関数側に `; @resident shared|local` 属性を追加。RESIDENT モード × 関数 Shared の交点で main 集約 (overlay 内 EXTERN 参照) になる
  - `@resident local` (明示) は #MODULE RESIDENT でも常に勝つ (self-modifying / overlay-specific WORK 等の安全側挙動)
  - 新規 `RuntimePlanner` で集約決定 (alias 正規化 / 依存閉包 / SLANGINIT inline 仕様化を担当)
  - CodeGenerator の runtime 参照 7 箇所 (overlay resolve / SLANGINIT exclude / main runtime emit / RUNTIME_INIT / @works 集約) を Plan 経由に統一。shared 関数の `@init_code` / `@works` が main 側 `RUNTIME_INIT` / `__WORK__` に正しく集約される
  - `#MODULE $addr SELFCONTAIN` / `AUTO` は enum 予約済み、現時点はコンパイルエラー
  - **注意**: 本 PR は **内部設計のみ**。実バイナリでの shared runtime リンクは別 PR (PR-B = main → `.sym` → overlay EQU 注入の二段アセンブル toolchain) が必要。既存 runtime ライブラリにもまだ `@resident shared` を付与していないため、`RESIDENT` を書いても現時点では何も共有されず、動作は完全に現状互換

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
