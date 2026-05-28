# RunCPM (bundled binaries)

[RunCPM](https://github.com/MockbaTheBorg/RunCPM) is a CP/M emulator
licensed under the MIT License. See `LICENSE` for full terms.

## Bundled binaries

Pre-built binaries are placed here for convenience:

- `RunCPM-macos-arm64` — macOS Apple Silicon
- `RunCPM-macos-x64`   — macOS Intel (cross-built with `-arch x86_64`)
- `RunCPM-linux-x64`   — Linux x86_64 (built on Linux host)
- `RunCPM-win-x64.exe` — Windows x86_64 (cross-built with x86_64-w64-mingw32-gcc)

All binaries were built from RunCPM's official upstream source
with one single modification: `BOOTONLY` in `globals.h` is set to
`TRUE` so that `AUTOEXEC.TXT` is consumed only on the first boot
(this is required for the SLANG Makefile's `run` target to run a
single .COM and drop to the interactive prompt instead of looping
AUTOEXEC forever).

## Wrapper script

`tools/runcpm.sh` (Unix) / `tools/runcpm.bat` (Windows) are wrappers
used by `make run ENV=cpm|lsx`. They:

1. Prepare a staging directory under the user's temp folder
2. Copy the target `.COM` into `A/0/<NAME>.COM`
3. Copy `cpm/SUBMIT.COM` and `cpm/EXIT.COM` into `A/0/`
4. Write a 2-line `A/0/BOOT.SUB` containing `<NAME>` then `EXIT` (CR/LF)
5. Write `SUBMIT BOOT` into `AUTOEXEC.TXT`
6. Launch the platform-appropriate RunCPM binary
7. The CCP runs `SUBMIT BOOT` (from AUTOEXEC), which expands `BOOT.SUB`
   into `$$$.SUB`. The CCP then executes the program and finally `EXIT`,
   which terminates RunCPM
8. Filter RunCPM's boot banner from stdout

Using SUBMIT/EXIT avoids relying on stdin redirection, which is
unreliable on Windows (RunCPM's Windows console build doesn't read
stdin via console pipe).

## Bundled CP/M utilities

`cpm/EXIT.COM` and `cpm/SUBMIT.COM` are taken from RunCPM's official
master disk image (`A0.ZIP`). Both are CP/M standard utilities (DRI
compatible). They are bundled here so the wrappers above can be
self-contained without requiring the user to download the master disk.

## Source and license

- Upstream:    https://github.com/MockbaTheBorg/RunCPM
- License:     MIT (see `LICENSE`)
- Copyright:   2017 Mockba the Borg
