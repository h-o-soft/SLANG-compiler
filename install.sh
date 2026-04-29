#!/bin/sh
# SLANG Compiler installer (Unix: Linux / macOS).
#
# 配布 zip 解凍 dir 直下で実行する想定 (= bin/, include/, runtime/, images/,
# tools/ が cwd にあること)。repo 直下から使う場合は `make publish-local` 後に
# 実行する。
#
# Usage: ./install.sh [options]
#   --prefix <path>      Install bin to <path>/bin (default: $HOME/.local)
#   --config-dir <path>  Install lib to <path>     (default: $HOME/.config/SLANG)
#   --dry-run            実行せず "DRY: ..." を表示するだけ
#   --verbose, -v        各ステップを stderr に出力
#   --force, -f          確認 prompt を skip (= CI 等の非対話用)
#   --uninstall          アンインストールモードに切替
#   --help, -h           この usage を表示
#
# `make install` は本 script を `--force` 付きで呼ぶ薄い wrapper。
# Make 経由は **uninstall 時の確認 prompt も出ない** ので注意。

set -eu

SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
cd "$SCRIPT_DIR"

# ---- arg parse ----
ACTION=install
PREFIX=""
CONFIG_DIR=""
DRY_RUN=0
VERBOSE=0
FORCE=0

usage() {
  sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//'
}

while [ $# -gt 0 ]; do
  case "$1" in
    --prefix)
      [ $# -ge 2 ] || { echo "Error: --prefix requires a value" >&2; exit 2; }
      [ -n "$2" ]  || { echo "Error: --prefix value cannot be empty" >&2; exit 2; }
      PREFIX="$2"; shift 2 ;;
    --config-dir)
      [ $# -ge 2 ] || { echo "Error: --config-dir requires a value" >&2; exit 2; }
      [ -n "$2" ]  || { echo "Error: --config-dir value cannot be empty" >&2; exit 2; }
      CONFIG_DIR="$2"; shift 2 ;;
    --dry-run)     DRY_RUN=1; shift ;;
    --verbose|-v)  VERBOSE=1; shift ;;
    --force|-f)    FORCE=1; shift ;;
    --uninstall)   ACTION=uninstall; shift ;;
    --help|-h)     usage; exit 0 ;;
    *)             echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

# ---- OS detect & default paths ----
case "$(uname -s)" in
  Darwin)               OS=macos ;;
  Linux)                OS=linux ;;
  MINGW*|MSYS*|CYGWIN*)
    echo "Warning: detected Unix-like Windows shell ($(uname -s))." >&2
    echo "         Native Windows users should use install.bat instead." >&2
    OS=winbash
    ;;
  *) echo "Unsupported OS: $(uname -s)" >&2; exit 1 ;;
esac

: "${PREFIX:=$HOME/.local}"
: "${CONFIG_DIR:=$HOME/.config/SLANG}"
BINDIR="$PREFIX/bin"

# ---- helper functions ----
log()  { [ "$VERBOSE" -eq 1 ] && echo "  $*" >&2; :; }
run()  {
  if [ "$DRY_RUN" -eq 1 ]; then
    echo "DRY: $*"
  else
    "$@"
  fi
}

confirm() {
  # $1: prompt message
  if [ "$FORCE" -eq 1 ]; then return 0; fi
  if [ ! -t 0 ]; then
    echo "Error: non-interactive shell detected. Use --force to skip prompts." >&2
    exit 1
  fi
  printf '%s [y/N]: ' "$1"
  read ans
  case "$ans" in [yY]*) return 0 ;; *) echo "Aborted." >&2; exit 1 ;; esac
}

