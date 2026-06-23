#!/usr/bin/env python3
"""Sync Card_*.asset values from v0.8 Excel export (_v08_excel_full.json)."""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
JSON_PATH = ROOT / "Docs" / "_v08_excel_full.json"
CARDS_DIR = ROOT / "Data" / "Cards"

# Excel 卡牌名称 -> 资产 CardId（DisplayName 对不上时的兜底）
NAME_TO_ID = {
    "基础斩击": "w_basic_slash",
    "举盾格挡": "w_shield_block",
    "防御架势": "w_defensive_stance",
    "猛力劈砍": "w_power_cleave",
    "剑柄猛击": "w_pommel_strike",
    "嘲讽挑衅": "w_taunt",
    "铁壁弹反": "w_iron_parry",
    "战士冲锋": "w_charge",
    "剑刃风暴": "w_blade_storm",
    "战吼鼓舞": "w_war_cry",
    "誓死守护": "w_guardian",
    "致命打击": "w_fatal_strike",
    "不屈意志": "w_unyielding",
    "天神下凡": "w_god_descends",
    "沙暴射线": "p_sand_ray",
    "祈祷祝福": "p_bless",
    "法老诅咒": "p_pharaoh_curse",
    "太阳之怒": "p_solar_wrath",
    "生命汲取": "p_lifesteal",
    "法老权令": "p_decree",
    "亡灵诅咒": "p_undead_curse",
    "圣甲虫护盾": "p_scarab_shield",
    "沙尘结界": "p_sand_barrier",
    "复活祝福": "p_revive_bless",
    "日光审判": "p_solar_judgment",
    "暗影爪击": "d_shadow_claw",
    "恶魔之触": "d_devil_touch",
    "鲜血铠甲": "d_blood_armor",
    "血尾贯穿": "d_blood_tail",
    "血焰爆发": "d_blood_flame",
    "灵魂撕裂": "d_soul_rip",
    "暗黑献祭": "d_dark_sacrifice",
    "恶魔契约": "d_demon_pact",
    "吸血光环": "d_vamp_aura",
    "诅咒之链": "d_curse_chain",
    "地狱烈焰": "d_hell_fire",
    "魔王降临": "d_demon_lord",
    "无尽血刃": "d_endless_blade",
    "最终鲜血仪式": "d_final_blood_ritual",
}

RE_DAMAGE = re.compile(r"造成\s*(\d+)\s*点?伤害")
RE_BLOCK = re.compile(r"(?:获得|添加|添加)?\s*(\d+)\s*护甲")
RE_HEAL = re.compile(r"(?:治疗|回复)\s*(?:一名队友|自己|等量)?\s*(\d+)\s*HP", re.I)
RE_BONUS_HP = re.compile(r"额外\s*[+＋]\s*(\d+)")
RE_IGNORE_BLOCK = re.compile(r"无视目标\s*(\d+)%\s*护甲")
RE_HIT_COUNT = re.compile(r"重复\s*(\d+)\s*次")
RE_RESPOND_MIT = re.compile(r"获得\s*(\d+)%\s*减伤")
RE_STATUS_STACKS = re.compile(r"(\d+)\s*层")


def load_excel_cards() -> dict[str, list]:
    data = json.loads(JSON_PATH.read_text(encoding="utf-8"))
    rows = {}
    for row in data["卡牌"][1:]:
        name = (row[1] or "").strip()
        if name:
            rows[name] = row
    return rows


def decode_display_name(raw: str) -> str:
    if "\\u" in raw:
        return raw.encode("utf-8").decode("unicode_escape")
    return raw


def parse_asset(path: Path) -> dict:
    text = path.read_text(encoding="utf-8")
    card_id = re.search(r"CardId:\s*(\S+)", text)
    display = re.search(r'DisplayName:\s*"([^"]*)"', text)
    return {
        "path": path,
        "text": text,
        "card_id": card_id.group(1) if card_id else "",
        "display_name": decode_display_name(display.group(1)) if display else "",
    }


def patch_value(text: str, field: str, value: int) -> str:
    pattern = rf"({re.escape(field)}:\s*)(-?\d+)"
    if not re.search(pattern, text):
        return text
    return re.sub(pattern, rf"\g<1>{value}", text, count=1)


def patch_bool(text: str, field: str, value: bool) -> str:
    v = 1 if value else 0
    pattern = rf"({re.escape(field)}:\s*)([01])"
    return re.sub(pattern, rf"\g<1>{v}", text)


def apply_patches(text: str, patches: dict[str, int]) -> str:
    for field, value in patches.items():
        if field.startswith("ScaleWith"):
            text = patch_bool(text, field, bool(value))
        else:
            text = patch_value(text, field, value)
    return text


def infer_patches(desc: str, card_type: str) -> dict[str, int]:
    patches: dict[str, int] = {
        "ScaleWithAttack": 0,
        "ScaleWithDefense": 0,
    }

    m = RE_DAMAGE.search(desc)
    if m:
        patches["Value"] = int(m.group(1))

    m = RE_BLOCK.search(desc)
    if m and card_type in ("防御", "Defense"):
        patches["Value"] = int(m.group(1))

    m = RE_HEAL.search(desc)
    if m:
        patches["Value"] = int(m.group(1))

    m = RE_BONUS_HP.search(desc)
    if m:
        patches["BonusIfTargetHpBelowFlat"] = int(m.group(1))

    m = RE_IGNORE_BLOCK.search(desc)
    if m:
        patches["IgnoreDefPercent"] = int(m.group(1))

    m = RE_HIT_COUNT.search(desc)
    if m:
        patches["HitCount"] = int(m.group(1))

    m = RE_RESPOND_MIT.search(desc)
    if m and "应对" in desc:
        patches["Value"] = int(m.group(1))

    return patches


def sync_card(asset: dict, excel_row: list | None) -> bool:
    text = asset["text"]
    if excel_row is None:
        print(f"  SKIP (no excel): {asset['card_id']} {asset['display_name']}")
        return False

    desc = excel_row[5] or ""
    card_type = excel_row[3] or ""
    patches = infer_patches(desc, card_type)
    if len(patches) <= 2:
        print(f"  SKIP (no numeric patch): {asset['card_id']} | {desc[:40]}")
        return False

    new_text = apply_patches(text, patches)
    if new_text != text:
        asset["path"].write_text(new_text, encoding="utf-8")
        print(f"  OK {asset['card_id']}: {patches}")
        return True
    return False


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    excel = load_excel_cards()
    id_to_row = {}
    name_to_row = excel
    for name, row in excel.items():
        cid = NAME_TO_ID.get(name)
        if cid:
            id_to_row[cid] = row

    updated = 0
    total = 0
    for path in sorted(CARDS_DIR.glob("Card_*.asset")):
        total += 1
        asset = parse_asset(path)
        row = name_to_row.get(asset["display_name"]) or id_to_row.get(asset["card_id"])
        if sync_card(asset, row):
            updated += 1

    print(f"\nDone: updated {updated}/{total} card assets")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
