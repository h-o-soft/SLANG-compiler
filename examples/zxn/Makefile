PROJECT = game
NEXCREATOR = nexcreator

all: build
	mono Emu/CSpect/CSpect.exe -w6 -tv -nextrom  -zxnext game.nex

build: game.nex

nomusic: game_nomusic.nex
	mono Emu/CSpect/CSpect.exe -w6 -tv -nextrom  -zxnext game_nomusic.nex

build_nomusic: game_nomusic.nex

clean:
	rm -f ${PROJECT}.bin
	rm -f $(PROJECT).asm
	rm -f game.nex
	rm -f game_nomusic.bin
	rm -f game_nomusic.asm
	rm -f game_nomusic.nex

game.nex: ${PROJECT}.bin
	${NEXCREATOR} game.cfg game.nex

game.bin: $(PROJECT).sl
	SLANGCompiler -E zxn --output-debug-symbol $(PROJECT).sl
	AILZ80ASM $(PROJECT).asm -bin -f

game_nomusic.nex: game_nomusic.bin
	${NEXCREATOR} game_nomusic.cfg game_nomusic.nex

game_nomusic.bin: game_nomusic.sl
	SLANGCompiler -E zxn --output-debug-symbol game_nomusic.sl
	AILZ80ASM game_nomusic.asm -bin -f
