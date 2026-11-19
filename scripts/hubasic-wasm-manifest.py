#!/usr/bin/env python3
"""hubasic2sl のブラウザ向けの一式 (束) の manifest.json を作り、束を検査する (scripts/build-hubasic-wasm.sh から呼ぶ)。

使い方 (python3 -I で動かす):
  hubasic-wasm-manifest.py envs   <repo>                      env の値と組の印の番号を JSON で stdout に出す (テストが使う)
  hubasic-wasm-manifest.py copy   <repo> <out>                runtime/ (4 つの env の libraries の和集合の .asm)・env/・include/ (X1 で使う .LIB)・
                                                              LICENSE・LICENSE-APACHE-2.0.txt を写す (env の disk.template のディスクの型は写さない)
  hubasic-wasm-manifest.py write  <repo> <out> <sha> <dirty> <version> <asm> <asm-version> <case,...> <hufs-add> <slang-case,...> [<manifest>]
                                                              束の全部のファイルを数えて <out>/manifest.json (<manifest> を書けばそこ) を書く。
                                                              <case> は HuBASIC の例。.SL の頭の `// env:` が x1native_hufs なら hufsTestdata、
                                                              無ければ testdata (x1native_slfs) に入れる。
                                                              <hufs-add> は「例の名前:名前=testdata のファイル,種類」を ; でつないだもの
                                                              (その例のディスクに --hufs-add で足したファイル)。<slang-case> は testdata/slang/ の x1 の env の例
  hubasic-wasm-manifest.py verify <out> [<manifest>]          束と manifest (既定は <out>/manifest.json) が合っているかを確かめる

env ファイルから読むのは env_type・os_type・default_org・defines・libraries と、disk: の main_name・main_load・main_exec・volume だけ
(EnvironmentLoader と同じ値になることをテストで確かめる)。読めないか、.asm が無ければ止める。
"""
import hashlib
import json
import os
import re
import shutil
import sys

ENVS = ["x1native_slfs", "x1native", "x1native_hufs", "x1"]
# x1 の env (X1Pen の SLANG タブ) の #INCLUDE で使う、X1 向けの include (include/ の C64・MSX などのものは入れない)
INCLUDE_FILES = ["CHIPLIB.LIB", "GRAPH.LIB", "GRAPHF.LIB", "SOROBAN.LIB", "SPRLIB.LIB", "TILELIB.LIB", "TILESPR.LIB", "UILIB.LIB"]
# SLANG タブ用の試験データの env と、そのファイルの拡張子
SLANG_TESTDATA_ENV = "x1"
SLANG_TESTDATA_EXTS = {"sl": "SL", "asm": "ASM", "bin": "bin"}
# 束の根に写すライセンスの表示 (runtime/ の .asm を配るので要る。libfloat.asm は Apache 2.0)
LICENSE_FILES = ["LICENSE", "LICENSE-APACHE-2.0.txt"]
TESTDATA_ENV = "x1native_slfs"
# ファイルの命令を使う例 (x1native_hufs で組む) は、testdata とは別の hufsTestdata に入れる
# (testdata の読み手は、いちばん上の env で全部の例を組むので、そこに違う env の例を混ぜない)
HUFS_TESTDATA_ENV = "x1native_hufs"
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
        # ディスクの形 (slfs-pack = SLFS、hufs-pack = Hu-BASIC と同じ形)。ブラウザの側がディスクを作るときに見分ける
        "diskTool": scalar(disk["tool"]) if "tool" in disk else None,
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


def read_bas_file_abi(repo):
    """runtime/libx1_basic_file.asm の組の印 (; @name BAS_FABI_n) の番号 (x1native_hufs の出力だけが呼ぶ)"""
    with open(os.path.join(repo, "runtime", "libx1_basic_file.asm"), encoding="utf-8") as f:
        found = re.findall(r"^;\s*@name\s+BAS_FABI_(\d+)\s*$", f.read(), re.M)
    if len(found) != 1:
        fail(f"runtime/libx1_basic_file.asm の組の印 BAS_FABI_n がちょうど 1 つではありません ({len(found)} 個)")
    return int(found[0])


def read_slangc_version(repo):
    """src/SLANGCompiler.Core/SlangVersion.cs の slangc の版"""
    with open(os.path.join(repo, "src", "SLANGCompiler.Core", "SlangVersion.cs"), encoding="utf-8") as f:
        found = re.findall(r'const\s+string\s+Version\s*=\s*"([^"]+)"', f.read())
    if len(found) != 1:
        fail("src/SLANGCompiler.Core/SlangVersion.cs の Version がちょうど 1 つではありません")
    return found[0]


def sl_env(sl_path, default):
    """hubasic2sl の SL の頭のコメントの並びから `// env: <名前>` を読む (無ければ default)"""
    with open(sl_path, encoding="utf-8") as f:
        for line in f:
            if not line.startswith("//"):
                break
            if line.startswith("// env:"):
                return line[len("// env:"):].split()[0]
    return default


