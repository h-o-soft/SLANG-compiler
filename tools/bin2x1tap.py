#!/usr/bin/env python3
"""bin2x1tap.py - 生バイナリ (= 外部アセンブラ出力等) を X1 cassette .tap に wrap。

`slangbuild --emit tape` の C# 実装 (src/SLANGCompiler.Build/TapeFormats/) を
Python で 1:1 mirror したもの。 X1 backend を持たないツール (= WLA-DX で組んだ
Z80 生 bin 等) を X1 実機/エミュで OS なし起動させたい時に使う。 出力は純正 /
互換 X1 IPL 両対応 (= memory `x1-tape-format-spec` 準拠)。

正確性は slangbuild 生成 tap との byte 一致で担保 (= tools/verify_bin2x1tap.sh)。

処理の流れ (= C# X1Program.Encode → X1FskCodec.Modulate → TapFile.Save):
  1. 論理 bit 列を組む (0/1):
       info: leader 1×8000 + sync(0×40,1×41) + (info32B+cksum2B) を 9bit/byte framing
       data: leader 1×4000 + sync(0×20,1×21) + (dataNB+cksum2B) を 9bit/byte framing
       trailing 1×1000
     checksum = payload 全 byte の popcount mod 65536、 Big-Endian
     info block の DataSize/Load/Exec は Little-Endian
  2. FSK 変調 (= X1FskCodec.Modulate): 論理 1bit → 1 square cycle:
       "0" = 4kHz cycle → 2 sample (1 0)
       "1" = 2kHz cycle → 4 sample (1 1 0 0)
     各 period の前半 HIGH, 後半 LOW (= i < period/2 ? 1 : 0)
  3. sample (0/1) を MSB-first で 1bit/sample に pack
  4. file = 40B TapHeader ("TAPE" magic) + packed samples

Usage:
  python3 tools/bin2x1tap.py input.bin -o output.tap \\
    --load 0x100 --exec 0x100 --name PROG [--sample-rate 8000]
"""

import argparse
import os
import sys

# --- framing 定数 (= X1TapeFraming.cs と同値) ---
INFO_LEADER_ONES = 8000
DATA_LEADER_ONES = 4000
TRAILING_ONES    = 1000
INFO_SYNC_ZEROS  = 40
INFO_SYNC_ONES   = 41
DATA_SYNC_ZEROS  = 20
DATA_SYNC_ONES   = 21

FREQ_ZERO_HZ = 4000.0      # X1FskCodec.FreqZeroHz ("0" bit = 4kHz cycle)
FREQ_ONE_HZ  = 2000.0      # X1FskCodec.FreqOneHz  ("1" bit = 2kHz cycle)
TAP_SAMPLE_RATE = 8000     # TapeImageBuilder が .tap encode に使う値


def build_info_block(name: str, ext: str, load: int, exec_addr: int,
                     data_size: int, boot_flag: int = 0x01,
                     password: int = 0x20) -> bytes:
    """info block 32 byte (= X1InfoBlock.ToBytes)。"""
    info = bytearray(32)
    info[0] = boot_flag
    for i in range(13):                       # FileName 13B (space-padded)
        info[1 + i] = ord(name[i]) if i < len(name) else 0x20
    for i in range(3):                        # Extension 3B
        info[14 + i] = ord(ext[i]) if i < len(ext) else 0x20
    info[17] = password
    info[18] = data_size & 0xFF               # DataSize (LE)
    info[19] = (data_size >> 8) & 0xFF
    info[20] = load & 0xFF                     # LoadAddress (LE)
    info[21] = (load >> 8) & 0xFF
    info[22] = exec_addr & 0xFF                # ExecuteAddress (LE)
    info[23] = (exec_addr >> 8) & 0xFF
    # Date 6B / Reserved 2B = 0
    return bytes(info)


def _checksum(payload: bytes) -> int:
    """popcount mod 65536 (= X1TapeFraming.ComputeChecksum)。"""
    return sum(bin(b).count("1") for b in payload) & 0xFFFF


def _write_bytes_bits(out: list, data: bytes) -> None:
    """9 bit/byte framing: start "1" + 8 data MSB first (= WriteBytes)。"""
    for b in data:
        out.append(1)
        for k in range(7, -1, -1):
            out.append((b >> k) & 1)


def _build_sync(out: list, leader: int, zeros: int, ones: int) -> None:
    out.extend([1] * leader)
    out.extend([0] * zeros)
    out.extend([1] * ones)


def build_logical_bits(info: bytes, data: bytes) -> list:
    """info + data → 論理 bit 列 (0/1) (= X1Program.Encode の bit stream)。"""
    bits = []
    # info block
    info_ck = _checksum(info)
    info_with_ck = info + bytes([(info_ck >> 8) & 0xFF, info_ck & 0xFF])  # BE
    _build_sync(bits, INFO_LEADER_ONES, INFO_SYNC_ZEROS, INFO_SYNC_ONES)
    _write_bytes_bits(bits, info_with_ck)
    # data block
    data_ck = _checksum(data)
    data_with_ck = data + bytes([(data_ck >> 8) & 0xFF, data_ck & 0xFF])  # BE
    _build_sync(bits, DATA_LEADER_ONES, DATA_SYNC_ZEROS, DATA_SYNC_ONES)
    _write_bytes_bits(bits, data_with_ck)
    # trailing
    bits.extend([1] * TRAILING_ONES)
    return bits


