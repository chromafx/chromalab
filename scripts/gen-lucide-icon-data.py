#!/usr/bin/env python3
"""Regenerate Controls/LucideIconData.cs from Assets/Icons/*.svg (Lucide subset)."""

from __future__ import annotations

import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[1]
ICONS = ROOT / "src/ChromaLab/Assets/Icons"
OUT = ROOT / "src/ChromaLab/Controls/LucideIconData.cs"


def circle_to_path(cx: float, cy: float, r: float) -> str:
    return f"M {cx-r},{cy} A {r},{r} 0 1 0 {cx+r},{cy} A {r},{r} 0 1 0 {cx-r},{cy}"


def rect_to_path(x: float, y: float, w: float, h: float, rx: float = 0, ry: float = 0) -> str:
    rx = float(rx or 0)
    ry = float(ry or rx or 0)
    if rx <= 0:
        return f"M {x},{y} H {x+w} V {y+h} H {x} Z"
    return (
        f"M {x+rx},{y} H {x+w-rx} A {rx},{ry} 0 0 1 {x+w},{y+ry} "
        f"V {y+h-ry} A {rx},{ry} 0 0 1 {x+w-rx},{y+h} "
        f"H {x+rx} A {rx},{ry} 0 0 1 {x},{y+h-ry} "
        f"V {y+ry} A {rx},{ry} 0 0 1 {x+rx},{y} Z"
    )


def line_to_path(x1: float, y1: float, x2: float, y2: float) -> str:
    return f"M {x1},{y1} L {x2},{y2}"


def parse_attrs(tag: str) -> dict[str, str]:
    return dict(re.findall(r'([a-zA-Z_:][a-zA-Z0-9:._-]*)="([^"]*)"', tag))


def svg_to_paths(text: str) -> list[str]:
    paths: list[str] = []
    for m in re.finditer(r"<(path|circle|rect|line)\b([^>]*)/?>", text):
        kind, attrs_s = m.group(1), m.group(2)
        a = parse_attrs(attrs_s)
        if kind == "path":
            paths.append(a["d"])
        elif kind == "circle":
            paths.append(circle_to_path(float(a["cx"]), float(a["cy"]), float(a["r"])))
        elif kind == "rect":
            paths.append(
                rect_to_path(
                    float(a["x"]),
                    float(a["y"]),
                    float(a["width"]),
                    float(a["height"]),
                    float(a.get("rx", 0) or 0),
                    float(a.get("ry", 0) or 0),
                )
            )
        elif kind == "line":
            paths.append(
                line_to_path(float(a["x1"]), float(a["y1"]), float(a["x2"]), float(a["y2"]))
            )
    return paths


def main() -> None:
    entries: list[str] = []
    for svg in sorted(ICONS.glob("*.svg")):
        paths = svg_to_paths(svg.read_text())
        arr = ", ".join('"' + p.replace("\\", "\\\\").replace('"', '\\"') + '"' for p in paths)
        entries.append(f'        ["{svg.stem}"] = [{arr}],')

    OUT.write_text(
        """/*
 * Copyright 2026 Ho Tzin Mein
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 *
 * Icon path data derived from Lucide Icons (ISC License).
 * See THIRD_PARTY_NOTICES.md. Regenerate: scripts/gen-lucide-icon-data.py
 */

namespace ChromaLab.Controls;

/// <summary>
/// Vendored Lucide icon path subsets (viewBox 0 0 24 24, stroke icons).
/// </summary>
internal static class LucideIconData
{
    public static readonly IReadOnlyDictionary<string, string[]> Paths =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
"""
        + "\n".join(entries)
        + """
        };
}
"""
    )
    print(f"wrote {OUT} ({len(entries)} icons)")


if __name__ == "__main__":
    main()