def hu_disk_name(name):
    """HuName.Parse と同じく、ディスクの上の名前として比べる形 (ドライブの 0: を除き、名前と拡張子の後ろの空白を除く)"""
    if name.startswith("0:"):
        name = name[2:]
    base, dot, ext = name.rpartition(".")
    if not dot:
        base, ext = name, ""
    return (base.rstrip(" "), ext.rstrip(" "))


def parse_hufs_add(spec):
    """「例の名前:名前=ファイル,種類」を ; でつないだものを {例の名前: [{name, file, kind}]} にする"""
    adds = {}
    for item in [x for x in spec.split(";") if x]:
        case, sep, rest = item.partition(":")
        name, sep2, file_kind = rest.partition("=")
        file, sep3, kind = file_kind.rpartition(",")
        if not (sep and sep2 and sep3) or kind not in ("text", "bin", "raw"):
            fail(f"hufs-add の書き方がおかしいです: {item}")
        adds.setdefault(case, []).append({"name": name, "file": file, "kind": kind})
    return adds


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
    os.makedirs(os.path.join(out, "include"))
    for name in INCLUDE_FILES:
        shutil.copyfile(os.path.join(repo, "include", name), os.path.join(out, "include", name))
    for name in LICENSE_FILES:
        shutil.copyfile(os.path.join(repo, name), os.path.join(out, name))


