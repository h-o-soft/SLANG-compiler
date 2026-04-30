PROJECT = game
NEXCREATOR = nexcreator
SLANGBUILD = slangbuild
EMU = mono Emu/CSpect/CSpect.exe -w6 -tv -nextrom -zxnext

all: build
	$(EMU) game.nex

build: game.nex

nomusic: game_nomusic.nex
	$(EMU) game_nomusic.nex

build_nomusic: game_nomusic.nex

# slangbuild が SLANG → ASM → bin を 1 段で実行 (旧 SLANGCompiler +
# AILZ80ASM 2 段経路を内製化)。デバッグ用 .sym / .ASM を残したい場合は
# --keep-asm を追加。
%.bin: %.sl
	$(SLANGBUILD) -E zxn $< -o $*

# nexcreator で .bin → .nex (CSpect 等で実行可能形式)。.cfg 変更時にも再生成。
%.nex: %.bin %.cfg
	$(NEXCREATOR) $*.cfg $@

clean:
	rm -f *.bin *.nex *.ASM *.LST *.sym
