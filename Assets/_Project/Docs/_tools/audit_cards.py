#!/usr/bin/env python3
"""Audit card assets vs Excel authoritative data."""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
JSON_PATH = ROOT / "Docs" / "_excel_authoritative.json"
CARDS_DIR = ROOT / "Data" / "Cards"

RARITY = {"白": 0, "绿": 1, "蓝": 2, "紫": 3, "橙": 4, "橙/金": 4}


def decode_display(raw: str) -> str:
    if "\\u" in raw:
        try:
            return raw.encode("utf-8").decode("unicode_escape")
        except UnicodeDecodeError:
            pass
    return raw


def parse_asset(text: str) -> dict:
    m = re.search(r"Rarity: (\d+)", text)
    rarity = int(m.group(1)) if m else None
    kws: list[str] = []
    if "Keywords:" in text:
        kw_sec = text.split("Keywords:")[1].split("Actions:")[0]
        kws = re.findall(r"  - (\S+)", kw_sec)
    actions = text.split("Actions:")[1] if "Actions:" in text else ""
    return {
        "rarity": rarity,
        "keywords": kws,
        "splash": "SplashBehindTarget: 1" in actions,
        "poison": "StatusId: poison" in actions,
        "slow": "StatusId: slow" in actions,
        "sacrifice_dmg": bool(
            re.search(r"Type: 0\n    Target: 1\n    Value: \d+", actions)
        )
        and "sacrifice" in kws,
        "parry": "Condition: 1" in actions and "Type: 9" in actions,
    }


def load_excel_rows() -> dict[str, dict]:
    raw = json.loads(JSON_PATH.read_text(encoding="utf-8"))
    data = raw["data"]
    excel: dict[str, dict] = {}
    for row in data["卡牌"][1:]:
        if not row or len(row) < 7:
            continue
        name = (row[1] or "").strip()
        desc = (row[5] or "").strip()
        if not name or not desc or name == "卡牌名称":
            continue
        excel[name] = {"desc": desc, "color": (row[6] or "白").strip()}

    current_monster = ""
    in_cards = False
    for row in data["小怪设计"]:
        if not isinstance(row, list) or not row:
            continue
        first = (row[0] or "").strip()
        if first == "卡牌名称":
            in_cards = True
            continue
        if first == "角色名":
            in_cards = False
            continue
        if not in_cards:
            if first in {
                "鼠人", "锁链怨灵", "石像鬼", "蜘蛛贵妇", "石傀儡", "哥布林",
                "史莱姆", "骷髅兵", "骷髅精英", "幽灵", "幽灵精英", "绿皮巨魔", "巨翼蝙蝠",
            }:
                current_monster = first
            continue
        name = first
        desc = ((row[7] if len(row) > 7 else "") or "").strip()
        if not name or not desc:
            continue
        excel[name] = {"desc": desc, "color": (row[5] or "白").strip()}
    return excel


def main() -> None:
    excel = load_excel_rows()
    rarity_issues: list[tuple] = []
    mechanic_issues: list[tuple] = []

    for path in sorted(CARDS_DIR.glob("Card_*.asset")):
        text = path.read_text(encoding="utf-8")
        m = re.search(r'DisplayName: "([^"]+)"', text)
        if not m:
            continue
        name = decode_display(m.group(1))
        if name not in excel:
            continue
        ex = excel[name]
        asset = parse_asset(text)
        exp_r = RARITY.get(ex["color"], 0)
        if asset["rarity"] != exp_r:
            rarity_issues.append((name, ex["color"], exp_r, asset["rarity"], path.name))

        d = ex["desc"]
        if "献祭" in d and "sacrifice" not in asset["keywords"]:
            mechanic_issues.append((name, "missing sacrifice keyword"))
        if re.search(r"献祭\s*\d+\s*HP", d) and not asset["sacrifice_dmg"]:
            mechanic_issues.append((name, "missing self sacrifice damage"))
        if ("身后" in d or "身后位置" in d) and not asset["splash"]:
            mechanic_issues.append((name, "missing SplashBehindTarget"))
        if "中毒" in d and not asset["poison"] and "免疫" not in d:
            mechanic_issues.append((name, "missing poison action"))
        applies_slow = bool(re.search(r"(?:获得|施加|附加).*减速", d))
        if applies_slow and not asset["slow"]:
            mechanic_issues.append((name, "missing slow action"))
        if "应对攻击" in d and not asset["parry"]:
            mechanic_issues.append((name, "missing respond/parry actions"))

    out = ROOT / "Docs" / "_tools" / "audit_report.txt"
    lines = [f"=== RARITY MISMATCHES: {len(rarity_issues)} ==="]
    lines.extend(str(row) for row in rarity_issues)
    lines.append(f"\n=== MECHANIC ISSUES: {len(mechanic_issues)} ===")
    lines.extend(f"{a}: {b}" for a, b in mechanic_issues)
    out.write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote {out} ({len(rarity_issues)} rarity, {len(mechanic_issues)} mechanic)")


if __name__ == "__main__":
    main()
