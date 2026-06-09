#!/usr/bin/env sh
# Verify conformance manifests match current compiler output.
set -eu
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/tests/HlaX64.Compiler.Tests/HlaX64.Compiler.Tests.csproj"
CONFIG="${1:-Release}"

echo "Conformance: running ConformanceTests ($CONFIG)..."
dotnet test "$PROJECT" -c "$CONFIG" --filter "FullyQualifiedName~ConformanceTests" --nologo
echo "Conformance: all cases passed."
