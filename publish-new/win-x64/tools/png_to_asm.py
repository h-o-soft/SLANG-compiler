#!/usr/bin/env python3
"""
PNG 画像を読んで UILIB.LIB 内の該当データブロックに DB 列として書き込む。

2 モード:
  - デフォルト: 128x128 PNG (16x16 = 256 glyph × 8x8) をフォントデータとして
    `_UI_FONT_DATA` ブロック (2048 byte) に書き込む。
  - `--box`:    24x24 PNG (3x3 = 9 slice × 8x8) を 9-slice ボックス画像として
    `_UI_BOX_DATA` ブロック (72 byte) に書き込む。スロット配置は
      0=TL, 1=T, 2=TR
      3=L,  4=M, 5=R
      6=BL, 7=B, 8=BR

Usage:
    # フォント (128x128 PNG)
    python3 tools/png_to_asm.py assets/ui/font_charset1.png --inplace include/UILIB.LIB
    python3 tools/png_to_asm.py assets/ui/font_charset1.png             # stdout 確認のみ

    # 9-slice ボックス (24x24 PNG)
    python3 tools/png_to_asm.py assets/ui/window.png --box --inplace include/UILIB.LIB
    python3 tools/png_to_asm.py assets/ui/window.png --box               # stdout 確認のみ

    # 白背景黒文字の PNG の場合は --invert
    python3 tools/png_to_asm.py ... --invert

Python 標準ライブラリのみ使用 (struct + zlib)。
"""

import argparse
import os
import struct
import sys
import tempfile
import zlib


FONT_MARKER_BEGIN = "; === FONT DATA BEGIN"
FONT_MARKER_END = "; === FONT DATA END ==="
BOX_MARKER_BEGIN = "; === BOX DATA BEGIN"
BOX_MARKER_END = "; === BOX DATA END ==="

BOX_SLOT_NAMES = ["TL", "T", "TR", "L", "M", "R", "BL", "B", "BR"]


