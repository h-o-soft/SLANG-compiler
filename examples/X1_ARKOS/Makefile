# ====================================================================================================
# Arkos Tracker driver sample — AKG / AKM × 4 env build orchestration
#
# AKG (generic player、 BGM=.akg):
#   make x1            → ARKOSMP_x1.d88      (LSX-Dodgers、 ndc で asset 同梱)
#   make sosx1         → ARKOSMP_sosx1.d88   (S-OS for X1、 HuDisk で asset 同梱)
#   make x1native      → ARKOSMP_x1native.tap (OS なし cassette tape、 --tape-add 多段)
#   make x1native_slfs → ARKOSMP_slfs.d88    (OS なし SLFS disk、 --slfs-add)
#
# AKM (minimalist player、 BGM=.akm、 driver は AKG より小さい):
#   make akm-x1 / akm-sosx1 / akm-x1native / akm-x1native_slfs → AKMMP_*.{d88,tap}
#
#   make all   … AKG / AKM 両方 × 4 env
#   make clean
#
# AKG / AKM は同一の中間層 (ARKOS_*) / 同一配置 (driver=$8000 / BGM=$B000 / SFX=$C000) で、
# 違いは driver bin (PSGAKG/PSGAKM) と BGM データ (.akg/.akm) だけ。 driver は jp table が
# absolute addr 参照のため固定 ORG ($8000) で RASM ビルド。 BGM/SFX は Arkos export 時の
# アドレス前提。 配置は ARKOSMP.SL / AKMMP.SL の CONST と一致させること。
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
SFX     = SE.AKX

# 配置アドレス (= *.SL の CONST と一致)
DRV_ORG = 0x8000

# --- AKG ---
SRC_AKG    = ARKOSMP.SL
DRV_AKG    = PSGAKG_8000.bin
SIZES_AKG  = ARKOSMP.sizes.inc
BGM_AKG    = BGM.AKG

# --- AKM ---
SRC_AKM    = AKMMP.SL
DRV_AKM    = PSGAKM_8000.bin
SIZES_AKM  = AKMMP.sizes.inc
BGM_AKM    = BGM.AKM

# ----------------------------------------------------------------------------------------------------
# driver: Arkos wrapper を ORG $8000 で RASM ビルド (AKG=generic / AKM=minimalist)
# ----------------------------------------------------------------------------------------------------
$(DRV_AKG): ../../runtime/x1/playerAkg_x1_wrapper.asm \
            ../../runtime/x1/PlayerAkg_x1.asm \
            ../../runtime/x1/PlayerAkg_SoundEffects.asm
	$(RASM) ../../runtime/x1/playerAkg_x1_wrapper.asm -DDRIVER_ORG=$(DRV_ORG) -o PSGAKG_8000 -s

$(DRV_AKM): ../../runtime/x1/playerAkm_x1_wrapper.asm \
            ../../runtime/x1/PlayerAkm_x1.asm \
            ../../runtime/x1/PlayerAkm_SoundEffects.asm
	$(RASM) ../../runtime/x1/playerAkm_x1_wrapper.asm -DDRIVER_ORG=$(DRV_ORG) -o PSGAKM_8000 -s

# ----------------------------------------------------------------------------------------------------
# sizes.inc: FREAD 用の file size 定数 (= driver/BGM/SFX の実 byte 数)。
# 同時に raw 配置の重なりを early fail でチェック (= runtime load 版は linker が守らない)。
#   DRV $8000 + DRV_SIZE <= BGM $B000、 BGM + BGM_SIZE <= SFX $C000、 SFX + SFX_SIZE <= $10000
# size 取得は `wc -c` (= macOS/BSD `stat -f%z` と GNU `stat -c%s` のオプション差を回避)。
# ----------------------------------------------------------------------------------------------------
$(SIZES_AKG): $(DRV_AKG) $(BGM_AKG) $(SFX)
	@drv=`wc -c < $(DRV_AKG) | tr -d ' '`; \
	bgm=`wc -c < $(BGM_AKG) | tr -d ' '`; \
	sfx=`wc -c < $(SFX) | tr -d ' '`; \
	if [ `expr 32768 + $$drv` -gt 45056 ]; then \
	  echo "ERROR: driver ($$drv B) overflows \$$8000..\$$B000"; exit 1; fi; \
	if [ `expr 45056 + $$bgm` -gt 49152 ]; then \
	  echo "ERROR: BGM ($$bgm B) overflows \$$B000..\$$C000"; exit 1; fi; \
	if [ `expr 49152 + $$sfx` -gt 65536 ]; then \
	  echo "ERROR: SFX ($$sfx B) overflows \$$C000..\$$10000"; exit 1; fi; \
	printf 'CONST DRV_SIZE = %s, BGM_SIZE = %s, SFX_SIZE = %s;\n' $$drv $$bgm $$sfx > $(SIZES_AKG); \
	echo "generated $(SIZES_AKG): DRV=$$drv BGM=$$bgm SFX=$$sfx"

