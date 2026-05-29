# ====================================================================================================
# PRINTTEST — PRINT 系メソッド表示確認サンプル (Issue #221 回帰確認も兼ねる)
#
#   make x1            → PRINTTEST_x1.d88       (LSX-Dodgers、 基準)
#   make sosx1         → PRINTTEST_sosx1.d88    (S-OS for X1、 基準)
#   make x1native      → PRINTTEST_x1native.tap (OS なし cassette tape)
#   make x1native_slfs → PRINTTEST_slfs.d88     (OS なし SLFS disk)
#   make all / make clean
#
# x1 / sosx1 を正常表示の基準として、 x1native / x1native_slfs と見比べる。
# tool override 可: SLANGBUILD= / NDC= / HUDISK= / MONO=
# ====================================================================================================

SLANGBUILD ?= slangbuild
NDC        ?= ndc
HUDISK     ?= $(HOME)/.config/SLANG/tools/HuDisk.exe
MONO       ?= mono

INCDIR = -I ../../include -I .
SRC    = PRINTTEST.SL

x1:
	$(SLANGBUILD) -E x1 $(INCDIR) $(SRC) -o PRINTTEST_x1 --emit disk

sosx1:
	$(SLANGBUILD) -E sosx1 $(INCDIR) $(SRC) -o PRINTTEST_sosx1 --emit disk

x1native:
	$(SLANGBUILD) -E x1native $(INCDIR) $(SRC) -o PRINTTEST_x1native --emit tape

x1native_slfs:
	$(SLANGBUILD) -E x1native_slfs $(INCDIR) $(SRC) -o PRINTTEST_slfs --emit disk

all: x1 sosx1 x1native x1native_slfs

clean:
	rm -f PRINTTEST_x1.* PRINTTEST_sosx1.* PRINTTEST_x1native.* PRINTTEST_slfs.* \
	      *.ASM *.LST *.sym *.bin

.PHONY: x1 sosx1 x1native x1native_slfs all clean
