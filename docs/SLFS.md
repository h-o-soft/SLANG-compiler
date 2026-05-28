# SLFS (SLANG File System)

ゲーム専用 minimal file I/O system。 x1native env で D88 disk image から asset を読込む。

- target: **x1native のみ** (= sosx1 / pc88 等への展開は scope 外)
- spec: Issue #212

## 設計思想

- **read-only main + small save area + 256 file 上限** で割り切る (= libmag / libm8a 互換は重い)
- HuBASIC IPL 互換 boot sector のみで起動性確保、 中身は独自 FS
- 「program loader」 化しない (= load/exec addr 等 program 属性は dir entry に入れない、 asset FS に徹する)

## 全体 layout (= 2D 320 KB 固定)

```
予約領域 (= logical sector 0 〜 data_area_start_sector - 1):
  sector 0: ブートセクタ (= HuBASIC IPL header 32 byte + 残り 0 fill、 X1 IPL ROM が読込)
  sector 1: SLFS スーパーブロック ("SLFS" magic + fields)
  sector 2〜: ディレクトリ (= 16 byte entry × 16 / sector)
  sector M〜: main program 本体 (= packer が data_area_start_sector の手前に配置)

データ領域 (= data_area_start_sector 以降): asset 連続配置 (= sorted by filename、 READ ONLY)

セーブ領域 (= save_area_start_sector 以降): free area (= Phase 2 で FS_SAVE_R / FS_SAVE_W 予定)
```

## boot sector (= sector 0、 HuBASIC IPL header)

X1 IPL ROM (= X1_compatible_rom 等) が起動時に sector 0 を `$FE00` に load → header parse → main program 本体を `LoadAddress` に連続 read → IPL ROM OFF → `ExecuteAddress` に jp。 **boot loader Z80 code は不要**。

| offset | size | name | 説明 |
|---|---|---|---|
| +00 | 1 | BootFlag | $01 = 起動可 |
| +01 | 13 | FileName | ASCII space-padded |
| +0E | 3 | Extension | "Sys" 推奨 |
| +11 | 1 | Password | $20 = none |
| +12 | 2 | DataSize | byte (LE)、 main program 全 size |
| +14 | 2 | LoadAddress | LE、 default $1000 |
| +16 | 2 | ExecuteAddress | LE、 default $1000 |
| +18 | 5 | Date | 0 fill OK |
| +1D | 3 | DiskOffset | byte (LE)、 main program の disk image 内 byte 開始位置 |

## superblock (= sector 1、 256 byte)

| offset | size | name | 説明 |
|---|---|---|---|
| +00 | 4 | Magic | "SLFS" |
| +04 | 1 | Version | 1 |
| +05 | 1 | Sides | 2 (= 2D 固定) |
| +06 | 1 | Tracks | 40 |
| +07 | 1 | SectorsPerTrack | 16 |
| +08 | 2 | DirStartSector | logical sector index (LE) |
| +0A | 2 | DirEntryCount | LE |
| +0C | 2 | DataAreaStartSector | LE |
| +0E | 2 | SaveAreaStartSector | LE |
| +10 | 2 | SaveSectorCount | LE |
| +12 | 16 | VolumeName | ASCII space-padded |
| +22 | 0xDE | Reserved | 0 fill |

## directory entry (= 16 byte、 16 entry / sector)

| offset | size | name | 説明 |
|---|---|---|---|
| +00 | 11 | FileName | ASCII space-padded (= 8.3 風 or 自由、 11 char 超 切詰め) |
| +0B | 1 | Type | 0 = raw、 1+ = reserved |
| +0C | 2 | StartSector | logical sector index (LE) |
| +0E | 2 | ByteSize | LE、 範囲 **1..65535** (= 0 / 64 KB 超は packer reject) |

- ファイル名 sort 前提 (= 11 byte normalized ordinal 昇順、 packer が保証)
- ID = sorted directory entry index、 範囲 **0..255** (= Phase 1、 SLANG `FS_READ_BY_ID(id, buf)` の id 8-bit)

## SLANG API (= Phase 1)

```sl
LET SIZE = FS_READ_BY_ID(id, buffer);   // id: 0..255、 buffer: addr
IF SIZE == 0 THEN ... END                // 失敗判定 (HL = 0)
```

