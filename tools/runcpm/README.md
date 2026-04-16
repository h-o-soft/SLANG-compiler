# RunCPM (bundled binaries)

[RunCPM](https://github.com/MockbaTheBorg/RunCPM) is a CP/M emulator
licensed under the MIT License. See `LICENSE` for full terms.

## Bundled binaries

Pre-built binaries are placed here for convenience:

- `RunCPM-macos-arm64` — macOS Apple Silicon
- `RunCPM-macos-x64`   — macOS Intel
- `RunCPM-linux-x64`   — Linux x86_64
- `RunCPM-win-x64.exe` — Windows x86_64

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
3. Write the .COM's basename into `AUTOEXEC.TXT`
4. Launch the platform-appropriate RunCPM binary
5. Pipe `EXIT` into stdin so RunCPM shuts down after the program returns
6. Filter RunCPM's boot banner from stdout

## Source and license

- Upstream:    https://github.com/MockbaTheBorg/RunCPM
- License:     MIT (see `LICENSE`)
- Copyright:   2017 Mockba the Borg
