# ====================================================================================================
# banjo 複数楽曲・複数SFX bundle サンプル (X1, AY/PSG または OPM)
#
#   make x1            → BANJOMULTI_x1.d88      (LSX-Dodgers、 ndc)
#   make sosx1         → BANJOMULTI_sosx1.d88   (S-OS for X1、 HuDisk)
#   make x1native      → BANJOMULTI_x1native.tap (OS なし cassette)
#   make x1native_slfs → BANJOMULTI_slfs.d88    (OS なし SLFS disk)
#   make all / make clean
#
# 単一曲の examples/X1_BANJO と違い、 manifest (assets/banjo_assets.txt) に複数の曲/SFX を
# 列挙すると、 banjo_pack_assets.py が各 blob を順次アドレスでビルド→1 つの bundle (banjodat.bin)
# に連結し、 アドレス定数 (BANJOMULTI.assets.addr.inc に MUSIC_* / SFX_* / BANJODAT_BASE 等) を
# 生成する。 SLANG はその定数で BANJO_PLAY / BANJO_SFX_PLAY するだけ (人手のアドレス計算不要)。
#
# 曲/SFX の .fur は配布に含めない。 assets/ に用意すること (README 参照)。
# CHIP=ay (曲 + SFX) / CHIP=opm (曲のみ)。 both は非対応 (1 bundle = 単一チップ)。
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

CHIP       ?= ay
DRV_ORG    = 0x8000
BANJO_RAM  = 0xC000
AY_MASTER_CLOCK ?= 4000000

BANJO_DIR  = ../../runtime/x1/banjo
UPSTREAM   = $(BANJO_DIR)/upstream
TOOLS      = ../../tools

MANIFEST = assets/banjo_assets.txt
SRC      = BANJOMULTI.SL
# ビルド成果物名は disk 投入名 (BANJODAT.BIN) と case-insensitive FS で衝突しないよう別名にする
# (cp bundle.bin BANJODAT.BIN が self-copy にならないように)。
BUNDLE   = bundle.bin
ADDRINC  = BANJOMULTI.assets.addr.inc
CONFIG   = BANJOMULTI.config.inc

ifeq ($(CHIP),ay)
  CHIP_FLAG = 4
else ifeq ($(CHIP),opm)
  CHIP_FLAG = 128
else
  $(error CHIP must be ay or opm (both is not supported for the multi sample))
endif

# driver bin (chip 選択式・固定 ORG)。
driver.bin: FORCE
	$(BANJO_DIR)/build_driver.sh --chip $(CHIP) --org $(DRV_ORG) --ram $(BANJO_RAM) --outdir .
driver.sym: driver.bin

# pack: driver 末尾 (= DRV_ORG + driver.bin サイズ) を base に、 manifest の全アセットを順次
# アドレスでビルド → bundle + addr.inc 生成。 manifest 内の個々の .fur 変更を Make が追えないので FORCE。
$(BUNDLE) $(ADDRINC): driver.bin driver.sym $(MANIFEST) FORCE
	@base=`expr $$(printf '%d' $(DRV_ORG)) + $$(wc -c < driver.bin | tr -d ' ')`; \
	 $(PYTHON) $(TOOLS)/banjo_pack_assets.py \
	   --manifest $(MANIFEST) --driver-sym driver.sym --driver-bin driver.bin \
	   --chip $(CHIP) --base $$base --ram-base $(BANJO_RAM) \
	   --ay-master-clock $(AY_MASTER_CLOCK) \
	   --tools-dir $(TOOLS) --upstream-dir $(UPSTREAM) --workdir . \
	   --out-bin $(BUNDLE) --out-inc $(ADDRINC)

$(CONFIG): FORCE
	printf 'CONST ASM BANJO_CHIP_FLAG = %s;\n' $(CHIP_FLAG) > $(CONFIG)
	@echo "generated $(CONFIG): CHIP=$(CHIP) FLAG=$(CHIP_FLAG)"

# x1 (LSX-Dodgers): driver + bundle の 2 ファイルを ndc で投入。
x1: driver.bin $(BUNDLE) $(ADDRINC) $(CONFIG)
	$(SLANGBUILD) -E x1 $(LIBDIR) $(INCDIR) $(SRC) -o BANJOMULTI_x1 --emit disk
	cp driver.bin BANJODRV.BIN
	cp $(BUNDLE) BANJODAT.BIN
	-$(NDC) D BANJOMULTI_x1.d88 0 BANJODRV.BIN
	-$(NDC) D BANJOMULTI_x1.d88 0 BANJODAT.BIN
	$(NDC) P BANJOMULTI_x1.d88 0 BANJODRV.BIN
	$(NDC) P BANJOMULTI_x1.d88 0 BANJODAT.BIN
	rm -f BANJODRV.BIN BANJODAT.BIN

sosx1: driver.bin $(BUNDLE) $(ADDRINC) $(CONFIG)
	$(SLANGBUILD) -E sosx1 $(LIBDIR) $(INCDIR) $(SRC) -o BANJOMULTI_sosx1 --emit disk
	cp driver.bin BANJODRV.BIN
	cp $(BUNDLE) BANJODAT.BIN
	-$(MONO) $(HUDISK) -d BANJOMULTI_sosx1.d88 BANJODRV.BIN
	-$(MONO) $(HUDISK) -d BANJOMULTI_sosx1.d88 BANJODAT.BIN
	$(MONO) $(HUDISK) -a BANJOMULTI_sosx1.d88 BANJODRV.BIN
	$(MONO) $(HUDISK) -a BANJOMULTI_sosx1.d88 BANJODAT.BIN
	rm -f BANJODRV.BIN BANJODAT.BIN

# x1native (cassette): driver → bundle の 2 段。 bundle の load addr は driver 末尾 (= base、 pack と同式)。
x1native: driver.bin $(BUNDLE) $(ADDRINC) $(CONFIG)
	@bbase=`expr $$(printf '%d' $(DRV_ORG)) + $$(wc -c < driver.bin | tr -d ' ')`; \
	 $(SLANGBUILD) -E x1native $(LIBDIR) $(INCDIR) $(SRC) -o BANJOMULTI_x1native --emit tape \
	   --tape-add driver.bin@$(DRV_ORG) \
	   --tape-add $(BUNDLE)@$$bbase

x1native_slfs: driver.bin $(BUNDLE) $(ADDRINC) $(CONFIG)
	$(SLANGBUILD) -E x1native_slfs $(LIBDIR) $(INCDIR) $(SRC) -o BANJOMULTI_slfs --emit disk \
	  --slfs-add "BANJODRV.BIN:driver.bin" \
	  --slfs-add "BANJODAT.BIN:$(BUNDLE)"

all: x1 sosx1 x1native x1native_slfs

clean:
	rm -f BANJOMULTI_x1.* BANJOMULTI_sosx1.* BANJOMULTI_x1native.* BANJOMULTI_slfs.* \
	      BANJOMULTI.assets.inc $(ADDRINC) $(CONFIG) BANJODRV.BIN BANJODAT.BIN $(BUNDLE) \
	      driver.bin driver.sym driver.o driver.rom driver_link.txt \
	      asset*.json asset*.asm asset*.ready.asm asset*.syms.inc \
	      asset*.o asset*.rom asset*.sym asset*.bin asset*.link.txt \
	      *.ASM *.LST

FORCE:

.PHONY: x1 sosx1 x1native x1native_slfs all clean FORCE
