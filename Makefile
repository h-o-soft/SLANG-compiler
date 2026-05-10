# SLANG Compiler Makefile
# ===================================
# make                               - SLANGTEST.SLをコンパイル→アセンブル
# make run                           - CPMエミュで実行 (LSX環境)
# make asm                           - アセンブルのみ
# make TARGET=examples/STARS         - 別ソース指定
# make ENV=msxrom TARGET=examples/MSXROM  - MSX ROM環境
# make compare                       - 旧コンパイラ出力と比較
# make clean                         - 成果物削除
# make publish VERSION=x.x.x         - 全プラットフォーム向けリリース作成
# make setup-tools                    - 開発ツール(AILZ80ASM等)のダウンロード

# === 設定 ===
TARGET ?= SLANGTEST
ENV ?= lsx

# ツール
DOTNET = dotnet
SLANGC_NEW = $(DOTNET) run --project src/SLANGCompiler.CLI/SLANGCompiler.CLI.csproj -c Release --
SLANGC_OLD = $(DOTNET) run --project SLANGCompiler.csproj -c Release --

# tools/ 配下のバイナリを直接参照 (PATH 環境に依存しない)。setup-tools で配置済みの想定。
ifeq ($(OS),Windows_NT)
  ASM = tools\AILZ80ASM.exe
  NDC = tools\NDC.exe
else
  ASM = tools/AILZ80ASM
  NDC = tools/ndc
endif
HUDISK = HuDisk
MZD88 ?= mzd88
MODSPLIT = ModuleSplitter

# ファイル
SRC = $(TARGET).SL
ASM_NEW = $(TARGET).ASM
ASM_OLD = $(TARGET).OLD.ASM
BIN_EXT = .bin
OUTPROG = $(dir $(TARGET))PROG$(BIN_EXT)
LST = $(TARGET).lst
SYM = $(TARGET).sym

# CP/M環境
ifeq ($(ENV),cpm)
  SLANGENV = lsx
else
  SLANGENV = $(ENV)
endif

# ASMオプション、エミュレータ、ディスクイメージを環境に応じて設定
ASM_OPT =
BIN_EXT_ENV = $(BIN_EXT)

ifeq ($(ENV), lsx)
  # RunCPM wrapper (tools/runcpm.sh on Unix, tools/runcpm.bat on Windows)
  ifeq ($(OS),Windows_NT)
    EMU = tools\runcpm.bat
  else
    EMU = ./tools/runcpm.sh
  endif
  DISK_IMAGE = $(OUTPROG)
  BIN_EXT_ENV = .com
else ifeq ($(ENV), cpm)
  ifeq ($(OS),Windows_NT)
    EMU = tools\runcpm.bat
  else
    EMU = ./tools/runcpm.sh
  endif
  DISK_IMAGE = $(OUTPROG)
  BIN_EXT_ENV = .com
else ifeq ($(ENV), x1)
  # EMU = ~/emu/X1/X1.exe
  EMU = @echo "X1 emulator not configured. Set EMU variable" \#
  DISK_IMAGE = images/LSXPROG.D88
  BIN_EXT_ENV = .com
else ifeq ($(ENV), sos)
  EMU = @echo "S-OS emulator not configured. Set EMU variable" \#
  DISK_IMAGE = images/SOSPROG.D88
else ifeq ($(ENV), sosx1)
  # EMU = ~/emu/X1/X1.exe
  EMU = @echo "S-OS emulator not configured. Set EMU variable" \#
  DISK_IMAGE = images/SOSPROG.D88
else ifeq ($(ENV), sosmz2500)
  EMU = @echo "MZ-2500 S-OS emulator not configured. Set EMU variable" \#
  DISK_IMAGE = images/SOSPROG.D88
else ifeq ($(ENV), mz25iocs)
  EMU = @echo "MZ-2500 emulator not configured. Set EMU variable" \#
  DISK_IMAGE = $(dir $(TARGET))M25PROG.D88
  BIN_EXT = .obj
  BIN_EXT_ENV = .obj
else ifeq ($(ENV), msxrom)
  # EMU = /Applications/openMSX.app/Contents/MacOS/openmsx
  EMU = @echo "MSX emulator not configured. Set EMU variable" \#
  EMUOPT = -cart
  DISK_IMAGE = $(OUTPROG)
else ifeq ($(ENV), msx2)
  # EMU = /Applications/openMSX.app/Contents/MacOS/openmsx
  EMU = @echo "MSX emulator not configured. Set EMU variable" \#
  DISK_IMAGE = images/dosformsx.dsk
  BIN_EXT_ENV = .com
  EMUOPT = -diska
else ifeq ($(findstring $(ENV),pc80mk2 pc80mk2x),$(ENV))
  # EMU = ~/emu/PC8001mkII/pc8001mk2.exe
  EMU = @echo "PC-8001 emulator not configured. Set EMU variable" \#
  BIN_EXT = .cmt
  BIN_EXT_ENV = .cmt
  ASM_OPT = -cmt -gap 0
