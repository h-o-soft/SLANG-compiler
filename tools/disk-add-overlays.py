#!/usr/bin/env python3
"""Legacy helper. New code should use `slangbuild --emit disk` instead.

Add overlay binaries to a d88 disk image as M0.BIN, M1.BIN, ...

Usage: disk-add-overlays.py <ndc> <d88> <target-prefix-dir>

Looks for <target-prefix-dir>PROG._m*.bin (output from slangbuild) and
stages each as M<N>.BIN under <target-prefix-dir>.staging/, then writes
them to the d88 via NDC P. Old M0..M9.BIN are deleted from the d88 first
(sample limited: assumes overlay count <= 10).

This is a *sample-only* helper for examples/MODTEST_RESIDENT.SL etc.
Cross-platform replacement for the previous .bat / .sh pair: the .bat
path didn't survive cmd.exe <-> sh.exe argument round-trips on Windows
make builds, and the .sh path required sh.exe to be on PATH.

Status (issue #157 Phase 1): The Makefile.dist disk_image target now
calls `slangbuild --emit disk` directly. This script is retained for
compatibility with user-side Makefiles / shell scripts that still
invoke it. Removal is deferred to a later phase.
"""
import glob
import re
import shutil
import subprocess
import sys
from pathlib import Path


def main(argv):
    if len(argv) < 4:
        print(f"Usage: {argv[0]} <ndc> <d88> <target-prefix-dir>", file=sys.stderr)
        return 1

    ndc, d88, prefix_dir = argv[1], argv[2], argv[3]
    stage_dir = Path(prefix_dir) / ".staging"

    try:
        # Delete leftover M0..M9.BIN from previous builds (best-effort:
        # NDC D returns non-zero when the entry is absent, which is fine).
        for n in range(10):
            subprocess.run(
                [ndc, "D", d88, "0", f"M{n}.BIN"],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                check=False,
            )

        # Stage and add overlay files. No matches is the common case for
        # samples that don't use #MODULE.
        stage_dir.mkdir(parents=True, exist_ok=True)
        pattern = re.compile(r"_m(\d+)\.bin$")
        for f in sorted(glob.glob(prefix_dir + "PROG._m*.bin")):
            m = pattern.search(f)
            if not m:
                continue
            staged = stage_dir / f"M{m.group(1)}.BIN"
            shutil.copyfile(f, staged)
            subprocess.run([ndc, "P", d88, "0", str(staged)], check=True)
    finally:
        if stage_dir.exists():
            shutil.rmtree(stage_dir, ignore_errors=True)

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
