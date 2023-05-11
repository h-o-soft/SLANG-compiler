# ファイル名拡張子の設定
SRC_EXT = .sl
ASM_EXT = .asm
BIN_EXT = .bin

# 環境名を指定
ENV ?= lsx
SLANGENV=$(ENV)

# ファイル名（拡張子なし）を指定
TARGET ?= examples/STARS

RUNADR = 3000
LOADADR = 3000

# ツールのコマンド名
SLANGCOMPILER = bin/SLANGcompiler
ASM = tools/AILZ80ASM
NDC = tools/ndc
HUDISK = tools/HuDisk

OUTPROG = $(dir $(TARGET))PROG.bin

# エミュレータのコマンド名とディスクイメージファイル名を環境に応じて設定
ifeq ($(ENV), lsx)
  EMU = C:\emu\X1\X1.exe
  # EMU = ~/emu/X1/X1.exe
  DISK_IMAGE = images/LSXPROG.D88
  BIN_EXT_ENV = .com
else ifeq ($(ENV), x1)
  EMU = C:\emu\X1\X1.exe
  # EMU = ~/emu/X1/X1.exe
  DISK_IMAGE = images/LSXPROG.D88
  BIN_EXT_ENV = .com
else ifeq ($(ENV), sos)
  EMU = C:\emu\X1\X1.exe
  # EMU = ~/emu/X1/X1.exe
  DISK_IMAGE = images/SOSPROG.D88
  BIN_EXT_ENV = $(BIN_EXT)
else ifeq ($(ENV), msxrom)
  EMU = C:\emu\MSX\openmsx\openmsx.exe
  # EMU = /Applications/openMSX.app/Contents/MacOS/openmsx
  BIN_EXT_ENV = $(BIN_EXT)
  EMUOPT = -cart
  DISK_IMAGE = $(OUTPROG)
else ifeq ($(ENV), msx2)
  EMU = C:\emu\MSX\openmsx\openmsx.exe
  # EMU = /Applications/openMSX.app/Contents/MacOS/openmsx
  DISK_IMAGE = images/dosformsx.dsk
  BIN_EXT_ENV = .com
  EMUOPT = -diska
else ifeq ($(ENV), msxlsx)
  EMU = C:\emu\MSX\openmsx\openmsx.exe
  # EMU = /Applications/openMSX.app/Contents/MacOS/openmsx
  DISK_IMAGE = images/dos2formsx.dsk
  BIN_EXT_ENV = .com
  EMUOPT = -diska
else ifeq ($(ENV), cpm)
  SLANGENV=lsx
  EMU = tools/cpm.exe
  DISK_IMAGE = $(OUTPROG)
endif

IMGPROG = $(basename $(OUTPROG))$(BIN_EXT_ENV)

# リネーム関数
define rename_func
ifeq ($(OS),Windows_NT)
  move /Y $(1) $(2)
else
  mv $(1) $(2)
endif
endef

all: run

# ソースコードのコンパイル
$(TARGET)$(ASM_EXT): $(TARGET)$(SRC_EXT)
	$(SLANGCOMPILER) $< -E $(SLANGENV) --output-debug-symbol

# アセンブリコードのアセンブル
$(OUTPROG): $(TARGET)$(ASM_EXT)
	$(ASM) $< -f -o $@ -bin -sym -lst

# ディスクイメージにバイナリファイルを格納
ifeq ($(ENV), cpm)
disk_image: $(OUTPROG)
else ifeq ($(ENV), msxrom)
disk_image: $(OUTPROG)
else ifeq ($(ENV), sos)
disk_image: $(IMGPROG)
	$(HUDISK) -d $(DISK_IMAGE) PROG.bin
	$(HUDISK) -a $(DISK_IMAGE) $(IMGPROG) -r $(LOADADR) -g $(RUNADR)
else ifeq ($(ENV), msx2)
disk_image: $(IMGPROG)
	- $(NDC) D $(DISK_IMAGE) 0 PROG$(BIN_EXT_ENV)
	$(NDC) P $(DISK_IMAGE) 0 $(IMGPROG)
else
disk_image: $(IMGPROG)
	- $(NDC) D $(DISK_IMAGE) 0 PROG$(BIN_EXT_ENV)
	$(NDC) P $(DISK_IMAGE) 0 $(IMGPROG)
endif

# バイナリファイルの拡張子を環境に応じて変更(必要に応じて)
ifneq ($(BIN_EXT), $(BIN_EXT_ENV))
$(basename $(OUTPROG))$(BIN_EXT_ENV): $(OUTPROG)
ifeq ($(OS),Windows_NT)
	move $< $@
else
	mv $< $@
endif
endif

# エミュレータでの実行
run: disk_image
	$(EMU) $(EMUOPT) $(DISK_IMAGE)

# クリーンアップ
clean:
ifeq ($(OS),Windows_NT)
	del /Q /F $(subst /,\,$(TARGET)$(ASM_EXT)) $(subst /,\,$(TARGET)$(BIN_EXT)) $(subst /,\,$(dir $(TARGET))PROG.bin) $(subst /,\,$(dir $(TARGET))PROG.com)
else
	rm -f $(TARGET)$(ASM_EXT) $(TARGET)$(BIN_EXT) $(TARGET)$(BIN_EXT_ENV) $(subst /,\,$(dir $(TARGET))PROG.bin) $(subst /,\,$(dir $(TARGET))PROG.com)
endif

.PHONY: all run clean
