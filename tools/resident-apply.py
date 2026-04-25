#!/usr/bin/env python3
"""
runtime/*.asm に `; @resident shared|local` を一括付与するスクリプト。

PR-C1 の作業を機械化するためのツール。resident-audit.py の判定結果に
**手動 override** を組み合わせ、各関数の `; @name X` 行直後に
`; @resident <decision>` を挿入する。

判定ロジック:
  1. ファイル単位の override マップで関数名を引き、ヒットすれば override
  2. ヒットしなければ resident-audit.py の機械判定:
     - self-mod 疑いあり → local
     - それ以外            → shared
  3. 既に `; @resident` が書かれている関数は **スキップ** (idempotent)

override マップは {ファイル名 → {関数名: "shared"|"local"}} の dict で、
このスクリプト先頭に直書き。各関数を local とした理由はコメントで併記。

Usage:
  python3 tools/resident-apply.py --env lsx --env x1   # lsx∪x1 を一括付与
  python3 tools/resident-apply.py --env lsx --dry-run  # 変更内容のプレビュー
  python3 tools/resident-apply.py runtime/libcompress.asm  # 単ファイル

ロールバック: `git diff` / `git checkout -- runtime/<file>.asm` で戻せる。
"""

import argparse
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from importlib import import_module
audit = import_module("resident-audit")


