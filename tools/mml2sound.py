#!/usr/bin/env python3
"""mml2sound.py — MML → libpc80mk2_sound byte data converter.

Converts a small MML subset to the byte stream consumed by
runtime/libpc80mk2_sound.asm (PC-8001mkII Mode II 3ch BEEP driver).

Driver byte format:
  - Length byte:  signed -1..-127  (= playback length 1..127 ticks)
  - Note byte:    0x00..0x71       (= TONE.O<N> + TONE.<note>, octave*12+semi)
  - Rest byte:    0x7F             (= TONE.REST, plays silence for current length)
  - End marker:   0x80
  Length byte sets LENDATA; subsequent notes inherit it until a new length
  byte appears. 1 tick = 1 VSYNC frame (~16.67 ms on 60 Hz).

MML syntax (subset):
  Channels:         @1 @2 @3 @SE ...     (any @<alnum> name)
  Notes:            c d e f g a b
  Sharp:            + or #               (e.g., c+ or c#)
  Flat:             - or _               (e.g., d- or d_)
  Octave set:       o<N>                 (1..6)
  Octave shift:     > (up) / < (down)
  Default length:   l<N>                 (1, 2, 4, 8, 16, 32, 64; whole=1)
  Per-note length:  c4 / r8              (overrides l for this note only)
  Dotted:           c4. / c4..           (1.5x / 1.75x)
  Rest:             r [length] [dot]
  Comments:         ; to end of line     (Z80 asm style)
                    // to end of line
                    /* ... */            (block comment)
  Channel ends at next @ marker or EOF; 0x80 is appended automatically.

Output modes:
  default     — ASM `.db` lines (compatible with #ASM block in .SL)
  --binary    — raw byte file per channel: <prefix>.@<chan>.bin
"""

import argparse
import re
import sys
from pathlib import Path

# Note letter → semitone offset within an octave (TONE.C=0, TONE.B=11)
NOTE_INDEX = {'c': 0, 'd': 2, 'e': 4, 'f': 5, 'g': 7, 'a': 9, 'b': 11}
NOTE_NAMES = ['C', 'CP', 'D', 'DP', 'E', 'F', 'FP', 'G', 'GP', 'A', 'AP', 'B']

REST_BYTE = 0x7F
END_BYTE = 0x80
MAX_TICKS = 127      # length byte is signed -1..-127
MAX_NOTE = 71        # 6 octaves * 12 - 1


class MMLError(Exception):
    pass


def strip_comments(text: str) -> str:
    text = re.sub(r'/\*.*?\*/', ' ', text, flags=re.DOTALL)
    text = re.sub(r'(?://|;)[^\n]*', '', text)
    return text


def line_col(src: str, pos: int):
    head = src[:pos]
    line = head.count('\n') + 1
    col = pos - (head.rfind('\n') + 1) + 1
    return line, col


def parse(text: str, ticks_per_quarter: int = 16):
    """Return {channel: [(ticks, note_byte_or_REST), ...]} preserving order."""
    src = strip_comments(text)
    n = len(src)
    pos = 0

    # ordered dict semantics via list of (name, events)
    channels = []
    chan_index = {}
    current = None
    octave = 4
    length_denom = 4  # default note length

    def whole_ticks():
        return ticks_per_quarter * 4

    def length_to_ticks(denom: int, dots: int) -> int:
        if denom <= 0 or whole_ticks() % denom != 0:
            raise MMLError(
                f'length {denom} does not divide whole-note ticks ({whole_ticks()}) evenly')
        base = whole_ticks() // denom
        ticks = base
        add = base
        for _ in range(dots):
            if add % 2:
                raise MMLError(f'dotted length {denom} would produce non-integer ticks')
            add //= 2
            ticks += add
        return ticks

    while pos < n:
        c = src[pos]
        if c.isspace():
            pos += 1
            continue

        if c == '@':
            m = re.match(r'@([A-Za-z0-9_]+)', src[pos:])
            if not m:
                ln, co = line_col(src, pos)
                raise MMLError(f'invalid @marker at line {ln} col {co}')
            name = m.group(1)
            if name not in chan_index:
                chan_index[name] = len(channels)
                channels.append((name, []))
            current = chan_index[name]
            # per-channel state reset (conventional MML behavior)
            octave = 4
            length_denom = 4
            pos += m.end()
            continue

        if current is None:
            ln, co = line_col(src, pos)
            raise MMLError(f"data before any @channel at line {ln} col {co}: {c!r}")

        if c == 'o':
            m = re.match(r'o(\d+)', src[pos:])
            if not m:
                ln, co = line_col(src, pos)
                raise MMLError(f'invalid octave at line {ln} col {co}')
            o = int(m.group(1))
            if not 1 <= o <= 6:
                ln, co = line_col(src, pos)
                raise MMLError(f'octave out of range 1..6 at line {ln} col {co}: {o}')
            octave = o
            pos += m.end()
            continue

        if c == '>':
            if octave >= 6:
                ln, co = line_col(src, pos)
                raise MMLError(f'octave overflow (>6) at line {ln} col {co}')
            octave += 1
            pos += 1
            continue
        if c == '<':
            if octave <= 1:
                ln, co = line_col(src, pos)
                raise MMLError(f'octave underflow (<1) at line {ln} col {co}')
            octave -= 1
            pos += 1
            continue

        if c == 'l':
            m = re.match(r'l(\d+)', src[pos:])
            if not m:
                ln, co = line_col(src, pos)
                raise MMLError(f'invalid length spec at line {ln} col {co}')
            length_denom = int(m.group(1))
            pos += m.end()
            continue

        if c in NOTE_INDEX or c == 'r':
            note_char = c
            pos += 1
            accidental = 0
            if pos < n and src[pos] in '+#':
                accidental = +1
                pos += 1
            elif pos < n and src[pos] in '-_':
                accidental = -1
                pos += 1
            m = re.match(r'(\d+)', src[pos:])
            if m:
                this_denom = int(m.group(1))
                pos += m.end()
            else:
                this_denom = length_denom
            dots = 0
            while pos < n and src[pos] == '.':
                dots += 1
                pos += 1

            ticks = length_to_ticks(this_denom, dots)
            if not 1 <= ticks <= MAX_TICKS:
                raise MMLError(
                    f'note ticks {ticks} out of range 1..{MAX_TICKS} '
                    f'(length {this_denom}{"."*dots})')

            if note_char == 'r':
                channels[current][1].append((ticks, REST_BYTE))
                continue

            semi = NOTE_INDEX[note_char] + accidental
            if not 0 <= semi <= 11:
                raise MMLError(
                    f'note {note_char}{"+" if accidental==1 else "-" if accidental==-1 else ""} '
                    f'crosses octave boundary; use explicit octave shift')
            note_byte = (octave - 1) * 12 + semi
            if not 0 <= note_byte <= MAX_NOTE:
                raise MMLError(f'note byte {note_byte} out of range 0..{MAX_NOTE}')
            channels[current][1].append((ticks, note_byte))
            continue

        ln, co = line_col(src, pos)
        raise MMLError(f"unexpected character {c!r} at line {ln} col {co}")

    return channels


