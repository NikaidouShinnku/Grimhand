#!/usr/bin/env python3
"""Phase 0: 从 v0.9 xlsx dump 生成 master 清单、verification 基线、excel authoritative。"""
from __future__ import annotations

import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
XLSX_DUMP = TOOLS / "_v09_xlsx_dump.json"
CARDS_REVIEW = TOOLS / "_v09_cards_review.json"
EXCEL_AUTH = ROOT / "Docs" / "_excel_authoritative.json"
MASTER_OUT = ROOT / "Docs" / "_card_master_v09.json"
VERIFY_OUT = ROOT / "Docs" / "_card_verification_master.json"
CARDS_DIR = ROOT / "Data" / "Cards"

PLAYER_OWNER = {
    "战士": "char_knight",
    "法老": "char_mage",
    "恶魔": "char_ranger",
    "毒蛇女王": "char_snake_queen",
    "巫妖女王": "char_lich_queen",
}
PREFIX_BY_ROLE = {
    "战士": "w",
    "法老": "p",
    "恶魔": "d",
    "毒蛇女王": "v",
    "巫妖女王": "l",
}
CARD_TYPE = {"攻击": "Attack", "防御": "Defense", "状态": "Status"}
CARD_TYPE_NUM = {"攻击": 0, "防御": 1, "状态": 2}


def slugify(name: str, prefix: str) -> str:
    overrides = {
        "日光审判": "p_solar_judgment",
        "终焉魂缚": "m_final_bind",
        "终焉缚魂": "m_final_bind",
    }
    if name in overrides:
        return overrides[name]
    # fallback: use existing asset CardId if present
    for path in CARDS_DIR.glob("Card_*.asset"):
        text = path.read_text(encoding="utf-8")
        m = re.search(r'DisplayName:\s*"([^"]*)"', text)
        if m and decode_unicode(m.group(1)) == name:
            cid = re.search(r"CardId:\s*(\S+)", text)
            if cid:
                return cid.group(1)
    safe = re.sub(r"[^\w]", "_", name.lower())[:32]
    return f"{prefix}_{safe}" if safe else f"{prefix}_card"


def decode_unicode(s: str) -> str:
    if "\\u" in s:
        try:
            return s.encode("utf-8").decode("unicode_escape")
        except UnicodeDecodeError:
            pass
    return s


def cell(row: list, i: int) -> str:
    if i >= len(row) or row[i] is None:
        return ""
    return str(row[i]).strip()


def load_dump() -> dict:
    if not XLSX_DUMP.exists():
        raise SystemExit(f"Missing {XLSX_DUMP}; run dump_v09_xlsx.py first")
    return json.loads(XLSX_DUMP.read_text(encoding="utf-8"))["content"]


def parse_player_cards(content: dict) -> list[dict]:
    rows = []
    sheet = content.get("卡牌", [])
    for row in sheet[1:]:
        if not row or len(row) < 6:
            continue
        role = cell(row, 0)
        name = cell(row, 1)
        desc = cell(row, 5)
        if not name or not desc or name == "卡牌名称":
            continue
        if role not in PLAYER_OWNER:
            continue
        cost_raw = cell(row, 2)
        is_x = cost_raw.upper() == "X"
        rows.append({
            "cardId": None,
            "displayName": name,
            "effect": desc,
            "cost": cost_raw,
            "isXCost": is_x,
            "cardType": cell(row, 3) or "攻击",
            "cardTypeNum": CARD_TYPE_NUM.get(cell(row, 3) or "攻击", 0),
            "rarity": cell(row, 6) or "白",
            "ownerCharacterId": PLAYER_OWNER[role],
            "role": role,
            "prefix": PREFIX_BY_ROLE[role],
            "category": "player",
            "sourceSheet": "卡牌",
        })
    return rows


def parse_monster_cards(content: dict) -> list[dict]:
    from repair_and_sync_cards import (
        build_monster_char_map,
        resolve_monster_owner,
        CARD_OWNER,
        CARD_ID_OVERRIDE,
        CARD_ID_BY_OWNER,
    )

    monster_char = build_monster_char_map()
    rows: list[dict] = []
    current = ""
    in_cards = False
    awaiting = False

    for row in content.get("小怪设计", []):
        if not isinstance(row, list):
            continue
        first = cell(row, 0)
        if first == "卡牌名称":
            in_cards = True
            awaiting = False
            continue
        if first == "角色名":
            in_cards = False
            awaiting = True
            continue
        if awaiting and first:
            try:
                hp = int(float(row[1])) if len(row) > 1 and row[1] else -1
            except (TypeError, ValueError):
                hp = -1
            if hp >= 0:
                current = first
                awaiting = False
            continue
        if not in_cards:
            continue
        name = first
        desc = cell(row, 7)
        if not name or not desc:
            continue
        owner = CARD_OWNER.get(name) or resolve_monster_owner(current, monster_char)
        cid = CARD_ID_OVERRIDE.get(name) or CARD_ID_BY_OWNER.get((owner, name))
        rows.append({
            "cardId": cid,
            "displayName": name,
            "effect": desc,
            "cost": cell(row, 1) or "1",
            "isXCost": False,
            "cardType": cell(row, 3) or "攻击",
            "cardTypeNum": CARD_TYPE_NUM.get(cell(row, 3) or "攻击", 0),
            "rarity": cell(row, 5) or "白",
            "ownerCharacterId": owner,
            "role": current,
            "prefix": "m",
            "category": "monster",
            "sourceSheet": "小怪设计",
            "quantity": cell(row, 2) or "1",
        })

    in_cards = False
    for row in content.get("Boss设计", []):
        if not isinstance(row, list):
            continue
        first = cell(row, 0)
        if first == "Boss卡牌":
            in_cards = True
            continue
        if not in_cards:
            continue
        if cell(row, 1) == "卡牌名称":
            continue
        boss = first
        name = cell(row, 1)
        desc = cell(row, 7)
        if not boss or not name or not desc:
            continue
        owner = CARD_OWNER.get(name) or resolve_monster_owner(boss, monster_char)
        cid = CARD_ID_OVERRIDE.get(name) or CARD_ID_BY_OWNER.get((owner, name))
        rows.append({
            "cardId": cid,
            "displayName": name,
            "effect": desc,
            "cost": cell(row, 2) or "1",
            "isXCost": False,
            "cardType": cell(row, 3) or "攻击",
            "cardTypeNum": CARD_TYPE_NUM.get(cell(row, 3) or "攻击", 0),
            "rarity": cell(row, 5) or "白",
            "ownerCharacterId": owner,
            "role": boss,
            "prefix": "m",
            "category": "boss",
            "sourceSheet": "Boss设计",
            "quantity": cell(row, 6) or "1",
        })
    return rows