endif

IMGPROG = $(basename $(OUTPROG))$(BIN_EXT_ENV)

.PHONY: all compile asm run compare clean help old-compile publish setup-tools

all: asm

# === 新コンパイラでコンパイル ===
compile: $(ASM_NEW)

$(ASM_NEW): $(SRC)
	$(SLANGC_NEW) -E $(SLANGENV) -I include -o $@ $<

# === AILZ80ASMでアセンブル ===
asm: $(OUTPROG)

$(OUTPROG): $(ASM_NEW)
	$(ASM) $< -f -o $@ -bin -sym -lst $(ASM_OPT)
	@echo "=== Assemble OK: $@ ==="
ifeq ($(OS),Windows_NT)
	@dir $(subst /,\,$@)
else
	@ls -la $@
endif

# === バイナリ拡張子変換（.bin→.com等） ===
ifneq ($(BIN_EXT),$(BIN_EXT_ENV))
ifeq ($(OS),Windows_NT)
$(IMGPROG): $(OUTPROG)
	move $(subst /,\,$<) $(subst /,\,$@)
else
$(IMGPROG): $(OUTPROG)
	mv $< $@
endif
endif

# === ディスクイメージ作成+エミュレータ実行 ===
ifeq ($(ENV),$(filter $(ENV),lsx cpm))
# LSX/CPM: cpmエミュで直接実行 (Windows は引数のパス区切りを \ に変換)
ifeq ($(OS),Windows_NT)
run: $(OUTPROG)
	$(EMU) $(subst /,\,$<)
else
run: $(OUTPROG)
	$(EMU) $<
endif
else ifeq ($(ENV), msxrom)
# MSX ROM: カートリッジとして実行
run: $(OUTPROG)
	$(EMU) $(EMUOPT) $(DISK_IMAGE)
else ifeq ($(ENV),$(filter $(ENV),sos sosx1 sosmz2500))
# S-OS: HuDiskでD88イメージに格納
run: $(IMGPROG)
	$(HUDISK) -d $(DISK_IMAGE) PROG.bin
	$(HUDISK) -a $(DISK_IMAGE) $(IMGPROG) -r 3000 -g 3000
	$(EMU) $(DISK_IMAGE)
else ifeq ($(ENV),mz25iocs)
# MZ-2500 BASIC/IOCS: mzd88でD88イメージに格納
run: $(IMGPROG)
	$(MZD88) -blank $(DISK_IMAGE) --title SLANG
	$(MZD88) -add $(DISK_IMAGE) $(IMGPROG) --force --load-addr 8000H --exec-addr 8000H
	$(MZD88) -add $(DISK_IMAGE) runtime/mz2500/J8000.bas.bsd --force
	$(EMU) $(DISK_IMAGE)
else ifeq ($(ENV),$(filter $(ENV),x1 msx2 msxlsx))
# X1/MSX: ndcでディスクイメージに格納
run: $(IMGPROG)
	- $(NDC) D $(DISK_IMAGE) 0 PROG$(BIN_EXT_ENV)
	$(NDC) P $(DISK_IMAGE) 0 $(IMGPROG)
	$(EMU) $(EMUOPT) $(DISK_IMAGE)
else
run: $(OUTPROG)
	@echo "Run: ENV=$(ENV) — use appropriate emulator manually with $(OUTPROG)"
endif

# === 旧コンパイラでコンパイル（比較用） ===
old-compile: $(ASM_OLD)

$(ASM_OLD): $(SRC)
	$(SLANGC_OLD) -O $@ $<

# === 新旧ASM出力比較 ===
compare: $(ASM_NEW) $(ASM_OLD)
	@echo "=== Diff: $(ASM_OLD) vs $(ASM_NEW) ==="
	@echo "Old: $$(wc -l < $(ASM_OLD)) lines"
	@echo "New: $$(wc -l < $(ASM_NEW)) lines"
	@diff $(ASM_OLD) $(ASM_NEW) | head -60 || true
	@echo "..."
	@echo "Total diff lines: $$(diff $(ASM_OLD) $(ASM_NEW) | wc -l)"

# === 未解決シンボルチェック ===
check-symbols: $(ASM_NEW)
	@echo "=== Undefined symbols check ==="
	@grep 'CALL\s' $(ASM_NEW) | sed 's/.*CALL[[:space:]]*//' | sort -u | while read f; do \
		case "$$f" in \$$*|.*|NZ,*|Z,*|C,*|NC,*|P,*|M,*|PE,*|PO,*) continue;; esac; \
		if ! grep -q "^$${f}:" $(ASM_NEW); then \
			echo "  MISSING: $$f"; \
		fi; \
	done
	@echo "Done."