def note_to_str(b: int) -> str:
    if b == REST_BYTE:
        return 'TONE.REST'
    octave = b // 12 + 1
    semi = b % 12
    return f'TONE.O{octave} + TONE.{NOTE_NAMES[semi]}'


def label_for(name: str) -> str:
    """Local-label form. Channel '1' becomes '.@1' to match existing convention."""
    return f'.@{name}'


def emit_asm_channel(name, events):
    lines = []
    cur_len = None
    first = True
    lbl = label_for(name)
    for ticks, b in events:
        note_str = note_to_str(b)
        if ticks != cur_len:
            cur_len = ticks
            payload = f'-{ticks}, {note_str}'
        else:
            payload = note_str
        if first:
            lines.append(f'{lbl}\tdb\t{payload}')
            first = False
        else:
            lines.append(f'\tdb\t{payload}')
    if first:
        lines.append(f'{lbl}\tdb\t0x{END_BYTE:02X}')
    else:
        lines.append(f'\tdb\t0x{END_BYTE:02X}')
    return lines


def emit_asm(channels, label: str, bgm_label: str) -> str:
    out = [f'{label}:']
    refs = ', '.join(label_for(n) for n, _ in channels)
    out.append(f'{bgm_label}:\tdw\t{refs}')
    for i, (name, events) in enumerate(channels):
        if i > 0:
            out.append('')  # blank line between channels (readability)
        out.extend(emit_asm_channel(name, events))
    return '\n'.join(out) + '\n'


def encode_channel_bytes(events) -> bytes:
    """Encode one channel to raw bytes with implicit length compression + 0x80 end."""
    out = bytearray()
    cur_len = None
    for ticks, b in events:
        if ticks != cur_len:
            cur_len = ticks
            out.append((-ticks) & 0xFF)
        out.append(b)
    out.append(END_BYTE)
    return bytes(out)


def main():
    ap = argparse.ArgumentParser(
        prog='mml2sound',
        description='MML → libpc80mk2_sound byte data converter',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    ap.add_argument('input', help='input MML text file (- for stdin)')
    ap.add_argument('-o', '--output', help='output ASM file (default: stdout)')
    ap.add_argument('--label', default='SOUNDDATA',
                    help='top-level label (default: SOUNDDATA)')
    ap.add_argument('--bgm-label', default='BGM',
                    help='dispatch label (default: BGM)')
    ap.add_argument('--quarter', type=int, default=16,
                    help='ticks per quarter note (default: 16; whole=64 ticks)')
    ap.add_argument('--binary', metavar='PREFIX',
                    help='write raw bytes per channel to PREFIX_<chan>.bin '
                         '(disables ASM output unless --output is also set)')
    args = ap.parse_args()

    if args.input == '-':
        text = sys.stdin.read()
    else:
        text = Path(args.input).read_text(encoding='utf-8')

    try:
        channels = parse(text, ticks_per_quarter=args.quarter)
    except MMLError as e:
        print(f'mml2sound: parse error: {e}', file=sys.stderr)
        return 1

    if not channels:
        print('mml2sound: no channels found (need at least one @<name> marker)',
              file=sys.stderr)
        return 1

    if args.binary:
        for name, events in channels:
            data = encode_channel_bytes(events)
            path = Path(f'{args.binary}_{name}.bin')
            path.write_bytes(data)
            print(f'wrote {path} ({len(data)} bytes)', file=sys.stderr)

    if args.output or not args.binary:
        asm = emit_asm(channels, label=args.label, bgm_label=args.bgm_label)
        if args.output:
            Path(args.output).write_text(asm, encoding='utf-8')
        else:
            sys.stdout.write(asm)

    return 0


if __name__ == '__main__':
    sys.exit(main())
