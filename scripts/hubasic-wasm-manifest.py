#!/usr/bin/env python3
"""hubasic2sl のブラウザ向けの一式 (束) の manifest.json を作り、束を検査する (scripts/build-hubasic-wasm.sh から呼ぶ)。

使い方 (python3 -I で動かす):
  hubasic-wasm-manifest.py envs   <repo>                      env の値と組の印の番号を JSON で stdout に出す (テストが使う)
  hubasic-wasm-manifest.py copy   <repo> <out>                runtime/ (2 つの env の libraries の和集合の .asm) と env/ を写す
  hubasic-wasm-manifest.py write  <repo> <out> <sha> <dirty> <version> <asm> <asm-version> <case,...>
                                                              束の全部のファイルを数えて <out>/manifest.json を書く
  hubasic-wasm-manifest.py verify <out>                       束と manifest が合っているかを確かめる

env ファイルから読むのは env_type・os_type・default_org・defines・libraries と、disk: の main_name・main_load・main_exec・volume だけ
(EnvironmentLoader と同じ値になることをテストで確かめる)。読めないか、.asm が無ければ止める。
"""
import hashlib
import json
import os
import re
import shutil
import sys

ENVS = ["x1native_slfs", "x1native"]
TESTDATA_ENV = "x1native_slfs"
TESTDATA_EXTS = ["bas", "sl", "bin", "lst", "d88"]


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
    """env ファイルの要る項目だけを読む (このための最小の読み方。YAML の全部は読まない)"""
    top = {}
    blocks = {"defines": {}, "disk": {}}
    block = None
    libraries = []
    with open(path, encoding="utf-8") as f:
        for raw in f:
            line = strip_comment(raw.rstrip("\r\n"))
            if not line.strip():
                continue
            if line[0] not in " \t":
                key, sep, value = line.partition(":")
                if not sep:
                    continue
                key = key.strip()
                block = key if not value.strip() else None
                if block is None:
                    top[key] = value
                continue
            body = line.strip()
            if block == "libraries" and body.startswith("-"):
                libraries.append(scalar(body[1:]))
            elif block in blocks:
                name, sep, value = body.partition(":")
                if sep:
                    blocks[block][name.strip()] = value
    for key in ("env_type", "os_type", "default_org"):
        if key not in top:
            fail(f"{path} に {key} がありません")
    if not libraries:
        fail(f"{path} に libraries がありません")
    disk = blocks["disk"]
    env = {
        "envType": number(top["env_type"], f"{path} の env_type"),
        "osType": number(top["os_type"], f"{path} の os_type"),
        "defaultOrg": number(top["default_org"], f"{path} の default_org"),
        "defines": {k: number(v, f"{path} の defines の {k}") for k, v in blocks["defines"].items()},
        # EnvironmentLoader と同じく .yml を .asm にする
        "libraries": [os.path.splitext(lib)[0] + ".asm" for lib in libraries],
        "defaultDiskMainName": scalar(disk["main_name"]) if "main_name" in disk else None,
        "diskMainLoad": number(disk["main_load"], f"{path} の disk.main_load") if "main_load" in disk else None,
        "diskMainExec": number(disk["main_exec"], f"{path} の disk.main_exec") if "main_exec" in disk else None,
        "diskVolume": scalar(disk["volume"]) if "volume" in disk else None,
    }
    return env


def read_envs(repo):
    envs = {name: read_env(os.path.join(repo, "runtime", "env", name + ".env")) for name in ENVS}
    for env in envs.values():
        for lib in env["libraries"]:
            if not os.path.isfile(os.path.join(repo, "runtime", lib)):
                fail(f"runtime/{lib} がありません")
    return envs


def read_bas_abi(repo):
    """runtime/libx1_basic.asm の組の印 (; @name BAS_ABI_n) の番号。ちょうど 1 つでなければ止める"""
    with open(os.path.join(repo, "runtime", "libx1_basic.asm"), encoding="utf-8") as f:
        found = re.findall(r"^;\s*@name\s+BAS_ABI_(\d+)\s*$", f.read(), re.M)
    if len(found) != 1:
        fail(f"runtime/libx1_basic.asm の組の印 BAS_ABI_n がちょうど 1 つではありません ({len(found)} 個)")
    return int(found[0])


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def list_files(out):
    """束の中の全部のファイル (manifest.json を除く)。束の根からの相対で / 区切り、path の順"""
    files = []
    for root, _, names in os.walk(out):
        for n in names:
            rel = os.path.relpath(os.path.join(root, n), out).replace(os.sep, "/")
            if rel != "manifest.json":
                files.append(rel)
    return sorted(files)


