#!/usr/bin/env python3
"""Diff v0.81 vs v0.9 卡牌 sheet for existing characters (战士/法老/恶魔)."""
from __future__ import annotations
import sys
from pathlib import Path

V081 = Path(__file__).resolve().parent / "_v081_dump" / "卡牌.txt"
V09 = Path(__file__).resolve().parent / "_v09_dump" / "卡牌.txt"
EXISTING = {"战士", "法老", "恶魔"}

def parse(path):
    rows = []
    lines = path.read_text(encoding="utf-8").splitlines()
    for ln in lines[1:]:  # skip header line "=== Sheet ..."
        if not ln.strip():
            continue
        cells = ln.split("\t")
        if len(cells) < 7:
            continue
        role, name, cost, ctype, _, desc, rarity = cells[0], cells[1], cells[2], cells[3], cells[4], cells[5], cells[6]
        if role not in EXISTING:
            continue
        rows.append((role, name, cost, ctype, desc, rarity))
    return rows

old = {(r[0], r[1]): r for r in parse(V081)}
new = {(r[0], r[1]): r for r in parse(V09)}

out = []
out.append("=== MODIFIED (existing cards with changes) ===")
for key in sorted(set(old) & set(new), key=lambda k: (k[0], k[1])):
    o, n = old[key], new[key]
    diffs = []
    for i, label in enumerate([None, None, "费用", "类型", "描述", "稀有度"]):
        if label is None:
            continue
        if o[i] != n[i]:
            diffs.append(f"  {label}: {o[i]!r} -> {n[i]!r}")
    if diffs:
        out.append(f"[{key[0]}] {key[1]}")
        out.extend(diffs)

out.append("")
out.append("=== ADDED (new cards in v0.9 for existing chars) ===")
for key in sorted(set(new) - set(old), key=lambda k: (k[0], k[1])):
    r = new[key]
    out.append(f"[{key[0]}] {key[1]}  费{r[2]} {r[3]} {r[4]}  稀有度{r[5]}")

out.append("")
out.append("=== REMOVED (in v0.81 but gone in v0.9) ===")
for key in sorted(set(old) - set(new), key=lambda k: (k[0], k[1])):
    r = old[key]
    out.append(f"[{key[0]}] {key[1]}  费{r[2]} {r[3]} {r[4]}")

report = "\n".join(out)
Path(__file__).resolve().parent.parent.joinpath("_v09_card_diff_existing.txt").write_text(report, encoding="utf-8")
sys.stdout.buffer.write(report.encode("utf-8"))
