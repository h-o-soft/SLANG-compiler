# ====================================================================================================
# banjo (Furnace tracker) driver sample — AY/PSG or OPM/FM song (+ AY SFX) × 4 env build orchestration
#
#   make x1            → BANJOMP_x1.d88      (LSX-Dodgers、 ndc で asset 同梱)
#   make sosx1         → BANJOMP_sosx1.d88   (S-OS for X1、 HuDisk で asset 同梱)
#   make x1native      → BANJOMP_x1native.tap (OS なし cassette tape、 --tape-add 多段)
#   make x1native_slfs → BANJOMP_slfs.d88    (OS なし SLFS disk、 --slfs-add)
#
#   make all   … 4 env 全部
#   make clean
#
# 曲データ assets/song.fur を用意してください (Furnace tracker)。CHIP=opm|ay|both で driver を選択。
#
# SFX (効果音) は AY (PSG) 専用。 CHIP=ay のときだけ有効化され、 assets/sfx.fur (AY 1ch) を
# SFX blob にビルドして同梱する (CHIP=opm/both は BGM のみ)。 SFX_CH=0..2 で SFX を載せる AY ch
# を指定 (既定 0)。 SFX 再生中はその AY ch の BGM が一時 mute される。
#
# メモリ配置 (chip 別、 BANJOMP.config.inc に SONG_ADDR/SFX_ADDR を生成して SL と一致させる):
#   CHIP=ay      : driver $8000 / song $9000 / sfx $B000 / RAM $C000
#   CHIP=opm/both: driver $8000 / song $B000 / RAM $C000 (SFX 無し)
#
# 構成: driver bin (chip 選択式・固定 ORG=$8000) と曲/SFX bin を別ビルドし、 SLANG プログラムと
# 一緒に各 env のネイティブ手段で実行時ロードする。 曲/SFX は driver の関数アドレスを参照するので、
# driver build 後に driver.sym から EQU 抽出して注入する (banjo_extract_syms.py)。
#
# tool override 可: SLANGBUILD= / WLA= / WLALINK= / PYTHON= / NDC= / HUDISK= / MONO=
# ====================================================================================================

SLANGBUILD ?= slangbuild
WLA        ?= wla-z80
WLALINK    ?= wlalink
PYTHON     ?= python3
NDC        ?= ndc
HUDISK     ?= $(HOME)/.config/SLANG/tools/HuDisk.exe
MONO       ?= mono

INCDIR = -I ../../include -I .
LIBDIR = -L ../../runtime

CHIP       ?= opm
DRV_ORG    = 0x8000
BANJO_RAM  = 0xC000
AY_MASTER_CLOCK ?= 4000000

# SFX は AY 専用。 song/sfx の配置は SFX 有無で変わる。 SONG_ADDR_SL/SFX_ADDR_SL は SLANG hex ($)
# 表記 (config.inc 生成用)、 SONG_ORG/SFX_ORG は tool 用 (0x 表記)。
ifeq ($(CHIP),ay)
  SFX_ENABLED  = 1
  SONG_ORG     = 0x9000
  SFX_ORG      = 0xB000
  SONG_ADDR_SL = $$9000
  SFX_ADDR_SL  = $$B000
  TAPE_SFX     = --tape-add sfx.bin@$(SFX_ORG)
  SLFS_SFX     = --slfs-add "BANJOSFX.BIN:sfx.bin"
  ASSET_BINS   = song.bin sfx.bin
else
  SFX_ENABLED  = 0
  SONG_ORG     = 0xB000
  SONG_ADDR_SL = $$B000
  TAPE_SFX     =
  SLFS_SFX     =
  ASSET_BINS   = song.bin
endif

SFX_CH     ?= 0
SFX_FUR    = assets/sfx.fur

SRC        = BANJOMP.SL
SIZES      = BANJOMP.sizes.inc
CONFIG     = BANJOMP.config.inc

# banjo driver / tools
BANJO_DIR  = ../../runtime/x1/banjo
UPSTREAM   = $(BANJO_DIR)/upstream
TOOLS      = ../../tools

SONG_FUR   = assets/song.fur

ifeq ($(CHIP),opm)
  CHIP_FLAG = 128
  JSON2SMS = $(PYTHON) $(UPSTREAM)/json2sms.py
