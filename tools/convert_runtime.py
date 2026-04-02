#!/usr/bin/env python3
"""
既存YAML形式のSLANGランタイムライブラリを新形式(.asm+メタデータコメント)に変換する。

Usage:
    python3 convert_runtime.py lib/libdef/runtime.yml > runtime/core.asm
    python3 convert_runtime.py lib/libdef/liblsx_print.yml > runtime/lsx_print.asm
"""

import sys
import os
import yaml


def load_extlib(extlib_ref, lib_root):
    """extlib: 'path:LABEL' → #LIB LABEL 〜 #ENDLIB 間のコードを抽出"""
    parts = extlib_ref.split(':')
    if len(parts) < 2:
        print(f"; WARNING: invalid extlib format: {extlib_ref}", file=sys.stderr)
        return ""
    filepath, label = parts[0], parts[1]
    full_path = os.path.join(lib_root, filepath)
    if not os.path.exists(full_path):
        print(f"; WARNING: extlib file not found: {full_path}", file=sys.stderr)
        return ""

    lines = []
    found = False
    with open(full_path, 'r', encoding='utf-8', errors='replace') as f:
        for line in f:
            line = line.rstrip('\n').rstrip('\r')
            stripped = line.strip().upper()
            if found and stripped.startswith("#ENDLIB"):
                break
            if found:
                lines.append(line)
            if stripped.startswith("#LIB"):
                parts2 = stripped.split()
                if len(parts2) >= 2 and parts2[1] == label.upper():
                    found = True
    if not found:
        print(f"; WARNING: extlib label not found: {label} in {full_path}", file=sys.stderr)
    return '\n'.join(lines)


def load_include(include_files, lib_root):
    """include: [file1, file2] → 各ファイルの内容を結合"""
    parts = []
    for fname in include_files:
        full_path = os.path.join(lib_root, fname)
        if os.path.exists(full_path):
            with open(full_path, 'r', encoding='utf-8', errors='replace') as f:
                parts.append(f.read())
        else:
            print(f"; WARNING: include file not found: {full_path}", file=sys.stderr)
    return '\n'.join(parts)


def convert_yaml_to_asm(yaml_path):
    # lib_root: lib/libdef/foo.yml → lib/
    lib_root = os.path.dirname(os.path.dirname(os.path.abspath(yaml_path)))

    with open(yaml_path, 'r') as f:
        data = yaml.safe_load(f)

    if not data:
        return

    if not isinstance(data, dict):
        print(f"; Skipped: {yaml_path} (not a function dictionary)", file=sys.stderr)
        return

    print(f"; Converted from {yaml_path}")
    print(f"; SLANG Runtime Library (new format)")
    print()

    for name, info in data.items():
        if not isinstance(info, dict):
            continue

        # inside_name: ASMラベルとして使う別名（Z80予約語回避）
        asm_name = info.get('inside_name', name)

        # メタデータコメント
        print(f"; @name {asm_name}")
        if asm_name != name:
            print(f"; @alias {name}")

        if 'param_count' in info:
            print(f"; @param_count {info['param_count']}")

        if 'function_type' in info:
            print(f"; @function_type {info['function_type']}")

        if 'calls' in info and info['calls']:
            calls = ','.join(info['calls'])
            print(f"; @calls {calls}")

        if 'lib_name' in info and info['lib_name']:
            print(f"; @lib {info['lib_name']}")

        if 'stack_cleanup' in info and info['stack_cleanup']:
            print(f"; @stack_cleanup {info['stack_cleanup']}")

        # works: メタデータとして出力（本体にDSを出さない）
        if 'works' in info and info['works']:
            items = ','.join(f"{k}:{v}" for k, v in info['works'].items())
            print(f"; @works {items}")

        # initialize_code: init_codeブロックとして出力
        if 'initialize_code' in info and info['initialize_code']:
            print("; @init_code")
            for line in info['initialize_code'].split('\n'):
                if line.rstrip():
                    print(line.rstrip())
            print("; @end_init")

        # コード本体の決定: code > extlib > include
        code = info.get('code', '')

        if not code and 'extlib' in info and info['extlib']:
            code = load_extlib(info['extlib'], lib_root)

        if not code and 'include' in info and info['include']:
            code = load_include(info['include'], lib_root)

        if code:
            for line in code.split('\n'):
                # 空行はスキップしない（構造を維持）
                print(line.rstrip())

        print()


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: convert_runtime.py <yaml_file> [<yaml_file> ...]", file=sys.stderr)
        sys.exit(1)

    for path in sys.argv[1:]:
        convert_yaml_to_asm(path)
