#!/usr/bin/env python3
"""One-shot path rewrite after examples/ category reorganization."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

REPLACEMENTS = [
    ("examples/20-project-euler", "examples/project-euler"),
    ("examples/10-real-tools", "examples/tools/10-windows"),
    ("examples/12-real-tools-linux", "examples/tools/12-linux"),
    ("examples/11-csharp-interop-real", "examples/interop/11-csharp-interop-real"),
    ("examples/98-bug-farm", "examples/qa/bug-farm"),
    ("examples/99-invalid", "examples/qa/invalid"),
    ("examples/09-benchmarks", "examples/benchmarks"),
    ("examples/08-ai-agent", "examples/curriculum/08-ai-agent"),
    ("examples/07-interop", "examples/interop/07-interop"),
    ("examples/06-abi", "examples/curriculum/06-abi"),
    ("examples/05-memory", "examples/curriculum/05-memory"),
    ("examples/04-procedures", "examples/curriculum/04-procedures"),
    ("examples/03-control-flow", "examples/curriculum/03-control-flow"),
    ("examples/02-types", "examples/curriculum/02-types"),
    ("examples/01-arithmetic", "examples/curriculum/01-arithmetic"),
    ("examples/00-getting-started", "examples/curriculum/00-getting-started"),
]

SKIP_DIRS = {
    ".git",
    "bin",
    "obj",
    "node_modules",
    "__pycache__",
    "build",
    "agent-tools",
    "agent-transcripts",
}

TEXT_SUFFIXES = {
    ".md",
    ".json",
    ".yml",
    ".yaml",
    ".ps1",
    ".py",
    ".cs",
    ".html",
    ".hla64",
    ".toml",
    ".arguments",
    ".txt",
    ".hs",
}


def should_scan(path: Path) -> bool:
    if any(part in SKIP_DIRS for part in path.parts):
        return False
    return path.suffix.lower() in TEXT_SUFFIXES or path.name in {
        "ci.yml",
        "CONTRIBUTING.md",
    }


def main() -> None:
    changed = 0
    for path in ROOT.rglob("*"):
        if not path.is_file() or not should_scan(path):
            continue
        if path.name == "update-example-paths.py":
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except (UnicodeDecodeError, OSError):
            continue
        original = text
        for old, new in REPLACEMENTS:
            text = text.replace(old, new)
        if text != original:
            path.write_text(text, encoding="utf-8", newline="\n")
            changed += 1
            print(path.relative_to(ROOT))
    print(f"Updated {changed} files")


if __name__ == "__main__":
    main()