else ifeq ($(CHIP),ay)
  CHIP_FLAG = 4
  JSON2SMS = $(PYTHON) $(TOOLS)/json2sms_x1.py --ay-master-clock $(AY_MASTER_CLOCK)
else ifeq ($(CHIP),both)
  CHIP_FLAG = 132
  JSON2SMS = $(PYTHON) $(TOOLS)/json2sms_x1.py --ay-master-clock $(AY_MASTER_CLOCK)
else
  $(error CHIP must be opm, ay, or both)
endif

# ----------------------------------------------------------------------------------------------------
# driver: banjo wrapper + Core(SYS=0) + chip を固定 ORG でビルド (chip 選択式)。
#   driver.bin + driver.sym を生成。 build_driver.sh が runtime/x1/banjo/ へ cd して
#   upstream/ 相対 include を解決する。
# ----------------------------------------------------------------------------------------------------
driver.bin: FORCE
	$(BANJO_DIR)/build_driver.sh --chip $(CHIP) --org $(DRV_ORG) --ram $(BANJO_RAM) --outdir .

driver.sym: driver.bin

# ----------------------------------------------------------------------------------------------------
# song: Furnace .fur → json → 曲 asm → (EQU 抽出 + ロードアドレス整形) → bin。
#   driver.sym 依存 (= 曲が参照する driver label の絶対アドレスを EQU 注入するため)。
# ----------------------------------------------------------------------------------------------------
song.json: $(SONG_FUR)
	$(PYTHON) $(UPSTREAM)/furnace2json.py -o song.json $(SONG_FUR)

song.asm: song.json
	$(JSON2SMS) -o song.asm -i song song.json

banjo_syms.inc: driver.sym song.asm
	$(PYTHON) $(TOOLS)/banjo_extract_syms.py driver.sym song.asm -o banjo_syms.inc

song_ready.asm: song.asm banjo_syms.inc
	$(PYTHON) $(TOOLS)/banjo_prep_song.py song.asm \
	    --load-addr $(SONG_ORG) --syms-include banjo_syms.inc -o song_ready.asm

song.bin: song_ready.asm
	$(WLA) -o song.o song_ready.asm
	printf '[objects]\nsong.o\n' > song_link.txt
	$(WLALINK) -s -S -r song_link.txt song.rom
	$(PYTHON) $(TOOLS)/banjo_trim_to_end.py song.rom song.sym song.bin \
	    --load $(SONG_ORG) --ram-base $(BANJO_RAM)

# ----------------------------------------------------------------------------------------------------
# sfx (AY 専用): Furnace .fur → json → SFX asm (-s で SFX ch 固定) → bin。
#   json2sms_x1.py -s は AY SFX で banjo_init_sfx_channel_ay を出す。 OPM 等を誤って SFX 指定した
#   場合 (SFX_CH ミス / sfx.fur の chip 構成ミス) を検出するため、 生成 asm が
#   banjo_init_sfx_channel_ay を参照していることを検査して落とす。
# ----------------------------------------------------------------------------------------------------
sfx.json: $(SFX_FUR)
	$(PYTHON) $(UPSTREAM)/furnace2json.py -o sfx.json $(SFX_FUR)

sfx.asm: sfx.json
	$(JSON2SMS) -s $(SFX_CH) -o sfx.asm -i sfx sfx.json
	@grep -q banjo_init_sfx_channel_ay sfx.asm || \
	  { echo "ERROR: SFX は AY 専用です (sfx.asm に banjo_init_sfx_channel_ay 無し)。 SFX_CH=$(SFX_CH) と assets/sfx.fur の chip 構成を確認してください"; exit 1; }

sfx_syms.inc: driver.sym sfx.asm
	$(PYTHON) $(TOOLS)/banjo_extract_syms.py driver.sym sfx.asm -o sfx_syms.inc

sfx_ready.asm: sfx.asm sfx_syms.inc
	$(PYTHON) $(TOOLS)/banjo_prep_song.py sfx.asm \
	    --load-addr $(SFX_ORG) --syms-include sfx_syms.inc -o sfx_ready.asm