$(SIZES_AKM): $(DRV_AKM) $(BGM_AKM) $(SFX)
	@drv=`wc -c < $(DRV_AKM) | tr -d ' '`; \
	bgm=`wc -c < $(BGM_AKM) | tr -d ' '`; \
	sfx=`wc -c < $(SFX) | tr -d ' '`; \
	if [ `expr 32768 + $$drv` -gt 45056 ]; then \
	  echo "ERROR: driver ($$drv B) overflows \$$8000..\$$B000"; exit 1; fi; \
	if [ `expr 45056 + $$bgm` -gt 49152 ]; then \
	  echo "ERROR: BGM ($$bgm B) overflows \$$B000..\$$C000"; exit 1; fi; \
	if [ `expr 49152 + $$sfx` -gt 65536 ]; then \
	  echo "ERROR: SFX ($$sfx B) overflows \$$C000..\$$10000"; exit 1; fi; \
	printf 'CONST DRV_SIZE = %s, BGM_SIZE = %s, SFX_SIZE = %s;\n' $$drv $$bgm $$sfx > $(SIZES_AKM); \
	echo "generated $(SIZES_AKM): DRV=$$drv BGM=$$bgm SFX=$$sfx"

# ----------------------------------------------------------------------------------------------------
# AKG: x1 (LSX-Dodgers): 本体 d88 を slangbuild で生成 → ndc で driver/BGM/SFX を投入。
# disk 投入名を SL の FOPEN 名 (PSGAKG.BIN / BGM.AKG / SE.AKX) に揃える
# (= staging copy で投入元 file 名を一致させてから ndc P)。
# ----------------------------------------------------------------------------------------------------
x1: $(DRV_AKG) $(SIZES_AKG)
	$(SLANGBUILD) -E x1 $(INCDIR) $(SRC_AKG) -o ARKOSMP_x1 --emit disk
	cp $(DRV_AKG) PSGAKG.BIN
	-$(NDC) D ARKOSMP_x1.d88 0 PSGAKG.BIN
	-$(NDC) D ARKOSMP_x1.d88 0 BGM.AKG
	-$(NDC) D ARKOSMP_x1.d88 0 SE.AKX
	$(NDC) P ARKOSMP_x1.d88 0 PSGAKG.BIN
	$(NDC) P ARKOSMP_x1.d88 0 BGM.AKG
	$(NDC) P ARKOSMP_x1.d88 0 SE.AKX
	rm -f PSGAKG.BIN

# AKG: sosx1 (S-OS for X1): 本体 d88 → HuDisk で asset 投入 (mono 経由)。
sosx1: $(DRV_AKG) $(SIZES_AKG)
	$(SLANGBUILD) -E sosx1 $(INCDIR) $(SRC_AKG) -o ARKOSMP_sosx1 --emit disk
	cp $(DRV_AKG) PSGAKG.BIN
	-$(MONO) $(HUDISK) -d ARKOSMP_sosx1.d88 PSGAKG.BIN
	-$(MONO) $(HUDISK) -d ARKOSMP_sosx1.d88 BGM.AKG
	-$(MONO) $(HUDISK) -d ARKOSMP_sosx1.d88 SE.AKX
	$(MONO) $(HUDISK) -a ARKOSMP_sosx1.d88 PSGAKG.BIN
	$(MONO) $(HUDISK) -a ARKOSMP_sosx1.d88 BGM.AKG
	$(MONO) $(HUDISK) -a ARKOSMP_sosx1.d88 SE.AKX
	rm -f PSGAKG.BIN

# AKG: x1native (OS なし cassette tape): main + driver/BGM/SFX を多段 tape に連結。
# --tape-add の順 = tape 物理順 = SL の MTREAD 呼び出し順 (driver → BGM → SFX)。
x1native: $(DRV_AKG) $(SIZES_AKG)
	$(SLANGBUILD) -E x1native $(INCDIR) $(SRC_AKG) -o ARKOSMP_x1native --emit tape \
	  --tape-add $(DRV_AKG)@$(DRV_ORG) \
	  --tape-add BGM.AKG \
	  --tape-add SE.AKX

