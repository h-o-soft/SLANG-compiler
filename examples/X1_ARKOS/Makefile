# ====================================================================================================
# Arkos Tracker AKG driver sample — 4 env build orchestration
#
#   make x1            → ARKOSMP_x1.d88      (LSX-Dodgers、 ndc で asset 同梱)
#   make sosx1         → ARKOSMP_sosx1.d88   (S-OS for X1、 HuDisk で asset 同梱)
#   make x1native      → ARKOSMP_x1native.tap (OS なし cassette tape、 --tape-add 多段)
#   make x1native_slfs → ARKOSMP_slfs.d88    (OS なし SLFS disk、 --slfs-add)
#   make all / make clean
#
# driver (PSGAKG_8000.bin) は RASM で ORG $8000 にビルド (= jp table が absolute addr
# 参照のため固定 ORG)。 BGM.AKG / SE.AKX は Arkos export 時のアドレス前提
# (BGM=$B000 / SFX=$C000)。 配置は ARKOSMP.SL の CONST と一致させること。
#
# tool override 可: SLANGBUILD= / RASM= / NDC= / HUDISK= / MONO=
# ====================================================================================================

SLANGBUILD ?= slangbuild
RASM       ?= rasm
NDC        ?= ndc
# HuDisk.exe は .NET assembly (= mono 経由で起動)。 default は install 済の
# ~/.config/SLANG/tools/。 repo から直接使うなら HUDISK=../../tools/HuDisk.exe で override。
HUDISK     ?= $(HOME)/.config/SLANG/tools/HuDisk.exe
MONO       ?= mono

INCDIR  = -I ../../include -I .
SRC     = ARKOSMP.SL
DRIVER  = PSGAKG_8000.bin
SIZES   = ARKOSMP.sizes.inc

# 配置アドレス (= ARKOSMP.SL の CONST と一致)
DRV_ORG = 0x8000

# ----------------------------------------------------------------------------------------------------
# driver: Arkos AKG wrapper を ORG $8000 で RASM ビルド
# ----------------------------------------------------------------------------------------------------
$(DRIVER): ../../runtime/x1/playerAkg_x1_wrapper.asm \
           ../../runtime/x1/PlayerAkg_x1.asm \
           ../../runtime/x1/PlayerAkg_SoundEffects.asm
	$(RASM) ../../runtime/x1/playerAkg_x1_wrapper.asm -DDRIVER_ORG=$(DRV_ORG) -o PSGAKG_8000 -s

# ----------------------------------------------------------------------------------------------------
# sizes.inc: FREAD 用の file size 定数 (= driver/BGM/SFX の実 byte 数)。
# 同時に raw 配置の重なりを early fail でチェック (= runtime load 版は linker が守らない)。
#   DRV $8000 + DRV_SIZE <= BGM $B000、 BGM + BGM_SIZE <= SFX $C000、 SFX + SFX_SIZE <= $10000
# size 取得は `wc -c` (= macOS/BSD `stat -f%z` と GNU `stat -c%s` のオプション差を回避)。
# ----------------------------------------------------------------------------------------------------
$(SIZES): $(DRIVER) BGM.AKG SE.AKX
	@drv=`wc -c < $(DRIVER) | tr -d ' '`; \
	bgm=`wc -c < BGM.AKG | tr -d ' '`; \
	sfx=`wc -c < SE.AKX | tr -d ' '`; \
	if [ `expr 32768 + $$drv` -gt 45056 ]; then \
	  echo "ERROR: driver ($$drv B) overflows \$$8000..\$$B000"; exit 1; fi; \
	if [ `expr 45056 + $$bgm` -gt 49152 ]; then \
	  echo "ERROR: BGM ($$bgm B) overflows \$$B000..\$$C000"; exit 1; fi; \
	if [ `expr 49152 + $$sfx` -gt 65536 ]; then \
	  echo "ERROR: SFX ($$sfx B) overflows \$$C000..\$$10000"; exit 1; fi; \
	printf 'CONST DRV_SIZE = %s, BGM_SIZE = %s, SFX_SIZE = %s;\n' $$drv $$bgm $$sfx > $(SIZES); \
	echo "generated $(SIZES): DRV=$$drv BGM=$$bgm SFX=$$sfx"