# === クリーンアップ ===
ifeq ($(OS),Windows_NT)
clean:
	-del /Q $(subst /,\,$(ASM_NEW)) $(subst /,\,$(ASM_OLD)) $(subst /,\,$(OUTPROG)) $(subst /,\,$(IMGPROG)) $(subst /,\,$(LST)) $(subst /,\,$(SYM)) 2>nul
	-del /Q $(subst /,\,$(dir $(TARGET)))PROG.bin $(subst /,\,$(dir $(TARGET)))PROG.com $(subst /,\,$(dir $(TARGET)))PROG.cmt 2>nul
	-del /Q $(subst /,\,$(TARGET)).lst $(subst /,\,$(TARGET)).sym 2>nul
else
clean:
	rm -f $(ASM_NEW) $(ASM_OLD) $(OUTPROG) $(IMGPROG) $(LST) $(SYM)
	rm -f $(dir $(TARGET))PROG.bin $(dir $(TARGET))PROG.com $(dir $(TARGET))PROG.cmt
	rm -f $(TARGET).lst $(TARGET).sym
endif

# === ヘルプ ===
help:
	@echo "SLANG Compiler Makefile"
	@echo ""
	@echo "Usage:"
	@echo "  make                          - コンパイル+アセンブル (SLANGTEST.SL)"
	@echo "  make run                      - CPMエミュで実行"
	@echo "  make compile                  - コンパイルのみ (.ASM生成)"
	@echo "  make asm                      - アセンブルまで (.bin生成)"
	@echo "  make compare                  - 旧コンパイラ出力との比較"
	@echo "  make check-symbols            - 未解決シンボルチェック"
	@echo "  make clean                    - 成果物削除"
	@echo "  make publish VERSION=x.x.x    - 全プラットフォーム向けリリース作成"
	@echo "  make setup-tools              - 開発ツールのダウンロード"
	@echo ""
	@echo "Options:"
	@echo "  TARGET=path  - ソースファイル (拡張子なし, default: SLANGTEST)"
	@echo "  ENV=name     - 環境名 (default: lsx)"
	@echo ""
	@echo "Examples:"
	@echo "  make TARGET=examples/STARS"
	@echo "  make ENV=msxrom TARGET=examples/MSXROM compile"
	@echo "  make compare"

# === リリースパッケージ作成 ===
publish:
ifndef VERSION
	$(error VERSION is required. Usage: make publish VERSION=0.20.0)
endif
	./publish.sh $(VERSION)

# === ローカル開発用 publish (現在 OS のみ、bin/ に配置) ===
#
# Windows clone 直後に Makefile.dist の install / disk_image / run 等の
# テストフローを回すための簡易 publish。**release zip 作成ではない**
# (= リリース用は make publish VERSION=x.x.x → publish.sh で 4 platform 一括)。
#
# RID は default で current OS を自動検出 (win-x64 / osx-arm64 / linux-x64
# 等)。`RID=win-arm64 make publish-local` のように上書き可能。
ifeq ($(OS),Windows_NT)
RID ?= win-x64
else
RID ?= $(shell dotnet --info | awk '/RID:/{print $$2; exit}')
endif

publish-local:
ifeq ($(OS),Windows_NT)
	-mkdir bin 2>nul
	$(DOTNET) publish src/SLANGCompiler.CLI/SLANGCompiler.CLI.csproj -c Release -r $(RID) --self-contained true /p:PublishSingleFile=true
	$(DOTNET) publish src/SLANGCompiler.Build/SLANGCompiler.Build.csproj -c Release -r $(RID) --self-contained true /p:PublishSingleFile=true
	copy /Y src\SLANGCompiler.CLI\bin\Release\net8.0\$(RID)\publish\slangc.exe bin
	copy /Y src\SLANGCompiler.Build\bin\Release\net8.0\$(RID)\publish\slangbuild.exe bin
else
	mkdir -p bin
	$(DOTNET) publish src/SLANGCompiler.CLI/SLANGCompiler.CLI.csproj -c Release -r $(RID) --self-contained true /p:PublishSingleFile=true
	$(DOTNET) publish src/SLANGCompiler.Build/SLANGCompiler.Build.csproj -c Release -r $(RID) --self-contained true /p:PublishSingleFile=true
	cp src/SLANGCompiler.CLI/bin/Release/net8.0/$(RID)/publish/slangc bin/
	cp src/SLANGCompiler.Build/bin/Release/net8.0/$(RID)/publish/slangbuild bin/
endif
	@echo "publish-local done. bin/ now has slangc(.exe) and slangbuild(.exe) for $(RID)."
	@echo "Use them via Makefile.dist: make -f Makefile.dist TARGET=examples/MODTEST_RESIDENT ENV=x1 run"

# === 開発ツールのダウンロード ===
setup-tools:
ifeq ($(OS),Windows_NT)
	setupenv.bat
else ifeq ($(shell uname -s),Darwin)
	./setupenv.sh mac
else
	./setupenv.sh linux
endif
