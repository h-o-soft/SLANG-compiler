#!/usr/bin/env python3
"""trim_to_end.py - wlalink の bank サイズ出力 (.rom) を実使用範囲に切り詰める。

wlalink -r は ROM bank 全体 ($F000 byte) を出力する。 X1 tape には実使用分だけ
載せたいので、 load アドレス ($1000) から「実使用最大アドレス」までを切り出す。

実使用最大アドレスは 2 つの推定の max を取る (= 互いの取りこぼしを補完):
  (1) ROM 内の最後の非ゼロバイトのアドレス
      → 最終 label 以降に伸びる data (曲パターン等) を拾える
  (2) .sym の全 label アドレスの最大
      → 末尾が $00 で終わる section を label 経由で拾える
※ 単独 PROGRAM_END marker は WLA-DX の .SECTION FREE が任意位置に再配置するため
  当てにならない (実測: 独立 FREE section の end label が SLOT 外 $0040 に落ちた)。
  よって marker 単体には依存しない。

さらに RAM 配置 assert: 実使用最大アドレス < RAM_BASE ($C000 既定) を確認し、
違反 (= code/data が RAM 領域に食い込む) なら error 終了。

Usage:
  python3 trim_to_end.py main.rom main.sym out.bin \\
    [--load 0x1000] [--ram-base 0xC000]
"""

import argparse
import os
import re
import sys


def parse_sym_max_label(sym_path: str, load: int, ram_base: int) -> int:
    """`.sym` の [labels] から **ROM 範囲 [load, ram_base) に入る** label の最大アドレスを返す。
    無ければ 0。 行形式: `<bank>:<addr4hex> <name>`。
    RAM section の label (= song_channels 等、 ram_base 以上) は bin に出ないので除外する
    (= これを含めると ROM trim が RAM アドレスまで伸びて壊れる)。"""
    if not sym_path or not os.path.isfile(sym_path):
        return 0
    mx = 0
    in_labels = False
    with open(sym_path, encoding="utf-8", errors="replace") as f:
        for ln in f:
            s = ln.strip()
            if s.startswith("[") and s.endswith("]"):
                in_labels = (s.lower() == "[labels]")
                continue
            if not in_labels:
                continue
            m = re.match(r"^[0-9A-Fa-f]+:([0-9A-Fa-f]{4})\s+\S", s)
            if m:
                a = int(m.group(1), 16)
                if load <= a < ram_base:        # ROM 範囲のみ
                    mx = max(mx, a)
    return mx


def last_nonzero_addr(rom: bytes, load: int) -> int:
    """ROM の最後の非ゼロ byte のメモリアドレス (= load + index)。 全ゼロなら load-1。"""
    i = len(rom) - 1
    while i >= 0 and rom[i] == 0:
        i -= 1
    return load + i  # i<0 なら load-1 (= 空)


# banjo の work RAM label (= banjo.asm の .RAMSECTION "BANJO_RAM")。 これらが RAM 領域
# ($ram_base 以上) に置けていないと、 code と重なって banjo_play_song の ldir が実行コードを
# 破壊する (= 実際に踏んだ重大バグ)。 linkfile [ramsections] 漏れの早期検出に使う。
_BANJO_RAM_LABELS = (
    "music_framerate", "banjo_has_chips", "banjo_max_channels",
    "song_playing", "song_state", "song_channels",
)


def check_ram_placement(sym_path: str, ram_base: int) -> list:
    """banjo work RAM label が $ram_base 未満 (= ROM/code 域) に居ないか検査。
    違反した (label, addr) を返す (= 空なら OK)。 sym 無し時も空 (= 検査スキップ)。"""
    if not sym_path or not os.path.isfile(sym_path):
        return []
    addr_of = {}
    in_labels = False
    with open(sym_path, encoding="utf-8", errors="replace") as f:
        for ln in f:
            s = ln.strip()
            if s.startswith("[") and s.endswith("]"):
                in_labels = (s.lower() == "[labels]")
                continue
            if not in_labels:
                continue
            m = re.match(r"^[0-9A-Fa-f]+:([0-9A-Fa-f]{4})\s+(\S+)", s)
            if m:
                name = m.group(2)
                if name in _BANJO_RAM_LABELS and name not in addr_of:
                    addr_of[name] = int(m.group(1), 16)
    return [(n, a) for n, a in addr_of.items() if a < ram_base]