sfx.bin: sfx_ready.asm
	$(WLA) -o sfx.o sfx_ready.asm
	printf '[objects]\nsfx.o\n' > sfx_link.txt
	$(WLALINK) -s -S -r sfx_link.txt sfx.rom
	$(PYTHON) $(TOOLS)/banjo_trim_to_end.py sfx.rom sfx.sym sfx.bin \
	    --load $(SFX_ORG) --ram-base $(BANJO_RAM)

# ----------------------------------------------------------------------------------------------------
# sizes.inc: FREAD 用の file size 定数 (= driver/song(/sfx) の実 byte 数)。
# 同時に raw 配置の重なりを early fail でチェック (= runtime load 版は linker が守らない)。
# ----------------------------------------------------------------------------------------------------
$(SIZES): driver.bin $(ASSET_BINS)
ifeq ($(SFX_ENABLED),1)
	@drv=`wc -c < driver.bin | tr -d ' '`; \
	song=`wc -c < song.bin | tr -d ' '`; \
	sfx=`wc -c < sfx.bin | tr -d ' '`; \
	if [ `expr 32768 + $$drv` -gt 36864 ]; then \
	  echo "ERROR: driver ($$drv B) overflows \$$8000..\$$9000"; exit 1; fi; \
	if [ `expr 36864 + $$song` -gt 45056 ]; then \
	  echo "ERROR: song ($$song B) overflows \$$9000..\$$B000"; exit 1; fi; \
	if [ `expr 45056 + $$sfx` -gt 49152 ]; then \
	  echo "ERROR: sfx ($$sfx B) overflows \$$B000..\$$C000"; exit 1; fi; \
	printf 'CONST DRV_SIZE = %s, SONG_SIZE = %s, SFX_SIZE = %s;\n' $$drv $$song $$sfx > $(SIZES); \
	echo "generated $(SIZES): DRV=$$drv SONG=$$song SFX=$$sfx"
else
	@drv=`wc -c < driver.bin | tr -d ' '`; \
	song=`wc -c < song.bin | tr -d ' '`; \
	if [ `expr 32768 + $$drv` -gt 45056 ]; then \
	  echo "ERROR: driver ($$drv B) overflows \$$8000..\$$B000"; exit 1; fi; \
	if [ `expr 45056 + $$song` -gt 65536 ]; then \
	  echo "ERROR: song ($$song B) overflows \$$B000..\$$10000"; exit 1; fi; \
	printf 'CONST DRV_SIZE = %s, SONG_SIZE = %s;\n' $$drv $$song > $(SIZES); \
	echo "generated $(SIZES): DRV=$$drv SONG=$$song"
endif

# config.inc: chip flag + 配置アドレス + SFX 有効フラグ。 BANJO_SFX_ENABLED は #IF 用なので
# 必ずプレーン CONST (CONST ASM は preprocessor が #IF 用に抽出しない)。
$(CONFIG): FORCE
	printf 'CONST ASM BANJO_CHIP_FLAG = %s;\n' $(CHIP_FLAG) > $(CONFIG)
	printf 'CONST SONG_ADDR = %s;\n' '$(SONG_ADDR_SL)' >> $(CONFIG)
	printf 'CONST BANJO_SFX_ENABLED = %s;\n' $(SFX_ENABLED) >> $(CONFIG)
ifeq ($(SFX_ENABLED),1)
	printf 'CONST SFX_ADDR = %s;\n' '$(SFX_ADDR_SL)' >> $(CONFIG)
endif
	@echo "generated $(CONFIG): CHIP=$(CHIP) FLAG=$(CHIP_FLAG) SFX_ENABLED=$(SFX_ENABLED)"

# ----------------------------------------------------------------------------------------------------
# x1 (LSX-Dodgers): 本体 d88 を slangbuild で生成 → ndc で driver/song(/sfx) を投入。
# 投入名を SL の FOPEN 名 (BANJODRV.BIN / BANJOSNG.BIN / BANJOSFX.BIN) に揃える。
# ----------------------------------------------------------------------------------------------------
x1: driver.bin $(ASSET_BINS) $(SIZES) $(CONFIG)
	$(SLANGBUILD) -E x1 $(LIBDIR) $(INCDIR) $(SRC) -o BANJOMP_x1 --emit disk
	cp driver.bin BANJODRV.BIN
	cp song.bin BANJOSNG.BIN
	-$(NDC) D BANJOMP_x1.d88 0 BANJODRV.BIN
	-$(NDC) D BANJOMP_x1.d88 0 BANJOSNG.BIN
	$(NDC) P BANJOMP_x1.d88 0 BANJODRV.BIN
	$(NDC) P BANJOMP_x1.d88 0 BANJOSNG.BIN
