#!/usr/bin/env python3
"""コンパイル→アセンブル→バイナリサイズ比較スクリプト"""
import subprocess, os, sys, tempfile

COMPILER = "dotnet run --project src/SLANGCompiler.CLI/SLANGCompiler.CLI.csproj -c Release --"
ASM = "ailz80asm"

# 旧コンパイラのバイナリサイズ
OLD_SIZES = {
    "FURUI": 528,
    "FMANDEL": 1115,
    "MANDEL": 4556,
    "STARS": 1199,
    "SLANGTEST": 6156,
}

SRC_FILES = {
    "FURUI": "examples/FURUI.SL",
    "FMANDEL": "examples/FMANDEL.SL",
    "MANDEL": "examples/MANDEL.SL",
    "STARS": "examples/STARS.SL",
    "SLANGTEST": "SLANGTEST.SL",
}

def compile_and_measure(name, src):
    tmpdir = tempfile.mkdtemp()
    asm_file = os.path.join(tmpdir, f"{name}.asm")
    bin_file = os.path.join(tmpdir, f"{name}.bin")

    # コンパイル
    cmd = f"{COMPILER} -E lsx -o {asm_file} {src}"
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    if r.returncode != 0 or not os.path.exists(asm_file):
        return None, f"compile error: {r.stderr}"

    # アセンブル
    cmd = f"{ASM} {asm_file} -f -o {bin_file} -bin"
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    if r.returncode != 0 or not os.path.exists(bin_file):
        return None, f"assemble error: {r.stderr}"

    size = os.path.getsize(bin_file)
    return size, None

def main():
    print("┌────────────┬───────┬───────┬────────┬────────┐")
    print("│ プログラム │  旧   │  新   │  差分  │  状態  │")
    print("├────────────┼───────┼───────┼────────┼────────┤")

    total_old = total_new = 0
    all_ok = True

    for name in ["FURUI", "FMANDEL", "MANDEL", "STARS", "SLANGTEST"]:
        src = SRC_FILES[name]
        old = OLD_SIZES[name]
        new_size, err = compile_and_measure(name, src)
        if err:
            print(f"│ {name:<10} │ {old:>5} │ ERROR │        │  ERR   │  {err}")
            all_ok = False
            continue

        diff = new_size - old
        total_old += old
        total_new += new_size
        status = "  OK  " if diff <= 0 else "  NG  "
        if diff > 0:
            all_ok = False
        print(f"│ {name:<10} │ {old:>5} │ {new_size:>5} │ {diff:>+6} │ {status} │")

    print("├────────────┼───────┼───────┼────────┼────────┤")
    td = total_new - total_old
    ts = "  OK  " if td <= 0 else "  NG  "
    print(f"│ {'TOTAL':<10} │ {total_old:>5} │ {total_new:>5} │ {td:>+6} │ {ts} │")
    print("└────────────┴───────┴───────┴────────┴────────┘")
    print("ALL OK" if all_ok else "NG: 旧コンパイラより大きいプログラムがあります")
    sys.exit(0 if all_ok else 1)

if __name__ == "__main__":
    main()
