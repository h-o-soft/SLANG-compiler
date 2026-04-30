NEXCREATOR = nexcreator
SLANGBUILD = slangbuild
EMU = mono Emu/CSpect/CSpect.exe -w6 -tv -nextrom -zxnext

# default は NextDAW 不要の game_nomusic 版 (= 配布 zip 解凍直後でも
# build 可能)。NextDAW を含む完全版が欲しい場合は `make music`
# (要 NextDAW_RuntimePlayer_E000.bin を examples/zxn/ 配下に配置)。

all: build
	$(EMU) game_nomusic.nex

build: game_nomusic.nex

# 旧 target との互換 alias
nomusic: build
build_nomusic: build

# NextDAW を含む完全版 (要 NextDAW Runtime Player を examples/zxn/ 配下に配置)
music: game.nex
	$(EMU) game.nex

build_music: game.nex

# slangbuild が SLANG → ASM → bin を 1 段で実行。デバッグ用 .sym / .ASM
# を残したい場合は --keep-asm を追加。
%.bin: %.sl
	$(SLANGBUILD) -E zxn $< -o $*

# nexcreator で .bin → .nex (CSpect 等で実行可能形式)。.cfg 変更時にも再生成。
%.nex: %.bin %.cfg
	$(NEXCREATOR) $*.cfg $@

clean:
	rm -f *.bin *.nex *.ASM *.LST *.sym