ifeq ($(SFX_ENABLED),1)
	cp sfx.bin BANJOSFX.BIN
	-$(NDC) D BANJOMP_x1.d88 0 BANJOSFX.BIN
	$(NDC) P BANJOMP_x1.d88 0 BANJOSFX.BIN
endif
	rm -f BANJODRV.BIN BANJOSNG.BIN BANJOSFX.BIN

# sosx1 (S-OS for X1): 本体 d88 → HuDisk で asset 投入 (mono 経由)。
sosx1: driver.bin $(ASSET_BINS) $(SIZES) $(CONFIG)
	$(SLANGBUILD) -E sosx1 $(LIBDIR) $(INCDIR) $(SRC) -o BANJOMP_sosx1 --emit disk
	cp driver.bin BANJODRV.BIN
	cp song.bin BANJOSNG.BIN
	-$(MONO) $(HUDISK) -d BANJOMP_sosx1.d88 BANJODRV.BIN
	-$(MONO) $(HUDISK) -d BANJOMP_sosx1.d88 BANJOSNG.BIN
	$(MONO) $(HUDISK) -a BANJOMP_sosx1.d88 BANJODRV.BIN
	$(MONO) $(HUDISK) -a BANJOMP_sosx1.d88 BANJOSNG.BIN
ifeq ($(SFX_ENABLED),1)
	cp sfx.bin BANJOSFX.BIN
	-$(MONO) $(HUDISK) -d BANJOMP_sosx1.d88 BANJOSFX.BIN
	$(MONO) $(HUDISK) -a BANJOMP_sosx1.d88 BANJOSFX.BIN
endif
	rm -f BANJODRV.BIN BANJOSNG.BIN BANJOSFX.BIN

# x1native (OS なし cassette tape): main + driver/song(/sfx) を多段 tape に連結。
# --tape-add の順 = tape 物理順 = SL の MTREAD 呼び出し順 (driver → song → sfx)。
x1native: driver.bin $(ASSET_BINS) $(SIZES) $(CONFIG)
	$(SLANGBUILD) -E x1native $(LIBDIR) $(INCDIR) $(SRC) -o BANJOMP_x1native --emit tape \
	  --tape-add driver.bin@$(DRV_ORG) \
	  --tape-add song.bin@$(SONG_ORG) \
	  $(TAPE_SFX)

# x1native_slfs (OS なし SLFS disk): --slfs-add で asset を SLFS に収納。
# asset name (BANJODRV.BIN 等) から generated header の FILE_* が作られる (BANJOMP.assets.inc)。
x1native_slfs: driver.bin $(ASSET_BINS) $(SIZES) $(CONFIG)
	$(SLANGBUILD) -E x1native_slfs $(LIBDIR) $(INCDIR) $(SRC) -o BANJOMP_slfs --emit disk \
	  --slfs-add "BANJODRV.BIN:driver.bin" \
	  --slfs-add "BANJOSNG.BIN:song.bin" \
	  $(SLFS_SFX)

all: x1 sosx1 x1native x1native_slfs

clean:
	rm -f BANJOMP_x1.* BANJOMP_sosx1.* BANJOMP_x1native.* BANJOMP_slfs.* \
	      BANJOMP.assets.inc $(SIZES) $(CONFIG) BANJODRV.BIN BANJOSNG.BIN BANJOSFX.BIN \
	      driver.bin driver.sym driver.o driver.rom driver_link.txt \
	      song.json song.asm song_ready.asm banjo_syms.inc \
	      song.o song.rom song.sym song.bin song_link.txt \
	      sfx.json sfx.asm sfx_ready.asm sfx_syms.inc \
	      sfx.o sfx.rom sfx.sym sfx.bin sfx_link.txt \
	      *.ASM *.LST

FORCE:

.PHONY: x1 sosx1 x1native x1native_slfs all clean FORCE