def parse_png(path):
    """8-bit RGB PNG を読んで (width, height, pixels) を返す。

    pixels は height × width × 3 の bytearray (RGB row-major, top-to-bottom)。
    """
    with open(path, "rb") as f:
        data = f.read()

    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"{path}: not a PNG file")

    pos = 8
    ihdr = None
    idat = bytearray()

    while pos < len(data):
        length = struct.unpack(">I", data[pos : pos + 4])[0]
        chunk_type = data[pos + 4 : pos + 8]
        chunk_data = data[pos + 8 : pos + 8 + length]
        # skip CRC (4 bytes)
        pos += 8 + length + 4

        if chunk_type == b"IHDR":
            ihdr = struct.unpack(">IIBBBBB", chunk_data)
        elif chunk_type == b"IDAT":
            idat.extend(chunk_data)
        elif chunk_type == b"IEND":
            break

    if ihdr is None:
        raise ValueError(f"{path}: no IHDR chunk")

    width, height, bit_depth, color_type, compression, filter_method, interlace = ihdr

    if bit_depth != 8 or color_type != 2:
        raise ValueError(
            f"{path}: expected 8-bit RGB (bit_depth=8, color_type=2), "
            f"got bit_depth={bit_depth}, color_type={color_type}"
        )
    if compression != 0 or filter_method != 0 or interlace != 0:
        raise ValueError(
            f"{path}: unsupported PNG features "
            f"(compression={compression}, filter={filter_method}, interlace={interlace})"
        )

    raw = zlib.decompress(bytes(idat))
    stride = width * 3
    expected = height * (1 + stride)
    if len(raw) != expected:
        raise ValueError(
            f"{path}: decompressed size mismatch (got {len(raw)}, expected {expected})"
        )

    pixels = bytearray(height * stride)
    prev_row = bytearray(stride)

    for y in range(height):
        row_start = y * (1 + stride)
        filter_type = raw[row_start]
        row = bytearray(raw[row_start + 1 : row_start + 1 + stride])

        if filter_type == 0:
            pass
        elif filter_type == 1:  # Sub
            for i in range(3, stride):
                row[i] = (row[i] + row[i - 3]) & 0xFF
        elif filter_type == 2:  # Up
            for i in range(stride):
                row[i] = (row[i] + prev_row[i]) & 0xFF
        elif filter_type == 3:  # Average
            for i in range(stride):
                left = row[i - 3] if i >= 3 else 0
                above = prev_row[i]
                row[i] = (row[i] + (left + above) // 2) & 0xFF
        elif filter_type == 4:  # Paeth
            for i in range(stride):
                left = row[i - 3] if i >= 3 else 0
                above = prev_row[i]
                upper_left = prev_row[i - 3] if i >= 3 else 0
                p = left + above - upper_left
                pa = abs(p - left)
                pb = abs(p - above)
                pc = abs(p - upper_left)
                if pa <= pb and pa <= pc:
                    pred = left
                elif pb <= pc:
                    pred = above
                else:
                    pred = upper_left
                row[i] = (row[i] + pred) & 0xFF
        else:
            raise ValueError(f"{path}: unknown filter type {filter_type} at row {y}")

        pixels[y * stride : (y + 1) * stride] = row
        prev_row = row

    return width, height, pixels


def png_to_glyphs(pixels, width, invert, grid_w, grid_h):
    """grid_w × grid_h のグリッド分の 8x8 グリフを抽出。
    戻り値: grid_w*grid_h*8 バイト (MSB = 左端ピクセル, row-major)"""
    stride = width * 3
    n_glyphs = grid_w * grid_h
    out = bytearray(n_glyphs * 8)
    for gy in range(grid_h):
        for gx in range(grid_w):
            glyph_idx = gy * grid_w + gx
            for row in range(8):
                pixel_y = gy * 8 + row
                byte = 0
                for col in range(8):
                    pixel_x = gx * 8 + col
                    r = pixels[pixel_y * stride + pixel_x * 3]
                    on = r > 128
                    if invert:
                        on = not on
                    if on:
                        byte |= 1 << (7 - col)
                out[glyph_idx * 8 + row] = byte
    return out


def format_font_db_lines(glyphs):
    """2048 bytes を 256 行の DB 列に整形 (glyph 番号コメント付き)"""
    lines = []
    for idx in range(256):
        vals = ", ".join(f"${glyphs[idx * 8 + r]:02X}" for r in range(8))
        lines.append(f"    DB {vals}\t; glyph {idx}")
    return lines


def format_box_db_lines(glyphs):
    """72 bytes を 9 行の DB 列に整形 (スロット名コメント付き)"""
    lines = []
    for idx in range(9):
        vals = ", ".join(f"${glyphs[idx * 8 + r]:02X}" for r in range(8))
        lines.append(f"    DB {vals}\t; slot {idx}: {BOX_SLOT_NAMES[idx]}")
    return lines


def do_stdout(lines):
    for line in lines:
        print(line)


def do_inplace(target_path, body_lines, marker_begin, marker_end, label):
    """UILIB.LIB を読み、指定マーカー間を置換して原子的に書き戻す。"""
    with open(target_path, "r", encoding="utf-8") as f:
        lines = f.readlines()

    begin_indices = [i for i, line in enumerate(lines) if line.startswith(marker_begin)]
    end_indices = [i for i, line in enumerate(lines) if line.startswith(marker_end)]

    if len(begin_indices) != 1:
        raise ValueError(
            f"{target_path}: expected exactly 1 '{marker_begin}' marker, found {len(begin_indices)}"
        )
    if len(end_indices) != 1:
        raise ValueError(
            f"{target_path}: expected exactly 1 '{marker_end}' marker, found {len(end_indices)}"
        )

    begin = begin_indices[0]
    end = end_indices[0]
    if end <= begin:
        raise ValueError(
            f"{target_path}: END marker appears before BEGIN marker"
        )

    new_body = [f"{label}:\n"]
    for line in body_lines:
        new_body.append(line + "\n")

    new_lines = lines[: begin + 1] + new_body + lines[end:]

    dir_name = os.path.dirname(os.path.abspath(target_path)) or "."
    with tempfile.NamedTemporaryFile(
        "w", encoding="utf-8", dir=dir_name, delete=False
    ) as tmp:
        tmp.writelines(new_lines)
        tmp_path = tmp.name
    os.replace(tmp_path, target_path)


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("png", help="input PNG")
    ap.add_argument("--inplace", metavar="UILIB.LIB",
                    help="rewrite font/box block in the given UILIB.LIB in place")
    ap.add_argument("--invert", action="store_true",
                    help="treat dark pixels as ON (for white-background images)")
    ap.add_argument("--box", action="store_true",
                    help="treat input as 24x24 9-slice box image (default: 128x128 font)")
    args = ap.parse_args()

    width, height, pixels = parse_png(args.png)

    if args.box:
        if width != 24 or height != 24:
            sys.exit(f"error (--box): expected 24x24 PNG, got {width}x{height}")
        glyphs = png_to_glyphs(pixels, width, args.invert, 3, 3)
        if len(glyphs) != 9 * 8:
            sys.exit(f"error: expected 72 bytes for 9-slice box, got {len(glyphs)}")
        body_lines = format_box_db_lines(glyphs)
        if args.inplace:
            do_inplace(args.inplace, body_lines,
                       BOX_MARKER_BEGIN, BOX_MARKER_END, "_UI_BOX_DATA")
            print(f"updated BOX DATA block in {args.inplace}", file=sys.stderr)
        else:
            do_stdout(body_lines)
    else:
        if width != 128 or height != 128:
            sys.exit(f"error: expected 128x128 PNG, got {width}x{height}")
        glyphs = png_to_glyphs(pixels, width, args.invert, 16, 16)
        if len(glyphs) != 256 * 8:
            sys.exit(f"error: expected 2048 bytes for font, got {len(glyphs)}")
        body_lines = format_font_db_lines(glyphs)
        if args.inplace:
            do_inplace(args.inplace, body_lines,
                       FONT_MARKER_BEGIN, FONT_MARKER_END, "_UI_FONT_DATA")
            print(f"updated FONT DATA block in {args.inplace}", file=sys.stderr)
        else:
            do_stdout(body_lines)


if __name__ == "__main__":
    main()