# 危険 path guard: 絶対パス正規化してから完全一致で拒否
guard_path() {
  p="$1"
  if [ -d "$p" ]; then
    abs=$(cd "$p" && pwd)
  else
    case "$p" in /*) abs="$p" ;; *) abs="$(pwd)/$p" ;; esac
  fi
  # 末尾 / を除去 (= /home/foo/ と /home/foo を同一視)。
  # ただし `/` 単体は除去すると "" になり message 表示が壊れるので保持。
  [ "$abs" = "/" ] || abs="${abs%/}"
  case "$abs" in
    "" | "/" | "$HOME" | "/root" | "/root/.config" | "/home" | "/usr" | \
    "/etc" | "/var" | "/tmp" | "/opt")
      echo "Refusing to remove dangerous path: '$abs' (from '$p')" >&2
      exit 1 ;;
  esac
  case "$p" in "." | "..") echo "Refusing relative path: '$p'" >&2; exit 1 ;; esac
}

# ---- sanity check (= 配布 zip 解凍 dir で実行されているか) ----
for required in bin/slangc bin/slangbuild include runtime images tools; do
  if [ ! -e "$required" ]; then
    cat >&2 <<EOF
Error: '$required' not found in $SCRIPT_DIR.

This installer must be run from an extracted distribution directory.
For repo development, run 'make publish-local' first to populate bin/.
EOF
    exit 1
  fi
done

# ---- install ----
do_install() {
  echo "Installing SLANG to:"
  echo "  Binaries:  $BINDIR"
  echo "  Libraries: $CONFIG_DIR"

  if [ -e "$BINDIR/slangc" ] || [ -e "$BINDIR/slangbuild" ] || [ -d "$CONFIG_DIR" ]; then
    confirm "Existing installation found. Overwrite?"
  fi

  run mkdir -p "$BINDIR" "$CONFIG_DIR"

  log "copy bin/slangc -> $BINDIR/"
  run cp bin/slangc     "$BINDIR/"
  run chmod +x          "$BINDIR/slangc"
  log "copy bin/slangbuild -> $BINDIR/"
  run cp bin/slangbuild "$BINDIR/"
  run chmod +x          "$BINDIR/slangbuild"

  # ghost file 対策: サブディレクトリは staging copy → 既存削除 → rename
  # で原子的置換 (= 古い env file 等が残らない)
  STAGING=""
  if [ "$DRY_RUN" -eq 0 ]; then
    STAGING=$(mktemp -d "$CONFIG_DIR/.install.XXXXXX")
    trap '[ -n "${STAGING:-}" ] && rm -rf "$STAGING"' EXIT INT TERM
  fi
  for d in include runtime images tools; do
    log "stage $d -> $CONFIG_DIR/$d (atomic replace)"
    if [ "$DRY_RUN" -eq 1 ]; then
      echo "DRY: cp -R $d <staging>/"
      echo "DRY: rm -rf $CONFIG_DIR/$d"
      echo "DRY: mv <staging>/$d $CONFIG_DIR/$d"
    else
      cp -R "$d" "$STAGING/"
      rm -rf "$CONFIG_DIR/$d"
      mv "$STAGING/$d" "$CONFIG_DIR/$d"
    fi
  done

  echo "Installation complete!"
  case ":$PATH:" in
    *":$BINDIR:"*) ;;
    *)
      echo
      echo "Note: '$BINDIR' is not in your PATH. Add this to your shell rc:"
      echo "  export PATH=\"$BINDIR:\$PATH\""
      ;;
  esac
}

# ---- uninstall ----
do_uninstall() {
  echo "Uninstalling SLANG from:"
  echo "  Binaries:  $BINDIR/{slangc,slangbuild}"
  echo "  Libraries: $CONFIG_DIR (entire directory)"

  guard_path "$CONFIG_DIR"
  confirm "Continue?"

  log "rm -f $BINDIR/slangc $BINDIR/slangbuild"
  run rm -f  "$BINDIR/slangc" "$BINDIR/slangbuild"
  log "rm -rf $CONFIG_DIR"
  run rm -rf "$CONFIG_DIR"

  echo "Uninstallation complete!"
}

# ---- dispatch ----
if [ "$ACTION" = uninstall ]; then
  do_uninstall
else
  do_install
fi
