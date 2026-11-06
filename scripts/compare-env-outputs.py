#!/usr/bin/env python3
"""いまの環境 (x1native・x1native_slfs) の出力が、基準のコミットから変わっていないことを確かめる。

    python3 scripts/compare-env-outputs.py --out <作業フォルダ> [--base main] [--bas-dir <dir>]... [--jobs N]

- 旧 = 基準のコミット (既定 main) を git worktree で取り出したもの、新 = いまの作業木 (コミットしていない変更も含む)。
- それぞれの slangbuild・hubasic2sl・slangc を <作業フォルダ>/<old|new>/bin に組み立て、それぞれの木の根を作業フォルダにして動かす
  (slangc は作業フォルダの runtime/ と env/ を先に探すので、旧と新のランタイムが混ざらない)。
- 入力:
  - examples/ の *.SL: x1native で --emit bin・--emit tape、x1native_slfs で --emit bin・--emit disk・
    --emit disk --disk-main-name、assets/ のある例は --emit disk --slfs-add assets/ も
  - examples/hubasic/*.BAS と --bas-dir の *.bas・*.BAS: 変換だけ・--emit disk・--emit tape・--wait line:480 --emit-line-comments
- 合格:
  - 旧で成功したものは、新でも成功して、出力 (.SL・.bin・.d88・.tap) がバイト単位で同じ
  - 旧で失敗したものは、新でも同じ失敗 (終了コードと、パスを除いたメッセージが同じ)。
    ただし期待の表 (scripts/compare-env-outputs.expect) に載せたものは、その表のとおりに変わってよい
- 終了コード: 一致なら 0、違いがあれば 1。違いの一覧と数を出す。
"""
import argparse
import concurrent.futures
import filecmp
import os
import re
import shutil
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_EXTS = (".SL", ".bin", ".d88", ".tap")


def run(cmd, cwd, timeout=300, env=None):
    try:
        p = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, timeout=timeout, env=env)
        return p.returncode, p.stdout + p.stderr
    except subprocess.TimeoutExpired:
        return -999, "timeout"


def build_tools(tree, bindir):
    os.makedirs(bindir, exist_ok=True)
    for proj in ("src/SLANGCompiler.CLI", "src/SLANGCompiler.Build", "src/SLANGCompiler.HuBasic"):
        rc, log = run(["dotnet", "build", proj, "-c", "Release", "-o", bindir, "-v", "q", "-nologo"], tree, timeout=900)
        if rc != 0:
            sys.exit(f"組み立てに失敗しました: {tree} {proj}\n{log}")


def load_expect(path):
    """期待の表: 1 行に「種類<TAB>入力<TAB>やり方<TAB>期待」。期待は success か error <メッセージの一部>。# から後ろは注釈"""
    table = {}
    if path and os.path.exists(path):
        for line in open(path, encoding="utf-8"):
            line = line.split("#", 1)[0].rstrip("\n")
            if not line.strip():
                continue
            kind, inp, mode, expect = line.split("\t")
            table[(kind, inp, mode)] = expect
    return table


def examples_jobs(tree):
    jobs = []
    for dirpath, _, files in os.walk(os.path.join(tree, "examples")):
        for f in sorted(files):
            if not f.upper().endswith(".SL"):
                continue
            rel = os.path.relpath(os.path.join(dirpath, f), tree)
            assets = os.path.join(dirpath, "assets")
            modes = [
                ("x1native-bin", ["-E", "x1native", "--emit", "bin"]),
                ("x1native-tape", ["-E", "x1native", "--emit", "tape"]),
                ("x1native_slfs-bin", ["-E", "x1native_slfs", "--emit", "bin"]),
                ("x1native_slfs-disk", ["-E", "x1native_slfs", "--emit", "disk"]),
                ("x1native_slfs-disk-name", ["-E", "x1native_slfs", "--emit", "disk", "--disk-main-name", "CMP TEST"]),
            ]
            if os.path.isdir(assets):
                modes.append(("x1native_slfs-disk-assets",
                              ["-E", "x1native_slfs", "--emit", "disk", "--slfs-add", os.path.relpath(assets, tree)]))
            for mode, args in modes:
                jobs.append(("sl", rel, mode, args))
    return jobs


def bas_jobs(tree, bas_dirs):
    files = []
    for d in [os.path.join(tree, "examples", "hubasic")] + bas_dirs:
        for f in sorted(os.listdir(d)):
            if f.lower().endswith(".bas"):
                files.append(os.path.join(d, f))
    jobs = []
    for path in files:
        key = os.path.relpath(path, tree) if path.startswith(tree) else path
        for mode, args in (("convert", []), ("disk", ["--emit", "disk"]), ("tape", ["--emit", "tape"]),
                           ("wait", ["--wait", "line:480", "--emit-line-comments"])):
            jobs.append(("bas", key, mode, args))
    return jobs


