#!/usr/bin/env python3
"""hubasic2sl のブラウザ向けの一式の manifest.json を作る (scripts/build-hubasic-wasm.sh から呼ぶ)。

使い方: python3 -I hubasic-wasm-manifest.py <repo> <sha> <dirty> <version> [<outdir>]
  manifest の JSON を stdout に出す。<outdir> があれば、runtime/ (2 つの env の libraries の和集合の .asm) と
  env/ (env ファイルの写し) をそこに作る。

env ファイルから読むのは env_type・os_type・default_org・defines・libraries の 5 つだけ
(EnvironmentLoader と同じ値になることをテストで確かめる)。どれかが読めないか、.asm が無ければ止める。
"""
import json
import os
import shutil
import sys

ENVS = ["x1native_slfs", "x1native"]


def fail(msg):
    print(f"hubasic-wasm-manifest: {msg}", file=sys.stderr)
    sys.exit(1)


def strip_comment(s):
    """引用符の外の # から後ろを除く"""
    quote = None
    for i, ch in enumerate(s):
        if quote:
            if ch == quote:
                quote = None
        elif ch in "\"'":
            quote = ch
        elif ch == "#":
            return s[:i]
    return s


def scalar(s):
    s = s.strip()
    if len(s) >= 2 and s[0] == s[-1] and s[0] in "\"'":
        s = s[1:-1]
    return s


def number(s, what):
    t = scalar(s)
    try:
        if t.startswith("$"):
            return int(t[1:], 16)
        if t.lower().startswith("0x"):
            return int(t[2:], 16)
        if t.lower().endswith("h"):
            return int(t[:-1], 16)
        return int(t)
    except ValueError:
        fail(f"{what} を数として読めません: {s.strip()}")


def read_env(path):
    """env ファイルの 5 つの項目を読む (この 5 つのための最小の読み方。YAML の全部は読まない)"""
    top = {}
    block = None
    defines = {}
    libraries = []
    with open(path, encoding="utf-8") as f:
        for raw in f:
            line = strip_comment(raw.rstrip("\r\n"))
            if not line.strip():
                continue
            indented = line[0] in " \t"
            if not indented:
                key, sep, value = line.partition(":")
                if not sep:
                    continue
                key = key.strip()
                block = key if not value.strip() else None
                if block is None:
                    top[key] = value
                continue
            body = line.strip()
            if block == "defines":
                name, sep, value = body.partition(":")
                if sep:
                    defines[name.strip()] = number(value, f"{path} の defines の {name.strip()}")
            elif block == "libraries" and body.startswith("-"):
                libraries.append(scalar(body[1:]))
    for key in ("env_type", "os_type", "default_org"):
        if key not in top:
            fail(f"{path} に {key} がありません")
    if not libraries:
        fail(f"{path} に libraries がありません")
    return {
        "envType": number(top["env_type"], f"{path} の env_type"),
        "osType": number(top["os_type"], f"{path} の os_type"),
        "defaultOrg": number(top["default_org"], f"{path} の default_org"),
        "defines": defines,
        # EnvironmentLoader と同じく .yml を .asm にする
        "libraries": [os.path.splitext(lib)[0] + ".asm" for lib in libraries],
    }


def main():
    if len(sys.argv) not in (5, 6):
        fail("使い方: hubasic-wasm-manifest.py <repo> <sha> <dirty> <version> [<outdir>]")
    repo, sha, dirty, version = sys.argv[1:5]
    outdir = sys.argv[5] if len(sys.argv) == 6 else None
    envs = {}
    for name in ENVS:
        envs[name] = read_env(os.path.join(repo, "runtime", "env", name + ".env"))
    files = []
    for env in envs.values():
        for lib in env["libraries"]:
            if not os.path.isfile(os.path.join(repo, "runtime", lib)):
                fail(f"runtime/{lib} がありません")
            if lib not in files:
                files.append(lib)
    manifest = {
        "slangCompilerRevision": sha,
        "dirty": dirty == "true",
        "hubasic2slVersion": version,
        "loadAddress": 0x1000,
        "reservedStart": 0xFFE0,
        "envs": envs,
    }
    if outdir:
        os.makedirs(os.path.join(outdir, "runtime"))
        os.makedirs(os.path.join(outdir, "env"))
        for lib in files:
            shutil.copyfile(os.path.join(repo, "runtime", lib), os.path.join(outdir, "runtime", lib))
        for name in ENVS:
            shutil.copyfile(os.path.join(repo, "runtime", "env", name + ".env"),
                            os.path.join(outdir, "env", name + ".env"))
    json.dump(manifest, sys.stdout, indent=2, ensure_ascii=False)
    print()


if __name__ == "__main__":
    main()
