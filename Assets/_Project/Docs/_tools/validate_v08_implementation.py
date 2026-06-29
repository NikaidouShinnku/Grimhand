#!/usr/bin/env python3
"""Validate repo implementation against Grimhand v0.8 overview xlsx."""
from __future__ import annotations

import importlib.util
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CARDS_DIR = ROOT / "Data" / "Cards"
XLSX = Path(r"c:\Users\Kelthuzad\Desktop\Grimhand实际内容总览表v0.8.xlsx")

NAME_ALIASES = {"日光审判": "太阳审判", "太阳审判": "日光审判"}


def load_apply_module():
    path = ROOT / "Docs" / "_tools" / "apply_v08_overview.py"
    spec = importlib.util.spec_from_file_location("apply_v08", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def decode_name(raw: str) -> str:
    if "\\u" in raw:
        try:
            return raw.encode("utf-8").decode("unicode_escape")
        except UnicodeDecodeError:
            pass
    return raw


def parse_asset_actions(text: str) -> list[dict]:
    actions: list[dict] = []
    for block in re.finditer(r"  - Type: (\d+)\n(.*?)(?=\n  - Type:|\n  CardArt:)", text, re.S):
        action: dict = {"Type": int(block.group(1))}
        for field in (
            "Target", "Value", "StatusId", "Stacks", "Duration", "HitCount",
            "IgnoreDefPercent", "SplashBehindTarget", "SplashPowerPercent",
            "SelfDamageFlat", "LifestealPercent", "OnKillHealAmount",
        ):
            m = re.search(rf"    {field}: (.+)", block.group(0))
            if not m:
                continue
            val = m.group(1).strip()
            if field in ("StatusId",):
                action[field] = val
            elif field in ("SplashBehindTarget",):
                action[field] = val == "1"
            else:
                try:
                    action[field] = int(val)
                except ValueError:
                    action[field] = val
        actions.append(action)
    return actions


def asset_lookup_name(name: str, assets: dict) -> dict | None:
    for key in (name, NAME_ALIASES.get(name, name)):
        if key in assets["name"]:
            return assets["name"][key]
    return None


INITIAL_DECKS: dict[str, list[tuple[str, int]]] = {
    "char_knight": [
        ("w_basic_slash", 3),
        ("w_shield_block", 2),
        ("w_defensive_stance", 1),
        ("w_iron_parry", 1),
        ("w_author_realm_strike", 1),
    ],
    "char_mage": [
        ("p_sand_ray", 3),
        ("p_bless", 2),
        ("p_scarab_shield", 1),
        ("p_undead_curse", 1),
    ],
    "char_ranger": [
        ("d_shadow_claw", 3),
        ("d_blood_armor", 2),
        ("d_devil_touch", 1),
        ("d_blood_tail", 1),
    ],
}


def validate_initial_decks() -> list[str]:
    issues: list[str] = []
    char_dir = ROOT / "Data" / "Characters"
    id_to_guid: dict[str, str] = {}
    for meta in (ROOT / "Data" / "Cards").glob("Card_*.asset.meta"):
        card_id = meta.name.replace("Card_", "").replace(".asset.meta", "")
        for line in meta.read_text(encoding="utf-8").splitlines():
            if line.startswith("guid:"):
                id_to_guid[card_id.replace("Card_", "")] = line.split(":", 1)[1].strip()
                break

    char_files = {
        "char_knight": char_dir / "Character_Knight.asset",
        "char_mage": char_dir / "Character_Mage.asset",
        "char_ranger": char_dir / "Character_Ranger.asset",
    }
    for char_id, path in char_files.items():
        if not path.exists():
            issues.append(f"角色资产缺失: {path.name}")
            continue
        text = path.read_text(encoding="utf-8")
        guids = re.findall(r"guid: ([0-9a-f]+)", text.split("Deck:", 1)[-1].split("SkillPool:", 1)[0])
        actual: dict[str, int] = {}
        guid_to_id = {v: k for k, v in id_to_guid.items()}
        for g in guids:
            cid = guid_to_id.get(g, g)
            actual[cid] = actual.get(cid, 0) + 1
        expected = {cid: n for cid, n in INITIAL_DECKS[char_id]}
        if actual != expected:
            issues.append(
                f"初始牌组不符 [{char_id}] 期望={expected} 实际={actual}"
            )
    return issues


def validate_yaml_integrity() -> list[str]:
    issues: list[str] = []
    for path in CARDS_DIR.glob("Card_*.asset"):
        lines = path.read_text(encoding="utf-8").splitlines()
        for i, line in enumerate(lines):
            if line.strip() in ("poison", "slow", "burn") and i > 0 and "StatusId" in lines[i - 1]:
                issues.append(f"YAML 损坏: {path.name}:{i + 1} 裸状态名 '{line.strip()}'")
    return issues


def validate_card_values(apply_mod) -> list[str]:
    issues: list[str] = []
    data = apply_mod.load_workbook_data()
    assets = apply_mod.index_assets()
    player = apply_mod.parse_player_cards(data)
    monster = apply_mod.parse_monster_cards(data)
    boss = apply_mod.parse_boss_cards(data)

    all_cards: dict[str, str] = {}
    all_cards.update({k: v["desc"] for k, v in player.items()})
    all_cards.update(monster)
    all_cards.update(boss)

    for name, desc in sorted(all_cards.items()):
        entry = asset_lookup_name(name, assets)
        if not entry:
            issues.append(f"卡牌缺失资产: {name}")
            continue
        text = entry["path"].read_text(encoding="utf-8")
        if entry["id"] in SPECIAL_EMPTY_ACTION_CARDS or entry["id"] in MECHANIC_ACTION_CARDS:
            continue
        expected = apply_mod.infer_patches(desc)
        actions = parse_asset_actions(text)
        if not actions:
            issues.append(f"卡牌无 Actions: {name}")
            continue

        primary = actions[0]
        if "Value" in expected and primary.get("Value") != expected["Value"]:
            issues.append(
                f"数值不符 [{name}] 期望 Value={expected['Value']} 实际={primary.get('Value')} "
                f"({entry['path'].name})"
            )
        if "HitCount" in expected and primary.get("HitCount") != expected["HitCount"]:
            issues.append(
                f"HitCount 不符 [{name}] 期望={expected['HitCount']} 实际={primary.get('HitCount')}"
            )
        if "IgnoreDefPercent" in expected and primary.get("IgnoreDefPercent") != expected["IgnoreDefPercent"]:
            issues.append(
                f"IgnoreDef 不符 [{name}] 期望={expected['IgnoreDefPercent']} "
                f"实际={primary.get('IgnoreDefPercent')}"
            )

        if "StatusId" in expected:
            status_actions = [a for a in actions if a.get("Type") == 3]
            if not status_actions:
                issues.append(f"缺少 ApplyStatus 动作: {name}")
                continue
            sa = status_actions[0]
            if sa.get("StatusId") != expected["StatusId"]:
                issues.append(
                    f"StatusId 不符 [{name}] 期望={expected['StatusId']} 实际={sa.get('StatusId')}"
                )
            if "Stacks" in expected and sa.get("Stacks") != expected["Stacks"]:
                issues.append(
                    f"Stacks 不符 [{name}] 期望={expected['Stacks']} 实际={sa.get('Stacks')}"
                )

    return issues


def validate_descriptions(apply_mod) -> list[str]:
    issues: list[str] = []
    desc_path = ROOT / "Scripts" / "Content" / "CardDescriptionCatalog.cs"
    text = desc_path.read_text(encoding="utf-8")
    data = apply_mod.load_workbook_data()
    assets = apply_mod.index_assets()
    player = apply_mod.parse_player_cards(data)
    monster = apply_mod.parse_monster_cards(data)
    boss = apply_mod.parse_boss_cards(data)
    all_cards: dict[str, str] = {}
    all_cards.update({k: v["desc"] for k, v in player.items()})
    all_cards.update(monster)
    all_cards.update(boss)

    for name, desc in all_cards.items():
        lookup = NAME_ALIASES.get(name, name)
        entry = assets["name"].get(lookup) or assets["name"].get(name)
        if not entry:
            continue
        pattern = rf'\["{re.escape(entry["id"])}"\]\s*=\s*"([^"]*)"'
        m = re.search(pattern, text)
        if not m:
            issues.append(f"CardDescriptionCatalog 缺条目: {name} ({entry['id']})")
            continue
        catalog_desc = m.group(1).replace("\\n", "\n")
        norm_desc = re.sub(r"\s+", "", desc)
        norm_cat = re.sub(r"\s+", "", catalog_desc)
        if norm_desc != norm_cat:
            issues.append(f"描述不符 [{name}]")
    return issues


def validate_relics() -> list[str]:
    issues: list[str] = []
    relic_db = (ROOT / "Scripts" / "Expedition" / "RelicDatabase.cs").read_text(encoding="utf-8")
    checks = [
        ("holysun_spellbook", "HolysunSpellbookBonusUpgradeLevels = 3"),
        ("post_battle_heal_3pct", "PostBattleTeamHealPercent += 3f"),
        ("front_armor_15", "BattleStartFrontBlock += 15"),
        ("WarriorFirstHitBlockAmount += 12", "WarriorFirstHitBlockAmount += 12"),
    ]
    for token, snippet in checks:
        if snippet not in relic_db and token not in relic_db:
            issues.append(f"RelicDatabase 缺: {token}")
    if "HolysunSpellbook" not in relic_db:
        issues.append("RelicDatabase 未注册 HolysunSpellbook")
    return issues


SPECIAL_EMPTY_ACTION_CARDS = {
    "p_solar_god_wrath",
    "p_solar_blessing",
}

MECHANIC_ACTION_CARDS = {
    "w_guardian",
    "m_bat_shadow_dodge",
    "m_queen_command",
}


def validate_scaling_and_xp() -> list[str]:
    issues: list[str] = []
    scaling = (ROOT / "Scripts" / "Expedition" / "EnemyFloorScaling.cs").read_text(encoding="utf-8")
    if "0.6" in scaling:
        issues.append("EnemyFloorScaling 仍含额外 ×0.6 倍率")
    if "DamageBonusEveryFloors = 3" not in scaling:
        issues.append("EnemyFloorScaling 伤害每3层+1 未配置")
    if "BlockBonusEveryFloors = 5" not in scaling:
        issues.append("EnemyFloorScaling 护甲每5层+1 未配置")
    if "HpBonusPerFloor = 1.5f" not in scaling:
        issues.append("EnemyFloorScaling HP 每层+1.5 未配置")

    xp = (ROOT / "Scripts" / "Expedition" / "CombatXpRules.cs").read_text(encoding="utf-8")
    for val in ("40", "60", "80"):
        if val not in xp:
            issues.append(f"CombatXpRules Boss XP {val} 未找到")

    mods = (ROOT / "Scripts" / "Battle" / "Model" / "RunModifierSnapshot.cs").read_text(encoding="utf-8")
    if "HolysunSpellbookBonusUpgradeLevels" not in mods:
        issues.append("RunModifierSnapshot 缺 HolysunSpellbookBonusUpgradeLevels")

    guard = (ROOT / "Scripts" / "Battle" / "Rules" / "CombatMechanicsRules.cs").read_text(encoding="utf-8")
    if "GuardDamageReductionPercent = 50" not in guard and "50" not in guard:
        issues.append("誓死守护 50% 减伤未确认")
    return issues


def validate_character_hp(apply_mod) -> list[str]:
    issues: list[str] = []
    prog = (ROOT / "Scripts" / "Expedition" / "CharacterProgression.cs").read_text(encoding="utf-8")
    expected = {"战士": 80, "法老": 60, "恶魔": 45}
    data = apply_mod.load_workbook_data()
    for row in data["角色"]:
        if not row or row[0] != "1":
            continue
        # Lv1: col1 warrior hp, col4 mage, col7 demon
        if len(row) > 7:
            pairs = [(row[1], "战士"), (row[4], "法老"), (row[7], "恶魔")]
            for hp, role in pairs:
                if hp.isdigit() and int(hp) != expected[role]:
                    issues.append(f"Excel Lv1 {role} HP={hp} 与基准 {expected[role]} 不一致")
                if hp.isdigit() and hp not in prog:
                    issues.append(f"CharacterProgression 缺 Lv1 {role} HP={hp}")
    return issues


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    if not XLSX.exists():
        print(f"ERROR: 找不到 {XLSX}")
        return 1

    apply_mod = load_apply_module()
    all_issues: list[str] = []
    all_issues += validate_yaml_integrity()
    all_issues += validate_initial_decks()
    all_issues += validate_card_values(apply_mod)
    all_issues += validate_descriptions(apply_mod)
    all_issues += validate_relics()
    all_issues += validate_scaling_and_xp()
    all_issues += validate_character_hp(apply_mod)

    out = ROOT / "Docs" / "_v08_validation_report.txt"
    lines = [
        f"v0.8 验证报告 — 共 {len(all_issues)} 项问题",
        "=" * 60,
        *all_issues,
    ]
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("\n".join(lines[:80]))
    if len(all_issues) > 80:
        print(f"... 另有 {len(all_issues) - 80} 项，见 {out}")
    print(f"\n完整报告: {out}")
    return 0 if not all_issues else 1


if __name__ == "__main__":
    raise SystemExit(main())
