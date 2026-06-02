#!/usr/bin/env python3
"""banjo_pack_assets.py - 複数の banjo 楽曲/SFX を順次アドレスでビルドし、
単一 bundle bin + アドレス CONST (.inc) を生成する。

banjo の楽曲/SFX blob は banjo_prep_song.py が load-addr 起点で内部ポインタを
**絶対アドレス焼込 (位置依存・再配置不可)** する。そのため複数を 1 アプリに載せるには
「各 blob を固有アドレスでビルド → そのアドレスにロード → アドレスで再生」が要る。
本ツールは driver 末尾から順にアドレスを割り当てて各 blob をビルド → 連結 → 各先頭
アドレスを SLANG CONST (.inc) に出力する。ユーザーは生成 .inc を #INCLUDE し
BANJO_PLAY(MUSIC_xxx) / BANJO_SFX_PLAY(SFX_xxx) で再生する (人手のアドレス計算が不要)。

既存ツール (furnace2json / json2sms_x1 / banjo_extract_syms / banjo_prep_song /
banjo_trim_to_end / upstream/json2sms) を orchestrate するだけで、 再実装はしない。

manifest 形式 (行順 = ロード順 = アドレス順、 # コメント / 空行は無視):
  music <file.fur>
  sfx   <file.fur> <ay_ch 0..2>

制約:
  - 1 ドライバ = 単一チップ (--chip ay | opm)。 --chip both は reject。
  - ay: 全アセット AY (曲=json2sms_x1 / SFX=json2sms_x1 -s)。
  - opm: 全アセット OPM (曲=upstream/json2sms)、 sfx 行は error (SFX は AY 専用)。
  - 全アセット常駐 (同時ロード)。 連結後の末尾が ram_base ($C000) を超えたら error。

複数 bundle の併用 (例: BGM 単独 bin + SFX パック bin、 BGM をディスクからスワップ):
  本ツールを bundle ごとに別々に実行し (--base / --out-bin / --out-inc を分け)、
  --const-prefix で生成 CONST 名を名前空間化すれば、 各 .inc を 1 プログラムで衝突なく
  #INCLUDE できる (例: SFX パックは --const-prefix SFX_、 BGM スロットは --const-prefix BGM_)。
  各 --base が重ならないよう配置すること (アドレスは呼び出し側で決める)。

Usage 例:
  banjo_pack_assets.py --manifest assets/banjo_assets.txt \\
    --driver-sym driver.sym --driver-bin driver.bin --chip ay \\
    --base 0x8DE6 --ram-base 0xC000 --ay-master-clock 4000000 \\
    --tools-dir ../../tools --upstream-dir ../../runtime/x1/banjo/upstream \\
    --workdir . --out-bin banjodat.bin --out-inc BANJOMULTI.assets.addr.inc
"""

import argparse
import json as jsonlib
import os
import re
import subprocess
import sys

# Furnace の sound_chips ID (furnace2json 出力)。 exact singleton で判定する。
CHIP_ID_AY = 0x80
CHIP_ID_OPM = 0x82


def die(msg):
    print(f"banjo_pack_assets: ERROR {msg}", file=sys.stderr)
    sys.exit(1)


def run(cmd):
    """サブプロセス実行。 失敗時は子プロセスの stderr を見せて停止 (= 既存ツールの
    error メッセージ。 chip 不一致 / $C000 跨ぎ等はここで伝播する)。"""
    r = subprocess.run([str(c) for c in cmd])
    if r.returncode != 0:
        die(f"command failed ({r.returncode}): {' '.join(str(c) for c in cmd)}")


def parse_addr(s):
    s = str(s).strip().lower()
    if s.startswith("0x"):
        return int(s, 16)
    if s.startswith("$"):
        return int(s[1:], 16)
    return int(s, 10)


def sanitize(stem):
    """basename(拡張子なし) を SLANG CONST 識別子に変換 (SLFS ToIdentifier 流):
    大文字化 + 英数字以外を '_' に。 先頭が数字なら '_' を付けて識別子化。"""
    ident = re.sub(r"[^A-Z0-9]", "_", stem.upper())
    if ident and ident[0].isdigit():
        ident = "_" + ident
    return ident


def parse_manifest(path):
    """manifest を (kind, fur_path, ch) のリストに。 fur は manifest dir 基準で解決。"""
    base_dir = os.path.dirname(os.path.abspath(path))
    entries = []
    with open(path, encoding="utf-8") as f:
        for lineno, raw in enumerate(f, 1):
            line = raw.split("#", 1)[0].strip()
            if not line:
                continue
            tok = line.split()
            kind = tok[0].lower()
            if kind == "music":
                if len(tok) != 2:
                    die(f"manifest:{lineno}: 'music <file.fur>' 形式で書くこと: {raw.rstrip()}")
                entries.append(("music", os.path.join(base_dir, tok[1]), None))
            elif kind == "sfx":
                if len(tok) != 3:
                    die(f"manifest:{lineno}: 'sfx <file.fur> <ch>' 形式で書くこと: {raw.rstrip()}")
                try:
                    ch = int(tok[2])
                except ValueError:
                    die(f"manifest:{lineno}: SFX の ch は整数 (0..2): {raw.rstrip()}")
                if not 0 <= ch <= 2:
                    die(f"manifest:{lineno}: AY SFX の ch は 0..2: {ch}")
                entries.append(("sfx", os.path.join(base_dir, tok[1]), ch))
            else:
                die(f"manifest:{lineno}: 未知の指定子 '{tok[0]}' (music | sfx)")
    if not entries:
        die(f"manifest にアセットがありません: {path}")
    return entries


