#!/usr/bin/env python3
"""
runtime/*.asm を走査し、各 SLANG runtime 関数について PR-C で
`; @resident shared` を付与できそうか / できなさそうかの判定材料を CSV で出力する。

ヒューリスティック (完全ではない、あくまで初期スクリーニング):
  - @works を持つ関数は「shared 候補」: 共有した方が __WORK__ に 1 回だけ配置されて
    メモリ効率が上がる
  - @init_code を持つ関数も shared 候補 (RUNTIME_INIT で 1 回初期化)
  - 関数サイズが大きい (例: 20 行以上) なら shared 候補
  - 関数サイズが極小 (例: 5 行未満) は shared コスト (CALL オーバーヘッド) より
    inline の方が合理的な場合あり → 要個別判断
  - local label (`.xxx`) だらけで跳び回る関数は self-modifying の心配は減るが
    shared でも問題なし
  - 関数コード内に `LD (&XXXX),...` / `LD (label),...` で自分のコードラベルを
    上書きしているパターンは self-modifying → `@resident local` 候補

Usage:
  python3 tools/resident-audit.py                # 全 runtime/*.asm を走査
  python3 tools/resident-audit.py runtime/liblsx_print.asm  # 特定ファイルのみ
  python3 tools/resident-audit.py --env lsx                  # lsx.env が参照する asm のみ
  python3 tools/resident-audit.py --env lsx --env x1 --by-file
  python3 tools/resident-audit.py --env lsx --list-asm        # 対象 asm のフラット一覧
"""

import argparse
import csv
import os
import re
import sys
from collections import defaultdict

AT_DIRECTIVE = re.compile(r"^\s*;\s*@(\w+)\s*(.*)$")
LABEL_DEF = re.compile(r"^\s*([A-Za-z_][\w\.]*)\s*:\s*(?:;.*)?$")
LOCAL_LABEL = re.compile(r"^\s*(\.[A-Za-z_]\w*)\b")
# self-modifying 疑い: LD (xxx),A / LD (xxx),HL 等で xxx が同関数内の local label か、
# 明確な非 work ラベル (例: WORK10 は @works にあるが、関数コード内で local label +
# 即値 offset を書き換えるパターン)
SELF_MOD_SUSPECT = re.compile(
    r"^\s*LD\s*\(\s*([A-Za-z_][\w\.]*)\s*(?:\+\s*\d+)?\s*\)\s*,\s*(A|HL|DE|BC)\b",
    re.IGNORECASE,
)


class Function:
    def __init__(self, name):
        self.name = name
        self.directives = {}   # dict[name] = value
        self.works = []        # list[(label, size)]
        self.has_init = False
        self.code_lines = 0
        self.local_labels = set()
        self.code_labels = set()   # 関数内 code 内で定義された label (local以外)
        self.self_mod_targets = []  # LD (label), regフォームで書き換えられているラベル
        self.lines_raw = []    # 生テキスト (デバッグ用)


def parse_asm(path):
    funcs = []
    current = None
    in_init = False

    with open(path, "r", encoding="utf-8", errors="replace") as f:
        for line in f:
            raw = line.rstrip("\n")
            m = AT_DIRECTIVE.match(raw)
            if m:
                key, value = m.group(1).lower(), m.group(2).strip()
                if key == "name":
                    if current:
                        funcs.append(current)
                    current = Function(value)
                    in_init = False
                    continue
                if current is None:
                    continue
                current.directives[key] = value
                if key == "works" and value:
                    for item in [x.strip() for x in value.split(",") if x.strip()]:
                        if ":" in item:
                            lbl, sz = item.split(":", 1)
                            try:
                                current.works.append((lbl.strip(), int(sz.strip())))
                            except ValueError:
                                pass
                elif key == "init_code":
                    current.has_init = True
                    in_init = True
                elif key == "end_init":
                    in_init = False
                continue

            if current is None:
                continue

            # init_code 部は別扱い (本体サイズから除外)
            if in_init:
                continue

            # code 行
            code = raw.split(";", 1)[0].rstrip()
            if not code.strip():
                continue
            current.code_lines += 1
            current.lines_raw.append(raw)

            ml = LOCAL_LABEL.match(code)
            if ml:
                current.local_labels.add(ml.group(1))

            ld = LABEL_DEF.match(code)
            if ld:
                lbl = ld.group(1)
                if not lbl.startswith("."):
                    current.code_labels.add(lbl)

            sm = SELF_MOD_SUSPECT.match(code)
            if sm:
                target = sm.group(1)
                # 対象が @works で宣言された「作業変数」ならそれは通常の変数書き込み
                work_labels = {lbl for lbl, _ in current.works}
                if target not in work_labels:
                    current.self_mod_targets.append(target)

    if current:
        funcs.append(current)
    return funcs