- 入力: HL = id (L 使用、 H 無視)、 DE = buffer
- 戻り値: **HL = 実 byte_size (1..65535)、 失敗時 HL = 0**
- buffer 容量: **sector round-up 分必要** (= `ceil(byte_size / 256) * 256 byte`)
- ID = packer の normalized filename sort 順依存 (= Phase 1 は数値直書き、 generated header は Phase 2 以降)

### sample (= 数値 ID 直書き)

```sl
VAR SIZE;
MAIN()
BEGIN
    SIZE = FS_READ_BY_ID(0, $4000);
    IF SIZE == 0 THEN PRINT("FAIL",/);
    IF SIZE != 0 THEN PRINT("OK SIZE=", SIZE, /);
END;
```

## slangbuild 使い方

### env file 設定

`runtime/env/x1native_slfs.env` (= x1native.env fork + disk: section):

```yaml
defines:
  X1NATIVE: 1
output: bin
disk:
  format: d88
  tool: slfs-pack
  main_name: "SLFSMAIN"
  main_load: "$1000"
  main_exec: "$1000"
  volume: "GAMEDISK"
libraries:
  - ... (= x1native.env と同期、 末尾に libx1native_slfs.yml 追加)
```

**注意**: `x1native_slfs.env` は `x1native.env` の disk variant、 **env include 機構未実装のため libraries 同期が必要**。 x1native.env を変更したら slfs.env も合わせて更新する。 将来的 env include は別 issue。

### build

```sh
slangbuild --emit disk -E x1native_slfs SLFSDEMO.SL -o SLFSDEMO \
  --slfs-add GREETING:examples/X1NATIVE_SLFS/GREETING.TXT \
  --slfs-add NUMBERS:examples/X1NATIVE_SLFS/NUMBERS.BIN
# または dir 指定で一括 (= non-recursive walk、 各 file の name = basename を 11 char に切詰め)
slangbuild --emit disk -E x1native_slfs SLFSDEMO.SL -o SLFSDEMO \
  --slfs-add examples/X1NATIVE_SLFS/assets/
```

### slfs-pack (standalone CLI)

```sh
slfs-pack -o output.d88 --main main.bin \
  --add NAME:path/to/file \
  --add path/to/dir/ \
  --main-load 0x1000 --main-exec 0x1000 \
  --volume "MYGAME"
```

## 実装

- runtime/libx1native_slfs.asm (= FDC + FS_READ_BY_ID + X1FDC_WORK)
- runtime/env/x1native_slfs.env
- src/SLANGCompiler.SlfsPack/ (= packer library + standalone CLI、 C# net8.0)
- src/SLANGCompiler.Build/DiskImageBuilder.cs (= BuildSlfsPack 分岐、 library 直接呼出)
- examples/X1NATIVE_SLFS/ (= SLFSDEMO.SL + assets/)

## 制約 (Phase 1)

- 2D 固定 (= 2 sides × 40 tracks × 16 sectors × 256 bytes = 320 KB)、 2HD は Phase 2 以降
- asset 最大 **256 file** (= ID 8-bit)
- 1 file 最大 **65535 byte** (= byte_size 16-bit、 0 / 上限超は packer reject)
- save area 未実装 (= superblock fields は確保済、 Phase 2 で `FS_SAVE_R / FS_SAVE_W`)
- name lookup 未実装 (= Phase 1 は数値 ID 直書き、 Phase 3 で `FS_OPEN`)
- buffer = sector round-up 分必要 (= byte_size mod 256 切詰めなし、 呼出側責任)

## Phase 2 以降 (= scope 外)

- save area relative `FS_SAVE_R` / `FS_SAVE_W`
- `FS_OPEN` name lookup + `FS_READ` low-level API
- 圧縮 type 内部処理 / 物理 CHS API / 2HD geometry
- env include 機構 (= x1native_slfs.env と x1native.env の drift 解消)
- 数値 ID → generated `#define` header (= compile 前 packer 走らせて #include)

## reference

- Issue #212: spec 確定
- X1 互換 IPL ROM: https://github.com/meister68k/X1_compatible_rom (CC0)、 FDC routine / boot sector spec の fork 元
- MB8877A datasheet: FDC cmd / status bit layout