def detect_chip(json_path):
    """furnace2json の sound_chips を exact singleton で判定。 ay/opm 以外は None。"""
    with open(json_path, encoding="utf-8") as f:
        d = jsonlib.load(f)
    sc = d.get("sound_chips", [])
    if sc == [CHIP_ID_AY]:
        return "ay"
    if sc == [CHIP_ID_OPM]:
        return "opm"
    return None


def rom_last_nonzero_offset(rom_path):
    """wlalink 出力 (.rom = bank 全体、 未使用部ゼロ) の最終非ゼロ offset。 全ゼロなら -1。"""
    rom = open(rom_path, "rb").read()
    i = len(rom) - 1
    while i >= 0 and rom[i] == 0:
        i -= 1
    return i


def main():
    ap = argparse.ArgumentParser(description="banjo 複数楽曲/SFX を bundle 化してアドレス CONST を生成")
    ap.add_argument("--manifest", required=True)
    ap.add_argument("--driver-sym", required=True)
    ap.add_argument("--driver-bin", required=True)
    ap.add_argument("--chip", required=True, choices=["ay", "opm", "both"])
    ap.add_argument("--base", required=True, help="先頭アセットのアドレス (= driver 末尾)")
    ap.add_argument("--ram-base", default="0xC000")
    ap.add_argument("--ay-master-clock", default="4000000")
    ap.add_argument("--tools-dir", required=True)
    ap.add_argument("--upstream-dir", required=True)
    ap.add_argument("--workdir", default=".")
    ap.add_argument("--out-bin", required=True)
    ap.add_argument("--out-inc", required=True)
    ap.add_argument("--const-prefix", default="",
                    help="生成 CONST 名の接頭辞。 複数 bundle を 1 プログラムで併用する時 "
                         "(例 BGM 単独 + SFX パック、 BGM スワップ) に bundle ごと別 prefix を付けて "
                         "メタ定数 (BANJODAT_BASE 等) の衝突を避ける。 既定は空 (= 接頭辞なし)")
    a = ap.parse_args()

    if a.chip == "both":
        die("--chip both は本機構では非対応 (1 bundle = 単一チップ)。 ay か opm を指定")
    if a.const_prefix and not re.match(r"^[A-Za-z_][A-Za-z0-9_]*$", a.const_prefix):
        die(f"--const-prefix は識別子として有効な文字のみ (英数字/_, 先頭は英字/_): {a.const_prefix!r}")

    base = parse_addr(a.base)
    ram_base = parse_addr(a.ram_base)
    wd = a.workdir
    py = sys.executable
    tools = a.tools_dir
    json2sms_x1 = os.path.join(tools, "json2sms_x1.py")
    json2sms_opm = os.path.join(a.upstream_dir, "json2sms.py")
    furnace2json = os.path.join(a.upstream_dir, "furnace2json.py")
    extract_syms = os.path.join(tools, "banjo_extract_syms.py")
    prep_song = os.path.join(tools, "banjo_prep_song.py")
    trim = os.path.join(tools, "banjo_trim_to_end.py")
    wla = os.environ.get("WLA", "wla-z80")
    wlalink = os.environ.get("WLALINK", "wlalink")

    entries = parse_manifest(a.manifest)

    drv_size = os.path.getsize(a.driver_bin)
    addr = base
    seen = {}            # ident -> fur (衝突検出)
    records = []         # (kind, ident, addr, size, ordinal)
    blob_paths = []
    music_n = sfx_n = 0

    for i, (kind, fur, ch) in enumerate(entries):
        if not os.path.isfile(fur):
            die(f"アセットが見つかりません: {fur}")
        stem_name = os.path.splitext(os.path.basename(fur))[0]
        ident = ("SFX_" if kind == "sfx" else "MUSIC_") + sanitize(stem_name)
        if ident in seen:
            die(f"CONST 名衝突 '{ident}': {seen[ident]} と {fur} が同名に解決")
        seen[ident] = fur

        stem = f"asset{i}_{sanitize(stem_name).lower()}"
        jp = os.path.join(wd, stem + ".json")
        asm = os.path.join(wd, stem + ".asm")
        syms = os.path.join(wd, stem + ".syms.inc")
        ready = os.path.join(wd, stem + ".ready.asm")
        obj = os.path.join(wd, stem + ".o")
        rom = os.path.join(wd, stem + ".rom")
        symf = os.path.join(wd, stem + ".sym")
        blob = os.path.join(wd, stem + ".bin")
        link = os.path.join(wd, stem + ".link.txt")

        # 1) .fur -> json、 chip 判定 (exact singleton) と --chip 照合
        run([py, furnace2json, "-o", jp, fur])
        ac = detect_chip(jp)
        if ac is None:
            die(f"{fur}: chip が単一の AY/OPM ではない (AY+OPM 混在曲等は非対応)")
        if ac != a.chip:
            die(f"{fur}: chip={ac} だが driver は --chip {a.chip} (不一致)")
        if kind == "sfx" and a.chip != "ay":
            die(f"{fur}: SFX は AY 専用。 driver chip={a.chip} では SFX を載せられない")

        # 2) json -> asm (ay: json2sms_x1 / sfx: -s / opm: upstream json2sms)
        if kind == "sfx":
            run([py, json2sms_x1, "--ay-master-clock", a.ay_master_clock,
                 "-s", str(ch), "-o", asm, "-i", stem, jp])
        elif a.chip == "opm":
            run([py, json2sms_opm, "-o", asm, "-i", stem, jp])
        else:
            run([py, json2sms_x1, "--ay-master-clock", a.ay_master_clock,
                 "-o", asm, "-i", stem, jp])

        # 3) driver.sym から EQU 抽出 (アセット別 = chip 不一致 backstop。 参照不在で error)
        run([py, extract_syms, a.driver_sym, asm, "-o", syms])

        # 4) load-addr 焼込 -> assemble -> link
        run([py, prep_song, asm, "--load-addr", hex(addr), "--syms-include", syms, "-o", ready])
        run([wla, "-o", obj, ready])
        with open(link, "w") as f:
            f.write("[objects]\n" + obj + "\n")
        run([wlalink, "-s", "-S", "-r", link, rom])

        # 5) per-blob overflow 独自検査 (trim の error にも委ねるが、 pack 側で明確なメッセージ)
        off = rom_last_nonzero_offset(rom)
        if off >= 0 and addr + off >= ram_base:
            die(f"{fur}: blob が RAM 域 (${ram_base:04X}) を跨ぐ "
                f"(load=${addr:04X}、 末尾=${addr + off:04X})。 アセットを減らすこと")

        # 6) trim (修正後は $C000 跨ぎを error)
        run([py, trim, rom, symf, blob, "--load", hex(addr), "--ram-base", hex(ram_base)])

        size = os.path.getsize(blob)
        ordinal = sfx_n if kind == "sfx" else music_n
        records.append((kind, ident, addr, size, ordinal))
        blob_paths.append(blob)
        if kind == "sfx":
            sfx_n += 1
        else:
            music_n += 1
        addr += size

    bundle_end = addr
    if bundle_end > ram_base:
        die(f"bundle が RAM 域を超過: base=${base:04X} 末尾=${bundle_end:04X} > ram_base=${ram_base:04X}。 "
            f"アセットを減らすか配置を見直すこと (全常駐モデルの上限)")

    # 連結 (各 blob は連結後オフセット=ロードアドレスで焼込済 -> 単純 cat で OK、 アライン不要)
    with open(a.out_bin, "wb") as o:
        for b in blob_paths:
            o.write(open(b, "rb").read())

    # アドレス CONST (.inc) 生成 (1 行 1 CONST = shell 抽出を堅牢化)。
    # P (--const-prefix) を全 CONST 名に前置する (既定 "" = 従来どおり)。 複数 bundle を
    # 併用する時は bundle ごとに別 P を渡せばメタ定数も含め衝突しない。
    has_sfx = 1 if sfx_n > 0 else 0
    P = a.const_prefix
    lines = [
        "// banjo asset address map (banjo_pack_assets.py 自動生成、 編集禁止)",
        f"// driver chip = {a.chip}  base = ${base:04X}  bundle = {bundle_end - base} byte  "
        f"end = ${bundle_end:04X}" + (f"  const-prefix = {P}" if P else ""),
        f"CONST {P}BANJODAT_BASE = ${base:04X};",
        f"CONST {P}BANJODAT_SIZE = {bundle_end - base};",
        f"CONST {P}DRV_SIZE = {drv_size};",
        f"CONST {P}BANJO_BUNDLE_HAS_SFX = {has_sfx};",
        f"CONST {P}BANJO_MUSIC_COUNT = {music_n};",
        f"CONST {P}BANJO_SFX_COUNT = {sfx_n};",
    ]
    for kind, ident, ad, size, ordinal in records:
        alias = f"BANJO_{'SFX' if kind == 'sfx' else 'MUSIC'}_{ordinal}"
        lines.append(f"CONST {P}{ident} = ${ad:04X};")
        lines.append(f"CONST {P}{ident}_SIZE = {size};")
        lines.append(f"CONST {P}{alias} = ${ad:04X};")
    with open(a.out_inc, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")

    print(f"banjo_pack_assets: {a.out_bin} = ${base:04X}..${bundle_end:04X} "
          f"({bundle_end - base} byte, music={music_n} sfx={sfx_n}) -> {a.out_inc}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
