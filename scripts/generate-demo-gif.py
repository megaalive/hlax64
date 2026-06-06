#!/usr/bin/env python3
"""Generate docs/assets/demo.gif — terminal-style HlaX64 demo (no external recorder)."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs" / "assets" / "demo.gif"

BG = (13, 17, 23)
FG = (201, 209, 217)
GREEN = (63, 185, 80)
YELLOW = (210, 153, 34)
RED = (248, 81, 73)
CYAN = (121, 192, 255)
MUTED = (110, 118, 129)

W, H = 920, 520
MARGIN = 24
LINE_H = 22
FRAMES_PER_CHAR = 1
HOLD_FRAMES = 45
SCENE_GAP = 12


def run_cli(args: list[str]) -> str:
    cmd = ["dotnet", "run", "--project", str(ROOT / "src" / "HlaX64.Cli"), "--", *args]
    r = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, timeout=120)
    out = (r.stdout or "") + (r.stderr or "")
    return out.strip() or f"(exit {r.returncode}, no output)"


def load_font(size: int):
    for name in ("Cascadia Mono", "Consolas", "Courier New"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def color_line(line: str) -> tuple[str, tuple[int, int, int]]:
    if line.startswith("  ✔"):
        return line, GREEN
    if line.startswith("  ✘"):
        return line, RED
    if line.startswith("---") or line.startswith("==="):
        return line, MUTED
    if line.startswith("; RUNTIME:") or "sys_write" in line:
        return line, CYAN
    if line.startswith("HlaX64") or line.startswith("0."):
        return line, YELLOW
    if line.startswith("$"):
        return line, FG
    return line, FG


def render_frame(lines: list[str], prompt: str, font) -> Image.Image:
    img = Image.new("RGB", (W, H), BG)
    draw = ImageDraw.Draw(img)
    draw.text((MARGIN, MARGIN - 2), "hla64 demo", font=font, fill=MUTED)
    y = MARGIN + LINE_H
    draw.text((MARGIN, y), prompt, font=font, fill=YELLOW)
    y += LINE_H + 4
    for line in lines:
        text, color = color_line(line)
        draw.text((MARGIN, y), text, font=font, fill=color)
        y += LINE_H
        if y > H - MARGIN:
            break
    return img


def animate_typing(full_lines: list[str], prompt: str, font) -> list[Image.Image]:
    frames: list[Image.Image] = []
    visible: list[str] = []
    for line in full_lines:
        visible.append("")
        for i in range(1, len(line) + 1):
            visible[-1] = line[:i]
            frames.append(render_frame(visible, prompt, font))
            if i == len(line):
                for _ in range(HOLD_FRAMES // max(len(full_lines), 1)):
                    frames.append(render_frame(visible, prompt, font))
    for _ in range(HOLD_FRAMES):
        frames.append(render_frame(full_lines, prompt, font))
    return frames


def main() -> int:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    font = load_font(16)

    version = run_cli(["--version"]).splitlines()[0]
    doctor = run_cli(["doctor"])
    explain = run_cli(["explain", "examples/00-getting-started/hello.hla64"])
    explain_lines = explain.splitlines()[:18]

    run_sim = [
        "$ hla64 run examples/00-getting-started/hello.hla64",
        "Hello from HlaX64",
        "(exit 0)",
    ]

    scenes: list[Image.Image] = []
    scenes += animate_typing([version], "$ hla64 --version", font)
    scenes += [render_frame([], "", font)] * SCENE_GAP
    scenes += animate_typing(doctor.splitlines()[:10], "$ hla64 doctor", font)
    scenes += [render_frame([], "", font)] * SCENE_GAP
    scenes += animate_typing(explain_lines, "$ hla64 explain hello.hla64", font)
    scenes += [render_frame([], "", font)] * SCENE_GAP
    scenes += animate_typing(run_sim[1:], run_sim[0], font)

    duration = max(80, 6000 // len(scenes))
    scenes[0].save(
        OUT,
        save_all=True,
        append_images=scenes[1:],
        duration=duration,
        loop=0,
        optimize=True,
    )
    print(f"Wrote {OUT} ({len(scenes)} frames)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