def disk_main_name(sl_path):
    """SL の頭のコメントの並びから `// disk-main-name:` を読む (無ければ空)"""
    with open(sl_path, encoding="utf-8") as f:
        for line in f:
            if not line.startswith("//"):
                break
            if line.startswith("// disk-main-name:"):
                return line[len("// disk-main-name:"):].strip()
    return ""


def cmd_copy(repo, out):
    envs = read_envs(repo)
    os.makedirs(os.path.join(out, "runtime"))
    os.makedirs(os.path.join(out, "env"))
    done = set()
    for env in envs.values():
        for lib in env["libraries"]:
            if lib not in done:
                shutil.copyfile(os.path.join(repo, "runtime", lib), os.path.join(out, "runtime", lib))
                done.add(lib)
    for name in ENVS:
        shutil.copyfile(os.path.join(repo, "runtime", "env", name + ".env"), os.path.join(out, "env", name + ".env"))


def cmd_write(repo, out, sha, dirty, version, asm, asm_version, cases):
    envs = read_envs(repo)
    testdata = {"env": TESTDATA_ENV, "transpileOptions": {}, "cases": []}
    for name in cases:
        case = {"name": name}
        for ext in TESTDATA_EXTS:
            case[ext] = f"testdata/{name}.{ext.upper() if ext in ('bas', 'sl', 'lst') else ext}"
        case["diskMainName"] = disk_main_name(os.path.join(out, case["sl"]))
        testdata["cases"].append(case)
    manifest = {
        "slangCompilerRevision": sha,
        "dirty": dirty == "true",
        "hubasic2slVersion": version,
        "basAbi": read_bas_abi(repo),
        "loadAddress": 0x1000,
        "reservedStart": 0xFFE0,
        "envs": envs,
        "assembler": {"name": "AILZ80ASM", "version": asm_version, "sha256": sha256(asm)},
        "testdata": testdata,
        "files": [{"path": p, "sha256": sha256(os.path.join(out, p)), "size": os.path.getsize(os.path.join(out, p))}
                  for p in list_files(out)],
    }
    with open(os.path.join(out, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")


def cmd_verify(out):
    with open(os.path.join(out, "manifest.json"), encoding="utf-8") as f:
        m = json.load(f)
    paths = [e["path"] for e in m["files"]]
    if len(paths) != len(set(paths)):
        fail("manifest の files に同じ path が 2 つ以上あります")
    actual = list_files(out)
    if sorted(paths) != actual:
        missing = sorted(set(actual) - set(paths))
        extra = sorted(set(paths) - set(actual))
        fail(f"束のファイルと manifest の files が合いません (files に無い: {missing[:5]}、束に無い: {extra[:5]})")
    for e in m["files"]:
        p = os.path.join(out, e["path"])
        if os.path.getsize(p) != e["size"] or sha256(p) != e["sha256"]:
            fail(f"{e['path']} の大きさか sha256 が manifest と合いません")
    listed = set(paths)
    if not m["testdata"]["cases"]:
        fail("testdata が空です")
    for case in m["testdata"]["cases"]:
        for ext in TESTDATA_EXTS:
            if case.get(ext) not in listed:
                fail(f"testdata の {case['name']} の {ext} ({case.get(ext)}) が束にありません")
    for env in m["envs"].values():
        for lib in env["libraries"]:
            if f"runtime/{lib}" not in listed:
                fail(f"runtime/{lib} が束にありません")
    if not re.fullmatch(r"[0-9a-f]{40}", m["slangCompilerRevision"]):
        fail(f"slangCompilerRevision が SHA ではありません: {m['slangCompilerRevision']}")
    print(f"hubasic-wasm-manifest: 束は manifest と合っています ({len(paths)} ファイル、testdata {len(m['testdata']['cases'])} 本)")


def main():
    args = sys.argv[1:]
    if args[:1] == ["envs"] and len(args) == 2:
        json.dump({"envs": read_envs(args[1]), "basAbi": read_bas_abi(args[1])}, sys.stdout, indent=2, ensure_ascii=False)
        print()
    elif args[:1] == ["copy"] and len(args) == 3:
        cmd_copy(args[1], args[2])
    elif args[:1] == ["write"] and len(args) == 9:
        cmd_write(*args[1:8], [c for c in args[8].split(",") if c])
    elif args[:1] == ["verify"] and len(args) == 2:
        cmd_verify(args[1])
    else:
        fail("使い方: hubasic-wasm-manifest.py envs|copy|write|verify … (ファイルの頭の説明を参照)")


if __name__ == "__main__":
    main()
