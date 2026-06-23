#!/usr/bin/env python3
"""根据卡牌资产 Reach/Target 为 Excel 权威 JSON 的玩家牌描述补回位置标签（【前/中】等）。"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
JSON_PATH = ROOT / "Docs" / "_excel_authoritative.json"
CARDS_DIR = ROOT / "Data" / "Cards"
SHEET_EXPORT = ROOT / "Docs" / "卡牌_含位置描述.md"
DESC_CS = ROOT / "Scripts" / "Content" / "CardDescriptionCatalog.cs"

sys.path.insert(0, str(Path(__file__).resolve().parent))
from dump_cards import parse_card  # noqa: E402
from repair_and_sync_cards import (  # noqa: E402
    emit_description_catalog,
    index_assets,
    parse_monster_rows,
    parse_player_rows,
)

PLAYER_OWNERS = {"char_knight", "char_mage", "char_ranger"}

REACH_TAG = {
    "Any": "【前/中/后】",
    "FrontAndMiddle": "【前/中】",
    "BackOnly": "【后排】",
    "MiddleAndBack": "【中/后】",
}

# 需从描述开头剥离的旧位置/AOE 标签（保留【消耗】【献祭】【应对】等）
STRIP_LEADING = re.compile(
    r"^(?:【(?:前/中/后|前/中|中/后|后排|AOE)】)+"
)

ENEMY_TARGETS = {
    "DefaultEnemy",
    "ManualSelected",
    "EnemyFrontSlot",
    "EnemyMiddleSlot",
    "EnemyBackSlot",
}

ACTION_TYPES_WITH_REACH = {
    "DealDamage",
    "Heal",
    "GainBlock",
    "ApplyStatus",
    "RemoveStatus",
}


def should_show_reach(action: dict, pick_side: str = "Enemy") -> bool:
    target = action["target"]
    if target in ("AllEnemies", "RandomEnemy", "RandomEnemies", "Self"):
        return False
    if target in ("AllyFrontSlot", "AllyMiddleSlot", "AllyBackSlot", "FrontAlly", "BackAlly"):
        return False
    if action["type"] not in ACTION_TYPES_WITH_REACH:
        return False
    if pick_side == "Ally":
        return action.get("reach") != "Any"
    if pick_side == "Enemy" or target in ENEMY_TARGETS:
        return True
    return action.get("reach") != "Any"


def card_pick_side(card: dict) -> str:
    for action in card["actions"]:
        if action.get("condition") != "None":
            continue
        t = action["target"]
        if t in ("FrontAlly", "BackAlly"):
            return "Ally"
        if t in ENEMY_TARGETS or t == "ManualSelected":
            return "Enemy"
    return "None"


def compute_position_tags(card: dict, body: str = "") -> list[str]:
    if body and re.search(r"随机(?:使|对)?一?个?敌人", body):
        return []
    if body and "对随机敌人" in body:
        return []

    tags: list[str] = []
    pick_side = card_pick_side(card)

    for action in card["actions"]:
        if action.get("condition") != "None":
            continue
        target = action["target"]
        atype = action["type"]

        if target == "AllEnemies" and atype == "DealDamage":
            if "【AOE】" not in tags:
                tags.append("【AOE】")
            continue

        if target in ("RandomEnemy", "RandomEnemies"):
            continue

        if not should_show_reach(action, pick_side):
            continue

        tag = REACH_TAG.get(action.get("reach", "FrontAndMiddle"), "")
        if tag and tag not in tags:
            tags.append(tag)
            break

    # 有 aoe 关键词且全体伤害但未在上面的循环命中
    if "aoe" in card["keywords"] and "【AOE】" not in tags:
        for action in card["actions"]:
            if action.get("condition") != "None":
                continue
            if action["target"] == "AllEnemies" and action["type"] == "DealDamage":
                tags.insert(0, "【AOE】")
                break

    return tags


def strip_position_prefix(desc: str) -> str:
    text = (desc or "").strip()
    while True:
        nxt = STRIP_LEADING.sub("", text, count=1)
        if nxt == text:
            break
        text = nxt.lstrip()
    return text


def merge_description(card: dict, body: str) -> str:
    body = strip_position_prefix(body)
    pos_tags = compute_position_tags(card, body)
    if not pos_tags:
        return body
    prefix = "".join(pos_tags)
    if body.startswith("【"):
        return prefix + body
    return prefix + body


def build_card_lookup() -> dict[str, dict]:
    by_name: dict[str, dict] = {}
    by_id: dict[str, dict] = {}
    for path in CARDS_DIR.glob("Card_*.asset"):
        card = parse_card(path)
        if card["owner"] not in PLAYER_OWNERS:
            continue
        by_name[card["name"]] = card
        by_id[card["id"]] = card
    return by_name


def update_excel_sheet(data: dict, by_name: dict[str, dict]) -> list[tuple[str, str, str]]:
    changes: list[tuple[str, str, str]] = []
    for row in data["卡牌"]:
        if not isinstance(row, list) or len(row) < 6:
            continue
        role = (row[0] or "").strip()
        name = (row[1] or "").strip()
        if role not in ("战士", "法老", "恶魔") or not name:
            continue
        card = by_name.get(name)
        if not card:
            continue
        old = (row[5] or "").strip()
        new = merge_description(card, old)
        if new != old:
            row[5] = new
            changes.append((role, name, new))
    return changes


def export_markdown_sheet(data: dict) -> None:
    lines = [
        "# 卡牌 sheet（含位置描述）",
        "",
        "由 `restore_card_position_descriptions.py` 根据游戏内卡牌 Reach 自动生成。",
        "",
        "| 角色 | 卡牌名称 | 费用 | 类型 | 效果描述 | 稀有度 |",
        "| --- | --- | ---: | --- | --- | --- |",
    ]
    for row in data["卡牌"][1:]:
        if not isinstance(row, list) or len(row) < 7:
            continue
        role = (row[0] or "").strip()
        name = (row[1] or "").strip()
        if role not in ("战士", "法老", "恶魔") or not name or name == "卡牌名称":
            continue
        cost = row[2] if len(row) > 2 else ""
        ctype = row[3] if len(row) > 3 else ""
        desc = (row[5] or "").strip() if len(row) > 5 else ""
        rarity = (row[6] or "").strip() if len(row) > 6 else ""
        lines.append(f"| {role} | {name} | {cost} | {ctype} | {desc} | {rarity} |")
    SHEET_EXPORT.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    payload = json.loads(JSON_PATH.read_text(encoding="utf-8"))
    data = payload["data"]
    by_name = build_card_lookup()

    changes = update_excel_sheet(data, by_name)
    player = parse_player_rows(data)
    JSON_PATH.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    export_markdown_sheet(data)

    print(f"=== 更新 {len(changes)} 张玩家卡牌描述 ===")
    for role, name, new in changes:
        print(f"  [{role}] {name}")
        print(f"    -> {new}")

    # 重生成 CardDescriptionCatalog（不重建 YAML 动作，仅刷新描述表）
    catalog_entries: list[dict] = []
    for info in player:
        if info["name"] == "作者境的一击":
            continue
        card = by_name.get(info["name"])
        catalog_entries.append({
            "name": info["name"],
            "card_id": card["id"] if card else "",
            "desc": info["desc"],
        })
    monster = parse_monster_rows(data)
    assets = index_assets()
    for info in monster:
        path = assets.get(info["name"])
        card_id = ""
        if path and path.exists():
            m = re.search(r"CardId:\s*(\S+)", path.read_text(encoding="utf-8"))
            if m:
                card_id = m.group(1)
        catalog_entries.append({"name": info["name"], "card_id": card_id, "desc": info["desc"]})

    emit_description_catalog(catalog_entries)
    print(f"\n=== 已写入 {SHEET_EXPORT.relative_to(ROOT)} ===")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
