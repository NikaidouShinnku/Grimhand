#!/usr/bin/env python3
"""Remove legacy unused card keywords: melee, guard, far_shot, snipe."""

from __future__ import annotations

import re
from pathlib import Path

LEGACY = frozenset({"melee", "guard", "far_shot", "snipe"})
PROJECT = Path(__file__).resolve().parents[2]
REPO = PROJECT.parent.parent
CARDS = PROJECT / "Data" / "Cards"

CS_PATTERNS = [
    (re.compile(r", Kw\(\"melee\"\)"), ", null"),
    (re.compile(r", Kw\(\"guard\"\)"), ", null"),
    (re.compile(r", Kw\(\"far_shot\"\)"), ", null"),
    (re.compile(r", Kw\(\"snipe\"\)"), ", null"),
    (re.compile(r", TargetReach\.(\w+), \"melee\"\)"), r", TargetReach.\1)"),
    (re.compile(r", TargetReach\.(\w+), \"far_shot\"\)"), r", TargetReach.\1)"),
    (re.compile(r", TargetReach\.(\w+), \"snipe\"\)"), r", TargetReach.\1)"),
    (re.compile(r", CardType\.(\w+), \"guard\"\)"), r", CardType.\1)"),
    (re.compile(r"\"pierce\", \"melee\""), '"pierce"'),
]


def strip_asset_keywords(path: Path) -> bool:
    text = path.read_text(encoding="utf-8")
    lines = text.splitlines(keepends=True)
    kept: list[str] = []
    changed = False
    for line in lines:
        stripped = line.strip()
        if stripped.startswith("- ") and stripped[2:] in LEGACY:
            changed = True
            continue
        kept.append(line)

    new_text = "".join(kept)
    new_text, n = re.subn(r"  Keywords:\n(?!\s*-)", "  Keywords: []\n", new_text)
    changed = changed or n > 0

    if changed and new_text != text:
        path.write_text(new_text, encoding="utf-8")
        return True
    return False


def strip_cs_keywords(path: Path) -> bool:
    text = path.read_text(encoding="utf-8")
    new_text = text
    for pattern, repl in CS_PATTERNS:
        new_text = pattern.sub(repl, new_text)

    if new_text != text:
        path.write_text(new_text, encoding="utf-8")
        return True
    return False


def main() -> None:
    asset_count = 0
    for path in sorted(CARDS.glob("Card_*.asset")):
        if strip_asset_keywords(path):
            asset_count += 1
            print(f"asset: {path.name}")

    cs_roots = [
        PROJECT / "Scripts" / "Content" / "Editor",
        PROJECT / "Scripts" / "Expedition",
        PROJECT / "Scripts" / "Battle" / "Demo",
    ]
    cs_count = 0
    for root in cs_roots:
        if not root.exists():
            continue
        for path in sorted(root.rglob("*.cs")):
            if strip_cs_keywords(path):
                cs_count += 1
                print(f"cs: {path.relative_to(REPO)}")

    print(f"Done. Updated {asset_count} assets, {cs_count} C# files.")


if __name__ == "__main__":
    main()