def parse_addr(s: str) -> int:
    s = s.strip().lower()
    if s.startswith("0x"):
        return int(s, 16)
    if s.startswith("$"):
        return int(s[1:], 16)
    return int(s, 10)


def main() -> int:
    p = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("rom", help="wlalink 出力 .rom (bank 全体)")
    p.add_argument("sym", help="wlalink 出力 .sym (label 一覧)")
    p.add_argument("out", help="出力 .bin (= load..max を切出し)")
    p.add_argument("--load", default="0x1000", help="load address (default 0x1000)")
    p.add_argument("--ram-base", default="0xC000",
                   help="RAM 領域開始 (この未満に収まらなければ error、 default 0xC000)")
    args = p.parse_args()

    load = parse_addr(args.load)
    ram_base = parse_addr(args.ram_base)

    if not os.path.isfile(args.rom):
        print(f"rom not found: {args.rom}", file=sys.stderr)
        return 1
    rom = open(args.rom, "rb").read()
    if not rom:
        print("empty rom", file=sys.stderr)
        return 1

    # banjo work RAM が RAM 領域に置けているかを先に検査 (= linkfile [ramsections] 漏れ検出)。
    # 違反すると code と RAM が重なり banjo_play_song の ldir が実行コードを破壊する。
    misplaced = check_ram_placement(args.sym, ram_base)
    if misplaced:
        detail = ", ".join(f"{n}=${a:04X}" for n, a in sorted(misplaced))
        print(
            f"trim_to_end: ERROR banjo work RAM が RAM 域 (${ram_base:04X}+) 外に配置: {detail}\n"
            f"  → linkfile.txt の [ramsections] で BANJO_RAM / MAIN_RAM を SLOT 1 に置くこと",
            file=sys.stderr)
        return 1

    # ROM (= bin に出る code/data) の最大使用アドレス。 非ゼロ末尾 と ROM 範囲 label の max。
    # RAM section (ram_base 以上) は bin に出ないので両推定とも ROM 範囲に限定する。
    nz_addr = last_nonzero_addr(rom, load)
    if nz_addr >= ram_base:
        # wlalink の bank 全体出力に RAM 残骸が混じることは無い想定だが、 念のため clamp。
        nz_addr = ram_base - 1
    sym_max = parse_sym_max_label(args.sym, load, ram_base)
    max_addr = max(nz_addr, sym_max)

    if max_addr < load:
        print("no used bytes found in rom", file=sys.stderr)
        return 1

    # RAM 配置 assert: code/data 終端 ($max_addr) が RAM 領域 ($ram_base) に食い込んでいないか。
    # ※ ROM 範囲に限定済なので通常ここは通る。 layout 崩れ (RAM が下に降りた等) の最終防御。
    if max_addr >= ram_base:
        print(
            f"trim_to_end: ERROR max used ROM addr ${max_addr:04X} >= RAM base ${ram_base:04X} "
            f"(code/data が RAM 領域に衝突)。 layout を見直すこと",
            file=sys.stderr)
        return 1

    size = max_addr - load + 1
    if size > len(rom):
        print(f"trim_to_end: ERROR computed size {size} > rom {len(rom)}", file=sys.stderr)
        return 1

    out = rom[:size]
    with open(args.out, "wb") as f:
        f.write(out)
    print(f"trim_to_end: {args.out} = ${load:04X}..${max_addr:04X} ({size} byte) "
          f"[nonzero=${nz_addr:04X} sym_max=${sym_max:04X}]")
    return 0


if __name__ == "__main__":
    sys.exit(main())
