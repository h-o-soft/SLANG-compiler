# 更新履歴

## Unreleased (v0.24.0 候補)

- 配布 zip に `install.sh` / `install.bat` / `uninstall.sh` / `uninstall.bat` を同梱、Makefile に依存せず install / uninstall できるよう導線を切り出し (#160 短期案)
  - オプション: `--prefix <path>` / `--config-dir <path>` / `--dry-run` / `--verbose` / `--force` / `--uninstall` / `--help`
  - **危険 path guard**: uninstall 時に空 / `/` / `$HOME` / `/tmp` 単体 / `C:\` / `%USERPROFILE%` 等を refuse (絶対パス正規化してから完全一致判定、`/tmp/sub` 等は許可)
  - **ghost file 対策**: install 時、サブディレクトリ (include / runtime / images / tools) は staging copy → 既存削除 → rename でサブディレクトリ単位置換 (= 古い env file 等が残らない)
  - `make install` / `make uninstall` は scripts への薄い wrapper として残置 (`--force` 既定 ON で後方互換、Make 経由は uninstall も非対話)
  - **Windows install default を `%LOCALAPPDATA%\Programs\SLANG` → `%USERPROFILE%\.local\bin` に変更** (= install.sh の `~/.local/bin` と対称、uv / pipx 等の CLI ツール慣習)
  - 中期案 (`setupenv.{sh,bat}` の .NET 化 = `slangsetup` 新設) は別 PR で扱う

- pc88mk2sr 環境を `slangbuild --emit disk` 経路に統合
  - 新 tool: `udostool` (.NET assembly、**Bookworm's Library 公開の汎用ディスクルーチン** `filesys_20141128` 系用、サイト記載の「改変含め自由に使ってください」を license として信用し repo 同梱)。`tools/udostool.exe` に commit、setup-tools 不要。Linux/macOS は mono 経由起動、Windows は .exe 直接起動
  - 5 ステップ build: template (`images/templates/PC88MK2SR.D88`) を出力先 copy → `udostool` で IPL / SUB / SYS 書込 → main `$1A00.$$$` + overlay `M{index}.BIN` を staging dir に bulk copy → `udostool disk <staging>` で 1 回 flush
  - 新 `DiskConfig.SystemFiles` フィールド (= IPL / SUB / SYS の path + flag、env file dir 基準で path 絶対化、flag は `-IPL`/`-SUB`/`-SYS` allowlist で大文字固定)
  - **overlay 対応**: `#MODULE` 使った SL の overlay bin が `M{index}.BIN` として disk に格納される。pc88mk2sr では FOPEN/FREAD ではなく libp88_file の `Disk_Load` / `Disk_Load3` 系で読み込む想定、disk 内名は `Disk_Load("M0 BIN", addr)` のような space 区切り形式
  - **ORG = $1A00 固定運用** (= 本 PR の制約)。SL 側で `#ORG $XXXX` 上書きすると `main_name` (= `"$1A00.$$$"`) と loader 期待が不整合になり、build は通るが boot しない silent wrong になる **既知リスク**。ORG 可変化 (= `main_name` template 化 + slangc 出力 sym からの整合 check) は別 PR で対応
  - 同梱物の出典 (provenance) を新規 `THIRD_PARTY_NOTICES.md` に記録 (= 配布物名 / 取得元 / 取得日 / 許諾文 / 改変有無 を表で明記)
  - **VRTC 割り込みハンドラの GAMEVSYNC を条件化**: `libp88_base.asm` の `INTVRTC` で `CALL GAMEVSYNC` を `#if exists USE_GAMEVSYNC` で囲み、SL 側で `CONST ASM USE_GAMEVSYNC = 1;` を立てた場合のみ呼ぶ形に。`USE_GAMEVSYNC` 未定義の SL は GAMEVSYNC 関数を書かなくても build できる
  - **AILZ80ASM エラー表示**: `--verbose` 無しでも `main assembly failed` の詳細 (= 未定義 label 等) が stderr に出るよう修正 (= AILZ80ASM がエラーを stdout に流す癖を吸収)

- pc80mk2xsd 環境 (PC-8001mkII XBIOS 直接環境、SD カード経路) を `slangbuild` に対応
  - 新 env file フィールド `cmt_assets:` (= output dir に copy する static asset の相対 path リスト)、`overlay_name:` (= overlay 出力 file 名 template、`{index}` 展開)、`overlay_output_format:` (= overlay 専用の出力 format、bin / cmt 切替)
  - pc80mk2xsd では main を CMT 形式 + overlay を raw binary で出し、`M{index}.BIN` 命名で output dir に rename。`cmt_assets` で `XBIOS.CMT` を output dir にコピーするので、user は output dir 全体を SD カードに移すだけで揃う
  - `cmt_concat` と `cmt_assets` は同 env で両方指定すると env load 時 reject (= build flow 排他)
  - 全 CMT 系新フィールドは `output: cmt` 専用 (= 不一致は reject)
  - `overlay_name` は `{index}` placeholder 必須、output dir 外書き禁止 (= absolute path / separator / `..` を loader と driver の二重で validate)

- env file `defines:` フィールドを追加 — env 別の整数定数を slangc Preprocessor と AILZ80ASM に同時 inject
  - env file `defines: { NAME: int_value }` 形式で、env 選択時に slangc が `Preprocessor.DefineConst()` 経由で SL の `#IF NAME==VAL` 判定に登録、slangbuild が AILZ80ASM 起動時に `-dl NAME=VAL` を main / overlay / prelink Pass 1/3 全段に pass (= ASM 側 `#IF exists NAME` も活きる)
  - pc80mk2xsd で `defines: { PC8001_SD: 1 }` を定義しているので、ユーザー側 SL に `CONST ASM PC8001_SD = 1;` を書く必要なし (env 切替だけで SD 経路が自動活性化)
  - 名前は `^[A-Za-z_][A-Za-z0-9_]*$` で validate、value は int 限定

- pc80mk2x 環境 (PC-8001mkII XBIOS 直接環境) を `slangbuild` に対応 — XBIOS.CMT 結合 build
  - `obsolete/lib/pc8001/XBIOS/XBIOS.CMT` を `runtime/templates/XBIOS.CMT` に移動。slangbuild が pc80mk2x build 時に main.cmt + XBIOS.CMT + overlay._mN.cmt を 1 本に結合 (= 旧 `COPY /B` / `cat` 手動結合を内製化)
  - 新 `EnvironmentConfig.CmtConcat` (List<string>?) + env file `cmt_concat:` フィールド (= env file dir 基準の相対 path リスト、build 後 main bin 直後に concat される)
  - 結合は同一 dir tmp file 経由 + `File.Move(.., overwrite: true)` で delete-then-move の中間状態を避けつつ main.cmt を置換、結合元欠落時は明示エラー、結合に消費された overlay は intermediate cleanup 対象 (= `--keep-asm` 指定時は残る)
  - `cmt_concat` は `output: cmt` 専用、それ以外で指定すると env load 時 `InvalidDataException` で reject (= 壊れた出力 silent wrong 防止)
  - `THIRD_PARTY_NOTICES.md` に XBIOS.CMT の出典を記載

- pc80mk2 環境 (PC-8001mkII ROM 環境) を `slangbuild` に統合 — CMT (cassette tape) 出力対応
  - env file (`runtime/env/pc80mk2.env`) に新フィールド `output: cmt` を追加。`slangbuild` が AILZ80ASM 起動時に `-bin` shortcut を `-cmt` に置換 + `-gap 0` を自動付与し、出力拡張子を `.bin` → `.cmt` に切替 (= 旧 dev `Makefile` の `BIN_EXT/ASM_OPT` 設定を `slangbuild` に移植、`Makefile.dist` 化で抜けていた CMT 対応を復元)
  - 新 `EnvironmentConfig.OutputFormat` (string?)。null/未指定 = `.bin` default、`"cmt"` で AILZ80ASM CMT format。`output:` 値は `bin` / `cmt` のみ許可、それ以外は `InvalidDataException` で reject (= typo 早期検出)
  - `AssemblerRunner.AssembleMain` / `AssembleOverlay` に `outputFlag` (`-bin` / `-cmt`) + `extraArgs: string[]?` 引数追加。`-bin` と `-cmt` を同時に渡すと両 format の file が出てしまうので、format ごとに 1 つに切替える設計。prelink Pass 1 / Pass 3 / 単段 / overlay 全段で同じ outputFlag/extraArgs を pass (= AILZ80ASM option 不一致でアドレス解決崩壊するのを防止)
  - `Driver.Run()` 冒頭で `EnvironmentResolver` を 1 度だけ呼ぶ前倒しに変更、`BuildDiskImage()` 内の env 二重解決を排除。`--emit disk` + `disk:` 不在 env の組合せは compile 前に early reject
  - `Makefile.dist`: `OUTPROG` を `$(BIN_EXT)` 経由で拡張子化 (`PROG.bin` 固定 → `PROG$(BIN_EXT)`)、pc80mk2 ENV ブロック追加 (`BIN_EXT/BIN_EXT_ENV = .cmt`、`DISK_IMAGE = $(OUTPROG)`)、`disk_image` 分岐に `pc80mk2: $(OUTPROG)` 明示追加 (= 旧 ELSE 分岐 ndc 経路に落ちないよう、cpm / msxrom と同じ pattern)、`clean` recipe / `help` ENV 一覧にも反映
  - `make ENV=pc80mk2 build / run / disk_image` のいずれでも `examples/PROG.cmt` が生成される。M88 等のエミュレータで `.cmt` をそのまま CLOAD で読み込める想定 (`EMU` 変数はユーザー側設定)
  - **scope 外**: pc80mk2x (XBIOS 直接環境、XBIOS.CMT 結合が別途必要) / overlay 結合 (= `M0.cmt` を main に結合) / `--emit disk` 統合 (= cassette は disk じゃないので非対象) は本 PR 外。overlay も `_m0.cmt` 別ファイルで出るのみ、loader 組み立てはユーザー側

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
  - **従来経路維持 env**: msx2 / msxlsx / pc80mk2 / pc88mk2sr 等は従来の `tools/disk-add-overlays.py` 経路 (= 今後 env ごとに `--emit disk` へ移行予定)
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
