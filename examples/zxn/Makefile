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

# --- #MODULE を Next のページに入れる見本 (MODULE.SL) ---
# slangbuild が NEX を直接書く (--emit nex。nexcreator を使わない)
module: MODULE.nex
	$(EMU) MODULE.nex

MODULE.nex: MODULE.SL
	$(SLANGBUILD) -E zxn MODULE.SL -o MODULE --emit nex

# --- Arkos Tracker (AKM) の見本 (ARKOS.md) ---
# 再生部 PSGAKM.bin を RASM で $E000 に組み、ARKOS.SL と曲・効果音と一緒に NEX にする。
# 置き場: 本体 $8000〜 / 曲 BGM.AKM $B000 / 効果音 SE.AKX $C000 / ワーク $D000〜 / スタック $E000 から下 / 再生部 $E000〜
RASM ?= rasm

arkos: ARKOS.nex
	$(EMU) ARKOS.nex

PSGAKM.bin: ../../runtime/zxn/playerAkm_zxn_wrapper.asm ../../runtime/x1/PlayerAkm_x1.asm ../../runtime/x1/PlayerAkm_SoundEffects.asm
	$(RASM) ../../runtime/zxn/playerAkm_zxn_wrapper.asm -DDRIVER_ORG=0xE000 -o PSGAKM

# .sym を __WORKEND__ の検査に使うので --keep-asm を付ける
ARKOS.bin: ARKOS.SL ARKOSZXN.SL
	$(SLANGBUILD) -E zxn ARKOS.SL -o ARKOS --keep-asm

# 置き場の重なりを調べてから NEX にする (重なったら止める)。スタックに 512 バイトを取っておく
ARKOS.nex: ARKOS.bin PSGAKM.bin ARKOS.cfg ../X1_ARKOS/BGM.AKM ../X1_ARKOS/SE.AKX
	@prg=`wc -c < ARKOS.bin | tr -d ' '`; \
	bgm=`wc -c < ../X1_ARKOS/BGM.AKM | tr -d ' '`; \
	sfx=`wc -c < ../X1_ARKOS/SE.AKX | tr -d ' '`; \
	drv=`wc -c < PSGAKM.bin | tr -d ' '`; \
	we=`sed -n 's/^__WORKEND__ equ \$$\([0-9A-Fa-f]*\).*/\1/p' ARKOS.sym`; \
	if [ -z "$$we" ]; then echo "ERROR: __WORKEND__ not found in ARKOS.sym"; exit 1; fi; \
	we=`printf '%d' 0x$$we`; \
	if [ `expr 32768 + $$prg` -gt 45056 ]; then echo "ERROR: ARKOS.bin ($$prg B) overflows \$$8000..\$$B000"; exit 1; fi; \
	if [ `expr 45056 + $$bgm` -gt 49152 ]; then echo "ERROR: BGM.AKM ($$bgm B) overflows \$$B000..\$$C000"; exit 1; fi; \
	if [ `expr 49152 + $$sfx` -gt 53248 ]; then echo "ERROR: SE.AKX ($$sfx B) overflows \$$C000..\$$D000"; exit 1; fi; \
	if [ $$we -gt 56832 ]; then echo "ERROR: work ends at $$we, over \$$E000-512"; exit 1; fi; \
	if [ `expr 57344 + $$drv` -gt 64768 ]; then echo "ERROR: PSGAKM.bin ($$drv B) overflows \$$E000..\$$FD00"; exit 1; fi; \
	echo "ARKOS: prg=$$prg bgm=$$bgm sfx=$$sfx work_end=$$we drv=$$drv"
	$(NEXCREATOR) ARKOS.cfg ARKOS.nex

clean:
	rm -f *.bin *.nex *.ASM *.LST *.sym