def modulate(bits: list, sample_rate: int) -> list:
    """論理 bit → FSK サンプル (0/1) (= X1FskCodec.Modulate)。
    "0" = 4kHz cycle (2 sample at 8kHz)、 "1" = 2kHz cycle (4 sample)、 前半 HIGH。"""
    samples_for_zero = round(sample_rate / FREQ_ZERO_HZ)
    samples_for_one  = round(sample_rate / FREQ_ONE_HZ)
    if samples_for_zero < 2 or samples_for_one < 2:
        raise ValueError(f"sample rate {sample_rate} too low for FSK")
    samples = []
    for bit in bits:
        period = samples_for_one if bit else samples_for_zero
        half_high = period // 2
        samples.extend(1 if i < half_high else 0 for i in range(period))
    return samples


def pack_samples(samples: list) -> bytes:
    """sample (0/1) を MSB-first で 1bit/sample に pack (= TapFile.PackSamples)。"""
    out = bytearray((len(samples) + 7) // 8)
    for i, s in enumerate(samples):
        if s:
            out[i >> 3] |= 0x80 >> (i & 7)
    return bytes(out)


def build_tap_header(sample_rate: int, tape_name: str, sample_count: int) -> bytes:
    """40 byte TapHeader (= TapHeader.Write)。 name は NUL 終端の余地を残し 16 char まで。"""
    h = bytearray(40)
    h[0:4] = b"TAPE"
    nb = tape_name.encode("ascii")[:TapHeader_NAME_MAX - 1]
    h[4:4 + len(nb)] = nb
    h[0x1A] = 0x10   # WriteProtect = ProtectionWriteProtected (= Encode default)
    h[0x1B] = 0x01   # Format = FormatSampling
    h[0x1C] = sample_rate & 0xFF
    h[0x1D] = (sample_rate >> 8) & 0xFF
    h[0x1E] = (sample_rate >> 16) & 0xFF
    h[0x1F] = (sample_rate >> 24) & 0xFF
    h[0x20] = sample_count & 0xFF
    h[0x21] = (sample_count >> 8) & 0xFF
    h[0x22] = (sample_count >> 16) & 0xFF
    h[0x23] = (sample_count >> 24) & 0xFF
    # PositionBits (+0x24) = 0
    return bytes(h)


TapHeader_NAME_MAX = 17


def bin_to_tap(data: bytes, name: str, load: int, exec_addr: int,
               sample_rate: int = TAP_SAMPLE_RATE) -> bytes:
    """生 bin → .tap file 全体 byte 列。"""
    tape_name = name.upper()
    info = build_info_block(tape_name, "BIN", load, exec_addr, len(data))
    logical = build_logical_bits(info, data)
    samples = modulate(logical, sample_rate)
    header = build_tap_header(sample_rate, tape_name, len(samples))
    return header + pack_samples(samples)


def _parse_addr(s: str) -> int:
    s = s.strip()
    if s.lower().startswith("0x"):
        return int(s, 16)
    if s.startswith("$"):
        return int(s[1:], 16)
    return int(s, 10)


def main() -> int:
    p = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("input", help="入力生バイナリ")
    p.add_argument("-o", "--output", required=True, help="出力 .tap")
    p.add_argument("--load", required=True, help="load address ($100 / 0x100 / 256)")
    p.add_argument("--exec", dest="exec_addr", default=None,
                   help="exec address (省略時 = load)")
    p.add_argument("--name", default=None,
                   help="tape file 名 (省略時 = 入力 stem、 1..13 char ASCII)")
    p.add_argument("--sample-rate", type=int, default=TAP_SAMPLE_RATE,
                   help=f"TapHeader sample rate (default {TAP_SAMPLE_RATE})")
    args = p.parse_args()

    if not os.path.isfile(args.input):
        print(f"input not found: {args.input}", file=sys.stderr)
        return 1

    load = _parse_addr(args.load)
    exec_addr = _parse_addr(args.exec_addr) if args.exec_addr else load
    name = args.name or os.path.splitext(os.path.basename(args.input))[0]
    if not (1 <= len(name) <= 13) or any(ord(c) < 0x20 or ord(c) > 0x7E for c in name):
        print(f"invalid tape name '{name}' (= 1..13 char ASCII printable)", file=sys.stderr)
        return 1

    with open(args.input, "rb") as f:
        data = f.read()
    if not data:
        print("empty input bin", file=sys.stderr)
        return 1
    if len(data) > 0xFFFF:
        print(f"bin size {len(data)} exceeds 64KB", file=sys.stderr)
        return 1
    if load + len(data) - 1 > 0xFFFF:
        print(f"load ${load:04X} + size {len(data)} overflows 16-bit memory", file=sys.stderr)
        return 1

    tap = bin_to_tap(data, name, load, exec_addr, args.sample_rate)
    with open(args.output, "wb") as f:
        f.write(tap)
    print(f"wrote {args.output} ({len(data)} byte bin -> {len(tap)} byte tap, "
          f"name={name.upper()} load=${load:04X} exec=${exec_addr:04X})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