# ----------------------------------------------------------------------------------------------------
# x1 (LSX-Dodgers): 本体 d88 を slangbuild で生成 → ndc で driver/BGM/SFX を投入。
# disk 投入名を ARKOSMP.SL の FOPEN 名 (PSGAKG.BIN / BGM.AKG / SE.AKX) に揃える
# (= staging copy で投入元 file 名を一致させてから ndc P)。
# ----------------------------------------------------------------------------------------------------
x1: $(DRIVER) $(SIZES)
	$(SLANGBUILD) -E x1 $(INCDIR) $(SRC) -o ARKOSMP_x1 --emit disk
	cp $(DRIVER) PSGAKG.BIN
	-$(NDC) D ARKOSMP_x1.d88 0 PSGAKG.BIN
	-$(NDC) D ARKOSMP_x1.d88 0 BGM.AKG
	-$(NDC) D ARKOSMP_x1.d88 0 SE.AKX
	$(NDC) P ARKOSMP_x1.d88 0 PSGAKG.BIN
	$(NDC) P ARKOSMP_x1.d88 0 BGM.AKG
	$(NDC) P ARKOSMP_x1.d88 0 SE.AKX
	rm -f PSGAKG.BIN

# ----------------------------------------------------------------------------------------------------
# sosx1 (S-OS for X1): 本体 d88 を slangbuild で生成 → HuDisk で asset 投入。
# HuDisk は mono 経由 (macOS/Linux)。 load/exec は asset データなので不問 (= -r/-g 省略)。
# ----------------------------------------------------------------------------------------------------
sosx1: $(DRIVER) $(SIZES)
	$(SLANGBUILD) -E sosx1 $(INCDIR) $(SRC) -o ARKOSMP_sosx1 --emit disk
	cp $(DRIVER) PSGAKG.BIN
	-$(MONO) $(HUDISK) -d ARKOSMP_sosx1.d88 PSGAKG.BIN
	-$(MONO) $(HUDISK) -d ARKOSMP_sosx1.d88 BGM.AKG
	-$(MONO) $(HUDISK) -d ARKOSMP_sosx1.d88 SE.AKX
	$(MONO) $(HUDISK) -a ARKOSMP_sosx1.d88 PSGAKG.BIN
	$(MONO) $(HUDISK) -a ARKOSMP_sosx1.d88 BGM.AKG
	$(MONO) $(HUDISK) -a ARKOSMP_sosx1.d88 SE.AKX
	rm -f PSGAKG.BIN

# ----------------------------------------------------------------------------------------------------
# x1native (OS なし cassette tape): main + driver/BGM/SFX を多段 tape に連結。
# --tape-add の順 = tape 物理順 = ARKOSMP.SL の MTREAD 呼び出し順 (driver → BGM → SFX)。
# driver だけ @$8000 明記で tap inspect 時に配置意図を残す (= MTREAD は呼出側 addr 優先)。
# ----------------------------------------------------------------------------------------------------
x1native: $(DRIVER) $(SIZES)
	$(SLANGBUILD) -E x1native $(INCDIR) $(SRC) -o ARKOSMP_x1native --emit tape \
	  --tape-add $(DRIVER)@$(DRV_ORG) \
	  --tape-add BGM.AKG \
	  --tape-add SE.AKX

# ----------------------------------------------------------------------------------------------------
# x1native_slfs (OS なし SLFS disk): --slfs-add で asset を SLFS に収納。
# asset name (PSGAKG.BIN 等) から generated header の FILE_* が作られる
# (= ARKOSMP.assets.inc、 slangbuild 自動生成)。 ARKOSMP.SL の FS_READ_BY_ID(FILE_*) と一致。
# ----------------------------------------------------------------------------------------------------
x1native_slfs: $(DRIVER) $(SIZES)
	$(SLANGBUILD) -E x1native_slfs $(INCDIR) $(SRC) -o ARKOSMP_slfs --emit disk \
	  --slfs-add "PSGAKG.BIN:$(DRIVER)" \
	  --slfs-add "BGM.AKG:BGM.AKG" \
	  --slfs-add "SE.AKX:SE.AKX"

all: x1 sosx1 x1native x1native_slfs

clean:
	rm -f $(DRIVER) $(SIZES) PSGAKG.BIN \
	      ARKOSMP_x1.* ARKOSMP_sosx1.* ARKOSMP_x1native.* ARKOSMP_slfs.* \
	      ARKOSMP.assets.inc \
	      *.ASM *.LST *.sym *.bin

.PHONY: x1 sosx1 x1native x1native_slfs all clean
