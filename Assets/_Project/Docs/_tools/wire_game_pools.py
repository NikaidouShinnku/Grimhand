#!/usr/bin/env python3
"""将 Excel 卡牌接入 ExpeditionSetup.PlayerCardCatalog 与各怪物 SkillPool。"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
JSON_PATH = ROOT / "Docs" / "_excel_authoritative.json"
CARDS_DIR = ROOT / "Data" / "Cards"
CHAR_DIR = ROOT / "Data" / "Characters"
EXPEDITION_SETUP = ROOT / "Data" / "Setups" / "ExpeditionSetup_Demo.asset"

PLAYER_OWNER = {"战士": "char_knight", "法老": "char_mage", "恶魔": "char_ranger"}
PLAYER_CHAR_IDS = {"char_knight", "char_mage", "char_ranger"}

# 与 repair_and_sync_cards.py 保持一致
from repair_and_sync_cards import (  # noqa: E402
    CARD_ID_BY_OWNER,
    CARD_ID_OVERRIDE,
    CARD_OWNER,
    NAME_ALIASES,
    decode_display,
    parse_boss_rows,
    parse_monster_rows,
    parse_player_rows,
    safe_int,
)


def read_guid(asset_path: Path) -> str:
    meta = asset_path.with_suffix(asset_path.suffix + ".meta")
    text = meta.read_text(encoding="utf-8")
    m = re.search(r"guid: (\w+)", text)
    if not m:
        raise ValueError(f"No guid in {meta}")
    return m.group(1)


def index_cards() -> tuple[dict[tuple[str, str], Path], dict[str, Path]]:
    by_owner_name: dict[tuple[str, str], Path] = {}
    by_id: dict[str, Path] = {}
    for path in CARDS_DIR.glob("Card_*.asset"):
        text = path.read_text(encoding="utf-8")
        m_name = re.search(r'DisplayName:\s*"([^"]*)"', text)
        m_owner = re.search(r"OwnerCharacterId:\s*(\S+)", text)
        m_id = re.search(r"CardId:\s*(\S+)", text)
        if m_name and m_owner:
            by_owner_name[(m_owner.group(1), decode_display(m_name.group(1)))] = path
        if m_id:
            by_id[m_id.group(1)] = path
    return by_owner_name, by_id


def resolve_card_path(
    owner: str,
    name: str,
    by_owner_name: dict[tuple[str, str], Path],
    by_id: dict[str, Path],
) -> Path | None:
    lookup = NAME_ALIASES.get(name, name)
    card_id = CARD_ID_OVERRIDE.get(name) or CARD_ID_BY_OWNER.get((owner, name))
    if card_id and card_id in by_id:
        return by_id[card_id]
    path = by_owner_name.get((owner, lookup)) or by_owner_name.get((owner, name))
    return path


def yaml_ref(guid: str) -> str:
    return f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n"


def replace_yaml_list(text: str, key: str, refs: list[str]) -> str:
    block = f"  {key}:\n" + "".join(yaml_ref(g) for g in refs)
    pattern = rf"  {key}:\n(?:  - .+\n)*"
    if re.search(pattern, text):
        return re.sub(pattern, block, text)
    return re.sub(rf"(  m_EditorClassIdentifier: .+\n)", rf"\1{block}", text, count=1)


def load_characters() -> dict[str, dict]:
    chars: dict[str, dict] = {}
    for path in CHAR_DIR.glob("Character_*.asset"):
        text = path.read_text(encoding="utf-8")
        m_id = re.search(r"CharacterId:\s*(\S+)", text)
        m_dn = re.search(r'DisplayName:\s*"([^"]*)"', text)
        m_team = re.search(r"Team:\s*(\d+)", text)
        if not m_id or not m_dn:
            continue
        display = decode_display(m_dn.group(1))
        chars[m_id.group(1)] = {
            "path": path,
            "display": display,
            "team": int(m_team.group(1)) if m_team else 1,
            "guid": read_guid(path),
        }
    return chars


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    data = json.loads(JSON_PATH.read_text(encoding="utf-8"))["data"]
    by_owner_name, by_id = index_cards()
    characters = load_characters()

    player_rows = parse_player_rows(data)
    monster_rows = parse_monster_rows(data) + parse_boss_rows(data)

    player_guids: list[str] = []
    player_missing: list[str] = []
    seen_player: set[str] = set()
    for info in player_rows:
        if info["name"] == "作者境的一击":
            continue
        path = resolve_card_path(info["owner"], info["name"], by_owner_name, by_id)
        if path is None:
            player_missing.append(f"{info['owner']}:{info['name']}")
            continue
        guid = read_guid(path)
        if guid in seen_player:
            continue
        seen_player.add(guid)
        player_guids.append(guid)

    skill_pools: dict[str, list[str]] = {}
    monster_missing: list[str] = []
    for info in monster_rows:
        path = resolve_card_path(info["owner"], info["name"], by_owner_name, by_id)
        if path is None:
            monster_missing.append(f"{info['owner']}:{info['name']}")
            continue
        guid = read_guid(path)
        qty = info.get("quantity", 1)
        skill_pools.setdefault(info["owner"], []).extend([guid] * max(1, qty))

    setup_text = EXPEDITION_SETUP.read_text(encoding="utf-8")
    setup_text = replace_yaml_list(setup_text, "PlayerCardCatalog", player_guids)

    enemy_guids: list[str] = []
    seen_enemy: set[str] = set()
    for char_id in sorted(skill_pools.keys()):
        meta = characters.get(char_id)
        if meta is None:
            print(f"  WARN: no Character asset for {char_id}")
            continue
        if meta["guid"] not in seen_enemy:
            seen_enemy.add(meta["guid"])
            enemy_guids.append(meta["guid"])
    setup_text = replace_yaml_list(setup_text, "MonsterCharacters", enemy_guids)
    EXPEDITION_SETUP.write_text(setup_text, encoding="utf-8")

    char_updated = 0
    for char_id, guids in skill_pools.items():
        meta = characters.get(char_id)
        if meta is None:
            print(f"  WARN: no Character asset for {char_id}")
            continue
        text = meta["path"].read_text(encoding="utf-8")
        new_text = replace_yaml_list(text, "SkillPool", guids)
        if new_text != text:
            meta["path"].write_text(new_text, encoding="utf-8")
            char_updated += 1
            print(f"  SkillPool [{meta['display']}] {len(guids)} entries")

    print(f"\nPlayerCardCatalog: {len(player_guids)} cards")
    print(f"MonsterCharacters: {len(enemy_guids)} characters")
    print(f"Updated {char_updated} character SkillPools")
    if player_missing:
        print(f"Missing player cards ({len(player_missing)}): {player_missing[:10]}")
    if monster_missing:
        print(f"Missing monster cards ({len(monster_missing)}): {monster_missing[:10]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