# ---- 関数別 override マップ ----
# 各エントリの理由は plan + 目視確認に基づく。"shared"=機械判定 false positive、
# "local"=真の self-mod / 特殊事情あり。明記されていない関数は機械判定に従う。
OVERRIDES = {
    "runtime.asm": {
        "SLANGINIT": "local",   # main inline only (RuntimePlanner で MainInlineFunctions に入る)
        "SRAND":     "shared",  # work 変数 (seed) への書き込みのみ、self-mod ではない
        "VTOS":      "shared",  # local label への即値書き込みではなく単なる data 領域参照
        "GETREG":    "shared",  # 同上
    },
    "libfloat.asm": {
        "f24toa":    "shared",  # local label のサイズ書き換え (data, not code)
        "formatstr": "shared",  # 同上
    },
    "liblsx_base.asm": {
        # SLANGINIT は inline 専用 → local
        "SLANGINIT":      "local",
        # 以下は self-mod ではなく work 変数 / sXXX バッファへの書き込み
        "sLOC":           "shared",
        "sGETL":          "shared",
        "sFGETL":         "shared",
        "sINKBF":         "shared",
        "GETKY_DOINIT":   "shared",
        "sKYBFC":         "shared",
        "sPRINT":         "shared",
        "sPCLR":          "shared",
    },
    "liblsx_input.asm": {
        "GETLIN": "shared",  # work 変数 sKBFAD への書き込み
        "INPUT":  "shared",
    },
    "liblsx_print.asm": {
        "VTOS":   "shared",  # work 変数経由
    },
    "liblsx_file.asm": {
        # CP/M FCB 操作。FCB は work 変数領域 → shared 可
        "FOPEN":  "shared",
        "FSEEK":  "shared",
        "FGETC":  "shared",
        "FPUTC":  "shared",
        "FREAD":  "shared",
        "FWRITE": "shared",
    },
    "libx1_base.asm": {
        "VSYNC_CHECK": "shared",  # I/O port 直叩き、work 変数経由
        "VSYNC":       "shared",
    },
    "libx1_print.asm": {
        # WIDTH / LOCATE / PRT 等は work 変数 (_WIDTH, _XCUR 等) への書き込み
        "WIDTH":  "shared",
        "CTRL0B": "shared",
        "LOCATE": "shared",
        "PRT":    "shared",
        "VTOS":   "shared",
    },
    "libx1_grp.asm": {
        # マウス / グラフィック処理 — local label の書き換えではなく WORK バッファ参照
        "MSINIT":         "shared",
        "MSGET":          "shared",
        "PAINT1":         "shared",
        "SET_PAINTBUF":   "shared",
        "BFILL":          "shared",
        "LINECOMMON":     "shared",
        "LINE":           "shared",
        "XLINE":          "shared",
    },
    "libx1_pcg.asm": {
        "PCGDEF": "shared",
    },
    "libx1_psg.asm": {
        # PSG 状態は WORK 領域 (PSG_BASE で確保) → shared
        "PSG_BASE":   "shared",
        "PSG_INIT":   "shared",
        "PSG_PLAY":   "shared",
        "PSG_SFX":    "shared",
        "PSG_STOP":   "shared",
        "PSG_PAUSE":  "shared",
        "PSG_RESUME": "shared",
    },
    "libx1_magic.asm": {
        "MAGICBASE": "shared",
    },
    "libx1_sgl_lsx.asm": {
        "X1SGLINCLUDE":    "shared",
        "SGL_SPRDESTROY":  "shared",
        "SGL_SPRDISP":     "shared",
        "SGL_FPSMODE":     "shared",
    },
    "libsoroban.asm": {
        "SOROBAN": "shared",
    },
    "libmag.asm": {
        "GRDISP":  "shared",
        "MAGLOAD": "shared",
    },
    "libm8a.asm": {
        # M8ALOAD は実コード自己書き換えあり (ファイル先頭の disasm で要確認)
        # PR-C1 はまず保守的に local。後で再評価。
        "M8ALOAD": "local",
    },

    # ---- PR-C2: 横展開 env ----
    # MSX 系
    "libmsxlsx_base.asm": {
        "SLANGINIT": "local",  # main inline only
        # 残りは liblsx_base と同パターン (work 変数書き込み = false positive)
        "sLOC":      "shared",
        "sGETL":     "shared",
        "sFGETL":    "shared",
        "sINKBF":    "shared",
        "sKYBFC":    "shared",
        "sPRINT":    "shared",
        "sPCLR":     "shared",
    },
    "libmsx_grp.asm": {
        "MSX_SET_COLOR": "shared",  # BAKCLR/BDRCLR/FORCLR/VDP_ATTR は MSX system var (= work)
    },
    "libmsx_iot.asm": {
        "IOTGET_STR": "shared",  # HL register false-positive
    },
    "libmsx_psg.asm": {
        # SOUNDDRV_STATE は work module var, IX は register, H_TIMI は MSX BIOS hook
        "PSG_INIT":   "shared",
        "PSG_PLAY":   "shared",
        "PSG_SFX":    "shared",
        "PSG_STOP":   "shared",
        "PSG_PAUSE":  "shared",
        "PSG_RESUME": "shared",
        "PSG_PROC":   "shared",
    },
    "libmsx_spdrv.asm": {
        # MSXSPDRV.sprite_* は module-prefixed work
        "SPDRV_INITIALIZE":  "shared",
        "SPDRV_FLIP":        "shared",
        "SPDRV_UPDATE":      "shared",
        "SPDRV2_INITIALIZE": "shared",
        "SPDRV2_FLIP":       "shared",
        "SPDRV2_UPDATE":     "shared",
    },
    "libmsx2_file.asm": {
        "FOPEN": "shared",  # HL register / LSXFCB,LSXFMODE work
    },
    "libmsxrom_base.asm": {
        "SLANGINIT": "local",  # main inline only
    },
    "libmsxrom_print.asm": {
        "WIDTH": "shared",  # LINL40 は MSX system var (= work)
        "VTOS":  "shared",  # HL register false-positive
    },

    # SOS 系
    "libsos_base.asm": {
        "SLANGINIT": "local",
    },
    "libsosx1_base.asm": {
        "SLANGINIT": "local",
    },
    "libsos_input.asm": {
        "GETLIN": "shared",
        "INPUT":  "shared",
    },
    "libsos_print.asm": {
        "WIDTH":  "shared",  # AT_WIDTH は work
        "PRMODE": "local",   # PRT+1 を patch する真の self-mod
    },
    "libsos_file.asm": {
        # FILEWORKS data carrier (SPACEOS/DIREND/sDSK) を共有
        "FOPEN":    "shared",
        "FPG":      "shared",
        "FCLOSE":   "shared",
        "FILEUTIL": "shared",
    },
    "libsos_pcg.asm": {
        "PCGDEF": "shared",  # PCG_NODISPADR は関数内 inline data
    },

    # PC-8001/8801 系
    "libpc80mk2_base.asm": {
        "SLANGINIT":   "local",
        "MEMMODE":     "shared",  # HL register
        "CMDSCREEN":   "shared",  # N80WORK.PORT31 work
        "KANJILOCATE": "shared",  # KanjiX/KanjiY work
        "KANJIPUT":    "shared",  # KanjiVRAM/KanjiX/KanjiY work + hl register
    },
    "libpc80mk2_print.asm": {
        "CTRL0D": "shared",  # _TXADR work
        "VTOS":   "shared",  # HL register
        "SETATR": "shared",  # hl register
    },
    "libpc80mk2_sound.asm": {
        "SND_SYNC": "shared",  # SND.BLANKFLG work
    },
    "libpc80mk2xbios_base.asm": {
        "SLANGINIT":   "local",
        "MEMMODE":     "shared",  # HL register
        "CMDSCREEN":   "shared",  # XBIOS.PORT31 work
        "SD_UTIL":     "shared",  # XBIOS.PORT31 work
        "KANJILOCATE": "shared",
        "KANJIPUT":    "shared",
        "SET_PORT31":  "shared",  # XBIOS.PORT31 work
    },
    "libpc80mk2xbios_input.asm": {
        "GETLIN": "shared",
        "INPUT":  "shared",
    },
    "libpc80mk2xbios_print.asm": {
        "WIDTH":  "shared",  # AT_WIDTH work
        "PRMODE": "local",   # PRT+1 を patch する真の self-mod
    },
    "libp88_base.asm": {
        "SLANGINIT": "local",
        "PSET":      "local",   # PSETADR/PSETCOLOR の operand を patch する真の self-mod
        "SETGRP":    "shared",  # IO32H は @works
        "P88INT":    "shared",  # IO32H は @works
    },
    "libp88_print.asm": {
        # P88PCOMMON data carrier 経由で LOCX/LOCY を共有
        "LOCATE": "shared",
        "PRT":    "shared",
    },

    # x1 SGL (sosx1 用、libx1_sgl_lsx と同パターン)
    "libx1_sgl.asm": {
        "X1SGLINCLUDE":   "shared",
        "SGL_SPRDESTROY": "shared",
        "SGL_SPRDISP":    "shared",
        "SGL_FPSMODE":    "shared",
    },

    # VGS-Zero
    "libvgs0_base.asm": {
        "SLANGINIT":       "local",
        "vgs0_oam_set16":  "shared",  # HL register false-positive
        "vgs0_oam_set":    "shared",
    },
    "libvgs0_print.asm": {
        # LOCX/LOCY/TXTATR/TXTPLANE は work 相当のシステム var
        "LOCATE":    "shared",
        "PRT":       "shared",
        "COLOR":     "shared",
        "TEXTPLANE": "shared",
    },

    # ZX Spectrum Next
    "libzxn_base.asm": {
        "SLANGINIT":      "local",
        # 以下は ULA_CTRL/EULA_CTRL/L2_ACCESS/TILE_CTRL/SPL_SYS の HW register
        # I/O port 風アクセスへの書き込みのみ (= 真の self-mod ではない)
        "ULA_VISIBLE":    "shared",
        "SET_PAL":        "shared",
        "SET_PALALL":     "shared",
        "SET_PAL9":       "shared",
        "SET_PAL9ALL":    "shared",
        "L2_VISIBLE":     "shared",
        "TILE_INIT":      "shared",
        "TILE_VISIBLE":   "shared",
        "LAYER_PRIORITY": "shared",
        "SPR_VISIBLE":    "shared",
    },
    "libzxn_print.asm": {
        "LOCATE": "shared",
        "PRT":    "shared",
        "COLOR":  "shared",
    },
}


