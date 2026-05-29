#!/usr/bin/env python3
"""arkos_bin_to_asm.py - Convert binary file to AILZ80ASM `.db` stream asm.

Used for embedding RASM-built Arkos driver bin (= PSGAKG_C300.bin etc.)
and Arkos asset files (= BGM.AKG / SE.AKX) into SLANG programs via
`#ASM INCLUDE` blocks.

Existing repo conventions: `tools/charmap-encode.py` / `tools/png_to_asm.py`.

Usage:
  python3 tools/arkos_bin_to_asm.py <input.bin> \\
    [--org 0xC300] [--no-org] \\
    --label PSGAKG_DRIVER \\
    --output examples/X1NATIVE_ARKOS/PSGAKG_C300.asm

Output layout (with --org):
    ; Generated from <input.bin> (<size> bytes, ORG $XXXX)
        ORG $XXXX
    LABEL:
        DB $XX, $XX, ...
    LABEL_END:

Output layout (with --no-org, e.g. for asset data placed after a SLANG label):
    ; Generated from <input.bin> (<size> bytes, no ORG)
    LABEL:
        DB $XX, $XX, ...
    LABEL_END:
"""

import argparse
import os
import sys


def bin_to_db_lines(data: bytes, indent: str = "    ", per_line: int = 16) -> list[str]:
    """Convert bytes to a list of `DB $XX, $XX, ...` lines."""
    lines = []
    for offset in range(0, len(data), per_line):
        chunk = data[offset:offset + per_line]
        bytes_text = ", ".join(f"${b:02X}" for b in chunk)
        lines.append(f"{indent}DB {bytes_text}")
    return lines


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("input", help="Input binary file (= RASM bin or Arkos asset)")
    parser.add_argument("--org", help="ORG address (hex like 0xC300 or decimal). Required unless --no-org or --slang-array.")
    parser.add_argument("--no-org", action="store_true", help="Skip ORG line (= asset data after SLANG label)")
    parser.add_argument("--slang-array", action="store_true",
                        help="Output SLANG ARRAY BYTE initializer instead of asm DB stream (= for `#INCLUDE` from SLANG)")
    parser.add_argument("--label", required=True, help="Primary label name (= <LABEL>: + <LABEL>_END: for asm、 ARRAY 名 for slang-array)")
    parser.add_argument("--output", "-o", required=True, help="Output file (.asm or .sl)")
    args = parser.parse_args()

    if not args.no_org and not args.org and not args.slang_array:
        parser.error("Either --org or --no-org or --slang-array is required")

    if not os.path.isfile(args.input):
        print(f"Input file not found: {args.input}", file=sys.stderr)
        return 1

    with open(args.input, "rb") as f:
        data = f.read()

    # Parse ORG (hex with 0x prefix or decimal)
    org_value = None
    if args.org:
        org_str = args.org.lower()
        if org_str.startswith("0x"):
            org_value = int(org_str, 16)
        else:
            org_value = int(org_str)

    lines: list[str] = []
    if args.slang_array:
        # SLANG ARRAY BYTE initializer: `ARRAY BYTE NAME[N-1] = { ... };` (= N+1 要素 = N byte)
        size_hint = max(len(data) - 1, 0)
        lines.append(f"// Generated from {os.path.basename(args.input)} ({len(data)} bytes)")
        lines.append(f"ARRAY BYTE {args.label}[{size_hint}] = {{")
        per_line = 16
        for offset in range(0, len(data), per_line):
            chunk = data[offset:offset + per_line]
            bytes_text = ", ".join(f"${b:02X}" for b in chunk)
            sep = "," if offset + per_line < len(data) else ""
            lines.append(f"    {bytes_text}{sep}")
        lines.append("};")
    else:
        if args.org:
            lines.append(f"; Generated from {os.path.basename(args.input)} ({len(data)} bytes, ORG ${org_value:04X})")
            lines.append(f"    ORG ${org_value:04X}")
        else:
            lines.append(f"; Generated from {os.path.basename(args.input)} ({len(data)} bytes, no ORG)")
        lines.append(f"{args.label}:")
        lines.extend(bin_to_db_lines(data))
        lines.append(f"{args.label}_END:")

    output_text = "\n".join(lines) + "\n"

    out_dir = os.path.dirname(args.output)
    if out_dir:
        os.makedirs(out_dir, exist_ok=True)
    with open(args.output, "w") as f:
        f.write(output_text)
    print(f"Wrote {args.output} ({len(data)} bytes -> {len(lines)} lines)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