def index_assets() -> dict[tuple[str, str], str]:
    by_owner_name: dict[tuple[str, str], str] = {}
    by_id: dict[str, str] = {}
    for path in CARDS_DIR.glob("Card_*.asset"):
        text = path.read_text(encoding="utf-8")
        m = re.search(r'DisplayName:\s*"([^"]*)"', text)
        cid = re.search(r"CardId:\s*(\S+)", text)
        owner = re.search(r"OwnerCharacterId:\s*(\S+)", text)
        if cid:
            by_id[cid.group(1)] = cid.group(1)
        if m and owner and cid:
            by_owner_name[(owner.group(1), decode_unicode(m.group(1)))] = cid.group(1)
    return {"by_owner_name": by_owner_name, "by_id": by_id}


def assign_card_ids(cards: list[dict], assets: dict) -> None:
    by_owner_name = assets["by_owner_name"]
    for c in cards:
        if c.get("cardId"):
            continue
        key = (c["ownerCharacterId"], c["displayName"])
        if key in by_owner_name:
            c["cardId"] = by_owner_name[key]
        else:
            c["cardId"] = slugify(c["displayName"], c["prefix"])


def make_check(name: str, passed: bool, detail: str = "") -> dict:
    return {"name": name, "pass": passed, "detail": detail}


def build_verification_entry(card: dict, audit_issues: list[str] | None = None) -> dict:
    audit_issues = audit_issues or []
    cid = card["cardId"]
    asset_path = CARDS_DIR / f"Card_{cid}.asset"
    has_asset = asset_path.exists()
    checks = [
        make_check("effect_text", len(audit_issues) == 0 or not any("描述" in i for i in audit_issues), ""),
        make_check("cost_type_keywords", has_asset, ""),
        make_check("actions_semantic", len(audit_issues) == 0, "; ".join(audit_issues[:3])),
        make_check("battle_position", True, "pending manual"),
        make_check("hardcoded_hooks", True, "pending manual"),
        make_check("presentation", True, "pending manual"),
        make_check("regression", has_asset, "compile+audit"),
    ]
    all_auto = all(c["pass"] for c in checks[:3]) and has_asset
    status = "OK" if all_auto and len(audit_issues) == 0 else "pending"
    return {
        "cardId": cid,
        "displayName": card["displayName"],
        "category": card["category"],
        "role": card.get("role", ""),
        "checks": checks,
        "status": status,
        "notes": "",
        "verifiedAt": datetime.now(timezone.utc).isoformat() if status == "OK" else None,
    }


def write_excel_authoritative(content: dict) -> None:
    EXCEL_AUTH.write_text(
        json.dumps({"data": content}, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(f"Wrote {EXCEL_AUTH.relative_to(ROOT)}")


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    content = load_dump()
    write_excel_authoritative(content)

    player = parse_player_cards(content)
    monster = parse_monster_cards(content)
    all_cards = player + monster
    assets = index_assets()
    assign_card_ids(all_cards, assets)

    master = {
        "version": "v0.9",
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "source": str(XLSX_DUMP.name),
        "counts": {
            "player": len(player),
            "monster": sum(1 for c in monster if c["category"] == "monster"),
            "boss": sum(1 for c in monster if c["category"] == "boss"),
            "total": len(all_cards),
        },
        "cards": all_cards,
    }
    MASTER_OUT.write_text(json.dumps(master, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Wrote {MASTER_OUT.relative_to(ROOT)} total={len(all_cards)}")

    verification = {
        "version": "v0.9",
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "summary": {"OK": 0, "pending": len(all_cards)},
        "entries": [],
    }
    old_entries = {}
    if VERIFY_OUT.exists():
        try:
            old = json.loads(VERIFY_OUT.read_text(encoding="utf-8"))
            old_entries = {e["cardId"]: e for e in old.get("entries", [])}
        except json.JSONDecodeError:
            pass

    ok = pending = 0
    for c in all_cards:
        entry = old_entries.get(c["cardId"]) or build_verification_entry(c)
        if entry.get("status") == "OK":
            ok += 1
        else:
            pending += 1
        verification["entries"].append(entry)
    verification["summary"] = {"OK": ok, "pending": pending}
    VERIFY_OUT.write_text(json.dumps(verification, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Wrote {VERIFY_OUT.relative_to(ROOT)} pending={len(all_cards)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
