PROJECT = game
NEXCREATOR = nexcreator

all: build
	mono Emu/CSpect/CSpect.exe -w6 -tv -nextrom  -zxnext game.nex

build: game.nex

clean:
	rm -f ${PROJECT}.bin
	rm -f $(PROJECT).asm
	rm -f game.nex

game.nex: ${PROJECT}.bin
	${NEXCREATOR} game.cfg game.nex

game.bin: $(PROJECT).sl
	SLANGCompiler -E zxn --output-debug-symbol $(PROJECT).sl
	AILZ80ASM $(PROJECT).asm -bin -f
