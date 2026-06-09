#!/usr/bin/env sh
# Bootstrap helper for Linux / WSL — checks toolchain and runs hla64 doctor.
# Full install paths: docs/install.md
set -eu

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "HlaX64 bootstrap (Linux / WSL)"
echo "Install guide: docs/install.md"
echo ""

missing=0
if ! command -v dotnet >/dev/null 2>&1; then
  echo "Missing: .NET SDK — https://dotnet.microsoft.com/download/dotnet/10.0"
  missing=1
fi
if ! command -v nasm >/dev/null 2>&1; then
  echo "Missing: NASM — sudo apt install nasm  (or see docs/install.md)"
  missing=1
fi
if ! command -v gcc >/dev/null 2>&1; then
  echo "Missing: gcc — sudo apt install build-essential"
  missing=1
fi

echo ""
if [ "$missing" -eq 1 ]; then
  echo "Fix missing tools above, then re-run this script."
  exit 1
fi

dotnet run --project src/HlaX64.Cli -- doctor
exit $?