def classify(func):
    """
    簡易分類 (改訂版: ユーザー直感「原則 shared、self-mod だけ local」に合わせる):
      - "local_forced": self-mod 疑いあり → shared にすると壊れる恐れ、local 固定
      - "shared_candidate": それ以外 (= 原則 shared にして良い)

    size / @works / @init_code は補助情報として reasons に記録するが、
    分類の分岐条件には使わない。
    """
    reasons = []
    if func.self_mod_targets:
        reasons.append("self-mod?:" + ",".join(sorted(set(func.self_mod_targets))))
        return "local_forced", reasons

    reasons.append(f"size={func.code_lines}")
    if func.works:
        reasons.append(f"@works={len(func.works)}")
    if func.has_init:
        reasons.append("@init_code")
    return "shared_candidate", reasons


def resolve_env_asm(env_name, runtime_dir="runtime"):
    """
    env file (YAML) の libraries: セクションを読み、各エントリを .asm に変換して
    対象 asm ファイルパスのリストを返す。
    EnvironmentLoader.cs の挙動に合わせ、.yml → .asm 読み替えを行う。
    PyYAML 不要 (libraries: の最小パーサ)。
    """
    env_path = os.path.join(runtime_dir, "env", f"{env_name}.env")
    if not os.path.exists(env_path):
        # lib/env/ も探す (CLI と同じ検索順)
        env_path = os.path.join("lib", "env", f"{env_name}.env")
        if not os.path.exists(env_path):
            raise FileNotFoundError(f"env not found: {env_name}.env")

    libs = []
    in_libs = False
    with open(env_path, "r", encoding="utf-8") as f:
        for line in f:
            stripped = line.strip()
            if not stripped or stripped.startswith("#"):
                continue
            if in_libs:
                if stripped.startswith("- "):
                    libs.append(stripped[2:].strip())
                    continue
                # libraries: セクション終了 (= 別 top-level key 開始)
                if not line.startswith((" ", "\t")):
                    in_libs = False
            if stripped.startswith("libraries:"):
                in_libs = True
                continue

    asm_files = []
    for lib in libs:
        asm = lib[:-4] + ".asm" if lib.endswith(".yml") else lib
        asm_files.append(os.path.join(runtime_dir, asm))
    return asm_files


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("files", nargs="*", default=None,
                    help="対象 asm ファイル (省略時は runtime/*.asm 全部)")
    ap.add_argument("--env", action="append", default=None,
                    help="env 名を指定し、その env が参照する asm のみを対象にする (複数指定可)")
    ap.add_argument("--list-asm", action="store_true",
                    help="対象 asm のフラット一覧を改行区切りで出力 (CSV 解析せず)")
    ap.add_argument("--csv", default=None, help="CSV 出力先 (省略時は stdout)")
    ap.add_argument("--by-file", action="store_true", help="ファイル単位サマリーだけ出す")
    args = ap.parse_args()

    if args.env:
        # 複数 env をまとめ重複排除 (順序は最初に出現した env の順)
        seen = set()
        files = []
        for env in args.env:
            for path in resolve_env_asm(env):
                if path not in seen:
                    seen.add(path)
                    files.append(path)
    elif args.files:
        files = args.files
    else:
        runtime_dir = "runtime"
        files = sorted(
            os.path.join(runtime_dir, f) for f in os.listdir(runtime_dir)
            if f.endswith(".asm")
        )

    if args.list_asm:
        for path in files:
            print(path)
        return

    # 関数単位の records
    records = []
    file_stats = defaultdict(lambda: {"shared": 0, "local_forced": 0, "total": 0,
                                        "local_forced_names": []})

    for path in files:
        funcs = parse_asm(path)
        for f in funcs:
            cls, reasons = classify(f)
            records.append({
                "file": os.path.basename(path),
                "name": f.name,
                "class": cls,
                "size": f.code_lines,
                "has_works": len(f.works),
                "has_init": "Y" if f.has_init else "",
                "self_mod": "Y" if f.self_mod_targets else "",
                "reasons": ";".join(reasons),
            })
            fstats = file_stats[os.path.basename(path)]
            fstats["total"] += 1
            if cls == "shared_candidate":
                fstats["shared"] += 1
            else:
                fstats["local_forced"] += 1
                fstats["local_forced_names"].append(f.name)

    out = open(args.csv, "w", newline="") if args.csv else sys.stdout

    if args.by_file:
        w = csv.writer(out)
        w.writerow(["file", "total", "shared_candidate", "local_forced", "verdict", "exceptions"])
        for fname in sorted(file_stats.keys()):
            s = file_stats[fname]
            if s["local_forced"] == 0:
                verdict = "all-shared"
            elif s["shared"] == 0:
                verdict = "all-local"
            else:
                verdict = "mostly-shared"
            w.writerow([fname, s["total"], s["shared"], s["local_forced"],
                        verdict, ";".join(s["local_forced_names"])])
    else:
        w = csv.DictWriter(out, fieldnames=[
            "file", "name", "class", "size", "has_works", "has_init", "self_mod", "reasons"
        ])
        w.writeheader()
        for r in records:
            w.writerow(r)

    if args.csv:
        out.close()
        print(f"written: {args.csv}", file=sys.stderr)


if __name__ == "__main__":
    main()
