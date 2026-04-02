
# MSX0側がアクセスするHTTPサーバのアドレス
WEBIP = 192.168.0.5
# MSX0側がアクセスするHTTPサーバのポート
WEBPORT = 80
# MSX0側がアクセスするHTTPサーバのURL
WEBADR = /~user

# HTTPサーバのドキュメントルート(実行ファイルのコピー先。ローカルでHTTPサーバが稼動している想定)
WEBDIR = ~/Sites

# MSX0のIPアドレスとポート番号
MSX0IP = 192.168.0.10
MSX0PORT = 2223

# SLANGプログラム名称
SRC ?= PROG.SL

# 各種ツールのパス(環境に合わせて変更すること)
SLANGCOMPILER = ./bin/SLANGCompiler
AILZASM = tools/AILZ80ASM
MSX0CMD = python3 bin/msx0cmd.py

TARGET = $(SRC:.SL=.COM)
ASMS= $(SRC:.SL=.ASM)

ifeq ($(OS),Windows_NT)
    CP = copy
else
    CP = cp
endif

ifeq ($(OS),Windows_NT)
    RM = del /Q
else
    RM = rm -f
endif

execute: transfer
	$(MSX0CMD) --ip $(MSX0IP) --port $(MSX0PORT) $(TARGET)

transfer: $(TARGET)
	$(CP) $(TARGET) $(WEBDIR)/
	$(MSX0CMD) --ip $(MSX0IP) --port $(MSX0PORT) --sleep 1 --check-str Done --error-str Error "MSX0GET $(WEBIP) $(WEBPORT) $(WEBADR)/$(TARGET) $(TARGET)"

$(TARGET): $(ASMS)
	$(AILZASM) $(ASMS) -f -o $@ -bin -sym -lst

$(ASMS): $(SRC)
	$(SLANGCOMPILER) $(SRC) -E msx2 --output-debug-symbol
  
all: clean $(ASMS) $(TARGET) execute

clean:
	-$(RM) $(ASMS) $(TARGET)