def do_job(side, tree, bindir, outroot, job):
    kind, inp, mode, args = job
    tag = re.sub(r"[^A-Za-z0-9_.-]", "_", inp) + "__" + mode
    odir = os.path.join(outroot, side, "out", kind, tag)
    shutil.rmtree(odir, ignore_errors=True)
    os.makedirs(odir)
    src = inp if os.path.isabs(inp) else os.path.join(tree, inp)
    stem = os.path.join(odir, "P")
    if kind == "sl":
        cmd = [os.path.join(bindir, "slangbuild"), "--slangc", os.path.join(bindir, "slangc"),
               "--asm", os.path.join(tree, "tools", "AILZ80ASM"), "-I", os.path.join(tree, "include"),
               src, "-o", stem] + args
    else:
        cmd = [os.path.join(bindir, "hubasic2sl"), src, "-o", stem + ".SL"] + args
        if "--emit" in args:
            cmd += ["--slangbuild", os.path.join(bindir, "slangbuild")]
    # hubasic2sl が呼ぶ slangbuild にもアセンブラが見つかるように (その木の tools/ のもの)
    env = dict(os.environ, AILZ80ASM_PATH=os.path.join(tree, "tools", "AILZ80ASM"))
    rc, log = run(cmd, tree, env=env)
    for t, rep in ((odir, "<OUT>"), (tree, "<TREE>"), (bindir, "<BIN>")):
        log = log.replace(t, rep)
    outs = {f for f in os.listdir(odir) if f.endswith(OUT_EXTS)}
    return rc, log, odir, outs


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--base", default="main")
    ap.add_argument("--bas-dir", action="append", default=[])
    ap.add_argument("--jobs", type=int, default=os.cpu_count() or 4)
    ap.add_argument("--expect", default=os.path.join(REPO, "scripts", "compare-env-outputs.expect"))
    ap.add_argument("--skip-build", action="store_true", help="前の実行で組み立てた道具をそのまま使う")
    a = ap.parse_args()

    out = os.path.abspath(a.out)
    os.makedirs(out, exist_ok=True)
    old_tree = os.path.join(out, "old-tree")
    base_sha = subprocess.run(["git", "rev-parse", a.base], cwd=REPO, capture_output=True, text=True, check=True).stdout.strip()
    if not a.skip_build:
        if os.path.exists(old_tree):
            subprocess.run(["git", "worktree", "remove", "--force", old_tree], cwd=REPO)
        subprocess.run(["git", "worktree", "add", "--detach", old_tree, base_sha], cwd=REPO, check=True,
                       capture_output=True)
        # 道具 (AILZ80ASM など) は追跡していないものがあるので、新の木から写す
        if not os.path.exists(os.path.join(old_tree, "tools", "AILZ80ASM")):
            shutil.copy2(os.path.join(REPO, "tools", "AILZ80ASM"), os.path.join(old_tree, "tools", "AILZ80ASM"))
        build_tools(old_tree, os.path.join(out, "old", "bin"))
        build_tools(REPO, os.path.join(out, "new", "bin"))
    print(f"基準: {a.base} {base_sha}")

    expect = load_expect(a.expect)
    bas_dirs = [os.path.abspath(d) for d in a.bas_dir]
    jobs = examples_jobs(old_tree) + bas_jobs(old_tree, bas_dirs)
    # 新の木にだけある例は比べない (旧に入力がない)。旧にだけあるものは、新で入力がないので違いにする
    results = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=a.jobs) as ex:
        futs = {}
        for job in jobs:
            for side, tree in (("old", old_tree), ("new", REPO)):
                futs[ex.submit(do_job, side, tree, os.path.join(out, side, "bin"), out, job)] = (side, job)
        for f in concurrent.futures.as_completed(futs):
            side, job = futs[f]
            results[(side, job[0], job[1], job[2])] = f.result()

    diffs, n_ok, n_fail_same, n_expected = [], 0, 0, 0
    per_mode = {}
    for kind, inp, mode, _ in jobs:
        ok = results[("old", kind, inp, mode)][0] == 0
        t = per_mode.setdefault((kind, mode), [0, 0])
        t[0] += 1
        t[1] += ok
    for kind, inp, mode, _ in jobs:
        orc, olog, odir, oouts = results[("old", kind, inp, mode)]
        nrc, nlog, ndir, nouts = results[("new", kind, inp, mode)]
        key = (kind, inp, mode)
        if orc == 0:
            if nrc != 0:
                diffs.append(f"{kind} {inp} {mode}: 旧は成功、新は失敗 ({nrc})\n    {nlog.strip()[:300]}")
                continue
            bad = [f for f in sorted(oouts | nouts)
                   if f not in oouts or f not in nouts or not filecmp.cmp(os.path.join(odir, f), os.path.join(ndir, f), shallow=False)]
            if bad:
                diffs.append(f"{kind} {inp} {mode}: 出力が違う {bad}")
            else:
                n_ok += 1
            continue
        if key in expect:
            want = expect[key]
            if (want == "success" and nrc == 0) or (want.startswith("error ") and nrc != 0 and want[6:] in nlog):
                n_expected += 1
            else:
                diffs.append(f"{kind} {inp} {mode}: 期待の表は {want}、新は rc={nrc}\n    {nlog.strip()[:300]}")
            continue
        if nrc != orc or nlog != olog:
            diffs.append(f"{kind} {inp} {mode}: 旧の失敗と新が違う (旧 rc={orc}、新 rc={nrc})\n"
                         f"    旧: {olog.strip()[:200]}\n    新: {nlog.strip()[:200]}")
        else:
            n_fail_same += 1

    print(f"入力とやり方の組: {len(jobs)}")
    print(f"  成功して出力が同じ: {n_ok}")
    print(f"  旧も新も同じ失敗: {n_fail_same}")
    print(f"  期待の表のとおりに変わった: {n_expected}")
    print(f"  違い: {len(diffs)}")
    print("やり方ごとの、旧で成功した数 / 全部 (成功が 0 のやり方は、比べられていない):")
    for (kind, mode), (tot, ok) in sorted(per_mode.items()):
        print(f"  {kind} {mode}: {ok} / {tot}")
    for d in diffs:
        print("  - " + d)
    return 1 if diffs else 0


if __name__ == "__main__":
    sys.exit(main())