# AKG: x1native_slfs (OS なし SLFS disk): --slfs-add で asset を SLFS に収納。
# asset name (PSGAKG.BIN 等) から generated header の FILE_* が作られる (ARKOSMP.assets.inc)。
x1native_slfs: $(DRV_AKG) $(SIZES_AKG)
	$(SLANGBUILD) -E x1native_slfs $(INCDIR) $(SRC_AKG) -o ARKOSMP_slfs --emit disk \
	  --slfs-add "PSGAKG.BIN:$(DRV_AKG)" \
	  --slfs-add "BGM.AKG:BGM.AKG" \
	  --slfs-add "SE.AKX:SE.AKX"

# ----------------------------------------------------------------------------------------------------
# AKM: AKG と同構造、 driver=PSGAKM / BGM=.akm / source=AKMMP.SL に差し替えただけ。
# ----------------------------------------------------------------------------------------------------
akm-x1: $(DRV_AKM) $(SIZES_AKM)
	$(SLANGBUILD) -E x1 $(INCDIR) $(SRC_AKM) -o AKMMP_x1 --emit disk
	cp $(DRV_AKM) PSGAKM.BIN
	-$(NDC) D AKMMP_x1.d88 0 PSGAKM.BIN
	-$(NDC) D AKMMP_x1.d88 0 BGM.AKM
	-$(NDC) D AKMMP_x1.d88 0 SE.AKX
	$(NDC) P AKMMP_x1.d88 0 PSGAKM.BIN
	$(NDC) P AKMMP_x1.d88 0 BGM.AKM
	$(NDC) P AKMMP_x1.d88 0 SE.AKX
	rm -f PSGAKM.BIN

akm-sosx1: $(DRV_AKM) $(SIZES_AKM)
	$(SLANGBUILD) -E sosx1 $(INCDIR) $(SRC_AKM) -o AKMMP_sosx1 --emit disk
	cp $(DRV_AKM) PSGAKM.BIN
	-$(MONO) $(HUDISK) -d AKMMP_sosx1.d88 PSGAKM.BIN
	-$(MONO) $(HUDISK) -d AKMMP_sosx1.d88 BGM.AKM
	-$(MONO) $(HUDISK) -d AKMMP_sosx1.d88 SE.AKX
	$(MONO) $(HUDISK) -a AKMMP_sosx1.d88 PSGAKM.BIN
	$(MONO) $(HUDISK) -a AKMMP_sosx1.d88 BGM.AKM
	$(MONO) $(HUDISK) -a AKMMP_sosx1.d88 SE.AKX
	rm -f PSGAKM.BIN

akm-x1native: $(DRV_AKM) $(SIZES_AKM)
	$(SLANGBUILD) -E x1native $(INCDIR) $(SRC_AKM) -o AKMMP_x1native --emit tape \
	  --tape-add $(DRV_AKM)@$(DRV_ORG) \
	  --tape-add BGM.AKM \
	  --tape-add SE.AKX

akm-x1native_slfs: $(DRV_AKM) $(SIZES_AKM)
	$(SLANGBUILD) -E x1native_slfs $(INCDIR) $(SRC_AKM) -o AKMMP_slfs --emit disk \
	  --slfs-add "PSGAKM.BIN:$(DRV_AKM)" \
	  --slfs-add "BGM.AKM:BGM.AKM" \
	  --slfs-add "SE.AKX:SE.AKX"

akg: x1 sosx1 x1native x1native_slfs
akm: akm-x1 akm-sosx1 akm-x1native akm-x1native_slfs
all: akg akm

clean:
	rm -f $(DRV_AKG) $(DRV_AKM) $(SIZES_AKG) $(SIZES_AKM) PSGAKG.BIN PSGAKM.BIN \
	      ARKOSMP_x1.* ARKOSMP_sosx1.* ARKOSMP_x1native.* ARKOSMP_slfs.* \
	      AKMMP_x1.* AKMMP_sosx1.* AKMMP_x1native.* AKMMP_slfs.* \
	      ARKOSMP.assets.inc AKMMP.assets.inc \
	      *.ASM *.LST *.sym *.bin

.PHONY: x1 sosx1 x1native x1native_slfs \
        akm-x1 akm-sosx1 akm-x1native akm-x1native_slfs \
        akg akm all clean
