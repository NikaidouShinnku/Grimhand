#!/usr/bin/env python3
"""从 Excel 导出遗物描述对照表，供手工/半自动更新 RelicDatabase 核对。"""
import json, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
data = json.loads((ROOT / "Docs" / "_v08_excel_full.json").read_text(encoding="utf-8"))
sys.stdout.reconfigure(encoding="utf-8")
for row in data["遗物"][2:]:
    if not row or len(row) < 5:
        continue
    rid, name, rarity, cat, desc = row[0], row[1], row[2], row[3], row[4]
    if rid and rid.endswith("_") is False and "." not in str(rid):
        print(f"{rid}|{name}|{desc[:80]}")
