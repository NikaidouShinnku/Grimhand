#!/usr/bin/env python3
"""Sync Card_*.asset Rarity from Grimhand实际卡牌遗物总览表.xlsx (卡牌 sheet)."""
from __future__ import annotations

import re
import sys
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CARDS = ROOT / "Data" / "Cards"
XLSX = Path(r"c:\Users\Kelthuzad\Desktop\The Grimhands Asset\Grimhand实际卡牌遗物总览表.xlsx")

NS = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}

COLOR_TO_RARITY = {
    "白": 0,  # Common
    "绿": 1,  # Rare
    "蓝": 2,  # SuperRare（蓝框）
    "紫": 3,  # Epic（紫框，比蓝更稀有）
    "橙": 4,  # Legendary
    "金": 4,
    "橙/金": 4,
}


def col_row(ref: str) -> tuple[int, int]:
    m = re.match(r"([A-Z]+)([0-9]+)", ref)
    col = 0
    for c in m.group(1):
        col = col * 26 + (ord(c) - 64)
    return col, int(m.group(2))


def read_sheet_rows(z: zipfile.ZipFile, sheet_path: str) -> dict[int, dict[int, str]]:
    shared: list[str] = []
    if "xl/sharedStrings.xml" in z.namelist():
        root = ET.fromstring(z.read("xl/sharedStrings.xml"))
        for si in root.findall(".//m:si", NS):
            shared.append("".join(t.text or "" for t in si.findall(".//m:t", NS)))

    root = ET.fromstring(z.read(sheet_path))
    rows: dict[int, dict[int, str]] = {}
    for c in root.findall(".//m:c", NS):
        ref = c.attrib.get("r", "")
        if not ref:
            continue
        col, row = col_row(ref)
        v = c.find("m:v", NS)
        if v is None:
            continue
        val = v.text or ""
        if c.attrib.get("t") == "s":
            val = shared[int(val)]
        rows.setdefault(row, {})[col] = val.strip()
    return rows


def load_xlsx_rarities() -> dict[str, int]:
    with zipfile.ZipFile(XLSX) as z:
        wb = ET.fromstring(z.read("xl/workbook.xml"))
        rels = ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))
        rid_to = {rel.attrib["Id"]: rel.attrib["Target"] for rel in rels}

        sheet_path = None
        for sh in wb.findall(".//m:sheet", NS):
            if sh.attrib.get("name") == "卡牌":
                rid = sh.attrib[
                    "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"
                ]
                sheet_path = "xl/" + rid_to[rid].lstrip("/")
                break

        if not sheet_path:
            raise SystemExit("Sheet 卡牌 not found")

        rows = read_sheet_rows(z, sheet_path)

    by_name: dict[str, int] = {}
    for row_idx, cols in rows.items():
        if row_idx == 1:
            continue
        name = cols.get(2, "")
        color = cols.get(7, "")
        if not name or not color:
            continue
        rarity = COLOR_TO_RARITY.get(color)
        if rarity is None:
            continue
        by_name[name] = rarity
    return by_name


def unescape_display(s: str) -> str:
    if not s:
        return s
    if "\\u" in s:
        try:
            return bytes(s, "utf-8").decode("unicode_escape")
        except Exception:
            pass
    return s


def load_asset_names() -> dict[str, Path]:
    mapping: dict[str, Path] = {}
    for path in CARDS.glob("Card_*.asset"):
        text = path.read_text(encoding="utf-8")
        m = re.search(r'DisplayName: "(.+)"', text)
        if m:
            mapping[unescape_display(m.group(1))] = path
    return mapping


def set_rarity(path: Path, rarity: int) -> bool:
    text = path.read_text(encoding="utf-8")
    new_text, n = re.subn(r"^(\s*Rarity: )\d+\s*$", rf"\g<1>{rarity}", text, count=1, flags=re.M)
    if n == 0:
        return False
    if new_text != text:
        path.write_text(new_text, encoding="utf-8")
        return True
    return False


def main() -> None:
    if not XLSX.exists():
        raise SystemExit(f"Missing xlsx: {XLSX}")

    xlsx = load_xlsx_rarities()
    assets = load_asset_names()
    updated = 0
    missing = []

    for name, rarity in sorted(xlsx.items()):
        path = assets.get(name)
        if path is None:
            missing.append(name)
            continue
        if set_rarity(path, rarity):
            print(f"  {path.name}: {name} -> Rarity {rarity}")
            updated += 1

    print(f"Updated {updated} cards.")
    if missing:
        print(f"No asset for {len(missing)} xlsx rows (skipped):")
        for name in missing:
            print(f"  - {name}")


if __name__ == "__main__":
    main()