def cmd_write(repo, out, sha, dirty, version, asm, asm_version, cases, hufs_add, slang_cases, manifest_path=None):
    envs = read_envs(repo)
    adds = parse_hufs_add(hufs_add)
    testdata = {"env": TESTDATA_ENV, "transpileOptions": {}, "cases": []}
    hufs_testdata = {"env": HUFS_TESTDATA_ENV, "transpileOptions": {}, "cases": []}
    for name in cases:
        case = {"name": name}
        for ext in TESTDATA_EXTS:
            case[ext] = f"testdata/{name}.{ext.upper() if ext in ('bas', 'sl', 'lst') else ext}"
        case["diskMainName"] = disk_main_name(os.path.join(out, case["sl"]))
        env = sl_env(os.path.join(out, case["sl"]), TESTDATA_ENV)
        if env == TESTDATA_ENV:
            if name in adds:
                fail(f"{name} は {TESTDATA_ENV} の例なので、ファイルを足せません")
            testdata["cases"].append(case)
        elif env == HUFS_TESTDATA_ENV:
            case["hufsAdd"] = adds.pop(name, [])
            hufs_testdata["cases"].append(case)
        else:
            fail(f"{name} の env {env} は testdata に入れられません")
    if adds:
        fail(f"hufs-add の例が testdata にありません: {sorted(adds)}")
    slang_testdata = {
        "env": SLANG_TESTDATA_ENV,
        "includePaths": ["include"],
        "cases": [{"name": name, **{k: f"testdata/slang/{name}.{ext}" for k, ext in SLANG_TESTDATA_EXTS.items()}}
                  for name in slang_cases],
    }
    manifest = {
        "slangCompilerRevision": sha,
        "dirty": dirty == "true",
        "hubasic2slVersion": version,
        "slangcVersion": read_slangc_version(repo),
        "basAbi": read_bas_abi(repo),
        "basFileAbi": read_bas_file_abi(repo),
        "loadAddress": 0x1000,
        "reservedStart": 0xFFE0,
        "envs": envs,
        "assembler": {"name": "AILZ80ASM", "version": asm_version, "sha256": sha256(asm)},
        "testdata": testdata,
        "hufsTestdata": hufs_testdata,
        "slangTestdata": slang_testdata,
        "files": [{"path": p, "sha256": sha256(os.path.join(out, p)), "size": os.path.getsize(os.path.join(out, p))}
                  for p in list_files(out)],
    }
    with open(manifest_path or os.path.join(out, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")


def cmd_verify(out, manifest_path=None):
    manifest_path = manifest_path or os.path.join(out, "manifest.json")
    if not os.path.isfile(manifest_path):
        fail(f"{manifest_path} がありません (組み立てが途中で止まった束か、束ではありません)")
    with open(manifest_path, encoding="utf-8") as f:
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
    for name in LICENSE_FILES:
        if name not in listed:
            fail(f"{name} が束にありません")
    if not m["testdata"]["cases"]:
        fail("testdata が空です")
    # 前の版の束 (hufsTestdata・slangTestdata・slangcVersion がどれも無い manifest) は、前からの項目だけを確かめる。
    # どれかだけがある manifest は、途中までしか書けていないので止める
    new_keys = ("slangcVersion", "hufsTestdata", "slangTestdata")
    present = [k for k in new_keys if k in m]
    # 今の形の束にだけあるファイル (x1 の env・SLANG タブの testdata・include) があれば、今の形の manifest が要る
    current_files = [p for p in paths if p == "env/x1.env" or p.startswith("testdata/slang/") or p.startswith("include/")]
    if (present or current_files) and len(present) != len(new_keys):
        why = f"{', '.join(present)} がある" if present else f"今の形の束のファイル ({current_files[0]} など) がある"
        fail(f"manifest に {', '.join(k for k in new_keys if k not in m)} がありません ({why}ので、今の形の manifest です)")
    current = bool(present)
    # 今の形の manifest では、試験データの組ごとの env は決まっている (write が書くもの)
    expected_env = {"testdata": TESTDATA_ENV, "hufsTestdata": HUFS_TESTDATA_ENV}
    for key in ("testdata", "hufsTestdata") if current else ("testdata",):
        if m[key]["env"] not in m["envs"]:
            fail(f"{key} の env ({m[key]['env']}) が束にありません")
        if current and m[key]["env"] != expected_env[key]:
            fail(f"{key} の env は {expected_env[key]} のはずです ({m[key]['env']})")
        for case in m[key]["cases"]:
            for ext in TESTDATA_EXTS:
                if case.get(ext) not in listed:
                    fail(f"{key} の {case['name']} の {ext} ({case.get(ext)}) が束にありません")
            if key != "hufsTestdata":
                continue
            # Hu-BASIC の形のディスクの例は、足したファイルの一覧 (hufsAdd) が要る (ディスクを作り直すのに使う)
            adds = case.get("hufsAdd")
            if not isinstance(adds, list):
                fail(f"{key} の {case['name']} に hufsAdd の一覧がありません")
            disk_names = set()
            for add in adds:
                if not isinstance(add, dict) or not add.get("name") or add.get("kind") not in ("text", "bin", "raw"):
                    fail(f"{key} の {case['name']} の hufsAdd の項目がおかしいです: {add}")
                # ディスクの上の名前は同じものを 2 つ足せない (HuName と同じ比べ方。大文字と小文字は区別する)
                disk_name = hu_disk_name(add["name"])
                if disk_name in disk_names:
                    fail(f"{key} の {case['name']} の hufsAdd に {add['name']} と同じ名前があります")
                disk_names.add(disk_name)
                if add.get("file") not in listed:
                    fail(f"{key} の {case['name']} の足すファイル ({add.get('file')}) が束にありません")
            if case["name"] == "FILEIO" and not any(a["name"] == "GREET.TXT" and a["kind"] == "text" for a in adds):
                fail(f"{key} の FILEIO に、GREET.TXT (text) を足した記録がありません")
    if current:
        if not m["hufsTestdata"]["cases"]:
            fail("hufsTestdata が空です")
        # ファイルの命令の例 FILEIO は hufsTestdata にちょうど 1 つ (testdata には入れない)
        if [c["name"] for c in m["hufsTestdata"]["cases"]].count("FILEIO") != 1:
            fail("hufsTestdata に FILEIO の例がちょうど 1 つありません")
        if any(c["name"] == "FILEIO" for c in m["testdata"]["cases"]):
            fail("testdata に FILEIO があります (hufsTestdata に入れるものです)")
        # env の値は、束の env ファイルから読み直したものと同じでなければならない
        for name, values in m["envs"].items():
            if read_env(os.path.join(out, "env", name + ".env")) != values:
                fail(f"manifest の envs の {name} が、束の env/{name}.env と合いません")
        if m["slangTestdata"].get("env") not in m["envs"]:
            fail(f"slangTestdata の env ({m['slangTestdata'].get('env')}) が束にありません")
        if m["slangTestdata"]["env"] != SLANG_TESTDATA_ENV:
            fail(f"slangTestdata の env は {SLANG_TESTDATA_ENV} のはずです ({m['slangTestdata']['env']})")
        if not m["slangTestdata"]["cases"]:
            fail("SLANG タブ用の testdata が空です")
        for case in m["slangTestdata"]["cases"]:
            for key in SLANG_TESTDATA_EXTS:
                if case.get(key) not in listed:
                    fail(f"SLANG タブ用の testdata の {case['name']} の {key} ({case.get(key)}) が束にありません")
        include_paths = m["slangTestdata"].get("includePaths") or []
        if not include_paths:
            fail("slangTestdata に includePaths がありません")
        for name in INCLUDE_FILES:
            if not any(f"{p.rstrip('/')}/{name}" in listed for p in include_paths):
                fail(f"{name} が slangTestdata の includePaths ({include_paths}) のどこにもありません")
    for name in m["envs"]:
        if f"env/{name}.env" not in listed:
            fail(f"env/{name}.env が束にありません")
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
        json.dump({"envs": read_envs(args[1]), "basAbi": read_bas_abi(args[1]), "basFileAbi": read_bas_file_abi(args[1])},
                  sys.stdout, indent=2, ensure_ascii=False)
        print()
    elif args[:1] == ["copy"] and len(args) == 3:
        cmd_copy(args[1], args[2])
    elif args[:1] == ["write"] and len(args) in (11, 12):
        cmd_write(*args[1:8], [c for c in args[8].split(",") if c], args[9], [c for c in args[10].split(",") if c],
                  args[11] if len(args) == 12 else None)
    elif args[:1] == ["verify"] and len(args) in (2, 3):
        cmd_verify(args[1], args[2] if len(args) == 3 else None)
    else:
        fail("使い方: hubasic-wasm-manifest.py envs|copy|write|verify … (ファイルの頭の説明を参照)")


if __name__ == "__main__":
    main()