# ; @name X 行と既存 @resident 行の検出
NAME_RE     = re.compile(r"^\s*;\s*@name\s+(\S+)\s*$")
RESIDENT_RE = re.compile(r"^\s*;\s*@resident\s+(\S+)\s*$")


def decide(filename, fname, has_self_mod):
    """ファイル名 + 関数名 + 機械判定 → "shared" or "local"。"""
    overrides = OVERRIDES.get(filename, {})
    if fname in overrides:
        return overrides[fname]
    return "local" if has_self_mod else "shared"


def apply_to_file(path, dry_run=False):
    """1 ファイル処理。書き換え行数を返す。"""
    filename = os.path.basename(path)

    # 機械判定 (= self_mod_targets が空かどうか) を audit から取得
    funcs = audit.parse_asm(path)
    self_mod = {f.name: bool(f.self_mod_targets) for f in funcs}

    with open(path, "r", encoding="utf-8") as f:
        lines = f.readlines()

    out = []
    inserts = 0
    skips_existing = 0
    i = 0
    while i < len(lines):
        out.append(lines[i])
        m = NAME_RE.match(lines[i])
        if m:
            fname = m.group(1)
            # 次の数行に既に @resident があればスキップ
            j = i + 1
            already = False
            while j < len(lines) and lines[j].lstrip().startswith(";"):
                if RESIDENT_RE.match(lines[j]):
                    already = True
                    break
                j += 1
            if already:
                skips_existing += 1
            else:
                decision = decide(filename, fname, self_mod.get(fname, False))
                # @name と同じインデント / コメント形式を維持
                indent = lines[i][:len(lines[i]) - len(lines[i].lstrip())]
                out.append(f"{indent}; @resident {decision}\n")
                inserts += 1
        i += 1

    if dry_run:
        if inserts > 0:
            print(f"  [dry] {path}: would insert {inserts} (skip {skips_existing} existing)")
        return inserts

    if inserts > 0:
        with open(path, "w", encoding="utf-8") as f:
            f.writelines(out)
        print(f"  {path}: inserted {inserts} (skip {skips_existing} existing)")
    else:
        print(f"  {path}: no changes (all {skips_existing} already annotated)")
    return inserts


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("files", nargs="*", help="対象 asm (省略時は --env から導出)")
    ap.add_argument("--env", action="append", default=None,
                    help="env 名指定で対象 .asm を逆引き (複数指定可)")
    ap.add_argument("--dry-run", action="store_true",
                    help="変更内容のプレビューのみ (ファイル書き換えなし)")
    args = ap.parse_args()

    if args.env:
        seen = set()
        files = []
        for env in args.env:
            for path in audit.resolve_env_asm(env):
                if path not in seen:
                    seen.add(path)
                    files.append(path)
    elif args.files:
        files = args.files
    else:
        ap.error("--env か files のどちらかを指定してください")

    total = 0
    for path in files:
        if not os.path.exists(path):
            print(f"  skip (not found): {path}")
            continue
        total += apply_to_file(path, dry_run=args.dry_run)

    label = "would insert" if args.dry_run else "inserted"
    print(f"\nTotal: {label} {total} @resident lines across {len(files)} files")


if __name__ == "__main__":
    main()
