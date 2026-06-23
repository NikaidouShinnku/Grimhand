#!/usr/bin/env python3
"""Audit code vs authoritative Excel export."""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
JSON_PATH = ROOT / "Docs" / "_excel_authoritative.json"
OUT = ROOT / "Docs" / "_excel_audit_report.txt"

TALENT_CATALOG = ROOT / "Scripts" / "Expedition" / "TalentCatalog.cs"
RELIC_DB = ROOT / "Scripts" / "Expedition" / "RelicDatabase.cs"


def load() -> dict:
    return json.loads(JSON_PATH.read_text(encoding="utf-8"))


def extract_talents(data: dict) -> list[tuple[str, int, int, str]]:
    rows = data["data"]["角色"]
    out = []
    for row in rows[3:]:
        if len(row) < 19:
            continue
        for cid, li, s1i, s2i in [
            ("knight", 16, 17, 18),
            ("mage", 19, 20, 21),
            ("ranger", 22, 23, 24),
        ]:
            lvl = row[li] if len(row) > li else None
            if lvl is None:
                continue
            try:
                lvl = int(float(lvl))
            except (TypeError, ValueError):
                continue
            for slot, idx in ((1, s1i), (2, s2i)):
                desc = row[idx] if len(row) > idx else None
                if desc:
                    out.append((cid, slot, lvl, str(desc).strip()))
    return out


def parse_talent_catalog(text: str) -> list[tuple[str, int, int, str, str]]:
    pattern = re.compile(
        r'Def\((\w+Id),\s*(\d+),\s*(\d+),\s*"([^"]+)",\s*"([^"]+)",\s*\n\s*"([^"]+)"\)',
        re.MULTILINE,
    )
    char_map = {"KnightId": "knight", "MageId": "mage", "RangerId": "ranger"}
    out = []
    for m in pattern.finditer(text):
        cid = char_map.get(m.group(1), m.group(1))
        slot, lvl = int(m.group(2)), int(m.group(3))
        out.append((cid, slot, lvl, m.group(5), m.group(6)))
    return out


def normalize(s: str) -> str:
    s = s.replace(" ", "").replace("％", "%").replace("＋", "+")
    s = re.sub(r"[，。；：、]", "", s)
    return s.lower()


def extract_relics(data: dict) -> dict[str, str]:
    rows = data["data"]["遗物"]
    out = {}
    for row in rows[2:]:
        if not row or not row[0] or row[0] == "遗物ID":
            continue
        rid = str(row[0]).strip()
        desc = (row[4] or "").strip().replace("\n", "")
        if rid and desc:
            out[rid] = desc
    return out


def parse_relic_db(text: str) -> dict[str, str]:
    pattern = re.compile(
        r'Def\(RelicIds\.(\w+),\s*"([^"]+)",[^,]+,[^,]+,\s*\n\s*"([^"]+)"',
        re.MULTILINE,
    )
    id_map = {
        "SunPyramid": "sun_pyramid",
        "KnightInCastle": "knight_in_castle",
        "BloodAlter": "blood_alter",
        "JadeStone": "jade_stone",
        "JadeRing": "jade_ring",
        "JadeDagger": "jade_dagger",
        "BurningBoots": "burning_boots",
        "CrimsonBurningBoots": "crimson_burning_boots",
        "FlameSword": "flame_sword",
        "IronArmor": "iron_armor",
        "WarriorHelmet": "warrior_helmet",
        "CatStatue": "cat_statue",
        "ElfBow": "elf_bow",
        "DragonRing": "dragon_ring",
        "PaladinShield": "paladin_shield",
        "SilverMoonPendant": "silver_moon_pendant",
        "TaichiRing": "taichi_ring",
        "LeafOfMiracle": "leaf_of_miracle",
    }
    out = {}
    for m in pattern.finditer(text):
        rid = id_map.get(m.group(1), m.group(1))
        out[rid] = m.group(3).replace("\n", "")
    return out


def main() -> None:
    data = load()
    lines: list[str] = []

    excel_t = extract_talents(data)
    code_t = parse_talent_catalog(TALENT_CATALOG.read_text(encoding="utf-8"))
    code_lookup = {(c, s, l): (title, desc) for c, s, l, title, desc in code_t}

    lines.append("=== TALENTS ===")
    for cid, slot, lvl, excel_desc in excel_t:
        title, code_desc = code_lookup.get((cid, slot, lvl), ("?", "?"))
        if normalize(excel_desc) != normalize(code_desc):
            lines.append(f"MISMATCH {cid} s{slot} lv{lvl} [{title}]")
            lines.append(f"  EXCEL: {excel_desc}")
            lines.append(f"  CODE:  {code_desc}")

    excel_r = extract_relics(data)
    code_r = parse_relic_db(RELIC_DB.read_text(encoding="utf-8"))

    lines.append("\n=== RELICS (in code) ===")
    for rid, excel_desc in excel_r.items():
        code_desc = code_r.get(rid)
        if code_desc is None:
            lines.append(f"MISSING IN CODE: {rid} | {excel_desc[:60]}")
        elif normalize(excel_desc) != normalize(code_desc):
            lines.append(f"MISMATCH {rid}")
            lines.append(f"  EXCEL: {excel_desc}")
            lines.append(f"  CODE:  {code_desc}")

    lines.append("\n=== RELICS (excel only, not in code db) ===")
    for rid in excel_r:
        if rid not in code_r:
            lines.append(f"  {rid}: {excel_r[rid][:80]}")

    OUT.write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote {OUT} ({len(lines)} lines)")


if __name__ == "__main__":
    main()
