# SLANG Compiler Makefile
# ===========================
# make              - コンパイラをビルド
# make release      - リリースビルド
# make install      - インストール (PREFIX=/usr/local)
# make uninstall    - アンインストール
# make publish      - 全プラットフォーム向けリリース作成 (VERSION必須)
# make setup-tools  - 開発ツール(AILZ80ASM等)のダウンロード
# make clean        - クリーンアップ

# 設定
DOTNET = dotnet
VERSION ?=
PREFIX ?= /usr/local
BINDIR = $(PREFIX)/bin
CONFIG_DIR = $(HOME)/.config/SLANG

# OS検出
ifeq ($(OS),Windows_NT)
    DETECTED_OS = Windows
    MKDIR = mkdir
    RM = del /Q
    RMDIR = rmdir /S /Q
    CP = copy
    XCOPY = xcopy /E /Y
    PATHSEP = \\
    EXE_EXT = .exe
    SHELL_EXT = bat
else
    UNAME_S := $(shell uname -s)
    ifeq ($(UNAME_S),Darwin)
        DETECTED_OS = macOS
        ARCH := $(shell uname -m)
        ifeq ($(ARCH),arm64)
            RID = osx-arm64
        else
            RID = osx-x64
        endif
    else
        DETECTED_OS = Linux
        RID = linux-x64
    endif
    MKDIR = mkdir -p
    RM = rm -f
    RMDIR = rm -rf
    CP = cp
    XCOPY = cp -R
    PATHSEP = /
    EXE_EXT =
    SHELL_EXT = sh
endif

# ターゲット
.PHONY: all build release install uninstall publish setup-tools clean help

all: build

# ビルド (Debug)
build:
	$(DOTNET) build
	cd ModuleSplitter && $(DOTNET) build

# ビルド (Release)
release:
	$(DOTNET) build -c Release
	cd ModuleSplitter && $(DOTNET) build -c Release

# ローカルインストール用にpublish (self-contained)
publish-local:
ifeq ($(DETECTED_OS),Windows)
	$(DOTNET) publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
	cd ModuleSplitter && $(DOTNET) publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=true
else
	$(DOTNET) publish -c Release -r $(RID) --self-contained true /p:PublishSingleFile=true
	cd ModuleSplitter && $(DOTNET) publish -c Release -r $(RID) --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=true
endif

# インストール
install: publish-local install-bin install-lib
	@echo "Installation complete!"
	@echo "  Binaries: $(BINDIR)"
	@echo "  Libraries: $(CONFIG_DIR)"

install-bin:
ifeq ($(DETECTED_OS),Windows)
	@echo "Windows: Please add bin directory to PATH manually"
	@if not exist "$(BINDIR)" $(MKDIR) "$(BINDIR)"
	$(CP) bin\Release\net6.0\win-x64\publish\SLANGCompiler.exe "$(BINDIR)\"
	$(CP) ModuleSplitter\ModuleSplitter\bin\Release\net6.0\win-x64\publish\ModuleSplitter.exe "$(BINDIR)\"
else
	$(MKDIR) $(BINDIR)
	$(CP) bin/Release/net6.0/$(RID)/publish/SLANGCompiler $(BINDIR)/
	$(CP) ModuleSplitter/ModuleSplitter/bin/Release/net6.0/$(RID)/publish/ModuleSplitter $(BINDIR)/
	chmod +x $(BINDIR)/SLANGCompiler
	chmod +x $(BINDIR)/ModuleSplitter
endif

install-lib:
ifeq ($(DETECTED_OS),Windows)
	@if not exist "$(CONFIG_DIR)" $(MKDIR) "$(CONFIG_DIR)"
	@if not exist "$(CONFIG_DIR)\include" $(MKDIR) "$(CONFIG_DIR)\include"
	@if not exist "$(CONFIG_DIR)\lib" $(MKDIR) "$(CONFIG_DIR)\lib"
	$(XCOPY) include "$(CONFIG_DIR)\include"
	$(XCOPY) lib "$(CONFIG_DIR)\lib"
else
	$(MKDIR) $(CONFIG_DIR)
	$(XCOPY) include $(CONFIG_DIR)/
	$(XCOPY) lib $(CONFIG_DIR)/
endif

# アンインストール
uninstall:
ifeq ($(DETECTED_OS),Windows)
	$(RM) "$(BINDIR)\SLANGCompiler.exe"
	$(RM) "$(BINDIR)\ModuleSplitter.exe"
	$(RMDIR) "$(CONFIG_DIR)"
else
	$(RM) $(BINDIR)/SLANGCompiler
	$(RM) $(BINDIR)/ModuleSplitter
	$(RMDIR) $(CONFIG_DIR)
endif
	@echo "Uninstallation complete!"

# パブリッシュ (全プラットフォーム向けリリース作成)
publish:
ifndef VERSION
	$(error VERSION is required. Usage: make publish VERSION=1.0.0)
endif
	./publish.sh $(VERSION)

# 開発ツールのセットアップ
setup-tools:
ifeq ($(DETECTED_OS),Windows)
	setupenv.bat
else ifeq ($(DETECTED_OS),macOS)
	./setupenv.sh mac
else
	./setupenv.sh linux
endif

# クリーンアップ
clean:
	$(DOTNET) clean
	cd ModuleSplitter && $(DOTNET) clean
	$(RMDIR) bin obj publish
	$(RMDIR) ModuleSplitter/ModuleSplitter/bin ModuleSplitter/ModuleSplitter/obj

# ヘルプ
help:
	@echo "SLANG Compiler Makefile"
	@echo ""
	@echo "Usage:"
	@echo "  make              - Build compiler (Debug)"
	@echo "  make release      - Build compiler (Release)"
	@echo "  make install      - Install to $(PREFIX)"
	@echo "  make uninstall    - Uninstall from $(PREFIX)"
	@echo "  make publish VERSION=x.x.x - Create release packages"
	@echo "  make setup-tools  - Download development tools"
	@echo "  make clean        - Clean build artifacts"
	@echo ""
	@echo "Options:"
	@echo "  PREFIX=path       - Installation prefix (default: /usr/local)"
	@echo "  VERSION=x.x.x     - Version for publish target"
	@echo ""
	@echo "Detected OS: $(DETECTED_OS)"
ifeq ($(DETECTED_OS),macOS)
	@echo "Runtime ID: $(RID)"
endif
