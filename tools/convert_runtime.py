#!/usr/bin/env python3
"""
既存YAML形式のSLANGランタイムライブラリを新形式(.asm+メタデータコメント)に変換する。

Usage:
    python3 convert_runtime.py lib/libdef/runtime.yml > runtime/core.asm
    python3 convert_runtime.py lib/libdef/liblsx_print.yml > runtime/lsx_print.asm
"""

import sys
import yaml

def convert_yaml_to_asm(yaml_path):
    with open(yaml_path, 'r') as f:
        data = yaml.safe_load(f)

    if not data:
        return

    print(f"; Converted from {yaml_path}")
    print(f"; SLANG Runtime Library (new format)")
    print()

    for name, info in data.items():
        if not isinstance(info, dict):
            continue

        # メタデータコメント
        print(f"; @name {name}")

        if 'param_count' in info:
            print(f"; @param_count {info['param_count']}")

        if 'function_type' in info:
            print(f"; @function_type {info['function_type']}")

        if 'calls' in info and info['calls']:
            calls = ','.join(info['calls'])
            print(f"; @calls {calls}")

        if 'lib_name' in info and info['lib_name']:
            print(f"; @lib {info['lib_name']}")

        # コード本体
        code = info.get('code', '')
        if code:
            for line in code.split('\n'):
                # 空行はスキップしない（構造を維持）
                print(line.rstrip())

        # extlib参照の場合
        if 'extlib' in info and info['extlib']:
            print(f"; @extlib {info['extlib']}")

        # include参照の場合
        if 'include' in info and info['include']:
            for inc in info['include']:
                print(f"; @include {inc}")

        print()

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: convert_runtime.py <yaml_file> [<yaml_file> ...]", file=sys.stderr)
        sys.exit(1)

    for path in sys.argv[1:]:
        convert_yaml_to_asm(path)
